using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using NapoleonicWars.Data;
using NapoleonicWars.Campaign;

namespace NapoleonicWars.Network
{
    /// <summary>
    /// Network manager for multiplayer campaign. Synchronizes campaign state between players.
    /// Supports 2-7 players with simultaneous turns, plus co-op mode (2-3 players per faction).
    /// </summary>
    public class NetworkCampaignManager : NetworkBehaviour
    {
        public static NetworkCampaignManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private float turnTimeLimit = 120f; // 2 minutes per turn

        // Network variables
        private NetworkVariable<int> currentTurn = new NetworkVariable<int>(1);
        private NetworkVariable<int> playersReady = new NetworkVariable<int>(0);
        private NetworkVariable<float> turnTimer = new NetworkVariable<float>(0f);
        private NetworkVariable<bool> turnInProgress = new NetworkVariable<bool>(false);

        // Player assignments (adversarial mode: 1 player per faction)
        private Dictionary<ulong, FactionType> playerFactions = new Dictionary<ulong, FactionType>();
        private Dictionary<FactionType, ulong> factionPlayers = new Dictionary<FactionType, ulong>();
        private HashSet<ulong> readyPlayers = new HashSet<ulong>();

        // Co-op mode: multiple players per faction (tracked via CoopRoleManager)
        private Dictionary<FactionType, HashSet<ulong>> factionCoopPlayers = new Dictionary<FactionType, HashSet<ulong>>();

        // Pending actions (queued during turn, executed at turn end)
        private List<NetworkCampaignAction> pendingActions = new List<NetworkCampaignAction>();

        public bool IsServerProcessingActions { get; private set; } = false;

        // Events
        public delegate void TurnEvent(int turn);
        public event TurnEvent OnTurnStart;
        public event TurnEvent OnTurnEnd;
        public event System.Action OnAllPlayersReady;
        public event System.Action<string, string, string> OnArmyMoved; // armyId, fromProvinceId, toProvinceId

        public int CurrentTurn => currentTurn.Value;
        public float TurnTimeRemaining => Mathf.Max(0f, turnTimeLimit - turnTimer.Value);
        public bool IsTurnInProgress => turnInProgress.Value;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                currentTurn.Value = 1;
                playersReady.Value = 0;
                turnInProgress.Value = false;
            }

