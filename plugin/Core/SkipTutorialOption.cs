using System.Collections.Generic;
using System.Linq;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Patches;
using HomuraHimeAP.UI;
using Opsive.UltimateInventorySystem.Core;
using Opsive.UltimateInventorySystem.Core.AttributeSystem;
using Opsive.UltimateInventorySystem.Core.DataStructures;
using Opsive.UltimateInventorySystem.Core.InventoryCollections;
using Opsive.UltimateInventorySystem.UI.Item;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace HomuraHimeAP.Core;

/// <summary>
/// A "Skip Tutorial" button on the System page of the pause menu, offered while the tutorial of a randomizer game is not
/// finished. Pressing it asks for a confirmation, and confirming completes the checks of the tutorial and leaves for the
/// hub (see <see cref="Skip"/>).
/// <para>
/// The System page is a list of items in the "SystemOptions" collection; what an item does is decided by its category. The
/// button is therefore an item of its own: a copy of the definition of "Load Latest Save", with its own name and description
/// (added to the game's menu texts), which is put into the collection while the tutorial is open and taken out again
/// afterwards. Clicking it opens the confirmation of "Load Latest Save" (<see cref="SkipTutorialActionPatch"/>), with the title
/// and the question replaced, and its confirm button runs the skip instead of reloading the level.
/// </para>
/// </summary>
internal static class SkipTutorialOption
{
    internal const string DefinitionName = "SkipTutorial_UIOpt";

    private const string SourceDefinition = "ReturnToCheckPoint_UIOpt";
    private const string OptionsGrid = "System Options List";
    private const string OptionsCollection = "SystemOptions";
    private const string TextTable = "Menu_UI";
    private const string NameKey = "Menu_UI_SystemOptions_SkipTutorial";
    private const string DetailsKey = NameKey + "Details";
    private const string PromptTitleKey = "Menu_UI_SkipTutorial_Title";
    private const string PromptKey = "Menu_UI_SkipTutorial_Prompt";
    private const string ConfirmPanel = "Return To CheckPoint Panel";

    /// <summary>The checks of the tutorial: the first fight (when fights are locations) and the clear reward.</summary>
    private static readonly string[] TutorialLocations = { "encounter/Level_0_Main/main", "tutorial/clear" };

    /// <summary>Seconds between looks at the pause menu while it is not known (a scene search is slow).</summary>
    private const float SearchInterval = 1f;

    /// <summary>Seconds between checks of the System page once it is known.</summary>
    private const float CheckInterval = 0.25f;

    /// <summary>Seconds the notifications are held back for the way to the hub; after that they are shown anyway.</summary>
    private const float HoldSeconds = 20f;

    private static float _nextSearch, _nextCheck, _holdUntil;
    private static InventoryGrid _grid;
    private static bool _defined, _menuKnown;
    private static ItemDefinition _definition;

    /// <summary>True from the click on the button until another option is chosen or the skip is confirmed.</summary>
    internal static bool Pending { get; private set; }

    /// <summary>The button is offered in a randomizer game whose tutorial is not finished.</summary>
    internal static bool Wanted => ApClient.Active && !TutorialTracker.Done;

    /// <summary>Called every frame: puts the button into the System page or takes it out, as the state asks.</summary>
    internal static void Tick(float now)
    {
        if (Toasts.Hold && now > _holdUntil) Toasts.Hold = false; // the hub never loaded: do not keep the messages back

        if (_grid == null)
        {
            if (!Wanted || now < _nextSearch) return;
            _nextSearch = now + SearchInterval;
            _grid = Object.FindObjectsByType<InventoryGrid>(FindObjectsSortMode.None).FirstOrDefault(g => g.name == OptionsGrid);
            if (_grid == null) return;
            _menuKnown = true;
        }

        if (now < _nextCheck) return;
        _nextCheck = now + CheckInterval;

        var collection = _grid.Inventory?.GetItemCollection(OptionsCollection);
        if (collection == null) return;

        var present = Find(collection) != null;
        if (Wanted == present) return;

        if (Wanted) Add(collection);
        else Remove(collection);
        _grid.Draw();
    }

    private static Item Find(ItemCollection collection) =>
        collection.GetAllItemStacks().Select(stack => stack.Item).FirstOrDefault(item => item.ItemDefinition.name == DefinitionName);

    private static void Add(ItemCollection collection)
    {
        if (!Define()) return;
        AddMenuTexts(); // the text tables can be loaded again after a scene change
        collection.AddItem(InventorySystemManager.CreateItem(DefinitionName), 1);
    }

    private static void Remove(ItemCollection collection)
    {
        var item = Find(collection);
        if (item != null) collection.RemoveItem(new ItemInfo(item, 1));
    }

