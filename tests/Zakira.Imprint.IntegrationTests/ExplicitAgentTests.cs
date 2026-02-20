using System.Diagnostics;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Zakira.Imprint.IntegrationTests;

/// <summary>
/// Integration tests for explicit agent targeting using ImprintTargetAgents property.
/// These tests verify that explicitly specifying agents works correctly, even when
/// the agent directory doesn't exist.
/// </summary>
[Collection("SdkPackage")]
public class ExplicitAgentTests : IDisposable
{
    private readonly SdkPackageFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly string _testDirectory;
    private readonly SkillPackageHelper _skillHelper;

    public ExplicitAgentTests(SdkPackageFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _testDirectory = Path.Combine(Path.GetTempPath(), "Zakira.Imprint.IntegrationTests", $"ExplicitAgent_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _output.WriteLine($"Test directory: {_testDirectory}");
        _skillHelper = new SkillPackageHelper(_fixture.PackagesPath, _fixture.SdkVersion, output);
    }

    [Theory]
    [InlineData("copilot", ".github/skills/")]
    [InlineData("claude", ".claude/skills/")]
    [InlineData("cursor", ".cursor/rules/")]
    [InlineData("roo", ".roo/rules/")]
    [InlineData("opencode", ".opencode/skills/")]
    [InlineData("windsurf", ".windsurf/rules/")]
    public async Task ExplicitAgent_NoAgentDirectory_StillCreatesFiles(string agentName, string expectedSkillPath)
    {
        // Arrange - Create and pack a skill package
        var (packageId, version, _) = await _skillHelper.CreateAndPackSkillPackageAsync(
            _testDirectory, $"Explicit{agentName}", SkillPackageContent.Simple("test-skill"));

        // Create a consumer project with explicit agent targeting, but NO agent directory
        var projectDir = await CreateConsumerProjectAsync($"Explicit{agentName}Consumer", packageId, version,
            $"<ImprintTargetAgents>{agentName}</ImprintTargetAgents>");

        // Verify no agent directories exist initially in the project
        Assert.False(Directory.Exists(Path.Combine(projectDir, ".github")));
        Assert.False(Directory.Exists(Path.Combine(projectDir, ".claude")));
        Assert.False(Directory.Exists(Path.Combine(projectDir, ".cursor")));
        Assert.False(Directory.Exists(Path.Combine(projectDir, ".roo")));
        Assert.False(Directory.Exists(Path.Combine(projectDir, ".opencode")));
        Assert.False(Directory.Exists(Path.Combine(projectDir, ".windsurf")));

        // Act - Build the project
        var result = await BuildProjectAsync(projectDir);

        // Assert
        Assert.True(result.Succeeded, $"Build failed:\n{result.StandardError}");

        // Verify skill content was created at the expected location (relative to project)
        var skillDestPath = Path.Combine(projectDir, expectedSkillPath, "test-skill");
        Assert.True(Directory.Exists(skillDestPath),
            $"Skill directory not found at: {skillDestPath}\n" +
            $"Explicit agent '{agentName}' should create content even without agent directory");

        var instructionsPath = Path.Combine(skillDestPath, "instructions.md");
        Assert.True(File.Exists(instructionsPath), $"instructions.md not found at: {instructionsPath}");
    }

    [Fact]
    public async Task ExplicitMultipleAgents_NoDirectories_CreatesFilesForAll()
    {
        // Arrange - Create and pack a skill package
        var (packageId, version, _) = await _skillHelper.CreateAndPackSkillPackageAsync(
            _testDirectory, "ExplicitMulti", SkillPackageContent.Simple("test-skill"));

        // Explicitly target multiple agents without any agent directories
        var projectDir = await CreateConsumerProjectAsync("ExplicitMultiConsumer", packageId, version,
            "<ImprintTargetAgents>copilot;claude;cursor</ImprintTargetAgents>");

        // Act
        var result = await BuildProjectAsync(projectDir);

        // Assert
        Assert.True(result.Succeeded, $"Build failed:\n{result.StandardError}");

        // All three should have content (relative to project directory)
        Assert.True(Directory.Exists(Path.Combine(projectDir, ".github", "skills", "test-skill")),
            "Copilot skill directory not found");
        Assert.True(Directory.Exists(Path.Combine(projectDir, ".claude", "skills", "test-skill")),
            "Claude skill directory not found");
        Assert.True(Directory.Exists(Path.Combine(projectDir, ".cursor", "rules", "test-skill")),
            "Cursor skill directory not found");
    }

    [Fact]
    public async Task ExplicitAgent_OverridesAutoDetection()
    {
        // Arrange - Create and pack a skill package
        var (packageId, version, _) = await _skillHelper.CreateAndPackSkillPackageAsync(
            _testDirectory, "Override", SkillPackageContent.Simple("test-skill"));

        // Create agent directories for claude and cursor, but explicitly target only copilot
        var projectDir = await CreateConsumerProjectAsync("OverrideConsumer", packageId, version,
            "<ImprintTargetAgents>copilot</ImprintTargetAgents>");

        // Create agent directories for claude and cursor inside project dir (should be ignored due to explicit targeting)
        Directory.CreateDirectory(Path.Combine(projectDir, ".claude"));
        Directory.CreateDirectory(Path.Combine(projectDir, ".cursor"));

        // Act
        var result = await BuildProjectAsync(projectDir);

        // Assert
        Assert.True(result.Succeeded, $"Build failed:\n{result.StandardError}");

        // Only copilot should have content (explicit target)
        Assert.True(Directory.Exists(Path.Combine(projectDir, ".github", "skills", "test-skill")),
            "Copilot skill directory not found (explicitly targeted)");
        
        // Claude and cursor should NOT have skill content (they were not targeted)
        Assert.False(Directory.Exists(Path.Combine(projectDir, ".claude", "skills", "test-skill")),
            "Claude should not have skill content (not in ImprintTargetAgents)");
        Assert.False(Directory.Exists(Path.Combine(projectDir, ".cursor", "rules", "test-skill")),
            "Cursor should not have skill content (not in ImprintTargetAgents)");
    }

    [Fact]
    public async Task UnknownAgent_UsesWindsurfConvention()
    {
        // Arrange - Create and pack a skill package
        var (packageId, version, _) = await _skillHelper.CreateAndPackSkillPackageAsync(
            _testDirectory, "Unknown", SkillPackageContent.Simple("test-skill"));

        // Explicitly target an unknown agent (should use windsurf convention: .{agent}/rules/)
        var projectDir = await CreateConsumerProjectAsync("UnknownAgentConsumer", packageId, version,
            "<ImprintTargetAgents>myagent</ImprintTargetAgents>");

        // Act
        var result = await BuildProjectAsync(projectDir);

        // Assert
        Assert.True(result.Succeeded, $"Build failed:\n{result.StandardError}");

        // Unknown agent should use windsurf convention: .{name}/rules/{skill}/
        Assert.True(Directory.Exists(Path.Combine(projectDir, ".myagent", "rules", "test-skill")),
            "Unknown agent should use windsurf convention: .myagent/rules/");
    }

    [Fact]
    public async Task EmptyImprintTargetAgents_ExplicitNoOp()
    {
        // Arrange - Create and pack a skill package
        var (packageId, version, _) = await _skillHelper.CreateAndPackSkillPackageAsync(
            _testDirectory, "OptOut", SkillPackageContent.Simple("test-skill"));

        // Explicitly set ImprintTargetAgents to empty (opt-out scenario)
        var projectDir = await CreateConsumerProjectAsync("OptOutConsumer", packageId, version,
            "<ImprintTargetAgents></ImprintTargetAgents>");

        // Create agent directories that would normally be auto-detected (inside project dir)
        Directory.CreateDirectory(Path.Combine(projectDir, ".github"));
        Directory.CreateDirectory(Path.Combine(projectDir, ".claude"));

        // Act
        var result = await BuildProjectAsync(projectDir);

        // Assert
        Assert.True(result.Succeeded, $"Build failed:\n{result.StandardError}");

        // No content should be created because ImprintTargetAgents is explicitly empty
        Assert.False(Directory.Exists(Path.Combine(projectDir, ".github", "skills", "test-skill")),
            "No content should be created when ImprintTargetAgents is empty");
        Assert.False(Directory.Exists(Path.Combine(projectDir, ".claude", "skills", "test-skill")),
            "No content should be created when ImprintTargetAgents is empty");
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
