using UnityEngine;

namespace SinkScrapt
{
    // Small procedural effects: steam, sparks and drifting dust particles plus flickering and pulsing lamps.
    public static class Fx
    {
        static Material material;
        static Texture2D soft;
        static Texture2D Soft
        {
            get {
                if(soft!=null)return soft;
                const int n=32;soft=new Texture2D(n,n,TextureFormat.RGBA32,false) {wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
                var pixels=new Color32[n*n];
                for(int y=0;y<n;y++)for(int x=0;x<n;x++) {float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(n/2f,n/2f))/(n/2f);float a=Mathf.Clamp01(1-d);a*=a;pixels[y*n+x]=new Color32(255,255,255,(byte)(a*255));}
                soft.SetPixels32(pixels);soft.Apply();return soft;
            }
        }
        static Material Particle
        {
            get {
                if(material!=null)return material;
                var source=Resources.Load<Material>("SinkScrapt/Particle");material=source!=null?new Material(source):new Material(Shader.Find("Sprites/Default"));material.mainTexture=Soft;return material;
            }
        }
        static ParticleSystem Make(string name,Transform parent,Vector3 local,Vector3 euler)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=local;go.transform.localRotation=Quaternion.Euler(euler);
            var system=go.AddComponent<ParticleSystem>();var renderer=go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial=Particle;renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            return system;
        }
        static void Fade(ParticleSystem system,float peakAlpha,float peakAt=.2f)
        {
            var fade=system.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(peakAlpha,peakAt),new GradientAlphaKey(0,1)});fade.color=gradient;
        }
        // A vertical jet of steam. Emission follows `Enable`.
        public static ParticleSystem Steam(Transform parent,Vector3 local)
        {
            var system=Make("Steam",parent,local,new Vector3(-90,0,0));
            var main=system.main;main.startLifetime=new ParticleSystem.MinMaxCurve(1.1f,1.9f);main.startSpeed=new ParticleSystem.MinMaxCurve(.9f,1.7f);main.startSize=new ParticleSystem.MinMaxCurve(.3f,.6f);main.startColor=new Color(.82f,.88f,.86f,1);main.maxParticles=80;main.gravityModifier=-.04f;
            var emission=system.emission;emission.rateOverTime=22;var shape=system.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=11;shape.radius=.07f;
            var size=system.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.5f,1,2.2f));
            Fade(system,.5f);return system;
        }
        public static ParticleSystem Sparks(Transform parent,Vector3 local)
        {
            var system=Make("Sparks",parent,local,new Vector3(-90,0,0));
            var main=system.main;main.startLifetime=new ParticleSystem.MinMaxCurve(.2f,.5f);main.startSpeed=new ParticleSystem.MinMaxCurve(1.5f,4f);main.startSize=new ParticleSystem.MinMaxCurve(.05f,.12f);main.startColor=new Color(.55f,.95f,1,1);main.gravityModifier=.5f;main.maxParticles=60;
            var emission=system.emission;emission.rateOverTime=38;var shape=system.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=40;shape.radius=.12f;
            Fade(system,1f,.05f);return system;
        }
        // Slow motes that give a flashlight beam something to catch. Sits under the player camera but simulates in world space.
        public static ParticleSystem Dust(Transform parent)
        {
            var system=Make("Dust",parent,Vector3.zero,Vector3.zero);
            var main=system.main;main.startLifetime=new ParticleSystem.MinMaxCurve(6f,10f);main.startSpeed=new ParticleSystem.MinMaxCurve(.03f,.18f);main.startSize=new ParticleSystem.MinMaxCurve(.025f,.06f);main.startColor=new Color(.85f,.9f,.85f,1);main.maxParticles=140;main.simulationSpace=ParticleSystemSimulationSpace.World;main.prewarm=true;
            var emission=system.emission;emission.rateOverTime=16;var shape=system.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(14,5,14);
            Fade(system,.35f,.3f);return system;
        }
        public static Light Glow(Transform parent,Vector3 local,Color color,float range,float intensity)
        {
            var go=new GameObject("Light");go.transform.SetParent(parent,false);go.transform.localPosition=local;
            var light=go.AddComponent<Light>();light.type=LightType.Point;light.color=color;light.range=range;light.intensity=intensity;light.shadows=LightShadows.None;return light;
        }
    }

    // Lamp that wavers like a tired fluorescent tube and occasionally drops out for a moment. A hull stalker within 9 m makes
    // it stutter and die over and over: a warning you can see before you hear anything.
    public class LightFlicker : MonoBehaviour
    {
        public float depth=.18f,speed=1,dropout=.0015f;
        Light lamp;
        float baseIntensity,seed,offUntil,checkAt,dread;
        void Awake() {lamp=GetComponent<Light>();baseIntensity=lamp.intensity;seed=Random.value*100;}
        void Update()
        {
            if(lamp==null)return;
            if(Time.time>checkAt) {
                checkAt=Time.time+.25f;dread=0;
                foreach(var e in NetEnemy.All)if(e!=null&&!e.waterEnemy) {float d=Vector3.Distance(e.transform.position,transform.position);if(d<9)dread=Mathf.Max(dread,1-d/9);}
            }
            if(Time.time>offUntil&&Random.value<dropout+dread*.09f)offUntil=Time.time+Random.Range(.05f,.25f+dread*.35f);
            float n=Mathf.PerlinNoise(Time.time*speed*3f,seed);
            lamp.intensity=Time.time<offUntil?baseIntensity*.08f:baseIntensity*(1-depth*(1-n)*2);
        }
    }

    // Status LED on machinery: toggles its renderer on a fixed rhythm.
    public class Blinker : MonoBehaviour
    {
        public float period=1.2f,onFraction=.5f;
        Renderer led;
        float offset;
        void Awake() {led=GetComponent<Renderer>();offset=Random.value*period;}
        void Update() {if(led!=null)led.enabled=((Time.time+offset)%period)/period<onFraction;}
    }

    // Constant turn about the local up axis (radar scanners).
    public class Spin : MonoBehaviour
    {
        public float degreesPerSecond=60;
        void Update() {transform.Rotate(0,degreesPerSecond*Time.deltaTime,0,Space.Self);}
    }

    // Slow blink for beacons and navigation lights.
    public class LightBlink : MonoBehaviour
    {
        public float period=2,onFraction=.25f;
        Light lamp;
        Renderer bulb;
        float baseIntensity,offset;
        void Awake() {lamp=GetComponent<Light>();baseIntensity=lamp.intensity;offset=Random.value*period;bulb=transform.parent!=null?transform.parent.GetComponent<Renderer>():null;}
        void Update()
        {
            bool on=((Time.time+offset)%period)/period<onFraction;lamp.intensity=on?baseIntensity:0;if(bulb!=null)bulb.enabled=on;
        }
    }

    // Red warning lights inside the wreck: dim while all is calm, flashing in the last two minutes.
    public class EmergencyLight : MonoBehaviour
    {
        Light lamp;
        float baseIntensity;
        void Awake() {lamp=GetComponent<Light>();baseIntensity=lamp.intensity;}
        void Update()
        {
            var session=NetSession.Instance;bool alarm=session!=null&&session.InExpedition&&session.Remaining<120;
            lamp.intensity=alarm?baseIntensity*(.3f+.7f*Mathf.Max(0,Mathf.Sin(Time.time*5f))):baseIntensity*.12f;
        }
    }
}
