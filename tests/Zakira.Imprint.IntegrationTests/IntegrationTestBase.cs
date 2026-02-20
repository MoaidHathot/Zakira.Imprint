using System.Diagnostics;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Zakira.Imprint.IntegrationTests;

/// <summary>
/// Base class for integration tests that run real dotnet pack and dotnet build commands.
/// </summary>
public abstract class IntegrationTestBase : IDisposable
{
    protected readonly ITestOutputHelper Output;
    protected readonly string TestDirectory;
    protected readonly string SdkProjectPath;
    protected readonly string LocalPackagesPath;

    protected IntegrationTestBase(ITestOutputHelper output)
    {
        Output = output;
        
        // Create a unique test directory for this test
        TestDirectory = Path.Combine(Path.GetTempPath(), "Zakira.Imprint.IntegrationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TestDirectory);
        
        // Find the SDK project path (relative to test assembly location)
        var assemblyLocation = Path.GetDirectoryName(typeof(IntegrationTestBase).Assembly.Location)!;
        SdkProjectPath = Path.GetFullPath(Path.Combine(assemblyLocation, "..", "..", "..", "..", "..", "src", "Zakira.Imprint.Sdk", "Zakira.Imprint.Sdk.csproj"));
        
        // Local packages folder for the packed SDK
        LocalPackagesPath = Path.Combine(TestDirectory, "packages");
        Directory.CreateDirectory(LocalPackagesPath);
        
        Output.WriteLine($"Test directory: {TestDirectory}");
        Output.WriteLine($"SDK project path: {SdkProjectPath}");
    }

    /// <summary>
    /// Packs the Zakira.Imprint.Sdk project and returns the path to the nupkg file.
    /// </summary>
    protected async Task<string> PackSdkAsync()
    {
        var result = await RunDotnetAsync("pack", SdkProjectPath, 
            $"-o \"{LocalPackagesPath}\"",
            "--no-build",
            "-c Release");
        
        Assert.True(result.ExitCode == 0, $"dotnet pack failed:\n{result.StandardError}");
        
        // Find the nupkg file
        var nupkgFiles = Directory.GetFiles(LocalPackagesPath, "*.nupkg");
        Assert.Single(nupkgFiles);
        
        Output.WriteLine($"Packed SDK: {nupkgFiles[0]}");
        return nupkgFiles[0];
    }

    /// <summary>
    /// Creates a test consumer project that references the Zakira.Imprint.Sdk package.
    /// </summary>
    protected async Task<string> CreateConsumerProjectAsync(string projectName, string? additionalProps = null)
    {
        var projectDir = Path.Combine(TestDirectory, projectName);
        Directory.CreateDirectory(projectDir);
        
        // Create the csproj file
        var csprojContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <OutputType>Library</OutputType>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    {additionalProps ?? ""}
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""Zakira.Imprint.Sdk"" Version=""*"" />
  </ItemGroup>
</Project>";

        var csprojPath = Path.Combine(projectDir, $"{projectName}.csproj");
        await File.WriteAllTextAsync(csprojPath, csprojContent);
        
        // Create a nuget.config that points to our local packages folder
        var nugetConfigContent = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <clear />
    <add key=""local"" value=""{LocalPackagesPath}"" />
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
  </packageSources>
</configuration>";
        
        await File.WriteAllTextAsync(Path.Combine(projectDir, "nuget.config"), nugetConfigContent);
        
        // Create a minimal Class1.cs so the project compiles
        await File.WriteAllTextAsync(Path.Combine(projectDir, "Class1.cs"), @"namespace TestProject;
public class Class1 { }");
        
        Output.WriteLine($"Created consumer project: {csprojPath}");
        return projectDir;
    }

    /// <summary>
    /// Creates agent directory marker(s) in the test directory to simulate agent detection.
    /// </summary>
    protected void CreateAgentDirectory(string agentName)
    {
        var agentDir = agentName.ToLowerInvariant() switch
        {
            "copilot" => ".github",
            "claude" => ".claude",
            "cursor" => ".cursor",
            "roo" => ".roo",
            "opencode" => ".opencode",
            "windsurf" => ".windsurf",
            _ => $".{agentName.ToLowerInvariant()}"
        };
        
        var fullPath = Path.Combine(TestDirectory, agentDir);
        Directory.CreateDirectory(fullPath);
        Output.WriteLine($"Created agent directory: {fullPath}");
    }

    /// <summary>
    /// Creates a sample skill package content in the consumer project.
    /// </summary>
    protected async Task CreateSkillContentAsync(string projectDir, string skillName = "test-skill")
    {
        // Create the imprint directory structure
        var imprintDir = Path.Combine(projectDir, "imprint");
        var skillDir = Path.Combine(imprintDir, skillName);
        Directory.CreateDirectory(skillDir);
        
        // Create a sample instruction file
        await File.WriteAllTextAsync(
            Path.Combine(skillDir, "instructions.md"),
            $"# {skillName}\n\nThis is a test skill for integration testing.");
        
        // Create a sample MCP configuration
        var mcpContent = @"{
  ""mcpServers"": {
    ""test-server"": {
      ""command"": ""npx"",
      ""args"": [""-y"", ""test-mcp-server""]
    }
  }
}";
        await File.WriteAllTextAsync(Path.Combine(skillDir, "mcp.json"), mcpContent);
        
        Output.WriteLine($"Created skill content in: {skillDir}");
    }

    /// <summary>
    /// Runs dotnet build on the specified project directory.
    /// </summary>
    protected async Task<ProcessResult> BuildProjectAsync(string projectDir, params string[] additionalArgs)
    {
        var args = new List<string> { "-c Release" };
        args.AddRange(additionalArgs);
        
        return await RunDotnetAsync("build", projectDir, args.ToArray());
    }

    /// <summary>
    /// Runs dotnet clean on the specified project directory.
    /// </summary>
    protected async Task<ProcessResult> CleanProjectAsync(string projectDir, params string[] additionalArgs)
    {
        var args = new List<string> { "-c Release" };
        args.AddRange(additionalArgs);
        
        return await RunDotnetAsync("clean", projectDir, args.ToArray());
    }

    /// <summary>
    /// Runs a dotnet command and returns the result.
    /// </summary>
    protected async Task<ProcessResult> RunDotnetAsync(string command, string workingDirectory, params string[] args)
    {
        var arguments = $"{command} {string.Join(" ", args)}";
        Output.WriteLine($"Running: dotnet {arguments}");
        Output.WriteLine($"Working directory: {workingDirectory}");
        
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
                Output.WriteLine($"[stdout] {e.Data}");
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stderrBuilder.AppendLine(e.Data);
                Output.WriteLine($"[stderr] {e.Data}");
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var completed = await Task.Run(() => process.WaitForExit(TimeSpan.FromMinutes(5)));
        
        if (!completed)
        {
            process.Kill();
            throw new TimeoutException($"dotnet {command} timed out after 5 minutes");
        }

        return new ProcessResult(process.ExitCode, stdoutBuilder.ToString(), stderrBuilder.ToString());
    }

    /// <summary>
    /// Checks if a file exists at the specified path relative to the test directory.
    /// </summary>
    protected bool FileExists(params string[] pathParts)
    {
        var fullPath = Path.Combine(new[] { TestDirectory }.Concat(pathParts).ToArray());
        return File.Exists(fullPath);
    }

    /// <summary>
    /// Checks if a directory exists at the specified path relative to the test directory.
    /// </summary>
    protected bool DirectoryExists(params string[] pathParts)
    {
        var fullPath = Path.Combine(new[] { TestDirectory }.Concat(pathParts).ToArray());
        return Directory.Exists(fullPath);
    }

    /// <summary>
    /// Reads file content at the specified path relative to the test directory.
    /// </summary>
    protected async Task<string> ReadFileAsync(params string[] pathParts)
    {
        var fullPath = Path.Combine(new[] { TestDirectory }.Concat(pathParts).ToArray());
        return await File.ReadAllTextAsync(fullPath);
    }

    public void Dispose()
    {
        // Clean up the test directory
        try
        {
            if (Directory.Exists(TestDirectory))
            {
                Directory.Delete(TestDirectory, recursive: true);
                Output.WriteLine($"Cleaned up test directory: {TestDirectory}");
            }
        }
        catch (Exception ex)
        {
            Output.WriteLine($"Warning: Failed to clean up test directory: {ex.Message}");
        }
    }
}

/// <summary>
/// Result of running a process.
/// </summary>
public record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}
