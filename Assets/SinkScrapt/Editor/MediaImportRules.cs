using UnityEditor;
using UnityEngine;

namespace SinkScrapt.EditorTools
{
    // Import rules for the processed CC0 recordings in Resources/SinkScrapt/Audio (built by ArtSource/Audio/build_audio.py,
    // sources in Docs/Assets.md) and for the item icons in Resources/SinkScrapt/Icons. Short one-shots are decompressed on load
    // so SoundBank can hand them out without a hitch; the long ambience beds stay Vorbis-compressed in memory.
    public class MediaImportRules : AssetPostprocessor
    {
        const string AudioRoot="Assets/SinkScrapt/Resources/SinkScrapt/Audio/",IconRoot="Assets/SinkScrapt/Resources/SinkScrapt/Icons/";
        static readonly string[] Beds={"Sea_","Lap_","Wind_","RoomTone_","Tension_","DrownedGurgle_","StalkerBreath_"};
        void OnPreprocessAudio()
        {
            if(!assetPath.StartsWith(AudioRoot,System.StringComparison.Ordinal))return;
            var audio=(AudioImporter)assetImporter;var name=System.IO.Path.GetFileName(assetPath);
            bool bed=System.Array.Exists(Beds,b=>name.StartsWith(b,System.StringComparison.Ordinal));
            var settings=audio.defaultSampleSettings;
            settings.loadType=bed?AudioClipLoadType.CompressedInMemory:AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=bed?.55f:.7f;settings.preloadAudioData=true;
            audio.defaultSampleSettings=settings;audio.loadInBackground=false;audio.forceToMono=false;
        }
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith(IconRoot,System.StringComparison.Ordinal))return;
            // Item icons rendered in Blender (ArtSource/Blender/scripts/icons.py): UI sprites with alpha, no mipmaps.
            var texture=(TextureImporter)assetImporter;
            texture.textureType=TextureImporterType.Sprite;texture.spriteImportMode=SpriteImportMode.Single;texture.alphaIsTransparency=true;
            texture.mipmapEnabled=false;texture.filterMode=FilterMode.Bilinear;texture.textureCompression=TextureImporterCompression.Uncompressed;texture.wrapMode=TextureWrapMode.Clamp;
        }
    }
}
