using Xunit;
using Zakira.Imprint.Sdk;

namespace Zakira.Imprint.Sdk.Tests;

public class AgentConfigTests : IDisposable
{
    private readonly string _testDir;

    public AgentConfigTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "ImprintAgentTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    // ── ParseAgentList ──────────────────────────────────────────────

    [Fact]
    public void ParseAgentList_SemicolonSeparated()
    {
        var result = AgentConfig.ParseAgentList("copilot;claude;cursor");
        Assert.Equal(new[] { "copilot", "claude", "cursor" }, result);
    }

    [Fact]
    public void ParseAgentList_CommaSeparated()
    {
        var result = AgentConfig.ParseAgentList("copilot,claude,cursor");
        Assert.Equal(new[] { "copilot", "claude", "cursor" }, result);
    }

    [Fact]
    public void ParseAgentList_MixedSeparators()
    {
        var result = AgentConfig.ParseAgentList("copilot;claude,cursor");
        Assert.Equal(new[] { "copilot", "claude", "cursor" }, result);
    }

    [Fact]
    public void ParseAgentList_TrimsWhitespace()
    {
        var result = AgentConfig.ParseAgentList("  copilot ; claude , cursor  ");
        Assert.Equal(new[] { "copilot", "claude", "cursor" }, result);
    }

    [Fact]
    public void ParseAgentList_RemovesDuplicates()
    {
        var result = AgentConfig.ParseAgentList("copilot;claude;copilot;claude");
        Assert.Equal(new[] { "copilot", "claude" }, result);
    }

    [Fact]
    public void ParseAgentList_CaseInsensitiveDuplicates()
    {
        var result = AgentConfig.ParseAgentList("Copilot;copilot;COPILOT");
        Assert.Single(result);
        Assert.Equal("copilot", result[0]);
    }

    [Fact]
    public void ParseAgentList_LowercasesOutput()
    {
        var result = AgentConfig.ParseAgentList("CLAUDE;Cursor");
        Assert.Equal(new[] { "claude", "cursor" }, result);
    }

    [Fact]
    public void ParseAgentList_SkipsEmptyEntries()
    {
        var result = AgentConfig.ParseAgentList("copilot;;claude;,;cursor");
        Assert.Equal(new[] { "copilot", "claude", "cursor" }, result);
    }

    [Fact]
    public void ParseAgentList_SingleAgent()
    {
        var result = AgentConfig.ParseAgentList("claude");
        Assert.Equal(new[] { "claude" }, result);
    }

    [Fact]
    public void ParseAgentList_EmptyString()
    {
        var result = AgentConfig.ParseAgentList("");
        Assert.Empty(result);
    }

    // ── DetectAgents ────────────────────────────────────────────────

    [Fact]
    public void DetectAgents_FindsCopilot()
    {
        Directory.CreateDirectory(Path.Combine(_testDir, ".github"));
        var detected = AgentConfig.DetectAgents(_testDir);
        Assert.Contains("copilot", detected);
    }

    [Fact]
    public void DetectAgents_FindsClaude()
    {
        Directory.CreateDirectory(Path.Combine(_testDir, ".claude"));
        var detected = AgentConfig.DetectAgents(_testDir);
        Assert.Contains("claude", detected);
    }

    [Fact]
    public void DetectAgents_FindsCursor()
    {
        Directory.CreateDirectory(Path.Combine(_testDir, ".cursor"));
        var detected = AgentConfig.DetectAgents(_testDir);
        Assert.Contains("cursor", detected);
    }

    [Fact]
    public void DetectAgents_FindsRoo()
    {
        Directory.CreateDirectory(Path.Combine(_testDir, ".roo"));
        var detected = AgentConfig.DetectAgents(_testDir);
        Assert.Contains("roo", detected);
    }

    [Fact]
    public void DetectAgents_FindsOpencode()
    {
        Directory.CreateDirectory(Path.Combine(_testDir, ".opencode"));
        var detected = AgentConfig.DetectAgents(_testDir);
        Assert.Contains("opencode", detected);
    }

