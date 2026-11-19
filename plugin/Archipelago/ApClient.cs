using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Archipelago.MultiClient.Net;
using global::Archipelago.MultiClient.Net.Enums;
using global::Archipelago.MultiClient.Net.Helpers;
using global::Archipelago.MultiClient.Net.MessageLog.Messages;
using global::Archipelago.MultiClient.Net.Models;
using HomuraHimeAP.Core;
using HomuraHimeAP.Items;
using HomuraHimeAP.UI;

namespace HomuraHimeAP.Archipelago;

internal enum ConnectionStatus { Disconnected, Connecting, Connected, Reconnecting, Failed }

/// <summary>What a login attempt produced (made off the game thread, handed back to it).</summary>
internal sealed class LoginOutcome
{
    internal ArchipelagoSession Session;
    internal SlotData Slot;
    internal string SlotDataJson;
    internal string Seed;
    internal string Error;

    /// <summary>The server refused the slot or password: trying again cannot help.</summary>
    internal bool Rejected;

    internal bool Succeeded => Session != null;
}

/// <summary>
/// Owns the Archipelago session. Network work runs on the thread pool and comes back through <see cref="MainThread"/>; the
/// session state (checked locations, received index, ...) outlives a dropped connection so the game keeps working while the
/// plugin reconnects, and a save that was bound to a slot can be played without any connection (see <see cref="SaveBinding"/>).
/// </summary>
internal static class ApClient
{
    internal const string GameName = "Homura-Hime";

    /// <summary>A login that has not finished after this long is given up (the library's own timeout can be much longer).</summary>
    private const int ConnectTimeoutSeconds = 30;

    internal static ConnectionStatus Status = ConnectionStatus.Disconnected;
    internal static string StatusText = "Not connected";
    internal static ArchipelagoSession Session;
    internal static SlotData Slot = new();

    /// <summary>The session state the game is played with; null when the game is not a randomizer game.</summary>
    internal static SessionState State;

    /// <summary>Why the loaded save is not played as a randomizer (it belongs to another slot or seed); null otherwise.</summary>
    internal static string Refusal;

    private static string _address, _slotName, _password;

    /// <summary>The slot and seed of the connection, kept while reconnecting; null without a connection.</summary>
    private static string _connectedSlot, _connectedSeed;

    /// <summary>The slot and seed of the live connection; null while not connected (the menu checks saves against them).</summary>
    internal static string ConnectedSlot => IsConnected ? _connectedSlot : null;
    internal static string ConnectedSeed => IsConnected ? _connectedSeed : null;

    private static readonly Dictionary<string, SessionState> Sessions = new();
    private static BindingDecision? _decision;
    private static int? _boundSaveSlot;
    private static SessionState _appliedState; // whose progress was last switched to that of the played save ...
    private static int? _appliedSaveSlot;      // ... and which save that was: connecting again must not roll the live progress back to the save file

    /// <summary>
    /// Counts manual connects and disconnects. A network task started under an older generation finds out it was superseded
    /// and drops its result.
    /// </summary>
    internal static int Generation { get; private set; }

    private static readonly HashSet<string> ReceivedDefinitions = new();
    private static ArchipelagoSession _receivedSession;
    private static int _receivedCount;

    /// <summary>
    /// Whether the multiworld has sent the player this item (by its game definition): the "AP inventory", as opposed to the
    /// game's. A door can ask this every frame, so the definitions are kept in a set that only takes in the items that arrived
    /// since the last call. Nothing here waits for the server: the list is the library's own cached copy and only the item ids
    /// are read (names are the part that can block, see SourceLintTests).
    /// </summary>
    internal static bool HasReceived(string definition)
    {
        var session = Session;
        var items = session?.Items.AllItemsReceived;
        if (items == null) return false;

        // a new session, or the server sent the whole list again (the library clears and rebuilds it): start over
        if (!ReferenceEquals(session, _receivedSession) || items.Count < _receivedCount)
        {
            (_receivedSession, _receivedCount) = (session, 0);
            ReceivedDefinitions.Clear();
        }

        for (; _receivedCount < items.Count; _receivedCount++)
            if (Catalog.Item(items[_receivedCount].ItemId)?.Game is { } game) ReceivedDefinitions.Add(game);

        return ReceivedDefinitions.Contains(definition);
    }

