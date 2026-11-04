using HomuraHimeAP.Core;
using Xunit;

namespace HomuraHimeAP.Tests;

public class HubGateTests
{
    private const string Hub = "HubWorld_Main";

    // the gate's triggers stand within a metre of (0, 2, 65.4); these are three of the thirty, as the hub scene has them
    [Theory]
    [InlineData("TriggerAction", 0f, 2f, 65.0f)]                                  // PortalDialog_TriggerAction
    [InlineData("TriggerAction", 0f, 2f, 65.8f)]                                  // PortalDialogStage_..._Departure
    [InlineData("TriggerAction DE Departure", 0f, 2f, 65.0f)]
    [InlineData("TriggerAction DE BeforeDeparture", 0f, 2f, 65.0f)]
    [InlineData("TriggerAction_Dialog_StageBase_大鳥居 (商人對話前)", 0f, 2f, 65.2f)] // before the merchant's dialogue
    [InlineData("TriggerAction_Dialog_StageBase_大鳥居 (任務完成前)", 0f, 2f, 65.2f)] // before the mission is complete
    public void EveryTriggerAtTheGateOpensTheChapterSelectWithFreeformKeys(string name, float x, float y, float z)
    {
        Assert.True(HubGate.OpensChapterSelect(UnlockMode.Freeform, Hub, name, x, y, z));
    }

    [Theory]
    [InlineData("Off")]
    [InlineData("Linear")]
    public void OtherKeyModesKeepTheGameGate(string mode)
    {
        Assert.False(HubGate.OpensChapterSelect(System.Enum.Parse<UnlockMode>(mode), Hub, "TriggerAction", 0f, 2f, 65.0f));
    }

    [Theory]
    [InlineData("HubWorld_PostGame_Main")]   // the post-game hub's gate does it by itself
    [InlineData("Level_1_Area_1_Main")]
    [InlineData("HubWorld_TrainingGround_Main")]
    [InlineData("")]
    public void OnlyTheHubIsAffected(string scene)
    {
        Assert.False(HubGate.OpensChapterSelect(UnlockMode.Freeform, scene, "TriggerAction", 0f, 2f, 65.0f));
    }

    [Theory]
    [InlineData("NPC_MVC_Controller", 5.4f, 2.1f, 65.4f)]                       // an NPC next to the gate, even at the gate's spot
    [InlineData("NPC_MVC_Controller", 0.0f, 2.0f, 62.3f)]
    [InlineData("TriggerAction", 5.3f, 2.0f, 6.5f)]                             // the training ground door
    [InlineData("TriggerAction", -9.7f, 2.0f, 0.5f)]
    [InlineData("TriggerAction", 0.0f, 3.2f, 5.7f)]                             // the front door
    [InlineData("PortalController", 0.0f, 2.0f, 65.0f)]
    [InlineData(null, 0.0f, 2.0f, 65.0f)]
    public void OtherInteractionsOfTheHubAreLeftAlone(string name, float x, float y, float z)
    {
        Assert.False(HubGate.OpensChapterSelect(UnlockMode.Freeform, Hub, name, x, y, z));
    }
}
