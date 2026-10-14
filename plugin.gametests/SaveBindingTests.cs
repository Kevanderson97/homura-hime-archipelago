using System.Collections;
using System.Linq;
using HomuraHimeAP.Archipelago;
using HomuraHimeAP.Core;
using Surity;

namespace HomuraHimeAP.GameTests;

/// <summary>
/// Binding of game saves to Archipelago slots (audit 2.1, 2.4): offline play of a bound save, adopting a save without a
/// record, refusing a save that belongs to another seed. The game's own save calls are stood in for by the binding's events.
/// </summary>
[TestClass]
public class SaveBindingTests
{
    private const int TestSaveSlot = 7;

    private static void Forget()
    {
        SaveBinding.Deleted(TestSaveSlot);
        SaveBinding.NewGame();
    }

    [Test]
    public IEnumerator ASaveWithoutARecordBelongsToTheConnectedSlot() => Harness.Guard(nameof(ASaveWithoutARecordBelongsToTheConnectedSlot), Adopts());

    private static IEnumerator Adopts()
    {
        yield return Harness.Connect();
        try
        {
            SaveBinding.Loaded(TestSaveSlot);
            Assert.IsTrue(ApClient.Active, "the save was not adopted");
            Assert.Equal(0, ApClient.State.ReceivedIndex, "received items of an adopted save");
        }
        finally
        {
            Forget();
        }
    }

    [Test]
    public IEnumerator ASaveOfAnotherSeedIsRefused() => Harness.Guard(nameof(ASaveOfAnotherSeedIsRefused), Refuses());

    private static IEnumerator Refuses()
    {
        yield return Harness.Connect();
        var own = ApClient.State;
        try
        {
            // a record made by a different slot and seed
            var foreign = SaveBinding.Serialize(new SessionState { Slot = "Stranger", Seed = "another-seed" });

            SaveBinding.LoadStarted(); // as the saver does when the game loads a save that carries the record
            SaveBinding.RecordLoaded(foreign);
            SaveBinding.Loaded(TestSaveSlot);
            Assert.IsTrue(!ApClient.Active, "a foreign save must not be played as a randomizer game");
            Assert.IsTrue(ApClient.Refusal != null, "no reason was given");

            // the checks of a refused save must not reach the slot
            Assert.IsTrue(!ApClient.IsConnected || ApClient.State == null, "no session state while refused");
        }
        finally
        {
            Forget();
        }

        Assert.IsTrue(ApClient.Active, "a new game on the connection must work again");
    }

    [Test]
    public IEnumerator ABoundSaveKeepsWorkingWithoutAConnection() => Harness.Guard(nameof(ABoundSaveKeepsWorkingWithoutAConnection), Offline());

    private static IEnumerator Offline()
    {
        yield return Harness.Connect();
        Harness.FreshSession();
        SaveBinding.Loaded(TestSaveSlot); // adopts
        SaveBinding.Saving(TestSaveSlot); // and records the binding, as the game's save does

        var target = Catalog.AllLocations.First(l => ScoutCache.Get(l.Id) is { IsOwn: true } && l.Category == "senbei"
                                                     && !ApClient.Session.Locations.AllLocationsChecked.Contains(l.Id));
        try
        {
            ApClient.Disconnect();
            Assert.IsTrue(!ApClient.IsConnected, "still connected");
            Assert.IsTrue(ApClient.Active, "a bound save must stay a randomizer game offline");
            Assert.IsTrue(ScoutCache.Get(target.Id) != null, "the stored scout table was not restored");

            ApClient.CheckLocation(target.Id);
            Assert.IsTrue(ApClient.State.CheckedLocations.Contains(target.Id), "the check was not recorded offline");

            ApClient.Connect(Harness.Server, Harness.Slot, "");
            yield return Harness.WaitFor(() => ApClient.IsConnected, "the connection");
            yield return OtherPlayer.WaitUntil(() => ApClient.Session.Locations.AllLocationsChecked.Contains(target.Id), "the offline check to reach the server");
        }
        finally
        {
            Forget();
        }
    }

    [Test]
    public IEnumerator TheSaverWritesAndReadsTheRecordThroughTheGamesSerialization() => Harness.Guard(nameof(TheSaverWritesAndReadsTheRecordThroughTheGamesSerialization), SaverRoundTrip());

    private static IEnumerator SaverRoundTrip()
    {
        yield return Harness.Connect();
        ApClient.State.ReceivedIndex = 41;
        var host = new UnityEngine.GameObject("saver test");
        try
        {
            var saver = host.AddComponent<ApSaver>();
            var serialized = saver.SerializeSaveData();

            SaveBinding.LoadStarted();
            saver.DeserializeAndLoadSaveData(serialized);

            var record = SaveBinding.LoadedRecord;
            Assert.IsTrue(record != null, "the record did not come back");
            Assert.Equal(Harness.Slot, record.Slot, "slot");
            Assert.Equal(41, record.Progress.ReceivedIndex, "received index");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            SaveBinding.LoadedRecord = null;
        }
    }

    [Test]
    public IEnumerator AnUnboundSaveIsVanillaWithoutAConnection() => Harness.Guard(nameof(AnUnboundSaveIsVanillaWithoutAConnection), Vanilla());

    private static IEnumerator Vanilla()
    {
        yield return Harness.Connect();
        ApClient.Disconnect();
        Assert.IsTrue(!ApClient.Active, "an unbound game is not a randomizer game");
    }
}
