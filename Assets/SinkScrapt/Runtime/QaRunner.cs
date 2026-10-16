using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SinkScrapt
{
    // Explicit QA entry point; only activated by the Editor tools or --qa-host.
    public class QaRunner : MonoBehaviour
    {
        public int expectedPlayers=1;
        public int iterations=10;
        public bool done,passed;
        public string failure="";
        public readonly List<string> checks=new();
        string folder;
        IEnumerator Start()
        {
            folder=Path.GetDirectoryName(SinkApp.Instance.SavePath);Directory.CreateDirectory(folder);StartCoroutine(SampleFrames());
            var stack=new Stack<IEnumerator>();stack.Push(Flow());
            while(stack.Count>0) {
                object current=null;bool moved=false;
                try {moved=stack.Peek().MoveNext();if(moved)current=stack.Peek().Current;}
                catch(Exception e) {failure=e.ToString();break;}
                if(!moved){stack.Pop();continue;}
                if(current is IEnumerator nested)stack.Push(nested);else yield return current;
            }
            passed=failure.Length==0;done=true;
            File.WriteAllText(Path.Combine(folder,"qa-result.json"),JsonUtility.ToJson(new Report{passed=passed,error=failure,checks=checks.ToArray(),perf=PerfSummary()},true));
            Debug.Log((passed?"SINKSCRAPT QA PASS: ":"SINKSCRAPT QA FAIL: ")+(passed?string.Join("; ",checks):failure));
            if(Environment.GetCommandLineArgs().Contains("--qa-host"))Application.Quit(passed?0:1);
        }
        [Serializable]class Report {public bool passed;public string error;public string[] checks;public string perf;}
        // ---- Client view (multiplayer QA) ---------------------------------------------------------------------------------------
        // What one client's screen shows, reported back to the host: its teammates' crew bodies, clips, torch beams and heights,
        // the cutscene, the HUD campaign line with the package countdown, falling packages and the hull stalker's clip.
        [Serializable] public class PeerView
        {
            public int character=-1,falling,width,height;
            public int[] crewCharacters;public bool[] crewModels,crewBeams;public string[] crewClips;public float[] crewHeights,orders;
            public bool cinematic;public string hud="",stalker="",stalkerState="";
        }
        public static string DescribeView()
        {
            var v=new PeerView{width=Screen.width,height=Screen.height,cinematic=Cinematic.Playing,hud=GameHUD.Instance!=null?GameHUD.Instance.StatusLine:""};
            var local=NetPlayer.Local;if(local!=null)v.character=local.Character.Value;
            var crew=NetPlayer.All.Where(p=>p!=null&&p.IsSpawned&&!p.IsOwner).OrderBy(p=>p.OwnerClientId).ToList();
            var bodies=crew.Select(p=>p.GetComponentInChildren<CrewAnimator>()).ToList();
            v.crewCharacters=crew.Select(p=>(int)p.Character.Value).ToArray();
            v.crewModels=bodies.Select(b=>b!=null&&b.GetComponentsInChildren<Renderer>().Any(r=>r.enabled&&r.shadowCastingMode!=UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly)).ToArray();
            v.crewClips=bodies.Select(b=>b!=null?b.Clip:"").ToArray();
            v.crewBeams=crew.Select(p=>p.GetComponentsInChildren<Light>().Count(l=>l.enabled&&l.type==LightType.Spot)>=2).ToArray();
            v.crewHeights=crew.Select(p=>p.transform.position.y).ToArray();
            var s=NetSession.Instance;v.orders=s!=null?s.Campaign.orders.Select(o=>o.remaining).ToArray():new float[0];
            v.falling=NetItem.Live.Count(i=>i!=null&&i.Falling);
            var stalker=NetEnemy.All.FirstOrDefault(e=>e!=null&&!e.waterEnemy);
            if(stalker!=null) {var rig=stalker.GetComponent<EnemyRig>();v.stalkerState=stalker.CurrentState.ToString();v.stalker=rig!=null?rig.Clip:"";}
            return JsonUtility.ToJson(v);
        }
        // Saves this peer's screen (HUD included) as <save folder>/<tag>.png; only in a build started with --qa-capture or --qa-host.
        public static void Capture(string tag)
        {
            var args=Environment.GetCommandLineArgs();if(!args.Contains("--qa-capture")&&!args.Contains("--qa-host"))return;
            var name=new string(tag.Where(c=>char.IsLetterOrDigit(c)||c=='-').Take(48).ToArray());
            var dir=Path.GetDirectoryName(SinkApp.Instance.SavePath);Directory.CreateDirectory(dir);ScreenCapture.CaptureScreenshot(Path.Combine(dir,name+".png"));
        }
        readonly List<PeerView> views=new();
        IEnumerator CollectViews(string tag)
        {
            var session=NetSession.Instance;yield return Wait(()=>session.QaViews.Count>=expectedPlayers-1,10,"Client view "+tag);
            views.Clear();views.AddRange(session.QaViews.Values.Select(j=>JsonUtility.FromJson<PeerView>(j)));
        }
        IEnumerator View(string tag,bool capture=true) {NetSession.Instance.ProbeView(tag,capture);yield return CollectViews(tag);}
        // Network-variable changes reach clients with the next tick, possibly after an RPC sent the same frame: ask again until
        // every client shows `ok` (or time runs out), then capture. The caller checks the final views.
        IEnumerator ViewUntil(string tag,Func<PeerView,bool> ok,float seconds)
        {
            float end=Time.realtimeSinceStartup+seconds;
            while(true) {yield return View(tag,false);if(views.All(ok)||Time.realtimeSinceStartup>end)break;yield return new WaitForSecondsRealtime(.2f);}
            yield return View(tag);
        }
        // Index of the host diver in a client's crew lists.
        static int HostIndex(PeerView v) => Array.IndexOf(v.crewCharacters,(int)NetPlayer.Local.Character.Value);
        static string HostClips(IEnumerable<PeerView> list) => string.Join(",",list.Select(v=>HostIndex(v)>=0?v.crewClips[HostIndex(v)]:"none"));
        // Where a client stands to watch the host on the boat: the deck's port end, looking across the cargo area (+X).
        static readonly Vector3 ClientWatch=new(22,1.05f,-1);
        // Moves the host diver straight ahead at `speed` (no physics: the open dialog keeps its own input frozen) and asks the
        // clients what they see three quarters of the way through.
        IEnumerator Pace(NetPlayer player,float speed,float seconds,string tag)
        {
            var session=NetSession.Instance;float start=Time.realtimeSinceStartup;bool asked=false;
            while(Time.realtimeSinceStartup-start<seconds) {
                player.transform.position+=player.transform.forward*speed*Time.unscaledDeltaTime;
                if(!asked&&Time.realtimeSinceStartup-start>seconds*.75f) {session.ProbeView(tag,true);asked=true;}
                yield return null;
            }
            yield return CollectViews(tag);
        }
        // A client's screen shows the host as the host's own crew character (not the client's), animated from replicated state
        // alone: idle, walk, run, crouch, swim and climb.
        IEnumerator TeammateChecks(NetPlayer player)
        {
            var mate=NetPlayer.All.First(p=>p!=player);
            Check(NetPlayer.All.All(p=>p.GetComponentInChildren<CrewAnimator>()!=null),"Host shows every crew member as a rigged character");
            yield return MoveTo(mate,ClientWatch,PlayerZone.Boat,90);
            yield return MoveTo(player,new Vector3(27,1.05f,-1),PlayerZone.Boat,270);
            GameHUD.Instance.Close();yield return new WaitForSecondsRealtime(1.2f);Capture("host-teammate");
            yield return View("teammate");
            Check(views.All(v=>v.crewCharacters.Length==NetPlayer.All.Count-1&&v.crewModels.All(m=>m)&&!v.crewCharacters.Contains(v.character)&&v.crewCharacters.Distinct().Count()==v.crewCharacters.Length),"Clients see every teammate as a crew model different from their own");
            Check(views.All(v=>HostIndex(v)>=0&&v.crewClips[HostIndex(v)]=="idle"),"Clients see the host's character standing idle ("+HostClips(views)+")");
            GameHUD.Instance.OpenSettings();
            yield return MoveTo(player,new Vector3(27,1.05f,-3),PlayerZone.Boat,0);yield return Pace(player,2,1.6f,"walk");
            Check(views.All(v=>v.crewClips[HostIndex(v)]=="walk"),"Clients see the host walk at 2 m/s ("+HostClips(views)+")");
            yield return MoveTo(player,new Vector3(27,1.05f,-5.5f),PlayerZone.Boat,0);yield return Pace(player,7,1.2f,"run");
            Check(views.All(v=>v.crewClips[HostIndex(v)]=="run"),"Clients see the host run at 7 m/s ("+HostClips(views)+")");
            yield return MoveTo(player,new Vector3(27,1.05f,-1),PlayerZone.Boat,270);
            foreach(var (clip,motion,crouch) in new[]{("crouch",(byte)0,true),("swim",(byte)1,false),("climb",(byte)2,false)}) {
                player.Crouched.Value=crouch;player.Motion.Value=motion;yield return new WaitForSecondsRealtime(.8f);yield return View(clip);
                Check(views.All(v=>v.crewClips[HostIndex(v)]==clip),"Clients see the host's "+clip+" clip ("+HostClips(views)+")");
            }
            player.Crouched.Value=false;player.Motion.Value=0;GameHUD.Instance.Close();
        }
        // Frame times while an expedition is running (the busiest part of play). Informational only: it is whatever machine ran the QA.
        readonly List<float> frames=new();
        IEnumerator SampleFrames()
        {
            while(!done) {
                var s=NetSession.Instance;if(s!=null&&s.InExpedition&&Time.timeScale>0&&Time.unscaledDeltaTime>0)frames.Add(Time.unscaledDeltaTime);
                yield return null;
            }
        }
        string PerfSummary()
        {
            if(frames.Count<30)return "not enough frames sampled";
            var sorted=frames.OrderBy(x=>x).ToArray();float total=frames.Sum();float p99=sorted[Mathf.Min(sorted.Length-1,Mathf.CeilToInt(sorted.Length*.99f)-1)];
            return "avgFps="+(frames.Count/total).ToString("0.0")+"; onePercentLowFps="+(1/p99).ToString("0.0")+"; frames="+frames.Count+"; resolution="+Screen.width+"x"+Screen.height+"; gpu="+SystemInfo.graphicsDeviceName+"; cpu="+SystemInfo.processorType+"; ramMB="+SystemInfo.systemMemorySize+"; editor="+Application.isEditor;
        }
        void Check(bool condition,string text) {if(!condition)throw new Exception(text);checks.Add(text);}
        IEnumerator Wait(Func<bool> condition,float seconds,string description)
        {
            float end=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<end)yield return null;
            if(!condition())throw new TimeoutException(description);
        }
        IEnumerator Act(NetPlayer player,ActionKind action,int arg=0,string payload="") {player.Act(action,arg,payload);yield return new WaitForSecondsRealtime(.25f);}
        IEnumerator MoveTo(NetPlayer player,Vector3 position,PlayerZone zone,float? facing=null)
        {
            player.Teleport(position,zone,facing);yield return new WaitForSecondsRealtime(.15f);
        }
        IEnumerator WalkPath(NetPlayer player,WorldZone world,List<int> route,int seed)
        {
            foreach(int node in route) {
                var target=world.transform.TransformPoint(world.nodes[node]);var deadline=Time.realtimeSinceStartup+6;
                while(Vector3.Distance(player.transform.position,target)>.22f&&Time.realtimeSinceStartup<deadline) {
                    var delta=target-player.transform.position;var horizontal=new Vector3(delta.x,0,delta.z);
                    player.controller.Move(Vector3.ClampMagnitude(horizontal,.25f)+Vector3.down*.15f);yield return null;
                }
                if((player.transform.position-target).sqrMagnitude>.3f)throw new Exception("Physical traversal blocked at node "+node+" seed "+seed+" pos "+player.transform.position+" target "+target);
            }
        }
        // Inside one of the entry vestibules (airlock flight or hatch) that stick out of the grid.
        static bool InEntry(InteriorLayout layout,Vector3 local)
        {
            for(int i=0;i<layout.entries.Count;i++) {
                var origin=layout.EntryOrigin(i,out var fwd);var d=local-origin;var right=new Vector3(fwd.z,0,-fwd.x);
                float along=Vector3.Dot(d,fwd),side=Mathf.Abs(Vector3.Dot(d,right));
                float length=layout.entries[i].Flight?ShipGrid.AirlockLength:ShipGrid.HatchLength;
                if(along>-.6f&&along<length+.4f&&side<2.1f)return true;
            }
            return false;
        }
        // A locked door refuses a diver without a key, opens with one (using it up) and stays open; then an unlocked door
        // toggles. Locked doors only ever close off a dead-end room.
        IEnumerator DoorAndKeyChecks(NetPlayer player,NetSession session,InteriorLayout layout)
        {
            int locked=Enumerable.Range(0,layout.doors.Count).FirstOrDefault(i=>layout.doors[i].locked&&session.DoorLockedNow(i));
            Check(layout.doors[locked].locked,"Layout has "+layout.doors.Count(d=>d.locked)+" locked doors");
            int keys=FindObjectsByType<NetItem>(FindObjectsInactive.Exclude).Count(i=>i.Item!=null&&i.Item.Definition.kind==ItemKind.Key);
            Check(keys==layout.doors.Count(d=>d.locked),"One key spawned per locked door ("+keys+")");
            var world=WorldZone.Interior;var room=layout.DoorRoom(locked);var d=layout.doors[locked];
            // Stand on the outer side of the door.
            var into=layout.RoomAt(d.floor,d.x,d.z)!=null&&layout.DoorCount(layout.RoomAt(d.floor,d.x,d.z))==1;
            var n=new Vector3(ShipGrid.Dirs[d.dir].x,0,ShipGrid.Dirs[d.dir].y);var outside=layout.DoorCenter(locked)+(into?n:-n)*1.2f;
            yield return MoveTo(player,WorldZone.InteriorOffset+outside+Vector3.up*.05f,PlayerZone.Interior);
            player.Inventory.items.RemoveAll(i=>i.Definition.kind==ItemKind.Key);player.PublishInventory();
            yield return Act(player,ActionKind.Door,locked);
            Check(session.DoorLockedNow(locked)&&!session.DoorOpen(locked),"Locked door refuses a diver without a key");
            var start=world.CellNode(d.floor,d.x,d.z);var inner=into?world.CellNode(d.floor,d.x,d.z):world.CellNode(d.floor,d.x+ShipGrid.Dirs[d.dir].x,d.z+ShipGrid.Dirs[d.dir].y);
            var outerCell=into?new Vector2Int(d.x,d.z)+ShipGrid.Dirs[d.dir]:new Vector2Int(d.x,d.z);int outer=world.CellNode(d.floor,outerCell.x,outerCell.y);
            Check(world.FindPath(outer,inner,false,0).Count==0,"Locked door closes the route into its room");
            var key=session.SpawnItem(ItemCatalog.Create(ItemCatalog.Key),player.transform.position+player.transform.forward*.5f+Vector3.up*.5f);
            if(player.Inventory.items.Count>=4){player.Inventory.items.RemoveAt(player.Inventory.items.Count-1);player.PublishInventory();}
            yield return new WaitForSecondsRealtime(.3f);yield return Act(player,ActionKind.Pickup,0,key.Uid);
            Check(player.Inventory.items.Any(i=>i.uid==key.Uid),"Key picked up");
            yield return Act(player,ActionKind.Door,locked);
            Check(!session.DoorLockedNow(locked)&&session.DoorOpen(locked)&&!player.Inventory.items.Any(i=>i.Definition.kind==ItemKind.Key),"Key opens the locked door and is used up");
            Check(world.FindPath(outer,inner,false,0).Count>0,"Unlocked door opens the route into its room");
            yield return new WaitForSecondsRealtime(.6f);
            var leaf=world.doors[locked];
            // The single leaf rests open inside the room, at most 1.15 m from the wall and clear of the door's centre line.
            var doorCentre=WorldZone.InteriorOffset+layout.DoorCenter(locked);
            bool intoRoom=leaf.hinges.All(h=>{var tip=h.position+h.right*(leaf.leafWidth+InteriorDoor.LeafGap);var local=tip-WorldZone.InteriorOffset;var c=new Vector2Int(Mathf.FloorToInt(local.x/ShipGrid.Cell+ShipGrid.Columns*.5f),Mathf.FloorToInt(local.z/ShipGrid.Cell+ShipGrid.Rows*.5f));
                var side=tip-doorCentre;side-=n*Vector3.Dot(side,n);side.y=0;
                return layout.Kind(d.floor,c.x,c.y,out _)==CellKind.Room&&Mathf.Abs(Vector3.Dot(tip-doorCentre,n))<1.15f&&side.magnitude>.5f;});
            Check(leaf.IsOpen&&leaf.Opening>=1&&leaf.blocks.All(b=>b.enabled)&&intoRoom,"The door leaf ("+leaf.style+") swings open into the room, beside the doorway");
            int plain=Enumerable.Range(0,layout.doors.Count).First(i=>!layout.doors[i].locked);
            var pn=new Vector3(ShipGrid.Dirs[layout.doors[plain].dir].x,0,ShipGrid.Dirs[layout.doors[plain].dir].y);
            yield return MoveTo(player,WorldZone.InteriorOffset+layout.DoorCenter(plain)-pn*1.2f+Vector3.up*.05f,PlayerZone.Interior);
            yield return Act(player,ActionKind.Door,plain);bool opened=session.DoorOpen(plain);
            yield return Act(player,ActionKind.Door,plain);
            Check(opened&&!session.DoorOpen(plain),"Unlocked door opens and closes");
            _=room;
        }
        // A lit torch drains its battery and goes dark when empty; a carried battery refills it and is used up.
        IEnumerator BatteryChecks(NetPlayer player,NetSession session)
        {
            yield return MoveTo(player,BoatLayout.HelmStand,PlayerZone.Boat,0);
            var lamp=player.Inventory.Torch;Check(lamp!=null&&lamp==player.Inventory.utility,"The torch rides in the belt slot");
            lamp.charge=.02f;player.Inventory.lightOn=false;player.PublishInventory();
            yield return Act(player,ActionKind.ToggleLight);Check(player.Inventory.lightOn,"Torch switches on (F)");
            yield return Wait(()=>!player.Inventory.lightOn,10,"Torch battery runs out");
            Check(lamp.charge<=0,"An empty battery switches the torch off");
            yield return Act(player,ActionKind.ToggleLight);Check(!player.Inventory.lightOn,"A dead torch without a spare battery stays dark");
            var cell=session.SpawnItem(ItemCatalog.Create(ItemCatalog.Battery),player.transform.position+player.transform.forward*.6f+Vector3.up*.6f);
            yield return new WaitForSecondsRealtime(.3f);yield return Act(player,ActionKind.Pickup,0,cell.Uid);
            player.Inventory.selected=player.Inventory.items.FindIndex(i=>i.uid==cell.Uid);player.PublishInventory();
            yield return Act(player,ActionKind.Reload);
            Check(lamp.charge>=1&&!player.Inventory.items.Any(i=>i.Definition.kind==ItemKind.Battery),"Battery refills the belt torch and is used up");
            // Changing what is in hand never switches the torch off.
            yield return Act(player,ActionKind.ToggleLight);player.Inventory.selected=0;player.PublishInventory();
            yield return Act(player,ActionKind.SelectSlot,Mathf.Max(0,player.Inventory.items.Count-1));
            Check(player.Inventory.lightOn,"Torch stays lit when the hand slot changes");
            if(expectedPlayers>1) {
                // A client on the deck looks across the host's beam (host faces +Z, the client +X).
                yield return MoveTo(NetPlayer.All.First(p=>p!=player),ClientWatch,PlayerZone.Boat,90);
                yield return MoveTo(player,new Vector3(25,1.05f,-3.5f),PlayerZone.Boat,0);yield return new WaitForSecondsRealtime(.6f);
                yield return View("torch-beam");
                Check(views.All(v=>v.crewBeams[HostIndex(v)]),"A teammate sees the host's belt torch light (hotspot and spill)");
            }
        }
        // Computer order -> parachute package on the deck -> opened into its tools (the torch goes to the belt slot).
        IEnumerator SupplyDrop(NetPlayer player,NetSession session)
        {
            yield return MoveTo(player,BoatLayout.TerminalStand,PlayerZone.Boat,0);
            yield return Act(player,ActionKind.Buy,0,"12,13,14");
            Check(session.Campaign.credits==0&&session.Campaign.orders.Count==1&&player.Inventory.items.Count==0,"Computer order is paid from the shared wallet and goes in flight");
            if(expectedPlayers>1) {
                // Clients count their copy of the timer down between campaign syncs (GameHUD); it must track the host's.
                yield return View("hud-order");float hostLeft=session.Campaign.orders[0].remaining;
                Check(views.All(v=>v.orders.Length==1&&Mathf.Abs(v.orders[0]-hostLeft)<1.5f&&v.hud.Contains("PAKET")),"Clients' HUD shows the package countdown ("+string.Join(",",views.Select(v=>v.orders.Length>0?v.orders[0].ToString("0.0"):"-"))+" s, host "+hostLeft.ToString("0.0")+" s)");
                float shown=views.Min(v=>v.orders[0]);yield return new WaitForSecondsRealtime(2);yield return View("hud-order-later",false);hostLeft=session.Campaign.orders[0].remaining;
                Check(views.All(v=>v.orders.Length==1&&v.orders[0]<shown-1.2f&&v.orders[0]>shown-3.5f&&Mathf.Abs(v.orders[0]-hostLeft)<1.5f),"Client order timers count down with the host's ("+string.Join(",",views.Select(v=>v.orders.Length>0?v.orders[0].ToString("0.0"):"-"))+" s, host "+hostLeft.ToString("0.0")+" s)");
            }
            yield return Act(player,ActionKind.Buy,0,"15");Check(session.Campaign.orders.Count==1&&session.Campaign.credits==0,"Insufficient funds rejected");
            yield return Act(player,ActionKind.Buy,0,"16,16,16,16,16");Check(session.Campaign.orders.Count==1,"An over-full package is rejected");
            session.Campaign.orders[0].remaining=.3f;
            yield return Wait(()=>session.Campaign.cargo.Any(i=>i.Definition.kind==ItemKind.Package),5,"Package dropped");
            if(expectedPlayers>1) {
                yield return ViewUntil("parachute",v=>v.falling>=1&&v.orders.Length==0,3);
                Check(views.All(v=>v.falling>=1&&v.orders.Length==0),"Clients see the package descend under its parachute and the order leave their HUD (falling "+string.Join(",",views.Select(v=>v.falling))+", orders "+string.Join(",",views.Select(v=>v.orders.Length))+")");
                var drop=session.Campaign.cargo.First(i=>i.Definition.kind==ItemKind.Package);NetItem falling=NetItem.Live.Find(i=>i!=null&&i.Uid==drop.uid);
                // Watch the landing from the cargo-area corner farthest from it.
                if(falling!=null) {
                    var spot=falling.transform.position;var corner=new[]{new Vector3(21.5f,1.05f,-5.5f),new Vector3(28.5f,1.05f,-5.5f),new Vector3(21.5f,1.05f,3.5f),new Vector3(28.5f,1.05f,3.5f)}.OrderByDescending(c=>new Vector2(c.x-spot.x,c.z-spot.z).sqrMagnitude).First();
                    yield return MoveTo(NetPlayer.All.First(p=>p!=player),corner,PlayerZone.Boat,Mathf.Atan2(spot.x-corner.x,spot.z-corner.z)*Mathf.Rad2Deg);
                }
                yield return Wait(()=>(falling=NetItem.Live.Find(i=>i!=null&&i.Uid==drop.uid))!=null&&(!falling.Falling||falling.transform.position.y<3.5f),15,"Package nears the deck");
                yield return View("parachute-low");
            }
            var uid=session.Campaign.cargo.First(i=>i.Definition.kind==ItemKind.Package).uid;
            NetItem package=null;yield return Wait(()=>(package=NetItem.Live.Find(i=>i!=null&&i.Uid==uid))!=null&&!package.Falling,20,"Package lands under its parachute");
            Check(BoatLayout.OnDeck(package.transform.position)&&session.Campaign.orders.Count==0,"Parachute package lands on the small boat's deck");
            yield return MoveTo(player,package.transform.position+Vector3.back*1.2f+Vector3.down*.3f,PlayerZone.Boat,0);
            yield return Act(player,ActionKind.Pickup,0,uid);
            var tools=session.Campaign.cargo.Where(i=>i.Definition.kind!=ItemKind.Package&&i.Definition.kind!=ItemKind.Scrap).ToList();
            Check(!session.Campaign.cargo.Any(i=>i.uid==uid)&&tools.Count==3,"Opening the package lays out its three tools on the deck");
            foreach(var tool in tools) {
                var entity=NetItem.Live.Find(i=>i!=null&&i.Uid==tool.uid);yield return MoveTo(player,entity.transform.position+Vector3.back*.8f+Vector3.down*.3f,PlayerZone.Boat,0);
                yield return Act(player,ActionKind.Pickup,0,tool.uid);
            }
            Check(player.Inventory.HasUtility&&player.Inventory.utility.Definition.kind==ItemKind.Flashlight&&player.Inventory.items.Count==2,"Picked-up torch fills the belt slot; radio and tank take hand slots");
            // [5] swaps a hand-held radio onto the belt and the torch into the hand, and back.
            player.Inventory.selected=player.Inventory.items.FindIndex(i=>i.Definition.kind==ItemKind.Radio);player.PublishInventory();
            yield return Act(player,ActionKind.Utility);Check(player.Inventory.utility.Definition.kind==ItemKind.Radio&&player.Inventory.items.Any(i=>i.Definition.kind==ItemKind.Flashlight),"Belt slot swaps with the tool in hand");
            yield return Act(player,ActionKind.Utility);Check(player.Inventory.utility.Definition.kind==ItemKind.Flashlight,"Belt slot swaps back");
        }
        // Pulling out of the water onto a low ledge, and not onto a high one. Uses a temporary block in open sea.
        IEnumerator MantleChecks(NetPlayer player)
        {
            // The open settings dialog freezes the diver (QA steers them directly); the mantle animation needs it closed.
            GameHUD.Instance.Close();
            var low=new GameObject("QA low ledge");low.transform.position=new Vector3(-40,-1,-40);low.AddComponent<BoxCollider>().size=new Vector3(6,3.2f,6);
            yield return MoveTo(player,new Vector3(-40,-NetPlayer.FloatDepth,-43.5f),PlayerZone.Exterior,0);
            Physics.SyncTransforms();
            Check(player.TryMantle(Vector3.forward,false,true),"Swimmer pulls up onto a ledge 0.6 m above the water");
            float end=Time.realtimeSinceStartup+3;while(player.Mantling&&Time.realtimeSinceStartup<end)yield return null;
            Check(!player.Mantling&&Mathf.Abs(player.transform.position.y-.6f)<.15f,"Mantle ends standing on the ledge ("+player.transform.position.y.ToString("0.00")+")");
            low.transform.position=new Vector3(-40,-.4f,-40);Physics.SyncTransforms();
            yield return MoveTo(player,new Vector3(-40,-NetPlayer.FloatDepth,-43.5f),PlayerZone.Exterior,0);
            Check(!player.TryMantle(Vector3.forward,false,true),"A ledge 1.2 m above the water is too high to climb");
            Destroy(low);yield return MoveTo(player,BoatLayout.HelmStand,PlayerZone.Boat,0);GameHUD.Instance.OpenSettings();
        }
        // Push the diver into all four walls from sample cells: they must stay on that deck and never end up in a void cell or
        // outside the hull (airlock flights excepted).
        IEnumerator SealedHull(NetPlayer player,WorldZone world,InteriorLayout layout,int iteration)
        {
            var keys=world.cellNodes.Keys.OrderBy(k=>k).ToList();int samples=iteration==0?10:4;
            for(int s=0;s<samples;s++) {
                int key=keys[(s*37+iteration*11)%keys.Count];int node=world.cellNodes[key];
                int deck=key/(ShipGrid.Columns*ShipGrid.Rows);
                for(int dir=0;dir<4;dir++) {
                    yield return MoveTo(player,world.transform.TransformPoint(world.nodes[node])+Vector3.up*.05f,PlayerZone.Interior);
                    var push=new Vector3(ShipGrid.Dirs[dir].x,0,ShipGrid.Dirs[dir].y);
                    for(int k=0;k<24;k++){player.controller.Move(push*.3f+Vector3.down*.15f);yield return null;}
                    var local=player.transform.position-WorldZone.InteriorOffset;
                    int cx=Mathf.FloorToInt(local.x/ShipGrid.Cell+ShipGrid.Columns*.5f),cz=Mathf.FloorToInt(local.z/ShipGrid.Cell+ShipGrid.Rows*.5f);
                    bool inAirlock=InEntry(layout,local);
                    // A stair may carry the diver to the next deck; any deck whose height matches must have a module in that cell.
                    bool inside=Enumerable.Range(0,ShipGrid.Decks).Any(d=>local.y>ShipTimeline.FloorHeight(d)-.3f&&local.y<ShipTimeline.FloorHeight(d)+3.05f&&layout.Kind(d,cx,cz,out _)!=CellKind.Void);
                    if(!inAirlock&&!inside)throw new Exception("Diver left the sealed hull from cell "+key+" pushing "+dir+": "+local+" seed "+layout.seed);
                }
            }
            Check(true,"Walls hold at "+samples+" sample cells x 4 directions: "+iteration);
        }
        // The stalker hunts a player in a room on its deck from a cell behind walls (it has to find the doorways), and is called
        // to noises around corners. Every frame its body must be clear of level geometry and inside a used cell.
        IEnumerator NoClipping(NetEnemy stalker,NetPlayer player,InteriorLayout layout,int iteration)
        {
            var world=WorldZone.Interior;var random=new System.Random(layout.seed);int frames=0;
            var overlap=new Collider[16];
            void Inspect()
            {
                var local=stalker.transform.position-WorldZone.InteriorOffset;frames++;
                int cx=Mathf.FloorToInt(local.x/ShipGrid.Cell+ShipGrid.Columns*.5f),cz=Mathf.FloorToInt(local.z/ShipGrid.Cell+ShipGrid.Rows*.5f);
                bool cellOk=Enumerable.Range(0,ShipGrid.Decks).Any(d=>local.y>ShipTimeline.FloorHeight(d)-.3f&&local.y<ShipTimeline.FloorHeight(d)+3.05f&&layout.Kind(d,cx,cz,out _)!=CellKind.Void)||InEntry(layout,local);
                foreach(float h in new[]{.6f,1.5f}) {
                    int count=Physics.OverlapSphereNonAlloc(stalker.transform.position+Vector3.up*h,.25f,overlap,~0,QueryTriggerInteraction.Ignore);
                    for(int i=0;i<count;i++) {var c=overlap[i];if(c.attachedRigidbody==null&&c is not CharacterController&&c.GetComponentInParent<NetEnemy>()==null)throw new Exception("Stalker body inside "+c.name+" at "+local+" seed "+layout.seed);}
                }
                if(!cellOk)throw new Exception("Stalker left the modules at "+local+" seed "+layout.seed);
            }
            for(int trial=0;trial<3;trial++) {
                var deck=random.Next(1,3);var rooms=layout.rooms.Where(r=>r.floor==deck).ToList();var room=rooms[random.Next(rooms.Count)];
                // Start in the cell farthest from the room on that deck, so the route has to turn through doors.
                var starts=world.cellNodes.Where(kv=>kv.Key/(ShipGrid.Columns*ShipGrid.Rows)==deck).Select(kv=>kv.Value).ToList();
                var target=world.transform.TransformPoint(world.nodes[world.CellNode(deck,room.x,room.z)]);
                int from=starts.OrderByDescending(n=>(world.transform.TransformPoint(world.nodes[n])-target).sqrMagnitude).First();
                stalker.transform.position=world.transform.TransformPoint(world.nodes[from]);
                yield return MoveTo(player,target+Vector3.up*.05f,PlayerZone.Interior);player.Health.Value=100;
                stalker.Hear(target,200);
                float end=Time.realtimeSinceStartup+7;while(Time.realtimeSinceStartup<end) {Inspect();player.Health.Value=100;yield return null;}
            }
            player.Health.Value=100;
            Check(frames>0,"Stalker body stayed clear of walls for "+frames+" frames of hunting through doors"+(iteration>0?" "+iteration:""));
        }
        // Monster behaviour on the live host: sight and chase, a telegraphed hit, flare distraction, and the water hunter once the wreck floods.
        IEnumerator EnemyChecks(NetPlayer player,NetSession session,int iteration)
        {
            var app=SinkApp.Instance;var balance=app.balance;var offset=WorldZone.InteriorOffset;string at=iteration>0?" "+iteration:"";
            var stalker=NetEnemy.All.FirstOrDefault(e=>!e.waterEnemy);
            Check(stalker!=null,"Hull stalker spawns with the expedition"+at);
            // The scripted strike checks need the plain strike; the grab gets its own check below.
            NetEnemy.GrabChanceOverride=0;
            Check(NetEnemy.All.Count(e=>e.waterEnemy)==0,"Drowned hunter stays away until the water reaches deck 2"+at);
            // Sight, chase and a telegraphed hit along the longest straight corridor on deck 2. Every expedition uses a different layout.
            var layout=WorldZone.Interior.layout;
            Check(layout.StraightRun(1,5,out var runStart,out int runDir,out _),"Deck 2 has a straight corridor of five cells"+at);
            Vector3 Cell(int deck,Vector2Int start,int dir,int k) {var c=start+ShipGrid.Dirs[dir]*k;return offset+ShipGrid.Center(deck,c.x,c.y);}
            var toPlayer=Cell(1,runStart,runDir,4)-Cell(1,runStart,runDir,1);
            // The stalker can spot the diver during the teleport itself, so the scream clock starts before either is placed.
            float noticedFrom=Time.time;
            stalker.transform.position=Cell(1,runStart,runDir,1);stalker.transform.rotation=Quaternion.LookRotation(toPlayer);
            yield return MoveTo(player,Cell(1,runStart,runDir,4)+Vector3.up*.05f,PlayerZone.Interior,Quaternion.LookRotation(-toPlayer).eulerAngles.y);player.Health.Value=100;
            yield return Wait(()=>stalker.CurrentState==EnemyState.Scream||stalker.CurrentState==EnemyState.Chase||stalker.CurrentState==EnemyState.WindUp||stalker.CurrentState==EnemyState.Attack,8,"Stalker notices the player");
            Check(iteration>0||stalker.LastScream>=noticedFrom,"Hull stalker screams when it first spots a player ("+stalker.CurrentState+", scream at "+stalker.LastScream.ToString("0.0")+" s, placed at "+noticedFrom.ToString("0.0")+" s)"+at);
            yield return Wait(()=>stalker.CurrentState!=EnemyState.Scream,balance.screamSeconds+1,"Stalker scream ends");
            Check(true,"Hull stalker spots a player in its corridor and gives chase"+at);
            yield return Wait(()=>stalker.CurrentState==EnemyState.WindUp,10,"Stalker winds up (layout seed "+session.Seed+")");
            Check(player.Health.Value>=100,"Stalker telegraphs before it hits"+at);
            yield return Wait(()=>player.Health.Value<100,5,"Stalker hit lands");
            Check(Mathf.Abs(player.Health.Value-(100-balance.stalkerDamage))<.01f,"Stalker hit costs "+balance.stalkerDamage+" health"+at);
            Check(NetEnemy.ThreatTo(player)>.5f,"Chasing stalker raises the threat cue"+at);
            if(iteration>0) {yield return NoClipping(stalker,player,layout,iteration);stalker.enabled=false;player.Health.Value=100;yield break;}
            yield return Wait(()=>stalker.CurrentState==EnemyState.Recover,3,"Stalker recovers");
            // Second attack: grab, lift and throw.
            NetEnemy.GrabChanceOverride=1;player.Health.Value=100;stalker.transform.position=Cell(1,runStart,runDir,1);stalker.transform.rotation=Quaternion.LookRotation(toPlayer);
            yield return MoveTo(player,Cell(1,runStart,runDir,3)+Vector3.up*.05f,PlayerZone.Interior,Quaternion.LookRotation(-toPlayer).eulerAngles.y);
            // A teammate watches from one cell behind the host (farther from the stalker, so the host stays its target), and the
            // host's belt torch lights the corridor.
            var mate=expectedPlayers>1?NetPlayer.All.FirstOrDefault(p=>p!=player):null;
            if(mate!=null) {
                if(player.Inventory.Torch!=null) {player.Inventory.lightOn=true;player.PublishInventory();}
                yield return MoveTo(mate,Cell(1,runStart,runDir,4)+Vector3.up*.05f,PlayerZone.Interior,Quaternion.LookRotation(-toPlayer).eulerAngles.y);mate.Health.Value=100;
            }
            yield return Wait(()=>stalker.CurrentState==EnemyState.Grab,12,"Stalker lunges to grab");
            yield return Wait(()=>player.Grabbed,3,"Stalker seizes the diver");
            float floor=Cell(1,runStart,runDir,0).y;float highest=0;bool asked=false;
            float liftEnd=Time.realtimeSinceStartup+1.3f;
            while(Time.realtimeSinceStartup<liftEnd) {
                highest=Mathf.Max(highest,player.transform.position.y-floor);
                if(mate!=null&&!asked&&Time.realtimeSinceStartup>liftEnd-.6f) {session.ProbeView("grab",true);asked=true;}
                yield return null;
            }
            Check(highest>.6f&&player.Health.Value>=100,"Grabbed diver is lifted off the floor ("+highest.ToString("0.00")+" m)");
            if(mate!=null) {
                yield return CollectViews("grab");
                Check(views.All(v=>v.stalkerState==nameof(EnemyState.Grab)&&v.stalker=="grab"&&v.crewHeights[HostIndex(v)]-floor>.4f),"A teammate sees the stalker's grab clip and the host lifted ("+string.Join(",",views.Select(v=>v.stalker+"/"+v.stalkerState+" "+(v.crewHeights[HostIndex(v)]-floor).ToString("0.00")+" m"))+")");
            }
            yield return Wait(()=>player.Health.Value<100,4,"Grabbed diver is thrown");
            Check(!player.Grabbed&&Mathf.Abs(player.Health.Value-(100-balance.grabDamage))<.01f,"Throw releases the diver and costs "+balance.grabDamage+" health");
            if(mate!=null) {
                yield return new WaitForSecondsRealtime(.3f);yield return View("thrown");
                Check(views.All(v=>v.stalkerState!=nameof(EnemyState.Grab)),"A teammate sees the throw end the grab ("+string.Join(",",views.Select(v=>v.stalkerState))+")");
                Check(mate.Health.Value>=100,"The watching teammate was not attacked");
                yield return MoveTo(mate,ClientWatch,PlayerZone.Boat,90);
            }
            NetEnemy.GrabChanceOverride=0;
            yield return Wait(()=>stalker.CurrentState!=EnemyState.Recover&&stalker.CurrentState!=EnemyState.Grab,4,"Stalker recovers from the throw");
            // A flare distracts it from a player in plain sight.
            player.Health.Value=100;stalker.transform.position=Cell(1,runStart,runDir,1);stalker.transform.rotation=Quaternion.LookRotation(toPlayer);
            yield return MoveTo(player,Cell(1,runStart,runDir,4)+Vector3.up*.05f,PlayerZone.Interior,Quaternion.LookRotation(-toPlayer).eulerAngles.y);
            yield return Wait(()=>stalker.CurrentState==EnemyState.Chase,8,"Stalker resumes chase");
            var flarePosition=Cell(1,runStart,runDir,0)+Vector3.up*.4f;
            session.flares.Add(new NetSession.FlareState {position=flarePosition,ends=app.TimeNow+12});
            yield return Wait(()=>stalker.CurrentState==EnemyState.Distracted,4,"Flare distracts stalker");
            float flareStart=Vector3.Distance(stalker.transform.position,flarePosition);yield return new WaitForSecondsRealtime(2.5f);
            Check(stalker.CurrentState==EnemyState.Distracted&&Vector3.Distance(stalker.transform.position,flarePosition)<flareStart-1&&player.Health.Value>=100,"Flare pulls the stalker away from a visible player");
            session.flares.Clear();
            yield return NoClipping(stalker,player,layout,iteration);
            // The hunter appears when the water reaches deck 2 and attacks a swimmer in the flooded deck.
            stalker.enabled=false;
            session.Started.Value=app.TimeNow-ShipTimeline.At(ShipTimeline.WaterDeck2+15,balance.duration);
            yield return Wait(()=>NetEnemy.All.Any(e=>e.waterEnemy),6,"Drowned hunter surfaces");
            var hunter=NetEnemy.All.First(e=>e.waterEnemy);
            Check(hunter.transform.position.y<session.FloodLevel,"Drowned hunter starts inside the flooded deck");
            Check(layout.StraightRun(0,3,out var lowStart,out int lowDir,out _),"Deck 1 has a straight corridor of three cells");
            var toSwimmer=Cell(0,lowStart,lowDir,2)-Cell(0,lowStart,lowDir,0);
            hunter.transform.position=Cell(0,lowStart,lowDir,0)+Vector3.up*1.1f;hunter.transform.rotation=Quaternion.LookRotation(toSwimmer);
            yield return MoveTo(player,Cell(0,lowStart,lowDir,2)+Vector3.up*.05f,PlayerZone.Interior,0);player.Health.Value=100;
            Check(player.Underwater,"Test swimmer is submerged");
            yield return Wait(()=>player.Health.Value<100,14,"Drowned hunter attacks a swimmer");
            Check(player.Health.Value<=100-balance.drownedDamage+.01f,"Drowned hunter hit costs "+balance.drownedDamage+" health");
            hunter.enabled=false;session.Started.Value=app.TimeNow;player.Health.Value=100;NetEnemy.GrabChanceOverride=null;
        }
        IEnumerator Flow()
        {
            var app=SinkApp.Instance;
            app.noMonsters=false;if(!app.Connected)app.StartLocal(true);
            // A new campaign gathers on the small boat at sea; the island is only for closing a quota cycle.
            yield return Wait(()=>NetSession.Instance!=null&&NetSession.Instance.Phase.Value==GamePhase.Ready,60,"Boat load");
            yield return Wait(()=>NetPlayer.All.Count>=expectedPlayers,90,"Expected crew count");
            var session=NetSession.Instance;var player=NetPlayer.Local;
            Check(player!=null&&session.IsServer,"Listen host and "+expectedPlayers+" connected players");
            Check(WorldZone.Island==null,"A new campaign starts at sea, not at the island");
            Check(NetPlayer.All.Select(p=>p.Character.Value).Distinct().Count()==NetPlayer.All.Count,"Every crew member wears a different character");
            if(expectedPlayers>1) {
                session.ProbePeers();yield return Wait(()=>session.QaReports.Count>=expectedPlayers,20,"Boat snapshot responses");
                Check(session.QaReports.Values.All(r=>r.boat),"Every peer loaded the small boat scene");
                yield return TeammateChecks(player);
            }
            yield return MoveTo(player,BoatLayout.HelmStand,PlayerZone.Boat,0);yield return Act(player,ActionKind.TravelIsland);
            Check(WorldZone.Island==null&&session.Phase.Value==GamePhase.Ready,"The island is closed before the third expedition");
            yield return SupplyDrop(player,session);
            for(int iteration=0;iteration<iterations;iteration++) {
                yield return MoveTo(player,BoatLayout.HelmStand,PlayerZone.Boat,0);
                yield return Act(player,ActionKind.TravelShip);
                yield return Wait(()=>session.InExpedition,80,"Expedition load "+iteration);
                yield return Wait(()=>WorldZone.Interior!=null&&WorldZone.Interior.nodes.Count>0,10,"Interior geometry");
                var layout=InteriorLayout.Unpack(session.BlueprintCode);
                Check(layout.Validate(),"Live layout "+iteration+" has valid rooms and portals");
                if(expectedPlayers>1) {
                    session.ProbePeers();yield return Wait(()=>session.QaReports.Count>=expectedPlayers,20,"Peer snapshot responses");
                    Check(session.QaReports.Values.All(r=>r.layout==session.BlueprintCode&&r.nodes==WorldZone.Interior.nodes.Count),"Peer blueprints and geometry agree: "+iteration);
                    if(!app.localTransport)Check(session.QaReports.Values.All(r=>r.voice),"Voice ready on every online peer: "+iteration);
                    if(iteration==0) {
                        yield return ViewUntil("arrival",v=>v.cinematic,3);Check(views.All(v=>v.cinematic),"The wreck arrival cutscene plays on every client");
                        // Let it end, so the client's captures of the stalker below show its own eyes rather than the cutscene camera.
                        yield return Wait(()=>app.TimeNow-session.ArrivalAt.Value>app.balance.arrivalCutscene+.5f,15,"Arrival cutscene ends");
                        yield return View("after-arrival",false);Check(views.All(v=>!v.cinematic),"The arrival cutscene ends on every client");
                    }
                }
                yield return EnemyChecks(player,session,iteration);
                foreach(var enemy in FindObjectsByType<NetEnemy>(FindObjectsInactive.Exclude))enemy.enabled=false;
                foreach(var hazard in FindObjectsByType<WorldHazard>(FindObjectsInactive.Exclude))hazard.enabled=false;
                if(iteration==0)yield return DoorAndKeyChecks(player,session,layout);
                session.OpenAllDoors();yield return new WaitForSecondsRealtime(.6f);
                Check(Enumerable.Range(0,layout.doors.Count).All(i=>session.DoorOpen(i)&&!session.DoorLockedNow(i)),"All "+layout.doors.Count+" doors open for the traversal walk: "+iteration);
                GameHUD.Instance.OpenSettings();
                var world=WorldZone.Interior;
                // Walk from a corridor into every room on each deck (through the bilge on deck 1), in the real CharacterController.
                for(int floor=0;floor<3;floor++) {
                    layout.StraightRun(floor,1,out var corridorCell,out _,out _);int start=world.CellNode(floor,corridorCell.x,corridorCell.y);
                    var rooms=layout.rooms.Where(r=>r.floor==floor).ToList();if(iteration>0)rooms=rooms.Take(2).ToList();
                    foreach(var room in rooms) {
                        int end=world.CellNode(floor,room.x,room.z);
                        var path=world.FindPath(start,end,false,0);Check(path.Count>0,"Deck "+floor+" route to room "+room.id+" "+iteration);
                        yield return MoveTo(player,world.transform.TransformPoint(world.nodes[start])+Vector3.up*.05f,PlayerZone.Interior);
                        yield return WalkPath(player,world,path,session.Seed);
                    }
                }
                Check(true,"CharacterController walked from corridors into rooms on 3 decks: "+iteration);
                // Bilge: deck 1 is waist-deep from the start, wading not swimming.
                layout.StraightRun(0,1,out var bilgeCell,out _,out _);
                yield return MoveTo(player,WorldZone.InteriorOffset+ShipGrid.Center(0,bilgeCell.x,bilgeCell.y)+Vector3.up*.05f,PlayerZone.Interior);
                for(int k=0;k<10;k++){player.controller.Move(Vector3.down*.2f);yield return null;}
                Check(Mathf.Abs(world.water.position.y-(ShipTimeline.FloorHeight(0)+ShipTimeline.BilgeDepth))<.05f&&player.Wading&&!player.Swimming&&!player.Underwater,"Deck 1 stands in waist-deep bilge water: "+iteration);
                yield return SealedHull(player,world,layout,iteration);
                // Every stair shaft, bottom to top, then both airlock flights.
                for(int i=0;i<world.stairRoutes.Count;i++) {
                    var (start,end)=world.stairRoutes[i];float top=world.nodes[end].y;
                    yield return MoveTo(player,world.transform.TransformPoint(world.nodes[start])+Vector3.up*.05f,PlayerZone.Interior);
                    yield return WalkPath(player,world,world.FindPath(start,end,false,0),session.Seed);
                    Check(player.transform.position.y>top-.2f,"Physical stair "+i+" reaches deck "+(Mathf.RoundToInt((top-2)/3)+1)+": "+iteration);
                }
                if(iteration==0) {
                    Check(layout.entries[0].floor==2&&layout.entries.Select(e=>e.floor).Distinct().Count()>=2,"Entry A is on the top deck; B and C are on other decks ("+string.Join(",",layout.entries.Select(e=>e.floor+1))+")");
                    for(int portal=0;portal<layout.entries.Count;portal++) {
                        var entry=layout.entries[portal];
                        layout.StraightRun(entry.floor,1,out var deckCell,out _,out _);int routeStart=world.CellNode(entry.floor,deckCell.x,deckCell.y),routeEnd=world.airlockNodes[portal];
                        yield return MoveTo(player,world.transform.TransformPoint(world.nodes[routeStart])+Vector3.up*.05f,PlayerZone.Interior);
                        yield return WalkPath(player,world,world.FindPath(routeStart,routeEnd,false,0),session.Seed);
                        Check(entry.Flight?player.transform.position.y>9.8f:Mathf.Abs(player.transform.position.y-ShipTimeline.FloorHeight(entry.floor))<.3f,"Physical route to entry "+InteriorLayout.EntryNames[portal]+" on deck "+(entry.floor+1)+(entry.Flight?" (airlock flight)":" (hatch)"));
                        yield return MoveTo(player,WorldZone.ExteriorExit(portal),PlayerZone.Exterior);
                        yield return Act(player,ActionKind.Portal,portal);Check(player.Zone.Value==PlayerZone.Interior&&Vector3.Distance(player.transform.position,WorldZone.InteriorEntry(portal))<.5f,"Portal "+InteriorLayout.EntryNames[portal]+" enters interior");
                        yield return Act(player,ActionKind.Portal,portal);Check(player.Zone.Value==PlayerZone.Exterior,"Portal "+InteriorLayout.EntryNames[portal]+" returns exterior");
                    }
                    yield return MantleChecks(player);
                    var mid=WorldZone.RopePoint(.5f);var chord=(WorldZone.RopeBottom+WorldZone.RopeTop)*.5f;
                    Check(mid.y<chord.y-.01f,"Boarding rope hangs below its chord ("+(chord.y-mid.y).ToString("0.00")+" m sag)");
                    yield return BatteryChecks(player,session);
                    bool Day(float h)=>h>=7&&h<18;
                    Check(Day(Mathf.Repeat(session.ClockFrom.Value,24))!=Day(Mathf.Repeat(session.ClockTo.Value,24)),"Expedition runs from "+(Day(Mathf.Repeat(session.ClockFrom.Value,24))?"day into night":"night into day")+" ("+session.ClockFrom.Value.ToString("0.0")+" h to "+Mathf.Repeat(session.ClockTo.Value,24).ToString("0.0")+" h)");
                    yield return MoveTo(player,BoatLayout.TerminalStand,PlayerZone.Boat,0);GameHUD.Instance.Open(ActionKind.Terminal);yield return new WaitForSecondsRealtime(.6f);
                    Check(GameHUD.Instance.TerminalMapShowing,"Boat computer shows the live deck map");GameHUD.Instance.OpenSettings();
                    var heavy=session.SpawnItem(ItemCatalog.Create(9),new Vector3(21.2f,1.5f,0));
                    yield return MoveTo(player,new Vector3(21.2f,1.05f,0),PlayerZone.Boat);yield return Act(player,ActionKind.Pickup,0,heavy.Uid);
                    Check(player.HasHeavy,"Heavy scrap uses both hands");
                    if(!player.Inventory.lightOn)yield return Act(player,ActionKind.ToggleLight);
                    Check(player.Inventory.lightOn&&player.Inventory.Torch!=null,"Belt torch stays lit with both hands full");
                    yield return Act(player,ActionKind.Climb,0);Check(player.climbing,"Boarding rope accepted heavy scrap");
                    float deadline=Time.realtimeSinceStartup+12;while(player.climbing&&Time.realtimeSinceStartup<deadline){player.StepClimb(1,.1f);yield return null;}
                    Check(!player.climbing&&player.transform.position.x<16,"Heavy scrap climbs onto sinking vessel");
                    yield return Act(player,ActionKind.Climb,0);deadline=Time.realtimeSinceStartup+12;while(player.climbing&&Time.realtimeSinceStartup<deadline){player.StepClimb(-1,.1f);yield return null;}
                    Check(!player.climbing&&player.OnBoat,"Heavy scrap returns along rope");yield return Act(player,ActionKind.Drop);
                    Check(session.Campaign.cargo.Any(i=>i.uid==heavy.Uid),"Heavy scrap put down on the boat becomes boat cargo");
                }
                // One-handed scrap from the wreck: the rack check below picks a tool up while still carrying it.
                var loose=FindObjectsByType<NetItem>(FindObjectsInactive.Exclude).Where(i=>i.Holder.Value==ulong.MaxValue&&i.transform.position.x>500&&i.Item.Definition.kind==ItemKind.Scrap).ToArray();
                var loot=loose.FirstOrDefault(i=>!i.Item.Definition.twoHands)??loose.First();
                yield return MoveTo(player,loot.transform.position+Vector3.back*.6f,PlayerZone.Interior);
                yield return Act(player,ActionKind.Pickup,0,loot.Uid);Check(player.Inventory.items.Any(i=>i.uid==loot.Uid),"Pickup authoritative "+iteration);
                player.Inventory.selected=player.Inventory.items.FindIndex(i=>i.uid==loot.Uid);player.PublishInventory();
                // Anything dropped aboard the small boat is Safe Cargo, where it lands.
                yield return MoveTo(player,new Vector3(25,1.05f,-2),PlayerZone.Boat,0);
                int before=session.Campaign.cargo.Count;yield return Act(player,ActionKind.Drop);
                Check(session.Campaign.cargo.Count==before+1&&session.Campaign.cargo.Any(i=>i.uid==loot.Uid&&BoatLayout.OnDeck(i.at)),"Dropping on the boat secures safe cargo "+iteration);
                if(iteration==0) {
                    yield return Act(player,ActionKind.Pickup,0,loot.Uid);
                    Check(player.Inventory.items.Any(i=>i.uid==loot.Uid)&&session.Campaign.cargo.Count==before,"Picking deck cargo up takes it out of safe cargo");
                    int inventoryCount=player.Inventory.items.Count;yield return Act(player,ActionKind.Pickup,0,loot.Uid);
                    Check(player.Inventory.items.Count==inventoryCount,"Deck pickup cannot duplicate items");
                    // Tool rack in the cabin: tools only, one per shelf, kept as safe cargo.
                    yield return MoveTo(player,BoatLayout.RackStand,PlayerZone.Boat,180);
                    player.Inventory.selected=player.Inventory.items.FindIndex(i=>i.uid==loot.Uid);player.PublishInventory();yield return Act(player,ActionKind.Rack);
                    Check(!session.Campaign.cargo.Any(i=>i.rack>0&&i.uid==loot.Uid),"Scrap cannot go on the tool rack");
                    var torch=player.Inventory.items.Find(i=>i.Definition.kind==ItemKind.Radio);
                    player.Inventory.selected=player.Inventory.items.IndexOf(torch);player.PublishInventory();yield return Act(player,ActionKind.Rack);
                    Check(session.Campaign.cargo.Any(i=>i.uid==torch.uid&&i.rack>0)&&!player.Inventory.items.Any(i=>i.uid==torch.uid),"Tool stored on a rack slot");
                    yield return Act(player,ActionKind.Pickup,0,torch.uid);
                    Check(player.Inventory.items.Any(i=>i.uid==torch.uid)&&!session.Campaign.cargo.Any(i=>i.uid==torch.uid),"Tool taken back from the rack");
                    player.Inventory.selected=player.Inventory.items.IndexOf(player.Inventory.items.Find(i=>i.uid==torch.uid));player.PublishInventory();yield return Act(player,ActionKind.Rack);
                    // Boat computer: shut a hazard down, then the cooldown refuses a repeat.
                    if(WorldZone.Interior.hazards.Count>0) {
                        yield return MoveTo(player,BoatLayout.TerminalStand,PlayerZone.Boat,0);yield return Act(player,ActionKind.HazardOff,0);
                        Check(session.HazardSuppressed(0),"Boat computer shuts a hazard down");double until=session.HazardOffUntil(0);
                        yield return Act(player,ActionKind.HazardOff,0);Check(session.HazardOffUntil(0)==until,"Hazard control respects its cooldown");
                    } else Check(true,"No hazards in this layout; hazard control skipped");
                    yield return MoveTo(player,new Vector3(25,1.05f,-2),PlayerZone.Boat,0);
                    player.Inventory.selected=player.Inventory.items.FindIndex(i=>i.uid==loot.Uid);player.PublishInventory();yield return Act(player,ActionKind.Drop);
                    Check(session.Campaign.cargo.Any(i=>i.uid==loot.Uid),"Scrap dropped back on deck");
                    var secured=session.Campaign.cargo.Select(i=>i.uid).ToArray();
                    session.Started.Value=app.TimeNow-ShipTimeline.At(ShipTimeline.DeckLevel,app.balance.duration);yield return new WaitForSecondsRealtime(.5f);
                    Check(Mathf.Abs(WorldZone.Exterior.ship.position.y+9)<.2f&&Mathf.Abs(WorldZone.Interior.water.position.y-9)<.2f,"Exterior deck alignment and interior flooding share clock");
                    Check(secured.Length>0&&session.Campaign.cargo.Any(i=>i.rack>0),"Boat holds deck cargo and a racked tool before the wipe");
                    foreach(var crew in NetPlayer.All.ToArray())crew.Kill();
                    yield return Wait(()=>session.Phase.Value==GamePhase.Ready||session.Phase.Value==GamePhase.Harbor,30,"Wipe extraction");
                    Check(session.Campaign.cargo.Count==0,"Full team wipe loses everything on the small boat (deck and rack)");
                    var onDeck=FindObjectsByType<NetItem>(FindObjectsInactive.Exclude).Select(i=>i.Uid).ToHashSet();
                    Check(!secured.Any(onDeck.Contains),"Lost boat cargo is removed from the deck");
                    var wiped=CampaignSave.Read(app.SavePath,out _);Check(wiped.cargo.Count==0,"The checkpoint after a wipe holds no boat cargo");
                } else {
                    session.Started.Value=app.TimeNow-app.balance.duration+.3f;yield return new WaitForSecondsRealtime(.6f);
                    yield return Wait(()=>!session.InExpedition&&session.Phase.Value!=GamePhase.Loading,30,"Automatic extraction");
                    Check(session.Campaign.cargo.Count==before+1,"Automatic departure preserves cargo "+iteration);
                }
                session.Campaign.expeditionsUsed=0;GameHUD.Instance.Close();
            }
            session.Campaign.expeditionsUsed=3;
            yield return MoveTo(player,BoatLayout.HelmStand,PlayerZone.Boat,0);yield return Act(player,ActionKind.TravelIsland);
            yield return Wait(()=>session.Phase.Value==GamePhase.Harbor,40,"Sail to the island after the third expedition");
            Check(session.Arrival.Value==2,"Island arrival cutscene is started for every client");
            if(expectedPlayers>1) {yield return ViewUntil("island-arrival",v=>v.cinematic,3);Check(views.All(v=>v.cinematic),"The island arrival cutscene plays on every client");}
            yield return MoveTo(player,new Vector3(44,1.05f,-9),PlayerZone.Island);
            var ids=string.Join(",",session.Campaign.cargo.Select(i=>i.uid));var scrapIds=session.Campaign.cargo.Where(i=>i.Definition.kind==ItemKind.Scrap).Select(i=>i.uid).ToArray();
            yield return Act(player,ActionKind.Sell,0,ids);
            Check(!session.Campaign.cargo.Any(i=>i.Definition.kind==ItemKind.Scrap)&&session.Campaign.sold>0,"Cargo selection sale at island");int credits=session.Campaign.credits;
            Check(!FindObjectsByType<NetItem>(FindObjectsInactive.Exclude).Any(i=>scrapIds.Contains(i.Uid)),"Sold scrap leaves the boat deck");
            yield return Act(player,ActionKind.Sell,0,ids);Check(session.Campaign.credits==credits,"Repeated sale cannot duplicate money");
            var saved=CampaignSave.Read(app.SavePath,out _);
            Check(saved.credits==credits&&saved.cargo.Count==session.Campaign.cargo.Count&&saved.cargo.All(i=>i.rack>0||BoatLayout.OnDeck(i.at)),"Safe checkpoint round trip keeps deck positions and rack slots");
            GameHUD.Instance.Close();yield return MoveTo(player,WorldZone.BoatSpawn,PlayerZone.Boat);NetEnemy.GrabChanceOverride=null;
        }
    }
}
