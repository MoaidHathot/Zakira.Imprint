using Microsoft.Build.Framework;

namespace Zakira.Imprint.Sdk.Tests;

/// <summary>
/// Minimal ITaskItem implementation for unit testing MSBuild tasks.
/// Supports custom metadata via a backing dictionary.
/// </summary>
public class MockTaskItem : ITaskItem
{
    private readonly Dictionary<string, string> _metadata = new(StringComparer.OrdinalIgnoreCase);

    public MockTaskItem(string itemSpec)
    {
        ItemSpec = itemSpec;
    }

    public MockTaskItem(string itemSpec, Dictionary<string, string> metadata)
    {
        ItemSpec = itemSpec;
        foreach (var kvp in metadata)
        {
            _metadata[kvp.Key] = kvp.Value;
        }
    }

    public string ItemSpec { get; set; }

    public System.Collections.ICollection MetadataNames => _metadata.Keys;
    public int MetadataCount => _metadata.Count;

    public System.Collections.IDictionary CloneCustomMetadata() => new Dictionary<string, string>(_metadata, StringComparer.OrdinalIgnoreCase);
    public void CopyMetadataTo(ITaskItem destinationItem)
    {
        foreach (var kvp in _metadata)
        {
            destinationItem.SetMetadata(kvp.Key, kvp.Value);
        }
    }
    public string GetMetadata(string metadataName) =>
        _metadata.TryGetValue(metadataName, out var value) ? value : string.Empty;
    public void RemoveMetadata(string metadataName) => _metadata.Remove(metadataName);
    public void SetMetadata(string metadataName, string metadataValue) => _metadata[metadataName] = metadataValue;
}
