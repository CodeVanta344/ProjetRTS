using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Network;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// Premium HoI4-style left navigation bar with gradient background, icon badges,
    /// active glow indicators, and divider lines between button groups.
    /// </summary>
    public class NavigationBar : MonoBehaviour
    {
        public enum NavPanel
        {
            None,
            Laws,
            Production,
            Research,
            Construction,
            Military,
            Diplomacy,
            Logistics
        }

        private NavPanel activePanel = NavPanel.None;
        private Dictionary<NavPanel, Button> navButtons = new Dictionary<NavPanel, Button>();
        private Dictionary<NavPanel, GameObject> panels = new Dictionary<NavPanel, GameObject>();
        private Dictionary<NavPanel, Image> buttonImages = new Dictionary<NavPanel, Image>();
        private Dictionary<NavPanel, bool> panelWriteAccess = new Dictionary<NavPanel, bool>();
        private Dictionary<NavPanel, GameObject> activeIndicators = new Dictionary<NavPanel, GameObject>();

        // Panel creation callbacks (set by CampaignUI)
        public Action<NavPanel> onPanelRequested;

        /// <summary>The RectTransform where overlay panels should be created</summary>
        public RectTransform panelContainer;

        public void Initialize(RectTransform container)
        {
            panelContainer = container;
            BuildNavBar();
        }

        private void BuildNavBar()
        {
            // === GRADIENT SIDEBAR BACKGROUND ===
            RectTransform barRT = UIFactory.CreateGradientPanel(transform, "NavBarBg",
                UIFactory.GradientTop, UIFactory.GradientBottom);
            barRT.anchorMin = new Vector2(0, 0);
            barRT.anchorMax = new Vector2(0, 1);
            barRT.offsetMin = Vector2.zero;
            barRT.offsetMax = new Vector2(74, -48); // Below top bar

            // === RIGHT EDGE GLOW ===
            RectTransform rightGlow = UIFactory.CreateGlowSeparator(barRT.transform, "RightGlow", true,
                new Color(0.55f, 0.48f, 0.35f, 0.15f), UIFactory.MutedGold);
            UIFactory.SetAnchors(rightGlow.gameObject, new Vector2(1, 0), new Vector2(1, 1),
                new Vector2(-3, 0), Vector2.zero);
            rightGlow.GetComponent<Image>().raycastTarget = false;
            rightGlow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            // === TOP ACCENT GLOW ===
            RectTransform topAccent = UIFactory.CreateGlowSeparator(barRT.transform, "TopAccent", false,
                new Color(0.85f, 0.72f, 0.35f, 0.3f), UIFactory.EmpireGold);
            UIFactory.SetAnchors(topAccent.gameObject, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -3), Vector2.zero);
            topAccent.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            // Vertical layout for buttons
            VerticalLayoutGroup vlg = UIFactory.AddVerticalLayout(barRT.gameObject, 3f,
                new RectOffset(4, 6, 10, 8));
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Create nav buttons with dividers between groups
            CreateNavButton(barRT, NavPanel.Laws, "⚖️", "Lois", new Color(0.6f, 0.7f, 1.0f));
            CreateNavButton(barRT, NavPanel.Production, "🏭", "Prod.", new Color(0.85f, 0.65f, 0.3f));
            CreateNavButton(barRT, NavPanel.Research, "🔬", "Rech.", new Color(0.4f, 0.75f, 0.9f));

            // Divider line
            CreateNavDivider(barRT);

            CreateNavButton(barRT, NavPanel.Construction, "🏗️", "Constr.", new Color(0.75f, 0.6f, 0.4f));
            CreateNavButton(barRT, NavPanel.Military, "⚔️", "Milit.", new Color(0.85f, 0.3f, 0.3f));

            // Divider line
            CreateNavDivider(barRT);

            CreateNavButton(barRT, NavPanel.Diplomacy, "🤝", "Diplo.", new Color(0.5f, 0.8f, 0.5f));
            CreateNavButton(barRT, NavPanel.Logistics, "📦", "Logist.", new Color(0.7f, 0.65f, 0.5f));
        }

        private void CreateNavDivider(Transform parent)
        {
            RectTransform divider = UIFactory.CreateGlowSeparator(parent, "NavDivider", false,
                new Color(0.55f, 0.48f, 0.35f, 0.15f), new Color(0.55f, 0.48f, 0.35f, 0.4f));
            UIFactory.AddLayoutElement(divider.gameObject, preferredHeight: 6);
        }

        private void CreateNavButton(Transform parent, NavPanel panel, string icon, string label, Color iconTint)
        {
            // Button container — slightly larger for premium feel
            GameObject slot = new GameObject("Nav_" + panel);
            slot.transform.SetParent(parent, false);
            RectTransform slotRT = slot.AddComponent<RectTransform>();
            UIFactory.AddLayoutElement(slot, preferredWidth: 64, preferredHeight: 66);

            // Background with subtle border
            Image bgImg = slot.AddComponent<Image>();
            bgImg.color = UIFactory.PanelSurface;
            bgImg.raycastTarget = true;

            Outline bgOutline = slot.AddComponent<Outline>();
            bgOutline.effectColor = new Color(0.25f, 0.23f, 0.20f, 0.3f);
            bgOutline.effectDistance = new Vector2(1f, 1f);

            // Button component
            Button btn = slot.AddComponent<Button>();
            btn.targetGraphic = bgImg;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.3f, 1.2f, 1.1f);
            cb.pressedColor = UIFactory.EmpireGold;
            cb.selectedColor = Color.white;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            NavPanel captured = panel;
            btn.onClick.AddListener(() => TogglePanel(captured));

            // Icon badge (small colored background behind the icon)
            GameObject iconBadge = new GameObject("IconBadge");
            iconBadge.transform.SetParent(slot.transform, false);
            RectTransform badgeRT = iconBadge.AddComponent<RectTransform>();
            badgeRT.anchorMin = new Vector2(0.15f, 0.38f);
            badgeRT.anchorMax = new Vector2(0.85f, 0.95f);
            badgeRT.offsetMin = Vector2.zero;
            badgeRT.offsetMax = Vector2.zero;
            
            Image badgeBg = iconBadge.AddComponent<Image>();
            badgeBg.color = new Color(iconTint.r * 0.2f, iconTint.g * 0.2f, iconTint.b * 0.2f, 0.7f);
            badgeBg.raycastTarget = false;

            // Icon text on badge
            Text iconText = UIFactory.CreateText(iconBadge.transform, "Icon", icon, 20, TextAnchor.MiddleCenter, iconTint);
            UIFactory.SetAnchors(iconText.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Label text
            Text labelText = UIFactory.CreateText(slot.transform, "Label", label.ToUpper(), 9, TextAnchor.MiddleCenter, UIFactory.SilverText);
            labelText.fontStyle = FontStyle.Bold;
            Shadow labelShadow = labelText.gameObject.AddComponent<Shadow>();
            labelShadow.effectColor = new Color(0, 0, 0, 0.5f);
            labelShadow.effectDistance = new Vector2(1f, -1f);
            UIFactory.SetAnchors(labelText.gameObject, new Vector2(0, 0), new Vector2(1, 0.38f), Vector2.zero, Vector2.zero);

            // Active indicator — left gold/crimson strip (initially hidden)
            GameObject indicator = new GameObject("ActiveIndicator");
            indicator.transform.SetParent(slot.transform, false);
            RectTransform indicatorRT = indicator.AddComponent<RectTransform>();
            indicatorRT.anchorMin = new Vector2(0, 0.1f);
            indicatorRT.anchorMax = new Vector2(0, 0.9f);
            indicatorRT.offsetMin = new Vector2(0, 0);
            indicatorRT.offsetMax = new Vector2(3, 0);
            Image indicatorImg = indicator.AddComponent<Image>();
            indicatorImg.color = UIFactory.EmpireGold;
            indicatorImg.raycastTarget = false;

            // Indicator glow (wider, semi-transparent)
            GameObject indicatorGlow = new GameObject("Glow");
            indicatorGlow.transform.SetParent(indicator.transform, false);
            RectTransform glowRT = indicatorGlow.AddComponent<RectTransform>();
            glowRT.anchorMin = Vector2.zero;
            glowRT.anchorMax = Vector2.one;
            glowRT.offsetMin = new Vector2(0, -2);
            glowRT.offsetMax = new Vector2(6, 2);
            Image glowImg = indicatorGlow.AddComponent<Image>();
            glowImg.color = new Color(UIFactory.EmpireGold.r, UIFactory.EmpireGold.g, UIFactory.EmpireGold.b, 0.3f);
            glowImg.raycastTarget = false;

            indicator.SetActive(false);
            activeIndicators[panel] = indicator;

            navButtons[panel] = btn;
            buttonImages[panel] = bgImg;
        }

        public void TogglePanel(NavPanel panel)
        {
            if (activePanel == panel)
            {
                CloseActivePanel();
                return;
            }

            CloseActivePanel();

            activePanel = panel;
            UpdateButtonHighlights();

            onPanelRequested?.Invoke(panel);
        }

        public void CloseActivePanel()
        {
            if (activePanel != NavPanel.None && panels.ContainsKey(activePanel))
            {
                Destroy(panels[activePanel]);
                panels.Remove(activePanel);
            }
            activePanel = NavPanel.None;
            UpdateButtonHighlights();
        }

        public void RegisterPanel(NavPanel panel, GameObject panelGO)
        {
            panels[panel] = panelGO;
        }

        private void UpdateButtonHighlights()
        {
            foreach (var kvp in buttonImages)
            {
                bool isActive = kvp.Key == activePanel;

                // Background color change
                kvp.Value.color = isActive 
                    ? new Color(UIFactory.ImperialCrimson.r * 0.6f, UIFactory.ImperialCrimson.g * 0.6f, UIFactory.ImperialCrimson.b * 0.6f)
                    : UIFactory.PanelSurface;

                // Outline change
                Outline outline = kvp.Value.GetComponent<Outline>();
                if (outline != null)
                    outline.effectColor = isActive
                        ? new Color(UIFactory.EmpireGold.r, UIFactory.EmpireGold.g, UIFactory.EmpireGold.b, 0.6f)
                        : new Color(0.25f, 0.23f, 0.20f, 0.3f);

                // Show/hide active indicator
                if (activeIndicators.ContainsKey(kvp.Key))
                    activeIndicators[kvp.Key].SetActive(isActive);

                // Tint icon and label
                Transform iconBadge = kvp.Value.transform.Find("IconBadge");
                if (iconBadge != null)
                {
                    Text iconText = iconBadge.Find("Icon")?.GetComponent<Text>();
                    if (iconText != null)
                        iconText.color = isActive ? Color.white : iconText.color; // Keep original tint when not active
                }

                Transform labelTr = kvp.Value.transform.Find("Label");
                if (labelTr != null)
                {
                    Text labelText = labelTr.GetComponent<Text>();
                    if (labelText != null)
                        labelText.color = isActive ? UIFactory.EmpireGold : UIFactory.SilverText;
                }
            }
        }

        public NavPanel ActivePanel => activePanel;

        public bool IsAnyPanelOpen => activePanel != NavPanel.None;

        /// <summary>Check if the local player has write access to a panel</summary>
        public bool HasWriteAccess(NavPanel panel)
        {
            return panelWriteAccess.ContainsKey(panel) ? panelWriteAccess[panel] : true;
        }

        /// <summary>Refresh button visuals based on co-op role.</summary>
        public void RefreshCoopAccess()
        {
            if (CoopRoleManager.Instance == null || !CoopRoleManager.Instance.IsCoopMode)
            {
                foreach (NavPanel p in Enum.GetValues(typeof(NavPanel)))
                    panelWriteAccess[p] = true;
                return;
            }

            CoopRole role = CoopRoleManager.Instance.GetLocalRole();

            foreach (var kvp in navButtons)
            {
                NavPanel panel = kvp.Key;
                Button btn = kvp.Value;
                string panelName = panel.ToString();
                bool canControl = CoopRoleManager.CanControlPanel(role, panelName);
                panelWriteAccess[panel] = canControl;

                Transform iconBadge = btn.transform.Find("IconBadge");
                Transform labelTransform = btn.transform.Find("Label");

                if (!canControl)
                {
                    if (iconBadge != null)
                    {
                        Text iconText = iconBadge.Find("Icon")?.GetComponent<Text>();
                        if (iconText != null) iconText.color = new Color(0.5f, 0.45f, 0.4f);
                        Image badgeBg = iconBadge.GetComponent<Image>();
                        if (badgeBg != null) badgeBg.color = new Color(0.15f, 0.15f, 0.15f, 0.5f);
                    }
                    if (labelTransform != null)
                    {
                        Text labelText = labelTransform.GetComponent<Text>();
                        if (labelText != null) labelText.color = new Color(0.35f, 0.3f, 0.25f);
                    }
                }
                else
                {
                    if (iconBadge != null)
                    {
                        Text iconText = iconBadge.Find("Icon")?.GetComponent<Text>();
                        if (iconText != null) iconText.color = UIFactory.TextWhite;
                    }
                    if (labelTransform != null)
                    {
                        Text labelText = labelTransform.GetComponent<Text>();
                        if (labelText != null) labelText.color = UIFactory.TextGrey;
                    }
                }
            }
        }

        /// <summary>Create a standard full-screen overlay panel with header and close button</summary>
        public GameObject CreateOverlayPanel(string title)
        {
            if (panelContainer == null) return null;

            // Full overlay
            GameObject overlay = new GameObject("Panel_" + title);
            overlay.transform.SetParent(panelContainer, false);
            RectTransform overlayRT = overlay.AddComponent<RectTransform>();
            overlayRT.anchorMin = new Vector2(0, 0);
            overlayRT.anchorMax = new Vector2(1, 1);
            overlayRT.offsetMin = new Vector2(74, 0);  // Right of nav bar
            overlayRT.offsetMax = new Vector2(0, -48);  // Below top bar

            Image bg = overlay.AddComponent<Image>();
            bg.color = UIFactory.DeepCharcoal;
            bg.raycastTarget = true;

            // Vertical layout
            VerticalLayoutGroup vlg = UIFactory.AddVerticalLayout(overlay, 0f, new RectOffset(0, 0, 0, 0));
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;

            // Header banner
            RectTransform header = UIFactory.CreateBannerHeader(overlay.transform, "Header", title, 22);
            UIFactory.AddLayoutElement(header.gameObject, preferredHeight: 44);

            // Close button (top right)
            Button closeBtn = UIFactory.CreateButton(header, "CloseBtn", "✕", 18, () =>
            {
                CloseActivePanel();
            });
            RectTransform closeBtnRT = closeBtn.GetComponent<RectTransform>();
            UIFactory.SetAnchors(closeBtnRT, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-44, 4), new Vector2(-4, -4));

            return overlay;
        }
    }
}
