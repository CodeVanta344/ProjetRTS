using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using NapoleonicWars.Data;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// Full-screen loading screen with async scene loading, progress bar, faction theme, and historical tips.
    /// Singleton — persists across scenes via DontDestroyOnLoad.
    /// </summary>
    public class LoadingScreenUI : MonoBehaviour
    {
        public static LoadingScreenUI Instance { get; private set; }

        private Canvas canvas;
        private CanvasGroup canvasGroup;
        private Image backgroundImage;
        private Image progressBarFill;
        private Image progressBarGlow;
        private Text progressText;
        private Text tipText;
        private Text factionNameText;
        private Text loadingLabel;
        private Image factionAccentTop;
        private Image factionAccentBot;
        private Image vignetteOverlay;

        private bool isLoading = false;
        private float displayedProgress = 0f;
        private float realProgressTarget = 0f;
        private int currentTipIndex = 0;
        private float tipTimer = 0f;

        // ─── FACTION COLORS ───
        private static readonly Color[] FactionThemeColors = {
            new Color(0.15f, 0.30f, 0.75f),    // France
            new Color(0.75f, 0.15f, 0.15f),    // Britain
            new Color(0.20f, 0.20f, 0.22f),    // Prussia
            new Color(0.15f, 0.55f, 0.20f),    // Russia
            new Color(0.92f, 0.82f, 0.35f),    // Austria
            new Color(0.90f, 0.55f, 0.15f),    // Spain
            new Color(0.55f, 0.15f, 0.20f),    // Ottoman
            new Color(0.20f, 0.80f, 0.20f),    // Portugal
            new Color(0.20f, 0.40f, 0.80f),    // Sweden
            new Color(0.80f, 0.20f, 0.20f),    // Denmark
            new Color(0.80f, 0.20f, 0.40f),    // Poland
            new Color(0.80f, 0.20f, 0.20f),    // Venice
            new Color(0.90f, 0.50f, 0.10f),    // Dutch
            new Color(0.20f, 0.60f, 0.90f),    // Bavaria
            new Color(0.20f, 0.80f, 0.40f),    // Saxony
            new Color(0.90f, 0.80f, 0.20f),    // Papal States
            new Color(0.80f, 0.20f, 0.20f),    // Savoy
            new Color(0.80f, 0.20f, 0.20f),    // Switzerland
            new Color(0.80f, 0.20f, 0.20f),    // Genoa
            new Color(0.80f, 0.20f, 0.20f),    // Tuscany
            new Color(0.80f, 0.20f, 0.20f),    // Hanover
            new Color(0.80f, 0.20f, 0.20f),    // Modena
            new Color(0.80f, 0.20f, 0.20f),    // Parma
            new Color(0.80f, 0.20f, 0.20f)     // Lorraine
        };

        private static readonly string[] FactionLoadingNames = {
            "EMPIRE FRANÇAIS", "UNITED KINGDOM", "KÖNIGREICH PREUSSEN",
            "РОССИЙСКАЯ ИМПЕРИЯ", "KAISERTUM ÖSTERREICH", "REINO DE ESPAÑA", "OSMANLI İMPARATORLUĞU",
            "REINO DE PORTUGAL", "SVENSKA STORMAKTSTIDEN", "DANMARK-NORGE", "RZECZPOSPOLITA",
            "SERENISSIMA REPUBBLICA DI VENEZIA", "REPUBLIEK DER ZEVEN VERENIGDE NEDERLANDEN",
            "KURFÜRSTENTUM BAYERN", "KURFÜRSTENTUM SACHSEN", "STATI PONTIFICI", "DUCATI DI SAVOIA",
            "CONFOEDERATIO HELVETICA", "REPUBBLICA DI GENOVA", "GRANDUCATO DI TOSCANA",
            "KURFÜRSTENTUM HANNOVER", "DUCATO DI MODENA", "DUCATO DI PARMA", "DUCHÉ DE LORRAINE"
        };

        // ─── HISTORICAL TIPS ───
        private static readonly string[] LoadingTips = {
            "La Grande Armée de Napoléon comptait plus de 600 000 hommes lors de l'invasion de la Russie en 1812.",
            "La Royal Navy britannique dominait les mers après la victoire de Trafalgar en 1805.",
            "Le mousquet Brown Bess avait une portée effective d'environ 75 mètres.",
            "Les grognards de la Vieille Garde n'ont jamais rompu les rangs au combat.",
            "La cavalerie lourde pouvait charger à plus de 30 km/h en formation serrée.",
            "Un canon de 12 livres tirait environ 2 coups par minute avec un équipage entraîné.",
            "La tactique de la colonne française privilégiait le choc moral sur le feu.",
            "Les tirailleurs opéraient en ordre dispersé pour harceler l'ennemi avant l'assaut principal.",
            "Le système de corps d'armée de Napoléon permettait une concentration rapide des forces.",
            "La ligne mince britannique maximisait la puissance de feu au détriment de la profondeur.",
            "Les hussards étaient réputés pour leur bravoure... et leur indiscipline.",
            "Le blocus continental visait à ruiner l'économie britannique en fermant les ports européens.",
            "Les batailles napoléoniennes duraient en moyenne 6 à 10 heures.",
            "La Garde Impériale était la réserve d'élite, engagée uniquement au moment décisif.",
            "Les dragons pouvaient combattre à pied comme à cheval, offrant une grande polyvalence.",
            "Le terrain élevé conférait un avantage décisif pour l'artillerie et la défense.",
        };

        // ─── LIFECYCLE ───

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildUI();
            Hide();
        }

        private void Update()
        {
            if (!isLoading) return;

            // Smooth progress bar — never jumps, lerps toward real target
            float smoothSpeed = 0.35f; // slow enough to feel real
            displayedProgress = Mathf.Lerp(displayedProgress, realProgressTarget, Time.unscaledDeltaTime * smoothSpeed);
            // Snap close when very near target to avoid stuck-at-99 feeling
            if (realProgressTarget - displayedProgress < 0.005f)
                displayedProgress = realProgressTarget;
            UpdateProgressVisuals(displayedProgress);

            // Cycle tips
            tipTimer += Time.unscaledDeltaTime;
            if (tipTimer > 4.5f)
            {
                tipTimer = 0f;
                CycleTip();
            }

            // Pulse the glow on the progress bar
            if (progressBarGlow != null)
            {
                float pulse = 0.3f + Mathf.Sin(Time.unscaledTime * 3f) * 0.2f;
                Color gc = progressBarGlow.color;
                progressBarGlow.color = new Color(gc.r, gc.g, gc.b, pulse);
            }

            // Pulse loading dots
            if (loadingLabel != null)
            {
                int dots = ((int)(Time.unscaledTime * 2f)) % 4;
                loadingLabel.text = "CHARGEMENT" + new string('.', dots);
            }
        }

        // ─── PUBLIC API ───

        /// <summary>
        /// Load a scene asynchronously with the loading screen displayed.
        /// </summary>
        public void LoadScene(string sceneName, int factionIndex = -1)
        {
            if (isLoading) return;
            StartCoroutine(LoadSceneRoutine(sceneName, factionIndex));
        }

        /// <summary>
        /// Static convenience: load scene with loading screen. Creates instance if needed.
        /// </summary>
        public static void LoadSceneWithScreen(string sceneName, int factionIndex = -1)
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("LoadingScreenUI");
                go.AddComponent<LoadingScreenUI>();
            }
            Instance.LoadScene(sceneName, factionIndex);
        }

        // ─── LOADING COROUTINE ───

        private IEnumerator LoadSceneRoutine(string sceneName, int factionIndex)
        {
            isLoading = true;
            displayedProgress = 0f;
            realProgressTarget = 0f;
            tipTimer = 0f;
            currentTipIndex = Random.Range(0, LoadingTips.Length);

            // Apply faction theme
            ApplyFactionTheme(factionIndex);

            // Show with fade-in
            Show();
            yield return StartCoroutine(FadeIn(0.4f));

            // Start async scene load
            AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName);
            asyncOp.allowSceneActivation = false;

            // ── Phase 1: real asset loading (asyncOp 0→0.9 = bar 0%→90%) ──
            // Unity caps at 0.9 while allowSceneActivation == false
            while (asyncOp.progress < 0.9f)
            {
                realProgressTarget = asyncOp.progress / 0.9f * 0.9f; // 0→90%
                yield return null;
            }

            // Assets loaded — set bar to 90%
            realProgressTarget = 0.9f;

            // Wait for the displayed bar to actually catch up to 90%
            while (displayedProgress < 0.89f)
                yield return null;

            // ── Phase 2: scene activation (90%→100%) ──
            // Now allow the scene to activate — this triggers all Awake/Start/OnEnable
            asyncOp.allowSceneActivation = true;

            // While Unity is integrating the scene, progress goes 0.9→1.0
            while (!asyncOp.isDone)
            {
                // Map 0.9→1.0 of asyncOp to 90%→100% of bar
                float activationProgress = Mathf.InverseLerp(0.9f, 1f, asyncOp.progress);
                realProgressTarget = 0.9f + activationProgress * 0.1f;
                yield return null;
            }

            // Scene is fully loaded and initialized — set 100%
            realProgressTarget = 1f;
            displayedProgress = 1f;
            UpdateProgressVisuals(1f);
            if (loadingLabel != null) loadingLabel.text = "PRÊT";

            // Brief pause so the player sees 100% before fade-out
            yield return new WaitForSecondsRealtime(0.4f);

            // Fade out
            yield return StartCoroutine(FadeOut(0.5f));

            Hide();
            isLoading = false;
        }

        // ─── FADE ───

        private IEnumerator FadeIn(float duration)
        {
            if (canvasGroup == null) yield break;
            float t = 0f;
            canvasGroup.alpha = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.SmoothStep(0f, 1f, t / duration);
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }

        private IEnumerator FadeOut(float duration)
        {
            if (canvasGroup == null) yield break;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.SmoothStep(1f, 0f, t / duration);
                yield return null;
            }
            canvasGroup.alpha = 0f;
        }

        // ─── UI BUILD ───

        private void BuildUI()
        {
            // Canvas at highest sort order
            GameObject canvasGO = new GameObject("LoadingCanvas");
            canvasGO.transform.SetParent(transform);
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            canvasGroup = canvasGO.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = false;

            // ── BACKGROUND ──
            GameObject bgGO = new GameObject("Background");
            bgGO.transform.SetParent(canvas.transform, false);
            RectTransform bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;
            backgroundImage = bgGO.AddComponent<Image>();
            backgroundImage.color = new Color(0.02f, 0.015f, 0.01f, 1f);

            // ── VIGNETTE OVERLAY (dark edges) ──
            GameObject vigGO = new GameObject("Vignette");
            vigGO.transform.SetParent(canvas.transform, false);
            RectTransform vigRT = vigGO.AddComponent<RectTransform>();
            vigRT.anchorMin = Vector2.zero; vigRT.anchorMax = Vector2.one;
            vigRT.offsetMin = Vector2.zero; vigRT.offsetMax = Vector2.zero;
            vignetteOverlay = vigGO.AddComponent<Image>();
            vignetteOverlay.color = new Color(0, 0, 0, 0.3f);
            vignetteOverlay.raycastTarget = false;

            // ── TOP ACCENT BAR ──
            GameObject topBarGO = new GameObject("TopAccent");
            topBarGO.transform.SetParent(canvas.transform, false);
            RectTransform topBarRT = topBarGO.AddComponent<RectTransform>();
            topBarRT.anchorMin = new Vector2(0, 0.96f); topBarRT.anchorMax = Vector2.one;
            topBarRT.offsetMin = Vector2.zero; topBarRT.offsetMax = Vector2.zero;
            factionAccentTop = topBarGO.AddComponent<Image>();
            factionAccentTop.color = UIFactory.BorderGold;
            factionAccentTop.raycastTarget = false;

            // ── BOTTOM ACCENT BAR ──
            GameObject botBarGO = new GameObject("BotAccent");
            botBarGO.transform.SetParent(canvas.transform, false);
            RectTransform botBarRT = botBarGO.AddComponent<RectTransform>();
            botBarRT.anchorMin = Vector2.zero; botBarRT.anchorMax = new Vector2(1, 0.04f);
            botBarRT.offsetMin = Vector2.zero; botBarRT.offsetMax = Vector2.zero;
            factionAccentBot = botBarGO.AddComponent<Image>();
            factionAccentBot.color = UIFactory.BorderGold;
            factionAccentBot.raycastTarget = false;

            // ── DECORATIVE GOLD LINES ──
            CreateGoldLine(canvas.transform, new Vector2(0, 0.945f), new Vector2(1, 0.95f));
            CreateGoldLine(canvas.transform, new Vector2(0, 0.05f), new Vector2(1, 0.055f));

            // ── CENTER EMBLEM AREA ──
            // Faction crest / eagle silhouette
            Text emblemText = CreateText(canvas.transform, "Emblem", "⚜",
                120, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.4f, 0.08f));
            SetAnchors(emblemText.gameObject, new Vector2(0.35f, 0.35f), new Vector2(0.65f, 0.75f));

            // ── FACTION NAME (large, center) ──
            factionNameText = CreateText(canvas.transform, "FactionName", "EMPIRE FRANÇAIS",
                42, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.55f, 0.95f));
            factionNameText.fontStyle = FontStyle.Bold;
            SetAnchors(factionNameText.gameObject, new Vector2(0.1f, 0.58f), new Vector2(0.9f, 0.72f));
            Shadow fnShadow = factionNameText.gameObject.AddComponent<Shadow>();
            fnShadow.effectColor = new Color(0, 0, 0, 0.9f);
            fnShadow.effectDistance = new Vector2(3, -3);

            // ── LOADING LABEL ──
            loadingLabel = CreateText(canvas.transform, "LoadingLabel", "CHARGEMENT...",
                18, TextAnchor.MiddleCenter, new Color(0.75f, 0.65f, 0.45f));
            loadingLabel.fontStyle = FontStyle.Bold;
            SetAnchors(loadingLabel.gameObject, new Vector2(0.3f, 0.26f), new Vector2(0.7f, 0.32f));

            // ── PROGRESS BAR ──
            BuildProgressBar(canvas.transform);

            // ── PROGRESS TEXT ──
            progressText = CreateText(canvas.transform, "ProgressText", "0%",
                14, TextAnchor.MiddleCenter, new Color(0.8f, 0.7f, 0.5f));
            SetAnchors(progressText.gameObject, new Vector2(0.45f, 0.155f), new Vector2(0.55f, 0.195f));

            // ── TIP TEXT ──
            Text tipLabel = CreateText(canvas.transform, "TipLabel", "LE SAVIEZ-VOUS ?",
                12, TextAnchor.MiddleCenter, new Color(0.6f, 0.5f, 0.35f, 0.8f));
            tipLabel.fontStyle = FontStyle.Bold;
            SetAnchors(tipLabel.gameObject, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.12f));

            tipText = CreateText(canvas.transform, "TipText", LoadingTips[0],
                13, TextAnchor.UpperCenter, new Color(0.82f, 0.75f, 0.60f, 0.9f));
            tipText.fontStyle = FontStyle.Italic;
            SetAnchors(tipText.gameObject, new Vector2(0.08f, 0.055f), new Vector2(0.92f, 0.085f));

            // ── DECORATIVE CORNER ORNAMENTS ──
            Text ornTL = CreateText(canvas.transform, "OrnTL", "╔══", 16, TextAnchor.UpperLeft, new Color(0.6f, 0.5f, 0.3f, 0.4f));
            SetAnchors(ornTL.gameObject, new Vector2(0.02f, 0.92f), new Vector2(0.1f, 0.96f));
            Text ornTR = CreateText(canvas.transform, "OrnTR", "══╗", 16, TextAnchor.UpperRight, new Color(0.6f, 0.5f, 0.3f, 0.4f));
            SetAnchors(ornTR.gameObject, new Vector2(0.9f, 0.92f), new Vector2(0.98f, 0.96f));
            Text ornBL = CreateText(canvas.transform, "OrnBL", "╚══", 16, TextAnchor.LowerLeft, new Color(0.6f, 0.5f, 0.3f, 0.4f));
            SetAnchors(ornBL.gameObject, new Vector2(0.02f, 0.04f), new Vector2(0.1f, 0.08f));
            Text ornBR = CreateText(canvas.transform, "OrnBR", "══╝", 16, TextAnchor.LowerRight, new Color(0.6f, 0.5f, 0.3f, 0.4f));
            SetAnchors(ornBR.gameObject, new Vector2(0.9f, 0.04f), new Vector2(0.98f, 0.08f));
        }

        private void BuildProgressBar(Transform parent)
        {
            // Outer frame (gold border)
            GameObject frameGO = new GameObject("ProgressFrame");
            frameGO.transform.SetParent(parent, false);
            RectTransform frameRT = frameGO.AddComponent<RectTransform>();
            SetAnchors(frameGO, new Vector2(0.2f, 0.195f), new Vector2(0.8f, 0.245f));
            Image frameImg = frameGO.AddComponent<Image>();
            frameImg.color = new Color(0.55f, 0.45f, 0.25f, 0.8f);

            // Inner background (dark)
            GameObject innerGO = new GameObject("ProgressInner");
            innerGO.transform.SetParent(frameGO.transform, false);
            RectTransform innerRT = innerGO.AddComponent<RectTransform>();
            innerRT.anchorMin = Vector2.zero; innerRT.anchorMax = Vector2.one;
            innerRT.offsetMin = new Vector2(2, 2); innerRT.offsetMax = new Vector2(-2, -2);
            Image innerImg = innerGO.AddComponent<Image>();
            innerImg.color = new Color(0.03f, 0.025f, 0.02f, 0.95f);

            // Fill bar
            GameObject fillGO = new GameObject("ProgressFill");
            fillGO.transform.SetParent(innerGO.transform, false);
            RectTransform fillRT = fillGO.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = new Vector2(0f, 1f);
            fillRT.offsetMin = new Vector2(1, 1);
            fillRT.offsetMax = new Vector2(-1, -1);
            progressBarFill = fillGO.AddComponent<Image>();
            progressBarFill.color = new Color(0.75f, 0.60f, 0.25f);

            // Glow overlay on fill
            GameObject glowGO = new GameObject("ProgressGlow");
            glowGO.transform.SetParent(fillGO.transform, false);
            RectTransform glowRT = glowGO.AddComponent<RectTransform>();
            glowRT.anchorMin = new Vector2(0.9f, 0);
            glowRT.anchorMax = Vector2.one;
            glowRT.offsetMin = Vector2.zero; glowRT.offsetMax = Vector2.zero;
            progressBarGlow = glowGO.AddComponent<Image>();
            progressBarGlow.color = new Color(1f, 0.95f, 0.7f, 0.4f);
            progressBarGlow.raycastTarget = false;

            // Subtle hash marks on the bar (10%, 25%, 50%, 75%)
            float[] marks = { 0.25f, 0.5f, 0.75f };
            foreach (float m in marks)
            {
                GameObject markGO = new GameObject($"Mark_{m}");
                markGO.transform.SetParent(innerGO.transform, false);
                RectTransform markRT = markGO.AddComponent<RectTransform>();
                markRT.anchorMin = new Vector2(m - 0.001f, 0);
                markRT.anchorMax = new Vector2(m + 0.001f, 1);
                markRT.offsetMin = Vector2.zero; markRT.offsetMax = Vector2.zero;
                Image markImg = markGO.AddComponent<Image>();
                markImg.color = new Color(0.4f, 0.35f, 0.25f, 0.3f);
                markImg.raycastTarget = false;
            }
        }

        // ─── THEME ───

        private void ApplyFactionTheme(int factionIndex)
        {
            if (factionIndex < 0 || factionIndex >= FactionThemeColors.Length)
                factionIndex = PlayerPrefs.GetInt("SelectedFaction", 0);

            Color themeColor = FactionThemeColors[Mathf.Clamp(factionIndex, 0, FactionThemeColors.Length - 1)];
            string factionName = FactionLoadingNames[Mathf.Clamp(factionIndex, 0, FactionLoadingNames.Length - 1)];

            // Background: very dark version of faction color
            if (backgroundImage != null)
                backgroundImage.color = new Color(themeColor.r * 0.06f, themeColor.g * 0.06f, themeColor.b * 0.06f, 1f);

            // Accent bars
            if (factionAccentTop != null)
                factionAccentTop.color = themeColor;
            if (factionAccentBot != null)
                factionAccentBot.color = themeColor;

            // Progress bar fill uses faction color blended with gold
            if (progressBarFill != null)
                progressBarFill.color = Color.Lerp(themeColor, new Color(0.50f, 0.48f, 0.38f), 0.4f);

            // Faction name
            if (factionNameText != null)
                factionNameText.text = factionName;

            // Set first tip
            currentTipIndex = Random.Range(0, LoadingTips.Length);
            if (tipText != null)
                tipText.text = LoadingTips[currentTipIndex];
        }

        // ─── HELPERS ───

        private void UpdateProgressVisuals(float progress)
        {
            if (progressBarFill != null)
            {
                RectTransform fillRT = progressBarFill.GetComponent<RectTransform>();
                fillRT.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
            }

            if (progressText != null)
                progressText.text = $"{Mathf.RoundToInt(progress * 100)}%";
        }

        private void CycleTip()
        {
            if (tipText == null) return;
            currentTipIndex = (currentTipIndex + 1) % LoadingTips.Length;
            StartCoroutine(FadeTip(LoadingTips[currentTipIndex]));
        }

        private IEnumerator FadeTip(string newTip)
        {
            if (tipText == null) yield break;
            // Fade out
            Color c = tipText.color;
            float t = 0f;
            while (t < 0.2f)
            {
                t += Time.unscaledDeltaTime;
                tipText.color = new Color(c.r, c.g, c.b, Mathf.Lerp(c.a, 0f, t / 0.2f));
                yield return null;
            }
            tipText.text = newTip;
            // Fade in
            t = 0f;
            while (t < 0.3f)
            {
                t += Time.unscaledDeltaTime;
                tipText.color = new Color(c.r, c.g, c.b, Mathf.Lerp(0f, c.a, t / 0.3f));
                yield return null;
            }
            tipText.color = c;
        }

        private void Show()
        {
            if (canvas != null) canvas.gameObject.SetActive(true);
        }

        private void Hide()
        {
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        private void CreateGoldLine(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject lineGO = new GameObject("GoldLine");
            lineGO.transform.SetParent(parent, false);
            RectTransform lineRT = lineGO.AddComponent<RectTransform>();
            lineRT.anchorMin = anchorMin; lineRT.anchorMax = anchorMax;
            lineRT.offsetMin = Vector2.zero; lineRT.offsetMax = Vector2.zero;
            Image lineImg = lineGO.AddComponent<Image>();
            lineImg.color = new Color(0.65f, 0.55f, 0.30f, 0.5f);
            lineImg.raycastTarget = false;
        }

        private Text CreateText(Transform parent, string name, string content, int fontSize, TextAnchor anchor, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            Text txt = go.AddComponent<Text>();
            txt.text = content;
            txt.fontSize = fontSize;
            txt.alignment = anchor;
            txt.color = color;
            txt.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }

        private void SetAnchors(GameObject go, Vector2 anchorMin, Vector2 anchorMax)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
