using System.Collections.Generic;
using UnityEngine;

namespace SinkScrapt
{
    public enum Bus { Sfx, Ambience, Music }

    // Static facade so gameplay code can play sounds without holding a reference.
    public static class Sfx
    {
        public static float Volume(Bus bus) => AudioDirector.BusVolume(bus);
        public static void Ui(Snd id,float volume=1,float pitch=1) {if(AudioDirector.Instance!=null)AudioDirector.Instance.Play2D(id,volume,pitch);}
        public static AudioSource At(Snd id,Vector3 position,float volume=1,float pitch=1,float range=30,int variant=-1) => AudioDirector.Instance!=null?AudioDirector.Instance.Play3D(id,position,volume,pitch,range,variant):null;
        // A dedicated looping 3D source parented to `owner`; the caller scales its volume with Volume(bus).
        public static AudioSource Loop(Transform owner,Snd id,float range=25)
        {
            var go=new GameObject("Loop "+id);go.transform.SetParent(owner,false);var s=go.AddComponent<AudioSource>();
            s.clip=SoundBank.Get(id);s.loop=true;s.spatialBlend=1;s.minDistance=2;s.maxDistance=range;s.rolloffMode=AudioRolloffMode.Logarithmic;s.volume=0;s.playOnAwake=false;s.Play();return s;
        }
    }

