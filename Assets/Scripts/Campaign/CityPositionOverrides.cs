using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Saves and loads city position overrides to a JSON file.
    /// Positions are stored as normalized map coordinates (0-1).
    /// </summary>
    public static class CityPositionOverrides
    {
        [System.Serializable]
        private class PositionEntry
        {
            public string id;
            public float x;
            public float y;
        }
        
        [System.Serializable]
        private class PositionData
        {
            public List<PositionEntry> positions = new List<PositionEntry>();
        }
        
        private static string FilePath => Path.Combine(Application.persistentDataPath, "city_positions.json");
        
        /// <summary>Save a single province position override. Merges with existing data.</summary>
        public static void SavePosition(string provinceId, Vector2 mapPos)
        {
            PositionData data = LoadAllData();
            
            // Update or add entry
            bool found = false;
            for (int i = 0; i < data.positions.Count; i++)
            {
                if (data.positions[i].id == provinceId)
                {
                    data.positions[i].x = mapPos.x;
                    data.positions[i].y = mapPos.y;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                data.positions.Add(new PositionEntry { id = provinceId, x = mapPos.x, y = mapPos.y });
            }
            
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(FilePath, json);
            Debug.Log($"[CityPositions] Saved {provinceId} -> ({mapPos.x:F4}, {mapPos.y:F4}) to {FilePath}");
        }
        
        /// <summary>Apply all saved overrides to provinces in the given dictionary.</summary>
        public static int ApplyOverrides(Dictionary<string, ProvinceData> provinces)
        {
            if (!File.Exists(FilePath)) return 0;
            
            PositionData data = LoadAllData();
            int applied = 0;
            
            foreach (var entry in data.positions)
            {
                if (provinces.TryGetValue(entry.id, out ProvinceData prov))
                {
                    prov.mapPosition = new Vector2(entry.x, entry.y);
                    applied++;
                }
            }
            
            if (applied > 0)
                Debug.Log($"[CityPositions] Applied {applied} position overrides from {FilePath}");
            
            return applied;
        }
        
        private static PositionData LoadAllData()
        {
            if (!File.Exists(FilePath))
                return new PositionData();
            
            try
            {
                string json = File.ReadAllText(FilePath);
                return JsonUtility.FromJson<PositionData>(json) ?? new PositionData();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CityPositions] Failed to read {FilePath}: {e.Message}");
                return new PositionData();
            }
        }
    }
}
