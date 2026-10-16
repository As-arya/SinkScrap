using System;
using System.Collections.Generic;
using UnityEngine;

namespace SinkScrapt
{
    public enum Snd
    {
        UiClick, UiHover, UiConfirm, UiError, UiOpen, Coin, Sale, Purchase, Pickup, Drop, Deposit, TorchOn, TorchOff, FlareIgnite, FlareHiss, Hatch, Rope,
        StepMetal, StepGround, StepWet, StepWood, Jump, Land, SplashIn, SplashOut, Swim, BreathIn, BreathOut, Heartbeat, Hurt, Death, Gasp,
        Warn, Siren, Horn, RadioOn, RadioOff, Steam, Zap, Drip, Creak, Groan, Rumble, Sea, Wind, Drone, Engine, Bell, Thunk,
        MusicBed, MusicPulse, MusicChase,
        StalkerBreath, StalkerGrowl, StalkerScreech, StalkerRoar, StalkerStep, DrownedGurgle, DrownedPing, DrownedLunge,
        QuotaPass, QuotaFail,
        // Recorded-only sounds (Resources/SinkScrapt/Audio); without a recording they fall back to a related synthesised one.
        StepGrate, StepGravel, Gull, Lap, BoatCreak, DoorOpen, DoorClose, DoorOpenWood, DoorCloseWood, DoorLocked,
        StalkerScream, DrownedScream, Bones, Stinger, Tension, RoomTone
    }

    // Game audio. Recorded clips come first: CC0 recordings processed by ArtSource/Audio/build_audio.py into
    // Resources/SinkScrapt/Audio/<Snd>_<variant>.ogg (sources and licences in Docs/Assets.md), one clip per variant. Every other
    // sound (UI, alarms, steam, sparks) is synthesised once from noise, oscillators and filters, which is also the fallback for a
    // missing recording.
    public static class SoundBank
    {
        public const int Rate=22050;
        static readonly Dictionary<(Snd,int),AudioClip> cache=new();
        static Dictionary<Snd,AudioClip[]> recorded;
        // The recordings, grouped by sound and ordered by variant number (file name "<Snd>_<n>").
        static AudioClip[] Recorded(Snd id)
        {
            if(recorded==null) {
                recorded=new Dictionary<Snd,AudioClip[]>();var found=new Dictionary<Snd,SortedDictionary<int,AudioClip>>();
                foreach(var clip in Resources.LoadAll<AudioClip>("SinkScrapt/Audio")) {
                    int bar=clip.name.LastIndexOf('_');
                    if(bar<0||!Enum.TryParse<Snd>(clip.name[..bar],out var snd)||!int.TryParse(clip.name[(bar+1)..],out int n))continue;
                    if(!found.TryGetValue(snd,out var set))found[snd]=set=new SortedDictionary<int,AudioClip>();set[n]=clip;
                }
                foreach(var pair in found) {var clips=new AudioClip[pair.Value.Count];pair.Value.Values.CopyTo(clips,0);recorded[pair.Key]=clips;}
            }
            return recorded.TryGetValue(id,out var list)?list:null;
        }
        public static bool HasRecording(Snd id) => Recorded(id)!=null;
        static int SynthVariants(Snd id) => id switch {
            Snd.StepMetal or Snd.StepGround or Snd.StepWet or Snd.StepWood or Snd.Drip => 4,
            Snd.Creak or Snd.Hurt or Snd.Pickup or Snd.StalkerStep => 3,
            Snd.Groan or Snd.Drop => 2, _ => 1 };
        public static int Variants(Snd id) => Recorded(id)?.Length??SynthVariants(id);
        public static bool IsLoop(Snd id) => id is Snd.Steam or Snd.Zap or Snd.Rumble or Snd.Sea or Snd.Wind or Snd.Drone or Snd.Engine or Snd.MusicBed or Snd.MusicPulse or Snd.MusicChase or Snd.StalkerBreath or Snd.DrownedGurgle or Snd.FlareHiss or Snd.Lap or Snd.Tension or Snd.RoomTone;
        public static AudioClip Get(Snd id,int variant=0)
        {
            var rec=Recorded(id);if(rec!=null)return rec[((variant%rec.Length)+rec.Length)%rec.Length];
            int count=SynthVariants(id);variant=((variant%count)+count)%count;
            if(cache.TryGetValue((id,variant),out var clip)&&clip!=null) return clip;
            var data=Build(id,variant);clip=AudioClip.Create(id+"."+variant,data.Length,1,Rate,false);clip.SetData(data,0);cache[(id,variant)]=clip;return clip;
        }
        // The synthesised stand-in for a recorded-only sound.
        static Snd Fallback(Snd id) => id switch {
            Snd.StepGrate=>Snd.StepMetal, Snd.StepGravel=>Snd.StepGround, Snd.Lap=>Snd.Sea, Snd.BoatCreak=>Snd.Creak, Snd.DoorOpen or Snd.DoorOpenWood=>Snd.Hatch,
            Snd.DoorClose or Snd.DoorCloseWood or Snd.DoorLocked or Snd.Bones=>Snd.Thunk, Snd.StalkerScream=>Snd.StalkerScreech, Snd.DrownedScream=>Snd.DrownedLunge,
            Snd.Tension or Snd.RoomTone=>Snd.Drone, _=>id };

