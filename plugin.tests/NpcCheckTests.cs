using HomuraHimeAP.Patches;
using Xunit;

namespace HomuraHimeAP.Tests;

public class NpcCheckTests
{
    [Theory]
    [InlineData(true, 0, false, true)]    // nothing listed yet because the NPC is still loading: refused
    [InlineData(true, 1, true, false)]    // a real list that the player meets
    [InlineData(true, 0, true, false)]    // an NPC that is set up and asks for nothing keeps passing
    [InlineData(false, 0, false, false)]  // a failed check stays failed
    public void ACheckOnAnNpcThatIsNotSetUpIsRefused(bool passed, int listed, bool setUp, bool refused)
    {
        Assert.Equal(refused, NpcCheckPatch.PassedForNothing(passed, listed, setUp));
    }
}
