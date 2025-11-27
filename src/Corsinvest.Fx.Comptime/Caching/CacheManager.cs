// using System.Text.Json;
// using System.Text.Json.Serialization;

// namespace Corsinvest.Fx.Comptime.Caching;

// internal class CacheManager
// {
//     public string FileName { get; private set; } = default!;
//     public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
//     public string Version { get; set; } = "1.0";
//     public Dictionary<string, Entry> Entries { get; set; } = [];

//     private volatile bool _isDirty;
//     private readonly object _lockObject = new();

//     public void SetProjectDir(string projectDir) => FileName = Path.Combine(projectDir, "obj", "CompileTimeCache.json");

//     public void Set(string key, Entry value)
//     {
//         lock (_lockObject)
//         {
//             Entries[key] = value;
//             if (value.Persistent) { _isDirty = true; }
//         }
//     }

//     public void Load()
//     {
//         if (File.Exists(FileName))
//         {
//             var data = JsonSerializer.Deserialize<CacheManager>(File.ReadAllText(FileName));
//             if (data != null && data.Version == Version)
//             {
//                 Entries = data.Entries ?? [];
//                 Version = data.Version;
//             }
//         }
//     }

//     public void Save()
//     {
//         lock (_lockObject)
//         {
//             if (!_isDirty) { return; }

//             var cacheToSave = new CacheManager
//             {
//                 FileName = FileName,
//                 CreatedAt = CreatedAt,
//                 Version = Version,
//                 Entries = Entries.Where(a => a.Value.Persistent).ToDictionary(a => a.Key, kvp => kvp.Value)
//             };

//             File.WriteAllText(FileName, JsonSerializer.Serialize(cacheToSave, new JsonSerializerOptions { WriteIndented = true }));
//             _isDirty = false;
//         }
//     }

//     public class Entry
//     {
//         public string MethodContentHash { get; set; } = string.Empty;
//         public bool Success { get; set; }
//         public string SerializedValue { get; set; } = default!;
//         public string? ValueType { get; set; }
//         public string? ErrorMessage { get; set; }
//         public string? ErrorCode { get; set; }
//         public DateTime CachedAt { get; set; }
//         public long ExecutionTimeMs { get; set; }

//         [JsonIgnore]
//         public bool Persistent { get; set; }
//         public long MemoryFootprintBytes { get; set; }
//     }
// }
