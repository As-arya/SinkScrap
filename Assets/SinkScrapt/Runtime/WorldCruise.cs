using UnityEngine;

namespace SinkScrapt
{
    // The abandoned cruise ship's interior look (CruiseLayout): passenger corridors, room finishes, furniture, the lobby with its
    // atrium, and the entry vestibules. Everything is built from textured boxes and primitives that keep their colliders; the
    // main pieces (beds, sofas, dining tables and chairs, slot machines, the grand piano, chandeliers and lobby pillars) are
    // drawn by Blender models (Models/Furniture/cruise_*) over hidden primitives when the model is present (Swap). Pieces follow
    // the interior's furniture rule: they stand against closed walls (at most 1 m deep, or in the corners beside a door), or on
    // the corner points shared by four cells of a large room (at most 1.3 m from that point). Both keep clear of the straight
    // lines between cell centres and doorways that monsters walk and loot is placed on.
    public static partial class WorldFactory
    {
        // Draws a cruise model in place of the primitive `parts`: their colliders stay, their renderers are hidden. The parts
        // are always built first, so the seeded random draws do not depend on whether the model exists.
        static void Swap(string model,Transform p,Vector3 at,Quaternion rot,Vector3? scale,Color? tint,params GameObject[] parts)
        {
            if(Model("Furniture/"+model,p,at,rot,scale,tint))foreach(var part in parts)Hide(part);
        }
        static readonly Color DarkWood=new(.36f,.2f,.12f),Mahogany=new(.45f,.22f,.14f),Linen=new(.78f,.76f,.7f),Gold=new(.72f,.56f,.24f),
            Velvet=new(.48f,.08f,.1f),Felt=new(.08f,.32f,.18f),Porcelain=new(.86f,.86f,.82f),Dead=new(.22f,.26f,.13f),Pale=new(.8f,.77f,.7f);
        static readonly Color[] Fabrics={new(.42f,.12f,.12f),new(.18f,.26f,.42f),new(.5f,.42f,.24f),new(.22f,.36f,.3f),new(.4f,.32f,.4f)};

        static Surface CruiseFloor(int type,out Color tint)
        {
            switch(type) {
                case CruiseLayout.Lobby: tint=new Color(.95f,.93f,.9f);return Surface.Marble;
                case CruiseLayout.Cabin: tint=new Color(.35f,.42f,.55f);return Surface.Carpet;
                case CruiseLayout.Suite: tint=new Color(.6f,.46f,.32f);return Surface.Carpet;
                case CruiseLayout.Restaurant: tint=new Color(.62f,.2f,.16f);return Surface.Carpet;
                case CruiseLayout.Theatre: tint=new Color(.5f,.1f,.12f);return Surface.Carpet;
                case CruiseLayout.Bar: tint=new Color(.55f,.4f,.3f);return Surface.Planks;
                case CruiseLayout.Casino: tint=new Color(.25f,.45f,.32f);return Surface.Carpet;
                case CruiseLayout.Boutique: tint=new Color(.92f,.9f,.86f);return Surface.Marble;
                default: tint=Color.white;return Surface.Tile;
            }
        }
        static Surface CruiseWall(int type,out Color tint)
        {
            switch(type) {
                case CruiseLayout.Lobby: tint=new Color(.88f,.82f,.68f);return Surface.Wallpaper;
                case CruiseLayout.Cabin: tint=new Color(.8f,.78f,.7f);return Surface.Wallpaper;
                case CruiseLayout.Suite: tint=new Color(.8f,.72f,.6f);return Surface.Wallpaper;
                case CruiseLayout.Restaurant: tint=new Color(.86f,.8f,.66f);return Surface.Wallpaper;
                case CruiseLayout.Theatre: tint=new Color(.6f,.28f,.28f);return Surface.Wallpaper;
                case CruiseLayout.Bar: tint=new Color(.62f,.46f,.36f);return Surface.Wood;
                case CruiseLayout.Casino: tint=new Color(.62f,.46f,.54f);return Surface.Wallpaper;
                case CruiseLayout.Boutique: tint=new Color(.86f,.8f,.82f);return Surface.Wallpaper;
                case CruiseLayout.Clinic: tint=new Color(.86f,.9f,.88f);return Surface.Bulkhead;
                default: tint=new Color(.72f,.77f,.8f);return Surface.Bulkhead;
            }
        }

        // ---- Lights ---------------------------------------------------------------------------------------------------------
        // A flush round ceiling light (cabins, halls, passenger corridors). Like the bulkhead lamps, many are dead and some flicker.
        static void FlushLight(Transform p,Vector3 pos,int seed,float intensity=1.1f,float range=6.5f,bool canDie=true)
        {
            float roll=Hash01(seed,3,5);bool dead=canDie&&roll<.3f;
            Shape("Ceiling light rim",PrimitiveType.Cylinder,p,pos+Vector3.up*.03f,new Vector3(.62f,.02f,.62f),Gold,false);
            Shape("Ceiling light",PrimitiveType.Cylinder,p,pos,new Vector3(.5f,.03f,.5f),dead?new Color(.2f,.19f,.17f):LampWarm,false,!dead);
            if(dead)return;
            var light=new GameObject("Lamp light");light.transform.SetParent(p,false);light.transform.localPosition=pos+Vector3.down*.2f;
            var l=light.AddComponent<Light>();l.type=LightType.Point;l.color=LampWarm;l.range=range;l.intensity=intensity;l.shadows=LightShadows.None;
            var flicker=light.AddComponent<LightFlicker>();flicker.depth=roll<.45f?.38f:.08f;flicker.dropout=roll<.45f?.01f:.0015f;
        }

