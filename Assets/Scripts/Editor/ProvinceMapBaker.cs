using UnityEngine;
using UnityEditor;
using System.IO;
using NapoleonicWars.Campaign;

namespace NapoleonicWars.EditorTools
{
    public class ProvinceMapBaker : MonoBehaviour
    {
        [MenuItem("Napoleonic Wars/Bake Voronoi Province Map")]
        public static void BakeProvinceMap()
        {
            // Find CampaignManager or create a dummy to get province data
            GameObject cmGO = new GameObject("TempCM");
            CampaignManager cm = cmGO.AddComponent<CampaignManager>();
            
            // Use reflection or standard init to get provinces
            var method = typeof(CampaignManager).GetMethod("CreateMap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(cm, null);
            }
            
            var provinces = cm.Provinces;
            int count = provinces.Count;
            Vector2[] positions = new Vector2[count];
            string[] ids = new string[count];
            
            int idx = 0;
            foreach (var kvp in provinces)
            {
                positions[idx] = kvp.Value.mapPosition;
                ids[idx] = kvp.Key;
                idx++;
            }
            
            int res = 2048;
            Texture2D tex = new Texture2D(res, res, TextureFormat.R8, false);
            Color32[] pixels = new Color32[res * res];
            
            for (int y = 0; y < res; y++)
            {
                float nz = (float)y / res;
                for (int x = 0; x < res; x++)
                {
                    float nx = (float)x / res;
                    
                    // Find nearest
                    int nearest = 0;
                    float minDist = float.MaxValue;
                    
                    for (int i = 0; i < count; i++)
                    {
                        float dist = Vector2.SqrMagnitude(new Vector2(nx, nz) - positions[i]);
                        if (dist < minDist)
                        {
                            minDist = dist;
                            nearest = i;
                        }
                    }
                    
                    // Re-evaluate with noise (simple Voronoi perturbation)
                    nearest = 0;
                    minDist = float.MaxValue;
                    for (int i = 0; i < count; i++)
                    {
                        Vector2 p = positions[i];
                        // Perturb position
                        p.x += Mathf.PerlinNoise(nx * 50f + i, nz * 50f) * 0.02f - 0.01f;
                        p.y += Mathf.PerlinNoise(nx * 50f, nz * 50f + i) * 0.02f - 0.01f;
                        
                        float dist = Vector2.SqrMagnitude(new Vector2(nx, nz) - p);
                        if (dist < minDist)
                        {
                            minDist = dist;
                            nearest = i;
                        }
                    }
                    
                    // Encode index as color (0-255)
                    // Index 0 is reserved for ocean/sea if needed, but here we just map 1 to count
                    byte idByte = (byte)(nearest + 1);
                    pixels[y * res + x] = new Color32(idByte, idByte, idByte, 255);
                }
            }
            
            tex.SetPixels32(pixels);
            tex.Apply();
            
            byte[] bytes = tex.EncodeToPNG();
            string path = "Assets/Resources/Campaign/provinces_id_map.png";
            File.WriteAllBytes(path, bytes);
            
            DestroyImmediate(cmGO);
            AssetDatabase.Refresh();
            
            Debug.Log($"[ProvinceMapBaker] Baked {count} provinces to {path}");
        }
    }
}
