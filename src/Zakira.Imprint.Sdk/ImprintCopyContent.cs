using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Zakira.Imprint.Sdk
{
    /// <summary>
    /// MSBuild task that copies ImprintContent items to their destination directories
    /// for all resolved target agents. Writes a unified manifest to .imprint/manifest.json
    /// tracking files per agent per package. Creates granular .gitignore files in skill directories.
    /// </summary>
    public class ImprintCopyContent : Task
    {
        private const string GitignoreHeader = "# Managed by Zakira.Imprint";

        /// <summary>
        /// The ImprintContent items to copy. Each item must have metadata:
        /// - PackageId: The NuGet package ID that owns this content
        /// - SourceBase: The root source directory (used to compute relative paths)
        /// Optional metadata:
        /// - SuggestedPrefix: Author's suggested prefix for skill folders
        /// - ImprintPrefix: Consumer override for prefix
        /// - ImprintUsePrefix: Consumer override for whether to use prefix (true/false)
        /// - ImprintEnabled: Consumer can disable this package's skills (true/false)
        /// </summary>
        [Required]
        public ITaskItem[] ContentItems { get; set; } = Array.Empty<ITaskItem>();

        /// <summary>
        /// The project directory (MSBuildProjectDirectory). Used for fallback if no repo root found.
        /// </summary>
        [Required]
        public string ProjectDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Explicit root directory override for agent directories and manifest storage.
        /// If empty, auto-detects repository root by walking up from ProjectDirectory.
        /// </summary>
        public string RootDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Explicit target agents (semicolon-separated: copilot;claude;cursor).
        /// When set, disables auto-detection.
        /// </summary>
        public string TargetAgents { get; set; } = string.Empty;

        /// <summary>
        /// Whether to auto-detect agents by scanning for their directories.
        /// </summary>
        public bool AutoDetectAgents { get; set; } = true;

        /// <summary>
        /// Default agents when auto-detection finds nothing (semicolon-separated).
        /// </summary>
        public string DefaultAgents { get; set; } = "";

        /// <summary>
        /// Global setting: whether to prefix skill folders with package identifier.
        /// Can be overridden per-package via ImprintUsePrefix metadata.
        /// </summary>
        public bool PrefixSkills { get; set; } = false;

        /// <summary>
        /// Global default prefix to use when PrefixSkills is true but no specific prefix is set.
        /// If empty, uses PackageId.
        /// </summary>
        public string DefaultPrefix { get; set; } = string.Empty;

        public override bool Execute()
        {
            try
            {
                // Resolve the root directory (repo root or explicit override)
                var resolvedRoot = AgentConfig.ResolveRootDirectory(ProjectDirectory, RootDirectory);
                
                // Clean up files from packages that were previously installed but are now removed
                // This must run BEFORE the early return for empty ContentItems, otherwise cleanup
                // won't happen when all packages are removed from the project
                var imprintDir = Path.Combine(resolvedRoot, ".imprint");
                
                if (ContentItems == null || ContentItems.Length == 0)
                {
                    Log.LogMessage(MessageImportance.Normal, "Zakira.Imprint.Sdk: No content items to copy.");
                    // Still clean up any previously installed packages (empty set = all should be removed)
                    CleanRemovedPackages(imprintDir, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                    return true;
                }

                // Resolve target agents using the resolved root directory
                var agents = AgentConfig.ResolveAgents(resolvedRoot, TargetAgents, AutoDetectAgents, DefaultAgents);
                
                if (agents.Count == 0)
                {
                    Log.LogMessage(MessageImportance.Normal, 
                        "Zakira.Imprint.Sdk: No target agents detected or configured. Skipping file copy. " +
                        "Set ImprintTargetAgents or create an agent directory (e.g., .github, .claude, .opencode) to enable.");
                    return true;
                }
                
                Log.LogMessage(MessageImportance.Normal, "Zakira.Imprint.Sdk: Resolved target agents: {0} (root: {1})", 
                    string.Join(", ", agents), resolvedRoot);

                // Parse content items into per-package groups with relative paths
                var packageItems = ParseContentItems();
                if (packageItems.Count == 0)
                {
                    return true;
                }

                // Check for destination conflicts before copying
                var conflictResult = CheckForDestinationConflicts(packageItems, agents, resolvedRoot);
                if (!conflictResult)
                {
                    return false; // Errors already logged
                }

                // Clean up packages that are no longer referenced
                CleanRemovedPackages(imprintDir, packageItems.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase));

                // Build the unified manifest tracking all files per agent per package
                var manifestPackages = new Dictionary<string, Dictionary<string, List<string>>>();

                // Track gitignore entries per skill directory per agent
                // Key: (agentSkillsPath, skillSubDir) -> (packageId, list of filenames)
                var gitignoreEntries = new Dictionary<string, Dictionary<string, List<string>>>();

                // Copy files to each agent's skills directory
                foreach (var agent in agents)
                {
                    var agentSkillsPath = AgentConfig.GetSkillsPath(resolvedRoot, agent);

                    foreach (var kvp in packageItems)
                    {
                        var packageId = kvp.Key;
                        var items = kvp.Value;

                        if (!manifestPackages.ContainsKey(packageId))
                        {
                            manifestPackages[packageId] = new Dictionary<string, List<string>>();
                        }
                        manifestPackages[packageId][agent] = new List<string>();

                        foreach (var (sourceFile, relativePath) in items)
                        {
                            var destFile = Path.Combine(agentSkillsPath, relativePath);
                            var destDir = Path.GetDirectoryName(destFile);
                            if (!string.IsNullOrEmpty(destDir))
                            {
                                Directory.CreateDirectory(destDir);
                            }

                            CopyFileSafe(sourceFile, destFile);
                            manifestPackages[packageId][agent].Add(destFile);

                            // Track for gitignore
                            TrackGitignoreEntry(gitignoreEntries, agentSkillsPath, relativePath, packageId);
                        }

                        Log.LogMessage(MessageImportance.High,
                            "Zakira.Imprint.Sdk: Copied {0} file(s) from {1} to {2} ({3})",
                            items.Count, packageId, agentSkillsPath, agent);
                    }
                }

                // Write gitignore files in skill directories
                WriteGitignoreFiles(gitignoreEntries, agents);

                // Write unified manifest
                Directory.CreateDirectory(imprintDir);
                WriteUnifiedManifest(imprintDir, manifestPackages);

                // Ensure .imprint/.gitignore exists
                EnsureImprintGitignore(imprintDir);

                return true;
            }
            catch (Exception ex)
            {
                Log.LogError("Zakira.Imprint.Sdk: Failed to copy content: {0}", ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// Checks for destination file conflicts where two packages try to copy to the same destination.
        /// </summary>
        private bool CheckForDestinationConflicts(
            Dictionary<string, List<(string SourceFile, string RelativePath)>> packageItems,
            IEnumerable<string> agents,
            string rootDirectory)
        {
            var destinationOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var hasConflicts = false;

            foreach (var agent in agents)
            {
                var agentSkillsPath = AgentConfig.GetSkillsPath(rootDirectory, agent);

                foreach (var kvp in packageItems)
                {
                    var packageId = kvp.Key;
                    foreach (var (_, relativePath) in kvp.Value)
                    {
                        var destFile = Path.Combine(agentSkillsPath, relativePath);

                        if (destinationOwners.TryGetValue(destFile, out var existingOwner))
                        {
                            if (!existingOwner.Equals(packageId, StringComparison.OrdinalIgnoreCase))
                            {
                                Log.LogError(
                                    "Zakira.Imprint.Sdk: Destination conflict - both '{0}' and '{1}' are trying to copy to '{2}'. " +
                                    "Consider using ImprintPrefix on one of the PackageReferences to avoid this conflict.",
                                    existingOwner, packageId, destFile);
                                hasConflicts = true;
                            }
                        }
                        else
                        {
                            destinationOwners[destFile] = packageId;
                        }
                    }
                }
            }

            return !hasConflicts;
        }

        /// <summary>
        /// Parses ContentItems into a dictionary of PackageId -> list of (sourceFile, relativePath).
        /// Applies prefix configuration to relative paths.
        /// </summary>
        private Dictionary<string, List<(string SourceFile, string RelativePath)>> ParseContentItems()
        {
            var byPackage = new Dictionary<string, List<(string SourceFile, string RelativePath)>>();

            foreach (var item in ContentItems)
            {
                var sourceFile = item.ItemSpec;
                var packageId = item.GetMetadata("PackageId");
                var sourceBase = item.GetMetadata("SourceBase");

                if (string.IsNullOrEmpty(packageId))
                {
                    Log.LogWarning("Zakira.Imprint.Sdk: Content item '{0}' is missing PackageId metadata, skipping.", sourceFile);
                    continue;
                }

                if (string.IsNullOrEmpty(sourceBase))
                {
                    Log.LogWarning("Zakira.Imprint.Sdk: Content item '{0}' is missing SourceBase metadata, skipping.", sourceFile);
                    continue;
                }

                // Check if package is disabled
                var imprintEnabled = item.GetMetadata("ImprintEnabled");
                if (!string.IsNullOrEmpty(imprintEnabled) &&
                    imprintEnabled.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    Log.LogMessage(MessageImportance.Normal,
                        "Zakira.Imprint.Sdk: Package '{0}' is disabled via ImprintEnabled=false, skipping.", packageId);
                    continue;
                }

                if (!File.Exists(sourceFile))
                {
                    Log.LogWarning("Zakira.Imprint.Sdk: Source file not found: {0}", sourceFile);
                    continue;
                }

                // Normalize paths
                var normalizedSource = Path.GetFullPath(sourceFile);
                var normalizedSourceBase = Path.GetFullPath(sourceBase);

                // Ensure sourceBase ends with directory separator
                if (!normalizedSourceBase.EndsWith(Path.DirectorySeparatorChar.ToString()) &&
                    !normalizedSourceBase.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
                {
                    normalizedSourceBase += Path.DirectorySeparatorChar;
                }

                // Compute relative path from source base
                string relativePath;
                if (normalizedSource.StartsWith(normalizedSourceBase, StringComparison.OrdinalIgnoreCase))
                {
                    relativePath = normalizedSource.Substring(normalizedSourceBase.Length);
                }
                else
                {
                    // Fallback: just use the filename
                    relativePath = Path.GetFileName(normalizedSource);
                    Log.LogWarning("Zakira.Imprint.Sdk: Source file '{0}' is not under SourceBase '{1}', using filename only.", sourceFile, sourceBase);
                }

                // Apply prefix if configured
                relativePath = ApplyPrefix(relativePath, item, packageId);

                if (!byPackage.ContainsKey(packageId))
                {
                    byPackage[packageId] = new List<(string, string)>();
                }
                byPackage[packageId].Add((normalizedSource, relativePath));
            }

            return byPackage;
        }

        /// <summary>
        /// Applies prefix configuration to a relative path.
        /// Priority: ImprintPrefix metadata > ImprintUsePrefix + (DefaultPrefix or SuggestedPrefix or PackageId) > PrefixSkills global > no prefix
        /// </summary>
        private string ApplyPrefix(string relativePath, ITaskItem item, string packageId)
        {
            // Check for explicit ImprintPrefix metadata (consumer override)
            var explicitPrefix = item.GetMetadata("ImprintPrefix");
            if (!string.IsNullOrEmpty(explicitPrefix))
            {
                return Path.Combine(explicitPrefix, relativePath);
            }

            // Check for ImprintUsePrefix metadata (consumer can force prefix on/off per package)
            var usePrefixMeta = item.GetMetadata("ImprintUsePrefix");
            bool? usePrefix = null;
            if (!string.IsNullOrEmpty(usePrefixMeta))
            {
                usePrefix = usePrefixMeta.Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            // Determine if we should use prefix
            var shouldUsePrefix = usePrefix ?? PrefixSkills;

            if (!shouldUsePrefix)
            {
                return relativePath; // No prefix
            }

            // Determine the prefix to use
            // Priority: DefaultPrefix (global) > SuggestedPrefix (from package author) > PackageId
            string prefix;
            if (!string.IsNullOrEmpty(DefaultPrefix))
            {
                prefix = DefaultPrefix;
            }
            else
            {
                var suggestedPrefix = item.GetMetadata("SuggestedPrefix");
                prefix = !string.IsNullOrEmpty(suggestedPrefix) ? suggestedPrefix : packageId;
            }

            return Path.Combine(prefix, relativePath);
        }

        /// <summary>
        /// Tracks a file for gitignore entry in its skill directory.
        /// </summary>
        private void TrackGitignoreEntry(
            Dictionary<string, Dictionary<string, List<string>>> gitignoreEntries,
            string agentSkillsPath,
            string relativePath,
            string packageId)
        {
            // Get the skill subdirectory (first path component) and the filename within it
            var pathParts = relativePath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

            if (pathParts.Length == 0) return;

            // Build the skill directory path (could be multiple levels deep)
            // We want to create gitignore in the immediate parent directory of the file
            var fileName = pathParts[pathParts.Length - 1];
            var skillDirParts = pathParts.Take(pathParts.Length - 1).ToArray();

            string gitignoreDir;
            string entryName;

            if (skillDirParts.Length > 0)
            {
                // File is in a subdirectory - gitignore goes in the deepest directory
                gitignoreDir = Path.Combine(agentSkillsPath, string.Join(Path.DirectorySeparatorChar.ToString(), skillDirParts));
                entryName = fileName;
            }
            else
            {
                // File is directly in skills folder - use the skills folder itself
                gitignoreDir = agentSkillsPath;
                entryName = fileName;
            }

            var key = gitignoreDir;

            if (!gitignoreEntries.ContainsKey(key))
            {
                gitignoreEntries[key] = new Dictionary<string, List<string>>();
            }

            if (!gitignoreEntries[key].ContainsKey(packageId))
            {
                gitignoreEntries[key][packageId] = new List<string>();
            }

            if (!gitignoreEntries[key][packageId].Contains(entryName))
            {
                gitignoreEntries[key][packageId].Add(entryName);
            }
        }

        /// <summary>
        /// Writes or updates .gitignore files in skill directories.
        /// </summary>
        private void WriteGitignoreFiles(
            Dictionary<string, Dictionary<string, List<string>>> gitignoreEntries,
            IEnumerable<string> agents)
        {
            foreach (var dirKvp in gitignoreEntries)
            {
                var gitignorePath = Path.Combine(dirKvp.Key, ".gitignore");
                WriteManagedGitignore(gitignorePath, dirKvp.Value);
            }
        }

        /// <summary>
        /// Performs the read-merge-write of a single managed .gitignore file, tolerating the
        /// file contention that occurs during parallel builds (see <see cref="CopyFileSafe"/>).
        /// Multiple projects sharing a repository root write the same shared .gitignore files,
        /// so both the read and the write are retried with backoff; the operation converges
        /// because once a parallel writer wins, the next read sees identical content and the
        /// write is skipped.
        /// </summary>
        private void WriteManagedGitignore(string gitignorePath, Dictionary<string, List<string>> packageFiles)
        {
            IOException lastError = null;

            for (var attempt = 1; attempt <= MaxCopyAttempts; attempt++)
            {
                try
                {
                    // Read existing gitignore if present.
                    var existingContent = File.Exists(gitignorePath) ? File.ReadAllText(gitignorePath) : string.Empty;
                    var newContent = BuildManagedGitignoreContent(existingContent, packageFiles);

                    // Only write if content changed (a parallel build may already have written it).
                    if (newContent.Equals(existingContent))
                    {
                        return;
                    }

                    File.WriteAllText(gitignorePath, newContent);
                    Log.LogMessage(MessageImportance.Normal,
                        "Zakira.Imprint.Sdk: Updated .gitignore in {0}", Path.GetDirectoryName(gitignorePath));
                    return;
                }
                catch (IOException ex)
                {
                    lastError = ex;
                    if (attempt < MaxCopyAttempts)
                    {
                        Thread.Sleep(GetCopyBackoffMilliseconds(attempt));
                    }
                }
            }

            // Retries exhausted: tolerate the benign case where a parallel build already wrote
            // the managed entries for every package; otherwise surface the original error.
            if (!GitignoreContainsPackages(gitignorePath, packageFiles.Keys))
            {
                throw lastError;
            }
        }

        /// <summary>
        /// Builds the managed .gitignore content: non-managed lines are preserved, followed by a
        /// managed section per package.
        /// </summary>
        private string BuildManagedGitignoreContent(string existingContent, Dictionary<string, List<string>> packageFiles)
        {
            // Parse existing content to preserve non-Imprint entries.
            var (preservedLines, _) = ParseExistingGitignore(existingContent);

            var sb = new StringBuilder();

            // Add preserved lines (non-managed content).
            foreach (var line in preservedLines)
            {
                sb.AppendLine(line);
            }

            // Add managed sections for each package.
            foreach (var pkgKvp in packageFiles.OrderBy(p => p.Key))
            {
                var packageId = pkgKvp.Key;
                var files = pkgKvp.Value.OrderBy(f => f).ToList();

                sb.AppendLine($"{GitignoreHeader} ({packageId})");
                foreach (var file in files)
                {
                    sb.AppendLine(file);
                }
            }

            return sb.ToString().TrimEnd() + "\n";
        }

        /// <summary>
        /// Checks (tolerating I/O contention) whether the .gitignore already contains a managed
        /// header for every supplied package.
        /// </summary>
        private static bool GitignoreContainsPackages(string gitignorePath, IEnumerable<string> packageIds)
        {
            try
            {
                if (!File.Exists(gitignorePath))
                {
                    return false;
                }

                var content = File.ReadAllText(gitignorePath);
                return packageIds.All(id => content.Contains($"{GitignoreHeader} ({id})"));
            }
            catch (IOException)
            {
                return false;
            }
        }

        /// <summary>
        /// Parses existing gitignore content to separate non-managed lines from managed sections.
        /// </summary>
        private (List<string> PreservedLines, Dictionary<string, List<string>> ManagedSections) ParseExistingGitignore(string content)
        {
            var preservedLines = new List<string>();
            var managedSections = new Dictionary<string, List<string>>();

            if (string.IsNullOrEmpty(content))
            {
                return (preservedLines, managedSections);
            }

            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string currentPackage = null;

            foreach (var line in lines)
            {
                if (line.StartsWith(GitignoreHeader))
                {
                    // Extract package ID from header: "# Managed by Zakira.Imprint (PackageId)"
                    var start = line.IndexOf('(');
                    var end = line.IndexOf(')');
                    if (start >= 0 && end > start)
                    {
                        currentPackage = line.Substring(start + 1, end - start - 1);
                        if (!managedSections.ContainsKey(currentPackage))
                        {
                            managedSections[currentPackage] = new List<string>();
                        }
                    }
                }
                else if (currentPackage != null)
                {
                    // Line belongs to current managed section
                    managedSections[currentPackage].Add(line);
                }
                else
                {
                    // Non-managed line
                    preservedLines.Add(line);
                }
            }

            return (preservedLines, managedSections);
        }

        /// <summary>
        /// Writes the unified manifest.json (version 2 format) to .imprint/manifest.json.
        /// </summary>
        private void WriteUnifiedManifest(string imprintDir,
            Dictionary<string, Dictionary<string, List<string>>> manifestPackages)
        {
            var manifestPath = Path.Combine(imprintDir, "manifest.json");

            var packagesObj = new JsonObject();
            foreach (var pkgKvp in manifestPackages.OrderBy(p => p.Key))
            {
                var filesObj = new JsonObject();
                foreach (var agentKvp in pkgKvp.Value.OrderBy(a => a.Key))
                {
                    var fileArray = new JsonArray(
                        agentKvp.Value.OrderBy(f => f)
                            .Select(f => (JsonNode)JsonValue.Create(f)!)
                            .ToArray());
                    filesObj[agentKvp.Key] = fileArray;
                }
                var pkgObj = new JsonObject { ["files"] = filesObj };
                packagesObj[pkgKvp.Key] = pkgObj;
            }

            var manifestObj = new JsonObject
            {
                ["version"] = 2,
                ["packages"] = packagesObj
            };

            var options = GetJsonOptions();
            var content = manifestObj.ToJsonString(options);
            if (!content.EndsWith("\n")) content += "\n";
            WriteAllTextSafe(manifestPath, content);
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

        /// <summary>
        /// Cleans up files and gitignore entries from packages that were previously installed
        /// but are no longer referenced.
        /// </summary>
        private void CleanRemovedPackages(string imprintDir, HashSet<string> currentPackageIds)
        {
            var manifestPath = Path.Combine(imprintDir, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                return;
            }

            try
            {
                var manifestText = File.ReadAllText(manifestPath);
                var manifestDoc = JsonNode.Parse(manifestText);
                var version = manifestDoc?["version"]?.GetValue<int>() ?? 0;

                if (version < 2)
                {
                    return; // Only support v2 manifests for incremental cleanup
                }

                var packages = manifestDoc?["packages"]?.AsObject();
                if (packages == null)
                {
                    return;
                }

                var removedPackages = new List<string>();
                var gitignoresToClean = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                var dirsToCheck = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var pkgKvp in packages)
                {
                    var packageId = pkgKvp.Key;

                    // Skip if package is still referenced
                    if (currentPackageIds.Contains(packageId))
                    {
                        continue;
                    }

                    removedPackages.Add(packageId);
                    var pkgObj = pkgKvp.Value?.AsObject();
                    var filesObj = pkgObj?["files"]?.AsObject();

                    if (filesObj == null) continue;

                    foreach (var agentKvp in filesObj)
                    {
                        var fileArray = agentKvp.Value?.AsArray();
                        if (fileArray == null) continue;

                        foreach (var fileNode in fileArray)
                        {
                            var filePath = fileNode?.GetValue<string>();
                            if (string.IsNullOrEmpty(filePath)) continue;

                            var dir = Path.GetDirectoryName(filePath);

                            // Delete the file
                            if (File.Exists(filePath))
                            {
                                File.Delete(filePath);
                            }

                            // Track gitignore cleanup and directory for potential removal
                            if (!string.IsNullOrEmpty(dir))
                            {
                                dirsToCheck.Add(dir);
                                if (!gitignoresToClean.ContainsKey(dir))
                                {
                                    gitignoresToClean[dir] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                }
                                gitignoresToClean[dir].Add(packageId);
                            }
                        }
                    }

                    Log.LogMessage(MessageImportance.High,
                        "Zakira.Imprint.Sdk: Cleaned up removed package '{0}'", packageId);
                }

                // Clean up gitignore entries
                foreach (var kvp in gitignoresToClean)
                {
                    CleanGitignoreForRemovedPackages(kvp.Key, kvp.Value);
                }

                // Clean up empty directories
                foreach (var dir in dirsToCheck.OrderByDescending(d => d.Length))
                {
                    TryRemoveEmptyDirectory(dir);
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning("Zakira.Imprint.Sdk: Failed to clean removed packages: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Cleans gitignore entries for removed packages in a specific directory.
        /// </summary>
        private void CleanGitignoreForRemovedPackages(string directory, HashSet<string> removedPackages)
        {
            var gitignorePath = Path.Combine(directory, ".gitignore");
            if (!File.Exists(gitignorePath)) return;

            try
            {
                var content = File.ReadAllText(gitignorePath);
                var newContent = RemovePackageSectionsFromGitignore(content, removedPackages);

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

        /// <summary>
        /// Removes sections managed by specified packages from gitignore content.
        /// </summary>
        private string RemovePackageSectionsFromGitignore(string content, HashSet<string> packagesToRemove)
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

        /// <summary>
        /// Tries to remove an empty directory and its empty parent directories.
        /// </summary>
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

        /// <summary>
        /// Maximum number of attempts to copy a contended destination file before giving up.
        /// </summary>
        private const int MaxCopyAttempts = 5;

        /// <summary>
        /// Copies a file to the destination, skipping if already identical, and tolerating
        /// the file contention that occurs during parallel builds where multiple projects
        /// reference the same package and race to write the same destination file.
        /// </summary>
        /// <remarks>
        /// Two complementary strategies are used:
        /// <list type="number">
        /// <item><description>
        /// Skip-if-identical: if the destination already matches the source (size +
        /// last-write time) no copy is attempted. This eliminates the race entirely on
        /// incremental builds, where a previous build already produced the file.
        /// </description></item>
        /// <item><description>
        /// Retry-with-recheck: on a clean build, several processes may attempt the very
        /// first copy at the same time. The OS lets one process win and throws
        /// <see cref="IOException"/> for the others. Instead of failing - or swallowing the
        /// error while the winner is still mid-write and the destination is incomplete - we
        /// retry with a short backoff and re-check identity at the top of every attempt, so a
        /// "loser" only returns successfully once the winner has produced a complete, matching
        /// file. The original error is surfaced only if the destination still does not match
        /// after the final attempt.
        /// </description></item>
        /// </list>
        /// </remarks>
        private static void CopyFileSafe(string sourceFile, string destFile)
        {
            for (var attempt = 1; ; attempt++)
            {
                // A previous build - or a parallel one that just won the race - may already
                // have produced an identical file, in which case there is nothing to do.
                if (File.Exists(destFile) && FilesAreIdentical(sourceFile, destFile))
                {
                    return;
                }

                try
                {
                    File.Copy(sourceFile, destFile, overwrite: true);
                    return;
                }
                catch (IOException) when (attempt < MaxCopyAttempts)
                {
                    // The destination is locked by another build process racing to write the
                    // same bytes. Back off and retry; the identity check at the top of the loop
                    // short-circuits as soon as the winning process finishes.
                    Thread.Sleep(GetCopyBackoffMilliseconds(attempt));
                }
                catch (IOException)
                {
                    // Final attempt failed: treat it as success only if the destination now
                    // matches the source (the winner completed), otherwise surface the error.
                    if (File.Exists(destFile) && FilesAreIdentical(sourceFile, destFile))
                    {
                        return;
                    }

                    throw;
                }
            }
        }

        /// <summary>
        /// Exponential backoff (50ms, 100ms, 200ms, 400ms, ...) between contended copy attempts.
        /// </summary>
        private static int GetCopyBackoffMilliseconds(int attempt) => 50 * (1 << (attempt - 1));

        private static bool FilesAreIdentical(string a, string b)
        {
            var infoA = new FileInfo(a);
            var infoB = new FileInfo(b);
            return infoA.Length == infoB.Length &&
                   infoA.LastWriteTimeUtc == infoB.LastWriteTimeUtc;
        }

        /// <summary>
        /// Writes text to a file, tolerating the file contention that occurs during parallel
        /// builds where multiple projects share a repository root and write the same shared file
        /// (e.g. the manifest or an auto-generated .gitignore). Writes are retried with backoff,
        /// and a write that loses the race but finds the destination already holds the intended
        /// content is treated as success.
        /// </summary>
        private static void WriteAllTextSafe(string path, string contents)
        {
            IOException lastError = null;

            for (var attempt = 1; attempt <= MaxCopyAttempts; attempt++)
            {
                // Another parallel build may already have written exactly this content.
                if (FileAlreadyHasText(path, contents))
                {
                    return;
                }

                try
                {
                    File.WriteAllText(path, contents);
                    return;
                }
                catch (IOException ex)
                {
                    lastError = ex;
                    if (attempt < MaxCopyAttempts)
                    {
                        Thread.Sleep(GetCopyBackoffMilliseconds(attempt));
                    }
                }
            }

            // Retries exhausted: tolerate the benign case where a parallel build already wrote
            // the same content; otherwise surface the original error.
            if (!FileAlreadyHasText(path, contents))
            {
                throw lastError;
            }
        }

        /// <summary>
        /// Checks (tolerating I/O contention) whether the file already contains exactly the given text.
        /// </summary>
        private static bool FileAlreadyHasText(string path, string contents)
        {
            try
            {
                return File.Exists(path) && File.ReadAllText(path) == contents;
            }
            catch (IOException)
            {
                return false;
            }
        }

        private void EnsureImprintGitignore(string imprintDir)
        {
            var gitignorePath = Path.Combine(imprintDir, ".gitignore");
            if (!File.Exists(gitignorePath))
            {
                WriteAllTextSafe(gitignorePath, "# Imprint manifests (auto-generated, do not commit)\n*\n");
                Log.LogMessage(MessageImportance.Normal, "Zakira.Imprint.Sdk: Created {0}", gitignorePath);
            }
        }
    }
}
