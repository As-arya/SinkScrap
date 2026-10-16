using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Components;

namespace SinkScrapt.Editor
{
    public static class AlphaBuilder
    {
        const string Root="Assets/SinkScrapt";
        const string ResourcesRoot=Root+"/Resources/SinkScrapt";
        [MenuItem("SinkScrapt/Build Alpha Assets")]
        public static void BuildAssets()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before generating assets.");
            Directory.CreateDirectory(ResourcesRoot);Directory.CreateDirectory(Root+"/Scenes");AssetDatabase.Refresh();
            TMPro.TMP_PackageResourceImporter.ImportResources(true,false,false);
            var balance=AssetDatabase.LoadAssetAtPath<BalanceConfig>(ResourcesRoot+"/Balance.asset");
            if(balance==null)AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<BalanceConfig>(),ResourcesRoot+"/Balance.asset");
            CreateMaterial("Lit",Shader.Find("Universal Render Pipeline/Lit"),false);
            CreateMaterial("Emission",Shader.Find("Universal Render Pipeline/Lit"),true);
            CreateMaterial("Water",Shader.Find("SinkScrapt/Water"),false);
            Prefab("Session",go=>go.AddComponent<NetSession>());
            Prefab("Player",go=>{var cc=go.AddComponent<CharacterController>();cc.height=1.8f;cc.radius=.3f;cc.center=Vector3.up*.9f;cc.stepOffset=.32f;cc.slopeLimit=50;cc.skinWidth=.04f;go.AddComponent<OwnerTransform>();go.AddComponent<NetPlayer>();});
            Prefab("Item",go=>{go.AddComponent<NetworkTransform>();var rb=go.AddComponent<Rigidbody>();rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;go.AddComponent<BoxCollider>();go.AddComponent<NetworkRigidbody>();go.AddComponent<NetItem>();});
            Prefab("Enemy",go=>{go.AddComponent<NetworkTransform>();go.AddComponent<NetEnemy>();});
            Scene("Bootstrap",scene=>{
                new GameObject("SinkScrapt").AddComponent<SinkApp>();
                var camera=new GameObject("Menu Camera",typeof(Camera),typeof(AudioListener));camera.tag="MainCamera";camera.transform.position=new Vector3(-256,23,-52);camera.transform.LookAt(new Vector3(-300,7,0));
                camera.GetComponent<Camera>().backgroundColor=new Color(.055f,.12f,.14f);camera.GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;
            });
            var names=new[]{"SmallBoat","Island","Exterior","Interior"};
            for(int i=0;i<names.Length;i++){var kind=(WorldZoneKind)i;Scene(names[i],scene=>new GameObject(names[(int)kind]+" Root").AddComponent<WorldZone>().kind=kind);}
            EditorBuildSettings.scenes=new[]{"Bootstrap","SmallBoat","Island","Exterior","Interior"}.Select(n=>new EditorBuildSettingsScene(Root+"/Scenes/"+n+".unity",true)).ToArray();
            PlayerSettings.companyName="SinkScrapt";PlayerSettings.productName="SinkScrapt";PlayerSettings.defaultScreenWidth=1920;PlayerSettings.defaultScreenHeight=1080;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=true;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            QualitySettings.shadowDistance=35;QualitySettings.shadowCascades=2;QualitySettings.antiAliasing=2;
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            BuildRetroLook();BuildAtmosphere();
            var active=SceneManager.GetActiveScene();if(!active.isDirty)EditorSceneManager.OpenScene(Root+"/Scenes/Bootstrap.unity",OpenSceneMode.Single);
            Debug.Log("SinkScrapt alpha assets ready: 5 scenes, 4 network prefabs, balance, water and TMP resources.");
        }
        // Wires the PS1-horror post pass into the PC renderer and makes every quality level use the PC pipeline asset (it is the only one with the pass).
        [MenuItem("SinkScrapt/Build Retro Look")]
        public static void BuildRetroLook()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before generating assets.");
            var shader=Shader.Find("Hidden/SinkScrapt/RetroPost");if(shader==null)throw new InvalidOperationException("Hidden/SinkScrapt/RetroPost shader missing.");
            Directory.CreateDirectory(ResourcesRoot);
            var matPath=ResourcesRoot+"/RetroPost.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,matPath);}else mat.shader=shader;
            var data=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");if(data==null)throw new InvalidOperationException("PC_Renderer asset missing.");
            var feature=data.rendererFeatures.OfType<FullScreenPassRendererFeature>().FirstOrDefault(f=>f!=null&&f.name=="SinkScrapt Retro");
            if(feature==null) {
                feature=ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();feature.name="SinkScrapt Retro";
                AssetDatabase.AddObjectToAsset(feature,data);AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature,out _,out long localId);
                var so=new SerializedObject(data);var features=so.FindProperty("m_RendererFeatures");var map=so.FindProperty("m_RendererFeatureMap");
                features.arraySize++;features.GetArrayElementAtIndex(features.arraySize-1).objectReferenceValue=feature;
                map.arraySize++;map.GetArrayElementAtIndex(map.arraySize-1).longValue=localId;so.ApplyModifiedProperties();
            }
            feature.passMaterial=mat;feature.passIndex=0;feature.fetchColorBuffer=true;feature.requirements=ScriptableRenderPassInput.None;feature.injectionPoint=FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            EditorUtility.SetDirty(feature);EditorUtility.SetDirty(data);
            var rp=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            rp.renderScale=.55f;rp.upscalingFilter=UpscalingFilterSelection.Point;EditorUtility.SetDirty(rp);
            int current=QualitySettings.GetQualityLevel();
            for(int i=0;i<QualitySettings.names.Length;i++){QualitySettings.SetQualityLevel(i,false);QualitySettings.renderPipeline=rp;}
            QualitySettings.SetQualityLevel(Mathf.Clamp(1,0,QualitySettings.names.Length-1),false);
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Debug.Log("SinkScrapt retro look ready: RetroPost pass on PC_Renderer, render scale 0.55 point upscaling.");
        }
        // Dusk sky and the shared particle material. Both live in Resources so their shaders ship with the build.
        [MenuItem("SinkScrapt/Build Atmosphere")]
        public static void BuildAtmosphere()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before generating assets.");
            Directory.CreateDirectory(ResourcesRoot);
            var skyShader=Shader.Find("Skybox/Procedural");if(skyShader==null)throw new InvalidOperationException("Skybox/Procedural shader missing.");
            var skyPath=ResourcesRoot+"/Sky.mat";var sky=AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if(sky==null){sky=new Material(skyShader);AssetDatabase.CreateAsset(sky,skyPath);}else sky.shader=skyShader;
            sky.SetFloat("_SunSize",.035f);sky.SetFloat("_SunSizeConvergence",6);sky.SetFloat("_AtmosphereThickness",.8f);sky.SetColor("_SkyTint",new Color(.34f,.36f,.44f));sky.SetColor("_GroundColor",new Color(.16f,.20f,.24f));sky.SetFloat("_Exposure",.85f);
            EditorUtility.SetDirty(sky);
            var particlePath=ResourcesRoot+"/Particle.mat";
            if(AssetDatabase.LoadAssetAtPath<Material>(particlePath)==null){var shader=Shader.Find("Sprites/Default");if(shader==null)throw new InvalidOperationException("Sprites/Default shader missing.");AssetDatabase.CreateAsset(new Material(shader),particlePath);}
            // Lit, alpha-blended materials: porthole glass (WorldFactory.Glass) and wall/floor decals (blood, scratches, rust,
            // writing: WorldFactory.Decal). As assets they keep URP's transparent Lit variant in the build.
            TransparentLit("Glass",.92f);TransparentLit("Decal",.3f);
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Debug.Log("SinkScrapt atmosphere ready: dusk sky, particle, glass and decal materials.");
        }
        static void TransparentLit(string name,float smoothness)
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new InvalidOperationException("URP Lit shader missing.");
            var path=ResourcesRoot+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}else mat.shader=shader;
            mat.SetFloat("_Surface",1);mat.SetFloat("_Blend",0);mat.SetFloat("_AlphaClip",0);mat.SetFloat("_Cull",2);
            mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);mat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha",(float)BlendMode.One);mat.SetFloat("_DstBlendAlpha",(float)BlendMode.OneMinusSrcAlpha);mat.SetFloat("_ZWrite",0);
            // "Preserve specular" expects premultiplied blending; left on with SrcAlpha blending it rendered the glass solid cyan.
            mat.SetFloat("_BlendModePreserveSpecular",0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.DisableKeyword("_ALPHATEST_ON");mat.SetOverrideTag("RenderType","Transparent");
            mat.renderQueue=(int)RenderQueue.Transparent;mat.SetShaderPassEnabled("DepthOnly",false);mat.SetShaderPassEnabled("ShadowCaster",false);
            mat.SetFloat("_Smoothness",smoothness);mat.SetFloat("_ReceiveShadows",1);
            EditorUtility.SetDirty(mat);
        }
        static void CreateMaterial(string name,Shader shader,bool emission)
        {
            var path=ResourcesRoot+"/"+name+".mat";if(AssetDatabase.LoadAssetAtPath<Material>(path)!=null)return;
            if(shader==null)throw new InvalidOperationException("Shader missing: "+name);
            var mat=new Material(shader);if(emission){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",Color.white*2);}AssetDatabase.CreateAsset(mat,path);
        }
        static void Prefab(string name,Action<GameObject> setup)
        {
            var path=ResourcesRoot+"/"+name+".prefab";if(AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null)return;
            var go=new GameObject(name);go.AddComponent<NetworkObject>();setup(go);PrefabUtility.SaveAsPrefabAsset(go,path);UnityEngine.Object.DestroyImmediate(go);
        }
        static void Scene(string name,Action<Scene> setup)
        {
            var path=Root+"/Scenes/"+name+".unity";if(File.Exists(path))return;
            var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);setup(scene);EditorSceneManager.SaveScene(scene,path);EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(previous);
        }
        public static string HealthCheck()
        {
            foreach(var name in new[]{"Session","Player","Item","Enemy"}) {
                var prefab=Resources.Load<GameObject>("SinkScrapt/"+name);
                if(prefab==null||prefab.GetComponent<NetworkObject>()==null)throw new Exception("Missing network prefab "+name);
            }
            for(int seed=0;seed<1000;seed++)if(!InteriorGenerator.Generate(seed).Validate())throw new Exception("Bad layout "+seed);
            return "1000 valid seeds and 4 network prefabs verified";
        }
    }
}
