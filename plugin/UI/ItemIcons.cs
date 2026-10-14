using HomuraHimeAP.Core;
using HomuraHimeAP.Data;
using Opsive.UltimateInventorySystem.Core;
using UnityEngine;

namespace HomuraHimeAP.UI;

/// <summary>Finds the game's own icon for an item of this world, so notifications look like the rest of the HUD.</summary>
internal static class ItemIcons
{
    /// <summary>The icon for an item, or null when the game has none (the notification then shows the Archipelago logo).</summary>
    internal static Sprite For(ItemRecord item)
    {
        if (item == null) return null;
        switch (item.Type)
        {
            case "currency":
                return InventorySystemManager.GetCurrency(item.Game)?.Icon;
            case "unlock":
                return ChapterData.TryGetChapter(item.Game, out var chapter) ? Definition(chapter.HeaderItem) ?? Definition(chapter.SubChapters[0]) : null;
            case "utility":
                return item.Game switch
                {
                    "FullHeal" => Definition("HP Crystal"),
                    "UltimateCharge" => Definition("HH_SpecialMove"),
                    _ => null,
                };
            default:
                return Definition(item.Game);
        }
    }

    private static Sprite Definition(string definitionName)
    {
        var definition = InventorySystemManager.GetItemDefinition(definitionName);
        return definition != null && definition.TryGetAttributeValue<Sprite>("Icon", out var icon) ? icon : null;
    }
}
