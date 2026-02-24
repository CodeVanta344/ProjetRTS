using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Core;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// Post-battle result screen showing statistics, rating, and options.
    /// Appears when the battle ends (victory or defeat).
    /// </summary>
    public class BattleResultUI : MonoBehaviour
    {
        private Canvas canvas;
        private GameObject panel;
        private bool isShowing;

        private void Start()
        {
            BuildUI();
            panel.SetActive(false);
        }

        private void Update()
        {
            if (isShowing) return;
            if (BattleStatistics.Instance == null) return;
            if (!BattleStatistics.Instance.BattleEnded) return;

            ShowResults();
        }

        private void ShowResults()
        {
            isShowing = true;
            panel.SetActive(true);
            Time.timeScale = 0f; // Pause game
        }

        private void BuildUI()
        {
            // Canvas
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            gameObject.AddComponent<GraphicRaycaster>();

            // Darkened overlay
            panel = new GameObject("ResultPanel");
            panel.transform.SetParent(canvas.transform, false);
            RectTransform panelRT = panel.AddComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;
            Image panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0f, 0f, 0f, 0.85f);

            // Center box
            GameObject box = CreateBox(panel.transform, 600f, 520f);

            float y = 220f;

            // Title
            CreateText(box.transform, "TITLE", "", 36, FontStyle.Bold, new Vector2(0, y), new Color(1f, 0.85f, 0.3f));
            y -= 55f;

            // Separator
            CreateSeparator(box.transform, y + 15f);
            y -= 10f;

            // Stats rows
            string[] labels = {
                "STAT_DURATION", "STAT_KILLS", "STAT_LOSSES", "STAT_KD",
                "STAT_SHOTS", "STAT_ACCURACY", "STAT_SURVIVAL", "STAT_RATING"
            };

            foreach (string label in labels)
            {
                CreateText(box.transform, label, "", 20, FontStyle.Normal, new Vector2(0, y), Color.white);
                y -= 30f;
            }

            y -= 10f;
            CreateSeparator(box.transform, y + 15f);
            y -= 20f;

            // Buttons
            CreateButton(box.transform, "Continue", new Vector2(-100, y), () =>
            {
                Time.timeScale = 1f;
                panel.SetActive(false);
                // Return to main menu or campaign
                if (GameManager.Instance != null)
                    GameManager.Instance.ReturnToMenu();
            });

            CreateButton(box.transform, "Replay", new Vector2(100, y), () =>
            {
                Time.timeScale = 1f;
                panel.SetActive(false);
                LoadingScreenUI.LoadSceneWithScreen(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            });
        }

        private void OnEnable()
        {
            if (panel != null && isShowing)
                PopulateStats();
        }

        private void PopulateStats()
        {
            var stats = BattleStatistics.Instance;
            if (stats == null) return;

            bool won = stats.PlayerWon;

            SetText("TITLE", won ? "VICTORY!" : "DEFEAT");
            var titleObj = panel.transform.Find("TITLE");
            if (titleObj != null)
            {
                Text t = titleObj.GetComponent<Text>();
                if (t != null)
                    t.color = won ? new Color(1f, 0.85f, 0.3f) : new Color(0.9f, 0.2f, 0.2f);
            }

            SetText("STAT_DURATION", $"Battle Duration:  {stats.GetBattleDurationString()}");
            SetText("STAT_KILLS", $"Enemy Killed:  {stats.PlayerKills}");
            SetText("STAT_LOSSES", $"Units Lost:  {stats.PlayerLosses} / {stats.PlayerStartingUnits}");
            SetText("STAT_KD", $"Kill/Death Ratio:  {stats.PlayerKDRatio:F1}");
            SetText("STAT_SHOTS", $"Shots Fired:  {stats.PlayerShotsFired}  |  Hits: {stats.PlayerShotsHit}");
            SetText("STAT_ACCURACY", $"Accuracy:  {stats.PlayerAccuracy * 100f:F1}%");
            SetText("STAT_SURVIVAL", $"Survival Rate:  {stats.PlayerSurvivalRate * 100f:F1}%");

            string rating = stats.GetBattleRating();
            Color ratingColor = rating switch
            {
                "S" => new Color(1f, 0.85f, 0f),
                "A" => new Color(0.3f, 1f, 0.3f),
                "B" => new Color(0.3f, 0.7f, 1f),
                "C" => new Color(0.8f, 0.8f, 0.8f),
                _ => new Color(0.9f, 0.3f, 0.3f)
            };
            SetText("STAT_RATING", $"Battle Rating:  {rating}");
            var ratingObj = panel.transform.Find("STAT_RATING");
            if (ratingObj != null)
            {
                Text t = ratingObj.GetComponent<Text>();
                if (t != null)
                {
                    t.color = ratingColor;
                    t.fontSize = 28;
                    t.fontStyle = FontStyle.Bold;
                }
            }
        }

        private void SetText(string name, string value)
        {
            Transform t = panel.transform.Find(name);
            if (t != null)
            {
                Text txt = t.GetComponent<Text>();
                if (txt != null) txt.text = value;
            }
        }

        // === UI HELPERS ===

        private GameObject CreateBox(Transform parent, float width, float height)
        {
            GameObject box = new GameObject("Box");
            box.transform.SetParent(parent, false);
            RectTransform rt = box.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = Vector2.zero;
            Image bg = box.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.1f, 0.08f, 0.95f);

            // Border
            Outline outline = box.AddComponent<Outline>();
            outline.effectColor = new Color(0.6f, 0.5f, 0.2f);
            outline.effectDistance = new Vector2(2, 2);

            return box;
        }

        private void CreateText(Transform parent, string name, string text, int fontSize, FontStyle style, Vector2 pos, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(500, 35);
            rt.anchoredPosition = pos;
            Text t = go.AddComponent<Text>();
            t.text = text;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
        }

        private void CreateSeparator(Transform parent, float yPos)
        {
            GameObject sep = new GameObject("Separator");
            sep.transform.SetParent(parent, false);
            RectTransform rt = sep.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(450, 2);
            rt.anchoredPosition = new Vector2(0, yPos);
            Image img = sep.AddComponent<Image>();
            img.color = new Color(0.5f, 0.4f, 0.2f, 0.6f);
        }

        private void CreateButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnGO = new GameObject($"Btn_{label}");
            btnGO.transform.SetParent(parent, false);
            RectTransform rt = btnGO.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160, 40);
            rt.anchoredPosition = pos;

            Image bg = btnGO.AddComponent<Image>();
            bg.color = new Color(0.25f, 0.2f, 0.12f);

            Button btn = btnGO.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.highlightedColor = new Color(0.4f, 0.35f, 0.15f);
            cb.pressedColor = new Color(0.5f, 0.45f, 0.2f);
            btn.colors = cb;
            btn.onClick.AddListener(onClick);

            // Label
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
