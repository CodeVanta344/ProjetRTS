using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

namespace NapoleonicWars.UI
{
    public static class UIFactory
    {
        // ════════════════════════════════════════════════════════════
        //  AAA COLOR PALETTE — Dark Imperial Napoleonic Aesthetic
        //  Inspired by Total War, Victoria 3, Crusader Kings 3
        // ════════════════════════════════════════════════════════════
        
        // === PRIMARY ACCENTS ===
        public static readonly Color EmpireGold      = new Color(0.85f, 0.72f, 0.38f);
        public static readonly Color BrightGold      = new Color(0.95f, 0.85f, 0.55f);
        public static readonly Color MutedGold       = new Color(0.50f, 0.42f, 0.28f);
        public static readonly Color ImperialCrimson = new Color(0.65f, 0.12f, 0.12f);
        
        // === BACKGROUND LAYERS (darkest → lightest) ===
        public static readonly Color DeepCharcoal    = new Color(0.06f, 0.065f, 0.07f, 0.98f);
        public static readonly Color PanelSurface    = new Color(0.09f, 0.095f, 0.10f, 0.97f);
        public static readonly Color HeaderSurface   = new Color(0.11f, 0.115f, 0.12f, 1.0f);
        public static readonly Color GlassOverlay    = new Color(0.04f, 0.04f, 0.05f, 0.88f);
        public static readonly Color CardSurface     = new Color(0.08f, 0.082f, 0.09f, 0.95f);
        public static readonly Color ElevatedSurface = new Color(0.13f, 0.135f, 0.14f, 0.96f);
        
        // === INTERACTIVE ===
        public static readonly Color ActionGreen     = new Color(0.30f, 0.55f, 0.25f);
        public static readonly Color WarningAmber    = new Color(0.85f, 0.60f, 0.18f);
        public static readonly Color DangerRed       = new Color(0.75f, 0.20f, 0.15f);
        
        // === TEXT HIERARCHY ===
        public static readonly Color Porcelain       = new Color(0.93f, 0.91f, 0.88f);       // Primary text
        public static readonly Color SilverText      = new Color(0.62f, 0.63f, 0.65f);       // Secondary text
        public static readonly Color Parchment       = new Color(0.82f, 0.77f, 0.66f);       // Warm accent text
        public static readonly Color GoldText        = EmpireGold;
        
        // === BORDERS ===
        public static readonly Color BorderGold      = new Color(0.45f, 0.38f, 0.25f, 0.7f);
        public static readonly Color BorderGoldBright= new Color(0.75f, 0.63f, 0.35f, 0.8f);
        public static readonly Color SubtleBorder    = new Color(0.20f, 0.19f, 0.17f, 0.5f);
        
        // === GRADIENTS & GLOW ===
        public static readonly Color GradientTop     = new Color(0.10f, 0.10f, 0.11f, 0.99f);
        public static readonly Color GradientBottom   = new Color(0.06f, 0.065f, 0.07f, 0.99f);
        public static readonly Color GlowGold        = new Color(0.85f, 0.72f, 0.35f, 0.15f);
        public static readonly Color InnerGlow       = new Color(0.77f, 0.63f, 0.35f, 0.06f);
        
        // === FACTION COLORS ===
        public static readonly Color FranceBlue      = new Color(0.20f, 0.30f, 0.65f);
        public static readonly Color BritainRed      = new Color(0.75f, 0.15f, 0.15f);
        
        // === BUTTON STATES ===
        public static readonly Color ButtonNormal    = new Color(0.10f, 0.10f, 0.11f, 0.95f);
        public static readonly Color ButtonHover     = new Color(0.14f, 0.13f, 0.11f, 0.98f);
        public static readonly Color ButtonActive    = new Color(0.18f, 0.16f, 0.10f, 1.0f);
        
        // === LEGACY ALIASES (for backward compat) ===
        public static readonly Color GoldAccent      = EmpireGold;
        public static readonly Color TextWhite       = Porcelain;
        public static readonly Color TextGrey        = SilverText;
        public static readonly Color DarkBg          = DeepCharcoal;
        public static readonly Color PanelBg         = PanelSurface;
        public static readonly Color HeaderBg        = HeaderSurface;
        public static readonly Color AlertRed        = ImperialCrimson;
        public static readonly Color ParchmentBeige  = Parchment;
        public static readonly Color TextGold        = EmpireGold;
        public static readonly Color CrimsonDeep     = ImperialCrimson;
        public static readonly Color CrimsonGlow     = new Color(0.85f, 0.20f, 0.15f);
        public static readonly Color DarkStone       = new Color(0.14f, 0.13f, 0.12f);
        public static readonly Color BronzeHighlight = new Color(0.72f, 0.53f, 0.26f);
        public static readonly Color PremiumSurface  = PanelSurface;