    // Central mixer: pooled 3D voices, smoothed ambience layers that follow where the player is, the time of day and how far
    // the wreck has sunk, listener effects (underwater muffling, interior reverb) and per-bus volume settings.
    // The soundscape is deliberately sparse: outside, the sea, wind, lapping water and gulls by day; inside the wreck, a quiet
    // room tone with creaks, groans, drips and distant clanks, and no music. A low tension drone (Music bus) rises only while
    // a monster bears down on you.
    public class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance;
        static readonly float[] bus={1,.8f,.6f};
        public static float BusVolume(Bus b) => bus[(int)b];
        public static void SetBus(Bus b,float value) {bus[(int)b]=Mathf.Clamp01(value);PlayerPrefs.SetFloat("SinkBus"+(int)b,bus[(int)b]);}
        class Layer {public Snd id;public AudioSource source;public Bus bus;public float baseVolume,gain,target;}
        readonly List<AudioSource> pool=new();
        readonly Dictionary<Snd,Layer> layers=new();
        int next;
        AudioListener listener;
        AudioLowPassFilter lowPass;
        AudioReverbFilter reverb;
        float findAt,dripAt,clankAt,creakAt,bellAt,gullAt,boatAt,slamAt,room=-10000;
        void Awake()
        {
            Instance=this;
            for(int i=0;i<3;i++)bus[i]=PlayerPrefs.GetFloat("SinkBus"+i,bus[i]);
            for(int i=0;i<32;i++) {var go=new GameObject("Voice "+i);go.transform.SetParent(transform,false);var s=go.AddComponent<AudioSource>();s.playOnAwake=false;pool.Add(s);}
            AddLayer(Snd.Sea,Bus.Ambience,.6f);AddLayer(Snd.Lap,Bus.Ambience,.55f);AddLayer(Snd.Wind,Bus.Ambience,.45f);AddLayer(Snd.Rumble,Bus.Ambience,.6f);
            AddLayer(Snd.RoomTone,Bus.Ambience,.55f);AddLayer(Snd.Engine,Bus.Ambience,.6f);
            AddLayer(Snd.Tension,Bus.Music,.75f);
            StartCoroutine(Prewarm());
        }
        void AddLayer(Snd id,Bus b,float baseVolume)
        {
            var go=new GameObject("Layer "+id);go.transform.SetParent(transform,false);var s=go.AddComponent<AudioSource>();
            s.loop=true;s.spatialBlend=0;s.volume=0;s.playOnAwake=false;
            layers[id]=new Layer {id=id,source=s,bus=b,baseVolume=baseVolume};
        }
        // Clips are synthesised on first use; this fills the cache a clip per frame while the menu is showing so nothing hitches mid-game.
        System.Collections.IEnumerator Prewarm()
        {
            yield return null;
            foreach(Snd id in System.Enum.GetValues(typeof(Snd))) {
                // The old synthesised music beds are no longer played.
                if(id is Snd.MusicBed or Snd.MusicPulse or Snd.MusicChase or Snd.Drone)continue;
                for(int v=0;v<SoundBank.Variants(id);v++) {SoundBank.Get(id,v);yield return null;}
            }
        }
        AudioSource Voice()
        {
            foreach(var s in pool)if(!s.isPlaying)return s;
            return pool[next++%pool.Count];
        }
        public AudioSource Play3D(Snd id,Vector3 position,float volume=1,float pitch=1,float range=30,int variant=-1)
        {
            var s=Voice();s.Stop();s.transform.position=position;s.clip=SoundBank.Get(id,variant<0?Random.Range(0,64):variant);s.volume=volume*bus[0];s.pitch=pitch;
            s.spatialBlend=1;s.minDistance=2;s.maxDistance=range;s.rolloffMode=AudioRolloffMode.Logarithmic;s.loop=false;s.Play();return s;
        }
        public AudioSource Play2D(Snd id,float volume=1,float pitch=1)
        {
            var s=Voice();s.Stop();s.transform.localPosition=Vector3.zero;s.clip=SoundBank.Get(id,Random.Range(0,64));s.volume=volume*bus[0];s.pitch=pitch;s.spatialBlend=0;s.loop=false;s.Play();return s;
        }
        // level: 1 = five minutes left, 2 = two minutes, 3 = thirty seconds.
        public void Alarm(int level=1) {Play2D(level>=3?Snd.Siren:Snd.Warn,level>=3?.7f:.6f);}
        public void Horn() {Play2D(Snd.Horn,.8f);}
        void SetLayer(Snd id,float target) {layers[id].target=Mathf.Clamp01(target);}
        void Update()
        {
            float dt=Time.unscaledDeltaTime;var session=NetSession.Instance;var player=NetPlayer.Local;
            bool inGame=SinkApp.Instance!=null&&SinkApp.Instance.Connected&&session!=null&&player!=null&&player.IsSpawned;
            var zone=inGame?player.Zone.Value:PlayerZone.Boat;bool under=inGame&&player.Underwater;
            bool expedition=inGame&&session.InExpedition;bool interior=inGame&&zone==PlayerZone.Interior;
            float progress=expedition?Mathf.Clamp01((float)session.Elapsed/Mathf.Max(1,session.Duration)):0;
            float threat=inGame?NetEnemy.ThreatTo(player):0;
            bool loading=inGame&&session.Phase.Value==GamePhase.Loading;float day=SkyClock.Daylight;
            // Ambience targets. The menu shows the sea, so it hears the sea (and a faint drone under it).
            SetLayer(Snd.Sea,!inGame?.55f:interior?.04f:loading?.45f:zone==PlayerZone.Island?.45f:.65f);
            SetLayer(Snd.Lap,!inGame?.25f:zone==PlayerZone.Boat?.6f:zone==PlayerZone.Exterior?.25f:zone==PlayerZone.Island?.25f:0);
            SetLayer(Snd.Wind,!inGame?.3f:interior?0:zone==PlayerZone.Exterior?.55f:.3f+(1-day)*.15f);
            SetLayer(Snd.Rumble,expedition?Mathf.Lerp(.03f,.55f,progress*progress)*(interior?1.1f:.7f):0);
            SetLayer(Snd.RoomTone,interior?.65f:0);
            SetLayer(Snd.Engine,loading?.9f:0);
            SetLayer(Snd.Tension,!inGame?.18f:Mathf.Clamp01(threat*1.5f-.2f));
            foreach(var l in layers.Values) {
                if(l.source.clip==null) {if(l.target<=0)continue;l.source.clip=SoundBank.Get(l.id);}
                l.gain=Mathf.MoveTowards(l.gain,l.target,dt*(l.target>l.gain?.5f:.8f));
                l.source.volume=l.gain*l.baseVolume*bus[(int)l.bus];
                if(l.gain<=.001f&&l.target<=0) {if(l.source.isPlaying)l.source.Pause();}
                else if(!l.source.isPlaying)l.source.UnPause();
                if(!l.source.isPlaying&&l.gain>.001f)l.source.Play();
            }
            // Listener effects: muffled underwater, metallic reverb inside the wreck.
            if((listener==null||!listener.isActiveAndEnabled)&&Time.unscaledTime>findAt) {
                findAt=Time.unscaledTime+.5f;listener=FindAnyObjectByType<AudioListener>();
                if(listener!=null) {
                    if(!listener.TryGetComponent(out lowPass))lowPass=listener.gameObject.AddComponent<AudioLowPassFilter>();
                    if(!listener.TryGetComponent(out reverb))reverb=listener.gameObject.AddComponent<AudioReverbFilter>();
                    reverb.reverbPreset=AudioReverbPreset.User;reverb.dryLevel=0;reverb.decayTime=1.7f;reverb.diffusion=70;reverb.density=80;reverb.reverbDelay=.03f;reverb.reflectionsLevel=-1500;reverb.reverbLevel=0;}
            }
            if(lowPass!=null) lowPass.cutoffFrequency=Mathf.Lerp(lowPass.cutoffFrequency,under?650:22000,dt*8);
            if(reverb!=null) {room=Mathf.Lerp(room,interior?-2100:inGame&&zone==PlayerZone.Exterior?-4800:-6500,dt*2);reverb.room=room;}
            if(!inGame) return;
            // Random one-shots that sell the space. Inside: long silences broken by the hull working, water dripping, metal
            // clanking somewhere and, rarely, a door slamming far off.
            var at=player.transform.position;float now=Time.time;
            if(interior&&now>dripAt) {dripAt=now+Random.Range(3f,9f);Play3D(Snd.Drip,at+RandomRing(4,14),.45f,Random.Range(.85f,1.15f),22);}
            if(interior&&now>clankAt) {clankAt=now+Random.Range(14f,32f);Play3D(Snd.Thunk,at+RandomRing(12,26),.45f,Random.Range(.8f,1.05f),50);}
            if(interior&&now>slamAt) {
                bool first=slamAt==0;slamAt=now+Random.Range(45f,95f);
                if(!first)Play3D(Random.value<.5f?Snd.DoorClose:Snd.Groan,at+RandomRing(16,30),.35f,Random.Range(.75f,.9f),60);
            }
            if(expedition&&(interior||zone==PlayerZone.Exterior)&&now>creakAt) {
                creakAt=now+Mathf.Lerp(16,6,progress)*Random.Range(.7f,1.3f);
                bool groan=progress>.4f&&Random.value<.35f;Play3D(groan?Snd.Groan:Snd.Creak,at+RandomRing(6,20),Mathf.Lerp(.4f,.9f,progress),Random.Range(.9f,1.1f),60);
            }
            // Outside by day: gulls wheeling overhead (more over the island), none at night.
            if(!interior&&day>.35f&&now>gullAt) {
                gullAt=now+Random.Range(3f,11f)/(day*(zone==PlayerZone.Island?1.6f:1f));
                var ring=RandomRing(15,45);Play3D(Snd.Gull,at+new Vector3(ring.x,Random.Range(10f,28f),ring.z),.55f*day,Random.Range(.92f,1.08f),140);
            }
            // The small boat's timbers working in the swell.
            if(zone==PlayerZone.Boat&&now>boatAt) {boatAt=now+Random.Range(5f,13f);Play3D(Snd.BoatCreak,at+RandomRing(2,6),.4f,Random.Range(.9f,1.08f),20);}
            if(!expedition&&(zone==PlayerZone.Island||zone==PlayerZone.Boat)&&now>bellAt) {bellAt=now+Random.Range(20f,40f);Play3D(Snd.Bell,at+new Vector3(60,2,25),.4f,Random.Range(.9f,1.05f),160);}
        }
        static Vector3 RandomRing(float min,float max) {var d=Random.insideUnitCircle.normalized*Random.Range(min,max);return new Vector3(d.x,Random.Range(-.5f,2f),d.y);}
        void OnDestroy() {if(Instance==this)Instance=null;}
    }
}

