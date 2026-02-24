using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Network;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// HoI4-style left navigation bar with icon buttons that toggle full-screen overlay panels.
    /// Buttons: Map, Production, Research, Construction, Military, Diplomacy, Logistics
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
            // Nav bar background — left side, 50px wide, below top bar
            RectTransform barRT = UIFactory.CreatePanel(transform, "NavBarBg",
                new Color(0.10f, 0.11f, 0.10f, 0.97f));
            barRT.anchorMin = new Vector2(0, 0);
            barRT.anchorMax = new Vector2(0, 1);
            barRT.offsetMin = new Vector2(0, 0);
            barRT.offsetMax = new Vector2(70, -40); // Below top bar

            // Gold border right
            RectTransform borderLine = UIFactory.CreatePanel(barRT, "Border", UIFactory.BorderGold);
            borderLine.anchorMin = new Vector2(1, 0);
            borderLine.anchorMax = new Vector2(1, 1);
            borderLine.offsetMin = new Vector2(-2, 0);
            borderLine.offsetMax = Vector2.zero;
            borderLine.GetComponent<Image>().raycastTarget = false;

            // Vertical layout for buttons
            VerticalLayoutGroup vlg = UIFactory.AddVerticalLayout(barRT.gameObject, 4f,
                new RectOffset(4, 6, 8, 8));
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Create nav buttons
            CreateNavButton(barRT, NavPanel.Laws, "⚖️", "Lois");
            CreateNavButton(barRT, NavPanel.Production, "🏭", "Prod.");
            CreateNavButton(barRT, NavPanel.Research, "🔬", "Rech.");
            CreateNavButton(barRT, NavPanel.Construction, "🏗️", "Constr.");
            CreateNavButton(barRT, NavPanel.Military, "⚔️", "Milit.");
            CreateNavButton(barRT, NavPanel.Diplomacy, "🤝", "Diplo.");
            CreateNavButton(barRT, NavPanel.Logistics, "📦", "Logist.");
        }

        private void CreateNavButton(Transform parent, NavPanel panel, string icon, string label)
        {
            GameObject container = new GameObject("Nav_" + panel);
            container.transform.SetParent(parent, false);
            UIFactory.AddLayoutElement(container, preferredHeight: 60, preferredWidth: 60);

            Image bg = container.AddComponent<Image>();
            bg.color = UIFactory.ButtonNormal;
            bg.raycastTarget = true;

            Button btn = container.AddComponent<Button>();
            btn.targetGraphic = bg;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.3f, 1.3f, 1.2f);
            cb.pressedColor = new Color(0.7f, 0.7f, 0.65f);
            cb.selectedColor = new Color(1.15f, 1.15f, 1.1f);
            cb.fadeDuration = 0.1f;
            btn.colors = cb;

            NavPanel captured = panel;
            btn.onClick.AddListener(() => TogglePanel(captured));

            // Icon
            Text iconText = UIFactory.CreateText(container.transform, "Icon", icon, 18,
                TextAnchor.MiddleCenter, UIFactory.TextWhite);
            RectTransform iconRT = iconText.GetComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0, 0.4f);
            iconRT.anchorMax = new Vector2(1, 1);
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = Vector2.zero;

            // Label
            Text labelText = UIFactory.CreateText(container.transform, "Label", label, 10,
                TextAnchor.MiddleCenter, UIFactory.TextGrey);
            RectTransform labelRT = labelText.GetComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0, 0);
            labelRT.anchorMax = new Vector2(1, 0.4f);
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;

            // Gold left accent when active (initially hidden)
            RectTransform accent = UIFactory.CreatePanel(container.transform, "Accent", UIFactory.GreenAccent);
            accent.anchorMin = new Vector2(0, 0.1f);
            accent.anchorMax = new Vector2(0, 0.9f);
            accent.offsetMin = Vector2.zero;
            accent.offsetMax = new Vector2(3, 0);
            accent.gameObject.SetActive(false);

            navButtons[panel] = btn;
            buttonImages[panel] = bg;
        }

        public void TogglePanel(NavPanel panel)
        {
            if (activePanel == panel)
            {
                // Close current panel
                CloseActivePanel();
                return;
            }

            // Close previous
            CloseActivePanel();

            // Open new
            activePanel = panel;
            UpdateButtonHighlights();

            // Request panel creation from CampaignUI
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
                kvp.Value.color = isActive ? UIFactory.ButtonActive : UIFactory.ButtonNormal;
                
                // Show/hide accent
                Transform accent = kvp.Value.transform.Find("Accent");
                if (accent != null) accent.gameObject.SetActive(isActive);
            }
        }

        public NavPanel ActivePanel => activePanel;

        public bool IsAnyPanelOpen => activePanel != NavPanel.None;

        /// <summary>Check if the local player has write access to a panel (false = read-only, greyed controls)</summary>
        public bool HasWriteAccess(NavPanel panel)
        {
            return panelWriteAccess.ContainsKey(panel) ? panelWriteAccess[panel] : true;
        }

        /// <summary>Refresh button visuals based on co-op role. Call after role assignment.</summary>
        public void RefreshCoopAccess()
        {
            if (CoopRoleManager.Instance == null || !CoopRoleManager.Instance.IsCoopMode)
            {
                // Single player: all panels writable
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

                // Visual: dim buttons for read-only panels
                Image img = buttonImages[panel];
                Transform iconTransform = btn.transform.Find("Icon");
                Transform labelTransform = btn.transform.Find("Label");

                if (!canControl)
                {
                    // Greyed out look but still clickable (read-only view)
                    if (iconTransform != null)
                    {
                        Text iconText = iconTransform.GetComponent<Text>();
                        if (iconText != null) iconText.color = new Color(0.5f, 0.45f, 0.4f);
                    }
                    if (labelTransform != null)
                    {
                        Text labelText = labelTransform.GetComponent<Text>();
                        if (labelText != null) labelText.color = new Color(0.35f, 0.3f, 0.25f);
                    }
                }
                else
                {
                    if (iconTransform != null)
                    {
                        Text iconText = iconTransform.GetComponent<Text>();
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

            // Full overlay (occupies space right of nav bar, below top bar)
            GameObject overlay = new GameObject("Panel_" + title);
            overlay.transform.SetParent(panelContainer, false);
            RectTransform overlayRT = overlay.AddComponent<RectTransform>();
            overlayRT.anchorMin = new Vector2(0, 0);
            overlayRT.anchorMax = new Vector2(1, 1);
            overlayRT.offsetMin = new Vector2(70, 0);  // Right of nav bar
            overlayRT.offsetMax = new Vector2(0, -40);  // Below top bar

            Image bg = overlay.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.13f, 0.11f, 0.96f);
            bg.raycastTarget = true; // Block clicks through to map

            // Vertical layout
            VerticalLayoutGroup vlg = UIFactory.AddVerticalLayout(overlay, 0f, new RectOffset(0, 0, 0, 0));
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;

            // Header banner
            RectTransform header = UIFactory.CreateBannerHeader(overlay.transform, "Header", title, 22);
            UIFactory.AddLayoutElement(header.gameObject, preferredHeight: 40);

            // Close button (top right)
            Button closeBtn = UIFactory.CreateButton(header, "CloseBtn", "✕", 16, () =>
            {
                CloseActivePanel();
            });
            RectTransform closeBtnRT = closeBtn.GetComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(1, 0);
            closeBtnRT.anchorMax = new Vector2(1, 1);
            closeBtnRT.offsetMin = new Vector2(-40, 2);
            closeBtnRT.offsetMax = new Vector2(-4, -2);

            return overlay;
        }
    }
}
