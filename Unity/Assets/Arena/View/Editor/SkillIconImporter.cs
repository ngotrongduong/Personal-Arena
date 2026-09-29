using UnityEditor;

namespace PersonalArena.View.Editor
{
    /// <summary>
    /// Import settings for the skill glyphs in a Resources/SkillIcons folder: SkillIconFactory reads
    /// their pixels at runtime to compose the HUD icons, so they must stay readable and uncompressed.
    /// </summary>
    public sealed class SkillIconImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/SkillIcons/"))
            {
                return;
            }

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
