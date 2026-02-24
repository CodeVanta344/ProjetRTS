using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace NapoleonicWars.UI
{
    public static class UIFactory
    {
        // === PRESTIGE 4X COLOR PALETTE — Napoleonic Era Aesthetic ===
        
        // Accents
        public static readonly Color EmpireGold = new Color(0.77f, 0.63f, 0.35f);    // Imperial Gold
        public static readonly Color BrightGold = new Color(0.92f, 0.82f, 0.55f);
        public static readonly Color MutedGold = new Color(0.55f, 0.48f, 0.35f);
        public static readonly Color ImperialCrimson = new Color(0.61f, 0.11f, 0.11f); // Deep Napoleonic Red
        
        // Backgrounds
        public static readonly Color DeepCharcoal = new Color(0.08f, 0.09f, 0.10f, 0.98f);
        public static readonly Color PanelSurface = new Color(0.12f, 0.13f, 0.14f, 0.97f);
        public static readonly Color HeaderSurface = new Color(0.16f, 0.17f, 0.18f, 1.0f);
        public static readonly Color GlassOverlay = new Color(0.05f, 0.05f, 0.06f, 0.85f);
        
        // Interactive
        public static readonly Color ActionGreen = new Color(0.28f, 0.48f, 0.22f);
        public static readonly Color WarningAmber = new Color(0.82f, 0.58f, 0.15f);
        
        // Text
        public static readonly Color Porcelain = new Color(0.95f, 0.94f, 0.92f);
        public static readonly Color SilverText = new Color(0.75f, 0.76f, 0.78f);
        public static readonly Color Parchment = new Color(0.85f, 0.80f, 0.70f);
        public static readonly Color GoldText = EmpireGold;

        // Legacy compatibility aliases
        public static readonly Color GoldAccent = EmpireGold;
        public static readonly Color TextWhite = Porcelain;
        public static readonly Color TextGrey = SilverText;
        public static readonly Color DarkBg = DeepCharcoal;
        public static readonly Color PanelBg = PanelSurface;
        public static readonly Color HeaderBg = HeaderSurface;
        public static readonly Color AlertRed = ImperialCrimson;
        public static readonly Color ParchmentBeige = Parchment;
        public static readonly Color TextGold = EmpireGold;
        public static readonly Color BorderGold = MutedGold;
        public static readonly Color BorderGoldBright = BrightGold;
        public static readonly Color CrimsonDeep = ImperialCrimson;
        public static readonly Color CrimsonGlow = new Color(0.85f, 0.20f, 0.15f);
        public static readonly Color DarkStone = new Color(0.18f, 0.17f, 0.16f);
        public static readonly Color BronzeHighlight = new Color(0.72f, 0.53f, 0.26f);
        public static readonly Color FranceBlue = new Color(0.20f, 0.30f, 0.65f);
        public static readonly Color BritainRed = new Color(0.75f, 0.15f, 0.15f);

        // Button state colors
        public static readonly Color ButtonNormal = DeepCharcoal;
        public static readonly Color ButtonActive = new Color(0.28f, 0.26f, 0.18f);

        // === CANVAS ===
        public static Canvas CreateCanvas(string name, int sortOrder = 0)
        {
            GameObject go = new GameObject(name);
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.referencePixelsPerUnit = 100;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static Canvas CreateResponsiveCanvas(string name, int sortOrder = 0, float matchMode = 0.5f)
        {
            GameObject go = new GameObject(name);
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = matchMode;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.referencePixelsPerUnit = 100;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        // === PANEL ===
        public static RectTransform CreatePanel(Transform parent, string name, Color bgColor)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            Image img = go.AddComponent<Image>();
            img.color = bgColor;
            img.raycastTarget = true;
            return rt;
        }

        public static RectTransform CreateBorderedPanel(Transform parent, string name, Color bgColor, Color borderColor, float borderWidth = 1f)
        {
            GameObject borderGO = new GameObject(name);
            borderGO.transform.SetParent(parent, false);
            RectTransform borderRT = borderGO.AddComponent<RectTransform>();
            Image borderImg = borderGO.AddComponent<Image>();
            borderImg.color = borderColor;

            GameObject innerGO = new GameObject("Inner");
            innerGO.transform.SetParent(borderGO.transform, false);
            RectTransform innerRT = innerGO.AddComponent<RectTransform>();
            innerRT.anchorMin = Vector2.zero;
            innerRT.anchorMax = Vector2.one;
            innerRT.offsetMin = new Vector2(borderWidth, borderWidth);
            innerRT.offsetMax = new Vector2(-borderWidth, -borderWidth);
            Image innerImg = innerGO.AddComponent<Image>();
            innerImg.color = bgColor;

            return borderRT;
        }
        
        /// <summary>
        /// Modern prestige panel with layered borders and inner content area.
        /// The inner content area is named "Inner" and can be accessed via transform.Find("Inner").
        /// </summary>
        public static RectTransform CreateOrnatePanel(Transform parent, string name, Color bgColor, Vector2? size = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            if (size.HasValue) rect.sizeDelta = size.Value;

            // 1. Ornate Border (Muted Gold)
            Image borderImg = go.AddComponent<Image>();
            borderImg.color = MutedGold;

            // 2. Main Surface
            GameObject surface = new GameObject("Surface");
            surface.transform.SetParent(go.transform, false);
            RectTransform sRect = surface.AddComponent<RectTransform>();
            sRect.anchorMin = Vector2.zero; sRect.anchorMax = Vector2.one;
            sRect.offsetMin = new Vector2(1, 1); sRect.offsetMax = new Vector2(-1, -1);
            Image sImg = surface.AddComponent<Image>();
            sImg.color = bgColor;

            // 3. Inner Design Frame
            GameObject design = new GameObject("DesignFrame");
            design.transform.SetParent(surface.transform, false);
            RectTransform dRect = design.AddComponent<RectTransform>();
            dRect.anchorMin = Vector2.zero; dRect.anchorMax = Vector2.one;
            dRect.offsetMin = new Vector2(2, 2); dRect.offsetMax = new Vector2(-2, -2);
            Outline outline = design.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.35f);
            outline.effectDistance = new Vector2(1, 1);

            // 4. Content Area — parented directly to root so Find("Inner") works
            GameObject innerGO = new GameObject("Inner");
            innerGO.transform.SetParent(go.transform, false);
            RectTransform innerRT = innerGO.AddComponent<RectTransform>();
            innerRT.anchorMin = Vector2.zero; innerRT.anchorMax = Vector2.one;
            innerRT.offsetMin = new Vector2(5, 5); innerRT.offsetMax = new Vector2(-5, -5);

            return rect;
        }

        // === TEXT ===
        public static Text CreateText(Transform parent, string name, string content,
            int fontSize = 14, TextAnchor alignment = TextAnchor.MiddleLeft, Color? color = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            Text text = go.AddComponent<Text>();
            text.text = content;
            text.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color ?? Porcelain;
            text.raycastTarget = false;
            text.supportRichText = true;

            return text;
        }

        // === BUTTON — Prestige Style ===
        public static Button CreateButton(Transform parent, string name, string label,
            int fontSize = 14, UnityAction onClick = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(160, 36);

            Image img = go.AddComponent<Image>();
            img.color = DeepCharcoal;
            
            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = MutedGold;
            outline.effectDistance = new Vector2(1, 1);

            Button btn = go.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.2f, 1.15f, 1.05f);
            cb.pressedColor = EmpireGold;
            cb.selectedColor = Color.white;
            cb.fadeDuration = 0.1f;
            btn.colors = cb;

            if (onClick != null)
                btn.onClick.AddListener(onClick);

            Text t = CreateText(go.transform, "Label", label, fontSize, TextAnchor.MiddleCenter, Porcelain);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            // Stretch label to fill button
            RectTransform labelRT = t.GetComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;

            return btn;
        }

        public static Button CreateGoldButton(Transform parent, string name, string label,
            int fontSize = 14, UnityAction onClick = null)
        {
            Button btn = CreateButton(parent, name, label, fontSize, onClick);
            
            Image img = btn.GetComponent<Image>();
            img.color = ImperialCrimson;
            
            Outline outline = btn.GetComponent<Outline>();
            if (outline != null) outline.effectColor = EmpireGold;

            return btn;
        }
        
        public static Button CreateWarhammerButton(Transform parent, string name, string label,
            int fontSize = 18, UnityAction onClick = null)
        {
            return CreateGoldButton(parent, name, label, fontSize, onClick);
        }
        
        // === SECTION HEADER — Imperial Style ===
        public static RectTransform CreateBannerHeader(Transform parent, string name, string title, int fontSize = 20)
        {
            RectTransform container = CreatePanel(parent, name, HeaderSurface);
            
            // Bottom gold line
            RectTransform line = CreatePanel(container, "GoldLine", EmpireGold);
            line.anchorMin = new Vector2(0, 0);
            line.anchorMax = new Vector2(1, 0);
            line.offsetMin = Vector2.zero;
            line.offsetMax = new Vector2(0, 2);
            
            // Title (centered and uppercase for authority)
            Text text = CreateText(container, "Title", title.ToUpper(), fontSize, TextAnchor.MiddleCenter, EmpireGold);
            text.fontStyle = FontStyle.Bold;
            SetAnchors(text.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            
            return container;
        }
        
        public static Text CreateSectionHeader(Transform parent, string name, string title, int fontSize = 15)
        {
            GameObject container = new GameObject(name);
            container.transform.SetParent(parent, false);
            RectTransform rect = container.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 32);

            Image img = container.AddComponent<Image>();
            img.color = GlassOverlay;

            Text text = CreateText(container.transform, "Label", title.ToUpper(), fontSize, TextAnchor.MiddleLeft, Parchment);
            text.fontStyle = FontStyle.Bold;
            SetAnchors(text.gameObject, Vector2.zero, Vector2.one, new Vector2(12, 0), new Vector2(-12, 0));

            return text;
        }
        
        public static RectTransform CreateOrnamentalSeparator(Transform parent)
        {
            return CreatePanel(parent, "Separator", new Color(0.55f, 0.48f, 0.35f, 0.4f));
        }

        // === INPUT FIELD ===
        public static InputField CreateInputField(Transform parent, string name, string placeholder,
            string defaultValue = "", int fontSize = 14)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            Image img = go.AddComponent<Image>();
            img.color = DeepCharcoal;

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = MutedGold;
            outline.effectDistance = new Vector2(1f, 1f);

            InputField input = go.AddComponent<InputField>();

            Text text = CreateText(go.transform, "Text", defaultValue, fontSize, TextAnchor.MiddleLeft, Porcelain);
            RectTransform textRT = text.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(8f, 2f);
            textRT.offsetMax = new Vector2(-8f, -2f);
            input.textComponent = text;
            input.text = defaultValue;

            Text ph = CreateText(go.transform, "Placeholder", placeholder, fontSize, TextAnchor.MiddleLeft, SilverText);
            RectTransform phRT = ph.GetComponent<RectTransform>();
            phRT.anchorMin = Vector2.zero;
            phRT.anchorMax = Vector2.one;
            phRT.offsetMin = new Vector2(8f, 2f);
            phRT.offsetMax = new Vector2(-8f, -2f);
            input.placeholder = ph;

            return input;
        }

        // === PROGRESS BAR — Imperial Style ===
        public static (Image bg, Image fill) CreateProgressBar(Transform parent, string name,
            Color fillColor, Color bgColor = default)
        {
            if (bgColor == default) bgColor = DeepCharcoal;

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 16);

            Image bgImg = go.AddComponent<Image>();
            bgImg.color = bgColor;
            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.25f, 0.15f, 0.5f);

            GameObject fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(go.transform, false);
            RectTransform fillRT = fillGO.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = new Vector2(1f, 1f);
            fillRT.offsetMin = new Vector2(2, 2);
            fillRT.offsetMax = new Vector2(-2, -2);
            fillRT.pivot = new Vector2(0f, 0.5f);

            Image fillImg = fillGO.AddComponent<Image>();
            fillImg.color = fillColor;

            return (bgImg, fillImg);
        }

        public static RectTransform CreateSeparator(Transform parent, Color? color = null)
        {
            return CreatePanel(parent, "Separator", color ?? new Color(0.55f, 0.48f, 0.35f, 0.25f));
        }

        // === SCROLL VIEW ===
        public static (ScrollRect scroll, RectTransform content) CreateScrollView(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            Image img = go.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.05f);

            ScrollRect scroll = go.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 60f;

            // Viewport
            GameObject viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(go.transform, false);
            RectTransform viewportRT = viewportGO.AddComponent<RectTransform>();
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.offsetMin = Vector2.zero;
            viewportRT.offsetMax = Vector2.zero;
            Image vpImg = viewportGO.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0.01f);
            viewportGO.AddComponent<Mask>().showMaskGraphic = false;
            scroll.viewport = viewportRT;

            // Content
            GameObject contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform, false);
            RectTransform contentRT = contentGO.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.offsetMin = Vector2.zero;
            contentRT.offsetMax = Vector2.zero;

            VerticalLayoutGroup vlg = contentGO.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 2f;
            vlg.padding = new RectOffset(4, 4, 4, 4);

            ContentSizeFitter csf = contentGO.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = contentRT;

            return (scroll, contentRT);
        }

        // === LAYOUT HELPERS ===
        public static VerticalLayoutGroup AddVerticalLayout(GameObject go, float spacing = 4f,
            RectOffset padding = null)
        {
            VerticalLayoutGroup vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = spacing;
            vlg.padding = padding ?? new RectOffset(8, 8, 8, 8);
            return vlg;
        }

        public static HorizontalLayoutGroup AddHorizontalLayout(GameObject go, float spacing = 4f,
            RectOffset padding = null)
        {
            HorizontalLayoutGroup hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.spacing = spacing;
            hlg.padding = padding ?? new RectOffset(12, 12, 4, 4);
            return hlg;
        }

        public static LayoutElement AddLayoutElement(GameObject go, float preferredHeight = -1,
            float preferredWidth = -1, float minHeight = -1, float flexibleWidth = -1, float flexibleHeight = -1)
        {
            LayoutElement le = go.AddComponent<LayoutElement>();
            if (preferredHeight >= 0) le.preferredHeight = preferredHeight;
            if (preferredWidth >= 0) le.preferredWidth = preferredWidth;
            if (minHeight >= 0) le.minHeight = minHeight;
            if (flexibleWidth >= 0) le.flexibleWidth = flexibleWidth;
            if (flexibleHeight >= 0) le.flexibleHeight = flexibleHeight;
            return le;
        }

        public static RawImage CreateRawImage(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            RawImage ri = go.AddComponent<RawImage>();
            return ri;
        }

        public static void SetAnchors(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        public static void SetAnchors(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        // === PREMIUM VISUAL HELPERS ===

        // Premium color additions
        public static readonly Color GradientTop = new Color(0.06f, 0.065f, 0.07f, 0.99f);
        public static readonly Color GradientBottom = new Color(0.10f, 0.105f, 0.11f, 0.99f);
        public static readonly Color GlowGold = new Color(0.85f, 0.72f, 0.35f, 0.25f);
        public static readonly Color PremiumSurface = new Color(0.11f, 0.115f, 0.12f, 0.98f);
        public static readonly Color InnerGlow = new Color(0.77f, 0.63f, 0.35f, 0.08f);
        public static readonly Color SubtleBorder = new Color(0.25f, 0.23f, 0.20f, 0.6f);

        /// <summary>
        /// Creates a panel with a simulated vertical gradient using stacked layers.
        /// Gives premium depth to bars and panels.
        /// </summary>
        public static RectTransform CreateGradientPanel(Transform parent, string name, 
            Color topColor, Color bottomColor, Color? midColor = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();

            // Base layer (bottom color)
            Image baseImg = go.AddComponent<Image>();
            baseImg.color = bottomColor;
            baseImg.raycastTarget = true;

            // Top gradient overlay (top half, fading down)
            GameObject topOverlay = new GameObject("GradTop");
            topOverlay.transform.SetParent(go.transform, false);
            RectTransform topRT = topOverlay.AddComponent<RectTransform>();
            topRT.anchorMin = new Vector2(0, 0.35f);
            topRT.anchorMax = Vector2.one;
            topRT.offsetMin = Vector2.zero;
            topRT.offsetMax = Vector2.zero;
            Image topImg = topOverlay.AddComponent<Image>();
            topImg.color = topColor;
            topImg.raycastTarget = false;
            LayoutElement topLE = topOverlay.AddComponent<LayoutElement>();
            topLE.ignoreLayout = true;

            // Inner subtle glow (center strip for depth)
            if (midColor.HasValue)
            {
                GameObject midGlow = new GameObject("MidGlow");
                midGlow.transform.SetParent(go.transform, false);
                RectTransform midRT = midGlow.AddComponent<RectTransform>();
                midRT.anchorMin = new Vector2(0, 0.25f);
                midRT.anchorMax = new Vector2(1, 0.75f);
                midRT.offsetMin = Vector2.zero;
                midRT.offsetMax = Vector2.zero;
                Image midImg = midGlow.AddComponent<Image>();
                midImg.color = midColor.Value;
                midImg.raycastTarget = false;
                LayoutElement midLE = midGlow.AddComponent<LayoutElement>();
                midLE.ignoreLayout = true;
            }

            return rt;
        }

        /// <summary>
        /// Creates a glowing gold separator — a thin crisp line with a wider semi-transparent glow behind it.
        /// Much richer than a plain 2px line.
        /// </summary>
        public static RectTransform CreateGlowSeparator(Transform parent, string name, 
            bool vertical = true, Color? glowColor = null, Color? lineColor = null)
        {
            Color glow = glowColor ?? GlowGold;
            Color line = lineColor ?? EmpireGold;

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();

            if (vertical)
            {
                // Outer glow (wide, semi-transparent)
                Image glowImg = go.AddComponent<Image>();
                glowImg.color = glow;
                glowImg.raycastTarget = false;

                // Inner crisp line
                GameObject lineGO = new GameObject("Line");
                lineGO.transform.SetParent(go.transform, false);
                RectTransform lineRT = lineGO.AddComponent<RectTransform>();
                lineRT.anchorMin = new Vector2(0.3f, 0.1f);
                lineRT.anchorMax = new Vector2(0.7f, 0.9f);
                lineRT.offsetMin = Vector2.zero;
                lineRT.offsetMax = Vector2.zero;
                Image lineImg = lineGO.AddComponent<Image>();
                lineImg.color = line;
                lineImg.raycastTarget = false;
            }
            else
            {
                // Horizontal glow
                Image glowImg = go.AddComponent<Image>();
                glowImg.color = glow;
                glowImg.raycastTarget = false;

                GameObject lineGO = new GameObject("Line");
                lineGO.transform.SetParent(go.transform, false);
                RectTransform lineRT = lineGO.AddComponent<RectTransform>();
                lineRT.anchorMin = new Vector2(0.05f, 0.3f);
                lineRT.anchorMax = new Vector2(0.95f, 0.7f);
                lineRT.offsetMin = Vector2.zero;
                lineRT.offsetMax = Vector2.zero;
                Image lineImg = lineGO.AddComponent<Image>();
                lineImg.color = line;
                lineImg.raycastTarget = false;
            }

            return rt;
        }

        /// <summary>
        /// Creates a colored icon badge — a small rounded-looking colored background behind an icon.
        /// Makes resource icons immediately recognizable with their own color identity.
        /// </summary>
        public static (GameObject container, Text iconText, Text valueText) CreateIconBadge(
            Transform parent, string name, string icon, string value, 
            Color badgeColor, Color textColor, float containerWidth = 150f)
        {
            // Container
            GameObject container = new GameObject(name);
            container.transform.SetParent(parent, false);
            RectTransform containerRT = container.AddComponent<RectTransform>();
            AddLayoutElement(container, preferredWidth: containerWidth, preferredHeight: 44);

            HorizontalLayoutGroup hlg = AddHorizontalLayout(container, 5f, new RectOffset(4, 6, 4, 4));
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            // Icon badge background
            GameObject badgeBg = new GameObject("BadgeBg");
            badgeBg.transform.SetParent(container.transform, false);
            RectTransform badgeBgRT = badgeBg.AddComponent<RectTransform>();
            AddLayoutElement(badgeBg, preferredWidth: 36, preferredHeight: 36);

            // Outer border (subtle)
            Image borderImg = badgeBg.AddComponent<Image>();
            borderImg.color = new Color(badgeColor.r * 0.6f, badgeColor.g * 0.6f, badgeColor.b * 0.6f, 0.5f);
            borderImg.raycastTarget = false;

            // Inner colored fill
            GameObject badgeInner = new GameObject("BadgeInner");
            badgeInner.transform.SetParent(badgeBg.transform, false);
            RectTransform innerRT = badgeInner.AddComponent<RectTransform>();
            innerRT.anchorMin = Vector2.zero;
            innerRT.anchorMax = Vector2.one;
            innerRT.offsetMin = new Vector2(1, 1);
            innerRT.offsetMax = new Vector2(-1, -1);
            Image innerImg = badgeInner.AddComponent<Image>();
            innerImg.color = new Color(badgeColor.r * 0.25f, badgeColor.g * 0.25f, badgeColor.b * 0.25f, 0.85f);
            innerImg.raycastTarget = false;

            // Icon text centered on badge
            Text iconText = CreateText(badgeInner.transform, "Icon", icon, 18, TextAnchor.MiddleCenter, badgeColor);
            iconText.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform iconRT = iconText.GetComponent<RectTransform>();
            iconRT.anchorMin = Vector2.zero;
            iconRT.anchorMax = Vector2.one;
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = Vector2.zero;

            // Value text
            Text valueText = CreateText(container.transform, "Value", value, 14, TextAnchor.MiddleLeft, textColor);
            valueText.fontStyle = FontStyle.Bold;
            valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
            AddLayoutElement(valueText.gameObject, preferredWidth: containerWidth - 46, preferredHeight: 44);

            // Subtle shadow on value text for readability
            Shadow shadow = valueText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.6f);
            shadow.effectDistance = new Vector2(1f, -1f);

            return (container, iconText, valueText);
        }

        /// <summary>
        /// Creates a premium inset container — a small panel with a subtle double border effect.
        /// Used for grouping related items in the top/bottom bars.
        /// </summary>
        public static RectTransform CreateInsetContainer(Transform parent, string name, Color? bgColor = null)
        {
            Color bg = bgColor ?? new Color(0.07f, 0.075f, 0.08f, 0.7f);

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();

            // Outer subtle border
            Image borderImg = go.AddComponent<Image>();
            borderImg.color = SubtleBorder;
            borderImg.raycastTarget = false;

            // Inner fill
            GameObject inner = new GameObject("Inner");
            inner.transform.SetParent(go.transform, false);
            RectTransform innerRT = inner.AddComponent<RectTransform>();
            innerRT.anchorMin = Vector2.zero;
            innerRT.anchorMax = Vector2.one;
            innerRT.offsetMin = new Vector2(1, 1);
            innerRT.offsetMax = new Vector2(-1, -1);
            Image innerImg = inner.AddComponent<Image>();
            innerImg.color = bg;
            innerImg.raycastTarget = false;

            return rt;
        }

        /// <summary>
        /// Enhanced progress bar with glow effect and inset border.
        /// </summary>
        public static (Image bg, Image fill, Image glow) CreatePremiumProgressBar(Transform parent, string name,
            Color fillColor, float width = 200f, float height = 14f)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);

            // Outer border
            Image borderImg = go.AddComponent<Image>();
            borderImg.color = SubtleBorder;

            // Background inset
            GameObject bgGO = new GameObject("Bg");
            bgGO.transform.SetParent(go.transform, false);
            RectTransform bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = new Vector2(1, 1);
            bgRT.offsetMax = new Vector2(-1, -1);
            Image bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.04f, 0.04f, 0.05f, 0.9f);

            // Fill
            GameObject fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(bgGO.transform, false);
            RectTransform fillRT = fillGO.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = new Vector2(1f, 1f);
            fillRT.offsetMin = new Vector2(1, 1);
            fillRT.offsetMax = new Vector2(-1, -1);
            fillRT.pivot = new Vector2(0f, 0.5f);
            Image fillImg = fillGO.AddComponent<Image>();
            fillImg.color = fillColor;

            // Glow overlay on fill (lighter version, half-height centered)
            GameObject glowGO = new GameObject("Glow");
            glowGO.transform.SetParent(fillGO.transform, false);
            RectTransform glowRT = glowGO.AddComponent<RectTransform>();
            glowRT.anchorMin = new Vector2(0, 0.2f);
            glowRT.anchorMax = new Vector2(1, 0.6f);
            glowRT.offsetMin = Vector2.zero;
            glowRT.offsetMax = Vector2.zero;
            Image glowImg = glowGO.AddComponent<Image>();
            glowImg.color = new Color(1f, 1f, 1f, 0.2f);
            glowImg.raycastTarget = false;

            return (bgImg, fillImg, glowImg);
        }

        // === INSET CONTAINER (bordered inset panel) ===
        public static RectTransform CreateInsetContainer(Transform parent, string name, Color bgColor, Color borderColor)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            Image bgImg = go.AddComponent<Image>();
            bgImg.color = bgColor;

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = borderColor;
            outline.effectDistance = new Vector2(1, 1);

            return rt;
        }
    }
}
