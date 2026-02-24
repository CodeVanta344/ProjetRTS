using UnityEngine;

namespace NapoleonicWars.Battlefield
{
    /// <summary>
    /// Marks a GameObject as a trench/cover position.
    /// Stores the facing direction (toward enemy) for unit positioning.
    /// </summary>
    public class TrenchComponent : MonoBehaviour
    {
        [Tooltip("Direction the trench faces (toward enemy)")]
        public Vector3 facingDirection = Vector3.forward;

        [Tooltip("Length of the trench")]
        public float length = 15f;

        [Tooltip("Width of the trench")]
        public float width = 4f;

        [Tooltip("How many units can fit in this trench")]
        public int maxUnitCount = 12;

        [Tooltip("Current units occupying this trench")]
        public int currentUnitCount = 0;

        private void OnDrawGizmos()
        {
            // Draw trench outline in editor
            Gizmos.color = new Color(0.4f, 0.3f, 0.2f, 0.5f);
            Vector3 center = transform.position;
            Vector3 right = Vector3.Cross(Vector3.up, facingDirection).normalized;

            Vector3 p1 = center + right * (length * 0.5f);
            Vector3 p2 = center - right * (length * 0.5f);
            Vector3 p3 = p2 + facingDirection * width;
            Vector3 p4 = p1 + facingDirection * width;

            Gizmos.DrawLine(p1, p2);
            Gizmos.DrawLine(p2, p3);
            Gizmos.DrawLine(p3, p4);
            Gizmos.DrawLine(p4, p1);

            // Draw facing direction arrow
            Gizmos.color = Color.red;
            Gizmos.DrawRay(center + facingDirection * (width * 0.5f), facingDirection * 3f);
        }

        /// <summary>
        /// Get the best position along the trench for a unit
        /// </summary>
        public Vector3 GetPositionForUnit(int unitIndex, int totalUnits)
        {
            Vector3 right = Vector3.Cross(Vector3.up, facingDirection).normalized;
            float spacing = length / Mathf.Max(1, totalUnits);
            float offset = (unitIndex - (totalUnits - 1) * 0.5f) * spacing;

            // Position inside the trench (behind the parapet)
            Vector3 pos = transform.position + right * offset - facingDirection * (width * 0.25f);
            pos.y = transform.position.y;

            return pos;
        }

        /// <summary>
        /// Get firing position at the edge of the trench
        /// </summary>
        public Vector3 GetFiringPosition(int unitIndex, int totalUnits)
        {
            Vector3 right = Vector3.Cross(Vector3.up, facingDirection).normalized;
            float spacing = length / Mathf.Max(1, totalUnits);
            float offset = (unitIndex - (totalUnits - 1) * 0.5f) * spacing;

            // Position at the parapet edge (facing enemy)
            Vector3 pos = transform.position + right * offset + facingDirection * (width * 0.25f);
            pos.y = transform.position.y;

            return pos;
        }

        /// <summary>
        /// Check if this trench has space for more units
        /// </summary>
        public bool HasSpace(int unitCount)
        {
            return (currentUnitCount + unitCount) <= maxUnitCount;
        }

        /// <summary>
        /// Occupy the trench with given unit count
        /// </summary>
        public void Occupy(int unitCount)
        {
            currentUnitCount = Mathf.Min(currentUnitCount + unitCount, maxUnitCount);
        }

        /// <summary>
        /// Vacate the trench
        /// </summary>
        public void Vacate(int unitCount)
        {
            currentUnitCount = Mathf.Max(0, currentUnitCount - unitCount);
        }
    }
}
