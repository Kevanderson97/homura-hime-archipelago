using HomuraHimeAP.Archipelago;
using Xunit;

namespace HomuraHimeAP.Tests;

public class ServerAddressTests
{
    [Theory]
    [InlineData("localhost", "ws://localhost:38281")]
    [InlineData("localhost:1234", "ws://localhost:1234")]
    [InlineData(" 192.168.1.5:38282 ", "ws://192.168.1.5:38282")]
    [InlineData("mypc", "ws://mypc:38281")]
    [InlineData("archipelago.gg:38281", "archipelago.gg:38281")]
    [InlineData("archipelago.gg", "archipelago.gg:38281")]
    [InlineData("wss://example.com:443", "wss://example.com:443")]
    public void ValidAddressesAreNormalized(string input, string expected)
    {
        Assert.Equal(expected, ServerAddress.Normalize(input, out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("host:abc")]
    [InlineData("host:0")]
    [InlineData("host:70000")]
    [InlineData(":38281")]
    public void InvalidAddressesGiveAReadableError(string input)
    {
        Assert.Null(ServerAddress.Normalize(input, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }
}
