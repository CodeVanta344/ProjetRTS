using UnityEngine;
using UnityEngine.EventSystems;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    public class CampaignSceneSetup : MonoBehaviour
    {
        [Header("Camera Settings")]
        public bool use3DMap = true;  // Default to 3D map
        public Vector3 camera3DPosition = new Vector3(-300f, 1500f, -1500f); // HoI4: higher altitude, more top-down
        public Vector3 camera3DRotation = new Vector3(75f, 0f, 0f); // HoI4: nearly top-down view
        
        [Header("Map Settings")]
        [Tooltip("Assign a real-world grayscale heightmap here (e.g., EuropeHeightmap.png). Must have Read/Write enabled in import settings.")]
        public Texture2D heightmapTexture;
        
        private void Awake()
        {
            // CampaignManager
            if (CampaignManager.Instance == null)
            {
                GameObject cmGO = new GameObject("CampaignManager");
                cmGO.AddComponent<CampaignManager>();
            }
            
            // BattleTransitionManager (persistent)
            if (BattleTransitionManager.Instance == null)
            {
                GameObject btmGO = new GameObject("BattleTransitionManager");
                btmGO.AddComponent<BattleTransitionManager>();
            }
            
            // ArmyTooltipUI
            if (NapoleonicWars.UI.ArmyTooltipUI.Instance == null)
            {
                GameObject tooltipGO = new GameObject("ArmyTooltipUI");
                tooltipGO.AddComponent<NapoleonicWars.UI.ArmyTooltipUI>();
            }

            // Campaign Clock (real-time tick system)
            if (CampaignClock.Instance == null)
            {
                GameObject clockGO = new GameObject("CampaignClock");
                clockGO.AddComponent<CampaignClock>();
            }
            
            // Campaign UI
            GameObject uiGO = new GameObject("CampaignUI");
            uiGO.AddComponent<NapoleonicWars.UI.CampaignUI>();
            
            // 3D Map (if enabled)
            if (use3DMap)
            {
                Setup3DMap();
            }

            // Camera for campaign map
            SetupCamera();

            // EventSystem (required for Canvas UI)
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
            }
        }
        
        private void Setup3DMap()
        {
            GameObject mapGO = new GameObject("CampaignMap3D");
            CampaignMap3D map3D = mapGO.AddComponent<CampaignMap3D>();
            
            // Pass the heightmap texture to the map generator
            map3D.heightmapTexture = heightmapTexture;
            
            // Configure map materials
            map3D.mapMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            map3D.mapMaterial.color = new Color(0.35f, 0.45f, 0.25f);
            
            // Water material is created in CreateSeaPlane() with Sprites/Default for reliable transparency
            // Do NOT set waterMaterial here — it overrides the transparent setup
            
            map3D.borderMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            map3D.borderMaterial.color = new Color(0.8f, 0.7f, 0.4f);
        }
        
        private void SetupCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
                cam = camGO.AddComponent<Camera>();
                camGO.AddComponent<AudioListener>();
            }

            // Ensure lighting and environment are clean to prevent white/black screen issues
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.65f);

            // Add a directional light if missing (URP Lit needs light)
            if (FindAnyObjectByType<Light>() == null)
            {
                GameObject lightGO = new GameObject("Directional Light");
                Light light = lightGO.AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                light.intensity = 1.2f;
                light.shadows = LightShadows.Soft;
            }

            if (use3DMap)
            {
                // 3D perspective camera
                cam.transform.position = camera3DPosition;
                cam.transform.rotation = Quaternion.Euler(camera3DRotation);
                cam.orthographic = false;
                cam.fieldOfView = 45f;
                cam.farClipPlane = 20000f; // Massively increased for the new 10000x7500 map scale
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
                
                // Add camera controller for movement
                var camCtrl = cam.gameObject.AddComponent<CampaignCameraController>();
                
                // Focus on player faction after a short delay (let map generate first)
                StartCoroutine(FocusCameraOnPlayerFaction(camCtrl));
            }
            else
            {
                // 2D top-down orthographic
                cam.transform.position = new Vector3(0f, 10f, 0f);
                cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
            }
        }
        
        private System.Collections.IEnumerator FocusCameraOnPlayerFaction(CampaignCameraController camCtrl)
        {
            // Wait a frame for CampaignManager + CampaignMap3D to initialize
            yield return null;
            yield return null;
            
            if (CampaignManager.Instance == null) yield break;
            
            FactionType playerFaction = CampaignManager.Instance.PlayerFaction;
            var map3D = FindAnyObjectByType<CampaignMap3D>();
            if (map3D == null) yield break;
            
            // Compute average world position of player's provinces
            var provinces = CampaignManager.Instance.GetAllProvinces();
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (var prov in provinces.Values)
            {
                if (prov.owner == playerFaction)
                {
                    sum += map3D.MapToWorldPosition(prov.mapPosition);
                    count++;
                }
            }
            
            if (count > 0)
            {
                Vector3 center = sum / count;
                camCtrl.FocusOnPosition(center, 800f); // 800 = comfortable zoom level
                Debug.Log($"[Camera] Focused on {playerFaction}: {center} ({count} provinces)");
            }
        }
    }
    
    public class CampaignCameraController : MonoBehaviour
    {
        public float moveSpeed = 400f;
        public float zoomSpeed = 200f;
        public float minHeight = 10f; // Zoom in very close
        public float maxHeight = 6000f; // Zoom out to see the whole massive map
        public float minX = -4000f;
        public float maxX = 4000f;
        public float minZ = -3000f;
        public float maxZ = 3000f;
        
        private Camera cam;
        private Vector3 targetPosition;
        
        private void Start()
        {
            cam = GetComponent<Camera>();
            targetPosition = transform.position;
        }
        
        private void Update()
        {
            HandleInput();
            ApplyMovement();
        }
        
        private void HandleInput()
        {
            Vector3 moveDir = Vector3.zero;
            
            // WASD or Arrow keys movement
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
                moveDir += Vector3.forward;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                moveDir += Vector3.back;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                moveDir += Vector3.left;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                moveDir += Vector3.right;
            
            // Normalize to prevent faster diagonal movement
            if (moveDir.magnitude > 1f)
                moveDir.Normalize();
            
            // Apply movement speed - faster when zoomed out
            float currentSpeed = moveSpeed * Mathf.Lerp(1f, 3f, (transform.position.y - minHeight) / (maxHeight - minHeight));
            targetPosition += moveDir * currentSpeed * Time.deltaTime;
            
            // Zoom with scroll wheel — ignore if mouse is over UI (e.g. city panel scroll)
            if (!EventSystem.current || !EventSystem.current.IsPointerOverGameObject())
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (scroll != 0)
                {
                    float actualZoomSpeed = Input.GetKey(KeyCode.LeftControl) ? zoomSpeed * 3f : zoomSpeed;
                    targetPosition.y -= scroll * actualZoomSpeed;
                }
            }
            
            // Clamp position
            targetPosition.x = Mathf.Clamp(targetPosition.x, minX, maxX);
            targetPosition.y = Mathf.Clamp(targetPosition.y, minHeight, maxHeight);
            targetPosition.z = Mathf.Clamp(targetPosition.z, minZ, maxZ);
        }
        
        private void ApplyMovement()
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 10f);
        }
        
        /// <summary>
        /// Instantly move camera to look at a world position at a specified height.
        /// </summary>
        public void FocusOnPosition(Vector3 worldPos, float height = 800f)
        {
            targetPosition = new Vector3(worldPos.x, height, worldPos.z);
            transform.position = targetPosition;
        }
    }
}
