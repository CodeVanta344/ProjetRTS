using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;
using NapoleonicWars.Data;
using NapoleonicWars.Campaign;

namespace NapoleonicWars.Network
{
    // ==================== CO-OP ROLES ====================
    public enum CoopRole
    {
        None,
        Marshal,        // Military: armies, battles, recruitment, divisions
        Intendant,      // Economy & Logistics: production, construction, supply, trade
        Chancellor,     // Politics & Diplomacy: laws, diplomacy, research, espionage
        GrandVizir      // 2-player mode: Intendant + Chancellor combined
    }

    // ==================== PERMISSIONS ====================
    public enum CoopPermission
    {
        // Marshal
        MoveArmy,
        AttackProvince,
        RecruitUnits,
        DesignDivisions,
        AssignGenerals,
        ManageGarrisons,

        // Intendant
        ManageProduction,
        ManageConstruction,
        ManageSupply,
        ChangeTradeLaw,
        AssignFactories,
        ManageTradeRoutes,

        // Chancellor
        ChangeConscriptionLaw,
        ChangeEconomyLaw,
        DeclareWar,
        ProposePeace,
        ProposeAlliance,
        ManageResearch,
        ManageAgents,
        SpendPoliticalPower,

        // Shared (all roles)
        ViewAllPanels,
        SendChat,
        MakeRequest,
        ViewMap
    }

