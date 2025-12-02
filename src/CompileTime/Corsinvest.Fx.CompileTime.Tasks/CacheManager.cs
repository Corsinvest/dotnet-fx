using System.Text.Json;
using System.Text.Json.Serialization;

namespace Corsinvest.Fx.CompileTime.Tasks;

internal class CacheManager
{
    public string FileName { get; private set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Version { get; set; } = "1.0";
    public Dictionary<string, Entry> Entries { get; set; } = [];

    private bool _isDirty;
    private readonly object _lockObject = new();
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public void SetProjectDir(string projectDir) => FileName = Path.Combine(projectDir, "obj", "CompileTimeCache.json");

    public void Set(string key, Entry value)
    {
        lock (_lockObject)
        {
            Entries[key] = value;
            if (value.Persistent) { _isDirty = true; }
        }
    }

    public void Load()
    {
        try
        {
            if (!File.Exists(FileName)) { return; }

            var json = File.ReadAllText(FileName);
            var data = JsonSerializer.Deserialize<CacheManager>(json);

            if (data != null && data.Version == Version)
            {
                lock (_lockObject)
                {
                    Entries = data.Entries ?? [];
                    Version = data.Version;
                }
            }
        }
        catch (Exception)
        {
        }
    }

    public void Save()
    {
        Dictionary<string, Entry>? entriesToSave = null;

        lock (_lockObject)
        {
            if (!_isDirty) { return; }

            entriesToSave = Entries
                .Where(a => a.Value.Persistent)
                .ToDictionary(a => a.Key, kvp => kvp.Value);

            _isDirty = false;
        }

        try
        {

            var directory = Path.GetDirectoryName(FileName);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(new CacheManager
            {
                FileName = FileName,
                CreatedAt = CreatedAt,
                Version = Version,
                Entries = entriesToSave
            }, _jsonOptions);
            File.WriteAllText(FileName, json);
        }
        catch (Exception)
        {
            lock (_lockObject)
            {
                _isDirty = true;
            }
        }
    }

    public class Entry
    {
        public string MethodContentHash { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string SerializedValue { get; set; } = default!;
        public string? ValueType { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ErrorCode { get; set; }
        public DateTime CachedAt { get; set; }
        public long ExecutionTimeMs { get; set; }

        [JsonIgnore]
        public bool Persistent { get; set; }
        public long MemoryFootprintBytes { get; set; }
    }
}