    /// <summary>True while a live session exists (items can be received, checks sent now).</summary>
    internal static bool IsConnected => Status == ConnectionStatus.Connected && Session != null;

    /// <summary>
    /// True while the randomizer is in charge of the game: the played save is bound to a slot, whether the connection is up,
    /// reconnecting or absent. Hooks use this, so a dropped connection never hands the player the vanilla rewards.
    /// </summary>
    internal static bool Active => State != null;

    /// <summary>True when items can be received now: connected, and the played save belongs to the connected slot.</summary>
    internal static bool CanReceive => IsConnected && State != null;

    /// <summary>Forgets all connection and session state (tests, and a clean start after a failure).</summary>
    internal static void Reset()
    {
        Disconnect(rebind: false);
        Sessions.Clear();
        (_decision, _boundSaveSlot, Refusal) = (null, null, null);
        (_appliedState, _appliedSaveSlot) = (null, null);
        Status = ConnectionStatus.Disconnected;
        StatusText = "Not connected";
        Slot = new SlotData();
        SaveBinding.Reset();
    }

    // ------------------------------------------------------------------ connecting

    internal static void Connect(string address, string slotName, string password)
    {
        if (Status == ConnectionStatus.Connecting)
        {
            Plugin.Log.LogInfo("Connect ignored: a connection attempt is already running");
            return;
        }

        Plugin.Log.LogInfo($"Connecting to \"{address}\" as \"{slotName}\"");
        Disconnect(rebind: false);
        (_address, _slotName, _password) = (address, slotName, password);
        Status = ConnectionStatus.Connecting;
        StatusText = "Connecting...";

        var generation = Generation;
        Task.Delay(ConnectTimeoutSeconds * 1000).ContinueWith(_ => MainThread.Run(() =>
        {
            if (generation != Generation || Status != ConnectionStatus.Connecting) return;
            Generation++; // the late answer, if it ever comes, is discarded
            Fail($"No answer from the server after {ConnectTimeoutSeconds} s");
        }));
        Task.Run(() =>
        {
            var outcome = TryLogin();
            MainThread.Run(() =>
            {
                if (generation != Generation) { Discard(outcome); return; }
                if (outcome.Succeeded) OnConnected(outcome, reconnect: false);
                else Fail(outcome.Error);
            });
        });
    }

    /// <summary>One login attempt with the parameters of the last connect. Safe to call from any thread.</summary>
    internal static LoginOutcome TryLogin()
    {
        try
        {
            var address = ServerAddress.Normalize(_address, out var addressError);
            if (addressError != null) return new LoginOutcome { Error = addressError, Rejected = true };

            var session = ArchipelagoSessionFactory.CreateSession(address);
            var result = session.TryConnectAndLogin(GameName, _slotName, ItemsHandlingFlags.AllItems,
                new Version(0, 5, 0), new[] { "AP" }, null, string.IsNullOrEmpty(_password) ? null : _password, true);

            if (result is LoginFailure failure)
                return new LoginOutcome { Error = "Login failed: " + string.Join("; ", failure.Errors), Rejected = true };

            var success = (LoginSuccessful)result;
            var slot = SlotData.Parse(success.SlotData);
            if (slot.Problem != null)
            {
                try { session.Socket.DisconnectAsync(); } catch { /* the login is abandoned anyway */ }
                return new LoginOutcome { Error = slot.Problem, Rejected = true };
            }

            return new LoginOutcome
            {
                Session = session,
                Slot = slot,
                SlotDataJson = SlotData.ToJson(success.SlotData),
                Seed = session.RoomState.Seed,
            };
        }
        catch (Exception e)
        {
            return new LoginOutcome { Error = "Connection error: " + e.Message };
        }
    }

