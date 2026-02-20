using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Zakira.Imprint.IntegrationTests;

/// <summary>
/// Integration tests for MCP (Model Context Protocol) configuration transformation.
/// These tests verify that MCP configurations are correctly merged and transformed
/// for each agent's expected format.
/// 
/// Architecture: The SDK uses a two-phase workflow:
/// 1. Skill Package Authors: Reference SDK, declare Imprint items, pack creates .targets
/// 2. Consumers: Reference skill packages, SDK copies content to agent directories
/// </summary>
[Collection("SdkPackage")]
public class McpTransformationTests : IDisposable
{
    private readonly SdkPackageFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly string _testDirectory;
    private readonly SkillPackageHelper _skillHelper;

    public McpTransformationTests(SdkPackageFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _testDirectory = Path.Combine(Path.GetTempPath(), "Zakira.Imprint.IntegrationTests", $"McpTransform_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _skillHelper = new SkillPackageHelper(_fixture.PackagesPath, _fixture.SdkVersion, _output);
        _output.WriteLine($"Test directory: {_testDirectory}");
    }

    [Theory]
    [InlineData("copilot", ".vscode/mcp.json", "servers")]
    [InlineData("claude", ".claude/mcp.json", "mcpServers")]
    [InlineData("cursor", ".cursor/mcp.json", "mcpServers")]
    [InlineData("roo", ".roo/mcp.json", "mcpServers")]
    [InlineData("windsurf", ".windsurf/mcp.json", "mcpServers")]
    public async Task StandardAgents_McpFormat_UsesStandardFormat(
        string agentName, string expectedMcpPath, string expectedRootKey)
    {
        // Arrange - Create a skill package with MCP content
        var skillPackage = await _skillHelper.CreateAndPackSkillPackageAsync(
            _testDirectory,
            $"McpSkill{agentName}",
            SkillPackageContent.WithMcp($"mcp-skill-{agentName}", CreateMcpJson("test-server")));

        // Create consumer project that references the skill package
        var projectDir = await CreateConsumerProjectAsync($"Mcp{agentName}Project",
            $"<ImprintTargetAgents>{agentName}</ImprintTargetAgents>",
            skillPackage.PackageId, skillPackage.Version);

        // Act
        var result = await BuildProjectAsync(projectDir);

        // Assert
        Assert.True(result.Succeeded, $"Build failed:\n{result.StandardError}");

        // MCP file is created relative to project directory
        var mcpFilePath = Path.Combine(projectDir, expectedMcpPath);
        Assert.True(File.Exists(mcpFilePath), $"MCP file not found at: {mcpFilePath}");

        var mcpJson = await File.ReadAllTextAsync(mcpFilePath);
        _output.WriteLine($"MCP content for {agentName}:\n{mcpJson}");

        using var doc = JsonDocument.Parse(mcpJson);
        Assert.True(doc.RootElement.TryGetProperty(expectedRootKey, out var serversElement),
            $"Expected '{expectedRootKey}' key in MCP file, got: {mcpJson}");
        Assert.True(serversElement.TryGetProperty("test-server", out _),
            "Expected 'test-server' in servers object");
    }

    [Fact]
    public async Task OpenCode_McpFormat_UsesOpencodeRootKey()
    {
        // Arrange - OpenCode uses "mcp" as root key and stores at opencode.json at repo root
        var skillPackage = await _skillHelper.CreateAndPackSkillPackageAsync(
            _testDirectory,
            "McpSkillOpenCode",
            SkillPackageContent.WithMcp("opencode-skill", CreateMcpJson("opencode-server")));

        var projectDir = await CreateConsumerProjectAsync("McpOpenCodeProject",
            "<ImprintTargetAgents>opencode</ImprintTargetAgents>",
            skillPackage.PackageId, skillPackage.Version);

        // Act
        var result = await BuildProjectAsync(projectDir);

        // Assert
        Assert.True(result.Succeeded, $"Build failed:\n{result.StandardError}");

        // OpenCode MCP goes to opencode.json at project root (repo root simulation)
        var mcpFilePath = Path.Combine(projectDir, "opencode.json");
        Assert.True(File.Exists(mcpFilePath), $"opencode.json not found at: {mcpFilePath}");

        var mcpJson = await File.ReadAllTextAsync(mcpFilePath);
        _output.WriteLine($"MCP content for opencode:\n{mcpJson}");

        using var doc = JsonDocument.Parse(mcpJson);
        
        // OpenCode uses "mcp" as root key (not "mcpServers")
        Assert.True(doc.RootElement.TryGetProperty("mcp", out var mcpElement),
            $"Expected 'mcp' root key for opencode, got: {mcpJson}");
        Assert.True(mcpElement.TryGetProperty("opencode-server", out _),
            "Expected 'opencode-server' in mcp object");
    }

    [Fact]
    public async Task MultipleSkills_McpMerged_AllServersIncluded()
    {
        // Arrange - Create two skill packages with different MCP servers
        var skillPackage1 = await _skillHelper.CreateAndPackSkillPackageAsync(
            _testDirectory,
            "MergeSkillOne",
            SkillPackageContent.WithMcp("skill-one", CreateMcpJson("server-alpha")));

        var skillPackage2 = await _skillHelper.CreateAndPackSkillPackageAsync(
            _testDirectory,
            "MergeSkillTwo",
            SkillPackageContent.WithMcp("skill-two", CreateMcpJson("server-beta")));

        // Consumer references both skill packages
        var projectDir = await CreateConsumerProjectWithMultipleSkillsAsync("McpMergeProject",
            "<ImprintTargetAgents>claude</ImprintTargetAgents>",
            (skillPackage1.PackageId, skillPackage1.Version),
            (skillPackage2.PackageId, skillPackage2.Version));

        // Act
        var result = await BuildProjectAsync(projectDir);

        // Assert
        Assert.True(result.Succeeded, $"Build failed:\n{result.StandardError}");

        var mcpFilePath = Path.Combine(projectDir, ".claude", "mcp.json");
        Assert.True(File.Exists(mcpFilePath), $"MCP file not found at: {mcpFilePath}");

        var mcpJson = await File.ReadAllTextAsync(mcpFilePath);
        _output.WriteLine($"Merged MCP content:\n{mcpJson}");

        using var doc = JsonDocument.Parse(mcpJson);
        var servers = doc.RootElement.GetProperty("mcpServers");
        
        // Both servers should be present
        Assert.True(servers.TryGetProperty("server-alpha", out _),
            "Expected 'server-alpha' from skill-one");
        Assert.True(servers.TryGetProperty("server-beta", out _),
            "Expected 'server-beta' from skill-two");
    }

    [Fact]
    public async Task McpWithEnvVars_PreservedCorrectly()
    {
        // Arrange - Create MCP config with environment variables
        // Note: Skill package MCP fragments must use "servers" as root key (not agent-specific keys)
        // The SDK transforms to agent-specific format (mcpServers for claude) during merge
        var mcpWithEnv = @"{
  ""servers"": {
    ""env-server"": {
      ""command"": ""node"",
      ""args"": [""server.js""],
      ""env"": {
        ""API_KEY"": ""${API_KEY}"",
        ""DEBUG"": ""true""
      }
    }
  }
}";
        var skillPackage = await _skillHelper.CreateAndPackSkillPackageAsync(
            _testDirectory,
            "McpEnvSkill",
            SkillPackageContent.WithMcp("env-skill", mcpWithEnv));

        var projectDir = await CreateConsumerProjectAsync("McpEnvProject",
            "<ImprintTargetAgents>claude</ImprintTargetAgents>",
            skillPackage.PackageId, skillPackage.Version);

        // Act
        var result = await BuildProjectAsync(projectDir);

        // Assert
        Assert.True(result.Succeeded, $"Build failed:\n{result.StandardError}");

        var mcpFilePath = Path.Combine(projectDir, ".claude", "mcp.json");
        var mcpJson = await File.ReadAllTextAsync(mcpFilePath);
        _output.WriteLine($"MCP with env:\n{mcpJson}");

        using var doc = JsonDocument.Parse(mcpJson);
        var server = doc.RootElement.GetProperty("mcpServers").GetProperty("env-server");
        var env = server.GetProperty("env");
        
        Assert.Equal("${API_KEY}", env.GetProperty("API_KEY").GetString());
        Assert.Equal("true", env.GetProperty("DEBUG").GetString());
    }

