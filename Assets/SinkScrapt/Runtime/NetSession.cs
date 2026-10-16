using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SinkScrapt
{
    public class NetSession : NetworkBehaviour
    {
        public static NetSession Instance;
        public readonly NetworkVariable<GamePhase> Phase=new(GamePhase.Loading);
        public readonly NetworkVariable<double> Started=new();
        public readonly NetworkVariable<double> DepartureAt=new();
        public readonly NetworkVariable<int> SeedValue=new();
        // The interior blueprint, packed (InteriorLayout.Pack: gzip + base64 JSON).
        public readonly NetworkVariable<FixedString4096Bytes> Blueprint=new();
        // Boat-computer hazard control, two entries per interior hazard (WorldHazard.index): [2i] = off until, [2i+1] = ready again at.
        public NetworkList<double> HazardTimers;
        // One entry per blueprint door: bit 0 = open, bit 1 = unlocked with a key (only meaningful for locked doors).
        public NetworkList<byte> DoorStates;
        // Host setting for testing: no monsters spawn this session.
        public readonly NetworkVariable<bool> NoMonsters=new();
        // Time of day in hours. During an expedition it runs from ClockFrom to ClockTo with the shared clock (day into night or
        // night into day); otherwise it holds at ClockFrom. ClockTo may exceed 24 (it wraps).
        public readonly NetworkVariable<float> ClockFrom=new(18.5f);
        public readonly NetworkVariable<float> ClockTo=new(18.5f);
        // Arrival cutscene: which destination was reached (0 none, 1 wreck, 2 island) and the server time it started. Every client
        // plays it locally from these two values; the wreck's expedition clock starts once it ends (Started is set that far ahead).
        public readonly NetworkVariable<byte> Arrival=new();
        public readonly NetworkVariable<double> ArrivalAt=new();
        // Where the island merchant (scrap buyer) and the quota terminal stand (island zone coordinates).
        public static readonly Vector3 MerchantSpot=new(44,1.6f,-10),QuotaSpot=new(48,1.6f,-16);
        public CampaignState Campaign=new();
        public string LastResult="";
        public readonly List<FlareState> flares=new();
        public readonly Dictionary<ulong,QaSnapshot> QaReports=new();
        public class QaSnapshot {public string layout;public int nodes;public bool alive;public bool voice;public float depth;public bool boat,island;}
        readonly HashSet<string> loaded=new();
        readonly HashSet<ulong> layoutReady=new();
        readonly Dictionary<ulong,double> lastAction=new();
        readonly List<NetItem> items=new();
        readonly List<NetEnemy> enemies=new();
        bool transitioning;
        // True while scenes are being loaded or unloaded. New clients are turned away then: NGO would otherwise hand them the same scene twice.
        public bool Busy=>Phase.Value==GamePhase.Loading||transitioning;
        double nextVitals;
        int warningLevel;
        public int Seed=>SeedValue.Value;
        public string BlueprintCode=>Blueprint.Value.ToString();
        public bool DoorOpen(int index) => index>=0&&index<DoorStates.Count&&(DoorStates[index]&1)!=0;
        public bool DoorUnlocked(int index) => index>=0&&index<DoorStates.Count&&(DoorStates[index]&2)!=0;
        // A blueprint-locked door that no key has opened yet.
        public bool DoorLockedNow(int index)
        {
            var layout=WorldZone.Interior!=null?WorldZone.Interior.layout:null;
            return layout!=null&&index>=0&&index<layout.doors.Count&&layout.doors[index].locked&&!DoorUnlocked(index);
        }
        public float Hour
        {
            get {
                float from=ClockFrom.Value,to=ClockTo.Value;
                float t=InExpedition||Phase.Value==GamePhase.Results?Mathf.Clamp01((float)Elapsed/Mathf.Max(1,SinkApp.Instance.balance.duration)):0;
                return Mathf.Repeat(Mathf.Lerp(from,to,t),24);
            }
        }
        public bool InExpedition=>Phase.Value==GamePhase.Expedition||Phase.Value==GamePhase.Departing;
        public double Elapsed=>InExpedition||Phase.Value==GamePhase.Results?Math.Max(0,SinkApp.Instance.TimeNow-Started.Value):0;
        public float Depth=>InExpedition||Phase.Value==GamePhase.Results?ShipTimeline.Depth((float)Elapsed,SinkApp.Instance.balance.duration):0;
        // Water height inside the wreck (interior coordinates): the bilge on the lowest deck, then the shared sinking curve.
        public float FloodLevel=>InExpedition||Phase.Value==GamePhase.Results?ShipTimeline.InteriorWater(Depth):0;
        public float Remaining=>Mathf.Max(0,SinkApp.Instance.balance.duration-(float)Elapsed);
        public struct FlareState {public Vector3 position;public double ends;}
        public bool HazardSuppressed(int index) => index>=0&&index*2+1<HazardTimers.Count&&SinkApp.Instance.TimeNow<HazardTimers[index*2];
        public double HazardOffUntil(int index) => index>=0&&index*2+1<HazardTimers.Count?HazardTimers[index*2]:0;
        public double HazardReadyAt(int index) => index>=0&&index*2+1<HazardTimers.Count?HazardTimers[index*2+1]:0;
        double nextDeckGuard;

        void Awake() {HazardTimers=new NetworkList<double>();DoorStates=new NetworkList<byte>();}

        public override void OnNetworkSpawn()
        {
            Instance=this;NetworkManager.SceneManager.OnSceneEvent+=SceneEvent;
            if(IsServer)StartCoroutine(Boot());
            else RequestStateRpc();
        }
        public override void OnNetworkDespawn() {if(NetworkManager!=null&&NetworkManager.SceneManager!=null)NetworkManager.SceneManager.OnSceneEvent-=SceneEvent;if(Instance==this)Instance=null;}
        void SceneEvent(SceneEvent e) {if(e.SceneEventType==SceneEventType.LoadEventCompleted||e.SceneEventType==SceneEventType.UnloadEventCompleted)loaded.Add(e.SceneName);}
        IEnumerator Boot()
        {
            try {
                Campaign=SinkApp.Instance.newCampaign?new CampaignState():CampaignSave.Read(SinkApp.Instance.SavePath,out SinkApp.Instance.notice);
            } catch(Exception e) {SinkApp.Instance.notice=e.Message;LastResult=e.Message;Phase.Value=GamePhase.Menu;yield break;}
            NoMonsters.Value=SinkApp.Instance.noMonsters;IslandClock();
            // The crew gathers on the small boat at sea. The island is only reached at the end of a quota cycle, so a campaign
            // saved after its third expedition (quota not yet closed) resumes moored there.
            bool atIsland=Campaign.expeditionsUsed>=3;
            yield return Load("SmallBoat");if(atIsland)yield return Load("Island");Phase.Value=atIsland?GamePhase.Harbor:GamePhase.Ready;
            SpawnDeckCargo();
            foreach(var player in NetPlayer.All)Activate(player);
            SyncCampaign();Save();NoticeRpc(atIsland?"Di pulau: jual scrap ke pedagang lalu selesaikan kuota.":"Kapal siap. Pesan alat di KOMPUTER KAPAL, lalu berangkat lewat KEMUDI.");
        }
        IEnumerator Load(string name)
        {
            if(SceneManager.GetSceneByName(name).isLoaded)yield break;
            loaded.Remove(name);
            var status=NetworkManager.SceneManager.LoadScene(name,LoadSceneMode.Additive);
            if(status!=SceneEventProgressStatus.Started)throw new InvalidOperationException("Gagal memuat "+name+": "+status);
            var deadline=Time.realtimeSinceStartup+40;
            while(!loaded.Contains(name)&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!loaded.Contains(name))throw new TimeoutException("Pemuatan "+name+" melewati batas waktu.");
        }
        IEnumerator Unload(string name)
        {
            var scene=SceneManager.GetSceneByName(name);if(!scene.isLoaded)yield break;
            loaded.Remove(name);var status=NetworkManager.SceneManager.UnloadScene(scene);
            if(status!=SceneEventProgressStatus.Started)yield break;
            var deadline=Time.realtimeSinceStartup+40;while(!loaded.Contains(name)&&Time.realtimeSinceStartup<deadline)yield return null;
        }
        void Update()
        {
            if(!IsServer||!IsSpawned)return;
            flares.RemoveAll(f=>f.ends<SinkApp.Instance.TimeNow);
            if(Time.unscaledTimeAsDouble>=nextDeckGuard) {nextDeckGuard=Time.unscaledTimeAsDouble+1;GuardDeckCargo();}
            TickOrders(Time.unscaledDeltaTime);
            if(!InExpedition)return;
            if(Time.unscaledTimeAsDouble>=nextVitals) {
                float vitalsDelta=(float)Math.Max(0,.2+Time.unscaledTimeAsDouble-nextVitals);
                nextVitals=Time.unscaledTimeAsDouble+.2;
                foreach(var player in NetPlayer.All)if(player.Alive.Value)player.TickVitals(vitalsDelta);
                if(NetPlayer.All.Count>0&&!NetPlayer.All.Any(p=>p.Alive.Value)) {Finish("Seluruh kru hilang.");return;}
            }
            // Warnings: when the wreck starts to sink (six minutes left on the nominal clock), two minutes, thirty seconds.
            float duration=SinkApp.Instance.balance.duration;
            int level=Remaining<=30?3:Remaining<=120?2:Elapsed>=ShipTimeline.At(ShipTimeline.SinkStart,duration)?1:0;
            if(level>warningLevel) {warningLevel=level;NoticeRpc(level==1?Mathf.CeilToInt(Remaining/60)+" MENIT: kapal mulai tenggelam, air naik.":level==2?"2 MENIT: segera kembali ke kapal kecil!":"30 DETIK: kapal kecil akan berangkat!");AlarmRpc(level);}
            // The drowned hunter wakes when the flood reaches the middle deck.
            if(!NoMonsters.Value&&FloodLevel>=ShipTimeline.FloorHeight(1)&&!enemies.Any(e=>e!=null&&e.waterEnemy))SpawnEnemy(true);
            if(Remaining<=0) {Finish("Waktu habis. Kapal kecil berangkat otomatis.");return;}
            if(Phase.Value==GamePhase.Departing&&SinkApp.Instance.TimeNow>=DepartureAt.Value)Finish("Kapal kecil meninggalkan lokasi.");
        }
        public void Activate(NetPlayer player)
        {
            if(player==null||!player.IsSpawned)return;
            player.Alive.Value=true;player.Health.Value=100;player.Oxygen.Value=SinkApp.Instance.balance.breath;
            player.Teleport(WorldZone.BoatSpawn+new Vector3((player.OwnerClientId%4)*.8f,0,0),PlayerZone.Boat,WorldZone.Island!=null?90:-90);
        }
        public void RestoreLoadout(NetPlayer player)
        {
            if(InExpedition)return;
            var saved=Campaign.loadouts.Find(l=>l.profile==player.Profile);
            if(saved!=null&&player.Inventory.items.Count==0&&!player.Inventory.HasUtility) {
                player.Inventory.items=saved.items.Select(x=>x.Copy()).ToList();
                player.Inventory.utility=!string.IsNullOrEmpty(saved.utility?.uid)?saved.utility.Copy():null;player.PublishInventory();
            }
        }
        public void OnCrewDisconnected(ulong client)
        {
            var player=NetPlayer.All.Find(p=>p.OwnerClientId==client);if(player==null)return;
            if(InExpedition)DropAll(player);
            else {
                // Between expeditions a leaving diver's gear is laid out in the boat's cargo area instead of vanishing.
                foreach(var item in player.Inventory.All.ToList())StoreAndSpawn(item,BoatLayout.StowPosition(Campaign.cargo.Count));
                player.Inventory.items.Clear();player.Inventory.utility=null;Campaign.loadouts.RemoveAll(x=>x.profile==player.Profile);Save();SyncCampaign();
            }
        }
        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Everyone)] void RequestStateRpc(RpcParams rpc=default) {SyncCampaign();}
        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Everyone)] public void LayoutReadyRpc(RpcParams rpc=default) {layoutReady.Add(rpc.Receive.SenderClientId);}
        [Rpc(SendTo.ClientsAndHost,InvokePermission=RpcInvokePermission.Server)] public void NoticeRpc(string message) {if(SinkApp.Instance!=null)SinkApp.Instance.notice=message;}
        [Rpc(SendTo.ClientsAndHost,InvokePermission=RpcInvokePermission.Server)] void AlarmRpc(int level) {if(AudioDirector.Instance!=null)AudioDirector.Instance.Alarm(level);if(GameHUD.Instance!=null)GameHUD.Instance.AlertLevel(level);}
        [Rpc(SendTo.ClientsAndHost,InvokePermission=RpcInvokePermission.Server)] void SfxRpc(byte id,Vector3 position,float volume,float pitch) {Sfx.At((Snd)id,position,volume,pitch);}
        [Rpc(SendTo.ClientsAndHost,InvokePermission=RpcInvokePermission.Server)] void UiSfxRpc(byte id,float volume) {Sfx.Ui((Snd)id,volume);}
        [Rpc(SendTo.SpecifiedInParams,InvokePermission=RpcInvokePermission.Server)] void ClientSfxRpc(byte id,float volume,RpcParams rpc=default) {Sfx.Ui((Snd)id,volume);}
        // Server-side helpers: gameplay events are heard by everyone, positioned where they happened.
        public void PlaySfx(Snd id,Vector3 position,float volume=1,float pitch=1) {if(IsServer&&IsSpawned)SfxRpc((byte)id,position,volume,pitch);}
        public void PlayUi(Snd id,float volume=1) {if(IsServer&&IsSpawned)UiSfxRpc((byte)id,volume);}
        void Reject(ulong client) {if(IsServer&&IsSpawned)ClientSfxRpc((byte)Snd.UiError,1,RpcTarget.Single(client,RpcTargetUse.Temp));}
        [Rpc(SendTo.SpecifiedInParams,InvokePermission=RpcInvokePermission.Server)] void ClientNoticeRpc(string message,RpcParams rpc=default) {if(SinkApp.Instance!=null)SinkApp.Instance.notice=message;}
        // A message for one player only (pickups, refusals), so teammates' screens are not spammed.
        public void Tell(ulong client,string message) {if(IsServer&&IsSpawned)ClientNoticeRpc(message,RpcTarget.Single(client,RpcTargetUse.Temp));}
        [Rpc(SendTo.ClientsAndHost,InvokePermission=RpcInvokePermission.Server)] void CampaignRpc(string data) {if(!IsServer)Campaign=JsonUtility.FromJson<CampaignState>(data);}
        [Rpc(SendTo.ClientsAndHost,InvokePermission=RpcInvokePermission.Server)] void ResultRpc(string text) {LastResult=text;}
        void SyncCampaign() {CampaignRpc(JsonUtility.ToJson(Campaign));}
        public void Save()
        {
            if(InExpedition)return;
            // Deck cargo is saved where it has come to rest.
            foreach(var stored in Campaign.cargo) {
                if(stored.rack>0)continue;var entity=items.Find(x=>x!=null&&x.IsSpawned&&x.Uid==stored.uid&&x.Holder.Value==ulong.MaxValue);
                // A package still under its parachute is saved where it will land.
                if(entity!=null&&!entity.Falling) {stored.at=entity.transform.position;stored.yaw=entity.transform.eulerAngles.y;}
            }
            foreach(var player in NetPlayer.All) {
                if(string.IsNullOrEmpty(player.Profile))continue;
                Campaign.loadouts.RemoveAll(x=>x.profile==player.Profile);
                if(player.Alive.Value)Campaign.loadouts.Add(new CrewLoadout {profile=player.Profile,items=player.Inventory.items.Select(x=>x.Copy()).ToList(),utility=player.Inventory.HasUtility?player.Inventory.utility.Copy():null});
            }
            try {CampaignSave.Write(Campaign,SinkApp.Instance.SavePath);}
            catch(Exception e) {NoticeRpc("Checkpoint gagal disimpan: "+e.Message);Debug.LogError(e);}
        }
        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Everyone)]
        public void ActRpc(ActionKind action,int argument,string payload,RpcParams rpc=default)
        {
            var sender=rpc.Receive.SenderClientId;var player=NetPlayer.All.Find(p=>p.OwnerClientId==sender);
            if(player==null||!player.Alive.Value||Phase.Value==GamePhase.Loading||Phase.Value==GamePhase.Menu)return;
            var now=SinkApp.Instance.TimeNow;
            if(lastAction.TryGetValue(sender,out var previous)&&now-previous<.08)return;lastAction[sender]=now;
            bool safe=Phase.Value==GamePhase.Harbor||Phase.Value==GamePhase.Ready||Phase.Value==GamePhase.Results;
            switch(action) {
                case ActionKind.Pickup:
                    // Reach is measured from the diver's chest, so an item on the floor or below a swimmer counts as close.
                    var entity=items.Find(x=>x!=null&&x.Uid==payload);float reach=SinkApp.Instance.balance.reach+.75f;
                    if(entity==null||(entity.transform.position-(player.transform.position+Vector3.up*1.1f)).sqrMagnitude>reach*reach) {Reject(sender);return;}
                    // A supply package is opened where it landed rather than carried.
                    if(entity.Item.Definition.kind==ItemKind.Package) {OpenPackage(player,entity);return;}
                    // A torch or a radio goes onto an empty belt slot first, so it does not take a hand slot.
                    bool toBelt=ItemCatalog.FitsUtility(entity.Item.Definition)&&!player.Inventory.HasUtility;
                    if(!toBelt&&player.Inventory.items.Count>=4) {Tell(sender,"Slot penuh (4/4). Jatuhkan atau simpan sesuatu dulu.");Reject(sender);return;}
                    if(!toBelt&&player.HasHeavy) {Tell(sender,"Tanganmu penuh: lepaskan barang dua tangan dulu.");Reject(sender);return;}
                    if(entity.Claim(player)) {
                        // Taking an item off the boat removes it from the boat's cargo; it is only cargo again once it is put down aboard.
                        var taken=Campaign.TakeFromDeck(entity.Uid);var data=taken??entity.Item.Copy();
                        var picked=data.Definition;
                        if(toBelt)player.Inventory.utility=data;
                        else {player.Inventory.items.Add(data);if(picked.twoHands)player.Inventory.selected=player.Inventory.items.Count-1;}
                        player.PublishInventory();Noise(player.transform.position,8);PlaySfx(Snd.Pickup,player.transform.position+Vector3.up);
                        Tell(sender,"Diambil: "+picked.title+(picked.kind==ItemKind.Scrap?"  ("+ItemCatalog.Money(picked.value)+")":"")+(picked.twoHands?"  [dua tangan]":"")+(toBelt?"  • slot sabuk [5]":"")+(taken!=null?"  • keluar dari muatan kapal":""));
                        if(taken!=null) {SyncCampaign();if(safe)Save();}
                    }
                    break;
                case ActionKind.Drop: DropSelected(player);break;
                case ActionKind.Rack:
                    if(!player.OnBoat||!Near(player,BoatLayout.Rack))return;
                    var tool=player.Selected;
                    if(tool==null||tool.Definition.kind==ItemKind.Scrap) {Tell(sender,"Rak hanya untuk alat. Pegang alat lalu tekan E.");Reject(sender);return;}
                    int slot=Campaign.FreeRackSlot();
                    if(slot==0) {Tell(sender,"Rak alat penuh ("+BoatLayout.RackSlots+"/"+BoatLayout.RackSlots+").");Reject(sender);return;}
                    var shelf=BoatLayout.RackSlot(slot);
                    if(!Campaign.StoreOnDeck(tool,shelf,0,slot)) {Reject(sender);return;}
                    player.Inventory.items.Remove(tool);if(player.Inventory.Torch==null)player.Inventory.lightOn=false;
                    var racked=items.Find(x=>x!=null&&x.Uid==tool.uid);
                    if(racked!=null) {racked.SetItem(Campaign.cargo[^1]);racked.Release(shelf,0,true);}else SpawnItem(Campaign.cargo[^1].Copy(),shelf).Release(shelf,0,true);
                    player.PublishInventory();SyncCampaign();PlaySfx(Snd.Deposit,shelf,.7f);Tell(sender,"Diletakkan di rak alat: "+tool.Definition.title);if(safe)Save();break;
                case ActionKind.Buy:
                    // Orders are placed at the boat computer (at sea, at the island or during an expedition). `payload` lists the
                    // tool ids of one package; it lands on the boat by parachute after BalanceConfig.deliverySeconds.
                    if(!player.OnBoat||!Near(player,BoatLayout.Terminal))return;
                    var wanted=new List<int>();foreach(var part in (payload??"").Split(','))if(int.TryParse(part,out int id))wanted.Add(id);
                    var order=Campaign.Order(wanted,SinkApp.Instance.balance.deliverySeconds);
                    if(order==null) {Tell(sender,"Pesanan ditolak: uang tim tidak cukup atau paket terlalu penuh.");Reject(sender);return;}
                    if(safe)Save();SyncCampaign();PlaySfx(Snd.Purchase,BoatLayout.Terminal);
                    NoticeRpc("Pesanan dibayar: "+string.Join(", ",order.items.Select(i=>ItemCatalog.Get(i).title))+". Paket diterjunkan dalam "+Mathf.CeilToInt(order.remaining)+" detik.");
                    break;
                case ActionKind.Sell:
                    if(Phase.Value!=GamePhase.Harbor||!Near(player,MerchantSpot))return;
                    var ids=(payload??"").Split(',');var onDeck=Campaign.cargo.Select(x=>x.uid).ToArray();int revenue=Campaign.Sell(ids);
                    foreach(var uid in onDeck)if(!Campaign.cargo.Any(x=>x.uid==uid))DespawnItem(uid);
                    Save();SyncCampaign();NoticeRpc("Penjualan: "+ItemCatalog.Money(revenue)+".");if(revenue>0)PlaySfx(Snd.Sale,MerchantSpot);else Reject(sender);break;
                case ActionKind.Refill:
                    // The boat's compressor refills a carried oxygen tank from the computer.
                    if(player.OnBoat&&Near(player,BoatLayout.Terminal)&&Campaign.Refill(player.Inventory)) {player.PublishInventory();if(safe)Save();SyncCampaign();PlaySfx(Snd.Purchase,BoatLayout.Terminal);}else Reject(sender);break;
                case ActionKind.ResolveQuota:
                    if(Phase.Value!=GamePhase.Harbor||Campaign.expeditionsUsed<3||!Near(player,QuotaSpot))return;
                    var kept=Campaign.cargo.Select(x=>x.uid).ToArray();bool success=Campaign.ResolveQuota();
                    if(!success) {foreach(var uid in kept)DespawnItem(uid);foreach(var crew in NetPlayer.All){crew.Inventory=new InventoryData();crew.PublishInventory();Activate(crew);}}
                    Save();SyncCampaign();NoticeRpc(success?"Kuota terpenuhi! Siklus baru dimulai.":"Kuota gagal. Kampanye dimulai kembali.");PlayUi(success?Snd.QuotaPass:Snd.QuotaFail,.9f);break;
                case ActionKind.TravelShip:
                    if(safe&&player.OnBoat&&Near(player,BoatLayout.Helm)&&Campaign.expeditionsUsed<3)StartCoroutine(Travel(false));break;
                case ActionKind.TravelIsland:
                    // The island closes a quota cycle: the crew only sails there after its third expedition.
                    if(safe&&player.OnBoat&&Near(player,BoatLayout.Helm)&&Campaign.expeditionsUsed>=3&&WorldZone.Island==null)StartCoroutine(Travel(true));break;
                case ActionKind.Depart:
                    if(!InExpedition||!player.OnBoat||!Near(player,BoatLayout.Helm))return;
                    if(Phase.Value==GamePhase.Expedition) {DepartureAt.Value=now+5;Phase.Value=GamePhase.Departing;NoticeRpc("Berangkat dalam 5 detik. Kru yang tertinggal akan hilang.");PlayUi(Snd.Horn,.8f);}break;
                case ActionKind.CancelDeparture:
                    if(Phase.Value==GamePhase.Departing&&player.OnBoat&&Near(player,BoatLayout.Helm)) {Phase.Value=GamePhase.Expedition;DepartureAt.Value=0;NoticeRpc("Keberangkatan dibatalkan.");}break;
                case ActionKind.HazardOff:
                    if(!InExpedition||!player.OnBoat||!Near(player,BoatLayout.Terminal))return;
                    var wreck=WorldZone.Interior;if(wreck==null||argument<0||argument>=wreck.hazards.Count) {Reject(sender);return;}
                    while(HazardTimers.Count<wreck.hazards.Count*2)HazardTimers.Add(0);
                    if(now<HazardTimers[argument*2+1]) {Tell(sender,"Kontrol bahaya masih mendingin.");Reject(sender);return;}
                    var balance=SinkApp.Instance.balance;HazardTimers[argument*2]=now+balance.hazardOffSeconds;HazardTimers[argument*2+1]=now+balance.hazardOffSeconds+balance.hazardCooldown;
                    var hazard=wreck.hazards[argument];
                    NoticeRpc((hazard.electric?"Listrik":"Uap")+" di "+hazard.label+" (dek "+(hazard.deck+1)+") dimatikan "+Mathf.RoundToInt(balance.hazardOffSeconds)+" detik.");PlayUi(Snd.UiConfirm,.6f);break;
                case ActionKind.SelectSlot:
                    // Changing what is in hand never switches the torch off (it rides on the belt or the shoulder).
                    if(argument>=0&&argument<player.Inventory.items.Count&&!player.HasHeavy) {player.Inventory.selected=argument;player.PublishInventory();}break;
                case ActionKind.Utility:
                    if(player.Inventory.SwapUtility()) {player.PublishInventory();PlaySfx(Snd.Pickup,player.transform.position+Vector3.up,.4f,1.3f);}
                    else {Tell(sender,"Slot sabuk hanya untuk senter atau radio.");Reject(sender);}
                    break;
                case ActionKind.ToggleLight:
                    var torch=player.Inventory.Torch;
                    if(torch==null) {if(player.Inventory.lightOn){player.Inventory.lightOn=false;player.PublishInventory();}Tell(sender,"Tidak ada senter. Pesan senter di KOMPUTER KAPAL.");Reject(sender);return;}
                    // A dead torch takes a carried battery first; with none, it stays dark.
                    if(!player.Inventory.lightOn&&torch.charge<=0) {
                        var spent=player.Inventory.InsertBattery();
                        if(spent==null) {Tell(sender,"Baterai senter habis. Pesan baterai di KOMPUTER KAPAL atau cari di kapal.");Reject(sender);return;}
                        DespawnItem(spent.uid);Tell(sender,"Baterai baru dipasang.");
                    }
                    player.Inventory.lightOn=!player.Inventory.lightOn;player.PublishInventory();PlaySfx(player.Inventory.lightOn?Snd.TorchOn:Snd.TorchOff,player.transform.position+Vector3.up,.7f);
                    break;
                case ActionKind.Reload:
                    if(player.HasHeavy)return;
                    var used=player.Inventory.InsertBattery();
                    if(used==null) {Tell(sender,player.Inventory.Torch!=null?"Senter masih penuh.":"Tidak ada senter untuk diisi.");Reject(sender);return;}
                    DespawnItem(used.uid);player.PublishInventory();PlaySfx(Snd.TorchOn,player.transform.position+Vector3.up,.5f,.8f);Tell(sender,"Baterai dipasang: senter penuh.");break;
                case ActionKind.Door:
                    var wreckDoors=WorldZone.Interior;
                    if(!InExpedition||wreckDoors==null||wreckDoors.layout==null||argument<0||argument>=wreckDoors.layout.doors.Count||argument>=DoorStates.Count)return;
                    if((WorldZone.InteriorOffset+wreckDoors.layout.DoorCenter(argument)+Vector3.up-(player.transform.position+Vector3.up)).sqrMagnitude>12) {Reject(sender);return;}
                    if(DoorLockedNow(argument)) {
                        // A key opens one locked door and is used up there.
                        var key=player.Inventory.items.Find(i=>i.Definition.kind==ItemKind.Key);
                        var lockAt=WorldZone.InteriorOffset+wreckDoors.layout.DoorCenter(argument)+Vector3.up;
                        if(key==null) {Tell(sender,"Pintu terkunci. Cari kunci di dalam kapal.");PlaySfx(Snd.DoorLocked,lockAt,.75f);Noise(player.transform.position,5);return;}
                        player.Inventory.items.Remove(key);DespawnItem(key.uid);player.PublishInventory();DoorStates[argument]=3;
                        Tell(sender,"Kunci dipakai: pintu terbuka.");PlaySfx(Snd.DoorLocked,lockAt,.6f,1.2f);PlaySfx(WorldFactory.WoodDoor(wreckDoors.layout,argument)?Snd.DoorOpenWood:Snd.DoorOpen,lockAt,.9f);Noise(player.transform.position,9);return;
                    }
                    SetDoor(argument,!DoorOpen(argument));Noise(player.transform.position,7);break;
                case ActionKind.UseFlare:
                    if(!InExpedition||player.HasHeavy||player.Selected?.Definition.kind!=ItemKind.Flare)return;
                    var flare=player.Selected;player.Inventory.items.Remove(flare);DespawnItem(flare.uid);player.PublishInventory();
                    var position=player.transform.position+player.transform.forward*2+Vector3.up*.4f;
                    flares.Add(new FlareState {position=position,ends=now+SinkApp.Instance.balance.flareDuration});FlareRpc(position);PlaySfx(Snd.FlareIgnite,position,.9f);Noise(position,14);break;
                case ActionKind.Portal:
                    if(!InExpedition||argument<0||argument>=InteriorLayout.EntryCount)return;
                    bool interior=player.Zone.Value==PlayerZone.Interior;
                    var door=interior?WorldZone.InteriorEntry(argument):WorldZone.ExteriorEntry(argument);
                    if((player.transform.position-door).sqrMagnitude>22)return;
                    PlaySfx(Snd.Hatch,player.transform.position,.9f);
                    if(interior)player.Teleport(WorldZone.ExteriorExit(argument),PlayerZone.Exterior,WorldZone.ExteriorExitFacing(argument));
                    else player.Teleport(WorldZone.InteriorEntry(argument),PlayerZone.Interior,WorldZone.InteriorFacing(argument));
                    break;
                case ActionKind.Climb:
                    if(argument==0&&!InExpedition)return;
                    var bottom=argument==1?WorldZone.RescueBottom:WorldZone.RopeBottom;var top=argument==1?WorldZone.RescueTop:WorldZone.RopeTop;
                    if((player.transform.position-bottom).sqrMagnitude>14&&(player.transform.position-top).sqrMagnitude>14)return;
                    player.BeginClimbRpc(argument);break;
            }
        }
        static bool Near(NetPlayer player,Vector3 pos)=>(player.transform.position-pos).sqrMagnitude<16;
        // Server: open or close an unlocked door, with the sound at the door.
        public void SetDoor(int index,bool open)
        {
            if(!IsServer||index<0||index>=DoorStates.Count||DoorLockedNow(index)||DoorOpen(index)==open)return;
            DoorStates[index]=(byte)((DoorStates[index]&2)|(open?1:0));
            var layout=WorldZone.Interior!=null?WorldZone.Interior.layout:null;
            if(layout!=null) {
                bool wood=WorldFactory.WoodDoor(layout,index);
                PlaySfx(open?(wood?Snd.DoorOpenWood:Snd.DoorOpen):(wood?Snd.DoorCloseWood:Snd.DoorClose),WorldZone.InteriorOffset+layout.DoorCenter(index)+Vector3.up,open?.7f:.65f,UnityEngine.Random.Range(.94f,1.06f));
            }
        }
        // QA: every door unlocked and standing open, so traversal checks can walk into every room.
        public void OpenAllDoors() {if(!IsServer)return;for(int i=0;i<DoorStates.Count;i++)DoorStates[i]=3;}
        // Time of day at the island: day or night, at random, each time the crew arrives.
        void IslandClock()
        {
            float hour=UnityEngine.Random.value<.5f?UnityEngine.Random.Range(8f,16.5f):Mathf.Repeat(UnityEngine.Random.Range(20f,28.5f),24);
            ClockFrom.Value=hour;ClockTo.Value=hour;
        }
        // An expedition that starts by day ends at night, and one that starts at night ends at dawn.
        void ExpeditionClock()
        {
            if(UnityEngine.Random.value<.5f) {float from=UnityEngine.Random.Range(12.5f,14.5f);ClockFrom.Value=from;ClockTo.Value=UnityEngine.Random.Range(20.6f,21.6f);}
            else {float from=UnityEngine.Random.Range(1.5f,3f);ClockFrom.Value=from;ClockTo.Value=UnityEngine.Random.Range(7.6f,8.6f);}
        }
        IEnumerator Travel(bool island)
        {
            if(transitioning)yield break;transitioning=true;Save();Phase.Value=GamePhase.Loading;NoticeRpc("Mesin menyala. Menuju "+(island?"pulau...":"kapal kargo..."));
            foreach(var player in NetPlayer.All)Activate(player);
            yield return new WaitForSecondsRealtime(2);
            ClearEntities();yield return Unload("Interior");yield return Unload("Exterior");
            if(island) {IslandClock();yield return Load("Island");Blueprint.Value=default;Phase.Value=GamePhase.Harbor;BeginArrival(2);}
            else {
                yield return Unload("Island");SeedValue.Value=UnityEngine.Random.Range(1,int.MaxValue);
                var layout=InteriorGenerator.Generate(Seed);Blueprint.Value=new FixedString4096Bytes(layout.Pack());layoutReady.Clear();
                DoorStates.Clear();foreach(var _ in layout.doors)DoorStates.Add(0);
                yield return Load("Exterior");yield return Load("Interior");
                var deadline=Time.realtimeSinceStartup+30;
                while(NetworkManager.ConnectedClientsIds.Any(id=>!layoutReady.Contains(id))&&Time.realtimeSinceStartup<deadline)yield return null;
                foreach(var id in NetworkManager.ConnectedClientsIds.Where(id=>!layoutReady.Contains(id)).ToArray()) if(id!=NetworkManager.LocalClientId)NetworkManager.DisconnectClient(id,"Layout tidak siap.");
                if(WorldZone.Interior==null||WorldZone.Interior.nodes.Count==0) {NoticeRpc("Interior gagal dibangun. Kembali ke pulau.");Phase.Value=GamePhase.Ready;transitioning=false;yield break;}
                HazardTimers.Clear();SpawnLoot(layout);
                if(!NoMonsters.Value) {SpawnEnemy(false);if(Campaign.cycle>=4)SpawnEnemy(false);}
                ExpeditionClock();
                // The shared clock starts when the arrival cutscene ends: until then Elapsed stays at zero for everyone.
                Started.Value=SinkApp.Instance.TimeNow+SinkApp.Instance.balance.arrivalCutscene;nextVitals=Time.unscaledTimeAsDouble+.2;warningLevel=0;Phase.Value=GamePhase.Expedition;BeginArrival(1);
                NoticeRpc(Mathf.RoundToInt(SinkApp.Instance.balance.duration/60)+" menit. Kapal mulai tenggelam pada menit "+Mathf.RoundToInt(ShipTimeline.At(ShipTimeline.SinkStart,SinkApp.Instance.balance.duration)/60)+"."+(NoMonsters.Value?"  [MODE TANPA MONSTER]":""));
            }
            foreach(var player in NetPlayer.All)Activate(player);transitioning=false;if(island)Save();
        }
        public void NotifyLayoutBuilt() {if(IsSpawned)LayoutReadyRpc();}
        public void ProbePeers() {if(!IsServer)return;QaReports.Clear();QaProbeRpc();}
        [Rpc(SendTo.ClientsAndHost,InvokePermission=RpcInvokePermission.Server)]void QaProbeRpc()
        {
            var player=NetPlayer.Local;var comms=SinkApp.Instance.GetComponent<VoiceComms>();
            QaReplyRpc(BlueprintCode,WorldZone.Interior?.nodes.Count??0,player!=null&&player.Alive.Value,comms.ready,Depth,WorldZone.Boat!=null,WorldZone.Island!=null);
        }
        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Everyone)]void QaReplyRpc(string layout,int nodes,bool alive,bool voice,float depth,bool boat,bool island,RpcParams rpc=default)
        {
            if(layout.Length>4096||nodes<0||nodes>1000)return;
            QaReports[rpc.Receive.SenderClientId]=new QaSnapshot{layout=layout,nodes=nodes,alive=alive,voice=voice,depth=depth,boat=boat,island=island};
        }
        // QA: what each client's screen shows right now (QaRunner.PeerView as JSON); clients started with --qa-capture also save
        // a screenshot of it next to their save path.
        public readonly Dictionary<ulong,string> QaViews=new();
        public void ProbeView(string tag,bool capture) {if(!IsServer)return;QaViews.Clear();QaViewRpc(tag,capture);}
        [Rpc(SendTo.NotServer,InvokePermission=RpcInvokePermission.Server)]void QaViewRpc(string tag,bool capture)
        {
            if(capture)QaRunner.Capture("client-"+tag);QaViewReplyRpc(QaRunner.DescribeView());
        }
        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Everyone)]void QaViewReplyRpc(string view,RpcParams rpc=default)
        {
            if(view.Length>8192)return;QaViews[rpc.Receive.SenderClientId]=view;
        }
        void SpawnLoot(InteriorLayout layout)
        {
            var random=new System.Random(Seed);int count=random.Next(18,26);
            // Loot lands on the clear walkway through a room cell; furniture only stands against the walls.
            Vector3 InRoom(RoomData room) {var cell=ShipGrid.Center(room.floor,room.x+random.Next(room.w),room.z+random.Next(room.d));return WorldZone.InteriorOffset+cell+new Vector3((float)random.NextDouble()*1.1f-.55f,.5f,(float)random.NextDouble()*1.1f-.55f);}
            for(int i=0;i<count;i++) {
                var room=layout.rooms[random.Next(layout.rooms.Count)];int roll=random.Next(100);
                int def=roll<(room.floor==0?18:room.floor==1?10:4)?random.Next(9,12):roll<35?random.Next(6,9):random.Next(6);
                SpawnItem(ItemCatalog.Create(def),InRoom(room));
            }
            // Behind every locked door waits something worth the key: a high-value or large item.
            var locked=new HashSet<int>();
            for(int i=0;i<layout.doors.Count;i++) {
                if(!layout.doors[i].locked)continue;var room=layout.DoorRoom(i);
                if(room==null||layout.DoorCount(room)!=1)room=layout.RoomAt(layout.doors[i].floor,layout.doors[i].x,layout.doors[i].z);
                if(room==null||!locked.Add(room.id))continue;
                SpawnItem(ItemCatalog.Create(random.Next(100)<40?random.Next(9,12):random.Next(7,9)),InRoom(room));
            }
            // One key per locked door and a few spare torch batteries, anywhere a diver can reach without a key.
            var open=layout.rooms.Where(r=>!locked.Contains(r.id)).ToList();
            var corridorCells=new List<Vector3>();
            for(int f=0;f<ShipGrid.Decks;f++)for(int z=0;z<ShipGrid.Rows;z++)for(int x=0;x<ShipGrid.Columns;x++)if(layout.IsCorridor(f,x,z))corridorCells.Add(ShipGrid.Center(f,x,z));
            Vector3 Anywhere() {
                if(random.Next(100)<65&&open.Count>0)return InRoom(open[random.Next(open.Count)]);
                var c=corridorCells[random.Next(corridorCells.Count)];return WorldZone.InteriorOffset+c+new Vector3((float)random.NextDouble()*1.6f-.8f,.5f,(float)random.NextDouble()*1.6f-.8f);
            }
            for(int k=0;k<locked.Count;k++)SpawnItem(ItemCatalog.Create(ItemCatalog.Key),Anywhere());
            int batteries=random.Next(2,4);for(int k=0;k<batteries;k++)SpawnItem(ItemCatalog.Create(ItemCatalog.Battery),Anywhere());
        }
        public NetItem SpawnItem(ItemInstance data,Vector3 position,float yaw=0)
        {
            var go=Instantiate(Resources.Load<GameObject>("SinkScrapt/Item"),position,Quaternion.Euler(0,yaw,0));var item=go.GetComponent<NetItem>();
            item.initialJson=JsonUtility.ToJson(data);item.GetComponent<NetworkObject>().Spawn(false);items.Add(item);return item;
        }
        void SpawnEnemy(bool water)
        {
            var world=WorldZone.Interior;if(world==null||world.nodes.Count==0)return;
            int count=water&&Campaign.cycle>=4?2:1;
            for(int n=0;n<count;n++) {
                // Start far from both entry hatches: the dry stalker on the upper decks, the drowned hunter on the lowest (flooded first).
                var random=new System.Random(Seed+(water?991:17)+enemies.Count*31+n);var spawn=new List<Vector3>();
                for(int i=0;i<world.nodes.Count;i++) {
                    var node=world.nodes[i];if(!world.IsOpenFloor(i)||(water?node.y>3.5f:node.y<4.5f))continue;
                    var at=world.transform.TransformPoint(node);
                    if(Enumerable.Range(0,InteriorLayout.EntryCount).Any(e=>Vector3.Distance(at,WorldZone.InteriorEntry(e))<22))continue;
                    spawn.Add(at);
                }
                if(spawn.Count==0)for(int i=0;i<world.nodes.Count;i++)if(world.IsOpenFloor(i)&&(water?world.nodes[i].y<3.5f:world.nodes[i].y>4.5f))spawn.Add(world.transform.TransformPoint(world.nodes[i]));
                var position=spawn[random.Next(spawn.Count)];
                var go=Instantiate(Resources.Load<GameObject>("SinkScrapt/Enemy"));var enemy=go.GetComponent<NetEnemy>();
                enemy.IsWater.Value=water;enemy.transform.position=position+(water?Vector3.up*1.1f:Vector3.zero);enemy.GetComponent<NetworkObject>().Spawn(false);enemies.Add(enemy);
            }
        }
        // Anything put down aboard the small boat is the boat's cargo from that moment: it stays where it lands and is saved
        // with the campaign, unless the whole crew is lost (Finish).
        public void DropSelected(NetPlayer player)
        {
            var data=player.Selected;if(data==null)return;player.Inventory.items.Remove(data);
            if(player.Inventory.Torch==null)player.Inventory.lightOn=false;
            bool aboard=player.OnBoat;float yaw=player.transform.eulerAngles.y;
            var pos=player.transform.position+player.transform.forward*.8f+Vector3.up*.4f;
            if(aboard)pos=BoatLayout.ClampToDeck(pos);
            bool secured=aboard&&Campaign.StoreOnDeck(data,pos,yaw);
            var existing=items.Find(x=>x!=null&&x.Uid==data.uid);
            if(existing!=null) {existing.SetItem(secured?Campaign.cargo[^1]:data);existing.Release(pos,yaw);}else SpawnItem((secured?Campaign.cargo[^1]:data).Copy(),pos,yaw);
            player.PublishInventory();Noise(player.transform.position,10);PlaySfx(secured?Snd.Deposit:Snd.Drop,pos,secured?.6f:1);
            if(secured) {SyncCampaign();if(Phase.Value==GamePhase.Harbor||Phase.Value==GamePhase.Ready||Phase.Value==GamePhase.Results)Save();}
            if(player.Alive.Value)Tell(player.OwnerClientId,secured?"Di kapal: "+data.Definition.title+(data.Definition.kind==ItemKind.Scrap?"  ("+ItemCatalog.Money(data.Definition.value)+")":""):"Dijatuhkan: "+data.Definition.title);
        }
        public void DropAll(NetPlayer player)
        {
            if(player.Inventory.HasUtility) {player.Inventory.items.Add(player.Inventory.utility);player.Inventory.utility=null;}
            while(player.Inventory.items.Count>0) {player.Inventory.selected=0;DropSelected(player);}player.Inventory.lightOn=false;player.PublishInventory();
        }
        void DespawnItem(string uid) {var item=items.Find(i=>i!=null&&i.Uid==uid);if(item==null)return;items.Remove(item);if(item.IsSpawned)item.NetworkObject.Despawn();}
        // Deck cargo belongs to the boat and outlives every scene change; only loose wreck items and monsters are cleared.
        void ClearEntities()
        {
            foreach(var item in items)if(item!=null&&item.IsSpawned&&!IsDeckCargo(item.Uid))item.NetworkObject.Despawn();
            foreach(var e in enemies)if(e!=null&&e.IsSpawned)e.NetworkObject.Despawn();
            items.RemoveAll(i=>i==null||!i.IsSpawned);enemies.Clear();flares.Clear();
        }
        bool IsDeckCargo(string uid) => Campaign.cargo.Any(x=>x.uid==uid);
        void StoreAndSpawn(ItemInstance item,Vector3 at)
        {
            if(!Campaign.StoreOnDeck(item,at,0))return;var stored=Campaign.cargo[^1];
            var existing=items.Find(x=>x!=null&&x.Uid==stored.uid);
            if(existing!=null) {existing.SetItem(stored);existing.Release(at,0);}else SpawnItem(stored.Copy(),at);
        }
        // Rebuild the boat's cargo from the checkpoint when the session starts.
        void SpawnDeckCargo()
        {
            foreach(var stored in Campaign.cargo) {
                if(items.Any(x=>x!=null&&x.Uid==stored.uid))continue;
                var at=stored.rack>0?BoatLayout.RackSlot(stored.rack):BoatLayout.OnDeck(stored.at)?stored.at+Vector3.up*.05f:BoatLayout.StowPosition(Campaign.cargo.IndexOf(stored));
                SpawnItem(stored.Copy(),at,stored.yaw).Release(at,stored.yaw,stored.rack>0);
            }
        }
        // Cargo that tips over the rail or falls through the deck is put back in the cargo area rather than lost.
        void GuardDeckCargo()
        {
            for(int i=0;i<Campaign.cargo.Count;i++) {
                var stored=Campaign.cargo[i];if(stored.rack>0)continue;
                var entity=items.Find(x=>x!=null&&x.IsSpawned&&x.Uid==stored.uid);
                if(entity==null||entity.Falling||entity.Holder.Value!=ulong.MaxValue||BoatLayout.OnDeck(entity.transform.position))continue;
                var at=BoatLayout.StowPosition(i);stored.at=at;entity.SetItem(stored);entity.Release(at,0);
            }
        }
        void Finish(string reason)
        {
            if(transitioning||!InExpedition)return;transitioning=true;
            int survivors=0;
            foreach(var player in NetPlayer.All) {
                // Keys only open doors in this wreck; they stay behind with it.
                if(player.Alive.Value&&player.Inventory.items.RemoveAll(i=>i.Definition.kind==ItemKind.Key)>0)player.PublishInventory();
                if(player.Alive.Value&&player.OnBoat) {survivors++;continue;}
                if(player.Alive.Value)player.Kill();else {player.Inventory.items.Clear();player.Inventory.utility=null;}player.PublishInventory();
            }
            Campaign.FinishExpedition();Phase.Value=GamePhase.Results;DepartureAt.Value=0;HazardTimers.Clear();DoorStates.Clear();
            float endHour=Hour;ClockFrom.Value=endHour;ClockTo.Value=endHour;
            // Nobody made it back aboard: the boat drifts home empty and everything on it (deck cargo, tool rack) is lost.
            bool wiped=survivors==0;if(wiped)Campaign.LoseBoatCargo();
            ClearEntities();
            ResultRpc(reason+"\nPenyintas: "+survivors+(wiped?"\n<color=#E64033>Tidak ada kru yang kembali ke kapal kecil: semua muatan di kapal hilang.</color>":" / Muatan di kapal: "+Campaign.cargo.Count+" barang."));
            PlayUi(survivors>0?Snd.Horn:Snd.QuotaFail,.7f);
            foreach(var player in NetPlayer.All)Activate(player);
            Save();SyncCampaign();StartCoroutine(AfterResult());
        }
        IEnumerator AfterResult()
        {
            yield return Unload("Interior");yield return Unload("Exterior");Blueprint.Value=default;Phase.Value=GamePhase.Ready;
            NoticeRpc(Campaign.expeditionsUsed>=3?"Tiga ekspedisi selesai. Berlayar ke pulau lewat KEMUDI untuk menjual scrap dan menutup kuota.":"Kru kembali. Pakai KEMUDI untuk ekspedisi berikutnya.");
            transitioning=false;Save();
        }
        void BeginArrival(byte destination) {Arrival.Value=destination;ArrivalAt.Value=SinkApp.Instance.TimeNow;}
        // ---- Computer orders and parachute drops ----------------------------------------------------------------------------
        // Orders count down on the host in every phase except scene loading; a due order lands on the boat as a package.
        void TickOrders(float dt)
        {
            if(Campaign.orders.Count==0||Phase.Value==GamePhase.Loading||Phase.Value==GamePhase.Menu||transitioning||WorldZone.Boat==null)return;
            foreach(var order in Campaign.orders.ToArray()) {order.remaining-=dt;if(order.remaining<=0)Deliver(order);}
        }
        void Deliver(SupplyOrder order)
        {
            var package=Campaign.Package(order);
            // A free spot in the open cargo area (x 21..29, z -6..4), away from other cargo where possible.
            var random=new System.Random(order.uid.GetHashCode());Vector3 spot=new(25,1.35f,-1);float best=-1;
            for(int i=0;i<12;i++) {
                var candidate=new Vector3(21.8f+(float)random.NextDouble()*6.4f,1.35f,-5.2f+(float)random.NextDouble()*8.4f);
                float clear=Campaign.cargo.Where(c=>c.rack==0).Select(c=>Vector3.Distance(c.at,candidate)).DefaultIfEmpty(9).Min();
                if(clear>best) {best=clear;spot=candidate;}
            }
            float yaw=(float)random.NextDouble()*360;
            if(!Campaign.StoreOnDeck(package,spot,yaw))return;
            var entity=SpawnItem(Campaign.cargo[^1].Copy(),spot+Vector3.up*NetItem.DropHeight,yaw);entity.Parachute(spot);
            SyncCampaign();NoticeRpc("Paket pasokan diterjunkan ke kapal kecil. Tekan E pada paket untuk membukanya.");PlaySfx(Snd.Horn,spot+Vector3.up*6,.35f,1.6f);
            if(Phase.Value==GamePhase.Harbor||Phase.Value==GamePhase.Ready||Phase.Value==GamePhase.Results)Save();
        }
        // Opening a landed package lays its tools out on the deck around it, as cargo of the boat.
        void OpenPackage(NetPlayer player,NetItem entity)
        {
            if(entity.Falling||!player.OnBoat) {Tell(player.OwnerClientId,"Tunggu paket mendarat di kapal kecil.");Reject(player.OwnerClientId);return;}
            var package=Campaign.TakeFromDeck(entity.Uid);if(package==null)return;
            var at=entity.transform.position;DespawnItem(entity.Uid);
            for(int i=0;i<package.contents.Count;i++) {
                float angle=i*Mathf.PI*2/Mathf.Max(1,package.contents.Count);
                StoreAndSpawn(ItemCatalog.Create(package.contents[i]),BoatLayout.ClampToDeck(at+new Vector3(Mathf.Cos(angle)*.55f,.25f,Mathf.Sin(angle)*.55f)));
            }
            PlaySfx(Snd.Hatch,at,.8f,1.2f);SyncCampaign();
            Tell(player.OwnerClientId,"Paket dibuka: "+string.Join(", ",package.contents.Select(i=>ItemCatalog.Get(i).title))+".");
            if(Phase.Value==GamePhase.Harbor||Phase.Value==GamePhase.Ready||Phase.Value==GamePhase.Results)Save();
        }
        public void Noise(Vector3 position,float range) {foreach(var enemy in enemies)if(enemy!=null)enemy.Hear(position,range);}
        [Rpc(SendTo.ClientsAndHost,InvokePermission=RpcInvokePermission.Server)]void FlareRpc(Vector3 pos)
        {
            var go=WorldFactory.Shape("Flare menyala",PrimitiveType.Sphere,null,pos,Vector3.one*.2f,new Color(1,.3f,.08f),false,true);
            var light=go.AddComponent<Light>();light.type=LightType.Point;light.range=12;light.intensity=5;light.color=new Color(1,.24f,.08f);Destroy(go,SinkApp.Instance.balance.flareDuration);
        }
    }
}
