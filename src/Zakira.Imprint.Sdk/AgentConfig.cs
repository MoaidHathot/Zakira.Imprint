#nullable enable

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
        /// VCS markers that indicate the root of a repository.
        /// These are the most authoritative indicators.
        /// </summary>
        private static readonly string[] VcsMarkers = new[]
        {
            ".git",           // Git repository
            ".svn",           // SVN repository
            ".hg",            // Mercurial repository
        };

        /// <summary>
        /// IDE/editor project directories that indicate a workspace root.
        /// Only includes markers that are project-specific (not found in user home directories).
        /// </summary>
        private static readonly string[] IdeMarkers = new[]
        {
            ".vs",            // Visual Studio (project-specific settings)
            ".idea",          // JetBrains IDEs (project-specific settings)
        };

        /// <summary>
        /// Solution file extensions that indicate a workspace root.
        /// Used as a fallback when no VCS or IDE markers are found.
        /// </summary>
        private static readonly string[] SolutionExtensions = new[]
        {
            "*.sln",
            "*.slnx",
        };
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
        /// Finds the repository root by walking up from the starting directory.
        /// Priority order:
        /// 1. VCS markers (.git, .svn, .hg) - most authoritative
        /// 2. IDE markers (.vs, .idea) - project-specific IDE directories
        /// 3. Solution files (*.sln, *.slnx) - fallback
        /// Returns null if no repository root is found.
        /// </summary>
        /// <param name="startDirectory">The directory to start searching from.</param>
        /// <returns>The repository root path, or null if not found.</returns>
        public static string? FindRepositoryRoot(string startDirectory)
        {
            var fullPath = Path.GetFullPath(startDirectory);
            var root = Path.GetPathRoot(fullPath);

            // Pass 1: VCS markers (most authoritative)
            var result = FindDirectoryWithMarker(fullPath, root, VcsMarkers);
            if (result != null) return result;

            // Pass 2: IDE markers (project-specific directories)
            result = FindDirectoryWithMarker(fullPath, root, IdeMarkers);
            if (result != null) return result;

            // Pass 3: Solution files (fallback)
            var current = fullPath;
            while (!string.IsNullOrEmpty(current) && current != root)
            {
                foreach (var pattern in SolutionExtensions)
                {
                    try
                    {
                        if (Directory.GetFiles(current, pattern).Length > 0)
                        {
                            return current;
                        }
                    }
                    catch
                    {
                        // Ignore access errors
                    }
                }
                current = Path.GetDirectoryName(current);
            }

            return null;
        }

        /// <summary>
        /// Helper method to find a directory containing any of the specified markers.
        /// Walks up the directory tree from startPath until it reaches root.
        /// </summary>
        private static string? FindDirectoryWithMarker(string startPath, string? root, string[] markers)
        {
            var current = startPath;
            while (!string.IsNullOrEmpty(current) && current != root)
            {
                foreach (var marker in markers)
                {
                    var markerPath = Path.Combine(current, marker);
                    if (Directory.Exists(markerPath) || File.Exists(markerPath))
                    {
                        return current;
                    }
                }
                current = Path.GetDirectoryName(current);
            }
            return null;
        }

        /// <summary>
        /// Resolves the root directory for Imprint operations.
        /// Priority:
        /// 1. Explicit rootDirectory parameter (ImprintRootDirectory from MSBuild)
        /// 2. Repository root (found by walking up from projectDirectory)
        /// 3. Project directory (fallback)
        /// </summary>
        /// <param name="projectDirectory">The project directory (MSBuildProjectDirectory).</param>
        /// <param name="rootDirectory">Explicit root directory override (may be null/empty).</param>
        /// <returns>The resolved root directory for Imprint operations.</returns>
        public static string ResolveRootDirectory(string projectDirectory, string? rootDirectory)
        {
            // 1. If explicit root is set, use it
            if (!string.IsNullOrWhiteSpace(rootDirectory))
            {
                return Path.GetFullPath(rootDirectory);
            }

            // 2. Try to find repository root
            var repoRoot = FindRepositoryRoot(projectDirectory);
            if (repoRoot != null)
            {
                return repoRoot;
            }

            // 3. Fallback to project directory
            return Path.GetFullPath(projectDirectory);
        }

        /// <summary>
        /// Resolves the final list of target agents using the priority hierarchy:
        /// 1. Explicit consumer setting (targetAgents parameter)
        /// 2. Auto-detection (if autoDetect is true)
        /// 3. Default agents fallback (if set)
        /// 4. Empty list (no agents = no files created)
        /// </summary>
        /// <param name="rootDirectory">The root directory to scan for agents (repository root).</param>
        /// <param name="targetAgents">Explicit agent list from consumer.</param>
        /// <param name="autoDetect">Whether to auto-detect agents.</param>
        /// <param name="defaultAgents">Default agents to use if none detected.</param>
        public static List<string> ResolveAgents(
            string rootDirectory,
            string targetAgents,
            bool autoDetect,
            string defaultAgents)
        {
            // 1. If consumer explicitly set agents, use those
            if (!string.IsNullOrWhiteSpace(targetAgents))
            {
                return ParseAgentList(targetAgents);
            }

            // 2. Auto-detect: scan for agent directories in root directory
            if (autoDetect)
            {
                var detected = DetectAgents(rootDirectory);
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
        /// <param name="rootDirectory">The root directory to scan (repository root).</param>
        public static List<string> DetectAgents(string rootDirectory)
        {
            var detected = new List<string>();
            foreach (var kvp in KnownAgents)
            {
                var detectionPath = Path.Combine(rootDirectory, kvp.Value.DetectionDir);
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
        /// <param name="rootDirectory">The repository root directory.</param>
        /// <param name="agentName">The agent name.</param>
        public static string GetSkillsPath(string rootDirectory, string agentName)
        {
            if (KnownAgents.TryGetValue(agentName, out var def))
            {
                return Path.Combine(rootDirectory, def.SkillsSubPath);
            }
            // Unknown agent: use windsurf convention (.{agent}/rules/)
            return Path.Combine(rootDirectory, $".{agentName}", "rules");
        }

        /// <summary>
        /// Gets the absolute MCP config file path for an agent.
        /// </summary>
        /// <param name="rootDirectory">The repository root directory.</param>
        /// <param name="agentName">The agent name.</param>
        public static string GetMcpPath(string rootDirectory, string agentName)
        {
            if (KnownAgents.TryGetValue(agentName, out var def))
            {
                return Path.Combine(rootDirectory, def.McpSubPath, def.McpFileName);
            }
            // Unknown agent: use .{agent}/mcp.json convention
            return Path.Combine(rootDirectory, $".{agentName}", "mcp.json");
        }

        /// <summary>
        /// Gets the MCP directory path (parent of the mcp.json file) for an agent.
        /// </summary>
        /// <param name="rootDirectory">The repository root directory.</param>
        /// <param name="agentName">The agent name.</param>
        public static string GetMcpDirectory(string rootDirectory, string agentName)
        {
            if (KnownAgents.TryGetValue(agentName, out var def))
            {
                return Path.Combine(rootDirectory, def.McpSubPath);
            }
            return Path.Combine(rootDirectory, $".{agentName}");
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
