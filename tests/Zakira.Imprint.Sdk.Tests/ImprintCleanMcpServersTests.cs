using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

using Zakira.Imprint.Sdk;

namespace Zakira.Imprint.Sdk.Tests;

public class ImprintCleanMcpServersTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _vscodePath;
    private readonly string _mcpJsonPath;
    private readonly string _manifestPath;

    public ImprintCleanMcpServersTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "ImprintTests", Guid.NewGuid().ToString());
        _vscodePath = Path.Combine(_testDir, ".vscode");
        _mcpJsonPath = Path.Combine(_vscodePath, "mcp.json");
        _manifestPath = Path.Combine(_vscodePath, ".imprint-mcp-manifest");
        Directory.CreateDirectory(_vscodePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    private void WriteMcpJson(JsonObject doc)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(_mcpJsonPath, doc.ToJsonString(options));
    }

    private void WriteManifest(params string[] managedKeys)
    {
        var manifest = new JsonObject
        {
            ["managedServers"] = new JsonArray(managedKeys.Select(k => (JsonNode)JsonValue.Create(k)!).ToArray())
        };
        File.WriteAllText(_manifestPath, manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private ImprintCleanMcpServers CreateTask()
    {
        return new ImprintCleanMcpServers
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _testDir
        };
    }

    [Fact]
    public void Execute_RemovesManagedServers()
    {
        var mcpDoc = new JsonObject
        {
            ["servers"] = new JsonObject
            {
                ["managed-server"] = new JsonObject { ["type"] = "stdio", ["command"] = "npx" },
                ["user-server"] = new JsonObject { ["type"] = "stdio", ["command"] = "node" }
            }
        };
        WriteMcpJson(mcpDoc);
        WriteManifest("managed-server");

        var task = CreateTask();
        var result = task.Execute();

        Assert.True(result);
        Assert.True(File.Exists(_mcpJsonPath));
        var doc = JsonNode.Parse(File.ReadAllText(_mcpJsonPath))!;
        Assert.Null(doc["servers"]?["managed-server"]);
        Assert.NotNull(doc["servers"]?["user-server"]);
    }

    [Fact]
    public void Execute_DeletesMcpJson_WhenNoContentRemains()
    {
        var mcpDoc = new JsonObject
        {
            ["servers"] = new JsonObject
            {
                ["managed-server"] = new JsonObject { ["type"] = "stdio", ["command"] = "npx" }
            }
        };
        WriteMcpJson(mcpDoc);
        WriteManifest("managed-server");

        var task = CreateTask();
        task.Execute();

        Assert.False(File.Exists(_mcpJsonPath));
    }

    [Fact]
    public void Execute_DeletesManifest()
    {
        var mcpDoc = new JsonObject
        {
            ["servers"] = new JsonObject
            {
                ["managed-server"] = new JsonObject { ["type"] = "stdio" }
            }
        };
        WriteMcpJson(mcpDoc);
        WriteManifest("managed-server");

        var task = CreateTask();
        task.Execute();

        Assert.False(File.Exists(_manifestPath));
    }

    [Fact]
    public void Execute_SkipsWhenNoManifest()
    {
        var engine = new MockBuildEngine();
        var task = new ImprintCleanMcpServers
        {
            BuildEngine = engine,
            ProjectDirectory = _testDir
        };

        var result = task.Execute();

        Assert.True(result);
        Assert.Contains(engine.Messages, m => m.Contains("No MCP manifests found"));
    }

    [Fact]
    public void Execute_PreservesUserServers()
    {
        var mcpDoc = new JsonObject
        {
            ["servers"] = new JsonObject
            {
                ["managed-a"] = new JsonObject { ["type"] = "stdio" },
                ["managed-b"] = new JsonObject { ["type"] = "stdio" },
                ["user-server"] = new JsonObject { ["type"] = "stdio", ["command"] = "my-tool" }
            }
        };
        WriteMcpJson(mcpDoc);
        WriteManifest("managed-a", "managed-b");

        var task = CreateTask();
        task.Execute();

        Assert.True(File.Exists(_mcpJsonPath));
        var doc = JsonNode.Parse(File.ReadAllText(_mcpJsonPath))!;
        Assert.Null(doc["servers"]?["managed-a"]);
        Assert.Null(doc["servers"]?["managed-b"]);
        Assert.NotNull(doc["servers"]?["user-server"]);
        Assert.Equal("my-tool", doc["servers"]!["user-server"]!["command"]!.GetValue<string>());
    }

    [Fact]
    public void Execute_PreservesInputsProperty()
    {
        var mcpDoc = new JsonObject
        {
            ["inputs"] = new JsonArray(
                new JsonObject
                {
                    ["type"] = "promptString",
                    ["id"] = "api-key",
                    ["description"] = "Enter key",
                    ["password"] = true
                }
            ),
            ["servers"] = new JsonObject
            {
                ["managed-server"] = new JsonObject { ["type"] = "stdio" }
            }
        };
        WriteMcpJson(mcpDoc);
        WriteManifest("managed-server");

        var task = CreateTask();
        task.Execute();

        Assert.True(File.Exists(_mcpJsonPath)); // Not deleted because inputs exist
        var doc = JsonNode.Parse(File.ReadAllText(_mcpJsonPath))!;
        Assert.NotNull(doc["inputs"]);
        var inputs = doc["inputs"]!.AsArray();
        Assert.Single(inputs);
        Assert.Equal("api-key", inputs[0]!["id"]!.GetValue<string>());
    }

    [Fact]
    public void Execute_PreservesOtherTopLevelKeys()
    {
        var mcpDoc = new JsonObject
        {
            ["customProp"] = "someValue",
            ["servers"] = new JsonObject
            {
                ["managed-server"] = new JsonObject { ["type"] = "stdio" }
            }
        };
        WriteMcpJson(mcpDoc);
        WriteManifest("managed-server");

        var task = CreateTask();
        task.Execute();

        Assert.True(File.Exists(_mcpJsonPath)); // Not deleted because customProp exists
        var doc = JsonNode.Parse(File.ReadAllText(_mcpJsonPath))!;
        Assert.Equal("someValue", doc["customProp"]!.GetValue<string>());
    }

    [Fact]
    public void Execute_RemovesEmptyServersObject_AfterClean()
    {
        var mcpDoc = new JsonObject
        {
            ["inputs"] = new JsonArray(),
            ["customProp"] = "keep-me",
            ["servers"] = new JsonObject
            {
                ["managed-server"] = new JsonObject { ["type"] = "stdio" }
            }
        };
        WriteMcpJson(mcpDoc);
        WriteManifest("managed-server");

        var task = CreateTask();
        task.Execute();

        Assert.True(File.Exists(_mcpJsonPath));
        var doc = JsonNode.Parse(File.ReadAllText(_mcpJsonPath))!;
        // servers key should be removed since it's empty
        Assert.Null(doc["servers"]);
        Assert.Equal("keep-me", doc["customProp"]!.GetValue<string>());
    }

    [Fact]
    public void Execute_HandlesCorruptManifest()
    {
        File.WriteAllText(_manifestPath, "not json!!!!");

        var engine = new MockBuildEngine();
        var task = new ImprintCleanMcpServers
        {
            BuildEngine = engine,
            ProjectDirectory = _testDir
        };

        var result = task.Execute();

        Assert.True(result);
        Assert.Contains(engine.Warnings, w => w.Contains("Failed to read legacy manifest"));
    }

    [Fact]
    public void Execute_HandlesMissingMcpJson_WithManifest()
    {
        // Manifest exists but mcp.json doesn't — shouldn't crash
        WriteManifest("server-a");
        if (File.Exists(_mcpJsonPath)) File.Delete(_mcpJsonPath);

        var task = CreateTask();
        var result = task.Execute();

        Assert.True(result);
        Assert.False(File.Exists(_manifestPath)); // manifest still cleaned up
    }

    [Fact]
    public void Execute_FullWorkflow_BuildThenClean()
    {
        // Simulate a full build -> clean cycle using both tasks

        // Build: merge fragments
        var fragDir = Path.Combine(_testDir, "fragments");
        Directory.CreateDirectory(fragDir);
        var fragment = Path.Combine(fragDir, "Sample.mcp.json");
        File.WriteAllText(fragment, """
        {
          "servers": {
            "sample-server": {
              "type": "stdio",
              "command": "npx",
              "args": ["-y", "@example/sample"]
            }
          }
        }
        """);

        var mergeTask = new ImprintMergeMcpServers
        {
            BuildEngine = new MockBuildEngine(),
            ProjectDirectory = _testDir,
            TargetAgents = "copilot",
            McpFragmentFiles = new[] { new MockTaskItem(fragment) }
        };
        mergeTask.Execute();

        // Verify build result
        Assert.True(File.Exists(_mcpJsonPath));
        Assert.True(File.Exists(_manifestPath));

        // Clean
        var cleanTask = CreateTask();
        cleanTask.Execute();

        // Verify clean result
        Assert.False(File.Exists(_mcpJsonPath));
        Assert.False(File.Exists(_manifestPath));
    }
}
