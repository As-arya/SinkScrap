using System;
using System.Collections.Generic;
using UnityEngine;

namespace SinkScrapt
{
    public enum NavKind { Corridor, Room, Door, Stair, Airlock }

    // Builds the wreck interior from a validated blueprint. Structure (slabs, walls, stairs, furniture) is static-batched;
    // lights, signs, LEDs and hazards stay separate. Furniture only stands against closed walls or in cell corners, never on
    // the straight lines between cell centres and door midpoints that the navigation graph uses.
    public static partial class WorldFactory
    {
        static readonly Color PaintLow=new(.30f,.42f,.40f),PaintHigh=new(.82f,.80f,.70f),CabinPaint=new(.78f,.74f,.62f),GalleyPaint=new(.88f,.88f,.84f),ElectricPaint=new(.55f,.62f,.58f),
            DarkSteel=new(.30f,.33f,.34f),Brass=new(.62f,.47f,.22f),RedPaint=new(.55f,.12f,.09f),LampWarm=new(1f,.74f,.45f),LampCold=new(.62f,.80f,.74f);
        const float WallTop=2.98f;
        static float Hash01(int a,int b,int c) {unchecked {uint h=(uint)(a*73856093^b*19349663^c*83492791);h=(h^(h>>13))*1274126177u;h^=h>>16;return (h&0xFFFF)/65536f;}}
        static Vector3 Dir3(int dir) => new(ShipGrid.Dirs[dir].x,0,ShipGrid.Dirs[dir].y);

        public static void Interior(WorldZone zone,InteriorLayout layout)
        {
            var problem=layout?.Problem();
            if(problem!=null)throw new InvalidOperationException("Blueprint interior tidak valid: "+problem+" ("+layout?.Describe+")");
            zone.layout=layout;zone.hazards.Clear();
            var structure=new GameObject("Wreck structure").transform;structure.SetParent(zone.transform,false);
            var fixtures=new GameObject("Wreck fixtures").transform;fixtures.SetParent(zone.transform,false);
            for(int f=0;f<ShipGrid.Decks;f++) {
                Slabs(structure,layout,f);
                Walls(structure,fixtures,layout,f);
                for(int z=0;z<ShipGrid.Rows;z++)for(int x=0;x<ShipGrid.Columns;x++)if(layout.Kind(f,x,z,out _)==CellKind.Corridor)CorridorCell(structure,fixtures,layout,f,x,z);
            }
            Slabs(structure,layout,ShipGrid.Decks);
            foreach(var room in layout.rooms)Room(zone,structure,fixtures,layout,room);
            for(int i=0;i<layout.stairs.Count;i++)Stair(structure,fixtures,layout,layout.stairs[i]);
            for(int i=0;i<layout.entries.Count;i++)Entry(structure,fixtures,layout,i);
            zone.doors.Clear();for(int i=0;i<layout.doors.Count;i++)zone.doors.Add(Door(zone,layout,i));
            Dress(zone,structure,fixtures,layout);
            var water=Shape("Interior flood surface",PrimitiveType.Plane,zone.transform,Vector3.zero,new Vector3((ShipGrid.HalfWidth+ShipGrid.AirlockLength)*.2f,1,(ShipGrid.HalfLength+ShipGrid.AirlockLength)*.2f),Teal,false);
            water.GetComponent<Renderer>().sharedMaterial=InteriorWaterMaterial();zone.water=water.transform;
            BuildNavigation(zone,layout);
            StaticBatchingUtility.Combine(structure.gameObject);
        }
        static Material interiorWater;
        // The bilge is murkier and greener than the open sea; both use the depth-aware transparent water shader.
        static Material InteriorWaterMaterial()
        {
            if(interiorWater!=null)return interiorWater;
            var source=Resources.Load<Material>("SinkScrapt/Water");interiorWater=source!=null?new Material(source):new Material(Shader.Find("SinkScrapt/Water"));
            interiorWater.SetColor("_BaseColor",new Color(.05f,.13f,.10f,1));interiorWater.SetColor("_SkyColor",new Color(.10f,.16f,.15f,1));interiorWater.SetColor("_DeepColor",new Color(.01f,.04f,.035f,1));
            interiorWater.SetFloat("_Clarity",1.6f);interiorWater.SetFloat("_Swell",.25f);
            return interiorWater;
        }

        // ---- Slabs ----------------------------------------------------------------------------------------------------------
        // The cruise ship's passenger decks (all but the lowest) are carpeted and papered; its lowest deck is crew and machinery.
        static bool Passenger(InteriorLayout layout,int f) => layout.ship==ShipKind.Cruise&&f>=1;
        static readonly Color CorridorCarpet=new(.5f,.16f,.14f),CorridorPaper=new(.82f,.78f,.66f);
        static Surface FloorSurface(InteriorLayout layout,int f,int x,int z,out Color tint)
        {
            tint=Color.white;
            switch(layout.Kind(f,x,z,out int id)) {
                case CellKind.Corridor: if(Passenger(layout,f)){tint=CorridorCarpet;return Surface.Carpet;}return Surface.Diamond;
                case CellKind.Stair: return Surface.Grating;
                case CellKind.Room:
                    if(layout.rooms[id].type>=CruiseLayout.Lobby)return CruiseFloor(layout.rooms[id].type,out tint);
                    switch(layout.rooms[id].type) {
                        case 0: return Surface.RustPlate;
                        case 2: tint=new Color(.8f,.7f,.6f);return Surface.Planks;
                        case 3: return Surface.Tile;
                        case 4: case 5: return Surface.Grating;
                        case 6: tint=new Color(.55f,.62f,.56f);return Surface.DeckPaint;
                        default: tint=new Color(.62f,.36f,.28f);return Surface.DeckPaint;
                    }
            }
            return Surface.Plate;
        }
        // Slab `level` sits at FloorHeight(level) (the roof is level 3). It exists wherever the deck above or below it is used,
        // except where a stair shaft rises through it. Runs of identical cells along x are merged into one block.
        static void Slabs(Transform p,InteriorLayout layout,int level)
        {
            float y=level<ShipGrid.Decks?ShipTimeline.FloorHeight(level):ShipTimeline.FloorHeight(ShipGrid.Decks-1)+3;
            for(int z=0;z<ShipGrid.Rows;z++) {
                int x=0;
                while(x<ShipGrid.Columns) {
                    bool Needed(int cx) {
                        bool above=level<ShipGrid.Decks&&layout.Kind(level,cx,z,out _)!=CellKind.Void,below=level>0&&layout.Kind(level-1,cx,z,out _)!=CellKind.Void;
                        return (above||below)&&(level>=ShipGrid.Decks||layout.HasFloor(level,cx,z));
                    }
                    if(!Needed(x)) {x++;continue;}
                    var tint=DarkSteel;var surf=Surface.Plate;if(level<ShipGrid.Decks)surf=FloorSurface(layout,level,x,z,out tint);
                    bool hasCeiling=level>0&&layout.Kind(level-1,x,z,out _)!=CellKind.Void;
                    int end=x+1;
                    while(end<ShipGrid.Columns&&Needed(end)) {
                        var nextTint=DarkSteel;var next=Surface.Plate;if(level<ShipGrid.Decks)next=FloorSurface(layout,level,end,z,out nextTint);
                        bool nextCeiling=level>0&&layout.Kind(level-1,end,z,out _)!=CellKind.Void;
                        if(next!=surf||nextTint!=tint||nextCeiling!=hasCeiling)break;end++;
                    }
                    float x0=ShipGrid.X(x)-ShipGrid.Cell*.5f,x1=ShipGrid.X(end-1)+ShipGrid.Cell*.5f;var mid=new Vector3((x0+x1)*.5f,0,ShipGrid.Z(z));
                    Surfaces.Block("Deck slab",p,mid+Vector3.up*(y-.1f),new Vector3(x1-x0,.2f,ShipGrid.Cell),level<ShipGrid.Decks?surf:Surface.Plate,tint);
                    if(hasCeiling)Surfaces.Block("Ceiling",p,mid+Vector3.up*(y-.22f),new Vector3(x1-x0,.04f,ShipGrid.Cell),Surface.Plate,new Color(.42f,.44f,.43f),false);
                    x=end;
                }
            }
        }

