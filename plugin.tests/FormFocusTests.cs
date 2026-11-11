using HomuraHimeAP.Core;
using Xunit;

namespace HomuraHimeAP.Tests;

public class FormFocusTests
{
    [Fact]
    public void TabGoesThroughEverythingAndAround()
    {
        var order = new[] { FormFocus.Server, FormFocus.Slot, FormFocus.Password, FormFocus.Connect, FormFocus.Disconnect, FormFocus.Command };
        var focus = FormFocus.Server;
        foreach (var expected in order)
        {
            Assert.Equal(expected, focus);
            focus = FormFocus.Next(focus, true, true);
        }

        Assert.Equal(FormFocus.Server, focus);
        Assert.Equal(FormFocus.Command, FormFocus.Previous(FormFocus.Server, true, true));
        Assert.Equal(FormFocus.Password, FormFocus.Previous(FormFocus.Connect, true, true));
    }

    [Fact]
    public void DisconnectIsSkippedWhileThereIsNothingToDisconnect()
    {
        Assert.Equal(FormFocus.Command, FormFocus.Next(FormFocus.Connect, false, true));
        Assert.Equal(FormFocus.Connect, FormFocus.Previous(FormFocus.Command, false, true));
        Assert.Equal(FormFocus.Server, FormFocus.Down(FormFocus.Connect, false));
        Assert.Equal(FormFocus.Connect, FormFocus.Up(FormFocus.Server, false));
        Assert.Equal(FormFocus.Disconnect, FormFocus.Down(FormFocus.Connect, true));
        Assert.Equal(FormFocus.Disconnect, FormFocus.Up(FormFocus.Server, true));
    }

    [Fact]
    public void TheCommandBoxIsSkippedWhileThereIsNoConnection()
    {
        // not connected: the Disconnect button and the command box are both unusable, Tab goes round the first four things only
        var focus = FormFocus.Server;
        var seen = new System.Collections.Generic.List<int>();
        for (var i = 0; i < 6; i++)
        {
            seen.Add(focus);
            focus = FormFocus.Next(focus, false, false);
        }

        Assert.Equal(new[] { FormFocus.Server, FormFocus.Slot, FormFocus.Password, FormFocus.Connect, FormFocus.Server, FormFocus.Slot }, seen);
        Assert.Equal(FormFocus.Connect, FormFocus.Previous(FormFocus.Server, false, false));
        Assert.Equal(FormFocus.Server, FormFocus.Next(FormFocus.Connect, false, false));
    }

    [Fact]
    public void RightDoesNotGoToACommandBoxThatCannotBeUsed()
    {
        Assert.Equal(FormFocus.Slot, FormFocus.Right(FormFocus.Slot, false, false)); // stays where it is
        Assert.Equal(FormFocus.Connect, FormFocus.Right(FormFocus.Connect, false, false));
        Assert.Equal(FormFocus.Command, FormFocus.Right(FormFocus.Slot, false, true));
        Assert.Equal(FormFocus.Disconnect, FormFocus.Right(FormFocus.Connect, true, false)); // the neighbour button still counts
    }

    [Fact]
    public void UpAndDownStayInTheLeftColumn()
    {
        Assert.Equal(FormFocus.Slot, FormFocus.Down(FormFocus.Server, true));
        Assert.Equal(FormFocus.Server, FormFocus.Up(FormFocus.Slot, true));
        Assert.Equal(FormFocus.Server, FormFocus.Down(FormFocus.Disconnect, true)); // around
        Assert.Equal(FormFocus.Command, FormFocus.Down(FormFocus.Command, true)); // nothing below or above the command box
        Assert.Equal(FormFocus.Command, FormFocus.Up(FormFocus.Command, true));
    }

    [Fact]
    public void RightGoesToTheCommandBoxAndLeftComesBackToWhereTheColumnWasLeft()
    {
        foreach (var from in new[] { FormFocus.Server, FormFocus.Slot, FormFocus.Password, FormFocus.Disconnect })
        {
            Assert.Equal(FormFocus.Command, FormFocus.Right(from, true, true));
            Assert.Equal(from, FormFocus.Left(FormFocus.Command, from, true));
        }

        Assert.Equal(FormFocus.Command, FormFocus.Right(FormFocus.Command, true, true));
    }

    [Fact]
    public void ConnectAndDisconnectAreNeighboursWhenDisconnectCanBeUsed()
    {
        Assert.Equal(FormFocus.Disconnect, FormFocus.Right(FormFocus.Connect, true, true));
        Assert.Equal(FormFocus.Command, FormFocus.Right(FormFocus.Connect, false, true)); // nothing to move to: on to the command box
        Assert.Equal(FormFocus.Connect, FormFocus.Left(FormFocus.Disconnect, FormFocus.Server, true));
        Assert.Equal(FormFocus.Server, FormFocus.Left(FormFocus.Server, FormFocus.Slot, true)); // nothing at the left of a field
    }

    [Fact]
    public void ADisconnectThatCannotBeUsedIsNotComeBackTo()
    {
        Assert.Equal(FormFocus.Connect, FormFocus.Left(FormFocus.Command, FormFocus.Disconnect, false));
        Assert.Equal(FormFocus.Server, FormFocus.Usable(FormFocus.Command, true)); // the command box is not part of the left column
        Assert.Equal(FormFocus.Server, FormFocus.Usable(-1, true));
        Assert.Equal(FormFocus.Slot, FormFocus.Usable(FormFocus.Slot, false));
    }

    [Fact]
    public void OnlyTheThreeFirstThingsAreFields()
    {
        for (var focus = 0; focus < FormFocus.Count; focus++) Assert.Equal(focus <= FormFocus.Password, FormFocus.IsField(focus));
    }
}
