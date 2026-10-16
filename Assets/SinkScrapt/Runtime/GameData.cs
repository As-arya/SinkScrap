using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SinkScrapt
{
    public enum GamePhase { Menu, Loading, Harbor, Ready, Expedition, Departing, Results }
    public enum PlayerZone { Boat, Island, Exterior, Interior }
    public enum ItemKind { Scrap, Flashlight, Radio, Oxygen, Flare, Battery, Key, Package }
    public enum ActionKind { Pickup, Drop, Rack, Buy, Sell, Refill, ResolveQuota, Depart, CancelDeparture, TravelShip, TravelIsland, UseFlare, SelectSlot, ToggleLight, Portal, Climb, Terminal, HazardOff, Door, Reload, Utility }

    [Serializable]
    public class ItemDefinition
    {
        public int id;
        public string title;
        public ItemKind kind;
        public int value;
        public float weight;
        public bool twoHands;
        public Color color;
        public ItemDefinition(int id, string title, ItemKind kind, int value, float weight, bool twoHands, Color color)
        { this.id=id; this.title=title; this.kind=kind; this.value=value; this.weight=weight; this.twoHands=twoHands; this.color=color; }
    }

    public static class ItemCatalog
    {
        public static readonly ItemDefinition[] All = {
            new(0,"Kotak baut",ItemKind.Scrap,20,2,false,new Color(.35f,.39f,.40f)),
            new(1,"Pelat besi",ItemKind.Scrap,25,3,false,new Color(.50f,.28f,.18f)),
            new(2,"Katup",ItemKind.Scrap,30,2,false,new Color(.52f,.32f,.15f)),
            new(3,"Pengukur tekanan",ItemKind.Scrap,35,2,false,new Color(.65f,.64f,.48f)),
            new(4,"Lampu kapal",ItemKind.Scrap,40,2,false,new Color(.57f,.53f,.30f)),
            new(5,"Baterai bekas",ItemKind.Scrap,45,4,false,new Color(.24f,.30f,.30f)),
            new(6,"Radio rusak",ItemKind.Scrap,60,3,false,new Color(.31f,.36f,.27f)),
            new(7,"Papan sirkuit",ItemKind.Scrap,75,1,false,new Color(.12f,.45f,.31f)),
            new(8,"Kompas kuningan",ItemKind.Scrap,90,2,false,new Color(.64f,.48f,.23f)),
            new(9,"Motor listrik",ItemKind.Scrap,110,12,true,new Color(.36f,.48f,.48f)),
            new(10,"Pompa industri",ItemKind.Scrap,130,14,true,new Color(.52f,.28f,.16f)),
            new(11,"Generator",ItemKind.Scrap,150,18,true,new Color(.50f,.47f,.21f)),
            new(12,"Senter",ItemKind.Flashlight,15,1,false,new Color(.90f,.62f,.24f)),
            new(13,"Radio",ItemKind.Radio,25,1,false,new Color(.25f,.53f,.48f)),
            new(14,"Tabung oksigen",ItemKind.Oxygen,80,4,false,new Color(.23f,.51f,.63f)),
            new(15,"Flare",ItemKind.Flare,20,1,false,new Color(.95f,.28f,.12f)),
            // A spare cell for the flashlight: bought at the island or found in the wreck, used up when it is put in.
            new(16,"Baterai senter",ItemKind.Battery,10,.5f,false,new Color(.86f,.78f,.22f)),
            // Opens one locked door in the wreck it was found in. Keys never leave the expedition.
            new(17,"Kunci",ItemKind.Key,0,.2f,false,new Color(.78f,.62f,.30f)),
            // A parachute supply drop ordered on the boat computer. It lands on the small boat's deck; opening it (E) lays its
            // contents out on the deck. It is never sold and is not bought directly.
            new(18,"Paket pasokan",ItemKind.Package,0,8,true,new Color(.55f,.42f,.27f))
        };
        public const int Flashlight=12,Radio=13,Battery=16,Key=17,Package=18;
        // Tools sold through the boat computer, in shop order.
        public static readonly int[] Shop={12,16,13,14,15};
        // At most this many tools ride in one parachute package.
        public const int PackageCapacity=4;
        public static ItemDefinition Get(int id) => id>=0 && id<All.Length ? All[id] : null;
        public static ItemInstance Create(int id) => new() { uid=Guid.NewGuid().ToString("N"), definitionId=id, oxygen=id==14?90:0, charge=id==Flashlight?1:0 };
        // Tools that fit the belt (utility) slot: a torch or a radio.
        public static bool FitsUtility(ItemDefinition def) => def!=null&&(def.kind==ItemKind.Flashlight||def.kind==ItemKind.Radio);
        // Money is shown in dollars everywhere in the interface.
        public static string Money(int amount) => (amount<0?"-$":"$")+Mathf.Abs(amount);
    }
    // A computer order in flight: paid for, landing on the small boat by parachute once `remaining` seconds have passed.
    [Serializable] public class SupplyOrder { public string uid; public List<int> items=new(); public float remaining; }

    [Serializable]
    public class ItemInstance
    {
        public string uid;
        public int definitionId;
        public float oxygen;
        // Flashlight only: battery left, 0..1 (BalanceConfig.flashlightBattery seconds of light when full).
        public float charge;
        // Safe Cargo only: where the item lies on the small boat (zone coordinates), its heading, and its tool-rack slot
        // (1..BoatLayout.RackSlots, 0 = lying on the deck).
        public Vector3 at;
        public float yaw;
        public int rack;
        // Package only: the tool definition ids inside.
        public List<int> contents=new();
        public ItemDefinition Definition => ItemCatalog.Get(definitionId);
        public ItemInstance Copy() => new() { uid=uid, definitionId=definitionId, oxygen=oxygen, charge=charge, at=at, yaw=yaw, rack=rack, contents=new List<int>(contents??new List<int>()) };
    }

    // The small boat (SmallBoat scene, zone coordinates): a 10 x 20 m deck at x 20..30, z -15..5 with its top at y 1.
    // Every server check, the boat builder and QA read their positions from here.
    public static class BoatLayout
    {
        public const float MinX=20, MaxX=30, MinZ=-15, MaxZ=5;
        public const int RackSlots=6;
        public static readonly Vector3 Origin=new(25,0,-5);
        public static readonly Vector3 Helm=new(23.2f,1.6f,-8.7f), Terminal=new(26.8f,1.6f,-8.7f), Rack=new(25,1.8f,-13.6f);
        // Where a player stands to use the helm (QA and the departure spawn use it).
        public static readonly Vector3 HelmStand=new(23.2f,1.05f,-9.8f), TerminalStand=new(26.8f,1.05f,-9.8f), RackStand=new(25,1.05f,-12.6f);
        public static bool OnDeck(Vector3 p) => p.x>=MinX&&p.x<=MaxX&&p.z>=MinZ-.5f&&p.z<=MaxZ+.5f&&p.y>=.75f&&p.y<4.5f;
        // Drops land inside the rail, never on top of it.
        public static Vector3 ClampToDeck(Vector3 p) => new(Mathf.Clamp(p.x,MinX+.45f,MaxX-.45f),Mathf.Max(p.y,1.3f),Mathf.Clamp(p.z,MinZ+.45f,MaxZ-.45f));
        // Shelf slots 1..6 on the rack inside the cabin's back wall: top row 1-3, bottom row 4-6, at x 24, 25, 26.
        public static Vector3 RackSlot(int slot) {int k=Mathf.Clamp(slot,1,RackSlots)-1;return new Vector3(24+k%3,k<3?2.22f:1.62f,-13.6f);}
        // A tidy spot in the open cargo area for cargo without a position (old saves, disconnecting crew).
        public static Vector3 StowPosition(int index) {int layer=index/63,k=index%63;return new Vector3(21.8f+(k%7)*1.05f,1.35f+layer*.7f,-6+(k/7));}
    }
    [Serializable] public class InventoryData
    {
        // Four hand slots plus one belt (utility) slot for a torch or a radio. `lightOn` is the crew member's torch switch: it
        // lights the active torch (Torch) whatever is in hand, including a two-handed item.
        public List<ItemInstance> items=new(); public int selected; public bool lightOn;
        public ItemInstance utility;
        public bool HasUtility => utility!=null&&!string.IsNullOrEmpty(utility.uid)&&utility.Definition!=null;
        public IEnumerable<ItemInstance> All => HasUtility?items.Append(utility):items;
        public ItemInstance Selected => selected>=0&&selected<items.Count?items[selected]:null;
        // The torch the F key switches: the one on the belt, else the one in hand, else any carried torch.
        public ItemInstance Torch
        {
            get {
                if(HasUtility&&utility.Definition.kind==ItemKind.Flashlight)return utility;
                var held=Selected;if(held!=null&&held.Definition.kind==ItemKind.Flashlight)return held;
                return items.Where(i=>i.Definition.kind==ItemKind.Flashlight).OrderByDescending(i=>i.charge).FirstOrDefault();
            }
        }
        // Clears the belt slot when its item leaves the inventory (JsonUtility cannot store null, so an empty slot has no uid).
        public void Normalize() {if(!HasUtility)utility=null;}
        // Puts a carried battery into the active torch (or, failing that, the emptiest carried one). Returns the battery that was
        // used up, or null when there is no battery or no flashlight that needs one.
        public ItemInstance InsertBattery()
        {
            var battery=items.Find(i=>i.Definition.kind==ItemKind.Battery);if(battery==null)return null;
            var active=Torch;
            var torch=active!=null&&active.charge<.99f?active:All.Where(i=>i.Definition.kind==ItemKind.Flashlight&&i.charge<.99f).OrderBy(i=>i.charge).FirstOrDefault();
            if(torch==null)return null;
            var current=Selected;
            torch.charge=1;items.Remove(battery);
            selected=current!=null&&items.Contains(current)?items.IndexOf(current):items.Contains(torch)?items.IndexOf(torch):0;
            return battery;
        }
        // Belt slot swap: a torch or radio in hand goes onto the belt (whatever was there comes into the hand); with something
        // else in hand, the belt item comes into a free hand slot. Returns false when nothing could move.
        public bool SwapUtility()
        {
            var held=Selected;
            if(held!=null&&ItemCatalog.FitsUtility(held.Definition)) {
                var previous=HasUtility?utility:null;utility=held;
                if(previous!=null)items[selected]=previous;else {items.RemoveAt(selected);selected=Mathf.Clamp(selected,0,Mathf.Max(0,items.Count-1));}
                return true;
            }
            if(HasUtility&&items.Count<4&&(held==null||!held.Definition.twoHands)) {items.Add(utility);utility=null;selected=items.Count-1;return true;}
            return false;
        }
    }
    [Serializable] public class CrewLoadout { public string profile; public List<ItemInstance> items=new(); public ItemInstance utility; }
    [Serializable]
    public class CampaignState
    {
        // Version 2: Safe Cargo is whatever lies on the small boat, so every cargo item carries its deck position or rack slot.
        // Version 3: flashlights carry a battery charge.
        // Version 4: a belt (utility) slot per crew loadout, computer orders in flight, and package contents.
        public const int Version=4;
        public int version=Version;
        public int cycle=1;
        public int credits=120;
        public int quota=600;
        public int sold;
        public int expeditionsUsed;
        public List<ItemInstance> cargo=new();
        public List<CrewLoadout> loadouts=new();
        public List<SupplyOrder> orders=new();

        // A computer order: up to PackageCapacity tools paid for now and dropped on the boat `delay` seconds later.
        public SupplyOrder Order(IReadOnlyList<int> ids,float delay)
        {
            if(ids==null||ids.Count==0||ids.Count>ItemCatalog.PackageCapacity)return null;
            int total=0;
            foreach(int id in ids) {var def=ItemCatalog.Get(id);if(def==null||Array.IndexOf(ItemCatalog.Shop,id)<0)return null;total+=def.value;}
            if(credits<total)return null;
            credits-=total;var order=new SupplyOrder{uid=Guid.NewGuid().ToString("N"),items=ids.ToList(),remaining=Mathf.Max(0,delay)};orders.Add(order);return order;
        }
        // A package that has landed: it becomes cargo on the deck (so it is saved where it lies) holding the ordered tools.
        public ItemInstance Package(SupplyOrder order)
        {
            var package=ItemCatalog.Create(ItemCatalog.Package);package.contents=new List<int>(order.items);orders.Remove(order);return package;
        }
        // The whole crew died or was left behind: the small boat drifts home empty. Everything on it is lost (deck cargo and
        // the tool rack); orders still in flight are paid for and still arrive.
        public void LoseBoatCargo() {cargo.Clear();}
        // An item put down on the small boat becomes Safe Cargo where it lies (rack 0) or on a free tool-rack slot (tools only).
        public bool StoreOnDeck(ItemInstance item,Vector3 at,float yaw,int rack=0)
        {
            // Keys belong to the wreck they open and are never cargo.
            if(item==null || item.Definition==null || item.Definition.kind==ItemKind.Key || cargo.Any(x=>x.uid==item.uid) || rack<0 || rack>BoatLayout.RackSlots) return false;
            if(rack>0 && (item.Definition.kind==ItemKind.Scrap || cargo.Any(x=>x.rack==rack))) return false;
            var stored=item.Copy(); stored.at=at; stored.yaw=yaw; stored.rack=rack; cargo.Add(stored); return true;
        }
        // Picking an item up takes it out of Safe Cargo. Returns null when it is not (or no longer) cargo.
        public ItemInstance TakeFromDeck(string uid)
        {
            var item=cargo.Find(x=>x.uid==uid); if(item==null) return null;
            cargo.Remove(item); var taken=item.Copy(); taken.at=Vector3.zero; taken.yaw=0; taken.rack=0; return taken;
        }
        public int FreeRackSlot() {for(int n=1;n<=BoatLayout.RackSlots;n++)if(!cargo.Any(x=>x.rack==n))return n;return 0;}
        // Version-1 saves kept cargo as a list inside the old MUATAN AMAN chest; lay those items out on the deck.
        // Version-2 saves predate flashlight batteries; their flashlights come back fully charged.
        // Version-3 saves have no belt slots or orders; both start empty.
        public void Migrate()
        {
            orders??=new List<SupplyOrder>();
            if(cargo==null || loadouts==null || version<1 || version>=Version) return;
            if(version==1) for(int i=0;i<cargo.Count;i++) if(cargo[i]!=null) {cargo[i].at=BoatLayout.StowPosition(i); cargo[i].yaw=0; cargo[i].rack=0;}
            if(version<=2) foreach(var item in cargo.Concat(loadouts.Where(l=>l?.items!=null).SelectMany(l=>l.items))) if(item?.definitionId==ItemCatalog.Flashlight) item.charge=1;
            foreach(var loadout in loadouts) if(loadout!=null&&string.IsNullOrEmpty(loadout.utility?.uid)) loadout.utility=null;
            version=Version;
        }
        // Every item a loadout holds, belt slot included.
        static IEnumerable<ItemInstance> Carried(CrewLoadout l) => l.items==null?throw new InvalidDataException("Inventori rusak."):!string.IsNullOrEmpty(l.utility?.uid)?l.items.Append(l.utility):l.items;
        public int Sell(IReadOnlyCollection<string> ids)
        {
            if(ids==null) return 0;
            var selected=new HashSet<string>(ids);
            int revenue=0;
            for(int i=cargo.Count-1;i>=0;i--)
            {
                var item=cargo[i];
                if(item.Definition.kind!=ItemKind.Scrap || !selected.Contains(item.uid)) continue;
                revenue+=item.Definition.value; cargo.RemoveAt(i);
            }
            credits+=revenue; sold+=revenue; return revenue;
        }
        public bool Refill(InventoryData inventory)
        {
            var tank=inventory.All.FirstOrDefault(x=>x.definitionId==14 && x.oxygen<89.9f);
            if(tank==null || credits<20) return false;
            credits-=20; tank.oxygen=90; return true;
        }
        public void FinishExpedition() { expeditionsUsed=Mathf.Min(3,expeditionsUsed+1); }
        public bool ResolveQuota()
        {
            if(expeditionsUsed<3) throw new InvalidOperationException("Kuota hanya dinilai setelah tiga ekspedisi.");
            if(sold<quota) { Reset(); return false; }
            cycle++; quota=(int)Math.Ceiling(quota*1.35/10)*10; sold=0; expeditionsUsed=0; return true;
        }
        public void Reset() { cycle=1; credits=120; quota=600; sold=0; expeditionsUsed=0; cargo.Clear(); loadouts.Clear(); orders.Clear(); }
        public void Validate()
        {
            if(version!=Version || cycle<1 || credits<0 || quota<1 || sold<0 || expeditionsUsed<0 || expeditionsUsed>3 || cargo==null || loadouts==null || orders==null) throw new InvalidDataException("Data kampanye tidak valid.");
            foreach(var order in orders) if(order==null || order.items==null || order.items.Count==0 || order.items.Count>ItemCatalog.PackageCapacity || order.items.Any(id=>Array.IndexOf(ItemCatalog.Shop,id)<0) || float.IsNaN(order.remaining)) throw new InvalidDataException("Pesanan tidak valid.");
            var ids=new HashSet<string>();
            foreach(var item in cargo.Concat(loadouts.SelectMany(Carried)))
                if(item==null || string.IsNullOrEmpty(item.uid) || item.Definition==null || item.oxygen<0 || item.oxygen>90 || item.charge<0 || item.charge>1 || float.IsNaN(item.charge) || !ids.Add(item.uid)) throw new InvalidDataException("Barang tidak valid atau duplikat.");
            var racks=new HashSet<int>();
            foreach(var item in cargo)
                if(item.rack<0 || item.rack>BoatLayout.RackSlots || (item.rack>0 && (item.Definition.kind==ItemKind.Scrap || !racks.Add(item.rack))) || float.IsNaN(item.at.x+item.at.y+item.at.z) || float.IsInfinity(item.at.x+item.at.y+item.at.z))
                    throw new InvalidDataException("Posisi muatan tidak valid.");
        }
    }

    public static class CampaignSave
    {
        public static string DefaultPath => Path.Combine(Application.persistentDataPath,"campaign.json");
        public static void Write(CampaignState state,string path)
        {
            state.Validate();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            var temp=path+".tmp";
            using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
            using(var writer=new StreamWriter(stream)) { writer.Write(JsonUtility.ToJson(state,true)); writer.Flush(); stream.Flush(true); }
            if(File.Exists(path)) File.Replace(temp,path,path+".bak"); else File.Move(temp,path);
        }
        public static CampaignState Read(string path,out string notice)
        {
            notice="";
            if(!File.Exists(path) && !File.Exists(path+".bak")) return new CampaignState();
            foreach(var candidate in new[]{path,path+".bak"})
            {
                try {
                    if(!File.Exists(candidate)) continue;
                    var state=JsonUtility.FromJson<CampaignState>(File.ReadAllText(candidate));
                    if(state==null) throw new InvalidDataException();
                    state.Migrate();
                    state.Validate();
                    if(candidate!=path) notice="Checkpoint dipulihkan dari cadangan.";
                    return state;
                } catch(Exception e) when(e is IOException || e is ArgumentException || e is NullReferenceException) { notice="Checkpoint tidak dapat dibaca."; }
            }
            throw new InvalidDataException("Checkpoint dan cadangannya rusak. Pilih Kampanye Baru untuk mereset secara eksplisit.");
        }
    }

    public static class ShipTimeline
    {
        public const float BoatDeck=1;
        public const float CargoDeck=10;
        public const float ShipTop=14;
        // Milestones on a nominal 600 s (10 min) expedition; a different BalanceConfig.duration stretches them in proportion.
        // The wreck holds steady for the first four minutes (only the waist-deep bilge on deck 1), then sinks over the last six.
        public const float Nominal=600;
        public const float SinkStart=240, WaterDeck2=420, WaterDeck3=500, DeckLevel=540;
        static readonly float[] Times={0,SinkStart,330,WaterDeck2,WaterDeck3,DeckLevel,Nominal};
        static readonly float[] Depths={0,0,2,5,8,9,14.5f};
        // Seconds into an expedition of `duration` at which nominal milestone `nominal` happens.
        public static float At(float nominal,float duration) => nominal/Nominal*duration;
        // Smooth Hermite tangents keep motion continuous while passing every flood milestone.
        public static float Depth(float elapsed,float duration=Nominal)
        {
            var t=Mathf.Clamp(elapsed/duration*Nominal,0,Nominal);
            for(int i=0;i<Times.Length-1;i++)
            {
                if(t>Times[i+1]) continue;
                var h=Times[i+1]-Times[i]; var u=(t-Times[i])/h;
                var secant=(Depths[i+1]-Depths[i])/h;
                var left=i==0?secant:Mathf.Min(secant,(Depths[i]-Depths[i-1])/(Times[i]-Times[i-1]));
                var right=i==Times.Length-2?secant:Mathf.Min(secant,(Depths[i+2]-Depths[i+1])/(Times[i+2]-Times[i+1]));
                return (2*u*u*u-3*u*u+1)*Depths[i]+(u*u*u-2*u*u+u)*h*left+(-2*u*u*u+3*u*u)*Depths[i+1]+(u*u*u-u*u)*h*right;
            }
            return Depths[^1];
        }
        public static float FloorHeight(int floor) => 2+3*floor;
        // The lowest deck already stands in waist-deep bilge water when the crew arrives. The rising flood takes over once it is
        // higher (about minute 4); from then on the interior water is the sinking curve itself.
        public const float BilgeDepth=.9f;
        public static float InteriorWater(float depth) => Mathf.Max(depth,FloorHeight(0)+BilgeDepth);
    }
}
