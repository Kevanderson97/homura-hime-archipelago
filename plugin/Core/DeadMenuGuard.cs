using System;

namespace HomuraHimeAP.Core;

/// <summary>
/// The death screen of the game is a timeline (<c>LowHP_FullSenceTimeline</c>) whose "Enter" part plays up to a pause time (8 s)
/// and stops there; the menu is switched on by an activation clip that ends at that moment. A frame that is long enough to
/// carry the timeline past the end of the clip switches the menu off, and the controller then puts the time back to the pause
/// time without evaluating the timeline again, so the menu stays off with its buttons gone. A long frame happens when the window
/// has been in the background (DeathLink kills while the game is not in focus). Evaluating the timeline once more brings the
/// menu back.
/// </summary>
internal static class DeadMenuGuard
{
    /// <summary>How far from the pause time the timeline may be and still count as parked there.</summary>
    internal const double Tolerance = 0.01;

    /// <summary>True when the death screen is shown, parked at the end of its "Enter" part, and its menu is off.</summary>
    internal static bool NeedsRestore(bool shown, bool menuActive, bool paused, double time, double enterPauseTime) =>
        shown && !menuActive && paused && Math.Abs(time - enterPauseTime) <= Tolerance;
}
