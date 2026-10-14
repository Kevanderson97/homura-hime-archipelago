using System;
using global::Archipelago.MultiClient.Net.BounceFeatures.DeathLink;

namespace HomuraHimeAP.Archipelago;

/// <summary>What the DeathLink handler should do after a state change.</summary>
internal enum DeathLinkAction
{
    None,

    /// <summary>Kill the player now (a received death, the game is in a safe state).</summary>
    Kill,

    /// <summary>Send this player's death to the others.</summary>
    Send,
}

/// <summary>
/// The decisions of DeathLink, free of the game and the network so they can be tested. A received death waits for a safe moment
/// (<see cref="ReceiveWindow"/> at most); the death it causes is recognised and not sent back, but only for
/// <see cref="EchoWindow"/> seconds, so a kill that did not kill (invulnerability, a boss phase) cannot swallow the next real
/// death. The player's own death is sent when they die, or, with "skip revives", only when they give up instead of reviving.
/// </summary>
internal sealed class DeathLinkMachine
{
    /// <summary>A received death that has not found a safe moment within this long is dropped.</summary>
    internal const float ReceiveWindow = 10f;

    /// <summary>How long after our kill a death is taken for its result.</summary>
    internal const float EchoWindow = 5f;

    internal enum Phase { Idle, Pending, Applying, Dying }

    internal Phase Current { get; private set; } = Phase.Idle;
    internal DeathLink Received { get; private set; }

    /// <summary>Reviving does not send a death; it is only sent when the player picks another option in the death menu.</summary>
    internal bool SkipRevives;

    private float _since;
    private bool _deathSent;

    /// <summary>Called for logging each decision; tests ignore it.</summary>
    internal Action<string> Log = _ => { };

    internal void Receive(DeathLink death, float now)
    {
        if (Current == Phase.Applying || Current == Phase.Dying) { Log("received death ignored: the last one is still taking effect"); return; }
        Received = death;
        Current = Phase.Pending;
        _since = now;
    }

    /// <summary>Called every frame. <paramref name="canDie"/>: the player is alive and has control.</summary>
    internal DeathLinkAction Tick(float now, bool canDie)
    {
        switch (Current)
        {
            case Phase.Pending when now - _since > ReceiveWindow:
                Log("received death dropped: no safe moment within " + ReceiveWindow + " s");
                Reset();
                return DeathLinkAction.None;

            case Phase.Pending when canDie:
                Current = Phase.Applying;
                _since = now;
                return DeathLinkAction.Kill;

            case Phase.Dying when now - _since > EchoWindow:
                Log("the received death had no effect, ready for the next one");
                Reset();
                return DeathLinkAction.None;

            default:
                return DeathLinkAction.None;
        }
    }

    /// <summary>The kill that <see cref="Tick"/> asked for was attempted.</summary>
    internal void Killed(bool succeeded, float now)
    {
        if (succeeded) (Current, _since) = (Phase.Dying, now);
        else Reset();
    }

    /// <summary>The player died and the game shows the death menu.</summary>
    internal DeathLinkAction PlayerDied()
    {
        _deathSent = false;
        if (Current == Phase.Dying)
        {
            Log("this death was caused by a received DeathLink, not sent back");
            Current = Phase.Idle;
            _deathSent = true;
            return DeathLinkAction.None;
        }

        return SkipRevives ? DeathLinkAction.None : MarkSent();
    }

    /// <summary>The player chose to give up after dying (continue from the last checkpoint or go back to the menu).</summary>
    internal DeathLinkAction PlayerGaveUp() => SkipRevives ? MarkSent() : DeathLinkAction.None;

    internal void Reset()
    {
        Current = Phase.Idle;
        Received = null;
    }

    private DeathLinkAction MarkSent()
    {
        if (_deathSent) return DeathLinkAction.None;
        _deathSent = true;
        return DeathLinkAction.Send;
    }
}
