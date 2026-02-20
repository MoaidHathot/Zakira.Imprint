using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Zakira.Imprint.Sdk
{
    /// <summary>
    /// Agent configuration for multi-agent support.
    /// Maps agent identifiers to their native directory conventions.
    /// </summary>
    public static class AgentConfig
    {
        /// <summary>
        /// Known agent definitions with their native paths.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, AgentDefinition> KnownAgents =
            new Dictionary<string, AgentDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["copilot"] = new AgentDefinition(
                    Name: "copilot",
                    DetectionDir: ".github",
                    SkillsSubPath: ".github" + Path.DirectorySeparatorChar + "skills",
                    McpSubPath: ".vscode",
                    McpFileName: "mcp.json",
                    McpRootKey: "servers"),
                ["claude"] = new AgentDefinition(
                    Name: "claude",
                    DetectionDir: ".claude",
                    SkillsSubPath: ".claude" + Path.DirectorySeparatorChar + "skills",
                    McpSubPath: ".claude",
                    McpFileName: "mcp.json",
                    McpRootKey: "mcpServers"),
                ["cursor"] = new AgentDefinition(
                    Name: "cursor",
                    DetectionDir: ".cursor",
                    SkillsSubPath: ".cursor" + Path.DirectorySeparatorChar + "rules",
                    McpSubPath: ".cursor",
                    McpFileName: "mcp.json",
                    McpRootKey: "mcpServers"),
                ["roo"] = new AgentDefinition(
                    Name: "roo",
                    DetectionDir: ".roo",
                    SkillsSubPath: ".roo" + Path.DirectorySeparatorChar + "rules",
                    McpSubPath: ".roo",
                    McpFileName: "mcp.json",
                    McpRootKey: "mcpServers"),
                ["opencode"] = new AgentDefinition(
                    Name: "opencode",
                    DetectionDir: ".opencode",
                    SkillsSubPath: ".opencode" + Path.DirectorySeparatorChar + "skills",
                    McpSubPath: "",
                    McpFileName: "opencode.json",
                    McpRootKey: "mcp"),
                ["windsurf"] = new AgentDefinition(
                    Name: "windsurf",
                    DetectionDir: ".windsurf",
                    SkillsSubPath: ".windsurf" + Path.DirectorySeparatorChar + "rules",
                    McpSubPath: ".windsurf",
                    McpFileName: "mcp.json",
                    McpRootKey: "mcpServers"),
            };

        /// <summary>
        /// Resolves the final list of target agents using the priority hierarchy:
        /// 1. Explicit consumer setting (targetAgents parameter)
        /// 2. Auto-detection (if autoDetect is true)
        /// 3. Default agents fallback (if set)
        /// 4. Empty list (no agents = no files created)
        /// </summary>
        public static List<string> ResolveAgents(
            string projectDirectory,
            string targetAgents,
            bool autoDetect,
            string defaultAgents)
        {
            // 1. If consumer explicitly set agents, use those
            if (!string.IsNullOrWhiteSpace(targetAgents))
            {
                return ParseAgentList(targetAgents);
            }

            // 2. Auto-detect: scan for agent directories
            if (autoDetect)
            {
                var detected = DetectAgents(projectDirectory);
                if (detected.Count > 0)
                {
                    return detected;
                }
            }

            // 3. Fallback to defaults (if set)
            if (!string.IsNullOrWhiteSpace(defaultAgents))
            {
                return ParseAgentList(defaultAgents);
            }

            // 4. No agents found, no defaults set = empty list (no files created)
            return new List<string>();
        }

        /// <summary>
        /// Detects which agents are present by checking for their detection directories.
        /// </summary>
        public static List<string> DetectAgents(string projectDirectory)
        {
            var detected = new List<string>();
            foreach (var kvp in KnownAgents)
            {
                var detectionPath = Path.Combine(projectDirectory, kvp.Value.DetectionDir);
                if (Directory.Exists(detectionPath))
                {
                    detected.Add(kvp.Key);
                }
            }
            return detected;
        }

        /// <summary>
        /// Parses a semicolon-separated agent list (e.g. "copilot;claude;cursor").
        /// </summary>
        public static List<string> ParseAgentList(string agents)
        {
            return agents
                .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(a => a.Trim().ToLowerInvariant())
                .Where(a => !string.IsNullOrEmpty(a))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Gets the absolute skills destination path for an agent.
        /// </summary>
        public static string GetSkillsPath(string projectDirectory, string agentName)
        {
            if (KnownAgents.TryGetValue(agentName, out var def))
            {
                return Path.Combine(projectDirectory, def.SkillsSubPath);
            }
            // Unknown agent: use windsurf convention (.{agent}/rules/)
            return Path.Combine(projectDirectory, $".{agentName}", "rules");
        }

        /// <summary>
        /// Gets the absolute MCP config file path for an agent.
        /// </summary>
        public static string GetMcpPath(string projectDirectory, string agentName)
        {
            if (KnownAgents.TryGetValue(agentName, out var def))
            {
                return Path.Combine(projectDirectory, def.McpSubPath, def.McpFileName);
            }
            // Unknown agent: use .{agent}/mcp.json convention
            return Path.Combine(projectDirectory, $".{agentName}", "mcp.json");
        }

        /// <summary>
        /// Gets the MCP directory path (parent of the mcp.json file) for an agent.
        /// </summary>
        public static string GetMcpDirectory(string projectDirectory, string agentName)
        {
            if (KnownAgents.TryGetValue(agentName, out var def))
            {
                return Path.Combine(projectDirectory, def.McpSubPath);
            }
            return Path.Combine(projectDirectory, $".{agentName}");
        }

        /// <summary>
        /// Gets the MCP root key for an agent (e.g., "servers" for VS Code, "mcpServers" for Claude/Cursor).
        /// </summary>
        public static string GetMcpRootKey(string agentName)
        {
            if (KnownAgents.TryGetValue(agentName, out var def))
            {
                return def.McpRootKey;
            }
            // Default to "servers" for unknown agents
            return "servers";
        }
    }

    /// <summary>
    /// Defines an AI agent's directory conventions.
    /// </summary>
    /// <param name="Name">The agent identifier (e.g., "copilot", "claude", "cursor").</param>
    /// <param name="DetectionDir">Directory to check for auto-detection (e.g., ".github", ".claude").</param>
    /// <param name="SkillsSubPath">Relative path where skills are stored (e.g., ".github/skills").</param>
    /// <param name="McpSubPath">Relative path to the MCP config directory (e.g., ".vscode", ".claude").</param>
    /// <param name="McpFileName">Name of the MCP config file (e.g., "mcp.json").</param>
    /// <param name="McpRootKey">JSON root key for MCP servers ("servers" for VS Code, "mcpServers" for Claude/Cursor).</param>
    public record AgentDefinition(
        string Name,
        string DetectionDir,
        string SkillsSubPath,
        string McpSubPath,
        string McpFileName,
        string McpRootKey);
}