    [Fact]
    public void DetectAgents_FindsMultiple()
    {
        Directory.CreateDirectory(Path.Combine(_testDir, ".github"));
        Directory.CreateDirectory(Path.Combine(_testDir, ".claude"));
        Directory.CreateDirectory(Path.Combine(_testDir, ".cursor"));
        Directory.CreateDirectory(Path.Combine(_testDir, ".roo"));
        Directory.CreateDirectory(Path.Combine(_testDir, ".opencode"));
        var detected = AgentConfig.DetectAgents(_testDir);
        Assert.Equal(5, detected.Count);
        Assert.Contains("copilot", detected);
        Assert.Contains("claude", detected);
        Assert.Contains("cursor", detected);
        Assert.Contains("roo", detected);
        Assert.Contains("opencode", detected);
    }

    [Fact]
    public void DetectAgents_ReturnsEmpty_WhenNonePresent()
    {
        var detected = AgentConfig.DetectAgents(_testDir);
        Assert.Empty(detected);
    }

    // ── ResolveAgents ───────────────────────────────────────────────

    [Fact]
    public void ResolveAgents_ExplicitTakesPriority()
    {
        // Even with .github/ present, explicit setting wins
        Directory.CreateDirectory(Path.Combine(_testDir, ".github"));
        var result = AgentConfig.ResolveAgents(_testDir, "claude;cursor", autoDetect: true, defaultAgents: "copilot");
        Assert.Equal(new[] { "claude", "cursor" }, result);
    }

    [Fact]
    public void ResolveAgents_AutoDetectWhenNoExplicit()
    {
        Directory.CreateDirectory(Path.Combine(_testDir, ".claude"));
        Directory.CreateDirectory(Path.Combine(_testDir, ".cursor"));
        var result = AgentConfig.ResolveAgents(_testDir, "", autoDetect: true, defaultAgents: "copilot");
        Assert.Equal(2, result.Count);
        Assert.Contains("claude", result);
        Assert.Contains("cursor", result);
    }

    [Fact]
    public void ResolveAgents_FallsBackToDefault_WhenNoDetection()
    {
        var result = AgentConfig.ResolveAgents(_testDir, "", autoDetect: true, defaultAgents: "copilot");
        Assert.Equal(new[] { "copilot" }, result);
    }

    [Fact]
    public void ResolveAgents_SkipsAutoDetect_WhenDisabled()
    {
        Directory.CreateDirectory(Path.Combine(_testDir, ".claude"));
        var result = AgentConfig.ResolveAgents(_testDir, "", autoDetect: false, defaultAgents: "copilot");
        Assert.Equal(new[] { "copilot" }, result);
    }

    [Fact]
    public void ResolveAgents_ReturnsEmpty_WhenNoAgentsFoundAndNoDefaults()
    {
        // New behavior: return empty list instead of "copilot" fallback
        var result = AgentConfig.ResolveAgents(_testDir, "", autoDetect: true, defaultAgents: "");
        Assert.Empty(result);
    }

    [Fact]
    public void ResolveAgents_ReturnsEmpty_WhenAutoDetectDisabledAndNoDefaults()
    {
        // New behavior: return empty list instead of "copilot" fallback
        var result = AgentConfig.ResolveAgents(_testDir, "", autoDetect: false, defaultAgents: "");
        Assert.Empty(result);
    }

    [Fact]
    public void ResolveAgents_DefaultCanBeMultiple()
    {
        var result = AgentConfig.ResolveAgents(_testDir, "", autoDetect: false, defaultAgents: "claude;cursor");
        Assert.Equal(new[] { "claude", "cursor" }, result);
    }

    // ── GetSkillsPath ───────────────────────────────────────────────

    [Fact]
    public void GetSkillsPath_Copilot()
    {
        var path = AgentConfig.GetSkillsPath(_testDir, "copilot");
        Assert.Equal(Path.Combine(_testDir, ".github", "skills"), path);
    }

