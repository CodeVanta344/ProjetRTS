using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Clean floating city name label — city name, building icon row, and city level info.
    /// Billboards toward camera, scales with distance.
    /// </summary>
    public class CityNameLabel : MonoBehaviour
    {
        private Text nameText;
        private Text buildingIconsText;
        private Text cityInfoText;
        private Canvas labelCanvas;
        private RectTransform canvasRect;
        
        private float baseScale = 0.12f;
        private float maxDistance = 4000f;
        private Camera mainCamera;
        private Vector3 offset = new Vector3(0f, 20f, 0f);
        private bool isCapitalLabel;
        private Outline outline;
        private Shadow shadow;
        
        // Cached references for refresh
        private CityData linkedCityData;
        private bool isPlayerFaction;
        
        public void Initialize(string cityName, string subInfo, bool isCapital, bool isPlayerFaction)
        {
            mainCamera = Camera.main;
            isCapitalLabel = isCapital;
            this.isPlayerFaction = isPlayerFaction;
            
            // Canvas
            GameObject canvasGO = new GameObject("LabelCanvas");
            canvasGO.transform.SetParent(transform);
            labelCanvas = canvasGO.AddComponent<Canvas>();
            labelCanvas.renderMode = RenderMode.WorldSpace;
            
            canvasRect = canvasGO.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(400, 90); // Taller for 3 lines
            canvasRect.localScale = Vector3.one * baseScale;
            
            // === LINE 1: City Name ===
            GameObject nameGO = new GameObject("NameText");
            nameGO.transform.SetParent(canvasGO.transform, false);
            nameText = nameGO.AddComponent<Text>();
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.fontSize = isCapital ? 28 : 20;
            nameText.fontStyle = isCapital ? FontStyle.Bold : FontStyle.Normal;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            nameText.verticalOverflow = VerticalWrapMode.Overflow;
            nameText.raycastTarget = false;
            
            // Color: gold for capital, white for player cities, soft red for enemy
            if (isCapital)
                nameText.color = new Color(1f, 0.85f, 0.3f); // Warm gold
            else if (isPlayerFaction)
                nameText.color = new Color(0.95f, 0.95f, 0.9f); // Soft white
            else
                nameText.color = new Color(0.95f, 0.45f, 0.35f); // Soft red
            
            // Build display text
            if (isCapital && !string.IsNullOrEmpty(subInfo))
                nameText.text = $"★ {cityName}";
            else
                nameText.text = cityName;
            
            // Position: top third of canvas
            RectTransform nameRect = nameGO.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 0.6f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;
            
            // Black outline for readability on any background
            outline = nameGO.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            
            // Additional shadow for depth
            shadow = nameGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            shadow.effectDistance = new Vector2(2f, -2f);
            
            // === LINE 2: Building Icons ===
            GameObject iconsGO = new GameObject("BuildingIcons");
            iconsGO.transform.SetParent(canvasGO.transform, false);
            buildingIconsText = iconsGO.AddComponent<Text>();
            buildingIconsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            buildingIconsText.fontSize = 14;
            buildingIconsText.alignment = TextAnchor.MiddleCenter;
            buildingIconsText.horizontalOverflow = HorizontalWrapMode.Overflow;
            buildingIconsText.verticalOverflow = VerticalWrapMode.Overflow;
            buildingIconsText.raycastTarget = false;
            buildingIconsText.color = new Color(0.9f, 0.85f, 0.7f, 0.9f);
            buildingIconsText.text = "";
            
            RectTransform iconsRect = iconsGO.GetComponent<RectTransform>();
            iconsRect.anchorMin = new Vector2(0f, 0.3f);
            iconsRect.anchorMax = new Vector2(1f, 0.6f);
            iconsRect.offsetMin = Vector2.zero;
            iconsRect.offsetMax = Vector2.zero;
            
            var iconsOutline = iconsGO.AddComponent<Outline>();
            iconsOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            iconsOutline.effectDistance = new Vector2(1f, -1f);
            
            // === LINE 3: City Level & Population ===
            GameObject infoGO = new GameObject("CityInfo");
            infoGO.transform.SetParent(canvasGO.transform, false);
            cityInfoText = infoGO.AddComponent<Text>();
            cityInfoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cityInfoText.fontSize = 12;
            cityInfoText.alignment = TextAnchor.MiddleCenter;
            cityInfoText.horizontalOverflow = HorizontalWrapMode.Overflow;
            cityInfoText.verticalOverflow = VerticalWrapMode.Overflow;
            cityInfoText.raycastTarget = false;
            cityInfoText.color = new Color(0.7f, 0.7f, 0.65f, 0.8f);
            cityInfoText.text = "";
            
            RectTransform infoRect = infoGO.GetComponent<RectTransform>();
            infoRect.anchorMin = new Vector2(0f, 0f);
            infoRect.anchorMax = new Vector2(1f, 0.3f);
            infoRect.offsetMin = Vector2.zero;
            infoRect.offsetMax = Vector2.zero;
            
            var infoOutline = infoGO.AddComponent<Outline>();
            infoOutline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            infoOutline.effectDistance = new Vector2(1f, -1f);
        }
        
        /// <summary>
        /// Link city data and refresh building icons + city info line.
        /// </summary>
        public void RefreshFromCityData(CityData cityData)
        {
            linkedCityData = cityData;
            if (cityData == null) return;
            
            // === Building Icons ===
            string icons = BuildBuildingIconRow(cityData);
            if (buildingIconsText != null)
                buildingIconsText.text = icons;
            
            // === City Info ===
            string popStr = FormatPopulation(cityData.population);
            string levelName = CityLevelThresholds.GetLevelName(cityData.cityLevel);
            if (cityInfoText != null)
                cityInfoText.text = $"Lv.{cityData.cityLevel} {levelName} — {popStr}";
        }
        
        /// <summary>
        /// Build a compact icon string from constructed and constructing buildings.
        /// Constructed = bright icon, Constructing = dimmed with parentheses.
        /// </summary>
        private string BuildBuildingIconRow(CityData cityData)
        {
            if (cityData.buildings == null || cityData.buildings.Count == 0) return "";
            
            var iconParts = new List<string>();
            
            foreach (var bld in cityData.buildings)
            {
                string icon = BuildingInfo.GetIcon(bld.buildingType);
                if (bld.isConstructed)
                {
                    // Show level if > 1
                    if (bld.level > 1)
                        iconParts.Add($"{icon}{bld.level}");
                    else
                        iconParts.Add(icon);
                }
                else if (bld.isConstructing)
                {
                    // Dimmed: wrap with brackets
                    iconParts.Add($"<color=#888>[{icon}]</color>");
                }
            }
            
            return string.Join(" ", iconParts);
        }
        
        private string FormatPopulation(int pop)
        {
            if (pop >= 1000000) return $"{pop / 1000000f:F1}M";
            if (pop >= 1000) return $"{pop / 1000f:F0}k";
            return pop.ToString();
        }
        
        private void LateUpdate()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null) return;
            }
            if (canvasRect == null) return;
            
            // Billboard: face the camera
            transform.rotation = Quaternion.LookRotation(transform.position - mainCamera.transform.position);
            
            // Scale with distance so text stays readable
            float distance = Vector3.Distance(mainCamera.transform.position, transform.position);
            float scale = Mathf.Clamp(distance * 0.0008f, baseScale * 0.6f, baseScale * 3f);
            canvasRect.localScale = Vector3.one * scale;
            
            // Fade at extremes
            float alpha = 1f;
            if (distance > maxDistance * 0.75f)
                alpha = 1f - Mathf.InverseLerp(maxDistance * 0.75f, maxDistance, distance);
            
            // Fade building icons and info at closer zoom (they appear only when close enough)
            float detailAlpha = alpha;
            if (distance > maxDistance * 0.5f)
                detailAlpha = 1f - Mathf.InverseLerp(maxDistance * 0.5f, maxDistance * 0.75f, distance);
            
            if (nameText != null)
            {
                Color c = nameText.color;
                c.a = alpha;
                nameText.color = c;
            }
            if (outline != null)
            {
                Color c = outline.effectColor;
                c.a = alpha * 0.9f;
                outline.effectColor = c;
            }
            if (buildingIconsText != null)
            {
                Color c = buildingIconsText.color;
                c.a = detailAlpha * 0.9f;
                buildingIconsText.color = c;
            }
            if (cityInfoText != null)
            {
                Color c = cityInfoText.color;
                c.a = detailAlpha * 0.8f;
                cityInfoText.color = c;
            }
        }
        
        public void SetPosition(Vector3 worldPosition)
        {
            transform.position = worldPosition + offset;
        }
        
        public void SetText(string mainText, string subTextValue = null)
        {
            if (nameText != null)
                nameText.text = mainText;
        }
    }
}
