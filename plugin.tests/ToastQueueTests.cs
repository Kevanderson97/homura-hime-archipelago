using System.Collections.Generic;
using HomuraHimeAP.UI;
using UnityEngine;
using Xunit;

namespace HomuraHimeAP.Tests;

public class ToastQueueTests
{
    private static Toast Item(string text, bool important = false) => new() { Text = text, Color = Color.white, IsItem = true, Important = important };

    private static List<string> Drain(ToastQueue queue)
    {
        var shown = new List<string>();
        while (queue.TryDequeue(out var toast)) shown.Add(toast.Text);
        return shown;
    }

    [Fact]
    public void ToastsComeOutInOrder()
    {
        var queue = new ToastQueue();
        queue.Enqueue(Item("a"));
        queue.Enqueue(Item("b"));
        Assert.Equal(new[] { "a", "b" }, Drain(queue));
    }

    [Fact]
    public void AFloodOfFillerIsSummedUp()
    {
        var queue = new ToastQueue();
        for (var i = 0; i < 500; i++) queue.Enqueue(Item("filler " + i));

        var shown = Drain(queue);

        Assert.Equal(ToastQueue.Capacity + 1, shown.Count);
        Assert.Equal($"+{500 - ToastQueue.Capacity} more items", shown[^1]);
    }

    [Fact]
    public void ImportantItemsAreNotCollapsedWhileTheQueueHasRoomForThem()
    {
        var queue = new ToastQueue();
        for (var i = 0; i < ToastQueue.Capacity; i++) queue.Enqueue(Item("filler"));
        queue.Enqueue(Item("progression", important: true));

        Assert.Contains("progression", Drain(queue));
    }

    [Fact]
    public void ImportantItemsStopAtTheHardLimit()
    {
        var queue = new ToastQueue();
        for (var i = 0; i < ToastQueue.HardLimit + 50; i++) queue.Enqueue(Item("key", important: true));
        Assert.Equal(ToastQueue.HardLimit, queue.Count);
    }

    [Fact]
    public void StatusMessagesAreNeverFilteredOrCollapsed()
    {
        var queue = new ToastQueue { Verbosity = ToastVerbosity.Off };
        queue.Enqueue(new Toast { Text = "Connected", Important = true });
        Assert.Equal(new[] { "Connected" }, Drain(queue));
    }

    [Fact]
    public void VerbosityFiltersItemToasts()
    {
        var queue = new ToastQueue { Verbosity = ToastVerbosity.Important };
        queue.Enqueue(Item("filler"));
        queue.Enqueue(Item("progression", important: true));
        Assert.Equal(new[] { "progression" }, Drain(queue));

        queue.Verbosity = ToastVerbosity.Off;
        queue.Enqueue(Item("progression", important: true));
        Assert.Empty(Drain(queue));
    }
}