        // ---- Walls ----------------------------------------------------------------------------------------------------------
        static Surface WallSurface(InteriorLayout layout,int f,int x,int z,out Color tint)
        {
            tint=Color.white;
            switch(layout.Kind(f,x,z,out int id)) {
                case CellKind.Corridor: if(Passenger(layout,f)){tint=CorridorPaper;return Surface.Wallpaper;}tint=new Color(.62f,.70f,.66f);return Surface.Bulkhead;
                case CellKind.Stair: tint=new Color(.6f,.6f,.58f);return Surface.Plate;
                case CellKind.Atrium: return CruiseWall(CruiseLayout.Lobby,out tint);
                case CellKind.Room:
                    if(layout.rooms[id].type>=CruiseLayout.Lobby)return CruiseWall(layout.rooms[id].type,out tint);
                    switch(layout.rooms[id].type) {
                        case 0: case 4: case 5: return Surface.RustPlate;
                        case 2: tint=CabinPaint;return Surface.Bulkhead;
                        case 3: tint=GalleyPaint;return Surface.Bulkhead;
                        case 6: tint=ElectricPaint;return Surface.Bulkhead;
                        default: tint=new Color(.7f,.72f,.7f);return Surface.Plate;
                    }
            }
            return Surface.Plate;
        }
        // Every edge between different modules gets a wall; each used side gets its own 10 cm skin in that module's finish.
        static void Walls(Transform p,Transform fixtures,InteriorLayout layout,int f)
        {
            for(int z=-1;z<ShipGrid.Rows;z++)for(int x=-1;x<ShipGrid.Columns;x++)for(int dir=0;dir<2;dir++) {
                if((x<0&&dir==1)||(z<0&&dir==0))continue;
                var a=new Vector2Int(x,z);var b=a+ShipGrid.Dirs[dir];
                if(!ShipGrid.Inside(a.x,a.y)&&!ShipGrid.Inside(b.x,b.y))continue;
                var ka=layout.Kind(f,a.x,a.y,out int ma);var kb=layout.Kind(f,b.x,b.y,out int mb);
                if(ka==CellKind.Void&&kb==CellKind.Void)continue;
                // The lobby atrium's open air: a balustrade toward the gallery corridor, plain walls toward anything else.
                if(ka==CellKind.Atrium||kb==CellKind.Atrium) {
                    if(ka==kb)continue;
                    if(ka==CellKind.Corridor||kb==CellKind.Corridor) {Balustrade(p,ka==CellKind.Corridor?a:b,ka==CellKind.Corridor?dir:ShipGrid.Opposite(dir),f);continue;}
                    if(ka!=CellKind.Void)WallSide(p,fixtures,layout,f,a,dir,0,0,false,true,kb!=CellKind.Void);
                    if(kb!=CellKind.Void)WallSide(p,fixtures,layout,f,b,ShipGrid.Opposite(dir),0,0,false,ka==CellKind.Void,false);
                    continue;
                }
                bool open=ShipGrid.Inside(a.x,a.y)?layout.Open(f,a.x,a.y,dir):layout.Open(f,b.x,b.y,ShipGrid.Opposite(dir));
                if(open&&ka==kb&&ma==mb)continue;
                float width=0,height=ShipGrid.DoorHeight;bool door=false;
                if(open) {
                    door=ka!=CellKind.Stair&&kb!=CellKind.Stair&&ka!=CellKind.Void&&kb!=CellKind.Void;
                    // The lobby's arches are wide openings without a leaf, framed like the stair and airlock openings.
                    if(door) {int index=layout.DoorAt(f,a.x,a.y,dir);if(index>=0&&layout.doors[index].arch)door=false;}
                    width=door?ShipGrid.DoorWidth:3f;height=door?ShipGrid.DoorHeight:2.5f;
                }
                // An opening between two used cells gets one frame model, built from side a; against the hull, the used side builds it.
                if(ka!=CellKind.Void)WallSide(p,fixtures,layout,f,a,dir,width,height,door,true,kb!=CellKind.Void);
                if(kb!=CellKind.Void)WallSide(p,fixtures,layout,f,b,ShipGrid.Opposite(dir),width,height,door,ka==CellKind.Void,false);
            }
        }
        // The skin of `cell` on its edge toward `dir`, optionally with an opening of `width` x `height` in the middle.
        // `buildFrame`: this side places the opening's frame model; `doubleWall`: the other side has its own skin, so the frame
        // is centred on the shared edge and stretched over both 10 cm skins.
        static void WallSide(Transform p,Transform fixtures,InteriorLayout layout,int f,Vector2Int cell,int dir,float width,float height,bool door,bool buildFrame=true,bool doubleWall=false)
        {
            var surf=WallSurface(layout,f,cell.x,cell.y,out var tint);
            var n=Dir3(dir);var along=new Vector3(Mathf.Abs(n.z),0,Mathf.Abs(n.x));float y=ShipTimeline.FloorHeight(f);
            var edge=ShipGrid.Center(f,cell.x,cell.y)+n*(ShipGrid.Cell*.5f-.05f);
            Vector3 Size(float length,float h) => along*length+Vector3.up*h+new Vector3(Mathf.Abs(n.x),0,Mathf.Abs(n.z))*.1f;
            // Exactly one cell long: longer skins overlapped their neighbours on the same plane and flickered at every seam.
            const float full=ShipGrid.Cell;
            if(width<=0) Surfaces.Block("Wall",p,edge+Vector3.up*(WallTop*.5f),Size(full,WallTop),surf,tint);
            else {
                float side=(full-width)*.5f;
                foreach(float s in new[]{-1f,1f})Surfaces.Block("Wall",p,edge+along*s*(width*.5f+side*.5f)+Vector3.up*(WallTop*.5f),Size(side,WallTop),surf,tint);
                Surfaces.Block("Wall header",p,edge+Vector3.up*((height+WallTop)*.5f),Size(width,WallTop-height),surf,tint);
                // Hatch frame: jambs, lintel and a low sill (no colliders, the opening stays clear). The kit model, when present,
                // replaces the boxes: kit_door_frame for doors, kit_wide_opening_frame for stair and airlock openings.
                var framePos=doubleWall?edge+n*.05f:edge;
                bool modelled=!buildFrame||Model(door?"Kit/kit_door_frame":"Kit/kit_wide_opening_frame",p,framePos,Quaternion.LookRotation(-n),new Vector3(1,1,doubleWall?2:1));
                if(!modelled) {
                    var frame=door?new Color(.36f,.40f,.38f):DarkSteel;
                    foreach(float s in new[]{-1f,1f})Surfaces.Block("Door jamb",p,edge-n*.04f+along*s*(width*.5f+.07f)+Vector3.up*(height*.5f),Size(.14f,height)+Abs(n)*.04f,Surface.Plate,frame,false);
                    Surfaces.Block("Door lintel",p,edge-n*.04f+Vector3.up*(height+.07f),Size(width+.28f,.14f)+Abs(n)*.04f,Surface.Plate,frame,false);
                    Surfaces.Block("Door sill",p,edge+Vector3.up*.03f,Size(width,.06f)+Abs(n)*.1f,Surface.Plate,frame,false);
                }
            }
            var kind=layout.Kind(f,cell.x,cell.y,out _);
            // Ship framing: a rib at one end of every corridor wall and a porthole now and then on the outer hull.
            if(kind==CellKind.Corridor&&!Passenger(layout,f)) {
                var ribAt=edge-n*.05f+along*(ShipGrid.Cell*.5f-.15f);
                if(!Model("Kit/kit_frame_rib",p,ribAt,Quaternion.LookRotation(-n)))
                    Surfaces.Block("Frame rib",p,edge-n*.1f+along*(ShipGrid.Cell*.5f-.15f)+Vector3.up*(WallTop*.5f),Size(.22f,WallTop)+Abs(n)*.12f,Surface.Plate,DarkSteel,false);
            }
            var outside=cell+ShipGrid.Dirs[dir];
            if(width<=0&&!ShipGrid.Inside(outside.x,outside.y)&&kind!=CellKind.Stair&&Hash01(cell.x+f*11,cell.y,dir)<.45f)Porthole(p,fixtures,edge-n*.06f+Vector3.up*1.6f,n);
        }
        static void Porthole(Transform p,Transform fixtures,Vector3 pos,Vector3 normal)
        {
            // kit_porthole: pivot at the bottom of the ring on the wall face, glass centre 0.31 m above it, front toward the cabin.
            if(Model("Kit/kit_porthole",p,pos+normal*.01f+Vector3.down*.31f,Quaternion.LookRotation(-normal)))return;
            var rot=Quaternion.LookRotation(normal)*Quaternion.Euler(90,0,0);
            var ring=Shape("Porthole ring",PrimitiveType.Cylinder,p,pos-normal*.02f,new Vector3(.62f,.03f,.62f),Brass,false);ring.transform.localRotation=rot;
            var glass=Shape("Porthole glass",PrimitiveType.Cylinder,fixtures,pos-normal*.04f,new Vector3(.46f,.02f,.46f),new Color(.02f,.09f,.10f),false,true);glass.transform.localRotation=rot;
            for(int k=0;k<6;k++) {var a=k*Mathf.PI/3;var bolt=Shape("Porthole bolt",PrimitiveType.Cube,p,pos-normal*.05f+Quaternion.LookRotation(normal)*new Vector3(Mathf.Cos(a)*.28f,Mathf.Sin(a)*.28f,0),Vector3.one*.05f,Brass,false);bolt.transform.localRotation=Quaternion.LookRotation(normal);}
        }

