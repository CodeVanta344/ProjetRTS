using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using NapoleonicWars.Data;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// Cinematic loading screen with Ken Burns pan/zoom on Napoleonic era artwork,
    /// crossfade between paintings, dramatic vignette, faction-themed accents,
    /// and historical tips. Loads images from Resources/LoadingScreens/ at runtime.
    /// </summary>
    public class LoadingScreenUI : MonoBehaviour
    {
        public static LoadingScreenUI Instance { get; private set; }

        private Canvas canvas;
        private CanvasGroup canvasGroup;

        // Background artwork system
        private RawImage artworkImageA;
        private RawImage artworkImageB;
        private CanvasGroup artworkGroupA;
        private CanvasGroup artworkGroupB;
        private RectTransform artworkRTA;
        private RectTransform artworkRTB;
        private List<Texture2D> artworkTextures = new List<Texture2D>();
        private int currentArtworkIndex = 0;
        private bool showingA = true;

        // UI elements
        private Image progressBarFill;
        private Image progressBarGlow;
        private Text progressText;
        private Text tipText;
        private Text factionNameText;
        private Text loadingLabel;
        private Text subtitleText;
        private Image factionAccentTop;
        private Image factionAccentBot;

        private bool isLoading = false;
        private float displayedProgress = 0f;
        private float realProgressTarget = 0f;
        private int currentTipIndex = 0;
        private float tipTimer = 0f;
        private float artworkTimer = 0f;
        private float kenBurnsTime = 0f;

        // Ken Burns parameters (randomized per image)
        private float kbStartScale = 1.08f;
        private float kbEndScale = 1.18f;
        private Vector2 kbPanStart = Vector2.zero;
        private Vector2 kbPanEnd = Vector2.zero;
        private float kbDuration = 12f;

        // ─── FACTION DATA ───

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

        private static readonly string[] FactionSubtitles = {
            "Gloire et Conquête", "Rule Britannia", "Für König und Vaterland",
            "За Веру, Царя и Отечество", "Gott erhalte den Kaiser", "Plus Ultra", "Devlet-i Ebed Müddet",
            "Além-Mar", "Sverige i krig", "Gud og Kongen", "Za Wolność Naszą",
            "La Serenissima", "Eendracht Maakt Macht",
            "In Treue fest", "Providentiae Memor", "Deus lo Vult", "FERT",
            "Unus pro omnibus", "Libertas", "Fiorenza",
            "Suscipere et Finire", "Deo Confide", "Pro Patria", "Je Maintiendrai"
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
            "Napoléon dictait souvent ses ordres en marchant, parfois à trois secrétaires simultanément.",
            "L'Aigle impérial, porté par chaque régiment, était considéré comme sacré par les soldats.",
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
            LoadArtwork();
            BuildUI();
            Hide();
        }

        private void Update()
        {
            if (!isLoading) return;

            // Smooth progress bar
            float smoothSpeed = 4.5f;
            displayedProgress = Mathf.Lerp(displayedProgress, realProgressTarget, Time.unscaledDeltaTime * smoothSpeed);
            displayedProgress = Mathf.MoveTowards(displayedProgress, realProgressTarget, Time.unscaledDeltaTime * 0.1f); // Guarantee exact reach
            if (realProgressTarget - displayedProgress < 0.005f)
                displayedProgress = realProgressTarget;
            UpdateProgressVisuals(displayedProgress);

            // Ken Burns pan/zoom effect
            kenBurnsTime += Time.unscaledDeltaTime;
            ApplyKenBurns(showingA ? artworkRTA : artworkRTB);

            // Cycle artwork every kbDuration seconds
            artworkTimer += Time.unscaledDeltaTime;
            if (artworkTimer > kbDuration && artworkTextures.Count > 1)
            {
                artworkTimer = 0f;
                CrossfadeToNextArtwork();
            }

            // Cycle tips
            tipTimer += Time.unscaledDeltaTime;
            if (tipTimer > 5.5f)
            {
                tipTimer = 0f;
                CycleTip();
            }

            // Pulse the progress glow
            if (progressBarGlow != null)
            {
                float pulse = 0.35f + Mathf.Sin(Time.unscaledTime * 2.5f) * 0.25f;
                Color gc = progressBarGlow.color;
                progressBarGlow.color = new Color(gc.r, gc.g, gc.b, pulse);
            }

            // Animate loading dots
            if (loadingLabel != null)
            {
                int dots = ((int)(Time.unscaledTime * 1.5f)) % 4;
                loadingLabel.text = "CHARGEMENT" + new string('.', dots);
            }
        }

        // ─── ARTWORK LOADING ───

        private void LoadArtwork()
        {
            // Load all textures from Resources/LoadingScreens/
            Texture2D[] loaded = Resources.LoadAll<Texture2D>("LoadingScreens");
            if (loaded != null && loaded.Length > 0)
            {
                artworkTextures.AddRange(loaded);
                // Shuffle
                for (int i = artworkTextures.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    var tmp = artworkTextures[i];
                    artworkTextures[i] = artworkTextures[j];
                    artworkTextures[j] = tmp;
                }
            }
        }

        private void RandomizeKenBurns()
        {
            kbStartScale = Random.Range(1.08f, 1.14f);
            kbEndScale = Random.Range(1.16f, 1.25f);
            kbPanStart = new Vector2(Random.Range(-20f, 20f), Random.Range(-10f, 10f));
            kbPanEnd = new Vector2(Random.Range(-20f, 20f), Random.Range(-10f, 10f));
            kbDuration = Random.Range(10f, 14f);
            kenBurnsTime = 0f;
        }

        private void ApplyKenBurns(RectTransform rt)
        {
            if (rt == null) return;
            float t = Mathf.Clamp01(kenBurnsTime / kbDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            float scale = Mathf.Lerp(kbStartScale, kbEndScale, smoothT);
            Vector2 pan = Vector2.Lerp(kbPanStart, kbPanEnd, smoothT);
            rt.localScale = Vector3.one * scale;
            rt.anchoredPosition = pan;
        }

        private void CrossfadeToNextArtwork()
        {
            if (artworkTextures.Count < 2) return;

            currentArtworkIndex = (currentArtworkIndex + 1) % artworkTextures.Count;
            RandomizeKenBurns();

            if (showingA)
            {
                artworkImageB.texture = artworkTextures[currentArtworkIndex];
                artworkRTB.localScale = Vector3.one * kbStartScale;
                StartCoroutine(CrossfadeArtwork(artworkGroupA, artworkGroupB, 1.5f));
            }
            else
            {
                artworkImageA.texture = artworkTextures[currentArtworkIndex];
                artworkRTA.localScale = Vector3.one * kbStartScale;
                StartCoroutine(CrossfadeArtwork(artworkGroupB, artworkGroupA, 1.5f));
            }
            showingA = !showingA;
        }

        private IEnumerator CrossfadeArtwork(CanvasGroup fadeOut, CanvasGroup fadeIn, float duration)
        {
            float t = 0f;
            fadeIn.alpha = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.SmoothStep(0f, 1f, t / duration);
                fadeOut.alpha = 1f - p;
                fadeIn.alpha = p;
                yield return null;
            }
            fadeOut.alpha = 0f;
            fadeIn.alpha = 1f;
        }

        // ─── PUBLIC API ───

        public void LoadScene(string sceneName, int factionIndex = -1)
        {
            if (isLoading) return;
            StartCoroutine(LoadSceneRoutine(sceneName, factionIndex));
        }

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
            artworkTimer = 0f;
            currentTipIndex = Random.Range(0, LoadingTips.Length);

            // Setup artwork
            if (artworkTextures.Count > 0)
            {
                currentArtworkIndex = Random.Range(0, artworkTextures.Count);
                artworkImageA.texture = artworkTextures[currentArtworkIndex];
                artworkGroupA.alpha = 1f;
                artworkGroupB.alpha = 0f;
                showingA = true;
                RandomizeKenBurns();
            }

            ApplyFactionTheme(factionIndex);
            Show();
            yield return StartCoroutine(FadeIn(0.5f));

            AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName);
            asyncOp.allowSceneActivation = false;

            // Phase 1: Assets loading (0→90%)
            while (asyncOp.progress < 0.9f)
            {
                realProgressTarget = asyncOp.progress / 0.9f * 0.9f;
                yield return null;
            }

            realProgressTarget = 0.9f;
            while (displayedProgress < 0.89f)
                yield return null;

            // Phase 2: Scene activation (90→100%)
            asyncOp.allowSceneActivation = true;
            while (!asyncOp.isDone)
            {
                float activationProgress = Mathf.InverseLerp(0.9f, 1f, asyncOp.progress);
                realProgressTarget = 0.9f + activationProgress * 0.1f;
                yield return null;
            }

            realProgressTarget = 1f;
            displayedProgress = 1f;
            UpdateProgressVisuals(1f);
            if (loadingLabel != null) loadingLabel.text = "PRÊT";

            yield return new WaitForSecondsRealtime(0.5f);
            yield return StartCoroutine(FadeOut(0.6f));

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
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            canvasGroup = canvasGO.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = false;

            // ── BLACK UNDERLAY (prevents any flash) ──
            CreateFullscreenImage(canvas.transform, "BlackUnderlay", Color.black);

            // ── ARTWORK LAYER A (background painting with Ken Burns) ──
            artworkImageA = CreateArtworkLayer(canvas.transform, "ArtworkA", out artworkGroupA, out artworkRTA);

            // ── ARTWORK LAYER B (crossfade target) ──
            artworkImageB = CreateArtworkLayer(canvas.transform, "ArtworkB", out artworkGroupB, out artworkRTB);
            artworkGroupB.alpha = 0f;

            // ── CINEMATIC VIGNETTE (dark edges, dramatic focus) ──
            // Top dark gradient
            Image topVig = CreateFullscreenImage(canvas.transform, "VigTop", Color.clear);
            RectTransform topVigRT = topVig.GetComponent<RectTransform>();
            topVigRT.anchorMin = new Vector2(0, 0.55f); topVigRT.anchorMax = Vector2.one;
            topVigRT.offsetMin = Vector2.zero; topVigRT.offsetMax = Vector2.zero;
            topVig.color = new Color(0, 0, 0, 0.55f);

            // Bottom dark gradient (heavier for text readability)
            Image botVig = CreateFullscreenImage(canvas.transform, "VigBot", Color.clear);
            RectTransform botVigRT = botVig.GetComponent<RectTransform>();
            botVigRT.anchorMin = Vector2.zero; botVigRT.anchorMax = new Vector2(1, 0.45f);
            botVigRT.offsetMin = Vector2.zero; botVigRT.offsetMax = Vector2.zero;
            botVig.color = new Color(0, 0, 0, 0.7f);

            // Full overlay vignette (subtle darkening)
            Image fullVig = CreateFullscreenImage(canvas.transform, "VigFull", new Color(0, 0, 0, 0.2f));

            // ── TOP ACCENT BAR (faction colored) ──
            factionAccentTop = CreateFullscreenImage(canvas.transform, "TopAccent", UIFactory.BorderGold);
            RectTransform topBarRT = factionAccentTop.GetComponent<RectTransform>();
            topBarRT.anchorMin = new Vector2(0, 0.99f); topBarRT.anchorMax = Vector2.one;
            topBarRT.offsetMin = Vector2.zero; topBarRT.offsetMax = Vector2.zero;

            // Top gold line below accent
            CreateLine(canvas.transform, new Vector2(0, 0.985f), new Vector2(1, 0.988f), 
                new Color(0.7f, 0.58f, 0.28f, 0.4f));

            // ── BOTTOM ACCENT BAR ──
            factionAccentBot = CreateFullscreenImage(canvas.transform, "BotAccent", UIFactory.BorderGold);
            RectTransform botBarRT = factionAccentBot.GetComponent<RectTransform>();
            botBarRT.anchorMin = Vector2.zero; botBarRT.anchorMax = new Vector2(1, 0.01f);
            botBarRT.offsetMin = Vector2.zero; botBarRT.offsetMax = Vector2.zero;

            // Bottom gold line above accent
            CreateLine(canvas.transform, new Vector2(0, 0.012f), new Vector2(1, 0.015f),
                new Color(0.7f, 0.58f, 0.28f, 0.4f));

            // ── CENTER CONTENT: FACTION NAME (large, cinematic) ──
            factionNameText = CreateCinematicText(canvas.transform, "FactionName", "EMPIRE FRANÇAIS",
                52, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.6f, 0.95f));
            factionNameText.fontStyle = FontStyle.Bold;
            SetAnchors(factionNameText.gameObject, new Vector2(0.05f, 0.48f), new Vector2(0.95f, 0.62f));

            // Faction subtitle (below name)
            subtitleText = CreateCinematicText(canvas.transform, "Subtitle", "Gloire et Conquête",
                20, TextAnchor.MiddleCenter, new Color(0.85f, 0.75f, 0.55f, 0.7f));
            subtitleText.fontStyle = FontStyle.Italic;
            SetAnchors(subtitleText.gameObject, new Vector2(0.2f, 0.42f), new Vector2(0.8f, 0.50f));

            // Decorative line under subtitle
            CreateLine(canvas.transform, new Vector2(0.35f, 0.415f), new Vector2(0.65f, 0.42f),
                new Color(0.65f, 0.55f, 0.30f, 0.5f));

            // ── LOADING LABEL ──
            loadingLabel = CreateCinematicText(canvas.transform, "LoadingLabel", "CHARGEMENT...",
                16, TextAnchor.MiddleCenter, new Color(0.8f, 0.7f, 0.5f, 0.9f));
            loadingLabel.fontStyle = FontStyle.Bold;
            SetAnchors(loadingLabel.gameObject, new Vector2(0.3f, 0.24f), new Vector2(0.7f, 0.30f));

            // ── PROGRESS BAR (cinematic) ──
            BuildCinematicProgressBar(canvas.transform);

            // ── PROGRESS PERCENTAGE ──
            progressText = CreateCinematicText(canvas.transform, "ProgressText", "0%",
                13, TextAnchor.MiddleCenter, new Color(0.85f, 0.75f, 0.55f, 0.8f));
            SetAnchors(progressText.gameObject, new Vector2(0.45f, 0.145f), new Vector2(0.55f, 0.175f));

            // ── TIP SECTION (bottom) ──
            // Separator line
            CreateLine(canvas.transform, new Vector2(0.15f, 0.12f), new Vector2(0.85f, 0.124f),
                new Color(0.5f, 0.42f, 0.25f, 0.3f));

            Text tipLabel = CreateCinematicText(canvas.transform, "TipLabel", "— LE SAVIEZ-VOUS ? —",
                11, TextAnchor.MiddleCenter, new Color(0.65f, 0.55f, 0.40f, 0.7f));
            tipLabel.fontStyle = FontStyle.Bold;
            SetAnchors(tipLabel.gameObject, new Vector2(0.1f, 0.08f), new Vector2(0.9f, 0.115f));

            tipText = CreateCinematicText(canvas.transform, "TipText", LoadingTips[0],
                13, TextAnchor.UpperCenter, new Color(0.88f, 0.82f, 0.68f, 0.85f));
            tipText.fontStyle = FontStyle.Italic;
            SetAnchors(tipText.gameObject, new Vector2(0.1f, 0.042f), new Vector2(0.9f, 0.082f));

            // ── DECORATIVE CORNER ORNAMENTS ──
            CreateCornerOrnament(canvas.transform, "TL", new Vector2(0.015f, 0.935f), new Vector2(0.06f, 0.965f), "╔══", TextAnchor.UpperLeft);
            CreateCornerOrnament(canvas.transform, "TR", new Vector2(0.94f, 0.935f), new Vector2(0.985f, 0.965f), "══╗", TextAnchor.UpperRight);
            CreateCornerOrnament(canvas.transform, "BL", new Vector2(0.015f, 0.035f), new Vector2(0.06f, 0.065f), "╚══", TextAnchor.LowerLeft);
            CreateCornerOrnament(canvas.transform, "BR", new Vector2(0.94f, 0.035f), new Vector2(0.985f, 0.065f), "══╝", TextAnchor.LowerRight);
        }

        private RawImage CreateArtworkLayer(Transform parent, string name, out CanvasGroup group, out RectTransform rt)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-150, -100); // Oversized for Ken Burns movement
            rt.offsetMax = new Vector2(150, 100);

            RawImage img = go.AddComponent<RawImage>();
            img.color = Color.white;
            img.raycastTarget = false;

            group = go.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = false;

            return img;
        }

        private void BuildCinematicProgressBar(Transform parent)
        {
            // === ORNATE OUTER FRAME (double gold border) ===
            GameObject frameGO = new GameObject("ProgressFrame");
            frameGO.transform.SetParent(parent, false);
            SetAnchors(frameGO, new Vector2(0.22f, 0.185f), new Vector2(0.78f, 0.215f));

            // Outer gold border
            Image frameImg = frameGO.AddComponent<Image>();
            frameImg.color = new Color(0.55f, 0.45f, 0.22f, 0.85f);

            // Subtle outer glow
            Outline frameOutline = frameGO.AddComponent<Outline>();
            frameOutline.effectColor = new Color(0.6f, 0.5f, 0.25f, 0.15f);
            frameOutline.effectDistance = new Vector2(3f, 3f);

            // Inner gold border (double border effect)
            GameObject innerBorderGO = new GameObject("InnerBorder");
            innerBorderGO.transform.SetParent(frameGO.transform, false);
            RectTransform ibRT = innerBorderGO.AddComponent<RectTransform>();
            ibRT.anchorMin = Vector2.zero; ibRT.anchorMax = Vector2.one;
            ibRT.offsetMin = new Vector2(2, 2); ibRT.offsetMax = new Vector2(-2, -2);
            Image ibImg = innerBorderGO.AddComponent<Image>();
            ibImg.color = new Color(0.03f, 0.025f, 0.02f, 0.95f); // Dark inner bg

            // Second gold line (inner frame)
            GameObject innerFrame2 = new GameObject("InnerFrame2");
            innerFrame2.transform.SetParent(innerBorderGO.transform, false);
            RectTransform if2RT = innerFrame2.AddComponent<RectTransform>();
            if2RT.anchorMin = Vector2.zero; if2RT.anchorMax = Vector2.one;
            if2RT.offsetMin = new Vector2(1, 1); if2RT.offsetMax = new Vector2(-1, -1);
            Outline if2Outline = innerFrame2.AddComponent<Outline>();
            if2Outline.effectColor = new Color(0.45f, 0.38f, 0.2f, 0.5f);
            if2Outline.effectDistance = new Vector2(1f, 1f);
            Image if2Img = innerFrame2.AddComponent<Image>();
            if2Img.color = new Color(0.05f, 0.04f, 0.03f, 0.9f); // Very dark bg for fill

            // === FILL BAR (warm amber/gold) ===
            GameObject fillGO = new GameObject("ProgressFill");
            fillGO.transform.SetParent(if2RT.transform, false);
            RectTransform fillRT = fillGO.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = new Vector2(0f, 1f);
            fillRT.offsetMin = new Vector2(1, 1);
            fillRT.offsetMax = new Vector2(-1, -1);
            progressBarFill = fillGO.AddComponent<Image>();
            progressBarFill.color = new Color(0.72f, 0.55f, 0.18f); // Warm amber gold

            // Top highlight strip (metallic sheen effect)
            GameObject highlight = new GameObject("FillHighlight");
            highlight.transform.SetParent(fillGO.transform, false);
            RectTransform hlRT = highlight.AddComponent<RectTransform>();
            hlRT.anchorMin = new Vector2(0, 0.6f);
            hlRT.anchorMax = Vector2.one;
            hlRT.offsetMin = Vector2.zero; hlRT.offsetMax = Vector2.zero;
            Image hlImg = highlight.AddComponent<Image>();
            hlImg.color = new Color(1f, 0.9f, 0.6f, 0.2f);
            hlImg.raycastTarget = false;

            // Bottom shadow strip (depth)
            GameObject bottomShadow = new GameObject("FillShadow");
            bottomShadow.transform.SetParent(fillGO.transform, false);
            RectTransform bsRT = bottomShadow.AddComponent<RectTransform>();
            bsRT.anchorMin = Vector2.zero;
            bsRT.anchorMax = new Vector2(1, 0.3f);
            bsRT.offsetMin = Vector2.zero; bsRT.offsetMax = Vector2.zero;
            Image bsImg = bottomShadow.AddComponent<Image>();
            bsImg.color = new Color(0.3f, 0.2f, 0.05f, 0.3f);
            bsImg.raycastTarget = false;

            // Leading edge glow (pulsing)
            GameObject glowGO = new GameObject("ProgressGlow");
            glowGO.transform.SetParent(fillGO.transform, false);
            RectTransform glowRT = glowGO.AddComponent<RectTransform>();
            glowRT.anchorMin = new Vector2(0.9f, 0);
            glowRT.anchorMax = Vector2.one;
            glowRT.offsetMin = Vector2.zero; glowRT.offsetMax = Vector2.zero;
            progressBarGlow = glowGO.AddComponent<Image>();
            progressBarGlow.color = new Color(1f, 0.9f, 0.55f, 0.5f);
            progressBarGlow.raycastTarget = false;

            // Ornate tick marks
            float[] marks = { 0.25f, 0.5f, 0.75f };
            foreach (float m in marks)
            {
                GameObject markGO = new GameObject($"Mark_{m}");
                markGO.transform.SetParent(if2RT.transform, false);
                RectTransform markRT = markGO.AddComponent<RectTransform>();
                markRT.anchorMin = new Vector2(m - 0.001f, 0.15f);
                markRT.anchorMax = new Vector2(m + 0.001f, 0.85f);
                markRT.offsetMin = Vector2.zero; markRT.offsetMax = Vector2.zero;
                Image markImg = markGO.AddComponent<Image>();
                markImg.color = new Color(0.5f, 0.42f, 0.22f, 0.3f);
                markImg.raycastTarget = false;
            }

            // Corner ornaments on the bar (small gold dots)
            CreateBarCorner(frameGO.transform, new Vector2(0, 1), new Vector2(0.01f, 0.8f));
            CreateBarCorner(frameGO.transform, new Vector2(0.99f, 1), new Vector2(1, 0.8f));
            CreateBarCorner(frameGO.transform, new Vector2(0, 0.2f), new Vector2(0.01f, 0));
            CreateBarCorner(frameGO.transform, new Vector2(0.99f, 0.2f), new Vector2(1, 0));
        }

        private void CreateBarCorner(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject go = new GameObject("Corner");
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.85f, 0.7f, 0.35f, 0.7f);
            img.raycastTarget = false;
        }

        // ─── THEME ───

        private void ApplyFactionTheme(int factionIndex)
        {
            if (factionIndex < 0 || factionIndex >= FactionThemeColors.Length)
                factionIndex = PlayerPrefs.GetInt("SelectedFaction", 0);

            int idx = Mathf.Clamp(factionIndex, 0, FactionThemeColors.Length - 1);
            Color themeColor = FactionThemeColors[idx];

            if (factionAccentTop != null) factionAccentTop.color = themeColor;
            if (factionAccentBot != null) factionAccentBot.color = themeColor;

            if (progressBarFill != null)
                progressBarFill.color = Color.Lerp(new Color(0.72f, 0.55f, 0.18f), themeColor, 0.1f);

            if (factionNameText != null && idx < FactionLoadingNames.Length)
                factionNameText.text = FactionLoadingNames[idx];

            if (subtitleText != null && idx < FactionSubtitles.Length)
                subtitleText.text = "— " + FactionSubtitles[idx] + " —";

            currentTipIndex = Random.Range(0, LoadingTips.Length);
            if (tipText != null) tipText.text = LoadingTips[currentTipIndex];
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
            Color c = tipText.color;
            float t = 0f;
            while (t < 0.25f)
            {
                t += Time.unscaledDeltaTime;
                tipText.color = new Color(c.r, c.g, c.b, Mathf.Lerp(c.a, 0f, t / 0.25f));
                yield return null;
            }
            tipText.text = newTip;
            t = 0f;
            while (t < 0.35f)
            {
                t += Time.unscaledDeltaTime;
                tipText.color = new Color(c.r, c.g, c.b, Mathf.Lerp(0f, c.a, t / 0.35f));
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

        private Image CreateFullscreenImage(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private void CreateLine(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            GameObject go = new GameObject("Line");
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        private void CreateCornerOrnament(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, string text, TextAnchor anchor)
        {
            Text ornament = CreateCinematicText(parent, "Orn_" + name, text,
                14, anchor, new Color(0.6f, 0.5f, 0.3f, 0.35f));
            SetAnchors(ornament.gameObject, anchorMin, anchorMax);
        }

        private Text CreateCinematicText(Transform parent, string name, string content, int fontSize, TextAnchor anchor, Color color)
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
            txt.supportRichText = true;

            // Cinematic shadow on all text
            Shadow shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.85f);
            shadow.effectDistance = new Vector2(2f, -2f);

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