    /// <summary>Makes the item definition and its texts, once.</summary>
    private static bool Define()
    {
        if (_defined && _definition != null) return true;
        var source = InventorySystemManager.GetItemDefinition(SourceDefinition);
        if (source == null)
        {
            Plugin.Log.LogWarning($"Skip Tutorial button: the definition {SourceDefinition} is missing in this game build");
            return false;
        }

        var definition = Object.Instantiate(source);
        definition.name = DefinitionName;
        // nothing in the scene refers to it, so changing the scene would unload it as an unused asset
        definition.hideFlags = HideFlags.HideAndDontSave;
        // a definition needs an id of its own; the registered ones are random numbers, so a hash of the name will not clash
        HarmonyLib.AccessTools.Field(typeof(ItemDefinition), "m_ID").SetValue(definition, (uint)DefinitionName.GetHashCode() | 0x80000000u);
        if (!InventorySystemManager.ItemDefinitionRegister.Register(definition) || !definition.Initialize(true, true))
        {
            Plugin.Log.LogWarning("Skip Tutorial button: the item definition could not be registered");
            return false;
        }

        SetText(definition, "KeyID", NameKey);
        SetText(definition, "LogKeyID", DetailsKey);
        AddMenuTexts();
        _definition = definition;
        _defined = true;
        return true;
    }

    /// <summary>The menu reads the name and the description of an option by the keys held in these two attributes.</summary>
    private static void SetText(ItemDefinition definition, string attribute, string key)
    {
        if (definition.TryGetAttribute(attribute, out var found) && found is Attribute<string> text)
            text.SetOverrideValue(key, true, true);
    }

    /// <summary>The texts of the button, in every language the game has (there are only English ones).</summary>
    private static void AddMenuTexts()
    {
        foreach (var locale in LocalizationSettings.AvailableLocales.Locales)
        {
            var table = LocalizationSettings.StringDatabase.GetTableAsync(TextTable, locale).WaitForCompletion();
            if (table == null) continue;
            if (table.GetEntry(NameKey) == null) table.AddEntry(NameKey, "Skip Tutorial");
            if (table.GetEntry(DetailsKey) == null)
                table.AddEntry(DetailsKey, "Completes the checks of the tutorial and takes you to the hub.");
            if (table.GetEntry(PromptTitleKey) == null) table.AddEntry(PromptTitleKey, "Skip Tutorial");
            if (table.GetEntry(PromptKey) == null) table.AddEntry(PromptKey, "Are you sure you want to skip the tutorial?");
        }
    }

    /// <summary>
    /// A System option was chosen. The confirmation behind the button is the one of "Load Latest Save", so it is given our
    /// texts while the button opens it and the game's own again when anything else does.
    /// </summary>
    internal static void OptionChosen(string definition)
    {
        Pending = definition == DefinitionName;
        if (_menuKnown) ShowPrompt();
    }

    // (a method of its own: the code that touches the menu needs the game's assemblies, the state above does not)
    private static void ShowPrompt() => Prompt.Show(_grid.transform.parent, Pending);

    /// <summary>The title and the question of the confirmation (kept apart: they need Unity's localization).</summary>
    private static class Prompt
    {
        /// <summary>The texts of the confirmation as the game has them, while ours are shown.</summary>
        private static readonly Dictionary<LocalizeStringEvent, (TableReference Table, TableEntryReference Entry)> Original = new();

        internal static void Show(Transform menu, bool skipping)
        {
            var panel = menu.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith(ConfirmPanel));
            if (panel == null) return;

            foreach (var text in panel.GetComponentsInChildren<LocalizeStringEvent>(true))
            {
                var key = text.name == "Title" ? PromptTitleKey : text.name == "Content" ? PromptKey : null;
                if (key == null) continue;

                if (skipping)
                {
                    Original.TryAdd(text, (text.StringReference.TableReference, text.StringReference.TableEntryReference));
                    text.StringReference.SetReference(TextTable, key);
                }
                else if (Original.TryGetValue(text, out var original))
                {
                    text.StringReference.SetReference(original.Table, original.Entry);
                    Original.Remove(text);
                }
            }
        }
    }
    /// <summary>The confirm button of the question was pressed: skips the tutorial and closes the panel.</summary>
    internal static void Confirmed(object confirmBox)
    {
        OptionChosen(null);
        Skip();
        HarmonyLib.Traverse.Create(confirmBox).Field("closeDisplayPanel").Method("SmartClose").GetValue();
    }

    /// <summary>
    /// Completes everything the tutorial would check, marks it finished and goes to the hub.
    /// </summary>
    internal static void Skip()
    {
        if (!Wanted) return;

        // the "Found ..." messages of the checks would be drawn by the HUD of the tutorial, which the way to the hub removes
        Toasts.Hold = true;
        _holdUntil = Time.unscaledTime + HoldSeconds;
        foreach (var key in TutorialLocations)
        {
            var location = Catalog.Location(key);
            if (location != null) LocationHooks.Report(location);
        }

        TutorialTracker.Complete();
        if (_grid != null) { _nextCheck = 0f; Tick(Time.unscaledTime); } // the button goes away at once
        if (GameApi.TeleportToCategory("HubWorld")) Toasts.Show("Tutorial skipped", Toasts.Info);
        else
        {
            Toasts.Hold = false;
            Toasts.Show("Tutorial checks sent, but the way to the hub could not be opened", Toasts.Error);
        }
    }

    /// <summary>The hub has loaded: the messages that waited for it are shown.</summary>
    internal static void Arrived() => Toasts.Hold = false;
}
