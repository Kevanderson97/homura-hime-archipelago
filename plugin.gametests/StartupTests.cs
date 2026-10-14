using System.Linq;
using BepInEx.Bootstrap;
using HarmonyLib;
using Surity;
using System.Collections;

namespace HomuraHimeAP.GameTests;

/// <summary>Checks that need only the game process (title screen): the plugin loaded and its hooks found their targets.</summary>
[TestClass]
public class StartupTests
{
    [Test]
    public IEnumerator PluginIsLoaded() => Harness.Guard(nameof(PluginIsLoaded), () =>
        Assert.IsTrue(Chainloader.PluginInfos.ContainsKey("com.homurahime.archipelago"), "plugin not loaded"));

    [Test]
    public IEnumerator EveryHookFoundItsTargetInThisGameBuild() => Harness.Guard(nameof(EveryHookFoundItsTargetInThisGameBuild), HooksResolve);

    private static void HooksResolve()
    {
        var assembly = typeof(Plugin).Assembly;
        var patched = Harmony.GetAllPatchedMethods().ToList();
        var expected = assembly.GetTypes().Where(t => t.GetCustomAttributes(typeof(HarmonyPatch), false).Length > 0).ToList();
        Assert.IsTrue(expected.Count > 15, "found only " + expected.Count + " patch classes");

        var missing = expected
            .Where(type => AccessTools.Method(type, "Prepare") is { } prepare && !(bool)prepare.Invoke(null, null))
            .Select(type => type.Name).ToList();
        Assert.IsTrue(missing.Count == 0, "hooks without a target: " + string.Join(", ", missing));
        Assert.IsTrue(patched.Count >= expected.Count, patched.Count + " patched methods for " + expected.Count + " patch classes");
    }
}
