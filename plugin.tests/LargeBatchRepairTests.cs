using System.Collections.Generic;
using System.Linq;
using HomuraHimeAP.Core;
using Xunit;

namespace HomuraHimeAP.Tests;

/// <summary>
/// The batch that a player's log showed: connecting to a save with 42 items already granted and receiving 97 more at once (the
/// Pretty Little Flower among them), after which the Flower was missing from the inventory. The plugin must notice that and
/// give back exactly what is missing, never the items that are used up.
/// </summary>
public class LargeBatchRepairTests
{
    // the "Granting ..." lines of that log, in order
    private static readonly string[] BatchNames =
    {
        "1500 Konpeito", "3000 Konpeito", "3000 Konpeito", "3000 Konpeito", "Omamori Evasion Gem", "Life Crystal Shard", "Desire Circle",
        "4000 Konpeito", "5000 Konpeito", "Omamori Shura Ashes", "Senbei", "1500 Konpeito", "10000 Konpeito", "Puffer Swarm Trap",
        "4000 Konpeito", "14000 Konpeito", "Puffer Swarm Trap", "20 Sakura Petals", "10000 Konpeito", "2000 Konpeito", "1500 Konpeito",
        "Ultimate Charge", "5000 Konpeito", "3000 Konpeito", "3000 Konpeito", "Life Crystal Shard", "30 Sakura Petals",
        "Omamori Life Charm (L)", "Omamori Bone Charm (L)", "40 Sakura Petals", "3000 Konpeito", "Special Hanami Dango",
        "Nurikabe's Shoe", "Nurikabe's Gloves", "Combo Fire Wheel", "Skill Phantom Warrior", "Omamori Invincible Blade",
        "Life Crystal Shard", "Life Crystal Shard", "Blessed Crystal Shard", "Blessed Crystal Shard", "Blessed Crystal Shard",
        "Special Hanami Dango", "Senbei", "1000 Konpeito", "30 Sakura Petals", "Full Heal", "Full Heal", "Ultimate Charge",
        "Ultimate Charge", "Ultimate Charge", "3000 Konpeito", "3000 Konpeito", "3000 Konpeito", "3000 Konpeito", "3000 Konpeito",
        "5000 Konpeito", "5000 Konpeito", "5000 Konpeito", "10000 Konpeito", "2000 Konpeito", "4000 Konpeito", "2 Senbei",
        "7500 Konpeito", "Progressive Level Key", "Progressive Level Key", "Puffer Swarm Trap", "Puffer Swarm Trap",
        "Puffer Swarm Trap", "Puffer Swarm Trap", "Hexed Doll Trio Trap", "Chirakuin's Soul Core", "Agasa's Soul Core",
        "Omamori Strength Charm (L)", "5000 Konpeito", "2 Senbei", "Progressive Level Key", "Blessed Crystal Shard",
        "Skill Spark Rush", "14000 Konpeito", "3000 Konpeito", "2000 Konpeito", "Life Crystal Shard", "Omamori Aegis Fragment",
        "750 Konpeito", "Special Hanami Dango", "3000 Konpeito", "Pretty Little Flower", "1000 Konpeito", "Blessed Crystal Shard",
        "5000 Konpeito", "3000 Konpeito", "10 Sakura Petals", "30 Sakura Petals", "Skill Thousand Cuts",
        "Senbei", "Life Crystal Shard", // the two that arrived a little later, while the player was playing
    };

    // what in that batch must stay in the inventory: equipment, the key items and the Soul Cores
    private static readonly string[] Kept =
    {
        "Omamori Evasion Gem", "Desire Circle", "Omamori Shura Ashes", "Omamori Life Charm (L)", "Omamori Bone Charm (L)",
        "Nurikabe's Shoe", "Nurikabe's Gloves", "Combo Fire Wheel", "Skill Phantom Warrior", "Omamori Invincible Blade",
        "Chirakuin's Soul Core", "Agasa's Soul Core", "Omamori Strength Charm (L)", "Skill Spark Rush", "Omamori Aegis Fragment",
        "Pretty Little Flower", "Skill Thousand Cuts",
    };

    private static List<ItemRecord> Batch()
    {
        TestEnvironment.Ensure();
        return BatchNames.Select(name => Catalog.Item(name) ?? throw new System.InvalidOperationException("not in the catalog: " + name)).ToList();
    }

    [Fact]
    public void TheBatchOfTheLogIsNinetySevenItemsOfTheCatalog() => Assert.Equal(97, Batch().Count);

    [Fact]
    public void ALostFlowerIsTheOnlyThingGivenBack()
    {
        var batch = Batch();
        var missing = OwnedItems.Missing(batch, _ => true, definition => definition != "Codex_Important_Level1_Flower");

        Assert.Equal(new[] { "Pretty Little Flower" }, missing.Select(item => item.Name));
    }

    [Fact]
    public void AnInventoryThatKeptEverythingNeedsNoRepair() =>
        Assert.Empty(OwnedItems.Missing(Batch(), _ => true, _ => true));

    [Fact]
    public void AnEmptyInventoryGetsBackExactlyTheKeptItemsAndNeverTheUsedUpOnes()
    {
        var missing = OwnedItems.Missing(Batch(), _ => true, _ => false);

        Assert.Equal(Kept.OrderBy(name => name), missing.Select(item => item.Name).OrderBy(name => name));
        Assert.DoesNotContain(missing, item => item.Type is "currency" or "shard" or "consumable" or "utility" or "trap" or "unlock_progressive");
    }

    [Fact]
    public void AnItemTheGameHasNoDefinitionForIsLeftAlone()
    {
        var missing = OwnedItems.Missing(Batch(), definition => definition != "Codex_Important_Level1_Flower", _ => false);

        Assert.DoesNotContain(missing, item => item.Name == "Pretty Little Flower");
        Assert.Equal(Kept.Length - 1, missing.Count);
    }

    [Fact]
    public void AnItemReceivedTwiceIsRepairedOnce()
    {
        var twice = Batch().Concat(Batch().Where(item => item.Name == "Pretty Little Flower")).ToList();

        Assert.Single(OwnedItems.Missing(twice, _ => true, definition => definition != "Codex_Important_Level1_Flower"));
    }
}
