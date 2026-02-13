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
                    McpFileName: "mcp.json"),
                ["claude"] = new AgentDefinition(
                    Name: "claude",
                    DetectionDir: ".claude",
                    SkillsSubPath: ".claude" + Path.DirectorySeparatorChar + "skills",
                    McpSubPath: ".claude",
                    McpFileName: "mcp.json"),
                ["cursor"] = new AgentDefinition(
                    Name: "cursor",
                    DetectionDir: ".cursor",
                    SkillsSubPath: ".cursor" + Path.DirectorySeparatorChar + "rules",
                    McpSubPath: ".cursor",
                    McpFileName: "mcp.json"),
            };

        /// <summary>
        /// Resolves the final list of target agents using the priority hierarchy:
        /// 1. Explicit consumer setting (targetAgents parameter)
        /// 2. Auto-detection (if autoDetect is true)
        /// 3. Default agents fallback
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

            // 3. Fallback to defaults
            if (!string.IsNullOrWhiteSpace(defaultAgents))
            {
                return ParseAgentList(defaultAgents);
            }

            // Ultimate fallback
            return new List<string> { "copilot" };
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
            // Unknown agent: use .{agent}/skills/ convention
            return Path.Combine(projectDirectory, $".{agentName}", "skills");
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
    }

    /// <summary>
    /// Defines an AI agent's directory conventions.
    /// </summary>
    public record AgentDefinition(
        string Name,
        string DetectionDir,
        string SkillsSubPath,
        string McpSubPath,
        string McpFileName);
}
