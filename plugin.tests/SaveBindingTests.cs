using System.Linq;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Core;
using Xunit;

namespace HomuraHimeAP.Tests;

public class SaveBindingTests : System.IDisposable
{
    public SaveBindingTests()
    {
        TestEnvironment.Ensure();
        foreach (var file in new[] { "saves.json", "saves.json.bak" })
            System.IO.File.Delete(System.IO.Path.Combine(TestEnvironment.TempDirectory, file));
        ApClient.Reset();
    }

    public void Dispose() => ApClient.Reset();

    private static SaveEntry Entry(string slot, string seed) => new() { Slot = slot, Seed = seed };

    [Theory]
    [InlineData(null, null, false, "Vanilla")]
    [InlineData("Alice", "s1", false, "Adopt")]
    [InlineData(null, null, true, "Offline")]
    public void WithoutAMatchingRecord(string connectedSlot, string connectedSeed, bool hasRecord, string expected)
    {
        var entry = hasRecord ? Entry("Alice", "s1") : null;
        Assert.Equal(expected, SaveBinding.Decide(connectedSlot, connectedSeed, entry).ToString());
    }

    [Theory]
    [InlineData("Alice", "s1", "Continue")]
    [InlineData("Alice", "s2", "Refuse")] // same slot name, another seed
    [InlineData("Bob", "s1", "Refuse")] // another slot
    public void ARecordedSaveIsComparedWithTheConnection(string connectedSlot, string connectedSeed, string expected)
    {
        Assert.Equal(expected, SaveBinding.Decide(connectedSlot, connectedSeed, Entry("Alice", "s1")).ToString());
    }

    [Fact]
    public void TheRecordInASaveCarriesTheSlotAndTheProgress()
    {
        var state = SessionState.Load("Hana", "seedH");
        state.ReceivedIndex = 12;
        state.ClearedWorlds.Add("Level1");

        var json = SaveBinding.Serialize(state); // what the saver writes into the save file
        SaveBinding.LoadStarted();
        SaveBinding.RecordLoaded(json); // what it reads back when the save is loaded
        SaveBinding.CurrentSaveSlot = 2;

        var entry = SaveBinding.CurrentEntry;
        Assert.Equal("Hana", entry.Slot);
        Assert.Equal("seedH", entry.Seed);
        Assert.Equal(12, entry.Progress.ReceivedIndex);
        Assert.Contains("Level1", entry.Progress.ClearedWorlds);
    }

    [Fact]
    public void ASaveWithoutARecordIsUnbound()
    {
        SaveBinding.LoadStarted();
        SaveBinding.CurrentSaveSlot = 1;
        Assert.Null(SaveBinding.CurrentEntry);

        SaveBinding.RecordLoaded("not json at all");
        Assert.Null(SaveBinding.CurrentEntry); // an unreadable record is treated as no record
    }

    [Fact]
    public void ALoadWithoutTheSaverForgetsTheRecordOfTheEarlierSave()
    {
        var state = SessionState.Load("Ivy", "seedI");
        SaveBinding.LoadStarted();
        SaveBinding.RecordLoaded(SaveBinding.Serialize(state));

        SaveBinding.Loaded(1); // the saver took part: the record stays
        Assert.NotNull(SaveBinding.LoadedRecord);

        SaveBinding.CurrentSaveSlot = 0;
        SaveBinding.LoadedRecord = Deserialize(state);
        SaveBinding.Loaded(0); // no saver this time: whatever was loaded before no longer applies
        Assert.Null(SaveBinding.LoadedRecord);
    }

    private static SaveEntry Deserialize(SessionState state) => SaveBinding.Deserialize(SaveBinding.Serialize(state));

    [Fact]
    public void AnUnrandomizedSaveCarriesNoRecord()
    {
        ApClient.State = SessionState.Load("Jon", "seedJ");
        SaveBinding.Saving(0);
        Assert.NotNull(SaveBinding.LoadedRecord);

        ApClient.State = null;
        SaveBinding.Saving(0);
        Assert.Null(SaveBinding.LoadedRecord);
    }

    [Fact]
    public void ARefusedSaveKeepsItsRecordWhenTheGameSavesIt()
    {
        var record = Deserialize(SessionState.Load("Kay", "seedK"));
        SaveBinding.LoadedRecord = record;
        ApClient.State = null;
        ApClient.Refusal = "belongs to someone else";

        SaveBinding.Saving(1);

        Assert.Same(record, SaveBinding.LoadedRecord);
    }

    [Fact]
    public void DeletingASaveForgetsItsRecord()
    {
        SaveBinding.CurrentSaveSlot = 2;
        SaveBinding.LoadedRecord = Deserialize(SessionState.Load("Liv", "seedL"));

        SaveBinding.Deleted(2);

        Assert.Null(SaveBinding.CurrentEntry);
    }

