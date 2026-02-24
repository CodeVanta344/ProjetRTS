using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using NapoleonicWars.Core;
using NapoleonicWars.Units;

namespace NapoleonicWars.UI
{
    public class Minimap : MonoBehaviour, IPointerClickHandler
    {
        [Header("Minimap Settings")]
        [SerializeField] private float mapWorldSize = 250f;
        [SerializeField] private int minimapPixelSize = 200;
        [SerializeField] private float updateInterval = 0.2f;

        private Canvas canvas;
        private RectTransform minimapPanel;
        private RawImage mapImage;
        private RawImage fogImage;
        private Texture2D mapTexture;
        private RectTransform viewportRect;
        private float updateTimer;
        private Camera mainCamera;

        private Color bgColor = new Color(0.15f, 0.2f, 0.1f);
        private Color hillColor = new Color(0.25f, 0.32f, 0.15f);
        private Color playerColor = new Color(0.3f, 0.5f, 1f);
        private Color enemyColor = new Color(1f, 0.3f, 0.3f);
        private Color selectedColor = new Color(0.4f, 0.9f, 1f);
        private Color viewportColor = new Color(1f, 1f, 1f, 0.5f);
        
        // Cached pixel array to avoid GC alloc every frame
        private Color[] cachedPixels;

        private void Start()
        {
            BuildUI();
            mapTexture = new Texture2D(minimapPixelSize, minimapPixelSize, TextureFormat.RGBA32, false);
            mapTexture.filterMode = FilterMode.Point;
            mapTexture.wrapMode = TextureWrapMode.Clamp;
            mapImage.texture = mapTexture;
            
            // Pre-allocate pixel array to avoid GC alloc every frame
            cachedPixels = new Color[minimapPixelSize * minimapPixelSize];
            mainCamera = Camera.main;
        }

        private void Update()
        {
            if (BattleManager.Instance == null) return;

            updateTimer -= Time.deltaTime;
            if (updateTimer <= 0f)
            {
                updateTimer = updateInterval;
                RedrawMinimap();
            }

            UpdateViewport();
        }

        private void BuildUI()
        {
            canvas = UIFactory.CreateCanvas("MinimapCanvas", 15);
            canvas.transform.SetParent(transform);

            // Outer border - responsive sizing (top-right corner, ~12% width)
            minimapPanel = UIFactory.CreateOrnatePanel(canvas.transform, "MinimapPanel",
                new Color(0.12f, 0.13f, 0.11f, 0.95f));
            minimapPanel.anchorMin = new Vector2(0.87f, 0.75f);
            minimapPanel.anchorMax = new Vector2(0.995f, 0.995f);
            minimapPanel.offsetMin = Vector2.zero;
            minimapPanel.offsetMax = Vector2.zero;

            Transform inner = minimapPanel.Find("Inner");

            // Title
            Text title = UIFactory.CreateText(inner, "Title", "MINIMAP", 11, TextAnchor.MiddleCenter, UIFactory.GoldAccent);
            title.fontStyle = FontStyle.Bold;
            RectTransform titleRT = title.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, 1);
            titleRT.anchorMax = new Vector2(1, 1);
            titleRT.pivot = new Vector2(0.5f, 1);
            titleRT.anchoredPosition = Vector2.zero;
            titleRT.sizeDelta = new Vector2(0, 18);

            // Map image
            mapImage = UIFactory.CreateRawImage(inner, "MapImage");
            RectTransform mapRT = mapImage.GetComponent<RectTransform>();
            mapRT.anchorMin = new Vector2(0.5f, 0.5f);
            mapRT.anchorMax = new Vector2(0.5f, 0.5f);
            mapRT.pivot = new Vector2(0.5f, 0.5f);
            mapRT.anchoredPosition = new Vector2(0, -4);
            mapRT.sizeDelta = new Vector2(minimapPixelSize, minimapPixelSize);

            // Fog of war overlay
            if (FogOfWar.Instance != null)
            {
                fogImage = UIFactory.CreateRawImage(inner, "FogOverlay");
                fogImage.texture = FogOfWar.Instance.FogTexture;
                RectTransform fogRT = fogImage.GetComponent<RectTransform>();
                fogRT.anchorMin = mapRT.anchorMin;
                fogRT.anchorMax = mapRT.anchorMax;
                fogRT.pivot = mapRT.pivot;
                fogRT.anchoredPosition = mapRT.anchoredPosition;
                fogRT.sizeDelta = mapRT.sizeDelta;
            }

            // Camera viewport indicator
            RectTransform vpRT = UIFactory.CreatePanel(inner, "Viewport", new Color(1, 1, 1, 0));
            vpRT.anchorMin = new Vector2(0.5f, 0.5f);
            vpRT.anchorMax = new Vector2(0.5f, 0.5f);
            vpRT.pivot = new Vector2(0.5f, 0.5f);
            vpRT.sizeDelta = new Vector2(40, 40);
            viewportRect = vpRT;

            // Viewport border (4 thin edges)
            CreateEdge(vpRT, "Top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -1), Vector2.zero);
            CreateEdge(vpRT, "Bottom", new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 1));
            CreateEdge(vpRT, "Left", new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(1, 0));
            CreateEdge(vpRT, "Right", new Vector2(1, 0), new Vector2(1, 1), new Vector2(-1, 0), Vector2.zero);

