using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

using Zakira.Imprint.Sdk;

namespace Zakira.Imprint.Sdk.Tests;

public class ImprintMergeMcpServersTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _vscodePath;
    private readonly string _mcpJsonPath;
    private readonly string _manifestPath;
    private readonly string _gitignorePath;

    public ImprintMergeMcpServersTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "ImprintTests", Guid.NewGuid().ToString());
        _vscodePath = Path.Combine(_testDir, ".vscode");
        _mcpJsonPath = Path.Combine(_vscodePath, "mcp.json");
        _manifestPath = Path.Combine(_vscodePath, ".imprint-mcp-manifest");
        _gitignorePath = Path.Combine(_vscodePath, ".gitignore");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    private string CreateFragment(string name, Dictionary<string, object> servers)
    {
        var fragment = new JsonObject
        {
            ["servers"] = new JsonObject()
        };
        var serversObj = fragment["servers"]!.AsObject();
        foreach (var kvp in servers)
        {
            serversObj.Add(kvp.Key, JsonSerializer.SerializeToNode(kvp.Value));
        }

        var fragmentDir = Path.Combine(_testDir, "fragments");
        Directory.CreateDirectory(fragmentDir);
        var path = Path.Combine(fragmentDir, $"{name}.mcp.json");
        File.WriteAllText(path, fragment.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    private string CreateSimpleFragment(string name, string serverKey)
    {
        var path = Path.Combine(_testDir, "fragments");
        Directory.CreateDirectory(path);
        var filePath = Path.Combine(path, $"{name}.mcp.json");
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
        File.WriteAllText(filePath, json);
        return filePath;
    }

    private ImprintMergeMcpServers CreateTask(params string[] fragmentPaths)
    {
        return new ImprintMergeMcpServers
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _testDir,
            TargetAgents = "copilot",
            McpFragmentFiles = fragmentPaths.Select(p => new MockTaskItem(p)).ToArray()
        };
    }

    [Fact]
    public void Execute_CreatesNewMcpJson_WhenNoneExists()
    {
        var fragment = CreateSimpleFragment("PackageA", "server-a");
        var task = CreateTask(fragment);

        var result = task.Execute();

        Assert.True(result);
        Assert.True(File.Exists(_mcpJsonPath));
        var doc = JsonNode.Parse(File.ReadAllText(_mcpJsonPath))!;
        Assert.NotNull(doc["servers"]?["server-a"]);
        Assert.Equal("stdio", doc["servers"]!["server-a"]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void Execute_CreatesManifest_WithManagedKeys()
    {
        var fragment = CreateSimpleFragment("PackageA", "server-a");
        var task = CreateTask(fragment);

        task.Execute();

        Assert.True(File.Exists(_manifestPath));
        var manifest = JsonNode.Parse(File.ReadAllText(_manifestPath))!;
        var keys = manifest["managedServers"]!.AsArray().Select(n => n!.GetValue<string>()).ToList();
        Assert.Single(keys);
        Assert.Contains("server-a", keys);
    }

    [Fact]
    public void Execute_CreatesGitignore_WithManifestEntry()
    {
        var fragment = CreateSimpleFragment("PackageA", "server-a");
        var task = CreateTask(fragment);

        task.Execute();

        Assert.True(File.Exists(_gitignorePath));
        var content = File.ReadAllText(_gitignorePath);
        Assert.Contains(".imprint-mcp-manifest", content);
    }

    [Fact]
    public void Execute_PreservesUserServers()
    {
        // Setup: existing mcp.json with a user-defined server
        Directory.CreateDirectory(_vscodePath);
        File.WriteAllText(_mcpJsonPath, """
        {
          "servers": {
            "my-custom-server": {
              "type": "stdio",
              "command": "node",
              "args": ["server.js"]
            }
          }
        }
        """);

        var fragment = CreateSimpleFragment("PackageA", "server-a");
        var task = CreateTask(fragment);

        task.Execute();

        var doc = JsonNode.Parse(File.ReadAllText(_mcpJsonPath))!;
        Assert.NotNull(doc["servers"]?["my-custom-server"]);
        Assert.NotNull(doc["servers"]?["server-a"]);
    }

    [Fact]
    public void Execute_PreservesInputsProperty()
    {
        Directory.CreateDirectory(_vscodePath);
        File.WriteAllText(_mcpJsonPath, """
        {
          "inputs": [
            {
              "type": "promptString",
              "id": "api-key",
              "description": "Enter API key",
              "password": true
            }
          ],
          "servers": {}
        }
        """);

        var fragment = CreateSimpleFragment("PackageA", "server-a");
        var task = CreateTask(fragment);

        task.Execute();

        var doc = JsonNode.Parse(File.ReadAllText(_mcpJsonPath))!;
        Assert.NotNull(doc["inputs"]);
        var inputs = doc["inputs"]!.AsArray();
        Assert.Single(inputs);
        Assert.Equal("api-key", inputs[0]!["id"]!.GetValue<string>());
        Assert.NotNull(doc["servers"]?["server-a"]);
    }

    [Fact]
    public void Execute_MergesMultipleFragments()
    {
        var fragmentA = CreateSimpleFragment("PackageA", "server-a");
        var fragmentB = CreateSimpleFragment("PackageB", "server-b");
        var task = CreateTask(fragmentA, fragmentB);

        task.Execute();

        var doc = JsonNode.Parse(File.ReadAllText(_mcpJsonPath))!;
        Assert.NotNull(doc["servers"]?["server-a"]);
        Assert.NotNull(doc["servers"]?["server-b"]);

        var manifest = JsonNode.Parse(File.ReadAllText(_manifestPath))!;
        var keys = manifest["managedServers"]!.AsArray().Select(n => n!.GetValue<string>()).ToList();
        Assert.Contains("server-a", keys);
        Assert.Contains("server-b", keys);
    }

    [Fact]
    public void Execute_IsIdempotent_SkipsWriteWhenUnchanged()
    {
        var fragment = CreateSimpleFragment("PackageA", "server-a");

        // First run
        var task1 = CreateTask(fragment);
        task1.Execute();
        var firstContent = File.ReadAllText(_mcpJsonPath);
        var firstWriteTime = File.GetLastWriteTimeUtc(_mcpJsonPath);

        // Wait a tiny bit to ensure timestamps differ if file is rewritten
        System.Threading.Thread.Sleep(50);

        // Second run
        var task2 = CreateTask(fragment);
        var engine2 = new MockBuildEngine();
        task2.BuildEngine = engine2;
        task2.Execute();

        var secondContent = File.ReadAllText(_mcpJsonPath);
        Assert.Equal(firstContent, secondContent);
        Assert.Contains(engine2.Messages, m => m.Contains("already up to date"));
    }

    [Fact]
    public void Execute_RemovesOldManagedServers_WhenPackageRemoved()
    {
        // First build: two packages
        var fragmentA = CreateSimpleFragment("PackageA", "server-a");
        var fragmentB = CreateSimpleFragment("PackageB", "server-b");
        var task1 = CreateTask(fragmentA, fragmentB);
        task1.Execute();

        // Second build: only one package (B removed)
        var task2 = CreateTask(fragmentA);
        task2.Execute();

        var doc = JsonNode.Parse(File.ReadAllText(_mcpJsonPath))!;
        Assert.NotNull(doc["servers"]?["server-a"]);
        Assert.Null(doc["servers"]?["server-b"]); // removed

        var manifest = JsonNode.Parse(File.ReadAllText(_manifestPath))!;
        var keys = manifest["managedServers"]!.AsArray().Select(n => n!.GetValue<string>()).ToList();
        Assert.Contains("server-a", keys);
        Assert.DoesNotContain("server-b", keys);
    }

    [Fact]
    public void Execute_SkipsMissingFragmentFiles_WithWarning()
    {
        var engine = new MockBuildEngine();
        var task = new ImprintMergeMcpServers
        {
            BuildEngine = engine,
            ProjectDirectory = _testDir,
            TargetAgents = "copilot",
            McpFragmentFiles = new[] { new MockTaskItem("/nonexistent/fragment.mcp.json") }
        };

        var result = task.Execute();

        Assert.True(result); // does not fail
        Assert.Contains(engine.Warnings, w => w.Contains("not found"));
    }

    [Fact]
    public void Execute_HandlesInvalidJsonFragments_WithWarning()
    {
        var fragmentDir = Path.Combine(_testDir, "fragments");
        Directory.CreateDirectory(fragmentDir);
        var badFragment = Path.Combine(fragmentDir, "bad.mcp.json");
        File.WriteAllText(badFragment, "{ invalid json }}}");

        var engine = new MockBuildEngine();
        var task = new ImprintMergeMcpServers
        {
            BuildEngine = engine,
            ProjectDirectory = _testDir,
            TargetAgents = "copilot",
            McpFragmentFiles = new[] { new MockTaskItem(badFragment) }
        };

        var result = task.Execute();

        Assert.True(result);
        Assert.Contains(engine.Warnings, w => w.Contains("Failed to parse"));
    }

    [Fact]
    public void Execute_NoFragments_SkipsMerge()
    {
        var engine = new MockBuildEngine();
        var task = new ImprintMergeMcpServers
        {
            BuildEngine = engine,
            ProjectDirectory = _testDir,
            TargetAgents = "copilot",
            McpFragmentFiles = Array.Empty<MockTaskItem>()
        };

        var result = task.Execute();

        Assert.True(result);
        Assert.False(File.Exists(_mcpJsonPath));
    }

    [Fact]
    public void Execute_AppendsToExistingGitignore()
    {
        Directory.CreateDirectory(_vscodePath);
        File.WriteAllText(_gitignorePath, "*.bak\nsettings.json\n");

        var fragment = CreateSimpleFragment("PackageA", "server-a");
        var task = CreateTask(fragment);
        task.Execute();

        var content = File.ReadAllText(_gitignorePath);
        Assert.Contains("*.bak", content);
        Assert.Contains("settings.json", content);
        Assert.Contains(".imprint-mcp-manifest", content);
    }

    [Fact]
    public void Execute_DoesNotDuplicateGitignoreEntry()
    {
        Directory.CreateDirectory(_vscodePath);
        File.WriteAllText(_gitignorePath, ".imprint-mcp-manifest\n");

        var fragment = CreateSimpleFragment("PackageA", "server-a");
        var task = CreateTask(fragment);
        task.Execute();

        var content = File.ReadAllText(_gitignorePath);
        var count = content.Split(".imprint-mcp-manifest").Length - 1;
        Assert.Equal(1, count);
    }

    [Fact]
    public void Execute_ServerKeyConflict_LastFragmentWins()
    {
        // Two fragments define the same key with different configs
        var fragDir = Path.Combine(_testDir, "fragments");
        Directory.CreateDirectory(fragDir);

        var frag1 = Path.Combine(fragDir, "A.mcp.json");
        File.WriteAllText(frag1, """
        {
          "servers": {
            "shared-server": {
              "type": "stdio",
              "command": "cmd-a",
              "args": ["a"]
            }
          }
        }
        """);

        var frag2 = Path.Combine(fragDir, "B.mcp.json");
        File.WriteAllText(frag2, """
        {
          "servers": {
            "shared-server": {
              "type": "stdio",
              "command": "cmd-b",
              "args": ["b"]
            }
          }
        }
        """);

        var task = CreateTask(frag1, frag2);
        task.Execute();

        var doc = JsonNode.Parse(File.ReadAllText(_mcpJsonPath))!;
        // B was processed after A, so B's config wins
        Assert.Equal("cmd-b", doc["servers"]!["shared-server"]!["command"]!.GetValue<string>());
    }

    [Fact]
    public void Execute_HandlesCorruptManifest()
    {
        Directory.CreateDirectory(_vscodePath);
        File.WriteAllText(_manifestPath, "corrupt data here!!!");

        var fragment = CreateSimpleFragment("PackageA", "server-a");
        var task = CreateTask(fragment);

        var result = task.Execute();

        Assert.True(result);
        var doc = JsonNode.Parse(File.ReadAllText(_mcpJsonPath))!;
        Assert.NotNull(doc["servers"]?["server-a"]);
    }

    [Fact]
    public void Execute_HandlesCorruptExistingMcpJson()
    {
        Directory.CreateDirectory(_vscodePath);
        File.WriteAllText(_mcpJsonPath, "not valid json {{{");

        var fragment = CreateSimpleFragment("PackageA", "server-a");
        var task = CreateTask(fragment);

        var result = task.Execute();

        Assert.True(result);
        // Should have been recreated from scratch
        var doc = JsonNode.Parse(File.ReadAllText(_mcpJsonPath))!;
        Assert.NotNull(doc["servers"]?["server-a"]);
    }

    [Fact]
    public void Execute_Opencode_TransformsToLocalFormat()
    {
        // Setup: create .opencode directory so OpenCode is detected
        var opencodePath = Path.Combine(_testDir, ".opencode");
        Directory.CreateDirectory(opencodePath);

        var fragment = CreateSimpleFragment("PackageA", "server-a");
        var task = new ImprintMergeMcpServers
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _testDir,
            TargetAgents = "opencode",
            McpFragmentFiles = new[] { new MockTaskItem(fragment) }
        };

        var result = task.Execute();

        Assert.True(result);

        // OpenCode stores mcp.json at project root, not in a subdirectory
        var mcpPath = Path.Combine(_testDir, "opencode.json");
        Assert.True(File.Exists(mcpPath), $"Expected opencode.json at {mcpPath}");

        var doc = JsonNode.Parse(File.ReadAllText(mcpPath))!;
        // OpenCode uses "mcp" as root key
        Assert.NotNull(doc["mcp"]);
        var server = doc["mcp"]!["server-a"]!;

        // Verify transformation: type should be "local" instead of "stdio"
        Assert.Equal("local", server["type"]!.GetValue<string>());

        // Verify command is an array combining original command + args
        var commandArray = server["command"]!.AsArray();
        Assert.Equal(3, commandArray.Count);
        Assert.Equal("npx", commandArray[0]!.GetValue<string>());
        Assert.Equal("-y", commandArray[1]!.GetValue<string>());
        Assert.Equal("@example/server-a", commandArray[2]!.GetValue<string>());

        // Verify enabled property is set
        Assert.True(server["enabled"]!.GetValue<bool>());
    }

    [Fact]
    public void Execute_Opencode_TransformsEnvToEnvironment()
    {
        // Setup: create .opencode directory
        var opencodePath = Path.Combine(_testDir, ".opencode");
        Directory.CreateDirectory(opencodePath);

        // Create a fragment with env property
        var fragmentDir = Path.Combine(_testDir, "fragments");
        Directory.CreateDirectory(fragmentDir);
        var fragmentPath = Path.Combine(fragmentDir, "WithEnv.mcp.json");
        File.WriteAllText(fragmentPath, """
        {
          "servers": {
            "server-with-env": {
              "type": "stdio",
              "command": "node",
              "args": ["server.js"],
              "env": {
                "API_KEY": "secret123",
                "DEBUG": "true"
              }
            }
          }
        }
        """);

        var task = new ImprintMergeMcpServers
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _testDir,
            TargetAgents = "opencode",
            McpFragmentFiles = new[] { new MockTaskItem(fragmentPath) }
        };

        var result = task.Execute();

        Assert.True(result);

        var mcpPath = Path.Combine(_testDir, "opencode.json");
        var doc = JsonNode.Parse(File.ReadAllText(mcpPath))!;
        var server = doc["mcp"]!["server-with-env"]!;

        // Verify env is transformed to environment
        Assert.NotNull(server["environment"]);
        Assert.Equal("secret123", server["environment"]!["API_KEY"]!.GetValue<string>());
        Assert.Equal("true", server["environment"]!["DEBUG"]!.GetValue<string>());

        // Original env key should not be present
        Assert.Null(server["env"]);
    }

    [Fact]
    public void Execute_Copilot_KeepsStdioFormat()
    {
        // Ensure non-OpenCode agents keep the standard stdio format
        var fragment = CreateSimpleFragment("PackageA", "server-a");
        var task = CreateTask(fragment);

        task.Execute();

        var doc = JsonNode.Parse(File.ReadAllText(_mcpJsonPath))!;
        var server = doc["servers"]!["server-a"]!;

        // Should keep "stdio" type
        Assert.Equal("stdio", server["type"]!.GetValue<string>());

        // Should keep command as string, not array
        Assert.Equal("npx", server["command"]!.GetValue<string>());

        // Should keep args as separate array
        var args = server["args"]!.AsArray();
        Assert.Equal(2, args.Count);

        // Should NOT have "enabled" property
        Assert.Null(server["enabled"]);
    }

    [Fact]
    public void Execute_Opencode_PreservesUserServers()
    {
        // Setup: create .opencode directory and existing config
        var opencodePath = Path.Combine(_testDir, ".opencode");
        Directory.CreateDirectory(opencodePath);

        var mcpPath = Path.Combine(_testDir, "opencode.json");
        File.WriteAllText(mcpPath, """
        {
          "mcp": {
            "my-custom-server": {
              "type": "local",
              "command": ["node", "custom.js"],
              "enabled": true
            }
          }
        }
        """);

        var fragment = CreateSimpleFragment("PackageA", "server-a");
        var task = new ImprintMergeMcpServers
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _testDir,
            TargetAgents = "opencode",
            McpFragmentFiles = new[] { new MockTaskItem(fragment) }
        };

        task.Execute();

        var doc = JsonNode.Parse(File.ReadAllText(mcpPath))!;
        // User's server should be preserved
        Assert.NotNull(doc["mcp"]?["my-custom-server"]);
        // Managed server should be added
        Assert.NotNull(doc["mcp"]?["server-a"]);
    }
}