        // ---- Corridors ------------------------------------------------------------------------------------------------------
        static void CorridorCell(Transform p,Transform fixtures,InteriorLayout layout,int f,int x,int z)
        {
            if(Passenger(layout,f)) {PassengerCorridor(p,fixtures,layout,f,x,z);return;}
            var c=ShipGrid.Center(f,x,z);float y=c.y;
            bool alongX=Corridor(layout,f,x-1,z)||Corridor(layout,f,x+1,z),alongZ=Corridor(layout,f,x,z-1)||Corridor(layout,f,x,z+1);
            if(!alongX&&!alongZ)alongZ=true;
            // Overhead pipe runs and a cable tray along the corridor, at different heights so crossings do not intersect.
            if(alongZ) {
                Pipe(p,c+new Vector3(-1.35f,2.5f,0),Vector3.forward,ShipGrid.Cell,.18f,(f+x)%2==0?Rust:Teal);
                Pipe(p,c+new Vector3(-1.05f,2.58f,0),Vector3.forward,ShipGrid.Cell,.1f,Steel);
                CableTray(p,c+new Vector3(1.3f,2.55f,0),false);
            }
            if(alongX) {
                Pipe(p,c+new Vector3(0,2.36f,-1.35f),Vector3.right,ShipGrid.Cell,.18f,(f+z)%2==0?Teal:Rust);
                CableTray(p,c+new Vector3(0,2.42f,1.3f),true);
            }
            // Deck beams across the corridor where it continues into the next cell (kit_deck_beam: 4 m along its local X, pivot on top).
            for(int dir=0;dir<2;dir++)if(Corridor(layout,f,x+ShipGrid.Dirs[dir].x,z+ShipGrid.Dirs[dir].y)) {
                var n=Dir3(dir);var at=c+n*ShipGrid.Cell*.5f+Vector3.up*2.68f;
                if(!Model("Kit/kit_deck_beam",p,at+Vector3.up*.1f,Quaternion.Euler(0,n.x!=0?90:0,0)))
                    Surfaces.Block("Deck beam",p,at,new Vector3(Mathf.Abs(n.z)*ShipGrid.Cell+.25f*Mathf.Abs(n.x),.2f,Mathf.Abs(n.x)*ShipGrid.Cell+.25f*Mathf.Abs(n.z)),Surface.Plate,DarkSteel,false);
            }
            // Wall fittings on closed sides: extinguishers, valve wheels, fuse boxes, vertical pipes. Thin and collider-free.
            // The kit models stand on the wall face (`face`) with their front toward the corridor.
            for(int dir=0;dir<4;dir++) {
                if(layout.Open(f,x,z,dir)||layout.Balcony(f,x,z,dir))continue;var n=Dir3(dir);var along=new Vector3(-n.z,0,n.x);float roll=Hash01(x+f*13,z,dir+7);
                var at=c+n*(ShipGrid.Cell*.5f-.12f)+along*((roll-.5f)*2.2f);var face=at+n*.02f;var facing=Quaternion.LookRotation(-n);
                if(roll<.18f) {
                    if(!Model("Kit/kit_extinguisher",p,face+Vector3.up*.2f,facing)) {Shape("Extinguisher",PrimitiveType.Capsule,p,at+Vector3.up*.55f,new Vector3(.18f,.3f,.18f),RedPaint,false);Surfaces.Block("Bracket",p,at+n*.06f+Vector3.up*.55f,new Vector3(.22f,.08f,.22f),Surface.Plate,DarkSteel,false);}
                }
                else if(roll<.34f) {
                    if(Model("Kit/kit_valve_wheel",p,face+Vector3.up*1.0f,facing))Pipe(p,face-n*.07f+Vector3.up*1.4f,Vector3.up,3,.12f,Steel);
                    else {var wheel=Shape("Valve wheel",PrimitiveType.Cylinder,p,at+Vector3.up*1.3f,new Vector3(.4f,.03f,.4f),RedPaint,false);wheel.transform.localRotation=Quaternion.LookRotation(n)*Quaternion.Euler(90,0,0);Pipe(p,at+n*.05f+Vector3.up*1.4f,Vector3.up,3,.12f,Steel);}
                }
                else if(roll<.5f) {
                    bool modelled=Model("Kit/kit_fuse_box",p,face+Vector3.up*1.15f,facing,null,new Color(.45f,.5f,.46f));
                    if(!modelled)Surfaces.Block("Fuse box",p,at+Vector3.up*1.45f,new Vector3(.5f,.6f,.5f)-Abs(n)*.32f,Surface.Plate,new Color(.45f,.5f,.46f),false);
                    StatusLeds(fixtures,(modelled?face-n*.19f:at-n*.1f)+Vector3.up*1.3f,x*7+z+f);
                }
                else if(roll<.6f)Pipe(p,at+Vector3.up*1.5f,Vector3.up,3,.14f,(x+z)%2==0?Rust:Steel);
            }
            // Only a few corridor cells have a lamp at all, and many of those are dead: without a torch the wreck is near black.
            if(Hash01(x,z,f+31)<ShipProfile.Current.corridorLamps)CeilingLamp(fixtures,c+Vector3.up*2.48f,LampCold,f*100+x*11+z,1f,5.5f);
        }
        static Vector3 Abs(Vector3 v) => new(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z));
        static bool Corridor(InteriorLayout layout,int f,int x,int z) => layout.Kind(f,x,z,out _)==CellKind.Corridor;
        // kit_cable_tray: 4 m along its local Z, pivot at the top of the rails; `center` is the old 5 cm tray box's centre.
        static void CableTray(Transform p,Vector3 center,bool alongX)
        {
            if(!Model("Kit/kit_cable_tray",p,center+Vector3.up*.025f,Quaternion.Euler(0,alongX?90:0,0)))
                Surfaces.Block("Cable tray",p,center,alongX?new Vector3(ShipGrid.Cell,.05f,.5f):new Vector3(.5f,.05f,ShipGrid.Cell),Surface.Grating,DarkSteel,false);
        }
        // kit_pipe_straight/_flange (d10 or d18) scaled to the requested diameter and length; slot "Plate" takes `color`.
        static void Pipe(Transform p,Vector3 center,Vector3 axis,float length,float thickness,Color color)
        {
            string size=thickness<.15f?"d10":"d18";float d=thickness<.15f?.1f:.18f;var rotation=Quaternion.FromToRotation(Vector3.forward,axis);
            if(Model("Kit/kit_pipe_straight_"+size,p,center,rotation,new Vector3(thickness/d,thickness/d,length/ShipGrid.Cell),color)) {
                Model("Kit/kit_pipe_flange_"+size,p,center,rotation,Vector3.one*(thickness/d));return;
            }
            var pipe=Shape("Pipe",PrimitiveType.Cylinder,p,center,new Vector3(thickness,length*.5f,thickness),color,false);pipe.transform.localRotation=Quaternion.FromToRotation(Vector3.up,axis);
            var flange=Shape("Pipe flange",PrimitiveType.Cylinder,p,center,new Vector3(thickness*1.6f,.04f,thickness*1.6f),Steel,false);flange.transform.localRotation=pipe.transform.localRotation;
        }
        // Caged bulkhead lamp. Some are dead, some flicker; the wreck is meant to be dark enough that a torch matters.
        static void CeilingLamp(Transform p,Vector3 pos,Color color,int seed,float intensity=1.25f,float range=6.5f,bool canDie=true)
        {
            float roll=Hash01(seed,3,5);bool dead=canDie&&roll<.3f;
            if(!Model(dead?"Kit/kit_ceiling_lamp_broken":"Kit/kit_ceiling_lamp",p,pos+Vector3.up*.17f,Quaternion.identity,null,color)) {
                Surfaces.Block("Lamp housing",p,pos+Vector3.up*.12f,new Vector3(.5f,.1f,.28f),Surface.Plate,DarkSteel,false);
                Shape("Lamp glass",PrimitiveType.Cube,p,pos+Vector3.up*.04f,new Vector3(.4f,.07f,.2f),dead?new Color(.15f,.15f,.14f):color,false,!dead);
                for(int k=-1;k<=1;k++)Box("Lamp cage",p,pos+new Vector3(k*.15f,0,0),new Vector3(.02f,.1f,.24f),DarkSteel,false);
            }
            if(dead)return;
            var light=new GameObject("Lamp light");light.transform.SetParent(p,false);light.transform.localPosition=pos+Vector3.down*.15f;
            var l=light.AddComponent<Light>();l.type=LightType.Point;l.color=color;l.range=range;l.intensity=intensity;l.shadows=LightShadows.None;
            var flicker=light.AddComponent<LightFlicker>();flicker.depth=roll<.45f?.38f:.08f;flicker.dropout=roll<.45f?.01f:.0015f;
        }
        static void StatusLeds(Transform parent,Vector3 pos,int seed)
        {
            var colors=new[]{new Color(.3f,1,.4f),new Color(1,.25f,.2f),new Color(1,.7f,.2f)};
            for(int n=0;n<3;n++) {var led=Shape("Status LED",PrimitiveType.Cube,parent,pos+new Vector3(0,n*.12f,0),Vector3.one*.05f,colors[n],false,true);var blink=led.AddComponent<Blinker>();blink.period=.6f+((seed*7+n*3)%9)*.18f;blink.onFraction=n==1?.25f:.6f;}
        }

