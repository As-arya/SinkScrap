using System.Linq;
using UnityEngine;

namespace SinkScrapt
{
    // Swings a teammate's legs with their real speed and tilts the helmet with their replicated pitch.
    public class AvatarAnimator : MonoBehaviour
    {
        public Transform[] legs;
        public Transform head;
        public NetPlayer player;
        Vector3 last;
        float speed,phase;
        void OnEnable() {last=player!=null?player.transform.position:Vector3.zero;}
        void Update()
        {
            if(player==null||legs==null)return;
            float dt=Time.deltaTime;if(dt<=0)return;
            var pos=player.transform.position;var delta=pos-last;last=pos;if(delta.sqrMagnitude>16)delta=Vector3.zero;
            speed=Mathf.Lerp(speed,new Vector3(delta.x,0,delta.z).magnitude/dt,Mathf.Clamp01(dt*10));
            phase+=speed*dt*1.5f;float swing=Mathf.Clamp(speed*9f,0,45)*(player.Crouched.Value?.6f:1);
            legs[0].localRotation=Quaternion.Euler(Mathf.Sin(phase)*swing,0,0);legs[1].localRotation=Quaternion.Euler(-Mathf.Sin(phase)*swing,0,0);
            if(head!=null)head.localRotation=Quaternion.Euler(player.Pitch.Value,0,0);
            transform.localScale=new Vector3(1,player.Crouched.Value?.75f:1,1);
        }
    }

