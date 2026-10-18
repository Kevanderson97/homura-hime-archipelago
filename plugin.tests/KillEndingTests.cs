using HomuraHimeAP.Patches;
using Xunit;

namespace HomuraHimeAP.Tests;

public class KillEndingTests
{
    [Theory]
    [InlineData("Kill", "EndingInteracterFSM", "WaitForChoise", true)]     // Kill chosen
    [InlineData("NotKill", "EndingInteracterFSM", "WaitForChoise", false)] // Ann is spared
    [InlineData("Kill", "EndingInteracterFSM", "HH Trigger anim", false)]  // a repeat once the kill is under way
    [InlineData("Kill", "SomeOtherFSM", "WaitForChoise", false)]           // another FSM using the same event name
    public void OnlyKillWhileTheChoiceIsWaitingCompletesTheGoal(string evt, string fsmObject, string state, bool kills)
    {
        Assert.Equal(kills, KillEnding.IsKill(evt, fsmObject, state));
    }
}
