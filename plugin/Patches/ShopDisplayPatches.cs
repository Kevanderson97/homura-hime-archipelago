using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Core;
using HomuraHimeAP.UI;
using Opsive.UltimateInventorySystem.Core;
using Opsive.UltimateInventorySystem.Core.DataStructures;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace HomuraHimeAP.Patches;

/// <summary>
/// Shows what a shop slot really holds. The merchant's entries that are Archipelago locations are displayed as the
/// item they now give:
///  - an item of this game is shown exactly as the game shows that item (name, icon, description, demo image, combo
///    inputs), by feeding the display views the replacement item instead of the one the slot sells;
///  - anything else (items of other games, currencies, utilities) gets the Archipelago item's name, a description and
///    an icon of its own.
/// Only the display views inside the shop menu are changed: prices, stock and the purchase itself still use the item
/// the slot sells.
/// </summary>
internal static class ShopDisplay
{
    private const string ShopMenu = "Generic Shop Menu";

    /// <summary>Item types that exist as item definitions of the game (the others are currencies and plugin items).</summary>
    private static readonly HashSet<string> DefinitionTypes = new()
    {
        "key", "ability", "dash_skill", "skill", "combo", "omamori", "gun", "upgrade", "shard", "crystal", "consumable",
    };

    /// <summary>Stand-in shown for items that have no display of their own: a plain item whose panel is the generic one.</summary>
    private const string PlaceholderDefinition = "Konpeitou";

    private static readonly Dictionary<string, Opsive.UltimateInventorySystem.Core.Item> Replacements = new();
    private static readonly Dictionary<Graphic, Color> OriginalColors = new();
    private static readonly HashSet<LocalizeStringEvent> SilencedEvents = new();
    private static readonly HashSet<string> Reported = new();

    /// <summary>
    /// The entry a whole description panel is currently showing, set only while the panel draws. The views inside the
    /// panel are fed the replacement item and belong to that entry. This must not be recognised by comparing items:
    /// the game hands out one shared item object per definition, so the replacement for "Molten Impact" is the very
    /// object the real Molten Impact slot holds, and that slot would be taken for an already replaced one.
    /// </summary>
    internal static ScoutedItem Active;

    /// <summary>The scouted item behind a shop display view, or null when the view should look vanilla.</summary>
    internal static ScoutedItem For(Component view, ItemInfo info)
    {
        if (Active != null) return Active;

        var definition = info.Item?.ItemDefinition?.name;
        if (definition == null || !ApClient.Active) return null;
        if (!Hierarchy.HasAncestorNamed(view.transform, ShopMenu, 24)) return null;

        var location = ShopLocations.ForDisplay(definition);
        if (location == null) return null;

        HintSender.Seen(location); // a view only binds entries the shop lists, so this is exactly the visible ones
        return ScoutCache.Get(location.Id);
    }

    /// <summary>True when the game itself can display the item (it has an item definition with names and images).</summary>
    internal static bool HasGameDefinition(ScoutedItem item) =>
        item.IsOwn && item.Record != null && DefinitionTypes.Contains(item.Record.Type) &&
        InventorySystemManager.GetItemDefinition(item.Record.Game) != null;

    /// <summary>The item the display views are fed instead of the one the slot sells.</summary>
    internal static ItemInfo Replace(ScoutedItem scouted, ItemInfo original)
    {
        var definition = HasGameDefinition(scouted) ? scouted.Record.Game : PlaceholderDefinition;
        if (!Replacements.TryGetValue(definition, out var item) || item == null)
            Replacements[definition] = item = InventorySystemManager.CreateItem(definition);
        // the collection must be kept: modules such as ItemCollectionMatchItemView read info.ItemCollection.Name, and an exception
        // there aborts the whole selection event (the flow bar stops following, the detail panel and the empty state go stale)
        // a purchase pays out one item, whatever the stock of the slot says (the Special Hanami Dango slot sells "x999")
        return new ItemInfo(new ItemAmount(item, 1), original.ItemCollection);
    }

