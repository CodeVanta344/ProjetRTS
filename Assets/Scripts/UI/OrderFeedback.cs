using UnityEngine;
using NapoleonicWars.Core;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// Visual feedback for player orders: move markers, attack lines, selection circles.
    /// Creates temporary 3D indicators in the world when orders are given.
    /// </summary>
    public class OrderFeedback : MonoBehaviour
    {
        public static OrderFeedback Instance { get; private set; }

        private Material moveMaterial;
        private Material attackMaterial;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            moveMaterial = URPMaterialHelper.CreateLitEmissive(
                new Color(0.2f, 0.6f, 1f, 0.5f),
                new Color(0.2f, 0.6f, 1f) * 2f);
            attackMaterial = URPMaterialHelper.CreateLitEmissive(
                new Color(1f, 0.2f, 0.1f, 0.5f),
                new Color(1f, 0.2f, 0.1f) * 2f);
        }

        /// <summary>
        /// Show a move order marker at the target position.
        /// </summary>
        public void ShowMoveMarker(Vector3 position)
        {
            position.y = BattleManager.GetTerrainHeight(position) + 0.1f;
            CreateMarker(position, moveMaterial, 1.5f);
        }

        /// <summary>
        /// Show an attack order marker at the target position.
        /// </summary>
        public void ShowAttackMarker(Vector3 position)
        {
            position.y = BattleManager.GetTerrainHeight(position) + 0.1f;
            CreateMarker(position, attackMaterial, 2f);
        }

        /// <summary>
        /// Show an attack order arrow pointing to enemy regiment with soldier icon.
        /// Called when right-clicking on enemy regiment to attack.
        /// </summary>
        public void ShowAttackOrderArrow(Vector3 from, Vector3 to, string targetRegimentName)
        {
            from.y = BattleManager.GetTerrainHeight(from) + 1f;
            to.y = BattleManager.GetTerrainHeight(to) + 1f;

            GameObject arrowGO = new GameObject("AttackOrderArrow");
            
            // Create arrow mesh (using cylinders)
            GameObject arrowShaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            arrowShaft.name = "ArrowShaft";
            arrowShaft.transform.SetParent(arrowGO.transform);
            arrowShaft.transform.localScale = new Vector3(0.2f, 1.5f, 0.2f);
            
            GameObject arrowHead = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            arrowHead.name = "ArrowHead";
            arrowHead.transform.SetParent(arrowGO.transform);
            arrowHead.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            arrowHead.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            
            // Position arrow mid-way, pointing from -> to
            Vector3 midPoint = (from + to) * 0.5f;
            arrowGO.transform.position = midPoint;
            arrowGO.transform.rotation = Quaternion.LookRotation(to - from, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
            
            // Remove colliders
            Object.Destroy(arrowShaft.GetComponent<Collider>());
            Object.Destroy(arrowHead.GetComponent<Collider>());
            
            // Apply attack material
            Renderer shaftR = arrowShaft.GetComponent<Renderer>();
            Renderer headR = arrowHead.GetComponent<Renderer>();
            shaftR.material = attackMaterial;
            headR.material = attackMaterial;
            
            // Add pulsing glow effect
            arrowGO.AddComponent<PulsingGlow>().Initialize(attackMaterial.color);
            
            // Destroy after delay
            FadeAndDestroy fade = arrowGO.AddComponent<FadeAndDestroy>();
            fade.lifetime = 2f;
            
            // Also show attack line for clarity
            ShowAttackLine(from, to);
            
            Debug.Log($"[OrderFeedback] Attack order on {targetRegimentName}");
        }

        /// <summary>
        /// Show a line from origin to target (for attack orders).
        /// </summary>
        public void ShowAttackLine(Vector3 from, Vector3 to)
        {
            from.y = BattleManager.GetTerrainHeight(from) + 0.5f;
            to.y = BattleManager.GetTerrainHeight(to) + 0.5f;

            GameObject lineGO = new GameObject("AttackLine");
            lineGO.transform.position = from;

            LineRenderer lr = lineGO.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.startWidth = 0.3f;
            lr.endWidth = 0.1f;
            lr.material = attackMaterial;
            lr.startColor = new Color(1f, 0.3f, 0.1f, 0.8f);
            lr.endColor = new Color(1f, 0.3f, 0.1f, 0.2f);

            lineGO.AddComponent<FadeAndDestroy>().lifetime = 1.5f;
        }

        /// <summary>
        /// Show line extension indicator when dragging to widen formation
        /// </summary>
        public void ShowLineExtensionMarker(Vector3 position, float spacingMultiplier)
        {
            position.y = BattleManager.GetTerrainHeight(position) + 0.2f;
            
            // Create a visual indicator showing the spacing change
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "LineExtension";
            marker.transform.position = position;
            marker.transform.localScale = Vector3.one * (0.5f + spacingMultiplier * 0.3f);

            Collider col = marker.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            Renderer r = marker.GetComponent<Renderer>();
            Color indicatorColor = spacingMultiplier > 1f ? 
                new Color(0.2f, 0.8f, 0.3f) :  // Green for wider
                new Color(0.8f, 0.6f, 0.2f);   // Orange for tighter
            
            r.material = URPMaterialHelper.CreateLitEmissive(indicatorColor, indicatorColor * 2f);
            r.material.color = new Color(indicatorColor.r, indicatorColor.g, indicatorColor.b, 0.6f);

            marker.AddComponent<FadeAndDestroy>().lifetime = 0.3f; // Short duration for drag feedback
        }

        // Formation preview objects
        private GameObject formationPreviewParent;
        private GameObject[] formationPreviewMarkers;
        private LineRenderer formationPreviewLine;
        private GameObject directionArrow;
        private Material previewMaterial;
        private int lastPreviewCount = -1;

        /// <summary>
        /// Show a preview of the formation positions during line extension drag.
        /// Call this every frame while dragging to update the preview.
        /// </summary>
        public void ShowFormationPreview(Vector3[] positions, Vector3 lineStart, Vector3 lineEnd, Vector3 facingDirection)
        {
            ShowFormationPreview(positions, lineStart, lineEnd, facingDirection, null);
        }

        /// <summary>
        /// Show formation preview with custom color (e.g., cyan for cover mode).
        /// Only shows if there is a unit/selection active.
        /// </summary>
        public void ShowFormationPreview(Vector3[] positions, Vector3 lineStart, Vector3 lineEnd, Vector3 facingDirection, Color? customColor)
        {
            if (positions == null || positions.Length == 0) return;
            
            // Only show formation preview when there is a selection
            if (SelectionManager.Instance == null || !SelectionManager.Instance.HasSelection)
            {
                ClearFormationPreview();
                return;
            }

            // Create preview material if needed (only once)
            if (previewMaterial == null)
            {
                Color baseColor = customColor ?? new Color(0.3f, 0.9f, 0.4f, 0.6f);
                previewMaterial = URPMaterialHelper.CreateLitEmissive(
                    baseColor,
                    baseColor * 1.5f);
            }
            else if (customColor.HasValue)
            {
                // Update material color if custom color provided
                previewMaterial.color = customColor.Value;
                previewMaterial.SetColor("_EmissionColor", customColor.Value * 1.5f);
            }

            // Only recreate markers if count changed or parent is missing
            bool needsRecreate = formationPreviewParent == null || 
                                 formationPreviewMarkers == null || 
                                 lastPreviewCount != positions.Length;

            if (needsRecreate)
            {
                // Destroy old preview completely
                ClearFormationPreview();
                
                // Create new parent
                formationPreviewParent = new GameObject("FormationPreview");
                formationPreviewMarkers = new GameObject[positions.Length];
                lastPreviewCount = positions.Length;
                
                for (int i = 0; i < positions.Length; i++)
                {
                    GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    marker.name = $"PreviewUnit_{i}";
                    marker.transform.SetParent(formationPreviewParent.transform);
                    marker.transform.localScale = new Vector3(0.4f, 0.8f, 0.4f);
                    
                    Collider col = marker.GetComponent<Collider>();
                    if (col != null) Destroy(col);
                    
                    Renderer r = marker.GetComponent<Renderer>();
                    r.material = previewMaterial;
                    
                    formationPreviewMarkers[i] = marker;
                }

                // Create line renderer for formation line
                formationPreviewLine = formationPreviewParent.AddComponent<LineRenderer>();
                formationPreviewLine.positionCount = 2;
                formationPreviewLine.startWidth = 0.3f;
                formationPreviewLine.endWidth = 0.3f;
                formationPreviewLine.material = previewMaterial;
                formationPreviewLine.startColor = new Color(0.3f, 0.9f, 0.4f, 0.8f);
                formationPreviewLine.endColor = new Color(0.3f, 0.9f, 0.4f, 0.8f);

                // Create directional arrow using cylinder + rotated cap (no Cone in Unity)
                directionArrow = new GameObject("DirectionArrow");
                directionArrow.transform.SetParent(formationPreviewParent.transform);
                
                // Main arrow body (cylinder scaled to look like arrow shaft)
                GameObject arrowBody = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                arrowBody.name = "ArrowBody";
                arrowBody.transform.SetParent(directionArrow.transform);
                arrowBody.transform.localScale = new Vector3(0.3f, 1f, 0.3f);
                arrowBody.transform.localPosition = Vector3.zero;
                
                // Arrow head (small cylinder at top, rotated)
                GameObject arrowHead = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                arrowHead.name = "ArrowHead";
                arrowHead.transform.SetParent(directionArrow.transform);
                arrowHead.transform.localScale = new Vector3(0.6f, 0.4f, 0.6f);
                arrowHead.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                arrowHead.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
                
                // Remove colliders
                Collider bodyCol = arrowBody.GetComponent<Collider>();
                if (bodyCol != null) Destroy(bodyCol);
                Collider headCol = arrowHead.GetComponent<Collider>();
                if (headCol != null) Destroy(headCol);
                
                // Apply material
                Renderer bodyR = arrowBody.GetComponent<Renderer>();
                bodyR.material = previewMaterial;
                Renderer headR = arrowHead.GetComponent<Renderer>();
                headR.material = previewMaterial;
            }

            // Update marker positions (every frame)
            for (int i = 0; i < positions.Length; i++)
            {
                if (i < formationPreviewMarkers.Length && formationPreviewMarkers[i] != null)
                {
                    Vector3 pos = positions[i];
                    pos.y = BattleManager.GetTerrainHeight(pos) + 0.8f;
                    formationPreviewMarkers[i].transform.position = pos;
                    formationPreviewMarkers[i].transform.forward = facingDirection;
                }
            }

            // Update line
            if (formationPreviewLine != null)
            {
                lineStart.y = BattleManager.GetTerrainHeight(lineStart) + 0.3f;
                lineEnd.y = BattleManager.GetTerrainHeight(lineEnd) + 0.3f;
                formationPreviewLine.SetPosition(0, lineStart);
                formationPreviewLine.SetPosition(1, lineEnd);
            }

            // Update direction arrow - position it at center of line, pointing in facing direction
            if (directionArrow != null)
            {
                Vector3 centerPos = (lineStart + lineEnd) * 0.5f;
                centerPos.y = BattleManager.GetTerrainHeight(centerPos) + 1.5f; // Above formation
                directionArrow.transform.position = centerPos;
                
                // Rotate arrow to point in facing direction (Y-up arrow needs to lie flat then point)
                Quaternion lookRotation = Quaternion.LookRotation(facingDirection, Vector3.up);
                directionArrow.transform.rotation = lookRotation * Quaternion.Euler(90f, 0f, 0f);
            }
        }

        /// <summary>
        /// Clear the formation preview when drag is released or cancelled.
        /// </summary>
        public void ClearFormationPreview()
        {
            if (formationPreviewParent != null)
            {
                Destroy(formationPreviewParent);
                formationPreviewParent = null;
            }
            formationPreviewMarkers = null;
            formationPreviewLine = null;
            lastPreviewCount = -1;
        }

        private void CreateMarker(Vector3 position, Material mat, float duration)
        {
            // Ring marker (flattened torus approximation using a cylinder)
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "OrderMarker";
            marker.transform.position = position;
            marker.transform.localScale = new Vector3(2f, 0.05f, 2f);

            Collider col = marker.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            Renderer r = marker.GetComponent<Renderer>();
            r.material = mat;

            marker.AddComponent<FadeAndDestroy>().lifetime = duration;
        }
    }

    /// <summary>
    /// Adds a pulsing glow effect to order feedback objects.
    /// </summary>
    public class PulsingGlow : MonoBehaviour
    {
        private Color baseColor;
        private Renderer[] renderers;
        private float pulseSpeed = 8f;
        
        public void Initialize(Color color)
        {
            baseColor = color;
            renderers = GetComponentsInChildren<Renderer>();
        }
        
        private void Update()
        {
            float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * pulseSpeed);
            Color c = baseColor * pulse;
            c.a = baseColor.a;
            
            foreach (var r in renderers)
            {
                if (r != null)
                    r.material.color = c;
            }
        }
    }

    /// <summary>
    /// Fades out and destroys a GameObject over its lifetime.
    /// Uses MaterialPropertyBlock to avoid creating material instances.
    /// </summary>
    public class FadeAndDestroy : MonoBehaviour
    {
        public float lifetime = 1.5f;
        private float elapsed;
        private Renderer[] renderers;
        private LineRenderer lineRenderer;
        private MaterialPropertyBlock mpb;
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Start()
        {
            renderers = GetComponentsInChildren<Renderer>();
            lineRenderer = GetComponent<LineRenderer>();
            mpb = new MaterialPropertyBlock();
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lifetime;

            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            // Pulse + fade
            float alpha = (1f - t) * (0.5f + 0.5f * Mathf.Sin(elapsed * 8f));
            float scale = 1f + t * 0.5f;
            transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 1.5f, t);

            // Fade renderers using MaterialPropertyBlock - no material allocation
            if (mpb != null)
            {
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(mpb);
                    mpb.SetColor(ColorId, new Color(1f, 1f, 1f, alpha));
                    mpb.SetColor(BaseColorId, new Color(1f, 1f, 1f, alpha));
                    r.SetPropertyBlock(mpb);
                }
            }

            if (lineRenderer != null)
            {
                Color sc = lineRenderer.startColor;
                Color ec = lineRenderer.endColor;
                sc.a = alpha;
                ec.a = alpha * 0.3f;
                lineRenderer.startColor = sc;
                lineRenderer.endColor = ec;
            }
        }
    }
}
