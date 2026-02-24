using UnityEditor;
using UnityEngine;

namespace NapoleonicWars.Editor
{
    /// <summary>
    /// Automatically sets high-quality import settings for all flag textures
    /// in UI/Flags/. Run via menu: Tools > Fix Flag Texture Quality
    /// </summary>
    public class FlagTextureImporter : AssetPostprocessor
    {
        // Auto-apply on import
        private void OnPreprocessTexture()
        {
            if (!assetPath.Contains("UI/Flags/")) return;

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.isReadable = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.anisoLevel = 4;

            // Override per-platform to ensure quality
            var platformSettings = importer.GetDefaultPlatformTextureSettings();
            platformSettings.maxTextureSize = 2048;
            platformSettings.format = TextureImporterFormat.RGBA32;
            platformSettings.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(platformSettings);

            Debug.Log($"[FlagTextureImporter] Set high quality for: {assetPath}");
        }

        // Manual menu item to fix all existing flag textures
        [MenuItem("Tools/Fix Flag Texture Quality")]
        private static void FixAllFlagTextures()
        {
            string[] guids = AssetDatabase.FindAssets("flag_ t:Texture2D", new[] { "Assets/Resources/UI/Flags" });
            int count = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = true;
                importer.isReadable = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.anisoLevel = 4;

                var platformSettings = importer.GetDefaultPlatformTextureSettings();
                platformSettings.maxTextureSize = 2048;
                platformSettings.format = TextureImporterFormat.RGBA32;
                platformSettings.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SetPlatformTextureSettings(platformSettings);

                importer.SaveAndReimport();
                count++;
            }

            Debug.Log($"[FlagTextureImporter] Fixed {count} flag textures to high quality (2048, RGBA32, Uncompressed)");
            EditorUtility.DisplayDialog("Flag Texture Quality", $"Fixed {count} flag textures.\n\nAll set to 2048px, RGBA32, Uncompressed.", "OK");
        }
    }
}
