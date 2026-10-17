using System.Collections.Generic;
using UnityEngine;

namespace SinkScrapt
{
    public enum Surface { Plate, RustPlate, Diamond, Grating, Bulkhead, DeckPaint, Wood, Corrugated, Rock, Concrete, Hazard, Hull, Sand, Tile, Planks, Carpet, Wallpaper, Marble }

    // Procedural PS1-style surface kit. Every texture is a small, tileable, point-filtered image synthesised from seeded noise,
    // so all peers render the same thing and there are no third-party image files. Boxes get meshes whose UVs are measured in
    // metres from the box's own corner, so a texture keeps its scale however the box is sized (painted bands start at the floor).
    public static class Surfaces
    {
        const int N=128;
        static readonly Dictionary<Surface,Texture2D> textures=new();
        static readonly Dictionary<(Surface,Color),Material> materials=new();
        static readonly Dictionary<Vector3Int,Mesh> meshes=new();

        // Metres covered by one repeat of each texture.
        public static float Tile(Surface s) => s switch {
            Surface.Diamond=>1.2f, Surface.Grating=>1f, Surface.Hazard=>1f, Surface.Tile=>1.2f, Surface.Bulkhead=>3f, Surface.Concrete=>3f,
            Surface.Rock=>5f, Surface.Sand=>5f, Surface.Hull=>4f, Surface.Planks=>2.4f, Surface.Carpet=>1.6f, Surface.Wallpaper=>3f, Surface.Marble=>2.4f, _=>2f };
        static float Smoothness(Surface s) => s switch {Surface.Tile=>.45f,Surface.Marble=>.55f,Surface.Carpet=>.02f,Surface.Wallpaper=>.1f,Surface.Diamond=>.4f,Surface.Plate=>.32f,Surface.Bulkhead=>.28f,Surface.Rock=>.08f,Surface.Sand=>.05f,Surface.Wood=>.12f,Surface.Planks=>.12f,_=>.2f};

        public static Material Get(Surface s,Color tint)
        {
            if(materials.TryGetValue((s,tint),out var cached)&&cached!=null)return cached;
            var source=Resources.Load<Material>("SinkScrapt/Lit");
            var mat=source!=null?new Material(source):new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.name=s+" "+ColorUtility.ToHtmlStringRGB(tint);
            mat.SetTexture("_BaseMap",Texture(s));mat.SetTextureScale("_BaseMap",Vector2.one/Tile(s));mat.SetColor("_BaseColor",tint);
            mat.SetFloat("_Smoothness",Smoothness(s));
            materials[(s,tint)]=mat;return mat;
        }
        public static Material Get(Surface s) => Get(s,Color.white);

        // A textured box. `size` is its real size (scale stays 1), so the collider and UVs match it exactly.
        public static GameObject Block(string name,Transform parent,Vector3 position,Vector3 size,Surface s,Color tint,bool collider=true,Quaternion? rotation=null)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.localPosition=position;
            if(rotation.HasValue)go.transform.localRotation=rotation.Value;
            go.GetComponent<MeshFilter>().sharedMesh=BoxMesh(size);go.GetComponent<MeshRenderer>().sharedMaterial=Get(s,tint);
            if(collider)go.AddComponent<BoxCollider>().size=size;
            return go;
        }
        public static GameObject Block(string name,Transform parent,Vector3 position,Vector3 size,Surface s,bool collider=true) => Block(name,parent,position,size,s,Color.white,collider);

