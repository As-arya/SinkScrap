using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
namespace SinkScrapt
{
    public class NetItem : NetworkBehaviour
    {
        // Every spawned item on this peer (the forgiving pickup aim looks through them).
        public static readonly System.Collections.Generic.List<NetItem> Live=new();
        public readonly NetworkVariable<FixedString512Bytes> Data=new();
        public readonly NetworkVariable<ulong> Holder=new(ulong.MaxValue);
        public ItemInstance Item {get;private set;}
        public string Uid=>Item?.uid??"";
        Rigidbody body;
        BoxCollider hitbox;
        GameObject visual;
        // Server only: the item sits on a tool-rack shelf, held in place without physics.
        public bool racked;
        // The server hands the item over as JSON before Spawn and publishes it from OnNetworkSpawn, where the NetworkVariable knows its owner.
        public string initialJson="";
        // Supply packages float down under a parachute from this height above their landing spot (server-driven, synced by
        // NetworkTransform); the canopy is drawn while Descending and folds away on landing.
        public const float DropHeight=38,DropSpeed=4.2f;
        public readonly NetworkVariable<bool> Descending=new();
        public bool Falling=>Descending.Value;
        Vector3 landing;
        float landingYaw,chuteFold=1;
        Transform chute;
        public void Parachute(Vector3 at) {if(!IsServer)return;landing=at;landingYaw=transform.eulerAngles.y;Descending.Value=true;body.isKinematic=true;body.useGravity=false;}
        public override void OnNetworkSpawn()
        {
            if(IsServer&&initialJson.Length>0)Data.Value=new FixedString512Bytes(initialJson);
            Item=JsonUtility.FromJson<ItemInstance>(Data.Value.ToString());body=GetComponent<Rigidbody>();hitbox=GetComponent<BoxCollider>();
            Data.OnValueChanged+=Changed;
            visual=WorldFactory.ItemVisual(transform,Item.definitionId);
            hitbox.size=Item.Definition.kind==ItemKind.Package?new Vector3(.76f,.6f,.76f):Item.Definition.twoHands?new Vector3(.8f,.6f,.6f):new Vector3(.4f,.4f,.4f);
            chute=visual.transform.Find("Parachute");
            // Valuable scrap gives off a faint gold glint so a good find shows up in a dark room.
            if(Item.Definition.kind==ItemKind.Scrap&&Item.Definition.value>=75)Fx.Glow(transform,Vector3.up*.35f,new Color(1,.82f,.4f),3.4f,.9f);
            // Keys and spare batteries give off a faint glint too: in the dark wreck they are otherwise very hard to spot.
            if(Item.Definition.kind==ItemKind.Key||Item.Definition.kind==ItemKind.Battery)Fx.Glow(transform,Vector3.up*.3f,Item.Definition.kind==ItemKind.Key?new Color(1,.7f,.35f):new Color(.9f,1,.5f),2.2f,.6f);
            Live.Add(this);
            var target=gameObject.AddComponent<WorldInteractable>();target.title=Item.Definition.title;target.item=this;target.action=ActionKind.Pickup;
            body.mass=Item.Definition.weight;
            foreach(var player in NetPlayer.All)if(player.controller!=null)Physics.IgnoreCollision(hitbox,player.controller);
        }
        void Changed(FixedString512Bytes oldValue,FixedString512Bytes newValue) {Item=JsonUtility.FromJson<ItemInstance>(newValue.ToString());}
        public override void OnNetworkDespawn() {Data.OnValueChanged-=Changed;Live.Remove(this);}
        public override void OnDestroy() {Live.Remove(this);base.OnDestroy();}
        public void SetItem(ItemInstance item) {if(!IsServer)return;Item=item.Copy();Data.Value=new FixedString512Bytes(JsonUtility.ToJson(Item));}
        void Update()
        {
            if(Item==null)return;bool held=Holder.Value!=ulong.MaxValue;
            if(chute!=null) {chuteFold=Mathf.MoveTowards(chuteFold,Falling?1:0,Time.deltaTime*1.6f);chute.gameObject.SetActive(chuteFold>.01f);chute.localScale=new Vector3(chuteFold,Mathf.Max(.05f,chuteFold),chuteFold);}
            if(IsServer&&Falling) {
                // Drifts down with a slow sway, then settles on the deck as ordinary cargo.
                var next=Vector3.MoveTowards(transform.position,landing,DropSpeed*Time.deltaTime);float sway=Mathf.Sin(Time.time*.9f)*4;
                transform.SetPositionAndRotation(next,Quaternion.Euler(sway,landingYaw,sway*.6f));
                if((next-landing).sqrMagnitude<.0004f) {Descending.Value=false;Release(landing,landingYaw);}
                return;
            }
            if(body!=null&&IsServer) {body.isKinematic=held||racked;body.useGravity=!held&&!racked;}
            if(hitbox!=null)hitbox.enabled=!held;
            if(visual!=null)visual.SetActive(!held);
            if(held&&IsServer) {var owner=NetPlayer.All.Find(p=>p.OwnerClientId==Holder.Value);if(owner!=null)transform.position=owner.transform.position+Vector3.up;}
        }
        public bool Claim(NetPlayer player)
        {
            if(!IsServer||Holder.Value!=ulong.MaxValue)return false;
            Holder.Value=player.OwnerClientId;racked=false;body.isKinematic=true;return true;
        }
        public void Release(Vector3 position,float? yaw=null,bool onRack=false)
        {
            if(!IsServer)return;Holder.Value=ulong.MaxValue;racked=onRack;transform.position=position;if(yaw!=null)transform.rotation=Quaternion.Euler(0,yaw.Value,0);
            body.isKinematic=false;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;body.isKinematic=onRack;body.useGravity=!onRack;
        }
    }
}
