using UnityEngine;

namespace SinkScrapt
{
    // Client-side body of a monster: procedural model, walk/attack animation, glow light and voice.
    // It reads only the replicated NetEnemy state and the transform, so every peer sees and hears the same behaviour.
    public class EnemyRig : MonoBehaviour
    {
        NetEnemy brain;
        bool water,built;
        Transform root,hip,torso,head,jaw,shoulderL,shoulderR,foreL,foreR,legL,legR,shinL,shinR;
        Transform[] seg;
        Transform jawUp,jawLow,lure,tailFin,finL,finR;
        Light glow;
        AudioSource voice;
        float baseGlow;
        Vector3 last;
        float speed,phase,clock,stepDistance,pingAt,mutterAt;
        EnemyState state=EnemyState.Patrol,previous=EnemyState.Patrol;
        float lean,armRaise,armBend,headYaw,headPitch,jawOpen,shake;
        static readonly Color Skin=new(.15f,.20f,.19f),Wet=new(.09f,.14f,.14f),Bone=new(.64f,.60f,.47f),Belly=new(.30f,.38f,.33f);

        static Transform Pivot(string name,Transform parent,Vector3 local) {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=local;return t;}
        static GameObject Part(string name,PrimitiveType type,Transform parent,Vector3 local,Vector3 scale,Color color,bool emissive=false) => WorldFactory.Shape(name,type,parent,local,scale,color,false,emissive);
        static void Damp(ref float value,float target,float rate,float dt) {value=Mathf.Lerp(value,target,1-Mathf.Exp(-rate*dt));}

