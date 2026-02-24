using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Text;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Floating city label with compact info cards showing key stats at a glance.
    /// Layout: City Name on top, then a row of small colored cards below.
    /// Cards show: Level, Population, Income, Garrison, Specialization, Buildings.
    /// </summary>
    public class CityNameLabel : MonoBehaviour
    {
        private Text nameText;
        private Text cardsText;
        private Canvas labelCanvas;
        private RectTransform canvasRect;
        
        private float baseScale = 0.12f;
        private float maxDistance = 4000f;
        private Camera mainCamera;
        private Vector3 offset = new Vector3(0f, 25f, 0f);
        private bool isCapitalLabel;
        private Outline nameOutline;
        
        // Cached references for refresh
        private CityData linkedCityData;
        private bool isPlayerFaction;
        
        // Card colors
        private static readonly string COL_GOLD = "#FFD700";
        private static readonly string COL_GREEN = "#7FDB7F";
        private static readonly string COL_BLUE = "#7FB3FF";
        private static readonly string COL_RED = "#FF7F7F";
        private static readonly string COL_PURPLE = "#C09FFF";
        private static readonly string COL_ORANGE = "#FFB366";
        private static readonly string COL_GRAY = "#B0B0B0";
        private static readonly string COL_WHITE = "#EEEEDD";
        
        public void Initialize(string cityName, string subInfo, bool isCapital, bool isPlayerFaction)
        {
            mainCamera = Camera.main;
            isCapitalLabel = isCapital;
            this.isPlayerFaction = isPlayerFaction;
            
            // Canvas — wider to fit cards
            GameObject canvasGO = new GameObject("LabelCanvas");
            canvasGO.transform.SetParent(transform);
            labelCanvas = canvasGO.AddComponent<Canvas>();
            labelCanvas.renderMode = RenderMode.WorldSpace;
            
            canvasRect = canvasGO.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(600, 120);
            canvasRect.localScale = Vector3.one * baseScale;
            
            // === CITY NAME ===
            GameObject nameGO = new GameObject("NameText");
            nameGO.transform.SetParent(canvasGO.transform, false);
            nameText = nameGO.AddComponent<Text>();
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.fontSize = isCapital ? 32 : 22;
            nameText.fontStyle = isCapital ? FontStyle.Bold : FontStyle.Normal;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            nameText.verticalOverflow = VerticalWrapMode.Overflow;
            nameText.raycastTarget = false;
            nameText.supportRichText = true;
            
            // Color: gold for capital, white for player, soft red for enemy
            if (isCapital)
                nameText.color = new Color(1f, 0.85f, 0.3f);
            else if (isPlayerFaction)
                nameText.color = new Color(0.95f, 0.95f, 0.9f);
            else
                nameText.color = new Color(0.95f, 0.45f, 0.35f);
            
            string displayName = cityName;
            if (isCapital) displayName = $"★ {cityName} ★";
            nameText.text = displayName;
            
            // Position: top part of canvas
            RectTransform nameRect = nameGO.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 0.55f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;
            
            // Outline for readability
            nameOutline = nameGO.AddComponent<Outline>();
            nameOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            nameOutline.effectDistance = new Vector2(1.5f, -1.5f);
            
            var nameShadow = nameGO.AddComponent<Shadow>();
            nameShadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            nameShadow.effectDistance = new Vector2(2f, -2f);
            
            // === INFO CARDS ROW ===
            GameObject cardsGO = new GameObject("InfoCards");
            cardsGO.transform.SetParent(canvasGO.transform, false);
            cardsText = cardsGO.AddComponent<Text>();
            cardsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cardsText.fontSize = 14;
            cardsText.alignment = TextAnchor.MiddleCenter;
            cardsText.horizontalOverflow = HorizontalWrapMode.Overflow;
            cardsText.verticalOverflow = VerticalWrapMode.Overflow;
            cardsText.raycastTarget = false;
            cardsText.supportRichText = true;
            cardsText.color = Color.white;
            cardsText.text = "";
            
            RectTransform cardsRect = cardsGO.GetComponent<RectTransform>();
            cardsRect.anchorMin = new Vector2(0f, 0f);
            cardsRect.anchorMax = new Vector2(1f, 0.52f);
            cardsRect.offsetMin = Vector2.zero;
            cardsRect.offsetMax = Vector2.zero;
            
            var cardsOutline = cardsGO.AddComponent<Outline>();
            cardsOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            cardsOutline.effectDistance = new Vector2(1f, -1f);
            
            var cardsShadow = cardsGO.AddComponent<Shadow>();
            cardsShadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            cardsShadow.effectDistance = new Vector2(1.5f, -1.5f);
        }
        
        /// <summary>
        /// Refresh info cards from city data. Called when city data changes.
        /// </summary>
        public void RefreshFromCityData(CityData cityData)
        {
            linkedCityData = cityData;
            if (cityData == null || cardsText == null) return;
            
            var sb = new StringBuilder();
            
            // Card 1: Level
            string levelName = CityLevelThresholds.GetLevelName(cityData.cityLevel);
            sb.Append($"<color={COL_GOLD}>⚜Lv.{cityData.cityLevel}</color> ");
            
            // Card 2: Population
            string popStr = FormatPopulation(cityData.population);
            sb.Append($"<color={COL_GREEN}>♟{popStr}</color> ");
            
            // Card 3: Income
            int income = EstimateIncome(cityData);
            sb.Append($"<color={COL_ORANGE}>💰{income}</color> ");
            
            // Card 4: Garrison  
            int garrison = EstimateGarrison(cityData);
            if (garrison > 0)
                sb.Append($"<color={COL_RED}>⚔{garrison}</color> ");
            
            // Card 5: Buildings count
            int builtCount = 0;
            int buildingCount = 0;
            if (cityData.buildings != null)
            {
                foreach (var b in cityData.buildings)
                {
                    if (b.isConstructed) builtCount++;
                    else if (b.isConstructing) buildingCount++;
                }
            }
            if (builtCount > 0 || buildingCount > 0)
            {
                sb.Append($"<color={COL_BLUE}>🏛{builtCount}</color>");
                if (buildingCount > 0)
                    sb.Append($"<color={COL_GRAY}>+{buildingCount}</color>");
                sb.Append(" ");
            }
            
            // Card 6: Specialization
            if (cityData.specialization != CitySpecialization.None)
            {
                string specIcon = GetSpecIcon(cityData.specialization);
                string specName = GetSpecShortName(cityData.specialization);
                sb.Append($"<color={COL_PURPLE}>{specIcon}{specName}</color>");
            }
            
            cardsText.text = sb.ToString();
        }
        
        private int EstimateIncome(CityData city)
        {
            // Base income from population + buildings
            int income = city.population / 500;
            if (city.buildings != null)
            {
                foreach (var b in city.buildings)
                {
                    if (b.isConstructed)
                        income += b.level * 5;
                }
            }
            return income;
        }
        
        private int EstimateGarrison(CityData city)
        {
            if (city.buildings == null) return 0;
            int garrison = 0;
            foreach (var b in city.buildings)
            {
                if (b.isConstructed && (b.buildingType == BuildingType.Barracks || 
                    b.buildingType == BuildingType.Fortress ||
                    b.buildingType == BuildingType.VillageBarracks ||
                    b.buildingType == BuildingType.ProvincialBarracks))
                    garrison += b.level * 500;
            }
            return garrison;
        }
        
        private string GetSpecIcon(CitySpecialization spec) => spec switch
        {
            CitySpecialization.Agriculture => "🌾",
            CitySpecialization.Industry => "⚒",
            CitySpecialization.Commerce => "💎",
            CitySpecialization.Military => "⚔",
            CitySpecialization.Mining => "⛏",
            CitySpecialization.Fishing => "🐟",
            CitySpecialization.Forestry => "🌲",
            CitySpecialization.Port => "⚓",
            CitySpecialization.University => "📖",
            _ => "•"
        };
        
        private string GetSpecShortName(CitySpecialization spec) => spec switch
        {
            CitySpecialization.Agriculture => "Agri",
            CitySpecialization.Industry => "Ind",
            CitySpecialization.Commerce => "Com",
            CitySpecialization.Military => "Mil",
            CitySpecialization.Mining => "Mine",
            CitySpecialization.Fishing => "Fish",
            CitySpecialization.Forestry => "Wood",
            CitySpecialization.Port => "Port",
            CitySpecialization.University => "Univ",
            _ => ""
        };
        
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
            float scale = Mathf.Clamp(distance * 0.001f, baseScale * 0.6f, baseScale * 4f);
            canvasRect.localScale = Vector3.one * scale;
            
            // Fade at extremes
            float alpha = 1f;
            if (distance > maxDistance * 0.75f)
                alpha = 1f - Mathf.InverseLerp(maxDistance * 0.75f, maxDistance, distance);
            
            // Cards fade at farther distance (name stays visible longer)
            float cardsAlpha = alpha;
            if (distance > maxDistance * 0.4f)
                cardsAlpha = 1f - Mathf.InverseLerp(maxDistance * 0.4f, maxDistance * 0.7f, distance);
            
            if (nameText != null)
            {
                Color c = nameText.color;
                c.a = alpha;
                nameText.color = c;
            }
            if (nameOutline != null)
            {
                Color c = nameOutline.effectColor;
                c.a = alpha * 0.9f;
                nameOutline.effectColor = c;
            }
            if (cardsText != null)
            {
                Color c = cardsText.color;
                c.a = cardsAlpha;
                cardsText.color = c;
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
