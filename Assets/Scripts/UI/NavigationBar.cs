using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Network;
using TMPro;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// Premium HoI4/EU4-style left navigation bar with glassmorphic gradient,
    /// animated icon buttons, gold active indicators, role-based tab filtering,
    /// and full-screen overlay panels.
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

        // ── Tab metadata ──────────────────────────────────────
        private struct TabDef
        {
            public NavPanel panel;
            public string iconResource; // Path in Resources folder (e.g. "UI/NavIcons/icon_laws")
            public string iconFallback; // Unicode fallback if image not found
            public string label;
            public Color  accentColor;
            public int    group;        // 0 = governance, 1 = warfare, 2 = foreign
        }

        private static readonly TabDef[] Tabs = new[]
        {
            new TabDef { panel = NavPanel.Laws,         iconResource = "UI/NavIcons/icon_laws",         iconFallback = "⚖",  label = "LOIS",    accentColor = new Color(0.55f, 0.65f, 0.95f), group = 0 },
            new TabDef { panel = NavPanel.Production,   iconResource = "UI/NavIcons/icon_production",   iconFallback = "⚒",  label = "PROD.",   accentColor = new Color(0.90f, 0.70f, 0.30f), group = 0 },
            new TabDef { panel = NavPanel.Research,     iconResource = "UI/NavIcons/icon_research",     iconFallback = "✦",  label = "RECH.",   accentColor = new Color(0.30f, 0.75f, 0.90f), group = 0 },
            new TabDef { panel = NavPanel.Construction, iconResource = "UI/NavIcons/icon_construction", iconFallback = "⛏",  label = "CONSTR.", accentColor = new Color(0.80f, 0.55f, 0.30f), group = 1 },
            new TabDef { panel = NavPanel.Military,     iconResource = "UI/NavIcons/icon_military",     iconFallback = "⚔",  label = "MILIT.",  accentColor = new Color(0.90f, 0.22f, 0.22f), group = 1 },
            new TabDef { panel = NavPanel.Diplomacy,    iconResource = "UI/NavIcons/icon_diplomacy",    iconFallback = "☮",  label = "DIPLO.",  accentColor = new Color(0.35f, 0.80f, 0.50f), group = 2 },
            new TabDef { panel = NavPanel.Logistics,    iconResource = "UI/NavIcons/icon_logistics",    iconFallback = "✪",  label = "LOGIST.", accentColor = new Color(0.70f, 0.62f, 0.42f), group = 2 },
        };

        // ── Internal state ────────────────────────────────────
        private NavPanel activePanel = NavPanel.None;
        private Dictionary<NavPanel, Button>     navButtons       = new Dictionary<NavPanel, Button>();
        private Dictionary<NavPanel, GameObject> panels           = new Dictionary<NavPanel, GameObject>();
        private Dictionary<NavPanel, Image>      buttonBgs        = new Dictionary<NavPanel, Image>();
        private Dictionary<NavPanel, bool>       panelWriteAccess = new Dictionary<NavPanel, bool>();
        private Dictionary<NavPanel, GameObject> indicators       = new Dictionary<NavPanel, GameObject>();
        private Dictionary<NavPanel, GameObject> glowEffects      = new Dictionary<NavPanel, GameObject>();
        private Dictionary<NavPanel, Image>      iconImages       = new Dictionary<NavPanel, Image>();
        private Dictionary<NavPanel, TextMeshProUGUI> iconFallbacks = new Dictionary<NavPanel, TextMeshProUGUI>();
        private Dictionary<NavPanel, TextMeshProUGUI> labelTexts  = new Dictionary<NavPanel, TextMeshProUGUI>();
        private Dictionary<NavPanel, GameObject> tabSlots         = new Dictionary<NavPanel, GameObject>();
        private Dictionary<NavPanel, CanvasGroup> tabCanvasGroups = new Dictionary<NavPanel, CanvasGroup>();

        // Colors
        private static readonly Color BgDark       = new Color(0.045f, 0.048f, 0.055f, 0.98f);
        private static readonly Color BgLight      = new Color(0.065f, 0.068f, 0.075f, 0.98f);
        private static readonly Color GoldGlow     = new Color(0.85f, 0.72f, 0.38f, 1f);
        private static readonly Color GoldDim      = new Color(0.55f, 0.47f, 0.28f, 0.5f);
        private static readonly Color TextSilver   = new Color(0.68f, 0.65f, 0.62f, 1f);
        private static readonly Color TextActive   = new Color(0.95f, 0.88f, 0.65f, 1f);
        private static readonly Color HoverBg      = new Color(1f, 1f, 1f, 0.06f);
        private static readonly Color ActiveBg     = new Color(0.85f, 0.72f, 0.38f, 0.12f);
        private static readonly Color DividerColor = new Color(0.35f, 0.30f, 0.22f, 0.20f);
        private static readonly Color LockedColor  = new Color(0.30f, 0.28f, 0.25f, 0.5f);

        // Panel creation callback (set by CampaignUI)
        public Action<NavPanel> onPanelRequested;

        /// <summary>Where overlay panels should be parented</summary>
        public RectTransform panelContainer;

        // ── Dimensions ────────────────────────────────────────
        private const float BarWidth      = 80f;
        private const float TopBarHeight  = 48f;
        private const float ButtonHeight  = 76f;
        private const float IconSize      = 36f;
        private const float LabelSize     = 11f;
        private const float IndicatorW    = 3f;

        // ══════════════════════════════════════════════════════
        //  INITIALIZATION
        // ══════════════════════════════════════════════════════

        public void Initialize(RectTransform container)
        {
            panelContainer = container;
            BuildNavBar();
            RefreshCoopAccess();
        }

        // ══════════════════════════════════════════════════════
        //  BUILD
        // ══════════════════════════════════════════════════════

        private void BuildNavBar()
        {
            // ── Main sidebar background ──
            GameObject barGO = new GameObject("NavBarBg");
            barGO.transform.SetParent(transform, false);
            RectTransform barRT = barGO.AddComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0, 0);
            barRT.anchorMax = new Vector2(0, 1);
            barRT.offsetMin = Vector2.zero;
            barRT.offsetMax = new Vector2(BarWidth, -TopBarHeight);

            // Clean dark background
            Image barBg = barGO.AddComponent<Image>();
            barBg.color = BgDark;
            barBg.raycastTarget = true;

            // ── Vertical layout ──
            VerticalLayoutGroup vlg = barGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2f;
            vlg.padding = new RectOffset(0, 0, 8, 8);
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;

            // ── Build tabs by group ──
            int lastGroup = -1;
            foreach (var tab in Tabs)
            {
                if (tab.group != lastGroup && lastGroup >= 0)
                    CreateDivider(barRT);
                lastGroup = tab.group;

                CreateTabButton(barRT, tab);
            }

            // ── Thin right border ──
            CreateRightBorder(barRT);
        }

        // ══════════════════════════════════════════════════════
        //  TAB BUTTON
        // ══════════════════════════════════════════════════════

        private void CreateTabButton(Transform parent, TabDef tab)
        {
            // Slot container
            GameObject slot = new GameObject("Tab_" + tab.panel);
            slot.transform.SetParent(parent, false);
            RectTransform slotRT = slot.AddComponent<RectTransform>();
            slotRT.sizeDelta = new Vector2(BarWidth, ButtonHeight);
            LayoutElement le = slot.AddComponent<LayoutElement>();
            le.preferredWidth = BarWidth;
            le.preferredHeight = ButtonHeight;

            // Canvas group for role-based fade
            CanvasGroup cg = slot.AddComponent<CanvasGroup>();
            tabCanvasGroups[tab.panel] = cg;

            // Background image (for hover/active state)
            Image bg = slot.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0);
            bg.raycastTarget = true;

            // Button component
            Button btn = slot.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.None;
            NavPanel captured = tab.panel;
            btn.onClick.AddListener(() => TogglePanel(captured));

            // ── Active indicator (sharp gold strip on left) ──
            GameObject indicator = new GameObject("Indicator");
            indicator.transform.SetParent(slot.transform, false);
            RectTransform indRT = indicator.AddComponent<RectTransform>();
            indRT.anchorMin = new Vector2(0, 0.1f);
            indRT.anchorMax = new Vector2(0, 0.9f);
            indRT.offsetMin = Vector2.zero;
            indRT.offsetMax = new Vector2(IndicatorW, 0);
            Image indImg = indicator.AddComponent<Image>();
            indImg.color = GoldGlow;
            indImg.raycastTarget = false;

            // Subtle glow behind indicator
            GameObject indGlow = new GameObject("IndicatorGlow");
            indGlow.transform.SetParent(slot.transform, false);
            RectTransform igRT = indGlow.AddComponent<RectTransform>();
            igRT.anchorMin = new Vector2(0, 0.05f);
            igRT.anchorMax = new Vector2(0, 0.95f);
            igRT.offsetMin = Vector2.zero;
            igRT.offsetMax = new Vector2(10, 0);
            Image igImg = indGlow.AddComponent<Image>();
            igImg.color = new Color(GoldGlow.r, GoldGlow.g, GoldGlow.b, 0.06f);
            igImg.raycastTarget = false;
            indGlow.SetActive(false);

            indicator.SetActive(false);
            indicators[tab.panel] = indicator;
            glowEffects[tab.panel] = indGlow;

            // ── Icon (centered in upper area of button) ──
            GameObject iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(slot.transform, false);
            RectTransform iconRT = iconGO.AddComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.5f, 0.5f);
            iconRT.anchorMax = new Vector2(0.5f, 0.5f);
            iconRT.pivot = new Vector2(0.5f, 0.5f);
            iconRT.sizeDelta = new Vector2(48, 48);
            iconRT.anchoredPosition = new Vector2(0, 8);

            // Try loading image sprite from Resources
            Texture2D iconTex = Resources.Load<Texture2D>(tab.iconResource);
            if (iconTex != null)
            {
                Image iconImg = iconGO.AddComponent<Image>();
                iconImg.sprite = Sprite.Create(iconTex, 
                    new Rect(0, 0, iconTex.width, iconTex.height), 
                    new Vector2(0.5f, 0.5f), 100f);
                iconImg.preserveAspect = true;
                iconImg.type = Image.Type.Simple;
                iconImg.color = Color.white;
                iconImg.raycastTarget = false;
                iconImages[tab.panel] = iconImg;
            }
            else
            {
                // Fallback to TMP text icon
                TextMeshProUGUI iconTMP = iconGO.AddComponent<TextMeshProUGUI>();
                iconTMP.text = tab.iconFallback;
                iconTMP.fontSize = IconSize;
                iconTMP.alignment = TextAlignmentOptions.Center;
                iconTMP.color = tab.accentColor;
                iconTMP.raycastTarget = false;
                iconFallbacks[tab.panel] = iconTMP;
            }

            // ── Label (below icon, clean TMP) ──
            GameObject labelGO = new GameObject("Label");
            labelGO.transform.SetParent(slot.transform, false);
            RectTransform labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0, 0);
            labelRT.anchorMax = new Vector2(1, 0);
            labelRT.pivot = new Vector2(0.5f, 0);
            labelRT.sizeDelta = new Vector2(0, 18);
            labelRT.anchoredPosition = new Vector2(0, 2);
            TextMeshProUGUI labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
            labelTMP.text = tab.label;
            labelTMP.fontSize = LabelSize;
            labelTMP.fontStyle = FontStyles.Bold;
            labelTMP.alignment = TextAlignmentOptions.Center;
            labelTMP.color = TextSilver;
            labelTMP.raycastTarget = false;
            labelTMP.enableWordWrapping = false;
            labelTexts[tab.panel] = labelTMP;

            // ── Hover detection ──
            NavButtonHover hover = slot.AddComponent<NavButtonHover>();
            hover.Init(bg, tab.accentColor);

            // Store references
            navButtons[tab.panel] = btn;
            buttonBgs[tab.panel]  = bg;
            tabSlots[tab.panel]   = slot;
        }

        // ══════════════════════════════════════════════════════
        //  VISUAL ELEMENTS
        // ══════════════════════════════════════════════════════

        private void CreateDivider(Transform parent)
        {
            GameObject div = new GameObject("Divider");
            div.transform.SetParent(parent, false);
            LayoutElement le = div.AddComponent<LayoutElement>();
            le.preferredHeight = 6;
            le.flexibleWidth = 1;

            // Thin centered line
            GameObject line = new GameObject("Line");
            line.transform.SetParent(div.transform, false);
            RectTransform lineRT = line.AddComponent<RectTransform>();
            lineRT.anchorMin = new Vector2(0.15f, 0.45f);
            lineRT.anchorMax = new Vector2(0.85f, 0.55f);
            lineRT.offsetMin = Vector2.zero;
            lineRT.offsetMax = Vector2.zero;
            Image img = line.AddComponent<Image>();
            img.color = DividerColor;
            img.raycastTarget = false;
        }

        private void CreateRightBorder(RectTransform parent)
        {
            GameObject border = new GameObject("RightBorder");
            border.transform.SetParent(parent, false);
            RectTransform rt = border.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(-1, 0);
            rt.offsetMax = Vector2.zero;
            Image img = border.AddComponent<Image>();
            img.color = new Color(0.35f, 0.30f, 0.22f, 0.30f);
            img.raycastTarget = false;
            LayoutElement le = border.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }

        // ══════════════════════════════════════════════════════
        //  PANEL MANAGEMENT
        // ══════════════════════════════════════════════════════

        public void TogglePanel(NavPanel panel)
        {
            // Check write access — if no access, don't open
            if (panelWriteAccess.ContainsKey(panel) && !panelWriteAccess[panel])
                return;

            if (activePanel == panel)
            {
                CloseActivePanel();
                return;
            }

            CloseActivePanel();
            activePanel = panel;
            UpdateButtonVisuals();
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
            UpdateButtonVisuals();
        }

        public void RegisterPanel(NavPanel panel, GameObject panelGO)
        {
            panels[panel] = panelGO;
            // Bring overlay to front so it renders above army panel, build menu, bottom bar, etc.
            panelGO.transform.SetAsLastSibling();
        }

        public NavPanel ActivePanel => activePanel;
        public bool IsAnyPanelOpen => activePanel != NavPanel.None;

        /// <summary>Check if the local player has write access to a panel</summary>
        public bool HasWriteAccess(NavPanel panel)
        {
            return !panelWriteAccess.ContainsKey(panel) || panelWriteAccess[panel];
        }

        // ══════════════════════════════════════════════════════
        //  BUTTON VISUALS UPDATE
        // ══════════════════════════════════════════════════════

        private void UpdateButtonVisuals()
        {
            foreach (var kvp in navButtons)
            {
                NavPanel panel = kvp.Key;
                bool isActive = panel == activePanel;
                bool hasAccess = HasWriteAccess(panel);

                // Background
                if (buttonBgs.ContainsKey(panel))
                    buttonBgs[panel].color = isActive ? ActiveBg : new Color(0, 0, 0, 0);

                // Active indicator (gold strip)
                if (indicators.ContainsKey(panel))
                    indicators[panel].SetActive(isActive);
                if (glowEffects.ContainsKey(panel))
                    glowEffects[panel].SetActive(isActive);

                // Icon color/tint
                if (iconImages.ContainsKey(panel))
                {
                    // Image-based icon — tint
                    if (!hasAccess)
                        iconImages[panel].color = LockedColor;
                    else if (isActive)
                        iconImages[panel].color = Color.white;
                    else
                        iconImages[panel].color = new Color(0.85f, 0.85f, 0.85f, 0.9f); // Slightly muted when not active
                }
                else if (iconFallbacks.ContainsKey(panel))
                {
                    // Text fallback icon
                    Color baseColor = GetTabAccentColor(panel);
                    if (!hasAccess)
                        iconFallbacks[panel].color = LockedColor;
                    else if (isActive)
                        iconFallbacks[panel].color = Color.white;
                    else
                        iconFallbacks[panel].color = baseColor;
                }

                // Label color
                if (labelTexts.ContainsKey(panel))
                {
                    if (!hasAccess)
                        labelTexts[panel].color = new Color(0.30f, 0.28f, 0.25f, 0.4f);
                    else if (isActive)
                        labelTexts[panel].color = TextActive;
                    else
                        labelTexts[panel].color = TextSilver;
                }
            }
        }

        private Color GetTabAccentColor(NavPanel panel)
        {
            foreach (var tab in Tabs)
                if (tab.panel == panel) return tab.accentColor;
            return TextSilver;
        }

        // ══════════════════════════════════════════════════════
        //  CO-OP ROLE FILTERING
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Refresh tab visibility and access based on co-op role.
        /// Tabs the player cannot control are hidden entirely (not just dimmed).
        /// In solo mode, all tabs are visible.
        /// </summary>
        public void RefreshCoopAccess()
        {
            bool isCoop = CoopRoleManager.Instance != null && CoopRoleManager.Instance.IsCoopMode;

            if (!isCoop)
            {
                // Solo mode: all tabs visible and accessible
                foreach (NavPanel p in Enum.GetValues(typeof(NavPanel)))
                {
                    panelWriteAccess[p] = true;
                    if (tabSlots.ContainsKey(p))
                    {
                        tabSlots[p].SetActive(true);
                        if (tabCanvasGroups.ContainsKey(p))
                            tabCanvasGroups[p].alpha = 1f;
                    }
                }
                UpdateButtonVisuals();
                return;
            }

            CoopRole role = CoopRoleManager.Instance.GetLocalRole();

            foreach (var kvp in navButtons)
            {
                NavPanel panel = kvp.Key;
                string panelName = panel.ToString();
                bool canControl = CoopRoleManager.CanControlPanel(role, panelName);
                panelWriteAccess[panel] = canControl;

                // Hide tabs the player cannot control
                if (tabSlots.ContainsKey(panel))
                {
                    tabSlots[panel].SetActive(canControl);
                }
            }

            UpdateButtonVisuals();
        }

        // ══════════════════════════════════════════════════════
        //  OVERLAY PANEL FACTORY
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Create a standard full-screen overlay panel with premium header, 
        /// close button, and content area.
        /// </summary>
        public GameObject CreateOverlayPanel(string title)
        {
            if (panelContainer == null) return null;

            // ── Full overlay ──
            GameObject overlay = new GameObject("Panel_" + title);
            overlay.transform.SetParent(panelContainer, false);
            RectTransform overlayRT = overlay.AddComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.offsetMin = new Vector2(BarWidth, 0);
            overlayRT.offsetMax = new Vector2(0, -TopBarHeight);

            // Semi-transparent dark background with slight blue tint
            Image bg = overlay.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.045f, 0.06f, 0.96f);
            bg.raycastTarget = true;

            // ── Vertical layout ──
            VerticalLayoutGroup vlg = overlay.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 0;
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;

            // ── Header banner with gold accent ──
            GameObject header = CreatePremiumHeader(overlay.transform, title);

            // ── Close button ──
            GameObject closeBtnGO = new GameObject("CloseBtn");
            closeBtnGO.transform.SetParent(header.transform, false);
            RectTransform closeRT = closeBtnGO.AddComponent<RectTransform>();
            closeRT.anchorMin = new Vector2(1, 0);
            closeRT.anchorMax = new Vector2(1, 1);
            closeRT.offsetMin = new Vector2(-44, 4);
            closeRT.offsetMax = new Vector2(-4, -4);

            Image closeBg = closeBtnGO.AddComponent<Image>();
            closeBg.color = new Color(0.8f, 0.2f, 0.2f, 0.3f);
            closeBg.raycastTarget = true;

            Button closeBtn = closeBtnGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            ColorBlock ccb = closeBtn.colors;
            ccb.normalColor = new Color(1, 1, 1, 0.6f);
            ccb.highlightedColor = new Color(1, 0.3f, 0.3f, 1f);
            ccb.pressedColor = new Color(0.8f, 0.1f, 0.1f, 1f);
            closeBtn.colors = ccb;
            closeBtn.onClick.AddListener(CloseActivePanel);

            GameObject closeTextGO = new GameObject("X");
            closeTextGO.transform.SetParent(closeBtnGO.transform, false);
            RectTransform closeTextRT = closeTextGO.AddComponent<RectTransform>();
            closeTextRT.anchorMin = Vector2.zero;
            closeTextRT.anchorMax = Vector2.one;
            closeTextRT.offsetMin = Vector2.zero;
            closeTextRT.offsetMax = Vector2.zero;
            TextMeshProUGUI closeTMP = closeTextGO.AddComponent<TextMeshProUGUI>();
            closeTMP.text = "✕";
            closeTMP.fontSize = 18;
            closeTMP.alignment = TextAlignmentOptions.Center;
            closeTMP.color = new Color(0.9f, 0.85f, 0.8f);
            closeTMP.raycastTarget = false;

            // Set layout element to ignore layout for close button
            LayoutElement closeLe = closeBtnGO.AddComponent<LayoutElement>();
            closeLe.ignoreLayout = true;

            return overlay;
        }

        private GameObject CreatePremiumHeader(Transform parent, string title)
        {
            GameObject header = new GameObject("Header");
            header.transform.SetParent(parent, false);
            RectTransform headerRT = header.AddComponent<RectTransform>();
            LayoutElement headerLE = header.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 48;
            headerLE.flexibleWidth = 1;

            // Header background — dark with gold bottom accent
            Image headerBg = header.AddComponent<Image>();
            headerBg.color = new Color(0.06f, 0.065f, 0.08f, 1f);
            headerBg.raycastTarget = false;

            // Gold bottom line
            GameObject goldLine = new GameObject("GoldLine");
            goldLine.transform.SetParent(header.transform, false);
            RectTransform glRT = goldLine.AddComponent<RectTransform>();
            glRT.anchorMin = new Vector2(0, 0);
            glRT.anchorMax = new Vector2(1, 0);
            glRT.offsetMin = new Vector2(12, 0);
            glRT.offsetMax = new Vector2(-12, 2);
            Image glImg = goldLine.AddComponent<Image>();
            glImg.color = new Color(GoldGlow.r, GoldGlow.g, GoldGlow.b, 0.45f);
            glImg.raycastTarget = false;
            LayoutElement glLE = goldLine.AddComponent<LayoutElement>();
            glLE.ignoreLayout = true;

            // Title text (TMP)
            GameObject titleGO = new GameObject("Title");
            titleGO.transform.SetParent(header.transform, false);
            RectTransform ttRT = titleGO.AddComponent<RectTransform>();
            ttRT.anchorMin = Vector2.zero;
            ttRT.anchorMax = Vector2.one;
            ttRT.offsetMin = new Vector2(16, 0);
            ttRT.offsetMax = new Vector2(-50, 0);
            TextMeshProUGUI titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
            titleTMP.text = title.ToUpper();
            titleTMP.fontSize = 18;
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.alignment = TextAlignmentOptions.MidlineLeft;
            titleTMP.color = new Color(0.90f, 0.85f, 0.72f);
            titleTMP.raycastTarget = false;
            LayoutElement ttLE = titleGO.AddComponent<LayoutElement>();
            ttLE.ignoreLayout = true;

            return header;
        }
    }

    // ══════════════════════════════════════════════════════════
    //  HOVER EFFECT COMPONENT
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Smooth hover effect for navigation buttons.
    /// Adds a subtle glow and background shift on mouse enter/exit.
    /// </summary>
    public class NavButtonHover : MonoBehaviour, 
        UnityEngine.EventSystems.IPointerEnterHandler, 
        UnityEngine.EventSystems.IPointerExitHandler
    {
        private Image bg;
        private Color hoverColor;
        private Color normalColor = new Color(0, 0, 0, 0);
        private float targetAlpha = 0f;
        private float currentAlpha = 0f;

        public void Init(Image background, Color accent)
        {
            bg = background;
            hoverColor = new Color(accent.r * 0.3f, accent.g * 0.3f, accent.b * 0.3f, 0.12f);
        }

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
        {
            targetAlpha = 1f;
        }

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
        {
            targetAlpha = 0f;
        }

        private void Update()
        {
            if (bg == null) return;

            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Time.unscaledDeltaTime * 6f);
            
            // Only modify if not actively selected (active panels handled elsewhere)
            NavigationBar nav = GetComponentInParent<NavigationBar>();
            if (nav != null && nav.ActivePanel != NavigationBar.NavPanel.None)
            {
                // Check if THIS button's panel is the active one
                // If so, don't override the active highlight
                string myName = gameObject.name;
                if (myName.Contains(nav.ActivePanel.ToString())) return;
            }

            bg.color = Color.Lerp(normalColor, hoverColor, currentAlpha);
        }
    }
}
