using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;
using NapoleonicWars.Data;

namespace NapoleonicWars.Network
{
    // ==================== REQUEST TYPES ====================
    public enum CoopRequestType
    {
        // Marshal → Intendant
        RequestMoreEquipment,
        RequestPriorityProduction,
        RequestSupplyUpgrade,

        // Marshal → Chancellor
        RequestConscription,
        RequestWarDeclaration,
        RequestResearchMilitary,

        // Intendant → Marshal
        RequestProvinceProtection,
        RequestArmyRetreat,

        // Intendant → Chancellor
        RequestTradeLawChange,
        RequestStabilityFocus,

        // Chancellor → Intendant
        RequestBuildingPriority,
        RequestEconomicFocus,

        // Chancellor → Marshal
        RequestOffensive,
        RequestDefensivePosture,

        // Critical (requires vote)
        VoteCapitulation,
        VoteTotalMobilization,
        VoteWarDeclaration
    }

    public enum CoopRequestStatus
    {
        Pending,
        Accepted,
        Declined,
        Expired
    }

    // ==================== REQUEST DATA ====================
    [System.Serializable]
    public struct CoopRequest : INetworkSerializable
    {
        public int requestId;
        public ulong senderClientId;
        public CoopRole senderRole;
        public CoopRole targetRole;
        public CoopRequestType type;
        public CoopRequestStatus status;
        public int turnCreated;
        public int turnExpires;