        // ---- DSP building blocks -------------------------------------------------------------------------------------------------
        static int Len(float sec) => Mathf.Max(1,Mathf.CeilToInt(sec*Rate));
        static float[] Noise(float sec,System.Random r) {var b=new float[Len(sec)];for(int i=0;i<b.Length;i++)b[i]=(float)(r.NextDouble()*2-1);return b;}
        // wave: 0 sine, 1 saw, 2 square, 3 triangle. f1<0 keeps the pitch constant.
        static float[] Osc(float sec,float f0,float f1,int wave,float amp=1)
        {
            var b=new float[Len(sec)];double ph=0;
            for(int i=0;i<b.Length;i++) {
                float u=(float)i/b.Length;double f=f1<0?f0:f0+(f1-f0)*u;ph+=f/Rate;float p=(float)(ph-Math.Floor(ph));
                b[i]=amp*(wave==0?Mathf.Sin(p*Mathf.PI*2):wave==1?p*2-1:wave==2?(p<.5f?1:-1):Mathf.Abs(p*4-2)-1);
            }
            return b;
        }
        static float[] Mix(params float[][] layers)
        {
            int n=0;foreach(var l in layers)n=Mathf.Max(n,l.Length);var b=new float[n];
            foreach(var l in layers)for(int i=0;i<l.Length;i++)b[i]+=l[i];return b;
        }
        static float[] Gain(float[] x,float g) {var b=new float[x.Length];for(int i=0;i<x.Length;i++)b[i]=x[i]*g;return b;}
        static float[] Mul(float[] a,float[] b) {var y=new float[a.Length];for(int i=0;i<a.Length;i++)y[i]=a[i]*b[i%b.Length];return y;}
        static float[] Delay(float[] x,float sec) {int d=Len(sec);var b=new float[x.Length+d];Array.Copy(x,0,b,d,x.Length);return b;}
        // Attack/decay envelope: linear attack, then exponential decay that reaches about -43 dB at `decay` seconds. A short tail fade avoids clicks.
        static float[] Env(float[] x,float attack,float decay)
        {
            var y=new float[x.Length];int fade=Mathf.Min(x.Length/2,Len(.004f));
            for(int i=0;i<x.Length;i++) {
                float t=(float)i/Rate;float e=t<attack?t/Mathf.Max(attack,.0001f):Mathf.Exp(-(t-attack)*5f/Mathf.Max(decay,.001f));
                if(i>x.Length-fade)e*=(float)(x.Length-i)/fade;y[i]=x[i]*e;
            }
            return y;
        }
        // Smooth swell with fade in/out over the whole clip.
        static float[] Swell(float[] x,float power=1.5f) {var y=new float[x.Length];for(int i=0;i<x.Length;i++)y[i]=x[i]*Mathf.Pow(Mathf.Sin(Mathf.PI*i/x.Length),power);return y;}
        static float[] Tanh(float[] x,float drive) {var y=new float[x.Length];for(int i=0;i<x.Length;i++)y[i]=(float)Math.Tanh(x[i]*drive);return y;}
        static float[] Echo(float[] x,float sec,float feedback,int repeats)
        {
            int d=Len(sec);var b=new float[x.Length+d*repeats];float g=1;
            for(int k=0;k<=repeats;k++) {for(int i=0;i<x.Length;i++)b[i+d*k]+=x[i]*g;g*=feedback;}return b;
        }
        static float[] Norm(float[] x,float peak)
        {
            float max=.0001f;for(int i=0;i<x.Length;i++)max=Mathf.Max(max,Mathf.Abs(x[i]));var y=new float[x.Length];float g=peak/max;for(int i=0;i<x.Length;i++)y[i]=x[i]*g;return y;
        }
        // Biquad filter. type: 0 low-pass, 1 high-pass, 2 band-pass.
        static float[] Biquad(float[] x,int type,float freq,float q)
        {
            var y=new float[x.Length];float x1=0,x2=0,y1=0,y2=0;Coeff(type,freq,q,out float b0,out float b1,out float b2,out float a1,out float a2);
            for(int i=0;i<x.Length;i++) {
                float v=b0*x[i]+b1*x1+b2*x2-a1*y1-a2*y2;y[i]=v;x2=x1;x1=x[i];y2=y1;y1=v;
            }
            return y;
        }
        // Same filter with the cutoff gliding from f0 to f1 over the clip.
        static float[] Sweep(float[] x,int type,float f0,float f1,float q)
        {
            var y=new float[x.Length];float x1=0,x2=0,y1=0,y2=0;float b0=0,b1=0,b2=0,a1=0,a2=0;
            for(int i=0;i<x.Length;i++) {
                if((i&31)==0)Coeff(type,Mathf.Lerp(f0,f1,(float)i/x.Length),q,out b0,out b1,out b2,out a1,out a2);
                float v=b0*x[i]+b1*x1+b2*x2-a1*y1-a2*y2;y[i]=v;x2=x1;x1=x[i];y2=y1;y1=v;
            }
            return y;
        }
        static void Coeff(int type,float freq,float q,out float b0,out float b1,out float b2,out float a1,out float a2)
        {
            float w=2*Mathf.PI*Mathf.Clamp(freq,20,Rate*.45f)/Rate,cos=Mathf.Cos(w),alpha=Mathf.Sin(w)/(2*Mathf.Max(q,.1f));float a0=1+alpha;
            if(type==0) {b0=(1-cos)/2;b1=1-cos;b2=b0;}else if(type==1) {b0=(1+cos)/2;b1=-(1+cos);b2=b0;}else {b0=alpha;b1=0;b2=-alpha;}
            a1=-2*cos;a2=1-alpha;b0/=a0;b1/=a0;b2/=a0;a1/=a0;a2/=a0;
        }
        static float[] LP(float[] x,float f,float q=.7f)=>Biquad(x,0,f,q);
        static float[] HP(float[] x,float f,float q=.7f)=>Biquad(x,1,f,q);
        static float[] BP(float[] x,float f,float q=1)=>Biquad(x,2,f,q);
        // Cross-fades the tail into the head so the clip repeats without a click.
        static float[] Loop(float[] x,float xfade=.25f)
        {
            int f=Mathf.Min(Len(xfade),x.Length/3);int n=x.Length-f;var y=new float[n];
            for(int i=0;i<n;i++) {float w=i<f?(float)i/f:1;y[i]=x[i]*w+(i<f?x[n+i]*(1-w):0);}
            return y;
        }
        static float[] Bell(float f,float sec,float bright=1)
        {
            return Env(Mix(Osc(sec,f,-1,0,1),Osc(sec,f*2.76f,-1,0,.35f*bright),Osc(sec,f*5.4f,-1,0,.16f*bright),Osc(sec,f*8.9f,-1,0,.07f*bright)),.002f,sec*.9f);
        }
        static float[] Thump(float sec,float f0,float f1,float amp=1)=>Env(Osc(sec,f0,f1,0,amp),.002f,sec*.8f);
        static float Rand(System.Random r,float a,float b)=>a+(float)r.NextDouble()*(b-a);
        // Quiet sparse bubbles used by underwater sounds.
        static float[] Bubbles(System.Random r,float sec,int count,float low=300,float high=1200,float amp=.35f)
        {
            var b=new float[Len(sec)];
            for(int k=0;k<count;k++) {
                float f=Rand(r,low,high);var p=Env(Osc(.09f,f,f*Rand(r,1.4f,2.2f),0,amp),.002f,.07f);int at=r.Next(0,Mathf.Max(1,b.Length-p.Length));
                for(int i=0;i<p.Length&&at+i<b.Length;i++)b[at+i]+=p[i];
            }
            return b;
        }

