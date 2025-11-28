using UnityEngine;
using Mapbox.Unity.Map;
using Mapbox.Utils;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

public class MapboxOfflineManager : MonoBehaviour
{
    [Header("Map Reference")]
    public AbstractMap map;
    
    [Header("Current Map Being Cached")]
    public string currentMapId = "";
    public Vector2d currentMapCenter = new Vector2d(0, 0);
    
    [Header("Caching Settings")]
    public float radiusInKm = 2.0f;
    public int minZoomLevel = 15;
    public int maxZoomLevel = 18;
    public int gridSize = 5;
    public float cachingSpeed = 0.5f;
    
    [Header("Status")]
    public bool isCaching = false;
    public float cachingProgress = 0f;
    
    public System.Action<float> OnCacheProgress;
    public System.Action OnCacheComplete;
    public System.Action<string> OnCacheError;
    
    void Start()
    {
    }
    
    public void StartCachingMap(string mapId, Vector2d mapCenter)
    {
        if (isCaching)
        {
            return;
        }
        
        if (map == null)
        {
            OnCacheError?.Invoke("Map not initialized");
            return;
        }
        
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            OnCacheError?.Invoke("No internet connection");
            return;
        }
        
        currentMapId = mapId;
        currentMapCenter = mapCenter;
        
