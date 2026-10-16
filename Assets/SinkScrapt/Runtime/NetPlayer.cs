using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SinkScrapt
{
    public class NetPlayer : NetworkBehaviour
    {
        public static readonly List<NetPlayer> All=new();
        public static NetPlayer Local=>All.Find(p=>p.IsOwner);
        public readonly NetworkVariable<bool> Alive=new(true);
        public readonly NetworkVariable<float> Health=new(100);
        public readonly NetworkVariable<float> Oxygen=new(35);
        public readonly NetworkVariable<PlayerZone> Zone=new(PlayerZone.Boat);
        public readonly NetworkVariable<FixedString4096Bytes> InventoryJson=new();
        public readonly NetworkVariable<FixedString64Bytes> Name=new("Penyelam");
        public readonly NetworkVariable<float> Pitch=new(0,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<bool> Crouched=new(false,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Owner);
        // Which crew character this diver is (0..3, CrewModels), handed out by the host so every peer shows the same body.
        public readonly NetworkVariable<byte> Character=new(255);
        // Owner-reported movement mode for the third-person animation: 0 on foot, 1 swimming, 2 climbing, 3 held by a monster.
        public readonly NetworkVariable<byte> Motion=new(0,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Owner);
        public static readonly string[] CrewModels={"char_crew_parka","char_crew_medic","char_crew_engineer","char_crew_fisher"};
        public InventoryData Inventory=new();
        public string Profile="";
        public Camera eyes;
        public CharacterController controller;
        public float stamina=100;
        public bool climbing;
        public WorldInteractable Focus;
        public bool OnBoat=>BoatLayout.OnDeck(transform.position);
        public bool HasHeavy=>Inventory.items.Any(i=>i.Definition.twoHands);
        public float Weight=>Inventory.All.Sum(i=>i.Definition.weight);
        public bool HasRadio=>Inventory.All.Any(i=>i.Definition.kind==ItemKind.Radio)&&Alive.Value;
        // A radio on the belt can be keyed with both hands full.
        public bool RadioOnBelt=>Inventory.HasUtility&&Inventory.utility.Definition.kind==ItemKind.Radio;
        public ItemInstance Selected=>Inventory.selected>=0&&Inventory.selected<Inventory.items.Count?Inventory.items[Inventory.selected]:null;
        public bool Underwater=>transform.position.y+HeadHeight<WaterHeight;
        public float WaterHeight=>Zone.Value==PlayerZone.Interior?NetSession.Instance?.FloodLevel??0:0;
        public float HeadHeight=>Crouched.Value?1.05f:1.65f;
        // Water above chest height lifts the diver into swimming; below that they wade (waist-deep bilge on the lowest deck).
        public const float SwimDepth=1.15f;
        public bool Swimming=>transform.position.y+SwimDepth<WaterHeight;
        public bool Wading=>!Swimming&&transform.position.y+.25f<WaterHeight;
        float vertical,pitch,yaw,lastDepth,climbProgress,noiseAt,batteryPublish;
        bool speedFov;
        // Owner-side swim state with hysteresis: it starts when the water is above chest height and only ends once the diver
        // stands in water clearly below it, so a depth right at the threshold does not flip between swimming and walking.
        bool swimState;
        public const float SwimEnter=1.25f,SwimExit=1.0f,FloatDepth=1.35f;
        // Pulling up onto a ledge (out of the water, or a jump onto a low obstacle).
        bool mantling;
        float mantleTime;
        Vector3 mantleFrom,mantleTo;
        public bool Mantling=>mantling;
        // Camera shake in 0..1, raised by RetroLook when the local diver is hurt.
        public float shake;
        int climbKind,spectateIndex;
        Transform avatar,hand,handBone;
        GameObject held;
        string heldId="";
        Light flashlight,spill,bounce;
        // Torch shape: the hotspot's cone in degrees and the wide spill's share of the torch intensity (balance.flashlightAngle
        // is the spill's cone).
        const float TorchHotspot=40,TorchSpill=.3f;
        Vector3 lastServerPosition;
        int builtCharacter=-1;
        // Held by a hull stalker (owner side): follow its hands until it throws. `knock` is a shove that decays on the ground.
        NetEnemy grabbedBy;
        float grabbedAt,grabbedUntil;
        Vector3 knock;
        public bool Grabbed=>grabbedBy!=null;
        public override void OnNetworkSpawn()
        {
            All.Add(this);controller=GetComponent<CharacterController>();controller.enabled=IsOwner;
            foreach(var item in FindObjectsByType<NetItem>(FindObjectsInactive.Exclude))Physics.IgnoreCollision(controller,item.GetComponent<BoxCollider>());
            if(IsServer) {
                // The lowest character not worn by anyone already aboard.
                var taken=All.Where(p=>p!=this).Select(p=>(int)p.Character.Value).ToHashSet();
                int pick=0;while(taken.Contains(pick)&&pick<CrewModels.Length-1)pick++;Character.Value=(byte)pick;
            }
            gameObject.AddComponent<PlayerFeel>();InventoryJson.OnValueChanged+=InventoryChanged;
            if(InventoryJson.Value.Length>0)InventoryChanged(default,InventoryJson.Value);
            if(IsOwner) {
                var go=new GameObject("Player Camera");go.transform.SetParent(transform,false);go.transform.localPosition=Vector3.up*1.65f;
                eyes=go.AddComponent<Camera>();eyes.nearClipPlane=.07f;eyes.farClipPlane=400;eyes.fieldOfView=75;eyes.backgroundColor=WorldFactory.OutdoorFog;eyes.clearFlags=CameraClearFlags.Skybox;
                go.AddComponent<AudioListener>();Fx.Dust(go.transform);
                hand=new GameObject("Held item").transform;hand.SetParent(go.transform,false);hand.localPosition=new Vector3(.33f,-.3f,.65f);
                var menu=GameObject.Find("Menu Camera");if(menu!=null){menu.GetComponent<Camera>().enabled=false;var listener=menu.GetComponent<AudioListener>();if(listener!=null)listener.enabled=false;}
                StartCoroutine(RegisterWhenReady());
                SinkApp.Instance.SetMenuBackdrop(false);
            } else {hand=new GameObject("Hand").transform;hand.SetParent(transform,false);hand.localPosition=new Vector3(.35f,1.05f,.45f);}
            // The light starts at the held torch. What you hold casts no shadow (UpdateHeld), so the torch never throws its own
            // shadow into the view. No light shaft is drawn: a narrow bright hotspot and a wide dim spill light the room itself,
            // and your own torch adds a faint bounce light where the beam lands (UpdateHeld). (A light cookie gave the same look
            // but URP shaded some surfaces outside the cone solid cyan with it.)
            var balance=SinkApp.Instance.balance;
            var lamp=new GameObject("Torch light");lamp.transform.SetParent(IsOwner?eyes.transform:transform,false);lamp.transform.localPosition=IsOwner?new Vector3(.28f,-.22f,.45f):new Vector3(.35f,1.1f,.6f);
            flashlight=lamp.AddComponent<Light>();flashlight.type=LightType.Spot;flashlight.spotAngle=TorchHotspot;flashlight.innerSpotAngle=TorchHotspot*.45f;
            flashlight.range=balance.flashlightRange;flashlight.intensity=balance.flashlightIntensity;flashlight.color=new Color(1,.94f,.84f);flashlight.shadows=IsOwner?LightShadows.Soft:LightShadows.None;
            spill=new GameObject("Torch spill").AddComponent<Light>();spill.transform.SetParent(lamp.transform,false);spill.type=LightType.Spot;
            spill.spotAngle=balance.flashlightAngle;spill.innerSpotAngle=balance.flashlightAngle*.6f;spill.range=balance.flashlightRange*.7f;spill.intensity=balance.flashlightIntensity*TorchSpill;spill.color=new Color(1,.92f,.82f);spill.shadows=LightShadows.None;
            if(IsOwner) {
                var fill=new GameObject("Torch bounce");fill.transform.SetParent(transform,false);
                bounce=fill.AddComponent<Light>();bounce.type=LightType.Point;bounce.shadows=LightShadows.None;bounce.color=new Color(1,.9f,.78f);bounce.range=6;bounce.intensity=0;
            }
            if(IsServer) {Alive.Value=NetSession.Instance==null||!NetSession.Instance.InExpedition;lastServerPosition=transform.position;}
        }
        IEnumerator RegisterWhenReady()
        {
            while(NetSession.Instance==null)yield return null;
            RegisterRpc(SinkApp.Instance.profile,SinkApp.Instance.playerName);
        }
        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Owner)]void RegisterRpc(string profile,string nickname)
        {
            if(!string.IsNullOrEmpty(Profile))return;
            Profile=(profile??"").Length>64?profile[..64]:profile;
            string clean=new string((nickname??"Penyelam").Where(c=>!char.IsControl(c)).Take(20).ToArray());Name.Value=new FixedString64Bytes(clean);
            NetSession.Instance.RestoreLoadout(this);
            if(NetSession.Instance.InExpedition)Alive.Value=false;
            else NetSession.Instance.Activate(this);
        }
        // The crew member's body: the Blender character for their slot (Models/Characters, animated by CrewAnimator), or the old
        // primitive diver when the model is missing. Your own body only casts a shadow, so it never blocks the first-person view.
        void BuildAvatar()
        {
            if(avatar!=null)Destroy(avatar.gameObject);handBone=null;
            int index=Character.Value<CrewModels.Length?Character.Value:(int)(OwnerClientId%4);builtCharacter=Character.Value;
            var model=ModelLibrary.Spawn("Characters/"+CrewModels[index],transform,Vector3.zero);
            if(model!=null) {
                avatar=model.transform;avatar.name="Crew body";
                var crew=model.AddComponent<CrewAnimator>();crew.player=this;
                handBone=ModelLibrary.FindBone(model.transform,"Hand.R");
                if(IsOwner)foreach(var r in model.GetComponentsInChildren<Renderer>())r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                return;
            }
            avatar=new GameObject("Diver").transform;avatar.SetParent(transform,false);
            var color=(index%4) switch {0=>new Color(.7f,.42f,.17f),1=>new Color(.17f,.47f,.48f),2=>new Color(.55f,.32f,.28f),_=>new Color(.47f,.51f,.32f)};
            WorldFactory.Shape("Suit",PrimitiveType.Capsule,avatar,new Vector3(0,.95f,0),new Vector3(.55f,.45f,.4f),color,false);
            var head=new GameObject("Head").transform;head.SetParent(avatar,false);head.localPosition=new Vector3(0,1.65f,0);
            WorldFactory.Shape("Helmet",PrimitiveType.Sphere,head,Vector3.zero,Vector3.one*.42f,WorldFactory.Steel,false);
            WorldFactory.Box("Visor",head,new Vector3(0,.02f,.21f),new Vector3(.32f,.15f,.05f),WorldFactory.Teal,false);
            var legs=new Transform[2];
            for(int i=0;i<2;i++) {var leg=new GameObject("Leg").transform;leg.SetParent(avatar,false);leg.localPosition=new Vector3(i==0?-.15f:.15f,.5f,0);WorldFactory.Box("Boot",leg,new Vector3(0,-.25f,0),new Vector3(.2f,.5f,.25f),WorldFactory.Hull,false);legs[i]=leg;}
            WorldFactory.Shape("Dive pack",PrimitiveType.Capsule,avatar,new Vector3(0,1,-.25f),new Vector3(.3f,.4f,.22f),WorldFactory.Cream,false);
            var animator=avatar.gameObject.AddComponent<AvatarAnimator>();animator.legs=legs;animator.head=head;animator.player=this;
            if(IsOwner)foreach(var r in avatar.GetComponentsInChildren<Renderer>())r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }
        public override void OnNetworkDespawn() {if(IsServer&&NetworkManager.IsListening&&!NetworkManager.ShutdownInProgress&&NetSession.Instance!=null)NetSession.Instance.OnCrewDisconnected(OwnerClientId);All.Remove(this);InventoryJson.OnValueChanged-=InventoryChanged;if(IsOwner){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;} }
        void InventoryChanged(FixedString4096Bytes previous,FixedString4096Bytes next) {if(!IsServer&&next.Length>0) {Inventory=JsonUtility.FromJson<InventoryData>(next.ToString());Inventory.Normalize();}}
        public void PublishInventory()
        {
            if(!IsServer||!IsSpawned)return;Inventory.Normalize();Inventory.selected=Mathf.Clamp(Inventory.selected,0,Mathf.Max(0,Inventory.items.Count-1));InventoryJson.Value=new FixedString4096Bytes(JsonUtility.ToJson(Inventory));
        }
        void Update()
        {
            if(!IsSpawned)return;
            if(builtCharacter!=Character.Value||avatar==null)BuildAvatar();
            UpdateHeld();
            if(avatar!=null)avatar.gameObject.SetActive(Alive.Value);
            if(IsServer) {
                if(transform.position.x>500)Zone.Value=PlayerZone.Interior;
                else if(OnBoat)Zone.Value=PlayerZone.Boat;
                else if(WorldZone.Island!=null&&transform.position.x>39)Zone.Value=PlayerZone.Island;
                else if(WorldZone.Exterior!=null)Zone.Value=PlayerZone.Exterior;
            }
            if(!IsOwner||eyes==null)return;
            var session=NetSession.Instance;var keyboard=Keyboard.current;var mouse=Mouse.current;
            if(session==null||keyboard==null||mouse==null)return;
            eyes.fieldOfView=Mathf.Lerp(eyes.fieldOfView,GameHUD.Fov+(speedFov?5:0),Mathf.Clamp01(Time.deltaTime*8));
            bool modal=GameHUD.Instance!=null&&GameHUD.Instance.Modal;
            bool playable=Alive.Value&&!modal&&session.Phase.Value!=GamePhase.Loading&&!Cinematic.Playing;
            Cursor.lockState=playable||Cinematic.Playing?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!playable&&!Cinematic.Playing;
            if(!Alive.Value) {Spectate(keyboard);Focus=null;ReleaseGrab();return;}
            if(!playable){Focus=null;if(Grabbed)StepGrabbed();return;}
            var delta=mouse.delta.ReadValue()*GameHUD.Sensitivity*.07f;pitch=Mathf.Clamp(pitch-delta.y,-85,85);yaw+=delta.x;Pitch.Value=pitch;transform.rotation=Quaternion.Euler(0,yaw,0);eyes.transform.localRotation=Quaternion.Euler(pitch,0,0);
            // The torch switch and the belt slot work in every state (climbing, swimming, both hands full).
            if(keyboard.fKey.wasPressedThisFrame)Act(ActionKind.ToggleLight);
            if(keyboard.digit5Key.wasPressedThisFrame)Act(ActionKind.Utility);
            byte motionMode=(byte)(Grabbed?3:climbing?2:swimState?1:0);if(Motion.Value!=motionMode)Motion.Value=motionMode;
            if(Grabbed) {StepGrabbed();Focus=null;return;}
            if(climbing) {StepClimb((keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0),Mathf.Min(Time.deltaTime,.05f),keyboard.spaceKey.wasPressedThisFrame);Focus=null;return;}
            if(mantling) {StepMantle(Mathf.Min(Time.deltaTime,.05f));Focus=null;return;}
            Crouched.Value=keyboard.leftCtrlKey.isPressed&&!Underwater&&!swimState;controller.height=Crouched.Value?1.2f:1.8f;controller.center=Vector3.up*(controller.height/2);
            shake=Mathf.MoveTowards(shake,0,Time.deltaTime*1.6f);eyes.transform.localPosition=Vector3.up*HeadHeight+Random.insideUnitSphere*shake*.07f;
            float h=(keyboard.dKey.isPressed?1:0)-(keyboard.aKey.isPressed?1:0);float v=(keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0);
            var input=Vector3.ClampMagnitude(transform.right*h+transform.forward*v,1);float dt=Mathf.Min(Time.deltaTime,.05f);
            var balance=SinkApp.Instance.balance;
            float water=WaterHeight,wet=water-transform.position.y;
            if(!swimState&&wet>SwimEnter)swimState=true;
            else if(swimState&&(wet<.6f||(wet<SwimExit&&controller.isGrounded)))swimState=false;
            bool swim=swimState,wading=!swim&&wet>.25f;
            bool sprint=keyboard.leftShiftKey.isPressed&&input.sqrMagnitude>.1f&&!swim&&!Crouched.Value&&stamina>0;
            speedFov=sprint;stamina=Mathf.Clamp(stamina+(sprint?-14:18)*dt,0,100);
            float speed=(swim?balance.swimSpeed:Crouched.Value?2.2f:sprint?balance.sprintSpeed:balance.walkSpeed)*(1-Mathf.Min(.4f,Weight/50))*(wading?balance.wadeSpeedFactor:1);
            // A low ledge in front: climb out of the water onto it by swimming at it, or vault a low obstacle with Space.
            bool jump=keyboard.spaceKey.wasPressedThisFrame||(swim&&keyboard.spaceKey.isPressed);
            if((swim||jump)&&TryMantle(input,jump,swim)) {Focus=null;return;}
            if(swim) {
                // Float with the head just out of the water; underwater the diver drifts up slowly unless Space or Ctrl is held.
                float floatAt=water-FloatDepth,y=transform.position.y;float goal;
                if(keyboard.spaceKey.isPressed)goal=y<floatAt+.15f?2.2f:0;
                else if(keyboard.leftCtrlKey.isPressed)goal=-2.2f;
                else goal=Mathf.Clamp((floatAt-y)*2.5f,-1.2f,y<floatAt-.6f?.25f:1.2f);
                vertical=Mathf.MoveTowards(vertical,goal,dt*9);
            }
            else {if(controller.isGrounded&&vertical<0)vertical=-2;if(controller.isGrounded&&keyboard.spaceKey.wasPressedThisFrame)vertical=5.5f;vertical-=18*dt;}
            float depth=session.Depth;var motion=input*speed*dt+Vector3.up*vertical*dt+knock*dt;
            // A shove (a strike or a throw) slides on and dies away quickly once the diver lands.
            knock=Vector3.MoveTowards(knock,Vector3.zero,(controller.isGrounded||swim?12:2.5f)*dt);
            if(Zone.Value==PlayerZone.Exterior&&controller.isGrounded)motion+=Vector3.down*(depth-lastDepth);lastDepth=depth;
            controller.Move(motion);
            if(transform.position.y<-30)TeleportLocal(WorldZone.BoatSpawn);
            // Safety net: the interior hull is sealed, but anyone who still ends up below the lowest deck returns to access A.
            if(Zone.Value==PlayerZone.Interior&&transform.position.y<ShipTimeline.FloorHeight(0)-1.5f)TeleportLocal(WorldZone.InteriorEntry(0));
            if(keyboard.gKey.wasPressedThisFrame)Act(ActionKind.Drop);
            for(int n=0;n<4;n++)if(keyboard[(Key)((int)Key.Digit1+n)].wasPressedThisFrame)Act(ActionKind.SelectSlot,n);
            if(mouse.leftButton.wasPressedThisFrame) {
                var kind=Selected?.Definition.kind;
                if(kind==ItemKind.Flare)Act(ActionKind.UseFlare);else if(kind==ItemKind.Battery)Act(ActionKind.Reload);else if(kind==ItemKind.Flashlight)Act(ActionKind.ToggleLight);
            }
            Focus=FindFocus(balance.reach);
            if(Focus!=null&&keyboard.eKey.wasPressedThisFrame)Interact(Focus);
            float mood=Mathf.Clamp01(Time.deltaTime*4);bool inside=Zone.Value==PlayerZone.Interior;
            RenderSettings.fogDensity=Mathf.Lerp(RenderSettings.fogDensity,Underwater?.08f:inside?.056f:WorldFactory.OutdoorFogDensity,mood);
            RenderSettings.fogColor=Color.Lerp(RenderSettings.fogColor,Underwater?WorldFactory.UnderwaterFog:inside?WorldFactory.InteriorFog:WorldFactory.OutdoorFog,mood);
            // Inside the wreck there is next to no ambient light: lamps, hazards and torches are what lights it.
            RenderSettings.ambientIntensity=Mathf.Lerp(RenderSettings.ambientIntensity,inside?.012f:WorldFactory.OutdoorAmbient,mood*.75f);
            // Inside the wreck the world ends at the bulkheads; show a dark void there rather than the sky.
            eyes.backgroundColor=RenderSettings.fogColor;eyes.clearFlags=inside?CameraClearFlags.SolidColor:CameraClearFlags.Skybox;
        }
        static readonly Collider[] overlap=new Collider[12];
        // True when a standing diver would fit at `feet` (only level geometry counts: not this diver, loot or triggers).
        bool Fits(Vector3 feet,float height)
        {
            float r=controller.radius*.95f;
            int count=Physics.OverlapCapsuleNonAlloc(feet+Vector3.up*(r+.06f),feet+Vector3.up*(height-r),r,overlap,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)if(overlap[i]!=controller&&overlap[i].attachedRigidbody==null&&overlap[i].GetComponentInParent<NetPlayer>()==null)return false;
            return true;
        }
        // Looks for a ledge just ahead whose top is within reach: up to 2 m above the feet when swimming (about 0.65 m above the
        // waterline), up to 1 m when jumping on land (the boat rail is 1.15 m, so it still keeps the crew aboard).
        public bool TryMantle(Vector3 wish,bool jump,bool inWater)
        {
            if(HasHeavy&&!inWater)return false;
            var fwd=wish.sqrMagnitude>.1f?wish:(jump?transform.forward:Vector3.zero);fwd.y=0;if(fwd.sqrMagnitude<.01f)return false;fwd.Normalize();
            float maxRise=inWater?2f:1f;var feet=transform.position;
            bool blocked=false;RaycastHit wall=default;
            foreach(float h in new[]{.3f,.9f,1.4f}) {
                if(h>maxRise)break;
                if(Physics.Raycast(feet+Vector3.up*h,fwd,out wall,controller.radius+.45f,~0,QueryTriggerInteraction.Ignore)&&wall.collider.attachedRigidbody==null&&Vector3.Dot(wall.normal,-fwd)>.5f) {blocked=true;break;}
            }
            if(!blocked)return false;
            var probe=new Vector3(wall.point.x,feet.y+maxRise+.3f,wall.point.z)+fwd*.4f;
            if(!Physics.Raycast(probe,Vector3.down,out var top,maxRise+.05f,~0,QueryTriggerInteraction.Ignore)||top.normal.y<.75f||top.collider.attachedRigidbody!=null)return false;
            float rise=top.point.y-feet.y;if(rise<.3f||rise>maxRise)return false;
            var target=top.point+Vector3.up*.04f;
            if(!Fits(target,1.8f)||!Fits(new Vector3(feet.x,target.y,feet.z),1.8f))return false;
            mantling=true;mantleTime=0;mantleFrom=feet;mantleTo=target;controller.enabled=false;vertical=0;swimState=false;
            return true;
        }
        // Up the face first, then over the edge, about 0.45 s in all.
        void StepMantle(float dt)
        {
            mantleTime+=dt/.45f;float t=Mathf.Clamp01(mantleTime);
            var up=new Vector3(mantleFrom.x,mantleTo.y,mantleFrom.z);
            transform.position=t<.6f?Vector3.Lerp(mantleFrom,up,Mathf.SmoothStep(0,1,t/.6f)):Vector3.Lerp(up,mantleTo,Mathf.SmoothStep(0,1,(t-.6f)/.4f));
            if(t>=1) {mantling=false;controller.enabled=true;vertical=0;}
        }
        // What the diver is aiming at within `reach`: a thick ray first, then the nearest loose item close to the line of sight
        // (small items on the floor, or below a swimmer) that nothing solid hides.
        WorldInteractable FindFocus(float reach)
        {
            var origin=eyes.transform.position;var dir=eyes.transform.forward;
            if(Physics.SphereCast(origin,.1f,dir,out var hit,reach,~0,QueryTriggerInteraction.Ignore)) {var target=hit.collider.GetComponentInParent<WorldInteractable>();if(target!=null)return target;}
            WorldInteractable best=null;float bestScore=float.MaxValue;
            foreach(var item in NetItem.Live) {
                if(item==null||item.Holder.Value!=ulong.MaxValue||item.Item==null)continue;
                var to=item.transform.position-origin;float distance=to.magnitude;if(distance>reach||distance<.05f)continue;
                float angle=Vector3.Angle(dir,to);if(angle>16)continue;
                if(Physics.Linecast(origin,item.transform.position,out var block,~0,QueryTriggerInteraction.Ignore)&&block.collider.GetComponentInParent<NetItem>()!=item)continue;
                float score=angle+distance*3;if(score<bestScore){bestScore=score;best=item.GetComponent<WorldInteractable>();}
            }
            return best;
        }
        void UpdateHeld()
        {
            var item=Selected;var id=item?.uid??"";
            if(id!=heldId&&hand!=null) {
                if(held!=null)Destroy(held);heldId=id;
                if(item!=null) {
                    held=WorldFactory.ItemVisual(hand,item.definitionId);held.transform.localScale=Vector3.one*(item.Definition.twoHands?.7f:.65f);
                    // What you hold never throws a shadow into your own view (the torch beam starts right in front of it).
                    foreach(var r in held.GetComponentsInChildren<Renderer>())r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            // A teammate's held item follows their animated right hand.
            if(!IsOwner&&hand!=null&&handBone!=null&&avatar!=null&&avatar.gameObject.activeInHierarchy) {hand.position=handBone.position+transform.forward*.08f;hand.rotation=transform.rotation;}
            if(flashlight==null)return;
            // The torch stays lit whatever is in hand: held, it shines from the hand; otherwise from its shoulder clip.
            var torch=Inventory.Torch;
            bool on=Alive.Value&&Inventory.lightOn&&torch!=null&&torch.charge>0;
            bool inHand=on&&item==torch&&!HasHeavy;
            flashlight.enabled=on;spill.enabled=on;if(bounce!=null&&!on)bounce.enabled=false;
            if(!on)return;
            var lamp=flashlight.transform;
            if(IsOwner)lamp.localPosition=inHand?new Vector3(.28f,-.22f,.45f):new Vector3(-.2f,-.3f,.18f);
            else if(inHand&&handBone!=null&&avatar!=null&&avatar.gameObject.activeInHierarchy) {lamp.position=handBone.position+transform.forward*.2f;}
            else lamp.localPosition=inHand?new Vector3(.35f,1.1f,.6f):new Vector3(-.2f,HeadHeight-.3f,.25f);
            if(!IsOwner)lamp.rotation=transform.rotation*Quaternion.Euler(Pitch.Value,0,0);
            // A weak battery makes the beam dim and stutter.
            float level=torch.charge>.15f?1:(.25f+.75f*Mathf.PerlinNoise(Time.time*7,OwnerClientId))*Mathf.Lerp(.5f,1,torch.charge/.15f);
            flashlight.intensity=SinkApp.Instance.balance.flashlightIntensity*level;spill.intensity=flashlight.intensity*TorchSpill;
            if(bounce!=null)Bounce(level);
        }
        // Light the torch throws back off whatever it hits: a faint warm point light pulled out of the surface toward the eye,
        // so the room around the hotspot is not pitch black. Only for your own torch (one extra light per machine).
        void Bounce(float level)
        {
            var lamp=flashlight.transform;var from=lamp.position+lamp.forward*.3f;
            if(Physics.Raycast(from,lamp.forward,out var hit,14,~0,QueryTriggerInteraction.Ignore)) {
                var goal=hit.point+hit.normal*.8f-lamp.forward*.4f;
                bounce.transform.position=bounce.enabled?Vector3.Lerp(bounce.transform.position,goal,1-Mathf.Exp(-12*Time.deltaTime)):goal;
                bounce.enabled=true;bounce.intensity=.9f*level*Mathf.Clamp01(1.15f-hit.distance/14f);
            } else bounce.enabled=false;
        }
        public void Act(ActionKind kind,int argument=0,string payload="") {if(NetSession.Instance!=null)NetSession.Instance.ActRpc(kind,argument,payload);}
        void Interact(WorldInteractable focus)
        {
            if(focus.item!=null)Act(ActionKind.Pickup,0,focus.item.Uid);
            else if(focus.action==ActionKind.Depart||focus.action==ActionKind.Buy||focus.action==ActionKind.Sell||focus.action==ActionKind.ResolveQuota||focus.action==ActionKind.Terminal)GameHUD.Instance.Open(focus.action);
            else Act(focus.action,focus.argument);
        }
        public void Teleport(Vector3 position,PlayerZone zone,float? facing=null) {if(!IsServer)return;Zone.Value=zone;RelocateRpc(position,facing??transform.eulerAngles.y);}
        [Rpc(SendTo.ClientsAndHost,InvokePermission=RpcInvokePermission.Server)]void RelocateRpc(Vector3 position,float heading)
        {
            bool active=controller!=null&&controller.enabled;if(active)controller.enabled=false;transform.position=position;if(active)controller.enabled=true;
            ReleaseGrab();knock=Vector3.zero;
            vertical=0;climbing=false;mantling=false;swimState=false;lastDepth=NetSession.Instance?.Depth??0;if(IsOwner&&GameHUD.Instance!=null)GameHUD.Instance.Fade();
            if(IsOwner) {yaw=heading;pitch=0;transform.rotation=Quaternion.Euler(0,yaw,0);if(eyes!=null)eyes.transform.localRotation=Quaternion.identity;}
        }
        void TeleportLocal(Vector3 position) {controller.enabled=false;transform.position=position;controller.enabled=true;vertical=0;}
        [Rpc(SendTo.ClientsAndHost,InvokePermission=RpcInvokePermission.Server)]public void BeginClimbRpc(int kind)
        {
            if(!IsOwner)return;climbKind=kind;
            var bottom=kind==1?WorldZone.RescueBottom:WorldZone.RopeBottom;var top=kind==1?WorldZone.RescueTop:WorldZone.RopeTop;
            climbProgress=(transform.position-bottom).sqrMagnitude<(transform.position-top).sqrMagnitude?.03f:.97f;climbing=true;controller.enabled=false;
        }
        public void StepClimb(float direction,float dt,bool release=false)
        {
            if(!IsOwner||!climbing)return;
            var bottom=climbKind==1?WorldZone.RescueBottom:WorldZone.RopeBottom;var top=climbKind==1?WorldZone.RescueTop:WorldZone.RopeTop;
            float speed=SinkApp.Instance.balance.climbSpeed*(HasHeavy?.5f:1);
            climbProgress=Mathf.Clamp01(climbProgress+direction*speed*dt/Mathf.Max(1,Vector3.Distance(top,bottom)*(climbKind==1?1:1.05f)));
            // The boarding rope sags; the climber follows the same curve that is drawn. The rescue ladder is straight.
            transform.position=climbKind==1?Vector3.Lerp(bottom,top,climbProgress):WorldZone.RopePoint(climbProgress);
            if(climbProgress<=0||climbProgress>=1||release) {
                climbing=false;controller.enabled=true;
                if(climbProgress>=1)TeleportLocal(top+(climbKind==1?Vector3.left*.6f:Vector3.left*.8f));
                else if(climbProgress<=0)TeleportLocal(bottom+(climbKind==1?Vector3.right*.7f:Vector3.right*.8f));
                vertical=0;
            }
        }
        void Spectate(Keyboard keyboard)
        {
            var survivors=All.Where(p=>p.Alive.Value).ToArray();if(survivors.Length==0)return;
            if(keyboard.rightArrowKey.wasPressedThisFrame)spectateIndex++;if(keyboard.leftArrowKey.wasPressedThisFrame)spectateIndex--;
            spectateIndex=(spectateIndex%survivors.Length+survivors.Length)%survivors.Length;var target=survivors[spectateIndex];
            eyes.transform.position=target.transform.position+Vector3.up*target.HeadHeight;eyes.transform.rotation=target.transform.rotation*Quaternion.Euler(target.Pitch.Value,0,0);
        }
        public void TickVitals(float dt)
        {
            if(!IsServer||!Alive.Value)return;
            if(Underwater) {
                var tank=Inventory.All.FirstOrDefault(i=>i.Definition.kind==ItemKind.Oxygen&&i.oxygen>0);
                if(tank!=null) {tank.oxygen=Mathf.Max(0,tank.oxygen-dt);PublishInventory();}
                else Oxygen.Value=Mathf.Max(0,Oxygen.Value-dt);
                if(Oxygen.Value<=0&&tank==null)Damage(SinkApp.Instance.balance.drowningDamage*dt);
            } else Oxygen.Value=Mathf.Min(SinkApp.Instance.balance.breath,Oxygen.Value+8*dt);
            // A lit torch drains its battery; the charge is republished about once a second and when it runs out.
            var torch=Inventory.Torch;
            if(Inventory.lightOn&&torch!=null) {
                torch.charge=Mathf.Max(0,torch.charge-dt/Mathf.Max(1,SinkApp.Instance.balance.flashlightBattery));batteryPublish+=dt;
                if(torch.charge<=0) {Inventory.lightOn=false;PublishInventory();NetSession.Instance.Tell(OwnerClientId,"Baterai senter habis. Pegang baterai lalu klik kiri, atau cari baterai di kapal.");NetSession.Instance.PlaySfx(Snd.TorchOff,transform.position+Vector3.up,.7f);}
                else if(batteryPublish>=1) {batteryPublish=0;PublishInventory();}
            }
            if(Zone.Value==PlayerZone.Interior&&Time.time>=noiseAt) {
                // Footsteps carry as far as the pace: sprinting is loud, creeping is nearly silent.
                float pace=(transform.position-lastServerPosition).magnitude/Mathf.Max(dt,.01f);
                float range=Underwater?(pace>1.5f?5:0):pace>5.5f?14:pace>3?8:pace>.8f?(Crouched.Value?2:4):0;
                if(range>0) {noiseAt=Time.time+.5f;NetSession.Instance.Noise(transform.position,range);}
            }
            lastServerPosition=transform.position;
        }
        public void Damage(float amount) {if(!IsServer||!Alive.Value)return;Health.Value=Mathf.Max(0,Health.Value-amount);if(Health.Value<=0)Kill();}
        // ---- Hull stalker grab and throw (NetEnemy decides; the owner moves its own body) ------------------------------------
        [Rpc(SendTo.ClientsAndHost,InvokePermission=RpcInvokePermission.Server)]public void GrabbedRpc(NetworkObjectReference enemy,float seconds)
        {
            if(!IsOwner||!enemy.TryGet(out var obj))return;
            grabbedBy=obj.GetComponent<NetEnemy>();grabbedAt=Time.time;grabbedUntil=Time.time+seconds+1;
            climbing=false;mantling=false;swimState=false;vertical=0;knock=Vector3.zero;if(controller!=null)controller.enabled=false;shake=1;
        }
        // A strike's shove, or the throw that ends a grab.
        [Rpc(SendTo.ClientsAndHost,InvokePermission=RpcInvokePermission.Server)]public void ShoveRpc(Vector3 velocity)
        {
            if(!IsOwner)return;ReleaseGrab();knock=new Vector3(velocity.x,0,velocity.z);vertical=Mathf.Max(vertical,velocity.y);shake=Mathf.Max(shake,.8f);
        }
        void ReleaseGrab() {if(grabbedBy==null)return;grabbedBy=null;if(controller!=null&&!climbing&&!mantling)controller.enabled=true;}
        // Held up in front of the monster: lifted off the floor over the first 0.6 s, shaken, until the throw (or a safety timeout).
        void StepGrabbed()
        {
            if(grabbedBy==null||!grabbedBy.IsSpawned||Time.time>grabbedUntil) {ReleaseGrab();return;}
            float lift=Mathf.SmoothStep(0,1,(Time.time-grabbedAt)/.6f);var e=grabbedBy.transform;
            var target=e.position+e.forward*(.85f-.25f*lift)+Vector3.up*(.15f+.75f*lift)+Random.insideUnitSphere*.03f;
            transform.position=Vector3.Lerp(transform.position,target,1-Mathf.Exp(-14*Time.deltaTime));
            shake=Mathf.Max(shake,.55f);if(eyes!=null)eyes.transform.localPosition=Vector3.up*HeadHeight+Random.insideUnitSphere*shake*.07f;
        }
        public void Kill()
        {
            if(!IsServer||!Alive.Value)return;NetSession.Instance.DropAll(this);Health.Value=0;Alive.Value=false;Inventory.lightOn=false;PublishInventory();
        }
    }
}
