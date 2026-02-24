using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Units
{
    /// <summary>
    /// Visualizes firing range zones with color-coded accuracy levels
    /// Green = Optimal (0-30m, 100%), Yellow = Effective (30-60m, 85%)
    /// Orange = Medium (60-100m, 60%), Red = Long (100-150m, 35%), Dark Red = Extreme (150m+, 15%)
    /// </summary>
    public class FiringRangeVisualizer : MonoBehaviour
    {
        [Header("Range Zone Colors")]
        // Couleurs plus "tactiques" (moins saturées, style carte d'état-major)
        [SerializeField] private Color optimalZoneColor = new Color(0.2f, 0.7f, 0.3f, 0.25f);      // Green
        [SerializeField] private Color effectiveZoneColor = new Color(0.8f, 0.8f, 0.2f, 0.20f);    // Yellow
        [SerializeField] private Color mediumZoneColor = new Color(0.9f, 0.5f, 0.1f, 0.15f);       // Orange
        [SerializeField] private Color longZoneColor = new Color(0.8f, 0.2f, 0.2f, 0.10f);         // Red
        [SerializeField] private Color extremeZoneColor = new Color(0.5f, 0.1f, 0.1f, 0.08f);      // Dark Red

        [Header("Visual Settings")]
        [SerializeField] private int segmentCount = 64; // Plus de segments pour un arc plus lisse
        [SerializeField] private float lineWidth = 0.4f; // Lignes de bordure un peu plus épaisses
        [SerializeField] private float yOffset = 0.15f; // Un peu plus haut pour éviter le z-fighting avec le terrain
        [SerializeField] private bool showFill = true;
        [SerializeField] private float coneAngle = 60f; // Total cone angle in degrees (centered on forward)

        // Zone line renderers (concentric rings)
        private LineRenderer optimalLine;
        private LineRenderer effectiveLine;
        private LineRenderer mediumLine;
        private LineRenderer longLine;
        private LineRenderer extremeLine;

        // Zone mesh renderers for filled areas
        private MeshRenderer optimalFill;
        private MeshRenderer effectiveFill;
        private MeshRenderer mediumFill;
        private MeshRenderer longFill;
        private MeshRenderer extremeFill;

        private Regiment regiment;
        private UnitData unitData;
        private float maxRange;
        private bool isVisible = false;
        private Transform zonesParentTransform;

        private void Awake()
        {
            // Get data from parent Regiment (this component is now attached to Regiment, not UnitBase)
            regiment = GetComponent<Regiment>();
            if (regiment != null)
            {
                unitData = regiment.UnitData;
            }
            
            CreateZoneVisuals();
            Hide(); // Hidden by default, only show when regiment is selected
        }

        private void CreateZoneVisuals()
        {
            // Use unit data to determine max range
            UnitData data = unitData;
            
            // Use attack range from unit data (or default 50f)
            maxRange = data != null ? data.attackRange : 50f;

            // Only create for ranged units
            if (data != null && data.attackRange <= data.meleeRange + 1f)
            {
                enabled = false;
                return;
            }

            // Create parent container - rotated to face forward (cone points in +Z direction)
            GameObject zonesParent = new GameObject("FiringZones");
            zonesParent.transform.SetParent(transform, false);
            zonesParent.transform.localPosition = new Vector3(0, yOffset, 0);
            zonesParentTransform = zonesParent.transform;
            // Default orientation: cone points forward (in Unity, forward is +Z)
            // No rotation needed as we'll build the cone in the forward direction

            // Create zones from outer to inner (so they render correctly)
            float halfAngle = coneAngle * 0.5f;
            
            // Zone 5: Extreme (150m to max range or 150m+)
            float extremeStart = Mathf.Min(150f, maxRange);
            float extremeEnd = maxRange;
            if (maxRange > 150f)
            {
                extremeLine = CreateConeArc("ExtremeArc", extremeStart, extremeEnd, extremeZoneColor, zonesParent.transform, halfAngle);
                if (showFill) extremeFill = CreateConeFill("ExtremeFill", extremeStart, extremeEnd, extremeZoneColor, zonesParent.transform, halfAngle);
            }

            // Zone 4: Long (100-150m)
            if (maxRange > 100f)
            {
                longLine = CreateConeArc("LongArc", 100f, Mathf.Min(150f, maxRange), longZoneColor, zonesParent.transform, halfAngle);
                if (showFill) longFill = CreateConeFill("LongFill", 100f, Mathf.Min(150f, maxRange), longZoneColor, zonesParent.transform, halfAngle);
            }

            // Zone 3: Medium (60-100m)
            if (maxRange > 60f)
            {
                mediumLine = CreateConeArc("MediumArc", 60f, Mathf.Min(100f, maxRange), mediumZoneColor, zonesParent.transform, halfAngle);
                if (showFill) mediumFill = CreateConeFill("MediumFill", 60f, Mathf.Min(100f, maxRange), mediumZoneColor, zonesParent.transform, halfAngle);
            }

            // Zone 2: Effective (30-60m)
            if (maxRange > 30f)
            {
                effectiveLine = CreateConeArc("EffectiveArc", 30f, Mathf.Min(60f, maxRange), effectiveZoneColor, zonesParent.transform, halfAngle);
                if (showFill) effectiveFill = CreateConeFill("EffectiveFill", 30f, Mathf.Min(60f, maxRange), effectiveZoneColor, zonesParent.transform, halfAngle);
            }

            // Zone 1: Optimal (0-30m) - always inner
            optimalLine = CreateConeArc("OptimalArc", 0f, Mathf.Min(30f, maxRange), optimalZoneColor, zonesParent.transform, halfAngle);
            if (showFill) optimalFill = CreateConeFill("OptimalFill", 0f, Mathf.Min(30f, maxRange), optimalZoneColor, zonesParent.transform, halfAngle);
        }

        /// <summary>
        /// Creates an arc line renderer for a cone sector centered on forward direction.
        /// The arc spans from -halfAngle to +halfAngle around the forward axis.
        /// </summary>
        private LineRenderer CreateConeArc(string name, float innerRadius, float outerRadius, Color color, Transform parent, float halfAngle)
        {
            GameObject arc = new GameObject(name);
            arc.transform.SetParent(parent, false);

            LineRenderer lr = arc.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = false;
            
            // Calculate segment count based on arc angle (fewer segments for smaller arcs)
            int arcSegments = Mathf.Max(8, Mathf.RoundToInt(segmentCount * (halfAngle * 2f) / 360f));
            
            // Position count: start point + arc points + end point
            // For inner radius (0 means just the center point)
            int positionsPerArc = arcSegments + 1;
            bool hasInner = innerRadius > 0.1f;
            
            lr.positionCount = hasInner ? (positionsPerArc * 2) : (positionsPerArc + 1);
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.startColor = color;
            lr.endColor = color;
            lr.material = GetRingMaterial(color);

            // Convert angles to radians - centered on forward (0 degrees = forward/Z+)
            float startAngle = -halfAngle * Mathf.Deg2Rad;
            float endAngle = halfAngle * Mathf.Deg2Rad;
            float angleStep = (endAngle - startAngle) / arcSegments;

            int posIdx = 0;
            
            // Add center point at start (for 0 inner radius, or to close the shape)
            if (!hasInner)
            {
                lr.SetPosition(posIdx++, new Vector3(0, 0, 0));
            }

            // Outer arc (from start to end angle)
            for (int i = 0; i <= arcSegments; i++)
            {
                float angle = startAngle + i * angleStep;
                // In Unity, forward is +Z, so x = sin(angle), z = cos(angle)
                float x = Mathf.Sin(angle) * outerRadius;
                float z = Mathf.Cos(angle) * outerRadius;
                lr.SetPosition(posIdx++, new Vector3(x, 0, z));
            }

            // Inner arc (from end back to start, if applicable)
            if (hasInner)
            {
                for (int i = arcSegments; i >= 0; i--)
                {
                    float angle = startAngle + i * angleStep;
                    float x = Mathf.Sin(angle) * innerRadius;
                    float z = Mathf.Cos(angle) * innerRadius;
                    lr.SetPosition(posIdx++, new Vector3(x, 0, z));
                }
            }

            return lr;
        }

        /// <summary>
        /// Creates a filled mesh for a cone sector (forward-facing firing arc).
        /// </summary>
        private MeshRenderer CreateConeFill(string name, float innerRadius, float outerRadius, Color color, Transform parent, float halfAngle)
        {
            GameObject zone = new GameObject(name);
            zone.transform.SetParent(parent, false);

            Mesh mesh = new Mesh();
            mesh.name = name + "Mesh";

            // Calculate segments based on arc angle
            int arcSegments = Mathf.Max(8, Mathf.RoundToInt(segmentCount * (halfAngle * 2f) / 360f));
            bool hasInner = innerRadius > 0.1f;

            int verticesPerArc = arcSegments + 1;
            int totalVertices = hasInner ? (verticesPerArc * 2) : (verticesPerArc + 1);
            Vector3[] vertices = new Vector3[totalVertices];
            Color[] colors = new Color[totalVertices]; // Pour le dégradé de transparence
            int[] triangles;

            // Convert angles to radians
            float startAngle = -halfAngle * Mathf.Deg2Rad;
            float endAngle = halfAngle * Mathf.Deg2Rad;
            float angleStep = (endAngle - startAngle) / arcSegments;

            int vertIdx = 0;

            // Dégradé de transparence : plus opaque au centre, plus transparent vers l'extérieur
            Color innerColor = new Color(color.r, color.g, color.b, color.a * 1.5f); // Plus opaque près du tireur
            Color outerColor = new Color(color.r, color.g, color.b, color.a * 0.5f); // Plus transparent au loin

            // Center vertex for pie-slice style (when inner radius is 0 or very small)
            if (!hasInner)
            {
                vertices[vertIdx] = new Vector3(0, 0, 0);
                colors[vertIdx] = innerColor;
                vertIdx++;
            }

            // Outer arc vertices
            for (int i = 0; i <= arcSegments; i++)
            {
                float angle = startAngle + i * angleStep;
                float x = Mathf.Sin(angle) * outerRadius;
                float z = Mathf.Cos(angle) * outerRadius;
                vertices[vertIdx] = new Vector3(x, 0, z);
                colors[vertIdx] = outerColor;
                vertIdx++;
            }

            // Inner arc vertices (for ring-sector style)
            if (hasInner)
            {
                for (int i = 0; i <= arcSegments; i++)
                {
                    float angle = startAngle + i * angleStep;
                    float x = Mathf.Sin(angle) * innerRadius;
                    float z = Mathf.Cos(angle) * innerRadius;
                    vertices[vertIdx] = new Vector3(x, 0, z);
                    colors[vertIdx] = innerColor;
                    vertIdx++;
                }
            }

            // Generate triangles
            if (hasInner)
            {
                // Ring-sector: quads between inner and outer arcs
                triangles = new int[arcSegments * 6];
                int innerStartIdx = verticesPerArc;
                
                for (int i = 0; i < arcSegments; i++)
                {
                    int baseIdx = i * 6;
                    int outer0 = i;
                    int outer1 = i + 1;
                    int inner0 = innerStartIdx + i;
                    int inner1 = innerStartIdx + i + 1;

                    triangles[baseIdx] = outer0;
                    triangles[baseIdx + 1] = inner0;
                    triangles[baseIdx + 2] = outer1;
                    triangles[baseIdx + 3] = outer1;
                    triangles[baseIdx + 4] = inner0;
                    triangles[baseIdx + 5] = inner1;
                }
            }
            else
            {
                // Pie-slice: triangles from center to outer arc
                triangles = new int[arcSegments * 3];
                int centerIdx = 0;
                
                for (int i = 0; i < arcSegments; i++)
                {
                    int baseIdx = i * 3;
                    triangles[baseIdx] = centerIdx;
                    triangles[baseIdx + 1] = i + 1;
                    triangles[baseIdx + 2] = i + 2;
                }
            }

            mesh.vertices = vertices;
            mesh.colors = colors; // Assigner les couleurs au mesh
            mesh.triangles = triangles;
            mesh.RecalculateNormals();

            MeshFilter mf = zone.AddComponent<MeshFilter>();
            mf.mesh = mesh;

            MeshRenderer mr = zone.AddComponent<MeshRenderer>();
            
            // Utiliser un shader Unlit qui gère la transparence et les Vertex Colors
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            if (mat.shader == null || !mat.shader.isSupported)
            {
                // Fallback si Particles/Unlit n'est pas dispo
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            }
            
            // Blanc car la couleur vient des vertex colors (ColorMode = Multiply par défaut dans les particles URP)
            mat.color = Color.white; 
            
            // Propriétés standards pour transparence URP
            mat.SetFloat("_Surface", 1); // 1 = Transparent
            mat.SetFloat("_Blend", 0);   // 0 = Alpha
            mat.SetFloat("_Cull", 2);    // 2 = Off (Double face)
            mat.SetFloat("_ZWrite", 0);  // 0 = Off
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_BLENDMODE_ALPHA");
            mat.renderQueue = 3000;
            
            mr.material = mat;

            return mr;
        }

        private Material GetRingMaterial(Color color)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            if (mat.shader == null || !mat.shader.isSupported)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            }
            
            mat.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a * 2.5f)); // Ligne plus opaque que le fond
            mat.SetFloat("_Surface", 1); // Transparent
            mat.SetFloat("_Blend", 0);   // Alpha
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_BLENDMODE_ALPHA");
            
            return mat;
        }

        private MaterialPropertyBlock mpb;

        public void Show()
        {
            if (isVisible) return;
            isVisible = true;

            SetZoneEnabled(optimalLine, optimalFill, true);
            SetZoneEnabled(effectiveLine, effectiveFill, true);
            SetZoneEnabled(mediumLine, mediumFill, true);
            SetZoneEnabled(longLine, longFill, true);
            SetZoneEnabled(extremeLine, extremeFill, true);
        }

        private void Update()
        {
            if (!isVisible) return;

            // Keep the cone centered on the formation
            UpdateFormationCenter();

            // Effet de pulsation très lent et subtil pour l'indicateur de portée
            float pulse = Mathf.Sin(Time.time * 2f) * 0.15f + 0.85f; // 0.7 to 1.0

            if (mpb == null) mpb = new MaterialPropertyBlock();

            // Animation des materials (pulsation de luminosité et opacité)
            UpdateZonePulse(optimalLine, optimalFill, pulse);
            UpdateZonePulse(effectiveLine, effectiveFill, pulse);
            UpdateZonePulse(mediumLine, mediumFill, pulse);
            UpdateZonePulse(longLine, longFill, pulse);
            UpdateZonePulse(extremeLine, extremeFill, pulse);
        }

        /// <summary>
        /// Repositions the cone origin to the center of the current formation.
        /// Uses the actual center of mass of alive units for accurate positioning.
        /// </summary>
        private void UpdateFormationCenter()
        {
            if (zonesParentTransform == null || regiment == null) return;

            var aliveUnits = regiment.Units;
            if (aliveUnits == null || aliveUnits.Count == 0) return;

            // Compute center of mass of alive units
            Vector3 worldCenter = Vector3.zero;
            int count = 0;
            for (int i = 0; i < aliveUnits.Count; i++)
            {
                if (aliveUnits[i] != null && aliveUnits[i].CurrentState != UnitState.Dead)
                {
                    worldCenter += aliveUnits[i].transform.position;
                    count++;
                }
            }

            if (count == 0) return;
            worldCenter /= count;

            // Convert to local space of the regiment
            Vector3 localCenter = transform.InverseTransformPoint(worldCenter);
            localCenter.y = yOffset;

            zonesParentTransform.localPosition = localCenter;
        }

        private void UpdateZonePulse(LineRenderer lr, MeshRenderer mr, float pulseMult)
        {
            if (lr != null)
            {
                Color baseColor = lr.startColor;
                Color animatedColor = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * pulseMult);
                
                lr.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", animatedColor);
                mpb.SetColor("_Color", animatedColor);
                lr.SetPropertyBlock(mpb);
            }

            if (mr != null)
            {
                mr.GetPropertyBlock(mpb);
                // On applique juste un multiplicateur d'alpha global sur le mesh (les vertex colors feront le reste)
                mpb.SetColor("_BaseColor", new Color(1f, 1f, 1f, pulseMult));
                mpb.SetColor("_Color", new Color(1f, 1f, 1f, pulseMult));
                mr.SetPropertyBlock(mpb);
            }
        }

        public void Hide()
        {
            // Always disable zone visuals, even if already marked as hidden
            SetZoneEnabled(optimalLine, optimalFill, false);
            SetZoneEnabled(effectiveLine, effectiveFill, false);
            SetZoneEnabled(mediumLine, mediumFill, false);
            SetZoneEnabled(longLine, longFill, false);
            SetZoneEnabled(extremeLine, extremeFill, false);
            
            isVisible = false;
        }

        private void SetZoneEnabled(LineRenderer line, MeshRenderer fill, bool enabled)
        {
            if (line != null) line.enabled = enabled;
            if (fill != null) fill.enabled = enabled;
        }

        public void Toggle()
        {
            if (isVisible) Hide();
            else Show();
        }

        /// <summary>
        /// Get accuracy multiplier for a given distance
        /// </summary>
        public static float GetAccuracyMultiplier(float distance)
        {
            if (distance <= 30f) return 1.00f;
            if (distance <= 60f) return 0.85f;
            if (distance <= 100f) return 0.60f;
            if (distance <= 150f) return 0.35f;
            return 0.15f;
        }

        /// <summary>
        /// Get color for accuracy zone
        /// </summary>
        public static Color GetZoneColor(float distance)
        {
            if (distance <= 30f) return new Color(0.2f, 0.8f, 0.2f, 0.8f);      // Green
            if (distance <= 60f) return new Color(0.9f, 0.9f, 0.2f, 0.8f);      // Yellow
            if (distance <= 100f) return new Color(1f, 0.6f, 0.1f, 0.8f);     // Orange
            if (distance <= 150f) return new Color(0.9f, 0.2f, 0.2f, 0.8f);     // Red
            return new Color(0.5f, 0.1f, 0.1f, 0.8f);                          // Dark Red
        }

        /// <summary>
        /// Get zone description
        /// </summary>
        public static string GetZoneDescription(float distance)
        {
            if (distance <= 30f) return "OPTIMAL (100%)";
            if (distance <= 60f) return "EFFECTIVE (85%)";
            if (distance <= 100f) return "MEDIUM (60%)";
            if (distance <= 150f) return "LONG (35%)";
            return "EXTREME (15%)";
        }
    }
}