            currentTurn.OnValueChanged += OnTurnChanged;
        }

        private void Update()
        {
            if (!IsServer) return;
            if (!turnInProgress.Value) return;

            turnTimer.Value += Time.deltaTime;

            // Auto-end turn if time runs out
            if (turnTimer.Value >= turnTimeLimit)
            {
                ForceEndTurn();
            }
        }

        #region Player Management

        /// <summary>
        /// Assign a faction to a player (adversarial mode: 1 player per faction)
        /// In co-op mode, multiple players join the same faction via CoopRoleManager.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestFactionServerRpc(FactionType faction, ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            // Co-op mode: allow multiple players on same faction
            if (CoopRoleManager.Instance != null && CoopRoleManager.Instance.IsCoopMode)
            {
                if (faction == CoopRoleManager.Instance.CoopFaction)
                {
                    // Register as co-op player for this faction
                    playerFactions[clientId] = faction;
                    if (!factionCoopPlayers.ContainsKey(faction))
                        factionCoopPlayers[faction] = new HashSet<ulong>();
                    factionCoopPlayers[faction].Add(clientId);
                    factionPlayers[faction] = clientId; // Last joiner is primary (for compat)

                    NotifyFactionAssignedClientRpc(clientId, faction);
                    Debug.Log($"[NetworkCampaign] Co-op player {clientId} joined faction {faction}");
                    return;
                }
            }

            // Adversarial mode: check if faction is available
            if (factionPlayers.ContainsKey(faction))
            {
                NotifyFactionUnavailableClientRpc(faction, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
                });
                return;
            }

            // Assign faction
            playerFactions[clientId] = faction;
            factionPlayers[faction] = clientId;

            // Notify all clients
            NotifyFactionAssignedClientRpc(clientId, faction);

            Debug.Log($"[NetworkCampaign] Player {clientId} assigned to {faction}");
        }

        [ClientRpc]
        private void NotifyFactionAssignedClientRpc(ulong clientId, FactionType faction)
        {
            Debug.Log($"[NetworkCampaign] Player {clientId} is now playing as {faction}");
        }

        [ClientRpc]
        private void NotifyFactionUnavailableClientRpc(FactionType faction, ClientRpcParams rpcParams = default)
        {
            Debug.Log($"[NetworkCampaign] Faction {faction} is not available");
        }

        public FactionType? GetPlayerFaction(ulong clientId)
        {
            return playerFactions.TryGetValue(clientId, out var faction) ? faction : null;
        }

        public bool IsPlayerFaction(FactionType faction)
        {
            return factionPlayers.ContainsKey(faction);
        }

        #endregion

        #region Turn System

        /// <summary>
        /// Start a new turn (server only)
        /// </summary>
        public void StartTurn()
        {
            if (!IsServer) return;

            turnInProgress.Value = true;
            turnTimer.Value = 0f;
            readyPlayers.Clear();
            playersReady.Value = 0;

            // Reset co-op ready states
            if (CoopRoleManager.Instance != null && CoopRoleManager.Instance.IsCoopMode)
                CoopRoleManager.Instance.ResetAllReady();

            // Notify all clients
            StartTurnClientRpc(currentTurn.Value);

            Debug.Log($"[NetworkCampaign] Turn {currentTurn.Value} started");
        }

        [ClientRpc]
        private void StartTurnClientRpc(int turn)
        {
            OnTurnStart?.Invoke(turn);
        }

        /// <summary>
        /// Player signals they're done with their turn.
        /// In co-op mode, all co-op players must confirm before the turn ends.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void EndTurnServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            if (readyPlayers.Contains(clientId))
                return;

            readyPlayers.Add(clientId);
            playersReady.Value = readyPlayers.Count;

            // In co-op mode, also update CoopRoleManager ready state
            if (CoopRoleManager.Instance != null && CoopRoleManager.Instance.IsCoopMode)
            {
                CoopRoleManager.Instance.SetReadyServerRpc(true, rpcParams);
            }

            Debug.Log($"[NetworkCampaign] Player {clientId} ended turn ({playersReady.Value}/{playerFactions.Count})");

            // Check if all players are ready
            bool allReady;
            if (CoopRoleManager.Instance != null && CoopRoleManager.Instance.IsCoopMode)
                allReady = CoopRoleManager.Instance.AreAllPlayersReady();
            else
                allReady = readyPlayers.Count >= playerFactions.Count;

            if (allReady)
            {
                OnAllPlayersReady?.Invoke();
                ProcessTurnEnd();
            }
        }

        private void ForceEndTurn()
        {
            Debug.Log("[NetworkCampaign] Turn time expired, forcing turn end");
            ProcessTurnEnd();
        }

        private void ProcessTurnEnd()
        {
            if (!IsServer) return;

            turnInProgress.Value = false;

            // Process all pending actions
            ProcessPendingActions();

            // Process AI factions
            ProcessAIFactions();

            // Advance turn
            currentTurn.Value++;

            // Notify clients
            EndTurnClientRpc(currentTurn.Value - 1);

            // Start next turn after delay
            Invoke(nameof(StartTurn), 2f);
        }

        [ClientRpc]
        private void EndTurnClientRpc(int turn)
        {
            OnTurnEnd?.Invoke(turn);
        }

        private void OnTurnChanged(int oldValue, int newValue)
        {
            Debug.Log($"[NetworkCampaign] Turn changed: {oldValue} -> {newValue}");
        }

        #endregion

        #region Action Queue

        /// <summary>
        /// Queue a campaign action to be executed at turn end.
        /// In co-op mode, validates that the player's role has permission for the action.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void QueueActionServerRpc(NetworkCampaignAction action, ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            // Verify player owns the faction
            if (!playerFactions.TryGetValue(clientId, out var faction))
                return;

            if (action.faction != faction)
                return;

            // Co-op permission check
            if (CoopRoleManager.Instance != null && CoopRoleManager.Instance.IsCoopMode)
            {
                CoopPermission? required = GetRequiredPermission(action.actionType);
                if (required.HasValue && !CoopRoleManager.Instance.HasPermission(clientId, required.Value))
                {
                    RejectActionClientRpc(action.actionType, "Action hors de votre domaine.",
                        new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
                    return;
                }
            }

            pendingActions.Add(action);
            Debug.Log($"[NetworkCampaign] Action queued: {action.actionType} from {faction}");
        }

        [ClientRpc]
        private void RejectActionClientRpc(CampaignActionType actionType, string reason, ClientRpcParams rpcParams = default)
        {
            Debug.LogWarning($"[NetworkCampaign] Action {actionType} rejected: {reason}");
        }

        /// <summary>Map action types to required co-op permissions</summary>
        private CoopPermission? GetRequiredPermission(CampaignActionType actionType)
        {
            return actionType switch
            {
                CampaignActionType.MoveArmy => CoopPermission.MoveArmy,
                CampaignActionType.AttackProvince => CoopPermission.AttackProvince,
                CampaignActionType.RecruitUnit => CoopPermission.RecruitUnits,
                CampaignActionType.BuildBuilding => CoopPermission.ManageConstruction,
                CampaignActionType.DeclareWar => CoopPermission.DeclareWar,
                CampaignActionType.ProposePeace => CoopPermission.ProposePeace,
                CampaignActionType.ProposeAlliance => CoopPermission.ProposeAlliance,
                CampaignActionType.ChangeProduction => CoopPermission.ManageProduction,
                CampaignActionType.ChangeConstruction => CoopPermission.ManageConstruction,
                CampaignActionType.ChangeResearch => CoopPermission.ManageResearch,
                CampaignActionType.ChangeLaw => CoopPermission.SpendPoliticalPower,
                CampaignActionType.ManageSupply => CoopPermission.ManageSupply,
                _ => null
            };
        }

        private void ProcessPendingActions()
        {
            // Sort actions by priority
            pendingActions.Sort((a, b) => a.priority.CompareTo(b.priority));

            IsServerProcessingActions = true;
            foreach (var action in pendingActions)
            {
                ExecuteAction(action);
            }
            IsServerProcessingActions = false;

            pendingActions.Clear();

            // Sync state to all clients
            SyncCampaignStateClientRpc();
        }

        private void ExecuteAction(NetworkCampaignAction action)
        {
            var cm = CampaignManager.Instance;
            if (cm == null) return;

            switch (action.actionType)
            {
                case CampaignActionType.MoveArmy:
                    string currentProvId = null;
                    if (cm.Armies.TryGetValue(action.sourceId, out var army))
                        currentProvId = army.currentProvinceId;
                        
                    if (cm.MoveArmy(action.sourceId, action.targetId))
                    {
                        OnArmyMoved?.Invoke(action.sourceId, currentProvId, action.targetId);
                    }
                    break;

                case CampaignActionType.AttackProvince:
                    // Handle battle initiation
                    InitiateBattle(action);
                    break;

                case CampaignActionType.BuildBuilding:
                    if (cm.Cities.TryGetValue(action.sourceId, out var city))
                    {
                        city.QueueBuilding((BuildingType)action.param1);
                    }
                    break;

                case CampaignActionType.RecruitUnit:
                    if (cm.Cities.TryGetValue(action.sourceId, out var recruitCity))
                    {
                        recruitCity.QueueUnit((UnitType)action.param1);
                    }
                    break;

                case CampaignActionType.DeclareWar:
                    DiplomacySystem.Instance?.DeclareWar(action.faction, (FactionType)action.param1);
                    break;

                case CampaignActionType.ProposePeace:
                    var terms = new PeaceTerms { goldPayment = action.param1 };
                    DiplomacySystem.Instance?.ProposePeace(action.faction, (FactionType)action.param2, terms);
                    break;

                case CampaignActionType.ProposeAlliance:
                    DiplomacySystem.Instance?.ProposeAlliance(action.faction, (FactionType)action.param1, 
                        (AllianceType)action.param2);
                    break;
            }
        }

        private void InitiateBattle(NetworkCampaignAction action)
        {
            // Check if defender is a player
            var cm = CampaignManager.Instance;
            if (!cm.Provinces.TryGetValue(action.targetId, out var province))
                return;

            if (IsPlayerFaction(province.owner))
            {
                // PvP battle - notify both players
                NotifyBattleClientRpc(action.faction, province.owner, action.sourceId, action.targetId);
            }
            else
            {
                // PvE battle - just the attacker
                ulong attackerClient = factionPlayers[action.faction];
                NotifyBattleClientRpc(action.faction, province.owner, action.sourceId, action.targetId,
                    new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { attackerClient } } });
            }
        }

        [ClientRpc]
        private void NotifyBattleClientRpc(FactionType attacker, FactionType defender, 
            string armyId, string provinceId, ClientRpcParams rpcParams = default)
        {
            Debug.Log($"[NetworkCampaign] Battle: {attacker} attacks {defender} in {provinceId}");
            // UI would show battle notification here
        }

        [ClientRpc]
        private void SyncCampaignStateClientRpc()
        {
            // The state is already synced via the executed actions, but we could do a full
            // validation pass here if we had desync issues.
            Debug.Log("[NetworkCampaign] Campaign state synced after turn resolution");
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestFullStateSyncServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!IsServer) return;

            // Generate a temporary save to serialize the current state
            string tempSaveName = "multiplayer_sync_" + System.Guid.NewGuid().ToString();
            SaveSystem.SaveCampaign(tempSaveName);
            
            // Read the JSON
            string filePath = System.IO.Path.Combine(SaveSystem.SaveDirectory, $"{tempSaveName}.json");
            if (System.IO.File.Exists(filePath))
            {
                string json = System.IO.File.ReadAllText(filePath);
                
                // Delete temp file
                System.IO.File.Delete(filePath);
                
                // Send to the requesting client
                ReceiveFullStateSyncClientRpc(json, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
                });
            }
        }

        [ClientRpc]
        private void ReceiveFullStateSyncClientRpc(string jsonState, ClientRpcParams rpcParams = default)
        {
            if (IsServer) return; // Server already has the state

            Debug.Log("[NetworkCampaign] Received full state sync from server. Applying...");
            
            // Save the received JSON to a temporary file
            string tempSaveName = "client_sync_received";
            string filePath = System.IO.Path.Combine(SaveSystem.SaveDirectory, $"{tempSaveName}.json");
            System.IO.Directory.CreateDirectory(SaveSystem.SaveDirectory);
            System.IO.File.WriteAllText(filePath, jsonState);
            
            // Load the state into the CampaignManager
            SaveSystem.LoadCampaign(tempSaveName);
            
            // Refresh visual representation
            var map3D = FindAnyObjectByType<CampaignMap3D>();
            if (map3D != null)
            {
                map3D.SendMessage("RefreshMap", SendMessageOptions.DontRequireReceiver);
            }
        }

        #endregion

        #region AI Factions

        private void ProcessAIFactions()
        {
            var cm = CampaignManager.Instance;
            if (cm == null) return;

            foreach (var faction in cm.Factions.Keys)
            {
                // Skip player-controlled factions
                if (IsPlayerFaction(faction))
                    continue;

                // Process AI turn
                AI.CampaignAI.Instance?.ProcessFactionTurn(faction);
            }
        }

        #endregion

        #region Diplomacy Sync

        /// <summary>
        /// Sync a diplomatic action to all clients
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void SyncDiplomacyServerRpc(FactionType from, FactionType to, int actionType)
        {
            SyncDiplomacyClientRpc(from, to, actionType);
        }

        [ClientRpc]
        private void SyncDiplomacyClientRpc(FactionType from, FactionType to, int actionType)
        {
            Debug.Log($"[NetworkCampaign] Diplomacy sync: {from} -> {to}, action {actionType}");
        }

        #endregion

        #region Chat

        [ServerRpc(RequireOwnership = false)]
        public void SendChatMessageServerRpc(string message, bool isGlobal, int targetFactionInt = -1,
            ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            FactionType? senderFaction = GetPlayerFaction(senderId);

            if (!senderFaction.HasValue) return;

            if (isGlobal)
            {
                // Send to all
                ReceiveChatMessageClientRpc(senderFaction.Value, message, true);
            }
            else if (targetFactionInt >= 0)
            {
                FactionType targetFaction = (FactionType)targetFactionInt;
                if (factionPlayers.TryGetValue(targetFaction, out ulong targetClient))
                {
                    // Send to specific player
                    ReceiveChatMessageClientRpc(senderFaction.Value, message, false,
                        new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { senderId, targetClient } } });
                }
            }
        }

        [ClientRpc]
        private void ReceiveChatMessageClientRpc(FactionType sender, string message, bool isGlobal,
            ClientRpcParams rpcParams = default)
        {
            string prefix = isGlobal ? "[Global]" : "[Private]";
            Debug.Log($"{prefix} {sender}: {message}");
            // UI would display this message
        }

        #endregion

        #region Save/Load

        [ServerRpc(RequireOwnership = false)]
        public void RequestSaveServerRpc()
        {
            if (!IsServer) return;

            // Save campaign state
            SaveSystem.SaveCampaign("multiplayer_save");
            
            NotifySaveCompleteClientRpc();
        }

        [ClientRpc]
        private void NotifySaveCompleteClientRpc()
        {
            Debug.Log("[NetworkCampaign] Game saved");
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestLoadServerRpc(string saveName)
        {
            if (!IsServer) return;

            // Load campaign state
            SaveSystem.LoadCampaign(saveName);

            // Sync to all clients
            SyncCampaignStateClientRpc();
        }

        #endregion
    }

    /// <summary>
    /// Types of campaign actions that can be queued
    /// </summary>
    public enum CampaignActionType
    {
        MoveArmy,
        AttackProvince,
        BuildBuilding,
        RecruitUnit,
        DeclareWar,
        ProposePeace,
        ProposeAlliance,
        MoveFleet,
        StartSiege,
        // Co-op action types
        ChangeProduction,
        ChangeConstruction,
        ChangeResearch,
        ChangeLaw,
        ManageSupply,
        AssignGeneral,
        DesignDivision
    }

    /// <summary>
    /// Serializable campaign action for network transmission
    /// </summary>
    [System.Serializable]
    public struct NetworkCampaignAction : INetworkSerializable
    {
        public FactionType faction;
        public CampaignActionType actionType;
        public string sourceId;
        public string targetId;
        public int param1;
        public int param2;
        public int priority;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref faction);
            serializer.SerializeValue(ref actionType);
            serializer.SerializeValue(ref sourceId);
            serializer.SerializeValue(ref targetId);
            serializer.SerializeValue(ref param1);
            serializer.SerializeValue(ref param2);
            serializer.SerializeValue(ref priority);
        }
    }
}
