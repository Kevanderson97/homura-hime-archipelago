using System;
using System.Threading.Tasks;
using global::Archipelago.MultiClient.Net;
using global::Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using HomuraHimeAP.Core;
using HomuraHimeAP.Items;
using HomuraHimeAP.UI;
using UnityEngine;

namespace HomuraHimeAP.Archipelago;

/// <summary>
/// DeathLink: a death of this player is sent to everyone who has the tag, and a death received from someone else kills the
/// player once the game is in a safe state. The decisions live in <see cref="DeathLinkMachine"/>; this class connects them to
/// the network and the game.
/// </summary>
internal static class DeathLinkHandler
{
    /// <summary>A received death waits for a level that has been stable for this long.</summary>
    private const float SettleSeconds = 2f;

    private static DeathLinkService _service;
    private static string _slotName;
    private static readonly DeathLinkMachine Machine = new() { Log = message => Plugin.Log.LogInfo("DeathLink: " + message) };

    internal static bool Enabled => _service != null;

    /// <summary>A received death waits for a safe moment (for tests and the status display).</summary>
    internal static bool DeathWaiting => Machine.Current == DeathLinkMachine.Phase.Pending;

    internal static void Connect(ArchipelagoSession session, string slotName, SlotData slot)
    {
        Disconnect();
        if (!slot.DeathLink) return;

        _slotName = slotName;
        Machine.SkipRevives = slot.DeathLinkSkipRevives;
        _service = session.CreateDeathLinkService();
        _service.OnDeathLinkReceived += OnReceived;
        _service.EnableDeathLink();
    }

    internal static void Disconnect()
    {
        if (_service != null) _service.OnDeathLinkReceived -= OnReceived;
        _service = null;
        Machine.Reset();
    }

    // ------------------------------------------------------------------ receiving

    private static void OnReceived(DeathLink death) => MainThread.Run(() => Machine.Receive(death, Time.unscaledTime));

    /// <summary>Called every frame: applies a received death as soon as the player can safely die.</summary>
    internal static void Tick(float time)
    {
        var canDie = Machine.Current == DeathLinkMachine.Phase.Pending && ApClient.Active && SafeToDie(time);
        if (Machine.Tick(time, canDie) != DeathLinkAction.Kill) return;

        var death = Machine.Received;
        var killed = PlayerApi.Kill();
        Machine.Killed(killed, time);
        if (killed) Toasts.Show(string.IsNullOrWhiteSpace(death.Cause) ? $"{death.Source} died" : death.Cause, Toasts.Trap);
        else Plugin.Log.LogWarning("DeathLink: the player could not be killed");
    }

    /// <summary>In a level or the hub, alive, in control (no menu, dialogue or cutscene), and the scene has settled.</summary>
    private static bool SafeToDie(float time) =>
        PlayerApi.InGame && PlayerApi.IsPlayerAlive() && PlayerApi.HasControl() && SceneClock.Settled(time, SettleSeconds);

    // ------------------------------------------------------------------ sending

    /// <summary>The player has died and the game is showing the death menu.</summary>
    internal static void OnPlayerDied() => Act(Machine.PlayerDied());

    /// <summary>The player chose to give up after dying (continue from the last checkpoint or go back to the menu).</summary>
    internal static void OnPlayerGaveUp() => Act(Machine.PlayerGaveUp());

    private static void Act(DeathLinkAction action)
    {
        if (action != DeathLinkAction.Send || _service == null) return;

        // the client library must not be called from the game thread
        var service = _service;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        var death = new DeathLink(_slotName, $"{_slotName} died in {scene}");
        Task.Run(() =>
        {
            try { service.SendDeathLink(death); }
            catch (Exception e) { Plugin.Log.LogWarning("Could not send the DeathLink: " + e.Message); }
        });
    }
}
