using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Core;
using System.Collections.Generic;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// HUD for siege battles. Shows fortification status, capture points, and siege controls.
    /// </summary>
    public class SiegeHUD : MonoBehaviour
    {
        private Canvas canvas;

        // Fortification status panel
        private RectTransform fortPanel;
        private Image wallHealthBar;
        private Image gateHealthBar;
        private Text wallText;
        private Text gateText;
        private Text breachText;

        // Capture points panel
        private RectTransform capturePanel;

        // Timer
        private Text timerText;

        // Controls
        private RectTransform controlsPanel;

        // Assault orders panel
        private RectTransform ordersPanel;

        private void Start()
        {
            BuildUI();
        }

        private void Update()
        {
            UpdateFortificationStatus();
            UpdateTimer();
        }

        private void BuildUI()
        {
            canvas = UIFactory.CreateCanvas("SiegeHUD", 10);
            canvas.transform.SetParent(transform);

            BuildFortificationPanel();
            BuildCapturePanel();
            BuildOrdersPanel();
            BuildTimer();
            BuildControlsHelp();
        }

        private void BuildFortificationPanel()
        {
            fortPanel = UIFactory.CreatePanel(canvas.transform, "FortPanel", new Color(0.15f, 0.1f, 0.1f, 0.9f));
            UIFactory.SetAnchors(fortPanel, new Vector2(0.02f, 0.7f), new Vector2(0.2f, 0.98f), Vector2.zero, Vector2.zero);

            Text title = UIFactory.CreateText(fortPanel, "Title", "FORTIFICATIONS", 14, TextAnchor.MiddleCenter, UIFactory.TextGold);
            UIFactory.SetAnchors(title.gameObject, new Vector2(0f, 0.88f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            // Wall health
            Text wallLabel = UIFactory.CreateText(fortPanel, "WallLabel", "Walls:", 11, TextAnchor.MiddleLeft, UIFactory.TextGrey);
            UIFactory.SetAnchors(wallLabel.gameObject, new Vector2(0.05f, 0.7f), new Vector2(0.3f, 0.82f), Vector2.zero, Vector2.zero);

            RectTransform wallBg = UIFactory.CreatePanel(fortPanel, "WallBg", new Color(0.2f, 0.2f, 0.2f));
            UIFactory.SetAnchors(wallBg, new Vector2(0.32f, 0.72f), new Vector2(0.85f, 0.8f), Vector2.zero, Vector2.zero);

            RectTransform wallFill = UIFactory.CreatePanel(wallBg, "WallFill", new Color(0.5f, 0.4f, 0.3f));
            wallHealthBar = wallFill.GetComponent<Image>();
            wallFill.anchorMin = Vector2.zero;
            wallFill.anchorMax = Vector2.one;
            wallFill.offsetMin = Vector2.zero;
            wallFill.offsetMax = Vector2.zero;

            wallText = UIFactory.CreateText(fortPanel, "WallText", "100%", 10, TextAnchor.MiddleRight, Color.white);
            UIFactory.SetAnchors(wallText.gameObject, new Vector2(0.86f, 0.7f), new Vector2(0.98f, 0.82f), Vector2.zero, Vector2.zero);

            // Gate health
            Text gateLabel = UIFactory.CreateText(fortPanel, "GateLabel", "Gate:", 11, TextAnchor.MiddleLeft, UIFactory.TextGrey);
            UIFactory.SetAnchors(gateLabel.gameObject, new Vector2(0.05f, 0.52f), new Vector2(0.3f, 0.64f), Vector2.zero, Vector2.zero);

            RectTransform gateBg = UIFactory.CreatePanel(fortPanel, "GateBg", new Color(0.2f, 0.2f, 0.2f));
            UIFactory.SetAnchors(gateBg, new Vector2(0.32f, 0.54f), new Vector2(0.85f, 0.62f), Vector2.zero, Vector2.zero);

            RectTransform gateFill = UIFactory.CreatePanel(gateBg, "GateFill", new Color(0.4f, 0.25f, 0.15f));
            gateHealthBar = gateFill.GetComponent<Image>();
            gateFill.anchorMin = Vector2.zero;
            gateFill.anchorMax = Vector2.one;
            gateFill.offsetMin = Vector2.zero;
            gateFill.offsetMax = Vector2.zero;

            gateText = UIFactory.CreateText(fortPanel, "GateText", "100%", 10, TextAnchor.MiddleRight, Color.white);
            UIFactory.SetAnchors(gateText.gameObject, new Vector2(0.86f, 0.52f), new Vector2(0.98f, 0.64f), Vector2.zero, Vector2.zero);

            // Breach status
            breachText = UIFactory.CreateText(fortPanel, "BreachText", "No breaches", 12, TextAnchor.MiddleCenter, UIFactory.TextGrey);
            UIFactory.SetAnchors(breachText.gameObject, new Vector2(0.05f, 0.35f), new Vector2(0.95f, 0.48f), Vector2.zero, Vector2.zero);
        }

        private void BuildCapturePanel()
        {
            capturePanel = UIFactory.CreatePanel(canvas.transform, "CapturePanel", new Color(0.1f, 0.15f, 0.1f, 0.9f));
            UIFactory.SetAnchors(capturePanel, new Vector2(0.4f, 0.85f), new Vector2(0.6f, 0.98f), Vector2.zero, Vector2.zero);

            Text title = UIFactory.CreateText(capturePanel, "Title", "CAPTURE POINTS", 12, TextAnchor.MiddleCenter, UIFactory.TextGold);
            UIFactory.SetAnchors(title.gameObject, new Vector2(0f, 0.7f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        }

        private void BuildOrdersPanel()
        {
            ordersPanel = UIFactory.CreatePanel(canvas.transform, "OrdersPanel", new Color(0.15f, 0.15f, 0.2f, 0.9f));
            UIFactory.SetAnchors(ordersPanel, new Vector2(0.02f, 0.02f), new Vector2(0.2f, 0.25f), Vector2.zero, Vector2.zero);

            Text title = UIFactory.CreateText(ordersPanel, "Title", "SIEGE ORDERS", 12, TextAnchor.MiddleCenter, UIFactory.TextGold);
            UIFactory.SetAnchors(title.gameObject, new Vector2(0f, 0.85f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            // Assault wall button
            Button btnAssaultWall = UIFactory.CreateButton(ordersPanel, "BtnAssaultWall", "Assault Wall", 11, OnAssaultWall);
            UIFactory.SetAnchors(btnAssaultWall.gameObject, new Vector2(0.05f, 0.55f), new Vector2(0.95f, 0.75f), Vector2.zero, Vector2.zero);

            // Assault gate button
            Button btnAssaultGate = UIFactory.CreateButton(ordersPanel, "BtnAssaultGate", "Assault Gate", 11, OnAssaultGate);
            UIFactory.SetAnchors(btnAssaultGate.gameObject, new Vector2(0.05f, 0.3f), new Vector2(0.95f, 0.5f), Vector2.zero, Vector2.zero);

            // Bombard button
            Button btnBombard = UIFactory.CreateButton(ordersPanel, "BtnBombard", "Bombard Walls", 11, OnBombard);
            UIFactory.SetAnchors(btnBombard.gameObject, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.25f), Vector2.zero, Vector2.zero);
        }

        private void BuildTimer()
        {
            timerText = UIFactory.CreateText(canvas.transform, "Timer", "30:00", 24, TextAnchor.MiddleCenter, Color.white);
            UIFactory.SetAnchors(timerText.gameObject, new Vector2(0.45f, 0.92f), new Vector2(0.55f, 0.98f), Vector2.zero, Vector2.zero);
        }

        private void BuildControlsHelp()
        {
            controlsPanel = UIFactory.CreatePanel(canvas.transform, "ControlsPanel", new Color(0.1f, 0.1f, 0.1f, 0.7f));
            UIFactory.SetAnchors(controlsPanel, new Vector2(0.3f, 0.02f), new Vector2(0.7f, 0.08f), Vector2.zero, Vector2.zero);

            string controls = "LMB: Select | RMB: Move | 1-4: Formations | V: Volley Fire";
            Text controlsText = UIFactory.CreateText(controlsPanel, "Controls", controls, 11, TextAnchor.MiddleCenter, UIFactory.TextGrey);
            UIFactory.SetAnchors(controlsText.gameObject, new Vector2(0.02f, 0f), new Vector2(0.98f, 1f), Vector2.zero, Vector2.zero);
        }

        private void UpdateFortificationStatus()
        {
            var siege = SiegeBattleManager.Instance;
            if (siege == null) return;

            float wallHealth = 0.75f;
            float gateHealth = 0.5f;
            int breaches = 1;

            if (wallHealthBar != null)
            {
                wallHealthBar.rectTransform.anchorMax = new Vector2(wallHealth, 1f);
                wallText.text = $"{wallHealth * 100f:F0}%";
            }

            if (gateHealthBar != null)
            {
                gateHealthBar.rectTransform.anchorMax = new Vector2(gateHealth, 1f);
                gateText.text = $"{gateHealth * 100f:F0}%";
                
                if (gateHealth <= 0)
                {
                    gateText.text = "DESTROYED";
                    gateText.color = Color.red;
                }
            }

            if (breachText != null)
            {
                if (breaches > 0)
                {
                    breachText.text = $"{breaches} BREACH(ES)!";
                    breachText.color = new Color(0.9f, 0.6f, 0.2f);
                }
                else
                {
                    breachText.text = "No breaches";
                    breachText.color = UIFactory.TextGrey;
                }
            }
        }

        private void UpdateTimer()
        {
            var siege = SiegeBattleManager.Instance;
            if (siege == null) return;

            float time = siege.BattleTimeRemaining;
            int minutes = (int)(time / 60f);
            int seconds = (int)(time % 60f);
            timerText.text = $"{minutes:D2}:{seconds:D2}";
        }

        private void OnAssaultWall()
        {
            Debug.Log("[SiegeHUD] Assault Wall ordered");
        }

        private void OnAssaultGate()
        {
            Debug.Log("[SiegeHUD] Assault Gate ordered");
        }

        private void OnBombard()
        {
            Debug.Log("[SiegeHUD] Bombard ordered");
        }
    }
}
