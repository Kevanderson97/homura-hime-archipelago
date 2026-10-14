using System;
using System.Threading.Tasks;
using HomuraHimeAP.Core;

namespace HomuraHimeAP.Archipelago;

/// <summary>
/// Reports that the game was beaten. The fact is stored in the session first, so a dropped connection or a game closed
/// right afterwards does not lose it; it is sent again on every connection until the server has it. Sending happens on the
/// thread pool because the client library must not be called from the game thread.
/// </summary>
internal static class GoalReporter
{
    /// <summary>The goal was reached just now.</summary>
    internal static void Reached()
    {
        var state = ApClient.State;
        if (state == null) return;
        state.GoalReached = true;
        state.Save();
        Resend();
    }

    /// <summary>Sends the goal if it is reached and a connection exists (called on every successful connection).</summary>
    internal static void Resend()
    {
        var session = ApClient.Session;
        if (session == null || ApClient.State is not { GoalReached: true }) return;

        Task.Run(() =>
        {
            try
            {
                session.SetGoalAchieved();
                Plugin.Log.LogInfo("Goal reported to the server");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not report the goal, it is sent again on the next connection: " + e.Message);
            }
        });
    }
}
