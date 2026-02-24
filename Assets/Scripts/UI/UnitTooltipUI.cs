using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Core;
using NapoleonicWars.Units;
using NapoleonicWars.Data;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// Shows a tooltip when hovering over a regiment with unit info:
    /// name, type, alive count, morale, formation, stamina, ammo, experience.
    /// </summary>
    public class UnitTooltipUI : MonoBehaviour
    {
        private Canvas canvas;
        private GameObject tooltipPanel;
        private Text nameText;
        private Text typeText;
        private Text countText;
        private Text moraleText;
        private Text formationText;
        private Text staminaText;
        private Text ammoText;
        private Text expText;
        private Text stateText;

        private Regiment hoveredRegiment;
        private Camera mainCam;

        private void Start()
        {
            mainCam = Camera.main;
            BuildTooltip();
            tooltipPanel.SetActive(false);
        }

        private void Update()
        {
            if (mainCam == null) mainCam = Camera.main;
            if (mainCam == null) return;

            // Raycast from mouse
            Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
            Regiment reg = null;

            if (Physics.Raycast(ray, out RaycastHit hit, 500f))
            {
                // Check if we hit a unit
                UnitBase unit = hit.collider.GetComponent<UnitBase>();
                if (unit == null) unit = hit.collider.GetComponentInParent<UnitBase>();
                if (unit != null) reg = unit.Regiment;

                // Or directly a regiment
                if (reg == null) reg = hit.collider.GetComponent<Regiment>();
                if (reg == null) reg = hit.collider.GetComponentInParent<Regiment>();
            }

            if (reg != null && reg.AliveCount > 0)
            {
                hoveredRegiment = reg;
                UpdateTooltip(reg);
                PositionTooltip();
                tooltipPanel.SetActive(true);
            }
            else
            {
                hoveredRegiment = null;
                tooltipPanel.SetActive(false);
            }
        }

        private void UpdateTooltip(Regiment reg)
        {
            nameText.text = reg.gameObject.name;

            string unitType = reg.UnitData != null ? reg.UnitData.unitType.ToString() : "Unknown";
            typeText.text = unitType.Replace("LineInfantry", "Line Infantry")
                                    .Replace("LightInfantry", "Light Infantry");

            bool isPlayer = reg.TeamId == 0;
            nameText.color = isPlayer ? new Color(0.3f, 0.6f, 1f) : new Color(1f, 0.35f, 0.3f);

            countText.text = $"Strength: {reg.AliveCount} / {reg.Units.Count}";

            float morale = reg.AverageMorale;
            moraleText.text = $"Morale: {morale:F0}%";
            moraleText.color = morale > 60 ? Color.green : morale > 30 ? Color.yellow : Color.red;

            formationText.text = $"Formation: {reg.CurrentFormation}";

            // Stamina (average from alive units)
            float avgStamina = 0f;
            float avgAmmo = 0f;
            float avgExp = 0f;
            int alive = 0;
            foreach (var unit in reg.Units)
            {
                if (unit == null || unit.CurrentState == UnitState.Dead) continue;
                avgStamina += unit.CurrentStamina;
                avgAmmo += unit.CurrentAmmo;
                avgExp += unit.Experience;
                alive++;
            }
            if (alive > 0)
            {
                avgStamina /= alive;
                avgAmmo /= alive;
                avgExp /= alive;
            }

            staminaText.text = $"Stamina: {avgStamina:F0}%";
            staminaText.color = avgStamina > 50 ? Color.white : avgStamina > 25 ? Color.yellow : Color.red;

            ammoText.text = $"Ammo: {avgAmmo:F0}";

            string rank = avgExp >= 50 ? "Elite" : avgExp >= 20 ? "Veteran" : "Recruit";
            expText.text = $"Rank: {rank}";
            expText.color = rank == "Elite" ? new Color(1f, 0.85f, 0.3f) :
                            rank == "Veteran" ? new Color(0.6f, 0.8f, 1f) : Color.white;

            // Current state
            string state = "Idle";
            if (reg.Units.Count > 0)
            {
                foreach (var unit in reg.Units)
                {
                    if (unit != null && unit.CurrentState != UnitState.Dead)
                    {
                        state = unit.CurrentState.ToString();
                        break;
                    }
                }
            }
            stateText.text = $"Status: {state}";
        }

        private void PositionTooltip()
        {
            RectTransform rt = tooltipPanel.GetComponent<RectTransform>();
            Vector2 mousePos = Input.mousePosition;

            // Offset tooltip from cursor
            float offsetX = 20f;
            float offsetY = -20f;

            // Keep on screen
            float w = rt.sizeDelta.x;
            float h = rt.sizeDelta.y;
            if (mousePos.x + offsetX + w > Screen.width)
                offsetX = -w - 10f;
            if (mousePos.y + offsetY - h < 0)
                offsetY = h + 10f;

            rt.position = new Vector3(mousePos.x + offsetX, mousePos.y + offsetY, 0);
        }

        private void BuildTooltip()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            tooltipPanel = new GameObject("TooltipPanel");
            tooltipPanel.transform.SetParent(canvas.transform, false);
            RectTransform panelRT = tooltipPanel.AddComponent<RectTransform>();
            panelRT.sizeDelta = new Vector2(240, 230);
            panelRT.pivot = new Vector2(0, 1);

            Image bg = tooltipPanel.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.07f, 0.05f, 0.92f);
            Outline outline = tooltipPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.5f, 0.4f, 0.2f, 0.8f);
            outline.effectDistance = new Vector2(1, 1);

            float y = -8f;
            float lineH = 22f;

            nameText = AddLine(tooltipPanel.transform, ref y, lineH, 16, FontStyle.Bold, Color.white);
            typeText = AddLine(tooltipPanel.transform, ref y, lineH, 13, FontStyle.Italic, new Color(0.7f, 0.7f, 0.7f));
            y -= 4f; // Small gap
            countText = AddLine(tooltipPanel.transform, ref y, lineH, 14, FontStyle.Normal, Color.white);
            moraleText = AddLine(tooltipPanel.transform, ref y, lineH, 14, FontStyle.Normal, Color.green);
            formationText = AddLine(tooltipPanel.transform, ref y, lineH, 14, FontStyle.Normal, Color.white);
            staminaText = AddLine(tooltipPanel.transform, ref y, lineH, 14, FontStyle.Normal, Color.white);
            ammoText = AddLine(tooltipPanel.transform, ref y, lineH, 14, FontStyle.Normal, Color.white);
            expText = AddLine(tooltipPanel.transform, ref y, lineH, 14, FontStyle.Normal, Color.white);
            stateText = AddLine(tooltipPanel.transform, ref y, lineH, 14, FontStyle.Normal, new Color(0.8f, 0.8f, 0.6f));
        }

        private Text AddLine(Transform parent, ref float y, float lineHeight, int fontSize, FontStyle style, Color color)
        {
            GameObject go = new GameObject("Line");
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(10, y);
            rt.sizeDelta = new Vector2(-20, lineHeight);
            y -= lineHeight;

            Text t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAnchor.MiddleLeft;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }
    }
}
