using UnityEngine;
namespace SinkScrapt
{
    // Steam leak or submerged electrical hazard. Both announce themselves with particles, light and a looping sound before they hurt anyone;
    // the electric one only fires while the shared flood line is above it.
    public class WorldHazard : MonoBehaviour
    {
        public bool electric;
        // Position in WorldZone.hazards (the same on every peer, since all build from one blueprint), the room title and deck,
        // for the boat computer's hazard control.
        public int index;
        public string label="";
        public int deck;
        ParticleSystem fx;
        Light glow;
        AudioSource loop;
        float tick;
        void Start()
        {
            WorldFactory.Shape(electric?"Live contact":"Steam vent",PrimitiveType.Cylinder,transform,new Vector3(0,.1f,0),new Vector3(.5f,.1f,.5f),electric?new Color(.15f,.4f,.45f):new Color(.3f,.32f,.32f),false,electric);
            fx=electric?Fx.Sparks(transform,new Vector3(0,.25f,0)):Fx.Steam(transform,new Vector3(0,.2f,0));
            glow=electric?Fx.Glow(transform,new Vector3(0,.7f,0),new Color(.4f,.9f,1f),6,2.4f):null;
            loop=Sfx.Loop(transform,electric?Snd.Zap:Snd.Steam,14);
        }
        void Update()
        {
            var session=NetSession.Instance;if(session==null||fx==null)return;
            bool active=session.InExpedition && !session.HazardSuppressed(index) && (electric?session.FloodLevel>transform.position.y:.5f+Mathf.Sin((float)session.Elapsed*.7f+transform.position.z)>0);
            var emission=fx.emission;emission.enabled=active;
            if(glow!=null) {glow.enabled=active;glow.intensity=2.4f*(.6f+.4f*Mathf.Sin(Time.time*40));}
            loop.volume=Mathf.MoveTowards(loop.volume,active?Sfx.Volume(Bus.Sfx)*(electric?.45f:.35f):0,Time.deltaTime*2);
            if(!session.IsServer||!active||Time.time<tick)return;tick=Time.time+.5f;
            foreach(var player in NetPlayer.All) if(player.Alive.Value&&player.Zone.Value==PlayerZone.Interior&&(player.transform.position-transform.position).sqrMagnitude<2.5f) player.Damage(electric?18:10);
        }
    }
}
