using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Zakira.Imprint.Sdk
{
    /// <summary>
    /// MSBuild task that cleans files previously copied by ImprintCopyContent.
    /// Supports both unified manifest.json (v2) and legacy per-package .manifest files.
    /// Also cleans up associated .gitignore entries in skill directories.
    /// </summary>
    public class ImprintCleanContent : Task
    {
        private const string GitignoreHeader = "# Managed by Zakira.Imprint";

        /// <summary>
        /// The project directory (contains .imprint/ manifest storage).
        /// </summary>
        [Required]
        public string ProjectDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Explicit target agents (not used for clean — clean relies on manifest data).
        /// Accepted for compatibility with targets file.
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
                var imprintDir = Path.Combine(ProjectDirectory, ".imprint");

                if (!Directory.Exists(imprintDir))
                {
                    Log.LogMessage(MessageImportance.Normal, "Zakira.Imprint.Sdk: No .imprint directory found, skipping content clean.");
                    return true;
                }

                var allDeletedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var gitignoresToClean = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

                // Try unified manifest first (v2)
                var unifiedManifestPath = Path.Combine(imprintDir, "manifest.json");
                var cleanedFromUnified = false;

                if (File.Exists(unifiedManifestPath))
                {
                    cleanedFromUnified = CleanFromUnifiedManifest(unifiedManifestPath, allDeletedDirs, gitignoresToClean);
                }

                // Also process legacy per-package manifests (backward compatibility)
                CleanFromLegacyManifests(imprintDir, allDeletedDirs, gitignoresToClean);

                if (!cleanedFromUnified && allDeletedDirs.Count == 0)
                {
                    Log.LogMessage(MessageImportance.Normal, "Zakira.Imprint.Sdk: No manifests found, skipping content clean.");
                    return true;
                }

                // Clean up gitignore entries
                CleanGitignoreEntries(gitignoresToClean);

                // Clean up empty directories (deepest first)
                var sortedDirs = allDeletedDirs
                    .OrderByDescending(d => d.Length)
                    .ToList();

                foreach (var dir in sortedDirs)
                {
                    TryRemoveEmptyDirectory(dir);
                }

                // Clean up .imprint directory if empty
                TryCleanImprintDir(imprintDir);

                Log.LogMessage(MessageImportance.High, "Zakira.Imprint.Sdk: Content clean complete.");
                return true;
            }
            catch (Exception ex)
            {
                Log.LogError("Zakira.Imprint.Sdk: Failed to clean content: {0}", ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// Cleans files tracked in the unified manifest.json (v2 format).
        /// </summary>
        private bool CleanFromUnifiedManifest(
            string manifestPath,
            HashSet<string> allDeletedDirs,
            Dictionary<string, HashSet<string>> gitignoresToClean)
        {
            try
            {
                var manifestText = File.ReadAllText(manifestPath);
                var manifestDoc = JsonNode.Parse(manifestText);
                var version = manifestDoc?["version"]?.GetValue<int>() ?? 0;

                if (version < 2)
                {
                    // Not a v2 manifest, skip
                    return false;
                }

                var packages = manifestDoc?["packages"]?.AsObject();
                if (packages == null)
                {
                    File.Delete(manifestPath);
                    return true;
                }

                var totalDeleted = 0;

                foreach (var pkgKvp in packages)
                {
                    var packageId = pkgKvp.Key;
                    var pkgObj = pkgKvp.Value?.AsObject();
                    var filesObj = pkgObj?["files"]?.AsObject();

                    if (filesObj == null) continue;

                    foreach (var agentKvp in filesObj)
                    {
                        var agentName = agentKvp.Key;
                        var fileArray = agentKvp.Value?.AsArray();
                        if (fileArray == null) continue;

                        foreach (var fileNode in fileArray)
                        {
                            var filePath = fileNode?.GetValue<string>();
                            if (string.IsNullOrEmpty(filePath)) continue;

                            var dir = Path.GetDirectoryName(filePath);

                            if (File.Exists(filePath))
                            {
                                File.Delete(filePath);
                                totalDeleted++;

                                if (!string.IsNullOrEmpty(dir))
                                {
                                    allDeletedDirs.Add(dir);
                                }
                            }

                            // Track gitignore cleanup needed
                            if (!string.IsNullOrEmpty(dir))
                            {
                                TrackGitignoreCleanup(gitignoresToClean, dir, packageId);
                            }
                        }
                    }

                    Log.LogMessage(MessageImportance.Normal, "Zakira.Imprint.Sdk: Cleaned files from package {0}", packageId);
                }

                // Re-read the manifest and update it (preserving the mcp section for ImprintCleanMcpServers)
                try
                {
                    var currentText = File.ReadAllText(manifestPath);
                    var currentDoc = JsonNode.Parse(currentText)?.AsObject();
                    if (currentDoc != null)
                    {
                        // Remove packages section
                        currentDoc.Remove("packages");
                        
                        // Check if mcp section still has data
                        var hasMcpData = currentDoc["mcp"]?.AsObject()?.Count > 0;
                        
                        if (!hasMcpData)
                        {
                            // No more data - delete the manifest
                            File.Delete(manifestPath);
                        }
                        else
                        {
                            // Write back without packages section (mcp will be cleaned by ImprintCleanMcpServers)
                            var options = new System.Text.Json.JsonSerializerOptions
                            {
                                WriteIndented = true,
                                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                                TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
                            };
                            var content = currentDoc.ToJsonString(options);
                            if (!content.EndsWith("\n")) content += "\n";
                            File.WriteAllText(manifestPath, content);
                        }
                    }
                    else
                    {
                        File.Delete(manifestPath);
                    }
                }
                catch
                {
                    // If we fail to update, just delete the manifest as before
                    File.Delete(manifestPath);
                }

                Log.LogMessage(MessageImportance.High, "Zakira.Imprint.Sdk: Cleaned {0} file(s) via unified manifest.", totalDeleted);
                return true;
            }
            catch (Exception ex)
            {
                Log.LogWarning("Zakira.Imprint.Sdk: Failed to process unified manifest: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Cleans files tracked in legacy per-package .manifest files.
        /// </summary>
        private void CleanFromLegacyManifests(
            string imprintDir,
            HashSet<string> allDeletedDirs,
            Dictionary<string, HashSet<string>> gitignoresToClean)
        {
            var manifestFiles = Directory.GetFiles(imprintDir, "*.manifest");
            if (manifestFiles.Length == 0) return;

            foreach (var manifestPath in manifestFiles)
            {
                try
                {
                    var manifestText = File.ReadAllText(manifestPath);
                    var manifestDoc = JsonNode.Parse(manifestText);
                    var packageId = manifestDoc?["packageId"]?.GetValue<string>() ?? Path.GetFileNameWithoutExtension(manifestPath);
                    var filesArray = manifestDoc?["files"]?.AsArray();

                    if (filesArray == null || filesArray.Count == 0)
                    {
                        Log.LogMessage(MessageImportance.Normal, "Zakira.Imprint.Sdk: Manifest for {0} has no files.", packageId);
                        File.Delete(manifestPath);
                        continue;
                    }

                    var deletedCount = 0;
                    foreach (var fileNode in filesArray)
                    {
                        var filePath = fileNode?.GetValue<string>();
                        if (string.IsNullOrEmpty(filePath)) continue;

                        var dir = Path.GetDirectoryName(filePath);

                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                            deletedCount++;

                            if (!string.IsNullOrEmpty(dir))
                            {
                                allDeletedDirs.Add(dir);
                            }
                        }

                        // Track gitignore cleanup needed
                        if (!string.IsNullOrEmpty(dir))
                        {
                            TrackGitignoreCleanup(gitignoresToClean, dir, packageId);
                        }
                    }

                    File.Delete(manifestPath);
                    Log.LogMessage(MessageImportance.High, "Zakira.Imprint.Sdk: Cleaned {0} file(s) from {1} (legacy manifest)", deletedCount, packageId);
                }
                catch (Exception ex)
                {
                    Log.LogWarning("Zakira.Imprint.Sdk: Failed to process manifest {0}: {1}", manifestPath, ex.Message);
                }
            }
        }

        /// <summary>
        /// Tracks which package's entries need to be cleaned from which gitignore files.
        /// </summary>
        private void TrackGitignoreCleanup(
            Dictionary<string, HashSet<string>> gitignoresToClean,
            string directory,
            string packageId)
        {
            if (!gitignoresToClean.ContainsKey(directory))
            {
                gitignoresToClean[directory] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
            gitignoresToClean[directory].Add(packageId);
        }

        /// <summary>
        /// Cleans up gitignore entries for removed packages.
        /// </summary>
        private void CleanGitignoreEntries(Dictionary<string, HashSet<string>> gitignoresToClean)
        {
            foreach (var kvp in gitignoresToClean)
            {
                var directory = kvp.Key;
                var packagesToRemove = kvp.Value;

                var gitignorePath = Path.Combine(directory, ".gitignore");
                if (!File.Exists(gitignorePath)) continue;

                try
                {
                    var content = File.ReadAllText(gitignorePath);
                    var newContent = RemovePackageSections(content, packagesToRemove);

                    if (string.IsNullOrWhiteSpace(newContent) || IsOnlyWhitespaceOrComments(newContent))
                    {
                        // If gitignore is now empty or only comments, delete it
                        File.Delete(gitignorePath);
                        Log.LogMessage(MessageImportance.Normal,
                            "Zakira.Imprint.Sdk: Deleted empty .gitignore in {0}", directory);
                    }
                    else if (!newContent.Equals(content))
                    {
                        File.WriteAllText(gitignorePath, newContent);
                        Log.LogMessage(MessageImportance.Normal,
                            "Zakira.Imprint.Sdk: Cleaned .gitignore entries in {0}", directory);
                    }
                }
                catch (Exception ex)
                {
                    Log.LogWarning("Zakira.Imprint.Sdk: Failed to clean gitignore {0}: {1}", gitignorePath, ex.Message);
                }
            }
        }

        /// <summary>
        /// Removes sections managed by specified packages from gitignore content.
        /// </summary>
        private string RemovePackageSections(string content, HashSet<string> packagesToRemove)
        {
            var sb = new StringBuilder();
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.None);

            string currentPackage = null;
            var skipLines = false;

            foreach (var line in lines)
            {
                if (line.StartsWith(GitignoreHeader))
                {
                    // Extract package ID from header
                    var start = line.IndexOf('(');
                    var end = line.IndexOf(')');
                    if (start >= 0 && end > start)
                    {
                        currentPackage = line.Substring(start + 1, end - start - 1);
                        skipLines = packagesToRemove.Contains(currentPackage);
                    }
                    else
                    {
                        currentPackage = null;
                        skipLines = false;
                    }

                    if (!skipLines)
                    {
                        sb.AppendLine(line);
                    }
                }
                else if (currentPackage != null)
                {
                    // We're in a managed section
                    if (!skipLines)
                    {
                        sb.AppendLine(line);
                    }
                    // Check if this is the last entry in this section (empty line or new section coming)
                    // This is handled by the header check above
                }
                else
                {
                    // Non-managed line, preserve it
                    sb.AppendLine(line);
                }
            }

            // Trim trailing empty lines but ensure newline at end
            var result = sb.ToString().TrimEnd() + "\n";
            return result;
        }

        /// <summary>
        /// Checks if content is only whitespace or comment lines.
        /// </summary>
        private bool IsOnlyWhitespaceOrComments(string content)
        {
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return lines.All(line => string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"));
        }

        private void TryRemoveEmptyDirectory(string dir)
        {
            try
            {
                if (Directory.Exists(dir) &&
                    !Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    Directory.Delete(dir);
                    var parent = Path.GetDirectoryName(dir);
                    if (!string.IsNullOrEmpty(parent))
                    {
                        TryRemoveEmptyDirectory(parent);
                    }
                }
            }
            catch
            {
                // Ignore - directory might be in use or protected
            }
        }

        private void TryCleanImprintDir(string imprintDir)
        {
            try
            {
                if (!Directory.Exists(imprintDir)) return;

                var remainingManifests = Directory.GetFiles(imprintDir, "*.manifest");
                var hasUnifiedManifest = File.Exists(Path.Combine(imprintDir, "manifest.json"));

                if (remainingManifests.Length == 0 && !hasUnifiedManifest)
                {
                    // No more manifests - remove .gitignore and the directory
                    var gitignorePath = Path.Combine(imprintDir, ".gitignore");
                    if (File.Exists(gitignorePath))
                    {
                        File.Delete(gitignorePath);
                    }

                    if (!Directory.EnumerateFileSystemEntries(imprintDir).Any())
                    {
                        Directory.Delete(imprintDir);
                    }
                }
            }
            catch
            {
                // Ignore
            }
        }
    }
}
