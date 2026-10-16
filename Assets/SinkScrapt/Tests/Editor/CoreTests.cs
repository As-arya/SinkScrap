using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace SinkScrapt.Tests
{
    public class CoreTests
    {
        [Test] public void EverySoundSynthesisesCleanly()
        {
            foreach(Snd id in Enum.GetValues(typeof(Snd)))
                for(int variant=0;variant<SoundBank.Variants(id);variant++) {
                    var clip=SoundBank.Get(id,variant);Assert.That(clip,Is.Not.Null,id+"."+variant);Assert.That(clip.length,Is.GreaterThan(.03f),id+"."+variant+" is too short");
                    // Long recorded beds stay Vorbis-compressed in memory and cannot be read back; their length is checked above.
                    if(clip.loadType!=AudioClipLoadType.DecompressOnLoad)continue;
                    var data=new float[clip.samples*clip.channels];Assert.That(clip.GetData(data,0),Is.True,id+"."+variant+" data");float peak=0;
                    foreach(var sample in data) {Assert.That(float.IsNaN(sample)||float.IsInfinity(sample),Is.False,id+"."+variant+" has a bad sample");peak=Mathf.Max(peak,Mathf.Abs(sample));}
                    // Vorbis decoding can ring a hair past a 0.95 peak.
                    Assert.That(peak,Is.InRange(.02f,SoundBank.HasRecording(id)?1.05f:1f),id+"."+variant+" peak "+peak);
                    // Loops must start and end at similar levels or they click when they wrap.
                    if(SoundBank.IsLoop(id))Assert.That(Mathf.Abs(data[0]-data[^1]),Is.LessThan(.35f),id+" loop seam");
                }
        }
        // The horror soundscape relies on the processed CC0 recordings (ArtSource/Audio/build_audio.py): surface footsteps,
        // the outside (sea, gulls, wind), the hull and the monsters' screams.
        [Test] public void RecordedSoundsCoverFootstepsSeaHullAndScreams()
        {
            foreach(var id in new[]{Snd.StepWood,Snd.StepMetal,Snd.StepGrate,Snd.StepGround,Snd.StepGravel,Snd.StepWet,Snd.Sea,Snd.Lap,Snd.Wind,Snd.Gull,Snd.RoomTone,
                Snd.Creak,Snd.Groan,Snd.Drip,Snd.DoorOpen,Snd.DoorClose,Snd.DoorOpenWood,Snd.DoorLocked,Snd.StalkerScream,Snd.DrownedScream,Snd.Stinger,Snd.Tension})
                Assert.That(SoundBank.HasRecording(id),Is.True,id+" has no recording in Resources/SinkScrapt/Audio");
            Assert.That(SoundBank.Variants(Snd.StepMetal),Is.GreaterThanOrEqualTo(4),"Footsteps need several variants");
            Assert.That(SoundBank.Variants(Snd.Gull),Is.GreaterThanOrEqualTo(4),"Gull calls need several variants");
        }
        [Test] public void ThousandSeedsHaveConnectedThreeDeckLayouts()
        {
            var shapes=new System.Collections.Generic.HashSet<string>();
            for(int seed=0;seed<1000;seed++) {
                var layout=InteriorGenerator.Generate(seed);Assert.That(layout.Problem(),Is.Null,"seed "+seed);Assert.That(layout.fallback,Is.False,"seed "+seed);
                // The packed blueprint travels in one FixedString4096Bytes network variable.
                Assert.That(System.Text.Encoding.UTF8.GetByteCount(layout.Pack()),Is.LessThan(4000),"seed "+seed);
                Assert.That(layout.entries[0].floor,Is.EqualTo(ShipGrid.Decks-1),"entry A on the top deck, seed "+seed);
                Assert.That(layout.doors.Count(d=>d.locked),Is.InRange(1,4),"locked doors, seed "+seed);
                // Runtime QA walks a monster down five corridor cells on deck 2 and puts a swimmer three cells along deck 1.
                Assert.That(layout.StraightRun(1,5,out _,out _,out _),Is.True,"deck 2 run, seed "+seed);Assert.That(layout.StraightRun(0,3,out _,out _,out _),Is.True,"deck 1 run, seed "+seed);
                shapes.Add(string.Join("|",layout.corridors));
            }
            Assert.That(shapes.Count,Is.GreaterThan(900),"corridor networks should differ between seeds");
        }
        [TestCase(-1)][TestCase(int.MinValue)][TestCase(int.MaxValue)]public void GeneratorAcceptsBoundarySeeds(int seed){Assert.That(InteriorGenerator.Generate(seed).Validate(),Is.True);}
        [Test]public void FallbackSeedGeneratesDirectly(){Assert.That(InteriorGenerator.Generate(InteriorGenerator.FallbackSeed).fallback,Is.False);}
        [Test]public void SameSeedReproducesBlueprint(){Assert.That(JsonUtility.ToJson(InteriorGenerator.Generate(901)),Is.EqualTo(JsonUtility.ToJson(InteriorGenerator.Generate(901))));Assert.That(JsonUtility.ToJson(InteriorGenerator.Generate(902)),Is.Not.EqualTo(JsonUtility.ToJson(InteriorGenerator.Generate(901))));}
        [Test]public void InvalidDisconnectedOrOverlappingLayoutIsRejected()
        {
            var layout=InteriorGenerator.Generate(91);var other=layout.rooms.Find(r=>r.floor==layout.rooms[0].floor&&r.id!=0);other.x=layout.rooms[0].x;other.z=layout.rooms[0].z;Assert.That(layout.Validate(),Is.False,"overlap");
            layout=InteriorGenerator.Generate(91);layout.doors.Clear();Assert.That(layout.Validate(),Is.False,"no doors");
            layout=InteriorGenerator.Generate(91);layout.stairs.RemoveAt(0);Assert.That(layout.Validate(),Is.False,"missing stair");
            layout=InteriorGenerator.Generate(91);var a=layout.entries[0];int cell=a.x+a.z*ShipGrid.Columns;layout.corridors[a.floor]=layout.corridors[a.floor].Remove(cell,1).Insert(cell,"0");Assert.That(layout.Validate(),Is.False,"entry off the corridor");
            layout=InteriorGenerator.Generate(91);layout.entries[0].floor=0;Assert.That(layout.Validate(),Is.False,"entry A below the top deck");
            layout=InteriorGenerator.Generate(91);layout.entries.RemoveAt(2);Assert.That(layout.Validate(),Is.False,"two entries");
            layout=InteriorGenerator.Generate(91);var shared=layout.doors.FindIndex(d=>!d.locked&&layout.DoorRoom(layout.doors.IndexOf(d))!=null&&layout.DoorCount(layout.DoorRoom(layout.doors.IndexOf(d)))>1);
            if(shared>=0){layout.doors[shared].locked=true;Assert.That(layout.Validate(),Is.False,"locked door that is not a dead end");}
            layout=InteriorGenerator.Generate(91);layout.version=2;Assert.That(layout.Validate(),Is.False,"old generator version");
        }
        // Walls are built from either side of an edge, so openness must not depend on which side asks; the hull edge is closed
        // everywhere except the three entry cells.
        [Test]public void OpeningsAreSymmetricAndTheHullIsSealed()
        {
            for(int seed=0;seed<200;seed++) {
                var layout=InteriorGenerator.Generate(seed);int airlocks=0;
                for(int f=0;f<ShipGrid.Decks;f++)for(int z=0;z<ShipGrid.Rows;z++)for(int x=0;x<ShipGrid.Columns;x++)for(int dir=0;dir<4;dir++) {
                    var n=new Vector2Int(x,z)+ShipGrid.Dirs[dir];bool open=layout.Open(f,x,z,dir);
                    if(ShipGrid.Inside(n.x,n.y)) {
                        Assert.That(layout.Open(f,n.x,n.y,ShipGrid.Opposite(dir)),Is.EqualTo(open),"seed "+seed);
                        if(open)Assert.That(layout.Kind(f,x,z,out _)!=CellKind.Void&&layout.Kind(f,n.x,n.y,out _)!=CellKind.Void,"opening into void, seed "+seed);
                    } else if(open)airlocks++;
                }
                Assert.That(airlocks,Is.EqualTo(InteriorLayout.EntryCount),"seed "+seed);
                // B and C never share a deck, so at least one entry leads below the top deck.
                Assert.That(layout.entries[1].floor,Is.Not.EqualTo(layout.entries[2].floor),"seed "+seed);
            }
        }
        // 10 minutes: steady for 4, then six minutes of sinking. A longer configured duration stretches the same curve.
        [Test]public void TimelineHitsMilestonesAndNeverReverses()
        {
            float previous=-1;for(int i=0;i<=600;i++){float depth=ShipTimeline.Depth(i);Assert.That(depth,Is.GreaterThanOrEqualTo(previous));previous=depth;}
            Assert.That(ShipTimeline.Depth(ShipTimeline.SinkStart),Is.EqualTo(0).Within(.001));Assert.That(ShipTimeline.Depth(200),Is.EqualTo(0).Within(.001));
            Assert.That(ShipTimeline.Depth(ShipTimeline.WaterDeck2),Is.EqualTo(ShipTimeline.FloorHeight(1)).Within(.001));Assert.That(ShipTimeline.Depth(ShipTimeline.WaterDeck3),Is.EqualTo(ShipTimeline.FloorHeight(2)).Within(.001));
            Assert.That(ShipTimeline.CargoDeck-ShipTimeline.Depth(ShipTimeline.DeckLevel),Is.EqualTo(ShipTimeline.BoatDeck).Within(.001));Assert.That(ShipTimeline.ShipTop-ShipTimeline.Depth(600),Is.LessThan(0));
            Assert.That(ShipTimeline.Depth(900,900),Is.EqualTo(ShipTimeline.Depth(600)).Within(.001));Assert.That(ShipTimeline.Depth(810,900),Is.EqualTo(ShipTimeline.Depth(540)).Within(.001));
            // The lowest deck starts waist-deep; the flood overtakes the bilge and then follows the sinking curve.
            Assert.That(ShipTimeline.InteriorWater(ShipTimeline.Depth(0)),Is.EqualTo(ShipTimeline.FloorHeight(0)+.9f).Within(.001));
            Assert.That(ShipTimeline.InteriorWater(ShipTimeline.Depth(ShipTimeline.DeckLevel)),Is.EqualTo(9).Within(.001));
            previous=-1;for(int i=0;i<=600;i++){float water=ShipTimeline.InteriorWater(ShipTimeline.Depth(i));Assert.That(water,Is.GreaterThanOrEqualTo(previous));previous=water;}
            Assert.That(ScriptableObject.CreateInstance<BalanceConfig>().duration,Is.EqualTo(600));
        }
        [Test]public void BlueprintPacksAndUnpacksExactly()
        {
            var layout=InteriorGenerator.Generate(4242);var back=InteriorLayout.Unpack(layout.Pack());
            Assert.That(JsonUtility.ToJson(back),Is.EqualTo(JsonUtility.ToJson(layout)));Assert.That(back.Validate(),Is.True);
        }
        [Test]public void BatteryRefillsTheTorchAndKeysAreNeverCargo()
        {
            var inv=new InventoryData();var torch=ItemCatalog.Create(ItemCatalog.Flashlight);Assert.That(torch.charge,Is.EqualTo(1));
            inv.items.Add(torch);Assert.That(inv.InsertBattery(),Is.Null,"no battery");
            var battery=ItemCatalog.Create(ItemCatalog.Battery);inv.items.Add(battery);inv.selected=1;
            Assert.That(inv.InsertBattery(),Is.Null,"torch already full");torch.charge=.1f;
            Assert.That(inv.InsertBattery().uid,Is.EqualTo(battery.uid));Assert.That(torch.charge,Is.EqualTo(1));Assert.That(inv.items.Count,Is.EqualTo(1));Assert.That(inv.selected,Is.EqualTo(0));
            var state=new CampaignState();Assert.That(state.StoreOnDeck(ItemCatalog.Create(ItemCatalog.Key),new Vector3(25,1.3f,0),0),Is.False);
            Assert.That(state.Order(new[]{ItemCatalog.Key},30),Is.Null);Assert.That(state.Order(new[]{ItemCatalog.Battery},30),Is.Not.Null);Assert.That(state.credits,Is.EqualTo(110));
            var bad=new CampaignState();var drained=ItemCatalog.Create(ItemCatalog.Flashlight);drained.charge=1.5f;bad.StoreOnDeck(drained,new Vector3(25,1.3f,0),0);Assert.Throws<InvalidDataException>(()=>bad.Validate());
        }
        [Test]public void VersionTwoSaveGetsChargedFlashlights()
        {
            var folder=Path.Combine(Application.dataPath,"../Library/SinkScraptTests",Guid.NewGuid().ToString("N"));var path=Path.Combine(folder,"campaign.json");
            try {
                Directory.CreateDirectory(folder);
                var old=new CampaignState{version=2};var torch=ItemCatalog.Create(ItemCatalog.Flashlight);torch.charge=0;old.cargo.Add(torch);torch.at=new Vector3(25,1.3f,0);
                var carried=ItemCatalog.Create(ItemCatalog.Flashlight);carried.charge=0;old.loadouts.Add(new CrewLoadout{profile="p",items={carried}});File.WriteAllText(path,JsonUtility.ToJson(old));
                var loaded=CampaignSave.Read(path,out _);Assert.That(loaded.version,Is.EqualTo(CampaignState.Version));
                Assert.That(loaded.cargo[0].charge,Is.EqualTo(1));Assert.That(loaded.loadouts[0].items[0].charge,Is.EqualTo(1));Assert.That(loaded.cargo[0].at,Is.EqualTo(new Vector3(25,1.3f,0)));
            }
            finally {if(Directory.Exists(folder))Directory.Delete(folder,true);}
        }
        [Test]public void OrdersRespectMoneyAndPackageSizeAndDoNotReduceQuotaProgress()
        {
            var state=new CampaignState{sold=450};var order=state.Order(new[]{12},30);Assert.That(order,Is.Not.Null);Assert.That(state.credits,Is.EqualTo(105));Assert.That(state.sold,Is.EqualTo(450));
            Assert.That(order.remaining,Is.EqualTo(30));Assert.That(state.orders.Count,Is.EqualTo(1));
            state.credits=0;Assert.That(state.Order(new[]{14},30),Is.Null);state.credits=500;
            Assert.That(state.Order(new[]{12,12,12,12,12},30),Is.Null,"over-full package");Assert.That(state.Order(new[]{0},30),Is.Null,"scrap is not sold");Assert.That(state.Order(new int[0],30),Is.Null);
            Assert.That(state.credits,Is.EqualTo(500));
            var package=state.Package(order);Assert.That(package.Definition.kind,Is.EqualTo(ItemKind.Package));Assert.That(package.contents,Is.EqualTo(new List<int>{12}));Assert.That(state.orders,Is.Empty);
        }
        [Test]public void BeltSlotHoldsATorchOrRadioAndTheTorchStaysTheActiveOne()
        {
            var inv=new InventoryData();var torch=ItemCatalog.Create(ItemCatalog.Flashlight);var scrap=ItemCatalog.Create(9);
            inv.items.Add(torch);inv.items.Add(scrap);inv.selected=0;
            Assert.That(inv.SwapUtility(),Is.True);Assert.That(inv.utility,Is.SameAs(torch));Assert.That(inv.items.Count,Is.EqualTo(1));
            Assert.That(inv.Torch,Is.SameAs(torch),"belt torch is active with a two-handed item in hand");
            inv.selected=0;Assert.That(inv.SwapUtility(),Is.False,"belt item cannot come into a hand holding a two-handed item");
            inv.items.Clear();Assert.That(inv.SwapUtility(),Is.True);Assert.That(inv.HasUtility,Is.False);Assert.That(inv.items[0],Is.SameAs(torch));
            var json=JsonUtility.FromJson<InventoryData>(JsonUtility.ToJson(inv));json.Normalize();Assert.That(json.HasUtility,Is.False,"an empty belt slot survives serialization as empty");
        }
        [Test]public void DeckCargoIsStoredOnceAndSellIsIdempotentAndGearCannotBeSoldAsScrap()
        {
            var state=new CampaignState();var item=ItemCatalog.Create(11);var at=new Vector3(25,1.3f,-2);
            Assert.That(state.StoreOnDeck(item,at,90),Is.True);Assert.That(state.StoreOnDeck(item,at,90),Is.False);Assert.That(state.cargo[0].at,Is.EqualTo(at));Assert.That(state.cargo[0].yaw,Is.EqualTo(90));
            state.StoreOnDeck(ItemCatalog.Create(12),at,0);Assert.That(state.Sell(new[]{item.uid,item.uid}),Is.EqualTo(150));Assert.That(state.Sell(new[]{item.uid}),Is.Zero);Assert.That(state.credits,Is.EqualTo(270));Assert.That(state.cargo.Count,Is.EqualTo(1));
        }
        [Test]public void ExpeditionEndKeepsCargoButAWipeLosesTheWholeBoat()
        {
            var state=new CampaignState();state.StoreOnDeck(ItemCatalog.Create(9),new Vector3(25,1.3f,0),0);state.StoreOnDeck(ItemCatalog.Create(12),BoatLayout.RackSlot(1),0,1);state.Order(new[]{16},30);
            state.FinishExpedition();Assert.That(state.cargo.Count,Is.EqualTo(2));Assert.That(state.expeditionsUsed,Is.EqualTo(1));
            state.LoseBoatCargo();Assert.That(state.cargo,Is.Empty,"deck and rack are lost");Assert.That(state.orders.Count,Is.EqualTo(1),"paid orders still arrive");
        }
        [Test]public void DeckCargoCanBeTakenOnlyOnce()
        {
            var state=new CampaignState();var gear=ItemCatalog.Create(14);state.StoreOnDeck(gear,new Vector3(22,1.3f,0),0);
            var taken=state.TakeFromDeck(gear.uid);Assert.That(taken,Is.Not.Null);Assert.That(taken.uid,Is.EqualTo(gear.uid));Assert.That(taken.rack,Is.Zero);
            Assert.That(state.TakeFromDeck(gear.uid),Is.Null);Assert.That(state.cargo,Is.Empty);
        }
        [Test]public void ToolRackTakesOnlyToolsOnePerSlot()
        {
            var state=new CampaignState();
            Assert.That(state.StoreOnDeck(ItemCatalog.Create(3),BoatLayout.RackSlot(1),0,1),Is.False,"scrap on the rack");
            Assert.That(state.StoreOnDeck(ItemCatalog.Create(12),BoatLayout.RackSlot(1),0,1),Is.True);
            Assert.That(state.StoreOnDeck(ItemCatalog.Create(13),BoatLayout.RackSlot(1),0,1),Is.False,"occupied slot");
            Assert.That(state.StoreOnDeck(ItemCatalog.Create(13),Vector3.zero,0,BoatLayout.RackSlots+1),Is.False,"slot out of range");
            Assert.That(state.FreeRackSlot(),Is.EqualTo(2));
            for(int n=2;n<=BoatLayout.RackSlots;n++)state.StoreOnDeck(ItemCatalog.Create(15),BoatLayout.RackSlot(n),0,n);
            Assert.That(state.FreeRackSlot(),Is.Zero);Assert.DoesNotThrow(()=>state.Validate());
            for(int n=1;n<=BoatLayout.RackSlots;n++)Assert.That(BoatLayout.OnDeck(BoatLayout.RackSlot(n)),Is.True,"rack slot "+n+" is aboard");
        }
        [Test]public void DropsAreClampedInsideTheRail()
        {
            Assert.That(BoatLayout.OnDeck(BoatLayout.ClampToDeck(new Vector3(19,1.5f,0))),Is.True);Assert.That(BoatLayout.ClampToDeck(new Vector3(35,0,9)).x,Is.LessThan(BoatLayout.MaxX));
            for(int i=0;i<200;i++)Assert.That(BoatLayout.OnDeck(BoatLayout.StowPosition(i)),Is.True,"stow "+i);
        }
        [Test]public void VersionOneSaveMovesChestCargoOntoTheDeck()
        {
            var folder=Path.Combine(Application.dataPath,"../Library/SinkScraptTests",Guid.NewGuid().ToString("N"));var path=Path.Combine(folder,"campaign.json");
            try {
                Directory.CreateDirectory(folder);
                var old=new CampaignState{version=1,credits=333};old.cargo.Add(ItemCatalog.Create(8));old.cargo.Add(ItemCatalog.Create(13));File.WriteAllText(path,JsonUtility.ToJson(old));
                var loaded=CampaignSave.Read(path,out _);Assert.That(loaded.version,Is.EqualTo(CampaignState.Version));Assert.That(loaded.credits,Is.EqualTo(333));Assert.That(loaded.cargo.Count,Is.EqualTo(2));
                Assert.That(loaded.cargo.All(i=>i.rack==0&&BoatLayout.OnDeck(i.at)),Is.True);Assert.That(loaded.cargo[0].at,Is.Not.EqualTo(loaded.cargo[1].at));
            }
            finally {if(Directory.Exists(folder))Directory.Delete(folder,true);}
        }
        [Test]public void QuotaCannotEndEarlyAndSuccessKeepsMoneyCargo()
        {
            var state=new CampaignState{sold=600,credits=777};state.cargo.Add(ItemCatalog.Create(8));Assert.Throws<InvalidOperationException>(()=>state.ResolveQuota());state.expeditionsUsed=3;
            Assert.That(state.ResolveQuota(),Is.True);Assert.That(state.quota,Is.EqualTo(810));Assert.That(state.credits,Is.EqualTo(777));Assert.That(state.cargo.Count,Is.EqualTo(1));Assert.That(state.sold,Is.Zero);Assert.That(state.expeditionsUsed,Is.Zero);
        }
        [Test]public void FailedQuotaResetsOnlyWhenResolved(){var state=new CampaignState{credits=900,sold=599,expeditionsUsed=3};state.cargo.Add(ItemCatalog.Create(8));Assert.That(state.ResolveQuota(),Is.False);Assert.That(state.credits,Is.EqualTo(120));Assert.That(state.quota,Is.EqualTo(600));Assert.That(state.cargo,Is.Empty);}
        [Test]public void TankRefillConsumesCreditsAndCannotBeRepeatedWhenFull(){var state=new CampaignState();var inv=new InventoryData();var tank=ItemCatalog.Create(14);tank.oxygen=10;inv.items.Add(tank);Assert.That(state.Refill(inv),Is.True);Assert.That(tank.oxygen,Is.EqualTo(90));Assert.That(state.credits,Is.EqualTo(100));Assert.That(state.Refill(inv),Is.False);}
        [Test]public void SaveRecoversBackupAndRejectsDuplicateItemIdentity()
        {
            var folder=Path.Combine(Application.dataPath,"../Library/SinkScraptTests",Guid.NewGuid().ToString("N"));var path=Path.Combine(folder,"campaign.json");
            try {var state=new CampaignState();state.StoreOnDeck(ItemCatalog.Create(3),new Vector3(25,1.3f,0),0);CampaignSave.Write(state,path);state.credits=300;CampaignSave.Write(state,path);File.WriteAllText(path,"broken");var loaded=CampaignSave.Read(path,out var notice);Assert.That(loaded.credits,Is.EqualTo(120));Assert.That(loaded.cargo.Count,Is.EqualTo(1));Assert.That(notice,Does.Contain("cadangan"));state.cargo.Add(state.cargo[0].Copy());Assert.Throws<InvalidDataException>(()=>state.Validate());File.WriteAllText(path+".bak","broken");Assert.Throws<InvalidDataException>(()=>CampaignSave.Read(path,out _));}
            finally {if(Directory.Exists(folder))Directory.Delete(folder,true);}
        }
    }
}