    private static void Discard(LoginOutcome outcome)
    {
        if (outcome.Succeeded) Task.Run(() => { try { outcome.Session.Socket.DisconnectAsync(); } catch { /* closed anyway */ } });
    }

    /// <summary>Manual disconnect (also cancels a running reconnect). A bound save carries on offline.</summary>
    internal static void Disconnect(bool rebind = true)
    {
        Generation++;
        var session = Session;
        Session = null;
        (_connectedSlot, _connectedSeed) = (null, null);
        DeathLinkHandler.Disconnect();
        Status = ConnectionStatus.Disconnected;
        SyncMessageListener();

        if (session != null)
        {
            Plugin.Log.LogInfo("Disconnected from Archipelago");
            session.Items.ItemReceived -= OnItemReceived;
            session.Locations.CheckedLocationsUpdated -= OnServerChecked;
            Task.Run(() => { try { session.Socket.DisconnectAsync(); } catch { /* already closed */ } });
        }

        _decision = null;
        if (rebind) Rebind();
        else SetState(null);
        SessionState.FlushAll();
        UpdateStatusText();
    }

    private static void OnConnected(LoginOutcome outcome, bool reconnect)
    {
        var session = outcome.Session;
        Session = session;
        Slot = outcome.Slot;
        (_connectedSlot, _connectedSeed) = (_slotName, outcome.Seed);
        Status = ConnectionStatus.Connected;

        // What the connection tells us is stored with the slot, so a bound save can be played without a connection later.
        var stored = StateFor(_connectedSlot, _connectedSeed);
        stored.SlotDataJson = outcome.SlotDataJson;
        stored.LocationIds = session.Locations.AllLocations.ToList();
        stored.Save();

        ConnectionMonitor.Watch(session);
        DeathLinkHandler.Connect(session, _slotName, Slot);
        ScoutCache.Reload(session, entries => { stored.Scouted = entries; stored.Save(); });
        session.Items.ItemReceived += OnItemReceived;
        session.Locations.CheckedLocationsUpdated += OnServerChecked;
        SyncMessageListener();

        Plugin.Log.LogInfo($"{(reconnect ? "Reconnected" : "Connected")} to Archipelago as {_slotName} (seed {outcome.Seed})");
        if (!reconnect) foreach (var line in Slot.Describe()) Plugin.Log.LogInfo("  " + line);
        foreach (var warning in Slot.Warnings) Plugin.Log.LogWarning("Slot data: " + warning);
        Rebind(reconcile: false);
        if (State != null) Reconcile(session);
        GoalReporter.Resend();
        UpdateStatusText();

        Toasts.Show(reconnect ? "Connection to Archipelago restored" : $"Connected to Archipelago as {_slotName}", Toasts.Info);
    }

    private static void Fail(string message)
    {
        Status = ConnectionStatus.Failed;
        StatusText = message;
        Plugin.Log.LogWarning(message);
        Toasts.Show(message, Toasts.Error);
    }

    // ------------------------------------------------------------------ the game save and the slot

