using UnityEngine;
using UnityEngine.Rendering;

namespace NapoleonicWars.Core
{
    /// <summary>
    /// Centralized material creation that always uses URP shaders.
    /// Prevents pink/magenta materials when running under Universal Render Pipeline.
    /// </summary>
    public static class URPMaterialHelper
    {
        private static Shader _litShader;
        private static Shader _unlitShader;

        public static Shader LitShader
        {
            get
            {
                if (_litShader == null)
                {
                    // Try URP shaders first
                    _litShader = Shader.Find("Universal Render Pipeline/Lit");
                    if (_litShader == null)
                        _litShader = Shader.Find("Universal Render Pipeline/Simple Lit");
                    // Fallback to built-in
                    if (_litShader == null)
                        _litShader = Shader.Find("Standard");
                    // Last resort - get any shader
                    if (_litShader == null)
                        _litShader = Shader.Find("Diffuse");
                }
                return _litShader;
            }
        }

        public static Shader UnlitShader
        {
            get
            {
                if (_unlitShader == null)
                {
                    _unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (_unlitShader == null)
                        _unlitShader = Shader.Find("Unlit/Color");
                    if (_unlitShader == null)
                        _unlitShader = Shader.Find("Unlit/Texture");
                }
                return _unlitShader;
            }
        }

        /// <summary>
        /// Create a standard URP Lit material with the given color.
        /// </summary>
        public static Material CreateLit(Color color)
        {
            Shader shader = LitShader;
            if (shader == null)
            {
                Debug.LogError("[URPMaterialHelper] No shader found! Using default material.");
                return new Material(Shader.Find("Standard"));
            }
            
            Material mat = new Material(shader);
            mat.enableInstancing = true; // GPU instancing for thousands of identical units
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            return mat;
        }

        /// <summary>
        /// Create a URP Lit material with emission.
        /// </summary>
        public static Material CreateLitEmissive(Color color, Color emissionColor)
        {
            Material mat = CreateLit(color);
            if (mat.shader == null) return mat;
            
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emissionColor);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            
            // For URP, also set emission map if needed
            mat.SetFloat("_EmissionIntensity", 1f);
            
            return mat;
        }

        /// <summary>
        /// Create a URP Lit material with transparency.
        /// </summary>
        public static Material CreateLitTransparent(Color color)
        {
            Material mat = new Material(LitShader);
            mat.SetColor("_BaseColor", color);
            mat.color = color;

            // URP transparency settings
            mat.SetFloat("_Surface", 1); // 0=Opaque, 1=Transparent
            mat.SetFloat("_Blend", 0);   // 0=Alpha, 1=Premultiply, 2=Additive, 3=Multiply
            mat.SetFloat("_AlphaClip", 0);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            return mat;
        }

        /// <summary>
        /// Create an Unlit material (for particles, UI elements, etc.)
        /// </summary>
        public static Material CreateUnlit(Color color)
        {
            Shader shader = UnlitShader;
            if (shader == null)
                shader = Shader.Find("Standard");
                
            Material mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            return mat;
        }

        /// <summary>
        /// Fix the material on a GameObject created with CreatePrimitive().
        /// Call this right after CreatePrimitive() to replace the pink Standard material.
        /// </summary>
        public static void FixPrimitiveMaterial(GameObject go, Color color)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
                r.material = CreateLit(color);
        }
    }
}
