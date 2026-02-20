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
    /// MSBuild task that removes Imprint-managed MCP servers from agent mcp.json files
    /// during dotnet clean. Reads from unified manifest and legacy per-agent manifests.
    /// </summary>
    public class ImprintCleanMcpServers : Task
    {
        [Required]
        public string ProjectDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Explicit target agents. Accepted for compatibility with targets file.
        /// Clean primarily relies on manifest data.
        /// </summary>
        public string TargetAgents { get; set; } = string.Empty;

        /// <summary>
        /// Whether to auto-detect agents. Accepted for compatibility.
        /// </summary>
        public bool AutoDetectAgents { get; set; } = true;

        /// <summary>
        /// Default agents. Accepted for compatibility.
        /// </summary>
        public string DefaultAgents { get; set; } = "";

        public override bool Execute()
        {
            try
            {
                var cleanedAny = false;

                // 1. Try unified manifest first
                cleanedAny |= CleanFromUnifiedManifest();

                // 2. Also clean from legacy manifests (backward compatibility)
                cleanedAny |= CleanFromLegacyManifests();

                if (!cleanedAny)
                {
                    Log.LogMessage(MessageImportance.Normal, "Zakira.Imprint.Sdk: No MCP manifests found, skipping MCP clean.");
                }

                Log.LogMessage(MessageImportance.High, "Zakira.Imprint.Sdk: MCP clean complete.");
                return true;
            }
            catch (Exception ex)
            {
                Log.LogError("Zakira.Imprint.Sdk: Failed to clean MCP servers: {0}", ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// Cleans MCP servers tracked in the unified manifest.json mcp section.
        /// </summary>
        private bool CleanFromUnifiedManifest()
        {
            var imprintDir = Path.Combine(ProjectDirectory, ".imprint");
            var manifestPath = Path.Combine(imprintDir, "manifest.json");

            if (!File.Exists(manifestPath)) return false;

            try
            {
                var manifestText = File.ReadAllText(manifestPath);
                var manifestDoc = JsonNode.Parse(manifestText)?.AsObject();
                if (manifestDoc == null) return false;

                var mcpSection = manifestDoc["mcp"]?.AsObject();
                if (mcpSection == null) return false;

                foreach (var agentKvp in mcpSection)
                {
                    var agentName = agentKvp.Key;
                    var agentObj = agentKvp.Value?.AsObject();
                    if (agentObj == null) continue;

                    var mcpRelPath = agentObj["path"]?.GetValue<string>();
                    var managedServersArr = agentObj["managedServers"]?.AsArray();

                    if (managedServersArr == null || managedServersArr.Count == 0) continue;

                    var managedKeys = new HashSet<string>();
                    foreach (var entry in managedServersArr)
                    {
                        var key = entry?.GetValue<string>();
                        if (key != null) managedKeys.Add(key);
                    }

                    // Resolve the MCP file path
                    string mcpFilePath;
                    if (!string.IsNullOrEmpty(mcpRelPath))
                    {
                        mcpFilePath = Path.GetFullPath(Path.Combine(ProjectDirectory, mcpRelPath));
                    }
                    else
                    {
                        mcpFilePath = AgentConfig.GetMcpPath(ProjectDirectory, agentName);
                    }

                    CleanServersFromFile(mcpFilePath, managedKeys, agentName);

                    // Clean legacy manifest for this agent if it exists
                    var mcpDir = Path.GetDirectoryName(mcpFilePath);
                    if (!string.IsNullOrEmpty(mcpDir))
                    {
                        var legacyManifest = Path.Combine(mcpDir, ".imprint-mcp-manifest");
                        if (File.Exists(legacyManifest))
                        {
                            File.Delete(legacyManifest);
                        }
                    }
                }

                // Remove mcp section from unified manifest
                manifestDoc.Remove("mcp");

                // Check if manifest still has packages data
                var hasPackages = manifestDoc["packages"]?.AsObject()?.Count > 0;
                if (!hasPackages)
                {
                    // No more data - delete the manifest
                    File.Delete(manifestPath);
                }
                else
                {
                    // Rewrite without mcp section
                    var options = GetJsonOptions();
                    var content = manifestDoc.ToJsonString(options);
                    if (!content.EndsWith("\n")) content += "\n";
                    File.WriteAllText(manifestPath, content);
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.LogWarning("Zakira.Imprint.Sdk: Failed to process unified manifest for MCP clean: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Cleans MCP servers from legacy .imprint-mcp-manifest files.
        /// Checks known agent directories for legacy manifests.
        /// </summary>
        private bool CleanFromLegacyManifests()
        {
            var cleaned = false;

            // Check all known agent MCP directories for legacy manifests
            // Map directory to agent name for proper root key lookup
            var dirsToCheck = new Dictionary<string, string>
            {
                { Path.Combine(ProjectDirectory, ".vscode"), "copilot" },
                { Path.Combine(ProjectDirectory, ".claude"), "claude" },
                { Path.Combine(ProjectDirectory, ".cursor"), "cursor" },
            };

            foreach (var kvp in dirsToCheck)
            {
                var dir = kvp.Key;
                var agentName = kvp.Value;
                var manifestPath = Path.Combine(dir, ".imprint-mcp-manifest");
                if (!File.Exists(manifestPath)) continue;

                var managedKeys = new HashSet<string>();
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
                            if (key != null) managedKeys.Add(key);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.LogWarning("Zakira.Imprint.Sdk: Failed to read legacy manifest {0}: {1}", manifestPath, ex.Message);
                }

                if (managedKeys.Count > 0)
                {
                    var mcpJsonPath = Path.Combine(dir, "mcp.json");
                    CleanServersFromFile(mcpJsonPath, managedKeys, agentName);
                    cleaned = true;
                }

                // Delete the legacy manifest
                File.Delete(manifestPath);
            }

            return cleaned;
        }

        private void CleanServersFromFile(string mcpFilePath, HashSet<string> managedKeys, string agent)
        {
            if (!File.Exists(mcpFilePath) || managedKeys.Count == 0) return;

            // Get the agent-specific root key (e.g., "servers" for VS Code, "mcpServers" for Claude/Cursor)
            var rootKey = AgentConfig.GetMcpRootKey(agent);

            try
            {
                var mcpText = File.ReadAllText(mcpFilePath);
                var mcpDoc = JsonNode.Parse(mcpText)?.AsObject();
                if (mcpDoc == null) return;

                var serversObj = mcpDoc[rootKey]?.AsObject();
                if (serversObj == null) return;

                foreach (var key in managedKeys)
                {
                    serversObj.Remove(key);
                }

                // Check if anything meaningful remains
                var hasServers = serversObj.Count > 0;
                var hasInputs = false;
                if (mcpDoc["inputs"] != null)
                {
                    try { hasInputs = mcpDoc["inputs"]!.AsArray().Count > 0; }
                    catch { hasInputs = false; }
                }
                var hasOtherKeys = mcpDoc.Count > (mcpDoc.ContainsKey(rootKey) ? 1 : 0) + (mcpDoc.ContainsKey("inputs") ? 1 : 0);

                if (!hasServers && !hasInputs && !hasOtherKeys)
                {
                    File.Delete(mcpFilePath);
                    Log.LogMessage(MessageImportance.High, "Zakira.Imprint.Sdk: Removed empty {0} ({1})", mcpFilePath, agent);
                }
                else
                {
                    if (!hasServers)
                    {
                        mcpDoc.Remove(rootKey);
                    }

                    var options = GetJsonOptions();
                    var cleaned = mcpDoc.ToJsonString(options);
                    cleaned = cleaned.Replace("\r\n", "\n").Replace("\r", "\n");
                    if (!cleaned.EndsWith("\n")) cleaned += "\n";
                    File.WriteAllText(mcpFilePath, cleaned);
                    Log.LogMessage(MessageImportance.High, "Zakira.Imprint.Sdk: Removed {0} managed server(s) from {1} ({2})",
                        managedKeys.Count, mcpFilePath, agent);
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning("Zakira.Imprint.Sdk: Failed to clean {0}: {1}", mcpFilePath, ex.Message);
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
    }
}
