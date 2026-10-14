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
    private static bool _opened, _failed;
    private static readonly Dictionary<string, Sound?> Samples = new();

    /// <summary>
    /// Plays <paramref name="length"/> milliseconds from <paramref name="start"/> of the sample whose name contains
    /// <paramref name="nameFragment"/> (the whole sample when length is 0), on the game's SFX bus.
    /// </summary>
    internal static void Play(string nameFragment, uint start = 0, uint length = 0)
    {
        try
        {
            if (!Open() || FindSample(nameFragment) is not { } sample) return;

            var system = RuntimeManager.CoreSystem;
            if (system.playSound(sample, SfxGroup(system), true, out var channel) != RESULT.OK) return;
            if (start > 0) channel.setPosition(start, TIMEUNIT.MS);
            if (length > 0)
            {
                system.getSoftwareFormat(out var rate, out _, out _);
                channel.getDSPClock(out var clock, out var parent);
                var end = clock + (ulong)(rate * (length / 1000.0));
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

    private static bool Open()
    {
        if (_opened) return true;
        if (_failed) return false;

        try
        {
            var path = Path.Combine(Application.streamingAssetsPath, BankFile);
            var offset = FindContainer(path);
            var info = new CREATESOUNDEXINFO { cbsize = System.Runtime.InteropServices.Marshal.SizeOf<CREATESOUNDEXINFO>(), fileoffset = (uint)offset, length = (uint)(new FileInfo(path).Length - offset) };
            var result = RuntimeManager.CoreSystem.createSound(path, MODE.OPENONLY | MODE.LOOP_OFF, ref info, out _container);
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
