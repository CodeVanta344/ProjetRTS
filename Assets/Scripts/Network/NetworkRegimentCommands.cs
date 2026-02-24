using Unity.Netcode;
using UnityEngine;
using NapoleonicWars.Units;

namespace NapoleonicWars.Network
{
    public class NetworkRegimentCommands : NetworkBehaviour
    {
        public static NetworkRegimentCommands Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        // === MOVE COMMAND ===

        [ServerRpc(RequireOwnership = false)]
        public void MoveRegimentServerRpc(int regimentIndex, Vector3 destination, int teamId, ServerRpcParams rpcParams = default)
        {
            if (!ValidateTeam(rpcParams.Receive.SenderClientId, teamId)) return;

            Regiment reg = FindRegiment(regimentIndex, teamId);
            if (reg != null)
            {
                reg.MoveRegiment(destination);
                MoveRegimentClientRpc(regimentIndex, destination, teamId);
            }
        }

        [ClientRpc]
        private void MoveRegimentClientRpc(int regimentIndex, Vector3 destination, int teamId)
        {
            if (IsServer) return; // Already executed on server

            Regiment reg = FindRegiment(regimentIndex, teamId);
            if (reg != null)
                reg.MoveRegiment(destination);
        }

        // === ATTACK COMMAND ===

        [ServerRpc(RequireOwnership = false)]
        public void AttackRegimentServerRpc(int attackerIndex, int attackerTeam, int targetIndex, int targetTeam, ServerRpcParams rpcParams = default)
        {
            if (!ValidateTeam(rpcParams.Receive.SenderClientId, attackerTeam)) return;

            Regiment attacker = FindRegiment(attackerIndex, attackerTeam);
            Regiment target = FindRegiment(targetIndex, targetTeam);

            if (attacker != null && target != null)
            {
                attacker.AttackTargetRegiment(target);
                AttackRegimentClientRpc(attackerIndex, attackerTeam, targetIndex, targetTeam);
            }
        }

        [ClientRpc]
        private void AttackRegimentClientRpc(int attackerIndex, int attackerTeam, int targetIndex, int targetTeam)
        {
            if (IsServer) return;

            Regiment attacker = FindRegiment(attackerIndex, attackerTeam);
            Regiment target = FindRegiment(targetIndex, targetTeam);

            if (attacker != null && target != null)
                attacker.AttackTargetRegiment(target);
        }

        // === CHARGE COMMAND ===

        [ServerRpc(RequireOwnership = false)]
        public void ChargeRegimentServerRpc(int attackerIndex, int attackerTeam, int targetIndex, int targetTeam, ServerRpcParams rpcParams = default)
        {
            if (!ValidateTeam(rpcParams.Receive.SenderClientId, attackerTeam)) return;

            Regiment attacker = FindRegiment(attackerIndex, attackerTeam);
            Regiment target = FindRegiment(targetIndex, targetTeam);

            if (attacker != null && target != null)
            {
                attacker.ChargeTargetRegiment(target);
                ChargeRegimentClientRpc(attackerIndex, attackerTeam, targetIndex, targetTeam);
            }
        }

        [ClientRpc]
        private void ChargeRegimentClientRpc(int attackerIndex, int attackerTeam, int targetIndex, int targetTeam)
        {
            if (IsServer) return;

            Regiment attacker = FindRegiment(attackerIndex, attackerTeam);
            Regiment target = FindRegiment(targetIndex, targetTeam);

            if (attacker != null && target != null)
                attacker.ChargeTargetRegiment(target);
        }

        // === FORMATION COMMAND ===

        [ServerRpc(RequireOwnership = false)]
        public void SetFormationServerRpc(int regimentIndex, int teamId, FormationType formation, ServerRpcParams rpcParams = default)
        {
            if (!ValidateTeam(rpcParams.Receive.SenderClientId, teamId)) return;

            Regiment reg = FindRegiment(regimentIndex, teamId);
            if (reg != null)
            {
                reg.SetFormation(formation);
                SetFormationClientRpc(regimentIndex, teamId, formation);
            }
        }

        [ClientRpc]
        private void SetFormationClientRpc(int regimentIndex, int teamId, FormationType formation)
        {
            if (IsServer) return;

            Regiment reg = FindRegiment(regimentIndex, teamId);
            if (reg != null)
                reg.SetFormation(formation);
        }

        // === VOLLEY FIRE TOGGLE ===

        [ServerRpc(RequireOwnership = false)]
        public void ToggleVolleyServerRpc(int regimentIndex, int teamId, ServerRpcParams rpcParams = default)
        {
            if (!ValidateTeam(rpcParams.Receive.SenderClientId, teamId)) return;

            Regiment reg = FindRegiment(regimentIndex, teamId);
            if (reg != null)
            {
                reg.ToggleVolleyFire();
                ToggleVolleyClientRpc(regimentIndex, teamId);
            }
        }

        [ClientRpc]
        private void ToggleVolleyClientRpc(int regimentIndex, int teamId)
        {
            if (IsServer) return;

            Regiment reg = FindRegiment(regimentIndex, teamId);
            if (reg != null)
                reg.ToggleVolleyFire();
        }

        // === HELPERS ===

        private bool ValidateTeam(ulong clientId, int teamId)
        {
            if (NetworkGameManager.Instance == null) return true;
            return NetworkGameManager.Instance.GetTeamForClient(clientId) == teamId;
        }

        private Regiment FindRegiment(int index, int teamId)
        {
            if (NapoleonicWars.Core.BattleManager.Instance == null) return null;

            var list = teamId == 0
                ? NapoleonicWars.Core.BattleManager.Instance.PlayerRegiments
                : NapoleonicWars.Core.BattleManager.Instance.EnemyRegiments;

            if (index < 0 || index >= list.Count) return null;
            return list[index];
        }
    }
}
