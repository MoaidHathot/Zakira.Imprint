using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Build.Framework;
using Xunit;
using Zakira.Imprint.Sdk;

namespace Zakira.Imprint.Sdk.Tests;

/// <summary>
/// Integration tests exercising multi-agent copy, merge, and clean workflows.
/// These verify that Imprint correctly distributes files and MCP servers
/// to multiple agents (copilot, claude, cursor) simultaneously.
/// </summary>
public class MultiAgentTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _sourceDir;
    private readonly string _projectDir;

    public MultiAgentTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "ImprintMultiAgent", Guid.NewGuid().ToString("N"));
        _sourceDir = Path.Combine(_testDir, "source");
        _projectDir = Path.Combine(_testDir, "project");
        Directory.CreateDirectory(_sourceDir);
        Directory.CreateDirectory(_projectDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    #region Helpers

    private string CreateSourceFile(string relativePath, string content = "test content")
    {
        var fullPath = Path.Combine(_sourceDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    private MockTaskItem CreateContentItem(string sourceFile, string packageId)
    {
        return new MockTaskItem(sourceFile, new Dictionary<string, string>
        {
            { "PackageId", packageId },
            { "SourceBase", _sourceDir }
        });
    }

    private string CreateMcpFragment(string serverKey)
    {
        var fragDir = Path.Combine(_testDir, "fragments");
        Directory.CreateDirectory(fragDir);
        var path = Path.Combine(fragDir, $"{serverKey}.mcp.json");
        var json = $$"""
        {
          "servers": {
            "{{serverKey}}": {
              "type": "stdio",
              "command": "npx",
              "args": ["-y", "@example/{{serverKey}}"]
            }
          }
        }
        """;
        File.WriteAllText(path, json);
        return path;
    }

    private string CreateMultiServerFragment(string name, params string[] serverKeys)
    {
        var fragDir = Path.Combine(_testDir, "fragments");
        Directory.CreateDirectory(fragDir);
        var path = Path.Combine(fragDir, $"{name}.mcp.json");
        var serversObj = new JsonObject();
        foreach (var key in serverKeys)
        {
            serversObj[key] = new JsonObject
            {
                ["type"] = "stdio",
                ["command"] = "npx",
                ["args"] = new JsonArray("-y", $"@example/{key}")
            };
        }
        var doc = new JsonObject { ["servers"] = serversObj };
        File.WriteAllText(path, doc.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    #endregion

    // ===================================================================
    // COPY TASK: Multi-Agent Tests
    // ===================================================================

    #region Copy - Multi-Agent

    [Fact]
    public void Copy_TwoAgents_FilesAppearInBothLocations()
    {
        var src = CreateSourceFile("guide.md", "# Guide");
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir,
            TargetAgents = "copilot;claude",
            ContentItems = new ITaskItem[] { CreateContentItem(src, "Zakira.Imprint.Sample") }
        };

        var result = task.Execute();

        Assert.True(result);
        var copilotFile = Path.Combine(_projectDir, ".github", "skills", "guide.md");
        var claudeFile = Path.Combine(_projectDir, ".claude", "skills", "guide.md");
        Assert.True(File.Exists(copilotFile), $"Expected copilot file at {copilotFile}");
        Assert.True(File.Exists(claudeFile), $"Expected claude file at {claudeFile}");
        Assert.Equal("# Guide", File.ReadAllText(copilotFile));
        Assert.Equal("# Guide", File.ReadAllText(claudeFile));
    }

    [Fact]
    public void Copy_ThreeAgents_FilesAppearInAllLocations()
    {
        var src = CreateSourceFile("skill.md", "# Skill");
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir,
            TargetAgents = "copilot;claude;cursor",
            ContentItems = new ITaskItem[] { CreateContentItem(src, "Zakira.Imprint.Sample") }
        };

        var result = task.Execute();

        Assert.True(result);
        Assert.True(File.Exists(Path.Combine(_projectDir, ".github", "skills", "skill.md")), "copilot path");
        Assert.True(File.Exists(Path.Combine(_projectDir, ".claude", "skills", "skill.md")), "claude path");
        Assert.True(File.Exists(Path.Combine(_projectDir, ".cursor", "rules", "skill.md")), "cursor path");
    }

    [Fact]
    public void Copy_TwoAgents_PreservesSubdirectoryStructure()
    {
        var src1 = CreateSourceFile("a/deep.md", "deep");
        var src2 = CreateSourceFile("top.md", "top");
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir,
            TargetAgents = "copilot;claude",
            ContentItems = new ITaskItem[]
            {
                CreateContentItem(src1, "Pkg"),
                CreateContentItem(src2, "Pkg")
            }
        };

        task.Execute();

        // Copilot
        Assert.True(File.Exists(Path.Combine(_projectDir, ".github", "skills", "a", "deep.md")));
        Assert.True(File.Exists(Path.Combine(_projectDir, ".github", "skills", "top.md")));
        // Claude
        Assert.True(File.Exists(Path.Combine(_projectDir, ".claude", "skills", "a", "deep.md")));
        Assert.True(File.Exists(Path.Combine(_projectDir, ".claude", "skills", "top.md")));
    }

    [Fact]
    public void Copy_TwoAgents_UnifiedManifest_TracksPerAgent()
    {
        var src = CreateSourceFile("guide.md", "# Guide");
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir,
            TargetAgents = "copilot;claude",
            ContentItems = new ITaskItem[] { CreateContentItem(src, "Zakira.Imprint.Sample") }
        };

        task.Execute();

        var manifestPath = Path.Combine(_projectDir, ".imprint", "manifest.json");
        Assert.True(File.Exists(manifestPath), "Unified manifest should exist");

        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!;
        Assert.Equal(2, manifest["version"]!.GetValue<int>());

        var pkg = manifest["packages"]!["Zakira.Imprint.Sample"]!;
        var copilotFiles = pkg["files"]!["copilot"]!.AsArray();
        var claudeFiles = pkg["files"]!["claude"]!.AsArray();

        Assert.Single(copilotFiles);
        Assert.Single(claudeFiles);
        Assert.Contains(".github", copilotFiles[0]!.GetValue<string>());
        Assert.Contains(".claude", claudeFiles[0]!.GetValue<string>());
    }

    [Fact]
    public void Copy_MultiplePackages_TwoAgents()
    {
        var src1 = CreateSourceFile("pkg1/file.md", "pkg1");
        var src2 = CreateSourceFile("pkg2/file.md", "pkg2");
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir,
            TargetAgents = "copilot;cursor",
            ContentItems = new ITaskItem[]
            {
                CreateContentItem(src1, "Package.One"),
                CreateContentItem(src2, "Package.Two")
            }
        };

        var result = task.Execute();

        Assert.True(result);

        // Copilot
        Assert.True(File.Exists(Path.Combine(_projectDir, ".github", "skills", "pkg1", "file.md")));
        Assert.True(File.Exists(Path.Combine(_projectDir, ".github", "skills", "pkg2", "file.md")));
        // Cursor
        Assert.True(File.Exists(Path.Combine(_projectDir, ".cursor", "rules", "pkg1", "file.md")));
        Assert.True(File.Exists(Path.Combine(_projectDir, ".cursor", "rules", "pkg2", "file.md")));

        // Unified manifest has both packages
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(_projectDir, ".imprint", "manifest.json")))!;
        Assert.NotNull(manifest["packages"]!["Package.One"]);
        Assert.NotNull(manifest["packages"]!["Package.Two"]);
    }

    #endregion

    // ===================================================================
    // MCP MERGE TASK: Multi-Agent Tests
    // ===================================================================

    #region MCP Merge - Multi-Agent

    [Fact]
    public void MergeMcp_TwoAgents_ServerInBothFiles()
    {
        var fragment = CreateMcpFragment("sample-server");
        var task = new ImprintMergeMcpServers
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir,
            TargetAgents = "copilot;claude",
            McpFragmentFiles = new ITaskItem[] { new MockTaskItem(fragment) }
        };

        var result = task.Execute();

        Assert.True(result);

        // Copilot: .vscode/mcp.json - uses "servers" root key
        var copilotMcp = Path.Combine(_projectDir, ".vscode", "mcp.json");
        Assert.True(File.Exists(copilotMcp), "Copilot mcp.json should exist");
        var copilotDoc = JsonNode.Parse(File.ReadAllText(copilotMcp))!;
        Assert.NotNull(copilotDoc["servers"]?["sample-server"]);
        Assert.Null(copilotDoc["mcpServers"]); // Should NOT have mcpServers

        // Claude: .claude/mcp.json - uses "mcpServers" root key
        var claudeMcp = Path.Combine(_projectDir, ".claude", "mcp.json");
        Assert.True(File.Exists(claudeMcp), "Claude mcp.json should exist");
        var claudeDoc = JsonNode.Parse(File.ReadAllText(claudeMcp))!;
        Assert.NotNull(claudeDoc["mcpServers"]?["sample-server"]);
        Assert.Null(claudeDoc["servers"]); // Should NOT have servers
    }

    [Fact]
    public void MergeMcp_ThreeAgents_ServerInAllFiles()
    {
        var fragment = CreateMcpFragment("my-server");
        var task = new ImprintMergeMcpServers
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir,
            TargetAgents = "copilot;claude;cursor",
            McpFragmentFiles = new ITaskItem[] { new MockTaskItem(fragment) }
        };

        task.Execute();

        Assert.True(File.Exists(Path.Combine(_projectDir, ".vscode", "mcp.json")), "copilot mcp.json");
        Assert.True(File.Exists(Path.Combine(_projectDir, ".claude", "mcp.json")), "claude mcp.json");
        Assert.True(File.Exists(Path.Combine(_projectDir, ".cursor", "mcp.json")), "cursor mcp.json");

        // All should have the server with correct root key per agent
        // Copilot uses "servers", Claude and Cursor use "mcpServers"
        var copilotDoc = JsonNode.Parse(File.ReadAllText(Path.Combine(_projectDir, ".vscode", "mcp.json")))!;
        Assert.NotNull(copilotDoc["servers"]?["my-server"]);
        Assert.Null(copilotDoc["mcpServers"]); // Should NOT have mcpServers

        var claudeDoc = JsonNode.Parse(File.ReadAllText(Path.Combine(_projectDir, ".claude", "mcp.json")))!;
        Assert.NotNull(claudeDoc["mcpServers"]?["my-server"]);
        Assert.Null(claudeDoc["servers"]); // Should NOT have servers

        var cursorDoc = JsonNode.Parse(File.ReadAllText(Path.Combine(_projectDir, ".cursor", "mcp.json")))!;
        Assert.NotNull(cursorDoc["mcpServers"]?["my-server"]);
        Assert.Null(cursorDoc["servers"]); // Should NOT have servers
    }

    [Fact]
    public void MergeMcp_TwoAgents_LegacyManifestsInEachDir()
    {
        var fragment = CreateMcpFragment("server-a");
        var task = new ImprintMergeMcpServers
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir,
            TargetAgents = "copilot;claude",
            McpFragmentFiles = new ITaskItem[] { new MockTaskItem(fragment) }
        };

        task.Execute();

        // Legacy manifests should exist in each agent's MCP directory
        var copilotManifest = Path.Combine(_projectDir, ".vscode", ".imprint-mcp-manifest");
        var claudeManifest = Path.Combine(_projectDir, ".claude", ".imprint-mcp-manifest");
        Assert.True(File.Exists(copilotManifest), "Copilot legacy manifest");
        Assert.True(File.Exists(claudeManifest), "Claude legacy manifest");

        // Both should list "server-a"
        foreach (var mp in new[] { copilotManifest, claudeManifest })
        {
            var doc = JsonNode.Parse(File.ReadAllText(mp))!;
            var keys = doc["managedServers"]!.AsArray().Select(n => n!.GetValue<string>()).ToList();
            Assert.Contains("server-a", keys);
        }
    }

    [Fact]
    public void MergeMcp_TwoAgents_UnifiedManifestHasMcpSection()
    {
        var fragment = CreateMcpFragment("test-server");
        var task = new ImprintMergeMcpServers
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir,
            TargetAgents = "copilot;claude",
            McpFragmentFiles = new ITaskItem[] { new MockTaskItem(fragment) }
        };

        task.Execute();

        var manifestPath = Path.Combine(_projectDir, ".imprint", "manifest.json");
        Assert.True(File.Exists(manifestPath), "Unified manifest should exist");

        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!;
        var mcp = manifest["mcp"]!.AsObject();

        // Should have entries for both agents
        Assert.NotNull(mcp["copilot"]);
        Assert.NotNull(mcp["claude"]);

        // Copilot MCP path should reference .vscode/mcp.json
        var copilotPath = mcp["copilot"]!["path"]!.GetValue<string>();
        Assert.Contains(".vscode", copilotPath);

        // Claude MCP path should reference .claude/mcp.json
        var claudePath = mcp["claude"]!["path"]!.GetValue<string>();
        Assert.Contains(".claude", claudePath);

        // Both should track "test-server"
        foreach (var agent in new[] { "copilot", "claude" })
        {
            var servers = mcp[agent]!["managedServers"]!.AsArray()
                .Select(s => s!.GetValue<string>()).ToList();
            Assert.Contains("test-server", servers);
        }
    }

    [Fact]
    public void MergeMcp_MultipleFragments_TwoAgents()
    {
        var frag1 = CreateMcpFragment("server-a");
        var frag2 = CreateMcpFragment("server-b");
        var task = new ImprintMergeMcpServers
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir,
            TargetAgents = "copilot;cursor",
            McpFragmentFiles = new ITaskItem[]
            {
                new MockTaskItem(frag1),
                new MockTaskItem(frag2)
            }
        };

        task.Execute();

        // Copilot uses "servers", Cursor uses "mcpServers"
        var copilotDoc = JsonNode.Parse(File.ReadAllText(Path.Combine(_projectDir, ".vscode", "mcp.json")))!;
        Assert.NotNull(copilotDoc["servers"]?["server-a"]);
        Assert.NotNull(copilotDoc["servers"]?["server-b"]);
        Assert.Null(copilotDoc["mcpServers"]); // Should NOT have mcpServers

        var cursorDoc = JsonNode.Parse(File.ReadAllText(Path.Combine(_projectDir, ".cursor", "mcp.json")))!;
        Assert.NotNull(cursorDoc["mcpServers"]?["server-a"]);
        Assert.NotNull(cursorDoc["mcpServers"]?["server-b"]);
        Assert.Null(cursorDoc["servers"]); // Should NOT have servers
    }

    [Fact]
    public void MergeMcp_TwoAgents_PreservesExistingUserServers()
    {
        // Pre-create user servers in copilot's mcp.json
        var vscodeDir = Path.Combine(_projectDir, ".vscode");
        Directory.CreateDirectory(vscodeDir);
        File.WriteAllText(Path.Combine(vscodeDir, "mcp.json"), """
        {
          "servers": {
            "my-custom-server": { "type": "stdio", "command": "node", "args": ["server.js"] }
          }
        }
        """);

        var fragment = CreateMcpFragment("managed-server");
        var task = new ImprintMergeMcpServers
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir,
            TargetAgents = "copilot;claude",
            McpFragmentFiles = new ITaskItem[] { new MockTaskItem(fragment) }
        };

        task.Execute();

        // Copilot uses "servers", should have both user and managed servers
        var copilotDoc = JsonNode.Parse(File.ReadAllText(Path.Combine(vscodeDir, "mcp.json")))!;
        Assert.NotNull(copilotDoc["servers"]?["my-custom-server"]);
        Assert.NotNull(copilotDoc["servers"]?["managed-server"]);
        Assert.Null(copilotDoc["mcpServers"]); // Should NOT have mcpServers

        // Claude uses "mcpServers", should only have managed server (no pre-existing)
        var claudeDoc = JsonNode.Parse(File.ReadAllText(Path.Combine(_projectDir, ".claude", "mcp.json")))!;
        Assert.NotNull(claudeDoc["mcpServers"]?["managed-server"]);
        Assert.Null(claudeDoc["mcpServers"]?["my-custom-server"]);
        Assert.Null(claudeDoc["servers"]); // Should NOT have servers
    }

    #endregion

    // ===================================================================
    // CLEAN TASKS: Multi-Agent Tests
    // ===================================================================

    #region Clean Content - Multi-Agent

    [Fact]
    public void CopyThenClean_TwoAgents_AllFilesRemoved()
    {
        // Copy to copilot + claude
        var src = CreateSourceFile("guide.md", "# Guide");
        var copyTask = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir,
            TargetAgents = "copilot;claude",
            ContentItems = new ITaskItem[] { CreateContentItem(src, "Zakira.Imprint.Guide") }
        };
        copyTask.Execute();

        var copilotFile = Path.Combine(_projectDir, ".github", "skills", "guide.md");
        var claudeFile = Path.Combine(_projectDir, ".claude", "skills", "guide.md");
        Assert.True(File.Exists(copilotFile), "Pre-condition: copilot file exists");
        Assert.True(File.Exists(claudeFile), "Pre-condition: claude file exists");

        // Clean
        var cleanTask = new ImprintCleanContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir
        };
        var result = cleanTask.Execute();

        Assert.True(result);
        Assert.False(File.Exists(copilotFile), "Copilot file should be deleted");
        Assert.False(File.Exists(claudeFile), "Claude file should be deleted");
        Assert.False(File.Exists(Path.Combine(_projectDir, ".imprint", "manifest.json")), "Unified manifest should be deleted");
        Assert.False(File.Exists(Path.Combine(_projectDir, ".imprint", "Zakira.Imprint.Guide.manifest")), "Legacy manifest should be deleted");
    }

    [Fact]
    public void CopyThenClean_ThreeAgents_AllFilesRemoved()
    {
        var src = CreateSourceFile("skill.md", "# Skill");
        var copyTask = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir,
            TargetAgents = "copilot;claude;cursor",
            ContentItems = new ITaskItem[] { CreateContentItem(src, "Pkg") }
        };
        copyTask.Execute();

        // Clean
        var cleanTask = new ImprintCleanContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir
        };
        cleanTask.Execute();

        Assert.False(File.Exists(Path.Combine(_projectDir, ".github", "skills", "skill.md")));
        Assert.False(File.Exists(Path.Combine(_projectDir, ".claude", "skills", "skill.md")));
        Assert.False(File.Exists(Path.Combine(_projectDir, ".cursor", "rules", "skill.md")));
    }

    [Fact]
    public void CopyThenClean_TwoAgents_MultiplePackages()
    {
        var src1 = CreateSourceFile("pkg1/file.md", "pkg1");
        var src2 = CreateSourceFile("pkg2/file.md", "pkg2");
        var copyTask = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir,
            TargetAgents = "copilot;claude",
            ContentItems = new ITaskItem[]
            {
                CreateContentItem(src1, "Package.One"),
                CreateContentItem(src2, "Package.Two")
            }
        };
        copyTask.Execute();

        // Clean
        var cleanTask = new ImprintCleanContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir
        };
        cleanTask.Execute();

        // All four locations should be cleaned
        Assert.False(File.Exists(Path.Combine(_projectDir, ".github", "skills", "pkg1", "file.md")));
        Assert.False(File.Exists(Path.Combine(_projectDir, ".github", "skills", "pkg2", "file.md")));
        Assert.False(File.Exists(Path.Combine(_projectDir, ".claude", "skills", "pkg1", "file.md")));
        Assert.False(File.Exists(Path.Combine(_projectDir, ".claude", "skills", "pkg2", "file.md")));

        // All manifests deleted
        Assert.False(File.Exists(Path.Combine(_projectDir, ".imprint", "manifest.json")));
        Assert.False(File.Exists(Path.Combine(_projectDir, ".imprint", "Package.One.manifest")));
        Assert.False(File.Exists(Path.Combine(_projectDir, ".imprint", "Package.Two.manifest")));
    }

    #endregion

    #region Clean MCP - Multi-Agent

    [Fact]
    public void MergeThenCleanMcp_TwoAgents_AllServersRemoved()
    {
        // Merge to copilot + claude
        var fragment = CreateMcpFragment("sample-server");
        var mergeTask = new ImprintMergeMcpServers
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir,
            TargetAgents = "copilot;claude",
            McpFragmentFiles = new ITaskItem[] { new MockTaskItem(fragment) }
        };
        mergeTask.Execute();

        // Verify pre-conditions
        Assert.True(File.Exists(Path.Combine(_projectDir, ".vscode", "mcp.json")));
        Assert.True(File.Exists(Path.Combine(_projectDir, ".claude", "mcp.json")));

        // Clean
        var cleanTask = new ImprintCleanMcpServers
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir
        };
        var result = cleanTask.Execute();

        Assert.True(result);
        // MCP files should be deleted (no user servers remain)
        Assert.False(File.Exists(Path.Combine(_projectDir, ".vscode", "mcp.json")), "Copilot mcp.json should be deleted");
        Assert.False(File.Exists(Path.Combine(_projectDir, ".claude", "mcp.json")), "Claude mcp.json should be deleted");
        // Legacy manifests should be cleaned
        Assert.False(File.Exists(Path.Combine(_projectDir, ".vscode", ".imprint-mcp-manifest")));
        Assert.False(File.Exists(Path.Combine(_projectDir, ".claude", ".imprint-mcp-manifest")));
    }

    [Fact]
    public void MergeThenCleanMcp_ThreeAgents_AllServersRemoved()
    {
        var fragment = CreateMcpFragment("test-server");
        var mergeTask = new ImprintMergeMcpServers
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir,
            TargetAgents = "copilot;claude;cursor",
            McpFragmentFiles = new ITaskItem[] { new MockTaskItem(fragment) }
        };
        mergeTask.Execute();

        // Clean
        var cleanTask = new ImprintCleanMcpServers
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir
        };
        cleanTask.Execute();

        Assert.False(File.Exists(Path.Combine(_projectDir, ".vscode", "mcp.json")));
        Assert.False(File.Exists(Path.Combine(_projectDir, ".claude", "mcp.json")));
        Assert.False(File.Exists(Path.Combine(_projectDir, ".cursor", "mcp.json")));
    }

    [Fact]
    public void MergeThenCleanMcp_TwoAgents_PreservesUserServers()
    {
        // Pre-create user server in copilot's mcp.json
        var vscodeDir = Path.Combine(_projectDir, ".vscode");
        Directory.CreateDirectory(vscodeDir);
        File.WriteAllText(Path.Combine(vscodeDir, "mcp.json"), """
        {
          "servers": {
            "user-server": { "type": "stdio", "command": "node", "args": ["my-server.js"] }
          }
        }
        """);

        // Merge to both agents
        var fragment = CreateMcpFragment("managed-server");
        var mergeTask = new ImprintMergeMcpServers
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir,
            TargetAgents = "copilot;claude",
            McpFragmentFiles = new ITaskItem[] { new MockTaskItem(fragment) }
        };
        mergeTask.Execute();

        // Clean
        var cleanTask = new ImprintCleanMcpServers
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir
        };
        cleanTask.Execute();

        // Copilot mcp.json should still exist with user server
        var copilotMcp = Path.Combine(vscodeDir, "mcp.json");
        Assert.True(File.Exists(copilotMcp), "Copilot mcp.json should remain (has user server)");
        var doc = JsonNode.Parse(File.ReadAllText(copilotMcp))!;
        Assert.NotNull(doc["servers"]?["user-server"]);
        Assert.Null(doc["servers"]?["managed-server"]);

        // Claude mcp.json should be deleted (no user servers)
        Assert.False(File.Exists(Path.Combine(_projectDir, ".claude", "mcp.json")), "Claude mcp.json should be deleted");
    }

    #endregion

    // ===================================================================
    // FULL WORKFLOW: Copy + MCP + Clean across multiple agents
    // ===================================================================

    #region Full Workflow

    [Fact]
    public void FullWorkflow_CopyAndMerge_ThenCleanBoth()
    {
        // Step 1: Copy skills to copilot + claude
        var src = CreateSourceFile("skill.md", "# Skill");
        var copyTask = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir,
            TargetAgents = "copilot;claude",
            ContentItems = new ITaskItem[] { CreateContentItem(src, "Zakira.Imprint.Sample") }
        };
        copyTask.Execute();

        // Step 2: Merge MCP to copilot + claude
        var fragment = CreateMcpFragment("sample-server");
        var mergeTask = new ImprintMergeMcpServers
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir,
            TargetAgents = "copilot;claude",
            McpFragmentFiles = new ITaskItem[] { new MockTaskItem(fragment) }
        };
        mergeTask.Execute();

        // Verify everything exists
        Assert.True(File.Exists(Path.Combine(_projectDir, ".github", "skills", "skill.md")));
        Assert.True(File.Exists(Path.Combine(_projectDir, ".claude", "skills", "skill.md")));
        Assert.True(File.Exists(Path.Combine(_projectDir, ".vscode", "mcp.json")));
        Assert.True(File.Exists(Path.Combine(_projectDir, ".claude", "mcp.json")));

        // Unified manifest should have both packages and mcp sections
        var manifestPath = Path.Combine(_projectDir, ".imprint", "manifest.json");
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!;
        Assert.NotNull(manifest["packages"]);
        Assert.NotNull(manifest["mcp"]);

        // Step 3: Clean MCP
        var cleanMcp = new ImprintCleanMcpServers
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir
        };
        cleanMcp.Execute();

        Assert.False(File.Exists(Path.Combine(_projectDir, ".vscode", "mcp.json")));
        Assert.False(File.Exists(Path.Combine(_projectDir, ".claude", "mcp.json")));

        // Unified manifest should still exist with packages but no mcp section
        Assert.True(File.Exists(manifestPath), "Unified manifest should still exist (has packages)");
        var postMcpClean = JsonNode.Parse(File.ReadAllText(manifestPath))!;
        Assert.NotNull(postMcpClean["packages"]);
        Assert.Null(postMcpClean["mcp"]);

        // Step 4: Clean content
        var cleanContent = new ImprintCleanContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir
        };
        cleanContent.Execute();

        Assert.False(File.Exists(Path.Combine(_projectDir, ".github", "skills", "skill.md")));
        Assert.False(File.Exists(Path.Combine(_projectDir, ".claude", "skills", "skill.md")));
        Assert.False(File.Exists(manifestPath), "Unified manifest should be deleted after content clean");
    }

    #endregion

    // ===================================================================
    // AUTO-DETECTION Integration Tests
    // ===================================================================

    #region Auto-Detection

    [Fact]
    public void AutoDetect_CopiesOnlyToDetectedAgents()
    {
        // Create detection directories for copilot and cursor (but not claude)
        Directory.CreateDirectory(Path.Combine(_projectDir, ".github"));
        Directory.CreateDirectory(Path.Combine(_projectDir, ".cursor"));

        var src = CreateSourceFile("skill.md", "# Skill");
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir,
            // No explicit TargetAgents — rely on auto-detection
            AutoDetectAgents = true,
            ContentItems = new ITaskItem[] { CreateContentItem(src, "Pkg") }
        };

        task.Execute();

        // Should copy to copilot and cursor (detected), not claude
        Assert.True(File.Exists(Path.Combine(_projectDir, ".github", "skills", "skill.md")), "Copilot (detected)");
        Assert.True(File.Exists(Path.Combine(_projectDir, ".cursor", "rules", "skill.md")), "Cursor (detected)");
        Assert.False(File.Exists(Path.Combine(_projectDir, ".claude", "skills", "skill.md")), "Claude (not detected)");
    }

    [Fact]
    public void AutoDetect_FallsBackToDefault_WhenNoAgentsDetected()
    {
        // No detection directories created
        var src = CreateSourceFile("skill.md", "# Skill");
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir,
            AutoDetectAgents = true,
            DefaultAgents = "copilot",
            ContentItems = new ITaskItem[] { CreateContentItem(src, "Pkg") }
        };

        task.Execute();

        // Should fall back to copilot default
        Assert.True(File.Exists(Path.Combine(_projectDir, ".github", "skills", "skill.md")), "Default copilot");
        Assert.False(File.Exists(Path.Combine(_projectDir, ".claude", "skills", "skill.md")), "No claude");
    }

    [Fact]
    public void AutoDetect_NoAgentsDetected_NoDefaults_CreatesNoFiles()
    {
        // No detection directories created and no defaults set (new 1.0.1 behavior)
        var src = CreateSourceFile("skill.md", "# Skill");
        var mockEngine = new MockBuildEngine();
        var task = new ImprintCopyContent
        {
            BuildEngine = mockEngine,
            ProjectDirectory = _projectDir,
            AutoDetectAgents = true,
            DefaultAgents = "", // Empty - no fallback
            ContentItems = new ITaskItem[] { CreateContentItem(src, "Pkg") }
        };

        var result = task.Execute();

        // Should succeed but create no files
        Assert.True(result);
        Assert.False(File.Exists(Path.Combine(_projectDir, ".github", "skills", "skill.md")), "No copilot file");
        Assert.False(File.Exists(Path.Combine(_projectDir, ".claude", "skills", "skill.md")), "No claude file");
        Assert.False(File.Exists(Path.Combine(_projectDir, ".cursor", "rules", "skill.md")), "No cursor file");
        Assert.False(Directory.Exists(Path.Combine(_projectDir, ".imprint")), "No manifest directory");
        
        // Should log info message about no agents
        Assert.Contains(mockEngine.Messages, m => m.Contains("No target agents"));
    }

    [Fact]
    public void ExplicitAgents_OverridesAutoDetect()
    {
        // Create detection directory for copilot
        Directory.CreateDirectory(Path.Combine(_projectDir, ".github"));

        var src = CreateSourceFile("skill.md", "# Skill");
        var task = new ImprintCopyContent
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _projectDir,
            TargetAgents = "claude", // Explicit: only claude, even though copilot dir exists
            AutoDetectAgents = true,
            ContentItems = new ITaskItem[] { CreateContentItem(src, "Pkg") }
        };

        task.Execute();

        // Should only copy to claude (explicit), not copilot (detected but overridden)
        Assert.True(File.Exists(Path.Combine(_projectDir, ".claude", "skills", "skill.md")), "Claude (explicit)");
        Assert.False(File.Exists(Path.Combine(_projectDir, ".github", "skills", "skill.md")), "Copilot (overridden by explicit)");
    }

    #endregion
}
