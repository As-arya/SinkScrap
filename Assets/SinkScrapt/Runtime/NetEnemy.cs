using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
namespace SinkScrapt
{
    // Grab: the hull stalker's second attack. It lunges (0.45 s); a diver still in reach is lifted and shaken, then thrown.
    // Scream: on first sight of a diver the monster stops, faces them and screams (balance.screamSeconds), then chases.
    public enum EnemyState : byte { Patrol, Investigate, Chase, WindUp, Attack, Recover, Search, Distracted, Retreat, Grab, Scream }

    // Server-side brain for both monsters. Clients only read State/Target to animate and play audio (EnemyRig).
    // Monsters move over the same connected node graph the generator validates, so they never cross walls; flooding decides where each may go.
    public class NetEnemy : NetworkBehaviour
    {
        public static readonly List<NetEnemy> All=new();
        // 0..1: how hard a monster is bearing down on this player; drives heartbeat, vignette and chase music.
        public static float ThreatTo(NetPlayer player)
        {
            if(player==null||!player.Alive.Value)return 0;float threat=0;
            foreach(var e in All) {
                if(e==null||!e.IsSpawned||player.Zone.Value!=PlayerZone.Interior)continue;
                float distance=Vector3.Distance(e.transform.position,player.transform.position);var s=e.CurrentState;
                float hunt=s==EnemyState.Chase||s==EnemyState.WindUp||s==EnemyState.Attack||s==EnemyState.Grab||s==EnemyState.Scream?1f:s==EnemyState.Search||s==EnemyState.Investigate?.35f:s==EnemyState.Recover?.5f:0;
                if(hunt>0&&e.Target.Value!=player.OwnerClientId)hunt*=.4f;
                threat=Mathf.Max(threat,hunt*Mathf.Clamp01(1-distance/24f));
                if(s==EnemyState.Patrol||s==EnemyState.Retreat)threat=Mathf.Max(threat,.3f*Mathf.Clamp01(1-distance/9f));
            }
            return threat;
        }
        public readonly NetworkVariable<bool> IsWater=new();
        public readonly NetworkVariable<byte> State=new((byte)EnemyState.Patrol);
        public readonly NetworkVariable<ulong> Target=new(ulong.MaxValue);
        public bool waterEnemy=>IsWater.Value;
        public EnemyState CurrentState=>(EnemyState)State.Value;
        List<int> path=new();
        Vector3 heard,lastSeen,goal;
        float repathAt,attackReadyAt,phaseEnd,searchEnd,idleUntil,heardUntil,lostAt,attackLungeEnd,screamReadyAt;
        // Server time of the last scream (QA reads it).
        public float LastScream {get;private set;}=-99;
        bool hasGoal;
        NetPlayer victim;
        // Grab attack: the diver currently held (server), and the time the throw comes.
        NetPlayer held;
        public const float GrabLunge=.45f,GrabHold=1.47f;
        // QA only: forces (1) or forbids (0) the grab without touching the Balance asset.
        public static float? GrabChanceOverride;
        public override void OnNetworkSpawn() {All.Add(this);gameObject.AddComponent<EnemyRig>();}
        public void Hear(Vector3 position,float range)
        {
            if(!IsServer||!IsSpawned||(position-transform.position).sqrMagnitude>range*range)return;
            var session=NetSession.Instance;if(session==null||(waterEnemy&&position.y>=session.FloodLevel-.2f))return;
            var s=CurrentState;if(s==EnemyState.Chase||s==EnemyState.WindUp||s==EnemyState.Attack||s==EnemyState.Recover||s==EnemyState.Grab||s==EnemyState.Distracted||s==EnemyState.Scream)return;
            heard=position;heardUntil=Time.time+7;repathAt=0;
        }
        void SetState(EnemyState next) {if(State.Value!=(byte)next)State.Value=(byte)next;}
        Vector3 NodeWorld(int node)=>WorldZone.Interior.transform.TransformPoint(WorldZone.Interior.nodes[node])+(waterEnemy?Vector3.up*1.1f:Vector3.zero);
        void Face(Vector3 direction,float rate=10)
        {
            direction.y=0;if(direction.sqrMagnitude>.0004f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(direction),Mathf.Clamp01(Time.deltaTime*rate));
        }
        NetPlayer PickTarget(float depth)
        {
            NetPlayer best=null;float distance=float.MaxValue;
            foreach(var p in NetPlayer.All) {
                if(p==null||!p.IsSpawned||!p.Alive.Value||p.Zone.Value!=PlayerZone.Interior)continue;
                if(waterEnemy?p.transform.position.y+.9f>=depth:p.Underwater)continue;
                float d=(p.transform.position-transform.position).sqrMagnitude;if(d<distance) {distance=d;best=p;}
            }
            return best;
        }
        bool CanSee(NetPlayer p,BalanceConfig balance)
        {
            var eye=transform.position+Vector3.up*(waterEnemy?1.1f:1.8f);var chest=p.transform.position+Vector3.up*1.2f;var to=chest-eye;float distance=to.magnitude;
            float range=waterEnemy?balance.drownedSight:balance.stalkerSight;
            if(p.Inventory.lightOn)range*=1.25f;if(p.Crouched.Value)range*=.6f;
            if(distance>range)return false;
            if(!waterEnemy&&distance>3&&Vector3.Angle(transform.forward,new Vector3(to.x,0,to.z))>70)return false;
            if(Physics.Linecast(eye,chest,out var hit,~0,QueryTriggerInteraction.Ignore))return hit.collider.GetComponentInParent<NetPlayer>()==p;
            return true;
        }
        bool NearestFlare(out Vector3 position)
        {
            position=default;float best=225;var session=NetSession.Instance;
            foreach(var f in session.flares) {
                float d=(f.position-transform.position).sqrMagnitude;
                if(d<best&&Mathf.Abs(f.position.y-transform.position.y)<4.5f) {best=d;position=f.position;}
            }
            return best<225;
        }
        void Repath(Vector3 destination,bool anyDepth,float depth)
        {
            int goal=WorldZone.Interior.NearestNode(destination,waterEnemy,anyDepth?0:depth);
            path=goal<0?new List<int>():Route(goal,anyDepth,depth);
        }
        // Graph route to `goal`. Graph links only run through open space, so the one leg that can cut through a wall is the
        // first one, from wherever the monster stands to its starting node: only start nodes it can reach in a straight line with
        // its whole body are considered. Among those the cheapest total route wins (the single nearest node can lie behind the
        // monster and flip back and forth as it walks).
        List<int> Route(int goal,bool anyDepth,float depth)
        {
            var world=WorldZone.Interior;float d=anyDepth?0:depth;var origin=transform.position;
            List<int> bestRoute=null;
            foreach(float slack in new[]{4f,10f}) {
                float best=float.MaxValue;
                foreach(int start in world.NodesNear(origin,waterEnemy,d,slack)) {
                    if(Blocked(origin,NodeWorld(start),BodyRadius))continue;
                    var route=new List<int>{start};route.AddRange(world.FindPath(start,goal,waterEnemy,d));
                    if(route[^1]!=goal)continue;
                    float cost=Vector3.Distance(origin,NodeWorld(route[0]));for(int i=1;i<route.Count;i++)cost+=Vector3.Distance(NodeWorld(route[i-1]),NodeWorld(route[i]));
                    if(cost<best) {best=cost;bestRoute=route;}
                }
                if(bestRoute!=null)break;
            }
            // Wedged against a wall (every sweep starts inside it): fall back to the nearest node a thin line can still reach.
            if(bestRoute==null) {
                int start=-1;float nearest=float.MaxValue;
                foreach(int n in world.NodesNear(origin,waterEnemy,d,10f)) {float dist=(NodeWorld(n)-origin).sqrMagnitude;if(dist<nearest&&!Blocked(origin,NodeWorld(n),.05f)) {nearest=dist;start=n;}}
                if(start<0)return new List<int>();
                bestRoute=new List<int>{start};bestRoute.AddRange(world.FindPath(start,goal,waterEnemy,d));
            }
            // Already standing on the first leg: skip the node behind us and walk straight on.
            if(bestRoute.Count>1) {
                var a=NodeWorld(bestRoute[0]);var ab=NodeWorld(bestRoute[1])-a;float t=Vector3.Dot(origin-a,ab)/Mathf.Max(ab.sqrMagnitude,.0001f);
                if(t>0&&t<1&&(a+ab*t-origin).magnitude<1.6f&&!Blocked(origin,NodeWorld(bestRoute[1]),BodyRadius))bestRoute.RemoveAt(0);
            }
            return bestRoute;
        }
        // Half the width of the stalker's shoulders; the drowned hunter's body is about as wide.
        const float BodyRadius=.42f;
        static readonly RaycastHit[] sweepHits=new RaycastHit[24];
        // True when level geometry lies between two points at body height. Players, loose loot, other monsters and triggers do
        // not block (they are not walls); a sweep that starts inside geometry counts as blocked.
        bool Blocked(Vector3 from,Vector3 to,float radius)
        {
            var delta=to-from;float distance=delta.magnitude;if(distance<.01f)return false;var direction=delta/distance;
            foreach(float height in waterEnemy?new[]{0f}:new[]{.6f,1.5f}) {
                int count=Physics.SphereCastNonAlloc(from+Vector3.up*height,radius,direction,sweepHits,distance,~0,QueryTriggerInteraction.Ignore);
                for(int i=0;i<count;i++) {var c=sweepHits[i].collider;if(c!=null&&c.attachedRigidbody==null&&c is not CharacterController&&c.GetComponentInParent<NetEnemy>()==null&&!Openable(c))return true;}
            }
            return false;
        }
        // A closed door the monster can simply push open. Locked doors are walls to it, and so is a leaf that already stands open.
        static bool Openable(Collider c) {var door=c.GetComponentInParent<InteriorDoor>();return door!=null&&!door.IsLocked&&!door.IsOpen;}
        // Monsters shove open any unlocked door they reach; the bang carries.
        void OpenDoorsAhead()
        {
            var world=WorldZone.Interior;var session=NetSession.Instance;if(world==null||world.layout==null||session==null)return;
            foreach(var door in world.doors) {
                if(door==null||door.IsOpen||door.IsLocked)continue;
                var at=world.transform.TransformPoint(world.layout.DoorCenter(door.index));
                if(Mathf.Abs(at.y-transform.position.y)>=2.2f||(new Vector2(at.x-transform.position.x,at.z-transform.position.z)).sqrMagnitude>=3.6f)continue;
                // Not while standing where an open leaf will come to rest (it would close around the body).
                bool inTheWay=false;
                for(int k=0;k<door.hinges.Length;k++) {
                    var hinge=door.hinges[k].position;var leaf=door.transform.rotation*Quaternion.Euler(0,door.closedYaw[k]+door.openAngles[k],0)*Vector3.right;
                    var to=transform.position-hinge;to.y=0;float along=Mathf.Clamp(Vector3.Dot(to,leaf),0,door.leafWidth+InteriorDoor.LeafGap);
                    if((to-leaf*along).magnitude<.3f)inTheWay=true;
                }
                if(!inTheWay)session.SetDoor(door.index,true);
            }
        }
        void Follow(float speed,float dt)
        {
            if(path.Count==0)return;
            var destination=NodeWorld(path[0]);var next=Vector3.MoveTowards(transform.position,destination,speed*dt);
            Face(next-transform.position);transform.position=next;if((destination-next).sqrMagnitude<.09f)path.RemoveAt(0);
        }
        // Straight step toward `to` (chasing a visible player, the attack lunge). Refused when the body would touch a wall.
        bool Direct(Vector3 to,float speed,float dt)
        {
            var delta=to-transform.position;if(!waterEnemy)delta.y=0;
            var step=Vector3.ClampMagnitude(delta,speed*dt);if(step.sqrMagnitude<1e-6f)return true;
            if(Blocked(transform.position,transform.position+step+step.normalized*.15f,BodyRadius))return false;
            Face(step);transform.position+=step;return true;
        }
        // Moves along the validated route toward `destination`, repathing a few times a second.
        void Travel(Vector3 destination,float speed,float dt,bool anyDepth,float depth,bool allowDirect)
        {
            float now=Time.time;
            if(allowDirect&&(destination-transform.position).magnitude<7&&Mathf.Abs(destination.y-transform.position.y)<(waterEnemy?3f:1.3f)&&Direct(destination,speed,dt)) {path.Clear();return;}
            if(now>=repathAt||path.Count==0) {repathAt=now+.35f;Repath(destination,anyDepth,depth);}
            // Both ends can snap to the same graph node; then close the last stretch directly rather than standing still.
            if(path.Count==0) {if(allowDirect)Direct(destination,speed,dt);return;}
            Follow(speed,dt);
        }
        void Update()
        {
            if(!IsSpawned||!IsServer)return;
            var session=NetSession.Instance;var world=WorldZone.Interior;
            if(session==null||!session.InExpedition||world==null||world.nodes.Count==0)return;
            var balance=SinkApp.Instance.balance;float dt=Mathf.Min(Time.deltaTime,.05f),now=Time.time,depth=session.FloodLevel;
            OpenDoorsAhead();
            float patrol=waterEnemy?balance.drownedPatrolSpeed:balance.stalkerPatrolSpeed,chase=waterEnemy?balance.drownedChaseSpeed:balance.stalkerChaseSpeed;
            var state=CurrentState;
            // Committed attack phases run to completion before anything else is reconsidered.
            if(state==EnemyState.WindUp||state==EnemyState.Attack||state==EnemyState.Recover||state==EnemyState.Grab) {RunAttack(state,balance,now,dt);return;}
            // The scream: rooted, turning to the diver it saw, then straight into the chase.
            if(state==EnemyState.Scream) {
                if(victim!=null)Face(victim.transform.position-transform.position,8);
                if(now>=phaseEnd) {lostAt=now;SetState(EnemyState.Chase);}
                return;
            }
            if(!waterEnemy&&transform.position.y+1.65f<depth) {SetState(EnemyState.Retreat);RetreatStep(chase,dt,depth);return;}
            victim=PickTarget(depth);bool sees=victim!=null&&CanSee(victim,balance);
            if(sees) {lastSeen=victim.transform.position;lostAt=now;Target.Value=victim.OwnerClientId;}
            if(NearestFlare(out var flare)) {
                SetState(EnemyState.Distracted);
                if((flare-transform.position).sqrMagnitude>6)Travel(flare,patrol*1.3f,dt,false,depth,false);else Face(flare-transform.position,4);
                lastSeen=flare;searchEnd=now+balance.searchSeconds*.6f;return;
            }
            if(sees) {
                // First sight (not already chasing): stop and scream before the chase, at most once per screamCooldown.
                if(state!=EnemyState.Chase&&now>=screamReadyAt) {
                    phaseEnd=now+balance.screamSeconds;screamReadyAt=now+balance.screamCooldown;LastScream=now;path.Clear();SetState(EnemyState.Scream);return;
                }
                SetState(EnemyState.Chase);
                float distance=(victim.transform.position-transform.position).magnitude;
                if(distance<=balance.attackReach+.3f&&now>=attackReadyAt) {
                    path.Clear();
                    // The stalker sometimes grabs instead of striking (never a diver another monster is already holding).
                    bool grab=!waterEnemy&&Random.value<(GrabChanceOverride??balance.grabChance)&&!All.Any(e=>e!=this&&e.held==victim);
                    if(grab) {phaseEnd=now+GrabLunge;held=null;SetState(EnemyState.Grab);}
                    else {phaseEnd=now+balance.attackWindUp;SetState(EnemyState.WindUp);}
                    return;
                }
                if(distance<=balance.attackReach+.3f) {Face(victim.transform.position-transform.position,12);return;}
                Travel(victim.transform.position,chase,dt,false,depth,true);return;
            }
            if(state==EnemyState.Chase&&now-lostAt<3.2f) {Travel(lastSeen,chase,dt,false,depth,true);return;}
            if(state==EnemyState.Chase) {SetState(EnemyState.Search);searchEnd=now+balance.searchSeconds;return;}
            if(heardUntil>now) {
                SetState(EnemyState.Investigate);Travel(heard,patrol*1.5f,dt,false,depth,false);
                if((heard-transform.position).sqrMagnitude<5) {heardUntil=0;lastSeen=heard;SetState(EnemyState.Search);searchEnd=now+balance.searchSeconds*.6f;}
                return;
            }
            if(state==EnemyState.Search||state==EnemyState.Distracted&&now<searchEnd) {
                if(state==EnemyState.Distracted)SetState(EnemyState.Search);
                if(now<searchEnd) {if(path.Count==0)WanderNear(lastSeen,depth);Follow(patrol*1.2f,dt);return;}
            }
            SetState(EnemyState.Patrol);
            if(path.Count==0) {
                if(now<idleUntil)return;
                PickPatrolGoal(depth);if(path.Count==0)return;
            }
            Follow(patrol,dt);
            if(path.Count==0)idleUntil=now+Random.Range(1.2f,3.2f);
        }
        void RetreatStep(float speed,float dt,float depth)
        {
            // Climb to the nearest node that is still above the waterline.
            var world=WorldZone.Interior;int end=world.NearestNode(transform.position,false,depth);if(end<0)return;
            if(path.Count==0||Time.time>=repathAt) {repathAt=Time.time+.5f;path=Route(end,true,depth);}
            Follow(speed,dt);
        }
        void WanderNear(Vector3 center,float depth)
        {
            var world=WorldZone.Interior;var options=new List<int>();
            for(int i=0;i<world.nodes.Count;i++) {
                bool eligible=waterEnemy?world.nodes[i].y+.8f<depth:world.nodes[i].y+1.65f>depth;
                if(eligible&&(world.transform.TransformPoint(world.nodes[i])-center).sqrMagnitude<150)options.Add(i);
            }
            if(options.Count==0)return;
            path=Route(options[Random.Range(0,options.Count)],false,depth);
        }
        void PickPatrolGoal(float depth)
        {
            var world=WorldZone.Interior;var options=new List<int>();
            for(int i=0;i<world.nodes.Count;i++) {
                bool eligible=waterEnemy?world.nodes[i].y+.8f<depth:world.nodes[i].y+1.65f>depth;
                float d=(world.transform.TransformPoint(world.nodes[i])-transform.position).sqrMagnitude;
                if(eligible&&d>64&&d<1400&&world.IsOpenFloor(i))options.Add(i);
            }
            if(options.Count==0)return;
            path=Route(options[Random.Range(0,options.Count)],false,depth);
        }
        void RunAttack(EnemyState state,BalanceConfig balance,float now,float dt)
        {
            if(victim!=null)Face(victim.transform.position-transform.position,14);
            if(state==EnemyState.WindUp&&now>=phaseEnd) {SetState(EnemyState.Attack);attackLungeEnd=now+.3f;phaseEnd=now+.3f;DealDamage(balance);return;}
            if(state==EnemyState.Attack) {
                if(now<attackLungeEnd&&victim!=null)Direct(victim.transform.position,3.2f,dt);
                if(now>=phaseEnd) {SetState(EnemyState.Recover);phaseEnd=now+1.15f;attackReadyAt=now+balance.attackCooldown;}
                return;
            }
            if(state==EnemyState.Grab) {RunGrab(balance,now,dt);return;}
            if(state==EnemyState.Recover&&now>=phaseEnd) {lostAt=now;SetState(EnemyState.Chase);}
        }
        // Lunge, then (if the diver is still in reach) hold them up for GrabHold seconds and throw them away from the body.
        void RunGrab(BalanceConfig balance,float now,float dt)
        {
            if(held==null) {
                if(now<phaseEnd) {if(victim!=null)Direct(victim.transform.position,2.4f,dt);return;}
                float reach=balance.attackReach+.6f;
                if(victim!=null&&victim.Alive.Value&&victim.Zone.Value==PlayerZone.Interior&&(victim.transform.position-transform.position).sqrMagnitude<=reach*reach) {
                    held=victim;phaseEnd=now+GrabHold;held.GrabbedRpc(NetworkObject,GrabHold);NetSession.Instance.PlaySfx(Snd.Bones,transform.position+Vector3.up*1.6f,.9f,.9f);
                } else {SetState(EnemyState.Recover);phaseEnd=now+1.0f;attackReadyAt=now+balance.attackCooldown;}
                return;
            }
            if(!held.Alive.Value) {held=null;SetState(EnemyState.Recover);phaseEnd=now+.9f;attackReadyAt=now+balance.attackCooldown;return;}
            if(now<phaseEnd)return;
            var fling=transform.forward*balance.throwSpeed+Vector3.up*4.5f;
            held.ShoveRpc(fling);held.Damage(balance.grabDamage);NetSession.Instance.PlaySfx(Snd.Thunk,held.transform.position+Vector3.up,1,.8f);
            held=null;SetState(EnemyState.Recover);phaseEnd=now+.9f;attackReadyAt=now+balance.attackCooldown+1;
        }
        void DealDamage(BalanceConfig balance)
        {
            if(victim==null||!victim.Alive.Value)return;
            float reach=balance.attackReach+1f;var to=victim.transform.position-transform.position;
            if(to.sqrMagnitude<=reach*reach) {
                victim.Damage(waterEnemy?balance.drownedDamage:balance.stalkerDamage);
                to.y=0;victim.ShoveRpc((to.sqrMagnitude>.01f?to.normalized:transform.forward)*balance.hitKnockback+Vector3.up*1.5f);
            }
        }
        public override void OnNetworkDespawn() {if(IsServer&&held!=null&&held.IsSpawned)held.ShoveRpc(Vector3.zero);held=null;All.Remove(this);}
    }
}
