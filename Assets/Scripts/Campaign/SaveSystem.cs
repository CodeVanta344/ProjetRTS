using System.Collections.Generic;
using System.IO;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Save/Load system for campaign state.
    /// Serializes all campaign data to JSON files.
    /// Save files stored in Application.persistentDataPath/Saves/
    /// </summary>
    public static class SaveSystem
    {
        public static string SaveDirectory => Path.Combine(Application.persistentDataPath, "Saves");

        [System.Serializable]
        public class CampaignSaveData
        {
            public int currentTurn;
            public string playerFaction;
            public List<FactionSaveData> factions = new List<FactionSaveData>();
            public List<ProvinceSaveData> provinces = new List<ProvinceSaveData>();
            public List<ArmySaveData> armies = new List<ArmySaveData>();
            public List<string> researchedTechs = new List<string>();
            public string currentResearchId;
            public int currentResearchTurnsLeft;
            public string saveDate;
            public int saveVersion = 1;
        }

        [System.Serializable]
        public class FactionSaveData
        {
            public string factionType;
            public string factionName;
            public float gold;
            public float food;
            public float iron;
            public bool isEliminated;
            public List<string> ownedProvinceIds = new List<string>();
            public List<string> armyIds = new List<string>();
            public List<DiplomacySaveData> relations = new List<DiplomacySaveData>();
            public List<string> researchedTechs = new List<string>();
            public string currentResearchId;
        }

        [System.Serializable]
        public class DiplomacySaveData
        {
            public string targetFaction;
            public string state;
            public int relationScore;
            public int turnsAtWar;
            public int turnsOfAlliance;
        }

        [System.Serializable]
        public class ProvinceSaveData
        {
            public string id;
            public string name;
            public string owner;
            public List<BuildingSaveData> buildings = new List<BuildingSaveData>();
        }

        [System.Serializable]
        public class BuildingSaveData
        {
            public string type;
            public int level;
            public bool isConstructing;
            public int turnsToComplete;
        }

        [System.Serializable]
        public class ArmySaveData
        {
            public string armyId;
            public string armyName;
            public string faction;
            public string currentProvinceId;
            public int movementPoints;
            public List<RegimentSaveData> regiments = new List<RegimentSaveData>();
        }

        [System.Serializable]
        public class RegimentSaveData
        {
            public string name;
            public string unitType;
            public int maxSize;
            public int currentSize;
            public float experience;
        }

        // ============================================================
        // SAVE
        // ============================================================

        public static bool Save(CampaignManager cm, string slotName = "autosave")
        {
            if (cm == null) return false;

            try
            {
                Directory.CreateDirectory(SaveDirectory);

                CampaignSaveData data = new CampaignSaveData
                {
                    currentTurn = cm.CurrentTurn,
                    playerFaction = cm.PlayerFaction.ToString(),
                    saveDate = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                // Save factions
                foreach (var kvp in cm.Factions)
                {
                    FactionSaveData fd = new FactionSaveData
                    {
                        factionType = kvp.Key.ToString(),
                        factionName = kvp.Value.factionName,
                        gold = kvp.Value.gold,
                        food = kvp.Value.food,
                        iron = kvp.Value.iron,
                        isEliminated = kvp.Value.isEliminated,
                        ownedProvinceIds = new List<string>(kvp.Value.ownedProvinceIds),
                        armyIds = new List<string>(kvp.Value.armyIds)
                    };

                    // Diplomacy
                    foreach (var rel in kvp.Value.relations)
                    {
                        fd.relations.Add(new DiplomacySaveData
                        {
                            targetFaction = rel.Key.ToString(),
                            state = rel.Value.state.ToString(),
                            relationScore = rel.Value.relationScore,
                            turnsAtWar = rel.Value.turnsAtWar,
                            turnsOfAlliance = rel.Value.turnsOfAlliance
                        });
                    }

                    // Tech tree
                    if (kvp.Value.techTree != null)
                    {
                        foreach (var tech in kvp.Value.techTree.GetResearchedTechs())
                            fd.researchedTechs.Add(tech.id);
                        fd.currentResearchId = kvp.Value.techTree.CurrentResearchId;
                    }

                    data.factions.Add(fd);
                }

                // Save provinces
                foreach (var kvp in cm.Provinces)
                {
                    ProvinceSaveData pd = new ProvinceSaveData
                    {
                        id = kvp.Key,
                        name = kvp.Value.provinceName,
                        owner = kvp.Value.owner.ToString()
                    };

                    foreach (var slot in kvp.Value.buildings)
                    {
                        pd.buildings.Add(new BuildingSaveData
                        {
                            type = slot.type.ToString(),
                            level = slot.level,
                            isConstructing = slot.isConstructing,
                            turnsToComplete = slot.turnsToComplete
                        });
                    }

                    data.provinces.Add(pd);
                }

                // Save armies
                foreach (var kvp in cm.Armies)
                {
                    ArmySaveData ad = new ArmySaveData
                    {
                        armyId = kvp.Value.armyId,
                        armyName = kvp.Value.armyName,
                        faction = kvp.Value.faction.ToString(),
                        currentProvinceId = kvp.Value.currentProvinceId,
                        movementPoints = kvp.Value.movementPoints
                    };

                    foreach (var reg in kvp.Value.regiments)
                    {
                        ad.regiments.Add(new RegimentSaveData
                        {
                            name = reg.regimentName,
                            unitType = reg.unitType.ToString(),
                            maxSize = reg.maxSize,
                            currentSize = reg.currentSize,
                            experience = reg.experience
                        });
                    }

                    data.armies.Add(ad);
                }

                string json = JsonUtility.ToJson(data, true);
                string filePath = Path.Combine(SaveDirectory, $"{slotName}.json");
                File.WriteAllText(filePath, json);

                Debug.Log($"[SaveSystem] Campaign saved to: {filePath}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveSystem] Save failed: {e.Message}");
                return false;
            }
        }

        // ============================================================
        // LOAD
        // ============================================================

        public static CampaignSaveData Load(string slotName = "autosave")
        {
            string filePath = Path.Combine(SaveDirectory, $"{slotName}.json");

            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[SaveSystem] Save file not found: {filePath}");
                return null;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                CampaignSaveData data = JsonUtility.FromJson<CampaignSaveData>(json);
                Debug.Log($"[SaveSystem] Loaded save: Turn {data.currentTurn}, {data.saveDate}");
                return data;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveSystem] Load failed: {e.Message}");
                return null;
            }
        }

        // ============================================================
        // UTILITIES
        // ============================================================

        public static bool SaveExists(string slotName = "autosave")
        {
            return File.Exists(Path.Combine(SaveDirectory, $"{slotName}.json"));
        }

        public static void DeleteSave(string slotName)
        {
            string filePath = Path.Combine(SaveDirectory, $"{slotName}.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Debug.Log($"[SaveSystem] Deleted save: {slotName}");
            }
        }

        public static string[] GetSaveSlots()
        {
            if (!Directory.Exists(SaveDirectory))
                return new string[0];

            string[] files = Directory.GetFiles(SaveDirectory, "*.json");
            string[] slots = new string[files.Length];
            for (int i = 0; i < files.Length; i++)
                slots[i] = Path.GetFileNameWithoutExtension(files[i]);
            return slots;
        }

        public static string GetSaveInfo(string slotName)
        {
            CampaignSaveData data = Load(slotName);
            if (data == null) return "Empty";
            return $"Turn {data.currentTurn} | {data.playerFaction} | {data.saveDate}";
        }

        /// <summary>
        /// Parse a FactionType from string. Returns France as default.
        /// </summary>
        public static FactionType ParseFaction(string s)
        {
            if (System.Enum.TryParse<FactionType>(s, out FactionType result))
                return result;
            return FactionType.France;
        }

        public static DiplomacyState ParseDiplomacy(string s)
        {
            if (System.Enum.TryParse<DiplomacyState>(s, out DiplomacyState result))
                return result;
            return DiplomacyState.Neutral;
        }

        public static UnitType ParseUnitType(string s)
        {
            if (System.Enum.TryParse<UnitType>(s, out UnitType result))
                return result;
            return UnitType.LineInfantry;
        }

        public static BuildingType ParseBuildingType(string s)
        {
            if (System.Enum.TryParse<BuildingType>(s, out BuildingType result))
                return result;
            return BuildingType.Empty;
        }

        // Convenience methods for NetworkCampaignManager
        public static void SaveCampaign(string slotName)
        {
            Save(CampaignManager.Instance, slotName);
        }

        public static void LoadCampaign(string slotName)
        {
            CampaignManager.Instance?.LoadCampaign(slotName);
        }
    }
}
