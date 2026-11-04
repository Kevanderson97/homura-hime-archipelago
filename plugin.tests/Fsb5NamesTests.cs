using System.IO;
using System.Text;
using HomuraHimeAP.Core;
using Xunit;

namespace HomuraHimeAP.Tests;

public class Fsb5NamesTests
{
    /// <summary>A container with the given names: junk before it, the header, empty sample headers, then the name table.</summary>
    private static byte[] Container(int version, string[] names, int junkBefore = 17)
    {
        var table = new MemoryStream();
        var offsets = new int[names.Length];
        var strings = new MemoryStream();
        for (var i = 0; i < names.Length; i++)
        {
            offsets[i] = names.Length * 4 + (int)strings.Length;
            var bytes = Encoding.UTF8.GetBytes(names[i]);
            strings.Write(bytes, 0, bytes.Length);
            strings.WriteByte(0);
        }

        using (var writer = new BinaryWriter(table, Encoding.UTF8, leaveOpen: true))
        {
            foreach (var offset in offsets) writer.Write(offset);
            writer.Write(strings.ToArray());
        }

        var sampleHeaders = new byte[names.Length * 8];
        var file = new MemoryStream();
        using var w = new BinaryWriter(file);
        w.Write(new byte[junkBefore]);
        w.Write(Encoding.ASCII.GetBytes("FSB5"));
        w.Write(version);
        w.Write(names.Length);
        w.Write(sampleHeaders.Length);
        w.Write((int)table.Length);
        w.Write(0); // data size
        w.Write(new byte[(version == 0 ? 64 : 60) - 24]); // the rest of the header
        w.Write(sampleHeaders);
        w.Write(table.ToArray());
        return file.ToArray();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    public void ReadsEveryNameInSampleOrder(int version)
    {
        var names = new[] { "Control Flames SND73616", "audiostock_226672", "SND122929", "x" };
        var read = Fsb5Names.Read(new MemoryStream(Container(version, names)), 17);
        Assert.Equal(names, read);
    }

    [Fact]
    public void FindsTheFirstSampleThatContainsTheFragment()
    {
        var names = new[] { "a_laser", "Control Flames SND73616", "Control Flames SND73616 copy" };
        Assert.Equal(1, Fsb5Names.IndexOf(names, "Control Flames SND73616"));
        Assert.Equal(-1, Fsb5Names.IndexOf(names, "missing"));
    }

    [Fact]
    public void AMangledNameStillMatchesOnItsReadablePart()
    {
        var mangled = Encoding.GetEncoding("latin1").GetString(new byte[] { 0xE9, 0xFF }) + " SND5 " + "Ã©";
        var read = Fsb5Names.Read(new MemoryStream(Container(1, new[] { mangled })), 17);
        Assert.Equal(0, Fsb5Names.IndexOf(read, "SND5"));
    }

    [Fact]
    public void SomethingThatIsNotAContainerGivesNull()
    {
        Assert.Null(Fsb5Names.Read(new MemoryStream(new byte[200]), 0));
        Assert.Null(Fsb5Names.Read(new MemoryStream(Encoding.ASCII.GetBytes("FSB5")), 0)); // cut short
    }

    [Fact]
    public void AContainerWithoutANameTableGivesNull()
    {
        var bytes = Container(1, new[] { "a" });
        bytes[17 + 16] = 0; // name table size 0
        bytes[17 + 17] = 0;
        bytes[17 + 18] = 0;
        bytes[17 + 19] = 0;
        Assert.Null(Fsb5Names.Read(new MemoryStream(bytes), 17));
    }
}
