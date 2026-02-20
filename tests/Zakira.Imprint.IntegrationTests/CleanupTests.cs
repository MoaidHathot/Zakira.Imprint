using System.Diagnostics;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Zakira.Imprint.IntegrationTests;

/// <summary>
/// Integration tests for cleanup operations (dotnet clean).
/// These tests verify that files created during build are properly removed during clean.
/// 
/// Architecture: The SDK uses a two-phase workflow:
/// 1. Skill Package Authors: Reference SDK, declare Imprint items, pack creates .targets
/// 2. Consumers: Reference skill packages, SDK copies content to agent directories
/// </summary>
[Collection("SdkPackage")]
public class CleanupTests : IDisposable
{
    private readonly SdkPackageFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly string _testDirectory;
    private readonly SkillPackageHelper _skillHelper;

    public CleanupTests(SdkPackageFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _testDirectory = Path.Combine(Path.GetTempPath(), "Zakira.Imprint.IntegrationTests", $"Cleanup_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _skillHelper = new SkillPackageHelper(_fixture.PackagesPath, _fixture.SdkVersion, _output);
        _output.WriteLine($"Test directory: {_testDirectory}");
    }

    [Fact]
    public async Task DotnetClean_RemovesSkillContent()
    {
        // Arrange - Create skill package and consumer
        var skillPackage = await _skillHelper.CreateAndPackSkillPackageAsync(
            _testDirectory,
            "CleanSkill",
            SkillPackageContent.Simple("test-skill"));

        var projectDir = await CreateConsumerProjectAsync("CleanSkillProject",
            "<ImprintTargetAgents>claude</ImprintTargetAgents>",
            skillPackage.PackageId, skillPackage.Version);

        var buildResult = await BuildProjectAsync(projectDir);
        Assert.True(buildResult.Succeeded, $"Build failed:\n{buildResult.StandardError}");

        // Verify content was created (relative to project directory)
        var skillPath = Path.Combine(projectDir, ".claude", "skills", "test-skill");
        Assert.True(Directory.Exists(skillPath), $"Skill directory should exist after build: {skillPath}");

        // Act - Clean the project
        var cleanResult = await CleanProjectAsync(projectDir);

        // Assert
        Assert.True(cleanResult.Succeeded, $"Clean failed:\n{cleanResult.StandardError}");
        
        // Skill content should be removed
        Assert.False(Directory.Exists(skillPath), 
            $"Skill directory should be removed after clean: {skillPath}");
    }

    [Fact]
    public async Task DotnetClean_RemovesMcpConfig()
    {
        // Arrange - Create skill package with MCP and consumer
        var skillPackage = await _skillHelper.CreateAndPackSkillPackageAsync(
            _testDirectory,
            "CleanMcpSkill",
            SkillPackageContent.WithMcp("mcp-skill"));

        var projectDir = await CreateConsumerProjectAsync("CleanMcpProject",
            "<ImprintTargetAgents>claude</ImprintTargetAgents>",
            skillPackage.PackageId, skillPackage.Version);

        var buildResult = await BuildProjectAsync(projectDir);
        Assert.True(buildResult.Succeeded, $"Build failed:\n{buildResult.StandardError}");

        // Verify MCP was created (relative to project directory)
        var mcpPath = Path.Combine(projectDir, ".claude", "mcp.json");
        Assert.True(File.Exists(mcpPath), $"MCP file should exist after build: {mcpPath}");

        // Act - Clean the project
        var cleanResult = await CleanProjectAsync(projectDir);

        // Assert
        Assert.True(cleanResult.Succeeded, $"Clean failed:\n{cleanResult.StandardError}");
        
        // MCP config should be removed
        Assert.False(File.Exists(mcpPath), 
            $"MCP file should be removed after clean: {mcpPath}");
    }

    [Fact]
    public async Task DotnetClean_MultipleAgents_RemovesAllContent()
    {
        // Arrange - Create skill package and consumer with multiple agents
        var skillPackage = await _skillHelper.CreateAndPackSkillPackageAsync(
            _testDirectory,
            "MultiAgentSkill",
            SkillPackageContent.Simple("test-skill"));

        var projectDir = await CreateConsumerProjectAsync("CleanMultiProject",
            "<ImprintTargetAgents>copilot;claude;cursor</ImprintTargetAgents>",
            skillPackage.PackageId, skillPackage.Version);

        var buildResult = await BuildProjectAsync(projectDir);
        Assert.True(buildResult.Succeeded, $"Build failed:\n{buildResult.StandardError}");

        // Verify content was created for all agents (relative to project directory)
        Assert.True(Directory.Exists(Path.Combine(projectDir, ".github", "skills", "test-skill")));
        Assert.True(Directory.Exists(Path.Combine(projectDir, ".claude", "skills", "test-skill")));
        Assert.True(Directory.Exists(Path.Combine(projectDir, ".cursor", "rules", "test-skill")));

        // Act - Clean the project
        var cleanResult = await CleanProjectAsync(projectDir);

        // Assert
        Assert.True(cleanResult.Succeeded, $"Clean failed:\n{cleanResult.StandardError}");
        
        // All skill content should be removed
        Assert.False(Directory.Exists(Path.Combine(projectDir, ".github", "skills", "test-skill")),
            "Copilot skill should be removed");
        Assert.False(Directory.Exists(Path.Combine(projectDir, ".claude", "skills", "test-skill")),
            "Claude skill should be removed");
        Assert.False(Directory.Exists(Path.Combine(projectDir, ".cursor", "rules", "test-skill")),
            "Cursor skill should be removed");
    }

    [Fact]
    public async Task DotnetClean_OpenCode_RemovesMcpAtRoot()
    {
        // Arrange - Create skill package with MCP and consumer for OpenCode
        // Use unique package name to avoid collision with AgentDetectionTests.opencodeSkill
        var skillPackage = await _skillHelper.CreateAndPackSkillPackageAsync(
            _testDirectory,
            "CleanupOpenCodeSkill",
            SkillPackageContent.WithMcp("opencode-skill"));

        var projectDir = await CreateConsumerProjectAsync("CleanOpenCodeProject",
            "<ImprintTargetAgents>opencode</ImprintTargetAgents>",
            skillPackage.PackageId, skillPackage.Version);

        var buildResult = await BuildProjectAsync(projectDir);
        Assert.True(buildResult.Succeeded, $"Build failed:\n{buildResult.StandardError}");

        // Verify content was created (relative to project directory)
        var skillPath = Path.Combine(projectDir, ".opencode", "skills", "opencode-skill");
        var mcpPath = Path.Combine(projectDir, "opencode.json");
        Assert.True(Directory.Exists(skillPath), $"Skill directory should exist: {skillPath}");
        Assert.True(File.Exists(mcpPath), $"opencode.json should exist: {mcpPath}");

        // Act - Clean the project
        var cleanResult = await CleanProjectAsync(projectDir);

        // Assert
        Assert.True(cleanResult.Succeeded, $"Clean failed:\n{cleanResult.StandardError}");
        
        // Both skill content and root MCP should be removed
        Assert.False(Directory.Exists(skillPath), "OpenCode skill should be removed");
        Assert.False(File.Exists(mcpPath), "opencode.json should be removed");
    }

    [Fact]
    public async Task DotnetClean_PreservesExistingAgentDirectory()
    {
        // Arrange - Create skill package and consumer
        var skillPackage = await _skillHelper.CreateAndPackSkillPackageAsync(
            _testDirectory,
            "PreserveSkill",
            SkillPackageContent.Simple("test-skill"));

        var projectDir = await CreateConsumerProjectAsync("PreserveProject",
            "<ImprintTargetAgents>claude</ImprintTargetAgents>",
            skillPackage.PackageId, skillPackage.Version);
        
        // Create user content inside the project directory (simulating existing agent config at repo root)
        var existingDir = Path.Combine(projectDir, ".claude");
        Directory.CreateDirectory(existingDir);
        var userFilePath = Path.Combine(existingDir, "user-file.md");
        await File.WriteAllTextAsync(userFilePath, "# User's custom file");

        var buildResult = await BuildProjectAsync(projectDir);
        Assert.True(buildResult.Succeeded, $"Build failed:\n{buildResult.StandardError}");

        // Verify skill was added alongside user content
        Assert.True(Directory.Exists(Path.Combine(projectDir, ".claude", "skills", "test-skill")));
        Assert.True(File.Exists(userFilePath), "User file should still exist");

        // Act - Clean the project
        var cleanResult = await CleanProjectAsync(projectDir);

        // Assert
        Assert.True(cleanResult.Succeeded, $"Clean failed:\n{cleanResult.StandardError}");
        
        // Skill content should be removed, but user file should be preserved
        Assert.False(Directory.Exists(Path.Combine(projectDir, ".claude", "skills", "test-skill")),
            "Skill should be removed");
        Assert.True(File.Exists(userFilePath), 
            "User's custom file should be preserved");
    }

    [Fact]
    public async Task BuildThenCleanThenBuild_WorksCorrectly()
    {
        // Arrange - Create skill package and consumer
        var skillPackage = await _skillHelper.CreateAndPackSkillPackageAsync(
            _testDirectory,
            "CycleSkill",
            SkillPackageContent.Simple("test-skill"));

        var projectDir = await CreateConsumerProjectAsync("CycleProject",
            "<ImprintTargetAgents>claude</ImprintTargetAgents>",
            skillPackage.PackageId, skillPackage.Version);

        // First build
        var build1 = await BuildProjectAsync(projectDir);
        Assert.True(build1.Succeeded, "First build failed");
        Assert.True(Directory.Exists(Path.Combine(projectDir, ".claude", "skills", "test-skill")));

        // Clean
        var clean = await CleanProjectAsync(projectDir);
        Assert.True(clean.Succeeded, "Clean failed");
        Assert.False(Directory.Exists(Path.Combine(projectDir, ".claude", "skills", "test-skill")));

        // Second build - should recreate content
        var build2 = await BuildProjectAsync(projectDir);
        Assert.True(build2.Succeeded, "Second build failed");
        Assert.True(Directory.Exists(Path.Combine(projectDir, ".claude", "skills", "test-skill")),
            "Content should be recreated after rebuild");
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

    private async Task<ProcessResult> BuildProjectAsync(string projectDir)
    {
        return await RunDotnetAsync("build -c Release", projectDir);
    }

    private async Task<ProcessResult> CleanProjectAsync(string projectDir)
    {
        return await RunDotnetAsync("clean -c Release", projectDir);
    }

    private async Task<ProcessResult> RunDotnetAsync(string arguments, string workingDirectory)
    {
        _output.WriteLine($"Running: dotnet {arguments}");
        _output.WriteLine($"Working directory: {workingDirectory}");

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
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
            throw new TimeoutException($"dotnet command timed out after 2 minutes");
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
