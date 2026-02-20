using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Zakira.Imprint.IntegrationTests;

/// <summary>
/// Integration tests for agent auto-detection across all supported agents.
/// These tests verify that the SDK correctly detects agent directories and
/// copies content to the appropriate locations when a consumer references
/// a skill package built with the SDK.
/// </summary>
[Collection("SdkPackage")]
public class AgentDetectionTests : IDisposable
{
    private readonly SdkPackageFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly string _testDirectory;
    private readonly SkillPackageHelper _skillHelper;

    public AgentDetectionTests(SdkPackageFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _testDirectory = Path.Combine(Path.GetTempPath(), "Zakira.Imprint.IntegrationTests", $"AgentDetection_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _output.WriteLine($"Test directory: {_testDirectory}");
        _skillHelper = new SkillPackageHelper(_fixture.PackagesPath, _fixture.SdkVersion, output);
    }

    [Fact]
    public async Task NoAgentsDetected_NoExplicitTargets_CreatesNoFiles()
    {
        // Arrange - Create and pack a skill package
        var (packageId, version, _) = await _skillHelper.CreateAndPackSkillPackageAsync(
            _testDirectory, "NoAgents", SkillPackageContent.Simple());

        // Create a consumer project with NO agent directories
        var projectDir = await CreateConsumerProjectAsync("NoAgentsConsumer", packageId, version);

        // Act - Build the project
        var result = await BuildProjectAsync(projectDir);

        // Assert - Build should succeed
        Assert.True(result.Succeeded, $"Build failed:\n{result.StandardError}");
        
        // Verify no agent skill directories were created (the project may have bin/obj)
        Assert.False(Directory.Exists(Path.Combine(projectDir, ".github", "skills")), ".github/skills should not exist");
        Assert.False(Directory.Exists(Path.Combine(projectDir, ".claude", "skills")), ".claude/skills should not exist");
        Assert.False(Directory.Exists(Path.Combine(projectDir, ".cursor", "rules")), ".cursor/rules should not exist");
        Assert.False(Directory.Exists(Path.Combine(projectDir, ".roo", "rules")), ".roo/rules should not exist");
        Assert.False(Directory.Exists(Path.Combine(projectDir, ".opencode", "skills")), ".opencode/skills should not exist");
        Assert.False(Directory.Exists(Path.Combine(projectDir, ".windsurf", "rules")), ".windsurf/rules should not exist");
    }

    [Theory]
    [InlineData("copilot", ".github", "skills")]
    [InlineData("claude", ".claude", "skills")]
    [InlineData("cursor", ".cursor", "rules")]
    [InlineData("roo", ".roo", "rules")]
    [InlineData("opencode", ".opencode", "skills")]
    [InlineData("windsurf", ".windsurf", "rules")]
    public async Task SingleAgent_AutoDetected_CopiesContentToCorrectLocation(
        string agentName, string agentDir, string skillSubdir)
    {
        // Arrange - Create and pack a skill package
        var (packageId, version, _) = await _skillHelper.CreateAndPackSkillPackageAsync(
            _testDirectory, $"{agentName}Skill", SkillPackageContent.Simple("test-skill"));

        // Create a consumer project with the agent directory marker
        var projectDir = await CreateConsumerProjectAsync($"{agentName}Consumer", packageId, version);
        
        // Create the agent directory marker inside the project directory
        var agentMarkerPath = Path.Combine(projectDir, agentDir);
        Directory.CreateDirectory(agentMarkerPath);
        _output.WriteLine($"Created agent marker: {agentMarkerPath}");

        // Act - Build the project
        var result = await BuildProjectAsync(projectDir);

        // Assert
        Assert.True(result.Succeeded, $"Build failed:\n{result.StandardError}");
        
        // Verify skill content was copied to the correct location
        var skillDestPath = Path.Combine(projectDir, agentDir, skillSubdir, "test-skill");
        Assert.True(Directory.Exists(skillDestPath), 
            $"Skill directory not found at: {skillDestPath}\nExpected agent '{agentName}' to create content at '{agentDir}/{skillSubdir}/test-skill'");
        
        // Verify the instructions.md file was copied
        var instructionsPath = Path.Combine(skillDestPath, "instructions.md");
        Assert.True(File.Exists(instructionsPath), $"instructions.md not found at: {instructionsPath}");
        
        var content = await File.ReadAllTextAsync(instructionsPath);
        Assert.Contains("test-skill", content);
    }

    [Fact]
    public async Task MultipleAgents_AllDetected_CopiesContentToAllLocations()
    {
        // Arrange - Create and pack a skill package
        var (packageId, version, _) = await _skillHelper.CreateAndPackSkillPackageAsync(
            _testDirectory, "MultiAgent", SkillPackageContent.Simple("shared-skill"));

        // Create a consumer project with multiple agent directory markers
        var projectDir = await CreateConsumerProjectAsync("MultiAgentConsumer", packageId, version);

        // Create agent directory markers
        Directory.CreateDirectory(Path.Combine(projectDir, ".github"));
        Directory.CreateDirectory(Path.Combine(projectDir, ".claude"));
        Directory.CreateDirectory(Path.Combine(projectDir, ".cursor"));
        _output.WriteLine("Created markers for copilot, claude, and cursor");

        // Act - Build the project
        var result = await BuildProjectAsync(projectDir);

        // Assert
        Assert.True(result.Succeeded, $"Build failed:\n{result.StandardError}");

        // Verify content was copied to all three agent locations
        Assert.True(Directory.Exists(Path.Combine(projectDir, ".github", "skills", "shared-skill")),
            "Copilot skill directory not found");
        Assert.True(Directory.Exists(Path.Combine(projectDir, ".claude", "skills", "shared-skill")),
            "Claude skill directory not found");
        Assert.True(Directory.Exists(Path.Combine(projectDir, ".cursor", "rules", "shared-skill")),
            "Cursor skill directory not found");
    }

    [Fact]
    public async Task OpenCode_McpFormat_TransformedCorrectly()
    {
        // Arrange - Create and pack a skill package with MCP
        var (packageId, version, nupkgPath) = await _skillHelper.CreateAndPackSkillPackageAsync(
            _testDirectory, "OpenCodeMcp", SkillPackageContent.WithMcp("mcp-skill"));

        // Inspect the generated nupkg to see the .targets file
        var extractDir = Path.Combine(_testDirectory, "nupkg-extract");
        System.IO.Compression.ZipFile.ExtractToDirectory(nupkgPath, extractDir);
        _output.WriteLine("=== Package Contents ===");
        foreach (var file in Directory.GetFiles(extractDir, "*", SearchOption.AllDirectories))
        {
            _output.WriteLine($"  {file.Replace(extractDir, "")}");
        }
        
        // Read and log the .targets file
        var targetsFiles = Directory.GetFiles(extractDir, "*.targets", SearchOption.AllDirectories);
        foreach (var targetsFile in targetsFiles)
        {
            _output.WriteLine($"\n=== {Path.GetFileName(targetsFile)} ===");
            _output.WriteLine(await File.ReadAllTextAsync(targetsFile));
        }

        // Create a consumer project with OpenCode agent marker
        var projectDir = await CreateConsumerProjectAsync("OpenCodeMcpConsumer", packageId, version);
        Directory.CreateDirectory(Path.Combine(projectDir, ".opencode"));

        // Act - Build the project with verbose logging to see MCP items
        var result = await BuildProjectAsync(projectDir, verbose: true);
        
        // Log the output for debugging
        _output.WriteLine("\n=== Build Output (filtered) ===");
        foreach (var line in result.StandardOutput.Split('\n'))
        {
            if (line.Contains("Imprint") || line.Contains("MCP") || line.Contains("mcp"))
            {
                _output.WriteLine(line);
            }
        }

        // Assert
        Assert.True(result.Succeeded, $"Build failed:\n{result.StandardError}");

        // Verify OpenCode MCP config at project root with "mcp" key
        var opencodeMcpPath = Path.Combine(projectDir, "opencode.json");
        Assert.True(File.Exists(opencodeMcpPath), $"opencode.json not found at root: {opencodeMcpPath}");
        
        var mcpJson = await File.ReadAllTextAsync(opencodeMcpPath);
        using var doc = JsonDocument.Parse(mcpJson);
        
        // OpenCode uses "mcp" as root key, not "mcpServers"
        Assert.True(doc.RootElement.TryGetProperty("mcp", out var mcpElement),
            $"Expected 'mcp' key in opencode.json, got: {mcpJson}");
        Assert.True(mcpElement.TryGetProperty("test-server", out _),
            "Expected 'test-server' in mcp object");
    }

    private async Task<string> CreateConsumerProjectAsync(string projectName, string skillPackageId, string skillPackageVersion, string? additionalProps = null)
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
    <PackageReference Include=""{skillPackageId}"" Version=""{skillPackageVersion}"" />
  </ItemGroup>
</Project>";

        await File.WriteAllTextAsync(Path.Combine(projectDir, $"{projectName}.csproj"), csprojContent);

        // Create nuget.config pointing to our local packages
        var nugetConfig = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <clear />
    <add key=""local"" value=""{_fixture.PackagesPath}"" />
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
  </packageSources>
</configuration>";
        await File.WriteAllTextAsync(Path.Combine(projectDir, "nuget.config"), nugetConfig);

        // Create minimal source file
        await File.WriteAllTextAsync(Path.Combine(projectDir, "Class1.cs"), 
            "namespace TestProject;\npublic class Class1 { }");

        _output.WriteLine($"Created consumer project: {projectDir}");
        return projectDir;
    }

    private async Task<ProcessResult> BuildProjectAsync(string projectDir, bool verbose = false)
    {
        var arguments = verbose ? "build -c Release -v d" : "build -c Release";
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
