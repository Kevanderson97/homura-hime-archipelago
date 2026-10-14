using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HomuraHimeAP.UI;

/// <summary>
/// Keeps the game from reacting to the keys typed into the connection form (menus on the title screen, the character in
/// a level). The form draws with IMGUI and reads keys itself, which the game's input system does not know about.
/// </summary>
internal static class InputBlocker
{
    private static bool _blocked;
    private static bool _playerWasLocked;

    private static readonly Type PlayerType = AccessTools.TypeByName("HonoHime.CharacterSystem.Player");

    /// <summary>Blocks or releases the game's input; repeating the same call does nothing.</summary>
    internal static void Set(bool blocked)
    {
        if (blocked == _blocked) return;
        _blocked = blocked;

        try
        {
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = !blocked;
            LockPlayer(blocked);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("Could not " + (blocked ? "block" : "release") + " the game input: " + e.Message);
        }
    }

    private static void LockPlayer(bool locked)
    {
        if (PlayerType == null) return;
        var player = UnityEngine.Object.FindAnyObjectByType(PlayerType);
        var property = player == null ? null : AccessTools.Property(PlayerType, "lockInput");
        if (property == null) return;

        if (locked)
        {
            _playerWasLocked = property.GetValue(player) is true;
            property.SetValue(player, true);
        }
        else
        {
            property.SetValue(player, _playerWasLocked);
        }
    }
}