        // ════════════════════════════════════════════════════════════
        //  CANVAS
        // ════════════════════════════════════════════════════════════

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
            Canvas canvas = CreateCanvas(name, sortOrder);
            canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = matchMode;
            return canvas;
        }

        // ════════════════════════════════════════════════════════════
        //  PANELS — Dark Premium Style
        // ════════════════════════════════════════════════════════════

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

        /// <summary>
        /// Premium bordered panel with gold edge and dark interior.
        /// </summary>
        public static RectTransform CreateBorderedPanel(Transform parent, string name, Color bgColor, Color borderColor, float borderWidth = 1.5f)
        {
            // Outer border
            GameObject borderGO = new GameObject(name);
            borderGO.transform.SetParent(parent, false);
            RectTransform borderRT = borderGO.AddComponent<RectTransform>();
            Image borderImg = borderGO.AddComponent<Image>();
            borderImg.color = borderColor;

            // Inner dark fill
            GameObject innerGO = new GameObject("Inner");
            innerGO.transform.SetParent(borderGO.transform, false);
            RectTransform innerRT = innerGO.AddComponent<RectTransform>();
            innerRT.anchorMin = Vector2.zero;
            innerRT.anchorMax = Vector2.one;
            innerRT.offsetMin = new Vector2(borderWidth, borderWidth);
            innerRT.offsetMax = new Vector2(-borderWidth, -borderWidth);
            Image innerImg = innerGO.AddComponent<Image>();
            innerImg.color = bgColor;

            // Inner highlight line (top edge shimmer)
            GameObject shimmer = new GameObject("Shimmer");
            shimmer.transform.SetParent(innerGO.transform, false);
            RectTransform shimmerRT = shimmer.AddComponent<RectTransform>();
            shimmerRT.anchorMin = new Vector2(0, 1);
            shimmerRT.anchorMax = Vector2.one;
            shimmerRT.offsetMin = new Vector2(0, -1);
            shimmerRT.offsetMax = Vector2.zero;
            Image shimmerImg = shimmer.AddComponent<Image>();
            shimmerImg.color = new Color(1f, 1f, 1f, 0.04f);
            shimmerImg.raycastTarget = false;

            return borderRT;
        }

        /// <summary>
        /// Ornate panel with layered border, inner shadow, and content area.
        /// </summary>
        public static RectTransform CreateOrnatePanel(Transform parent, string name, Color bgColor, Vector2? size = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            if (size.HasValue) rect.sizeDelta = size.Value;

            // Outer border (gold)
            Image borderImg = go.AddComponent<Image>();
            borderImg.color = MutedGold;

            // Main surface
            GameObject surface = new GameObject("Surface");
            surface.transform.SetParent(go.transform, false);
            RectTransform sRect = surface.AddComponent<RectTransform>();
            sRect.anchorMin = Vector2.zero; sRect.anchorMax = Vector2.one;
            sRect.offsetMin = new Vector2(1.5f, 1.5f); sRect.offsetMax = new Vector2(-1.5f, -1.5f);
            Image sImg = surface.AddComponent<Image>();
            sImg.color = bgColor;

            // Inner top highlight  
            GameObject highlight = new GameObject("Highlight");
            highlight.transform.SetParent(surface.transform, false);
            RectTransform hRT = highlight.AddComponent<RectTransform>();
            hRT.anchorMin = new Vector2(0, 1); hRT.anchorMax = Vector2.one;
            hRT.offsetMin = new Vector2(2, -1); hRT.offsetMax = new Vector2(-2, 0);
            Image hImg = highlight.AddComponent<Image>();
            hImg.color = new Color(1f, 0.9f, 0.7f, 0.05f);
            hImg.raycastTarget = false;
            LayoutElement hLE = highlight.AddComponent<LayoutElement>();
            hLE.ignoreLayout = true;

            // Content area
            GameObject innerGO = new GameObject("Inner");
            innerGO.transform.SetParent(go.transform, false);
            RectTransform innerRT = innerGO.AddComponent<RectTransform>();
            innerRT.anchorMin = Vector2.zero; innerRT.anchorMax = Vector2.one;
            innerRT.offsetMin = new Vector2(5, 5); innerRT.offsetMax = new Vector2(-5, -5);

            return rect;
        }