        // ---- Corridors and the atrium balcony -------------------------------------------------------------------------------
        // A passenger corridor cell: carpet and papered walls (from the surfaces), a wooden handrail along closed walls, faded
        // pictures, and the odd flush light. No pipes or cable trays: those stay below, on the crew deck.
        static void PassengerCorridor(Transform p,Transform fixtures,InteriorLayout layout,int f,int x,int z)
        {
            var c=ShipGrid.Center(f,x,z);
            for(int dir=0;dir<4;dir++) {
                if(layout.Open(f,x,z,dir)||layout.Balcony(f,x,z,dir))continue;
                var n=Dir3(dir);var along=new Vector3(-n.z,0,n.x);var face=c+n*(ShipGrid.Cell*.5f-.1f);float roll=Hash01(x+f*17,z,dir+3);
                Surfaces.Block("Handrail",p,face-n*.06f+Vector3.up*.92f,along*3.9f+Vector3.up*.06f+Abs(n)*.07f,Surface.Wood,Mahogany,false);
                if(roll<.22f) {
                    // A faded painting: frame and a dark, water-stained canvas.
                    float shift=(Hash01(x,z+f*7,dir)-.5f)*1.6f;var at=face-n*.03f+along*shift+Vector3.up*1.65f;
                    Surfaces.Block("Picture frame",p,at,along*.9f+Vector3.up*.7f+Abs(n)*.05f,Surface.Wood,Gold*.8f,false);
                    Box("Picture",p,at-n*.03f,along*.76f+Vector3.up*.56f+Abs(n)*.02f,Fabrics[(x+z+f)%Fabrics.Length]*.45f,false);
                } else if(roll<.3f) {
                    // A brass wall sconce (unlit).
                    var at=face-n*.08f+Vector3.up*1.9f;Box("Sconce",p,at,new Vector3(.12f,.25f,.12f),Gold,false);
                    Shape("Sconce shade",PrimitiveType.Cylinder,p,at+Vector3.up*.18f-n*.06f,new Vector3(.22f,.08f,.22f),Linen*.7f,false);
                }
            }
            if(Hash01(x,z,f+31)<layout.Profile.corridorLamps)FlushLight(fixtures,c+Vector3.up*2.74f,f*100+x*11+z,1f,6f);
        }
        // The balustrade between a gallery corridor cell and the atrium over the lobby: a marble parapet with a wooden rail.
        // An invisible wall up to the ceiling keeps divers and dropped items on the gallery.
        static void Balustrade(Transform p,Vector2Int cell,int dir,int f)
        {
            var n=Dir3(dir);var along=new Vector3(Mathf.Abs(n.z),0,Mathf.Abs(n.x));var edge=ShipGrid.Center(f,cell.x,cell.y)+n*(ShipGrid.Cell*.5f-.06f);
            Surfaces.Block("Balustrade",p,edge+Vector3.up*.5f,along*4+Vector3.up*1f+Abs(n)*.14f,Surface.Marble,new Color(.85f,.83f,.78f),false);
            Surfaces.Block("Balustrade rail",p,edge+Vector3.up*1.06f,along*4.02f+Vector3.up*.08f+Abs(n)*.2f,Surface.Wood,Mahogany,false);
            for(int k=-1;k<=1;k++)Box("Baluster",p,edge+along*k*1.3f+Vector3.up*.5f,new Vector3(.16f,1,.16f),Gold*.7f,false);
            var block=new GameObject("Balustrade collider");block.transform.SetParent(p,false);block.transform.localPosition=edge+Vector3.up*1.49f;
            block.AddComponent<BoxCollider>().size=along*4+Vector3.up*2.98f+Abs(n)*.14f;
        }