    [Fact]
    public void ARecordKeptInTheOldFileIsStillUsedUntilTheSaveHoldsIt()
    {
        var path = System.IO.Path.Combine(TestEnvironment.TempDirectory, "saves.json");
        System.IO.File.WriteAllText(path, "{\"Version\":1,\"Saves\":{\"3\":{\"Slot\":\"Old\",\"Seed\":\"oldseed\",\"Progress\":{\"ReceivedIndex\":9}}}}");
        SaveBinding.Reset();
        SaveBinding.CurrentSaveSlot = 3;

        Assert.Equal("Old", SaveBinding.CurrentEntry.Slot);
        Assert.Equal(9, SaveBinding.CurrentEntry.Progress.ReceivedIndex);

        ApClient.State = SessionState.Load("Old", "oldseed");
        SaveBinding.Saving(3); // the saver writes the record into the save now, the old file entry is dropped
        Assert.Equal("Old", SaveBinding.LoadedRecord.Slot);
        SaveBinding.LoadedRecord = null;
        Assert.Null(SaveBinding.CurrentEntry);
    }
    [Fact]
    public void TheSaveWrittenAtTheEndOfTheTutorialDoesNotUndoTheFinishedTutorial()
    {
        // the game saves when the tutorial ends, before the hub loads, and loads that save into the hub right after
        var state = SessionState.Load("Nia", "seedN");
        ApClient.State = state;
        var stored = new Playthrough { TutorialDone = false, ReceivedIndex = 0 };
        state.ApplyPlaythrough(stored);
        TutorialTracker.InHub = () => true;
        try
        {
            TutorialTracker.SaveApplied();
        }
        finally
        {
            TutorialTracker.InHub = () => false;
        }

        Assert.True(state.TutorialDone);
        Assert.True(TutorialTracker.Done);
    }

    [Fact]
    public void AStoredScoutTableFillsTheEmptiedCacheAgain()
    {
        // refusing another save empties the table; the next save of the slot gets the stored copy back
        var entry = new ScoutEntry { LocationId = 99, ItemId = 7200001, ItemName = "Some Item", PlayerName = "Quester" };
        ScoutCache.Clear();
        Assert.False(ScoutCache.HasEntries);

        ScoutCache.Restore(new[] { entry });

        Assert.True(ScoutCache.HasEntries);
        Assert.Equal("Some Item", ScoutCache.Get(99).Name);
        ScoutCache.Clear();
    }

    [Fact]
    public void TheSkipTutorialButtonIsOfferedOnlyInARandomizerGameWithAnUnfinishedTutorial()
    {
        ApClient.State = null;
        Assert.False(SkipTutorialOption.Wanted); // no randomizer on this save

        var state = SessionState.Load("Pat", "seedP");
        ApClient.State = state;
        state.ApplyPlaythrough(new Playthrough { TutorialDone = false });
        Assert.True(SkipTutorialOption.Wanted);

        TutorialTracker.Complete();
        Assert.False(SkipTutorialOption.Wanted); // finished, the button goes away
    }

    [Fact]
    public void TheSkipQuestionIsShownOnlyWhileTheSkipButtonOpenedIt()
    {
        SkipTutorialOption.OptionChosen(SkipTutorialOption.DefinitionName);
        Assert.True(SkipTutorialOption.Pending);

        SkipTutorialOption.OptionChosen("ReturnToCheckPoint_UIOpt"); // "Load Latest Save" opens the same panel
        Assert.False(SkipTutorialOption.Pending);

        SkipTutorialOption.OptionChosen(SkipTutorialOption.DefinitionName);
        SkipTutorialOption.OptionChosen(null); // the question was answered
        Assert.False(SkipTutorialOption.Pending);
    }

    [Fact]
    public void ASaveLoadedOutsideTheHubKeepsWhatItStores()
    {
        var state = SessionState.Load("Ola", "seedO");
        ApClient.State = state;
        state.ApplyPlaythrough(new Playthrough { TutorialDone = false });

        TutorialTracker.SaveApplied(); // the main menu, not the hub, and the hub progress is 0

        Assert.False(state.TutorialDone);
    }

    [Fact]
    public void ProgressCanBeSwitchedBetweenSaves()
    {
        var state = SessionState.Load("Liv", "seedL");
        state.ReceivedIndex = 30;
        state.UnlockedChapters.Add("Level2");
        var older = new Playthrough { ReceivedIndex = 10 };

        state.ApplyPlaythrough(older);

        Assert.Equal(10, state.ReceivedIndex);
        Assert.Empty(state.UnlockedChapters);
        Assert.Equal(30, new Playthrough { ReceivedIndex = 30 }.ReceivedIndex);
    }

    [Fact]
    public void UnresolvedItemsTravelWithTheSave()
    {
        var state = SessionState.Load("Mia", "seedM");
        state.Unresolved.Add(new UnresolvedItem { ItemId = 7, Name = "Odd Item", Reason = "missing" });

        var other = SessionState.Load("Mia", "seedM2");
        other.ApplyPlaythrough(Deserialize(state).Progress);

        Assert.Equal("Odd Item", Assert.Single(other.Unresolved).Name);
    }
    [Fact]
    public void SlotDataSurvivesAJsonRoundTrip()
    {
        var raw = new System.Collections.Generic.Dictionary<string, object>
        {
            ["world_version"] = "0.3.0", ["dango_count"] = 7L, ["death_link"] = true,
            ["level_order"] = HomuraHimeAP.Core.WorldRules.VanillaOrder.Reverse().ToArray(),
        };

        var slot = SlotData.FromJson(SlotData.ToJson(raw));

        Assert.Equal("0.3.0", slot.WorldVersion);
        Assert.Equal(7, slot.DangoCount);
        Assert.True(slot.DeathLink);
        Assert.Equal(HomuraHimeAP.Core.WorldRules.VanillaOrder.Reverse(), slot.LevelOrder);
    }
}