        // ════════════════════════════════════════════════════════════
        //  TEXT — Clean Typography
        // ════════════════════════════════════════════════════════════

        public static Text CreateText(Transform parent, string name, string content,
            int fontSize = 14, TextAnchor alignment = TextAnchor.MiddleLeft, Color? color = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            Text text = go.AddComponent<Text>();
            text.text = content;
            // Try premium fonts first, fallback to Arial
            text.font = Font.CreateDynamicFontFromOSFont("Segoe UI", fontSize);
            if (text.font == null) text.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color ?? Porcelain;
            text.raycastTarget = false;
            text.supportRichText = true;

            // Subtle shadow for readability
            Shadow shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.5f);
            shadow.effectDistance = new Vector2(0.5f, -0.5f);

            return text;
        }

        /// <summary>
        /// Create a TextMeshPro text element — superior rendering with SDF fonts.
        /// Use this over CreateText for any new UI that needs crisp, scalable text.
        /// </summary>
        public static TextMeshProUGUI CreateTMPText(Transform parent, string name, string content,
            float fontSize = 14f, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft, Color? color = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = color ?? Porcelain;
            tmp.raycastTarget = false;
            tmp.richText = true;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.enableWordWrapping = true;

            return tmp;
        }

        // ════════════════════════════════════════════════════════════
        //  BUTTONS — Premium Imperial Style
        // ════════════════════════════════════════════════════════════

        public static Button CreateButton(Transform parent, string name, string label,
            int fontSize = 14, UnityAction onClick = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(160, 36);

            // Background
            Image img = go.AddComponent<Image>();
            img.color = ButtonNormal;

            // Gold border
            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = BorderGold;
            outline.effectDistance = new Vector2(1, 1);

            // Button component with rich state colors
            Button btn = go.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.3f, 1.25f, 1.1f);
            cb.pressedColor = new Color(0.8f, 0.75f, 0.6f);
            cb.selectedColor = new Color(1.1f, 1.05f, 1.0f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            if (onClick != null)
                btn.onClick.AddListener(onClick);

            // Top edge highlight (gives 3D feel)
            GameObject highlight = new GameObject("HL");
            highlight.transform.SetParent(go.transform, false);
            RectTransform hlRT = highlight.AddComponent<RectTransform>();
            hlRT.anchorMin = new Vector2(0, 1);
            hlRT.anchorMax = Vector2.one;
            hlRT.offsetMin = new Vector2(1, -1);
            hlRT.offsetMax = new Vector2(-1, 0);
            Image hlImg = highlight.AddComponent<Image>();
            hlImg.color = new Color(1f, 1f, 1f, 0.06f);
            hlImg.raycastTarget = false;
            LayoutElement hlLE = highlight.AddComponent<LayoutElement>();
            hlLE.ignoreLayout = true;

            // Label
            Text t = CreateText(go.transform, "Label", label, fontSize, TextAnchor.MiddleCenter, Porcelain);
            // Remove the shadow we just added (button text doesn't need double shadow)
            Shadow labelShadow = t.GetComponent<Shadow>();
            if (labelShadow != null) labelShadow.effectColor = new Color(0, 0, 0, 0.7f);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
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
            
            // Rich crimson background with gold border
            Image img = btn.GetComponent<Image>();
            img.color = new Color(0.35f, 0.08f, 0.08f, 0.95f);
            
            Outline outline = btn.GetComponent<Outline>();
            if (outline != null) outline.effectColor = EmpireGold;

            // Make label gold
            Text labelText = btn.GetComponentInChildren<Text>();
            if (labelText != null) labelText.color = BrightGold;

            return btn;
        }
        
        public static Button CreateWarhammerButton(Transform parent, string name, string label,
            int fontSize = 18, UnityAction onClick = null)
        {
            return CreateGoldButton(parent, name, label, fontSize, onClick);
        }

        // ════════════════════════════════════════════════════════════
        //  SECTION HEADERS — Imperial Banner Style
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Banner header with gradient background and gold accent.
        /// </summary>
        public static RectTransform CreateBannerHeader(Transform parent, string name, string title, int fontSize = 20)
        {
            RectTransform container = CreatePanel(parent, name, HeaderSurface);
            
            // Left gold accent bar
            RectTransform accent = CreatePanel(container, "Accent", EmpireGold);
            accent.anchorMin = Vector2.zero;
            accent.anchorMax = new Vector2(0, 1);
            accent.offsetMin = Vector2.zero;
            accent.offsetMax = new Vector2(3, 0);
            accent.GetComponent<LayoutElement>()?.Equals(null); // force create
            LayoutElement accentLE = accent.gameObject.AddComponent<LayoutElement>();
            accentLE.ignoreLayout = true;
            
            // Bottom gold line
            RectTransform line = CreatePanel(container, "GoldLine", new Color(EmpireGold.r, EmpireGold.g, EmpireGold.b, 0.4f));
            line.anchorMin = new Vector2(0, 0);
            line.anchorMax = new Vector2(1, 0);
            line.offsetMin = Vector2.zero;
            line.offsetMax = new Vector2(0, 1);
            LayoutElement lineLE = line.gameObject.AddComponent<LayoutElement>();
            lineLE.ignoreLayout = true;
            
            // Title text
            Text text = CreateText(container, "Title", title.ToUpper(), fontSize, TextAnchor.MiddleCenter, EmpireGold);
            text.fontStyle = FontStyle.Bold;
            SetAnchors(text.gameObject, Vector2.zero, Vector2.one, new Vector2(8, 0), new Vector2(-8, 0));
            
            return container;
        }
        
        /// <summary>
        /// Compact section header with left gold bar and dark glass background.
        /// </summary>
        public static Text CreateSectionHeader(Transform parent, string name, string title, int fontSize = 13)
        {
            GameObject container = new GameObject(name);
            container.transform.SetParent(parent, false);
            RectTransform rect = container.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 28);

            // Dark glass background
            Image img = container.AddComponent<Image>();
            img.color = new Color(0.04f, 0.04f, 0.05f, 0.9f);
            
            // Left gold accent
            GameObject accent = new GameObject("Accent");
            accent.transform.SetParent(container.transform, false);
            RectTransform accentRT = accent.AddComponent<RectTransform>();
            accentRT.anchorMin = Vector2.zero;
            accentRT.anchorMax = new Vector2(0, 1);
            accentRT.offsetMin = Vector2.zero;
            accentRT.offsetMax = new Vector2(3, 0);
            Image accentImg = accent.AddComponent<Image>();
            accentImg.color = EmpireGold;
            accentImg.raycastTarget = false;
            LayoutElement accentLE = accent.AddComponent<LayoutElement>();
            accentLE.ignoreLayout = true;

            Text text = CreateText(container.transform, "Label", title.ToUpper(), fontSize, TextAnchor.MiddleLeft, Parchment);
            text.fontStyle = FontStyle.Bold;
            SetAnchors(text.gameObject, Vector2.zero, Vector2.one, new Vector2(12, 0), new Vector2(-12, 0));

            return text;
        }
        
        public static RectTransform CreateOrnamentalSeparator(Transform parent)
        {
            return CreateSeparator(parent);
        }

        // ════════════════════════════════════════════════════════════
        //  INPUT FIELD
        // ════════════════════════════════════════════════════════════

        public static InputField CreateInputField(Transform parent, string name, string placeholder,
            string defaultValue = "", int fontSize = 14)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            Image img = go.AddComponent<Image>();
            img.color = new Color(0.05f, 0.05f, 0.06f, 0.95f);

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = BorderGold;
            outline.effectDistance = new Vector2(1f, 1f);

            InputField input = go.AddComponent<InputField>();

            Text text = CreateText(go.transform, "Text", defaultValue, fontSize, TextAnchor.MiddleLeft, Porcelain);
            RectTransform textRT = text.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(10f, 2f);
            textRT.offsetMax = new Vector2(-10f, -2f);
            input.textComponent = text;
            input.text = defaultValue;

            Text ph = CreateText(go.transform, "Placeholder", placeholder, fontSize, TextAnchor.MiddleLeft, SilverText);
            RectTransform phRT = ph.GetComponent<RectTransform>();
            phRT.anchorMin = Vector2.zero;
            phRT.anchorMax = Vector2.one;
            phRT.offsetMin = new Vector2(10f, 2f);
            phRT.offsetMax = new Vector2(-10f, -2f);
            input.placeholder = ph;

            return input;
        }

        // ════════════════════════════════════════════════════════════
        //  PROGRESS BAR — Premium Glass Style
        // ════════════════════════════════════════════════════════════

        public static (Image bg, Image fill) CreateProgressBar(Transform parent, string name,
            Color fillColor, Color bgColor = default)
        {
            if (bgColor == default) bgColor = new Color(0.04f, 0.04f, 0.05f, 0.9f);

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 14);

            // Border
            Image bgImg = go.AddComponent<Image>();
            bgImg.color = SubtleBorder;
            
            // Inner background
            GameObject innerBg = new GameObject("InnerBg");
            innerBg.transform.SetParent(go.transform, false);
            RectTransform innerBgRT = innerBg.AddComponent<RectTransform>();
            innerBgRT.anchorMin = Vector2.zero;
            innerBgRT.anchorMax = Vector2.one;
            innerBgRT.offsetMin = new Vector2(1, 1);
            innerBgRT.offsetMax = new Vector2(-1, -1);
            Image innerBgImg = innerBg.AddComponent<Image>();
            innerBgImg.color = bgColor;

            // Fill
            GameObject fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(innerBg.transform, false);
            RectTransform fillRT = fillGO.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = new Vector2(1f, 1f);
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            fillRT.pivot = new Vector2(0f, 0.5f);
            Image fillImg = fillGO.AddComponent<Image>();
            fillImg.color = fillColor;
            
            // Glass highlight on fill (top half, subtle white)
            GameObject gloss = new GameObject("Gloss");
            gloss.transform.SetParent(fillGO.transform, false);
            RectTransform glossRT = gloss.AddComponent<RectTransform>();
            glossRT.anchorMin = new Vector2(0, 0.45f);
            glossRT.anchorMax = Vector2.one;
            glossRT.offsetMin = Vector2.zero;
            glossRT.offsetMax = Vector2.zero;
            Image glossImg = gloss.AddComponent<Image>();
            glossImg.color = new Color(1f, 1f, 1f, 0.15f);
            glossImg.raycastTarget = false;

            return (bgImg, fillImg);
        }

        // ════════════════════════════════════════════════════════════
        //  SEPARATORS
        // ════════════════════════════════════════════════════════════

        public static RectTransform CreateSeparator(Transform parent, Color? color = null)
        {
            Color sepColor = color ?? new Color(MutedGold.r, MutedGold.g, MutedGold.b, 0.3f);
            return CreatePanel(parent, "Separator", sepColor);
        }

        /// <summary>
        /// Glow separator with centered crisp line and outer glow.
        /// </summary>
        public static RectTransform CreateGlowSeparator(Transform parent, string name, 
            bool vertical = true, Color? glowColor = null, Color? lineColor = null)
        {
            Color glow = glowColor ?? GlowGold;
            Color line = lineColor ?? new Color(EmpireGold.r, EmpireGold.g, EmpireGold.b, 0.6f);

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();

            // Outer glow
            Image glowImg = go.AddComponent<Image>();
            glowImg.color = glow;
            glowImg.raycastTarget = false;

            // Inner crisp line
            GameObject lineGO = new GameObject("Line");
            lineGO.transform.SetParent(go.transform, false);
            RectTransform lineRT = lineGO.AddComponent<RectTransform>();
            if (vertical)
            {
                lineRT.anchorMin = new Vector2(0.3f, 0.1f);
                lineRT.anchorMax = new Vector2(0.7f, 0.9f);
            }
            else
            {
                lineRT.anchorMin = new Vector2(0.02f, 0.25f);
                lineRT.anchorMax = new Vector2(0.98f, 0.75f);
            }
            lineRT.offsetMin = Vector2.zero;
            lineRT.offsetMax = Vector2.zero;
            Image lineImg = lineGO.AddComponent<Image>();
            lineImg.color = line;
            lineImg.raycastTarget = false;

            return rt;
        }

        // ════════════════════════════════════════════════════════════
        //  SCROLL VIEW — Dark Inset
        // ════════════════════════════════════════════════════════════

        public static (ScrollRect scroll, RectTransform content) CreateScrollView(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            Image img = go.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.15f);

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

        // ════════════════════════════════════════════════════════════
        //  LAYOUT HELPERS
        // ════════════════════════════════════════════════════════════

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

        // ════════════════════════════════════════════════════════════
        //  PREMIUM COMPOSITE WIDGETS
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Gradient panel using stacked layers for depth.
        /// </summary>
        public static RectTransform CreateGradientPanel(Transform parent, string name, 
            Color topColor, Color bottomColor, Color? midColor = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();

            Image baseImg = go.AddComponent<Image>();
            baseImg.color = bottomColor;
            baseImg.raycastTarget = true;

            // Top overlay
            GameObject topOverlay = new GameObject("GradTop");
            topOverlay.transform.SetParent(go.transform, false);
            RectTransform topRT = topOverlay.AddComponent<RectTransform>();
            topRT.anchorMin = new Vector2(0, 0.3f);
            topRT.anchorMax = Vector2.one;
            topRT.offsetMin = Vector2.zero;
            topRT.offsetMax = Vector2.zero;
            Image topImg = topOverlay.AddComponent<Image>();
            topImg.color = topColor;
            topImg.raycastTarget = false;
            LayoutElement topLE = topOverlay.AddComponent<LayoutElement>();
            topLE.ignoreLayout = true;

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
        /// Icon badge — colored icon with dark background and value text.
        /// </summary>
        public static (GameObject container, Text iconText, Text valueText) CreateIconBadge(
            Transform parent, string name, string icon, string value, 
            Color badgeColor, Color textColor, float containerWidth = 150f)
        {
            GameObject container = new GameObject(name);
            container.transform.SetParent(parent, false);
            container.AddComponent<RectTransform>();
            AddLayoutElement(container, preferredWidth: containerWidth, preferredHeight: 40);

            HorizontalLayoutGroup hlg = AddHorizontalLayout(container, 6f, new RectOffset(3, 6, 3, 3));
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            // Badge background
            GameObject badgeBg = new GameObject("BadgeBg");
            badgeBg.transform.SetParent(container.transform, false);
            badgeBg.AddComponent<RectTransform>();
            AddLayoutElement(badgeBg, preferredWidth: 34, preferredHeight: 34);

            Image borderImg = badgeBg.AddComponent<Image>();
            borderImg.color = new Color(badgeColor.r * 0.5f, badgeColor.g * 0.5f, badgeColor.b * 0.5f, 0.4f);
            borderImg.raycastTarget = false;

            // Inner fill
            GameObject badgeInner = new GameObject("BadgeInner");
            badgeInner.transform.SetParent(badgeBg.transform, false);
            RectTransform innerRT = badgeInner.AddComponent<RectTransform>();
            innerRT.anchorMin = Vector2.zero;
            innerRT.anchorMax = Vector2.one;
            innerRT.offsetMin = new Vector2(1, 1);
            innerRT.offsetMax = new Vector2(-1, -1);
            Image innerImg = badgeInner.AddComponent<Image>();
            innerImg.color = new Color(badgeColor.r * 0.2f, badgeColor.g * 0.2f, badgeColor.b * 0.2f, 0.85f);
            innerImg.raycastTarget = false;

            // Icon
            Text iconText = CreateText(badgeInner.transform, "Icon", icon, 17, TextAnchor.MiddleCenter, badgeColor);
            iconText.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform iconRT = iconText.GetComponent<RectTransform>();
            iconRT.anchorMin = Vector2.zero;
            iconRT.anchorMax = Vector2.one;
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = Vector2.zero;

            // Value
            Text valueText = CreateText(container.transform, "Value", value, 14, TextAnchor.MiddleLeft, textColor);
            valueText.fontStyle = FontStyle.Bold;
            valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
            AddLayoutElement(valueText.gameObject, preferredWidth: containerWidth - 44, preferredHeight: 40);

            return (container, iconText, valueText);
        }

        /// <summary>
        /// Inset container with subtle border.
        /// </summary>
        public static RectTransform CreateInsetContainer(Transform parent, string name, Color? bgColor = null)
        {
            Color bg = bgColor ?? new Color(0.05f, 0.055f, 0.06f, 0.8f);

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();

            Image borderImg = go.AddComponent<Image>();
            borderImg.color = SubtleBorder;
            borderImg.raycastTarget = false;

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
        /// Premium progress bar with glass effect and glow.
        /// </summary>
        public static (Image bg, Image fill, Image glow) CreatePremiumProgressBar(Transform parent, string name,
            Color fillColor, float width = 200f, float height = 14f)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);

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
            bgImg.color = new Color(0.03f, 0.03f, 0.04f, 0.9f);

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

            // Glass glow on fill
            GameObject glowGO = new GameObject("Glow");
            glowGO.transform.SetParent(fillGO.transform, false);
            RectTransform glowRT = glowGO.AddComponent<RectTransform>();
            glowRT.anchorMin = new Vector2(0, 0.4f);
            glowRT.anchorMax = Vector2.one;
            glowRT.offsetMin = Vector2.zero;
            glowRT.offsetMax = Vector2.zero;
            Image glowImg = glowGO.AddComponent<Image>();
            glowImg.color = new Color(1f, 1f, 1f, 0.18f);
            glowImg.raycastTarget = false;

            return (bgImg, fillImg, glowImg);
        }

        // Overload for backward compat
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
