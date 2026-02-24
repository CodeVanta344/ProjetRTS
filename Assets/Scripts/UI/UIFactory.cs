using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace NapoleonicWars.UI
{
    public static class UIFactory
    {
        // === HOI4 COLOR PALETTE — dark olive/grey, clean & modern ===
        
        // Accent colors
        public static readonly Color GoldAccent = new Color(0.78f, 0.72f, 0.50f);      // Muted gold-khaki
        public static readonly Color BronzeHighlight = new Color(0.65f, 0.58f, 0.38f);
        public static readonly Color GreenAccent = new Color(0.35f, 0.55f, 0.30f);      // HoI4 green
        public static readonly Color GreenBright = new Color(0.45f, 0.72f, 0.38f);      // Bright action green
        public static readonly Color BorderGold = new Color(0.40f, 0.38f, 0.30f, 0.6f); // Subtle border
        public static readonly Color BorderGoldBright = new Color(0.50f, 0.48f, 0.38f, 0.8f);
        
        // Backgrounds — cool dark olive-grey
        public static readonly Color DarkBg = new Color(0.10f, 0.11f, 0.10f, 0.96f);
        public static readonly Color PanelBg = new Color(0.14f, 0.15f, 0.13f, 0.94f);
        public static readonly Color HeaderBg = new Color(0.18f, 0.19f, 0.17f, 0.97f);
        public static readonly Color DarkStone = new Color(0.12f, 0.13f, 0.11f, 0.95f);
        public static readonly Color PanelBgLight = new Color(0.18f, 0.20f, 0.17f, 0.92f);
        
        // No more crimson — HoI4 uses red only for alerts
        public static readonly Color AlertRed = new Color(0.75f, 0.25f, 0.20f);
        public static readonly Color WarningYellow = new Color(0.85f, 0.75f, 0.30f);
        
        // Buttons — dark with green tint
        public static readonly Color ButtonNormal = new Color(0.16f, 0.18f, 0.15f, 0.95f);
        public static readonly Color ButtonHover = new Color(0.22f, 0.26f, 0.20f, 0.95f);
        public static readonly Color ButtonActive = new Color(0.30f, 0.40f, 0.25f, 0.95f);
        
        // Text — clean white/grey
        public static readonly Color TextWhite = new Color(0.90f, 0.90f, 0.88f);
        public static readonly Color TextGrey = new Color(0.58f, 0.58f, 0.55f);
        public static readonly Color ParchmentBeige = new Color(0.80f, 0.78f, 0.70f);
        public static readonly Color TextGold = GoldAccent;
        
        // Faction accents (kept)
        public static readonly Color FranceBlue = new Color(0.25f, 0.4f, 0.9f);
        public static readonly Color BritainRed = new Color(0.9f, 0.25f, 0.25f);
        
        // Legacy alias
        public static readonly Color CrimsonDark = AlertRed;
        public static readonly Color CrimsonDeep = new Color(0.50f, 0.15f, 0.12f);
        public static readonly Color CrimsonGlow = new Color(0.70f, 0.25f, 0.18f);
        public static readonly Color WarpGlow = GreenAccent;

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
        /// Modern flat panel with thin border and inner content area.
        /// Replaces the old ornate Warhammer panel. The inner content area is named "Inner"
        /// and can be accessed via transform.Find("Inner").
        /// </summary>
        public static RectTransform CreateOrnatePanel(Transform parent, string name, Color bgColor)
        {
            // Outer container with thin border
            GameObject outerGO = new GameObject(name);
            outerGO.transform.SetParent(parent, false);
            RectTransform outerRT = outerGO.AddComponent<RectTransform>();
            Image outerImg = outerGO.AddComponent<Image>();
            outerImg.color = new Color(0.25f, 0.26f, 0.22f, 0.7f); // Subtle olive border

            // Inner content area
            GameObject innerGO = new GameObject("Inner");
            innerGO.transform.SetParent(outerGO.transform, false);
            RectTransform innerRT = innerGO.AddComponent<RectTransform>();
            innerRT.anchorMin = Vector2.zero;
            innerRT.anchorMax = Vector2.one;
            innerRT.offsetMin = new Vector2(1f, 1f);
            innerRT.offsetMax = new Vector2(-1f, -1f);
            Image innerImg = innerGO.AddComponent<Image>();
            innerImg.color = bgColor;

            return outerRT;
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
            text.color = color ?? TextWhite;
            text.raycastTarget = false;
            text.supportRichText = true;

            return text;
        }

        // === BUTTON — Clean flat modern style ===
        public static Button CreateButton(Transform parent, string name, string label,
            int fontSize = 14, UnityAction onClick = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            Image img = go.AddComponent<Image>();
            img.color = ButtonNormal;

            Button btn = go.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.3f, 1.3f, 1.2f);
            cb.pressedColor = new Color(0.7f, 0.7f, 0.65f);
            cb.selectedColor = new Color(1.15f, 1.15f, 1.1f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            if (onClick != null)
                btn.onClick.AddListener(onClick);

            Text text = CreateText(go.transform, "Label", label, fontSize, TextAnchor.MiddleCenter, TextWhite);
            RectTransform textRT = text.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(4, 0);
            textRT.offsetMax = new Vector2(-4, 0);

            return btn;
        }

        /// <summary>Modern accent button — used for primary actions. Green-tinted.</summary>
        public static Button CreateGoldButton(Transform parent, string name, string label,
            int fontSize = 14, UnityAction onClick = null)
        {
            Button btn = CreateButton(parent, name, label, fontSize, onClick);
            
            Image img = btn.GetComponent<Image>();
            img.color = new Color(0.18f, 0.22f, 0.16f, 0.95f);
            
            // Update label color to accent
            Text labelText = btn.GetComponentInChildren<Text>();
            if (labelText != null) labelText.color = GoldAccent;

            return btn;
        }
        
        /// <summary>Alias — kept for backward compatibility, same as CreateGoldButton.</summary>
        public static Button CreateWarhammerButton(Transform parent, string name, string label,
            int fontSize = 18, UnityAction onClick = null)
        {
            return CreateGoldButton(parent, name, label, fontSize, onClick);
        }
        
        // === SECTION HEADER — clean bar with accent ===
        public static RectTransform CreateBannerHeader(Transform parent, string name, string title, int fontSize = 18)
        {
            RectTransform container = CreatePanel(parent, name, HeaderBg);
            
            // Left green accent bar
            RectTransform leftBar = CreatePanel(container, "Accent", GreenAccent);
            leftBar.anchorMin = new Vector2(0, 0.1f);
            leftBar.anchorMax = new Vector2(0, 0.9f);
            leftBar.offsetMin = Vector2.zero;
            leftBar.offsetMax = new Vector2(3, 0);
            leftBar.GetComponent<Image>().raycastTarget = false;
            
            // Title
            Text text = CreateText(container, "Title", title, fontSize, TextAnchor.MiddleLeft, TextWhite);
            text.fontStyle = FontStyle.Bold;
            RectTransform textRT = text.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(12, 0);
            textRT.offsetMax = new Vector2(-8, 0);
            
            return container;
        }
        
        // === SECTION HEADER (smaller) ===
        public static Text CreateSectionHeader(Transform parent, string name, string title, int fontSize = 14)
        {
            RectTransform container = CreatePanel(parent, name, new Color(0.16f, 0.18f, 0.15f, 0.9f));
            
            // Thin left accent
            RectTransform leftBar = CreatePanel(container, "Accent", GreenAccent);
            leftBar.anchorMin = new Vector2(0, 0.15f);
            leftBar.anchorMax = new Vector2(0, 0.85f);
            leftBar.offsetMin = Vector2.zero;
            leftBar.offsetMax = new Vector2(2, 0);
            leftBar.GetComponent<Image>().raycastTarget = false;
            
            Text text = CreateText(container, "Text", title, fontSize, TextAnchor.MiddleLeft, GoldAccent);
            RectTransform textRT = text.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(10, 0);
            textRT.offsetMax = new Vector2(-5, 0);
            
            return text;
        }
        
        // === SEPARATOR — thin clean line ===
        public static RectTransform CreateOrnamentalSeparator(Transform parent)
        {
            RectTransform rt = CreatePanel(parent, "Separator", new Color(0.30f, 0.32f, 0.28f, 0.5f));
            return rt;
        }

        // === INPUT FIELD ===
        public static InputField CreateInputField(Transform parent, string name, string placeholder,
            string defaultValue = "", int fontSize = 14)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            Image img = go.AddComponent<Image>();
            img.color = new Color(0.10f, 0.11f, 0.10f, 0.95f);

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.30f, 0.32f, 0.28f, 0.5f);
            outline.effectDistance = new Vector2(1f, 1f);

            InputField input = go.AddComponent<InputField>();

            Text text = CreateText(go.transform, "Text", defaultValue, fontSize, TextAnchor.MiddleLeft, TextWhite);
            RectTransform textRT = text.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(8f, 2f);
            textRT.offsetMax = new Vector2(-8f, -2f);
            input.textComponent = text;
            input.text = defaultValue;

            Text ph = CreateText(go.transform, "Placeholder", placeholder, fontSize, TextAnchor.MiddleLeft, TextGrey);
            RectTransform phRT = ph.GetComponent<RectTransform>();
            phRT.anchorMin = Vector2.zero;
            phRT.anchorMax = Vector2.one;
            phRT.offsetMin = new Vector2(8f, 2f);
            phRT.offsetMax = new Vector2(-8f, -2f);
            input.placeholder = ph;

            return input;
        }

        // === PROGRESS BAR ===
        public static (Image bg, Image fill) CreateProgressBar(Transform parent, string name,
            Color fillColor, Color bgColor = default)
        {
            if (bgColor == default) bgColor = new Color(0.10f, 0.11f, 0.10f, 0.9f);

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            Image bgImg = go.AddComponent<Image>();
            bgImg.color = bgColor;

            GameObject fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(go.transform, false);
            RectTransform fillRT = fillGO.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = new Vector2(1f, 1f);
            fillRT.offsetMin = new Vector2(1f, 1f);
            fillRT.offsetMax = new Vector2(-1f, -1f);
            fillRT.pivot = new Vector2(0f, 0.5f);

            Image fillImg = fillGO.AddComponent<Image>();
            fillImg.color = fillColor;

            return (bgImg, fillImg);
        }

        // === SEPARATOR LINE ===
        public static RectTransform CreateSeparator(Transform parent, Color? color = null)
        {
            return CreatePanel(parent, "Separator", color ?? new Color(0.30f, 0.32f, 0.28f, 0.4f));
        }

        // === SCROLL VIEW ===
        public static (ScrollRect scroll, RectTransform content) CreateScrollView(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            Image img = go.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.01f);

            ScrollRect scroll = go.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

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
            Mask mask = viewportGO.AddComponent<Mask>();
            mask.showMaskGraphic = false;
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
            hlg.padding = padding ?? new RectOffset(4, 4, 2, 2);
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

        // === RAW IMAGE ===
        public static RawImage CreateRawImage(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            RawImage ri = go.AddComponent<RawImage>();
            return ri;
        }

        // === ANCHOR HELPERS ===
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
    }
}
