using System.Collections.Generic;
using UnityEngine;

namespace SinkScrapt
{
    // The small boat, the island harbour and the wreck's exterior. Every gameplay anchor (stations, rope ends, rescue ladder,
    // access doors, deck heights, collider footprints) keeps the position the network and QA code expect; the rest is dressing.
    public static partial class WorldFactory
    {
        static readonly Color Antifouling=new(.42f,.12f,.09f),OffWhite=new(.80f,.79f,.73f),WindowLit=new(1f,.78f,.45f),WindowDark=new(.05f,.08f,.09f),Yellow=new(.78f,.6f,.18f);

        // Hull with a sloped side, painted above `waterline` and antifouling red below it. UVs are in metres.
        static GameObject HullMesh(string name,Transform parent,Vector3 position,float width,float length,float bottom,float top,Color paint,float waterline)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));go.transform.SetParent(parent,false);go.transform.localPosition=position;
            var verts=new List<Vector3>();var uvs=new List<Vector2>();var upper=new List<int>();var lower=new List<int>();
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,List<int> into)
            {
                var n=Vector3.Cross(b-a,c-a);n=new Vector3(Mathf.Abs(n.x),Mathf.Abs(n.y),Mathf.Abs(n.z));int start=verts.Count;
                foreach(var v in new[]{a,b,c,d}) {verts.Add(v);uvs.Add(n.z>=n.x&&n.z>=n.y?new Vector2(v.x,v.y-bottom):n.y>=n.x?new Vector2(v.x,v.z):new Vector2(v.z,v.y-bottom));}
                into.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
            }
            var section=new Vector3[4,4];float[] zs={-length*.5f,-length*.5f+3,length*.5f-5,length*.5f+5};float[] widths={width*.38f,width*.5f,width*.5f,width*.025f};
            for(int i=0;i<4;i++){float w=widths[i];section[i,0]=new Vector3(-w*.65f,bottom,zs[i]);section[i,1]=new Vector3(w*.65f,bottom,zs[i]);section[i,2]=new Vector3(w,top,zs[i]);section[i,3]=new Vector3(-w,top,zs[i]);}
            float t=Mathf.Clamp01((waterline-bottom)/(top-bottom));
            for(int i=0;i<3;i++)for(int j=0;j<4;j++) {
                Vector3 a=section[i,j],b=section[i,(j+1)%4],c=section[i+1,(j+1)%4],d=section[i+1,j];
                if(j==0)Quad(a,b,c,d,lower);
                else if(j==2)Quad(a,b,c,d,upper);
                else if(j==1) {var a2=Vector3.Lerp(a,b,t);var d2=Vector3.Lerp(d,c,t);Quad(a,a2,d2,d,lower);Quad(a2,b,c,d2,upper);}
                else {var a2=Vector3.Lerp(a,b,1-t);var d2=Vector3.Lerp(d,c,1-t);Quad(a,a2,d2,d,upper);Quad(a2,b,c,d2,lower);}
            }
            foreach(int i in new[]{0,3}) {
                Vector3 tl=section[i,3],tr=section[i,2],br=section[i,1],bl=section[i,0];var l2=Vector3.Lerp(tl,bl,1-t);var r2=Vector3.Lerp(tr,br,1-t);
                if(i==0) {Quad(tl,tr,r2,l2,upper);Quad(l2,r2,br,bl,lower);} else {Quad(bl,br,r2,l2,lower);Quad(l2,r2,tr,tl,upper);}
            }
            var mesh=new Mesh{name=name,subMeshCount=2};mesh.SetVertices(verts);mesh.SetUVs(0,uvs);mesh.SetTriangles(upper,0);mesh.SetTriangles(lower,1);mesh.RecalculateNormals();mesh.RecalculateBounds();
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterials=new[]{Surfaces.Get(Surface.Hull,paint),Surfaces.Get(Surface.Hull,Antifouling)};go.GetComponent<MeshCollider>().sharedMesh=mesh;
            return go;
        }
        // Visual railing (posts and two bars) over an invisible collider that keeps the old solid rail footprint.
        static void Railing(Transform p,Vector3 center,Vector3 size,Color color,bool collider=true)
        {
            if(collider) {var block=new GameObject("Rail collider");block.transform.SetParent(p,false);block.transform.localPosition=center;block.AddComponent<BoxCollider>().size=size;}
            bool alongZ=size.z>size.x;float length=alongZ?size.z:size.x;var axis=alongZ?Vector3.forward:Vector3.right;float bottom=center.y-size.y*.5f;
            // ext_rail_section tiles (2 m, stretched to fit) with ext_rail_post closing the run; each section's post is at its local +X.
            if(color==OffWhite&&Mathf.Abs(size.y-1)<.05f&&ModelLibrary.Has("Exterior/Wreck/ext_rail_section")) {
                int sections=Mathf.Max(1,Mathf.RoundToInt(length/2));float seg=length/sections;var turn=Quaternion.Euler(0,alongZ?90:0,0);var postSide=turn*Vector3.right;
                var start=new Vector3(center.x,bottom,center.z)-axis*length*.5f;
                for(int i=0;i<sections;i++)Model("Exterior/Wreck/ext_rail_section",p,start+axis*(i+.5f)*seg,turn,new Vector3(seg/2,1,1));
                Model("Exterior/Wreck/ext_rail_post",p,new Vector3(center.x,bottom,center.z)-postSide*length*.5f,Quaternion.identity);
                return;
            }
            foreach(float h in new[]{.55f,1f})Surfaces.Block("Rail bar",p,new Vector3(center.x,bottom+h*size.y,center.z),alongZ?new Vector3(.06f,.06f,length):new Vector3(length,.06f,.06f),Surface.Plate,color,false);
            int posts=Mathf.Max(2,Mathf.RoundToInt(length/1.8f));
            for(int k=0;k<=posts;k++)Surfaces.Block("Rail post",p,new Vector3(center.x,center.y,center.z)+axis*(-length*.5f+k*length/posts),new Vector3(.07f,size.y,.07f),Surface.Plate,color,false);
        }
        static GameObject Window(Transform p,Vector3 pos,Vector3 size,bool lit) => Shape(lit?"Lit window":"Window",PrimitiveType.Cube,p,pos,size,lit?WindowLit:WindowDark,false,lit);
        static void Tyre(Transform p,Vector3 pos,Vector3 normal)
        {
            var tyre=Shape("Tyre fender",PrimitiveType.Cylinder,p,pos,new Vector3(.6f,.12f,.6f),new Color(.06f,.06f,.06f),false);tyre.transform.localRotation=Quaternion.FromToRotation(Vector3.up,normal);
        }
        static void Bollard(Transform p,Vector3 pos)
        {
            if(Model("Exterior/Wreck/ext_bollard",p,pos,Quaternion.identity))return;
            Shape("Bollard",PrimitiveType.Cylinder,p,pos+Vector3.up*.2f,new Vector3(.3f,.2f,.3f),DarkSteel,false);Shape("Bollard cap",PrimitiveType.Cylinder,p,pos+Vector3.up*.42f,new Vector3(.38f,.03f,.38f),DarkSteel,false);
        }

        // ---- Small boat -----------------------------------------------------------------------------------------------------
        // A 10 x 20 m workboat (BoatLayout): enclosed stern cabin with the helm, the boat computer and the tool rack, an open
        // cargo deck where anything put down is Safe Cargo, and one continuous rail with a boarding gate that only opens at the
        // island dock. The Blender model (Models/Exterior/Boat) is the look; every collider below stays in code. Without the
        // model, the same boxes are drawn as stand-ins.
        public static void SmallBoat(WorldZone zone)
        {
            ConfigureEnvironment(); var p=zone.transform;var origin=BoatLayout.Origin;
            bool model=ModelLibrary.Has("Exterior/Boat/boat_hull");
            foreach(var part in new[]{"boat_hull","boat_cabin","boat_helm","boat_terminal","boat_tool_rack"})ModelLibrary.Spawn("Exterior/Boat/"+part,p,origin);
            Solid("Salvage deck",p,new Vector3(25,.8f,-5),new Vector3(10,.4f,20.2f),Surface.Planks,new Color(.78f,.70f,.60f),model);
            var hull=HullMesh("Salvage boat hull",p,new Vector3(25,0,-5),9.8f,19.8f,-1.6f,.8f,new Color(.20f,.42f,.44f),.12f);
            if(model)hull.GetComponent<MeshRenderer>().enabled=false;
            // One closed rail round the deck (1.15 m above it, higher than a jump) except the starboard gate at z -6..-4.
            RailBlock(p,new Vector3(20.04f,1.575f,-5.05f),new Vector3(.15f,1.15f,20.1f),model);
            RailBlock(p,new Vector3(29.96f,1.575f,-10.55f),new Vector3(.15f,1.15f,9.1f),model);
            RailBlock(p,new Vector3(29.96f,1.575f,.5f),new Vector3(.15f,1.15f,9),model);
            RailBlock(p,new Vector3(25,1.575f,-15.1f),new Vector3(10,1.15f,.15f),model);
            RailBlock(p,new Vector3(25,1.575f,5),new Vector3(10,1.15f,.15f),model);
            var gate=new GameObject("Boarding gate").transform;gate.SetParent(p,false);gate.localPosition=new Vector3(29.96f,1,-6);
            if(ModelLibrary.Spawn("Exterior/Boat/boat_gate",gate,Vector3.zero)==null)Surfaces.Block("Gate",gate,new Vector3(0,.55f,1),new Vector3(.06f,1.1f,2),Surface.Plate,OffWhite,false);
            var gateBlock=new GameObject("Gate collider");gateBlock.transform.SetParent(p,false);gateBlock.transform.localPosition=new Vector3(29.96f,1.575f,-5);
            zone.gate=gate;zone.gateBlock=gateBlock.AddComponent<BoxCollider>();((BoxCollider)zone.gateBlock).size=new Vector3(.15f,1.15f,2);
            // Cabin (x 21.5..28.5, z -14..-8): walls, a 1.6 m door in the front, roof.
            var wall=new Color(.80f,.79f,.73f);
            Solid("Cabin back",p,new Vector3(25,2.3f,-13.95f),new Vector3(7,2.6f,.1f),Surface.Bulkhead,wall,model);
            foreach(float x in new[]{21.55f,28.45f})Solid("Cabin side",p,new Vector3(x,2.3f,-11),new Vector3(.1f,2.6f,6),Surface.Bulkhead,wall,model);
            foreach(float x in new[]{22.85f,27.15f})Solid("Cabin front",p,new Vector3(x,2.3f,-8.05f),new Vector3(2.7f,2.6f,.1f),Surface.Bulkhead,wall,model);
            Solid("Cabin door header",p,new Vector3(25,3.4f,-8.05f),new Vector3(1.6f,.4f,.1f),Surface.Bulkhead,wall,model);
            Solid("Cabin roof",p,new Vector3(25,3.675f,-11),new Vector3(7.3f,.15f,6.3f),Surface.Plate,OffWhite,model);
            // Helm, boat computer and tool rack. Their colliders carry the interaction.
            Interactive(Solid("KEMUDI",p,new Vector3(23.2f,1.475f,-8.62f),new Vector3(1.4f,.95f,.6f),Surface.Plate,DarkSteel,model),"KEMUDI",ActionKind.Depart);
            Text("KEMUDI",p,new Vector3(23.2f,2.2f,-8.9f),.012f,Cream,Quaternion.identity);
            Interactive(Solid("KOMPUTER",p,new Vector3(26.8f,1.6f,-8.62f),new Vector3(1.6f,1.2f,.75f),Surface.Plate,DarkSteel,model),"KOMPUTER KAPAL",ActionKind.Terminal);
            Text("KOMPUTER",p,new Vector3(26.8f,2.3f,-8.82f),.01f,Cream,Quaternion.identity);
            Interactive(Solid("RAK ALAT",p,new Vector3(25,1.9f,-13.815f),new Vector3(3.2f,1.4f,.13f),Surface.Wood,Color.white,model),"RAK ALAT",ActionKind.Rack);
            Text("RAK ALAT",p,new Vector3(25,2.49f,-13.7f),.015f,Cream);
            var screen=new GameObject("Boat computer screen").AddComponent<BoatScreen>();screen.transform.SetParent(p,false);screen.transform.localPosition=new Vector3(26.8f,1.96f,-8.94f);
            Text("SINKSCRAPT / 04",p,new Vector3(25,3.45f,-7.94f),.05f,Cream);
            Text("AREA MUATAN",p,new Vector3(25,1.75f,4.88f),.04f,Cream,Quaternion.identity);
            if(!model) {
                var wheel=Shape("Ship wheel",PrimitiveType.Cylinder,p,new Vector3(23.2f,1.78f,-9.18f),new Vector3(.6f,.03f,.6f),new Color(.45f,.3f,.18f),false);wheel.transform.localRotation=Quaternion.Euler(90,0,0);
                for(int k=0;k<3;k++)Surfaces.Block("Rack shelf",p,new Vector3(25,1.42f+k*.6f,-13.6f),new Vector3(3,.04f,.3f),Surface.Plate,DarkSteel,false);
                foreach(float side in new[]{19.8f,30.2f})foreach(float z in new[]{-9f,-3f,4.5f})Tyre(p,new Vector3(side,.55f,z),Vector3.right);
            }
            // Radar on the cabin roof mast.
            var radar=ModelLibrary.Spawn("Exterior/Boat/boat_radar",p,new Vector3(25,5.65f,-11))??Surfaces.Block("Radar",p,new Vector3(25,5.7f,-11),new Vector3(1.3f,.1f,.18f),Surface.Plate,DarkSteel,false);
            radar.AddComponent<Spin>().degreesPerSecond=70;
            // Boarding rope (port, over the rail) and rescue ladder (starboard hull).
            var hook=Solid("Panjat tali",p,new Vector3(20.55f,1.5f,0),new Vector3(.3f,1,.3f),Surface.Plate,Cream,model);
            var climb=hook.AddComponent<WorldInteractable>();climb.action=ActionKind.Climb;climb.argument=0;climb.title="Panjat ke kapal kargo";
            var rescue=Solid("Tangga penyelamatan",p,new Vector3(30.35f,.2f,1),new Vector3(.25f,2.4f,1),Surface.Plate,Teal,model);
            var ladder=rescue.AddComponent<WorldInteractable>();ladder.action=ActionKind.Climb;ladder.argument=1;ladder.title="Naik ke kapal kecil";
            // A thick hemp rope hanging in a curve (WorldZone.RopePoint), drawn with a twisted-strand texture that repeats along it.
            var ropeGo=new GameObject("Boarding rope");ropeGo.transform.SetParent(p,false);zone.rope=ropeGo.AddComponent<LineRenderer>();
            zone.rope.positionCount=WorldZone.RopeSegments+1;zone.rope.startWidth=.085f;zone.rope.endWidth=.085f;zone.rope.sharedMaterial=RopeMaterial();zone.rope.useWorldSpace=true;
            zone.rope.textureMode=LineTextureMode.Tile;zone.rope.numCornerVertices=2;zone.rope.generateLightingData=true;zone.rope.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;
            // Rope eyes: a coil on the bitt where the rope is made fast on the boat.
            for(int k=0;k<3;k++){var coil=Shape("Rope coil",PrimitiveType.Cylinder,p,new Vector3(20.6f,1.04f+k*.05f,.9f),new Vector3(.55f-k*.1f,.025f,.55f-k*.1f),new Color(.62f,.52f,.36f),false);coil.GetComponent<Renderer>().sharedMaterial=RopeMaterial();}
            var ocean=Shape("Ocean",PrimitiveType.Plane,p,Vector3.zero,new Vector3(90,1,90),new Color(.06f,.26f,.29f),false);
            ocean.GetComponent<Renderer>().sharedMaterial=Resources.Load<Material>("SinkScrapt/Water");
            var light=new GameObject("Sun");light.transform.SetParent(p,false);light.transform.rotation=Quaternion.Euler(17,-38,0);
            var sun=light.AddComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(1f,.68f,.48f);sun.intensity=1.15f;sun.shadows=LightShadows.Soft;RenderSettings.sun=sun;
            var clock=zone.gameObject.AddComponent<SkyClock>();clock.sun=sun;clock.ocean=ocean.GetComponent<Renderer>();
            // The bow (z 5..10) is walled off by the forward rail; these floors keep anything that drops in there from falling through.
            foreach(var (z,length,width) in new[]{(5.9f,1.8f,9.9f),(7.6f,1.6f,8.4f),(9f,1.2f,5.6f),(9.9f,.6f,2.6f)}) {var bow=new GameObject("Bow deck collider");bow.transform.SetParent(p,false);bow.transform.localPosition=new Vector3(25,.85f,z);bow.AddComponent<BoxCollider>().size=new Vector3(width,.3f,length);}
            // Cabin lamp, the two roof floodlights over the cargo deck, and navigation lights (port red, starboard green, mast white).
            var cabin=Fx.Glow(p,new Vector3(25,3.3f,-11),new Color(1,.7f,.4f),8,2f);cabin.gameObject.AddComponent<LightFlicker>().depth=.1f;
            foreach(float x in new[]{22.6f,27.4f})Fx.Glow(p,new Vector3(x,3.2f,-7.2f),new Color(1,.86f,.66f),11,1.7f);
            NavLight(p,new Vector3(20.1f,2.3f,4.8f),new Color(1,.18f,.15f));NavLight(p,new Vector3(29.9f,2.3f,4.8f),new Color(.2f,1,.4f));NavLight(p,new Vector3(25,5.05f,-11),new Color(1,.95f,.85f),9,1.4f);
        }
        static Material rope;
        // Three twisted strands: diagonal light bands with dark grooves, point-filtered like the rest of the kit.
        static Material RopeMaterial()
        {
            if(rope!=null)return rope;
            const int w=16,h=16;var tex=new Texture2D(w,h,TextureFormat.RGBA32,true){name="Rope",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Repeat,hideFlags=HideFlags.DontSave};
            var pixels=new Color[w*h];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++) {
                float strand=Mathf.Repeat(x+y*1.0f,w/3f)/(w/3f);float v=.55f+.45f*Mathf.Sin(strand*Mathf.PI);if(strand<.12f)v*=.35f;
                v*=.85f+.15f*Mathf.PerlinNoise(x*.7f,y*.7f);pixels[y*w+x]=new Color(.62f*v,.50f*v,.33f*v,1);
            }
            tex.SetPixels(pixels);tex.Apply(true,true);
            var source=Resources.Load<Material>("SinkScrapt/Lit");rope=source!=null?new Material(source):new Material(Shader.Find("Universal Render Pipeline/Lit"));
            rope.name="Rope";rope.SetTexture("_BaseMap",tex);rope.SetColor("_BaseColor",Color.white);rope.SetFloat("_Smoothness",.05f);
            // A faint self-glow so the boarding rope can still be found at night.
            rope.EnableKeyword("_EMISSION");rope.SetTexture("_EmissionMap",tex);rope.SetColor("_EmissionColor",new Color(.16f,.13f,.09f));
            // LineRenderer Tile mode lays one texture repeat per line width along the rope; stretch it to a few strand turns per metre.
            rope.SetTextureScale("_BaseMap",new Vector2(.5f,1));
            return rope;
        }
        // A box that always collides; it is drawn only when the boat model is missing.
        static GameObject Solid(string name,Transform parent,Vector3 position,Vector3 size,Surface surface,Color tint,bool hidden)
        {
            if(!hidden)return Surfaces.Block(name,parent,position,size,surface,tint);
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.AddComponent<BoxCollider>().size=size;return go;
        }
        static void RailBlock(Transform parent,Vector3 center,Vector3 size,bool hidden)
        {
            if(!hidden) {Railing(parent,center,size,OffWhite);return;}
            var go=new GameObject("Rail collider");go.transform.SetParent(parent,false);go.transform.localPosition=center;go.AddComponent<BoxCollider>().size=size;
        }
        static void Interactive(GameObject go,string title,ActionKind action) {var interact=go.AddComponent<WorldInteractable>();interact.title=title;interact.action=action;}
        // Dock geometry is shared by the island builder: 12 x 4 m, deck top y 1, from the boat's starboard rail (x 30) to x 42.
        const float DockX=36;

        // ---- Island ---------------------------------------------------------------------------------------------------------
        public static void Island(WorldZone zone)
        {
            var p=zone.transform;var random=new System.Random(4);
            Surfaces.Block("Island rock",p,new Vector3(48,-1,-12),new Vector3(26,3,30),Surface.Rock,Color.white);
            Surfaces.Block("Beach shelf",p,new Vector3(48.5f,-.35f,-12),new Vector3(30,1.2f,34),Surface.Sand,Color.white);
            // Broken shoreline: half-sunk boulders around the island, leaving the dock and the boat berth clear.
            for(int i=0;i<34;i++) {
                float u=(float)random.NextDouble();int edge=random.Next(4);
                var pos=edge switch {0=>new Vector3(34.5f+u*28,0,4.2f),1=>new Vector3(34.5f+u*28,0,-28.2f),2=>new Vector3(34.2f,0,-28+u*32),_=>new Vector3(62.2f,0,-28+u*32)};
                if(pos.x<37&&pos.z>-10&&pos.z<0)continue;
                float s=1.2f+(float)random.NextDouble()*2.6f;pos+=new Vector3((float)random.NextDouble()-.5f,(float)random.NextDouble()*.6f-.2f,(float)random.NextDouble()-.5f);
                var turn=Quaternion.Euler(random.Next(-25,25),random.Next(360),random.Next(-25,25));
                var boulder=Surfaces.Block("Boulder",p,pos,new Vector3(s,s*.8f,s*1.1f),Surface.Rock,new Color(.85f,.9f,.85f),true,turn);
                // isl_boulder_a..e are unit rocks shaped like this 1 x 0.8 x 1.1 box.
                if(Model("Exterior/Island/isl_boulder_"+(char)('a'+i%5),p,pos,turn,Vector3.one*s,new Color(.85f,.9f,.85f)))Hide(boulder);
            }
            Surfaces.Block("Harbor ground",p,new Vector3(48,.7f,-12),new Vector3(25,.4f,29),Surface.Concrete,new Color(.72f,.72f,.68f));
            // Dock on timber piles, with bollards and tyres on its edge. It meets the boat's boarding gate (starboard, z -6..-4).
            var dock=new List<GameObject>{Surfaces.Block("Dock",p,new Vector3(DockX,.9f,-5),new Vector3(12,.2f,4),Surface.Planks,new Color(.62f,.55f,.46f))};
            foreach(int z in new[]{-7,-3}) for(int x=32;x<=41;x+=3) dock.Add(Shape("Dock piling",PrimitiveType.Cylinder,p,new Vector3(x,-.4f,z),new Vector3(.32f,1.5f,.32f),new Color(.32f,.26f,.2f)));
            // isl_dock turned 180 degrees puts its piles on the code's piles and its ladder at the seaward (east) end.
            if(Model("Exterior/Island/isl_dock",p,new Vector3(DockX,1,-5),Quaternion.Euler(0,180,0)))foreach(var part in dock)Hide(part);
            foreach(float x in new[]{31f,35f,39f})Bollard(p,new Vector3(x,1,-6.75f));
            foreach(float x in new[]{32.5f,37.5f})Tyre(p,new Vector3(x,.55f,-7.15f),Vector3.forward);
            // Workshop: concrete slab, corrugated walls and roof, steel columns.
            var shed=new List<GameObject>{
                Surfaces.Block("Workshop floor",p,new Vector3(48,1,-13),new Vector3(15,.2f,12),Surface.Concrete,new Color(.6f,.6f,.58f)),
                Surfaces.Block("Workshop back",p,new Vector3(48,3,-19),new Vector3(15,4,.3f),Surface.Corrugated,new Color(.55f,.62f,.6f)),
                Surfaces.Block("Workshop roof",p,new Vector3(48,5,-13),new Vector3(16,.3f,13),Surface.Corrugated,new Color(.62f,.38f,.26f))};
            foreach(float x in new[]{40.6f,55.4f})shed.Add(Surfaces.Block("Workshop side",p,new Vector3(x,3,-15.5f),new Vector3(.3f,4,7),Surface.Corrugated,new Color(.55f,.62f,.6f)));
            foreach(float x in new[]{41f,55f})shed.Add(Surfaces.Block("Workshop column",p,new Vector3(x,3,-8),new Vector3(.3f,4,.3f),Surface.Plate,Steel));
            if(Model("Exterior/Island/isl_workshop",p,new Vector3(48,1.1f,-13),Quaternion.identity))foreach(var part in shed)Hide(part);
            else Surfaces.Block("Roof trim",p,new Vector3(48,4.8f,-6.55f),new Vector3(16,.3f,.12f),Surface.Plate,Teal,false);
            // The scrap buyer: an old diver in a brass helmet behind the counter (the counter carries the interaction). Tools are
            // no longer sold here; they are ordered on the boat computer and dropped by parachute.
            Station(p,NetSession.MerchantSpot,"PEDAGANG SCRAP",ActionKind.Sell,Teal,island:true,sign:2.05f);
            var merchant=ModelLibrary.Spawn("Characters/npc_diver_merchant",p,NetSession.MerchantSpot+new Vector3(0,-.5f,-1.05f));
            if(merchant!=null)merchant.AddComponent<MerchantNpc>();
            Station(p,NetSession.QuotaSpot,"SELESAIKAN KUOTA",ActionKind.ResolveQuota,Cream,island:true);
            Text("DERMAGA 04\nSALVAGE & SUPPLY",p,new Vector3(48,3.5f,-18.75f),.11f,Cream);
            // Workbench and shelving along the back wall.
            Surfaces.Block("Workbench",p,new Vector3(43.2f,1.95f,-18.3f),new Vector3(3.2f,.08f,.9f),Surface.Wood,Color.white);
            foreach(float x in new[]{41.8f,44.6f})Surfaces.Block("Bench leg",p,new Vector3(x,1.5f,-18.3f),new Vector3(.08f,.85f,.8f),Surface.Plate,DarkSteel,false);
            Surfaces.Block("Vice",p,new Vector3(42.3f,2.1f,-18.1f),new Vector3(.3f,.22f,.25f),Surface.Plate,Teal,false);
            Shelf(p,new Vector3(52.6f,1.1f,-18.5f),Quaternion.identity,3.2f,random);
            for(int i=0;i<5;i++) Surfaces.Block("Supply crate",p,new Vector3(53,1.5f,-15+i*.8f),new Vector3(1,.8f,.7f),i%2==0?Surface.Wood:Surface.Corrugated,i%2==0?Color.white:Teal);
            // Fuel tanks, a container stack and drums behind the workshop.
            foreach(float z in new[]{-22.5f,-18.8f}) {
                var tank=Shape("Fuel tank",PrimitiveType.Cylinder,p,new Vector3(58.5f,2.4f,z),new Vector3(3,1.5f,3),OffWhite);
                if(Model("Exterior/Island/isl_fuel_tank",p,new Vector3(58.5f,.9f,z),Quaternion.identity))Hide(tank);
                else Shape("Tank band",PrimitiveType.Cylinder,p,new Vector3(58.5f,3.3f,z),new Vector3(3.05f,.12f,3.05f),Antifouling,false);
            }
            Surfaces.Block("Container",p,new Vector3(39.5f,2.2f,-23.5f),new Vector3(6,2.6f,2.5f),Surface.Corrugated,new Color(.62f,.28f,.18f));
            Surfaces.Block("Container",p,new Vector3(39.8f,4.8f,-23.5f),new Vector3(6,2.6f,2.5f),Surface.Corrugated,new Color(.24f,.38f,.52f));
            for(int i=0;i<4;i++)Shape("Drum",PrimitiveType.Cylinder,p,new Vector3(44+i*.9f,1.35f,-24),new Vector3(.8f,.45f,.8f),i%2==0?Rust:Teal);
            // Lighthouse beacon with a red band and gallery.
            var tower=Shape("Harbor beacon",PrimitiveType.Cylinder,p,new Vector3(58,4.6f,0),new Vector3(2.2f,4.6f,2.2f),OffWhite);
            if(Model("Exterior/Island/isl_lighthouse",p,new Vector3(58,0,0),Quaternion.identity))Hide(tower);
            else {
                Shape("Beacon band",PrimitiveType.Cylinder,p,new Vector3(58,6.4f,0),new Vector3(2.25f,.6f,2.25f),Antifouling,false);
                Shape("Beacon gallery",PrimitiveType.Cylinder,p,new Vector3(58,9.25f,0),new Vector3(3.2f,.08f,3.2f),DarkSteel,false);
                Shape("Lamp room",PrimitiveType.Cylinder,p,new Vector3(58,9.8f,0),new Vector3(1.4f,.5f,1.4f),new Color(.1f,.12f,.12f),false);
                Shape("Beacon cap",PrimitiveType.Sphere,p,new Vector3(58,10.4f,0),new Vector3(1.5f,.8f,1.5f),Antifouling,false);
            }
            var beaconBulb=Shape("Beacon",PrimitiveType.Sphere,p,new Vector3(58,9.8f,0),Vector3.one*.8f,new Color(1,.55f,.18f),false,true);
            var beaconLight=Fx.Glow(beaconBulb.transform,Vector3.zero,new Color(1,.5f,.15f),34,5);beaconLight.gameObject.AddComponent<LightBlink>().period=2.6f;
            // Power poles with sagging wires.
            var poles=new[]{new Vector3(38,0,-1.5f),new Vector3(46,0,-1.5f),new Vector3(54,0,-1.5f),new Vector3(60.5f,0,-9)};
            foreach(var pole in poles) {
                var post=Surfaces.Block("Power pole",p,pole+Vector3.up*3.5f,new Vector3(.22f,5.2f,.22f),Surface.Wood,new Color(.55f,.45f,.38f));
                if(Model("Exterior/Island/isl_power_pole",p,pole+Vector3.up*.9f,Quaternion.identity))Hide(post);
                else Surfaces.Block("Crossarm",p,pole+Vector3.up*5.8f,new Vector3(1.4f,.12f,.12f),Surface.Wood,new Color(.55f,.45f,.38f),false);
            }
            for(int i=0;i<poles.Length-1;i++)foreach(float side in new[]{-.6f,.6f}) {
                var wire=new GameObject("Wire");wire.transform.SetParent(p,false);var line=wire.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=8;line.startWidth=line.endWidth=.035f;line.sharedMaterial=Material(new Color(.05f,.05f,.05f));
                for(int k=0;k<8;k++) {float u=k/7f;line.SetPosition(k,Vector3.Lerp(poles[i],poles[i+1],u)+new Vector3(side,5.85f-Mathf.Sin(u*Mathf.PI)*.6f,0));}
            }
            // Scrub on the rocks.
            for(int i=0;i<18;i++) {
                var at=new Vector3(36.5f+(float)random.NextDouble()*25,1.1f,i%2==0?-26.3f+(float)random.NextDouble()*1.5f:-2+(float)random.NextDouble()*3.5f);
                if(at.x<41&&at.z>-8)continue;
                float size=.5f+(float)random.NextDouble()*.6f;var tumble=new Vector3(random.Next(360),random.Next(360),random.Next(360));
                // isl_shrub_a..c are about 0.9 m clumps; they only turn about the vertical axis.
                if(!Model("Exterior/Island/isl_shrub_"+(char)('a'+i%3),p,at+Vector3.down*.15f*size,Quaternion.Euler(0,tumble.y,0),Vector3.one*size*1.1f))
                    Surfaces.Block("Scrub",p,at,Vector3.one*size,Surface.Rock,new Color(.35f,.5f,.3f),false,Quaternion.Euler(tumble));
            }
            foreach(int x in new[]{33,37,41}) HarborLamp(p,new Vector3(x,2.5f,-6.7f));
            HarborLamp(p,new Vector3(43,4.5f,-8.5f),14,2.8f);HarborLamp(p,new Vector3(53,4.5f,-8.5f),14,2.8f);HarborLamp(p,new Vector3(48,4.5f,-17.5f),12,2.2f);
        }

        // ---- Wreck exterior -------------------------------------------------------------------------------------------------
        public static void Exterior(WorldZone zone)
        {
            var root=new GameObject("Sinking cargo vessel");root.transform.SetParent(zone.transform,false);zone.ship=root.transform;var p=root.transform;
            // The hull reaches below the sea so the ship floats; the red bottom paint shows just above the water at the start.
            HullMesh("Cargo hull",p,Vector3.zero,29.8f,70,-6,10,new Color(.22f,.27f,.32f),.8f);
            // The deck plate stands 3 cm proud of the hull's top face (y 10): coplanar, the two flickered against each other.
            Surfaces.Block("Main deck",p,new Vector3(0,9.93f,0),new Vector3(31,.2f,72),Surface.DeckPaint,new Color(.48f,.55f,.46f));
            foreach(float x in new[]{-15.2f,15.2f}) {
                Railing(p,new Vector3(x,10.55f,-18),new Vector3(.15f,1,35),OffWhite);
                Railing(p,new Vector3(x,10.55f,18),new Vector3(.15f,1,35),OffWhite);
            }
            foreach(float z in new[]{-35.5f,35.5f}) Railing(p,new Vector3(0,10.55f,z),new Vector3(30,1,.15f),OffWhite);
            // Container rows, some stacked two high, with door bars on their ends.
            var containerColors=new[]{new Color(.62f,.26f,.16f),new Color(.22f,.42f,.55f),new Color(.30f,.48f,.34f),new Color(.75f,.48f,.18f),new Color(.45f,.45f,.47f),new Color(.55f,.18f,.2f),new Color(.22f,.48f,.47f),new Color(.6f,.55f,.48f)};
            // Blender models (Models/Exterior/Wreck, Docs/BlenderAssets.md) are placed at the code's anchors with no rotation; the
            // blocks they replace keep their colliders and are hidden.
            for(int i=0;i<8;i++) {
                float x=i%2==0?-7:7,z=-22+(i/2)*14;
                var box=Surfaces.Block("Freight container",p,new Vector3(x,11.4f,z),new Vector3(6,2.8f,10),Surface.Corrugated,containerColors[i]);
                if(Model("Exterior/Wreck/ext_container_40ft",p,new Vector3(x,10,z),Quaternion.identity,null,containerColors[i]))Hide(box);
                else for(int r=0;r<4;r++)Surfaces.Block("Door bar",p,new Vector3(x-2.2f+r*1.45f,11.4f,z+5.04f),new Vector3(.06f,2.6f,.06f),Surface.Plate,DarkSteel,false);
                if(i==1||i==4||i==6) {
                    var stacked=new Vector3(x,14.2f,z+(i==4?1:0));var top=Surfaces.Block("Stacked container",p,stacked,new Vector3(6,2.8f,8),Surface.Corrugated,containerColors[(i+3)%8]);
                    if(Model("Exterior/Wreck/ext_container_8m",p,stacked+Vector3.down*1.4f,Quaternion.identity,null,containerColors[(i+3)%8]))Hide(top);
                }
            }
            // Bridge house at the stern (access A): three tiers, lit windows, lifeboats, mast and funnel.
            var bridge=new[]{
                Surfaces.Block("Bridge house",p,new Vector3(0,11.5f,-32.25f),new Vector3(14,3,5.5f),Surface.Bulkhead,OffWhite),
                Surfaces.Block("Bridge tier",p,new Vector3(0,14.5f,-32.25f),new Vector3(12,3,4.5f),Surface.Bulkhead,OffWhite),
                Surfaces.Block("Wheelhouse",p,new Vector3(0,17,-32),new Vector3(15,2,4),Surface.Plate,OffWhite),
                Surfaces.Block("Bridge roof",p,new Vector3(0,18.1f,-32),new Vector3(15.4f,.2f,4.4f),Surface.Plate,DarkSteel)};
            if(Model("Exterior/Wreck/ext_bridge_house",p,new Vector3(0,10,-29.5f),Quaternion.identity))foreach(var block in bridge)Hide(block);
            else {
                for(int k=-3;k<=3;k++) {
                    if(k!=0)Window(p,new Vector3(k*2,12.2f,-29.47f),new Vector3(.9f,.6f,.06f),(k+7)%3==0);
                    Window(p,new Vector3(k*1.7f,15.2f,-29.97f),new Vector3(.9f,.6f,.06f),k%2==0);
                    Window(p,new Vector3(k*2.05f,17.3f,-29.97f),new Vector3(1.7f,.75f,.06f),k==0||k==2);
                }
                Railing(p,new Vector3(0,16.5f,-29.85f),new Vector3(12,1,.08f),OffWhite,false);
            }
            Fx.Glow(p,new Vector3(0,17,-29.3f),WindowLit,9,1.2f);
            foreach(float side in new[]{-1f,1f}) {
                // ext_lifeboat_davit is modelled for the port (-X) side; the starboard one is mirrored.
                if(Model("Exterior/Wreck/ext_lifeboat_davit",p,new Vector3(side*7.3f,14.4f,-32.2f),Quaternion.identity,new Vector3(side<0?1:-1,1,1)))continue;
                var lifeboat=Shape("Lifeboat",PrimitiveType.Capsule,p,new Vector3(side*7.3f,14.4f,-32.2f),new Vector3(1.3f,2,1.1f),new Color(.9f,.42f,.12f),false);lifeboat.transform.localRotation=Quaternion.Euler(90,0,0);
                foreach(float z in new[]{-33.6f,-30.8f})Surfaces.Block("Davit",p,new Vector3(side*6.6f,15.4f,z),new Vector3(1.6f,.15f,.15f),Surface.Plate,DarkSteel,false);
            }
            var door=Box("Interior access",p,new Vector3(0,11.2f,-29.4f),new Vector3(1.8f,2.4f,.12f),Teal);
            var portal=door.AddComponent<WorldInteractable>();portal.action=ActionKind.Portal;portal.argument=0;portal.title="Masuk palka A (utama, dek atas)";
            Text("PALKA A",p,new Vector3(0,12.75f,-29.42f),.07f,Cream,FacePlusZ);
            DressWreckDeck(p);
            if(!Model("Exterior/Wreck/ext_mast",p,new Vector3(0,18,-31),Quaternion.identity)) {
                Surfaces.Block("Mast",p,new Vector3(0,21,-31),new Vector3(.25f,6,.25f),Surface.Plate,OffWhite,false);
                Surfaces.Block("Mast yard",p,new Vector3(0,22.5f,-31),new Vector3(3,.12f,.12f),Surface.Plate,OffWhite,false);
            }
            var mastBulb=Shape("Mast light",PrimitiveType.Sphere,p,new Vector3(0,24.2f,-31),Vector3.one*.4f,new Color(1,.15f,.1f),false,true);
            Fx.Glow(mastBulb.transform,Vector3.zero,new Color(1,.15f,.1f),20,5).gameObject.AddComponent<LightBlink>().period=1.8f;
            if(!Model("Exterior/Wreck/ext_funnel",p,new Vector3(0,18.1f,-33.3f),Quaternion.identity)) {
                Shape("Funnel",PrimitiveType.Cylinder,p,new Vector3(0,19.8f,-33.3f),new Vector3(2.4f,1.7f,2.4f),Antifouling,false);
                Shape("Funnel band",PrimitiveType.Cylinder,p,new Vector3(0,20.9f,-33.3f),new Vector3(2.45f,.25f,2.45f),OffWhite,false);
            }
            var smoke=Fx.Steam(p,new Vector3(0,21.6f,-33.3f));var smokeMain=smoke.main;smokeMain.startColor=new Color(.2f,.21f,.23f,1);smokeMain.startSize=new ParticleSystem.MinMaxCurve(.8f,1.6f);smokeMain.startSpeed=new ParticleSystem.MinMaxCurve(1.2f,2.2f);smokeMain.startLifetime=new ParticleSystem.MinMaxCurve(3f,5f);
            // Forecastle cabin at the bow (access B), windlass and bow light.
            var cabinB=Surfaces.Block("Access cabin",p,new Vector3(0,11.5f,31),new Vector3(6,3,3),Surface.Bulkhead,OffWhite);
            bool cabinModel=Model("Exterior/Wreck/ext_forecastle_cabin",p,new Vector3(0,10,29.5f),Quaternion.identity);
            if(cabinModel)Hide(cabinB);else Surfaces.Block("Cabin roof",p,new Vector3(0,13.05f,31),new Vector3(6.3f,.12f,3.3f),Surface.Plate,DarkSteel,false);
            var doorB=Box("Interior access",p,new Vector3(0,11.2f,29.4f),new Vector3(1.8f,2.4f,.12f),Teal);
            var portalB=doorB.AddComponent<WorldInteractable>();portalB.action=ActionKind.Portal;portalB.argument=1;portalB.title="Masuk palka B";
            Text("PALKA B",p,new Vector3(0,12.75f,29.3f),.07f,Cream,Quaternion.identity);
            if(!cabinModel)foreach(float wx in new[]{-1.9f,1.9f})Window(p,new Vector3(wx,12f,29.47f),new Vector3(.8f,.55f,.06f),true);
            bool windlass=Model("Exterior/Wreck/ext_windlass",p,new Vector3(0,10,34),Quaternion.identity);
            foreach(float x in new[]{-2.6f,2.6f}) {
                if(!windlass) {
                    var drum=Shape("Windlass drum",PrimitiveType.Cylinder,p,new Vector3(x,10.6f,34),new Vector3(.9f,.5f,.9f),DarkSteel,false);drum.transform.localRotation=Quaternion.Euler(0,0,90);
                    for(int k=0;k<8;k++)Box("Anchor chain",p,new Vector3(x*.6f,10.06f,34.4f+k*.28f),new Vector3(.12f,.08f,.22f),new Color(.2f,.16f,.13f),false);
                }
                Bollard(p,new Vector3(x*4.5f,10,33));
            }
            Surfaces.Block("Bow mast",p,new Vector3(0,13,34.5f),new Vector3(.2f,6,.2f),Surface.Plate,OffWhite,false);
            NavLight(p,new Vector3(0,16.1f,34.5f),new Color(1,.95f,.85f),14,2);
            // Deck cranes in the gaps between the container rows.
            foreach(var (x,z,yaw) in new[]{(-7f,-15f,20f),(7f,13f,200f)}) {
                var pedestal=Shape("Crane pedestal",PrimitiveType.Cylinder,p,new Vector3(x,11.5f,z),new Vector3(1.4f,1.5f,1.4f),Yellow);
                if(Model("Exterior/Wreck/ext_deck_crane_pedestal",p,new Vector3(x,10,z),Quaternion.identity))Hide(pedestal);
                var crane=new GameObject("Crane").transform;crane.SetParent(p,false);crane.localPosition=new Vector3(x,13,z);crane.localRotation=Quaternion.Euler(0,yaw,0);
                if(Model("Exterior/Wreck/ext_deck_crane_house",crane,Vector3.zero,Quaternion.identity))continue;
                Surfaces.Block("Crane house",crane,new Vector3(0,.8f,0),new Vector3(2,1.6f,2.4f),Surface.Plate,Yellow,false);
                Window(crane,new Vector3(0,1.05f,1.21f),new Vector3(1.4f,.6f,.04f),false);
                Surfaces.Block("Crane jib",crane,new Vector3(0,3.2f,4.6f),new Vector3(.45f,.45f,10.5f),Surface.Plate,Yellow,false,Quaternion.Euler(-28,0,0));
                Surfaces.Block("Crane cable",crane,new Vector3(0,4.1f,9.2f),new Vector3(.04f,3.6f,.04f),Surface.Plate,DarkSteel,false);
                Surfaces.Block("Crane hook",crane,new Vector3(0,2.2f,9.2f),new Vector3(.3f,.3f,.3f),Surface.Plate,DarkSteel,false);
            }
            // Access C: a raised cargo hatch amidships, between the container rows.
            Surfaces.Block("Hatch coaming",p,new Vector3(0,10.33f,-2.3f),new Vector3(2.4f,.6f,2.4f),Surface.Plate,DarkSteel);
            var lid=Surfaces.Block("Hatch C",p,new Vector3(0,10.66f,-2.3f),new Vector3(2.1f,.08f,2.1f),Surface.Hazard,Color.white);
            var portalC=lid.AddComponent<WorldInteractable>();portalC.action=ActionKind.Portal;portalC.argument=2;portalC.title="Masuk palka C";
            Shape("Hatch wheel",PrimitiveType.Cylinder,p,new Vector3(0,10.72f,-2.3f),new Vector3(.55f,.03f,.55f),Brass,false);
            Text("PALKA C",p,new Vector3(0,11.35f,-1.05f),.07f,Cream);
            Fx.Glow(p,new Vector3(0,12.6f,-1.6f),new Color(1,.8f,.55f),7,1.6f);
            var ropeHook=Box("Rope anchor",p,new Vector3(15,10.5f,0),new Vector3(.3f,1,.3f),Cream);
            var climb=ropeHook.AddComponent<WorldInteractable>();climb.action=ActionKind.Climb;climb.argument=0;climb.title="Turun ke kapal kecil";
            // Name on the hull's sloped side at y=8, just proud of the plating.
            Text("KARGO / S-04",p,new Vector3(14.9f*(.65f+.35f*14f/16f)+.06f,8,-20),.18f,Cream,Quaternion.Euler(0,-90,0));
            foreach(float x in new[]{-13f,13f})foreach(float z in new[]{-20f,0f,20f}) {
                var lamp=new GameObject("Floodlight").transform;lamp.SetParent(p,false);lamp.localPosition=new Vector3(x,14.8f,z);
                if(!Model("Exterior/Wreck/ext_floodlight_pole",p,new Vector3(x,10,z),Quaternion.identity)) {
                    Surfaces.Block("Floodlight pole",p,new Vector3(x,12.4f,z),new Vector3(.12f,4.6f,.12f),Surface.Plate,OffWhite,false);
                    Shape("Floodlight bulb",PrimitiveType.Sphere,lamp,Vector3.zero,Vector3.one*.32f,new Color(1,.88f,.7f),false,true);
                }
                Fx.Glow(lamp,new Vector3(0,-.5f,0),new Color(1,.8f,.6f),21,4.6f).gameObject.AddComponent<LightFlicker>().depth=.06f;
            }
        }
    }
}
