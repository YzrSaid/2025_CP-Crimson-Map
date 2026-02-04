using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System;
using System.Linq;

/// <summary>
/// FULLY OFFLINE JSON File Manager with smart caching.
/// Loads files from APK (StreamingAssets) once, then reuses cached data.
/// NO Firebase - everything is offline.
/// </summary>
public class JSONFileManager : MonoBehaviour
{
    public static JSONFileManager Instance { get; private set; }

    // Files that are READ-ONLY from APK (will be cached)
    private readonly string[] readOnlyFiles = {
        "categories.json",
        "infrastructure.json",
        "campus.json",
        "maps.json",
        "indoor.json",
        "indoor_edges.json"
    };

    // Files that users can modify (NOT cached, read from PersistentDataPath)
    private readonly string[] userDataFiles = {
        "bookmarks.json",
        "recent_destinations.json"
    };

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Initialize JSON files - preloads everything into cache.
    /// </summary>
    public void InitializeJSONFiles(System.Action onComplete = null)
    {
        StartCoroutine(InitializeJSONFilesCoroutine(onComplete));
    }

    private IEnumerator InitializeJSONFilesCoroutine(System.Action onComplete)
    {
        Debug.Log("[JSONFileManager] Initializing OFFLINE mode - loading from APK...");

        // Create user data files if they don't exist
        yield return InitializeUserDataFiles();

        // PRELOAD all files from APK into cache
        Debug.Log("[JSONFileManager] Preloading files into cache...");
        bool preloadComplete = false;

        yield return PreloadEssentialFilesCoroutine(() =>
        {
            preloadComplete = true;
        });

        yield return new WaitUntil(() => preloadComplete);

        Debug.Log("[JSONFileManager] ✅ All files cached and ready!");
        Debug.Log(CrossPlatformFileLoader.GetCacheStats());

        onComplete?.Invoke();
    }

    private IEnumerator PreloadEssentialFilesCoroutine(System.Action onComplete)
    {
        // Initialize user data files first
        yield return InitializeUserDataFiles();

        // Build list of files to preload
        List<string> filesToPreload = new List<string>(readOnlyFiles);

        // Load maps.json to get map IDs
        bool mapsLoaded = false;
        List<string> mapIds = new List<string>();

        yield return CrossPlatformFileLoader.LoadJsonFile("maps.json",
            (content) =>
            {
                try
                {
                    var mapsArray = JsonHelper.FromJson<MapInfo>(content);
                    mapIds.AddRange(mapsArray.Select(m => m.map_id));
                    Debug.Log($"[JSONFileManager] Found {mapIds.Count} maps to preload");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[JSONFileManager] Error parsing maps.json: {e.Message}");
                }
                mapsLoaded = true;
            },
            (error) =>
            {
                Debug.LogError($"[JSONFileManager] Error loading maps.json: {error}");
                mapsLoaded = true;
            }
        );

        yield return new WaitUntil(() => mapsLoaded);

        // Add map-specific files
        foreach (string mapId in mapIds)
        {
            filesToPreload.Add($"nodes_{mapId}.json");
            filesToPreload.Add($"edges_{mapId}.json");
        }

        // Preload all files
        bool preloadComplete = false;
        yield return CrossPlatformFileLoader.PreloadFiles(filesToPreload.ToArray(), () =>
        {
            preloadComplete = true;
        });

        yield return new WaitUntil(() => preloadComplete);

        onComplete?.Invoke();
    }

    private IEnumerator InitializeUserDataFiles()
    {
        foreach (string fileName in userDataFiles)
        {
            string filePath = Path.Combine(Application.persistentDataPath, fileName);

            if (!File.Exists(filePath))
            {
                CreateDefaultUserDataFile(fileName, filePath);
            }

            yield return null;
        }
    }

    private void CreateDefaultUserDataFile(string fileName, string filePath)
    {
        string defaultContent = GetDefaultUserDataContent(fileName);

        try
        {
            File.WriteAllText(filePath, defaultContent);
            Debug.Log($"[JSONFileManager] Created default user file: {fileName}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[JSONFileManager] Error creating {fileName}: {e.Message}");
        }
    }

    private string GetDefaultUserDataContent(string fileName)
    {
        switch (fileName)
        {
            case "bookmarks.json":
                return "[]";
            case "recent_destinations.json":
                return JsonUtility.ToJson(new { recent_destinations = new object[] { } }, true);
            default:
                return "[]";
        }
    }

    /// <summary>
    /// Read a JSON file synchronously (backward compatible).
    /// Works for user data and cached read-only files.
    /// </summary>
    public string ReadJSONFile(string fileName)
    {
        if (IsUserDataFile(fileName))
        {
            // User data - read from PersistentDataPath
            string filePath = Path.Combine(Application.persistentDataPath, fileName);
            if (File.Exists(filePath))
            {
                try
                {
                    return File.ReadAllText(filePath);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[JSONFileManager] Error reading {fileName}: {e.Message}");
                    return null;
                }
            }
            return null;
        }
        else
        {
            // Read-only file - get from cache
            string cached = CrossPlatformFileLoader.GetCached(fileName);
            if (cached == null)
            {
                Debug.LogWarning($"[JSONFileManager] {fileName} not in cache yet. Call InitializeJSONFiles first!");
            }
            return cached;
        }
    }