        // ---- Rooms ----------------------------------------------------------------------------------------------------------
        // Lighting and centre pieces of a cruise room (its wall furniture comes from FurnishCruise).
        static void CruiseRoom(WorldZone zone,Transform p,Transform fixtures,InteriorLayout layout,RoomData room,System.Random random,int seed)
        {
            var centre=layout.Position(room);
            if(layout.IsLobby(room)) {Lobby(p,fixtures,layout,room,seed);return;}
            if(Hash01(seed,11,room.floor)<.25f)FlushLight(fixtures,centre+Vector3.up*2.74f,seed,1.1f,6+Mathf.Max(room.w,room.d)*1.5f);
            else Shape("Dead ceiling light",PrimitiveType.Cylinder,fixtures,centre+Vector3.up*2.76f,new Vector3(.5f,.03f,.5f),new Color(.2f,.19f,.17f),false);
            // Corner points shared by four cells of the room.
            int index=0;
            for(int x=room.x+1;x<room.x+room.w;x++)for(int z=room.z+1;z<room.z+room.d;z++) {
                var at=ShipGrid.Center(room.floor,x,z)-new Vector3(ShipGrid.Cell*.5f,0,ShipGrid.Cell*.5f);
                Centrepiece(p,fixtures,room.type,at,random,index++);
            }
        }
        static void Centrepiece(Transform p,Transform fixtures,int type,Vector3 at,System.Random random,int index)
        {
            float yaw=random.Next(4)*90;var rot=Quaternion.Euler(0,yaw,0);
            switch(type) {
                case CruiseLayout.Restaurant: DiningTable(p,at,random);break;
                case CruiseLayout.Theatre: {
                    // Two rows of four seats, all facing the same way (-Z): toward the stage end of the hall.
                    for(int row=0;row<2;row++)for(int k=0;k<4;k++) {
                        var seat=at+new Vector3(-1.05f+k*.7f,0,-.4f+row*.9f);
                        Surfaces.Block("Theatre seat",p,seat+Vector3.up*.22f,new Vector3(.6f,.44f,.55f),Surface.Carpet,Velvet,false);
                        Surfaces.Block("Seat back",p,seat+new Vector3(0,.62f,.25f),new Vector3(.6f,.6f,.1f),Surface.Carpet,Velvet,false);
                    }
                    var block=new GameObject("Seat rows");block.transform.SetParent(p,false);block.transform.localPosition=at+new Vector3(0,.5f,.05f);block.AddComponent<BoxCollider>().size=new Vector3(2.8f,1,1.5f);
                    break;
                }
                case CruiseLayout.Casino:
                    if(index%2==0) {
                        // A card table: green felt over a wooden rim, chairs round it, scattered chips.
                        var table=Shape("Card table",PrimitiveType.Cylinder,p,at+Vector3.up*.4f,new Vector3(2f,.4f,1.3f),DarkWood,true);table.transform.localRotation=rot;
                        Shape("Felt",PrimitiveType.Cylinder,p,at+Vector3.up*.81f,new Vector3(1.8f,.02f,1.15f),Felt,false).transform.localRotation=rot;
                        for(int k=0;k<4;k++){var dir=rot*new Vector3(Mathf.Cos(k*Mathf.PI*.5f),0,Mathf.Sin(k*Mathf.PI*.5f));Chair(p,at+Vector3.Scale(dir,new Vector3(1.15f,0,1.15f)),Quaternion.LookRotation(-dir),Velvet,random.Next(6)==0);}
                        for(int k=0;k<6;k++)Shape("Chip",PrimitiveType.Cylinder,p,at+new Vector3((float)random.NextDouble()-.5f,.83f,(float)random.NextDouble()*.6f-.3f),new Vector3(.05f,.008f,.05f),Fabrics[k%Fabrics.Length],false);
                    } else {
                        // A roulette table: the wheel in a round wooden bowl.
                        Shape("Roulette base",PrimitiveType.Cylinder,p,at+Vector3.up*.42f,new Vector3(1.4f,.42f,1.4f),DarkWood,true);
                        Shape("Roulette wheel",PrimitiveType.Cylinder,p,at+Vector3.up*.86f,new Vector3(1.1f,.04f,1.1f),new Color(.12f,.1f,.08f),false);
                        Shape("Roulette hub",PrimitiveType.Cylinder,p,at+Vector3.up*.94f,new Vector3(.18f,.08f,.18f),Gold,false);
                    }
                    break;
                case CruiseLayout.Bar: {
                    Shape("High table",PrimitiveType.Cylinder,p,at+Vector3.up*.55f,new Vector3(.7f,.55f,.7f),DarkWood,true);
                    Shape("High table top",PrimitiveType.Cylinder,p,at+Vector3.up*1.1f,new Vector3(.9f,.03f,.9f),Mahogany,false);
                    for(int k=0;k<3;k++){var dir=Quaternion.Euler(0,yaw+k*120,0)*Vector3.forward;Shape("Bar stool",PrimitiveType.Cylinder,p,at+dir*.85f+Vector3.up*.37f,new Vector3(.35f,.37f,.35f),k==1?Velvet:DarkWood,false);}
                    break;
                }
                case CruiseLayout.Suite:
                    if(index>0)break;
                    // A king-size bed on the room's middle point, headboard to one side.
                    var kingBed=Surfaces.Block("King bed",p,at+Vector3.up*.25f,new Vector3(2.1f,.5f,2.2f),Surface.Wood,DarkWood,true,rot);
                    var kingMattress=Surfaces.Block("King mattress",p,at+Vector3.up*.56f,new Vector3(2f,.14f,2.1f),Surface.Sand,Linen,false,rot);
                    var kingFabric=Fabrics[random.Next(Fabrics.Length)];
                    var kingBlanket=Surfaces.Block("King blanket",p,at+rot*new Vector3(0,.64f,.3f),new Vector3(2.04f,.06f,1.4f),Surface.Carpet,kingFabric,false,rot);
                    var kingHead=Surfaces.Block("Headboard",p,at+rot*new Vector3(0,.8f,-1.08f),new Vector3(2.2f,1.3f,.1f),Surface.Wood,Mahogany,true,rot);
                    var pillowL=Surfaces.Block("Pillow",p,at+rot*new Vector3(-.5f,.68f,-.8f),new Vector3(.7f,.14f,.4f),Surface.Sand,Linen,false,rot);
                    var pillowR=Surfaces.Block("Pillow",p,at+rot*new Vector3(.5f,.68f,-.8f),new Vector3(.7f,.14f,.4f),Surface.Sand,Linen,false,rot);
                    Swap("cruise_bed_king",p,at,rot,null,kingFabric,kingBed,kingMattress,kingBlanket,kingHead,pillowL,pillowR);
                    break;
                case CruiseLayout.Boutique:
                    GlassCase(p,at,new Vector3(1.4f,.9f,1.4f));break;
                case CruiseLayout.Laundry:
                    Surfaces.Block("Folding table",p,at+Vector3.up*.45f,new Vector3(2f,.9f,1f),Surface.Plate,new Color(.7f,.72f,.72f),true,rot);
                    Shape("Linen pile",PrimitiveType.Sphere,p,at+Vector3.up*1f,new Vector3(.9f,.25f,.6f),Linen,false);
                    break;
            }
        }
        static void DiningTable(Transform p,Vector3 at,System.Random random)
        {
            var parts=new System.Collections.Generic.List<GameObject> {
                Shape("Dining table",PrimitiveType.Cylinder,p,at+Vector3.up*.37f,new Vector3(.5f,.37f,.5f),DarkWood,true),
                Shape("Tablecloth",PrimitiveType.Cylinder,p,at+Vector3.up*.76f,new Vector3(1.3f,.03f,1.3f),Linen,false),
                Shape("Cloth drape",PrimitiveType.Cylinder,p,at+Vector3.up*.6f,new Vector3(1.28f,.15f,1.28f),Linen*.9f,false)};
            for(int k=0;k<3;k++)parts.Add(Shape("Plate",PrimitiveType.Cylinder,p,at+Quaternion.Euler(0,k*120,0)*Vector3.forward*.4f+Vector3.up*.79f,new Vector3(.25f,.005f,.25f),Porcelain,false));
            Swap("cruise_dining_table",p,at,Quaternion.identity,null,null,parts.ToArray());
            // The candle stays a primitive (it is a seeded extra on some tables).
            if(random.Next(3)==0)Shape("Candle",PrimitiveType.Cylinder,p,at+Vector3.up*.88f,new Vector3(.05f,.1f,.05f),Linen,false);
            for(int k=0;k<4;k++) {
                var dir=Quaternion.Euler(0,k*90+45,0)*Vector3.forward;Chair(p,at+dir*1.05f,Quaternion.LookRotation(-dir),Velvet,random.Next(4)==0);
            }
        }
        // A dining chair, upright or knocked over on its back.
        static void Chair(Transform p,Vector3 at,Quaternion facing,Color seat,bool toppled)
        {
            var root=new GameObject("Chair").transform;root.SetParent(p,false);root.localPosition=at;
            root.localRotation=toppled?facing*Quaternion.Euler(-80,0,0)*Quaternion.Euler(0,0,0):facing;if(toppled)root.localPosition=at+Vector3.up*.25f;
            if(Model("Furniture/cruise_chair",root,Vector3.zero,Quaternion.identity,null,seat))return;
            Surfaces.Block("Chair seat",root,Vector3.up*.45f,new Vector3(.45f,.06f,.45f),Surface.Carpet,seat,false);
            Surfaces.Block("Chair back",root,new Vector3(0,.78f,-.2f),new Vector3(.45f,.6f,.05f),Surface.Wood,DarkWood,false);
            foreach(var (lx,lz) in new[]{(-.19f,-.19f),(.19f,-.19f),(-.19f,.19f),(.19f,.19f)})Box("Chair leg",root,new Vector3(lx,.22f,lz),new Vector3(.04f,.44f,.04f),DarkWood,false);
        }
        static void GlassCase(Transform p,Vector3 at,Vector3 size)
        {
            Surfaces.Block("Case base",p,at+Vector3.up*size.y*.3f,new Vector3(size.x,size.y*.6f,size.z),Surface.Wood,DarkWood,true);
            var glass=GameObject.CreatePrimitive(PrimitiveType.Cube);glass.name="Case glass";glass.transform.SetParent(p,false);
            glass.transform.localPosition=at+Vector3.up*size.y*.8f;glass.transform.localScale=new Vector3(size.x-.04f,size.y*.4f,size.z-.04f);
            glass.GetComponent<Renderer>().sharedMaterial=Glass(new Color(.55f,.7f,.72f,.3f));Object.Destroy(glass.GetComponent<Collider>());
            Shape("Watch",PrimitiveType.Cylinder,p,at+Vector3.up*(size.y*.62f),new Vector3(.12f,.01f,.12f),Gold,false);
        }

