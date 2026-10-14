using System;
using HomuraHimeAP.Archipelago;
using Opsive.Shared.Utility;
using Opsive.UltimateInventorySystem.SaveSystem;
using UnityEngine;

namespace HomuraHimeAP.Core;

/// <summary>What the saver stores in the game''s save: one JSON text (a <see cref="SaveEntry"/>).</summary>
[Serializable]
public class ApSaveHolder
{
    public string Json;
}

/// <summary>
/// Puts the Archipelago record of a game save (slot, seed, what the save has received, its progress) into the save file itself.
/// It is a saver in the sense of the game''s inventory framework, the same kind the game uses for its own data: the framework
/// asks every registered saver to write when it saves and to read when it loads. Because the record is part of the file, loading
/// an older save also brings back its older received-item index, and a copied or restored save keeps its binding. A save
/// loaded without the plugin simply ignores the extra entry.
/// </summary>
public sealed class ApSaver : SaverBase
{
    internal const string Name = "HomuraHimeArchipelago Saver";

    public override Serialization SerializeSaveData()
    {
        var state = ApClient.State;
        return Serialization.Serialize(new ApSaveHolder { Json = state == null ? "" : SaveBinding.Serialize(state) });
    }

    public override void DeserializeAndLoadSaveData(Serialization serializedSaveData)
    {
        if (serializedSaveData.DeserializeFields(MemberVisibility.All) is ApSaveHolder holder) SaveBinding.RecordLoaded(holder.Json);
    }

    /// <summary>Called by the framework when a save is loaded: a save without our entry is an unbound one.</summary>
    public override void Load()
    {
        SaveBinding.LoadStarted();
        try { base.Load(); }
        catch (Exception e) { Plugin.Log.LogError("Reading the Archipelago record of the save failed: " + e); }
    }

    /// <summary>Called by the framework when a save is written.</summary>
    public override void Save()
    {
        try
        {
            if (ApClient.State != null) base.Save();
            else if (ApClient.Refusal == null) SaveSystemManager.RemoveFromSaveData(FullKey); // an unrandomized game carries no record
            // a save that was refused keeps the record it was loaded with
        }
        catch (Exception e)
        {
            Plugin.Log.LogError("Writing the Archipelago record into the save failed: " + e);
        }
    }

    // ------------------------------------------------------------------ registration

    private static ApSaver _instance;
    private static SaveSystemManager _registeredWith;

    /// <summary>
    /// Keeps the saver registered with the game''s save system, which can be recreated when scenes change. Called every frame
    /// from the plugin; cheap once registered.
    /// </summary>
    internal static void Tick()
    {
        if (SaveSystemManager.IsNull) return;
        var manager = SaveSystemManager.Instance;
        if (manager == _registeredWith && _instance != null) return;

        if (_instance == null)
        {
            var host = new GameObject(Name);
            UnityEngine.Object.DontDestroyOnLoad(host);
            _instance = host.AddComponent<ApSaver>();
            _instance.m_Key = "ap";
        }

        SaveSystemManager.RegisterSaver(_instance);
        _registeredWith = manager;
        Plugin.Log.LogInfo("Archipelago saver registered with the save system");
    }
}
