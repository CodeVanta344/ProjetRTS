using UnityEngine;
using UnityEngine.UI;
using System.Text;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// UI for the deployment phase before battle.
    /// Shows timer, ready status for both players, and the ready button.
    /// </summary>
    public class DeploymentUI : MonoBehaviour
    {
        private Canvas canvas;
        private Text timerText;
        private Text statusText;
        private Button readyButton;
        private Text readyButtonText;
        private Image readyButtonImage;

        private Color readyColor = new Color(0.2f, 0.7f, 0.2f, 1f);
        private Color notReadyColor = new Color(0.7f, 0.2f, 0.2f, 1f);
        private Color waitingColor = new Color(0.7f, 0.7f, 0.2f, 1f);

        private void Awake()
        {
            CreateUI();
        }

        private void OnEnable()
        {
            if (Core.BattleManager.Instance != null)
            {
                Core.BattleManager.Instance.OnPlayerReadyChanged += UpdateUI;
                Core.BattleManager.Instance.OnAIReadyChanged += UpdateUI;
                Core.BattleManager.Instance.OnDeploymentEnded += HideUI;
            }
        }

        private void OnDisable()
        {
            if (Core.BattleManager.Instance != null)
            {
                Core.BattleManager.Instance.OnPlayerReadyChanged -= UpdateUI;
                Core.BattleManager.Instance.OnAIReadyChanged -= UpdateUI;
                Core.BattleManager.Instance.OnDeploymentEnded -= HideUI;
            }
        }

        private void CreateUI()
        {
            // Create canvas
            GameObject canvasGO = new GameObject("DeploymentCanvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // Background panel (top of screen)
            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(canvasGO.transform, false);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -30f);
            panelRect.sizeDelta = new Vector2(-40f, 60f);

            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.6f);

            // Timer text
            GameObject timerGO = new GameObject("TimerText");
            timerGO.transform.SetParent(panel.transform, false);
            RectTransform timerRect = timerGO.AddComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0f, 0.5f);
            timerRect.anchorMax = new Vector2(0f, 0.5f);
            timerRect.pivot = new Vector2(0f, 0.5f);
            timerRect.anchoredPosition = new Vector2(20f, 0f);
            timerRect.sizeDelta = new Vector2(200f, 40f);

            timerText = timerGO.AddComponent<Text>();
            timerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            timerText.fontSize = 28;
            timerText.color = Color.white;
            timerText.alignment = TextAnchor.MiddleLeft;
            timerText.text = "Time: 120s";

            // Status text (center)
            GameObject statusGO = new GameObject("StatusText");
            statusGO.transform.SetParent(panel.transform, false);
            RectTransform statusRect = statusGO.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.5f, 0.5f);
            statusRect.anchorMax = new Vector2(0.5f, 0.5f);
            statusRect.pivot = new Vector2(0.5f, 0.5f);
            statusRect.anchoredPosition = new Vector2(0f, 0f);
            statusRect.sizeDelta = new Vector2(400f, 40f);

            statusText = statusGO.AddComponent<Text>();
            statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusText.fontSize = 24;
            statusText.color = Color.white;
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.text = "You: Not Ready | AI: Not Ready";

            // Ready button (right side)
            GameObject buttonGO = new GameObject("ReadyButton");
            buttonGO.transform.SetParent(panel.transform, false);
            RectTransform buttonRect = buttonGO.AddComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(1f, 0.5f);
            buttonRect.anchorMax = new Vector2(1f, 0.5f);
            buttonRect.pivot = new Vector2(1f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(-20f, 0f);
            buttonRect.sizeDelta = new Vector2(180f, 45f);

            readyButtonImage = buttonGO.AddComponent<Image>();
            readyButtonImage.color = notReadyColor;

            readyButton = buttonGO.AddComponent<Button>();
            readyButton.onClick.AddListener(OnReadyButtonClicked);

            // Button text
            GameObject buttonTextGO = new GameObject("Text");
            buttonTextGO.transform.SetParent(buttonGO.transform, false);
            RectTransform buttonTextRect = buttonTextGO.AddComponent<RectTransform>();
            buttonTextRect.anchorMin = Vector2.zero;
            buttonTextRect.anchorMax = Vector2.one;
            buttonTextRect.offsetMin = Vector2.zero;
            buttonTextRect.offsetMax = Vector2.zero;

            readyButtonText = buttonTextGO.AddComponent<Text>();
            readyButtonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            readyButtonText.fontSize = 20;
            readyButtonText.color = Color.white;
            readyButtonText.alignment = TextAnchor.MiddleCenter;
            readyButtonText.text = "READY";
        }

        private void Update()
        {
            if (Core.BattleManager.Instance == null) return;
            if (!Core.BattleManager.Instance.IsDeploymentPhase) return;

            UpdateTimer();
            UpdateStatusText();
        }

        private void UpdateTimer()
        {
            float timeRemaining = Core.BattleManager.Instance.DeploymentTimeRemaining;
            timerText.text = $"Time: {timeRemaining:F0}s";
            
            // Change color when time is running low
            if (timeRemaining < 30f)
                timerText.color = Color.red;
            else if (timeRemaining < 60f)
                timerText.color = new Color(1f, 0.7f, 0.2f);
            else
                timerText.color = Color.white;
        }

        private void UpdateStatusText()
        {
            var bm = Core.BattleManager.Instance;
            StringBuilder sb = new StringBuilder();

            sb.Append("You: ");
            sb.Append(bm.PlayerReady ? "<color=green>Ready</color>" : "<color=red>Not Ready</color>");
            sb.Append(" | AI: ");
            sb.Append(bm.AIReady ? "<color=green>Ready</color>" : "<color=red>Not Ready</color>");

            statusText.text = sb.ToString();
        }

        private void UpdateUI()
        {
            if (Core.BattleManager.Instance == null) return;

            bool playerReady = Core.BattleManager.Instance.PlayerReady;

            // Update button appearance
            readyButtonImage.color = playerReady ? readyColor : notReadyColor;
            readyButtonText.text = playerReady ? "CANCEL" : "READY";
        }

        private void OnReadyButtonClicked()
        {
            if (Core.BattleManager.Instance != null)
            {
                Core.BattleManager.Instance.TogglePlayerReady();
            }
        }

        private void HideUI()
        {
            if (canvas != null)
                canvas.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            OnDisable();
        }
    }
}
