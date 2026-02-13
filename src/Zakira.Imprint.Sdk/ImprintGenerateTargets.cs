using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Zakira.Imprint.Sdk
{
    /// <summary>
    /// MSBuild task that generates a .targets file for a skill package at pack time.
    /// Reads &lt;Imprint&gt; items from the project and generates the corresponding
    /// &lt;ImprintContent&gt; and &lt;ImprintMcpFragment&gt; items for the NuGet package.
    /// </summary>
    public class ImprintGenerateTargets : Task
    {
        /// <summary>
        /// The &lt;Imprint&gt; items to process. Each item can have metadata:
        /// - Type: "Skill" (default) or "Mcp"
        /// - SuggestedPrefix: Author's suggested prefix for skill folders
        /// - DestinationBase: Override for destination base (defaults to $(ImprintSkillsPath))
        /// </summary>
        [Required]
        public ITaskItem[] ImprintItems { get; set; } = Array.Empty<ITaskItem>();

        /// <summary>
        /// The NuGet package ID.
        /// </summary>
        [Required]
        public string PackageId { get; set; } = string.Empty;

        /// <summary>
        /// The output directory for the generated .targets file.
        /// Typically $(IntermediateOutputPath)Imprint/
        /// </summary>
        [Required]
        public string OutputPath { get; set; } = string.Empty;

        /// <summary>
        /// Whether Imprint is enabled by default when consumers install the package.
        /// Defaults to true.
        /// </summary>
        public bool EnabledByDefault { get; set; } = true;

        /// <summary>
        /// The path to the generated .targets file.
        /// </summary>
        [Output]
        public string GeneratedTargetsFile { get; set; } = string.Empty;

        public override bool Execute()
        {
            try
            {
                if (ImprintItems == null || ImprintItems.Length == 0)
                {
                    Log.LogMessage(MessageImportance.Normal,
                        "Zakira.Imprint.Sdk: No <Imprint> items found, skipping .targets generation.");
                    return true;
                }

                if (string.IsNullOrEmpty(PackageId))
                {
                    Log.LogError("Zakira.Imprint.Sdk: PackageId is required for .targets generation.");
                    return false;
                }

                // Ensure output directory exists
                Directory.CreateDirectory(OutputPath);

                // Generate safe property name from package ID (replace dots with underscores)
                var safePackageId = MakeSafePropertyName(PackageId);

                // Group items by type
                var skillItems = new List<ITaskItem>();
                var mcpItems = new List<ITaskItem>();

                foreach (var item in ImprintItems)
                {
                    var itemType = item.GetMetadata("Type");
                    if (string.IsNullOrEmpty(itemType) ||
                        itemType.Equals("Skill", StringComparison.OrdinalIgnoreCase))
                    {
                        skillItems.Add(item);
                    }
                    else if (itemType.Equals("Mcp", StringComparison.OrdinalIgnoreCase))
                    {
                        mcpItems.Add(item);
                    }
                    else
                    {
                        Log.LogWarning(
                            "Zakira.Imprint.Sdk: Unknown Imprint Type '{0}' for item '{1}', skipping.",
                            itemType, item.ItemSpec);
                    }
                }

                // Generate .targets content
                var content = GenerateTargetsContent(safePackageId, skillItems, mcpItems);

                // Write to file
                var outputFile = Path.Combine(OutputPath, $"{PackageId}.targets");
                File.WriteAllText(outputFile, content);

                GeneratedTargetsFile = outputFile;

                Log.LogMessage(MessageImportance.High,
                    "Zakira.Imprint.Sdk: Generated {0} with {1} skill item(s) and {2} MCP item(s).",
                    outputFile, skillItems.Count, mcpItems.Count);

                return true;
            }
            catch (Exception ex)
            {
                Log.LogError("Zakira.Imprint.Sdk: Failed to generate .targets file: {0}", ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// Converts a package ID to a safe MSBuild property name by replacing
        /// non-alphanumeric characters with underscores.
        /// </summary>
        private string MakeSafePropertyName(string packageId)
        {
            return Regex.Replace(packageId, @"[^a-zA-Z0-9]", "_");
        }

        /// <summary>
        /// Generates the .targets file content.
        /// </summary>
        private string GenerateTargetsContent(
            string safePackageId,
            List<ITaskItem> skillItems,
            List<ITaskItem> mcpItems)
        {
            var sb = new StringBuilder();
            var rootProperty = $"_Imprint_{safePackageId}_Root";

            sb.AppendLine("<Project>");
            sb.AppendLine("  <!--");
            sb.AppendLine($"    {PackageId} - MSBuild Targets");
            sb.AppendLine("    ");
            sb.AppendLine("    This file was auto-generated by Zakira.Imprint.Sdk at pack time.");
            sb.AppendLine("    It declares ImprintContent/ImprintMcpFragment items that the SDK will process");
            sb.AppendLine("    when the consumer builds their project.");
            sb.AppendLine("  -->");
            sb.AppendLine();

            // Root property pointing to package content directory
            // Files are packed at content/{relative-path} so we need to point to ../content/
            sb.AppendLine("  <PropertyGroup>");
            sb.AppendLine($"    <{rootProperty}>$(MSBuildThisFileDirectory)..\\content\\</{rootProperty}>");
            sb.AppendLine("  </PropertyGroup>");
            sb.AppendLine();

            // Generate ImprintContent items for skills
            if (skillItems.Count > 0)
            {
                sb.AppendLine("  <!-- Skill files to copy (handled by Zakira.Imprint.Sdk's ImprintCopyContent task) -->");
                sb.AppendLine("  <ItemGroup>");

                foreach (var item in skillItems)
                {
                    var includePath = NormalizeIncludePath(item.ItemSpec);
                    var sourceBase = GetSourceBase(item.ItemSpec);
                    var suggestedPrefix = item.GetMetadata("SuggestedPrefix");
                    var destinationBase = item.GetMetadata("DestinationBase");

                    sb.AppendLine($"    <ImprintContent Include=\"$({rootProperty}){includePath}\">");
                    
                    // DestinationBase - use provided or default to $(ImprintSkillsPath)
                    if (!string.IsNullOrEmpty(destinationBase))
                    {
                        sb.AppendLine($"      <DestinationBase>{destinationBase}</DestinationBase>");
                    }
                    else
                    {
                        sb.AppendLine("      <DestinationBase>$(ImprintSkillsPath)</DestinationBase>");
                    }

                    sb.AppendLine($"      <PackageId>{PackageId}</PackageId>");
                    sb.AppendLine($"      <SourceBase>$({rootProperty}){sourceBase}</SourceBase>");

                    if (!string.IsNullOrEmpty(suggestedPrefix))
                    {
                        sb.AppendLine($"      <SuggestedPrefix>{suggestedPrefix}</SuggestedPrefix>");
                    }

                    // Add EnabledByDefault if not true (the default)
                    if (!EnabledByDefault)
                    {
                        sb.AppendLine("      <EnabledByDefault>false</EnabledByDefault>");
                    }

                    sb.AppendLine("    </ImprintContent>");
                }

                sb.AppendLine("  </ItemGroup>");
                sb.AppendLine();
            }

            // Generate ImprintMcpFragment items
            if (mcpItems.Count > 0)
            {
                sb.AppendLine("  <!-- MCP server fragments (handled by Zakira.Imprint.Sdk's ImprintMergeMcpServers task) -->");
                sb.AppendLine("  <ItemGroup>");

                foreach (var item in mcpItems)
                {
                    var includePath = NormalizeIncludePath(item.ItemSpec);

                    sb.AppendLine($"    <ImprintMcpFragment Include=\"$({rootProperty}){includePath}\">");
                    sb.AppendLine($"      <PackageId>{PackageId}</PackageId>");

                    // Add EnabledByDefault if not true
                    if (!EnabledByDefault)
                    {
                        sb.AppendLine("      <EnabledByDefault>false</EnabledByDefault>");
                    }

                    sb.AppendLine("    </ImprintMcpFragment>");
                }

                sb.AppendLine("  </ItemGroup>");
                sb.AppendLine();
            }

            sb.AppendLine("</Project>");

            return sb.ToString();
        }

        /// <summary>
        /// Normalizes an include path by converting backslashes to forward slashes
        /// and removing leading slashes.
        /// </summary>
        private string NormalizeIncludePath(string path)
        {
            // Normalize directory separators
            var normalized = path.Replace('/', '\\');

            // Remove leading slash/backslash if present
            if (normalized.StartsWith("\\") || normalized.StartsWith("/"))
            {
                normalized = normalized.Substring(1);
            }

            return normalized;
        }

        /// <summary>
        /// Extracts the source base directory from an include path.
        /// For "skills\**\*" returns "skills\"
        /// For "mcp\*.mcp.json" returns "mcp\"
        /// </summary>
        private string GetSourceBase(string includePath)
        {
            var normalized = NormalizeIncludePath(includePath);

            // Find the first wildcard
            var wildcardIndex = normalized.IndexOfAny(new[] { '*', '?' });

            if (wildcardIndex < 0)
            {
                // No wildcard - use the directory of the file
                var lastSep = normalized.LastIndexOfAny(new[] { '\\', '/' });
                if (lastSep >= 0)
                {
                    return normalized.Substring(0, lastSep + 1);
                }
                return string.Empty;
            }

            // Find the last separator before the wildcard
            var beforeWildcard = normalized.Substring(0, wildcardIndex);
            var lastSepBeforeWildcard = beforeWildcard.LastIndexOfAny(new[] { '\\', '/' });

            if (lastSepBeforeWildcard >= 0)
            {
                return normalized.Substring(0, lastSepBeforeWildcard + 1);
            }

            return string.Empty;
        }
    }
}
