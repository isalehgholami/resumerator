using ResumeMaker.Core.Domain;
using ResumeMaker.Infrastructure;
using ResumeMaker.Infrastructure.Storage;

namespace ResumeMaker.Infrastructure.Tests;

public class StorageTests
{
    [Fact]
    public void AppPaths_UsesEnvOverride()
    {
        var old = Environment.GetEnvironmentVariable(AppPaths.DataEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(AppPaths.DataEnvVar, Path.GetTempPath() + "rm-override");
            Assert.EndsWith("rm-override", AppPaths.Default().Root);
            Environment.SetEnvironmentVariable(AppPaths.DataEnvVar, null);
            Assert.EndsWith("ResumeMaker", AppPaths.Default().Root);
        }
        finally { Environment.SetEnvironmentVariable(AppPaths.DataEnvVar, old); }
    }

    [Fact]
    public async Task Repository_RoundTrip_AndIndex()
    {
        using var dir = new TempDir();
        var repo = new JsonProfileRepository(dir.Paths);
        var p = Samples.Profile("fa", "علی");
        await repo.SaveAsync(p);

        var loaded = await repo.GetAsync(p.Id);
        Assert.NotNull(loaded);
        Assert.Equal("علی", loaded!.Contact.FullName);
        Assert.Equal(p.Sections.Count, loaded.Sections.Count);
        Assert.Equal(SectionType.Experience, loaded.Sections[0].Type);

        var list = await repo.ListAsync();
        Assert.Single(list);
        Assert.Equal(p.Id, list[0].Id);

        var bytes = File.ReadAllBytes(dir.Paths.ProfilePath(p.Id));
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "no BOM");
        Assert.Empty(Directory.GetFiles(dir.Paths.ProfilesDir, "*.tmp"));
        Assert.True(File.Exists(dir.Paths.ProfileIndexPath));
    }

    [Fact]
    public async Task Repository_Delete()
    {
        using var dir = new TempDir();
        var repo = new JsonProfileRepository(dir.Paths);
        var p = Samples.Profile();
        await repo.SaveAsync(p);
        Assert.True(await repo.DeleteAsync(p.Id));
        Assert.False(await repo.DeleteAsync(p.Id));
        Assert.Null(await repo.GetAsync(p.Id));
        Assert.Empty(await repo.ListAsync());
    }

    [Fact]
    public async Task Repository_ToleratesCorruptAndDeletedFiles()
    {
        using var dir = new TempDir();
        var repo = new JsonProfileRepository(dir.Paths);
        var a = Samples.Profile(name: "A");
        var b = Samples.Profile(name: "B");
        var c = Samples.Profile(name: "C");
        await repo.SaveAsync(a);
        await repo.SaveAsync(b);
        await repo.SaveAsync(c);

        File.WriteAllText(dir.Paths.ProfilePath(b.Id), "{ not json");
        File.Delete(dir.Paths.ProfilePath(c.Id)); // manually deleted, index is stale

        var list = await repo.ListAsync();
        Assert.Single(list);
        Assert.Equal(a.Id, list[0].Id);
        Assert.Null(await repo.GetAsync(b.Id));

        File.WriteAllText(dir.Paths.ProfileIndexPath, "garbage");
        Assert.Single(await new JsonProfileRepository(dir.Paths).ListAsync());
    }

    [Fact]
    public async Task Settings_DefaultsWhenMissingOrCorrupt_AndRoundTrip()
    {
        using var dir = new TempDir();
        var store = new JsonSettingsStore(dir.Paths);
        Assert.Equal(new AppSettings(), await store.LoadAsync());

        File.WriteAllText(dir.Paths.SettingsPath, "}}}");
        Assert.Equal(10, (await store.LoadAsync()).BackupMaxCount);

        await store.SaveAsync(new AppSettings { Theme = "dark", BackupMaxCount = 3, ChromePath = "/x" });
        var s = await store.LoadAsync();
        Assert.Equal("dark", s.Theme);
        Assert.Equal(3, s.BackupMaxCount);
        Assert.Equal("/x", s.ChromePath);
    }

    [Fact]
    public async Task Backup_SkipsUnchanged_AndPrunesOldest()
    {
        using var dir = new TempDir();
        var settings = new JsonSettingsStore(dir.Paths);
        await settings.SaveAsync(new AppSettings { BackupMaxCount = 3 });
        var backup = new FileBackupService(dir.Paths, settings);
        var p = Samples.Profile();

        var first = await backup.SnapshotAsync(p);
        Assert.NotNull(first);
        Assert.Null(await backup.SnapshotAsync(p)); // unchanged

        var paths = new List<string> { first! };
        for (var i = 0; i < 5; i++)
            paths.Add((await backup.SnapshotAsync(p with { Summary = "v" + i }))!);

        var files = Directory.GetFiles(dir.Paths.BackupsDir, p.Id + ".*.json");
        Assert.Equal(3, files.Length);
        Assert.DoesNotContain(paths[0], files);
        Assert.Contains(paths[^1], files);
        Assert.Null(await backup.SnapshotAsync(p with { Summary = "v4" }));
    }
}
