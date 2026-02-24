using UnityEngine;
using System.Collections.Generic;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Generates procedural 3D visuals for logistics convoys on the campaign map.
    /// Creates wooden cart meshes with faction-colored flags and resource indicators.
    /// </summary>
    public static class ConvoyVisualGenerator
    {
        // ==================== CONVOY VISUAL ====================
        
        /// <summary>
        /// Create a full convoy visual: wooden cart body, wheels, canvas cover, and faction flag.
        /// Scale based on total resource value.
        /// </summary>
        public static GameObject CreateConvoyVisual(LogisticsConvoy convoy, FactionType faction)
        {
            GameObject root = new GameObject($"Convoy_{convoy.convoyId}");
            
            float valueScale = Mathf.Clamp(convoy.TotalResourceValue / 200f, 0.5f, 2f);
            root.transform.localScale = Vector3.one * valueScale;
            
            // Cart body
            GameObject body = CreateCartBody(root.transform, faction);
            
            // Wheels (4)
            CreateWheel(root.transform, new Vector3(-0.4f, -0.15f, 0.3f));
            CreateWheel(root.transform, new Vector3(-0.4f, -0.15f, -0.3f));
            CreateWheel(root.transform, new Vector3(0.4f, -0.15f, 0.3f));
            CreateWheel(root.transform, new Vector3(0.4f, -0.15f, -0.3f));
            
            // Canvas cover (if large convoy)
            if (convoy.TotalResourceValue > 100f)
                CreateCanvasCover(root.transform);
            
            // Faction flag
            CreateConvoyFlag(root.transform, faction);
            
            // Horses (2)
            CreateHorse(root.transform, new Vector3(1.0f, 0f, 0.15f));
            CreateHorse(root.transform, new Vector3(1.0f, 0f, -0.15f));
            
            return root;
        }
        
        // ==================== ROUTE LINE VISUAL ====================
        
        /// <summary>
        /// Create a dashed gold line representing a permanent logistics route on the map.
        /// </summary>
        public static GameObject CreateRouteLineVisual(List<Vector3> worldPoints, int routeLevel, bool isAllied)
        {
            if (worldPoints == null || worldPoints.Count < 2) return null;
            
            GameObject root = new GameObject("RouteVisual");
            
            Color lineColor = isAllied 
                ? new Color(0.3f, 0.6f, 0.9f, 0.6f)  // Blue for allied routes
                : routeLevel switch
                {
                    1 => new Color(0.7f, 0.6f, 0.3f, 0.4f),  // Faint gold
                    2 => new Color(0.85f, 0.7f, 0.3f, 0.55f), // Bright gold
                    3 => new Color(1f, 0.85f, 0.3f, 0.7f),    // Brilliant gold
                    _ => new Color(0.7f, 0.6f, 0.3f, 0.4f)
                };
            
            float lineWidth = routeLevel switch { 1 => 8f, 2 => 12f, 3 => 16f, _ => 8f };
            
            // Create dashed line segments between consecutive points
            for (int i = 0; i < worldPoints.Count - 1; i++)
            {
                Vector3 from = worldPoints[i];
                Vector3 to = worldPoints[i + 1];
                float dist = Vector3.Distance(from, to);
                float dashLen = 20f;
                int dashCount = Mathf.Max(1, Mathf.FloorToInt(dist / dashLen));
                
                for (int d = 0; d < dashCount; d += 2) // Every other segment is a gap
                {
                    float t0 = (float)d / dashCount;
                    float t1 = Mathf.Min(1f, (float)(d + 1) / dashCount);
                    Vector3 p0 = Vector3.Lerp(from, to, t0);
                    Vector3 p1 = Vector3.Lerp(from, to, t1);
                    
                    CreateDashSegment(root.transform, p0, p1, lineWidth, lineColor);
                }
            }
            
            return root;
        }
        
        // ==================== INTERNAL COMPONENTS ====================
        
        private static GameObject CreateCartBody(Transform parent, FactionType faction)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "CartBody";
            body.transform.SetParent(parent);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = new Vector3(0.8f, 0.2f, 0.4f);
            
            Renderer r = body.GetComponent<Renderer>();
            r.material = new Material(Shader.Find("Standard"));
            r.material.color = new Color(0.45f, 0.3f, 0.15f); // Wood brown
            
            Object.Destroy(body.GetComponent<Collider>());
            return body;
        }
        
        private static void CreateWheel(Transform parent, Vector3 localPos)
        {
            GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = "Wheel";
            wheel.transform.SetParent(parent);
            wheel.transform.localPosition = localPos;
            wheel.transform.localScale = new Vector3(0.15f, 0.02f, 0.15f);
            wheel.transform.localRotation = Quaternion.Euler(0, 0, 90f);
            
            Renderer r = wheel.GetComponent<Renderer>();
            r.material = new Material(Shader.Find("Standard"));
            r.material.color = new Color(0.3f, 0.2f, 0.1f); // Dark wood
            
            Object.Destroy(wheel.GetComponent<Collider>());
        }
        
        private static void CreateCanvasCover(Transform parent)
        {
            // Half-cylinder canvas cover
            GameObject cover = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            cover.name = "Canvas";
            cover.transform.SetParent(parent);
            cover.transform.localPosition = new Vector3(0, 0.2f, 0);
            cover.transform.localScale = new Vector3(0.35f, 0.3f, 0.35f);
            cover.transform.localRotation = Quaternion.Euler(0, 0, 90f);
            
            Renderer r = cover.GetComponent<Renderer>();
            r.material = new Material(Shader.Find("Standard"));
            r.material.color = new Color(0.85f, 0.8f, 0.7f); // Canvas white
            
            Object.Destroy(cover.GetComponent<Collider>());
        }
        
        private static void CreateConvoyFlag(Transform parent, FactionType faction)
        {
            // Flag pole
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "FlagPole";
            pole.transform.SetParent(parent);
            pole.transform.localPosition = new Vector3(-0.35f, 0.35f, 0);
            pole.transform.localScale = new Vector3(0.02f, 0.3f, 0.02f);
            
            Renderer pr = pole.GetComponent<Renderer>();
            pr.material = new Material(Shader.Find("Standard"));
            pr.material.color = new Color(0.35f, 0.25f, 0.1f); // Wood
            Object.Destroy(pole.GetComponent<Collider>());
            
            // Flag
            GameObject flag = GameObject.CreatePrimitive(PrimitiveType.Quad);
            flag.name = "Flag";
            flag.transform.SetParent(pole.transform);
            flag.transform.localPosition = new Vector3(0.5f, 0.5f, 0);
            flag.transform.localScale = new Vector3(4f, 2.5f, 1f);
            
            Renderer fr = flag.GetComponent<Renderer>();
            fr.material = new Material(Shader.Find("Standard"));
            fr.material.color = GetFactionColor(faction);
            fr.material.SetFloat("_Glossiness", 0f);
            Object.Destroy(flag.GetComponent<Collider>());
        }
        
        private static void CreateHorse(Transform parent, Vector3 localPos)
        {
            // Simple horse body (box + smaller head box)
            GameObject horse = GameObject.CreatePrimitive(PrimitiveType.Cube);
            horse.name = "Horse";
            horse.transform.SetParent(parent);
            horse.transform.localPosition = localPos;
            horse.transform.localScale = new Vector3(0.4f, 0.25f, 0.15f);
            
            Renderer r = horse.GetComponent<Renderer>();
            r.material = new Material(Shader.Find("Standard"));
            r.material.color = new Color(0.4f, 0.28f, 0.15f); // Horse brown
            Object.Destroy(horse.GetComponent<Collider>());
            
            // Head
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Head";
            head.transform.SetParent(horse.transform);
            head.transform.localPosition = new Vector3(0.6f, 0.3f, 0);
            head.transform.localScale = new Vector3(0.4f, 0.5f, 0.8f);
            
            Renderer hr = head.GetComponent<Renderer>();
            hr.material = new Material(Shader.Find("Standard"));
            hr.material.color = new Color(0.35f, 0.25f, 0.12f);
            Object.Destroy(head.GetComponent<Collider>());
        }
        
        private static void CreateDashSegment(Transform parent, Vector3 from, Vector3 to, float width, Color color)
        {
            GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seg.name = "Dash";
            seg.transform.SetParent(parent);
            
            Vector3 mid = (from + to) / 2f;
            mid.y += 2f; // Slightly above terrain
            seg.transform.position = mid;
            
            float length = Vector3.Distance(from, to);
            seg.transform.localScale = new Vector3(length, 1f, width);
            seg.transform.LookAt(to);
            
            Renderer r = seg.GetComponent<Renderer>();
            r.material = new Material(Shader.Find("Standard"));
            r.material.color = color;
            r.material.SetFloat("_Glossiness", 0f);
            r.material.SetFloat("_Mode", 3); // Transparent
            r.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            r.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            r.material.SetInt("_ZWrite", 0);
            r.material.DisableKeyword("_ALPHATEST_ON");
            r.material.EnableKeyword("_ALPHABLEND_ON");
            r.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            r.material.renderQueue = 3000;
            
            Object.Destroy(seg.GetComponent<Collider>());
        }
        
        // ==================== FACTION COLORS ====================
        
        public static Color GetFactionColor(FactionType faction)
        {
            return faction switch
            {
                FactionType.France => new Color(0.1f, 0.2f, 0.6f),     // Blue
                FactionType.Britain => new Color(0.8f, 0.15f, 0.15f),  // Red
                FactionType.Prussia => new Color(0.15f, 0.15f, 0.15f), // Black/grey
                FactionType.Russia => new Color(0.15f, 0.5f, 0.15f),   // Green
                FactionType.Austria => new Color(0.9f, 0.85f, 0.3f),   // Yellow/gold
                FactionType.Spain => new Color(0.85f, 0.75f, 0f),      // Gold
                FactionType.Ottoman => new Color(0.9f, 0.2f, 0.2f),    // Red/crimson
                _ => new Color(0.5f, 0.5f, 0.5f)                       // Grey
            };
        }
    }
}
