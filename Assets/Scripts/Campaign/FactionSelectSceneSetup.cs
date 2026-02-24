using UnityEngine;
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
            
            // Faction Selection UI
            GameObject uiGO = new GameObject("FactionSelectionUI");
            uiGO.AddComponent<NapoleonicWars.UI.FactionSelectionUI>();
            
            Debug.Log("[FactionSelectSceneSetup] UI created successfully");
        }
    }
}
