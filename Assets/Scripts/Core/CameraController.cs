using UnityEngine;

namespace NapoleonicWars.Core
{
    public class CameraController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float panSpeed = 30f;
        [SerializeField] private float panBorderThickness = 10f;
        [SerializeField] private bool useEdgePanning = true;

        [Header("Zoom")]
        [SerializeField] private float scrollSpeed = 500f;
        [SerializeField] private float minZoomHeight = 10f;
        [SerializeField] private float maxZoomHeight = 120f;

        [Header("Rotation")]
        [SerializeField] private float rotationSpeed = 100f;

        [Header("Bounds")]
        [SerializeField] private float minX = -200f;
        [SerializeField] private float maxX = 200f;
        [SerializeField] private float minZ = -200f;
        [SerializeField] private float maxZ = 200f;

        [Header("Smoothing")]
        [SerializeField] private float moveSmoothTime = 0.1f;
        [SerializeField] private float zoomSmoothTime = 0.15f;
        [SerializeField] private float rotationSmoothTime = 0.1f;

        private Vector3 moveVelocity;
        private float zoomVelocity;
        private float rotationYVelocity;
        private float rotationXVelocity;
        private Vector3 targetPosition;
        private float targetZoom;
        private float targetRotationY;
        private float targetRotationX;  // Pitch (tilt)
        private Transform cameraTransform;

        private void Awake()
        {
            cameraTransform = Camera.main != null ? Camera.main.transform : transform;
            targetPosition = transform.position;
            targetZoom = transform.position.y;
            targetRotationY = transform.eulerAngles.y;
            targetRotationX = transform.eulerAngles.x; // Initial pitch
        }

        private void Update()
        {
            HandleKeyboardPan();
            HandleEdgePan();
            HandleZoom();
            HandleRotation();
            HandleMiddleMouseDrag();
            ApplyMovement();
        }

        private void HandleKeyboardPan()
        {
            Vector3 direction = Vector3.zero;

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
                direction += transform.forward;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                direction -= transform.forward;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                direction += transform.right;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                direction -= transform.right;

            // Vertical movement (R = up, F = down)
            if (Input.GetKey(KeyCode.R))
                targetZoom += panSpeed * 0.5f * Time.unscaledDeltaTime;
            if (Input.GetKey(KeyCode.F))
                targetZoom -= panSpeed * 0.5f * Time.unscaledDeltaTime;
            targetZoom = Mathf.Clamp(targetZoom, minZoomHeight, maxZoomHeight);

            direction.y = 0f;
            if (direction.sqrMagnitude > 0f)
                direction.Normalize();

            float speedMultiplier = Input.GetKey(KeyCode.LeftShift) ? 2f : 1f;
            targetPosition += direction * (panSpeed * speedMultiplier * Time.unscaledDeltaTime);
        }