        // ---- Recipes ----------------------------------------------------------------------------------------------------------------
        static float[] Build(Snd id,int v)
        {
            var r=new System.Random((int)id*131+v*17+7);
            switch(id) {
                case Snd.UiClick: return Norm(Env(Mix(Osc(.06f,1900,-1,0),Osc(.06f,950,-1,0,.5f)),.001f,.045f),.5f);
                case Snd.UiHover: return Norm(Env(Osc(.05f,1350,-1,0),.002f,.035f),.25f);
                case Snd.UiConfirm: return Norm(Mix(Env(Osc(.2f,660,-1,0),.003f,.15f),Delay(Env(Osc(.2f,990,-1,0),.003f,.17f),.07f)),.55f);
                case Snd.UiError: return Norm(Env(LP(Osc(.26f,130,-1,2),900),.004f,.22f),.55f);
                case Snd.UiOpen: return Norm(Env(Sweep(Noise(.3f,r),2,500,2600,2.2f),.06f,.24f),.4f);
                case Snd.Coin: return Norm(Mix(Bell(1568,.6f,.6f),Delay(Bell(2093,.65f,.6f),.09f)),.6f);
                case Snd.Sale: return Norm(Mix(Bell(1568,.5f,.6f),Delay(Bell(1760,.5f,.6f),.08f),Delay(Bell(2093,.55f,.6f),.16f),Delay(Bell(2637,.8f,.6f),.26f),Thump(.2f,140,60,.6f)),.65f);
                case Snd.Purchase: {var tick=Env(HP(Noise(.025f,r),2200),.001f,.015f);return Norm(Mix(tick,Delay(tick,.045f),Delay(tick,.09f),Delay(tick,.135f),Delay(Bell(1320,.4f,.5f),.18f)),.6f);}
                case Snd.Pickup: {float f=1100+v*170;return Norm(Mix(Env(Mix(Osc(.25f,f,-1,0),Osc(.25f,f*2.4f,-1,0,.4f),Osc(.25f,f*3.7f,-1,0,.2f)),.001f,.16f),Env(HP(Noise(.06f,r),2500),.001f,.04f)),.5f);}
                case Snd.Drop: return Norm(Mix(Thump(.25f,110-v*15,45,1),Env(Mix(Osc(.2f,420+v*70,-1,0,.4f),Osc(.2f,1160+v*60,-1,0,.2f)),.001f,.14f),Env(LP(Noise(.12f,r),1800),.001f,.08f)),.7f);
                case Snd.Deposit: return Norm(Mix(Thump(.32f,150,55,1),Env(LP(Noise(.2f,r),700),.002f,.14f),Delay(Bell(880,.9f,.6f),.05f),Delay(Bell(1320,.7f,.5f),.15f)),.7f);
                case Snd.TorchOn: return Norm(Mix(Env(Osc(.05f,520,300,0),.001f,.035f),Env(HP(Noise(.02f,r),1800),.001f,.012f)),.5f);
                case Snd.TorchOff: return Norm(Mix(Env(Osc(.05f,330,210,0),.001f,.035f),Env(HP(Noise(.02f,r),1400),.001f,.012f)),.45f);
                case Snd.FlareIgnite: return Norm(Mix(Env(HP(Noise(1.4f,r),2800),.04f,1.1f),Env(Sweep(Noise(.5f,r),0,400,3500,1),.05f,.4f),Thump(.1f,300,120,.8f)),.7f);
                case Snd.FlareHiss: return Loop(Norm(Mul(HP(Noise(2f,r),3200),Gain(Osc(2f,1,-1,0),.15f)),.5f),.2f);
                case Snd.Hatch: return Norm(Mix(Thump(.4f,90,45,1),Env(Mix(Osc(.9f,220,-1,0,.5f),Osc(.9f,607,-1,0,.25f),Osc(.9f,1188,-1,0,.12f)),.002f,.7f),Delay(Env(HP(Noise(.5f,r),3500),.02f,.4f),.1f)),.75f);
                case Snd.Rope: return Norm(Mul(Env(BP(Osc(.5f,70,88,1),380,3),.05f,.4f),Gain(Osc(.5f,14,-1,0),.4f)),.45f);
                case Snd.StepMetal: {float f=240+v*55;return Norm(Mix(Env(Mix(Osc(.2f,f,-1,0,.5f),Osc(.2f,f*2.76f,-1,0,.25f),Osc(.2f,f*5.4f,-1,0,.12f)),.001f,.16f),Env(LP(Noise(.1f,r),2200),.001f,.07f),Thump(.1f,95,60,.8f)),.5f);}
                case Snd.StepGround: return Norm(Mix(Env(BP(Noise(.16f,r),900+v*150,1.2f),.003f,.1f),Gain(Env(HP(Noise(.08f,r),3500),.001f,.05f),.4f),Thump(.09f,85,55,.6f)),.45f);
                case Snd.StepWet: return Norm(Mix(Env(Sweep(Noise(.3f,r),0,2600,700,.8f),.01f,.25f),Bubbles(r,.3f,2,350,900,.25f)),.5f);
                case Snd.StepWood: return Norm(Mix(Thump(.14f,130-v*8,70,1),Gain(Env(LP(Noise(.08f,r),900),.001f,.05f),.5f)),.5f);
                case Snd.Jump: return Norm(Mix(Gain(Env(BP(Noise(.15f,r),1500,.8f),.01f,.1f),.4f),Env(Osc(.18f,200,420,0,.3f),.01f,.15f)),.35f);
                case Snd.Land: return Norm(Mix(Thump(.22f,80,40,1),Env(LP(Noise(.15f,r),1200),.002f,.1f)),.6f);
                case Snd.SplashIn: return Norm(Mix(Env(Sweep(Noise(.7f,r),0,3500,500,.8f),.01f,.6f),Bubbles(r,.7f,6,300,1300,.3f)),.7f);
                case Snd.SplashOut: return Norm(Mix(Env(Sweep(Noise(.55f,r),0,2400,900,.8f),.02f,.45f),Bubbles(r,.5f,3,400,1000,.2f)),.5f);
                case Snd.Swim: return Norm(Env(Sweep(Noise(.55f,r),0,500,1300,.9f),.15f,.35f),.3f);
                case Snd.BreathIn: return Norm(Env(Sweep(Noise(.9f,r),2,900,2000,1.5f),.35f,.5f),.35f);
                case Snd.BreathOut: return Norm(Mix(Env(Sweep(Noise(1f,r),2,2000,900,1.5f),.08f,.8f),Bubbles(r,1f,7,350,1000,.18f)),.35f);
                case Snd.Gasp: return Norm(Env(Sweep(Noise(.45f,r),2,1200,3000,1.2f),.05f,.35f),.5f);
                case Snd.Heartbeat: return Norm(LP(Mix(Thump(.25f,62,38,1),Delay(Thump(.25f,55,34,.7f),.22f)),200),.8f);
                case Snd.Hurt: return Norm(Mix(Env(LP(Noise(.25f,r),1400),.002f,.18f),Env(LP(Osc(.3f,150-v*20,80,1,.5f),900),.005f,.22f)),.6f);
                case Snd.Death: return Norm(Mix(Env(LP(Osc(1.2f,140,35,1,.6f),500),.01f,1f),Env(LP(Noise(.7f,r),300),.02f,.6f)),.7f);
                case Snd.Warn: {var beep=Env(Osc(.12f,880,-1,0),.003f,.1f);return Norm(Mix(beep,Delay(beep,.2f),Delay(beep,.4f)),.5f);}
                case Snd.Siren: {var hi=Osc(.35f,980,-1,2);var lo=Osc(.35f,700,-1,2);return Norm(Env(LP(Mix(lo,Delay(hi,.35f),Delay(lo,.7f),Delay(hi,1.05f),Delay(lo,1.4f),Delay(hi,1.75f)),2200),.02f,2.2f),.5f);}
                case Snd.Horn: return Norm(Env(LP(Mix(Osc(2.4f,98,-1,1,.5f),Osc(2.4f,147,-1,1,.3f),Osc(2.4f,198,-1,1,.2f)),1100),.25f,2f),.7f);
                case Snd.RadioOn: return Norm(Mix(Env(BP(Noise(.14f,r),2400,2),.003f,.1f),Delay(Env(Osc(.05f,1800,-1,0),.002f,.03f),.12f)),.4f);
                case Snd.RadioOff: return Norm(Mix(Env(Osc(.05f,1200,-1,0),.002f,.03f),Delay(Env(BP(Noise(.14f,r),2000,2),.003f,.1f),.05f)),.4f);
                case Snd.Steam: return Loop(Norm(LP(HP(Noise(2.2f,r),1800),9000),.5f),.3f);
                case Snd.Zap: {var b=new float[Len(2.2f)];for(int i=0;i<b.Length;i++)if(r.NextDouble()<.004){float a=Rand(r,.4f,1);for(int k=0;k<200&&i+k<b.Length;k++)b[i+k]+=a*Mathf.Exp(-k*.05f)*(r.Next(2)*2-1);}return Loop(Norm(Mix(HP(b,1200),Osc(2.2f,120,-1,1,.25f)),.5f),.3f);}
                case Snd.Drip: {var plink=Env(Osc(.12f,1400+v*180,900+v*100,0,.7f),.001f,.07f);return Norm(Echo(LP(plink,3500),.19f,.45f,3),.35f);}
                case Snd.Creak: {float dur=1.8f+v*.6f;return Norm(Swell(Mul(BP(Osc(dur,62+v*6,84+v*4,1),420+v*60,4),Add(.6f,Gain(Osc(dur,9+v,-1,0),.4f)))),.5f);}
                case Snd.Groan: return Norm(Swell(Mul(LP(Mix(Osc(4f,38+v*4,52,1,.6f),Osc(4f,39+v*4,53.5f,1,.4f)),420),Add(.7f,Gain(Osc(4f,5,-1,0),.3f)))),.6f);
                case Snd.Rumble: return Loop(Norm(Mul(LP(Noise(6.5f,r),90,.5f),Add(.7f,Gain(Osc(6.5f,2f/6f,-1,0),.3f))),.7f),.5f);
                case Snd.Sea: return Loop(Norm(Mul(Mix(LP(Noise(10.5f,r),600),Gain(LP(Noise(10.5f,r),1600),.3f)),Add(.65f,Gain(Osc(10.5f,.2f,-1,0),.35f))),.55f),.5f);
                case Snd.Wind: return Loop(Norm(Mul(LP(HP(Noise(8.5f,r),220),1500,.8f),Add(.6f,Gain(Osc(8.5f,.125f,-1,0),.4f))),.4f),.5f);
                case Snd.Drone: {const float L=12;var d=Mix(Osc(L+.5f,55,-1,0,.6f),Osc(L+.5f,82.5f,-1,0,.35f),Osc(L+.5f,110,-1,0,.25f),Osc(L+.5f,116.5f,-1,0,.12f),Gain(LP(Noise(L+.5f,r),160),.5f));return Loop(Norm(Mul(d,Add(.75f,Gain(Osc(L+.5f,1f/6f,-1,0),.25f))),.6f),.5f);}
                case Snd.Engine: {const float L=3;return Loop(Norm(Mul(Mix(LP(Osc(L+.4f,48,-1,1,.7f),500),Gain(LP(Noise(L+.4f,r),250),.6f)),Add(.75f,Gain(Osc(L+.4f,8,-1,0),.25f))),.5f),.4f);}
                case Snd.Bell: return Norm(Echo(Bell(392,2.4f,.8f),.5f,.4f,3),.5f);
                case Snd.Thunk: return Norm(Mix(Thump(.25f,100,50,1),Env(LP(Noise(.15f,r),600),.002f,.1f)),.6f);
                case Snd.MusicBed: {const float L=24;var d=Mix(Osc(L+1,55,-1,0,.5f),Osc(L+1,58.25f,-1,0,.35f),Osc(L+1,82.5f,-1,0,.3f),Osc(L+1,110,-1,0,.2f),Mul(Osc(L+1,164.5f,-1,0,.12f),Add(.5f,Gain(Osc(L+1,1f/L,-1,0),.5f))),Mul(Osc(L+1,880.5f,-1,0,.025f),Add(.5f,Gain(Osc(L+1,2f/L,-1,0),.5f))));return Loop(Norm(Mul(d,Add(.8f,Gain(Osc(L+1,1f/12f,-1,0),.2f))),.55f),1f);}
                case Snd.MusicPulse: {var one=Thump(.4f,58,36,1);var b=new float[Len(16f)];for(int k=0;k<16;k++)for(int i=0;i<one.Length&&k*Rate+i<b.Length;i++)b[k*Rate+i]+=one[i]*(k%4==0?1f:.6f);return Norm(LP(b,160),.55f);}
                case Snd.MusicChase: {var b=new float[Len(6.4f)];float[] notes={41.2f,41.2f,43.65f,41.2f,55f,41.2f,43.65f,32.7f};for(int k=0;k<32;k++){var n=Env(LP(Osc(.25f,notes[k%8],-1,1,.8f),700),.003f,.18f);int at=Mathf.RoundToInt(k*.2f*Rate);for(int i=0;i<n.Length&&at+i<b.Length;i++)b[at+i]+=n[i];if(k%2==1){var h=Env(HP(Noise(.05f,r),5000),.001f,.03f);for(int i=0;i<h.Length&&at+i<b.Length;i++)b[at+i]+=h[i]*.25f;}}return Norm(Tanh(b,1.8f),.55f);}
                case Snd.StalkerBreath: return Loop(Norm(Mul(Mix(BP(Noise(5f,r),520,1),Gain(LP(Osc(5f,62,-1,1,.3f),400),.5f)),Add(.2f,Gain(Mul(Osc(5f,1f/2.4f,-1,0),Osc(5f,1f/2.4f,-1,0)),.8f))),.5f),.4f);
                case Snd.StalkerGrowl: return Norm(Swell(Mix(Mul(LP(Osc(1.4f,75,55,1,.8f),700),Add(.5f,Gain(Osc(1.4f,35,-1,0),.5f))),Gain(BP(Noise(1.4f,r),350,1.5f),.6f)),1.2f),.8f);
                case Snd.StalkerScreech: return Norm(Env(Tanh(Mix(Osc(1.1f,880,1650,0,1),Mul(Osc(1.1f,880,1650,0,1),Add(.5f,Osc(1.1f,70,-1,0,.5f))),Gain(HP(Noise(1.1f,r),2000),.5f)),2f),.02f,1f),.7f);
                case Snd.StalkerRoar: return Norm(Env(Tanh(Mix(Osc(1.2f,55,48,1,.7f),Osc(1.2f,82,70,1,.4f),Gain(BP(Noise(1.2f,r),300,1),.8f)),3f),.08f,1f),.85f);
                case Snd.StalkerStep: return Norm(Mix(Thump(.28f,70-v*6,36,1),Gain(Env(BP(Noise(.1f,r),500,1),.002f,.08f),.5f),Gain(Env(HP(Noise(.04f,r),3200),.001f,.02f),.3f)),.7f);
                case Snd.DrownedGurgle: {var b=Mix(LP(Noise(4.3f,r),650),Bubbles(r,4.3f,14,250,900,.5f));return Loop(Norm(LP(b,1400),.5f),.3f);}
                case Snd.DrownedPing: return Norm(LP(Echo(Env(Osc(.9f,540,-1,0),.004f,.8f),.25f,.4f,2),2500),.6f);
                case Snd.DrownedLunge: return Norm(Env(Tanh(Mix(Sweep(Noise(.7f,r),0,3000,400,1),LP(Osc(.7f,160,70,1,.6f),600)),2f),.01f,.6f),.8f);
                case Snd.QuotaPass: return Norm(Mix(Bell(523,.9f,.5f),Delay(Bell(659,.9f,.5f),.12f),Delay(Bell(784,.9f,.5f),.24f),Delay(Bell(1047,1.4f,.5f),.4f)),.7f);
                case Snd.QuotaFail: return Norm(Mix(Env(LP(Mix(Osc(.7f,110,-1,1,.5f),Osc(.7f,131,-1,1,.4f)),700),.02f,.6f),Delay(Env(LP(Mix(Osc(1.1f,92,-1,1,.5f),Osc(1.1f,110,-1,1,.4f)),600),.02f,1f),.5f),Delay(Thump(.4f,70,35,1),.5f)),.7f);
            }
            var stand=Fallback(id);
            return stand!=id?Build(stand,v%SynthVariants(stand)):new float[Len(.1f)];
        }
        static float[] Add(float c,float[] x) {var y=new float[x.Length];for(int i=0;i<x.Length;i++)y[i]=c+x[i];return y;}
    }
}