    // ==================== PLAYER CO-OP DATA ====================
    [System.Serializable]
    public struct CoopPlayerData : INetworkSerializable
    {
        public ulong clientId;
        public FactionType faction;
        public CoopRole role;
        public bool isReady;
        public bool isConnected;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref clientId);
            serializer.SerializeValue(ref faction);
            serializer.SerializeValue(ref role);
            serializer.SerializeValue(ref isReady);
            serializer.SerializeValue(ref isConnected);
        }
    }

    // ==================== CO-OP ROLE MANAGER ====================
    /// <summary>
    /// Manages co-op roles, permissions, and validation for multi-player country management.
    /// Server-authoritative: all role assignments and permission checks happen on the server.
    /// </summary>
    public class CoopRoleManager : NetworkBehaviour
    {
        public static CoopRoleManager Instance { get; private set; }

        // === Network state ===
        private NetworkVariable<bool> isCoopMode = new NetworkVariable<bool>(false);
        private NetworkVariable<FactionType> coopFaction = new NetworkVariable<FactionType>(FactionType.France);
        private NetworkVariable<int> maxCoopPlayers = new NetworkVariable<int>(2);
        private NetworkVariable<int> connectedCoopPlayers = new NetworkVariable<int>(0);

        // === Server-side data ===
        private Dictionary<ulong, CoopPlayerData> playerData = new Dictionary<ulong, CoopPlayerData>();
        private Dictionary<CoopRole, ulong> rolePlayers = new Dictionary<CoopRole, ulong>();

        // === Permission tables (static, built once) ===
        private static Dictionary<CoopRole, HashSet<CoopPermission>> rolePermissions;

        // === Events ===
        public delegate void RoleAssigned(ulong clientId, CoopRole role);
        public event RoleAssigned OnRoleAssigned;

        public delegate void CoopStateChanged();
        public event CoopStateChanged OnCoopStateChanged;

        public delegate void PlayerReadyChanged(ulong clientId, bool ready);
        public event PlayerReadyChanged OnPlayerReadyChanged;

        // === Public accessors ===
        public bool IsCoopMode => isCoopMode.Value;
        public FactionType CoopFaction => coopFaction.Value;
        public int MaxCoopPlayers => maxCoopPlayers.Value;
        public int ConnectedCoopPlayers => connectedCoopPlayers.Value;

        // ============================================================
        // LIFECYCLE
        // ============================================================

        static CoopRoleManager()
        {
            BuildPermissionTables();
        }

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
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            }

            isCoopMode.OnValueChanged += (_, _) => OnCoopStateChanged?.Invoke();
            coopFaction.OnValueChanged += (_, _) => OnCoopStateChanged?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        // ============================================================
        // PERMISSION TABLES
        // ============================================================

        private static void BuildPermissionTables()
        {
            rolePermissions = new Dictionary<CoopRole, HashSet<CoopPermission>>();

            // Shared permissions for all roles
            var shared = new HashSet<CoopPermission>
            {
                CoopPermission.ViewAllPanels,
                CoopPermission.SendChat,
                CoopPermission.MakeRequest,
                CoopPermission.ViewMap
            };

            // Marshal
            var marshal = new HashSet<CoopPermission>(shared)
            {
                CoopPermission.MoveArmy,
                CoopPermission.AttackProvince,
                CoopPermission.RecruitUnits,
                CoopPermission.DesignDivisions,
                CoopPermission.AssignGenerals,
                CoopPermission.ManageGarrisons
            };
            rolePermissions[CoopRole.Marshal] = marshal;

            // Intendant
            var intendant = new HashSet<CoopPermission>(shared)
            {
                CoopPermission.ManageProduction,
                CoopPermission.ManageConstruction,
                CoopPermission.ManageSupply,
                CoopPermission.ChangeTradeLaw,
                CoopPermission.AssignFactories,
                CoopPermission.ManageTradeRoutes
            };
            rolePermissions[CoopRole.Intendant] = intendant;

            // Chancellor
            var chancellor = new HashSet<CoopPermission>(shared)
            {
                CoopPermission.ChangeConscriptionLaw,
                CoopPermission.ChangeEconomyLaw,
                CoopPermission.DeclareWar,
                CoopPermission.ProposePeace,
                CoopPermission.ProposeAlliance,
                CoopPermission.ManageResearch,
                CoopPermission.ManageAgents,
                CoopPermission.SpendPoliticalPower
            };
            rolePermissions[CoopRole.Chancellor] = chancellor;

            // GrandVizir = Intendant + Chancellor
            var grandVizir = new HashSet<CoopPermission>(intendant);
            grandVizir.UnionWith(chancellor);
            rolePermissions[CoopRole.GrandVizir] = grandVizir;
        }

        // ============================================================
        // ROLE ASSIGNMENT (Server RPCs)
        // ============================================================

        /// <summary>Host configures co-op mode before starting the campaign</summary>
        public void ConfigureCoopMode(bool enabled, FactionType faction, int maxPlayers)
        {
            if (!IsServer) return;

            isCoopMode.Value = enabled;
            coopFaction.Value = faction;
            maxCoopPlayers.Value = Mathf.Clamp(maxPlayers, 2, 3);

            Debug.Log($"[CoopRole] Co-op mode {(enabled ? "enabled" : "disabled")}: {faction}, max {maxPlayers} players");
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestRoleServerRpc(CoopRole role, ServerRpcParams rpcParams = default)
        {
            if (!isCoopMode.Value) return;

            ulong clientId = rpcParams.Receive.SenderClientId;

            // Validate role availability
            if (role == CoopRole.None)
            {
                RejectRoleClientRpc("Rôle invalide.", CreateTargetRpcParams(clientId));
                return;
            }

            // In 2-player mode, Chancellor and Intendant are not available separately
            if (maxCoopPlayers.Value == 2 && (role == CoopRole.Chancellor || role == CoopRole.Intendant))
            {
                RejectRoleClientRpc("En mode 2 joueurs, utilisez Grand Vizir.", CreateTargetRpcParams(clientId));
                return;
            }

            // In 3-player mode, GrandVizir is not available
            if (maxCoopPlayers.Value == 3 && role == CoopRole.GrandVizir)
            {
                RejectRoleClientRpc("En mode 3 joueurs, choisissez Intendant ou Chancelier.", CreateTargetRpcParams(clientId));
                return;
            }

            // Check if role is already taken
            if (rolePlayers.ContainsKey(role))
            {
                RejectRoleClientRpc($"Le rôle {GetRoleName(role)} est déjà pris.", CreateTargetRpcParams(clientId));
                return;
            }

            // Remove previous role if player had one
            RemovePlayerRole(clientId);

            // Assign role
            rolePlayers[role] = clientId;
            var data = new CoopPlayerData
            {
                clientId = clientId,
                faction = coopFaction.Value,
                role = role,
                isReady = false,
                isConnected = true
            };
            playerData[clientId] = data;
            connectedCoopPlayers.Value = playerData.Count;

            // Notify all clients
            ConfirmRoleClientRpc(clientId, role);
            OnRoleAssigned?.Invoke(clientId, role);

            Debug.Log($"[CoopRole] Player {clientId} assigned role: {GetRoleName(role)}");
        }

        [ClientRpc]
        private void ConfirmRoleClientRpc(ulong clientId, CoopRole role)
        {
            Debug.Log($"[CoopRole] Player {clientId} is now {GetRoleName(role)}");
            OnRoleAssigned?.Invoke(clientId, role);
            OnCoopStateChanged?.Invoke();
        }

        [ClientRpc]
        private void RejectRoleClientRpc(string reason, ClientRpcParams rpcParams = default)
        {
            Debug.LogWarning($"[CoopRole] Role request rejected: {reason}");
        }

        // ============================================================
        // PERMISSION CHECKS
        // ============================================================

        /// <summary>Check if a client has a specific permission. Server-side.</summary>
        public bool HasPermission(ulong clientId, CoopPermission permission)
        {
            // If not in co-op mode, everything is allowed (single-player)
            if (!isCoopMode.Value) return true;

            if (!playerData.TryGetValue(clientId, out var data)) return false;

            CoopRole role = data.role;
            if (role == CoopRole.None) return false;

            return rolePermissions.ContainsKey(role) && rolePermissions[role].Contains(permission);
        }

        /// <summary>Check if the local client has a specific permission. Client-side convenience.</summary>
        public bool LocalHasPermission(CoopPermission permission)
        {
            if (!isCoopMode.Value) return true;
            if (NetworkManager.Singleton == null) return true;
            return HasPermission(NetworkManager.Singleton.LocalClientId, permission);
        }

        /// <summary>Get the role of the local player</summary>
        public CoopRole GetLocalRole()
        {
            if (!isCoopMode.Value) return CoopRole.None;
            if (NetworkManager.Singleton == null) return CoopRole.None;

            ulong localId = NetworkManager.Singleton.LocalClientId;
            return playerData.TryGetValue(localId, out var data) ? data.role : CoopRole.None;
        }

        /// <summary>Get the role of a specific client</summary>
        public CoopRole GetRole(ulong clientId)
        {
            return playerData.TryGetValue(clientId, out var data) ? data.role : CoopRole.None;
        }

        /// <summary>Get all connected co-op players</summary>
        public List<CoopPlayerData> GetAllPlayers()
        {
            return new List<CoopPlayerData>(playerData.Values);
        }

        /// <summary>Get the client ID for a specific role, or 0 if not assigned</summary>
        public ulong GetPlayerForRole(CoopRole role)
        {
            return rolePlayers.TryGetValue(role, out var clientId) ? clientId : 0;
        }

        /// <summary>Check if all co-op players are ready for turn end</summary>
        public bool AreAllPlayersReady()
        {
            if (!isCoopMode.Value) return true;
            if (playerData.Count == 0) return false;
            return playerData.Values.All(p => p.isReady || !p.isConnected);
        }

        /// <summary>Check if a specific role is filled</summary>
        public bool IsRoleFilled(CoopRole role)
        {
            return rolePlayers.ContainsKey(role);
        }

        /// <summary>Check if all required roles are filled</summary>
        public bool AreAllRolesFilled()
        {
            if (!isCoopMode.Value) return true;

            if (maxCoopPlayers.Value == 2)
                return rolePlayers.ContainsKey(CoopRole.Marshal) && rolePlayers.ContainsKey(CoopRole.GrandVizir);
            else
                return rolePlayers.ContainsKey(CoopRole.Marshal) &&
                       rolePlayers.ContainsKey(CoopRole.Intendant) &&
                       rolePlayers.ContainsKey(CoopRole.Chancellor);
        }

        // ============================================================
        // READY STATE (for turn end voting)
        // ============================================================

        [ServerRpc(RequireOwnership = false)]
        public void SetReadyServerRpc(bool ready, ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!playerData.ContainsKey(clientId)) return;

            var data = playerData[clientId];
            data.isReady = ready;
            playerData[clientId] = data;

            SyncReadyStateClientRpc(clientId, ready);
            OnPlayerReadyChanged?.Invoke(clientId, ready);

            Debug.Log($"[CoopRole] Player {clientId} ({GetRoleName(data.role)}) ready: {ready}");
        }

        /// <summary>Reset all ready states (called at start of each turn)</summary>
        public void ResetAllReady()
        {
            if (!IsServer) return;

            var keys = new List<ulong>(playerData.Keys);
            foreach (ulong key in keys)
            {
                var data = playerData[key];
                data.isReady = false;
                playerData[key] = data;
            }

            ResetReadyClientRpc();
        }

        [ClientRpc]
        private void SyncReadyStateClientRpc(ulong clientId, bool ready)
        {
            OnPlayerReadyChanged?.Invoke(clientId, ready);
        }

        [ClientRpc]
        private void ResetReadyClientRpc()
        {
            OnCoopStateChanged?.Invoke();
        }

        // ============================================================
        // DISCONNECTION HANDLING
        // ============================================================

        private void OnClientDisconnected(ulong clientId)
        {
            if (!playerData.ContainsKey(clientId)) return;

            var data = playerData[clientId];
            data.isConnected = false;
            playerData[clientId] = data;
            connectedCoopPlayers.Value = playerData.Values.Count(p => p.isConnected);

            NotifyDisconnectClientRpc(clientId, data.role);

            Debug.Log($"[CoopRole] Player {clientId} ({GetRoleName(data.role)}) disconnected. Autopilot enabled for their domain.");
        }

        /// <summary>Handle reconnection: player reclaims their role</summary>
        [ServerRpc(RequireOwnership = false)]
        public void ReconnectServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            if (playerData.ContainsKey(clientId))
            {
                var data = playerData[clientId];
                data.isConnected = true;
                data.isReady = false;
                playerData[clientId] = data;
                connectedCoopPlayers.Value = playerData.Values.Count(p => p.isConnected);

                NotifyReconnectClientRpc(clientId, data.role);
                Debug.Log($"[CoopRole] Player {clientId} ({GetRoleName(data.role)}) reconnected.");
            }
        }

        [ClientRpc]
        private void NotifyDisconnectClientRpc(ulong clientId, CoopRole role)
        {
            Debug.Log($"[CoopRole] {GetRoleName(role)} disconnected. Their domain is on autopilot.");
            OnCoopStateChanged?.Invoke();
        }

        [ClientRpc]
        private void NotifyReconnectClientRpc(ulong clientId, CoopRole role)
        {
            Debug.Log($"[CoopRole] {GetRoleName(role)} reconnected!");
            OnCoopStateChanged?.Invoke();
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private void RemovePlayerRole(ulong clientId)
        {
            if (!playerData.ContainsKey(clientId)) return;

            CoopRole oldRole = playerData[clientId].role;
            if (oldRole != CoopRole.None && rolePlayers.ContainsKey(oldRole) && rolePlayers[oldRole] == clientId)
            {
                rolePlayers.Remove(oldRole);
            }
            playerData.Remove(clientId);
            connectedCoopPlayers.Value = playerData.Count;
        }

        private ClientRpcParams CreateTargetRpcParams(ulong clientId)
        {
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
            };
        }

        /// <summary>Get the display name for a co-op role</summary>
        public static string GetRoleName(CoopRole role) => role switch
        {
            CoopRole.Marshal => "Maréchal",
            CoopRole.Intendant => "Intendant",
            CoopRole.Chancellor => "Chancelier",
            CoopRole.GrandVizir => "Grand Vizir",
            _ => "Aucun"
        };

        /// <summary>Get the icon emoji for a co-op role</summary>
        public static string GetRoleIcon(CoopRole role) => role switch
        {
            CoopRole.Marshal => "⚔️",
            CoopRole.Intendant => "🏭",
            CoopRole.Chancellor => "⚖️",
            CoopRole.GrandVizir => "👑",
            _ => "❓"
        };

        /// <summary>Get the accent color for a co-op role</summary>
        public static Color GetRoleColor(CoopRole role) => role switch
        {
            CoopRole.Marshal => new Color(0.9f, 0.3f, 0.3f),    // Red
            CoopRole.Intendant => new Color(0.3f, 0.7f, 0.9f),  // Blue
            CoopRole.Chancellor => new Color(0.9f, 0.8f, 0.3f), // Gold
            CoopRole.GrandVizir => new Color(0.7f, 0.4f, 0.9f), // Purple
            _ => Color.white
        };

        /// <summary>Check which NavPanel domains a role can control (write access)</summary>
        public static bool CanControlPanel(CoopRole role, string panelName)
        {
            switch (panelName)
            {
                case "Military":
                    return role == CoopRole.Marshal;
                case "Production":
                case "Construction":
                case "Logistics":
                    return role == CoopRole.Intendant || role == CoopRole.GrandVizir;
                case "Laws":
                case "Research":
                case "Diplomacy":
                    return role == CoopRole.Chancellor || role == CoopRole.GrandVizir;
                default:
                    return true; // Map, etc.
            }
        }
    }
}
