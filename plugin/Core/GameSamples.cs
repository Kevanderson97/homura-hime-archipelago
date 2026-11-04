using System;
using System.Collections.Generic;
using System.IO;
using FMOD;
using FMODUnity;
using UnityEngine;

namespace HomuraHimeAP.Core;

/// <summary>
/// Plays pieces of the game's own sound effects straight from its installed sound bank, so the plugin ships no audio. The
/// bank (<c>SFX.assets.bank</c>) holds one FSB5 container with the samples; it is opened once through the game's FMOD and a sample
/// is found by (part of) its name, so it survives a rebuilt bank. When anything is missing the plugin logs once and stays silent.
/// </summary>
internal static class GameSamples
{
    private const string BankFile = "SFX.assets.bank";
    private const int HeaderSize = 60; // FSB5 version 1

    private static Sound _container;
    private static string _bankPath;
    private static long _containerOffset;
    private static string[] _names; // every sample's name, from the container's header (null when it could not be read)
    private static bool _namesRead;
    private static bool _opened, _failed;
    private static readonly Dictionary<string, Sound?> Samples = new();

    /// <summary>
    /// Plays <paramref name="length"/> milliseconds from <paramref name="start"/> of the sample whose name contains
    /// <paramref name="nameFragment"/> (the whole sample when length is 0), on the game's SFX bus, at <paramref name="volume"/>
    /// (1 is the sample as it is stored, which is louder than the game's own sound events).
    /// </summary>
    internal static void Play(string nameFragment, uint start = 0, uint length = 0, float volume = 1f)
    {
        try
        {
            if (!Open() || FindSample(nameFragment) is not { } sample) return;

            var system = RuntimeManager.CoreSystem;
            if (system.playSound(sample, SfxGroup(system), true, out var channel) != RESULT.OK) return;
            channel.setVolume(volume);
            if (start > 0) channel.setPosition(start, TIMEUNIT.MS);
            if (length > 0)
            {
                system.getSoftwareFormat(out var rate, out _, out _);
                channel.getDSPClock(out _, out var parent); // delays and fades are set on the parent's clock; the channel's own starts at 0
                var end = parent + (ulong)(rate * (length / 1000.0));
                channel.setDelay(0, end, true);
                channel.addFadePoint(end - (ulong)(rate * 0.01), 1f); // a short fade so the cut does not click
                channel.addFadePoint(end, 0f);
            }

            channel.setPaused(false);
        }
        catch (Exception e)
        {
            Fail("playing " + nameFragment, e);
        }
    }

    /// <summary>Opens the bank and looks every named sample up now (see <see cref="Play"/> for the names).</summary>
    internal static void Preload(params string[] nameFragments)
    {
        try
        {
            if (!Open()) return;
            foreach (var fragment in nameFragments) FindSample(fragment);
        }
        catch (Exception e)
        {
            Fail("preparing the samples", e);
        }
    }

    private static bool Open()
    {
        if (_opened) return true;
        if (_failed) return false;

        try
        {
            var path = Path.Combine(Application.streamingAssetsPath, BankFile);
            var offset = FindContainer(path);
            (_bankPath, _containerOffset) = (path, offset);
            var info = new CREATESOUNDEXINFO { cbsize = System.Runtime.InteropServices.Marshal.SizeOf<CREATESOUNDEXINFO>(), fileoffset = (uint)offset, length = (uint)(new FileInfo(path).Length - offset) };
            // a stream: the samples are played straight from the file (OPENONLY only allows reading the data, it plays silence)
            var result = RuntimeManager.CoreSystem.createSound(path, MODE.CREATESTREAM | MODE.LOOP_OFF, ref info, out _container);
            if (result != RESULT.OK) throw new InvalidOperationException("FMOD: " + result);
            return _opened = true;
        }
        catch (Exception e)
        {
            _failed = true;
            Fail("opening the sound bank", e);
            return false;
        }
    }

    /// <summary>The position of the FSB5 container inside the bank file (it is searched for, never hard-coded).</summary>
    private static long FindContainer(string path)
    {
        using var stream = File.OpenRead(path);
        var buffer = new byte[1 << 20];
        var read = stream.Read(buffer, 0, buffer.Length);
        for (var i = 0; i + 4 <= read; i++)
            if (buffer[i] == 'F' && buffer[i + 1] == 'S' && buffer[i + 2] == 'B' && buffer[i + 3] == '5') return i;
        throw new InvalidDataException("no FSB5 container in " + BankFile);
    }

    private static Sound? FindSample(string nameFragment)
    {
        if (Samples.TryGetValue(nameFragment, out var cached)) return cached;

        // The index comes from the name table in the container's header, so only the wanted sample is asked of FMOD. (Asking
        // every sample for its name, one by one, took seconds and left every sample object it touched in memory.)
        if (!_namesRead)
        {
            _namesRead = true;
            _names = ReadNames();
        }

        if (_names != null)
        {
            var index = Fsb5Names.IndexOf(_names, nameFragment);
            if (index < 0)
            {
                Samples[nameFragment] = null;
                Plugin.Log.LogWarning("Sound sample not found in the bank: " + nameFragment);
                return null;
            }

            // FMOD's own name for the sample must agree, or the header was read wrongly (then the slow search below is used)
            if (_container.getSubSound(index, out var found) == RESULT.OK && found.getName(out var actual, 256) == RESULT.OK &&
                actual != null && actual.Contains(nameFragment))
            {
                Samples[nameFragment] = found;
                return found;
            }

            Plugin.Log.LogWarning($"The bank header and FMOD disagree about sample {index} ({nameFragment}); searching the bank the slow way");
            _names = null;
        }

        // the names could not be read: the slow way, asking every sample
        _container.getNumSubSounds(out var count);
        for (var i = 0; i < count; i++)
        {
            if (_container.getSubSound(i, out var sub) != RESULT.OK) continue;
            sub.getName(out var name, 256);
            if (name == null || !name.Contains(nameFragment)) continue;
            Samples[nameFragment] = sub;
            return sub;
        }

        Samples[nameFragment] = null;
        Plugin.Log.LogWarning("Sound sample not found in the bank: " + nameFragment);
        return null;
    }

    private static string[] ReadNames()
    {
        try
        {
            using var stream = new FileStream(_bankPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var names = Fsb5Names.Read(stream, _containerOffset);
            if (names == null) Plugin.Log.LogWarning("The sample names could not be read from the bank header; searching it the slow way");
            return names;
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("The sample names could not be read from the bank header: " + e.Message);
            return null;
        }
    }

    private static ChannelGroup SfxGroup(FMOD.System system)
    {
        try
        {
            if (RuntimeManager.GetBus("bus:/SFX").getChannelGroup(out var group) == RESULT.OK) return group;
        }
        catch (Exception)
        {
            // the bus is not loaded: the master group does as well
        }

        system.getMasterChannelGroup(out var master);
        return master;
    }

    private static void Fail(string what, Exception e) => Plugin.Log.LogWarning($"Game sound: {what} failed: {e.Message}");
}
