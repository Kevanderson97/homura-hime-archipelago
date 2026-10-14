using System;
using System.Collections;
using System.IO;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HomuraHimeAP.GameTests;

/// <summary>Shared helpers: a connection to the test server, and scenes/objects that stand in for the game's.</summary>
internal static class Harness
{
    private const float ConnectTimeout = 25f;

    /// <summary>
    /// Surity cannot report a failing test in this game (serializing the error needs assemblies Unity's Mono does not have, and
    /// the run then hangs). Every test therefore runs inside <see cref="Guard"/>, which catches the failure and writes it to this
    /// file; tools/run_tests.ps1 reads it. Surity itself only ever sees passing tests.
    /// </summary>
    internal static readonly string FailureFile = Path.Combine(Path.GetTempPath(), "homura-hime-gametests-failures.txt");

    internal static readonly string ProgressFile = Path.Combine(Path.GetTempPath(), "homura-hime-gametests-progress.txt");

    /// <summary>Progress notes, written straight to a file (the game's own log is lost when a hung run is killed).</summary>
    internal static void Note(string message) => File.AppendAllText(ProgressFile, DateTime.Now.ToString("HH:mm:ss.fff") + " " + message + Environment.NewLine);

    /// <summary>
    /// Runs a test body (a coroutine), turning an exception into a recorded failure. Nested coroutines are run here too:
    /// Unity drops a whole coroutine chain without a word when a nested coroutine throws, which would leave the test hanging.
    /// </summary>
    internal static IEnumerator Guard(string test, IEnumerator body)
    {
        Note("start " + test);
        var stack = new System.Collections.Generic.Stack<IEnumerator>();
        stack.Push(body);

        while (stack.Count > 0)
        {
            object current;
            try
            {
                var top = stack.Peek();
                if (!top.MoveNext())
                {
                    stack.Pop();
                    continue;
                }

                current = top.Current;
                if (current is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }
            }
            catch (Exception e)
            {
                File.AppendAllText(FailureFile, test + ": " + e.Message + Environment.NewLine);
                Plugin.Log.LogError($"[test] FAILED {test}: {e}");
                yield break;
            }

            yield return current;
        }

        Note("done " + test);
    }
    /// <summary>The same for a test without waiting.</summary>
    internal static IEnumerator Guard(string test, Action body)
    {
        IEnumerator Run()
        {
            body();
            yield break;
        }

        return Guard(test, Run());
    }

    internal static string Server => Environment.GetEnvironmentVariable("HH_AP_SERVER") ?? "localhost:38281";
    internal static string Slot => Environment.GetEnvironmentVariable("HH_AP_SLOT") ?? "HimeTest";

    /// <summary>Connects to the test server (once) and waits until the slot's locations have been scouted.</summary>
    internal static IEnumerator Connect()
    {
        if (ApClient.IsConnected && ScoutCache.Get(Catalog.Location("chest/Level_1_Area_1_Main/1").Id) != null) yield break;

        ApClient.Connect(Server, Slot, "");
        var deadline = Time.realtimeSinceStartup + ConnectTimeout;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (ApClient.Status == ConnectionStatus.Failed) throw new Exception("connection failed: " + ApClient.StatusText);
            if (ApClient.IsConnected && ScoutCache.Get(Catalog.Location("chest/Level_1_Area_1_Main/1").Id) != null) yield break;
            yield return null;
        }

        throw new Exception("not connected after " + ConnectTimeout + " s: " + ApClient.StatusText);
    }

    /// <summary>Waits on the game thread until the condition holds (for state the plugin only touches on the game thread).</summary>
    internal static IEnumerator WaitFor(Func<bool> condition, string what, float seconds = 20f)
    {
        var deadline = Time.realtimeSinceStartup + seconds;
        while (!condition())
        {
            if (Time.realtimeSinceStartup > deadline) throw new Exception("timed out waiting for " + what);
            yield return null;
        }
    }

    /// <summary>A session that has not collected anything yet, so a test sees only its own checks.</summary>
    internal static void FreshSession()
    {
        var state = ApClient.State;
        state.CheckedLocations.Clear();
        state.HintedLocations.Clear();
        state.PendingHints.Clear();
        state.ReceivedIndex = int.MaxValue; // nothing received so far is granted during a test
    }

    /// <summary>An object in a scene of the given name, so hooks that look at the scene name see the place they expect.</summary>
    internal static GameObject InScene(string sceneName, string objectName, Vector3 position)
    {
        var scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid()) scene = SceneManager.CreateScene(sceneName);
        var go = new GameObject(objectName);
        SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.position = position;
        return go;
    }

    internal static void Cleanup(params GameObject[] objects)
    {
        foreach (var go in objects)
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
    }
}