        // ---- Rooms ----------------------------------------------------------------------------------------------------------
        // Room titles by type: 0-6 cargo ship (types 1-4 and 6 also on the cruise ship's crew deck), 7-16 cruise ship.
        public static readonly string[] RoomTitles={"PALKA","GUDANG","KABIN KRU","DAPUR","RUANG MESIN","POMPA","LISTRIK",
            "LOBI","KABIN","SUITE","RESTORAN","TEATER","BAR","KASINO","BUTIK","KLINIK","BINATU"};
        // The sign over a room's door: passenger cabins and suites carry a cabin number (deck digit first).
        static string RoomSign(RoomData room) => room.type==CruiseLayout.Cabin||room.type==CruiseLayout.Suite?RoomTitles[room.type]+" "+(room.floor+1)+(room.id%100).ToString("00"):RoomTitles[room.type];
        static void Room(WorldZone zone,Transform p,Transform fixtures,InteriorLayout layout,RoomData room)
        {
            int seed=layout.seed^(room.id*7919);var random=new System.Random(seed);
            for(int x=room.x;x<room.x+room.w;x++)for(int z=room.z;z<room.z+room.d;z++) {
                var c=ShipGrid.Center(room.floor,x,z);
                for(int dir=0;dir<4;dir++) {
                    var n=Dir3(dir);var along=new Vector3(-n.z,0,n.x);var nb=new Vector2Int(x,z)+ShipGrid.Dirs[dir];
                    bool open=layout.Open(room.floor,x,z,dir);
                    if(!open) Furnish(p,fixtures,room.type,c,n,along,random,3.6f,0);
                    else if(!room.Contains(nb.x,nb.y)) {
                        var outsideKind=layout.Kind(room.floor,nb.x,nb.y,out _);
                        // The lobby's 3 m openings (arches, grand stairs, main entry) keep their sides clear.
                        int index=layout.DoorAt(room.floor,x,z,dir);
                        bool wide=outsideKind==CellKind.Stair||outsideKind==CellKind.Void||(index>=0&&layout.doors[index].arch);
                        // A doorway: keep the corners beside it for small things only, and sign the corridor side.
                        if(!wide&&random.Next(2)==0)Furnish(p,fixtures,room.type,c,n,along,random,.9f,random.Next(2)==0?-1.4f:1.4f);
                        // TextMeshPro reads from its local -Z side, so the sign looks away from the corridor reader.
                        if(outsideKind==CellKind.Corridor)Text(RoomSign(room),fixtures,c+n*(ShipGrid.Cell*.5f+.13f)+Vector3.up*(wide?2.75f:2.58f),.045f,Cream,Quaternion.LookRotation(-n));
                    }
                }
            }
            var first=ShipGrid.Center(room.floor,room.x,room.z);var centre=layout.Position(room);
            if(room.type>=CruiseLayout.Lobby)CruiseRoom(zone,p,fixtures,layout,room,random,seed);
            // About one room in four still has power; the rest are dark (a hanging dead lamp at most). The lobby has its chandeliers.
            else if(Hash01(seed,11,room.floor)<.27f)CeilingLamp(fixtures,centre+Vector3.up*2.5f,room.type==6||room.type==4?LampCold:LampWarm,seed,1.1f,6+Mathf.Max(room.w,room.d));
            else Model("Kit/kit_ceiling_lamp_broken",fixtures,centre+Vector3.up*2.67f,Quaternion.identity);
            // Seeded hazard against a closed wall of the room's first cell.
            int every=layout.Profile.hazardEvery;
            if(!layout.IsLobby(room)&&(room.id+Math.Abs((NetSession.Instance?.Seed??layout.seed)%every))%every==0) {
                int side=0;for(int dir=0;dir<4;dir++)if(!layout.Open(room.floor,room.x,room.z,dir)) {side=dir;break;}
                var hazardGo=new GameObject(room.type==6?"Live electricity":"Steam leak");hazardGo.transform.SetParent(zone.transform,false);hazardGo.transform.localPosition=first+Dir3(side)*1.2f+Vector3.up*.1f;
                var hazard=hazardGo.AddComponent<WorldHazard>();hazard.electric=(room.id%2)==0;hazard.index=zone.hazards.Count;hazard.label=RoomTitles[room.type];hazard.deck=room.floor;zone.hazards.Add(hazard);
            }
        }
        // One piece of furniture against the wall facing `n` of the cell centred at `c`. `length` is the usable wall length
        // (3.6 for a closed wall, 0.9 for a corner beside a door); `offset` shifts it along the wall. Depth never exceeds 1 m.
        static void Furnish(Transform p,Transform fixtures,int type,Vector3 c,Vector3 n,Vector3 along,System.Random random,float length,float offset)
        {
            if(type>=CruiseLayout.Lobby) {FurnishCruise(p,fixtures,type,c,n,along,random,length,offset);return;}
            var rot=Quaternion.LookRotation(-n);var wall=c+n*1.9f+along*offset;int pick=random.Next(4);
            Vector3 At(float depth,float height,float shift=0) => wall-n*depth*.5f+along*shift+Vector3.up*height;
            bool corner=length<1.5f;
            // Furniture models (Furniture/furn_*) stand at the bottom centre of each piece's footprint, front toward the room. The
            // blocks below keep their colliders; Hide() stops drawing those a model replaces. Random draws are identical with or
            // without models, so every peer furnishes the same room.
            GameObject Piece(GameObject block,string model,Vector3 at,Quaternion? facing=null,Vector3? scale=null,Color? tint=null) {if(Model("Furniture/"+model,p,at,facing??rot,scale,tint))Hide(block);return block;}
            switch(type) {
                case 0: // cargo hold: crates, barrels and pallets
                    if(pick==0&&!corner) {
                        for(int k=-1;k<=1;k++) {
                            Piece(Surfaces.Block("Crate",p,At(.9f,.45f,k*1.1f),new Vector3(1,.9f,.9f),Surface.Wood,Color.white,true,rot),"furn_crate_large",At(.9f,0,k*1.1f));
                            if(random.Next(2)==0)Piece(Surfaces.Block("Crate",p,At(.8f,1.3f,k*1.1f),new Vector3(.8f,.8f,.8f),Surface.Wood,new Color(.8f,.75f,.7f),true,rot),"furn_crate_small",At(.8f,.9f,k*1.1f),rot*Quaternion.Euler(0,(k+2)*7,0));
                        }
                    }
                    else if(pick==1||corner) Barrels(p,wall-n*.45f,along,corner?1:3,random);
                    else if(pick==2) {
                        var pallet=Surfaces.Block("Pallet",p,At(1,.08f),new Vector3(3.2f,.16f,1),Surface.Planks,Color.white,true,rot);var sacks=Surfaces.Block("Sacks",p,At(.9f,.5f),new Vector3(2.8f,.7f,.85f),Surface.Sand,new Color(.7f,.65f,.5f),true,rot);
                        if(Model("Furniture/furn_pallet_sacks",p,At(1,0),rot)) {Hide(pallet);Hide(sacks);}
                    }
                    break;
                case 1: // storage: shelving with boxes
                    if(!corner&&pick<3) Shelf(p,At(.6f,0),rot,3.2f,random);
                    else {float width=corner?.8f:1.4f;Piece(Surfaces.Block("Box",p,At(.7f,.35f),new Vector3(width,.7f,.7f),Surface.Wood,new Color(.75f,.7f,.62f),true,rot),"furn_storage_box",At(.7f,0),rot,new Vector3(width/1.4f,1,1));}
                    break;
                case 2: // crew cabin: bunks, lockers
                    if(!corner&&pick<2) Bunk(p,At(.9f,0),rot);
                    else if(!corner&&pick==2) {
                        var lockers=new GameObject[3];for(int k=-1;k<=1;k++)lockers[k+1]=Surfaces.Block("Locker",p,At(.55f,.95f,k*.65f),new Vector3(.6f,1.9f,.55f),Surface.Corrugated,new Color(.45f,.55f,.52f),true,rot);
                        if(Model("Furniture/furn_locker_row",p,At(.55f,0),rot))foreach(var locker in lockers)Hide(locker);
                    }
                    else Piece(Surfaces.Block("Footlocker",p,At(.5f,.25f),new Vector3(.8f,.5f,.5f),Surface.Wood,Color.white,true,rot),"furn_footlocker",At(.5f,0));
                    break;
                case 3: // galley: counters, stove, fridge
                    if(!corner) {
                        var counter=Surfaces.Block("Counter",p,At(.7f,.45f),new Vector3(3.4f,.9f,.7f),Surface.Plate,new Color(.7f,.72f,.72f),true,rot);
                        if(Model("Furniture/furn_galley_counter",p,At(.74f,0),rot))Hide(counter);
                        else Surfaces.Block("Worktop",p,At(.74f,.92f),new Vector3(3.5f,.05f,.74f),Surface.Tile,Color.white,false,rot);
                        if(pick<2)for(int k=0;k<2;k++)Shape("Burner",PrimitiveType.Cylinder,p,At(.4f,.96f,-.6f+k*.6f),new Vector3(.3f,.02f,.3f),DarkSteel,false);
                        else Piece(Surfaces.Block("Fridge",p,At(.7f,1.0f,1.2f),new Vector3(.9f,2f,.7f),Surface.Plate,new Color(.85f,.86f,.84f),true,rot),"furn_fridge",At(.7f,0,1.2f));
                    } else if(ModelLibrary.Has("Furniture/furn_stove")) {
                        random.Next(3);// the barrel colour this corner used to draw
                        Piece(Surfaces.Block("Stove",p,At(.7f,.45f),new Vector3(.9f,.9f,.7f),Surface.Plate,DarkSteel,true,rot),"furn_stove",At(.7f,0));
                    } else Barrels(p,wall-n*.4f,along,1,random);
                    break;
                case 4: // engine room: machinery block with cylinder heads, gauges
                    if(!corner) {
                        var engine=Surfaces.Block("Engine block",p,At(1,.8f),new Vector3(3.2f,1.6f,1),Surface.RustPlate,new Color(.7f,.7f,.7f),true,rot);
                        bool modelled=Model("Furniture/furn_engine_block",p,At(1,0),rot,null,new Color(.7f,.7f,.7f));
                        if(modelled)Hide(engine);else for(int k=0;k<4;k++)Shape("Cylinder head",PrimitiveType.Cylinder,p,At(.5f,1.75f,-1.2f+k*.8f),new Vector3(.4f,.15f,.4f),Rust,false);
                        Pipe(p,wall-n*.2f+along*1.6f+Vector3.up*1.6f,Vector3.up,2.4f,.2f,Teal);
                        StatusLeds(fixtures,At(modelled?1.88f:1.02f,1.2f,1.4f),random.Next(99));
                    } else {
                        bool modelled=Model("Furniture/furn_gauge_panel",p,At(.3f,0),rot);
                        var panel=Surfaces.Block("Gauge panel",p,At(.3f,1.2f),new Vector3(.8f,1.2f,.3f),Surface.Plate,DarkSteel,true,rot);if(modelled)Hide(panel);
                        StatusLeds(fixtures,modelled?At(.68f,.62f,.25f):At(.32f,1.1f),random.Next(99));
                    }
                    break;
                case 5: // pump room: horizontal pump with motor, riser pipes
                    if(!corner) {
                        var pump=Shape("Pump",PrimitiveType.Cylinder,p,At(.8f,.55f,-.5f),new Vector3(.8f,.9f,.8f),Rust,true);pump.transform.localRotation=Quaternion.LookRotation(along)*Quaternion.Euler(90,0,0);
                        var motor=Surfaces.Block("Pump motor",p,At(.8f,.5f,1f),new Vector3(.9f,.9f,.8f),Surface.Plate,Teal,true,rot);
                        // The pump model's volute sits at its local -X once turned to face the wall side, matching the code's pump/motor order.
                        if(Model("Furniture/furn_pump_unit",p,At(.8f,0,.25f),rot*Quaternion.Euler(0,180,0))) {Hide(pump);Hide(motor);}
                        if(!Model("Furniture/furn_riser_pipe",p,wall-n*.25f+along*(-1.5f),rot,null,Teal)) {
                            Pipe(p,wall-n*.25f+along*(-1.5f)+Vector3.up*1.5f,Vector3.up,3,.24f,Teal);
                            var wheel=Shape("Valve wheel",PrimitiveType.Cylinder,p,wall-n*.45f+along*(-1.5f)+Vector3.up*1.2f,new Vector3(.45f,.03f,.45f),RedPaint,false);wheel.transform.localRotation=Quaternion.LookRotation(n)*Quaternion.Euler(90,0,0);
                        }
                    } else if(!Model("Furniture/furn_riser_pipe",p,wall-n*.3f,rot,new Vector3(1.25f,1,1.25f),Rust))Pipe(p,wall-n*.3f+Vector3.up*1.5f,Vector3.up,3,.3f,Rust);
                    break;
                default: // electrical: cabinets with LEDs
                    int count=corner?1:3;
                    for(int k=0;k<count;k++) {float s=corner?0:-1.15f+k*1.15f;Piece(Surfaces.Block("Electrical cabinet",p,At(.6f,1,s),new Vector3(1,2,.6f),Surface.Plate,new Color(.5f,.58f,.52f),true,rot),"furn_electrical_cabinet",At(.6f,0,s));StatusLeds(fixtures,At(1.22f,1.1f,s+.3f),random.Next(99));}
                    break;
            }
        }
        static void Barrels(Transform p,Vector3 at,Vector3 along,int count,System.Random random)
        {
            for(int k=0;k<count;k++) {
                var color=random.Next(3) switch {0=>Rust,1=>Teal,_=>new Color(.25f,.3f,.32f)};var pos=at+along*(count==1?0:(k-1)*.95f);
                var barrel=Shape("Barrel",PrimitiveType.Cylinder,p,pos+Vector3.up*.45f,new Vector3(.8f,.45f,.8f),color,true);
                if(Model("Furniture/furn_barrel",p,pos,Quaternion.Euler(0,k*67+pos.x*13,0),null,color))Hide(barrel);
            }
        }
        static void Shelf(Transform p,Vector3 at,Quaternion rot,float length,System.Random random)
        {
            var right=rot*Vector3.right;bool modelled=Model("Furniture/furn_shelf_unit",p,at,rot);
            foreach(float s in new[]{-1f,1f}) {var upright=Surfaces.Block("Shelf upright",p,at+right*s*(length*.5f-.05f)+Vector3.up*1.1f,new Vector3(.08f,2.2f,.6f),Surface.Plate,DarkSteel,true,rot);if(modelled)Hide(upright);}
            for(int k=0;k<4;k++) {
                if(!modelled)Surfaces.Block("Shelf",p,at+Vector3.up*(.15f+k*.65f),new Vector3(length,.05f,.6f),Surface.Plate,DarkSteel,false,rot);
                for(int b=0;b<3;b++)if(random.Next(3)>0)Surfaces.Block("Shelf box",p,at+right*(-length*.35f+b*length*.35f)+Vector3.up*(.33f+k*.65f),new Vector3(.5f+(float)random.NextDouble()*.3f,.32f,.45f),Surface.Wood,new Color(.75f,.68f,.55f),false,rot);
            }
            // One collider for the whole unit so nobody squeezes between the shelves.
            var block=new GameObject("Shelf collider");block.transform.SetParent(p,false);block.transform.localPosition=at+Vector3.up*1.1f;block.transform.localRotation=rot;block.AddComponent<BoxCollider>().size=new Vector3(length,2.2f,.6f);
        }
        static void Bunk(Transform p,Vector3 at,Quaternion rot)
        {
            var right=rot*Vector3.right;
            if(!Model("Furniture/furn_bunk_bed",p,at,rot)) {
                foreach(float h in new[]{.45f,1.45f}) {Surfaces.Block("Bunk frame",p,at+Vector3.up*h,new Vector3(2.1f,.12f,.9f),Surface.Plate,DarkSteel,false,rot);Surfaces.Block("Mattress",p,at+Vector3.up*(h+.12f),new Vector3(1.95f,.14f,.8f),Surface.Sand,new Color(.55f,.6f,.58f),false,rot);}
                foreach(float s in new[]{-1f,1f})Surfaces.Block("Bunk post",p,at+right*s*1.02f+Vector3.up*.95f,new Vector3(.06f,1.9f,.9f),Surface.Plate,DarkSteel,false,rot);
            }
            var block=new GameObject("Bunk collider");block.transform.SetParent(p,false);block.transform.localPosition=at+Vector3.up*.95f;block.transform.localRotation=rot;block.AddComponent<BoxCollider>().size=new Vector3(2.1f,1.9f,.9f);
        }

