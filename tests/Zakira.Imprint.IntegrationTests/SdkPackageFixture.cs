using System.Diagnostics;
using System.Text;
using Xunit;

namespace Zakira.Imprint.IntegrationTests;

/// <summary>
/// Shared fixture that packs the SDK once for all integration tests.
/// This fixture is shared across all test classes in the collection.
/// </summary>
public class SdkPackageFixture : IAsyncLifetime
{
    public string PackagesPath { get; private set; } = string.Empty;
    public string NupkgPath { get; private set; } = string.Empty;
    public string SdkProjectPath { get; private set; } = string.Empty;
    public string SdkVersion { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        // Create a shared packages directory
        PackagesPath = Path.Combine(Path.GetTempPath(), "Zakira.Imprint.IntegrationTests", "shared-packages");
        
        // Clean any existing packages to ensure fresh build
        if (Directory.Exists(PackagesPath))
        {
            Directory.Delete(PackagesPath, recursive: true);
        }
        Directory.CreateDirectory(PackagesPath);

        // Find the SDK project path
        var assemblyLocation = Path.GetDirectoryName(typeof(SdkPackageFixture).Assembly.Location)!;
        SdkProjectPath = Path.GetFullPath(Path.Combine(assemblyLocation, "..", "..", "..", "..", "..", "src", "Zakira.Imprint.Sdk", "Zakira.Imprint.Sdk.csproj"));

        if (!File.Exists(SdkProjectPath))
        {
            throw new InvalidOperationException($"SDK project not found at: {SdkProjectPath}");
        }

        // First build the SDK in Release mode
        var buildResult = await RunDotnetAsync("build", Path.GetDirectoryName(SdkProjectPath)!, "-c Release");
        if (buildResult.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to build SDK:\n{buildResult.StandardError}\n{buildResult.StandardOutput}");
        }

        // Pack the SDK
        var packResult = await RunDotnetAsync("pack", Path.GetDirectoryName(SdkProjectPath)!, 
            $"-c Release -o \"{PackagesPath}\" --no-build");
        
        if (packResult.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to pack SDK:\n{packResult.StandardError}\n{packResult.StandardOutput}");
        }

        // Find the nupkg file
        var nupkgFiles = Directory.GetFiles(PackagesPath, "*.nupkg");
        if (nupkgFiles.Length == 0)
        {
            throw new InvalidOperationException($"No nupkg file found in {PackagesPath}");
        }
        
        NupkgPath = nupkgFiles[0];
        
        // Extract version from filename (e.g., Zakira.Imprint.Sdk.1.0.1-preview.nupkg)
        var fileName = Path.GetFileNameWithoutExtension(NupkgPath);
        SdkVersion = fileName.Replace("Zakira.Imprint.Sdk.", "");
        
        Console.WriteLine($"Packed SDK: {NupkgPath}");
        Console.WriteLine($"SDK Version: {SdkVersion}");
    }

    public Task DisposeAsync()
    {
        // Keep the packages directory for debugging if needed
        // It will be cleaned up on next test run
        return Task.CompletedTask;
    }

    private static async Task<ProcessResult> RunDotnetAsync(string command, string workingDirectory, params string[] args)
    {
        var arguments = $"{command} {string.Join(" ", args)}";
        Console.WriteLine($"[SdkPackageFixture] Running: dotnet {arguments}");
        Console.WriteLine($"[SdkPackageFixture] Working directory: {workingDirectory}");

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
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stderrBuilder.AppendLine(e.Data);
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
}

/// <summary>
/// Collection definition for integration tests that share the SDK package fixture.
/// </summary>
[CollectionDefinition("SdkPackage")]
public class SdkPackageCollection : ICollectionFixture<SdkPackageFixture>
{
}
