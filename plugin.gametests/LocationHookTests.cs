using System.Collections;
using HarmonyLib;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Core;
using Surity;
using UnityEngine;

namespace HomuraHimeAP.GameTests;

/// <summary>The location hooks, run against stand-ins for the game's objects placed where the recon found the real ones.</summary>
[TestClass]
public class LocationHookTests
{
    private static void OpenChest(GameObject chest)
    {
        var type = AccessTools.TypeByName("TreasureChestAddItem");
        var component = chest.AddComponent(type);
        AccessTools.Method(type, "AddItemToPlayer").Invoke(component, null);
    }

    [Test]
    public IEnumerator OpeningAChestAtItsPlaceSendsItsCheck() => Harness.Guard(nameof(OpeningAChestAtItsPlaceSendsItsCheck), ChestAtItsPlace());

    private static IEnumerator ChestAtItsPlace()
    {
        yield return Harness.Connect();
        Harness.FreshSession();

        var location = Catalog.Location("chest/Level_1_Area_1_Main/1");
        var chest = Harness.InScene("Level_1_Area_1_Main", "TreasureChest_Konpeitou (1)", new Vector3(location.Pos[0], location.Pos[1], location.Pos[2]));
        try
        {
            OpenChest(chest);
            Assert.IsTrue(ApClient.State.CheckedLocations.Contains(location.Id), "chest was not reported: " + location.Name);
        }
        finally
        {
            Harness.Cleanup(chest);
        }
    }

    [Test]
    public IEnumerator AChestAnywhereElseIsLeftAlone() => Harness.Guard(nameof(AChestAnywhereElseIsLeftAlone), ChestElsewhere());

    private static IEnumerator ChestElsewhere()
    {
        yield return Harness.Connect();
        Harness.FreshSession();

        var chest = Harness.InScene("Some_Unknown_Scene", "TreasureChest_Nowhere", new Vector3(1234f, 5f, 4321f));
        try
        {
            OpenChest(chest);
            Assert.Equal(0, ApClient.State.CheckedLocations.Count, "checked locations");
        }
        finally
        {
            Harness.Cleanup(chest);
        }
    }

    [Test]
    public IEnumerator ChestsThatSpawnAfterAFightTakeTheHeadChaseLocation() =>
        Harness.Guard(nameof(ChestsThatSpawnAfterAFightTakeTheHeadChaseLocation), SpawnedChest());

    private static IEnumerator SpawnedChest()
    {
        yield return Harness.Connect();
        Harness.FreshSession();

        var chest = Harness.InScene("Level_1_Ikai_Part_1_Main", "TreasureChest_Konpeitou(Clone)", new Vector3(103.98f, -1.34f, 235.2f));
        try
        {
            OpenChest(chest);
            var head = Catalog.Location("head/Level1");
            Assert.IsTrue(ApClient.State.CheckedLocations.Contains(head.Id), "head chase was not reported");
        }
        finally
        {
            Harness.Cleanup(chest);
        }
    }
}
