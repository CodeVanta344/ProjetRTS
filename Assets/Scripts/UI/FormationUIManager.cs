using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using NapoleonicWars.Core;
using NapoleonicWars.Units;

namespace NapoleonicWars.UI
{
    public class FormationUIManager : MonoBehaviour
    {
        public static FormationUIManager Instance { get; private set; }

        private GameObject panelGO;
        private List<Button> formationButtons = new List<Button>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("FormationUIManager");
                go.AddComponent<FormationUIManager>();
                // DontDestroyOnLoad(go); // Optional: keep it across scene unloads
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            CreateUI();
        }

        private void CreateUI()
        {
            // Find or create a Canvas
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                canvas = UIFactory.CreateCanvas("BattleHUDCanvas", 10);
            }

            // Ensure EventSystem exists
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject eventSystemGO = new GameObject("EventSystem");
                eventSystemGO.AddComponent<EventSystem>();
                eventSystemGO.AddComponent<StandaloneInputModule>();
            }

            // Create Main Panel
            panelGO = new GameObject("FormationPanel");
            panelGO.transform.SetParent(canvas.transform, false);
            
            RectTransform panelRect = panelGO.AddComponent<RectTransform>();
            
            // Anchor to bottom-left
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(0f, 0f);
            panelRect.pivot = new Vector2(0f, 0f);
            panelRect.anchoredPosition = new Vector2(25f, 130f); // offset from bottom left corner (above unit panel)
            
            // Scaled down based on user request for a cleaner, smaller UI
            panelRect.sizeDelta = new Vector2(260f, 280f); 

            // Load and set the provided background image
            Image bgImage = panelGO.AddComponent<Image>();
            Texture2D tex = Resources.Load<Texture2D>("UI/FormationMenuBg");
            if (tex != null)
            {
                Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                bgImage.sprite = sprite;
                bgImage.color = Color.white;
            }
            else
            {
                Debug.LogWarning("[FormationUIManager] Could not find UI/FormationMenuBg in Resources.");
                bgImage.color = new Color(0.1f, 0.1f, 0.15f, 0.9f); // Fallback color
            }

            // Create Invisible Buttons mathematically positioned over the text
            // The image has 4 large rows taking up the bottom 85% of the image.
            
            // 1. Ligne de feu (top row - just below title)
            CreateInvisibleButton(FormationType.Line, panelRect, new Vector2(0.06f, 0.62f), new Vector2(0.94f, 0.81f));
            
            // 2. Colonne d'attaque
            CreateInvisibleButton(FormationType.Column, panelRect, new Vector2(0.06f, 0.43f), new Vector2(0.94f, 0.62f));
            
            // 3. Carré
            CreateInvisibleButton(FormationType.Square, panelRect, new Vector2(0.06f, 0.24f), new Vector2(0.94f, 0.43f));
            
            // 4. Escarmouche (bottom row)
            CreateInvisibleButton(FormationType.Skirmish, panelRect, new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.24f));

            // Initially hide panel
            panelGO.SetActive(false);
        }

        private void CreateInvisibleButton(FormationType formationType, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject btnGO = new GameObject($"Btn_{formationType}");
            btnGO.transform.SetParent(parent, false);
            
            RectTransform btnRect = btnGO.AddComponent<RectTransform>();
            
            // Use precise proportional anchors to perfectly frame the visual rows
            btnRect.anchorMin = anchorMin;
            btnRect.anchorMax = anchorMax;
            
            // Zero offset perfectly snaps the rect boundaries to the anchors (do NOT set sizeDelta)
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;

            // Add Image for raycasting and visual feedback on hover/click
            Image btnImage = btnGO.AddComponent<Image>();
            // Base color MUST be solid white, or else the ColorBlock multipliers will always be transparent!
            btnImage.color = Color.white; 

            Button btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = btnImage;
            
            // STRONG Visual Feedback
            ColorBlock cb = btn.colors;
            cb.normalColor = new Color(1f, 1f, 1f, 0f);    // Invisible by default
            cb.highlightedColor = new Color(1f, 0.85f, 0.4f, 0.4f); // Strong gold highlight
            cb.pressedColor = new Color(1f, 0.85f, 0.4f, 0.6f); // Very strong gold on press
            cb.selectedColor = cb.normalColor;
            cb.fadeDuration = 0.1f;
            btn.colors = cb;

            btn.onClick.AddListener(() => OnFormationButtonClicked(formationType));
            formationButtons.Add(btn);
        }

        private void OnFormationButtonClicked(FormationType formationType)
        {
            if (SelectionManager.Instance == null) return;

            var selectedRegiments = SelectionManager.Instance.SelectedRegiments;
            if (selectedRegiments.Count == 0) return;

            foreach (var regiment in selectedRegiments)
            {
                // Assign new formation
                regiment.SetFormationTypeOnly(formationType);
                
                // Trigger units to move to their new formation slots
                // Without moving the regiment's center
                regiment.MoveRegiment(regiment.transform.position, regiment.transform.eulerAngles.y);
            }
            
            Debug.Log($"[FormationUI] Set formation: {formationType} for {selectedRegiments.Count} regiments.");
        }

        private void Update()
        {
            if (SelectionManager.Instance == null || panelGO == null) return;

            // Show panel only if at least one regiment is selected
            bool hasRegimentSelection = SelectionManager.Instance.SelectedRegiments.Count > 0;
            
            if (panelGO.activeSelf != hasRegimentSelection)
            {
                panelGO.SetActive(hasRegimentSelection);
            }
        }
    }
}
