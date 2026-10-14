using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using UnityEngine;

namespace HomuraHimeAP.GameTests;

/// <summary>
/// A second player of the test seed, an "AP Quest" slot (a small game that lives entirely inside its world), connected from
/// inside the test process. It is kept apart from the test classes on purpose: Surity inspects the test classes when the game
/// starts, and a field of a MultiClient type there would make it load that assembly too early.
/// </summary>
internal sealed class OtherPlayer
{
    internal const string SlotName = "Quester";
    private const float Timeout = 20f;

    private readonly ArchipelagoSession _session;

    private OtherPlayer(ArchipelagoSession session) => _session = session;

    internal static IEnumerator Connect(Action<OtherPlayer> done) => Connect("APQuest", SlotName, done);

    /// <summary>Connects as any slot of the test seed (also as a second connection of our own slot).</summary>
    internal static IEnumerator Connect(string game, string slot, Action<OtherPlayer> done)
    {
        Harness.Note("other player: connecting");
        var session = ArchipelagoSessionFactory.CreateSession("ws://" + Harness.Server);
        var login = Task.Run(() => session.TryConnectAndLogin(game, slot, ItemsHandlingFlags.AllItems, new Version(0, 6, 0), new[] { "AP" }));
        while (!login.IsCompleted) yield return null;
        if (!login.Result.Successful)
            throw new Exception(game + " slot " + slot + " could not log in: " + string.Join("; ", ((LoginFailure)login.Result).Errors));
        Harness.Note("other player: logged in");
        done(new OtherPlayer(session));
    }

    /// <summary>
    /// How many items with this id this player has received from the given sender. Ids, not names: a name is resolved from the
    /// data package, which may wait for the server and must not happen on the game thread.
    /// </summary>
    internal int ReceivedFrom(string sender, long itemId) =>
        _session.Items.AllItemsReceived.Count(i => i.ItemId == itemId && i.Player.Name == sender);

    /// <summary>
    /// Finds every location of this player (the items in them go to their owners). The asynchronous call is required: the
    /// synchronous one waits for the socket from the main thread, which the socket needs to finish, and deadlocks.
    /// </summary>
    internal void CheckEverything() => _session.Locations.CompleteLocationChecksAsync(_session.Locations.AllLocations.ToArray());

    /// <summary>Checks one location of this player.</summary>
    internal void Check(long locationId) => _session.Locations.CompleteLocationChecksAsync(locationId);

    /// <summary>True when a hint about this location exists (read from the server's hint storage; call from a pool thread).</summary>
    internal bool HasHintAt(long locationId) => _session.DataStorage.GetHints().Any(h => h.LocationId == locationId);

    private DeathLinkService _deathLink;
    private int _deathsReceived;

    /// <summary>Joins DeathLink (this player then receives the deaths other slots send).</summary>
    internal void EnableDeathLink()
    {
        _deathLink = _session.CreateDeathLinkService();
        _deathLink.OnDeathLinkReceived += _ => System.Threading.Interlocked.Increment(ref _deathsReceived);
        _deathLink.EnableDeathLink();
    }

    internal int DeathsReceived => _deathsReceived;

    internal void SendDeath() => Task.Run(() => _deathLink.SendDeathLink(new DeathLink(SlotName, "test death")));

    internal void Disconnect() => _session.Socket.DisconnectAsync();

    /// <summary>
    /// Waits until the condition holds, polling it on a thread-pool thread. The Archipelago client library must not be touched
    /// from the game thread while waiting on the network (the other Archipelago mods follow the same rule), so tests that read
    /// the other player's session do it through this method.
    /// </summary>
    internal static IEnumerator WaitUntil(Func<bool> condition, string what)
    {
        Harness.Note("waiting for " + what);
        var poll = Task.Run(() =>
        {
            var deadline = DateTime.UtcNow.AddSeconds(Timeout);
            var lastNote = DateTime.UtcNow;
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return true;
                if ((DateTime.UtcNow - lastNote).TotalSeconds >= 2) { lastNote = DateTime.UtcNow; Harness.Note("  poll thread alive, condition still false"); }
                System.Threading.Thread.Sleep(50);
            }

            return false;
        });
        var lastFrameNote = Time.realtimeSinceStartup;
        while (!poll.IsCompleted)
        {
            if (Time.realtimeSinceStartup - lastFrameNote >= 2) { lastFrameNote = Time.realtimeSinceStartup; Harness.Note("  game thread alive"); }
            yield return null;
        }
        if (!poll.Result) throw new Exception("timed out waiting for " + what);
        Harness.Note("got " + what);
    }
}