    [Fact]
    public void GetSkillsPath_Claude()
    {
        var path = AgentConfig.GetSkillsPath(_testDir, "claude");
        Assert.Equal(Path.Combine(_testDir, ".claude", "skills"), path);
    }

    [Fact]
    public void GetSkillsPath_Cursor()
    {
        var path = AgentConfig.GetSkillsPath(_testDir, "cursor");
        Assert.Equal(Path.Combine(_testDir, ".cursor", "rules"), path);
    }

    [Fact]
    public void GetSkillsPath_Roo()
    {
        var path = AgentConfig.GetSkillsPath(_testDir, "roo");
        Assert.Equal(Path.Combine(_testDir, ".roo", "rules"), path);
    }

    [Fact]
    public void GetSkillsPath_Opencode()
    {
        var path = AgentConfig.GetSkillsPath(_testDir, "opencode");
        Assert.Equal(Path.Combine(_testDir, ".opencode", "skills"), path);
    }

    [Fact]
    public void GetSkillsPath_UnknownAgent_UsesConvention()
    {
        var path = AgentConfig.GetSkillsPath(_testDir, "futureagent");
        Assert.Equal(Path.Combine(_testDir, ".futureagent", "rules"), path);
    }

    // ── GetMcpPath ──────────────────────────────────────────────────

    [Fact]
    public void GetMcpPath_Copilot()
    {
        var path = AgentConfig.GetMcpPath(_testDir, "copilot");
        Assert.Equal(Path.Combine(_testDir, ".vscode", "mcp.json"), path);
    }

    [Fact]
    public void GetMcpPath_Claude()
    {
        var path = AgentConfig.GetMcpPath(_testDir, "claude");
        Assert.Equal(Path.Combine(_testDir, ".claude", "mcp.json"), path);
    }

    [Fact]
    public void GetMcpPath_Cursor()
    {
        var path = AgentConfig.GetMcpPath(_testDir, "cursor");
        Assert.Equal(Path.Combine(_testDir, ".cursor", "mcp.json"), path);
    }

    [Fact]
    public void GetMcpPath_Roo()
    {
        var path = AgentConfig.GetMcpPath(_testDir, "roo");
        Assert.Equal(Path.Combine(_testDir, ".roo", "mcp.json"), path);
    }

    [Fact]
    public void GetMcpPath_Opencode()
    {
        var path = AgentConfig.GetMcpPath(_testDir, "opencode");
        Assert.Equal(Path.Combine(_testDir, "opencode.json"), path);
    }

    [Fact]
    public void GetMcpPath_UnknownAgent_UsesConvention()
    {
        var path = AgentConfig.GetMcpPath(_testDir, "windsurf");
        Assert.Equal(Path.Combine(_testDir, ".windsurf", "mcp.json"), path);
    }

    // ── GetMcpDirectory ─────────────────────────────────────────────

    [Fact]
    public void GetMcpDirectory_Copilot()
    {
        var dir = AgentConfig.GetMcpDirectory(_testDir, "copilot");
        Assert.Equal(Path.Combine(_testDir, ".vscode"), dir);
    }

    [Fact]
    public void GetMcpDirectory_Claude()
    {
        var dir = AgentConfig.GetMcpDirectory(_testDir, "claude");
        Assert.Equal(Path.Combine(_testDir, ".claude"), dir);
    }

    [Fact]
    public void GetMcpDirectory_Cursor()
    {
        var dir = AgentConfig.GetMcpDirectory(_testDir, "cursor");
        Assert.Equal(Path.Combine(_testDir, ".cursor"), dir);
    }

    [Fact]
    public void GetMcpDirectory_Roo()
    {
        var dir = AgentConfig.GetMcpDirectory(_testDir, "roo");
        Assert.Equal(Path.Combine(_testDir, ".roo"), dir);
    }

    [Fact]
    public void GetMcpDirectory_Opencode()
    {
        var dir = AgentConfig.GetMcpDirectory(_testDir, "opencode");
        Assert.Equal(_testDir, dir);
    }

