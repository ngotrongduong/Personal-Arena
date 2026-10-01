using UnityEditor;
using UnityEngine;

namespace PersonalArena.View.Editor
{
    /// <summary>
    /// Import settings of the viewer's audio (Assets/ThirdParty/Audio): short effects are Vorbis, decompressed on
    /// load (no decoding cost when a horde plays them); the music loops are Vorbis and streamed from disk.
    /// </summary>
    public sealed class AudioImportRules : AssetPostprocessor
    {
        private const string Root = "Assets/ThirdParty/Audio/";
        private const string MusicFolder = Root + "Music/";

        public override uint GetVersion() => 1;

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Root, System.StringComparison.Ordinal))
            {
                return;
            }

            AudioImporter importer = (AudioImporter)assetImporter;
            bool isMusic = assetPath.StartsWith(MusicFolder, System.StringComparison.Ordinal);
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.loadType = isMusic ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.quality = isMusic ? 0.6f : 0.7f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = isMusic;
            importer.forceToMono = false;
        }
    }
}