        // The lobby: eight marble pillars, a dry fountain, a grand piano, sofa groups, one chandelier still hanging (and lit)
        // and one that has crashed to the floor.
        static void Lobby(Transform p,Transform fixtures,InteriorLayout layout,RoomData room,int seed)
        {
            float y=ShipTimeline.FloorHeight(room.floor),ceiling=ShipTimeline.FloorHeight(room.floor+2)-.22f-y;
            Vector3 Corner(int i,int j) => ShipGrid.Center(room.floor,room.x+1+i,room.z+1+j)-new Vector3(ShipGrid.Cell*.5f,0,ShipGrid.Cell*.5f);
            foreach(int i in new[]{0,4})foreach(int j in new[]{0,2,3,5}) {
                var at=Corner(i,j);
                var pillar=Surfaces.Block("Pillar",p,at+Vector3.up*ceiling*.5f,new Vector3(.7f,ceiling,.7f),Surface.Marble,new Color(.88f,.86f,.82f),true);
                var baseBand=Surfaces.Block("Pillar band",p,at+Vector3.up*.15f,new Vector3(.86f,.3f,.86f),Surface.Plate,Gold,false);
                var topBand=Surfaces.Block("Pillar band",p,at+Vector3.up*(ceiling-.2f),new Vector3(.86f,.3f,.86f),Surface.Plate,Gold,false);
                // cruise_pillar is modelled 5.78 m tall (the lobby's floor-to-atrium-ceiling height); stretch it to fit.
                Swap("cruise_pillar",p,at,Quaternion.identity,new Vector3(1,ceiling/5.78f,1),null,pillar,baseBand,topBand);
            }
            // The dry fountain: a round basin with a three-tier column, stagnant water at the bottom.
            var fountain=Corner(2,2);
            Shape("Fountain basin",PrimitiveType.Cylinder,p,fountain+Vector3.up*.3f,new Vector3(2.4f,.3f,2.4f),new Color(.82f,.8f,.76f),true);
            Shape("Fountain water",PrimitiveType.Cylinder,p,fountain+Vector3.up*.52f,new Vector3(2.15f,.02f,2.15f),new Color(.06f,.1f,.08f),false);
            for(int k=0;k<3;k++){Shape("Fountain tier",PrimitiveType.Cylinder,p,fountain+Vector3.up*(.9f+k*.55f),new Vector3(.25f-k*.05f,.3f,.25f-k*.05f),new Color(.82f,.8f,.76f),false);Shape("Fountain bowl",PrimitiveType.Cylinder,p,fountain+Vector3.up*(1.2f+k*.55f),new Vector3(1.1f-k*.3f,.05f,1.1f-k*.3f),new Color(.82f,.8f,.76f),false);}
            // A grand piano, lid propped open.
            var piano=Corner(2,3);var turn=Quaternion.Euler(0,25,0);
            var pianoParts=new System.Collections.Generic.List<GameObject>{Surfaces.Block("Piano body",p,piano+Vector3.up*.7f,new Vector3(1.45f,.35f,1.9f),Surface.Wood,new Color(.06f,.05f,.05f),true,turn)};
            foreach(var (lx,lz) in new[]{(-.6f,-.8f),(.6f,-.8f),(0f,.85f)})pianoParts.Add(Box("Piano leg",p,piano+turn*new Vector3(lx,.27f,lz),new Vector3(.1f,.55f,.1f),new Color(.06f,.05f,.05f),false));
            pianoParts.Add(Surfaces.Block("Piano lid",p,piano+turn*new Vector3(.3f,1.35f,.1f),new Vector3(.05f,1.2f,1.8f),Surface.Wood,new Color(.06f,.05f,.05f),false,turn*Quaternion.Euler(0,0,-35)));
            pianoParts.Add(Surfaces.Block("Piano keys",p,piano+turn*new Vector3(0,.78f,-1.02f),new Vector3(1.3f,.04f,.2f),Surface.Plate,Porcelain,false,turn));
            // The model's keyboard is its front (+Z); here the keys face -Z.
            Swap("cruise_piano",p,piano,turn*Quaternion.Euler(0,180,0),null,null,pianoParts.ToArray());
            // Sofa groups round low tables.
            foreach(var (i,j) in new[]{(1,1),(3,1),(1,4)}) {
                var at=Corner(i,j);Shape("Coffee table",PrimitiveType.Cylinder,p,at+Vector3.up*.22f,new Vector3(.9f,.22f,.9f),DarkWood,true);
                foreach(float s in new[]{-1f,1f}) {
                    var facing=Quaternion.Euler(0,s<0?0:180,0);var seat=at+new Vector3(0,0,s*1.05f);var fabric=Fabrics[(i+j+(s<0?0:1))%Fabrics.Length];
                    var sofa=Surfaces.Block("Lobby sofa",p,seat+Vector3.up*.22f,new Vector3(1.9f,.44f,.75f),Surface.Carpet,fabric,true,facing);
                    var back=Surfaces.Block("Sofa back",p,seat+facing*new Vector3(0,.62f,-.3f),new Vector3(1.9f,.6f,.18f),Surface.Carpet,fabric*.85f,false,facing);
                    Swap("cruise_sofa",p,seat,facing,new Vector3(1.9f/2.8f,1,.75f/.84f),fabric,sofa,back);
                }
            }
            // The hanging chandelier over the middle of the lobby, still lit, and the one that fell.
            var middle=layout.Position(room);var hang=middle+Vector3.up*(ceiling-1.6f);
            var lit=new System.Collections.Generic.List<GameObject>{Box("Chandelier chain",fixtures,hang+Vector3.up*.9f,new Vector3(.04f,1.4f,.04f),Gold,false)};
            for(int ring=0;ring<3;ring++) {
                float r=1.2f-ring*.35f;
                for(int k=0;k<10-ring*2;k++) {var a=k*Mathf.PI*2/(10-ring*2);lit.Add(Shape("Crystal",PrimitiveType.Cube,fixtures,hang+new Vector3(Mathf.Cos(a)*r,-ring*.35f,Mathf.Sin(a)*r),new Vector3(.08f,.22f,.08f),new Color(1,.86f,.6f),false,true));}
                lit.Add(Shape("Chandelier ring",PrimitiveType.Cylinder,fixtures,hang+Vector3.up*(.12f-ring*.35f),new Vector3(r*2,.02f,r*2),Gold,false));
            }
            Swap("cruise_chandelier",fixtures,hang,Quaternion.identity,null,null,lit.ToArray());
            var glow=Fx.Glow(fixtures,hang+Vector3.down*.6f,new Color(1,.74f,.45f),18,2.4f);var flicker=glow.gameObject.AddComponent<LightFlicker>();flicker.depth=.3f;flicker.dropout=.006f;
            var fallen=Corner(3,4);var wreck=new System.Collections.Generic.List<GameObject>();
            for(int k=0;k<14;k++) {
                var a=k*.9f;float r=.3f+(k%4)*.25f;var piece=Shape("Fallen crystal",PrimitiveType.Cube,p,fallen+new Vector3(Mathf.Cos(a)*r,.05f,Mathf.Sin(a)*r),new Vector3(.1f,.08f,.25f),new Color(.75f,.7f,.55f),false);
                piece.transform.localRotation=Quaternion.Euler(0,k*47,k*13);wreck.Add(piece);
            }
            var bent=Shape("Fallen chandelier ring",PrimitiveType.Cylinder,p,fallen+Vector3.up*.2f,new Vector3(2f,.03f,2f),Gold,false);bent.transform.localRotation=Quaternion.Euler(18,30,0);wreck.Add(bent);
            wreck.Add(Surfaces.Block("Fallen chandelier",p,fallen+Vector3.up*.25f,new Vector3(1.4f,.5f,1.4f),Surface.Plate,Gold*.6f,true,Quaternion.Euler(0,30,0)));
            wreck.Add(Box("Snapped chain",p,fallen+new Vector3(.3f,.04f,.9f),new Vector3(.04f,.04f,1.4f),Gold,false));
            Swap("cruise_chandelier_fallen",p,fallen,Quaternion.Euler(0,30,0),null,null,wreck.ToArray());
            // Two of the eight wall lights still glow faintly.
            foreach(int k in new[]{1,4}) {var at=Corner(k==1?0:4,k)+Vector3.up*(ceiling-.6f);Fx.Glow(fixtures,at,new Color(1,.7f,.42f),9,.9f).gameObject.AddComponent<LightFlicker>().depth=.4f;}
        }