            // Make map image clickable
            mapImage.raycastTarget = true;
            mapImage.gameObject.AddComponent<MinimapClickHandler>().minimap = this;
        }

        private void CreateEdge(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMax, Vector2 offsetMin)
        {
            RectTransform edge = UIFactory.CreatePanel(parent, name, viewportColor);
            edge.anchorMin = anchorMin;
            edge.anchorMax = anchorMax;
            edge.offsetMin = offsetMin;
            edge.offsetMax = offsetMax;
        }

        private void RedrawMinimap()
        {
            // Clear to background using cached array
            if (cachedPixels == null) cachedPixels = new Color[minimapPixelSize * minimapPixelSize];
            for (int i = 0; i < cachedPixels.Length; i++)
                cachedPixels[i] = bgColor;

            // Draw player regiments
            DrawRegimentDots(cachedPixels, BattleManager.Instance.PlayerRegiments, playerColor);

            // Draw enemy regiments (only if visible through fog of war)
            DrawRegimentDots(cachedPixels, BattleManager.Instance.EnemyRegiments, enemyColor);

            mapTexture.SetPixels(cachedPixels);
            mapTexture.Apply();

            // Update fog overlay
            if (fogImage != null && FogOfWar.Instance != null)
                fogImage.texture = FogOfWar.Instance.FogTexture;
        }

        private void DrawRegimentDots(Color[] pixels, List<Regiment> regiments, Color color)
        {
            if (regiments == null) return;

            foreach (var reg in regiments)
            {
                if (reg == null || reg.CachedAliveCount <= 0) continue;

                bool visible = true;
                if (FogOfWar.Instance != null && reg.TeamId != 0)
                    visible = FogOfWar.Instance.IsVisible(reg.transform.position);
                if (!visible) continue;

                Color dotColor = reg.IsSelected ? selectedColor : color;
                int dotRadius = reg.IsSelected ? 3 : 2;

                Vector2Int px = WorldToPixel(reg.transform.position);
                DrawDot(pixels, px.x, px.y, dotRadius, dotColor);

                // Draw individual units for selected regiment
                if (reg.IsSelected)
                {
                    var alive = reg.GetAliveUnits();
                    foreach (var unit in alive)
                    {
                        if (unit == null) continue;
                        Vector2Int uPx = WorldToPixel(unit.transform.position);
                        DrawDot(pixels, uPx.x, uPx.y, 1, dotColor * 0.7f);
                    }
                }
            }
        }

        private void DrawDot(Color[] pixels, int cx, int cy, int radius, Color color)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int px = cx + dx;
                    int py = cy + dy;
                    if (px < 0 || px >= minimapPixelSize || py < 0 || py >= minimapPixelSize) continue;
                    if (dx * dx + dy * dy > radius * radius) continue;
                    pixels[py * minimapPixelSize + px] = color;
                }
            }
        }

        private void UpdateViewport()
        {
            Camera cam = mainCamera;
            if (cam == null) return;

            Vector3 camPos = cam.transform.position;
            float nx = (camPos.x + mapWorldSize / 2f) / mapWorldSize;
            float nz = (camPos.z + mapWorldSize / 2f) / mapWorldSize;

            // Position viewport indicator relative to map image center
            float mapUISize = minimapPixelSize;
            float vpX = (nx - 0.5f) * mapUISize;
            float vpY = (nz - 0.5f) * mapUISize;
            viewportRect.anchoredPosition = new Vector2(vpX, vpY - 4);

            float viewSize = (cam.orthographic ? cam.orthographicSize : 30f) / mapWorldSize * mapUISize;
            viewSize = Mathf.Clamp(viewSize, 15f, mapUISize * 0.5f);
            viewportRect.sizeDelta = new Vector2(viewSize * 2, viewSize * 2);
        }

        public void OnMinimapClick(Vector2 localPoint)
        {
            // Convert local click position to world position
            float nx = (localPoint.x / minimapPixelSize) + 0.5f;
            float ny = (localPoint.y / minimapPixelSize) + 0.5f;

            float worldX = nx * mapWorldSize - mapWorldSize / 2f;
            float worldZ = ny * mapWorldSize - mapWorldSize / 2f;

            Camera cam = mainCamera;
            if (cam != null)
                cam.transform.position = new Vector3(worldX, cam.transform.position.y, worldZ - 30f);
        }

        // IPointerClickHandler for the panel itself (fallback)
        public void OnPointerClick(PointerEventData eventData) { }

        private Vector2Int WorldToPixel(Vector3 worldPos)
        {
            float nx = (worldPos.x + mapWorldSize / 2f) / mapWorldSize;
            float ny = (worldPos.z + mapWorldSize / 2f) / mapWorldSize;
            int px = Mathf.Clamp(Mathf.FloorToInt(nx * minimapPixelSize), 0, minimapPixelSize - 1);
            int py = Mathf.Clamp(Mathf.FloorToInt(ny * minimapPixelSize), 0, minimapPixelSize - 1);
            return new Vector2Int(px, py);
        }
    }

    // Helper component for click handling on the map image
    public class MinimapClickHandler : MonoBehaviour, IPointerClickHandler
    {
        public Minimap minimap;

        public void OnPointerClick(PointerEventData eventData)
        {
            RectTransform rt = GetComponent<RectTransform>();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            {
                minimap.OnMinimapClick(localPoint);
            }
        }
    }
}
