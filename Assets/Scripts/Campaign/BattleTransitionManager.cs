using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using NapoleonicWars.Data;
using NapoleonicWars.Units;
using NapoleonicWars.UI;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Manages transition from campaign map to battle scenes.
    /// Stores battle data including armies and fortification levels.
    /// </summary>
    public class BattleTransitionManager : MonoBehaviour
    {
        public static BattleTransitionManager Instance { get; private set; }
        
        [Header("Battle Data")]
        public ArmyData AttackingArmy { get; private set; }
        public ArmyData DefendingArmy { get; private set; }
        public ProvinceData BattleProvince { get; private set; }
        public CityData BattleCity { get; private set; }
        public int FortificationLevel { get; private set; }
        public bool IsSiegeBattle { get; private set; }
        
        [Header("Scene Names")]
        [SerializeField] private string battleSceneName = "Battle";
        [SerializeField] private string campaignSceneName = "Campaign";
        
        [Header("Battle Settings")]
        [SerializeField] private bool autoTransitionToBattle = true;
        [SerializeField] private float transitionDelay = 0.5f;
        
        // Events
        public System.Action OnBattlePreparing;
        public System.Action OnBattleStarted;
        public System.Action OnBattleEnded;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        
        /// <summary>
        /// Prepare and start a battle between two armies in a province
        /// </summary>
        public void PrepareBattle(ArmyData attacker, ArmyData defender, ProvinceData province, bool immediateTransition = true)
        {
            if (attacker == null)
            {
                Debug.LogError("[BattleTransitionManager] Attacker army is null!");
                return;
            }
            
            AttackingArmy = attacker;
            DefendingArmy = defender;
            BattleProvince = province;
            
            // Get city data for fortification
            if (CampaignManager.Instance != null)
            {
                BattleCity = CampaignManager.Instance.GetCityForProvince(province.provinceId);
            }
            
            // Calculate fortification level
            FortificationLevel = CalculateFortificationLevel();
            IsSiegeBattle = FortificationLevel > 0 && defender != null;
            
            Debug.Log($"[BattleTransitionManager] Battle prepared:");
            Debug.Log($"  Attacker: {attacker.armyName} ({attacker.faction}) - {attacker.TotalSoldiers} soldiers");
            if (defender != null)
                Debug.Log($"  Defender: {defender.armyName} ({defender.faction}) - {defender.TotalSoldiers} soldiers");
            else
                Debug.Log($"  Defender: Garrison/City defense");
            Debug.Log($"  Location: {province.provinceName}");
            Debug.Log($"  Fortification Level: {FortificationLevel}");
            Debug.Log($"  Siege Battle: {IsSiegeBattle}");
            
            OnBattlePreparing?.Invoke();
            
            if (immediateTransition && autoTransitionToBattle)
            {
                Invoke(nameof(StartBattle), transitionDelay);
            }
        }
        
        /// <summary>
        /// Calculate fortification level based on city size and buildings
        /// </summary>
        private int CalculateFortificationLevel()
        {
            int level = 0;
            
            // Base level from city size
            if (BattleCity != null)
            {
                level = BattleCity.GetFortificationLevel();
            }
            else if (BattleProvince != null)
            {
                // Fallback to province-based estimation
                // Small provinces (villages) have no walls
                // Larger settlements have walls
                level = 0; // Default minimal fortification
            }
            
            return Mathf.Clamp(level, 0, 5);
        }
        
        /// <summary>
        /// Start the battle scene
        /// </summary>
        public void StartBattle()
        {
            if (string.IsNullOrEmpty(battleSceneName))
            {
                Debug.LogError("[BattleTransitionManager] Battle scene name not set!");
                return;
            }
            
            // Save battle data to PlayerPrefs for the battle scene to read
            SaveBattleData();
            
            Debug.Log($"[BattleTransitionManager] Starting battle scene: {battleSceneName}");
            LoadingScreenUI.LoadSceneWithScreen(battleSceneName);
            
            OnBattleStarted?.Invoke();
        }
        
        /// <summary>
        /// Return to campaign map after battle
        /// </summary>
        public void EndBattle(BattleResult result)
        {
            // Process battle results
            ProcessBattleResult(result);
            
            // Clear battle data
            ClearBattleData();
            
            // Return to campaign
            if (!string.IsNullOrEmpty(campaignSceneName))
            {
                LoadingScreenUI.LoadSceneWithScreen(campaignSceneName);
            }
            
            OnBattleEnded?.Invoke();
        }
        
        /// <summary>
        /// Save battle data for the battle scene to read
        /// </summary>
        private void SaveBattleData()
        {
            // Store in PlayerPrefs for cross-scene persistence
            PlayerPrefs.SetInt("Battle_FortificationLevel", FortificationLevel);
            PlayerPrefs.SetInt("Battle_IsSiege", IsSiegeBattle ? 1 : 0);
            PlayerPrefs.SetString("Battle_ProvinceId", BattleProvince?.provinceId ?? "");
            PlayerPrefs.SetString("Battle_ProvinceName", BattleProvince?.provinceName ?? "");
            PlayerPrefs.SetInt("Battle_AttackerFaction", (int)(AttackingArmy?.faction ?? FactionType.France));
            PlayerPrefs.SetInt("Battle_DefenderFaction", (int)(DefendingArmy?.faction ?? FactionType.Britain));
            
            // Terrain biome from province
            PlayerPrefs.SetInt("Battle_TerrainType", (int)(BattleProvince?.terrainType ?? ProvinceTerrainType.Plains));
            
            // Season from current turn
            int currentTurn = CampaignManager.Instance != null ? CampaignManager.Instance.CurrentTurn : 0;
            PlayerPrefs.SetString("Battle_Season", DifficultyManager.GetSeason(currentTurn));
            
            // Fortress flag
            bool hasFortress = false;
            if (BattleProvince != null)
            {
                foreach (var slot in BattleProvince.buildings)
                {
                    if (slot.type == BuildingType.Fortress && slot.level > 0) { hasFortress = true; break; }
                }
            }
            PlayerPrefs.SetInt("Battle_HasFortress", hasFortress ? 1 : 0);
            
            // Serialize army compositions with per-regiment rank data
            SaveArmyRegimentData("Attacker", AttackingArmy);
            SaveArmyRegimentData("Defender", DefendingArmy);
            
            PlayerPrefs.Save();
        }
        
        /// <summary>Save per-regiment data (type, rank, size) for battle scene to read.</summary>
        private void SaveArmyRegimentData(string prefix, ArmyData army)
        {
            if (army == null) return;
            
            PlayerPrefs.SetInt($"Battle_{prefix}Regiments", army.regiments.Count);
            PlayerPrefs.SetInt($"Battle_{prefix}Soldiers", army.TotalSoldiers);
            
            for (int i = 0; i < army.regiments.Count; i++)
            {
                var reg = army.regiments[i];
                string key = $"Battle_{prefix}_Reg{i}";
                PlayerPrefs.SetInt($"{key}_Type", (int)reg.unitType);
                PlayerPrefs.SetInt($"{key}_Size", reg.currentSize);
                PlayerPrefs.SetInt($"{key}_Rank", reg.rank);
                PlayerPrefs.SetFloat($"{key}_XP", reg.experience);
                PlayerPrefs.SetString($"{key}_Name", reg.regimentName);
            }
        }
        
        /// <summary>
        /// Process battle results and update campaign state
        /// </summary>
        private void ProcessBattleResult(BattleResult result)
        {
            if (result == null) return;
            
            Debug.Log($"[BattleTransitionManager] Processing battle result: {result.ResultType}");
            
            bool attackerWon = result.ResultType == BattleResultType.AttackerVictory;
            bool defenderWon = result.ResultType == BattleResultType.DefenderVictory;
            
            // Award XP to regiments based on battle outcome
            ProcessRegimentXP(AttackingArmy, attackerWon, result);
            ProcessRegimentXP(DefendingArmy, defenderWon, result);
            
            switch (result.ResultType)
            {
                case BattleResultType.AttackerVictory:
                    // Attacker won - capture province if it was enemy-owned
                    if (BattleProvince != null && AttackingArmy != null)
                    {
                        if (BattleProvince.owner != AttackingArmy.faction)
                        {
                            CampaignManager.Instance?.TransferProvince(
                                BattleProvince.provinceId, 
                                AttackingArmy.faction);
                            Debug.Log($"[BattleTransitionManager] Province {BattleProvince.provinceName} captured!");
                        }
                    }
                    break;
                    
                case BattleResultType.DefenderVictory:
                    // Defender won - attacker retreats or is destroyed
                    if (AttackingArmy != null)
                    {
                        Debug.Log($"[BattleTransitionManager] Attacking army {AttackingArmy.armyName} defeated!");
                    }
                    break;
                    
                case BattleResultType.Draw:
                    Debug.Log($"[BattleTransitionManager] Battle ended in draw");
                    break;
            }
        }
        
        /// <summary>Process XP gains for all regiments in an army after a battle.</summary>
        private void ProcessRegimentXP(ArmyData army, bool won, BattleResult result)
        {
            if (army == null) return;
            
            int totalCasualties = won ? result.AttackerCasualties : result.DefenderCasualties;
            int totalEnemyCasualties = won ? result.DefenderCasualties : result.AttackerCasualties;
            int regCount = army.regiments.Count;
            if (regCount == 0) return;
            
            // Distribute casualties and kills proportionally across regiments
            foreach (var reg in army.regiments)
            {
                float sizeRatio = army.TotalSoldiers > 0 ? (float)reg.currentSize / army.TotalSoldiers : 1f / regCount;
                int regCasualties = Mathf.RoundToInt(totalCasualties * sizeRatio);
                int regKills = Mathf.RoundToInt(totalEnemyCasualties * sizeRatio);
                
                // Cap casualties to regiment size
                regCasualties = Mathf.Min(regCasualties, reg.currentSize - 1); // Keep at least 1
                
                int oldRank = reg.rank;
                reg.ProcessBattleResult(won, regCasualties, regKills);
                
                if (reg.rank > oldRank)
                {
                    Debug.Log($"[BattleTransition] {reg.regimentName} ranked up! Rank {oldRank} \u2192 {reg.rank} ({reg.RankDisplayName})");
                }
            }
            
            // Remove destroyed regiments (0 soldiers)
            army.regiments.RemoveAll(r => r.currentSize <= 0);
        }
        
        /// <summary>
        /// Clear all battle data
        /// </summary>
        private void ClearBattleData()
        {
            AttackingArmy = null;
            DefendingArmy = null;
            BattleProvince = null;
            BattleCity = null;
            FortificationLevel = 0;
            IsSiegeBattle = false;
        }
        
        /// <summary>
        /// Get formatted battle info for UI display
        /// </summary>
        public string GetBattleInfoText()
        {
            string text = $"<b>Battle at {BattleProvince?.provinceName ?? "Unknown"}</b>\n\n";
            
            if (AttackingArmy != null)
            {
                text += $"<color=red>Attacker:</color> {AttackingArmy.armyName}\n";
                text += $"  Faction: {AttackingArmy.faction}\n";
                text += $"  Soldiers: {AttackingArmy.TotalSoldiers}\n";
                text += $"  Regiments: {AttackingArmy.regiments.Count}\n\n";
            }
            
            if (DefendingArmy != null)
            {
                text += $"<color=blue>Defender:</color> {DefendingArmy.armyName}\n";
                text += $"  Faction: {DefendingArmy.faction}\n";
                text += $"  Soldiers: {DefendingArmy.TotalSoldiers}\n";
                text += $"  Regiments: {DefendingArmy.regiments.Count}\n\n";
            }
            else if (FortificationLevel > 0)
            {
                text += $"<color=blue>Defender:</color> Garrison Forces\n";
                text += $"  Fortification: Level {FortificationLevel}\n\n";
            }
            
            if (IsSiegeBattle)
            {
                text += $"<b>Siege Battle</b>\n";
                text += $"Defenders have fortified positions.\n";
                text += $"Walls provide defensive bonuses.\n";
            }
            
            return text;
        }
        
        /// <summary>
        /// Check if we're currently in a battle transition
        /// </summary>
        public bool IsBattleActive => AttackingArmy != null;
    }
    
    /// <summary>
    /// Battle result data
    /// </summary>
    public class BattleResult
    {
        public BattleResultType ResultType;
        public int AttackerCasualties;
        public int DefenderCasualties;
        public float BattleDuration;
        public string BattleLog;
    }
    
    public enum BattleResultType
    {
        AttackerVictory,
        DefenderVictory,
        Draw,
        Retreated
    }
}
