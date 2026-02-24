using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Campaign;
using NapoleonicWars.Data;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// Premium HoI4-Style top bar — compact layout with icon badges and glow accents.
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
        private Text turnText;

        public void Initialize(CampaignManager manager)
        {
            campaignManager = manager;
            playerFaction = manager.GetPlayerFaction();
            BuildTopBar();
        }

        private void BuildTopBar()
        {
            // === MAIN BAR BACKGROUND (gradient) ===
            RectTransform barRT = UIFactory.CreateGradientPanel(transform, "TopBarBg",
                UIFactory.GradientTop, UIFactory.GradientBottom, UIFactory.InnerGlow);
            barRT.anchorMin = new Vector2(0, 1);
            barRT.anchorMax = new Vector2(1, 1);
            barRT.offsetMin = new Vector2(0, -48);
            barRT.offsetMax = Vector2.zero;

            // Top gold line (decorative, ignoreLayout)
            RectTransform topLine = UIFactory.CreatePanel(barRT.transform, "TopLine", UIFactory.EmpireGold);
            UIFactory.SetAnchors(topLine, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -1), Vector2.zero);
            topLine.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            topLine.GetComponent<Image>().raycastTarget = false;

            // Bottom edge shadow (decorative, ignoreLayout)
            RectTransform bottomShadow = UIFactory.CreatePanel(barRT.transform, "BottomShadow", new Color(0, 0, 0, 0.4f));
            UIFactory.SetAnchors(bottomShadow, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, -3), Vector2.zero);
            bottomShadow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            bottomShadow.GetComponent<Image>().raycastTarget = false;

            // Bottom gold accent
            RectTransform bottomLine = UIFactory.CreatePanel(barRT.transform, "BottomLine", 
                new Color(UIFactory.MutedGold.r, UIFactory.MutedGold.g, UIFactory.MutedGold.b, 0.5f));
            UIFactory.SetAnchors(bottomLine, new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 1));
            bottomLine.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            bottomLine.GetComponent<Image>().raycastTarget = false;

            // === HORIZONTAL LAYOUT ===
            HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(barRT.gameObject, 2f, new RectOffset(10, 10, 2, 2));
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            // === DATE ===
            dateText = CreateCompactItem(barRT, "📅", "Jan 1805", new Color(0.75f, 0.65f, 0.45f), 100);

            AddSep(barRT);

            // === RESOURCES ===
            goldText = CreateCompactItem(barRT, "💰", "0", new Color(1f, 0.85f, 0.3f), 110);
            foodText = CreateCompactItem(barRT, "🌾", "0", new Color(0.5f, 0.8f, 0.3f), 96);
            ironText = CreateCompactItem(barRT, "⛏️", "0", new Color(0.65f, 0.65f, 0.75f), 80);

            AddSep(barRT);

            // === MANPOWER + PP ===
            manpowerText = CreateCompactItem(barRT, "👥", "0/0", new Color(0.5f, 0.85f, 0.5f), 108);
            ppText = CreateCompactItem(barRT, "⚜️", "0", new Color(0.55f, 0.65f, 1f), 96);

            AddSep(barRT);

            // === STABILITY BAR ===
            stabilityText = CreateBarItem(barRT, "Stabilité", 0.7f, new Color(0.3f, 0.75f, 0.35f), out stabilityFill, 130);

            // === WAR SUPPORT BAR ===
            warSupportText = CreateBarItem(barRT, "Soutien", 0.5f, new Color(0.85f, 0.55f, 0.2f), out warSupportFill, 120);

            AddSep(barRT);

            // === TURN ===
            turnText = CreateCompactItem(barRT, "🔄", "Tour 1", new Color(0.7f, 0.7f, 0.75f), 84);
        }

        /// <summary>Creates a compact resource item: [icon badge] [value]</summary>
        private Text CreateCompactItem(Transform parent, string icon, string value, Color tint, float width)
        {
            GameObject container = new GameObject("Item");
            container.transform.SetParent(parent, false);
            container.AddComponent<RectTransform>();
            UIFactory.AddLayoutElement(container, preferredWidth: width, preferredHeight: 44);

            HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(container, 3f, new RectOffset(2, 2, 4, 4));
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            // Mini icon badge (colored square behind icon)
            GameObject badge = new GameObject("Badge");
            badge.transform.SetParent(container.transform, false);
            badge.AddComponent<RectTransform>();
            UIFactory.AddLayoutElement(badge, preferredWidth: 28, preferredHeight: 28);

            Image badgeBg = badge.AddComponent<Image>();
            badgeBg.color = new Color(tint.r * 0.18f, tint.g * 0.18f, tint.b * 0.18f, 0.8f);
            badgeBg.raycastTarget = false;

            Outline badgeOutline = badge.AddComponent<Outline>();
            badgeOutline.effectColor = new Color(tint.r * 0.5f, tint.g * 0.5f, tint.b * 0.5f, 0.4f);
            badgeOutline.effectDistance = new Vector2(1f, 1f);

            Text iconTxt = UIFactory.CreateText(badge.transform, "I", icon, 15, TextAnchor.MiddleCenter, tint);
            iconTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            UIFactory.SetAnchors(iconTxt.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Value label
            Text valTxt = UIFactory.CreateText(container.transform, "V", value, 12, TextAnchor.MiddleLeft, UIFactory.Porcelain);
            valTxt.fontStyle = FontStyle.Bold;
            valTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            UIFactory.AddLayoutElement(valTxt.gameObject, preferredWidth: width - 36, preferredHeight: 28);

            Shadow shadow = valTxt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.5f);
            shadow.effectDistance = new Vector2(1f, -1f);

            return valTxt;
        }

        /// <summary>Creates a labelled progress bar item.</summary>
        private Text CreateBarItem(Transform parent, string label, float pct, Color barColor, out Image fill, float width)
        {
            GameObject container = new GameObject("Bar_" + label);
            container.transform.SetParent(parent, false);
            container.AddComponent<RectTransform>();
            UIFactory.AddLayoutElement(container, preferredWidth: width, preferredHeight: 44);

            // Label on top
            Text labelText = UIFactory.CreateText(container.transform, "Lbl", $"{label}: {(int)(pct * 100)}%",
                10, TextAnchor.MiddleCenter, UIFactory.Parchment);
            labelText.fontStyle = FontStyle.Bold;
            RectTransform lblRT = labelText.GetComponent<RectTransform>();
            lblRT.anchorMin = new Vector2(0, 0.55f);
            lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = new Vector2(4, 0);
            lblRT.offsetMax = new Vector2(-4, -2);

            // Bar on bottom
            var (bg, barFill, glow) = UIFactory.CreatePremiumProgressBar(container.transform, "PB", barColor, width - 8, 10);
            RectTransform barParent = bg.transform.parent.GetComponent<RectTransform>();
            barParent.anchorMin = new Vector2(0, 0);
            barParent.anchorMax = new Vector2(1, 0.5f);
            barParent.offsetMin = new Vector2(4, 4);
            barParent.offsetMax = new Vector2(-4, -1);

            RectTransform fillRT = barFill.GetComponent<RectTransform>();
            fillRT.anchorMax = new Vector2(pct, 1);

            fill = barFill;
            return labelText;
        }

        /// <summary>Adds a thin gold separator to the layout.</summary>
        private void AddSep(Transform parent)
        {
            RectTransform sep = UIFactory.CreatePanel(parent, "Sep",
                new Color(UIFactory.MutedGold.r, UIFactory.MutedGold.g, UIFactory.MutedGold.b, 0.35f));
            UIFactory.AddLayoutElement(sep.gameObject, preferredWidth: 1, preferredHeight: 32);
            sep.GetComponent<Image>().raycastTarget = false;
        }

        public void UpdateDisplay()
        {
            if (playerFaction == null) return;

            var income = campaignManager.CalculateIncomeBreakdown(playerFaction.factionType);

            string gs = income.goldNet >= 0 ? "+" : "";
            goldText.text = $"{playerFaction.gold:F0} ({gs}{income.goldNet:F0})";
            foodText.text = $"{playerFaction.food:F0} (+{income.foodIncome:F0})";
            ironText.text = $"{playerFaction.iron:F0} (+{income.ironIncome:F0})";

            string mpK = playerFaction.manpower >= 1000 ? $"{playerFaction.manpower / 1000}K" : $"{playerFaction.manpower}";
            string maxK = playerFaction.maxManpower >= 1000 ? $"{playerFaction.maxManpower / 1000}K" : $"{playerFaction.maxManpower}";
            manpowerText.text = $"{mpK}/{maxK}";

            ppText.text = $"{playerFaction.politicalPower:F0} (+{playerFaction.politicalPowerGain:F1})";

            float stab = playerFaction.stability;
            stabilityText.text = $"Stabilité: {(int)(stab * 100)}%";
            stabilityFill.GetComponent<RectTransform>().anchorMax = new Vector2(stab, 1);
            stabilityFill.color = stab > 0.7f ? new Color(0.3f, 0.75f, 0.35f) :
                                  stab > 0.4f ? new Color(0.9f, 0.8f, 0.2f) : new Color(0.9f, 0.2f, 0.2f);

            float ws = playerFaction.warSupport;
            warSupportText.text = $"Soutien: {(int)(ws * 100)}%";
            warSupportFill.GetComponent<RectTransform>().anchorMax = new Vector2(ws, 1);

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
