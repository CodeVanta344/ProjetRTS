using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Naval;
using System.Collections.Generic;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// HUD for naval battles. Shows wind, ship status, and controls.
    /// </summary>
    public class NavalBattleHUD : MonoBehaviour
    {
        private Canvas canvas;

        // Wind indicator
        private RectTransform windPanel;
        private RectTransform windArrow;
        private Text windStrengthText;

        // Selected ship panel
        private RectTransform shipPanel;
        private Text shipNameText;
        private Text shipTypeText;
        private Image hullBar;
        private Image sailBar;
        private Image crewBar;
        private Text hullText;
        private Text sailText;
        private Text crewText;
        private Text cannonsText;
        private Text speedText;
        private Text reloadLeftText;
        private Text reloadRightText;

        // Fleet overview
        private RectTransform fleetPanel;
        private List<GameObject> shipIcons = new List<GameObject>();

        // Controls help
        private RectTransform controlsPanel;

        // Battle timer
        private Text timerText;

        private void Start()
        {
            BuildUI();
        }

        private void Update()
        {
            UpdateWindIndicator();
            UpdateSelectedShipPanel();
            UpdateFleetOverview();
            UpdateTimer();
        }

        #region UI Building

        private void BuildUI()
        {
            canvas = UIFactory.CreateCanvas("NavalBattleHUD", 10);
            canvas.transform.SetParent(transform);

            BuildWindIndicator();
            BuildShipPanel();
            BuildFleetOverview();
            BuildControlsHelp();
            BuildTimer();
        }

        private void BuildWindIndicator()
        {
            windPanel = UIFactory.CreateOrnatePanel(canvas.transform, "WindPanel", UIFactory.DeepCharcoal);
            UIFactory.SetAnchors(windPanel.gameObject, new Vector2(0.01f, 0.78f), new Vector2(0.12f, 0.98f), Vector2.zero, Vector2.zero);

            Text title = UIFactory.CreateText(windPanel.Find("Inner"), "Title", "WIND DIRECTION", 12, TextAnchor.MiddleCenter, UIFactory.EmpireGold);
            title.fontStyle = FontStyle.Bold;
            UIFactory.SetAnchors(title.gameObject, new Vector2(0f, 0.82f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            // Wind arrow (compass rose style)
            GameObject arrowGO = new GameObject("WindArrow");
            arrowGO.transform.SetParent(windPanel.Find("Inner"), false);
            windArrow = arrowGO.AddComponent<RectTransform>();
            UIFactory.SetAnchors(arrowGO, new Vector2(0.2f, 0.25f), new Vector2(0.8f, 0.75f), Vector2.zero, Vector2.zero);

            // Arrow visual
            Image arrowImg = arrowGO.AddComponent<Image>();
            arrowImg.color = Color.white;

            // Create arrow shape using child
            GameObject arrowHead = new GameObject("ArrowHead");
            arrowHead.transform.SetParent(arrowGO.transform, false);
            Image headImg = arrowHead.AddComponent<Image>();
            headImg.color = UIFactory.EmpireGold;
            RectTransform headRect = arrowHead.GetComponent<RectTransform>();
            headRect.anchorMin = new Vector2(0.35f, 0.55f);
            headRect.anchorMax = new Vector2(0.65f, 1f);
            headRect.offsetMin = Vector2.zero;
            headRect.offsetMax = Vector2.zero;

            windStrengthText = UIFactory.CreateText(windPanel.Find("Inner"), "Strength", "CALM", 11, TextAnchor.MiddleCenter, UIFactory.SilverText);
            UIFactory.SetAnchors(windStrengthText.gameObject, new Vector2(0f, 0f), new Vector2(1f, 0.2f), Vector2.zero, Vector2.zero);
        }

        private void BuildShipPanel()
        {
            RectTransform panel = UIFactory.CreateOrnatePanel(canvas.transform, "ShipPanel", UIFactory.DeepCharcoal);
            UIFactory.SetAnchors(panel.gameObject, new Vector2(0.01f, 0.02f), new Vector2(0.22f, 0.35f), Vector2.zero, Vector2.zero);
            shipPanel = panel;

            Transform inner = panel.Find("Inner");
            UIFactory.AddVerticalLayout(inner.gameObject, 0f, new RectOffset(12, 12, 8, 8));

            // Ship name
            shipNameText = UIFactory.CreateText(inner, "Name", "NO SHIP SELECTED", 14, TextAnchor.MiddleLeft, UIFactory.EmpireGold);
            shipNameText.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(shipNameText.gameObject, preferredHeight: 24);

            UIFactory.CreateSeparator(inner);

            // Ship type
            shipTypeText = UIFactory.CreateText(inner, "Type", "", 12, TextAnchor.MiddleLeft, UIFactory.SilverText);
            UIFactory.AddLayoutElement(shipTypeText.gameObject, preferredHeight: 20);

            // Stat bars
            CreateStatBar(inner, "HULL", UIFactory.ImperialCrimson, out hullBar, out hullText);
            CreateStatBar(inner, "SAILS", UIFactory.Parchment, out sailBar, out sailText);
            CreateStatBar(inner, "CREW", UIFactory.SilverText, out crewBar, out crewText);

            UIFactory.CreateSeparator(inner);

            // Info rows
            GameObject info = new GameObject("Info");
            info.transform.SetParent(inner, false);
            UIFactory.AddHorizontalLayout(info, 10f);
            UIFactory.AddLayoutElement(info, preferredHeight: 20);

            cannonsText = UIFactory.CreateText(info.transform, "Cannons", "CANNONS: 0", 11, TextAnchor.MiddleLeft, UIFactory.Porcelain);
            speedText = UIFactory.CreateText(info.transform, "Speed", "0.0 KTS", 11, TextAnchor.MiddleRight, UIFactory.EmpireGold);

            // Reload status
            GameObject reload = new GameObject("Reload");
            reload.transform.SetParent(inner, false);
            UIFactory.AddHorizontalLayout(reload, 10f);
            UIFactory.AddLayoutElement(reload, preferredHeight: 20);

            reloadLeftText = UIFactory.CreateText(reload.transform, "Port", "PORT: READY", 10, TextAnchor.MiddleLeft, UIFactory.EmpireGold);
            reloadRightText = UIFactory.CreateText(reload.transform, "Starboard", "STARBOARD: READY", 10, TextAnchor.MiddleRight, UIFactory.EmpireGold);

            shipPanel.gameObject.SetActive(false);
        }

        private void CreateStatBar(Transform parent, string label, Color color, out Image fill, out Text value)
        {
            GameObject row = new GameObject(label + "Row");
            row.transform.SetParent(parent, false);
            UIFactory.AddHorizontalLayout(row, 6f);
            UIFactory.AddLayoutElement(row, preferredHeight: 14);

            UIFactory.CreateText(row.transform, "Lbl", label, 10, TextAnchor.MiddleLeft, UIFactory.SilverText);
            var (bg, f) = UIFactory.CreateProgressBar(row.transform, "Bar", color);
            fill = f;
            UIFactory.AddLayoutElement(bg.gameObject, flexibleWidth: 1);
            
            value = UIFactory.CreateText(row.transform, "Val", "100%", 10, TextAnchor.MiddleRight, UIFactory.Porcelain);
            UIFactory.AddLayoutElement(value.gameObject, preferredWidth: 40);
        }



        private void BuildFleetOverview()
        {
            fleetPanel = UIFactory.CreatePanel(canvas.transform, "FleetPanel", new Color(0.1f, 0.15f, 0.2f, 0.7f));
            UIFactory.SetAnchors(fleetPanel, new Vector2(0.75f, 0.02f), new Vector2(0.98f, 0.15f), Vector2.zero, Vector2.zero);

            Text title = UIFactory.CreateText(fleetPanel, "Title", "YOUR FLEET", 12, TextAnchor.MiddleCenter, UIFactory.TextGold);
            UIFactory.SetAnchors(title.gameObject, new Vector2(0f, 0.8f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        }

        private void BuildControlsHelp()
        {
            controlsPanel = UIFactory.CreatePanel(canvas.transform, "ControlsPanel", new Color(0.1f, 0.1f, 0.1f, 0.7f));
            UIFactory.SetAnchors(controlsPanel, new Vector2(0.35f, 0.02f), new Vector2(0.65f, 0.12f), Vector2.zero, Vector2.zero);

            string controls = "LMB: Select | RMB: Move | W/S: Speed | F: Fire Port | G: Fire Starboard | B: Board";
            Text controlsText = UIFactory.CreateText(controlsPanel, "Controls", controls, 11, TextAnchor.MiddleCenter, UIFactory.TextGrey);
            UIFactory.SetAnchors(controlsText.gameObject, new Vector2(0.02f, 0f), new Vector2(0.98f, 1f), Vector2.zero, Vector2.zero);
        }

        private void BuildTimer()
        {
            timerText = UIFactory.CreateText(canvas.transform, "Timer", "30:00", 24, TextAnchor.MiddleCenter, Color.white);
            UIFactory.SetAnchors(timerText.gameObject, new Vector2(0.45f, 0.92f), new Vector2(0.55f, 0.98f), Vector2.zero, Vector2.zero);
        }

        #endregion

        #region Updates

        private void UpdateWindIndicator()
        {
            var naval = NavalBattleManager.Instance;
            if (naval == null) return;

            // Rotate arrow to show wind direction
            if (windArrow != null)
            {
                windArrow.localRotation = Quaternion.Euler(0, 0, -naval.WindDirection);
            }

            // Update strength text
            if (windStrengthText != null)
            {
                string strength;
                if (naval.WindStrength < 0.5f) strength = "Calm";
                else if (naval.WindStrength < 1f) strength = "Light";
                else if (naval.WindStrength < 1.5f) strength = "Moderate";
                else strength = "Strong";

                windStrengthText.text = strength;
            }
        }

        private void UpdateSelectedShipPanel()
        {
            var naval = NavalBattleManager.Instance;
            if (naval == null || naval.SelectedShips.Count == 0)
            {
                shipPanel.gameObject.SetActive(false);
                return;
            }

            shipPanel.gameObject.SetActive(true);
            Ship ship = naval.SelectedShips[0];

            shipNameText.text = ship.Data.shipName;
            shipTypeText.text = ship.Data.shipType.ToString();

            // Update bars
            UpdateBar(hullBar, hullText, ship.HullPercent);
            UpdateBar(sailBar, sailText, ship.SailPercent);
            UpdateBar(crewBar, crewText, ship.CrewPercent);

            // Cannons
            cannonsText.text = $"Cannons: {ship.Data.cannonCount}";

            // Speed
            speedText.text = $"Speed: {ship.Speed:F1} kts";

            // Reload status
            reloadLeftText.text = ship.CanFireLeft ? "Port: READY" : "Port: Reloading...";
            reloadLeftText.color = ship.CanFireLeft ? new Color(0.5f, 0.9f, 0.5f) : new Color(0.9f, 0.5f, 0.5f);

            reloadRightText.text = ship.CanFireRight ? "Starboard: READY" : "Starboard: Reloading...";
            reloadRightText.color = ship.CanFireRight ? new Color(0.5f, 0.9f, 0.5f) : new Color(0.9f, 0.5f, 0.5f);
        }

        private void UpdateBar(Image bar, Text text, float percent)
        {
            if (bar != null)
            {
                bar.rectTransform.anchorMax = new Vector2(percent, 1f);
            }
            if (text != null)
            {
                text.text = $"{percent * 100f:F0}%";
            }
        }

        private void UpdateFleetOverview()
        {
            var naval = NavalBattleManager.Instance;
            if (naval == null) return;

            // Clear old icons
            foreach (var icon in shipIcons)
            {
                if (icon != null) Destroy(icon);
            }
            shipIcons.Clear();

            // Create icons for player ships
            float xPos = 0.05f;
            foreach (var ship in naval.PlayerFleet)
            {
                if (!ship.IsOperational) continue;

                GameObject icon = CreateShipIcon(ship, xPos);
                shipIcons.Add(icon);
                xPos += 0.12f;
            }
        }

        private GameObject CreateShipIcon(Ship ship, float xPos)
        {
            RectTransform iconRT = UIFactory.CreatePanel(fleetPanel, $"Icon_{ship.Data.shipName}", 
                ship.IsSelected ? new Color(0.3f, 0.5f, 0.7f) : new Color(0.2f, 0.3f, 0.4f));
            
            iconRT.anchorMin = new Vector2(xPos, 0.1f);
            iconRT.anchorMax = new Vector2(xPos + 0.1f, 0.75f);
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = Vector2.zero;

            // Health indicator
            float health = ship.HullPercent;
            Color healthColor = health > 0.5f ? Color.green : (health > 0.25f ? Color.yellow : Color.red);
            
            RectTransform healthRT = UIFactory.CreatePanel(iconRT, "Health", healthColor);
            healthRT.anchorMin = new Vector2(0.1f, 0.1f);
            healthRT.anchorMax = new Vector2(0.9f, 0.1f + health * 0.8f);
            healthRT.offsetMin = Vector2.zero;
            healthRT.offsetMax = Vector2.zero;

            return iconRT.gameObject;
        }

        private void UpdateTimer()
        {
            var naval = NavalBattleManager.Instance;
            if (naval == null) return;

            float time = naval.BattleTimeRemaining;
            int minutes = (int)(time / 60f);
            int seconds = (int)(time % 60f);
            timerText.text = $"{minutes:D2}:{seconds:D2}";
        }

        #endregion
    }
}
