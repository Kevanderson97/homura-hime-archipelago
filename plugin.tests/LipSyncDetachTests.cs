using HomuraHimeAP.Patches;
using Xunit;

namespace HomuraHimeAP.Tests;

public class LipSyncDetachTests
{
    [Theory]
    [InlineData(true, true, true, true)]     // the event still plays on the group the DSP was added to
    [InlineData(false, false, false, false)] // the event is gone: the group is released
    [InlineData(true, false, false, false)]  // the event lost its group
    [InlineData(true, true, false, false)]   // the event restarted on another group
    public void TheDspIsRemovedOnlyFromAGroupTheEventStillHas(bool eventValid, bool hasGroup, bool same, bool remove)
    {
        Assert.Equal(remove, LipSyncDetachPatch.GroupIsAlive(eventValid, hasGroup, same));
    }
}