        // ---- Stairs and airlocks --------------------------------------------------------------------------------------------
        const int StairSteps=16;
        // Height of the stair surface `s` metres into a shaft (0 = back edge of the low cell).
        static float StairHeight(float s) => s<=1?0:s>=1+ShipGrid.StairRun?3:Mathf.Min(3,(Mathf.Floor((s-1)/(ShipGrid.StairRun/StairSteps))+1)*3f/StairSteps);
        static void Stair(Transform p,Transform fixtures,InteriorLayout layout,StairData s)
        {
            var fwd=Dir3(s.dir);var right=new Vector3(fwd.z,0,-fwd.x);float y0=ShipTimeline.FloorHeight(s.floor);
            // Origin: back edge of the low cell at height 0; every height below is added explicitly.
            var origin=ShipGrid.Center(s.floor,s.Low.x,s.Low.y)-fwd*ShipGrid.Cell*.5f;origin.y=0;var rot=Quaternion.LookRotation(fwd);
            float run=ShipGrid.StairRun/StairSteps,rise=3f/StairSteps;
            // kit_stair_flight shares this origin and draws steps, nosing, landing and rails; the step blocks below stay as colliders.
            bool modelled=Model("Kit/kit_stair_flight",p,origin+Vector3.up*y0,rot);
            // Solid steps from the deck up, so there is no space underneath to fall into.
            for(int i=0;i<StairSteps;i++) {
                float h=(i+1)*rise;var step=Surfaces.Block("Stair step",p,origin+fwd*(1+run*(i+.5f))+Vector3.up*(y0+h*.5f),new Vector3(3.5f,h,run+.002f),Surface.Diamond,new Color(.8f,.8f,.78f),true,rot);
                if(modelled)Hide(step);else Surfaces.Block("Step nosing",p,origin+fwd*(1+run*i+.03f)+Vector3.up*(y0+h-.01f),new Vector3(3.5f,.03f,.06f),Surface.Hazard,Color.white,false,rot);
            }
            var landing=Surfaces.Block("Stair landing",p,origin+fwd*(1+ShipGrid.StairRun+.5f)+Vector3.up*(y0+1.5f),new Vector3(3.5f,3,1),Surface.Grating,Color.white,true,rot);
            if(modelled)Hide(landing);
            // Handrails along both walls.
            float slope=Mathf.Atan2(3,ShipGrid.StairRun)*Mathf.Rad2Deg;var railRot=rot*Quaternion.Euler(-slope,0,0);var railLength=Mathf.Sqrt(9+ShipGrid.StairRun*ShipGrid.StairRun);
            if(!modelled)foreach(float side in new[]{-1f,1f}) {
                Surfaces.Block("Handrail",p,origin+fwd*(1+ShipGrid.StairRun*.5f)+right*side*1.7f+Vector3.up*(y0+1.5f+.95f),new Vector3(.06f,.06f,railLength),Surface.Plate,Teal,false,railRot);
                for(int k=0;k<4;k++) {float at=1+k*2;Surfaces.Block("Rail post",p,origin+fwd*at+right*side*1.7f+Vector3.up*(y0+StairHeight(at)+.48f),new Vector3(.05f,.95f,.05f),Surface.Plate,DarkSteel,false,rot);}
            }
            int deck=s.floor+2;
            Text("DEK "+deck,fixtures,origin-fwd*.13f+Vector3.up*(y0+2.74f),.06f,Cream,Quaternion.LookRotation(fwd));
            var alarm=origin+fwd*(1+ShipGrid.StairRun)+Vector3.up*(y0+5.6f);
            Shape("Alarm lamp",PrimitiveType.Cube,fixtures,alarm,new Vector3(.3f,.12f,.3f),new Color(1,.1f,.08f),false,true);
            Fx.Glow(fixtures,alarm+Vector3.down*.2f,new Color(1,.1f,.08f),8,3f).gameObject.AddComponent<EmergencyLight>();
        }
        // Entry `index`: on the top deck an airlock flight up to the cargo deck, on a lower deck a short hatch vestibule. Either
        // ends at the exit hatch (the portal back out to the wreck's deck).
        static void Entry(Transform p,Transform fixtures,InteriorLayout layout,int index)
        {
            var origin=layout.EntryOrigin(index,out var fwd);var right=new Vector3(fwd.z,0,-fwd.x);var rot=Quaternion.LookRotation(fwd);
            string name=InteriorLayout.EntryNames[index];
            if(layout.ship==ShipKind.Cruise) {CruiseVestibule(p,fixtures,origin,fwd,right,rot,ShipTimeline.FloorHeight(layout.entries[index].floor),index,name);return;}
            if(!layout.entries[index].Flight) {Hatch(p,fixtures,origin,fwd,right,rot,ShipTimeline.FloorHeight(layout.entries[index].floor),index,name);return;}
            const float y0=8;float top=ShipTimeline.CargoDeck,length=ShipGrid.AirlockLength,climb=ShipGrid.AirlockRun;
            Vector3 At(float s,float h) => origin+fwd*s+Vector3.up*h;
            Surfaces.Block("Airlock floor",p,At(climb*.5f,y0-.1f),new Vector3(4,.2f,climb),Surface.Plate,DarkSteel,true,rot);
            const int steps=11;float run=climb/steps,rise=(top-y0)/steps;
            bool flight=Model("Kit/kit_airlock_flight",p,At(0,y0),rot);
            for(int i=0;i<steps;i++) {float h=(i+1)*rise;var step=Surfaces.Block("Airlock step",p,At(run*(i+.5f),y0+h*.5f),new Vector3(3.6f,h,run+.002f),Surface.Diamond,new Color(.8f,.8f,.78f),true,rot);if(flight)Hide(step);}
            Surfaces.Block("Airlock platform",p,At(climb+(length-climb)*.5f,(y0+top)*.5f),new Vector3(3.6f,top-y0,length-climb),Surface.Grating,Color.white,true,rot);
            foreach(float side in new[]{-1f,1f})Surfaces.Block("Airlock wall",p,At(length*.5f,(y0+13)*.5f)+right*side*1.95f,new Vector3(.1f,13-y0,length),Surface.RustPlate,Color.white,true,rot);
            Surfaces.Block("Airlock end",p,At(length+.05f,(top+13)*.5f),new Vector3(4,13-top,.1f),Surface.RustPlate,Color.white,true,rot);
            Surfaces.Block("Airlock roof",p,At(length*.5f,13.1f),new Vector3(4,.2f,length),Surface.Plate,DarkSteel,true,rot);
            // Above the top deck's roof line the airlock needs its own front wall.
            Surfaces.Block("Airlock front",p,At(-.05f,(11+13)*.5f),new Vector3(4,2,.1f),Surface.RustPlate,Color.white,true,rot);
            var door=Box("Return to deck",p,At(length-.08f,top+1.2f),new Vector3(1.8f,2.4f,.15f),Teal);door.transform.localRotation=rot;
            var portal=door.AddComponent<WorldInteractable>();portal.action=ActionKind.Portal;portal.argument=index;portal.title="Kembali ke dek";
            // kit_hatch_exit stands on the end wall's face, front toward the crew; the interactable box keeps its collider.
            if(Model("Kit/kit_hatch_exit",p,At(length,top),Quaternion.LookRotation(-fwd)))Hide(door);
            else {var wheel=Shape("Hatch wheel",PrimitiveType.Cylinder,p,At(length-.2f,top+1.25f),new Vector3(.5f,.03f,.5f),Brass,false);wheel.transform.localRotation=rot*Quaternion.Euler(90,0,0);}
            Text("KELUAR / "+name,fixtures,At(length-.2f,top+2.65f),.05f,Cream,Quaternion.LookRotation(fwd));
            CeilingLamp(fixtures,At(length-2.5f,12.75f),LampWarm,index*977,1.6f,8,false);
        }
        static void Hatch(Transform p,Transform fixtures,Vector3 origin,Vector3 fwd,Vector3 right,Quaternion rot,float y0,int index,string name)
        {
            float length=ShipGrid.HatchLength;
            Vector3 At(float s,float h) => origin+fwd*s+Vector3.up*h;
            Surfaces.Block("Hatch floor",p,At(length*.5f,y0-.1f),new Vector3(4,.2f,length),Surface.Grating,Color.white,true,rot);
            foreach(float side in new[]{-1f,1f})Surfaces.Block("Hatch wall",p,At(length*.5f,y0+1.45f)+right*side*1.95f,new Vector3(.1f,2.9f,length),Surface.RustPlate,Color.white,true,rot);
            Surfaces.Block("Hatch roof",p,At(length*.5f,y0+2.8f),new Vector3(4,.2f,length),Surface.Plate,DarkSteel,true,rot);
            Surfaces.Block("Hatch end",p,At(length+.05f,y0+1.45f),new Vector3(4,2.9f,.1f),Surface.RustPlate,Color.white,true,rot);
            var door=Box("Return to deck",p,At(length-.08f,y0+1.2f),new Vector3(1.8f,2.4f,.15f),Teal);door.transform.localRotation=rot;
            var portal=door.AddComponent<WorldInteractable>();portal.action=ActionKind.Portal;portal.argument=index;portal.title="Naik ke dek";
            if(Model("Kit/kit_hatch_exit",p,At(length,y0),Quaternion.LookRotation(-fwd)))Hide(door);
            // Ladder rungs up the end wall beside the hatch: the way up to the cargo deck.
            for(int k=0;k<7;k++)Surfaces.Block("Ladder rung",p,At(length-.12f,y0+.35f+k*.36f)+right*1.35f,new Vector3(.5f,.04f,.04f),Surface.Plate,Brass,false,rot);
            Text("KELUAR / "+name,fixtures,At(length-.2f,y0+2.55f),.05f,Cream,Quaternion.LookRotation(fwd));
            CeilingLamp(fixtures,At(length-1.6f,y0+2.55f),LampWarm,index*977+5,1.3f,6,false);
        }

