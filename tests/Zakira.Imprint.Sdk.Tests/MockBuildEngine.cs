using Microsoft.Build.Framework;

namespace Zakira.Imprint.Sdk.Tests;

/// <summary>
/// Minimal IBuildEngine implementation for unit testing MSBuild tasks.
/// Captures logged messages, warnings, and errors.
/// </summary>
public class MockBuildEngine : IBuildEngine
{
    public List<string> Messages { get; } = new();
    public List<string> Warnings { get; } = new();
    public List<string> Errors { get; } = new();

    public bool ContinueOnError => false;
    public int LineNumberOfTaskNode => 0;
    public int ColumnNumberOfTaskNode => 0;
    public string ProjectFileOfTaskNode => "test.csproj";

    public void LogErrorEvent(BuildErrorEventArgs e)
    {
        Errors.Add(e.Message ?? string.Empty);
    }

    public void LogWarningEvent(BuildWarningEventArgs e)
    {
        Warnings.Add(e.Message ?? string.Empty);
    }

    public void LogMessageEvent(BuildMessageEventArgs e)
    {
        Messages.Add(e.Message ?? string.Empty);
    }

    public void LogCustomEvent(CustomBuildEventArgs e) { }

    public bool BuildProjectFile(string projectFileName, string[] targetNames, System.Collections.IDictionary globalProperties, System.Collections.IDictionary targetOutputs) => true;
}
