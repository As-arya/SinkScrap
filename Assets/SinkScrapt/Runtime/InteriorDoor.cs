using UnityEngine;

namespace SinkScrapt
{
    // Look of a room door (visual and sound only): a grey watertight door with a porthole, a varnished plank door with a
    // porthole, a rusted heavy watertight door, or a painted cabin door with a small window (Models/Kit/kit_door_*).
    public enum DoorStyle { Steel, Wood, Rusted, Cabin }

    // A hinged door in a room doorway: one leaf hung on one jamb (the side is picked per door). The host owns the state
    // (NetSession.DoorStates: open, unlocked); every peer animates the leaf from it. The leaf swings 90 degrees into the room
    // and then stands across the wall, 0.55 m off the door centre and 1.05 m deep: clear of the monster's body on the
    // navigation line through the door centre (0.42 m radius), of the corner furniture (from 0.95 m off the centre) and of
    // loot (at least 1.45 m from any wall). Colliders are only on while the leaf is at rest, so a swing never traps anyone.
    public class InteriorDoor : MonoBehaviour
    {
        // The leaf models are 1.04 m wide, hung 3 cm off the hinge jamb in the 1.1 m opening.
        public const float LeafGap=.03f,LeafWidth=1.04f;
        public int index;
        public bool locked;
        public DoorStyle style;
        // Per hinge: the closed yaw (0 or 180 about the door root) and the opening angle (+-90).
        public float[] closedYaw,openAngles;
        public Transform[] hinges;
        public BoxCollider[] blocks;
        public WorldInteractable[] targets;
        public Renderer[] lampRed,lampGreen;
        public TMPro.TextMeshPro[] labels;
        public float leafWidth=LeafWidth;
        float open;
        public bool IsOpen => NetSession.Instance!=null&&NetSession.Instance.DoorOpen(index);
        public bool IsLocked => locked&&(NetSession.Instance==null||!NetSession.Instance.DoorUnlocked(index));
        // 0 closed .. 1 fully open (the animation, not the state).
        public float Opening => open;
        void Update()
        {
            bool isOpen=IsOpen,shut=IsLocked;
            float goal=isOpen?1:0;open=Mathf.MoveTowards(open,goal,Time.deltaTime*2.2f);
            for(int i=0;i<hinges.Length;i++)hinges[i].localRotation=Quaternion.Euler(0,closedYaw[i]+openAngles[i]*Mathf.SmoothStep(0,1,open),0);
            bool rest=Mathf.Approximately(open,goal);foreach(var block in blocks)block.enabled=rest;
            if(lampRed!=null)foreach(var lamp in lampRed)lamp.enabled=shut;
            if(lampGreen!=null)foreach(var lamp in lampGreen)lamp.enabled=!shut;
            if(labels!=null)foreach(var label in labels)label.enabled=shut;
            foreach(var target in targets)target.title=shut?"Pintu terkunci":isOpen?"Tutup pintu":"Buka pintu";
        }
    }

    public static partial class WorldFactory
    {
        static readonly string[] DoorModels={"Kit/kit_door_steel","Kit/kit_door_wood","Kit/kit_door_rusted","Kit/kit_door_cabin"};
        static readonly Color[] DoorFallback={new(.55f,.58f,.57f),new(.55f,.36f,.2f),new(.5f,.3f,.2f),new(.74f,.71f,.6f)};
        // The style of door `index`, from the room it opens into: plank and painted cabin doors for crew cabins and the galley,
        // rusted watertight doors for holds and machinery, grey watertight doors elsewhere, mixed per door so a deck is not
        // stamped out of one piece. Deterministic per blueprint, so every peer (and the host's door sounds) agree.
        public static DoorStyle DoorStyleOf(InteriorLayout layout,int index)
        {
            var room=layout.DoorRoom(index);float roll=Hash01(layout.seed,index,17);
            return (room?.type??1) switch {
                2=>roll<.6f?DoorStyle.Wood:DoorStyle.Cabin,
                // Cruise ship: passenger rooms behind painted or varnished doors, service rooms behind steel.
                CruiseLayout.Cabin or CruiseLayout.Suite=>roll<.65f?DoorStyle.Cabin:DoorStyle.Wood,
                CruiseLayout.Restaurant or CruiseLayout.Theatre or CruiseLayout.Bar or CruiseLayout.Casino or CruiseLayout.Boutique=>roll<.6f?DoorStyle.Wood:DoorStyle.Cabin,
                CruiseLayout.Clinic or CruiseLayout.Laundry=>roll<.6f?DoorStyle.Steel:DoorStyle.Cabin,
                3=>roll<.5f?DoorStyle.Cabin:DoorStyle.Wood,
                4 or 5=>roll<.7f?DoorStyle.Rusted:DoorStyle.Steel,
                6=>roll<.6f?DoorStyle.Steel:DoorStyle.Cabin,
                _=>roll<.55f?DoorStyle.Steel:roll<.85f?DoorStyle.Rusted:DoorStyle.Wood,
            };
        }
        public static bool WoodDoor(InteriorLayout layout,int index) => DoorStyleOf(layout,index)==DoorStyle.Wood;

