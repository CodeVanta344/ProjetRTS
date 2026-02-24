using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Campaign;
using NapoleonicWars.Data;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// HoI4-Style top bar showing all key resources, manpower, PP, stability, war support, date, and speed controls.
    /// Permanently visible at the top of the screen (40px height).
    /// </summary>
    public class HoI4TopBar : MonoBehaviour
    {
        private CampaignManager campaignManager;
        private FactionData playerFaction;

        // Resource texts
        private Text dateText;
        private Text goldText, foodText, ironText;
        private Text manpowerText, ppText;
        private Text stabilityText, warSupportText;
        private Image stabilityBar, warSupportBar;
        private Text turnText;

        // Speed controls
        private int gameSpeed = 1;

        public void Initialize(CampaignManager manager)
        {
            campaignManager = manager;
            playerFaction = manager.GetPlayerFaction();
            BuildTopBar();
        }

        private void BuildTopBar()
        {
            // Bar background — full width, 40px at top
            RectTransform barRT = UIFactory.CreatePanel(transform, "TopBarBg", 
                new Color(0.10f, 0.11f, 0.10f, 0.97f));
            barRT.anchorMin = new Vector2(0, 1);
            barRT.anchorMax = new Vector2(1, 1);
            barRT.offsetMin = new Vector2(0, -40);
            barRT.offsetMax = Vector2.zero;

            // Gold border bottom
            RectTransform borderLine = UIFactory.CreatePanel(barRT, "Border", UIFactory.BorderGold);
            borderLine.anchorMin = new Vector2(0, 0);
            borderLine.anchorMax = new Vector2(1, 0);
            borderLine.offsetMin = Vector2.zero;
            borderLine.offsetMax = new Vector2(0, 2);
            borderLine.GetComponent<Image>().raycastTarget = false;

            // Horizontal layout
            HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(barRT.gameObject, 12f, 
                new RectOffset(10, 10, 4, 4));
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            // === DATE ===
            dateText = CreateResourceItem(barRT, "📅", "Jan 1805", 100f, UIFactory.GoldAccent);
            
            // Separator
            CreateSep(barRT);

            // === GOLD ===
            goldText = CreateResourceItem(barRT, "💰", "1000 (+150)", 120f, new Color(1f, 0.85f, 0.3f));
            
            // === FOOD ===
            foodText = CreateResourceItem(barRT, "🌾", "500 (+80)", 110f, new Color(0.6f, 0.85f, 0.3f));
            
            // === IRON ===
            ironText = CreateResourceItem(barRT, "⛏️", "200 (+20)", 100f, new Color(0.65f, 0.65f, 0.75f));
            
            CreateSep(barRT);

            // === MANPOWER ===
            manpowerText = CreateResourceItem(barRT, "👥", "50K / 120K", 120f, new Color(0.7f, 0.9f, 0.7f));
            
            // === POLITICAL POWER ===
            ppText = CreateResourceItem(barRT, "⚜️", "100 (+2.0)", 110f, new Color(0.6f, 0.7f, 1.0f));
            
            CreateSep(barRT);

            // === STABILITY BAR ===
            var stabContainer = CreateBarItem(barRT, "Stabilité", 0.70f, 
                new Color(0.2f, 0.8f, 0.3f), out stabilityBar, out stabilityText, 130f);
            
            // === WAR SUPPORT BAR ===
            var wsContainer = CreateBarItem(barRT, "Soutien", 0.50f, 
                new Color(0.9f, 0.5f, 0.2f), out warSupportBar, out warSupportText, 130f);
            
            CreateSep(barRT);

            // === SPEED CONTROLS ===
            CreateSpeedControls(barRT);
            
            CreateSep(barRT);

            // === TURN ===
            turnText = CreateResourceItem(barRT, "🔄", "Tour 1", 80f, UIFactory.ParchmentBeige);
        }

        private Text CreateResourceItem(Transform parent, string icon, string value, float width, Color textColor)
        {
            GameObject container = new GameObject("Res_" + icon);
            container.transform.SetParent(parent, false);
            RectTransform rt = container.AddComponent<RectTransform>();
            UIFactory.AddLayoutElement(container, preferredWidth: width, preferredHeight: 32);

            HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(container, 4f, new RectOffset(0, 0, 0, 0));
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            // Icon
            Text iconText = UIFactory.CreateText(container.transform, "Icon", icon, 14, TextAnchor.MiddleLeft, UIFactory.TextWhite);
            UIFactory.AddLayoutElement(iconText.gameObject, preferredWidth: 22, preferredHeight: 32);

            // Value
            Text valText = UIFactory.CreateText(container.transform, "Value", value, 13, TextAnchor.MiddleLeft, textColor);
            UIFactory.AddLayoutElement(valText.gameObject, preferredWidth: width - 26, preferredHeight: 32);

            return valText;
        }

        private GameObject CreateBarItem(Transform parent, string label, float pct, Color barColor, 
            out Image barFill, out Text labelText, float width)
        {
            GameObject container = new GameObject("Bar_" + label);
            container.transform.SetParent(parent, false);
            RectTransform rt = container.AddComponent<RectTransform>();
            UIFactory.AddLayoutElement(container, preferredWidth: width, preferredHeight: 32);

            // Label
            labelText = UIFactory.CreateText(container.transform, "Label", $"{label}: {(int)(pct * 100)}%", 
                11, TextAnchor.UpperCenter, UIFactory.ParchmentBeige);
            RectTransform labelRT = labelText.GetComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0, 0.5f);
            labelRT.anchorMax = new Vector2(1, 1);
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;

            // Bar background
            var (bg, fill) = UIFactory.CreateProgressBar(container.transform, "Bar", barColor);
            RectTransform bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0, 0);
            bgRT.anchorMax = new Vector2(1, 0.5f);
            bgRT.offsetMin = new Vector2(2, 2);
            bgRT.offsetMax = new Vector2(-2, -1);

            RectTransform fillRT = fill.GetComponent<RectTransform>();
            fillRT.anchorMax = new Vector2(pct, 1);

            barFill = fill;
            return container;
        }

        private void CreateSpeedControls(Transform parent)
        {
            GameObject container = new GameObject("SpeedControls");
            container.transform.SetParent(parent, false);
            UIFactory.AddLayoutElement(container, preferredWidth: 160, preferredHeight: 32);
            HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(container, 2f, new RectOffset(0, 0, 2, 2));
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            string[] labels = { "⏸", "1×", "2×", "3×", "5×" };
            for (int i = 0; i < labels.Length; i++)
            {
                int speed = i;
                Button btn = UIFactory.CreateButton(container.transform, "Speed" + i, labels[i], 11, () => SetSpeed(speed));
                UIFactory.AddLayoutElement(btn.gameObject, preferredWidth: 30, preferredHeight: 28);
            }
        }

        private void SetSpeed(int speed)
        {
            gameSpeed = speed;
            Time.timeScale = speed;
        }

        private void CreateSep(Transform parent)
        {
            RectTransform sep = UIFactory.CreatePanel(parent, "Sep", new Color(UIFactory.BorderGold.r, UIFactory.BorderGold.g, UIFactory.BorderGold.b, 0.4f));
            UIFactory.AddLayoutElement(sep.gameObject, preferredWidth: 2, preferredHeight: 32);
        }

        public void UpdateDisplay()
        {
            if (playerFaction == null) return;

            // Calculate real income breakdown
            var income = campaignManager.CalculateIncomeBreakdown(playerFaction.factionType);

            string goldSign = income.goldNet >= 0 ? "+" : "";
            goldText.text = $"{playerFaction.gold:F0} ({goldSign}{income.goldNet:F0})";
            foodText.text = $"{playerFaction.food:F0} (+{income.foodIncome:F0})";
            ironText.text = $"{playerFaction.iron:F0} (+{income.ironIncome:F0})";
            
            string mpK = playerFaction.manpower >= 1000 ? $"{playerFaction.manpower / 1000}K" : $"{playerFaction.manpower}";
            string maxK = playerFaction.maxManpower >= 1000 ? $"{playerFaction.maxManpower / 1000}K" : $"{playerFaction.maxManpower}";
            manpowerText.text = $"{mpK} / {maxK}";
            
            ppText.text = $"{playerFaction.politicalPower:F0} (+{playerFaction.politicalPowerGain:F1})";

            float stab = playerFaction.stability;
            stabilityText.text = $"Stabilité: {(int)(stab * 100)}%";
            stabilityBar.GetComponent<RectTransform>().anchorMax = new Vector2(stab, 1);
            stabilityBar.color = stab > 0.7f ? new Color(0.2f, 0.8f, 0.3f) : 
                                 stab > 0.4f ? new Color(0.9f, 0.8f, 0.2f) : new Color(0.9f, 0.2f, 0.2f);

            float ws = playerFaction.warSupport;
            warSupportText.text = $"Soutien: {(int)(ws * 100)}%";
            warSupportBar.GetComponent<RectTransform>().anchorMax = new Vector2(ws, 1);

            if (campaignManager != null)
            {
                int turn = campaignManager.CurrentTurn;
                int month = (turn % 12) + 1;
                int year = 1805 + turn / 12;
                string[] months = { "Jan", "Fév", "Mar", "Avr", "Mai", "Jun", "Jul", "Aoû", "Sep", "Oct", "Nov", "Déc" };
                dateText.text = $"{months[month - 1]} {year}";
                turnText.text = $"Tour {turn}";
            }
        }
    }
}