        StartCoroutine(CacheTilesCoroutine());
    }
    
    private IEnumerator CacheTilesCoroutine()
    {
        isCaching = true;
        cachingProgress = 0f;
        
        Vector2d originalCenter = map.CenterLatitudeLongitude;
        float originalZoom = map.Zoom;
        
        Vector2d[] cachePoints = GenerateCacheGrid();
        int totalPoints = cachePoints.Length * (maxZoomLevel - minZoomLevel + 1);
        int currentPoint = 0;
        
        for (int zoom = minZoomLevel; zoom <= maxZoomLevel; zoom++)
        {
            map.SetZoom(zoom);
            yield return new WaitForSeconds(0.3f);
            
            foreach (Vector2d point in cachePoints)
            {
                map.UpdateMap(point, map.Zoom);
                
                yield return new WaitForSeconds(cachingSpeed);
                
                currentPoint++;
                cachingProgress = (float)currentPoint / totalPoints;
                OnCacheProgress?.Invoke(cachingProgress);
            }
        }
        
        map.UpdateMap(originalCenter, originalZoom);
        yield return new WaitForSeconds(0.3f);
        
        PlayerPrefs.SetInt($"MapCache_{currentMapId}_Complete", 1);
        PlayerPrefs.SetString($"MapCache_{currentMapId}_Date", DateTime.UtcNow.ToString("o"));
        PlayerPrefs.SetString($"MapCache_{currentMapId}_Center", $"{currentMapCenter.x},{currentMapCenter.y}");
        PlayerPrefs.SetFloat($"MapCache_{currentMapId}_Radius", radiusInKm);
        
        SaveCachedMapIdToPlayerPrefs(currentMapId);
        
        PlayerPrefs.Save();
        
        isCaching = false;
        cachingProgress = 1f;
        
        OnCacheComplete?.Invoke();
    }
    
    private Vector2d[] GenerateCacheGrid()
    {
        Vector2d[] points = new Vector2d[gridSize * gridSize];
        int index = 0;
        
        float stepKm = (radiusInKm * 2) / (gridSize - 1);
        float stepDegrees = stepKm / 111.32f;
        
        double startLat = currentMapCenter.x + (radiusInKm / 111.32f);
        double startLng = currentMapCenter.y - (radiusInKm / 111.32f);
        
        for (int row = 0; row < gridSize; row++)
        {
            for (int col = 0; col < gridSize; col++)
            {
                double lat = startLat - (row * stepDegrees);
                double lng = startLng + (col * stepDegrees);
                
                points[index] = new Vector2d(lat, lng);
                index++;
            }
        }
        
        return points;
    }
    
    public bool HasCachedTiles(string mapId)
    {
        return PlayerPrefs.GetInt($"MapCache_{mapId}_Complete", 0) == 1;
    }
    
    public bool HasAnyCachedTiles()
    {
        List<string> cachedMapIds = GetAllCachedMapIds();
        return cachedMapIds.Count > 0;
    }
    
    public bool IsCacheStale(string mapId, int maxAgeDays = 30)
    {
        if (!HasCachedTiles(mapId))
            return true;
        
        string cacheDateStr = PlayerPrefs.GetString($"MapCache_{mapId}_Date", "");
        if (string.IsNullOrEmpty(cacheDateStr))
            return true;
        
        try
        {
            DateTime cacheDate = DateTime.Parse(cacheDateStr);
            TimeSpan age = DateTime.UtcNow - cacheDate;
            return age.TotalDays > maxAgeDays;
        }
        catch
        {
            return true;
        }
    }
    
    public string GetCacheInfo(string mapId)
    {
        if (!HasCachedTiles(mapId))
            return "Not downloaded";
        
        string dateStr = PlayerPrefs.GetString($"MapCache_{mapId}_Date", "Unknown");
        try
        {
            DateTime cacheDate = DateTime.Parse(dateStr);
            int daysOld = (DateTime.UtcNow - cacheDate).Days;
            
            if (daysOld == 0)
                return "Downloaded today";
            else if (daysOld == 1)
                return "Downloaded yesterday";
            else
                return $"Downloaded {daysOld} days ago";
        }
        catch
        {
            return "Downloaded (date unknown)";
        }
    }
    
    public List<string> GetAllCachedMapIds()
    {
        List<string> cachedMaps = new List<string>();
        
        if (FirestoreManager.Instance != null && FirestoreManager.Instance.AvailableMaps != null)
        {
            foreach (MapInfo map in FirestoreManager.Instance.AvailableMaps)
            {
                if (HasCachedTiles(map.map_id))
                {
                    cachedMaps.Add(map.map_id);
                }
            }
            return cachedMaps;
        }
        
        if (JSONFileManager.Instance != null)
        {
            string mapsJson = JSONFileManager.Instance.ReadJSONFile("maps.json");
            if (!string.IsNullOrEmpty(mapsJson))
            {
                try
                {
                    var mapsArray = JsonConvert.DeserializeObject<List<MapInfo>>(mapsJson);
                    foreach (MapInfo map in mapsArray)
                    {
                        if (HasCachedTiles(map.map_id))
                        {
                            cachedMaps.Add(map.map_id);
                        }
                    }
                    return cachedMaps;
                }
                catch (Exception)
                {
                }
            }
        }
        
        string cachedMapIdsList = PlayerPrefs.GetString("CachedMapIds", "");
        if (!string.IsNullOrEmpty(cachedMapIdsList))
        {
            string[] mapIds = cachedMapIdsList.Split(',');
            foreach (string mapId in mapIds)
            {
                if (!string.IsNullOrEmpty(mapId) && HasCachedTiles(mapId))
                {
                    cachedMaps.Add(mapId);
                }
            }
        }
        
        return cachedMaps;
    }
    
    private void SaveCachedMapIdToPlayerPrefs(string mapId)
    {
        string cachedMapIdsList = PlayerPrefs.GetString("CachedMapIds", "");
        List<string> mapIds = new List<string>();
        
        if (!string.IsNullOrEmpty(cachedMapIdsList))
        {
            mapIds = cachedMapIdsList.Split(',').ToList();
        }
        
        if (!mapIds.Contains(mapId))
        {
            mapIds.Add(mapId);
            PlayerPrefs.SetString("CachedMapIds", string.Join(",", mapIds));
            PlayerPrefs.Save();
        }
    }
    
    public void ClearCacheMetadata(string mapId)
    {
        PlayerPrefs.DeleteKey($"MapCache_{mapId}_Complete");
        PlayerPrefs.DeleteKey($"MapCache_{mapId}_Date");
        PlayerPrefs.DeleteKey($"MapCache_{mapId}_Center");
        PlayerPrefs.DeleteKey($"MapCache_{mapId}_Radius");
        
        RemoveCachedMapIdFromPlayerPrefs(mapId);
        
        PlayerPrefs.Save();
    }
    
    public void ClearAllCacheMetadata()
    {
        List<string> cachedMaps = GetAllCachedMapIds();
        
        foreach (string mapId in cachedMaps)
        {
            ClearCacheMetadata(mapId);
        }
        
        PlayerPrefs.DeleteKey("CachedMapIds");
        PlayerPrefs.Save();
    }
    
    private void RemoveCachedMapIdFromPlayerPrefs(string mapId)
    {
        string cachedMapIdsList = PlayerPrefs.GetString("CachedMapIds", "");
        if (!string.IsNullOrEmpty(cachedMapIdsList))
        {
            List<string> mapIds = cachedMapIdsList.Split(',').ToList();
            mapIds.Remove(mapId);
            
            if (mapIds.Count > 0)
            {
                PlayerPrefs.SetString("CachedMapIds", string.Join(",", mapIds));
            }
            else
            {
                PlayerPrefs.DeleteKey("CachedMapIds");
            }
            PlayerPrefs.Save();
        }
    }
    
    private double CalculateDistance(Vector2d point1, Vector2d point2)
    {
        double R = 6371;
        
        double lat1 = point1.x * Math.PI / 180;
        double lat2 = point2.x * Math.PI / 180;
        double dLat = (point2.x - point1.x) * Math.PI / 180;
        double dLon = (point2.y - point1.y) * Math.PI / 180;
        
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1) * Math.Cos(lat2) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        
        return R * c;
    }
}