    /// <summary>The picture of an entry: the game's own icon for own items, the Archipelago logo (black in lists, coloured in the details) for other worlds.</summary>
    internal static Sprite IconFor(ScoutedItem item, bool details = false) =>
        (item.IsOwn ? ItemIcons.For(item.Record) : null) ?? (details ? Assets.Logo : Assets.LogoBlack) ?? Assets.LogoBlack;

    internal static string Name(ScoutedItem item) => item.IsOwn ? item.Name : $"{item.PlayerName}'s {item.Name}";

    internal static string Description(ScoutedItem item)
    {
        if (!item.IsOwn) return $"{Importance(item.Flags)} item from {item.PlayerName}'s world.";
        return item.Record?.Type switch
        {
            "currency" => item.Name + " for you", // "750 Konpeito for you", "Senbei for you"
            "utility" when item.Record.Game == "FullHeal" => "Fully restores your health.",
            "utility" when item.Record.Game == "UltimateCharge" => "Fills your ultimate gauge.",
            "unlock" => "Unlocks a chapter.",
            _ => "An Archipelago item for you.",
        };
    }

    /// <summary>"An essential / a useful / a trap / a filler", from the item's Archipelago classification.</summary>
    private static string Importance(global::Archipelago.MultiClient.Net.Enums.ItemFlags flags)
    {
        if ((flags & global::Archipelago.MultiClient.Net.Enums.ItemFlags.Trap) != 0) return "A trap";
        if ((flags & global::Archipelago.MultiClient.Net.Enums.ItemFlags.Advancement) != 0) return "An essential";
        if ((flags & global::Archipelago.MultiClient.Net.Enums.ItemFlags.NeverExclude) != 0) return "A useful";
        return "A filler";
    }

    /// <summary>
    /// Writes the Archipelago item's name and description into the texts a view fills from localization, and silences
    /// those localization events so the vanilla text does not come back.
    /// </summary>
    internal static void WriteTexts(Component module, ScoutedItem item)
    {
        foreach (var localizer in module.GetComponentsInChildren<LocalizeStringEvent>(true))
        {
            var isDescription = localizer.name.Contains("Description");
            var text = isDescription ? Description(item) : Name(item);
            var color = isDescription ? (Color?)null : Toasts.NameColor(item.Flags, !item.IsOwn);

            if (localizer.TryGetComponent<TMP_Text>(out var tmp)) Apply(tmp, text, color);
            if (localizer.TryGetComponent<Text>(out var legacy)) Apply(legacy, text, color);

            localizer.enabled = false;
            SilencedEvents.Add(localizer);
        }
    }

    // ------------------------------------------------------------------ texts the shop's own tables lack

    /// <summary>
    /// The shop's views only know the string tables of the things the merchant really sells; an item of another type (a key item
    /// such as Nurikabe's Hat) is looked up in the wrong table and shows "No translation found". Such a text is looked up in the
    /// other item tables; when no table has it, the catalogue name is used and the item is written to <c>missing_shop_texts.txt</c>
    /// so a custom entry can be made for it.
    /// </summary>
    internal static void FixMissingTexts(Component module, ScoutedItem item)
    {
        if (item.Record == null) return;
        var definition = item.Record.Game;
        foreach (var localizer in module.GetComponentsInChildren<LocalizeStringEvent>(true))
        {
            if (!localizer.enabled || SilencedEvents.Contains(localizer)) continue;

            // the table can be referenced by name or by id, so ask the database instead of reading the name
            var reference = localizer.StringReference;
            var table = UnityEngine.Localization.Settings.LocalizationSettings.StringDatabase.GetTableAsync(reference.TableReference).WaitForCompletion();
            if (table == null || table.GetEntryFromReference(reference.TableEntryReference) != null) continue;

            var key = reference.TableEntryReference.ResolveKeyName(table.SharedData);
            var isDescription = localizer.name.Contains("Description");
            // the item''s own name can differ from its definition name (spaces become underscores in the key the view builds),
            // so the definition name is tried as well
            // (an entry that is referenced by id may have no key name at all)
            var candidates = new[] { key, isDescription ? definition + "_Description" : definition }.Where(c => !string.IsNullOrEmpty(c));
            var text = candidates.SelectMany(candidate => TextTables.Select(name => EntryText(name, candidate))).FirstOrDefault(found => found != null);
            if (text == null)
            {
                // a hand-written entry (data/custom_texts.json), else the catalogue name, and the item is listed for a new entry
                var custom = CustomTexts.Find(definition);
                text = isDescription ? custom?.Description : custom?.Name;
                if (text == null)
                {
                    RecordMissing(definition, key, table.TableCollectionName);
                    text = isDescription ? "" : item.Record.Name;
                }
            }

            if (localizer.TryGetComponent<TMP_Text>(out var tmp)) Apply(tmp, text, null);
            if (localizer.TryGetComponent<Text>(out var legacy)) Apply(legacy, text, null);
            localizer.enabled = false;
            SilencedEvents.Add(localizer);
        }
    }
    private static readonly string[] TextTables = { "HH_InventoryItemsCollection", "HH_Codex" };

