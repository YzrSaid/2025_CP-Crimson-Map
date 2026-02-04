using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
#if UNITY_ANDROID
using UnityEngine.Networking;
#endif

/// <summary>
/// Smart file loader that caches JSON content in memory.
/// Reads from APK only ONCE per session, then reuses cached data.
/// </summary>
public static class CrossPlatformFileLoader
{
    // Cache dictionary: fileName → JSON content
    private static Dictionary<string, string> fileCache = new Dictionary<string, string>();

    // Track which files are currently being loaded to prevent duplicate requests
    private static HashSet<string> currentlyLoading = new HashSet<string>();

    /// <summary>
    /// Load a JSON file with caching.
    /// First call: Loads from APK and caches in memory.
    /// Subsequent calls: Returns cached version instantly (no APK read).
    /// </summary>
    public static IEnumerator LoadJsonFile(string fileName, System.Action<string> onSuccess, System.Action<string> onError)
    {
        // Check if already cached
        if (fileCache.ContainsKey(fileName))
        {
            Debug.Log($"[FileLoader] Using CACHED version of {fileName}");
            onSuccess?.Invoke(fileCache[fileName]);
            yield break;
        }

        // Check if currently loading (prevent duplicate loads)
        if (currentlyLoading.Contains(fileName))
        {
            Debug.Log($"[FileLoader] {fileName} is already loading, waiting...");

            // Wait until loading completes
            yield return new WaitUntil(() => !currentlyLoading.Contains(fileName));

            // Now it should be in cache
            if (fileCache.ContainsKey(fileName))
            {
                onSuccess?.Invoke(fileCache[fileName]);
            }
            else
            {
                onError?.Invoke($"Failed to load {fileName}");
            }
            yield break;
        }

        // Mark as currently loading
        currentlyLoading.Add(fileName);

        Debug.Log($"[FileLoader] Loading {fileName} from APK (StreamingAssets)...");

        // Load from APK
        yield return LoadFromStreamingAssets(fileName,
            (content) =>
            {
                // Cache the content
                fileCache[fileName] = content;
                currentlyLoading.Remove(fileName);

                Debug.Log($"[FileLoader] Cached {fileName} ({content.Length} chars)");
                onSuccess?.Invoke(content);
            },
            (error) =>
            {
                currentlyLoading.Remove(fileName);
                onError?.Invoke(error);
            }
        );
    }

    /// <summary>
    /// Load from StreamingAssets (APK). This is the actual decompression work.
    /// </summary>
    private static IEnumerator LoadFromStreamingAssets(string fileName, System.Action<string> onSuccess, System.Action<string> onError)
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, fileName);

#if UNITY_ANDROID
        // Android: Files are compressed in APK, use UnityWebRequest
        UnityWebRequest request = UnityWebRequest.Get(filePath);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string jsonContent = request.downloadHandler.text;

            if (string.IsNullOrEmpty(jsonContent))
            {
                onError?.Invoke($"File is empty: {fileName}");
            }
            else
            {
                onSuccess?.Invoke(jsonContent);
            }
        }
        else
        {
            onError?.Invoke($"Error loading {fileName}: {request.error}");
        }
#else
        // Editor/iOS/Other: Direct file read
        try
        {
            if (!File.Exists(filePath))
            {
                onError?.Invoke($"File not found: {filePath}");
                yield break;
            }

            string jsonContent = File.ReadAllText(filePath);

            if (string.IsNullOrEmpty(jsonContent))
            {
                onError?.Invoke($"File is empty: {fileName}");
            }
            else
            {
                onSuccess?.Invoke(jsonContent);
            }
        }
        catch (System.Exception e)
        {
            onError?.Invoke($"Error reading {fileName}: {e.Message}");
        }
        
        yield return null;
#endif
    }

    /// <summary>
    /// Load a user-modifiable file from PersistentDataPath (bookmarks, recent destinations).
    /// These are NOT cached because they can change during the session.
    /// </summary>
    public static IEnumerator LoadUserDataFile(string fileName, System.Action<string> onSuccess, System.Action<string> onError)
    {
        string persistentPath = Path.Combine(Application.persistentDataPath, fileName);

        // Check if user has a modified version
        if (File.Exists(persistentPath))
        {
            try
            {
                string jsonContent = File.ReadAllText(persistentPath);
                onSuccess?.Invoke(jsonContent);
                yield break;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[FileLoader] Error reading user file {fileName}: {e.Message}. Loading default.");
            }
        }

        // If no user version exists, load default from StreamingAssets (with caching)
        yield return LoadJsonFile(fileName, onSuccess, onError);
    }

    /// <summary>
    /// Save user data to PersistentDataPath (bookmarks, recent destinations, etc.)
    /// </summary>
    public static void SaveUserDataFile(string fileName, string jsonContent)
    {
        string persistentPath = Path.Combine(Application.persistentDataPath, fileName);

        try
        {
            File.WriteAllText(persistentPath, jsonContent);
            Debug.Log($"[FileLoader] Saved user data: {fileName}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FileLoader] Error saving {fileName}: {e.Message}");
        }
    }

    /// <summary>
    /// Check if a file is already cached in memory.
    /// </summary>
    public static bool IsCached(string fileName)
    {
        return fileCache.ContainsKey(fileName);
    }

    /// <summary>
    /// Get cached file content directly (synchronous).
    /// Returns null if not cached yet.
    /// </summary>
    public static string GetCached(string fileName)
    {
        if (fileCache.ContainsKey(fileName))
        {
            return fileCache[fileName];
        }
        return null;
    }

    /// <summary>
    /// Clear cache for a specific file (useful if you need to reload).
    /// </summary>
    public static void ClearCache(string fileName)
    {
        if (fileCache.ContainsKey(fileName))
        {
            fileCache.Remove(fileName);
            Debug.Log($"[FileLoader] Cleared cache for {fileName}");
        }
    }

    /// <summary>
    /// Clear all cached files (useful when app closes or for memory management).
    /// </summary>
    public static void ClearAllCache()
    {
        int count = fileCache.Count;
        fileCache.Clear();
        Debug.Log($"[FileLoader] Cleared all cache ({count} files)");
    }

    /// <summary>
    /// Get cache statistics.
    /// </summary>
    public static string GetCacheStats()
    {
        int totalFiles = fileCache.Count;
        long totalSize = 0;

        foreach (var content in fileCache.Values)
        {
            totalSize += content.Length;
        }

        return $"Cached Files: {totalFiles} | Total Size: {totalSize / 1024}KB";
    }

    /// <summary>
    /// Preload multiple files at once (good for app startup).
    /// </summary>
    public static IEnumerator PreloadFiles(string[] fileNames, System.Action onComplete = null)
    {
        Debug.Log($"[FileLoader] Preloading {fileNames.Length} files...");

        int loadedCount = 0;
        int errorCount = 0;

        foreach (string fileName in fileNames)
        {
            bool loaded = false;

            yield return LoadJsonFile(fileName,
                (content) =>
                {
                    loadedCount++;
                    loaded = true;
                },
                (error) =>
                {
                    Debug.LogWarning($"[FileLoader] Failed to preload {fileName}: {error}");
                    errorCount++;
                    loaded = true;
                }
            );

            yield return new WaitUntil(() => loaded);
        }

        Debug.Log($"[FileLoader] Preload complete! Loaded: {loadedCount}, Errors: {errorCount}");
        Debug.Log($"[FileLoader] {GetCacheStats()}");

        onComplete?.Invoke();
    }
}