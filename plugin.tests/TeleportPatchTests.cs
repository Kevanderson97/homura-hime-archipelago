using HomuraHimeAP.Patches;
using Xunit;

namespace HomuraHimeAP.Tests;

public class TeleportPatchTests
{
    [Theory]
    [InlineData("StartScreen", true)]            // Continue on the title screen
    [InlineData("HubWorld_Main", false)]         // a hub board
    [InlineData("Level_4_Area_2_Main", false)]   // a trigger in a level
    public void OnlyATeleportFromTheTitleScreenLoadsASave(string scene, bool loads)
    {
        Assert.Equal(loads, TeleportPatch.LoadsASave(scene));
    }
}
