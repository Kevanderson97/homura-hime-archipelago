using HomuraHimeAP.UI;
using Xunit;

namespace HomuraHimeAP.Tests;

public class ItemMessagesTests
{
    [Fact]
    public void AnItemFoundForAnotherPlayerSaysWhoGetsIt()
    {
        Assert.Equal("Power Bomb sent to Quester", ItemMessages.Sent("Power Bomb", "Quester"));
    }

    [Fact]
    public void AnItemReceivedFromAnotherPlayerSaysWhoSentIt()
    {
        Assert.Equal("Phantom Blade from Quester", ItemMessages.Received("Phantom Blade", "Quester"));
    }

    [Fact]
    public void OwnItemsKeepTheirShortWording()
    {
        Assert.Equal("Found 3000 Konpeito", ItemMessages.Found("3000 Konpeito"));
        Assert.Equal("Received Senbei", ItemMessages.ReceivedFromSelf("Senbei"));
    }
}
