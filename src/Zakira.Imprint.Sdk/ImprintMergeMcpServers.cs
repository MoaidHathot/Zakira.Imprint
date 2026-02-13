using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Zakira.Imprint.Sdk
{
    /// <summary>
    /// MSBuild task that merges MCP server fragment files from Imprint packages
    /// into each resolved agent's mcp.json. Tracks managed servers in the unified
    /// manifest and legacy per-agent manifests.
    /// </summary>
    public class ImprintMergeMcpServers : Task
    {
        [Required]
        public ITaskItem[] McpFragmentFiles { get; set; } = Array.Empty<ITaskItem>();

        [Required]
        public string ProjectDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Explicit target agents (semicolon-separated: copilot;claude;cursor).
        /// </summary>
        public string TargetAgents { get; set; } = string.Empty;

        /// <summary>
        /// Whether to auto-detect agents by scanning for their directories.
        /// </summary>
        public bool AutoDetectAgents { get; set; } = true;

        /// <summary>
        /// Default agents when auto-detection finds nothing.
        /// </summary>
        public string DefaultAgents { get; set; } = "copilot";

        public override bool Execute()
        {
            try
            {
                // --- Collect servers from all fragments ---
                var newManagedServers = new Dictionary<string, JsonNode>();

                if (McpFragmentFiles != null)
                {
                    foreach (var item in McpFragmentFiles)
                    {
                        var fragmentFile = item.ItemSpec;
                        if (!File.Exists(fragmentFile))
                        {
                            Log.LogWarning("Zakira.Imprint.Sdk: Fragment file not found: {0}", fragmentFile);
                            continue;
                        }

                        try
                        {
                            var fragmentText = File.ReadAllText(fragmentFile);
                            var fragmentDoc = JsonNode.Parse(fragmentText);
                            var servers = fragmentDoc?["servers"]?.AsObject();
                            if (servers != null)
                            {
                                foreach (var kvp in servers)
                                {
                                    newManagedServers[kvp.Key] = kvp.Value?.DeepClone()!;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.LogWarning("Zakira.Imprint.Sdk: Failed to parse fragment {0}: {1}", fragmentFile, ex.Message);
                        }
                    }
                }

                if (newManagedServers.Count == 0)
                {
                    Log.LogMessage(MessageImportance.Normal, "Zakira.Imprint.Sdk: No MCP server fragments found, skipping merge.");
                    return true;
                }

                // Resolve target agents
                var agents = AgentConfig.ResolveAgents(ProjectDirectory, TargetAgents, AutoDetectAgents, DefaultAgents);
                Log.LogMessage(MessageImportance.Normal, "Zakira.Imprint.Sdk: Merging MCP servers for agents: {0}", string.Join(", ", agents));

                // Track MCP data for unified manifest
                var mcpManifestData = new Dictionary<string, McpAgentManifest>();

                // Merge to each agent's MCP file
                foreach (var agent in agents)
                {
                    var mcpFilePath = AgentConfig.GetMcpPath(ProjectDirectory, agent);
                    var mcpDir = AgentConfig.GetMcpDirectory(ProjectDirectory, agent);
                    var legacyManifestPath = Path.Combine(mcpDir, ".imprint-mcp-manifest");

                    // Read old managed keys from legacy manifest for this agent
                    var oldManagedKeys = ReadLegacyMcpManifest(legacyManifestPath);

                    // Merge servers into this agent's mcp.json
                    MergeServersIntoFile(mcpFilePath, mcpDir, newManagedServers, oldManagedKeys, agent);

                    // Write legacy manifest for this agent
                    WriteLegacyMcpManifest(legacyManifestPath, newManagedServers.Keys.ToList());

                    // Ensure gitignore for legacy manifest
                    EnsureGitignore(Path.Combine(mcpDir, ".gitignore"));

                    // Track for unified manifest
                    var relMcpPath = Path.GetRelativePath(ProjectDirectory, mcpFilePath);
                    mcpManifestData[agent] = new McpAgentManifest
                    {
                        Path = relMcpPath,
                        ManagedServers = newManagedServers.Keys.OrderBy(k => k).ToList()
                    };
                }

                // Update unified manifest with MCP section
                UpdateUnifiedManifestMcp(mcpManifestData);

                return true;
            }
            catch (Exception ex)
            {
                Log.LogError("Zakira.Imprint.Sdk: Failed to merge MCP servers: {0}", ex.ToString());
                return false;
            }
        }

        private void MergeServersIntoFile(string mcpFilePath, string mcpDir,
            Dictionary<string, JsonNode> newManagedServers, HashSet<string> oldManagedKeys, string agent)
        {
            // Read existing mcp.json
            JsonObject mcpDoc;
            if (File.Exists(mcpFilePath))
            {
                try
                {
                    var existingText = File.ReadAllText(mcpFilePath);
                    mcpDoc = JsonNode.Parse(existingText)?.AsObject() ?? new JsonObject();
                }
                catch
                {
                    mcpDoc = new JsonObject();
                }
            }
            else
            {
                mcpDoc = new JsonObject();
            }

            // Ensure "servers" object exists
            if (mcpDoc["servers"] == null)
            {
                mcpDoc["servers"] = new JsonObject();
            }
            var serversObj = mcpDoc["servers"]!.AsObject();

            // Remove previously managed servers that are no longer in fragments
            foreach (var oldKey in oldManagedKeys)
            {
                serversObj.Remove(oldKey);
            }

            // Add/update servers from current fragments
            foreach (var kvp in newManagedServers)
            {
                serversObj.Remove(kvp.Key); // remove first to avoid duplicate key
                serversObj.Add(kvp.Key, kvp.Value.DeepClone());
            }

            // Serialize with pretty-print
            var options = GetJsonOptions();
            var newMcpContent = mcpDoc.ToJsonString(options);
            newMcpContent = newMcpContent.Replace("\r\n", "\n").Replace("\r", "\n");
            if (!newMcpContent.EndsWith("\n")) newMcpContent += "\n";

            // Write mcp.json only if changed
            Directory.CreateDirectory(mcpDir);
            var existingContent = "";
            if (File.Exists(mcpFilePath))
            {
                existingContent = File.ReadAllText(mcpFilePath).Replace("\r\n", "\n").Replace("\r", "\n");
            }

            if (newMcpContent != existingContent)
            {
                File.WriteAllText(mcpFilePath, newMcpContent);
                Log.LogMessage(MessageImportance.High, "Zakira.Imprint.Sdk: Updated {0} with {1} managed server(s) ({2}).",
                    mcpFilePath, newManagedServers.Count, agent);
            }
            else
            {
                Log.LogMessage(MessageImportance.Normal, "Zakira.Imprint.Sdk: {0} is already up to date ({1}).",
                    mcpFilePath, agent);
            }
        }

        private HashSet<string> ReadLegacyMcpManifest(string manifestPath)
        {
            var keys = new HashSet<string>();
            if (!File.Exists(manifestPath)) return keys;

            try
            {
                var manifestText = File.ReadAllText(manifestPath);
                var manifestDoc = JsonNode.Parse(manifestText);
                var arr = manifestDoc?["managedServers"]?.AsArray();
                if (arr != null)
                {
                    foreach (var entry in arr)
                    {
                        var key = entry?.GetValue<string>();
                        if (key != null) keys.Add(key);
                    }
                }
            }
            catch
            {
                // manifest corrupt - will be rewritten
            }
            return keys;
        }

        private void WriteLegacyMcpManifest(string manifestPath, List<string> serverKeys)
        {
            var manifestObj = new JsonObject();
            var managedArray = new JsonArray();
            foreach (var key in serverKeys.OrderBy(k => k))
            {
                managedArray.Add(key);
            }
            manifestObj["managedServers"] = managedArray;

            var options = GetJsonOptions();
            var content = manifestObj.ToJsonString(options);
            if (!content.EndsWith("\n")) content += "\n";
            File.WriteAllText(manifestPath, content);
        }

        private void UpdateUnifiedManifestMcp(Dictionary<string, McpAgentManifest> mcpData)
        {
            var imprintDir = Path.Combine(ProjectDirectory, ".imprint");
            var manifestPath = Path.Combine(imprintDir, "manifest.json");

            JsonObject manifestDoc;
            if (File.Exists(manifestPath))
            {
                try
                {
                    var text = File.ReadAllText(manifestPath);
                    manifestDoc = JsonNode.Parse(text)?.AsObject() ?? new JsonObject();
                }
                catch
                {
                    manifestDoc = new JsonObject();
                }
            }
            else
            {
                Directory.CreateDirectory(imprintDir);
                manifestDoc = new JsonObject { ["version"] = 2 };
            }

            // Ensure version is set
            if (manifestDoc["version"] == null)
            {
                manifestDoc["version"] = 2;
            }

            // Build mcp section
            var mcpObj = new JsonObject();
            foreach (var kvp in mcpData.OrderBy(k => k.Key))
            {
                var agentObj = new JsonObject
                {
                    ["path"] = kvp.Value.Path,
                    ["managedServers"] = new JsonArray(
                        kvp.Value.ManagedServers.Select(s => (JsonNode)JsonValue.Create(s)!).ToArray())
                };
                mcpObj[kvp.Key] = agentObj;
            }
            manifestDoc.Remove("mcp");
            manifestDoc["mcp"] = mcpObj;

            var options = GetJsonOptions();
            var content = manifestDoc.ToJsonString(options);
            if (!content.EndsWith("\n")) content += "\n";
            File.WriteAllText(manifestPath, content);
        }

        private void EnsureGitignore(string gitignorePath)
        {
            var gitignoreEntry = ".imprint-mcp-manifest";
            var gitignoreLines = new List<string>();
            if (File.Exists(gitignorePath))
            {
                gitignoreLines = File.ReadAllLines(gitignorePath).ToList();
            }
            if (!gitignoreLines.Any(l => l.Trim() == gitignoreEntry))
            {
                if (gitignoreLines.Count > 0 && !string.IsNullOrWhiteSpace(gitignoreLines.Last()))
                {
                    gitignoreLines.Add("");
                }
                gitignoreLines.Add("# Imprint MCP manifest (auto-generated, do not commit)");
                gitignoreLines.Add(gitignoreEntry);
                File.WriteAllText(gitignorePath, string.Join("\n", gitignoreLines) + "\n");
                Log.LogMessage(MessageImportance.Normal, "Zakira.Imprint.Sdk: Added manifest to {0}", gitignorePath);
            }
        }

        private static JsonSerializerOptions GetJsonOptions()
        {
            return new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
            };
        }

        private class McpAgentManifest
        {
            public string Path { get; set; } = string.Empty;
            public List<string> ManagedServers { get; set; } = new();
        }
    }
}