    /// <summary>
    /// Decides how the played save relates to the connection (see <see cref="SaveBinding.Decide"/>) and switches the session
    /// state accordingly. Called when a save is loaded or started, and when the connection changes. Repeating it with nothing
    /// changed does nothing, so a reconnect never resets a playthrough. <paramref name="reload"/> is true when the game just
    /// loaded or started a save: its stored progress then replaces the live one.
    /// </summary>
    internal static void Rebind(bool reload = false, bool reconcile = true)
    {
        var entry = SaveBinding.CurrentEntry;
        var decision = SaveBinding.AtTitle ? BindingDecision.Vanilla : SaveBinding.Decide(_connectedSlot, _connectedSeed, entry);
        var saveSlot = SaveBinding.CurrentSaveSlot;
        if (decision == _decision && saveSlot == _boundSaveSlot) { UpdateStatusText(); return; }
        (_decision, _boundSaveSlot) = (decision, saveSlot);

        var wasRefused = Refusal != null;
        var previous = State;
        Refusal = null;
        switch (decision)
        {
            case BindingDecision.Continue:
            case BindingDecision.Offline:
                SetState(StateFor(entry.Slot, entry.Seed));
                if (ApplyFromSave(reload, saveSlot)) State.ApplyPlaythrough(entry.Progress);
                TutorialTracker.SaveApplied();
                if (decision == BindingDecision.Offline) UseStoredSlotData();
                Plugin.Log.LogInfo($"Save {saveSlot}: {decision}, {State.ReceivedIndex} items already granted");
                break;

            case BindingDecision.Adopt:
                SetState(StateFor(_connectedSlot, _connectedSeed));
                if (ApplyFromSave(reload, saveSlot)) State.ApplyPlaythrough(new Playthrough { TutorialDone = GameApi.HubProgress > 0 });
                Plugin.Log.LogInfo(saveSlot.HasValue ? $"Save {saveSlot} has no slot yet, it now belongs to {_connectedSlot}" : $"New game for {_connectedSlot}");
                break;

            case BindingDecision.Refuse:
                SetState(null);
                Refusal = $"This save belongs to {entry.Slot} (seed {Short(entry.Seed)}), not to {_connectedSlot} (seed {Short(_connectedSeed)})";
                Plugin.Log.LogWarning(Refusal + "; the randomizer is off for this save");
                Toasts.Show(Refusal, Toasts.Error);
                break;

            default:
                SetState(null);
                break;
        }

        if (wasRefused && Refusal == null) Plugin.Log.LogInfo("The loaded save can be played as a randomizer game again");
        if (reconcile && State != null && State != previous && Session != null) Reconcile(Session);
        UpdateStatusText();
    }

    /// <summary>
    /// Whether the progress stored with the played save replaces the live one: when the game loaded a save, or the played save or the
    /// slot changed. Not when only the connection did: a (re)connect while playing keeps what happened since the save was written.
    /// </summary>
    private static bool ApplyFromSave(bool reload, int? saveSlot)
    {
        var apply = SaveBinding.ShouldApplyPlaythrough(reload, State, _appliedState, saveSlot, _appliedSaveSlot);
        (_appliedState, _appliedSaveSlot) = (State, saveSlot);
        return apply;
    }

    private static string Short(string seed) => seed != null && seed.Length > 8 ? seed.Substring(0, 8) : seed;

    /// <summary>The session of a slot and seed, loaded from its file once and then shared.</summary>
    private static SessionState StateFor(string slot, string seed)
    {
        var key = slot + "|" + seed;
        if (!Sessions.TryGetValue(key, out var state)) Sessions[key] = state = SessionState.Load(slot, seed);
        return state;
    }

    private static void SetState(SessionState state)
    {
        if (State != null && State != state) State.Flush();
        State = state;

        if (state == null)
        {
            Catalog.SetSlotLocations(null);
            ScoutCache.Clear();
        }
        else if (Session != null)
        {
            Catalog.SetSlotLocations(Session.Locations.AllLocations);
            // refusing another save emptied the table, and the scout of the connection is long done by then: the slot's
            // stored copy fills it again (a scout that is still running replaces it when it finishes)
            if (!ScoutCache.HasEntries) ScoutCache.Restore(state.Scouted);
        }
        else
        {
            Catalog.SetSlotLocations(state.LocationIds);
            ScoutCache.Restore(state.Scouted);
        }
    }

