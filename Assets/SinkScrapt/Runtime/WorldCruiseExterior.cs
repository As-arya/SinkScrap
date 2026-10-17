using UnityEngine;

namespace SinkScrapt
{
    // The abandoned cruise ship's exterior (Exterior scene): a 36 x 160 m white hull moored with its starboard side 6 m off the
    // small boat's port rail. Gameplay anchors match WorldZone (rope, CruiseDoors) and the interior elevations:
    //   - Main door A: a shell door in the starboard hull at deck-2 height (y 5) with a gangway platform where the boarding rope
    //     is made fast. It is 4-6 m above the sea at the start and goes under at about 7:30.
    //   - Emergency exits B and C: in the superstructure's starboard wall on the promenade deck (y 11), aft and forward. An
    //     open steel stair runs up the hull from the gangway platform to the promenade.
    //   - The promenade deck is level with the small-boat deck at 9:45 and the funnel top (y 26.5) is under by 12:00.
    public static partial class WorldFactory
    {
        const float CruiseX=-4,CruiseBeam=36,CruiseLength=160;
        static readonly Color CruiseWhite=new(1.22f,1.2f,1.14f),CruiseNavy=new(.1f,.14f,.24f),Teak=new(.6f,.5f,.4f),Funnel=new(.55f,.13f,.1f),LifeboatOrange=new(.88f,.42f,.12f);
        // x of the starboard hull face at height y (the hull flares out slightly toward the deck).
        static float HullSide(float y) => CruiseX+CruiseBeam*.5f*(.94f+.06f*Mathf.Clamp01((y+8)/19));