    private static string EntryText(string table, string key)
    {
        var collection = UnityEngine.Localization.Settings.LocalizationSettings.StringDatabase.GetTableAsync(table).WaitForCompletion();
        var value = collection?.GetEntry(key)?.LocalizedValue;
        return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>Remembers (once per key) an item text no table has, in the log and in a file next to the session files.</summary>
    private static void RecordMissing(string definition, string key, string table)
    {
        if (!Reported.Add("missing:" + key)) return;
        Plugin.Log.LogWarning($"Shop text missing for {definition}: key {key} is in no item table (looked in {table})");
        try
        {
            var path = System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "HomuraHimeArchipelago", "missing_shop_texts.txt");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            System.IO.File.AppendAllText(path, $"{definition}\t{key}\n");
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("Could not write missing_shop_texts.txt: " + e.Message);
        }
    }

    /// <summary>Gives the views back to the game's own localization (after an Archipelago entry was shown there).</summary>
    internal static void RestoreTexts(Component module)
    {
        foreach (var localizer in module.GetComponentsInChildren<LocalizeStringEvent>(true))
        {
            if (!SilencedEvents.Remove(localizer)) continue;
            if (localizer.TryGetComponent<Graphic>(out var graphic) && OriginalColors.TryGetValue(graphic, out var color))
                graphic.color = color;
            localizer.enabled = true;
            localizer.RefreshString();
        }
    }

    private static void Apply(TMP_Text text, string value, Color? color)
    {
        text.text = value;
        Paint(text, color);
    }

    private static void Apply(Text text, string value, Color? color)
    {
        text.text = value;
        Paint(text, color);
    }

    /// <summary>
    /// Sets the colour of a text, or, when none is given, gives it the colour the game made it with. A view is reused for one
    /// entry after another, so an entry without a colour of its own must not keep the one the previous entry gave it.
    /// </summary>
    private static void Paint(Graphic graphic, Color? color)
    {
        if (color.HasValue) Tint(graphic, color.Value);
        else if (OriginalColors.TryGetValue(graphic, out var original)) graphic.color = original;
    }

    private static void Tint(Graphic graphic, Color color)
    {
        if (!OriginalColors.ContainsKey(graphic)) OriginalColors[graphic] = graphic.color;
        graphic.color = color;
    }

    /// <summary>A display problem must never break the shop, so it is logged once per source and otherwise ignored.</summary>
    internal static void ReportOnce(string source, Exception e)
    {
        if (Reported.Add(source)) Plugin.Log.LogWarning($"Shop display ({source}) failed: {e}");
    }
}

/// <summary>
/// The display modules of the shop: name and description (LocalizeItemView, LocalizationTableItemView), icons
/// (IconItemView, ImageByDefinitionItemView) and the description panels of the detail view (ItemDescriptionBase, which
/// also feeds the demo images and combo inputs). Before they draw, the item is swapped for the replacement; afterwards
/// the texts and icons the game cannot provide for it are filled in.
/// </summary>
[HarmonyPatch]
internal static class ShopViewPatch
{
    private static readonly string[] Targets =
    {
        "Opsive.UltimateInventorySystem.UI.Item.ItemViewModules.IconItemView",
        "Opsive.UltimateInventorySystem.Integrations.UnityLocalization.LocalizeItemView",
        "LocalizationTableItemView",
        "ImageByDefinitionItemView",
        "Opsive.UltimateInventorySystem.UI.Item.ItemViewModules.AmountItemView",
        "Opsive.UltimateInventorySystem.UI.Item.ItemDescriptionBase",
    };