        public static Mesh BoxMesh(Vector3 size)
        {
            var key=Vector3Int.RoundToInt(size*100);
            if(meshes.TryGetValue(key,out var cached)&&cached!=null)return cached;
            var h=size*.5f;var verts=new List<Vector3>();var uvs=new List<Vector2>();var normals=new List<Vector3>();var tris=new List<int>();
            int[] uAxis={2,0,0},vAxis={1,2,1};
            for(int a=0;a<3;a++)for(int sign=-1;sign<=1;sign+=2) {
                var n=Vector3.zero;n[a]=sign;int start=verts.Count;
                var corners=new Vector3[4];
                for(int k=0;k<4;k++) {
                    int i=k==1||k==2?1:0,j=k>=2?1:0;var p=Vector3.zero;
                    p[a]=sign*h[a];p[uAxis[a]]=i==1?h[uAxis[a]]:-h[uAxis[a]];p[vAxis[a]]=j==1?h[vAxis[a]]:-h[vAxis[a]];
                    corners[k]=p;verts.Add(p);normals.Add(n);uvs.Add(new Vector2(p[uAxis[a]]+h[uAxis[a]],p[vAxis[a]]+h[vAxis[a]]));
                }
                bool front=Vector3.Dot(Vector3.Cross(corners[1]-corners[0],corners[2]-corners[0]),n)>0;
                if(front)tris.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});else tris.AddRange(new[]{start,start+2,start+1,start,start+3,start+2});
            }
            var mesh=new Mesh{name="Box "+key};mesh.SetVertices(verts);mesh.SetNormals(normals);mesh.SetUVs(0,uvs);mesh.SetTriangles(tris,0);mesh.RecalculateBounds();
            meshes[key]=mesh;return mesh;
        }

        // ---- Texture synthesis ------------------------------------------------------------------------------------------------
        public static Texture2D Texture(Surface s)
        {
            if(textures.TryGetValue(s,out var cached)&&cached!=null)return cached;
            var pixels=new Color[N*N];
            for(int y=0;y<N;y++)for(int x=0;x<N;x++)pixels[y*N+x]=Pixel(s,x,y);
            var tex=new Texture2D(N,N,TextureFormat.RGBA32,true){name=s.ToString(),filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Repeat,anisoLevel=0,hideFlags=HideFlags.DontSave};
            tex.SetPixels(pixels);tex.Apply(true,true);textures[s]=tex;return tex;
        }
        static float Hash(int x,int y,int seed)
        {
            unchecked {uint h=(uint)(x*374761393+y*668265263+seed*1442695041);h=(h^(h>>13))*1274126177u;h^=h>>16;return (h&0xFFFFFF)/(float)0x1000000;}
        }
        // Tileable value noise: the lattice wraps every `period` cells across the texture.
        static float Noise(float x,float y,int period,int seed)
        {
            float fx=x/N*period,fy=y/N*period;int x0=Mathf.FloorToInt(fx),y0=Mathf.FloorToInt(fy);float tx=fx-x0,ty=fy-y0;
            tx=tx*tx*(3-2*tx);ty=ty*ty*(3-2*ty);
            int Wrap(int v)=>((v%period)+period)%period;
            float a=Hash(Wrap(x0),Wrap(y0),seed),b=Hash(Wrap(x0+1),Wrap(y0),seed),c=Hash(Wrap(x0),Wrap(y0+1),seed),d=Hash(Wrap(x0+1),Wrap(y0+1),seed);
            return Mathf.Lerp(Mathf.Lerp(a,b,tx),Mathf.Lerp(c,d,tx),ty);
        }
        static float Fbm(float x,float y,int period,int seed,int octaves=4)
        {
            float sum=0,amp=.5f,norm=0;for(int o=0;o<octaves;o++) {sum+=Noise(x,y,period<<o,seed+o*31)*amp;norm+=amp;amp*=.5f;}return sum/norm;
        }
        static readonly Color RustDark=new(.32f,.15f,.08f),RustLight=new(.62f,.32f,.14f);
        static Color Rust(int x,int y,int seed) {float n=Fbm(x,y,8,seed+5,3);return Color.Lerp(RustDark,RustLight,n);}
        // Rivet: a raised dot with a highlight on its upper-left.
        static float Rivet(int x,int y,int cx,int cy)
        {
            int dx=x-cx,dy=y-cy;if(dx*dx+dy*dy>3)return 0;return dx<=0&&dy>=0?1.35f:.7f;
        }
        static int Mod(int a,int m) => ((a%m)+m)%m;
        static Color Pixel(Surface s,int x,int y)
        {
            float grime=Fbm(x,y,4,11);float fine=Hash(x,y,3)*.08f-.04f;
            switch(s) {
                case Surface.Plate:
                case Surface.RustPlate: {
                    float v=.6f+fine+(grime-.5f)*.25f;
                    bool seam=Mod(x,64)==0||Mod(y,64)==0;bool lip=Mod(x,64)==1||Mod(y,64)==63;
                    if(seam)v*=.45f;else if(lip)v*=1.15f;
                    var c=new Color(v*.92f,v*.98f,v,1);
                    // Rows of rivets 4 px inside every seam.
                    float r=Rivet(x,y,x-Mod(x,64)+(Mod(x,64)<32?4:60),y-Mod(y,10)+5);
                    if(r==0)r=Rivet(x,y,x-Mod(x,10)+5,y-Mod(y,64)+(Mod(y,64)<32?4:60));
                    if(r>0)c*=r;
                    // Rust bleeding down from seams and rivet lines.
                    float streak=Mathf.Clamp01(1-Mod(y,64)/40f)*Noise(x*4,0,32,7);
                    float rust=s==Surface.RustPlate?Mathf.SmoothStep(.45f,.62f,Fbm(x,y,4,21))+streak*.5f:streak*.25f;
                    c=Color.Lerp(c,Rust(x,y,1),Mathf.Clamp01(rust));c.a=1;return c;
                }
                case Surface.Diamond: {
                    float v=.48f+fine+(grime-.5f)*.2f;int cx=Mod(x,16),cy=Mod(y,16);bool flip=((x/16)+(y/16))%2==0;
                    float u=(cx-7.5f)/7.5f,w=(cy-7.5f)/7.5f;float a=flip?u+w:u-w,b=flip?u-w:u+w;
                    if(a*a*6+b*b*.6f<.35f)v*=a>0?.75f:1.3f;
                    var c=new Color(v,v*1.01f,v*1.03f,1);return Color.Lerp(c,Rust(x,y,2),Mathf.SmoothStep(.6f,.75f,Fbm(x,y,4,9))*.6f);
                }
                case Surface.Grating: {
                    int cx=Mod(x,16),cy=Mod(y,16);bool bar=cx<4||cy<3;float v=bar?.5f+fine+(cx==0||cy==0?.12f:0):.06f+grime*.05f;
                    var c=new Color(v,v,v*1.02f,1);if(bar)c=Color.Lerp(c,Rust(x,y,4),Mathf.SmoothStep(.55f,.7f,Fbm(x,y,4,17))*.7f);return c;
                }
                case Surface.Bulkhead: {
                    // 3 m repeat: dark lower band to ~1.1 m with a stripe, light paint above, panel seams every 1.5 m.
                    float metres=y/(float)N*3f;bool lower=metres<1.1f;bool stripe=metres>=1.1f&&metres<1.2f;
                    float v=(lower?.32f:stripe?.18f:.78f)+fine*.8f;
                    v*=1-Mathf.Clamp01(1-metres/.6f)*.35f*(grime+.3f);
                    v*=.85f+grime*.3f;
                    if(Mod(x,64)==0)v*=.55f;else if(Mod(x,64)==1)v*=1.08f;
                    if(Mod(x,64)<3&&Mod(y,10)==5)v*=1.3f;
                    var c=new Color(v,v,v,1);
                    float chip=Mathf.SmoothStep(.62f,.7f,Fbm(x,y,8,23)+(lower?.06f:0)-metres*.02f);
                    float drip=Noise(x*4,y,8,41)>.78f&&!lower?.35f:0;
                    return Color.Lerp(c,Rust(x,y,3),Mathf.Clamp01(chip+drip*Mathf.Clamp01(metres-1.2f)));
                }
                case Surface.DeckPaint: {
                    float v=.55f+fine+(grime-.5f)*.35f;var c=new Color(v,v,v,1);
                    if(Mod(x,64)==0||Mod(y,64)==0)c*=.6f;
                    float worn=Mathf.SmoothStep(.55f,.72f,Fbm(x,y,4,51));return Color.Lerp(c,new Color(.3f,.3f,.3f)+Rust(x,y,5)*.4f,worn*.8f);
                }
                case Surface.Wood:
                case Surface.Planks: {
                    int plank=y/16;int shift=(int)(Hash(plank,0,61)*N);bool gap=Mod(y,16)==0||Mod(x+shift,s==Surface.Planks?128:64)==0;
                    float grain=Noise(x,y*12,8,plank+70)*.5f+Noise(x*2,y*16,8,plank+90)*.25f;
                    float v=.45f+Hash(plank,1,63)*.18f+grain*.25f;if(gap)v*=.35f;
                    var c=new Color(v*1.0f,v*.78f,v*.55f,1);c*=.8f+grime*.35f;c.a=1;return c;
                }
                case Surface.Corrugated: {
                    float ridge=Mathf.Sin(x/(float)N*Mathf.PI*2*16);float v=.62f+ridge*.16f+fine+(grime-.5f)*.25f;
                    var c=new Color(v,v,v,1);float edge=Mathf.Clamp01(1-Mathf.Min(y,N-1-y)/8f)*.45f;
                    float rust=Mathf.SmoothStep(.66f,.78f,Fbm(x,y,4,77))*.6f+edge*Noise(x*2,y,16,78);
                    return Color.Lerp(c,Rust(x,y,6)*1.3f,Mathf.Clamp01(rust));
                }
                case Surface.Rock: {
                    float n=Fbm(x,y,4,91,5);float crack=Mathf.Abs(Noise(x,y,8,93)-.5f)<.02f?.55f:1;
                    float v=(.22f+n*.3f)*crack;var c=new Color(v*.92f,v,v*.95f,1);
                    return Color.Lerp(c,new Color(.18f,.24f,.16f),Mathf.SmoothStep(.55f,.7f,Fbm(x,y,4,95))*.6f);
                }
                case Surface.Concrete: {
                    float v=.5f+(Fbm(x,y,8,101)-.5f)*.3f+fine;if(Mod(x,64)==0||Mod(y,64)==0)v*=.65f;
                    var c=new Color(v,v*.99f,v*.95f,1);return Color.Lerp(c,new Color(.2f,.19f,.16f),Mathf.SmoothStep(.6f,.78f,Fbm(x,y,4,103))*.6f);
                }
                case Surface.Hazard: {
                    bool yellow=Mod(x+y,64)<32;var c=yellow?new Color(.85f,.66f,.12f):new Color(.07f,.07f,.07f);c*=.85f+grime*.3f;
                    return Color.Lerp(c,Rust(x,y,7)*.8f,Mathf.SmoothStep(.6f,.75f,Fbm(x,y,4,111)));
                }
                case Surface.Hull: {
                    // 4 m repeat: 1 m plate courses with weld seams, rust streaks running down.
                    float v=.5f+fine+(grime-.5f)*.3f;
                    bool seam=Mod(y,32)==0||Mod(x+(y/32%2)*32,64)==0;if(seam)v*=.5f;
                    var c=new Color(v,v,v,1);
                    float streak=Noise(x*5,y,8,121);float rust=Mathf.SmoothStep(.74f,.88f,streak)*(.3f+.4f*Noise(x,y,8,123))+Mathf.SmoothStep(.68f,.8f,Fbm(x,y,4,125))*.4f;
                    return Color.Lerp(c,Rust(x,y,8),Mathf.Clamp01(rust)*.7f);
                }
                case Surface.Sand: {
                    float v=.62f+(Fbm(x,y,8,131)-.5f)*.25f+Hash(x,y,133)*.1f;return new Color(v,v*.9f,v*.72f,1);
                }
                case Surface.Tile: {
                    bool grout=Mod(x,32)<2||Mod(y,32)<2;bool dark=((x/32)+(y/32))%2==0;float v=grout?.3f:(dark?.55f:.82f)+fine;
                    var c=new Color(v,v*.98f,v*.93f,1);return Color.Lerp(c,new Color(.3f,.27f,.2f),Mathf.SmoothStep(.55f,.75f,Fbm(x,y,4,141))*.55f);
                }
                case Surface.Carpet: {
                    // Cruise-line carpet (tint it): a diamond lattice with a medallion in every other diamond, gold border
                    // lines, mildew blooms and threadbare, darkened patches.
                    int cx=Mod(x,32),cy=Mod(y,32);float u=Mathf.Abs(cx-15.5f)/16,w=Mathf.Abs(cy-15.5f)/16;float dia=u+w;
                    float v=.62f+Hash(x,y,151)*.12f;
                    if(Mathf.Abs(dia-.92f)<.06f)v=1.25f;else if(dia<.34f&&((x/32)+(y/32))%2==0)v=dia<.18f?1.15f:.45f;
                    var c=new Color(v,v*.88f,v*.8f,1);
                    float mould=Mathf.SmoothStep(.6f,.74f,Fbm(x,y,4,153));float worn=Mathf.SmoothStep(.55f,.72f,Fbm(x,y,8,155));
                    c=Color.Lerp(c,new Color(.42f,.4f,.33f),worn*.45f);return Color.Lerp(c,new Color(.12f,.16f,.1f),mould*.7f);
                }
                case Surface.Wallpaper: {
                    // 3 m repeat: dark wood wainscot to 0.95 m with a rail, then striped damask paper peeling and water-stained.
                    float metres=y/(float)N*3f;
                    if(metres<.95f) {
                        float grain=Noise(x,y*10,8,161)*.3f;float v=.38f+grain+fine;if(Mod(x,42)==0||metres>.88f)v*=.6f;
                        var wood=new Color(v*.95f,v*.66f,v*.42f,1);return Color.Lerp(wood,new Color(.1f,.08f,.06f),Mathf.SmoothStep(.6f,.75f,Fbm(x,y,4,163))*.6f);
                    }
                    int sx=Mod(x,16);float pattern=sx<2?.72f:(sx>=7&&sx<9)?.95f:.85f;
                    int my=Mod(y,24);float motif=Mathf.Abs(Mod(x,16)-8)+Mathf.Abs(my-12)<5&&Mod(x,32)<16==(Mod(y,48)<24)?.12f:0;
                    float vv=pattern+motif+fine;var paper=new Color(vv,vv*.95f,vv*.82f,1);
                    float stain=Mathf.Clamp01(Noise(x*3,y,8,165)-.55f)*2*Mathf.Clamp01((metres-1)/2);
                    float peel=Mathf.SmoothStep(.66f,.7f,Fbm(x,y,8,167));
                    paper=Color.Lerp(paper,new Color(.45f,.38f,.25f),stain*.8f);
                    return Color.Lerp(paper,new Color(.32f,.33f,.3f)*(.8f+grime*.3f),peel);
                }
                case Surface.Marble: {
                    // Big polished tiles in two tones with dark veins, dulled by grime toward the edges.
                    bool grout=Mod(x,64)<1||Mod(y,64)<1;bool dark=((x/64)+(y/64))%2==0;
                    float vein=Mathf.Abs(Noise(x+y*.5f,y,8,171)-.5f)<.035f?.55f:1;
                    float v=(dark?.42f:.86f)*vein+fine+(Fbm(x,y,8,173)-.5f)*.12f;if(grout)v*=.5f;
                    var c=dark?new Color(v*.7f,v*.82f,v*.78f,1):new Color(v,v*.97f,v*.92f,1);
                    return Color.Lerp(c,new Color(.25f,.22f,.17f),Mathf.SmoothStep(.58f,.76f,Fbm(x,y,4,175))*.6f);
                }
            }
            return Color.magenta;
        }
    }
}