        // ---- Navigation -----------------------------------------------------------------------------------------------------
        static void BuildNavigation(WorldZone zone,InteriorLayout layout)
        {
            zone.ClearNavigation();
            int cells=ShipGrid.Columns*ShipGrid.Rows;
            for(int f=0;f<ShipGrid.Decks;f++)for(int z=0;z<ShipGrid.Rows;z++)for(int x=0;x<ShipGrid.Columns;x++) {
                var kind=layout.Kind(f,x,z,out _);
                if(kind==CellKind.Corridor||kind==CellKind.Room)zone.cellNodes[f*cells+x+z*ShipGrid.Columns]=zone.AddNode(ShipGrid.Center(f,x,z),kind==CellKind.Corridor?NavKind.Corridor:NavKind.Room);
            }
            for(int f=0;f<ShipGrid.Decks;f++)for(int z=0;z<ShipGrid.Rows;z++)for(int x=0;x<ShipGrid.Columns;x++)for(int dir=0;dir<2;dir++) {
                var b=new Vector2Int(x,z)+ShipGrid.Dirs[dir];
                if(!ShipGrid.Inside(b.x,b.y)||!layout.Open(f,x,z,dir))continue;
                if(!zone.cellNodes.TryGetValue(f*cells+x+z*ShipGrid.Columns,out int na)||!zone.cellNodes.TryGetValue(f*cells+b.x+b.y*ShipGrid.Columns,out int nb))continue;
                layout.Kind(f,x,z,out int ma);layout.Kind(f,b.x,b.y,out int mb);
                if(layout.Kind(f,x,z,out _)==layout.Kind(f,b.x,b.y,out _)&&ma==mb)zone.Link(na,nb);
                else {int door=zone.AddNode(ShipGrid.Center(f,x,z)+Dir3(dir)*ShipGrid.Cell*.5f,NavKind.Door);zone.Link(na,door);zone.Link(door,nb);int index=layout.DoorAt(f,x,z,dir);if(index>=0)zone.doorNodes[door]=index;}
            }
            foreach(var s in layout.stairs) {
                var fwd=Dir3(s.dir);float y0=ShipTimeline.FloorHeight(s.floor);var origin=ShipGrid.Center(s.floor,s.Low.x,s.Low.y)-fwd*ShipGrid.Cell*.5f;origin.y=0;
                int prev=zone.cellNodes[s.floor*cells+s.Entry.x+s.Entry.y*ShipGrid.Columns];int first=-1;
                foreach(float at in new[]{0f,.6f,2f,3.5f,5f,6.5f,7.5f,8f}) {int node=zone.AddNode(origin+fwd*at+Vector3.up*(y0+StairHeight(at)),NavKind.Stair);if(first<0)first=node;zone.Link(prev,node);prev=node;}
                int exit=zone.cellNodes[(s.floor+1)*cells+s.Exit.x+s.Exit.y*ShipGrid.Columns];zone.Link(prev,exit);
                zone.stairRoutes.Add((zone.cellNodes[s.floor*cells+s.Entry.x+s.Entry.y*ShipGrid.Columns],exit));
            }
            for(int i=0;i<layout.entries.Count;i++) {
                var e=layout.entries[i];var origin=layout.EntryOrigin(i,out var fwd);float y0=ShipTimeline.FloorHeight(e.floor);
                int prev=zone.cellNodes[e.floor*cells+e.x+e.z*ShipGrid.Columns];
                var route=e.Flight?new[]{(0f,8f),(2f,9f+1f/11),(4.6f,10f),(ShipGrid.AirlockEntry,10f)}:new[]{(0f,y0),(ShipGrid.HatchEntry,y0)};
                foreach(var (at,h) in route) {int node=zone.AddNode(origin+fwd*at+Vector3.up*h,NavKind.Airlock);zone.Link(prev,node);prev=node;}
                zone.airlockNodes[i]=prev;
            }
            zone.FinishNavigation();
        }
    }
}
