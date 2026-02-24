using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace NapoleonicWars.Campaign
{
    public class FactionSelectSceneSetup : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("[FactionSelectSceneSetup] Awake - initializing scene");
            
            // EventSystem (required for Canvas UI)
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
            }
        }
        
        private void Start()
        {
            Debug.Log("[FactionSelectSceneSetup] Start - creating UI");
            
            // Destroy any pre-existing FactionSelectionUI to prevent doubles
            var existing = Object.FindFirstObjectByType<NapoleonicWars.UI.FactionSelectionUI>();
            if (existing != null)
            {
                Debug.Log("[FactionSelectSceneSetup] Destroying existing FactionSelectionUI");
                Object.Destroy(existing.gameObject);
            }
            
            // Also destroy any leftover canvases from previous scenes
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c.gameObject.name == "FactionSelectCanvas")
                {
                    Debug.Log("[FactionSelectSceneSetup] Destroying stale canvas");
                    Object.Destroy(c.gameObject);
                }
            }
            
            // Faction Selection UI
            GameObject uiGO = new GameObject("FactionSelectionUI");
            uiGO.AddComponent<NapoleonicWars.UI.FactionSelectionUI>();
            
            Debug.Log("[FactionSelectSceneSetup] UI created successfully");
        }
    }
}
