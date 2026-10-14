using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Opsive.UltimateInventorySystem.Core;
using Opsive.UltimateInventorySystem.Core.InventoryCollections;
using UnityEngine;

namespace HomuraHimeAP.Items;

/// <summary>Everything the plugin does to the player's inventory and character goes through here.</summary>
internal static class PlayerApi
{
    private static Inventory _inventory;
    private static Component _playerMotor;
    private static object _transformationSystem;

    private static readonly Type PlayerType = AccessTools.TypeByName("HonoHime.CharacterSystem.Player");
    private static readonly Type TransformationSystemType = AccessTools.TypeByName("TransformationSystem");

    /// <summary>The player's own inventory (id "fireGirlPlayer"), or null while no level/hub is loaded.</summary>
    internal static Inventory Inventory
    {
        get
        {
            if (_inventory != null) return _inventory;
            _inventory = UnityEngine.Object.FindObjectsByType<Inventory>(UnityEngine.FindObjectsSortMode.None).FirstOrDefault(i => i.name == "fireGirlPlayer");
            return _inventory;
        }
    }

    /// <summary>True when the game is in a state where changing the inventory is safe.</summary>
    internal static bool InGame
    {
        get
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (scene is "StartScreen" or "LoadingScene" or "MainCore" or "") return false;
            return Inventory != null && InventorySystemManager.IsNull == false;
        }
    }

    internal static bool Has(string definitionName)
    {
        var inv = Inventory;
        var def = InventorySystemManager.GetItemDefinition(definitionName);
        return inv != null && def != null && inv.GetItemAmount(def, true, false) > 0;
    }

    internal static int AmountOwned(string definitionName)
    {
        var inv = Inventory;
        var def = InventorySystemManager.GetItemDefinition(definitionName);
        return inv != null && def != null ? inv.GetItemAmount(def, true, false) : 0;
    }

    /// <summary>Takes items back out of the player's inventory (vanilla rewards replaced by randomized ones).</summary>
    internal static void Remove(string definitionName, int amount)
    {
        if (amount > 0 && Inventory != null) Inventory.RemoveItem(definitionName, amount);
    }

    internal static bool DefinitionExists(string definitionName) =>
        InventorySystemManager.GetItemDefinition(definitionName) != null;

    internal static bool Add(string definitionName, int amount = 1)
    {
        var inv = Inventory;
        if (inv == null) return false;
        inv.AddItem(definitionName, amount);
        return true;
    }

    internal static bool AddCurrency(string currencyName, int amount)
    {
        var owner = Inventory?.GetCurrencyOwner();
        if (owner == null) return false;
        return owner.AddCurrency(currencyName, amount);
    }

    // ---------------------------------------------------------------- character utilities
    private static Component PlayerMotor
    {
        get
        {
            if (_playerMotor == null && PlayerType != null)
                _playerMotor = UnityEngine.Object.FindAnyObjectByType(PlayerType) as Component;
            return _playerMotor;
        }
    }

    internal static bool IsPlayerAlive()
    {
        var motor = PlayerMotor;
        if (motor == null) return false;
        var dead = AccessTools.Property(motor.GetType(), "isDead")?.GetValue(motor);
        return !(dead is true);
    }

    /// <summary>
    /// True when the player can act: not locked by a cutscene, dialogue or menu (the character's own input locks), and the
    /// game is running. Unknown flags count as "has control" so a game update cannot disable the feature.
    /// </summary>
    internal static bool HasControl()
    {
        if (Time.timeScale < 0.01f) return false;
        var motor = PlayerMotor;
        if (motor == null) return false;
        foreach (var flag in new[] { "lockInput", "LockPlayer" })
            if (AccessTools.Property(motor.GetType(), flag)?.GetValue(motor) is true) return false;
        return true;
    }

    /// <summary>Kills the player at once (DeathLink). False when there is no character to kill.</summary>
    internal static bool Kill()
    {
        var motor = PlayerMotor;
        if (motor == null || !IsPlayerAlive()) return false;
        var kill = AccessTools.Method(motor.GetType(), "DeathImmediate");
        if (kill == null) return false;
        kill.Invoke(motor, null);
        return true;
    }

    /// <summary>Full Heal: restore the player's HP. False if the player is not available or is dead.</summary>
    internal static bool FullHeal()
    {
        var motor = PlayerMotor;
        if (motor == null) return true; // nothing to heal (hub without a character); don't stall the queue
        if (!IsPlayerAlive()) return false;
        var reset = AccessTools.Method(motor.GetType(), "ResetHP");
        if (reset == null) return false;
        reset.Invoke(motor, null);
        return true;
    }

    private static object TransformationSystem
    {
        get
        {
            if (_transformationSystem == null && TransformationSystemType != null)
                _transformationSystem = AccessTools.Field(TransformationSystemType, "ins")?.GetValue(null);
            return _transformationSystem;
        }
    }

    /// <summary>True if the player owns the ultimate or the transformation (the meter ignores charge otherwise).</summary>
    internal static bool HasChargeAbility() => Has("HH_SpecialMove") || Has("TransformationAbility");

    /// <summary>Ultimate Charge: fill the ultimate / transformation meter.</summary>
    internal static bool ChargeUltimate()
    {
        var system = TransformationSystem;
        if (system == null) return false;
        var add = AccessTools.Method(system.GetType(), "AddChargeValue", new[] { typeof(float) });
        if (add == null) return false;
        add.Invoke(system, new object[] { 100f });
        return true;
    }
}
