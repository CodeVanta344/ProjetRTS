using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Campaign;
using NapoleonicWars.Data;
using NapoleonicWars.Units;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// Tooltip UI for displaying army information on hover in campaign map.
    /// Shows unit count, types, and composition.
    /// </summary>
    public class ArmyTooltipUI : MonoBehaviour
    {
        [Header("UI References")]
        private Canvas canvas;
        private RectTransform tooltipPanel;
        private Text titleText;
        private Text infoText;
        private Text compositionText;
        
        [Header("Settings")]
        [SerializeField] private Vector2 tooltipOffset = new Vector2(15f, -15f);
        [SerializeField] private float fadeInDuration = 0.15f;
        [SerializeField] private float displayDelay = 0.2f;
        
        private CanvasGroup canvasGroup;
        private float displayTimer;
        private bool isHovering;
        private ArmyData currentArmy;
        
        private void Awake()
        {
            CreateUI();
        }
        
        private void CreateUI()
        {
            // Create canvas for tooltip
            GameObject canvasGO = new GameObject("ArmyTooltipCanvas");
            canvasGO.transform.SetParent(transform);
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // On top of everything
            
            // Add canvas scaler
            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            // Create tooltip panel
            tooltipPanel = UIFactory.CreateOrnatePanel(canvas.transform, "TooltipPanel", 
                new Color(0.12f, 0.13f, 0.11f, 0.95f));
            tooltipPanel.sizeDelta = new Vector2(280, 200);
            
            // Add canvas group for fading
            canvasGroup = tooltipPanel.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            
            // Get inner content area
            Transform inner = tooltipPanel.Find("Inner");
            UIFactory.AddVerticalLayout(inner.gameObject, 4f, new RectOffset(12, 12, 10, 10));
            
            // Title
            titleText = UIFactory.CreateText(inner, "Title", "Army Name", 16, TextAnchor.MiddleLeft, UIFactory.GoldAccent);
            titleText.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(titleText.gameObject, 24);
            
            // Separator
            RectTransform sep = UIFactory.CreateOrnamentalSeparator(inner);
            UIFactory.AddLayoutElement(sep.gameObject, 12);
            
            // Main info
            infoText = UIFactory.CreateText(inner, "Info", "Info", 13, TextAnchor.MiddleLeft, UIFactory.ParchmentBeige);
            UIFactory.AddLayoutElement(infoText.gameObject, 60);
            
            // Composition header
            Text compHeader = UIFactory.CreateSectionHeader(inner, "CompHeader", "Composition:", 12);
            UIFactory.AddLayoutElement(compHeader.transform.parent.gameObject, 20);
            
            // Composition details
            compositionText = UIFactory.CreateText(inner, "Composition", "", 11, TextAnchor.MiddleLeft, UIFactory.TextGrey);
            UIFactory.AddLayoutElement(compositionText.gameObject, 80);
            
            // Hide initially
            tooltipPanel.gameObject.SetActive(false);
        }
        
        private void Update()
        {
            if (isHovering && currentArmy != null)
            {
                displayTimer += Time.deltaTime;
                
                if (displayTimer >= displayDelay)
                {
                    ShowTooltip();
                }
                
                // Follow mouse
                UpdatePosition();
            }
            else
            {
                HideTooltipInternal();
            }
            
            // Smooth fade
            float targetAlpha = (isHovering && displayTimer >= displayDelay) ? 1f : 0f;
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime / fadeInDuration);
            
            if (canvasGroup.alpha <= 0.01f && !isHovering)
            {
                tooltipPanel.gameObject.SetActive(false);
            }
        }
        
        /// <summary>
        /// Show tooltip for an army
        /// </summary>
        public void ShowArmyTooltip(ArmyData army)
        {
            if (army == null) return;
            
            currentArmy = army;
            isHovering = true;
            displayTimer = 0f;
            
            UpdateTooltipContent();
        }
        
        /// <summary>
        /// Hide the tooltip
        /// </summary>
        public void HideArmyTooltip()
        {
            isHovering = false;
        }
        
        private void UpdateTooltipContent()
        {
            if (currentArmy == null) return;
            
            // Title
            titleText.text = currentArmy.armyName;
            
            // Main info
            infoText.text = $"Faction: {currentArmy.faction}\n" +
                           $"Total Soldiers: {currentArmy.TotalSoldiers}\n" +
                           $"Regiments: {currentArmy.regiments.Count}\n" +
                           $"Movement: {currentArmy.movementPoints}/{currentArmy.maxMovementPoints}\n" +
                           $"Upkeep: {currentArmy.MaintenanceCost:F0} gold/turn";
            
            // Composition breakdown
            int infantry = 0, cavalry = 0, artillery = 0, lightInf = 0, grenadiers = 0;
            int guard = 0, guardCav = 0, guardArt = 0;
            int totalRank = 0;
            
            foreach (var reg in currentArmy.regiments)
            {
                totalRank += reg.rank;
                switch (reg.unitType)
                {
                    case UnitType.LineInfantry: infantry++; break;
                    case UnitType.LightInfantry: lightInf++; break;
                    case UnitType.Grenadier: grenadiers++; break;
                    case UnitType.Cavalry:
                    case UnitType.Hussar:
                    case UnitType.Lancer: cavalry++; break;
                    case UnitType.Artillery: artillery++; break;
                    case UnitType.GuardInfantry: guard++; break;
                    case UnitType.GuardCavalry: guardCav++; break;
                    case UnitType.GuardArtillery: guardArt++; break;
                }
            }
            
            string compText = "";
            if (infantry > 0) compText += $"  ⚔ Ligne: {infantry}\n";
            if (lightInf > 0) compText += $"  🏹 Inf. légère: {lightInf}\n";
            if (grenadiers > 0) compText += $"  💣 Grenadiers: {grenadiers}\n";
            if (guard > 0) compText += $"  🌟 Garde Imp.: {guard}\n";
            if (cavalry > 0) compText += $"  🐎 Cavalerie: {cavalry}\n";
            if (guardCav > 0) compText += $"  🌟 Cav. Garde: {guardCav}\n";
            if (artillery > 0) compText += $"  💥 Artillerie: {artillery}\n";
            if (guardArt > 0) compText += $"  🌟 Art. Garde: {guardArt}\n";

            // Average rank
            if (currentArmy.regiments.Count > 0)
            {
                int avgRank = totalRank / currentArmy.regiments.Count;
                if (avgRank > 0)
                    compText += $"  🏅 Rang moyen: {RegimentRankSystem.GetRankName(avgRank)}\n";
            }
            
            compositionText.text = compText.TrimEnd();
            
            // Adjust panel height based on content
            float height = 140f; // Base height
            height += currentArmy.regiments.Count * 5f; // Extra space for regiments
            tooltipPanel.sizeDelta = new Vector2(280, Mathf.Min(height, 280f));
        }
        
        private void ShowTooltip()
        {
            tooltipPanel.gameObject.SetActive(true);
            UpdatePosition();
        }
        
        private void HideTooltipInternal()
        {
            // Fade out handled in Update
        }
        
        private void UpdatePosition()
        {
            // Position near mouse with offset
            Vector2 mousePos = Input.mousePosition;
            Vector2 tooltipPos = mousePos + tooltipOffset;
            
            // Keep on screen
            float maxX = Screen.width - tooltipPanel.sizeDelta.x - 10f;
            float maxY = Screen.height - tooltipPanel.sizeDelta.y - 10f;
            
            tooltipPos.x = Mathf.Clamp(tooltipPos.x, 10f, maxX);
            tooltipPos.y = Mathf.Clamp(tooltipPos.y, 10f, maxY);
            
            tooltipPanel.position = tooltipPos;
        }
        
        /// <summary>
        /// Static helper to show tooltip from anywhere
        /// </summary>
        public static void ShowTooltip(ArmyData army)
        {
            if (Instance != null)
            {
                Instance.ShowArmyTooltip(army);
            }
        }
        
        /// <summary>
        /// Static helper to hide tooltip
        /// </summary>
        public static void HideTooltip()
        {
            if (Instance != null)
            {
                Instance.HideArmyTooltip();
            }
        }
        
        private static ArmyTooltipUI _instance;
        public static ArmyTooltipUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<ArmyTooltipUI>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("ArmyTooltipUI");
                        _instance = go.AddComponent<ArmyTooltipUI>();
                    }
                }
                return _instance;
            }
        }
    }
}
