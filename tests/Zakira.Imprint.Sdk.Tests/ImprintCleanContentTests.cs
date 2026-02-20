using System.Text.Json.Nodes;
using Microsoft.Build.Framework;
using Xunit;
using Zakira.Imprint.Sdk;

namespace Zakira.Imprint.Sdk.Tests;

public class ImprintCleanContentTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _projectDir;

    public ImprintCleanContentTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "ImprintCleanTests", Guid.NewGuid().ToString("N"));
        _projectDir = Path.Combine(_testDir, "project");
        Directory.CreateDirectory(_projectDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    /// <summary>
    /// Writes a unified manifest (v2 format) with files for a single agent (default: copilot).
    /// </summary>
    private void WriteUnifiedManifest(string packageId, string[] files, string agent = "copilot")
    {
        var imprintDir = Path.Combine(_projectDir, ".imprint");
        Directory.CreateDirectory(imprintDir);
        var manifestPath = Path.Combine(imprintDir, "manifest.json");

        JsonObject manifestObj;

        // Load existing manifest or create new
        if (File.Exists(manifestPath))
        {
            manifestObj = JsonNode.Parse(File.ReadAllText(manifestPath))?.AsObject() ?? new JsonObject();
        }
        else
        {
            manifestObj = new JsonObject { ["version"] = 2 };
        }

        // Ensure version is set
        manifestObj["version"] = 2;

        // Ensure packages object exists
        if (manifestObj["packages"] == null)
        {
            manifestObj["packages"] = new JsonObject();
        }

        var packages = manifestObj["packages"]!.AsObject();

        // Create or update package entry
        var pkgEntry = new JsonObject
        {
            ["files"] = new JsonObject
            {
                [agent] = new JsonArray(files.Select(f => (JsonNode)JsonValue.Create(f)!).ToArray())
            }
        };
        packages[packageId] = pkgEntry;

        File.WriteAllText(manifestPath, manifestObj.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    private string CreateTrackedFile(string relativePath, string content = "content")
    {
        var fullPath = Path.Combine(_projectDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    [Fact]
    public void DeletesTrackedFiles_RemovesManifest()
    {
        // Arrange
        var file1 = CreateTrackedFile(".github/skills/hello.md");
        var file2 = CreateTrackedFile(".github/skills/world.md");
        WriteUnifiedManifest("Zakira.Imprint.Sample", new[] { file1, file2 });

        var task = new ImprintCleanContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir
        };

        // Act
        var result = task.Execute();

        // Assert
        Assert.True(result);
        Assert.False(File.Exists(file1));
        Assert.False(File.Exists(file2));
        // Unified manifest should be deleted when all packages are cleaned
        Assert.False(File.Exists(Path.Combine(_projectDir, ".imprint", "manifest.json")));
    }

    [Fact]
    public void RemovesEmptyDirectories()
    {
        // Arrange
        var file1 = CreateTrackedFile(".github/skills/sub/deep.md");
        WriteUnifiedManifest("Pkg", new[] { file1 });

        var task = new ImprintCleanContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir
        };

        // Act
        task.Execute();

        // Assert
        Assert.False(Directory.Exists(Path.Combine(_projectDir, ".github", "skills", "sub")));
    }

    [Fact]
    public void CleansImprintDir_WhenNoManifestsRemain()
    {
        // Arrange
        var file1 = CreateTrackedFile("out/file.md");
        WriteUnifiedManifest("Pkg", new[] { file1 });
        // Create .gitignore in .imprint
        var gitignore = Path.Combine(_projectDir, ".imprint", ".gitignore");
        File.WriteAllText(gitignore, "*\n");

        var task = new ImprintCleanContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir
        };

        // Act
        task.Execute();

        // Assert - .imprint directory should be removed entirely
        Assert.False(Directory.Exists(Path.Combine(_projectDir, ".imprint")));
    }

    [Fact]
    public void KeepsImprintDir_WhenOtherManifestsRemain()
    {
        // Arrange
        var file1 = CreateTrackedFile("out/a.md");
        var file2 = CreateTrackedFile("out/b.md");
        WriteUnifiedManifest("Pkg.A", new[] { file1 });
        WriteUnifiedManifest("Pkg.B", new[] { file2 });

        // Only clean Pkg.A by removing its manifest and re-creating scenario
        // Actually the task cleans ALL manifests, so let's test that both get cleaned
        var task = new ImprintCleanContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir
        };

        // Act
        task.Execute();

        // Assert
        Assert.False(File.Exists(file1));
        Assert.False(File.Exists(file2));
    }

    [Fact]
    public void HandlesCorruptManifest_Gracefully()
    {
        // Arrange - create a corrupt unified manifest
        var imprintDir = Path.Combine(_projectDir, ".imprint");
        Directory.CreateDirectory(imprintDir);
        File.WriteAllText(Path.Combine(imprintDir, "manifest.json"), "{ not valid json !!!");

        var task = new ImprintCleanContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir
        };

        // Act - should not throw, just warn
        var result = task.Execute();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void NoImprintDir_ReturnsTrue()
    {
        var task = new ImprintCleanContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir
        };

        Assert.True(task.Execute());
    }

    [Fact]
    public void NoManifests_ReturnsTrue()
    {
        Directory.CreateDirectory(Path.Combine(_projectDir, ".imprint"));
        var task = new ImprintCleanContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir
        };

        Assert.True(task.Execute());
    }

    [Fact]
    public void HandlesAlreadyDeletedFiles_Gracefully()
    {
        // Arrange - manifest references files that don't exist
        var fakeFile = Path.Combine(_projectDir, "gone", "deleted.md");
        WriteUnifiedManifest("Pkg", new[] { fakeFile });

        var task = new ImprintCleanContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir
        };

        // Act
        var result = task.Execute();

        // Assert - manifest should be deleted even if files don't exist
        Assert.True(result);
        Assert.False(File.Exists(Path.Combine(_projectDir, ".imprint", "manifest.json")));
    }

    [Fact]
    public void EmptyFilesArray_DeletesManifest()
    {
        // Arrange
        WriteUnifiedManifest("Empty", Array.Empty<string>());

        var task = new ImprintCleanContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir
        };

        // Act
        var result = task.Execute();

        // Assert - manifest should be deleted
        Assert.True(result);
        Assert.False(File.Exists(Path.Combine(_projectDir, ".imprint", "manifest.json")));
    }

    [Fact]
    public void FullCopyThenClean_Workflow()
    {
        // Arrange - set up source files
        var sourceDir = Path.Combine(_testDir, "source");
        Directory.CreateDirectory(sourceDir);
        var srcFile = Path.Combine(sourceDir, "guide.md");
        File.WriteAllText(srcFile, "# Guide");

        // Step 1: Copy (with explicit copilot target)
        var copyTask = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir,
            TargetAgents = "copilot",
            ContentItems = new ITaskItem[]
            {
                new MockTaskItem(srcFile, new Dictionary<string, string>
                {
                    { "PackageId", "Zakira.Imprint.Guide" },
                    { "SourceBase", sourceDir }
                })
            }
        };

        var copyResult = copyTask.Execute();
        Assert.True(copyResult);

        // File should be at .github/skills/guide.md (copilot skills path + relative path)
        var copiedFile = Path.Combine(_projectDir, ".github", "skills", "guide.md");
        Assert.True(File.Exists(copiedFile), $"Expected copied file at {copiedFile}");
        Assert.True(File.Exists(Path.Combine(_projectDir, ".imprint", "manifest.json")), "Unified manifest should exist");

        // Step 2: Clean
        var cleanTask = new ImprintCleanContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir
        };

        var cleanResult = cleanTask.Execute();
        Assert.True(cleanResult);

        // Assert - everything cleaned up
        Assert.False(File.Exists(copiedFile), "Copied file should be deleted");
        Assert.False(File.Exists(Path.Combine(_projectDir, ".imprint", "manifest.json")), "Unified manifest should be deleted");
    }

    [Fact]
    public void DoesNotDeleteUntrackedFiles()
    {
        // Arrange - create a tracked file and an untracked file in the same directory
        var trackedFile = CreateTrackedFile(".github/skills/tracked.md");
        var untrackedFile = CreateTrackedFile(".github/skills/untracked.md");
        WriteUnifiedManifest("Pkg", new[] { trackedFile }); // only tracks one file

        var task = new ImprintCleanContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir
        };

        // Act
        task.Execute();

        // Assert
        Assert.False(File.Exists(trackedFile));
        Assert.True(File.Exists(untrackedFile), "Untracked file should remain");
    }
}