        // One piece of cruise furniture against the wall facing `n` (FurnishCruise follows Furnish's contract: `length` 3.6 for a
        // closed wall, 0.9 for a corner beside a door; `offset` along the wall; never deeper than 1 m).
        static void FurnishCruise(Transform p,Transform fixtures,int type,Vector3 c,Vector3 n,Vector3 along,System.Random random,float length,float offset)
        {
            var rot=Quaternion.LookRotation(-n);var wall=c+n*1.9f+along*offset;int pick=random.Next(4);bool corner=length<1.5f;
            // Centre of a piece whose middle stands `from` metres off the wall face.
            Vector3 D(float from,float height,float shift=0) => wall-n*from+along*shift+Vector3.up*height;
            GameObject B(string name,Vector3 at,Vector3 size,Surface s,Color tint,bool collider=true) => Surfaces.Block(name,p,at,size,s,tint,collider,rot);
            var fabric=Fabrics[random.Next(Fabrics.Length)];
            switch(type) {
                case CruiseLayout.Cabin:
                    if(corner) {if(pick<2)Nightstand(p,D(.25f,0),rot);else Suitcase(p,D(.2f,0),rot,fabric);}
                    else if(pick<2) {Bed(p,D(.5f,0),rot,along,fabric);Nightstand(p,D(.25f,0,1.45f),rot);}
                    else if(pick==2) {Wardrobe(p,D(.3f,0,-.8f),rot);Suitcase(p,D(.2f,0,.6f),rot,fabric);}
                    else Desk(p,D(.3f,0),rot,along);
                    break;
                case CruiseLayout.Suite:
                    if(corner) FloorLamp(p,D(.25f,0));
                    else if(pick==0) {Bed(p,D(.5f,0),rot,along,fabric);Nightstand(p,D(.25f,0,1.45f),rot);}
                    else if(pick==1) Sofa(p,D(.42f,0),rot,2.4f,fabric);
                    else if(pick==2) {Wardrobe(p,D(.3f,0,-.7f),rot);Wardrobe(p,D(.3f,0,.5f),rot);}
                    else Desk(p,D(.3f,0),rot,along);
                    break;
                case CruiseLayout.Lobby:
                    // One reception desk, on the south wall facing the main door's side of the lobby; elsewhere sofas,
                    // planters and a luggage trolley.
                    bool reception=!corner&&n.z<-.5f&&Mathf.Abs(c.x-ShipGrid.X(CruiseLayout.LobbyX+4))<.1f;
                    if(corner) Planter(p,D(.4f,0));
                    else if(!reception&&pick==0) Sofa(p,D(.42f,0),rot,3f,fabric);
                    else if(reception) {
                        B("Reception desk",D(.45f,.55f),new Vector3(3.2f,1.1f,.6f),Surface.Wood,Mahogany);B("Desk top",D(.45f,1.12f),new Vector3(3.3f,.05f,.68f),Surface.Marble,Color.white,false);
                        Shape("Desk bell",PrimitiveType.Sphere,p,D(.45f,1.18f,.8f),new Vector3(.12f,.08f,.12f),Gold,false);
                        Text("RESEPSIONIS",fixtures,wall-n*.03f+Vector3.up*2.2f,.05f,Gold,Quaternion.LookRotation(n));
                    }
                    else if(pick==1) Sofa(p,D(.42f,0),rot,3f,fabric);
                    else if(pick==2) {Planter(p,D(.4f,0,-1.1f));Planter(p,D(.4f,0,1.1f));}
                    else {
                        // A brass luggage trolley with abandoned suitcases.
                        B("Trolley base",D(.45f,.15f),new Vector3(1.4f,.06f,.7f),Surface.Plate,Gold,true);
                        foreach(float s in new[]{-.65f,.65f})Box("Trolley post",p,D(.45f,1f,s),new Vector3(.05f,1.7f,.05f),Gold,false);
                        Box("Trolley bar",p,D(.45f,1.85f),Abs(along)*1.35f+new Vector3(.05f,.05f,.05f),Gold,false);
                        for(int k=0;k<3;k++)Suitcase(p,D(.45f,.18f+k*.32f,-.3f+k*.3f),rot*Quaternion.Euler(90,0,k*15),Fabrics[(k+pick)%Fabrics.Length]);
                    }
                    break;
                case CruiseLayout.Restaurant:
                    if(corner) Planter(p,D(.4f,0));
                    else if(pick==0) {
                        B("Buffet counter",D(.4f,.45f),new Vector3(3.2f,.9f,.8f),Surface.Wood,Mahogany);B("Buffet top",D(.4f,.92f),new Vector3(3.3f,.05f,.85f),Surface.Marble,Color.white,false);
                        for(int k=0;k<3;k++){Shape("Chafing dish",PrimitiveType.Cylinder,p,D(.4f,1f,-1.05f+k*1.05f),new Vector3(.5f,.06f,.4f),new Color(.6f,.62f,.6f),false);Shape("Dish lid",PrimitiveType.Sphere,p,D(.4f,1.07f,-1.05f+k*1.05f),new Vector3(.45f,.14f,.35f),new Color(.6f,.62f,.6f),false);}
                    }
                    else if(pick==1) Sofa(p,D(.42f,0),rot,3.2f,Velvet);
                    else {
                        B("Sideboard",D(.3f,.45f),new Vector3(2.4f,.9f,.55f),Surface.Wood,DarkWood);
                        for(int k=0;k<5;k++)Shape("Stacked plates",PrimitiveType.Cylinder,p,D(.3f,.92f+k*.025f,-.6f),new Vector3(.28f,.01f,.28f),Porcelain,false);
                        for(int k=0;k<4;k++)Shape("Wine bottle",PrimitiveType.Cylinder,p,D(.3f,1.06f,.3f+k*.18f),new Vector3(.08f,.14f,.08f),new Color(.1f,.2f,.12f),false);
                    }
                    break;
                case CruiseLayout.Theatre:
                    if(corner) B("Speaker",D(.3f,.6f),new Vector3(.6f,1.2f,.55f),Surface.Plate,new Color(.08f,.08f,.08f));
                    else if(pick<2) {
                        B("Stage",D(.5f,.35f),new Vector3(3.6f,.7f,1f),Surface.Planks,new Color(.5f,.36f,.26f));
                        B("Curtain",D(.07f,1.75f),new Vector3(3.6f,2.6f,.12f),Surface.Carpet,Velvet,false);
                        Text("PERTUNJUKAN MALAM INI",fixtures,wall-n*.15f+Vector3.up*2.7f,.04f,Gold,Quaternion.LookRotation(n));
                    }
                    else for(int k=0;k<4;k++) {var seat=D(.35f,0,-1.05f+k*.7f);B("Wall seat",seat+Vector3.up*.22f,new Vector3(.6f,.44f,.55f),Surface.Carpet,Velvet,false);B("Wall seat back",D(.06f,.6f,-1.05f+k*.7f),new Vector3(.6f,.7f,.1f),Surface.Carpet,Velvet,false);}
                    if(!corner&&pick>=2){var block=new GameObject("Wall seats");block.transform.SetParent(p,false);block.transform.localPosition=D(.35f,.45f);block.transform.localRotation=rot;block.AddComponent<BoxCollider>().size=new Vector3(2.8f,.9f,.65f);}
                    break;
                case CruiseLayout.Bar:
                    if(corner) Armchair(p,D(.45f,0),rot,fabric);
                    else if(pick<2) {
                        B("Back bar",D(.15f,1.1f),new Vector3(3.2f,2.2f,.3f),Surface.Wood,DarkWood);
                        for(int shelf=0;shelf<2;shelf++)for(int k=0;k<7;k++)Shape("Bottle",PrimitiveType.Cylinder,p,D(.36f,1.12f+shelf*.55f,-1.35f+k*.45f),new Vector3(.08f,.14f,.08f),Fabrics[(k+shelf)%Fabrics.Length]*.6f+new Color(.05f,.1f,.05f),false);
                        B("Bottle shelf",D(.38f,1f),new Vector3(3.2f,.04f,.16f),Surface.Wood,Mahogany,false);B("Bottle shelf",D(.38f,1.55f),new Vector3(3.2f,.04f,.16f),Surface.Wood,Mahogany,false);
                        B("Bar counter",D(.8f,.55f),new Vector3(3.2f,1.1f,.4f),Surface.Wood,Mahogany);B("Bar top",D(.82f,1.12f),new Vector3(3.3f,.05f,.5f),Surface.Marble,new Color(.3f,.28f,.26f),false);
                        for(int k=0;k<3;k++)Shape("Bar stool",PrimitiveType.Cylinder,p,D(1.25f,.38f,-1.1f+k*1.1f),new Vector3(.35f,.38f,.35f),Velvet,false);
                    }
                    else if(pick==2) Sofa(p,D(.42f,0),rot,2.8f,Velvet);
                    else {
                        // A jukebox, its panel still faintly lit.
                        B("Jukebox",D(.35f,.75f),new Vector3(1f,1.5f,.65f),Surface.Wood,Mahogany);
                        Shape("Jukebox glow",PrimitiveType.Cube,p,D(.69f,1.1f),new Vector3(.7f,.5f,.02f),new Color(1,.5f,.25f),false,true).transform.localRotation=rot;
                    }
                    break;
                case CruiseLayout.Casino:
                    if(corner) SlotMachine(p,fixtures,D(.3f,0),rot,random);
                    else if(pick<3) for(int k=-1;k<=1;k++)SlotMachine(p,fixtures,D(.3f,0,k*1.05f),rot,random);
                    else {
                        // The cashier's cage.
                        B("Cashier counter",D(.5f,.55f),new Vector3(3.2f,1.1f,.6f),Surface.Wood,DarkWood);
                        for(int k=0;k<9;k++)Box("Cage bar",p,D(.75f,1.6f,-1.5f+k*.375f),new Vector3(.03f,1,.03f),Gold,false);
                        Text("KASIR",fixtures,wall-n*.03f+Vector3.up*2.3f,.05f,Gold,Quaternion.LookRotation(n));
                    }
                    break;
                case CruiseLayout.Boutique:
                    if(corner) Mannequin(p,D(.4f,0),rot);
                    else if(pick==0) {
                        // A clothes rail with garments still on it.
                        foreach(float s in new[]{-1.3f,1.3f})Box("Rail post",p,D(.45f,.8f,s),new Vector3(.04f,1.6f,.04f),Gold,false);
                        Box("Clothes rail",p,D(.45f,1.55f),Abs(along)*2.65f+new Vector3(.04f,.04f,.04f),Gold,false);
                        for(int k=0;k<9;k++)B("Garment",D(.45f,1.05f,-1.15f+k*.29f),new Vector3(.06f,.95f,.55f),Surface.Carpet,Fabrics[(k*3+pick)%Fabrics.Length],false);
                        var rail=new GameObject("Rail collider");rail.transform.SetParent(p,false);rail.transform.localPosition=D(.45f,.8f);rail.transform.localRotation=rot;rail.AddComponent<BoxCollider>().size=new Vector3(2.7f,1.6f,.6f);
                    }
                    else if(pick==1) GlassCase(p,D(.4f,0),new Vector3(2.6f,.95f,.6f));
                    else if(pick==2) Shelf(p,D(.3f,0),rot,3.2f,random);
                    else {Mannequin(p,D(.4f,0,-.8f),rot);Mannequin(p,D(.4f,0,.8f),rot*Quaternion.Euler(0,25,0));}
                    break;
                case CruiseLayout.Clinic:
                    if(corner) {Box("IV stand",p,D(.3f,.9f),new Vector3(.04f,1.8f,.04f),new Color(.7f,.7f,.7f),false);Box("IV bag",p,D(.3f,1.65f,.08f),new Vector3(.14f,.22f,.05f),new Color(.55f,.65f,.62f),false);}
                    else if(pick<2) {
                        B("Hospital bed",D(.5f,.35f),new Vector3(2f,.7f,.95f),Surface.Plate,new Color(.75f,.77f,.78f));
                        B("Sheet",D(.5f,.73f,.2f),new Vector3(1.5f,.08f,.92f),Surface.Sand,Linen,false);B("Pillow",D(.5f,.8f,-.75f),new Vector3(.4f,.12f,.6f),Surface.Sand,Linen,false);
                        B("Bed rail",D(.97f,.9f),new Vector3(1.6f,.3f,.04f),Surface.Plate,new Color(.7f,.7f,.7f),false);
                    }
                    else if(pick==2) {
                        B("Medicine cabinet",D(.15f,1.5f),new Vector3(1.2f,1.1f,.3f),Surface.Plate,Porcelain);
                        B("Red cross",D(.31f,1.6f),new Vector3(.36f,.1f,.02f),Surface.Plate,RedPaint,false);B("Red cross",D(.31f,1.6f),new Vector3(.1f,.36f,.02f),Surface.Plate,RedPaint,false);
                        B("Clinic counter",D(.3f,.45f),new Vector3(2.4f,.9f,.6f),Surface.Plate,new Color(.8f,.82f,.82f));
                    }
                    else {
                        Box("Curtain rail",p,D(.9f,2.4f),Abs(along)*3.2f+new Vector3(.03f,.03f,.03f),new Color(.7f,.7f,.7f),false);
                        B("Privacy curtain",D(.9f,1.4f,-.6f),new Vector3(1.8f,2f,.03f),Surface.Sand,new Color(.55f,.68f,.66f),false);
                    }
                    break;
                case CruiseLayout.Laundry:
                    if(corner) {B("Laundry basket",D(.3f,.3f),new Vector3(.6f,.6f,.45f),Surface.Planks,new Color(.7f,.6f,.45f));Shape("Laundry",PrimitiveType.Sphere,p,D(.3f,.62f),new Vector3(.55f,.2f,.4f),Linen,false);}
                    else if(pick<2) for(int k=-1;k<=1;k++) {
                        B("Washing machine",D(.36f,.45f,k*.95f),new Vector3(.75f,.9f,.72f),Surface.Plate,Porcelain);
                        var door=Shape("Washer door",PrimitiveType.Cylinder,p,D(.73f,.5f,k*.95f),new Vector3(.45f,.02f,.45f),new Color(.12f,.16f,.17f),false);door.transform.localRotation=rot*Quaternion.Euler(90,0,0);
                    }
                    else if(pick==2) {B("Folding bench",D(.4f,.45f),new Vector3(3f,.9f,.8f),Surface.Plate,new Color(.7f,.72f,.72f));Shape("Linen pile",PrimitiveType.Sphere,p,D(.4f,.98f,-.6f),new Vector3(.8f,.25f,.55f),Linen,false);}
                    else for(int k=-1;k<=1;k++)for(int h=0;h<2;h++)B("Dryer",D(.36f,.4f+h*.82f,k*.95f),new Vector3(.75f,.8f,.72f),Surface.Plate,new Color(.82f,.84f,.84f),h==0);
                    break;
            }
        }
        // Single berth along the wall (2 m long, 0.95 m deep), headboard at one end.
        static void Bed(Transform p,Vector3 at,Quaternion rot,Vector3 along,Color blanket)
        {
            // `along` is the wall direction, which is local +X of `rot`: the model's headboard is on its -X end.
            Swap("cruise_bed",p,at,rot,null,blanket,
                Surfaces.Block("Bed frame",p,at+Vector3.up*.22f,new Vector3(2f,.44f,.95f),Surface.Wood,DarkWood,true,rot),
                Surfaces.Block("Mattress",p,at+Vector3.up*.5f,new Vector3(1.9f,.14f,.86f),Surface.Sand,Linen,false,rot),
                Surfaces.Block("Blanket",p,at+Vector3.up*.6f+along*.3f,new Vector3(1.3f,.06f,.9f),Surface.Carpet,blanket,false,rot),
                Surfaces.Block("Pillow",p,at+Vector3.up*.62f-along*.72f,new Vector3(.4f,.12f,.62f),Surface.Sand,Linen,false,rot),
                Surfaces.Block("Headboard",p,at+Vector3.up*.6f-along*1.02f,new Vector3(.08f,1.2f,.95f),Surface.Wood,Mahogany,false,rot));
        }
        static void Nightstand(Transform p,Vector3 at,Quaternion rot)
        {
            Surfaces.Block("Nightstand",p,at+Vector3.up*.3f,new Vector3(.45f,.6f,.45f),Surface.Wood,DarkWood,true,rot);
            Shape("Lamp base",PrimitiveType.Cylinder,p,at+Vector3.up*.75f,new Vector3(.08f,.15f,.08f),Gold,false);
            Shape("Lamp shade",PrimitiveType.Cylinder,p,at+Vector3.up*.95f,new Vector3(.28f,.1f,.28f),Linen*.8f,false);
        }
        static void Wardrobe(Transform p,Vector3 at,Quaternion rot)
        {
            Surfaces.Block("Wardrobe",p,at+Vector3.up*1f,new Vector3(1.1f,2f,.6f),Surface.Wood,DarkWood,true,rot);
            Surfaces.Block("Wardrobe seam",p,at+rot*new Vector3(0,1f,.31f),new Vector3(.02f,1.9f,.01f),Surface.Plate,new Color(.05f,.04f,.03f),false,rot);
            foreach(float s in new[]{-.08f,.08f})Box("Wardrobe handle",p,at+rot*new Vector3(s,1.1f,.32f),new Vector3(.03f,.18f,.03f),Gold,false);
        }
        static void Desk(Transform p,Vector3 at,Quaternion rot,Vector3 along)
        {
            Surfaces.Block("Desk",p,at+Vector3.up*.37f,new Vector3(1.3f,.74f,.55f),Surface.Wood,DarkWood,true,rot);
            Surfaces.Block("Mirror",p,at+rot*new Vector3(0,1.4f,-.26f),new Vector3(1f,.8f,.03f),Surface.Plate,new Color(.25f,.3f,.32f),false,rot);
            Surfaces.Block("Desk chair",p,at+rot*new Vector3(.2f,.23f,.75f),new Vector3(.45f,.46f,.45f),Surface.Carpet,Velvet,false,rot*Quaternion.Euler(0,20,0));
        }
        static void Suitcase(Transform p,Vector3 at,Quaternion rot,Color color)
        {
            Surfaces.Block("Suitcase",p,at+rot*new Vector3(0,.3f,0),new Vector3(.5f,.6f,.25f),Surface.Corrugated,color,true,rot);
            Box("Suitcase handle",p,at+rot*new Vector3(0,.64f,0),new Vector3(.18f,.05f,.04f),new Color(.08f,.08f,.08f),false);
        }
        static void Sofa(Transform p,Vector3 at,Quaternion rot,float length,Color fabric)
        {
            var seat=Surfaces.Block("Sofa",p,at+Vector3.up*.22f,new Vector3(length,.44f,.84f),Surface.Carpet,fabric,true,rot);
            var back=Surfaces.Block("Sofa back",p,at+rot*new Vector3(0,.62f,-.32f),new Vector3(length,.6f,.2f),Surface.Carpet,fabric*.85f,false,rot);
            var armL=Surfaces.Block("Sofa arm",p,at+rot*new Vector3(-(length*.5f-.1f),.55f,0),new Vector3(.2f,.3f,.84f),Surface.Carpet,fabric*.85f,false,rot);
            var armR=Surfaces.Block("Sofa arm",p,at+rot*new Vector3(length*.5f-.1f,.55f,0),new Vector3(.2f,.3f,.84f),Surface.Carpet,fabric*.85f,false,rot);
            // Two models: the sofa (2.8 m) and the armchair (0.85 m), stretched to the length.
            bool single=length<1.2f;
            Swap(single?"cruise_armchair":"cruise_sofa",p,at,rot,new Vector3(length/(single?.85f:2.8f),1,1),fabric,seat,back,armL,armR);
        }
        static void Armchair(Transform p,Vector3 at,Quaternion rot,Color fabric) => Sofa(p,at,rot,.85f,fabric);
        static void Planter(Transform p,Vector3 at)
        {
            Shape("Planter",PrimitiveType.Cylinder,p,at+Vector3.up*.35f,new Vector3(.65f,.35f,.65f),new Color(.55f,.5f,.42f),true);
            Shape("Dead plant",PrimitiveType.Sphere,p,at+Vector3.up*1.1f,new Vector3(.8f,1f,.8f),Dead,false);
            for(int k=0;k<3;k++){var stalk=Box("Dry stalk",p,at+Vector3.up*1.2f+Quaternion.Euler(0,k*120,0)*Vector3.forward*.25f,new Vector3(.03f,.9f,.03f),new Color(.35f,.3f,.18f),false);stalk.transform.localRotation=Quaternion.Euler(20,k*120,0);}
        }
        static void FloorLamp(Transform p,Vector3 at)
        {
            Shape("Lamp foot",PrimitiveType.Cylinder,p,at+Vector3.up*.02f,new Vector3(.35f,.02f,.35f),Gold,false);Box("Lamp pole",p,at+Vector3.up*.8f,new Vector3(.04f,1.6f,.04f),Gold,false);
            Shape("Floor lamp shade",PrimitiveType.Cylinder,p,at+Vector3.up*1.65f,new Vector3(.45f,.16f,.45f),Linen*.8f,false);
        }
        static void Mannequin(Transform p,Vector3 at,Quaternion rot)
        {
            Shape("Mannequin stand",PrimitiveType.Cylinder,p,at+Vector3.up*.02f,new Vector3(.4f,.02f,.4f),new Color(.1f,.1f,.1f),false);
            Box("Mannequin pole",p,at+Vector3.up*.35f,new Vector3(.04f,.7f,.04f),new Color(.1f,.1f,.1f),false);
            var body=Shape("Mannequin",PrimitiveType.Capsule,p,at+Vector3.up*1.2f,new Vector3(.4f,.48f,.26f),Pale,true);body.transform.localRotation=rot;
            Shape("Mannequin head",PrimitiveType.Sphere,p,at+Vector3.up*1.85f,new Vector3(.22f,.27f,.22f),Pale,false);
        }
        static void SlotMachine(Transform p,Transform fixtures,Vector3 at,Quaternion rot,System.Random random)
        {
            var colour=random.Next(2)==0?new Color(.5f,.08f,.08f):new Color(.55f,.42f,.15f);
            var cabinet=Surfaces.Block("Slot machine",p,at+Vector3.up*.85f,new Vector3(.75f,1.7f,.6f),Surface.Plate,colour,true,rot);
            var top=Surfaces.Block("Slot top",p,at+rot*new Vector3(0,1.78f,0),new Vector3(.75f,.15f,.6f),Surface.Plate,Gold,false,rot);
            // The model's reel window sits just behind the code's screen, which keeps its glow and blinking.
            Swap("cruise_slot_machine",p,at,rot,null,colour,cabinet,top,Box("Slot lever",p,at+rot*new Vector3(.42f,1.15f,0),new Vector3(.04f,.4f,.04f),new Color(.7f,.7f,.7f),false));
            var screen=Shape("Slot screen",PrimitiveType.Cube,fixtures,at+rot*new Vector3(0,1.25f,.31f),new Vector3(.5f,.35f,.02f),new Color(.9f,.65f,.3f),false,random.Next(3)>0);screen.transform.localRotation=rot;
            if(random.Next(3)>0){var blink=screen.AddComponent<Blinker>();blink.period=.4f+(float)random.NextDouble()*1.2f;blink.onFraction=.7f;}
        }

