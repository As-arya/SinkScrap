using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SinkScrapt
{
    // Shared look for every screen: dark teal industrial panels, amber accents, monospaced terminal type.
    public static class Theme
    {
        public static readonly Color Ink=new(.025f,.045f,.05f,.95f),Panel=new(.055f,.095f,.105f,.9f),Raised=new(.11f,.19f,.20f,.96f),Line=new(.31f,.46f,.43f,1),
            Paper=new(.89f,.91f,.83f),Muted=new(.54f,.67f,.63f),Amber=new(.97f,.67f,.25f),Teal=new(.30f,.73f,.68f),Red=new(.90f,.25f,.21f),Good=new(.52f,.83f,.47f),Clear=new(0,0,0,0);
        static TMP_FontAsset font;
        static Sprite fill,frame;
        public static TMP_FontAsset Font
        {
            get {
                if(font!=null)return font;
                var fallback=Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");TMP_FontAsset made=null;
                // Consolas ships with Windows; if it is missing the bundled font is used instead.
                try {made=TMP_FontAsset.CreateFontAsset("Consolas","Bold",90);if(made==null)made=TMP_FontAsset.CreateFontAsset("Consolas","Regular",90);} catch(Exception) {made=null;}
                if(made!=null&&fallback!=null) {if(made.fallbackFontAssetTable==null)made.fallbackFontAssetTable=new System.Collections.Generic.List<TMP_FontAsset>();made.fallbackFontAssetTable.Add(fallback);}
                return font=made!=null?made:fallback;
            }
        }
        static Sprite scanlines,vignette;
        // CRT overlays for the boat computer: one dark line every 4 px (tiled), and a soft dark edge.
        public static Sprite Scanlines
        {
            get {
                if(scanlines!=null)return scanlines;
                var tex=new Texture2D(4,4,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Repeat,hideFlags=HideFlags.HideAndDontSave};
                var pixels=new Color32[16];for(int i=0;i<16;i++)pixels[i]=new Color32(255,255,255,(byte)(i/4==0?255:i/4==1?70:0));
                tex.SetPixels32(pixels);tex.Apply();return scanlines=Sprite.Create(tex,new Rect(0,0,4,4),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
            }
        }
        public static Sprite Vignette
        {
            get {
                if(vignette!=null)return vignette;
                const int n=64;var tex=new Texture2D(n,n,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
                var pixels=new Color32[n*n];
                for(int y=0;y<n;y++)for(int x=0;x<n;x++) {float u=Mathf.Abs(x+.5f-n/2f)/(n/2f),v=Mathf.Abs(y+.5f-n/2f)/(n/2f);float d=Mathf.Pow(Mathf.Pow(u,4)+Mathf.Pow(v,4),.25f);pixels[y*n+x]=new Color32(255,255,255,(byte)(Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,1.02f,d))*255));}
                tex.SetPixels32(pixels);tex.Apply();return vignette=Sprite.Create(tex,new Rect(0,0,n,n),new Vector2(.5f,.5f),100);
            }
        }
        static Sprite metal;
        // Riveted, bevelled steel frame (9-slice, 8 px border) for the HUD clusters, after the reference HUD art.
        public static Sprite Metal
        {
            get {
                if(metal!=null)return metal;
                const int n=32,b=8;var tex=new Texture2D(n,n,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
                var pixels=new Color32[n*n];
                for(int y=0;y<n;y++)for(int x=0;x<n;x++) {
                    bool edge=x<b||y<b||x>=n-b||y>=n-b;if(!edge) {pixels[y*n+x]=new Color32(0,0,0,0);continue;}
                    int d=Mathf.Min(Mathf.Min(x,y),Mathf.Min(n-1-x,n-1-y));
                    float v=.34f;
                    if(d<=1)v=(x<=1||y>=n-2)?.52f:.16f;   // outer bevel: light top-left, dark bottom-right
                    else if(d==b-1)v=.1f;                 // dark lip against the inset
                    float grime=Mathf.Repeat(Mathf.Sin(x*12.9898f+y*78.233f)*43758.55f,1);v+=(grime-.5f)*.06f;
                    bool rust=grime>.93f;
                    bool rivet=(x==3||x==4||x==n-5||x==n-4)&&(y==3||y==4||y==n-5||y==n-4);
                    var c=rivet?new Color(.68f,.68f,.66f):rust?new Color(.42f,.24f,.14f):new Color(v,v*1.02f,v*1.03f);
                    if(rivet&&(x==4||x==n-4)&&(y==3||y==n-5))c=new Color(.2f,.2f,.2f);
                    pixels[y*n+x]=c;
                }
                tex.SetPixels32(pixels);tex.Apply();
                return metal=Sprite.Create(tex,new Rect(0,0,n,n),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(b,b,b,b));
            }
        }
        // 16 x 16 pixel icons ('#' full, '+' half, '.' empty), tinted by the Image colour.
        static readonly System.Collections.Generic.Dictionary<string,Sprite> icons=new();
        static readonly System.Collections.Generic.Dictionary<string,string[]> IconArt=new() {
            {"heart",new[]{"................","..###.....###...",".#####...#####..","###############.","###############.","###############.","###############.",".#############..","..###########...","...#########....","....#######.....",".....#####......","......###.......",".......#........","................","................"}},
            {"run",new[]{"..........##....","..........##....","......#####.....",".....##.###.##..","....##..#####...","...##...##......","........##......",".......####.....","......##..##....",".....##....##...","....##......#...","...##...........","..##............","................","................","................"}},
            {"torch",new[]{"................","................","................","..........#####.",".........#######","##########++++##","##########++++##","##########++++##","##########++++##",".........#######","..........#####.","................","................","................","................","................"}},
            {"radio",new[]{"...#............","...#............","...#............","..###########...","..#+++++++++#...","..#+++++++++#...","..###########...","..#.#.#.#...#...","..###########...","..#.#.#.#...#...","..###########...","..#.#.#.#...#...","..###########...","................","................","................"}},
            {"air",new[]{"................",".....######.....","...##......##...","..#...##.....#..",".#...#.......#..",".#..#.........#.","#.............#.","#.............#.","#.............#.","#.............#.",".#...........#..",".#...........#..","..#.........#...","...##.....##....",".....#####......","................"}},
        };
        public static Sprite Icon(string name)
        {
            if(icons.TryGetValue(name,out var s)&&s!=null)return s;
            if(!IconArt.TryGetValue(name,out var art))return Fill;
            var tex=new Texture2D(16,16,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            var pixels=new Color32[256];
            for(int y=0;y<16;y++)for(int x=0;x<16;x++) {char c=y<art.Length&&x<art[y].Length?art[y][x]:'.';pixels[(15-y)*16+x]=c=='#'?new Color32(255,255,255,255):c=='+'?new Color32(150,150,150,255):new Color32(0,0,0,0);}
            tex.SetPixels32(pixels);tex.Apply();
            return icons[name]=Sprite.Create(tex,new Rect(0,0,16,16),new Vector2(.5f,.5f),16);
        }
        public static Sprite Fill => fill!=null?fill:(fill=Make(4,0));
        public static Sprite Frame => frame!=null?frame:(frame=Make(12,3));
        static Sprite Make(int size,int border)
        {
            var tex=new Texture2D(size,size,TextureFormat.RGBA32,false) {filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++) {
                bool edge=border==0||x<border||y<border||x>=size-border||y>=size-border;
                bool corner=border>0&&(x==0||x==size-1)&&(y==0||y==size-1);
                pixels[y*size+x]=edge&&!corner?new Color32(255,255,255,255):new Color32(255,255,255,0);
            }
            tex.SetPixels32(pixels);tex.Apply();
            return Sprite.Create(tex,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(border,border,border,border));
        }
    }

    // Hover colour, accent bar, and click/hover sounds for any button-like rect.
    public class UiFeedback : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler
    {
        public Image fill,accent;
        public Color normal,hover;
        public Selectable selectable;
        bool over;
        Color current;
        void OnEnable() {current=normal;if(fill!=null)fill.color=normal;if(accent!=null)accent.color=Theme.Clear;}
        bool Interactive=>selectable==null||selectable.IsInteractable();
        public void OnPointerEnter(PointerEventData e) {over=true;if(Interactive)Sfx.Ui(Snd.UiHover,.5f);}
        public void OnPointerExit(PointerEventData e) {over=false;}
        public void OnPointerDown(PointerEventData e) {if(Interactive)Sfx.Ui(Snd.UiClick,.7f);else Sfx.Ui(Snd.UiError,.5f);}
        void Update()
        {
            bool on=over&&Interactive;var target=!Interactive?new Color(normal.r*.6f,normal.g*.6f,normal.b*.6f,normal.a):on?hover:normal;
            current=Color.Lerp(current,target,1-Mathf.Exp(-16*Time.unscaledDeltaTime));
            if(fill!=null)fill.color=current;
            if(accent!=null)accent.color=Color.Lerp(accent.color,on?Theme.Amber:Theme.Clear,1-Mathf.Exp(-20*Time.unscaledDeltaTime));
        }
    }

    // Segmented status bar: health, stamina and oxygen. Pulses when low.
    public class UiBar : MonoBehaviour
    {
        public Image fill,back;
        public TMP_Text label;
        public Color color;
        float shown=1;
        public void Set(float value,string text,bool low=false,bool snap=false)
        {
            value=Mathf.Clamp01(value);shown=snap?value:Mathf.MoveTowards(shown,value,Time.unscaledDeltaTime*1.6f);
            fill.fillAmount=shown;
            var tint=color;if(low)tint=Color.Lerp(color,Theme.Red,.5f+.5f*Mathf.Sin(Time.unscaledTime*9));fill.color=tint;
            if(label!=null)label.text=text;
        }
    }

    // Occasional horizontal jitter for the logo.
    public class UiGlitch : MonoBehaviour
    {
        RectTransform rect;Vector2 home;float until,next;
        void Awake() {rect=(RectTransform)transform;home=rect.anchoredPosition;next=Time.unscaledTime+UnityEngine.Random.Range(2f,5f);}
        void Update()
        {
            if(Time.unscaledTime>next) {until=Time.unscaledTime+.12f;next=Time.unscaledTime+UnityEngine.Random.Range(2.5f,6f);}
            rect.anchoredPosition=home+(Time.unscaledTime<until?new Vector2(UnityEngine.Random.Range(-6f,6f),UnityEngine.Random.Range(-2f,2f)):Vector2.zero);
        }
    }

    public static class Ui
    {
        public static readonly Vector2 TopLeft=new(0,1),TopRight=new(1,1),BottomLeft=Vector2.zero,BottomRight=new(1,0),Center=new(.5f,.5f),TopCenter=new(.5f,1),BottomCenter=new(.5f,0);
        public static RectTransform Rect(string name,Transform parent,Vector2 anchor,Vector2 pivot,Vector2 pos,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));var r=(RectTransform)go.transform;r.SetParent(parent,false);
            r.anchorMin=anchor;r.anchorMax=anchor;r.pivot=pivot;r.anchoredPosition=pos;r.sizeDelta=size;return r;
        }
        public static RectTransform Stretch(string name,Transform parent,float inset=0)
        {
            var go=new GameObject(name,typeof(RectTransform));var r=(RectTransform)go.transform;r.SetParent(parent,false);
            r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(inset,inset);r.offsetMax=new Vector2(-inset,-inset);return r;
        }
        public static Image Fill(Transform rect,Color color,bool raycast=false)
        {
            var image=rect.gameObject.AddComponent<Image>();image.sprite=Theme.Fill;image.color=color;image.raycastTarget=raycast;return image;
        }
        public static RectTransform Panel(Transform parent,string name,Vector2 anchor,Vector2 pivot,Vector2 pos,Vector2 size,Color? fill=null,Color? border=null,bool raycast=false)
        {
            var r=Rect(name,parent,anchor,pivot,pos,size);Fill(r,fill??Theme.Panel,raycast);
            var frame=Stretch("Frame",r);var image=frame.gameObject.AddComponent<Image>();image.sprite=Theme.Frame;image.type=Image.Type.Sliced;image.color=border??Theme.Line;image.raycastTarget=false;
            return r;
        }
        // A dark inset panel inside the riveted steel frame (HUD clusters).
        public static RectTransform MetalPanel(Transform parent,string name,Vector2 anchor,Vector2 pivot,Vector2 pos,Vector2 size,Color? inner=null)
        {
            var r=Rect(name,parent,anchor,pivot,pos,size);var back=Stretch("Inset",r,6);Fill(back,inner??new Color(.035f,.04f,.04f,.92f));
            var frame=Stretch("Frame",r);var image=frame.gameObject.AddComponent<Image>();image.sprite=Theme.Metal;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=.5f;image.color=Color.white;image.raycastTarget=false;
            return r;
        }
        public static Image Icon(Transform parent,string icon,Vector2 pos,float size,Color color,Vector2? anchor=null)
        {
            var a=anchor??TopLeft;var r=Rect("Icon "+icon,parent,a,a,pos,new Vector2(size,size));var image=r.gameObject.AddComponent<Image>();image.sprite=Theme.Icon(icon);image.color=color;image.raycastTarget=false;image.preserveAspect=true;return image;
        }
        public static TMP_Text Label(Transform parent,string text,Vector2 pos,Vector2 size,float fontSize=24,Color? color=null,TextAlignmentOptions align=TextAlignmentOptions.TopLeft,Vector2? anchor=null,Vector2? pivot=null)
        {
            var a=anchor??TopLeft;var r=Rect("Text",parent,a,pivot??a,pos,size);var t=r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font=Theme.Font;t.text=text;t.fontSize=fontSize;t.color=color??Theme.Paper;t.alignment=align;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Overflow;return t;
        }
        public static Button Button(Transform parent,string text,Vector2 pos,Vector2 size,Action click,bool enabled=true,bool primary=false,string hint="",float fontSize=25)
        {
            var r=Rect("Button "+text,parent,TopLeft,TopLeft,pos,size);
            var back=Fill(r,Theme.Raised,true);
            var frame=Stretch("Frame",r);var edge=frame.gameObject.AddComponent<Image>();edge.sprite=Theme.Frame;edge.type=Image.Type.Sliced;edge.color=primary?Theme.Amber:Theme.Line;edge.raycastTarget=false;
            var bar=Rect("Accent",r,new Vector2(0,.5f),new Vector2(0,.5f),Vector2.zero,new Vector2(7,size.y));var accent=Fill(bar,Theme.Clear);
            var button=r.gameObject.AddComponent<Button>();button.targetGraphic=back;button.transition=Selectable.Transition.None;button.interactable=enabled;button.onClick.AddListener(()=>click());
            var normal=primary?new Color(.43f,.27f,.10f,.98f):Theme.Raised;var hover=primary?new Color(.66f,.42f,.14f,1):new Color(.18f,.31f,.31f,1);
            var fx=r.gameObject.AddComponent<UiFeedback>();fx.fill=back;fx.accent=accent;fx.normal=normal;fx.hover=hover;fx.selectable=button;
            var label=Label(r,text,new Vector2(24,0),new Vector2(size.x-48-(hint.Length>0?size.x*.35f:0),size.y),fontSize,enabled?Theme.Paper:Theme.Muted,TextAlignmentOptions.MidlineLeft,new Vector2(0,.5f),new Vector2(0,.5f));
            if(hint.Length>0)Label(r,hint,new Vector2(-18,0),new Vector2(size.x*.4f,size.y),fontSize*.78f,Theme.Muted,TextAlignmentOptions.MidlineRight,new Vector2(1,.5f),new Vector2(1,.5f));
            return button;
        }
        public static TMP_InputField Input(Transform parent,string value,string placeholder,Vector2 pos,Vector2 size)
        {
            var r=Panel(parent,"Input",TopLeft,TopLeft,pos,size,new Color(.03f,.06f,.065f,.98f),Theme.Line,true);
            var field=r.gameObject.AddComponent<TMP_InputField>();var area=Stretch("Text Area",r,10);area.gameObject.AddComponent<RectMask2D>();
            var text=Label(area,value,Vector2.zero,Vector2.zero,26,Theme.Paper,TextAlignmentOptions.MidlineLeft);text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;text.rectTransform.offsetMin=Vector2.zero;text.rectTransform.offsetMax=Vector2.zero;
            var hint=Label(area,placeholder,Vector2.zero,Vector2.zero,26,Theme.Muted,TextAlignmentOptions.MidlineLeft);hint.rectTransform.anchorMin=Vector2.zero;hint.rectTransform.anchorMax=Vector2.one;hint.rectTransform.offsetMin=Vector2.zero;hint.rectTransform.offsetMax=Vector2.zero;
            field.textViewport=area;field.textComponent=text;field.placeholder=hint;field.characterLimit=40;field.text=value;return field;
        }
        public static UiBar Bar(Transform parent,string name,Vector2 pos,Vector2 size,Color color,float fontSize=20)
        {
            var r=Panel(parent,name,TopLeft,TopLeft,pos,size,new Color(.02f,.035f,.04f,.88f),Theme.Line);
            var holder=Stretch("Fill holder",r,4);var fillRect=Stretch("Fill",holder);var image=fillRect.gameObject.AddComponent<Image>();image.sprite=Theme.Fill;image.type=Image.Type.Filled;image.fillMethod=Image.FillMethod.Horizontal;image.fillOrigin=0;image.color=color;image.raycastTarget=false;
            var label=Label(r,"",new Vector2(12,0),new Vector2(size.x-24,size.y),fontSize,Theme.Paper,TextAlignmentOptions.MidlineLeft,new Vector2(0,.5f),new Vector2(0,.5f));
            var bar=r.gameObject.AddComponent<UiBar>();bar.fill=image;bar.label=label;bar.color=color;return bar;
        }
        public static KeyCap Key(Transform parent,string key,Vector2 pos,float size=46)
        {
            var r=Panel(parent,"Key "+key,TopLeft,TopLeft,pos,new Vector2(Mathf.Max(size,26+key.Length*20),size),Theme.Raised,Theme.Amber);
            var label=Label(r,key,Vector2.zero,r.sizeDelta,size*.52f,Theme.Amber,TextAlignmentOptions.Center);return new KeyCap {rect=r,label=label};
        }
        public class KeyCap {public RectTransform rect;public TMP_Text label;}
        public static Slider Slider(Transform parent,Vector2 pos,Vector2 size,float min,float max,float value,Action<float> changed,TMP_Text valueLabel=null,Func<float,string> format=null)
        {
            var r=Panel(parent,"Slider",TopLeft,TopLeft,pos,size,new Color(.02f,.035f,.04f,.95f),Theme.Line,true);
            var area=Stretch("Fill area",r,4);var fillRect=Stretch("Fill",area);var fill=fillRect.gameObject.AddComponent<Image>();fill.sprite=Theme.Fill;fill.color=new Color(.62f,.40f,.14f,1);fill.raycastTarget=false;
            var handleRect=Rect("Handle",area,new Vector2(0,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(14,size.y+6));var handle=Fill(handleRect,Theme.Amber,true);
            var slider=r.gameObject.AddComponent<Slider>();slider.fillRect=fillRect;slider.handleRect=handleRect;slider.targetGraphic=handle;slider.direction=UnityEngine.UI.Slider.Direction.LeftToRight;slider.minValue=min;slider.maxValue=max;slider.value=value;
            Action<float> update=v=>{if(valueLabel!=null)valueLabel.text=format!=null?format(v):Mathf.RoundToInt(v*100/Mathf.Max(max,.001f))+"%";};
            slider.onValueChanged.AddListener(v=>{changed(v);update(v);Sfx.Ui(Snd.UiHover,.35f,Mathf.Lerp(.8f,1.3f,Mathf.InverseLerp(min,max,v)));});update(value);return slider;
        }
        // Removes everything but the panel's own border.
        public static void Clear(Transform parent) {for(int i=parent.childCount-1;i>=0;i--) {var child=parent.GetChild(i);if(child.name!="Frame")UnityEngine.Object.Destroy(child.gameObject);}}
    }
}
