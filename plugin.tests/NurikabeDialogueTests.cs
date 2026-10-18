using HomuraHimeAP.Patches;
using Xunit;

namespace HomuraHimeAP.Tests;

public class NurikabeDialogueTests
{
    private const string Label = "Level4_C1A3_Dialogue_NurikabeMission";

    [Theory]
    [InlineData(Label, 13, false, false)]   // the Nurikabe's "won't budge" line is still played
    [InlineData(Label, 14, false, true)]    // Anne counting her fish and the choice are not
    [InlineData(Label, 21, false, true)]
    [InlineData(Label, 14, true, false)]    // with the fish the whole dialogue plays
    [InlineData("Level4_C1A3_Other", 14, false, false)] // any other dialogue is untouched
    public void TheDialogueEndsAfterTheRefusalWhenThereIsNoFish(string label, int index, bool hasFish, bool cut)
    {
        Assert.Equal(cut, NurikabeDialogue.Cuts(label, index, hasFish));
    }
}
