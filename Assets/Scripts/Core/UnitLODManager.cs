using UnityEngine;
using NapoleonicWars.Units;

namespace NapoleonicWars.Core
{
    public class UnitLODManager : MonoBehaviour
    {
        public static UnitLODManager Instance { get; private set; }

        [Header("LOD Settings")]
        [SerializeField] private float nearDistance = 40f;  // Reduced from 50f
        [SerializeField] private float midDistance = 80f;     // Reduced from 120f
        [SerializeField] private float farDistance = 140f;    // Reduced from 200f
        [SerializeField] private float nearUpdateRate = 0.05f;
        [SerializeField] private float midUpdateRate = 0.2f;
        [SerializeField] private float farUpdateRate = 0.5f;
        [SerializeField] private float veryFarUpdateRate = 1.0f;
        [SerializeField] private float cullDistance = 200f;   // Reduced from 350f - crucial for 200+ units

        private Camera mainCamera;
        private Vector3 cachedCamPos;
        private float camUpdateTimer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            mainCamera = Camera.main;
        }

        private void Update()
        {
            // Cache camera position — avoid per-unit Camera.main access
            camUpdateTimer -= Time.deltaTime;
            if (camUpdateTimer <= 0f)
            {
                camUpdateTimer = 0.1f;
                // Only update if camera reference is valid - don't call Camera.main every frame
                if (mainCamera != null)
                    cachedCamPos = mainCamera.transform.position;
            }
        }

        public float GetUpdateDelay(Vector3 unitPosition)
        {
            float dx = cachedCamPos.x - unitPosition.x;
            float dz = cachedCamPos.z - unitPosition.z;
            float distSqr = dx * dx + dz * dz;

            if (distSqr < nearDistance * nearDistance) return nearUpdateRate;
            if (distSqr < midDistance * midDistance) return midUpdateRate;
            if (distSqr < farDistance * farDistance) return farUpdateRate;
            return veryFarUpdateRate;
        }

        /// <summary>
        /// Returns a stagger offset so units don't all update on the same frame.
        /// Call once per unit at initialization.
        /// </summary>
        public float GetStaggerOffset()
        {
            return Random.Range(0f, nearUpdateRate);
        }

        public bool ShouldDisableRenderer(Vector3 unitPosition)
        {
            float dx = cachedCamPos.x - unitPosition.x;
            float dz = cachedCamPos.z - unitPosition.z;
            return dx * dx + dz * dz > cullDistance * cullDistance;
        }
    }
}
