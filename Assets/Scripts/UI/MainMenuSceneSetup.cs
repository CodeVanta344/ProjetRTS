using UnityEngine;
using UnityEngine.EventSystems;

namespace NapoleonicWars.UI
{
    public class MainMenuSceneSetup : MonoBehaviour
    {
        private void Awake()
        {
            // Camera
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
                cam = camGO.AddComponent<Camera>();
                camGO.AddComponent<AudioListener>();
            }

            cam.transform.position = new Vector3(0f, 5f, 0f);
            cam.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.06f, 0.04f);

            // GameManager (persists across scenes)
            if (NapoleonicWars.Core.GameManager.Instance == null)
            {
                GameObject gmGO = new GameObject("GameManager");
                gmGO.AddComponent<NapoleonicWars.Core.GameManager>();
            }

            // Difficulty Settings (persists across scenes)
            if (NapoleonicWars.Core.DifficultySettings.Instance == null)
            {
                GameObject dsGO = new GameObject("DifficultySettings");
                dsGO.AddComponent<NapoleonicWars.Core.DifficultySettings>();
            }

            // Graphics Settings (persists across scenes)
            if (NapoleonicWars.Core.GraphicsSettings.Instance == null)
            {
                GameObject gsGO = new GameObject("GraphicsSettings");
                gsGO.AddComponent<NapoleonicWars.Core.GraphicsSettings>();
            }

            // EventSystem (required for Canvas UI)
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
            }

            // Main Menu UI
            GameObject uiGO = new GameObject("MainMenuUI");
            uiGO.AddComponent<MainMenuUI>();
        }
    }
}
