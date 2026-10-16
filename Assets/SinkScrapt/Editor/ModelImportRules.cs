using UnityEditor;

namespace SinkScrapt.EditorTools
{
    // Import rules for the Blender exports in Resources/SinkScrapt/Models (Docs/BlenderAssets.md). The meshes stay readable because
    // the interior is static-batched at runtime (StaticBatchingUtility needs CPU access); colliders, animation, cameras and lights
    // are never imported because gameplay keeps its colliders in code. Material slots are kept by name for ModelLibrary to remap.
    public class ModelImportRules : AssetPostprocessor
    {
        const string Root="Assets/SinkScrapt/Resources/SinkScrapt/Models/";
        void OnPreprocessModel()
        {
            if(!assetPath.StartsWith(Root,System.StringComparison.Ordinal))return;
            var model=(ModelImporter)assetImporter;
            model.isReadable=true;model.addCollider=false;
            model.importAnimation=false;model.animationType=ModelImporterAnimationType.None;
            model.importCameras=false;model.importLights=false;model.importBlendShapes=false;
            model.materialImportMode=ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            // Rigged characters (Models/Characters): one armature with its clips as FBX takes, played through the legacy
            // Animation component (ClipSet) so no Animator Controller asset is needed. They are exported without "Apply
            // Transform" (it breaks armature animation), so the importer bakes the axis conversion instead.
            if(assetPath.StartsWith(Root+"Characters/",System.StringComparison.Ordinal)) {
                model.isReadable=false;model.importAnimation=true;model.animationType=ModelImporterAnimationType.Legacy;
                model.bakeAxisConversion=true;model.resampleCurves=true;model.animationCompression=ModelImporterAnimationCompression.KeyframeReduction;
            }
        }
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith(Root,System.StringComparison.Ordinal))return;
            // Item palettes: 8 x 8 flat swatches sampled at their centres, so no filtering, mipmaps or compression.
            var texture=(TextureImporter)assetImporter;
            texture.filterMode=UnityEngine.FilterMode.Point;texture.mipmapEnabled=false;texture.textureCompression=TextureImporterCompression.Uncompressed;
        }
    }
}
