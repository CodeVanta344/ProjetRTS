using UnityEngine;

namespace NapoleonicWars.Battlefield
{
    /// <summary>
    /// Visual helper to make trenches more visible in the game.
    /// Draws a colored outline around the trench in the Scene view and adds a visible marker.
    /// </summary>
    public class TrenchVisualizer : MonoBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] private Color trenchColor = new Color(0.35f, 0.25f, 0.15f, 0.8f);
        [SerializeField] private Color outlineColor = new Color(0.2f, 0.15f, 0.1f, 1f);
        [SerializeField] private float lineWidth = 0.1f;

        private LineRenderer lineRenderer;
        private TrenchComponent trenchComponent;

        private void Start()
        {
            trenchComponent = GetComponent<TrenchComponent>();
            CreateVisualOutline();
        }

        private void CreateVisualOutline()
        {
            // Create a line renderer to show the trench outline
            GameObject outlineObj = new GameObject("TrenchOutline");
            outlineObj.transform.SetParent(transform);
            outlineObj.transform.localPosition = Vector3.zero;

            lineRenderer = outlineObj.AddComponent<LineRenderer>();
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = outlineColor;
            lineRenderer.endColor = outlineColor;
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.positionCount = 5;
            lineRenderer.loop = true;
            lineRenderer.useWorldSpace = false;

            // Calculate trench dimensions
            float length = 12f; // Default trench length
            float width = 4f;   // Default trench width

            if (trenchComponent != null)
            {
                length = trenchComponent.length;
                width = trenchComponent.width;
            }

            // Set corner positions (rectangle)
            Vector3 p1 = new Vector3(-length * 0.5f, 0.05f, -width * 0.5f);
            Vector3 p2 = new Vector3(length * 0.5f, 0.05f, -width * 0.5f);
            Vector3 p3 = new Vector3(length * 0.5f, 0.05f, width * 0.5f);
            Vector3 p4 = new Vector3(-length * 0.5f, 0.05f, width * 0.5f);

            lineRenderer.SetPosition(0, p1);
            lineRenderer.SetPosition(1, p2);
            lineRenderer.SetPosition(2, p3);
            lineRenderer.SetPosition(3, p4);
            lineRenderer.SetPosition(4, p1);
        }

        private void OnDrawGizmos()
        {
            // Draw trench area in editor
            Gizmos.color = trenchColor;

            float length = 12f;
            float width = 4f;

            if (trenchComponent != null)
            {
                length = trenchComponent.length;
                width = trenchComponent.width;
            }

            Vector3 center = transform.position;
            Vector3 size = new Vector3(length, 0.1f, width);

            Gizmos.DrawCube(center + Vector3.up * 0.05f, size);

            // Draw outline
            Gizmos.color = outlineColor;
            Vector3 halfSize = size * 0.5f;
            Vector3 p1 = center + new Vector3(-halfSize.x, 0.05f, -halfSize.z);
            Vector3 p2 = center + new Vector3(halfSize.x, 0.05f, -halfSize.z);
            Vector3 p3 = center + new Vector3(halfSize.x, 0.05f, halfSize.z);
            Vector3 p4 = center + new Vector3(-halfSize.x, 0.05f, halfSize.z);

            Gizmos.DrawLine(p1, p2);
            Gizmos.DrawLine(p2, p3);
            Gizmos.DrawLine(p3, p4);
            Gizmos.DrawLine(p4, p1);

            // Draw facing direction arrow
            Vector3 facing = trenchComponent != null ? trenchComponent.facingDirection : Vector3.forward;
            Gizmos.color = Color.red;
            Gizmos.DrawRay(center, facing * 5f);
        }

        private void OnDrawGizmosSelected()
        {
            // Highlight when selected
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            float length = trenchComponent != null ? trenchComponent.length : 12f;
            float width = trenchComponent != null ? trenchComponent.width : 4f;
            Gizmos.DrawWireCube(transform.position, new Vector3(length, 2f, width));
        }
    }
}
