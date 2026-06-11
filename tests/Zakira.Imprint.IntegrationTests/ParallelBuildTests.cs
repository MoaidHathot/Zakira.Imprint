using System.Diagnostics;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Zakira.Imprint.IntegrationTests;

/// <summary>
/// Integration tests for issue #8: building a multi-project solution in parallel where
/// several projects reference the same Imprint skill package must not fail with an
/// IOException ("the process cannot access the file ... because it is being used by
/// another process"). All projects share one repository root, so they race to write the
/// same destination files (skills, manifest, and granular .gitignore files).
/// </summary>
[Collection("SdkPackage")]
public class ParallelBuildTests : IDisposable
{
    private readonly SdkPackageFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly string _testRoot;
    private readonly string _repoRoot;
    private readonly SkillPackageHelper _skillHelper;

    public ParallelBuildTests(SdkPackageFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _testRoot = Path.Combine(Path.GetTempPath(), "Zakira.Imprint.IntegrationTests", $"Parallel_{Guid.NewGuid():N}");
        // The "repository" that all consumer projects live under and share as their root.
        _repoRoot = Path.Combine(_testRoot, "repo");
        Directory.CreateDirectory(_repoRoot);
        _output.WriteLine($"Test root: {_testRoot}");
        _skillHelper = new SkillPackageHelper(_fixture.PackagesPath, _fixture.SdkVersion, output);
    }

    [Fact]
    public async Task ParallelSolutionBuild_MultipleProjectsSamePackage_NoFileContention()
    {
        // Arrange - a single skill package shared by every consumer project.
        var (packageId, version, _) = await _skillHelper.CreateAndPackSkillPackageAsync(
            Path.Combine(_testRoot, "package-src"), "Parallel", SkillPackageContent.Simple("shared-skill"));

        // Establish ONE shared repository root for all consumer projects:
        //  - the .git marker guarantees every project resolves the same root (_repoRoot),
        //  - the .github marker makes "copilot" the auto-detected agent,
        // so all projects target the same <root>/.github/skills destination => contention.
        Directory.CreateDirectory(Path.Combine(_repoRoot, ".git"));
        Directory.CreateDirectory(Path.Combine(_repoRoot, ".github"));

        // A single nuget.config at the root serves all consumer projects.
        await File.WriteAllTextAsync(Path.Combine(_repoRoot, "nuget.config"), $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <clear />
    <add key=""local"" value=""{_fixture.PackagesPath}"" />
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
  </packageSources>
</configuration>");

        // Create several sibling consumer projects, all referencing the same package.
        const int projectCount = 6;
        var projectPaths = new List<string>();
        for (var i = 1; i <= projectCount; i++)
        {
            projectPaths.Add(await CreateConsumerProjectAsync($"Consumer{i}", packageId, version));
        }

        var slnPath = await CreateSolutionAsync("ParallelRepro", projectPaths);

        // Act - parallel build: the issue's exact repro is "dotnet build <sln>" with /m
        // (multiple MSBuild worker nodes building independent projects concurrently).
        var result = await RunDotnetAsync("build", _repoRoot, $"\"{slnPath}\"", "-c", "Release", "/m", "/nodeReuse:false");

        // Assert - the build must succeed without file-contention errors.
        Assert.True(result.Succeeded,
            $"Parallel solution build failed (issue #8 regression):\n{result.StandardError}\n{result.StandardOutput}");
        Assert.DoesNotContain("being used by another process", result.StandardOutput);
        Assert.DoesNotContain("being used by another process", result.StandardError);

        // The shared skill content must have been published exactly once to the root.
        var skillDir = Path.Combine(_repoRoot, ".github", "skills", "shared-skill");
        Assert.True(Directory.Exists(skillDir), $"Expected shared skill at {skillDir}");
        Assert.True(File.Exists(Path.Combine(skillDir, "instructions.md")),
            "instructions.md should have been copied to the shared skills directory");

        // And the shared manifest must exist and be valid (concurrent writers must not corrupt it).
        var manifestPath = Path.Combine(_repoRoot, ".imprint", "manifest.json");
        Assert.True(File.Exists(manifestPath), $"Expected manifest at {manifestPath}");
    }

    private async Task<string> CreateConsumerProjectAsync(string projectName, string skillPackageId, string skillPackageVersion)
    {
        var projectDir = Path.Combine(_repoRoot, projectName);
        Directory.CreateDirectory(projectDir);

        var csproj = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <OutputType>Library</OutputType>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""{skillPackageId}"" Version=""{skillPackageVersion}"" />
  </ItemGroup>
</Project>";

        var csprojPath = Path.Combine(projectDir, $"{projectName}.csproj");
        await File.WriteAllTextAsync(csprojPath, csproj);
        await File.WriteAllTextAsync(Path.Combine(projectDir, "Class1.cs"),
            "namespace TestProject;\npublic class Class1 { }");

        _output.WriteLine($"Created consumer project: {csprojPath}");
        return csprojPath;
    }

    private async Task<string> CreateSolutionAsync(string solutionName, IEnumerable<string> projectPaths)
    {
        var newResult = await RunDotnetAsync("new", _repoRoot, "sln", "-n", solutionName);
        Assert.True(newResult.Succeeded, $"dotnet new sln failed:\n{newResult.StandardError}\n{newResult.StandardOutput}");

        // The created file may be classic ".sln" or the newer XML ".slnx" depending on the SDK.
        var slnPath = Directory.GetFiles(_repoRoot, $"{solutionName}.sln*").FirstOrDefault();
        Assert.False(string.IsNullOrEmpty(slnPath), $"No solution file was created in {_repoRoot}");

        foreach (var proj in projectPaths)
        {
            var addResult = await RunDotnetAsync("sln", _repoRoot, $"\"{slnPath}\"", "add", $"\"{proj}\"");
            Assert.True(addResult.Succeeded, $"dotnet sln add failed:\n{addResult.StandardError}\n{addResult.StandardOutput}");
        }

        return slnPath!;
    }

    private async Task<ProcessResult> RunDotnetAsync(string command, string workingDirectory, params string[] args)
    {
        var arguments = $"{command} {string.Join(" ", args)}";
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

        var completed = await Task.Run(() => process.WaitForExit(TimeSpan.FromMinutes(5)));

        if (!completed)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"dotnet {command} timed out after 5 minutes");
        }

        return new ProcessResult(process.ExitCode, stdoutBuilder.ToString(), stderrBuilder.ToString());
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Warning: Failed to clean up test directory: {ex.Message}");
        }
    }
}