        public static void CruiseExterior(WorldZone zone)
        {
            var root=new GameObject("Abandoned cruise ship");root.transform.SetParent(zone.transform,false);zone.ship=root.transform;var p=root.transform;
            float half=CruiseLength*.5f;
            // Hull: white topsides, a navy stripe under the deck edge, red antifouling just above the sea at the start.
            HullMesh("Cruise hull",p,new Vector3(CruiseX,0,0),CruiseBeam,CruiseLength,-8,11,CruiseWhite,.8f,.94f);
            foreach(float side in new[]{1f,-1f}) {
                float x=side>0?HullSide(9.7f)+.04f:2*CruiseX-HullSide(9.7f)-.04f;
                Surfaces.Block("Hull stripe",p,new Vector3(x,9.7f,-1),new Vector3(.06f,.5f,150),Surface.Plate,CruiseNavy,false);
                // Two rows of portholes along each side, a few still glowing.
                for(float z=-70;z<=70;z+=4)foreach(float y in new[]{3.4f,6.6f}) {
                    if(side>0&&y>6&&Mathf.Abs(z)<3)continue;
                    float hx=side>0?HullSide(y)+.03f:2*CruiseX-HullSide(y)-.03f;
                    Shape("Porthole",PrimitiveType.Cylinder,p,new Vector3(hx,y,z),new Vector3(.55f,.03f,.55f),Hash01((int)z,(int)(y*10),side>0?1:2)<.06f?WindowLit:WindowDark,false,Hash01((int)z,(int)(y*10),side>0?1:2)<.06f).transform.localRotation=Quaternion.Euler(0,0,90);
                }
                Text("BINTANG SAMUDRA",p,new Vector3(side>0?HullSide(8.2f)+.06f:2*CruiseX-HullSide(8.2f)-.06f,8.2f,52),.24f,CruiseNavy,Quaternion.Euler(0,side>0?-90:90,0));
            }
            Text("BINTANG SAMUDRA",p,new Vector3(CruiseX,8.6f,-half-.1f),.2f,CruiseNavy,Quaternion.identity);
            Text("JAKARTA",p,new Vector3(CruiseX,7.2f,-half-.1f),.12f,CruiseNavy,Quaternion.identity);
            // Teak promenade deck over the hull top (y 11), narrowing into the bow.
            Surfaces.Block("Promenade deck",p,new Vector3(CruiseX,10.93f,-1),new Vector3(CruiseBeam,.2f,152),Surface.Planks,Teak);
            Surfaces.Block("Bow deck",p,new Vector3(CruiseX,10.93f,77),new Vector3(CruiseBeam*.8f,.2f,4),Surface.Planks,Teak);
            Surfaces.Block("Bow deck tip",p,new Vector3(CruiseX,10.93f,81),new Vector3(CruiseBeam*.42f,.2f,4),Surface.Planks,Teak);
            Surfaces.Block("Stern deck",p,new Vector3(CruiseX,10.93f,-78.5f),new Vector3(CruiseBeam*.74f,.2f,3),Surface.Planks,Teak);
            // Rails round the deck edge; the starboard one leaves a gap where the hull stair arrives (z -14.6..-12.4).
            float rx=HullSide(11)-.1f,px=2*CruiseX-rx;
            Railing(p,new Vector3(rx,11.55f,-45.3f),new Vector3(.15f,1,61.4f),OffWhite);
            Railing(p,new Vector3(rx,11.55f,30.8f),new Vector3(.15f,1,86.4f),OffWhite);
            Railing(p,new Vector3(px,11.55f,-1),new Vector3(.15f,1,152),OffWhite);
            Railing(p,new Vector3(CruiseX,11.55f,-80),new Vector3(CruiseBeam*.74f,1,.15f),OffWhite);
            foreach(float s in new[]{-1f,1f}) {var bow=new Vector3(CruiseX+s*CruiseBeam*.3f,11.55f,79);Railing(p,bow,new Vector3(.15f,1,8.5f),OffWhite);}
            Railing(p,new Vector3(CruiseX,11.55f,83),new Vector3(CruiseBeam*.42f,1,.15f),OffWhite);

            // Superstructure: three tiers of cabins with rows of windows, the bridge forward, the funnel aft.
            Tier(p,"Deck A",-19,10,11,14,-62,58,12.6f,2.5f);
            Tier(p,"Deck B",-18,9,14,17,-58,54,15.6f,2.5f);
            Tier(p,"Deck C",-15,6,17,20,-50,30,18.6f,3f);
            Surfaces.Block("Bridge",p,new Vector3(-4.5f,21.3f,23),new Vector3(23,2.6f,10),Surface.Bulkhead,CruiseWhite);
            Surfaces.Block("Bridge roof",p,new Vector3(-4.5f,22.7f,23),new Vector3(23.6f,.2f,10.6f),Surface.Plate,CruiseNavy,false);
            for(int k=-5;k<=5;k++)Window(p,new Vector3(-4.5f+k*2,21.5f,28.04f),new Vector3(1.7f,1f,.06f),false);
            foreach(float s in new[]{-1f,1f})Window(p,new Vector3(-4.5f+s*11.54f,21.5f,23),new Vector3(.06f,1f,7f),false);
            NavLight(p,new Vector3(7.3f,21.6f,26),new Color(.2f,1,.4f));NavLight(p,new Vector3(-16.3f,21.6f,26),new Color(1,.18f,.15f));
            Shape("Radar dome",PrimitiveType.Sphere,p,new Vector3(-4.5f,23.4f,21),new Vector3(1.6f,1.4f,1.6f),CruiseWhite,false);
            Surfaces.Block("Bridge mast",p,new Vector3(-4.5f,24.6f,25),new Vector3(.25f,3.8f,.25f),Surface.Plate,CruiseWhite,false);
            var mastBulb=Shape("Mast light",PrimitiveType.Sphere,p,new Vector3(-4.5f,26.6f,25),Vector3.one*.35f,new Color(1,.15f,.1f),false,true);
            Fx.Glow(mastBulb.transform,Vector3.zero,new Color(1,.15f,.1f),22,5).gameObject.AddComponent<LightBlink>().period=1.8f;
            Surfaces.Block("Funnel",p,new Vector3(-4.5f,23,-32),new Vector3(5.5f,6,8.5f),Surface.Plate,Funnel,false);
            Surfaces.Block("Funnel top",p,new Vector3(-4.5f,26.2f,-32),new Vector3(5.6f,.6f,8.6f),Surface.Plate,new Color(.08f,.08f,.08f),false);
            foreach(float s in new[]{-1f,1f})Shape("Funnel emblem",PrimitiveType.Cylinder,p,new Vector3(-4.5f+s*2.78f,23.4f,-32),new Vector3(2.2f,.02f,2.2f),Gold,false).transform.localRotation=Quaternion.Euler(0,0,90);
            // Pool and loungers on deck B's forward roof; deck chairs on deck C's aft roof.
            Surfaces.Block("Pool rim",p,new Vector3(-4.5f,17.08f,42),new Vector3(11.6f,.16f,12.6f),Surface.Tile,Color.white,false);
            Surfaces.Block("Pool water",p,new Vector3(-4.5f,17.12f,42),new Vector3(10.6f,.12f,11.6f),Surface.Plate,new Color(.06f,.16f,.13f),false);
            for(int k=0;k<5;k++)Surfaces.Block("Lounger",p,new Vector3(4.5f,17.25f,36+k*2.6f),new Vector3(.8f,.3f,2f),Surface.Planks,Teak,false,Quaternion.Euler(0,k==2?40:0,k==4?80:0));
            Railing(p,new Vector3(-4.5f,17.55f,54),new Vector3(27,1,.15f),OffWhite,false);
            for(int k=0;k<6;k++)Surfaces.Block("Deck chair",p,new Vector3(-12+k*2.2f,20.25f,-44),new Vector3(.7f,.3f,1.8f),Surface.Planks,Teak,false,Quaternion.Euler(0,k*13,0));
            // Lifeboats hang over the promenade on both sides; two starboard davits are empty, their falls trailing.
            for(int k=0;k<8;k++)foreach(float s in new[]{1f,-1f}) {
                float z=-49+k*13.5f;float x=s>0?11.6f:-20.6f;float wall=s>0?9:-18;
                foreach(float dz in new[]{-1.6f,1.6f}) {
                    Surfaces.Block("Davit",p,new Vector3((x+wall)*.5f,16.6f,z+dz),new Vector3(Mathf.Abs(x-wall)+.4f,.18f,.18f),Surface.Plate,DarkSteel,false);
                    if(s>0&&(k==2||k==5))Surfaces.Block("Davit fall",p,new Vector3(x,15.2f,z+dz),new Vector3(.04f,2.8f,.04f),Surface.Plate,new Color(.5f,.42f,.3f),false);
                }
                if(s>0&&(k==2||k==5))continue;
                var boat=Shape("Lifeboat",PrimitiveType.Capsule,p,new Vector3(x,15.3f,z),new Vector3(1.7f,2.4f,1.5f),LifeboatOrange,false);boat.transform.localRotation=Quaternion.Euler(90,0,0);
                Surfaces.Block("Lifeboat canopy",p,new Vector3(x,15.85f,z),new Vector3(1.5f,.5f,3.4f),Surface.Plate,new Color(.92f,.9f,.86f),false);
            }

            // Main door A: the shell door in the starboard hull, with the gangway platform outside it.
            float door=HullSide(6.25f);
            var opening=Box("Interior access",p,new Vector3(door+.06f,6.25f,0),new Vector3(.12f,2.5f,2.2f),new Color(.03f,.04f,.04f));
            var portal=opening.AddComponent<WorldInteractable>();portal.action=ActionKind.Portal;portal.argument=0;portal.title="Masuk pintu utama A (lobi)";
            Box("Shell door frame",p,new Vector3(door+.1f,7.58f,0),new Vector3(.16f,.16f,2.5f),DarkSteel,false);
            foreach(float s in new[]{-1f,1f})Box("Shell door jamb",p,new Vector3(door+.1f,6.25f,s*1.18f),new Vector3(.16f,2.66f,.16f),DarkSteel,false);
            Surfaces.Block("Shell door leaf",p,new Vector3(door+.75f,6.25f,1.95f),new Vector3(1.3f,2.45f,.1f),Surface.Plate,CruiseWhite,false);
            Text("PINTU UTAMA  •  LOBI",p,new Vector3(door+.08f,8.25f,0),.07f,CruiseNavy,Quaternion.Euler(0,-90,0));
            Fx.Glow(p,new Vector3(door+.7f,7.9f,0),new Color(1,.78f,.5f),9,1.5f).gameObject.AddComponent<LightFlicker>().depth=.25f;
            Surfaces.Block("Gangway platform",p,new Vector3(15.1f,4.9f,0),new Vector3(2.6f,.2f,5),Surface.Grating,Color.white);
            foreach(float z in new[]{-2f,2f})Surfaces.Block("Platform strut",p,new Vector3(14.7f,4,z),new Vector3(.15f,1.9f,.15f),Surface.Plate,DarkSteel,false,Quaternion.Euler(0,0,35));
            Railing(p,new Vector3(16.35f,5.5f,0),new Vector3(.15f,1,5),OffWhite);
            Railing(p,new Vector3(15.1f,5.5f,2.5f),new Vector3(2.6f,1,.15f),OffWhite);
            Railing(p,new Vector3(16f,5.5f,-2.5f),new Vector3(.8f,1,.15f),OffWhite);
            var ropeHook=Box("Rope anchor",p,new Vector3(16.05f,5.5f,.9f),new Vector3(.3f,1,.3f),Cream);
            var climb=ropeHook.AddComponent<WorldInteractable>();climb.action=ActionKind.Climb;climb.argument=0;climb.title="Turun ke kapal kecil";
            Decal(p,Writing("JANGAN MASUK"),new Vector3(door+.02f,7.1f,-2.1f),Vector3.right,new Vector2(2.2f,.38f),-4);
            Decal(p,DecalOf(DecalKind.Handprints),new Vector3(door+.02f,5.9f,1.6f),Vector3.right,new Vector2(.6f,.6f),12);
            // The hull stair: an open steel flight from the platform (y 5, z -2.5) up to a landing on the promenade (y 11, z -13.5).
            HullStair(p);
            // Emergency exits B (aft) and C (forward) in deck A's starboard wall.
            foreach(var (index,z) in new[]{(1,-30f),(2,30f)}) {
                var exit=Box("Interior access",p,new Vector3(10.08f,12.2f,z),new Vector3(.12f,2.4f,1.6f),new Color(.2f,.42f,.3f));
                var exitPortal=exit.AddComponent<WorldInteractable>();exitPortal.action=ActionKind.Portal;exitPortal.argument=index;exitPortal.title="Masuk keluar darurat "+InteriorLayout.EntryNames[index];
                Box("Push bar",p,new Vector3(10.17f,12.05f,z),new Vector3(.06f,.08f,1.2f),new Color(.75f,.75f,.72f),false);
                Shape("Exit sign",PrimitiveType.Cube,p,new Vector3(10.1f,13.65f,z),new Vector3(.06f,.32f,1.3f),new Color(.15f,.9f,.35f),false,true);
                Text("KELUAR DARURAT "+InteriorLayout.EntryNames[index],p,new Vector3(10.16f,13.65f,z),.035f,new Color(.02f,.12f,.04f),Quaternion.Euler(0,-90,0));
                Fx.Glow(p,new Vector3(10.7f,13.4f,z),new Color(.2f,1,.4f),7,1.2f);
            }
            Decal(p,Writing("TOLONG"),new Vector3(10.03f,12.4f,-26),Vector3.right,new Vector2(2f,.6f),3);
            Decal(p,DecalOf(DecalKind.Handprints),new Vector3(10.03f,11.9f,27.6f),Vector3.right,new Vector2(.7f,.7f),-8);
            Decal(p,DecalOf(DecalKind.Drag),new Vector3(12,11.04f,24),Vector3.up,new Vector2(.6f,4f),6,Vector3.forward);
            // Promenade dressing: deck chairs against the superstructure (some knocked over) and life rings on the rail.
            for(float z=-56;z<=52;z+=9) {
                if(Mathf.Abs(Mathf.Abs(z)-30)<3)continue;
                bool toppled=Hash01((int)z,4,9)<.3f;
                Surfaces.Block("Deck chair",p,new Vector3(10.6f,11.3f,z),new Vector3(.7f,.3f,1.8f),Surface.Planks,Teak,true,toppled?Quaternion.Euler(0,30,75):Quaternion.identity);
            }
            for(float z=-66;z<=66;z+=12) {
                if(Mathf.Abs(z+13.5f)<3)continue;
                Shape("Life ring",PrimitiveType.Cylinder,p,new Vector3(rx-.1f,11.8f,z),new Vector3(.7f,.04f,.7f),LifeboatOrange,false).transform.localRotation=Quaternion.Euler(0,0,90);
            }
            // Stern: a flagpole with a tattered flag; bow: bollards and a forward mast.
            Surfaces.Block("Flagpole",p,new Vector3(CruiseX,13.5f,-78.6f),new Vector3(.12f,5,.12f),Surface.Plate,OffWhite,false);
            Box("Flag red",p,new Vector3(CruiseX,15.55f,-79.3f),new Vector3(.03f,.45f,1.3f),new Color(.7f,.08f,.06f),false);
            Box("Flag white",p,new Vector3(CruiseX,15.1f,-79.25f),new Vector3(.03f,.45f,1.2f),new Color(.85f,.84f,.8f),false);
            foreach(float s in new[]{-1f,1f}){Bollard(p,new Vector3(CruiseX+s*8,11,72));Bollard(p,new Vector3(CruiseX+s*9,11,-74));}
            Surfaces.Block("Bow mast",p,new Vector3(CruiseX,14,78),new Vector3(.2f,6,.2f),Surface.Plate,OffWhite,false);
            NavLight(p,new Vector3(CruiseX,17.1f,78),new Color(1,.95f,.85f),14,2);
            // A few deck lamps still burn along the promenade; most are dead.
            foreach(float z in new[]{-48f,-6f,18f,44f}) {
                var lamp=new GameObject("Deck lamp").transform;lamp.SetParent(p,false);lamp.localPosition=new Vector3(10.4f,13.6f,z);
                Shape("Deck lamp bulb",PrimitiveType.Sphere,lamp,Vector3.zero,Vector3.one*.25f,new Color(1,.85f,.62f),false,true);
                Fx.Glow(lamp,new Vector3(.3f,-.2f,0),new Color(1,.8f,.55f),14,2.6f).gameObject.AddComponent<LightFlicker>().depth=.2f;
            }
        }
        // One superstructure tier: a block from x0..x1, y0..y1, z0..z1 with windows on both long walls (fewer on the far side,
        // and none where the emergency exits are).
        static void Tier(Transform p,string name,float x0,float x1,float y0,float y1,float z0,float z1,float windowY,float step)
        {
            Surfaces.Block(name,p,new Vector3((x0+x1)*.5f,(y0+y1)*.5f,(z0+z1)*.5f),new Vector3(x1-x0,y1-y0,z1-z0),Surface.Bulkhead,CruiseWhite);
            Surfaces.Block(name+" roof",p,new Vector3((x0+x1)*.5f,y1+.05f,(z0+z1)*.5f),new Vector3(x1-x0+.3f,.1f,z1-z0+.3f),Surface.Plate,new Color(.55f,.56f,.55f),false);
            for(float z=z0+2;z<=z1-2;z+=step) {
                if(y0<12&&Mathf.Abs(Mathf.Abs(z)-30)<2.2f)continue;
                bool lit=Hash01((int)(z*3),(int)y0,7)<.05f;
                Window(p,new Vector3(x1+.03f,windowY,z),new Vector3(.06f,1.1f,step*.62f),lit);
                if(((int)((z-z0)/step))%2==0)Window(p,new Vector3(x0-.03f,windowY,z),new Vector3(.06f,1.1f,step*.62f),Hash01((int)(z*5),(int)y0,8)<.04f);
            }
            Window(p,new Vector3((x0+x1)*.5f,windowY,z1+.03f),new Vector3((x1-x0)*.7f,1.1f,.06f),false);
        }
        // Open steel stair up the starboard hull: a walkable ramp (31 degrees) under visual treads, rails both sides, and a
        // landing that joins the promenade through the gap in its rail.
        static void HullStair(Transform p)
        {
            const float x=14.9f,zBottom=-2.5f,zTop=-12.5f,yBottom=5,yTop=11;float run=zBottom-zTop,rise=yTop-yBottom;
            float length=Mathf.Sqrt(run*run+rise*rise),angle=Mathf.Atan2(rise,run)*Mathf.Rad2Deg;var tilt=Quaternion.Euler(angle,0,0);
            var mid=new Vector3(x,(yBottom+yTop)*.5f,(zBottom+zTop)*.5f);
            var ramp=new GameObject("Hull stair ramp");ramp.transform.SetParent(p,false);ramp.transform.localPosition=mid;ramp.transform.localRotation=tilt;ramp.AddComponent<BoxCollider>().size=new Vector3(1.4f,.2f,length);
            const int treads=20;
            for(int k=0;k<treads;k++) {float t=(k+.5f)/treads;Surfaces.Block("Stair tread",p,new Vector3(x,yBottom+rise*t+.06f,zBottom-run*t),new Vector3(1.4f,.06f,run/treads+.04f),Surface.Diamond,new Color(.7f,.7f,.68f),false);}
            foreach(float s in new[]{-1f,1f}) {
                Surfaces.Block("Stair stringer",p,mid+new Vector3(s*.72f,-.1f,0),new Vector3(.08f,.3f,length),Surface.Plate,DarkSteel,false,tilt);
                Surfaces.Block("Stair rail",p,mid+new Vector3(s*.72f,1f,0),new Vector3(.06f,.06f,length),Surface.Plate,OffWhite,false,tilt);
                var guard=new GameObject("Stair rail collider");guard.transform.SetParent(p,false);guard.transform.localPosition=mid+new Vector3(s*.75f,.7f,0);guard.transform.localRotation=tilt;guard.AddComponent<BoxCollider>().size=new Vector3(.08f,1.4f,length);
            }
            Surfaces.Block("Stair landing",p,new Vector3(x,10.9f,-13.5f),new Vector3(1.6f,.2f,2.2f),Surface.Grating,Color.white);
            Railing(p,new Vector3(x+.78f,11.55f,-13.5f),new Vector3(.15f,1,2.2f),OffWhite);
            Railing(p,new Vector3(x,11.55f,-14.6f),new Vector3(1.6f,1,.15f),OffWhite);
        }
    }
}
