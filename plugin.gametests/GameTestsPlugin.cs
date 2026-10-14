using BepInEx;

namespace HomuraHimeAP.GameTests;

/// <summary>
/// Loads the in-game tests. Surity's runner plugin finds every [TestClass] in the loaded assemblies and runs them once, but
/// only when the game was started through the Surity CLI (see docs/TESTING.md).
/// </summary>
[BepInPlugin("com.homurahime.archipelago.gametests", "Homura-Hime Archipelago game tests", "0.1.0")]
[BepInDependency("com.homurahime.archipelago")]
public sealed class GameTestsPlugin : BaseUnityPlugin
{
}
