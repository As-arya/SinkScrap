using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace SinkScrapt.Tests
{
    // The abandoned cruise ship (ShipProfile.Cruise, CruiseLayout): five decks, 75-90 rooms, the fixed two-storey lobby behind
    // main door A, emergency exits B and C on upper decks, and a 12-minute timeline that sinks over the last seven.
    public class CruiseTests
    {
        [TearDown] public void Restore() => ShipProfile.Use(ShipKind.Cargo);

        [Test] public void ThousandSeedsHaveConnectedFiveDeckCruiseLayouts()
        {
            var shapes=new System.Collections.Generic.HashSet<string>();int fallbacks=0;
            for(int seed=0;seed<1000;seed++) {
                var layout=InteriorGenerator.Generate(seed,ShipKind.Cruise);
                Assert.That(layout.Problem(),Is.Null,"seed "+seed);if(layout.fallback)fallbacks++;
                Assert.That(layout.ship,Is.EqualTo(ShipKind.Cruise));Assert.That(layout.version,Is.EqualTo(InteriorLayout.CruiseGeneratorVersion));
                Assert.That(ShipGrid.Decks,Is.EqualTo(5));Assert.That(layout.corridors.Count,Is.EqualTo(5));
                Assert.That(layout.rooms.Count,Is.InRange(75,90),"rooms, seed "+seed);
                // The packed blueprint still travels in one FixedString4096Bytes network variable.
                Assert.That(System.Text.Encoding.UTF8.GetByteCount(layout.Pack()),Is.LessThan(4000),"packed size, seed "+seed);
                // The lobby is fixed: room 0, 6 x 7 cells on deck 2, main door A in its hull wall, three open arches.
                var lobby=layout.Atrium;
                Assert.That(lobby.id==0&&lobby.floor==1&&lobby.x==CruiseLayout.LobbyX&&lobby.z==CruiseLayout.LobbyZ&&lobby.w==6&&lobby.d==7&&lobby.type==CruiseLayout.Lobby,Is.True,"lobby, seed "+seed);
                Assert.That(layout.entries[0].floor==1&&lobby.Contains(layout.entries[0].x,layout.entries[0].z),Is.True,"entry A in the lobby, seed "+seed);
                Assert.That(layout.doors.Count(d=>d.arch),Is.EqualTo(3),"arches, seed "+seed);
                Assert.That(layout.entries[1].floor,Is.GreaterThan(1));Assert.That(layout.entries[2].floor,Is.GreaterThan(1));Assert.That(layout.entries[1].floor,Is.Not.EqualTo(layout.entries[2].floor));
                Assert.That(layout.doors.Count(d=>d.locked),Is.InRange(4,7),"locked doors, seed "+seed);
                Assert.That(layout.stairs.Count,Is.EqualTo(12),"three shafts between each pair of decks, seed "+seed);
                // Runtime QA walks a monster down five corridor cells on deck 2 and puts a swimmer three cells along deck 1.
                Assert.That(layout.StraightRun(1,5,out _,out _,out _),Is.True,"deck 2 run, seed "+seed);Assert.That(layout.StraightRun(0,3,out _,out _,out _),Is.True,"deck 1 run, seed "+seed);
                // Room themes follow the decks: machinery and crew below, passenger cabins above.
                Assert.That(layout.rooms.Where(r=>r.floor==0).All(r=>new[]{1,2,3,4,6,CruiseLayout.Clinic,CruiseLayout.Laundry}.Contains(r.type)),Is.True,"deck 1 themes, seed "+seed);
                Assert.That(layout.rooms.Count(r=>r.type==CruiseLayout.Cabin||r.type==CruiseLayout.Suite),Is.GreaterThan(15),"passenger cabins, seed "+seed);
                shapes.Add(string.Join("|",layout.corridors));
            }
            Assert.That(fallbacks,Is.EqualTo(0),"no seed needed the fallback layout");
            Assert.That(shapes.Count,Is.GreaterThan(950),"corridor networks should differ between seeds");
        }
        // Openness never depends on which side asks; the hull is closed except at the three entries; nothing opens into the
        // atrium's open air, and its cells have no deck slab under them.
        [Test] public void CruiseOpeningsAreSymmetricAndTheHullAndAtriumAreSealed()
        {
            for(int seed=0;seed<200;seed++) {
                var layout=InteriorGenerator.Generate(seed,ShipKind.Cruise);int outside=0,atrium=0;
                for(int f=0;f<ShipGrid.Decks;f++)for(int z=0;z<ShipGrid.Rows;z++)for(int x=0;x<ShipGrid.Columns;x++) {
                    var kind=layout.Kind(f,x,z,out _);
                    if(kind==CellKind.Atrium) {atrium++;Assert.That(layout.HasFloor(f,x,z),Is.False,"atrium slab, seed "+seed);}
                    for(int dir=0;dir<4;dir++) {
                        var n=new Vector2Int(x,z)+ShipGrid.Dirs[dir];bool open=layout.Open(f,x,z,dir);
                        if(ShipGrid.Inside(n.x,n.y)) {
                            Assert.That(layout.Open(f,n.x,n.y,ShipGrid.Opposite(dir)),Is.EqualTo(open),"seed "+seed);
                            if(open)Assert.That(kind!=CellKind.Void&&kind!=CellKind.Atrium&&layout.Kind(f,n.x,n.y,out _)!=CellKind.Void&&layout.Kind(f,n.x,n.y,out _)!=CellKind.Atrium,"opening into void or atrium, seed "+seed);
                        } else if(open)outside++;
                    }
                }
                Assert.That(outside,Is.EqualTo(InteriorLayout.EntryCount),"seed "+seed);
                Assert.That(atrium,Is.EqualTo(42),"the atrium is the lobby's 6 x 7 footprint one deck up, seed "+seed);
            }
        }
        [Test] public void CruiseBlueprintIsDeterministicAndRoundTrips()
        {
            Assert.That(JsonUtility.ToJson(InteriorGenerator.Generate(901,ShipKind.Cruise)),Is.EqualTo(JsonUtility.ToJson(InteriorGenerator.Generate(901,ShipKind.Cruise))));
            Assert.That(JsonUtility.ToJson(InteriorGenerator.Generate(902,ShipKind.Cruise)),Is.Not.EqualTo(JsonUtility.ToJson(InteriorGenerator.Generate(901,ShipKind.Cruise))));
            var layout=InteriorGenerator.Generate(4242,ShipKind.Cruise);var back=InteriorLayout.Unpack(layout.Pack());
            Assert.That(JsonUtility.ToJson(back),Is.EqualTo(JsonUtility.ToJson(layout)));Assert.That(back.Validate(),Is.True);
            // The same seed on the two wrecks gives two different, valid blueprints, and the cargo one is unchanged by the cruise.
            var cargo=InteriorGenerator.Generate(4242);Assert.That(cargo.ship,Is.EqualTo(ShipKind.Cargo));Assert.That(cargo.Validate(),Is.True);Assert.That(ShipGrid.Decks,Is.EqualTo(3));
            Assert.That(InteriorGenerator.Generate(InteriorGenerator.FallbackSeed,ShipKind.Cruise).fallback,Is.False);
            foreach(int seed in new[]{-1,int.MinValue,int.MaxValue})Assert.That(InteriorGenerator.Generate(seed,ShipKind.Cruise).Validate(),Is.True,"seed "+seed);
        }
        [Test] public void BrokenCruiseBlueprintsAreRejected()
        {
            InteriorLayout Fresh() => InteriorGenerator.Generate(77,ShipKind.Cruise);
            var layout=Fresh();var a=layout.Atrium;int cell=a.x+a.z*ShipGrid.Columns;layout.corridors[a.floor+1]=layout.corridors[a.floor+1].Remove(cell,1).Insert(cell,"1");
            Assert.That(layout.Problem(),Is.EqualTo("atrium overlap"));
            layout=Fresh();layout.entries[0].floor=2;Assert.That(layout.Validate(),Is.False,"entry A outside the lobby");
            layout=Fresh();layout.entries[2].floor=layout.entries[1].floor;Assert.That(layout.Validate(),Is.False,"emergency exits on one deck");
            layout=Fresh();layout.doors.First(d=>d.arch).locked=true;Assert.That(layout.Validate(),Is.False,"locked arch");
            layout=Fresh();layout.atrium=-1;Assert.That(layout.Validate(),Is.False,"no lobby atrium");
            layout=Fresh();layout.rooms[5].type=0;Assert.That(layout.Validate(),Is.False,"cargo hold on the cruise ship");
            layout=Fresh();layout.version=InteriorLayout.GeneratorVersion+5;Assert.That(layout.Validate(),Is.False,"wrong generator version");
            layout=Fresh();layout.ship=ShipKind.Cargo;Assert.That(layout.Validate(),Is.False,"a cruise blueprint read as a cargo ship");
            var cargo=InteriorGenerator.Generate(77);cargo.doors[0].arch=true;Assert.That(cargo.Validate(),Is.False,"arches are cruise-only");
        }
        // 12 minutes: steady for 5, then seven minutes of sinking, with the flood reaching each deck in turn.
        [Test] public void CruiseTimelineHoldsFiveMinutesThenSinksForSeven()
        {
            ShipProfile.Use(ShipKind.Cruise);var balance=ScriptableObject.CreateInstance<BalanceConfig>();
            Assert.That(balance.cruiseDuration,Is.EqualTo(720));Assert.That(ShipProfile.Cruise.Duration(balance),Is.EqualTo(720));Assert.That(ShipProfile.Cargo.Duration(balance),Is.EqualTo(600));
            Assert.That(ShipTimeline.Nominal,Is.EqualTo(720));Assert.That(ShipTimeline.SinkStart,Is.EqualTo(300));
            float previous=-1;for(int i=0;i<=720;i++){float depth=ShipTimeline.Depth(i,720);Assert.That(depth,Is.GreaterThanOrEqualTo(previous),"t "+i);previous=depth;}
            Assert.That(ShipTimeline.Depth(299,720),Is.EqualTo(0).Within(.001));Assert.That(ShipTimeline.Depth(300,720),Is.EqualTo(0).Within(.001));Assert.That(ShipTimeline.Depth(330,720),Is.GreaterThan(0));
            for(int deck=1;deck<5;deck++)Assert.That(ShipTimeline.Depth(new[]{0,450,520,600,650}[deck],720),Is.EqualTo(ShipTimeline.FloorHeight(deck)).Within(.001),"water reaches deck "+(deck+1));
            Assert.That(ShipTimeline.Depth(ShipTimeline.WaterDeck2,720),Is.EqualTo(ShipTimeline.FloorHeight(1)).Within(.001));
            Assert.That(ShipTimeline.ExteriorDeck-ShipTimeline.Depth(ShipTimeline.DeckLevel,720),Is.EqualTo(ShipTimeline.BoatDeck).Within(.001),"promenade level with the boat");
            Assert.That(ShipTimeline.ShipTop-ShipTimeline.Depth(720,720),Is.LessThan(0),"funnel under at 12:00");
            // A longer configured duration stretches the same curve.
            Assert.That(ShipTimeline.Depth(900,900),Is.EqualTo(ShipTimeline.Depth(720,720)).Within(.001));Assert.That(ShipTimeline.Depth(ShipTimeline.At(450,900),900),Is.EqualTo(5).Within(.001));
            Assert.That(ShipTimeline.InteriorWater(ShipTimeline.Depth(0,720)),Is.EqualTo(ShipTimeline.FloorHeight(0)+.9f).Within(.001));
            previous=-1;for(int i=0;i<=720;i++){float water=ShipTimeline.InteriorWater(ShipTimeline.Depth(i,720));Assert.That(water,Is.GreaterThanOrEqualTo(previous));previous=water;}
        }

        // Cruise scrap (ids 19-24) drops in the cruise ship's passenger rooms only; its crew and machinery rooms and the cargo
        // ship keep the industrial scrap. Every table holds scrap worth 20-150, and a deck of cruise scrap saves and loads.
        [Test] public void CruiseScrapStaysOnTheCruiseShipAndSaves()
        {
            int[] cruise={19,20,21,22,23,24};
            foreach(var profile in new[]{ShipProfile.Cargo,ShipProfile.Cruise})
                foreach(var table in new[]{profile.commonLoot,profile.midLoot,profile.valuableLoot,profile.largeLoot})
                    foreach(int id in table) {
                        var def=ItemCatalog.Get(id);Assert.That(def!=null&&def.kind==ItemKind.Scrap&&def.value>=20&&def.value<=150,Is.True,"item "+id);
                        Assert.That(cruise.Contains(id),Is.EqualTo(profile==ShipProfile.Cruise),"item "+id+" on "+profile.name);
                    }
            Assert.That(ShipProfile.Cruise.largeLoot.All(id=>ItemCatalog.Get(id).twoHands),Is.True);
            Assert.That(ShipProfile.Cruise.LootFor(CruiseLayout.Casino),Is.SameAs(ShipProfile.Cruise));
            Assert.That(ShipProfile.Cruise.LootFor(4),Is.SameAs(ShipProfile.Cargo),"engine room");
            Assert.That(ShipProfile.Cargo.LootFor(4),Is.SameAs(ShipProfile.Cargo));
            foreach(int id in cruise)Assert.That(ModelLibrary.ItemModels[id],Does.StartWith("scrap_"));
            var folder=System.IO.Path.Combine(Application.dataPath,"../Library/SinkScraptTests",System.Guid.NewGuid().ToString("N"));var path=System.IO.Path.Combine(folder,"campaign.json");
            try {
                var state=new CampaignState();foreach(int id in cruise)Assert.That(state.StoreOnDeck(ItemCatalog.Create(id),new Vector3(22+id%6,1.3f,0),0),Is.True);
                CampaignSave.Write(state,path);var loaded=CampaignSave.Read(path,out _);
                Assert.That(loaded.cargo.Select(i=>i.definitionId),Is.EqualTo(cruise));
            }
            finally {if(System.IO.Directory.Exists(folder))System.IO.Directory.Delete(folder,true);}
        }
    }
}
