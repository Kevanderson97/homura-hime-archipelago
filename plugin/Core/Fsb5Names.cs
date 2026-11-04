using System;
using System.IO;
using System.Text;

namespace HomuraHimeAP.Core;

/// <summary>
/// Reads the sample names out of an FSB5 sound container without FMOD. Asking FMOD for a sample's name means creating the
/// sample object first, so finding one by name walked every sample of the game's sound bank (seconds, and every sample it
/// touched stayed in memory). The names are in a table in the container's header: a list of offsets, then the names. FMOD can
/// then be asked for the one sample by its index.
/// </summary>
internal static class Fsb5Names
{
    private const string Magic = "FSB5";

    /// <summary>The header is 60 bytes from version 1 on, 64 before.</summary>
    private static int HeaderSize(int version) => version == 0 ? 64 : 60;

    /// <summary>
    /// The name of every sample, in sample order (so a name's position is the sample's index), or null when the container has
    /// no name table or is not one this reader understands. <paramref name="start"/> is where the container begins in the stream.
    /// </summary>
    internal static string[] Read(Stream stream, long start)
    {
        try
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            stream.Position = start;
            if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != Magic) return null;

            var version = reader.ReadInt32();
            var count = reader.ReadInt32();
            var sampleHeadersSize = reader.ReadInt32();
            var nameTableSize = reader.ReadInt32();
            if (count <= 0 || nameTableSize <= count * 4 || sampleHeadersSize < 0) return null;

            stream.Position = start + HeaderSize(version) + sampleHeadersSize;
            var table = reader.ReadBytes(nameTableSize);
            if (table.Length < nameTableSize) return null;

            var names = new string[count];
            for (var i = 0; i < count; i++)
            {
                var offset = BitConverter.ToInt32(table, i * 4);
                if (offset < 0 || offset >= table.Length) return null;

                var end = Array.IndexOf(table, (byte)0, offset);
                if (end < 0) end = table.Length;
                // (some names are stored in a mangled encoding: the readable ASCII part is what is matched on)
                names[i] = Encoding.UTF8.GetString(table, offset, end - offset);
            }

            return names;
        }
        catch (Exception e) when (e is IOException or EndOfStreamException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>The index of the first sample whose name contains <paramref name="fragment"/>, or -1.</summary>
    internal static int IndexOf(string[] names, string fragment)
    {
        for (var i = 0; i < names.Length; i++)
            if (names[i].Contains(fragment, StringComparison.Ordinal)) return i;
        return -1;
    }
}
