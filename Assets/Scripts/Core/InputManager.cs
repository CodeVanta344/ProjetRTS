using UnityEngine;
using UnityEngine.EventSystems;

namespace NapoleonicWars.Core
{
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        // Mouse state
        public Vector3 MouseWorldPosition { get; private set; }
        public Vector2 MouseScreenPosition => Input.mousePosition;
        public bool IsMouseOverUI { get; private set; }

        // Selection
        public bool SelectionStarted => Input.GetMouseButtonDown(0) && !IsMouseOverUI;
        public bool SelectionHeld => Input.GetMouseButton(0) && !IsMouseOverUI;
        public bool SelectionReleased => Input.GetMouseButtonUp(0);

        // Commands - allow even when mouse is over UI unit cards
        public bool CommandIssued => Input.GetMouseButtonDown(1); // Removed IsMouseOverUI check for commands
        public bool IsAdditive => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        public bool IsAttackMove => Input.GetKey(KeyCode.A);

        // Formation hotkeys
        public bool FormationLine => Input.GetKeyDown(KeyCode.F1);
        public bool FormationColumn => Input.GetKeyDown(KeyCode.F2);
        public bool FormationSquare => Input.GetKeyDown(KeyCode.F3);
        public bool FormationSkirmish => Input.GetKeyDown(KeyCode.F4);

        // Command hotkeys
        public bool VolleyToggle => Input.GetKeyDown(KeyCode.V);
        public bool ChargePressed => Input.GetKeyDown(KeyCode.C);
        public bool ToggleRanges => Input.GetKeyDown(KeyCode.R);

        // Campaign hotkeys
        public bool IntelligencePanel => Input.GetKeyDown(KeyCode.I);
        public bool ResearchPanel => Input.GetKeyDown(KeyCode.T);

        // Game controls
        public bool PausePressed => Input.GetKeyDown(KeyCode.Space);
        public bool SpeedUp => Input.GetKeyDown(KeyCode.KeypadPlus);
        public bool SpeedDown => Input.GetKeyDown(KeyCode.KeypadMinus);

        private Camera mainCamera;
        private Plane groundPlane = new Plane(Vector3.up, 0f);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            mainCamera = Camera.main;
        }

        private void Update()
        {
            UpdateMouseWorldPosition();
            IsMouseOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            HandleGameControls();
        }

        private void UpdateMouseWorldPosition()
        {
            if (mainCamera == null) return;

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

            // Raycast against terrain/colliders first
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {
                MouseWorldPosition = hit.point;
            }
            else if (groundPlane.Raycast(ray, out float distance))
            {
                MouseWorldPosition = ray.GetPoint(distance);
            }
        }

        private void HandleGameControls()
        {
            if (GameManager.Instance == null) return;

            if (PausePressed)
                GameManager.Instance.TogglePause();
        }

        public Vector3 GetMouseWorldPositionOnTerrain()
        {
            if (mainCamera == null) return Vector3.zero;

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {
                return hit.point;
            }

            return MouseWorldPosition;
        }
    }
}
