using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SinkScrapt
{
    // Drives the PS1-horror filter: point-sampled low-resolution render scale plus the RetroPost full-screen pass.
    // Gameplay feedback (hits, drowning, nearby monsters) is fed to the shader through global floats so it renders behind the HUD.
    public class RetroLook : MonoBehaviour
    {
        public static RetroLook Instance;
        public static readonly string[] Names={"MATI","RINGAN","SEDANG","KUAT"};
        static readonly float[] Scales={1,.75f,.55f,.42f};
        static readonly float[] Strength={0,.55f,.85f,1};
        static readonly int Retro=Shader.PropertyToID("_SinkRetro"),Damage=Shader.PropertyToID("_SinkDamage"),Wet=Shader.PropertyToID("_SinkWet"),Fear=Shader.PropertyToID("_SinkFear"),Clock=Shader.PropertyToID("_SinkTime");
        public static readonly string[] ShadowNames={"MATI","RENDAH","TINGGI"};
        static readonly float[] ShadowDistance={0,22,45};
        static readonly int BrightId=Shader.PropertyToID("_SinkBright");
        public int Level {get;private set;}
        public int Shadows {get;private set;}
        public static float Brightness {get;private set;}=1;
        public static void SetBrightness(float value) {Brightness=Mathf.Clamp(value,.6f,1.6f);PlayerPrefs.SetFloat("SinkBright",Brightness);Shader.SetGlobalFloat(BrightId,Brightness);}
        float damage,wet,fear,lastHealth=100;
        UniversalRenderPipelineAsset asset;
        float originalScale,originalShadow;
        UpscalingFilterSelection originalFilter;
        void Awake()
        {
            Instance=this;asset=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if(asset!=null) {originalScale=asset.renderScale;originalFilter=asset.upscalingFilter;originalShadow=asset.shadowDistance;}
            SetLevel(PlayerPrefs.GetInt("SinkRetro",2));SetShadows(PlayerPrefs.GetInt("SinkShadow",2));SetBrightness(PlayerPrefs.GetFloat("SinkBright",1));
        }
        public void SetShadows(int level)
        {
            Shadows=Mathf.Clamp(level,0,ShadowDistance.Length-1);PlayerPrefs.SetInt("SinkShadow",Shadows);
            if(asset!=null)asset.shadowDistance=ShadowDistance[Shadows];
        }
        public void SetLevel(int level)
        {
            Level=Mathf.Clamp(level,0,Scales.Length-1);PlayerPrefs.SetInt("SinkRetro",Level);
            if(asset!=null) {asset.renderScale=Scales[Level];asset.upscalingFilter=Level==0?UpscalingFilterSelection.Linear:UpscalingFilterSelection.Point;}
            Shader.SetGlobalFloat(Retro,Strength[Level]);
        }
        public void Hit(float amount) {damage=Mathf.Clamp01(Mathf.Max(damage,amount));}
        void Update()
        {
            float dt=Time.unscaledDeltaTime;var player=NetPlayer.Local;
            float targetWet=0,targetFear=0;
            if(player!=null&&player.IsSpawned&&player.Alive.Value) {
                targetWet=player.Underwater?1:0;
                if(player.Oxygen.Value<8&&player.Underwater)targetWet=1;
                targetFear=NetEnemy.ThreatTo(player);
                if(player.Health.Value<lastHealth-.5f) {float hurt=Mathf.Clamp01((lastHealth-player.Health.Value)/45f+.25f);Hit(hurt);player.shake=Mathf.Max(player.shake,hurt);}
                lastHealth=player.Health.Value;
                if(player.Health.Value<30)damage=Mathf.Max(damage,.18f*(.5f+.5f*Mathf.Sin(Time.unscaledTime*6)));
            } else {lastHealth=player!=null?player.Health.Value:100;}
            damage=Mathf.MoveTowards(damage,0,dt*1.3f);
            wet=Mathf.MoveTowards(wet,targetWet,dt*2.5f);fear=Mathf.MoveTowards(fear,targetFear,dt*1.2f);
            Shader.SetGlobalFloat(Damage,damage);Shader.SetGlobalFloat(Wet,wet);Shader.SetGlobalFloat(Fear,fear);Shader.SetGlobalFloat(Clock,Time.unscaledTime);
        }
        void OnDestroy()
        {
            if(Instance==this)Instance=null;
            // The URP asset is shared with the Editor; put it back so Play Mode does not leave a low render scale behind.
            if(Application.isEditor&&asset!=null) {asset.renderScale=originalScale;asset.upscalingFilter=originalFilter;asset.shadowDistance=originalShadow;}
            Shader.SetGlobalFloat(Retro,0);Shader.SetGlobalFloat(Damage,0);Shader.SetGlobalFloat(Wet,0);Shader.SetGlobalFloat(Fear,0);Shader.SetGlobalFloat(BrightId,1);
        }
    }
}