        // The Blender monsters (Models/Characters: mon_hull_stalker, mon_drowned_hunter) with their clips; without them the
        // procedural bodies below are built and animated in code.
        ClipSet clips;
        public string Clip=>clips?.Current??"";
        void Start()
        {
            brain=GetComponent<NetEnemy>();water=brain.waterEnemy;last=transform.position;
            if(!BuildModel()) {if(water)BuildHunter();else BuildStalker();}
            built=true;state=previous=brain.CurrentState;
            Sfx.At(water?Snd.DrownedPing:Snd.StalkerGrowl,transform.position+Vector3.up*1.5f,.9f,.8f,60);
        }
        bool BuildModel()
        {
            var model=ModelLibrary.Spawn(water?"Characters/mon_drowned_hunter":"Characters/mon_hull_stalker",transform,Vector3.zero);if(model==null)return false;
            clips=ClipSet.Find(model);if(clips==null) {Destroy(model);return false;}
            root=model.transform;
            var anchor=ModelLibrary.FindBone(root,water?"Lure2":"Head")??root;
            var lamp=new GameObject(water?"Lure glow":"Eye glow");lamp.transform.SetParent(anchor,false);
            lamp.transform.position=water?anchor.position+anchor.up*.45f:anchor.position+transform.forward*.3f+Vector3.up*.12f;
            glow=lamp.AddComponent<Light>();glow.type=LightType.Point;glow.shadows=LightShadows.None;
            if(water) {glow.color=new Color(.4f,1,.75f);glow.range=9;glow.intensity=baseGlow=2.6f;voice=Sfx.Loop(root,Snd.DrownedGurgle,24);}
            else {glow.color=new Color(1,.8f,.55f);glow.range=4.5f;glow.intensity=baseGlow=1.1f;voice=Sfx.Loop(anchor,Snd.StalkerBreath,22);}
            clips.Loop("idle");
            return true;
        }
        // Clip choice from the replicated state: one-shot attack/grab/bite clips start on the state change and are left to play
        // through the attack and recovery; otherwise idle, walk/swim or run/chase at the real pace.
        void AnimateModel()
        {
            bool committed=state==EnemyState.WindUp||state==EnemyState.Attack||state==EnemyState.Grab||state==EnemyState.Recover||state==EnemyState.Scream;
            if(committed) {
                var current=clips.State(clips.Current);
                if(state!=EnemyState.Recover||current==null||current.time<current.length)return;
            }
            if(water) {
                if(state==EnemyState.Chase)clips.Loop("chase",Mathf.Clamp(speed/3.2f,.7f,1.4f));
                else if(speed>.3f)clips.Loop("swim",Mathf.Clamp(speed/2.2f,.6f,1.4f));
                else clips.Loop("idle");
            } else {
                if(state==EnemyState.Chase&&speed>.5f)clips.Loop("run",Mathf.Clamp(speed/4.2f,.7f,1.4f));
                else if(speed>.3f)clips.Loop("walk",Mathf.Clamp(speed/1.9f,.6f,1.5f));
                else clips.Loop("idle");
            }
        }
        void BuildStalker()
        {
            root=Pivot("Hull stalker",transform,Vector3.zero);hip=Pivot("Hip",root,new Vector3(0,1.05f,0));torso=Pivot("Torso",hip,Vector3.zero);
            Part("Chest",PrimitiveType.Capsule,torso,new Vector3(0,.5f,0),new Vector3(.62f,.62f,.42f),Skin);
            Part("Ribs",PrimitiveType.Cube,torso,new Vector3(0,.55f,.17f),new Vector3(.5f,.42f,.1f),Wet);
            for(int i=0;i<5;i++)Part("Spine spike",PrimitiveType.Cube,torso,new Vector3(0,.15f+i*.18f,-.25f),new Vector3(.06f,.12f,.14f),Bone);
            head=Pivot("Head",torso,new Vector3(0,1.15f,.12f));
            Part("Skull",PrimitiveType.Sphere,head,Vector3.zero,new Vector3(.34f,.44f,.37f),Skin);
            jaw=Pivot("Jaw",head,new Vector3(0,-.13f,.04f));
            Part("Jawbone",PrimitiveType.Cube,jaw,new Vector3(0,-.04f,.1f),new Vector3(.2f,.07f,.26f),Wet);
            for(int t=0;t<5;t++)Part("Tooth",PrimitiveType.Cube,jaw,new Vector3(-.08f+t*.04f,.02f,.2f),new Vector3(.02f,.07f,.02f),Bone);
            foreach(float x in new[]{-.09f,.09f})Part("Eye",PrimitiveType.Sphere,head,new Vector3(x,.07f,.16f),Vector3.one*.075f,new Color(1,.5f,.12f),true);
            foreach(float side in new[]{-1f,1f}) {
                var shoulder=Pivot("Shoulder",torso,new Vector3(side*.42f,.95f,0));Part("Upper arm",PrimitiveType.Cube,shoulder,new Vector3(0,-.4f,0),new Vector3(.12f,.8f,.12f),Skin);
                var fore=Pivot("Forearm",shoulder,new Vector3(0,-.8f,0));Part("Forearm",PrimitiveType.Cube,fore,new Vector3(0,-.42f,0),new Vector3(.1f,.85f,.1f),Wet);
                for(int c=0;c<3;c++)Part("Claw",PrimitiveType.Cube,fore,new Vector3((c-1)*.05f,-.95f,.02f),new Vector3(.02f,.3f,.02f),Bone);
                if(side<0){shoulderL=shoulder;foreL=fore;}else{shoulderR=shoulder;foreR=fore;}
                var leg=Pivot("Leg",hip,new Vector3(side*.17f,0,0));Part("Thigh",PrimitiveType.Cube,leg,new Vector3(0,-.28f,0),new Vector3(.16f,.56f,.16f),Skin);
                var shin=Pivot("Shin",leg,new Vector3(0,-.56f,0));Part("Shin",PrimitiveType.Cube,shin,new Vector3(0,-.28f,0),new Vector3(.13f,.56f,.13f),Wet);Part("Foot",PrimitiveType.Cube,shin,new Vector3(0,-.55f,.08f),new Vector3(.14f,.06f,.28f),Wet);
                if(side<0){legL=leg;shinL=shin;}else{legR=leg;shinR=shin;}
            }
            var lamp=new GameObject("Eye glow");lamp.transform.SetParent(head,false);lamp.transform.localPosition=new Vector3(0,.05f,.3f);
            glow=lamp.AddComponent<Light>();glow.type=LightType.Point;glow.color=new Color(1,.45f,.12f);glow.range=5.5f;glow.intensity=baseGlow=1.5f;glow.shadows=LightShadows.None;
            voice=Sfx.Loop(head,Snd.StalkerBreath,22);
        }
        void BuildHunter()
        {
            root=Pivot("Drowned hunter",transform,Vector3.zero);seg=new Transform[7];float[] size={.9f,.8f,.68f,.56f,.44f,.32f,.2f};
            for(int i=0;i<seg.Length;i++) {
                seg[i]=Pivot("Segment "+i,root,new Vector3(0,0,-i*.55f));Part("Body",PrimitiveType.Sphere,seg[i],Vector3.zero,new Vector3(size[i]*.9f,size[i]*.85f,.78f),i%2==0?Skin:Belly);
                if(i<5)Part("Dorsal fin",PrimitiveType.Cube,seg[i],new Vector3(0,size[i]*.48f,0),new Vector3(.04f,.26f,.38f),Belly);
            }
            jawUp=Pivot("Upper jaw",seg[0],new Vector3(0,.05f,.3f));Part("Upper",PrimitiveType.Cube,jawUp,new Vector3(0,.05f,.36f),new Vector3(.6f,.12f,.72f),Skin);
            jawLow=Pivot("Lower jaw",seg[0],new Vector3(0,-.12f,.25f));Part("Lower",PrimitiveType.Cube,jawLow,new Vector3(0,-.05f,.36f),new Vector3(.55f,.1f,.7f),Belly);
            for(int t=0;t<6;t++) {Part("Tooth",PrimitiveType.Cube,jawUp,new Vector3(-.25f+t*.1f,-.06f,.68f),new Vector3(.03f,.16f,.03f),Bone);Part("Tooth",PrimitiveType.Cube,jawLow,new Vector3(-.23f+t*.092f,.04f,.66f),new Vector3(.03f,.14f,.03f),Bone);}
            foreach(float x in new[]{-.32f,.32f})Part("Eye",PrimitiveType.Sphere,seg[0],new Vector3(x,.26f,.2f),Vector3.one*.11f,new Color(.45f,1,.7f),true);
            lure=Pivot("Lure",seg[0],new Vector3(0,.4f,.1f));Part("Stalk",PrimitiveType.Cube,lure,new Vector3(0,.2f,.45f),new Vector3(.03f,.03f,.95f),Belly);
            Part("Bulb",PrimitiveType.Sphere,lure,new Vector3(0,.2f,.95f),Vector3.one*.17f,new Color(.5f,1,.8f),true);
            var lamp=new GameObject("Lure glow");lamp.transform.SetParent(lure,false);lamp.transform.localPosition=new Vector3(0,.2f,.95f);
            glow=lamp.AddComponent<Light>();glow.type=LightType.Point;glow.color=new Color(.4f,1,.75f);glow.range=9;glow.intensity=baseGlow=2.6f;glow.shadows=LightShadows.None;
            tailFin=Pivot("Tail fin",seg[6],new Vector3(0,0,-.15f));Part("Fin",PrimitiveType.Cube,tailFin,new Vector3(0,0,-.25f),new Vector3(.75f,.05f,.45f),Belly);
            foreach(float side in new[]{-1f,1f}) {var fin=Pivot("Side fin",seg[1],new Vector3(side*.4f,-.1f,0));Part("Fin",PrimitiveType.Cube,fin,new Vector3(side*.25f,0,0),new Vector3(.5f,.04f,.35f),Belly);if(side<0)finL=fin;else finR=fin;}
            voice=Sfx.Loop(seg[0],Snd.DrownedGurgle,24);
        }
        void Update()
        {
            if(!built||brain==null||!brain.IsSpawned)return;
            float dt=Time.deltaTime;if(dt<=0)return;clock+=dt;
            var pos=transform.position;speed=Mathf.Lerp(speed,(pos-last).magnitude/dt,Mathf.Clamp01(dt*8));if((pos-last).sqrMagnitude>25)speed=0;last=pos;
            state=brain.CurrentState;if(state!=previous) {OnState(previous,state);previous=state;}
            if(clips!=null)AnimateModel();else if(water)AnimateHunter(dt);else AnimateStalker(dt);
            bool hunting=state==EnemyState.Chase||state==EnemyState.WindUp||state==EnemyState.Attack||state==EnemyState.Grab||state==EnemyState.Scream;
            glow.intensity=baseGlow*(state==EnemyState.WindUp||state==EnemyState.Scream?1.7f:hunting?1.25f:1f)*(1+.12f*Mathf.Sin(clock*21)+.08f*Mathf.Sin(clock*47));
            voice.volume=Sfx.Volume(Bus.Sfx)*(hunting?.9f:.5f);voice.pitch=hunting?1.15f:1f;
            // Footsteps and idle voice.
            if(!water&&speed>.4f) {stepDistance+=speed*dt;if(stepDistance>1.5f) {stepDistance=0;Sfx.At(Snd.StalkerStep,transform.position,Mathf.Clamp01(speed/3.5f),Random.Range(.9f,1.1f),30);}}
            if(water&&Time.time>pingAt&&(state==EnemyState.Patrol||state==EnemyState.Search)) {pingAt=Time.time+Random.Range(6f,11f);Sfx.At(Snd.DrownedPing,transform.position,.7f,Random.Range(.9f,1.1f),45);}
            if(!water&&Time.time>mutterAt&&(state==EnemyState.Patrol||state==EnemyState.Search)) {mutterAt=Time.time+Random.Range(9f,18f);Sfx.At(Snd.StalkerGrowl,transform.position+Vector3.up*1.7f,.45f,Random.Range(.9f,1.2f),32);}
        }
        void OnState(EnemyState from,EnemyState to)
        {
            var at=transform.position+Vector3.up*1.6f;bool wasHunting=from==EnemyState.WindUp||from==EnemyState.Attack||from==EnemyState.Recover||from==EnemyState.Grab||from==EnemyState.Scream;
            if(clips!=null&&to==EnemyState.WindUp)clips.Once(water?"bite":"attack");
            if(clips!=null&&to==EnemyState.Grab)clips.Once("grab");
            if(clips!=null&&to==EnemyState.Scream&&clips.Has("scream"))clips.Once("scream");
            switch(to) {
                case EnemyState.Scream: {
                    Sfx.At(water?Snd.DrownedScream:Snd.StalkerScream,at,1,Random.Range(.94f,1.04f),70);
                    // The diver it has seen also gets a sting in their own ears.
                    var local=NetPlayer.Local;
                    if(local!=null&&local.Alive.Value&&brain.Target.Value==local.OwnerClientId&&Vector3.Distance(local.transform.position,transform.position)<32)Sfx.Ui(Snd.Stinger,.5f);
                    break;
                }
                case EnemyState.Chase: if(!wasHunting)Sfx.At(water?Snd.DrownedLunge:Snd.StalkerRoar,at,.9f,Random.Range(.92f,1.05f),55);break;
                case EnemyState.WindUp: Sfx.At(water?Snd.DrownedLunge:Snd.StalkerRoar,at,1,Random.Range(.9f,1.05f),45);break;
                case EnemyState.Grab: Sfx.At(Snd.StalkerRoar,at,1,Random.Range(.8f,.9f),45);break;
                case EnemyState.Attack: Sfx.At(Snd.Thunk,at,.7f,.75f,28);break;
                case EnemyState.Investigate: Sfx.At(water?Snd.DrownedPing:Snd.StalkerGrowl,at,.8f,1,40);break;
                case EnemyState.Search: Sfx.At(Snd.StalkerGrowl,at,.55f,.9f,32);break;
                case EnemyState.Distracted: Sfx.At(Snd.StalkerGrowl,at,.7f,.8f,36);break;
                case EnemyState.Retreat: Sfx.At(Snd.StalkerGrowl,at,.6f,.7f,32);break;
            }
        }
        void AnimateStalker(float dt)
        {
            bool windUp=state==EnemyState.WindUp||state==EnemyState.Scream,attack=state==EnemyState.Attack,recover=state==EnemyState.Recover,chase=state==EnemyState.Chase;
            bool scanning=state==EnemyState.Patrol||state==EnemyState.Search||state==EnemyState.Investigate||state==EnemyState.Distracted;
            phase+=speed*dt*1.35f;float stride=Mathf.Clamp(speed*11f,0,48);
            Damp(ref lean,windUp?-9:attack?44:recover?40:chase?30:12,windUp?10:attack?22:5,dt);
            Damp(ref armRaise,windUp?-156:attack?-82:recover?8:chase?-58:0,windUp?9:attack?26:6,dt);
            Damp(ref armBend,windUp?-30:attack?-10:chase?-30:-8,8,dt);
            Damp(ref headYaw,scanning?Mathf.Sin(clock*.85f)*(state==EnemyState.Search?55:32):0,4,dt);
            Damp(ref headPitch,windUp?-28:recover?24:chase?12:0,6,dt);
            Damp(ref jawOpen,windUp?62:attack?46:chase?24+Mathf.Sin(clock*15)*8:5+Mathf.Sin(clock*2)*2,10,dt);
            Damp(ref shake,windUp?1:0,10,dt);
            float swing=Mathf.Sin(phase);
            hip.localPosition=new Vector3(Mathf.Sin(clock*47)*.02f*shake,1.05f+Mathf.Abs(swing)*.07f*Mathf.Clamp01(speed/2)-(recover?.12f:0),0);
            torso.localRotation=Quaternion.Euler(lean+Mathf.Sin(clock*1.1f)*2,swing*6*Mathf.Clamp01(speed/2)+Mathf.Sin(clock*53)*4*shake,0);
            head.localRotation=Quaternion.Euler(headPitch,headYaw,0);jaw.localRotation=Quaternion.Euler(jawOpen,0,0);
            float armSwing=armRaise<-40?0:swing*22*Mathf.Clamp01(speed/2);
            shoulderL.localRotation=Quaternion.Euler(armRaise-armSwing+Mathf.Sin(clock*47)*7*shake,0,6);shoulderR.localRotation=Quaternion.Euler(armRaise+armSwing+Mathf.Cos(clock*43)*7*shake,0,-6);
            foreL.localRotation=Quaternion.Euler(armBend,0,0);foreR.localRotation=Quaternion.Euler(armBend,0,0);
            legL.localRotation=Quaternion.Euler(swing*stride,0,0);legR.localRotation=Quaternion.Euler(-swing*stride,0,0);
            shinL.localRotation=Quaternion.Euler(Mathf.Max(0,Mathf.Sin(phase+1.2f))*stride*1.1f,0,0);shinR.localRotation=Quaternion.Euler(Mathf.Max(0,Mathf.Sin(phase+1.2f+Mathf.PI))*stride*1.1f,0,0);
        }
        void AnimateHunter(float dt)
        {
            bool windUp=state==EnemyState.WindUp||state==EnemyState.Scream,attack=state==EnemyState.Attack,chase=state==EnemyState.Chase;
            float amplitude=.14f+.12f*Mathf.Clamp01(speed/3.5f),rate=2.4f+speed*1.7f;phase+=rate*dt;
            for(int i=0;i<seg.Length;i++) {
                float lag=phase-i*.75f;seg[i].localPosition=new Vector3(Mathf.Sin(lag)*amplitude*(.35f+i*.25f),0,-i*.55f);
                seg[i].localRotation=Quaternion.Euler(0,Mathf.Cos(lag)*20f*(.4f+i*.13f)*(.5f+Mathf.Clamp01(speed/3)),0);
            }
            root.localPosition=new Vector3(0,Mathf.Sin(clock*1.3f)*.08f,0);
            Damp(ref jawOpen,windUp?52:attack?64:chase?24+Mathf.Sin(clock*9)*6:7+Mathf.Sin(clock*2.2f)*3,windUp?9:attack?24:8,dt);
            jawLow.localRotation=Quaternion.Euler(jawOpen,0,0);jawUp.localRotation=Quaternion.Euler(-jawOpen*.35f,0,0);
            lure.localRotation=Quaternion.Euler(Mathf.Sin(clock*1.7f)*10-(chase?8:0),Mathf.Sin(clock*1.1f)*12,0);
            tailFin.localRotation=Quaternion.Euler(0,Mathf.Sin(phase-seg.Length*.75f)*30,0);
            finL.localRotation=Quaternion.Euler(0,0,Mathf.Sin(clock*3)*18);finR.localRotation=Quaternion.Euler(0,0,-Mathf.Sin(clock*3)*18);
        }
    }
}
