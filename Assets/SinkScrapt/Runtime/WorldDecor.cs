using System.Collections.Generic;
using UnityEngine;

namespace SinkScrapt
{
    public enum DecalKind { Blood, Handprints, Drag, Pool, Scratches, Rust, Damp }

    // Horror dressing for the wreck, all procedural and lit: wall and floor decals (old blood, handprints, drag marks, claw
    // gouges, rust runs, damp), messages daubed on the walls in Indonesian with a brush-stroke font, hanging cables and floor
    // debris. Purely visual (no colliders) and placed from its own seeded random stream, so it never shifts the gameplay
    // draws and every peer dresses the same blueprint the same way.
    public static partial class WorldFactory
    {
        static Mesh quad;
        static readonly Dictionary<DecalKind,Material> decalMaterials=new();
        static readonly Dictionary<string,Material> writing=new();
        // Things the crew before you wrote. The last one is scratched tally marks rather than paint.
        public static readonly string[] Scrawls={"JANGAN TURUN","DIA MENDENGAR","TOLONG","MATIKAN SENTER","KAMI TERJEBAK","JANGAN BERHENTI","LARI","DIA DI BAWAH","IIII IIII III"};

        static Mesh Quad()
        {
            if(quad!=null)return quad;
            quad=new Mesh{name="Decal quad"};
            quad.SetVertices(new List<Vector3>{new(-.5f,-.5f,0),new(.5f,-.5f,0),new(.5f,.5f,0),new(-.5f,.5f,0)});
            quad.SetUVs(0,new List<Vector2>{new(0,0),new(1,0),new(1,1),new(0,1)});
            quad.SetNormals(new List<Vector3>{Vector3.back,Vector3.back,Vector3.back,Vector3.back});
            quad.SetTriangles(new[]{0,2,1,0,3,2},0);quad.RecalculateBounds();return quad;
        }
        static Material DecalMaterial(Texture2D texture,string name)
        {
            var source=Resources.Load<Material>("SinkScrapt/Decal");
            var mat=source!=null?new Material(source):new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.name=name;mat.SetTexture("_BaseMap",texture);mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_BlendModePreserveSpecular",0);
            return mat;
        }
        // A decal quad facing `normal` (it is seen from that side), centred on `pos` a few millimetres off the surface.
        static GameObject Decal(Transform parent,Material material,Vector3 pos,Vector3 normal,Vector2 size,float roll,Vector3? up=null)
        {
            var go=new GameObject("Decal "+material.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.transform.localPosition=pos+normal*.012f;
            go.transform.localRotation=Quaternion.LookRotation(-normal,up??(Mathf.Abs(normal.y)>.9f?Vector3.forward:Vector3.up))*Quaternion.Euler(0,0,roll);
            go.transform.localScale=new Vector3(size.x,size.y,1);
            go.GetComponent<MeshFilter>().sharedMesh=Quad();var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
        public static Material DecalOf(DecalKind kind)
        {
            if(decalMaterials.TryGetValue(kind,out var m)&&m!=null)return m;
            return decalMaterials[kind]=DecalMaterial(DecalArt.Make(kind),kind.ToString());
        }
        public static Material Writing(string text)
        {
            if(writing.TryGetValue(text,out var m)&&m!=null)return m;
            return writing[text]=DecalMaterial(DecalArt.Scrawl(text,text.Trim('I',' ').Length==0),"Writing "+text);
        }

        // ---- Interior ------------------------------------------------------------------------------------------------------
        static readonly string[] DebrisModels={"Props/prop_debris_chair","Props/prop_debris_bucket","Props/prop_debris_boots","Props/prop_debris_papers","Props/prop_debris_planks","Props/prop_debris_lifejacket"};
        static void Dress(WorldZone zone,Transform structure,Transform fixtures,InteriorLayout layout)
        {
            var random=new System.Random(layout.seed*7919+1301);
            float R()=>(float)random.NextDouble();
            int written=0;var scrawlOrder=new List<int>();for(int i=0;i<Scrawls.Length;i++)scrawlOrder.Insert(random.Next(scrawlOrder.Count+1),i);
            // The cruise ship dresses fewer of its many cells (ShipProfile.decorDensity) so the decal count stays affordable.
            float density=layout.Profile.decorDensity;
            for(int f=0;f<ShipGrid.Decks;f++)for(int z=0;z<ShipGrid.Rows;z++)for(int x=0;x<ShipGrid.Columns;x++) {
                var kind=layout.Kind(f,x,z,out int id);if(kind!=CellKind.Corridor&&kind!=CellKind.Room)continue;
                if(density<1&&R()>density)continue;
                var c=ShipGrid.Center(f,x,z);bool room=kind==CellKind.Room;
                for(int dir=0;dir<4;dir++) {
                    if(layout.Open(f,x,z,dir)||layout.Balcony(f,x,z,dir))continue;
                    var n=Dir3(dir);var along=new Vector3(-n.z,0,n.x);var face=c+n*(ShipGrid.Cell*.5f-.1f);var inward=-n;
                    float roll=R(),slide=(R()-.5f)*2.4f;
                    // Daubed messages on long closed corridor walls, a handful per wreck.
                    if(!room&&written<5&&roll<.07f) {
                        var text=Scrawls[scrawlOrder[written%scrawlOrder.Count]];written++;
                        // About 4 mm per texel (letters near 0.25 m tall), shrunk to fit inside one 4 m wall.
                        var mat=Writing(text);var tex=(Texture2D)mat.GetTexture("_BaseMap");float k=Mathf.Min(.0042f,3.3f/tex.width),w=tex.width*k,h=tex.height*k;
                        Decal(fixtures,mat,face+along*Mathf.Clamp(slide,-(3.4f-w)*.5f,(3.4f-w)*.5f)+Vector3.up*(1.45f+R()*.3f),inward,new Vector2(w,h),(R()-.5f)*6);
                        continue;
                    }
                    if(roll<.2f) Decal(fixtures,DecalOf(DecalKind.Rust),face+along*slide+Vector3.up*(1.9f+R()*.4f),inward,new Vector2(.9f+R()*.8f,1.3f+R()*.6f),0);
                    else if(roll<.3f) Decal(fixtures,DecalOf(DecalKind.Damp),face+along*slide+Vector3.up*(.5f+R()*1.6f),inward,Vector2.one*(.8f+R()*.9f),R()*360);
                    else if(roll<.37f) Decal(fixtures,DecalOf(DecalKind.Blood),face+along*slide+Vector3.up*(.9f+R()*.8f),inward,new Vector2(.8f+R()*.6f,.9f+R()*.5f),(R()-.5f)*30);
                    else if(roll<.42f) Decal(fixtures,DecalOf(DecalKind.Handprints),face+along*slide+Vector3.up*(1.0f+R()*.6f),inward,new Vector2(.7f,.7f),(R()-.5f)*40);
                    else if(roll<.48f) Decal(fixtures,DecalOf(DecalKind.Scratches),face+along*slide+Vector3.up*(1.1f+R()*.8f),inward,new Vector2(.55f+R()*.3f,.8f+R()*.4f),(R()-.5f)*50);
                }
                // Floor: old pools and drag marks; debris in room corners (clear of the 1.4 m band the monsters walk).
                float floorRoll=R();var floor=c+Vector3.up*.002f;
                if(floorRoll<.08f) Decal(fixtures,DecalOf(DecalKind.Pool),floor+new Vector3((R()-.5f)*1.6f,0,(R()-.5f)*1.6f),Vector3.up,Vector2.one*(.7f+R()*.7f),R()*360);
                else if(floorRoll<.14f) {
                    bool alongX=R()<.5f;Decal(fixtures,DecalOf(DecalKind.Drag),floor,Vector3.up,new Vector2(.55f,2.6f+R()*1.2f),(R()-.5f)*14,alongX?Vector3.right:Vector3.forward);
                }
                if(room&&R()<.45f) {
                    var corner=c+new Vector3((R()<.5f?-1:1)*(1.15f+R()*.5f),0,(R()<.5f?-1:1)*(1.15f+R()*.5f));
                    Model(DebrisModels[random.Next(DebrisModels.Length)],structure,corner,Quaternion.Euler(0,R()*360,0));
                } else if(!room&&R()<.1f) {
                    // A corridor gets the odd small thing against a wall.
                    int side=random.Next(4);var n=Dir3(side);if(!layout.Open(f,x,z,side))Model(DebrisModels[1+random.Next(3)],structure,c+n*1.45f+new Vector3(n.z,0,n.x)*(R()-.5f)*2,Quaternion.Euler(0,R()*360,0));
                }
                // Cables torn loose from the trays, hanging in loops into the corridor (above head height).
                if(!room&&R()<.12f)HangingCables(structure,c,random);
            }
            // Claw gouges on some closed doors, on the corridor side.
            for(int i=0;i<zone.doors.Count;i++) {
                if(R()>.22f||zone.doors[i]==null)continue;var door=zone.doors[i];var leaf=door.hinges[0].GetChild(0);
                foreach(float face in new[]{-1f,1f})if(R()<.6f)Decal(leaf,DecalOf(DecalKind.Scratches),new Vector3(InteriorDoor.LeafWidth*(.35f+R()*.3f),1.0f+R()*.6f,face*.036f),new Vector3(0,0,face),new Vector2(.45f,.75f),(R()-.5f)*40);
            }
        }
        // Two or three cables sagging from the ceiling between points 1-1.6 m apart, lowest about 2.1 m up.
        static void HangingCables(Transform p,Vector3 c,System.Random random)
        {
            var colours=new[]{new Color(.06f,.06f,.06f),new Color(.32f,.08f,.06f),new Color(.45f,.38f,.1f)};
            int count=2+random.Next(2);float yaw=(float)random.NextDouble()*Mathf.PI;
            for(int k=0;k<count;k++) {
                var dir=new Vector3(Mathf.Cos(yaw+k*.3f),0,Mathf.Sin(yaw+k*.3f));float span=1+(float)random.NextDouble()*.6f,sag=.35f+(float)random.NextDouble()*.3f;
                var a=c+Vector3.up*2.7f-dir*span*.5f+new Vector3((float)random.NextDouble()-.5f,0,(float)random.NextDouble()-.5f)*.6f;var b=a+dir*span;
                Vector3 Point(float t)=>Vector3.Lerp(a,b,t)+Vector3.down*sag*4*t*(1-t);
                const int segments=7;
                for(int s=0;s<segments;s++) {
                    var p0=Point((float)s/segments);var p1=Point((float)(s+1)/segments);
                    var seg=Box("Hanging cable",p,(p0+p1)*.5f,new Vector3(.025f,.025f,(p1-p0).magnitude+.01f),colours[(k+random.Next(2))%colours.Length],false);
                    seg.transform.localRotation=Quaternion.LookRotation(p1-p0);
                }
            }
        }

        // ---- Exterior: the first thing the crew sees -------------------------------------------------------------------------
        static void DressWreckDeck(Transform ship)
        {
            Decal(ship,Writing("JANGAN MASUK"),new Vector3(3.6f,11.35f,-29.5f),Vector3.forward,new Vector2(2.6f,.42f),-3);
            Decal(ship,Writing("TOLONG"),new Vector3(-4.6f,11.05f,-29.5f),Vector3.forward,new Vector2(2.1f,.6f),4);
            Decal(ship,DecalOf(DecalKind.Drag),new Vector3(.4f,10.04f,-27.4f),Vector3.up,new Vector2(.6f,3.6f),8,Vector3.forward);
            Decal(ship,DecalOf(DecalKind.Handprints),new Vector3(-1.2f,11.0f,-29.5f),Vector3.forward,new Vector2(.6f,.6f),-10);
        }
    }

    // Procedural decal textures (straight alpha). Seeded, so every peer draws identical art.
    public static class DecalArt
    {
        static readonly Color Blood=new(.2f,.015f,.012f),BloodDry=new(.12f,.025f,.018f),Rust=new(.4f,.17f,.06f),Damp=new(.03f,.04f,.035f),Metal=new(.58f,.58f,.56f);
        static float Hash(int x,int y,int seed) {unchecked {uint h=(uint)(x*374761393+y*668265263+seed*1442695041);h=(h^(h>>13))*1274126177u;h^=h>>16;return (h&0xFFFFFF)/(float)0x1000000;}}
        static float Noise(float x,float y,int seed)
        {
            int x0=Mathf.FloorToInt(x),y0=Mathf.FloorToInt(y);float tx=x-x0,ty=y-y0;tx=tx*tx*(3-2*tx);ty=ty*ty*(3-2*ty);
            return Mathf.Lerp(Mathf.Lerp(Hash(x0,y0,seed),Hash(x0+1,y0,seed),tx),Mathf.Lerp(Hash(x0,y0+1,seed),Hash(x0+1,y0+1,seed),tx),ty);
        }
        static float Fbm(float x,float y,int seed) {float s=0,a=.5f,n=0;for(int o=0;o<4;o++){s+=Noise(x,y,seed+o*17)*a;n+=a;x*=2;y*=2;a*=.5f;}return s/n;}
        static Texture2D New(int w,int h,string name) => new(w,h,TextureFormat.RGBA32,true){name=name,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.DontSave};
        static void Stamp(Color[] px,int w,int h,float cx,float cy,float r,Color c,float a)
        {
            int x0=Mathf.Max(0,(int)(cx-r-1)),x1=Mathf.Min(w-1,(int)(cx+r+1)),y0=Mathf.Max(0,(int)(cy-r-1)),y1=Mathf.Min(h-1,(int)(cy+r+1));
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++) {
                float d=Mathf.Sqrt((x-cx)*(x-cx)+(y-cy)*(y-cy));float k=Mathf.Clamp01(r-d+.5f)*a;if(k<=0)continue;
                var o=px[y*w+x];float na=k+o.a*(1-k);if(na<=0)continue;
                px[y*w+x]=new Color((c.r*k+o.r*o.a*(1-k))/na,(c.g*k+o.g*o.a*(1-k))/na,(c.b*k+o.b*o.a*(1-k))/na,na);
            }
        }
        static void Line(Color[] px,int w,int h,Vector2 a,Vector2 b,float r0,float r1,Color c,float alpha)
        {
            float len=(b-a).magnitude;int steps=Mathf.Max(1,Mathf.CeilToInt(len));
            for(int i=0;i<=steps;i++) {float t=(float)i/steps;var p=Vector2.Lerp(a,b,t);Stamp(px,w,h,p.x,p.y,Mathf.Lerp(r0,r1,t),c,alpha);}
        }
        static void Drip(Color[] px,int w,int h,float x,float y,float length,float r,Color c,System.Random rnd)
        {
            float wob=0;for(float d=0;d<length;d+=1) {wob+=((float)rnd.NextDouble()-.5f)*.25f;Stamp(px,w,h,x+wob,y-d,r*Mathf.Lerp(1,.55f,d/length),c,.95f);}
            Stamp(px,w,h,x+wob,y-length,r*.9f,c,1);
        }
        static Texture2D Done(Texture2D tex,Color[] px) {tex.SetPixels(px);tex.Apply(true,true);return tex;}

        public static Texture2D Make(DecalKind kind)
        {
            var rnd=new System.Random(911+(int)kind*131);
            switch(kind) {
                case DecalKind.Blood: {
                    // A smear wiped sideways across the wall with runs dripping down from it.
                    const int W=192,H=192;var px=new Color[W*H];var tex=New(W,H,"Blood");
                    for(int s=0;s<5;s++) {
                        float y=110+(float)rnd.NextDouble()*40,x0=30+(float)rnd.NextDouble()*20,x1=150+(float)rnd.NextDouble()*30;
                        for(float x=x0;x<x1;x+=1.5f) {float t=(x-x0)/(x1-x0);Stamp(px,W,H,x,y+Mathf.Sin(x*.05f+s)*6,Mathf.Lerp(15,4,t)*(.7f+.6f*Noise(x*.1f,s,3)),Color.Lerp(Blood,BloodDry,t),Mathf.Lerp(1f,.5f,t));}
                    }
                    for(int d=0;d<9;d++)Drip(px,W,H,40+(float)rnd.NextDouble()*110,108+(float)rnd.NextDouble()*20,30+(float)rnd.NextDouble()*80,3.2f,Blood,rnd);
                    return Done(tex,px);
                }
                case DecalKind.Handprints: {
                    const int W=160,H=160;var px=new Color[W*H];var tex=New(W,H,"Handprints");
                    for(int k=0;k<3;k++) {
                        float cx=40+k*38+(float)rnd.NextDouble()*10,cy=60+(float)rnd.NextDouble()*40,ang=((float)rnd.NextDouble()-.5f)*.6f,alpha=k==2?.45f:.9f;
                        Vector2 Rot(float x,float y)=>new Vector2(cx+x*Mathf.Cos(ang)-y*Mathf.Sin(ang),cy+x*Mathf.Sin(ang)+y*Mathf.Cos(ang));
                        for(float yy=-12;yy<=6;yy+=1.5f)for(float xx=-10;xx<=10;xx+=1.5f)if(xx*xx/100+yy*yy/150<1){var p=Rot(xx,yy);Stamp(px,W,H,p.x,p.y,2.2f,Blood,alpha);}
                        float[] fx={-7.5f,-2.5f,2.5f,7f};float[] fl={16,20,19,14};
                        for(int f=0;f<4;f++)Line(px,W,H,Rot(fx[f],6),Rot(fx[f]*1.15f,6+fl[f]),3.2f,2.6f,Blood,alpha);
                        Line(px,W,H,Rot(10,-4),Rot(19,8),3.2f,2.6f,Blood,alpha);
                        // The hand slid down: a faint streak below it.
                        Line(px,W,H,Rot(0,-12),Rot(1,-40),8,3,BloodDry,alpha*.4f);
                    }
                    return Done(tex,px);
                }
                case DecalKind.Drag: {
                    // Something heavy pulled along the floor: a long uneven band, broken and fading, with a few hand smears.
                    const int W=64,H=256;var px=new Color[W*H];var tex=New(W,H,"Drag");
                    for(int y=4;y<H-4;y++) {
                        float t=(float)y/H,width=10+8*Noise(y*.05f,1,7),cx=W*.5f+(Noise(y*.02f,2,9)-.5f)*14;
                        float a=Mathf.Lerp(.9f,.2f,t)*(.55f+.45f*Noise(y*.15f,3,11));
                        for(int k=0;k<3;k++)Stamp(px,W,H,cx+(k-1)*width*.45f,y,width*.35f,Color.Lerp(Blood,BloodDry,t),a*.6f);
                    }
                    return Done(tex,px);
                }
                case DecalKind.Pool: {
                    const int W=128,H=128;var px=new Color[W*H];var tex=New(W,H,"Pool");
                    for(int y=0;y<H;y++)for(int x=0;x<W;x++) {
                        float u=(x-64)/64f,v=(y-64)/64f,d=Mathf.Sqrt(u*u+v*v)+(Fbm(x*.06f,y*.06f,21)-.5f)*.6f;
                        float a=Mathf.Clamp01((.62f-d)*8);if(a<=0)continue;
                        px[y*W+x]=new Color(Mathf.Lerp(BloodDry.r,Blood.r,Mathf.Clamp01(.6f-d)*2),Blood.g,Blood.b,a*.92f);
                    }
                    for(int s=0;s<14;s++){float a=(float)rnd.NextDouble()*Mathf.PI*2,r=46+(float)rnd.NextDouble()*16;Stamp(px,W,H,64+Mathf.Cos(a)*r,64+Mathf.Sin(a)*r,1.5f+(float)rnd.NextDouble()*2.5f,Blood,.9f);}
                    return Done(tex,px);
                }
                case DecalKind.Scratches: {
                    // Four parallel gouges torn through the paint to bare metal, dark at the edges.
                    const int W=128,H=192;var px=new Color[W*H];var tex=New(W,H,"Scratches");
                    for(int k=0;k<4;k++) {
                        var a=new Vector2(30+k*20+(float)rnd.NextDouble()*6,170-k*6);var b=new Vector2(14+k*22+(float)rnd.NextDouble()*6,20+k*4);
                        Line(px,W,H,a,b,3.6f,1.2f,new Color(.08f,.06f,.05f),.85f);Line(px,W,H,a,b,1.8f,.6f,Metal,.95f);
                    }
                    return Done(tex,px);
                }
                case DecalKind.Rust: {
                    // Rust bleeding down from a seam: streaks that start strong at the top and thin out.
                    const int W=128,H=192;var px=new Color[W*H];var tex=New(W,H,"Rust");
                    for(int s=0;s<16;s++) {
                        float x=8+(float)rnd.NextDouble()*112,len=40+(float)rnd.NextDouble()*140,r=1.5f+(float)rnd.NextDouble()*4;
                        for(float d=0;d<len;d+=1)Stamp(px,W,H,x+(Noise(d*.05f,s,5)-.5f)*6,H-6-d,r*Mathf.Lerp(1,.4f,d/len),Rust,Mathf.Lerp(.75f,.05f,d/len));
                    }
                    return Done(tex,px);
                }
                default: {
                    const int W=128,H=128;var px=new Color[W*H];var tex=New(W,H,"Damp");
                    for(int y=0;y<H;y++)for(int x=0;x<W;x++) {
                        float u=(x-64)/64f,v=(y-64)/64f,d=Mathf.Sqrt(u*u+v*v)+(Fbm(x*.04f,y*.04f,31)-.5f)*.7f;
                        float a=Mathf.Clamp01((.75f-d)*2.5f)*.75f;if(a>0)px[y*W+x]=new Color(Damp.r,Damp.g,Damp.b,a);
                    }
                    return Done(tex,px);
                }
            }
        }

        // ---- Brush-stroke writing ------------------------------------------------------------------------------------------
        // Capital letters as polylines on a 4 x 6 grid (x right, y up); enough for the messages in WorldFactory.Scrawls.
        static readonly Dictionary<char,float[][]> Strokes=new() {
            {'A',new[]{new float[]{0,0,2,6,4,0},new float[]{1,3,3,3}}},
            {'B',new[]{new float[]{0,0,0,6,3,6,4,5,3,3.2f,0,3.2f},new float[]{3,3.2f,4,1.6f,3,0,0,0}}},
            {'C',new[]{new float[]{4,5,3,6,1,6,0,5,0,1,1,0,3,0,4,1}}},
            {'D',new[]{new float[]{0,0,0,6,2.5f,6,4,4.5f,4,1.5f,2.5f,0,0,0}}},
            {'E',new[]{new float[]{4,6,0,6,0,0,4,0},new float[]{0,3,3,3}}},
            {'F',new[]{new float[]{4,6,0,6,0,0},new float[]{0,3,3,3}}},
            {'G',new[]{new float[]{4,5,3,6,1,6,0,5,0,1,1,0,3,0,4,1,4,3,2,3}}},
            {'H',new[]{new float[]{0,0,0,6},new float[]{4,0,4,6},new float[]{0,3,4,3}}},
            {'I',new[]{new float[]{2,0,2,6}}},
            {'J',new[]{new float[]{4,6,4,1,3,0,1,0,0,1}}},
            {'K',new[]{new float[]{0,0,0,6},new float[]{4,6,0,2.5f},new float[]{1.4f,3.5f,4,0}}},
            {'L',new[]{new float[]{0,6,0,0,4,0}}},
            {'M',new[]{new float[]{0,0,0,6,2,3,4,6,4,0}}},
            {'N',new[]{new float[]{0,0,0,6,4,0,4,6}}},
            {'O',new[]{new float[]{1,0,0,1,0,5,1,6,3,6,4,5,4,1,3,0,1,0}}},
            {'P',new[]{new float[]{0,0,0,6,3,6,4,5,4,4,3,3,0,3}}},
            {'R',new[]{new float[]{0,0,0,6,3,6,4,5,4,4,3,3,0,3},new float[]{2,3,4,0}}},
            {'S',new[]{new float[]{4,5,3,6,1,6,0,5,0,4,1,3,3,3,4,2,4,1,3,0,1,0,0,1}}},
            {'T',new[]{new float[]{0,6,4,6},new float[]{2,6,2,0}}},
            {'U',new[]{new float[]{0,6,0,1,1,0,3,0,4,1,4,6}}},
            {'V',new[]{new float[]{0,6,2,0,4,6}}},
            {'W',new[]{new float[]{0,6,1,0,2,3,3,0,4,6}}},
            {'Y',new[]{new float[]{0,6,2,3,4,6},new float[]{2,3,2,0}}},
            {'!',new[]{new float[]{2,6,2,1.8f},new float[]{2,.4f,2,.2f}}},
        };
        const float Unit=9;
        // Paint (or, for tally marks, scratch) `text` into a texture: wobbling letters, an uneven brush and runs of paint.
        public static Texture2D Scrawl(string text,bool scratched)
        {
            var rnd=new System.Random(text.GetHashCode()&0x7fffffff);
            int w=Mathf.NextPowerOfTwo(Mathf.CeilToInt((text.Length*6+4)*Unit)),h=Mathf.CeilToInt(16*Unit);
            w=Mathf.Max(w,64);var px=new Color[w*h];var tex=New(w,h,"Scrawl");
            float scale=(w-4*Unit)/(text.Length*6*Unit);scale=Mathf.Min(scale,1.25f);
            float x=2*Unit,baseY=7*Unit;var ink=scratched?Metal:Blood;
            foreach(char ch in text) {
                float tilt=((float)rnd.NextDouble()-.5f)*.25f,lift=((float)rnd.NextDouble()-.5f)*Unit*.8f,size=Unit*scale*(.9f+(float)rnd.NextDouble()*.25f);
                if(Strokes.TryGetValue(ch,out var lines))foreach(var line in lines) {
                    for(int i=0;i+3<line.Length;i+=2) {
                        Vector2 P(float gx,float gy)=>new Vector2(x+(gx+gy*tilt)*size,baseY+lift+gy*size);
                        var a=P(line[i],line[i+1]);var b=P(line[i+2],line[i+3]);
                        // A fat, uneven brush (or a thin scratch with dark edges for the tally marks).
                        float r=scratched?1.6f:4.6f+(float)rnd.NextDouble()*1.8f;
                        if(scratched)Line(px,w,h,a,b,r+1.4f,r+1f,new Color(.08f,.06f,.05f),.85f);
                        Line(px,w,h,a,b,r,r*.75f,ink,1f);
                        // Paint runs from the low ends of strokes.
                        if(!scratched&&rnd.NextDouble()<.45f){var low=a.y<b.y?a:b;Drip(px,w,h,low.x,low.y,14+(float)rnd.NextDouble()*Unit*6,2.4f,BloodDry,rnd);}
                    }
                }
                x+=6*Unit*scale;
            }
            return Done(tex,px);
        }
    }
}