        // ---- Entries --------------------------------------------------------------------------------------------------------
        // Cruise entry vestibule: A is the main gangway door (brass and glass, carpet runner), B and C are emergency exits (steel
        // push-bar doors under green signs). Both end at the portal back out onto the ship's exterior.
        static void CruiseVestibule(Transform p,Transform fixtures,Vector3 origin,Vector3 fwd,Vector3 right,Quaternion rot,float y0,int index,string name)
        {
            float length=ShipGrid.HatchLength;bool main=index==0;
            Vector3 At(float s,float h) => origin+fwd*s+Vector3.up*h;
            Surfaces.Block("Vestibule floor",p,At(length*.5f,y0-.1f),new Vector3(4,.2f,length),main?Surface.Marble:Surface.Diamond,Color.white,true,rot);
            if(main)Surfaces.Block("Carpet runner",p,At(length*.5f,y0+.01f),new Vector3(1.6f,.02f,length),Surface.Carpet,CorridorCarpet,false,rot);
            foreach(float side in new[]{-1f,1f})Surfaces.Block("Vestibule wall",p,At(length*.5f,y0+1.45f)+right*side*1.95f,new Vector3(.1f,2.9f,length),main?Surface.Wallpaper:Surface.Bulkhead,main?CorridorPaper:new Color(.7f,.74f,.72f),true,rot);
            Surfaces.Block("Vestibule roof",p,At(length*.5f,y0+2.8f),new Vector3(4,.2f,length),Surface.Plate,DarkSteel,true,rot);
            Surfaces.Block("Vestibule end",p,At(length+.05f,y0+1.45f),new Vector3(4,2.9f,.1f),main?Surface.Wallpaper:Surface.Bulkhead,main?CorridorPaper:new Color(.7f,.74f,.72f),true,rot);
            var door=Box("Return to deck",p,At(length-.08f,y0+1.2f),new Vector3(1.8f,2.4f,.15f),main?Gold*.6f:new Color(.5f,.55f,.52f));door.transform.localRotation=rot;
            var portal=door.AddComponent<WorldInteractable>();portal.action=ActionKind.Portal;portal.argument=index;
            portal.title=main?"Keluar ke tangga kapal":"Keluar darurat ke geladak";
            if(main) {
                // Double glass doors in a brass frame, a welcome sign above.
                foreach(float s in new[]{-.46f,.46f}) {
                    var pane=GameObject.CreatePrimitive(PrimitiveType.Cube);pane.name="Door glass";pane.transform.SetParent(p,false);pane.transform.localPosition=At(length-.17f,y0+1.25f)+right*s;
                    pane.transform.localRotation=rot;pane.transform.localScale=new Vector3(.8f,2.1f,.03f);pane.GetComponent<Renderer>().sharedMaterial=Glass(new Color(.35f,.45f,.45f,.45f));Object.Destroy(pane.GetComponent<Collider>());
                    Box("Door handle",p,At(length-.22f,y0+1.15f)+right*s*.2f,new Vector3(.04f,.5f,.04f),Gold,false);
                }
                Text("SELAMAT DATANG DI BINTANG SAMUDRA",fixtures,At(length-.2f,y0+2.6f),.021f,Gold,Quaternion.LookRotation(fwd));
                Text("PINTU UTAMA / "+name,fixtures,At(-.08f,y0+2.74f),.04f,Cream,Quaternion.LookRotation(fwd));
                FlushLight(fixtures,At(length*.5f,y0+2.68f),index*977+5,1.3f,6,false);
            } else {
                Box("Push bar",p,At(length-.2f,y0+1f),new Vector3(1.4f,.08f,.08f),new Color(.75f,.75f,.72f),false).transform.localRotation=rot;
                var sign=Shape("Exit sign",PrimitiveType.Cube,fixtures,At(length-.12f,y0+2.55f),new Vector3(2.4f,.3f,.05f),new Color(.15f,.9f,.35f),false,true);sign.transform.localRotation=rot;
                Text("KELUAR DARURAT / "+name,fixtures,At(length-.2f,y0+2.55f),.026f,new Color(.02f,.12f,.04f),Quaternion.LookRotation(fwd));
                Fx.Glow(fixtures,At(length-.6f,y0+2.4f),new Color(.2f,1,.4f),6,1.1f);
                Text("KELUAR DARURAT",fixtures,At(-.08f,y0+2.74f),.04f,new Color(.3f,1,.5f),Quaternion.LookRotation(fwd));
            }
        }
    }
}