    /// <summary>Offline play uses what the last connection stored for the slot.</summary>
    private static void UseStoredSlotData()
    {
        Slot = SlotData.FromJson(State.SlotDataJson);
        if (State.LocationIds == null) Plugin.Log.LogWarning("The stored slot data has no locations; connect once to refresh it");
    }

    private static void UpdateStatusText()
    {
        switch (Status)
        {
            case ConnectionStatus.Connected:
                StatusText = Refusal ?? $"Connected as {_connectedSlot}";
                break;
            case ConnectionStatus.Reconnecting:
                StatusText = "Connection lost, reconnecting...";
                break;
            case ConnectionStatus.Disconnected:
                StatusText = Refusal ?? (State != null ? $"Offline: playing {State.Slot}" : "Not connected");
                break;
        }
    }

    // ------------------------------------------------------------------ connection loss

    /// <summary>The socket of a session closed. Only the current session matters; older ones are being replaced on purpose.</summary>
    internal static void OnSocketClosed(ArchipelagoSession session)
    {
        if (session != Session || Status != ConnectionStatus.Connected) return;

        Session = null;
        DeathLinkHandler.Disconnect();
        Status = ConnectionStatus.Reconnecting;
        UpdateStatusText();
        Plugin.Log.LogWarning("Connection to Archipelago lost, reconnecting");
        Toasts.Show("Connection to Archipelago lost, reconnecting", Toasts.Error);
        ConnectionMonitor.Reconnect(Generation);
    }

    /// <summary>The result of a reconnect attempt, back on the game thread.</summary>
    internal static void OnReconnectAttempt(LoginOutcome outcome, int generation)
    {
        if (generation != Generation) { Discard(outcome); return; }
        if (outcome.Succeeded) OnConnected(outcome, reconnect: true);
        else if (outcome.Rejected) Fail(outcome.Error);
        else Plugin.Log.LogInfo("Reconnect failed, trying again: " + outcome.Error);
    }

    // ------------------------------------------------------------------ checks

    /// <summary>Records a location check locally (always) and reports it to the server (when connected).</summary>
    internal static void CheckLocation(long id)
    {
        if (State == null) return;
        if (!State.CheckedLocations.Add(id)) return;
        State.Save();

        var session = Session;
        if (session != null) CheckSender.Send(session, id);

        var scouted = ScoutCache.Get(id);
        if (scouted != null) AnnounceFound(scouted);
        else if (session != null) ScoutAndAnnounce(session, id);
    }

    /// <summary>Brings the local and the server's record of checked locations together (after connecting).</summary>
    private static void Reconcile(ArchipelagoSession session)
    {
        Task.Run(() =>
        {
            try
            {
                var server = session.Locations.AllLocationsChecked.ToList();
                MainThread.Run(() => ApplyDifference(session, server, fullList: true));
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not read the checked locations from the server: " + e.Message);
            }
        });
    }

    /// <summary>The server reports locations checked by someone else (another client, `!collect`).</summary>
    private static void OnServerChecked(System.Collections.ObjectModel.ReadOnlyCollection<long> newlyChecked) =>
        MainThread.Run(() => ApplyDifference(Session, newlyChecked.ToList(), fullList: false));

    /// <summary>
    /// Adopts what the server has checked; sends what it lacks. Only a full list can tell what the server lacks, a report of
    /// newly checked locations says nothing about the rest.
    /// </summary>
    private static void ApplyDifference(ArchipelagoSession session, List<long> server, bool fullList)
    {
        if (State == null) return;
        var difference = Reconciler.Compare(State.CheckedLocations, server);
        if (difference.ToAdopt.Count > 0)
        {
            State.CheckedLocations.UnionWith(difference.ToAdopt);
            State.Save();
            Plugin.Log.LogInfo($"{difference.ToAdopt.Count} location(s) were already checked on the server");
        }

        if (fullList && session != null) CheckSender.Send(session, difference.ToSend);
    }

