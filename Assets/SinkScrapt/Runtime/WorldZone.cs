using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SinkScrapt
{
    public enum WorldZoneKind { SmallBoat, Island, Exterior, Interior }
    public class WorldZone : MonoBehaviour
    {
        public WorldZoneKind kind;
        public static WorldZone Boat, Island, Exterior, Interior;
        public static readonly Vector3 InteriorOffset=new(1000,0,0);
        public Transform ship;
        public Transform water;
        public LineRenderer rope;
        // Small boat only: the boarding gate in the starboard rail. It stands open while the boat is moored at the island dock
        // and closes at sea, so the rail is unbroken during an expedition.
        public Transform gate;
        public Collider gateBlock;
        float gateOpen;
        public readonly List<Vector3> nodes=new();
        public readonly List<NavKind> nodeKinds=new();
        public readonly List<(int a,int b)> links=new();
        public readonly List<WorldHazard> hazards=new();
        // Interior only: the physical doors (index = blueprint door index) and which navigation node sits in each doorway.
        public readonly List<InteriorDoor> doors=new();
        public readonly Dictionary<int,int> doorNodes=new();
        // Interior only: the blueprint it was built from, graph node per grid cell (key deck*Columns*Rows+x+z*Columns), each
        // stair's entry/exit cell nodes, and the arrival node of each entry.
        public InteriorLayout layout;
        public readonly Dictionary<int,int> cellNodes=new();
        public readonly List<(int entry,int exit)> stairRoutes=new();
        public readonly int[] airlockNodes={-1,-1,-1};
        List<int>[] adjacency;
        string builtLayout;
        public void ClearNavigation() {nodes.Clear();nodeKinds.Clear();links.Clear();cellNodes.Clear();stairRoutes.Clear();doorNodes.Clear();for(int i=0;i<airlockNodes.Length;i++)airlockNodes[i]=-1;adjacency=null;}
        public int AddNode(Vector3 position,NavKind kind) {nodes.Add(position);nodeKinds.Add(kind);return nodes.Count-1;}
        public void Link(int a,int b) => links.Add((a,b));
        public void FinishNavigation()
        {
            adjacency=new List<int>[nodes.Count];for(int i=0;i<nodes.Count;i++)adjacency[i]=new List<int>();
            foreach(var (a,b) in links) {adjacency[a].Add(b);adjacency[b].Add(a);}
        }
        // Corridor and room floor (not doorways, stairs or airlocks): where monsters spawn and patrol to.
        public bool IsOpenFloor(int node) => node>=0&&node<nodeKinds.Count&&(nodeKinds[node]==NavKind.Corridor||nodeKinds[node]==NavKind.Room);
        public int CellNode(int deck,int x,int z) => cellNodes.TryGetValue(deck*ShipGrid.Columns*ShipGrid.Rows+x+z*ShipGrid.Columns,out int node)?node:-1;
        void Awake()
        {
            switch(kind) {
                case WorldZoneKind.SmallBoat: Boat=this; WorldFactory.SmallBoat(this); break;
                case WorldZoneKind.Island: Island=this; WorldFactory.Island(this); break;
                case WorldZoneKind.Exterior: Exterior=this; WorldFactory.Exterior(this); break;
                case WorldZoneKind.Interior: Interior=this; transform.position=InteriorOffset; break;
            }
        }
        void Update()
        {
            var session=NetSession.Instance;
            if(session==null) return;
            if(kind==WorldZoneKind.Interior && !string.IsNullOrEmpty(session.BlueprintCode) && session.BlueprintCode!=builtLayout) {
                builtLayout=session.BlueprintCode; WorldFactory.Interior(this,InteriorLayout.Unpack(builtLayout));session.NotifyLayoutBuilt();
            }
            if(kind==WorldZoneKind.Exterior && ship!=null) ship.localPosition=Vector3.down*session.Depth;
            if(kind==WorldZoneKind.Interior && water!=null) water.localPosition=new Vector3(0,session.FloodLevel,0);
            if(kind==WorldZoneKind.SmallBoat && rope!=null) {
                bool active=Exterior!=null && session.InExpedition;
                rope.gameObject.SetActive(active);
                if(active) {rope.positionCount=RopeSegments+1;for(int i=0;i<=RopeSegments;i++)rope.SetPosition(i,RopePoint(i/(float)RopeSegments));}
            }
            if(kind==WorldZoneKind.SmallBoat && gate!=null) {
                bool docked=Island!=null;gateOpen=Mathf.MoveTowards(gateOpen,docked?1:0,Time.deltaTime*1.5f);
                gate.localRotation=Quaternion.Euler(0,-90*Mathf.SmoothStep(0,1,gateOpen),0);if(gateBlock!=null)gateBlock.enabled=gateOpen<.5f;
            }
        }
        public static Vector3 BoatSpawn => new(24,1.05f,-5);
        // The boarding rope runs from the port rail (x 20) to the wreck; the rescue ladder hangs off the starboard hull (x 30).
        public static Vector3 RopeBottom => new(19.9f,1.1f,0);
        public static Vector3 RopeTop => new(15.5f,10.2f-(NetSession.Instance?.Depth??0),0);
        // The rope has a fixed length (a little slack at the start). As the wreck sinks toward the boat its ends close in and
        // the slack hangs as a belly below the chord, on the downhill side. Everything derives from the shared depth, so every
        // peer draws (and climbs) the same curve.
        public const int RopeSegments=18;
        static readonly float RopeLength=Vector3.Distance(new Vector3(19.9f,1.1f,0),new Vector3(15.5f,10.2f,0))*1.035f;
        public static Vector3 RopePoint(float t)
        {
            var a=RopeBottom;var b=RopeTop;var chord=b-a;float span=chord.magnitude;var axis=chord/Mathf.Max(span,.01f);
            float sag=Mathf.Min(1.5f,Mathf.Sqrt(Mathf.Max(0,3*span*(RopeLength-span)/8)));
            var down=Vector3.down-axis*Vector3.Dot(Vector3.down,axis);
            down=down.sqrMagnitude>1e-4f?down.normalized:Vector3.down;
            return Vector3.Lerp(a,b,t)+down*sag*4*t*(1-t);
        }
        public static Vector3 RescueBottom => new(31,-1.5f,1);
        public static Vector3 RescueTop => new(29.4f,1.1f,1);
        // Deck doors on the wreck: A in the bridge house (stern), B in the forecastle cabin (bow), C a deck hatch amidships.
        static readonly Vector3[] ExteriorDoors={new(0,10.05f,-31),new(0,10.05f,31),new(0,10.05f,-1.1f)};
        // Where a diver coming back out of entry `index` stands, beside its door, and which way they face.
        static readonly Vector3[] ExteriorExits={new(0,0,3),new(0,0,-3),new(0,0,2.4f)};
        static readonly float[] ExteriorFacing={0,180,0};
        public static Vector3 ExteriorEntry(int index) => ExteriorDoors[Mathf.Clamp(index,0,2)]+Vector3.down*(NetSession.Instance?.Depth??0);
        public static Vector3 ExteriorExit(int index) => ExteriorEntry(index)+ExteriorExits[Mathf.Clamp(index,0,2)];
        public static float ExteriorExitFacing(int index) => ExteriorFacing[Mathf.Clamp(index,0,2)];
        // Inside entry `index` (airlock flight or hatch vestibule); where it is comes from the current blueprint.
        public static Vector3 InteriorEntry(int index)
        {
            var layout=Interior!=null?Interior.layout:null;
            var local=layout!=null&&index<layout.entries.Count?layout.EntryPosition(index):new Vector3(0,ShipTimeline.CargoDeck,-(ShipGrid.HalfLength+ShipGrid.AirlockEntry));
            return InteriorOffset+local+Vector3.up*.05f;
        }
        public static float InteriorFacing(int index) {var layout=Interior!=null?Interior.layout:null;return layout!=null&&index<layout.entries.Count?layout.EntryFacing(index):0;}
        // A doorway node is closed to everyone while its door is still locked.
        public bool Passable(int node) => !doorNodes.TryGetValue(node,out int door)||NetSession.Instance==null||!NetSession.Instance.DoorLockedNow(door);
        public int NearestNode(Vector3 position,bool wet,float depth)
        {
            int best=-1; float distance=float.MaxValue;
            for(int i=0;i<nodes.Count;i++) {
                bool eligible=wet?nodes[i].y+0.8f<depth:nodes[i].y+1.65f>depth;
                var d=(transform.TransformPoint(nodes[i])-position).sqrMagnitude;
                if(eligible&&d<distance) {distance=d;best=i;}
            }
            return best;
        }
        // Every eligible node within `slack` metres of the nearest one, so callers can compare several possible starting points.
        public List<int> NodesNear(Vector3 position,bool wet,float depth,float slack)
        {
            var distances=new float[nodes.Count];float nearest=float.MaxValue;
            for(int i=0;i<nodes.Count;i++) {
                bool eligible=wet?nodes[i].y+0.8f<depth:nodes[i].y+1.65f>depth;
                distances[i]=eligible?(transform.TransformPoint(nodes[i])-position).magnitude:float.MaxValue;nearest=Mathf.Min(nearest,distances[i]);
            }
            var result=new List<int>();for(int i=0;i<nodes.Count;i++)if(distances[i]<=nearest+slack)result.Add(i);return result;
        }
        public List<int> FindPath(int start,int end,bool wet,float depth)
        {
            var path=new List<int>(); if(start<0||end<0) return path;
            if(adjacency==null||adjacency.Length!=nodes.Count) FinishNavigation();
            var previous=new int[nodes.Count]; Array.Fill(previous,-1);
            var queue=new Queue<int>(); queue.Enqueue(start); previous[start]=start;
            while(queue.Count>0) {
                int n=queue.Dequeue(); if(n==end) break;
                foreach(int next in adjacency[n]) {
                    if(previous[next]>=0||!Passable(next)) continue;
                    bool eligible=wet?nodes[next].y+.8f<depth:nodes[next].y+1.65f>depth;
                    if(!eligible) continue;
                    previous[next]=n;queue.Enqueue(next);
                }
            }
            if(previous[end]<0) return path;
            for(int n=end;n!=start;n=previous[n]) path.Insert(0,n);
            return path;
        }
    }

    public static partial class WorldFactory
    {
        static readonly Dictionary<string,Material> materials=new();
        public static readonly Color Hull=new(.13f,.20f,.22f), Steel=new(.24f,.31f,.32f), Rust=new(.48f,.23f,.13f), Teal=new(.19f,.49f,.47f), Cream=new(.74f,.71f,.56f);
        public static Material Material(Color color,bool emissive=false)
        {
            string key=color.ToString()+emissive;
            if(materials.TryGetValue(key,out var cached)&&cached!=null) return cached;
            var source=Resources.Load<Material>(emissive?"SinkScrapt/Emission":"SinkScrapt/Lit");
            var mat=source!=null?new Material(source):new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.color=color;
            mat.SetFloat("_Smoothness",.25f);
            if(emissive) {mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",color*2);}
            materials[key]=mat;return mat;
        }
        // Porthole and window glass: URP Lit, alpha-blended (color.a is the opacity) and glossy, so a torch glints on it and
        // the far side shows through. Cloned from Resources/SinkScrapt/Glass.mat (Build Atmosphere), which keeps the
        // transparent shader variant in the build.
        public static Material Glass(Color color)
        {
            string key="glass"+color;
            if(materials.TryGetValue(key,out var cached)&&cached!=null) return cached;
            var source=Resources.Load<Material>("SinkScrapt/Glass");
            var mat=source!=null?new Material(source):new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",.92f);mat.SetFloat("_BlendModePreserveSpecular",0);
            materials[key]=mat;return mat;
        }
        public static GameObject Shape(string name,PrimitiveType type,Transform parent,Vector3 position,Vector3 scale,Color color,bool collider=true,bool emissive=false)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=Material(color,emissive);
            if(!collider) UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            return go;
        }
        public static GameObject Box(string name,Transform parent,Vector3 pos,Vector3 scale,Color color,bool collider=true) => Shape(name,PrimitiveType.Cube,parent,pos,scale,color,collider);
        // Places a Blender model (ModelLibrary, Docs/BlenderAssets.md) and reports whether it exists, so the caller can skip its
        // primitive stand-in. Gameplay colliders never come from the model.
        static bool Model(string path,Transform parent,Vector3 position,Quaternion rotation,Vector3? scale=null,Color? tint=null) => ModelLibrary.Spawn(path,parent,position,rotation,tint,scale)!=null;
        // Keeps a block's collider but stops drawing it (a model draws that piece instead).
        static GameObject Hide(GameObject go) {foreach(var r in go.GetComponentsInChildren<Renderer>())r.enabled=false;return go;}
        // TextMeshPro text is readable from -Z. Hub signs are read by players walking toward -Z, so the default faces +Z.
        static readonly Quaternion FacePlusZ=Quaternion.Euler(0,180,0);
        static void Text(string value,Transform parent,Vector3 pos,float size,Color color,Quaternion? rotation=null)
        {
            var go=new GameObject("Sign "+value);go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localRotation=rotation??FacePlusZ;
            var text=go.AddComponent<TMPro.TextMeshPro>();text.font=Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");text.text=value;text.fontSize=36;text.alignment=TMPro.TextAlignmentOptions.Center;text.color=color;text.textWrappingMode=TMPro.TextWrappingModes.NoWrap;text.overflowMode=TMPro.TextOverflowModes.Overflow;
            text.rectTransform.sizeDelta=new Vector2(60,12);go.transform.localScale=Vector3.one*size*2;
        }
        static void Station(Transform parent,Vector3 pos,string title,ActionKind action,Color color,int argument=0,bool island=false,float sign=1)
        {
            var go=Box(title,parent,pos,new Vector3(1.2f,.9f,.8f),color);
            var interact=go.AddComponent<WorldInteractable>();interact.title=title;interact.action=action;interact.argument=argument;interact.islandOnly=island;
            Text(title,parent,pos+new Vector3(0,sign,0),.05f,Cream);
        }
        public static void ConfigureEnvironment()
        {
            RenderSettings.fog=true;RenderSettings.fogColor=OutdoorFog;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.006f;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.38f,.44f,.55f);RenderSettings.ambientEquatorColor=new Color(.27f,.32f,.35f);RenderSettings.ambientGroundColor=new Color(.11f,.13f,.13f);RenderSettings.ambientIntensity=1f;
            var sky=Resources.Load<Material>("SinkScrapt/Sky");if(sky!=null)RenderSettings.skybox=sky;
        }
        // OutdoorFog, OutdoorFogDensity and OutdoorAmbient follow the time of day (SkyClock): a grey sea haze by day that
        // thickens at night, so the wreck looms out of it. Inside the wreck it is close to black.
        public static Color OutdoorFog=new(.17f,.21f,.25f);
        public static float OutdoorAmbient=1,OutdoorFogDensity=.011f;
        public static readonly Color InteriorFog=new(.006f,.011f,.013f),UnderwaterFog=new(.02f,.18f,.17f);
        // Warm sodium lamp on a post: emissive bulb plus a flickering point light.
        static void HarborLamp(Transform parent,Vector3 pos,float range=10,float intensity=2.4f)
        {
            Box("Lamp post",parent,new Vector3(pos.x,(pos.y+.9f)/2,pos.z),new Vector3(.12f,pos.y-.9f,.12f),Steel,false);
            var bulb=Shape("Lamp bulb",PrimitiveType.Sphere,parent,pos,Vector3.one*.28f,new Color(1,.78f,.45f),false,true);
            var light=Fx.Glow(bulb.transform,Vector3.zero,new Color(1,.62f,.30f),range,intensity);light.gameObject.AddComponent<LightFlicker>().depth=.08f;
        }
        static void NavLight(Transform parent,Vector3 pos,Color color,float range=7,float intensity=2.2f)
        {
            var bulb=Shape("Nav light",PrimitiveType.Sphere,parent,pos,Vector3.one*.22f,color,false,true);Fx.Glow(bulb.transform,Vector3.zero,color,range,intensity);
        }
        public static GameObject ItemVisual(Transform parent,int definitionId)
        {
            var def=ItemCatalog.Get(definitionId);var root=new GameObject(def.title);root.transform.SetParent(parent,false);
            if(definitionId>=0&&definitionId<ModelLibrary.ItemModels.Length&&ModelLibrary.Spawn("Items/"+ModelLibrary.ItemModels[definitionId],root.transform,Vector3.zero)!=null)return root;
            if(def.kind==ItemKind.Package) {
                // Crate (bottom-centre pivot) centred on the item, plus its canopy; NetItem folds the "Parachute" child away on landing.
                if(ModelLibrary.Spawn("Props/prop_supply_crate",root.transform,new Vector3(0,-.3f,0))==null)Box("Crate",root.transform,Vector3.zero,new Vector3(.75f,.6f,.75f),def.color,false);
                var chute=new GameObject("Parachute").transform;chute.SetParent(root.transform,false);chute.localPosition=new Vector3(0,-.3f,0);
                if(ModelLibrary.Spawn("Props/prop_parachute",chute,Vector3.zero)==null)Shape("Canopy",PrimitiveType.Sphere,chute,new Vector3(0,3.2f,0),new Vector3(3.2f,1,3.2f),new Color(.9f,.42f,.12f),false);
                // A strobe on the lifting eye so the drop can be followed at night; it folds away with the canopy.
                var strobe=Shape("Drop strobe",PrimitiveType.Sphere,chute,new Vector3(0,.78f,0),Vector3.one*.1f,new Color(1,.3f,.15f),false,true);
                Fx.Glow(strobe.transform,Vector3.zero,new Color(1,.35f,.15f),9,3).gameObject.AddComponent<LightBlink>().period=.9f;
                return root;
            }
            if(def.kind==ItemKind.Battery) {
                Shape("Cell",PrimitiveType.Cylinder,root.transform,Vector3.zero,new Vector3(.12f,.11f,.12f),def.color,false);
                Shape("Terminal",PrimitiveType.Cylinder,root.transform,new Vector3(0,.12f,0),new Vector3(.05f,.02f,.05f),Steel,false);
                Shape("Label",PrimitiveType.Cylinder,root.transform,new Vector3(0,-.03f,0),new Vector3(.125f,.04f,.125f),new Color(.1f,.1f,.1f),false);
            } else if(def.kind==ItemKind.Key) {
                // A brass key on a red tag, so it shows up under a torch beam.
                Box("Shaft",root.transform,Vector3.zero,new Vector3(.03f,.015f,.16f),def.color,false);
                Shape("Bow",PrimitiveType.Cylinder,root.transform,new Vector3(0,0,-.1f),new Vector3(.07f,.008f,.07f),def.color,false);
                Box("Bit",root.transform,new Vector3(.025f,0,.06f),new Vector3(.03f,.015f,.04f),def.color,false);
                Box("Tag",root.transform,new Vector3(0,0,-.19f),new Vector3(.07f,.01f,.09f),new Color(.75f,.12f,.08f),false);
            } else if(def.kind==ItemKind.Oxygen) {
                Shape("Tank",PrimitiveType.Capsule,root.transform,Vector3.zero,new Vector3(.3f,.6f,.3f),def.color,false);
                Box("Valve",root.transform,new Vector3(0,.62f,0),new Vector3(.15f,.1f,.15f),Cream,false);
            } else if(def.kind==ItemKind.Flashlight||def.kind==ItemKind.Flare) {
                var body=Shape("Body",PrimitiveType.Cylinder,root.transform,Vector3.zero,new Vector3(.15f,.3f,.15f),def.color,false);body.transform.localRotation=Quaternion.Euler(90,0,0);
                Shape("Lens",PrimitiveType.Sphere,root.transform,new Vector3(0,0,.3f),new Vector3(.17f,.17f,.08f),Cream,false,true);
            } else {
                Box("Body",root.transform,Vector3.zero,def.twoHands?new Vector3(.8f,.6f,.6f):new Vector3(.35f,.25f,.35f),def.color,false);
                Box("Detail",root.transform,new Vector3(0,.15f,.2f),new Vector3(.2f,.12f,.12f),Cream,false);
                if(def.twoHands) for(int n=0;n<5;n++) Box("Cooling fin",root.transform,new Vector3(-.3f+n*.15f,.4f,0),new Vector3(.04f,.2f,.6f),Steel,false);
            }
            return root;
        }
    }
}