    [Fact]
    public async Task NoMcpConfig_NoMcpFileCreated()
    {
        // Arrange - Create a skill package without MCP configuration
        var skillPackage = await _skillHelper.CreateAndPackSkillPackageAsync(
            _testDirectory,
            "NoMcpSkill",
            SkillPackageContent.Simple("no-mcp-skill"));

        var projectDir = await CreateConsumerProjectAsync("NoMcpProject",
            "<ImprintTargetAgents>claude</ImprintTargetAgents>",
            skillPackage.PackageId, skillPackage.Version);

        // Act
        var result = await BuildProjectAsync(projectDir);

        // Assert
        Assert.True(result.Succeeded, $"Build failed:\n{result.StandardError}");

        // Skill should be copied (relative to project directory)
        Assert.True(Directory.Exists(Path.Combine(projectDir, ".claude", "skills", "no-mcp-skill")),
            "Skill directory should be created");
        
        // But no MCP file should be created
        Assert.False(File.Exists(Path.Combine(projectDir, ".claude", "mcp.json")),
            "No mcp.json should be created when no skills have MCP configs");
    }

    private string CreateMcpJson(string serverName)
    {
        return $@"{{
  ""servers"": {{
    ""{serverName}"": {{
      ""type"": ""stdio"",
      ""command"": ""npx"",
      ""args"": [""-y"", ""@test/{serverName}""]
    }}
  }}
}}";
    }

    private async Task<string> CreateConsumerProjectAsync(
        string projectName,
        string? additionalProps,
        string skillPackageId,
        string skillPackageVersion)
    {
        var projectDir = Path.Combine(_testDirectory, projectName);
        Directory.CreateDirectory(projectDir);

        var csprojContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <OutputType>Library</OutputType>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    {additionalProps ?? ""}
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""Zakira.Imprint.Sdk"" Version=""{_fixture.SdkVersion}"" />
    <PackageReference Include=""{skillPackageId}"" Version=""{skillPackageVersion}"" />
  </ItemGroup>
</Project>";

        await File.WriteAllTextAsync(Path.Combine(projectDir, $"{projectName}.csproj"), csprojContent);

        var nugetConfig = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <clear />
    <add key=""local"" value=""{_fixture.PackagesPath}"" />
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
  </packageSources>
</configuration>";
        await File.WriteAllTextAsync(Path.Combine(projectDir, "nuget.config"), nugetConfig);

        await File.WriteAllTextAsync(Path.Combine(projectDir, "Class1.cs"),
            "namespace TestProject;\npublic class Class1 { }");

        _output.WriteLine($"Created consumer project: {projectDir}");
        return projectDir;
    }

    private async Task<string> CreateConsumerProjectWithMultipleSkillsAsync(
        string projectName,
        string? additionalProps,
        params (string PackageId, string Version)[] skillPackages)
    {
        var projectDir = Path.Combine(_testDirectory, projectName);
        Directory.CreateDirectory(projectDir);

        var packageRefs = new StringBuilder();
        packageRefs.AppendLine($@"    <PackageReference Include=""Zakira.Imprint.Sdk"" Version=""{_fixture.SdkVersion}"" />");
        foreach (var (packageId, version) in skillPackages)
        {
            packageRefs.AppendLine($@"    <PackageReference Include=""{packageId}"" Version=""{version}"" />");
        }

        var csprojContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <OutputType>Library</OutputType>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    {additionalProps ?? ""}
  </PropertyGroup>

  <ItemGroup>
{packageRefs}  </ItemGroup>
</Project>";

        await File.WriteAllTextAsync(Path.Combine(projectDir, $"{projectName}.csproj"), csprojContent);

        var nugetConfig = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <clear />
    <add key=""local"" value=""{_fixture.PackagesPath}"" />
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
  </packageSources>
</configuration>";
        await File.WriteAllTextAsync(Path.Combine(projectDir, "nuget.config"), nugetConfig);

        await File.WriteAllTextAsync(Path.Combine(projectDir, "Class1.cs"),
            "namespace TestProject;\npublic class Class1 { }");

        _output.WriteLine($"Created consumer project with multiple skills: {projectDir}");
        return projectDir;
    }

    private async Task<ProcessResult> BuildProjectAsync(string projectDir)
    {
        var arguments = "build -c Release";
        _output.WriteLine($"Running: dotnet {arguments}");
        _output.WriteLine($"Working directory: {projectDir}");

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            WorkingDirectory = projectDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stdoutBuilder.AppendLine(e.Data);
                _output.WriteLine($"[stdout] {e.Data}");
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stderrBuilder.AppendLine(e.Data);
                _output.WriteLine($"[stderr] {e.Data}");
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var completed = await Task.Run(() => process.WaitForExit(TimeSpan.FromMinutes(2)));

        if (!completed)
        {
            process.Kill();
            throw new TimeoutException("dotnet build timed out after 2 minutes");
        }

        return new ProcessResult(process.ExitCode, stdoutBuilder.ToString(), stderrBuilder.ToString());
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Warning: Failed to clean up test directory: {ex.Message}");
        }
    }
}