    /// <summary>What the server says (command results, chat, hints) goes to the connection form; the sending of items between other players does not.</summary>
    internal static readonly ConsoleLog ServerLog = new();

    private static bool _wantMessages;
    private static ArchipelagoSession _logSession;

    /// <summary>
    /// The client library only reads the messages of the server (and looks the items and players in them up, which can mean fetching
    /// the data of the other games) while something listens to them. In a large multiworld thousands of them go by, so the form
    /// listens only while it is open and the game does nothing for them otherwise.
    /// </summary>
    internal static void ListenToMessages(bool wanted)
    {
        _wantMessages = wanted;
        SyncMessageListener();
    }

    private static void SyncMessageListener()
    {
        var target = _wantMessages ? Session : null;
        if (_logSession == target) return;

        if (_logSession != null) _logSession.MessageLog.OnMessageReceived -= OnLogMessage;
        _logSession = target;
        if (_logSession != null) _logSession.MessageLog.OnMessageReceived += OnLogMessage;
    }

    private static void OnLogMessage(LogMessage message)
    {
        if (message is ItemSendLogMessage item && !item.IsRelatedToActivePlayer) return;
        if (message is TagsChangedLogMessage) return;

        var text = message.ToString();
        MainThread.Run(() => ServerLog.Add(text));
    }

    /// <summary>Sends a chat message, or a command that starts with "!", to the server as the connected slot. False when there is no connection.</summary>
    internal static bool Say(string text)
    {
        var session = Session;
        if (!IsConnected || session == null || string.IsNullOrWhiteSpace(text)) return false;

        // the client library must not be called from the game thread
        Task.Run(() =>
        {
            try { session.Say(text); }
            catch (Exception e) { Plugin.Log.LogWarning("Could not send the message: " + e.Message); }
        });
        return true;
    }

    private static void OnItemReceived(ReceivedItemsHelper helper)
    {
        // Granting happens in ItemGranter.Tick on the game thread; nothing to do beyond waking it.
        MainThread.Run(ItemGranter.Wake);
    }

    // ------------------------------------------------------------------ goal and announcements

    /// <summary>The game has been beaten: remembered in the session, then reported (now, or on the next connection).</summary>
    internal static void CompleteGoal()
    {
        if (State == null || State.GoalReached) return; // the choice at the end and the credits both report it
        Plugin.Log.LogInfo("Goal reached");
        Toasts.Show("Goal complete!", Toasts.Progression);
        GoalReporter.Reached();
    }

    /// <summary>Announces what a location check contained, right when it is found (before the item is granted).</summary>
    private static void AnnounceFound(ScoutedItem item)
    {
        // another player's item is shown with the Archipelago logo, coloured by how important the item is
        var text = item.IsOwn ? ItemMessages.Found(item.Name) : ItemMessages.Sent(item.Name, item.PlayerName);
        Toasts.ShowItem(text, item.Flags, !item.IsOwn, item.IsOwn ? ItemIcons.For(item.Record) : Assets.Logo);
    }

    /// <summary>Fallback for a check made before the scout of this connection has finished.</summary>
    private static void ScoutAndAnnounce(ArchipelagoSession session, long id)
    {
        Task.Run(async () =>
        {
            try
            {
                var scouted = await session.Locations.ScoutLocationsAsync(HintCreationPolicy.None, id);
                if (!scouted.TryGetValue(id, out var info)) return;
                var own = info.Player.Slot == session.ConnectionInfo.Slot;
                var text = own ? ItemMessages.Found(info.ItemName) : ItemMessages.Sent(info.ItemName, info.Player.Name);
                var flags = own && Catalog.Item(info.ItemId) is { } known ? known.OwnFlags : info.Flags;
                MainThread.Run(() => Toasts.ShowItem(text, flags, !own, own ? ItemIcons.For(Catalog.Item(info.ItemId)) : Assets.Logo));
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not scout location " + id + ": " + e.Message);
            }
        });
    }
}

