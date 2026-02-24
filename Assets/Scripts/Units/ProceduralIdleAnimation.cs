using UnityEngine;

namespace NapoleonicWars.Units
{
    /// <summary>
    /// Procedural idle "breathing" animation that works on ANY model, 
    /// even static meshes with no skeleton or animation clips.
    /// Applies subtle Y bob + rotation sway to simulate living soldiers.
    /// Attach to the model's LOD0 child (the mesh root).
    /// </summary>
    public class ProceduralIdleAnimation : MonoBehaviour
    {
        [Header("Breathing Bob")]
        [SerializeField] private float bobAmplitude = 0.012f;    // World units up/down
        [SerializeField] private float bobSpeed = 1.2f;          // Cycles per second

        [Header("Weight Shift Sway")]
        [SerializeField] private float swayAmplitude = 0.3f;     // Degrees
        [SerializeField] private float swaySpeed = 0.7f;         // Cycles per second

        [Header("Head/Torso Look")]
        [SerializeField] private float lookAmplitude = 0.4f;     // Degrees
        [SerializeField] private float lookSpeed = 0.4f;         // Cycles per second

        [Header("Variation")]
        [SerializeField] private float randomPhase = 1f;         // Per-soldier desync

        private Vector3 baseLocalPos;
        private Quaternion baseLocalRot;
        private float phaseOffset;
        private bool isMoving;
        private float walkBobMultiplier = 1f;

        private void Start()
        {
            baseLocalPos = transform.localPosition;
            baseLocalRot = transform.localRotation;
            
            // Random phase so soldiers don't breathe in sync
            phaseOffset = Random.Range(0f, Mathf.PI * 2f) * randomPhase;
        }

        private void Update()
        {
            if (isMoving)
            {
                // Walk bob is faster and more pronounced
                float walkT = Time.time * bobSpeed * 3f + phaseOffset;
                float walkBob = Mathf.Sin(walkT) * bobAmplitude * 2f * walkBobMultiplier;
                
                transform.localPosition = baseLocalPos + new Vector3(0f, walkBob, 0f);
                
                // Subtle body lean while walking
                float walkSway = Mathf.Sin(walkT * 0.5f) * swayAmplitude * 0.5f;
                transform.localRotation = baseLocalRot * Quaternion.Euler(0f, 0f, walkSway);
            }
            else
            {
                // Idle breathing
                float t = Time.time + phaseOffset;
                
                // Y bob (breathing)
                float bob = Mathf.Sin(t * bobSpeed * Mathf.PI * 2f) * bobAmplitude;
                
                // Z-axis rotation sway (weight shift)
                float sway = Mathf.Sin(t * swaySpeed * Mathf.PI * 2f) * swayAmplitude;
                
                // Y-axis rotation (subtle head turn / looking around)
                float look = Mathf.Sin(t * lookSpeed * Mathf.PI * 2f + 0.7f) * lookAmplitude;
                
                transform.localPosition = baseLocalPos + new Vector3(0f, bob, 0f);
                transform.localRotation = baseLocalRot * Quaternion.Euler(0f, look, sway);
            }
        }

        /// <summary>
        /// Switch between idle breathing and walk bob.
        /// Called by UnitAnimator when state changes.
        /// </summary>
        public void SetMoving(bool moving, float speedMultiplier = 1f)
        {
            isMoving = moving;
            walkBobMultiplier = speedMultiplier;
        }

        /// <summary>
        /// Reset to base pose (e.g., when dying).
        /// </summary>
        public void ResetPose()
        {
            transform.localPosition = baseLocalPos;
            transform.localRotation = baseLocalRot;
            enabled = false;
        }
    }
}
