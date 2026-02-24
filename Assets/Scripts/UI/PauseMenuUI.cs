using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Core;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// Pause menu with resume, settings, and quit options.
    /// Toggle with Escape key.
    /// </summary>
    public class PauseMenuUI : MonoBehaviour
    {
        private Canvas canvas;
        private GameObject panel;
        private bool isPaused;

        // Settings sliders
        private Slider masterVolumeSlider;
        private Slider musicVolumeSlider;
        private Slider sfxVolumeSlider;
        private Slider gameSpeedSlider;
        private Text gameSpeedLabel;
        private Text fpsText;
        private float fpsTimer;
        private int fpsFrameCount;
        private Image gfxLowImg, gfxMedImg, gfxHighImg;

        private void Start()
        {
            BuildUI();
            panel.SetActive(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (isPaused) Resume();
                else Pause();
            }
        }

        private void Pause()
        {
            // Don't pause if battle result is showing
            if (BattleStatistics.Instance != null && BattleStatistics.Instance.BattleEnded) return;

            isPaused = true;
            panel.SetActive(true);
            Time.timeScale = 0f;
        }

        private void Resume()
        {
            isPaused = false;
            panel.SetActive(false);
            Time.timeScale = gameSpeedSlider != null ? gameSpeedSlider.value : 1f;
        }

        private void BuildUI()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f; // Balance between width and height for all aspect ratios
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            gameObject.AddComponent<GraphicRaycaster>();

            // Overlay
            panel = new GameObject("PausePanel");
            panel.transform.SetParent(canvas.transform, false);
            RectTransform panelRT = panel.AddComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;
            Image panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0f, 0f, 0f, 0.75f);

            // Center box - responsive sizing (25% width, 55% height, centered)
            GameObject box = new GameObject("Box");
            box.transform.SetParent(panel.transform, false);
            RectTransform boxRT = box.AddComponent<RectTransform>();
            boxRT.anchorMin = new Vector2(0.35f, 0.2f);
            boxRT.anchorMax = new Vector2(0.65f, 0.85f);
            boxRT.offsetMin = Vector2.zero;
            boxRT.offsetMax = Vector2.zero;
            Image boxBg = box.AddComponent<Image>();
            boxBg.color = new Color(0.12f, 0.1f, 0.08f, 0.95f);
            Outline outline = box.AddComponent<Outline>();
            outline.effectColor = new Color(0.6f, 0.5f, 0.2f);
            outline.effectDistance = new Vector2(2, 2);

            float y = 200f;

            // Title
            CreateLabel(box.transform, "PAUSED", 32, FontStyle.Bold, new Vector2(0, y), new Color(0.9f, 0.85f, 0.6f));
            y -= 50f;

            // === SETTINGS ===
            CreateLabel(box.transform, "Master Volume", 16, FontStyle.Normal, new Vector2(-80, y), Color.white);
            masterVolumeSlider = CreateSlider(box.transform, new Vector2(100, y), 0.8f);
            masterVolumeSlider.onValueChanged.AddListener(v =>
            {
                if (BattleSoundManager.Instance != null)
                    BattleSoundManager.Instance.SetMasterVolume(v);
            });
            y -= 40f;

            CreateLabel(box.transform, "Music Volume", 16, FontStyle.Normal, new Vector2(-80, y), Color.white);
            musicVolumeSlider = CreateSlider(box.transform, new Vector2(100, y), 0.4f);
            musicVolumeSlider.onValueChanged.AddListener(v =>
            {
                if (BattleSoundManager.Instance != null)
                    BattleSoundManager.Instance.SetMusicVolume(v);
            });
            y -= 40f;

            CreateLabel(box.transform, "SFX Volume", 16, FontStyle.Normal, new Vector2(-80, y), Color.white);
            sfxVolumeSlider = CreateSlider(box.transform, new Vector2(100, y), 0.8f);
            sfxVolumeSlider.onValueChanged.AddListener(v =>
            {
                if (BattleSoundManager.Instance != null)
                    BattleSoundManager.Instance.SetSFXVolume(v);
            });
            y -= 50f;

            // Game speed
            CreateLabel(box.transform, "Game Speed", 16, FontStyle.Normal, new Vector2(-80, y), Color.white);
            gameSpeedSlider = CreateSlider(box.transform, new Vector2(100, y), 1f, 0.5f, 3f);
            gameSpeedLabel = CreateLabel(box.transform, "1.0x", 16, FontStyle.Bold, new Vector2(200, y), new Color(0.9f, 0.85f, 0.6f));
            gameSpeedSlider.onValueChanged.AddListener(v =>
            {
                if (gameSpeedLabel != null)
                    gameSpeedLabel.text = $"{v:F1}x";
            });
            y -= 50f;

            // Graphics quality
            CreateLabel(box.transform, "Graphics Quality", 16, FontStyle.Normal, new Vector2(-80, y), Color.white);
            y -= 30f;

            float gfxBtnW = 80f;
            float gfxStartX = -gfxBtnW - 10f;
            string[] gfxLabels = { "Low", "Medium", "High" };
            GraphicsQuality[] gfxValues = { GraphicsQuality.Low, GraphicsQuality.Medium, GraphicsQuality.High };
            Image[] gfxImgs = new Image[3];

            for (int i = 0; i < 3; i++)
            {
                GraphicsQuality gq = gfxValues[i];
                GameObject btnGO = new GameObject($"Gfx_{gfxLabels[i]}");
                btnGO.transform.SetParent(box.transform, false);
                RectTransform btnRT = btnGO.AddComponent<RectTransform>();
                btnRT.sizeDelta = new Vector2(gfxBtnW, 26);
                btnRT.anchoredPosition = new Vector2(gfxStartX + i * (gfxBtnW + 8), y);
                Image btnImg = btnGO.AddComponent<Image>();
                btnImg.color = new Color(0.2f, 0.18f, 0.12f);
                gfxImgs[i] = btnImg;
                Button btn = btnGO.AddComponent<Button>();
                btn.onClick.AddListener(() => SetGraphicsQuality(gq));

                Text t = CreateLabel(btnGO.transform, gfxLabels[i], 13, FontStyle.Bold, Vector2.zero, new Color(0.9f, 0.85f, 0.6f));
                RectTransform tRT = t.GetComponent<RectTransform>();
                tRT.anchorMin = Vector2.zero;
                tRT.anchorMax = Vector2.one;
                tRT.offsetMin = Vector2.zero;
                tRT.offsetMax = Vector2.zero;
            }
            gfxLowImg = gfxImgs[0];
            gfxMedImg = gfxImgs[1];
            gfxHighImg = gfxImgs[2];
            UpdateGraphicsButtons();

            y -= 40f;

            // FPS counter
            fpsText = CreateLabel(box.transform, "FPS: --", 12, FontStyle.Normal, new Vector2(160, 240), new Color(0.6f, 0.6f, 0.6f));

            // Separator
            GameObject sep = new GameObject("Sep");
            sep.transform.SetParent(box.transform, false);
            RectTransform sepRT = sep.AddComponent<RectTransform>();
            sepRT.sizeDelta = new Vector2(350, 2);
            sepRT.anchoredPosition = new Vector2(0, y + 20f);
            sep.AddComponent<Image>().color = new Color(0.5f, 0.4f, 0.2f, 0.5f);

            // Buttons
            CreateMenuButton(box.transform, "Resume", new Vector2(0, y), () => Resume());
            y -= 50f;
            CreateMenuButton(box.transform, "Restart Battle", new Vector2(0, y), () =>
            {
                Time.timeScale = 1f;
                LoadingScreenUI.LoadSceneWithScreen(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            });
            y -= 50f;
            CreateMenuButton(box.transform, "Quit to Menu", new Vector2(0, y), () =>
            {
                Time.timeScale = 1f;
                if (GameManager.Instance != null)
                    GameManager.Instance.ReturnToMenu();
                else
                    LoadingScreenUI.LoadSceneWithScreen("MainMenu");
            });
        }

        private void SetGraphicsQuality(GraphicsQuality quality)
        {
            if (GraphicsSettings.Instance != null)
                GraphicsSettings.Instance.SetQuality(quality);
            UpdateGraphicsButtons();
        }

        private void UpdateGraphicsButtons()
        {
            GraphicsQuality current = GraphicsSettings.Instance != null ? GraphicsSettings.Instance.CurrentQuality : GraphicsQuality.Medium;
            Color active = new Color(0.4f, 0.35f, 0.15f);
            Color inactive = new Color(0.2f, 0.18f, 0.12f);

            if (gfxLowImg != null) gfxLowImg.color = current == GraphicsQuality.Low ? active : inactive;
            if (gfxMedImg != null) gfxMedImg.color = current == GraphicsQuality.Medium ? active : inactive;
            if (gfxHighImg != null) gfxHighImg.color = current == GraphicsQuality.High ? active : inactive;
        }

        private void LateUpdate()
        {
            // FPS counter (always updates, even when paused via unscaledDeltaTime)
            fpsFrameCount++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer >= 0.5f)
            {
                float fps = fpsFrameCount / fpsTimer;
                if (fpsText != null)
                    fpsText.text = $"FPS: {fps:F0}";
                fpsFrameCount = 0;
                fpsTimer = 0f;
            }
        }

        // === UI HELPERS ===

        private Text CreateLabel(Transform parent, string text, int fontSize, FontStyle style, Vector2 pos, Color color)
        {
            GameObject go = new GameObject(text);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200, 30);
            rt.anchoredPosition = pos;
            Text t = go.AddComponent<Text>();
            t.text = text;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            return t;
        }

        private Slider CreateSlider(Transform parent, Vector2 pos, float defaultValue, float min = 0f, float max = 1f)
        {
            GameObject sliderGO = new GameObject("Slider");
            sliderGO.transform.SetParent(parent, false);
            RectTransform rt = sliderGO.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160, 20);
            rt.anchoredPosition = pos;

            // Background
            GameObject bgGO = new GameObject("Background");
            bgGO.transform.SetParent(sliderGO.transform, false);
            RectTransform bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            Image bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.18f, 0.12f);

            // Fill area
            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderGO.transform, false);
            RectTransform fillAreaRT = fillArea.AddComponent<RectTransform>();
            fillAreaRT.anchorMin = new Vector2(0, 0.25f);
            fillAreaRT.anchorMax = new Vector2(1, 0.75f);
            fillAreaRT.offsetMin = new Vector2(5, 0);
            fillAreaRT.offsetMax = new Vector2(-5, 0);

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fillRT = fill.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.7f, 0.6f, 0.25f);

            // Handle
            GameObject handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderGO.transform, false);
            RectTransform handleAreaRT = handleArea.AddComponent<RectTransform>();
            handleAreaRT.anchorMin = Vector2.zero;
            handleAreaRT.anchorMax = Vector2.one;
            handleAreaRT.offsetMin = new Vector2(10, 0);
            handleAreaRT.offsetMax = new Vector2(-10, 0);

            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            RectTransform handleRT = handle.AddComponent<RectTransform>();
            handleRT.sizeDelta = new Vector2(16, 24);
            Image handleImg = handle.AddComponent<Image>();
            handleImg.color = new Color(0.9f, 0.85f, 0.5f);

            Slider slider = sliderGO.AddComponent<Slider>();
            slider.fillRect = fillRT;
            slider.handleRect = handleRT;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = defaultValue;
            slider.targetGraphic = handleImg;

            return slider;
        }

        private void CreateMenuButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnGO = new GameObject($"Btn_{label}");
            btnGO.transform.SetParent(parent, false);
            RectTransform rt = btnGO.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(250, 38);
            rt.anchoredPosition = pos;

            Image bg = btnGO.AddComponent<Image>();
            bg.color = new Color(0.25f, 0.2f, 0.12f);

            Button btn = btnGO.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.highlightedColor = new Color(0.4f, 0.35f, 0.15f);
            cb.pressedColor = new Color(0.5f, 0.45f, 0.2f);
            btn.colors = cb;
            btn.onClick.AddListener(onClick);

            GameObject txtGO = new GameObject("Label");
            txtGO.transform.SetParent(btnGO.transform, false);
            RectTransform txtRT = txtGO.AddComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = Vector2.zero;
            txtRT.offsetMax = Vector2.zero;
            Text t = txtGO.AddComponent<Text>();
            t.text = label;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize = 18;
            t.fontStyle = FontStyle.Bold;
            t.color = new Color(0.9f, 0.85f, 0.6f);
            t.alignment = TextAnchor.MiddleCenter;
        }
    }
}
