using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Campaign;
using NapoleonicWars.Data;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// Premium HoI4-Style top bar — compact layout with icon badges and glow accents.
    /// All text is clearly readable with large fonts and sufficient spacing.
    /// </summary>
    public class HoI4TopBar : MonoBehaviour
    {
        private CampaignManager campaignManager;
        private FactionData playerFaction;

        // Value texts
        private Text dateText;
        private Text goldText, foodText, ironText;
        private Text manpowerText, ppText;
        private Text stabilityText, warSupportText;
        private Image stabilityFill, warSupportFill;
        private Text dayText;

        public void Initialize(CampaignManager manager)
        {
            campaignManager = manager;
            playerFaction = manager.GetPlayerFaction();
            BuildTopBar();
        }

        private void BuildTopBar()
        {
            // === MAIN BAR BACKGROUND ===
            RectTransform barRT = UIFactory.CreateGradientPanel(transform, "TopBarBg",
                new Color(0.08f, 0.08f, 0.09f, 0.98f), new Color(0.04f, 0.04f, 0.05f, 0.98f));
            barRT.anchorMin = new Vector2(0, 1);
            barRT.anchorMax = new Vector2(1, 1);
            barRT.offsetMin = new Vector2(0, -42);
            barRT.offsetMax = Vector2.zero;

            // Top gold line
            RectTransform topLine = UIFactory.CreatePanel(barRT.transform, "TopLine", UIFactory.EmpireGold);
            UIFactory.SetAnchors(topLine, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -1), Vector2.zero);
            topLine.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            topLine.GetComponent<Image>().raycastTarget = false;

            // Bottom gold accent
            RectTransform bottomLine = UIFactory.CreatePanel(barRT.transform, "BottomLine", 
                new Color(UIFactory.MutedGold.r, UIFactory.MutedGold.g, UIFactory.MutedGold.b, 0.4f));
            UIFactory.SetAnchors(bottomLine, new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 1));
            bottomLine.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            bottomLine.GetComponent<Image>().raycastTarget = false;

            // === HORIZONTAL LAYOUT ===
            HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(barRT.gameObject, 4f, new RectOffset(8, 8, 2, 2));
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            // === DATE ===
            dateText = CreateCompactItem(barRT, "📅", "Jan 1805", new Color(0.85f, 0.75f, 0.50f), 105);

            AddSep(barRT);

            // === RESOURCES ===
            goldText = CreateCompactItem(barRT, "💰", "0", new Color(1f, 0.85f, 0.3f), 120);
            foodText = CreateCompactItem(barRT, "🌾", "0", new Color(0.5f, 0.8f, 0.3f), 100);
            ironText = CreateCompactItem(barRT, "⛏", "0", new Color(0.7f, 0.7f, 0.8f), 90);

            AddSep(barRT);

            // === MANPOWER + PP ===
            manpowerText = CreateCompactItem(barRT, "👥", "0", new Color(0.5f, 0.85f, 0.5f), 100);
            ppText = CreateCompactItem(barRT, "⚜", "0", new Color(0.6f, 0.7f, 1f), 100);

            AddSep(barRT);

            // === STABILITY BAR ===
            stabilityText = CreateBarItem(barRT, "Stabilité", 0.7f, new Color(0.3f, 0.75f, 0.35f), out stabilityFill, 125);

            // === WAR SUPPORT BAR ===
            warSupportText = CreateBarItem(barRT, "Soutien", 0.5f, new Color(0.85f, 0.55f, 0.2f), out warSupportFill, 115);

            AddSep(barRT);

            // === DAY/DATE (not turns) ===
            dayText = CreateCompactItem(barRT, "☀", "Jour 1", new Color(0.75f, 0.75f, 0.8f), 80);
        }

        /// <summary>Compact resource item: [icon badge] [value text]</summary>
        private Text CreateCompactItem(Transform parent, string icon, string value, Color tint, float width)
        {
            GameObject container = new GameObject("Item");
            container.transform.SetParent(parent, false);
            container.AddComponent<RectTransform>();
            UIFactory.AddLayoutElement(container, preferredWidth: width, preferredHeight: 38);

            HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(container, 4f, new RectOffset(2, 2, 3, 3));
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            // Mini icon badge
            GameObject badge = new GameObject("Badge");
            badge.transform.SetParent(container.transform, false);
            badge.AddComponent<RectTransform>();
            UIFactory.AddLayoutElement(badge, preferredWidth: 26, preferredHeight: 26);

            Image badgeBg = badge.AddComponent<Image>();
            badgeBg.color = new Color(tint.r * 0.15f, tint.g * 0.15f, tint.b * 0.15f, 0.9f);
            badgeBg.raycastTarget = false;

            Text iconTxt = UIFactory.CreateText(badge.transform, "I", icon, 14, TextAnchor.MiddleCenter, tint);
            iconTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            UIFactory.SetAnchors(iconTxt.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Value label — CLEAR and LARGE
            Text valTxt = UIFactory.CreateText(container.transform, "V", value, 13, TextAnchor.MiddleLeft, UIFactory.Porcelain);
            valTxt.fontStyle = FontStyle.Bold;
            valTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            UIFactory.AddLayoutElement(valTxt.gameObject, preferredWidth: width - 34, preferredHeight: 26);

            // Drop shadow for readability
            Shadow shadow = valTxt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.7f);
            shadow.effectDistance = new Vector2(1f, -1f);

            return valTxt;
        }

        /// <summary>Labelled progress bar with percentage text.</summary>
        private Text CreateBarItem(Transform parent, string label, float pct, Color barColor, out Image fill, float width)
        {
            GameObject container = new GameObject("Bar_" + label);
            container.transform.SetParent(parent, false);
            container.AddComponent<RectTransform>();
            UIFactory.AddLayoutElement(container, preferredWidth: width, preferredHeight: 38);

            // Label on top
            Text labelText = UIFactory.CreateText(container.transform, "Lbl", $"{label}: {(int)(pct * 100)}%",
                11, TextAnchor.MiddleCenter, UIFactory.Porcelain);
            labelText.fontStyle = FontStyle.Bold;
            RectTransform lblRT = labelText.GetComponent<RectTransform>();
            lblRT.anchorMin = new Vector2(0, 0.5f);
            lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = new Vector2(4, 0);
            lblRT.offsetMax = new Vector2(-4, -2);
            
            // Drop shadow on label for clarity
            Shadow lblShadow = labelText.gameObject.AddComponent<Shadow>();
            lblShadow.effectColor = new Color(0, 0, 0, 0.7f);
            lblShadow.effectDistance = new Vector2(1f, -1f);

            // Bar on bottom
            var (bg, barFill, glow) = UIFactory.CreatePremiumProgressBar(container.transform, "PB", barColor, width - 8, 8);
            RectTransform barParent = bg.transform.parent.GetComponent<RectTransform>();
            barParent.anchorMin = new Vector2(0, 0);
            barParent.anchorMax = new Vector2(1, 0.45f);
            barParent.offsetMin = new Vector2(4, 3);
            barParent.offsetMax = new Vector2(-4, -1);

            RectTransform fillRT = barFill.GetComponent<RectTransform>();
            fillRT.anchorMax = new Vector2(pct, 1);

            fill = barFill;
            return labelText;
        }

        /// <summary>Thin gold separator.</summary>
        private void AddSep(Transform parent)
        {
            RectTransform sep = UIFactory.CreatePanel(parent, "Sep",
                new Color(UIFactory.MutedGold.r, UIFactory.MutedGold.g, UIFactory.MutedGold.b, 0.25f));
            UIFactory.AddLayoutElement(sep.gameObject, preferredWidth: 1, preferredHeight: 28);
            sep.GetComponent<Image>().raycastTarget = false;
        }

        public void UpdateDisplay()
        {
            if (playerFaction == null) return;

            var income = campaignManager.CalculateIncomeBreakdown(playerFaction.factionType);

            // Gold with +/- sign
            string gs = income.goldNet >= 0 ? "+" : "";
            goldText.text = $"{playerFaction.gold:F0} ({gs}{income.goldNet:F0})";
            
            // Food
            foodText.text = $"{playerFaction.food:F0} (+{income.foodIncome:F0})";
            
            // Iron
            ironText.text = $"{playerFaction.iron:F0} (+{income.ironIncome:F0})";

            // Manpower
            string mpK = playerFaction.manpower >= 1000 ? $"{playerFaction.manpower / 1000}K" : $"{playerFaction.manpower}";
            string maxK = playerFaction.maxManpower >= 1000 ? $"{playerFaction.maxManpower / 1000}K" : $"{playerFaction.maxManpower}";
            manpowerText.text = $"{mpK}/{maxK}";

            // Political power
            ppText.text = $"{playerFaction.politicalPower:F0} (+{playerFaction.politicalPowerGain:F1})";

            // Stability
            float stab = playerFaction.stability;
            stabilityText.text = $"Stabilité: {(int)(stab * 100)}%";
            stabilityFill.GetComponent<RectTransform>().anchorMax = new Vector2(stab, 1);
            stabilityFill.color = stab > 0.7f ? new Color(0.3f, 0.75f, 0.35f) :
                                  stab > 0.4f ? new Color(0.9f, 0.8f, 0.2f) : new Color(0.9f, 0.2f, 0.2f);

            // War support
            float ws = playerFaction.warSupport;
            warSupportText.text = $"Soutien: {(int)(ws * 100)}%";
            warSupportFill.GetComponent<RectTransform>().anchorMax = new Vector2(ws, 1);

            // Day counter (not turns)
            if (campaignManager != null)
            {
                int day = campaignManager.CurrentTurn;
                int month = (day % 12) + 1;
                int year = 1805 + day / 12;
                string[] months = { "Jan", "Fév", "Mar", "Avr", "Mai", "Jun", "Jul", "Aoû", "Sep", "Oct", "Nov", "Déc" };
                dateText.text = $"{months[month - 1]} {year}";
                dayText.text = $"Jour {day}";
            }
        }
    }
}