        // Door `index` of the blueprint, centred on its wall. Swings into the room side (for a door between two rooms, the far one).
        // Null for an arch: it has no leaf (WorldZone.doors keeps blueprint indices, so the slot stays empty).
        static InteriorDoor Door(WorldZone zone,InteriorLayout layout,int index)
        {
            var d=layout.doors[index];if(d.arch)return null;var n=Dir3(d.dir);var along=new Vector3(-n.z,0,n.x);
            var near=layout.Kind(d.floor,d.x,d.z,out _);var far=new Vector2Int(d.x,d.z)+ShipGrid.Dirs[d.dir];
            bool intoNear=near==CellKind.Room&&layout.Kind(d.floor,far.x,far.y,out _)!=CellKind.Room;
            var swing=intoNear?-n:n;
            // Root at the door centre with local +x along the wall.
            var root=new GameObject("Door "+index);root.transform.SetParent(zone.transform,false);
            var rootRotation=Quaternion.Euler(0,Mathf.Atan2(-along.z,along.x)*Mathf.Rad2Deg,0);
            root.transform.localPosition=layout.DoorCenter(index);root.transform.localRotation=rootRotation;
            var door=root.AddComponent<InteriorDoor>();door.index=index;door.locked=d.locked;door.style=DoorStyleOf(layout,index);
            float half=ShipGrid.DoorWidth*.5f,width=InteriorDoor.LeafWidth,height=ShipGrid.DoorHeight-.05f;
            // The hinge jamb (-1 or +1 along the wall); closed, the leaf's local +x points across the opening.
            float s=Hash01(layout.seed,index,29)<.5f?-1:1;float closedYaw=s<0?0:180;var closed=Quaternion.Euler(0,closedYaw,0);
            var hinge=new GameObject("Hinge").transform;hinge.SetParent(root.transform,false);hinge.localPosition=new Vector3(s*half,0,0);hinge.localRotation=closed;
            var leaf=new GameObject("Leaf").transform;leaf.SetParent(hinge,false);leaf.localPosition=new Vector3(InteriorDoor.LeafGap,0,0);
            // kit_door_*: hinge axis at the model origin, the leaf running along +x, bottom 7 cm above the floor (over the sill).
            if(!Model(DoorModels[(int)door.style],leaf,Vector3.zero,Quaternion.identity))
                Surfaces.Block("Door leaf",leaf,new Vector3(width*.5f,height*.5f+.04f,0),new Vector3(width,height-.04f,.07f),door.style==DoorStyle.Rusted?Surface.RustPlate:door.style==DoorStyle.Wood?Surface.Wood:Surface.Plate,DoorFallback[(int)door.style],false);
            var collider=leaf.gameObject.AddComponent<BoxCollider>();collider.center=new Vector3(width*.5f,ShipGrid.DoorHeight*.5f,0);collider.size=new Vector3(width,ShipGrid.DoorHeight,.08f);
            var interact=leaf.gameObject.AddComponent<WorldInteractable>();interact.action=ActionKind.Door;interact.argument=index;interact.title="Buka pintu";
            door.hinges=new[]{hinge};door.blocks=new[]{collider};door.targets=new[]{interact};door.closedYaw=new[]{closedYaw};
            // Open, the leaf's +x must point into the room: +90 if that turn takes it there, otherwise -90.
            door.openAngles=new[]{Vector3.Dot(rootRotation*closed*Quaternion.Euler(0,90,0)*Vector3.right,swing)>0?90f:-90f};
            if(d.locked) {
                // Lock lamps on both faces above the handle: red while locked, green once a key has opened it.
                var red=new System.Collections.Generic.List<Renderer>();var green=new System.Collections.Generic.List<Renderer>();
                foreach(float face in new[]{-1f,1f}) {
                    red.Add(Shape("Lock lamp",PrimitiveType.Cube,leaf,new Vector3(width-.13f,1.3f,face*.05f),new Vector3(.07f,.07f,.025f),new Color(1,.12f,.08f),false,true).GetComponent<Renderer>());
                    green.Add(Shape("Lock lamp",PrimitiveType.Cube,leaf,new Vector3(width-.13f,1.3f,face*.052f),new Vector3(.07f,.07f,.025f),new Color(.2f,1,.35f),false,true).GetComponent<Renderer>());
                    // A padlocked hasp across the gap to the jamb.
                    Box("Hasp",leaf,new Vector3(width-.02f,1.12f,face*.05f),new Vector3(.14f,.05f,.012f),new Color(.2f,.2f,.2f),false);
                    Box("Padlock",leaf,new Vector3(width+.03f,1.06f,face*.065f),new Vector3(.06f,.08f,.03f),new Color(.62f,.47f,.22f),false);
                }
                door.lampRed=red.ToArray();door.lampGreen=green.ToArray();
                // The warning sits across the closed leaf (below the room sign), readable from either side; it hides once unlocked.
                Text("TERKUNCI",root.transform,new Vector3(0,1.78f,-.11f),.03f,new Color(.9f,.3f,.2f),Quaternion.identity);
                Text("TERKUNCI",root.transform,new Vector3(0,1.78f,.11f),.03f,new Color(.9f,.3f,.2f),Quaternion.Euler(0,180,0));
                door.labels=root.GetComponentsInChildren<TMPro.TextMeshPro>();
            }
            return door;
        }
    }
}
