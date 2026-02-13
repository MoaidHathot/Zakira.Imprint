using System.Text.Json.Nodes;
using Microsoft.Build.Framework;
using Xunit;
using Zakira.Imprint.Sdk;

namespace Zakira.Imprint.Sdk.Tests;

public class ImprintCopyContentTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _sourceDir;
    private readonly string _destDir;

    public ImprintCopyContentTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "ImprintTests", Guid.NewGuid().ToString("N"));
        _sourceDir = Path.Combine(_testDir, "source");
        _destDir = Path.Combine(_testDir, "project");
        Directory.CreateDirectory(_sourceDir);
        Directory.CreateDirectory(_destDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    private string CreateSourceFile(string relativePath, string content = "test content")
    {
        var fullPath = Path.Combine(_sourceDir, relativePath);
        var dir = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    private MockTaskItem CreateContentItem(string sourceFile, string packageId, string destinationBase, string sourceBase)
    {
        return new MockTaskItem(sourceFile, new Dictionary<string, string>
        {
            { "PackageId", packageId },
            { "DestinationBase", destinationBase },
            { "SourceBase", sourceBase }
        });
    }

    [Fact]
    public void CopiesSingleFile_CreatesManifest()
    {
        // Arrange
        var src = CreateSourceFile("hello.md", "# Hello");
        var destBase = Path.Combine(_destDir, ".github", "skills"); // legacy metadata, ignored by task
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            ContentItems = new ITaskItem[]
            {
                CreateContentItem(src, "Zakira.Imprint.Sample", destBase, _sourceDir)
            }
        };

        // Act
        var result = task.Execute();

        // Assert — files go to {projectDir}/.github/skills/{relativePath}
        Assert.True(result);
        var expectedDest = Path.Combine(_destDir, ".github", "skills", "hello.md");
        Assert.True(File.Exists(expectedDest), $"Expected file at {expectedDest}");
        Assert.Equal("# Hello", File.ReadAllText(expectedDest));

        // Verify legacy per-package manifest
        var manifestPath = Path.Combine(_destDir, ".imprint", "Zakira.Imprint.Sample.manifest");
        Assert.True(File.Exists(manifestPath), "Legacy manifest should exist");
        var manifestDoc = JsonNode.Parse(File.ReadAllText(manifestPath));
        Assert.Equal("Zakira.Imprint.Sample", manifestDoc!["packageId"]!.GetValue<string>());
        var files = manifestDoc["files"]!.AsArray();
        Assert.Single(files);

        // Verify unified manifest
        var unifiedPath = Path.Combine(_destDir, ".imprint", "manifest.json");
        Assert.True(File.Exists(unifiedPath), "Unified manifest should exist");
        var unified = JsonNode.Parse(File.ReadAllText(unifiedPath));
        Assert.Equal(2, unified!["version"]!.GetValue<int>());
        Assert.NotNull(unified["packages"]!["Zakira.Imprint.Sample"]!["files"]!["copilot"]);
    }

    [Fact]
    public void CopiesMultipleFiles_SamePackage()
    {
        // Arrange
        var src1 = CreateSourceFile("file1.md", "content1");
        var src2 = CreateSourceFile("sub/file2.md", "content2");
        var destBase = Path.Combine(_destDir, ".github", "skills"); // legacy metadata, ignored by task
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            ContentItems = new ITaskItem[]
            {
                CreateContentItem(src1, "MyPkg", destBase, _sourceDir),
                CreateContentItem(src2, "MyPkg", destBase, _sourceDir)
            }
        };

        // Act
        var result = task.Execute();

        // Assert — files go to {projectDir}/.github/skills/{relativePath}
        var skillsDir = Path.Combine(_destDir, ".github", "skills");
        Assert.True(result);
        Assert.True(File.Exists(Path.Combine(skillsDir, "file1.md")));
        Assert.True(File.Exists(Path.Combine(skillsDir, "sub", "file2.md")));

        var manifestPath = Path.Combine(_destDir, ".imprint", "MyPkg.manifest");
        var manifestDoc = JsonNode.Parse(File.ReadAllText(manifestPath));
        Assert.Equal(2, manifestDoc!["files"]!.AsArray().Count);
    }

    [Fact]
    public void CopiesFiles_MultiplePackages_SeparateManifests()
    {
        // Arrange
        var src1 = CreateSourceFile("pkg1/file1.md", "pkg1 content");
        var src2 = CreateSourceFile("pkg2/file2.md", "pkg2 content");
        var destBase = Path.Combine(_destDir, ".github", "skills"); // legacy metadata, ignored by task
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            ContentItems = new ITaskItem[]
            {
                CreateContentItem(src1, "Package.One", destBase, _sourceDir),
                CreateContentItem(src2, "Package.Two", destBase, _sourceDir)
            }
        };

        // Act
        var result = task.Execute();

        // Assert
        Assert.True(result);
        Assert.True(File.Exists(Path.Combine(_destDir, ".imprint", "Package.One.manifest")));
        Assert.True(File.Exists(Path.Combine(_destDir, ".imprint", "Package.Two.manifest")));
        // Unified manifest should also exist
        Assert.True(File.Exists(Path.Combine(_destDir, ".imprint", "manifest.json")));
    }

    [Fact]
    public void CreatesGitignore_InImprintDir()
    {
        // Arrange
        var src = CreateSourceFile("file.md");
        var destBase = Path.Combine(_destDir, ".github", "skills"); // legacy metadata, ignored by task
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            ContentItems = new ITaskItem[]
            {
                CreateContentItem(src, "Pkg", destBase, _sourceDir)
            }
        };

        // Act
        task.Execute();

        // Assert
        var gitignorePath = Path.Combine(_destDir, ".imprint", ".gitignore");
        Assert.True(File.Exists(gitignorePath));
        var content = File.ReadAllText(gitignorePath);
        Assert.Contains("*", content);
    }

    [Fact]
    public void SkipItem_MissingPackageId()
    {
        // Arrange
        var src = CreateSourceFile("file.md");
        var destBase = Path.Combine(_destDir, ".github", "skills");
        var item = new MockTaskItem(src, new Dictionary<string, string>
        {
            { "DestinationBase", destBase },
            { "SourceBase", _sourceDir }
            // No PackageId
        });
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            ContentItems = new ITaskItem[] { item }
        };

        // Act
        var result = task.Execute();

        // Assert - succeeds but skips the item, no manifest written
        Assert.True(result);
        Assert.False(Directory.Exists(Path.Combine(_destDir, ".imprint")) &&
                     Directory.GetFiles(Path.Combine(_destDir, ".imprint"), "*.manifest").Length > 0);
    }

    [Fact]
    public void SucceedsWithoutDestinationBase()
    {
        // DestinationBase is no longer used by the multi-agent copy task;
        // destinations are determined by AgentConfig. This test verifies
        // items without DestinationBase are still copied successfully.
        var src = CreateSourceFile("file.md", "test content");
        var item = new MockTaskItem(src, new Dictionary<string, string>
        {
            { "PackageId", "Pkg" },
            { "SourceBase", _sourceDir }
        });
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            ContentItems = new ITaskItem[] { item }
        };

        var result = task.Execute();
        Assert.True(result);
        // File should be copied to copilot's skills path
        var expectedDest = Path.Combine(_destDir, ".github", "skills", "file.md");
        Assert.True(File.Exists(expectedDest), $"Expected file at {expectedDest}");
    }

    [Fact]
    public void SkipItem_MissingSourceBase()
    {
        var src = CreateSourceFile("file.md");
        var destBase = Path.Combine(_destDir, ".github", "skills");
        var item = new MockTaskItem(src, new Dictionary<string, string>
        {
            { "PackageId", "Pkg" },
            { "DestinationBase", destBase }
        });
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            ContentItems = new ITaskItem[] { item }
        };

        var result = task.Execute();
        Assert.True(result);
    }

    [Fact]
    public void SkipItem_SourceFileNotFound()
    {
        var destBase = Path.Combine(_destDir, ".github", "skills");
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            ContentItems = new ITaskItem[]
            {
                CreateContentItem(
                    Path.Combine(_sourceDir, "nonexistent.md"),
                    "Pkg", destBase, _sourceDir)
            }
        };

        var result = task.Execute();
        Assert.True(result);
    }

    [Fact]
    public void EmptyContentItems_ReturnsTrue()
    {
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            ContentItems = Array.Empty<ITaskItem>()
        };

        Assert.True(task.Execute());
    }

    [Fact]
    public void OverwritesExistingFile()
    {
        // Arrange - create an existing destination file where the agent will copy to
        var src = CreateSourceFile("file.md", "new content");
        var destBase = Path.Combine(_destDir, ".github", "skills"); // legacy metadata, ignored by task
        var destFile = Path.Combine(_destDir, ".github", "skills", "file.md");
        Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
        File.WriteAllText(destFile, "old content");

        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            ContentItems = new ITaskItem[]
            {
                CreateContentItem(src, "Pkg", destBase, _sourceDir)
            }
        };

        // Act
        task.Execute();

        // Assert
        Assert.Equal("new content", File.ReadAllText(destFile));
    }

    [Fact]
    public void PreservesSubdirectoryStructure()
    {
        // Arrange
        var src1 = CreateSourceFile("a/b/c/deep.md", "deep");
        var src2 = CreateSourceFile("a/top.md", "top");
        var destBase = Path.Combine(_destDir, ".github", "skills"); // not used by task, but kept for item creation
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            ContentItems = new ITaskItem[]
            {
                CreateContentItem(src1, "Pkg", destBase, _sourceDir),
                CreateContentItem(src2, "Pkg", destBase, _sourceDir)
            }
        };

        // Act
        task.Execute();

        // Assert — files go to {projectDir}/.github/skills/{relativePath}
        var skillsDir = Path.Combine(_destDir, ".github", "skills");
        Assert.True(File.Exists(Path.Combine(skillsDir, "a", "b", "c", "deep.md")));
        Assert.True(File.Exists(Path.Combine(skillsDir, "a", "top.md")));
        Assert.Equal("deep", File.ReadAllText(Path.Combine(skillsDir, "a", "b", "c", "deep.md")));
    }

    [Fact]
    public void IdempotentExecution_SecondRunSameResult()
    {
        // Arrange
        var src = CreateSourceFile("file.md", "content");
        var destBase = Path.Combine(_destDir, ".github", "skills"); // legacy metadata, ignored by task
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            ContentItems = new ITaskItem[]
            {
                CreateContentItem(src, "Pkg", destBase, _sourceDir)
            }
        };

        // Act - run twice
        task.Execute();
        var result = task.Execute();

        // Assert
        Assert.True(result);
        var destFile = Path.Combine(_destDir, ".github", "skills", "file.md");
        Assert.True(File.Exists(destFile));
        Assert.Equal("content", File.ReadAllText(destFile));

        // Manifest should still be valid
        var manifestPath = Path.Combine(_destDir, ".imprint", "Pkg.manifest");
        var manifestDoc = JsonNode.Parse(File.ReadAllText(manifestPath));
        Assert.Single(manifestDoc!["files"]!.AsArray());
    }

    #region Prefix Configuration Tests

    [Fact]
    public void PrefixSkills_WhenEnabled_AppliesPackageIdAsPrefix()
    {
        // Arrange
        var src = CreateSourceFile("MySkill/SKILL.md", "skill content");
        var destBase = Path.Combine(_destDir, ".github", "skills");
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            PrefixSkills = true, // Enable global prefix
            ContentItems = new ITaskItem[]
            {
                CreateContentItem(src, "Zakira.SomePackage", destBase, _sourceDir)
            }
        };

        // Act
        var result = task.Execute();

        // Assert - file should be prefixed with PackageId
        Assert.True(result);
        var expectedDest = Path.Combine(_destDir, ".github", "skills", "Zakira.SomePackage", "MySkill", "SKILL.md");
        Assert.True(File.Exists(expectedDest), $"Expected file at {expectedDest}");
    }

    [Fact]
    public void PrefixSkills_WhenDisabled_NoPrefix()
    {
        // Arrange
        var src = CreateSourceFile("MySkill/SKILL.md", "skill content");
        var destBase = Path.Combine(_destDir, ".github", "skills");
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            PrefixSkills = false, // Disabled (default)
            ContentItems = new ITaskItem[]
            {
                CreateContentItem(src, "Zakira.SomePackage", destBase, _sourceDir)
            }
        };

        // Act
        var result = task.Execute();

        // Assert - file should NOT be prefixed
        Assert.True(result);
        var expectedDest = Path.Combine(_destDir, ".github", "skills", "MySkill", "SKILL.md");
        Assert.True(File.Exists(expectedDest), $"Expected file at {expectedDest}");
    }

    [Fact]
    public void ImprintPrefix_MetadataOverride_AppliesCustomPrefix()
    {
        // Arrange
        var src = CreateSourceFile("MySkill/SKILL.md", "skill content");
        var destBase = Path.Combine(_destDir, ".github", "skills");
        var item = new MockTaskItem(src, new Dictionary<string, string>
        {
            { "PackageId", "Zakira.SomePackage" },
            { "DestinationBase", destBase },
            { "SourceBase", _sourceDir },
            { "ImprintPrefix", "CustomPrefix" } // Consumer override
        });
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            PrefixSkills = false, // Global setting doesn't matter when ImprintPrefix is set
            ContentItems = new ITaskItem[] { item }
        };

        // Act
        var result = task.Execute();

        // Assert - file should use the custom prefix
        Assert.True(result);
        var expectedDest = Path.Combine(_destDir, ".github", "skills", "CustomPrefix", "MySkill", "SKILL.md");
        Assert.True(File.Exists(expectedDest), $"Expected file at {expectedDest}");
    }

    [Fact]
    public void SuggestedPrefix_UsedWhenPrefixEnabled()
    {
        // Arrange
        var src = CreateSourceFile("MySkill/SKILL.md", "skill content");
        var destBase = Path.Combine(_destDir, ".github", "skills");
        var item = new MockTaskItem(src, new Dictionary<string, string>
        {
            { "PackageId", "Zakira.VeryLongPackageName" },
            { "DestinationBase", destBase },
            { "SourceBase", _sourceDir },
            { "SuggestedPrefix", "Short" } // Author's suggested prefix
        });
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            PrefixSkills = true, // Enable global prefix
            ContentItems = new ITaskItem[] { item }
        };

        // Act
        var result = task.Execute();

        // Assert - file should use author's suggested prefix
        Assert.True(result);
        var expectedDest = Path.Combine(_destDir, ".github", "skills", "Short", "MySkill", "SKILL.md");
        Assert.True(File.Exists(expectedDest), $"Expected file at {expectedDest}");
    }

    [Fact]
    public void ImprintUsePrefix_OverridesGlobalSetting()
    {
        // Arrange
        var src = CreateSourceFile("MySkill/SKILL.md", "skill content");
        var destBase = Path.Combine(_destDir, ".github", "skills");
        var item = new MockTaskItem(src, new Dictionary<string, string>
        {
            { "PackageId", "Zakira.SomePackage" },
            { "DestinationBase", destBase },
            { "SourceBase", _sourceDir },
            { "ImprintUsePrefix", "true" } // Per-package override
        });
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            PrefixSkills = false, // Global is OFF
            ContentItems = new ITaskItem[] { item }
        };

        // Act
        var result = task.Execute();

        // Assert - file should be prefixed despite global setting being off
        Assert.True(result);
        var expectedDest = Path.Combine(_destDir, ".github", "skills", "Zakira.SomePackage", "MySkill", "SKILL.md");
        Assert.True(File.Exists(expectedDest), $"Expected file at {expectedDest}");
    }

    [Fact]
    public void ImprintEnabled_WhenFalse_SkipsPackage()
    {
        // Arrange
        var src = CreateSourceFile("MySkill/SKILL.md", "skill content");
        var destBase = Path.Combine(_destDir, ".github", "skills");
        var item = new MockTaskItem(src, new Dictionary<string, string>
        {
            { "PackageId", "Zakira.DisabledPackage" },
            { "DestinationBase", destBase },
            { "SourceBase", _sourceDir },
            { "ImprintEnabled", "false" } // Disable this package
        });
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            ContentItems = new ITaskItem[] { item }
        };

        // Act
        var result = task.Execute();

        // Assert - no files should be copied
        Assert.True(result);
        var skillsDir = Path.Combine(_destDir, ".github", "skills");
        Assert.False(Directory.Exists(skillsDir) && Directory.GetFiles(skillsDir, "*", SearchOption.AllDirectories).Length > 0,
            "No skill files should be copied when ImprintEnabled=false");
    }

    [Fact]
    public void DefaultPrefix_UsedWhenSet()
    {
        // Arrange
        var src = CreateSourceFile("MySkill/SKILL.md", "skill content");
        var destBase = Path.Combine(_destDir, ".github", "skills");
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            PrefixSkills = true,
            DefaultPrefix = "GlobalDefault", // Global default prefix
            ContentItems = new ITaskItem[]
            {
                CreateContentItem(src, "Zakira.SomePackage", destBase, _sourceDir)
            }
        };

        // Act
        var result = task.Execute();

        // Assert - file should use global default prefix, not package ID
        Assert.True(result);
        var expectedDest = Path.Combine(_destDir, ".github", "skills", "GlobalDefault", "MySkill", "SKILL.md");
        Assert.True(File.Exists(expectedDest), $"Expected file at {expectedDest}");
    }

    #endregion

    #region Gitignore Management Tests

    [Fact]
    public void CreatesGranularGitignore_InSkillDirectories()
    {
        // Arrange
        var src1 = CreateSourceFile("StringUtils/SKILL.md", "skill1");
        var src2 = CreateSourceFile("StringUtils/helper.py", "helper");
        var destBase = Path.Combine(_destDir, ".github", "skills");
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            ContentItems = new ITaskItem[]
            {
                CreateContentItem(src1, "Zakira.Sample", destBase, _sourceDir),
                CreateContentItem(src2, "Zakira.Sample", destBase, _sourceDir)
            }
        };

        // Act
        var result = task.Execute();

        // Assert
        Assert.True(result);
        var gitignorePath = Path.Combine(_destDir, ".github", "skills", "StringUtils", ".gitignore");
        Assert.True(File.Exists(gitignorePath), $"Expected gitignore at {gitignorePath}");

        var content = File.ReadAllText(gitignorePath);
        Assert.Contains("# Managed by Zakira.Imprint (Zakira.Sample)", content);
        Assert.Contains("SKILL.md", content);
        Assert.Contains("helper.py", content);
    }

    [Fact]
    public void Gitignore_ListsOnlySpecificFiles_NotBlanketWildcard()
    {
        // Arrange
        var src = CreateSourceFile("MySkill/SKILL.md", "skill content");
        var destBase = Path.Combine(_destDir, ".github", "skills");
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            ContentItems = new ITaskItem[]
            {
                CreateContentItem(src, "Zakira.Sample", destBase, _sourceDir)
            }
        };

        // Act
        task.Execute();

        // Assert
        var gitignorePath = Path.Combine(_destDir, ".github", "skills", "MySkill", ".gitignore");
        var content = File.ReadAllText(gitignorePath);

        // Should list specific file, not a wildcard
        Assert.Contains("SKILL.md", content);
        Assert.DoesNotContain("\n*\n", content); // No blanket wildcard
    }

    [Fact]
    public void Gitignore_MultiplePackages_SeparateSections()
    {
        // Arrange
        var src1 = CreateSourceFile("SharedSkill/file1.md", "content1");
        var src2 = CreateSourceFile("SharedSkill/file2.md", "content2");
        var destBase = Path.Combine(_destDir, ".github", "skills");

        var item1 = new MockTaskItem(src1, new Dictionary<string, string>
        {
            { "PackageId", "Package.One" },
            { "DestinationBase", destBase },
            { "SourceBase", _sourceDir },
            { "ImprintPrefix", "Prefix1" }
        });
        var item2 = new MockTaskItem(src2, new Dictionary<string, string>
        {
            { "PackageId", "Package.Two" },
            { "DestinationBase", destBase },
            { "SourceBase", _sourceDir },
            { "ImprintPrefix", "Prefix2" }
        });

        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            ContentItems = new ITaskItem[] { item1, item2 }
        };

        // Act
        task.Execute();

        // Assert - each prefixed directory should have its own gitignore
        var gitignore1 = Path.Combine(_destDir, ".github", "skills", "Prefix1", "SharedSkill", ".gitignore");
        var gitignore2 = Path.Combine(_destDir, ".github", "skills", "Prefix2", "SharedSkill", ".gitignore");

        Assert.True(File.Exists(gitignore1));
        Assert.True(File.Exists(gitignore2));

        var content1 = File.ReadAllText(gitignore1);
        Assert.Contains("# Managed by Zakira.Imprint (Package.One)", content1);

        var content2 = File.ReadAllText(gitignore2);
        Assert.Contains("# Managed by Zakira.Imprint (Package.Two)", content2);
    }

    #endregion

    #region Conflict Detection Tests

    [Fact]
    public void DestinationConflict_TwoPackagesSameDestination_Errors()
    {
        // Arrange - two packages trying to copy to same destination
        var src1 = CreateSourceFile("pkg1/SharedSkill/SKILL.md", "content1");
        var src2 = CreateSourceFile("pkg2/SharedSkill/SKILL.md", "content2");
        var destBase = Path.Combine(_destDir, ".github", "skills");

        // Without prefixes, both would go to SharedSkill/SKILL.md
        var item1 = new MockTaskItem(src1, new Dictionary<string, string>
        {
            { "PackageId", "Package.One" },
            { "DestinationBase", destBase },
            { "SourceBase", Path.Combine(_sourceDir, "pkg1") }
        });
        var item2 = new MockTaskItem(src2, new Dictionary<string, string>
        {
            { "PackageId", "Package.Two" },
            { "DestinationBase", destBase },
            { "SourceBase", Path.Combine(_sourceDir, "pkg2") }
        });

        var mockEngine = new MockBuildEngine();
        var task = new ImprintCopyContent
        {
            BuildEngine = mockEngine,
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            ContentItems = new ITaskItem[] { item1, item2 }
        };

        // Act
        var result = task.Execute();

        // Assert - should fail with conflict error
        Assert.False(result, "Task should fail when destination conflict detected");
        Assert.Contains(mockEngine.Errors, e => e.Contains("Destination conflict"));
    }

    [Fact]
    public void DestinationConflict_SamePackage_NoError()
    {
        // Arrange - same package with multiple items to same destination (edge case, but allowed)
        var src = CreateSourceFile("SharedSkill/SKILL.md", "content");
        var destBase = Path.Combine(_destDir, ".github", "skills");

        var item1 = CreateContentItem(src, "Package.One", destBase, _sourceDir);
        var item2 = CreateContentItem(src, "Package.One", destBase, _sourceDir); // Same package, same file

        var mockEngine = new MockBuildEngine();
        var task = new ImprintCopyContent
        {
            BuildEngine = mockEngine,
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            ContentItems = new ITaskItem[] { item1, item2 }
        };

        // Act
        var result = task.Execute();

        // Assert - should succeed (same package owns both)
        Assert.True(result);
    }

    #endregion

    #region Incremental Cleanup Tests

    [Fact]
    public void CleanRemovedPackages_RemovesFilesFromUnreferencedPackage()
    {
        // Arrange - First build with Package.One
        var src1 = CreateSourceFile("SkillA/SKILL.md", "content from pkg1");
        var destBase = Path.Combine(_destDir, ".github", "skills");
        var task1 = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            ContentItems = new ITaskItem[]
            {
                CreateContentItem(src1, "Package.One", destBase, _sourceDir)
            }
        };

        var result1 = task1.Execute();
        Assert.True(result1);

        // Verify Package.One files exist
        var skillFile = Path.Combine(_destDir, ".github", "skills", "SkillA", "SKILL.md");
        Assert.True(File.Exists(skillFile));
        Assert.True(File.Exists(Path.Combine(_destDir, ".imprint", "Package.One.manifest")));

        // Act - Second build WITHOUT Package.One (simulate package removal)
        var src2 = CreateSourceFile("SkillB/SKILL.md", "content from pkg2");
        var task2 = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            ContentItems = new ITaskItem[]
            {
                CreateContentItem(src2, "Package.Two", destBase, _sourceDir)
            }
        };

        var result2 = task2.Execute();
        Assert.True(result2);

        // Assert - Package.One files should be removed
        Assert.False(File.Exists(skillFile), "Package.One file should be cleaned up");
        Assert.False(File.Exists(Path.Combine(_destDir, ".imprint", "Package.One.manifest")), 
            "Package.One manifest should be cleaned up");
        
        // Package.Two files should exist
        var skillFile2 = Path.Combine(_destDir, ".github", "skills", "SkillB", "SKILL.md");
        Assert.True(File.Exists(skillFile2), "Package.Two file should exist");
    }

    [Fact]
    public void CleanRemovedPackages_RemovesEmptyDirectories()
    {
        // Arrange - First build with Package.One in a deep directory structure
        var src1 = CreateSourceFile("DeepSkill/Sub/SKILL.md", "content");
        var destBase = Path.Combine(_destDir, ".github", "skills");
        var task1 = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            ContentItems = new ITaskItem[]
            {
                CreateContentItem(src1, "Package.One", destBase, _sourceDir)
            }
        };

        task1.Execute();

        var deepDir = Path.Combine(_destDir, ".github", "skills", "DeepSkill", "Sub");
        Assert.True(Directory.Exists(deepDir));

        // Act - Build without Package.One
        var task2 = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            ContentItems = Array.Empty<ITaskItem>()
        };

        // Need to have at least a manifest from previous run
        task2.Execute();

        // Assert - Empty directories should be cleaned up
        Assert.False(Directory.Exists(deepDir), "Empty deep directory should be removed");
        Assert.False(Directory.Exists(Path.Combine(_destDir, ".github", "skills", "DeepSkill")), 
            "Empty parent directory should be removed");
    }

    [Fact]
    public void CleanRemovedPackages_CleansGitignoreEntries()
    {
        // Arrange - First build with Package.One
        var src1 = CreateSourceFile("SkillA/SKILL.md", "content");
        var src2 = CreateSourceFile("SkillA/helper.py", "helper");
        var destBase = Path.Combine(_destDir, ".github", "skills");
        var task1 = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            ContentItems = new ITaskItem[]
            {
                CreateContentItem(src1, "Package.One", destBase, _sourceDir),
                CreateContentItem(src2, "Package.One", destBase, _sourceDir)
            }
        };

        task1.Execute();

        var gitignorePath = Path.Combine(_destDir, ".github", "skills", "SkillA", ".gitignore");
        Assert.True(File.Exists(gitignorePath));
        var gitignoreContent = File.ReadAllText(gitignorePath);
        Assert.Contains("Package.One", gitignoreContent);

        // Act - Build without Package.One
        var src3 = CreateSourceFile("SkillB/SKILL.md", "content2");
        var task2 = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            ContentItems = new ITaskItem[]
            {
                CreateContentItem(src3, "Package.Two", destBase, _sourceDir)
            }
        };

        task2.Execute();

        // Assert - Gitignore in SkillA should be deleted (since it's now empty)
        Assert.False(File.Exists(gitignorePath), 
            "Gitignore should be deleted when package is removed and directory is empty");
    }

    [Fact]
    public void CleanRemovedPackages_PreservesOtherPackageGitignoreEntries()
    {
        // Arrange - First build with two packages in same directory
        var src1 = CreateSourceFile("SharedSkill/file1.md", "content1");
        var src2 = CreateSourceFile("SharedSkill/file2.md", "content2");
        var destBase = Path.Combine(_destDir, ".github", "skills");

        var item1 = new MockTaskItem(src1, new Dictionary<string, string>
        {
            { "PackageId", "Package.One" },
            { "DestinationBase", destBase },
            { "SourceBase", _sourceDir }
        });
        var item2 = new MockTaskItem(src2, new Dictionary<string, string>
        {
            { "PackageId", "Package.Two" },
            { "DestinationBase", destBase },
            { "SourceBase", _sourceDir }
        });

        var task1 = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            ContentItems = new ITaskItem[] { item1, item2 }
        };

        task1.Execute();

        // Verify both packages in gitignore
        var gitignorePath = Path.Combine(_destDir, ".github", "skills", "SharedSkill", ".gitignore");
        var initialContent = File.ReadAllText(gitignorePath);
        Assert.Contains("Package.One", initialContent);
        Assert.Contains("Package.Two", initialContent);

        // Act - Build with only Package.Two (Package.One removed)
        var task2 = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            ContentItems = new ITaskItem[] { item2 }
        };

        task2.Execute();

        // Assert - Only Package.Two should remain in gitignore
        var finalContent = File.ReadAllText(gitignorePath);
        Assert.DoesNotContain("Package.One", finalContent);
        Assert.Contains("Package.Two", finalContent);
        Assert.Contains("file2.md", finalContent);
    }

    [Fact]
    public void CleanRemovedPackages_NoManifest_DoesNothing()
    {
        // Arrange - No previous manifest exists
        var src = CreateSourceFile("Skill/SKILL.md", "content");
        var destBase = Path.Combine(_destDir, ".github", "skills");
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            ContentItems = new ITaskItem[]
            {
                CreateContentItem(src, "Package.One", destBase, _sourceDir)
            }
        };

        // Act - Should succeed even with no prior manifest
        var result = task.Execute();

        // Assert
        Assert.True(result);
        Assert.True(File.Exists(Path.Combine(_destDir, ".github", "skills", "Skill", "SKILL.md")));
    }

    [Fact]
    public void CleanRemovedPackages_LegacyV1Manifest_SkipsCleanup()
    {
        // Arrange - Create a v1 manifest manually
        var imprintDir = Path.Combine(_destDir, ".imprint");
        Directory.CreateDirectory(imprintDir);
        var manifestPath = Path.Combine(imprintDir, "manifest.json");
        File.WriteAllText(manifestPath, @"{""version"": 1, ""files"": []}");

        var src = CreateSourceFile("Skill/SKILL.md", "content");
        var destBase = Path.Combine(_destDir, ".github", "skills");
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            ContentItems = new ITaskItem[]
            {
                CreateContentItem(src, "Package.One", destBase, _sourceDir)
            }
        };

        // Act - Should succeed without errors (v1 manifest is skipped)
        var result = task.Execute();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CleanRemovedPackages_LogsCleanupMessage()
    {
        // Arrange - First build
        var src1 = CreateSourceFile("SkillA/SKILL.md", "content");
        var destBase = Path.Combine(_destDir, ".github", "skills");
        var task1 = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            ContentItems = new ITaskItem[]
            {
                CreateContentItem(src1, "Package.ToRemove", destBase, _sourceDir)
            }
        };
        task1.Execute();

        // Act - Second build without Package.ToRemove
        var mockEngine = new MockBuildEngine();
        var task2 = new ImprintCopyContent
        {
            BuildEngine = mockEngine,
            ProjectDirectory = _destDir,
            TargetAgents = "copilot",
            ContentItems = Array.Empty<ITaskItem>()
        };
        task2.Execute();

        // Assert - Should log cleanup message
        Assert.Contains(mockEngine.Messages, m => m.Contains("Cleaned up removed package 'Package.ToRemove'"));
    }

    #endregion
}
