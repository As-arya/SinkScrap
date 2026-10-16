using UnityEngine;

namespace SinkScrapt
{
    // Time of day over the sea, the boat and the island, from NetSession.Hour (host-chosen, shared clock). An expedition that
    // starts by day ends at night and one that starts at night ends at dawn; the island gets a random day or night on each
    // visit. Drives the sun and a moon, the procedural sky, ambient light, the outdoor fog and the sea's sky reflection.
    // Lives on the SmallBoat zone, which stays loaded for the whole session. The wreck interior ignores it (it is dark anyway).
    public class SkyClock : MonoBehaviour
    {
        public Light sun;
        public Renderer ocean;
        Light moon;
        Material sky,sea,originalSky;
        static readonly int Exposure=Shader.PropertyToID("_Exposure"),Tint=Shader.PropertyToID("_SkyTint"),Thickness=Shader.PropertyToID("_AtmosphereThickness"),
            SeaSky=Shader.PropertyToID("_SkyColor"),SeaBase=Shader.PropertyToID("_BaseColor");
        // A washed-out, overcast grey by day (no postcard blue), a bruised dusk and a near-black night.
        static readonly Color DayFog=new(.33f,.37f,.40f),DuskFog=new(.24f,.2f,.21f),NightFog=new(.03f,.038f,.055f);
        public static float Daylight {get;private set;}=.5f;
        void Start()
        {
            var go=new GameObject("Moon");go.transform.SetParent(transform,false);moon=go.AddComponent<Light>();moon.type=LightType.Directional;
            moon.color=new Color(.55f,.66f,.9f);moon.shadows=LightShadows.Soft;moon.shadowStrength=.6f;go.transform.rotation=Quaternion.Euler(38,150,0);
            originalSky=RenderSettings.skybox;if(originalSky!=null) {sky=new Material(originalSky);RenderSettings.skybox=sky;}
            if(ocean!=null&&ocean.sharedMaterial!=null) {sea=new Material(ocean.sharedMaterial);ocean.sharedMaterial=sea;}
        }
        void Update()
        {
            var session=NetSession.Instance;float hour=session!=null?session.Hour:18.5f;
            // Sun elevation: 0 at 06:00 and 18:00, highest at noon; negative at night.
            float elevation=Mathf.Sin((hour-6)/12*Mathf.PI);
            float day=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.1f,.3f,elevation));Daylight=day;
            float warm=1-Mathf.Clamp01(Mathf.Abs(elevation)/.35f);
            // Inside the wreck neither sky light nor sky reflections reach the diver: only lamps, hazards and torches light it.
            var local=NetPlayer.Local;bool inside=local!=null&&local.Zone.Value==PlayerZone.Interior;
            RenderSettings.reflectionIntensity=Mathf.MoveTowards(RenderSettings.reflectionIntensity,inside?0:1,Time.deltaTime*3);
            if(sun!=null) {
                sun.transform.rotation=Quaternion.Euler(Mathf.Lerp(4,58,Mathf.Clamp01(elevation)),-38+(hour-12)*9,0);
                sun.intensity=1.3f*day;sun.enabled=day>.01f&&!inside;
                sun.color=Color.Lerp(new Color(1,.62f,.42f),new Color(1,.96f,.9f),Mathf.Clamp01(elevation*2.5f));
            }
            moon.intensity=.32f*(1-day);moon.enabled=day<.99f&&!inside;
            RenderSettings.sun=day>.35f||sun==null?sun:moon;
            var skyAmbient=Color.Lerp(new Color(.11f,.14f,.22f),new Color(.46f,.52f,.62f),day);skyAmbient=Color.Lerp(skyAmbient,new Color(.5f,.36f,.32f),warm*.45f);
            RenderSettings.ambientSkyColor=skyAmbient;
            RenderSettings.ambientEquatorColor=Color.Lerp(new Color(.07f,.09f,.13f),new Color(.32f,.37f,.40f),day);
            RenderSettings.ambientGroundColor=Color.Lerp(new Color(.03f,.04f,.05f),new Color(.13f,.15f,.15f),day);
            WorldFactory.OutdoorAmbient=Mathf.Lerp(.7f,1.0f,day);WorldFactory.OutdoorFogDensity=Mathf.Lerp(.017f,.0095f,day);
            WorldFactory.OutdoorFog=Color.Lerp(Color.Lerp(NightFog,DayFog,day),DuskFog,warm*.6f);
            if(sky!=null) {
                sky.SetFloat(Exposure,Mathf.Lerp(.16f,1.15f,day));sky.SetFloat(Thickness,Mathf.Lerp(.45f,1f,day)+warm*.4f);
                sky.SetColor(Tint,Color.Lerp(new Color(.16f,.2f,.34f),new Color(.44f,.5f,.6f),day));
            }
            if(sea!=null) {
                sea.SetColor(SeaSky,Color.Lerp(new Color(.06f,.08f,.12f),new Color(.5f,.58f,.68f),day));
                sea.SetColor(SeaBase,Color.Lerp(new Color(.015f,.06f,.07f),new Color(.05f,.27f,.3f),day));
            }
        }
        void OnDestroy()
        {
            if(RenderSettings.skybox==sky&&originalSky!=null)RenderSettings.skybox=originalSky;
            WorldFactory.OutdoorFog=new Color(.17f,.21f,.25f);WorldFactory.OutdoorAmbient=1;RenderSettings.reflectionIntensity=1;
            if(sky!=null)Destroy(sky);if(sea!=null)Destroy(sea);
        }
    }
}
