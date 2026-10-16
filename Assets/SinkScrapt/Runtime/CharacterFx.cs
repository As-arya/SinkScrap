using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SinkScrapt
{
    // Legacy clips of one Blender character (Models/Characters, Docs/BlenderAssets.md). Each FBX take is an NLA strip named after
    // its clip ("walk"); some importers prefix the take with the rig ("rig|walk"), so clips are looked up by their short name.
    public sealed class ClipSet
    {
        public Animation animation;
        readonly Dictionary<string,string> names=new();
        string current="";
        public static ClipSet Find(GameObject root)
        {
            var animation=root.GetComponentInChildren<Animation>();if(animation==null)return null;
            var set=new ClipSet{animation=animation};animation.cullingType=AnimationCullingType.AlwaysAnimate;
            foreach(AnimationState state in animation) {var n=state.name;int bar=n.LastIndexOf('|');set.names[(bar>=0?n[(bar+1)..]:n).ToLowerInvariant()]=n;}
            return set.names.Count>0?set:null;
        }
        public bool Has(string clip) => names.ContainsKey(clip);
        public AnimationState State(string clip) => names.TryGetValue(clip,out var n)?animation[n]:null;
        // Cross-fades into a looping clip (no restart when it is already playing).
        public void Loop(string clip,float speed=1,float fade=.2f)
        {
            if(!names.TryGetValue(clip,out var n))return;var state=animation[n];state.wrapMode=WrapMode.Loop;state.speed=speed;
            if(current!=clip) {animation.CrossFade(n,fade);current=clip;}
        }
        // Restarts a one-shot clip from its first frame; it holds the last frame until something else plays.
        public void Once(string clip,float speed=1,float fade=.1f)
        {
            if(!names.TryGetValue(clip,out var n))return;var state=animation[n];state.wrapMode=WrapMode.ClampForever;state.speed=speed;state.time=0;
            animation.CrossFade(n,fade);current=clip;
        }
        public string Current=>current;
    }

    // Third-person animation for a crew member, from replicated state only: crouch, swim, climb, run, walk or idle at the real
    // pace, and the head tilted with the player's look pitch.
    public class CrewAnimator : MonoBehaviour
    {
        public NetPlayer player;
        ClipSet clips;
        Transform head;
        Vector3 last;
        float speed;
        public string Clip=>clips?.Current??"";
        void Start() {clips=ClipSet.Find(gameObject);head=ModelLibrary.FindBone(transform,"Head");last=player!=null?player.transform.position:transform.position;}
        void Update()
        {
            if(player==null||clips==null)return;float dt=Time.deltaTime;if(dt<=0)return;
            var pos=player.transform.position;var delta=pos-last;last=pos;if(delta.sqrMagnitude>16)delta=Vector3.zero;
            speed=Mathf.Lerp(speed,new Vector3(delta.x,0,delta.z).magnitude/dt,Mathf.Clamp01(dt*10));
            byte motion=player.Motion.Value;
            if(motion==1||motion==3)clips.Loop("swim",motion==3?1.8f:Mathf.Clamp(.6f+speed*.3f,.6f,1.5f));
            else if(motion==2)clips.Loop("climb",Mathf.Clamp(Mathf.Abs(delta.y)/dt*.8f,.15f,1.4f));
            else if(player.Crouched.Value)clips.Loop("crouch",Mathf.Clamp(speed/1.8f,.05f,1.4f));
            else if(speed>5.2f)clips.Loop("run",Mathf.Clamp(speed/6.5f,.8f,1.4f));
            else if(speed>.4f)clips.Loop("walk",Mathf.Clamp(speed/1.8f,.5f,1.6f));
            else clips.Loop("idle");
        }
        void LateUpdate()
        {
            if(head==null||player==null)return;
            head.rotation=Quaternion.AngleAxis(Mathf.Clamp(player.Pitch.Value,-60,60)*.6f,player.transform.right)*head.rotation;
        }
    }

    // The island's scrap buyer: a diver in a brass helmet behind the counter. Turns toward whoever comes close and waves.
    public class MerchantNpc : MonoBehaviour
    {
        ClipSet clips;
        float baseYaw,yaw,waveUntil,nextWave;
        bool near;
        void Start() {clips=ClipSet.Find(gameObject);baseYaw=yaw=transform.eulerAngles.y;clips?.Loop("idle");}
        void Update()
        {
            NetPlayer closest=null;float best=7*7;
            foreach(var p in NetPlayer.All) {if(p==null||!p.Alive.Value)continue;float d=(p.transform.position-transform.position).sqrMagnitude;if(d<best) {best=d;closest=p;}}
            float goal=baseYaw;
            if(closest!=null) {var to=closest.transform.position-transform.position;goal=baseYaw+Mathf.Clamp(Mathf.DeltaAngle(baseYaw,Mathf.Atan2(to.x,to.z)*Mathf.Rad2Deg),-70,70);}
            yaw=Mathf.LerpAngle(yaw,goal,1-Mathf.Exp(-3*Time.deltaTime));transform.rotation=Quaternion.Euler(0,yaw,0);
            bool isNear=closest!=null;
            if(isNear&&!near&&Time.time>nextWave&&clips!=null&&clips.Has("wave")) {clips.Once("wave");waveUntil=Time.time+2.4f;nextWave=Time.time+12;}
            near=isNear;
            if(clips!=null&&Time.time>waveUntil)clips.Loop("idle");
        }
    }

    // Arrival cutscene at the wreck or the island: two camera shots with letterbox bars and a title, then a short blend into the
    // player's own view. Driven by NetSession.Arrival/ArrivalAt so every client plays the same shots; the wreck's expedition
    // clock starts when it ends. Space skips it locally.
    public class Cinematic : MonoBehaviour
    {
        public static bool Playing;
        static Cinematic instance;
        Camera cam;
        RectTransform top,bottom;
        TMP_Text title,subtitle;
        CanvasGroup group;
        double startedAt=-1,handled=-1;
        byte destination;
        float length;
        public static void Ensure(Transform host) {if(instance==null)instance=host.gameObject.AddComponent<Cinematic>();}
        void Awake()
        {
            var canvasGo=new GameObject("Cinematic UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));canvasGo.transform.SetParent(transform,false);
            var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=50;
            var scaler=canvasGo.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            group=canvasGo.AddComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;
            var root=(RectTransform)canvasGo.transform;
            top=Bar(root,1);bottom=Bar(root,0);
            title=Ui.Label(root,"",new Vector2(110,150),new Vector2(1400,80),56,Theme.Paper,TextAlignmentOptions.BottomLeft,Ui.BottomLeft,Ui.BottomLeft);
            subtitle=Ui.Label(root,"",new Vector2(114,112),new Vector2(1400,40),24,Theme.Amber,TextAlignmentOptions.BottomLeft,Ui.BottomLeft,Ui.BottomLeft);
            canvasGo.SetActive(false);
            var camGo=new GameObject("Cinematic Camera");camGo.transform.SetParent(transform,false);cam=camGo.AddComponent<Camera>();
            cam.depth=20;cam.fieldOfView=48;cam.nearClipPlane=.1f;cam.farClipPlane=700;cam.clearFlags=CameraClearFlags.Skybox;cam.enabled=false;
        }
        static RectTransform Bar(RectTransform root,float y)
        {
            var r=Ui.Rect(y>0?"Letterbox top":"Letterbox bottom",root,new Vector2(.5f,y),new Vector2(.5f,y),Vector2.zero,new Vector2(4000,0));Ui.Fill(r,Color.black);return r;
        }
        void Update()
        {
            var s=NetSession.Instance;var app=SinkApp.Instance;
            if(s==null||!s.IsSpawned||app==null||!app.Connected) {Stop();return;}
            length=Mathf.Max(1,app.balance.arrivalCutscene);
            double at=s.ArrivalAt.Value;
            if(s.Arrival.Value!=0&&at!=handled) {
                handled=at;
                // Late joiners and reconnects skip a cutscene that is already over.
                if(app.TimeNow-at<length-.5f&&!Application.isBatchMode) {startedAt=at;destination=s.Arrival.Value;Begin(s);}
            }
            if(startedAt<0)return;
            float t=(float)(app.TimeNow-startedAt);
            if(t>=length||(Keyboard.current!=null&&Keyboard.current.spaceKey.wasPressedThisFrame&&t>.5f)) {Stop();return;}
            Shoot(t);
        }
        void Begin(NetSession s)
        {
            Playing=true;cam.enabled=true;group.transform.gameObject.SetActive(true);
            bool wreck=destination==1;var c=s.Campaign;
            title.text=wreck?"KAPAL KARGO S-04":"PULAU DERMAGA 04";
            subtitle.text=wreck?"EKSPEDISI "+Mathf.Clamp(c.expeditionsUsed+1,1,3)+" / 3   •   "+TimeSpan(s.Hour)+"   •   [SPASI] lewati":"JUAL SCRAP   •   SELESAIKAN KUOTA   •   [SPASI] lewati";
        }
        static string TimeSpan(float hour) {int h=Mathf.FloorToInt(hour)%24,m=Mathf.FloorToInt((hour-Mathf.Floor(hour))*60);return h.ToString("00")+":"+m.ToString("00");}
        void Stop()
        {
            if(startedAt<0&&!Playing)return;
            startedAt=-1;Playing=false;if(cam!=null)cam.enabled=false;if(group!=null)group.transform.gameObject.SetActive(false);
        }
        // Two shots: a wide establishing view from the sea, then a low view past the boat's stern toward the destination.
        void Shoot(float t)
        {
            bool wreck=destination==1;float depth=NetSession.Instance!=null?NetSession.Instance.Depth:0;
            float cut=length*.48f;Vector3 from,to,lookA,lookB;float u;
            if(t<cut) {
                u=t/cut;
                if(wreck) {from=new Vector3(66,7,-66);to=new Vector3(50,8,-48);lookA=new Vector3(14,8-depth,-4);lookB=new Vector3(10,9-depth,0);}
                else {from=new Vector3(-6,6,30);to=new Vector3(8,7,20);lookA=new Vector3(46,5,-6);lookB=new Vector3(48,5,-10);}
            } else {
                u=(t-cut)/(length-cut);
                if(wreck) {from=new Vector3(27.5f,3.4f,-31);to=new Vector3(25.8f,2.8f,-22);lookA=new Vector3(6,12-depth,6);lookB=new Vector3(4,11-depth,10);}
                else {from=new Vector3(24,3.2f,-27);to=new Vector3(27,3.4f,-19);lookA=new Vector3(46,3,-10);lookB=new Vector3(45,2.6f,-10);}
            }
            float e=Mathf.SmoothStep(0,1,u);
            cam.transform.position=Vector3.Lerp(from,to,e);cam.transform.rotation=Quaternion.LookRotation(Vector3.Lerp(lookA,lookB,e)-cam.transform.position);
            // The last second eases into the player's own eyes so the hand-over is seamless.
            var player=NetPlayer.Local;float blend=Mathf.Clamp01((t-(length-1f))/1f);
            if(player!=null&&player.eyes!=null&&blend>0) {
                var eye=player.eyes.transform;float b=Mathf.SmoothStep(0,1,blend);
                cam.transform.position=Vector3.Lerp(cam.transform.position,eye.position,b);cam.transform.rotation=Quaternion.Slerp(cam.transform.rotation,eye.rotation,b);
                cam.fieldOfView=Mathf.Lerp(48,player.eyes.fieldOfView,b);
            } else cam.fieldOfView=48;
            // Letterbox bars slide in and out; the title fades in after the first beat.
            float bars=Mathf.Clamp01(Mathf.Min(t/.5f,(length-t)/.6f));top.sizeDelta=bottom.sizeDelta=new Vector2(4000,130*bars);
            float text=Mathf.Clamp01(Mathf.Min((t-.8f)/.6f,(cut+1.6f-t)/.6f));title.alpha=subtitle.alpha=text;
        }
    }
}
