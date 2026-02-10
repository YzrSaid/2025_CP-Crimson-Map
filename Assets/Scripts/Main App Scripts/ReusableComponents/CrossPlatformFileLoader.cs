using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
#if UNITY_ANDROID
using UnityEngine.Networking;
#endif

public static class CrossPlatformFileLoader
{
    private static Dictionary<string, string> fileCache = new Dictionary<string, string>();

    private static HashSet<string> currentlyLoading = new HashSet<string>();

    public static IEnumerator LoadJsonFile(string fileName, System.Action<string> onSuccess, System.Action<string> onError)
    {
        if (fileCache.ContainsKey(fileName))
        {
            onSuccess?.Invoke(fileCache[fileName]);
            yield break;
        }

        if (currentlyLoading.Contains(fileName))
        {
            yield return new WaitUntil(() => !currentlyLoading.Contains(fileName));

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

        currentlyLoading.Add(fileName);

        yield return LoadFromStreamingAssets(fileName,
            (content) =>
            {
                fileCache[fileName] = content;
                currentlyLoading.Remove(fileName);
                onSuccess?.Invoke(content);
            },
            (error) =>
            {
                currentlyLoading.Remove(fileName);
                onError?.Invoke(error);
            }
        );
    }

    private static IEnumerator LoadFromStreamingAssets(string fileName, System.Action<string> onSuccess, System.Action<string> onError)
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, fileName);

#if UNITY_EDITOR
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

#elif UNITY_ANDROID
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

    public static IEnumerator LoadUserDataFile(string fileName, System.Action<string> onSuccess, System.Action<string> onError)
    {
        string persistentPath = Path.Combine(Application.persistentDataPath, fileName);

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

        yield return LoadJsonFile(fileName, onSuccess, onError);
    }

    public static void SaveUserDataFile(string fileName, string jsonContent)
    {
        string persistentPath = Path.Combine(Application.persistentDataPath, fileName);

        try
        {
            File.WriteAllText(persistentPath, jsonContent);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FileLoader] Error saving {fileName}: {e.Message}");
        }
    }

    public static bool IsCached(string fileName)
    {
        return fileCache.ContainsKey(fileName);
    }

    public static string GetCached(string fileName)
    {
        if (fileCache.ContainsKey(fileName))
        {
            return fileCache[fileName];
        }
        return null;
    }

    public static void ClearCache(string fileName)
    {
        if (fileCache.ContainsKey(fileName))
        {
            fileCache.Remove(fileName);
        }
    }

    public static void ClearAllCache()
    {
        fileCache.Clear();
    }

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

    public static IEnumerator PreloadFiles(string[] fileNames, System.Action onComplete = null)
    {

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
                    errorCount++;
                    loaded = true;
                }
            );

            yield return new WaitUntil(() => loaded);
        }
        onComplete?.Invoke();
    }
}