using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// Simple tooltip component that shows text on hover
    /// </summary>
    public class TooltipUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private string tooltipText = "";
        private static GameObject activeTooltip;
        private static Text tooltipTextComponent;
        
        public void SetTooltip(string text)
        {
            tooltipText = text;
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (string.IsNullOrEmpty(tooltipText)) return;
            ShowTooltip();
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            HideTooltip();
        }
        
        private void ShowTooltip()
        {
            // Create tooltip if needed
            if (activeTooltip == null)
            {
                activeTooltip = new GameObject("Tooltip");
                var canvas = activeTooltip.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                
                var bg = activeTooltip.AddComponent<Image>();
                bg.color = new Color(0.1f, 0.08f, 0.05f, 0.95f);
                
                var outline = activeTooltip.AddComponent<Outline>();
                outline.effectColor = UIFactory.GoldAccent;
                outline.effectDistance = new Vector2(2, 2);
                
                // Padding
                var padding = new GameObject("Padding");
                padding.transform.SetParent(activeTooltip.transform, false);
                var padRT = padding.AddComponent<RectTransform>();
                padRT.anchorMin = new Vector2(0.02f, 0.1f);
                padRT.anchorMax = new Vector2(0.98f, 0.9f);
                
                tooltipTextComponent = UIFactory.CreateText(padRT, "Text", "", 13, TextAnchor.MiddleCenter, Color.white);
            }
            
            tooltipTextComponent.text = tooltipText;
            activeTooltip.SetActive(true);
            
            // Position at mouse
            var rt = activeTooltip.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(250, 60);
            rt.position = Input.mousePosition + new Vector3(10, 10, 0);
        }
        
        private void HideTooltip()
        {
            if (activeTooltip != null)
                activeTooltip.SetActive(false);
        }
        
        private void OnDestroy()
        {
            HideTooltip();
        }
    }
}