    /// <summary>
    /// Write user data to PersistentDataPath.
    /// </summary>
    public void WriteJSONFile(string fileName, string jsonContent)
    {
        if (!IsUserDataFile(fileName))
        {
            Debug.LogWarning($"[JSONFileManager] Cannot write to read-only file: {fileName}");
            return;
        }

        CrossPlatformFileLoader.SaveUserDataFile(fileName, jsonContent);
    }

    public string ReadMapSpecificData(string collectionName, string mapId)
    {
        string collectionLower = collectionName.ToLower();
        string fileName;

        if (collectionLower == "nodes" || collectionLower == "edges")
        {
            fileName = $"{collectionLower}_{mapId}.json";
        }
        else
        {
            fileName = $"{collectionLower}.json";
        }

        return ReadJSONFile(fileName);
    }

    public void WriteMapSpecificData(string collectionName, string mapId, string jsonContent)
    {
        Debug.LogWarning($"[JSONFileManager] Map data is read-only. Cannot write to {collectionName}_{mapId}");
    }

    public void InitializeMapSpecificFiles(List<string> mapIds, System.Action onComplete = null)
    {
        // Files already preloaded during initialization
        onComplete?.Invoke();
    }

    public bool DoesFileExist(string fileName)
    {
        if (IsUserDataFile(fileName))
        {
            string filePath = Path.Combine(Application.persistentDataPath, fileName);
            return File.Exists(filePath);
        }

        return CrossPlatformFileLoader.IsCached(fileName);
    }

    public bool IsMapDataFresh(string mapId, int maxAgeHours = 24)
    {
        // In offline mode, data is always "fresh" since it's from the APK
        return true;
    }

    public LocalVersionCache GetMapVersionCache(string mapId)
    {
        // In offline mode, return a default cache
        return new LocalVersionCache
        {
            map_id = mapId,
            cached_version = "v1.0.0",
            map_name = "Campus Map",
            cache_timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }

    public void CleanupUnusedMapFiles(List<string> currentMapIds)
    {
        // Not needed in offline mode
        Debug.Log("[JSONFileManager] Cleanup skipped - offline mode");
    }

    public void ClearAllCaches()
    {
        CrossPlatformFileLoader.ClearAllCache();
        Debug.Log("[JSONFileManager] Cache cleared");
    }

    public void AddRecentDestination(Dictionary<string, object> destination)
    {
        try
        {
            string persistentPath = Path.Combine(Application.persistentDataPath, "recent_destinations.json");
            string jsonContent = File.Exists(persistentPath) ? File.ReadAllText(persistentPath) : "{}";

            if (!string.IsNullOrEmpty(jsonContent))
            {
                var data = JsonUtility.FromJson<RecentDestinationsData>(jsonContent);
                var recentList = new List<Dictionary<string, object>>(data.recent_destinations ?? new Dictionary<string, object>[0]);

                recentList.RemoveAll(d => d.ContainsKey("id") && destination.ContainsKey("id") &&
                                          d["id"].ToString() == destination["id"].ToString());

                recentList.Insert(0, destination);

                if (recentList.Count > 10)
                {
                    recentList = recentList.GetRange(0, 10);
                }

                data.recent_destinations = recentList.ToArray();
                string updatedJson = JsonUtility.ToJson(data, true);
                WriteJSONFile("recent_destinations.json", updatedJson);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[JSONFileManager] Error adding recent destination: {e.Message}");
        }
    }

    public string GetCacheStatus()
    {
        string status = "=== JSON FILE CACHE STATUS (OFFLINE MODE) ===\n\n";
        status += CrossPlatformFileLoader.GetCacheStats() + "\n\n";

        status += "Read-Only Files (from APK, should be cached):\n";
        foreach (string file in readOnlyFiles)
        {
            bool cached = CrossPlatformFileLoader.IsCached(file);
            status += $"  - {file}: {(cached ? "✓ CACHED" : "✗ NOT LOADED")}\n";
        }

        status += "\nUser Data Files (from PersistentDataPath):\n";
        foreach (string file in userDataFiles)
        {
            string filePath = Path.Combine(Application.persistentDataPath, file);
            bool exists = File.Exists(filePath);
            status += $"  - {file}: {(exists ? "✓ EXISTS" : "✗ MISSING")}\n";
        }

        return status;
    }

    public string GetFileSystemStatus()
    {
        return GetCacheStatus();
    }

    private bool IsUserDataFile(string fileName)
    {
        return userDataFiles.Contains(fileName);
    }

    void OnApplicationQuit()
    {
        CrossPlatformFileLoader.ClearAllCache();
    }
}