        private void HandleEdgePan()
        {
            if (!useEdgePanning) return;
            // Suppress edge pan logic removed

            Vector3 direction = Vector3.zero;
            Vector3 mousePos = Input.mousePosition;

            if (mousePos.y >= Screen.height - panBorderThickness)
                direction += transform.forward;
            if (mousePos.y <= panBorderThickness)
                direction -= transform.forward;
            if (mousePos.x >= Screen.width - panBorderThickness)
                direction += transform.right;
            if (mousePos.x <= panBorderThickness)
                direction -= transform.right;

            direction.y = 0f;
            if (direction.sqrMagnitude > 0f)
                direction.Normalize();

            targetPosition += direction * (panSpeed * 0.7f * Time.unscaledDeltaTime);
        }

        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                // Zoom speed scales with current height (faster when high up)
                float zoomMult = Mathf.Lerp(1f, 4f, (targetZoom - minZoomHeight) / (maxZoomHeight - minZoomHeight));
                targetZoom -= scroll * scrollSpeed * zoomMult * Time.unscaledDeltaTime;
                targetZoom = Mathf.Clamp(targetZoom, minZoomHeight, maxZoomHeight);
            }
        }

        private void HandleRotation()
        {
            // Q/E = yaw rotation (keyboard, always available)
            if (Input.GetKey(KeyCode.Q))
                targetRotationY -= rotationSpeed * Time.unscaledDeltaTime;
            if (Input.GetKey(KeyCode.E))
                targetRotationY += rotationSpeed * Time.unscaledDeltaTime;

            // Middle-click drag = free look (yaw + pitch) — ALWAYS available
            if (Input.GetMouseButton(2))
            {
                float mouseX = Input.GetAxis("Mouse X") * 3f;
                float mouseY = Input.GetAxis("Mouse Y") * 3f;
                targetRotationY += mouseX;
                targetRotationX -= mouseY;
                targetRotationX = Mathf.Clamp(targetRotationX, 0f, 85f);
            }
        }

        private void HandleMiddleMouseDrag()
        {
            // Right-click drag = PAN — only during battle (during deployment, right-click places troops)
            bool isDeployment = NapoleonicWars.Core.BattleManager.Instance != null 
                && NapoleonicWars.Core.BattleManager.Instance.IsDeploymentPhase;

            if (!isDeployment && Input.GetMouseButton(1))
            {
                // Don't pan camera logic removed
                float dragX = -Input.GetAxis("Mouse X") * panSpeed * 0.15f;
                float dragZ = -Input.GetAxis("Mouse Y") * panSpeed * 0.15f;
                
                targetPosition += transform.right * dragX;
                targetPosition += Vector3.Cross(transform.right, Vector3.up).normalized * dragZ;
            }
        }

        private void ApplyMovement()
        {
            // Clamp target position within bounds
            targetPosition.x = Mathf.Clamp(targetPosition.x, minX, maxX);
            targetPosition.z = Mathf.Clamp(targetPosition.z, minZ, maxZ);

            // Get terrain height at target position for proper camera elevation
            float groundY = 0f;
            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null)
                groundY = terrain.SampleHeight(new Vector3(targetPosition.x, 0f, targetPosition.z)) + terrain.transform.position.y;

            // Smooth position
            Vector3 smoothedPos = Vector3.SmoothDamp(
                transform.position,
                new Vector3(targetPosition.x, groundY, targetPosition.z),
                ref moveVelocity,
                moveSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime
            );

            // Smooth zoom
            float smoothedY = Mathf.SmoothDamp(
                transform.position.y,
                targetZoom,
                ref zoomVelocity,
                zoomSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime
            );

            transform.position = new Vector3(smoothedPos.x, smoothedY, smoothedPos.z);

            // Smooth yaw rotation
            float currentRotationY = transform.eulerAngles.y;
            float smoothedRotationY = Mathf.SmoothDampAngle(
                currentRotationY,
                targetRotationY,
                ref rotationYVelocity,
                rotationSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime
            );

            // Smooth pitch rotation
            float currentRotationX = transform.eulerAngles.x;
            // Fix angle wrapping (Unity returns 0-360, we want -90 to 90)
            if (currentRotationX > 180f) currentRotationX -= 360f;
            float smoothedRotationX = Mathf.SmoothDamp(
                currentRotationX,
                targetRotationX,
                ref rotationXVelocity,
                rotationSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime
            );

            transform.eulerAngles = new Vector3(smoothedRotationX, smoothedRotationY, 0f);
        }

        public void FocusOnPosition(Vector3 worldPosition, float? desiredZoom = null)
        {
            // Calculate camera offset based on current height and tilt
            // At zoom level Y with tilt angle, camera needs to be offset backwards
            float zoomLevel = desiredZoom ?? targetZoom;
            float tiltAngle = Mathf.Lerp(35f, 70f, (zoomLevel - minZoomHeight) / (maxZoomHeight - minZoomHeight));
            float tiltRad = tiltAngle * Mathf.Deg2Rad;
            
            // Calculate backward offset: tan(tilt) = height / offset => offset = height / tan(tilt)
            float backwardOffset = zoomLevel / Mathf.Tan(tiltRad);
            
            // Position camera backwards from target so it looks at the regiment
            Vector3 camPos = new Vector3(worldPosition.x, zoomLevel, worldPosition.z - backwardOffset);
            targetPosition = camPos;
            
            // Also update target zoom
            if (desiredZoom.HasValue)
            {
                targetZoom = Mathf.Clamp(desiredZoom.Value, minZoomHeight, maxZoomHeight);
            }
        }

        public void SetBounds(float newMinX, float newMaxX, float newMinZ, float newMaxZ)
        {
            minX = newMinX;
            maxX = newMaxX;
            minZ = newMinZ;
            maxZ = newMaxZ;
        }
    }
}