    private static IEnumerable<MethodBase> TargetMethods() =>
        Targets.Select(AccessTools.TypeByName).Where(type => type != null).Select(type => AccessTools.Method(type, "SetValue"))
            .Where(method => method != null);

    private static bool Prepare() => TargetMethods().Any();

    private static readonly Type DescriptionBase = AccessTools.TypeByName("Opsive.UltimateInventorySystem.UI.Item.ItemDescriptionBase");

    private static bool IsDescriptionPanel(Component view) => DescriptionBase != null && DescriptionBase.IsInstanceOfType(view);

    /// <summary>What a patched view is showing: the entry, and whether this view is the description panel that opened it.</summary>
    internal sealed class Shown
    {
        internal ScoutedItem Item;
        internal bool OpenedPanel;
    }

    private static void Prefix(Component __instance, ref ItemInfo __0, out Shown __state)
    {
        __state = null;
        try
        {
            var item = ShopDisplay.For(__instance, __0);
            if (item == null) return;
            __0 = ShopDisplay.Replace(item, __0);
            var opens = IsDescriptionPanel(__instance) && ShopDisplay.Active == null;
            if (opens) ShopDisplay.Active = item;
            __state = new Shown { Item = item, OpenedPanel = opens };
        }
        catch (Exception e)
        {
            ShopDisplay.ReportOnce("replace", e);
        }
    }

    /// <summary>Runs even when the view throws: a panel left marked as active would turn every later view into its entry.</summary>
    private static Exception Finalizer(Exception __exception, Shown __state)
    {
        if (__state != null && __state.OpenedPanel) ShopDisplay.Active = null;
        return __exception;
    }

    private static void Postfix(Component __instance, Shown __state)
    {
        var item = __state?.Item;
        try
        {
            switch (__instance.GetType().Name)
            {
                case "LocalizeItemView":
                    // views are reused for other entries: give back the game's localization first (a text written for an
                    // earlier Archipelago entry would otherwise stay), then write our own where the game cannot provide it
                    if (item == null || ShopDisplay.HasGameDefinition(item)) ShopDisplay.RestoreTexts(__instance);
                    if (item != null)
                    {
                        if (!ShopDisplay.HasGameDefinition(item)) ShopDisplay.WriteTexts(__instance, item);
                        else ShopDisplay.FixMissingTexts(__instance, item);
                    }
                    break;
                case "LocalizationTableItemView":
                    // this module switches the table the texts are looked up in; check again once it has
                    if (item != null && ShopDisplay.HasGameDefinition(item)) ShopDisplay.FixMissingTexts(__instance, item);
                    break;
                case "IconItemView":
                case "ImageByDefinitionItemView":
                    if (item != null) SetIcon(__instance, item);
                    break;
            }
        }
        catch (Exception e)
        {
            ShopDisplay.ReportOnce("display", e);
        }
    }

    /// <summary>
    /// Items of this game keep the icon the game picked, unless it picked none (items that are not sold in the shop have
    /// no shop image); everything else gets its own icon.
    /// </summary>
    private static void SetIcon(Component module, ScoutedItem item)
    {
        var icon = Traverse.Create(module).Field("m_Icon").GetValue<Image>();
        if (icon == null) return;

        var missing = Traverse.Create(module).Field("m_MissingIcon").GetValue<Sprite>();
        var gameProvidedIcon = icon.sprite != null && icon.sprite != missing;
        if (ShopDisplay.HasGameDefinition(item) && gameProvidedIcon) return;

        var replacement = ShopDisplay.IconFor(item, details: module.GetType().Name == "ImageByDefinitionItemView");
        if (replacement != null) icon.sprite = replacement;
    }
}