    [Fact]
    public void GetMcpDirectory_UnknownAgent_UsesConvention()
    {
        var dir = AgentConfig.GetMcpDirectory(_testDir, "windsurf");
        Assert.Equal(Path.Combine(_testDir, ".windsurf"), dir);
    }

    // ── KnownAgents ─────────────────────────────────────────────────

    [Fact]
    public void KnownAgents_ContainsSixAgents()
    {
        Assert.Equal(6, AgentConfig.KnownAgents.Count);
        Assert.True(AgentConfig.KnownAgents.ContainsKey("copilot"));
        Assert.True(AgentConfig.KnownAgents.ContainsKey("claude"));
        Assert.True(AgentConfig.KnownAgents.ContainsKey("cursor"));
        Assert.True(AgentConfig.KnownAgents.ContainsKey("roo"));
        Assert.True(AgentConfig.KnownAgents.ContainsKey("opencode"));
        Assert.True(AgentConfig.KnownAgents.ContainsKey("windsurf"));
    }

    [Fact]
    public void KnownAgents_CaseInsensitiveLookup()
    {
        Assert.True(AgentConfig.KnownAgents.ContainsKey("Copilot"));
        Assert.True(AgentConfig.KnownAgents.ContainsKey("CLAUDE"));
        Assert.True(AgentConfig.KnownAgents.ContainsKey("cUrSoR"));
        Assert.True(AgentConfig.KnownAgents.ContainsKey("ROO"));
        Assert.True(AgentConfig.KnownAgents.ContainsKey("OpenCode"));
        Assert.True(AgentConfig.KnownAgents.ContainsKey("WindSurf"));
    }

    [Fact]
    public void KnownAgents_CopilotDefinition()
    {
        var def = AgentConfig.KnownAgents["copilot"];
        Assert.Equal("copilot", def.Name);
        Assert.Equal(".github", def.DetectionDir);
        Assert.Equal(".github" + Path.DirectorySeparatorChar + "skills", def.SkillsSubPath);
        Assert.Equal(".vscode", def.McpSubPath);
        Assert.Equal("mcp.json", def.McpFileName);
    }

    [Fact]
    public void KnownAgents_ClaudeDefinition()
    {
        var def = AgentConfig.KnownAgents["claude"];
        Assert.Equal("claude", def.Name);
        Assert.Equal(".claude", def.DetectionDir);
        Assert.Equal(".claude" + Path.DirectorySeparatorChar + "skills", def.SkillsSubPath);
        Assert.Equal(".claude", def.McpSubPath);
        Assert.Equal("mcp.json", def.McpFileName);
    }

    [Fact]
    public void KnownAgents_CursorDefinition()
    {
        var def = AgentConfig.KnownAgents["cursor"];
        Assert.Equal("cursor", def.Name);
        Assert.Equal(".cursor", def.DetectionDir);
        Assert.Equal(".cursor" + Path.DirectorySeparatorChar + "rules", def.SkillsSubPath);
        Assert.Equal(".cursor", def.McpSubPath);
        Assert.Equal("mcp.json", def.McpFileName);
    }

    [Fact]
    public void KnownAgents_RooDefinition()
    {
        var def = AgentConfig.KnownAgents["roo"];
        Assert.Equal("roo", def.Name);
        Assert.Equal(".roo", def.DetectionDir);
        Assert.Equal(".roo" + Path.DirectorySeparatorChar + "rules", def.SkillsSubPath);
        Assert.Equal(".roo", def.McpSubPath);
        Assert.Equal("mcp.json", def.McpFileName);
        Assert.Equal("mcpServers", def.McpRootKey);
    }

    [Fact]
    public void KnownAgents_OpencodeDefinition()
    {
        var def = AgentConfig.KnownAgents["opencode"];
        Assert.Equal("opencode", def.Name);
        Assert.Equal(".opencode", def.DetectionDir);
        Assert.Equal(".opencode" + Path.DirectorySeparatorChar + "skills", def.SkillsSubPath);
        Assert.Equal("", def.McpSubPath);
        Assert.Equal("opencode.json", def.McpFileName);
        Assert.Equal("mcp", def.McpRootKey);
    }
}
