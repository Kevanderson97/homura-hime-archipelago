using System.IO;
using HomuraHimeAP.Core;
using Xunit;

namespace HomuraHimeAP.Tests;

public class SessionStoreTests
{
    public SessionStoreTests() => TestEnvironment.Ensure();

    private static string NewPath() => Path.Combine(TestEnvironment.TempDirectory, $"store_{Path.GetRandomFileName()}.json");

    [Fact]
    public void WritingReplacesTheFileAndKeepsTheOldVersionAsBackup()
    {
        var path = NewPath();
        SessionStore.WriteAtomic(path, "one");
        SessionStore.WriteAtomic(path, "two");

        Assert.Equal("two", File.ReadAllText(path));
        Assert.Equal("one", File.ReadAllText(path + ".bak"));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void ACorruptFileFallsBackToTheBackup()
    {
        var path = NewPath();
        SessionStore.WriteAtomic(path, "good");
        SessionStore.WriteAtomic(path, "good2");
        File.WriteAllText(path, "garbage");

        Assert.Equal("good", SessionStore.Read(path, text => text.StartsWith("good")));
    }

    [Fact]
    public void MissingFilesReadAsNull()
    {
        Assert.Null(SessionStore.Read(NewPath(), _ => true));
    }

    [Fact]
    public void AFailedWriteLeavesTheOldContent()
    {
        var path = NewPath();
        SessionStore.WriteAtomic(path, "keep");
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.ThrowsAny<IOException>(() => SessionStore.WriteAtomic(path, "lost"));

        Assert.Equal("keep", File.ReadAllText(path));
        Assert.False(File.Exists(path + ".tmp"));
    }
}