    // Client-side audio for one diver (local or remote): footsteps from real movement on the right surface, water transitions,
    // breathing, heartbeat, damage and death. Everything is derived from replicated state, so teammates hear each other too.
    public class PlayerFeel : MonoBehaviour
    {
        NetPlayer player;
        Vector3 last;
        float stride,speed,breathAt,heartAt,strokeAt,ropeAt,lastHealth=100,airborne;
        bool wasHeadUnder,wasFeetWet,wasAlive=true,wasGrounded=true,seen;
        void Awake() {player=GetComponent<NetPlayer>();last=transform.position;}
        void Update()
        {
            if(player==null||!player.IsSpawned)return;
            float dt=Time.deltaTime;if(dt<=0)return;
            var pos=transform.position;var delta=pos-last;last=pos;
            bool teleported=delta.sqrMagnitude>16;if(teleported) {delta=Vector3.zero;stride=0;wasFeetWet=FeetWet(pos);wasHeadUnder=player.Underwater;}
            speed=Mathf.Lerp(speed,new Vector3(delta.x,0,delta.z).magnitude/dt,Mathf.Clamp01(dt*10));float vy=delta.y/dt;
            bool local=player.IsOwner,alive=player.Alive.Value;
            if(!seen) {wasAlive=alive;seen=true;}
            if(!alive&&wasAlive) Sfx.At(Snd.Death,pos+Vector3.up,.9f,1,30);
            wasAlive=alive;
            if(!alive) {lastHealth=100;return;}
            // Damage grunt.
            float health=player.Health.Value;
            if(health<lastHealth-.5f) {if(local)Sfx.Ui(Snd.Hurt,.8f);else Sfx.At(Snd.Hurt,pos+Vector3.up*1.4f,.8f,1,20);}
            lastHealth=health;
            bool headUnder=player.Underwater,feetWet=FeetWet(pos),swimming=feetWet&&(headUnder||player.Swimming);
            // Water transitions.
            if(feetWet&&!wasFeetWet&&!teleported) Sfx.At(Snd.SplashIn,pos,Mathf.Clamp01(.4f+Mathf.Abs(vy)*.1f),Random.Range(.9f,1.1f),30);
            if(wasHeadUnder&&!headUnder) {Sfx.At(Snd.SplashOut,pos,.6f,1,25);if(local)Sfx.Ui(Snd.Gasp,.7f);}
            wasFeetWet=feetWet;wasHeadUnder=headUnder;
            // Footsteps.
            bool grounded=local?player.controller!=null&&player.controller.isGrounded:Mathf.Abs(vy)<1.4f;
            if(!swimming&&!player.climbing&&grounded&&speed>.6f) {
                stride+=speed*dt;float length=speed>5.4f?2.2f:player.Crouched.Value?1.1f:1.75f;
                if(stride>=length) {stride-=length;Step(pos,local);}
            } else if(speed<.3f)stride=0;
            if(local&&player.controller!=null) {
                if(wasGrounded&&!player.controller.isGrounded&&vy>2) Sfx.Ui(Snd.Jump,.5f);
                if(!player.controller.isGrounded)airborne+=dt;
                else {if(!wasGrounded&&airborne>.35f)Sfx.At(Snd.Land,pos,Mathf.Clamp01(.3f+airborne*.5f),1,20);airborne=0;}
                wasGrounded=player.controller.isGrounded;
            }
            // Swimming strokes and rope.
            if(swimming&&speed>.8f&&Time.time>strokeAt) {strokeAt=Time.time+.95f;Sfx.At(Snd.Swim,pos,local?.5f:.6f,Random.Range(.9f,1.1f),18);}
            if(player.climbing&&Time.time>ropeAt) {ropeAt=Time.time+.7f;Sfx.At(Snd.Rope,pos,.6f,Random.Range(.9f,1.1f),15);}
            if(!local)return;
            // Breathing and heartbeat only matter to the person they happen to.
            float air=player.Oxygen.Value+player.Inventory.All.Where(i=>i.definitionId==14).Sum(i=>i.oxygen);
            if(headUnder&&Time.time>breathAt) {breathAt=Time.time+(air<10?1.6f:3.8f);Sfx.Ui(air<10?Snd.Gasp:Snd.BreathOut,air<10?.5f:.4f,Random.Range(.92f,1.05f));}
            float stress=Mathf.Max(health<35?1-health/35f:0,NetEnemy.ThreatTo(player),headUnder&&air<8?1-air/8f:0);
            if(stress>.15f&&Time.time>heartAt) {heartAt=Time.time+Mathf.Lerp(1.1f,.55f,stress);Sfx.Ui(Snd.Heartbeat,.35f+stress*.6f);}
        }
        bool FeetWet(Vector3 pos) => pos.y+.15f<player.WaterHeight;
        void Step(Vector3 pos,bool local)
        {
            Snd id=FeetWet(pos)?Snd.StepWet:Underfoot(pos);
            float volume=(speed>5.4f?1f:player.Crouched.Value?.35f:.65f)*(local?.6f:.85f);
            Sfx.At(id,pos,volume,Random.Range(.94f,1.06f),local?14:24);
        }
        static readonly System.Collections.Generic.Dictionary<Collider,Snd> floors=new();
        // The footstep for what the diver stands on, from the floor collider's procedural material ("Planks 8C7A5E"): wood on
        // the boat deck and the dock, iron plate on the wreck, ringing grating on stairs and in the engine rooms, concrete or
        // tile, gravel and sand on the island. Model-covered floors keep their (hidden) block with its material.
        Snd Underfoot(Vector3 pos)
        {
            var zone=player.Zone.Value;
            Snd fallback=zone==PlayerZone.Interior||zone==PlayerZone.Exterior?Snd.StepMetal:zone==PlayerZone.Boat?Snd.StepWood:Snd.StepGround;
            if(!Physics.Raycast(pos+Vector3.up*.4f,Vector3.down,out var hit,1.2f,~0,QueryTriggerInteraction.Ignore))return fallback;
            var key=hit.collider;if(floors.TryGetValue(key,out var known))return known;
            if(floors.Count>2048)floors.Clear();
            var renderer=hit.collider.GetComponent<Renderer>();string name=renderer!=null&&renderer.sharedMaterial!=null?renderer.sharedMaterial.name:"";
            int space=name.IndexOf(' ');
            Snd id=!System.Enum.TryParse<Surface>(space>0?name[..space]:name,out var surface)?fallback:surface switch {
                Surface.Grating=>Snd.StepGrate, Surface.Wood or Surface.Planks or Surface.Carpet=>Snd.StepWood, Surface.Concrete or Surface.Tile or Surface.Marble=>Snd.StepGround,
                Surface.Rock or Surface.Sand=>Snd.StepGravel, _=>Snd.StepMetal };
            floors[key]=id;return id;
        }
    }
}
