using System.IO;
using HomuraHimeAP.Core;
using Xunit;

namespace HomuraHimeAP.Tests;

public class SessionStateTests
{
    public SessionStateTests() => TestEnvironment.Ensure();

    [Fact]
    public void StateIsSavedPerSlotAndSeed()
    {
        var state = SessionState.Load("Alice", "seed1");
        state.ReceivedIndex = 7;
        state.CheckedLocations.Add(7300000);
        state.UnlockedChapters.Add("Level2");
        state.ClearedWorlds.Add("Level1");
        state.BankedUltimateCharge = 2;
        state.Save();
        SessionState.FlushAll();

        var again = SessionState.Load("Alice", "seed1");
        Assert.Equal(7, again.ReceivedIndex);
        Assert.Contains(7300000L, again.CheckedLocations);
        Assert.Contains("Level2", again.UnlockedChapters);
        Assert.Contains("Level1", again.ClearedWorlds);
        Assert.Equal(2, again.BankedUltimateCharge);

        Assert.Equal(0, SessionState.Load("Alice", "other-seed").ReceivedIndex);
        Assert.Equal(0, SessionState.Load("Bob", "seed1").ReceivedIndex);
    }

    [Fact]
    public void NewGameStartsOverButKeepsCheckedLocations()
    {
        var state = SessionState.Load("Carol", "seed2");
        state.ReceivedIndex = 5;
        state.UnlockedChapters.Add("Level3");
        state.ClearedWorlds.Add("Level3");
        state.BankedUltimateCharge = 1;
        state.CheckedLocations.Add(7300001);
        state.CompletedSubChapters.Add("Level1_Area_1_Entrance");

        state.ResetForNewGame();

        Assert.Equal(0, state.ReceivedIndex);
        Assert.Empty(state.UnlockedChapters);
        Assert.Empty(state.ClearedWorlds);
        Assert.Equal(0, state.BankedUltimateCharge);
        Assert.Empty(state.CompletedSubChapters);
        Assert.Contains(7300001L, state.CheckedLocations); // what the server already knows stays known
    }

    [Fact]
    public void UnreadableFileStartsFresh()
    {
        var path = Path.Combine(TestEnvironment.TempDirectory, "session_Dave_seed3.json");
        File.WriteAllText(path, "{ this is not json");
        var state = SessionState.Load("Dave", "seed3");
        Assert.Equal(0, state.ReceivedIndex);
        Assert.Empty(state.CheckedLocations);
    }

    [Fact]
    public void SlotNamesWithOddCharactersMakeSafeFileNames()
    {
        var state = SessionState.Load("A/B:C*?", "se ed");
        state.ReceivedIndex = 1;
        state.Save();
        SessionState.FlushAll();
        Assert.Equal(1, SessionState.Load("A/B:C*?", "se ed").ReceivedIndex);
    }

    [Fact]
    public void ProgressFlagsBelongToTheSession()
    {
        var state = SessionState.Load("Erin", "seed4");
        state.TutorialDone = true;
        state.CompletedSubChapters.Add("Level1_Area_1_Entrance");
        state.Save();
        SessionState.FlushAll();

        var again = SessionState.Load("Erin", "seed4");
        Assert.True(again.TutorialDone);
        Assert.Contains("Level1_Area_1_Entrance", again.CompletedSubChapters);
        Assert.Equal(SessionState.CurrentVersion, again.Version);
    }

    [Fact]
    public void FilesFromBeforeVersioningTakeOverTheSidecarFlags()
    {
        SaveState.Current.TutorialDone = true;
        SaveState.Current.CompletedSubChapters.Add("Level2_Area_1_Entrance");
        var path = Path.Combine(TestEnvironment.TempDirectory, "session_Finn_seed5.json");
        File.WriteAllText(path, "{\"Slot\":\"Finn\",\"Seed\":\"seed5\",\"ReceivedIndex\":3,\"CheckedLocations\":[7300002]}");

        var state = SessionState.Load("Finn", "seed5");

        Assert.Equal(3, state.ReceivedIndex);
        Assert.True(state.TutorialDone);
        Assert.Contains("Level2_Area_1_Entrance", state.CompletedSubChapters);
        Assert.Equal(SessionState.CurrentVersion, state.Version);
        SaveState.Current.TutorialDone = false;
        SaveState.Current.CompletedSubChapters.Clear();
    }

    [Fact]
    public void ChangesAreWrittenAfterTheDelayAndMergedIntoOneWrite()
    {
        var now = 100f;
        SessionState.Clock = () => now;
        var state = SessionState.Load("Gus", "seed6");
        var path = Path.Combine(TestEnvironment.TempDirectory, "session_Gus_seed6.json");

        state.ReceivedIndex = 1; state.Save();
        state.ReceivedIndex = 2; state.Save();
        SessionState.Tick(now + SessionState.SaveDelaySeconds / 2);
        Assert.False(File.Exists(path));

        SessionState.Tick(now + SessionState.SaveDelaySeconds);
        Assert.Equal(2, SessionState.Load("Gus", "seed6").ReceivedIndex);
    }
}