        // Context data (serialized as simple types for Netcode)
        public int contextParam1;       // e.g. equipment type, province index
        public int contextParam2;       // e.g. quantity
        public bool isVote;             // Critical decision requiring majority

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref requestId);
            serializer.SerializeValue(ref senderClientId);
            serializer.SerializeValue(ref senderRole);
            serializer.SerializeValue(ref targetRole);
            serializer.SerializeValue(ref type);
            serializer.SerializeValue(ref status);
            serializer.SerializeValue(ref turnCreated);
            serializer.SerializeValue(ref turnExpires);
            serializer.SerializeValue(ref contextParam1);
            serializer.SerializeValue(ref contextParam2);
            serializer.SerializeValue(ref isVote);
        }
    }

    // ==================== CO-OP REQUEST SYSTEM ====================
    /// <summary>
    /// Manages inter-player requests and votes in co-op mode.
    /// Server-authoritative: all requests are validated and routed through the server.
    /// </summary>
    public class CoopRequestSystem : NetworkBehaviour
    {
        public static CoopRequestSystem Instance { get; private set; }

        // All requests (server-side master list, synced to clients)
        private List<CoopRequest> allRequests = new List<CoopRequest>();
        private int nextRequestId = 1;

        // Vote tracking for critical decisions
        private Dictionary<int, HashSet<ulong>> voteAccepts = new Dictionary<int, HashSet<ulong>>();
        private Dictionary<int, HashSet<ulong>> voteDeclines = new Dictionary<int, HashSet<ulong>>();

        // Events (client-side)
        public delegate void RequestReceived(CoopRequest request);
        public event RequestReceived OnRequestReceived;

        public delegate void RequestUpdated(CoopRequest request);
        public event RequestUpdated OnRequestUpdated;

        public delegate void VoteResult(CoopRequest request, bool passed);
        public event VoteResult OnVoteResult;

        // ============================================================
        // LIFECYCLE
        // ============================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        // ============================================================
        // SENDING REQUESTS
        // ============================================================

        /// <summary>Send a request from local player to a target role</summary>
        [ServerRpc(RequireOwnership = false)]
        public void SendRequestServerRpc(CoopRequestType type, CoopRole targetRole,
            int param1 = 0, int param2 = 0, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;

            if (CoopRoleManager.Instance == null || !CoopRoleManager.Instance.IsCoopMode)
                return;

            CoopRole senderRole = CoopRoleManager.Instance.GetRole(senderId);
            if (senderRole == CoopRole.None) return;

            // Validate sender can make this request
            if (!ValidateRequest(senderRole, targetRole, type))
            {
                Debug.LogWarning($"[CoopRequest] Invalid request: {senderRole} cannot send {type} to {targetRole}");
                return;
            }

            int currentTurn = 1;
            if (Campaign.CampaignManager.Instance != null)
                currentTurn = Campaign.CampaignManager.Instance.CurrentTurn;

            bool isVoteType = type == CoopRequestType.VoteCapitulation ||
                              type == CoopRequestType.VoteTotalMobilization ||
                              type == CoopRequestType.VoteWarDeclaration;

            var request = new CoopRequest
            {
                requestId = nextRequestId++,
                senderClientId = senderId,
                senderRole = senderRole,
                targetRole = targetRole,
                type = type,
                status = CoopRequestStatus.Pending,
                turnCreated = currentTurn,
                turnExpires = currentTurn + (isVoteType ? 1 : 2), // Votes expire faster
                contextParam1 = param1,
                contextParam2 = param2,
                isVote = isVoteType
            };

            allRequests.Add(request);

            if (isVoteType)
            {
                voteAccepts[request.requestId] = new HashSet<ulong> { senderId }; // Sender auto-accepts
                voteDeclines[request.requestId] = new HashSet<ulong>();
            }

            // Notify target player(s)
            NotifyRequestClientRpc(request);

            Debug.Log($"[CoopRequest] {senderRole} → {targetRole}: {type} (id={request.requestId})");
        }

        // ============================================================
        // RESPONDING TO REQUESTS
        // ============================================================

        [ServerRpc(RequireOwnership = false)]
        public void RespondToRequestServerRpc(int requestId, bool accept, ServerRpcParams rpcParams = default)
        {
            ulong responderId = rpcParams.Receive.SenderClientId;

            int index = allRequests.FindIndex(r => r.requestId == requestId);
            if (index < 0) return;

            var request = allRequests[index];
            if (request.status != CoopRequestStatus.Pending) return;

            // Validate responder has the right role
            CoopRole responderRole = CoopRoleManager.Instance.GetRole(responderId);
            if (!request.isVote && responderRole != request.targetRole) return;

            if (request.isVote)
            {
                // Vote system: collect votes from all co-op players
                if (accept)
                    voteAccepts[requestId].Add(responderId);
                else
                    voteDeclines[requestId].Add(responderId);

                int totalPlayers = CoopRoleManager.Instance.GetAllPlayers().Count;
                int accepts = voteAccepts[requestId].Count;
                int declines = voteDeclines[requestId].Count;

                if (accepts > totalPlayers / 2)
                {
                    // Vote passed
                    request.status = CoopRequestStatus.Accepted;
                    allRequests[index] = request;
                    NotifyRequestUpdateClientRpc(request);
                    NotifyVoteResultClientRpc(request, true);
                    Debug.Log($"[CoopRequest] Vote {requestId} PASSED ({accepts}/{totalPlayers})");
                }
                else if (declines > totalPlayers / 2)
                {
                    // Vote failed
                    request.status = CoopRequestStatus.Declined;
                    allRequests[index] = request;
                    NotifyRequestUpdateClientRpc(request);
                    NotifyVoteResultClientRpc(request, false);
                    Debug.Log($"[CoopRequest] Vote {requestId} FAILED ({declines}/{totalPlayers})");
                }
                else
                {
                    // Still pending, notify update
                    NotifyVoteProgressClientRpc(requestId, accepts, declines, totalPlayers);
                }
            }
            else
            {
                // Simple accept/decline
                request.status = accept ? CoopRequestStatus.Accepted : CoopRequestStatus.Declined;
                allRequests[index] = request;

                NotifyRequestUpdateClientRpc(request);

                string result = accept ? "ACCEPTED" : "DECLINED";
                Debug.Log($"[CoopRequest] Request {requestId} {result} by {responderRole}");
            }
        }

        // ============================================================
        // TURN PROCESSING
        // ============================================================

        /// <summary>Call at end of each turn to expire old requests</summary>
        public void ProcessTurnExpiry(int currentTurn)
        {
            if (!IsServer) return;

            for (int i = allRequests.Count - 1; i >= 0; i--)
            {
                var req = allRequests[i];
                if (req.status == CoopRequestStatus.Pending && currentTurn >= req.turnExpires)
                {
                    req.status = CoopRequestStatus.Expired;
                    allRequests[i] = req;
                    NotifyRequestUpdateClientRpc(req);
                    Debug.Log($"[CoopRequest] Request {req.requestId} expired");
                }
            }

            // Clean up old resolved requests (keep last 20)
            var resolved = allRequests.Where(r => r.status != CoopRequestStatus.Pending).ToList();
            if (resolved.Count > 20)
            {
                int toRemove = resolved.Count - 20;
                for (int i = 0; i < toRemove; i++)
                    allRequests.Remove(resolved[i]);
            }
        }

        // ============================================================
        // QUERIES
        // ============================================================

        /// <summary>Get all pending requests targeting a specific role</summary>
        public List<CoopRequest> GetPendingRequestsForRole(CoopRole role)
        {
            return allRequests.Where(r =>
                r.status == CoopRequestStatus.Pending &&
                (r.targetRole == role || r.isVote)).ToList();
        }

        /// <summary>Get all requests (for history display)</summary>
        public List<CoopRequest> GetAllRequests()
        {
            return new List<CoopRequest>(allRequests);
        }

        /// <summary>Get pending request count for a role (for badge display)</summary>
        public int GetPendingCount(CoopRole role)
        {
            return allRequests.Count(r =>
                r.status == CoopRequestStatus.Pending &&
                (r.targetRole == role || r.isVote));
        }

        // ============================================================
        // CLIENT RPCS
        // ============================================================

        [ClientRpc]
        private void NotifyRequestClientRpc(CoopRequest request)
        {
            OnRequestReceived?.Invoke(request);
            Debug.Log($"[CoopRequest] New request received: {request.type} from {request.senderRole}");
        }

        [ClientRpc]
        private void NotifyRequestUpdateClientRpc(CoopRequest request)
        {
            // Update local cache
            int index = allRequests.FindIndex(r => r.requestId == request.requestId);
            if (index >= 0)
                allRequests[index] = request;
            else
                allRequests.Add(request);

            OnRequestUpdated?.Invoke(request);
        }

        [ClientRpc]
        private void NotifyVoteResultClientRpc(CoopRequest request, bool passed)
        {
            OnVoteResult?.Invoke(request, passed);
            string result = passed ? "ADOPTÉ" : "REJETÉ";
            Debug.Log($"[CoopRequest] Vote {request.type}: {result}");
        }

        [ClientRpc]
        private void NotifyVoteProgressClientRpc(int requestId, int accepts, int declines, int total)
        {
            Debug.Log($"[CoopRequest] Vote {requestId} progress: {accepts} pour, {declines} contre, {total} total");
        }

        // ============================================================
        // VALIDATION
        // ============================================================

        private bool ValidateRequest(CoopRole sender, CoopRole target, CoopRequestType type)
        {
            // Vote types can be sent by anyone
            if (type == CoopRequestType.VoteCapitulation ||
                type == CoopRequestType.VoteTotalMobilization ||
                type == CoopRequestType.VoteWarDeclaration)
                return true;

            // Validate sender-target pair makes sense
            return type switch
            {
                // Marshal → Intendant/GrandVizir
                CoopRequestType.RequestMoreEquipment => sender == CoopRole.Marshal &&
                    (target == CoopRole.Intendant || target == CoopRole.GrandVizir),
                CoopRequestType.RequestPriorityProduction => sender == CoopRole.Marshal &&
                    (target == CoopRole.Intendant || target == CoopRole.GrandVizir),
                CoopRequestType.RequestSupplyUpgrade => sender == CoopRole.Marshal &&
                    (target == CoopRole.Intendant || target == CoopRole.GrandVizir),

                // Marshal → Chancellor/GrandVizir
                CoopRequestType.RequestConscription => sender == CoopRole.Marshal &&
                    (target == CoopRole.Chancellor || target == CoopRole.GrandVizir),
                CoopRequestType.RequestWarDeclaration => sender == CoopRole.Marshal &&
                    (target == CoopRole.Chancellor || target == CoopRole.GrandVizir),
                CoopRequestType.RequestResearchMilitary => sender == CoopRole.Marshal &&
                    (target == CoopRole.Chancellor || target == CoopRole.GrandVizir),

                // Intendant → Marshal
                CoopRequestType.RequestProvinceProtection =>
                    (sender == CoopRole.Intendant || sender == CoopRole.GrandVizir) && target == CoopRole.Marshal,
                CoopRequestType.RequestArmyRetreat =>
                    (sender == CoopRole.Intendant || sender == CoopRole.GrandVizir) && target == CoopRole.Marshal,

                // Intendant → Chancellor
                CoopRequestType.RequestTradeLawChange =>
                    (sender == CoopRole.Intendant) && target == CoopRole.Chancellor,
                CoopRequestType.RequestStabilityFocus =>
                    (sender == CoopRole.Intendant) && target == CoopRole.Chancellor,

                // Chancellor → Intendant
                CoopRequestType.RequestBuildingPriority =>
                    (sender == CoopRole.Chancellor) && (target == CoopRole.Intendant || target == CoopRole.GrandVizir),
                CoopRequestType.RequestEconomicFocus =>
                    (sender == CoopRole.Chancellor) && (target == CoopRole.Intendant || target == CoopRole.GrandVizir),

                // Chancellor → Marshal
                CoopRequestType.RequestOffensive =>
                    (sender == CoopRole.Chancellor || sender == CoopRole.GrandVizir) && target == CoopRole.Marshal,
                CoopRequestType.RequestDefensivePosture =>
                    (sender == CoopRole.Chancellor || sender == CoopRole.GrandVizir) && target == CoopRole.Marshal,

                _ => false
            };
        }

        // ============================================================
        // DISPLAY HELPERS
        // ============================================================

        public static string GetRequestName(CoopRequestType type) => type switch
        {
            CoopRequestType.RequestMoreEquipment => "Demande d'équipement",
            CoopRequestType.RequestPriorityProduction => "Priorité de production",
            CoopRequestType.RequestSupplyUpgrade => "Amélioration du supply",
            CoopRequestType.RequestConscription => "Augmenter la conscription",
            CoopRequestType.RequestWarDeclaration => "Déclaration de guerre",
            CoopRequestType.RequestResearchMilitary => "Recherche militaire",
            CoopRequestType.RequestProvinceProtection => "Protection de province",
            CoopRequestType.RequestArmyRetreat => "Demande de retraite",
            CoopRequestType.RequestTradeLawChange => "Changement loi commerce",
            CoopRequestType.RequestStabilityFocus => "Focus stabilité",
            CoopRequestType.RequestBuildingPriority => "Priorité construction",
            CoopRequestType.RequestEconomicFocus => "Focus économique",
            CoopRequestType.RequestOffensive => "Demande d'offensive",
            CoopRequestType.RequestDefensivePosture => "Posture défensive",
            CoopRequestType.VoteCapitulation => "VOTE: Capitulation",
            CoopRequestType.VoteTotalMobilization => "VOTE: Mobilisation totale",
            CoopRequestType.VoteWarDeclaration => "VOTE: Déclaration de guerre",
            _ => type.ToString()
        };

        public static string GetStatusName(CoopRequestStatus status) => status switch
        {
            CoopRequestStatus.Pending => "En attente",
            CoopRequestStatus.Accepted => "Accepté",
            CoopRequestStatus.Declined => "Refusé",
            CoopRequestStatus.Expired => "Expiré",
            _ => status.ToString()
        };

        public static Color GetStatusColor(CoopRequestStatus status) => status switch
        {
            CoopRequestStatus.Pending => new Color(0.9f, 0.8f, 0.3f),
            CoopRequestStatus.Accepted => new Color(0.3f, 0.9f, 0.3f),
            CoopRequestStatus.Declined => new Color(0.9f, 0.3f, 0.3f),
            CoopRequestStatus.Expired => new Color(0.5f, 0.5f, 0.5f),
            _ => Color.white
        };
    }
}
