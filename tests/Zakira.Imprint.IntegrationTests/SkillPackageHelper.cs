using System.Diagnostics;
using System.Text;
using Xunit.Abstractions;

namespace Zakira.Imprint.IntegrationTests;

/// <summary>
/// Helper class for creating and packing skill packages that use the Zakira.Imprint.Sdk.
/// These skill packages can then be referenced by consumer projects to test the SDK's
/// content copying functionality.
/// </summary>
public class SkillPackageHelper
{
    private readonly string _packagesPath;
    private readonly string _sdkVersion;
    private readonly ITestOutputHelper _output;

    public SkillPackageHelper(string packagesPath, string sdkVersion, ITestOutputHelper output)
    {
        _packagesPath = packagesPath;
        _sdkVersion = sdkVersion;
        _output = output;
    }

    /// <summary>
    /// Creates and packs a skill package with the specified content.
    /// Returns the package ID and version.
    /// </summary>
    public async Task<(string PackageId, string Version, string NupkgPath)> CreateAndPackSkillPackageAsync(
        string testDirectory,
        string packageName,
        SkillPackageContent content)
    {
        var packageId = $"TestSkill.{packageName}";
        var version = "1.0.0";
        var projectDir = Path.Combine(testDirectory, $"{packageName}Package");
        Directory.CreateDirectory(projectDir);

        // Create the skill package .csproj
        var csprojContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <PackageId>{packageId}</PackageId>
    <Version>{version}</Version>
    <Authors>Test</Authors>
    <Description>Test skill package for integration testing</Description>
    <IsPackable>true</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""Zakira.Imprint.Sdk"" Version=""{_sdkVersion}"" />
  </ItemGroup>

  <ItemGroup>
    <Imprint Include=""skills\**\*"" />
    {(content.HasMcp ? @"<Imprint Include=""mcp\*.mcp.json"" Type=""Mcp"" />" : "")}
  </ItemGroup>
</Project>";

        await File.WriteAllTextAsync(Path.Combine(projectDir, $"{packageName}Package.csproj"), csprojContent);

        // Create nuget.config pointing to our local packages (for SDK reference)
        var nugetConfig = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <clear />
    <add key=""local"" value=""{_packagesPath}"" />
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
  </packageSources>
</configuration>";
        await File.WriteAllTextAsync(Path.Combine(projectDir, "nuget.config"), nugetConfig);

        // Create skill content
        var skillsDir = Path.Combine(projectDir, "skills", content.SkillName);
        Directory.CreateDirectory(skillsDir);
        await File.WriteAllTextAsync(
            Path.Combine(skillsDir, "instructions.md"),
            content.InstructionsContent ?? $"# {content.SkillName}\n\nThis is a test skill.");

        // Create MCP content if specified
        if (content.HasMcp && !string.IsNullOrEmpty(content.McpContent))
        {
            var mcpDir = Path.Combine(projectDir, "mcp");
            Directory.CreateDirectory(mcpDir);
            await File.WriteAllTextAsync(
                Path.Combine(mcpDir, $"{content.SkillName}.mcp.json"),
                content.McpContent);
        }

        _output.WriteLine($"Created skill package project: {projectDir}");

        // Pack the skill package
        var result = await RunDotnetAsync("pack", projectDir, $"-c Release -o \"{_packagesPath}\"");
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to pack skill package:\n{result.StandardError}\n{result.StandardOutput}");
        }

        var nupkgPath = Path.Combine(_packagesPath, $"{packageId}.{version}.nupkg");
        if (!File.Exists(nupkgPath))
        {
            throw new InvalidOperationException($"Expected nupkg not found at: {nupkgPath}");
        }

        _output.WriteLine($"Packed skill package: {nupkgPath}");
        return (packageId, version, nupkgPath);
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

        var completed = await Task.Run(() => process.WaitForExit(TimeSpan.FromMinutes(2)));

        if (!completed)
        {
            process.Kill();
            throw new TimeoutException($"dotnet {command} timed out after 2 minutes");
        }

        return new ProcessResult(process.ExitCode, stdoutBuilder.ToString(), stderrBuilder.ToString());
    }
}

/// <summary>
/// Content specification for a skill package.
/// </summary>
public class SkillPackageContent
{
    public string SkillName { get; set; } = "test-skill";
    public string? InstructionsContent { get; set; }
    public bool HasMcp { get; set; } = false;
    public string? McpContent { get; set; }

    public static SkillPackageContent Simple(string skillName = "test-skill") => new()
    {
        SkillName = skillName,
        InstructionsContent = $"# {skillName}\n\nThis is a test skill for integration testing.",
        HasMcp = false
    };

    public static SkillPackageContent WithMcp(string skillName = "test-skill", string? mcpContent = null) => new()
    {
        SkillName = skillName,
        InstructionsContent = $"# {skillName}\n\nThis is a test skill with MCP configuration.",
        HasMcp = true,
        McpContent = mcpContent ?? @"{
  ""servers"": {
    ""test-server"": {
      ""type"": ""stdio"",
      ""command"": ""npx"",
      ""args"": [""-y"", ""@test/mcp-server""]
    }
  }
}"
    };
}
