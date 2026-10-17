using UnityEngine;

namespace SinkScrapt
{
    // The wrecks the crew can salvage (product-owner change, 2026-10-16: a second, larger level). Stable byte values: the kind is
    // sent with the blueprint and in NetSession.Ship.
    public enum ShipKind : byte { Cargo, Cruise }

    // Everything that differs between the wrecks: the interior grid, the room rules, the sinking timeline and the expedition
    // length. ShipGrid and ShipTimeline read the active profile (`Current`), which is set from the blueprint being generated,
    // validated or built, and from NetSession.Ship on every peer. Only one wreck is ever loaded at a time.
    public sealed class ShipProfile
    {
        public ShipKind kind;
        // Player-facing names (Indonesian): the helm and cutscene title, and the short name used in hints ("kapal kargo").
        public string title, name;
        public int columns, rows, decks;
        public int minRooms, maxRooms, minRoomsPerDeck, maxRoomsPerDeck, minLocks, maxLocks;
        // Longest side of an ordinary room in cells (the cruise lobby is a fixed exception, see CruiseLayout).
        public int maxRoomSide;
        // Stair shafts between every pair of neighbouring decks.
        public int stairsPerPair;
        // Room types this ship's generator may use (indices into WorldFactory.RoomTitles).
        public int[] roomTypes;
        // Top-deck entries climb an airlock flight to the open deck (cargo ship); otherwise every entry is a flat vestibule.
        public bool flights;
        // Scrap per expedition (plus one per locked room) and hull stalkers at the start of every expedition.
        public int lootMin, lootMax, stalkers;
        // Scrap tables (ItemCatalog ids): common, mid-value and large items. Rooms of a type in `industrialRooms` use the cargo
        // ship's industrial tables instead (crew, storage and machinery rooms of the cruise ship's lowest deck).
        // `valuableLoot` is what waits behind a locked door when it is not a large item.
        public int[] commonLoot, midLoot, valuableLoot, largeLoot;
        public int[] industrialRooms=System.Array.Empty<int>();
        // One room in `hazardEvery` gets a seeded steam leak or live-electricity spot.
        public int hazardEvery;
        // Fraction of cells that get wall and floor dressing (decals, debris) and of corridor cells with a working lamp.
        public float decorDensity, corridorLamps;
        // Sinking: milestone times on the nominal clock (seconds) and the ship's drop at each (metres).
        public float nominal;
        public float[] times, depths;
        public float sinkStart, waterDeck2, waterDeck3, deckLevel;
        // The airlock flights' top (cargo deck), the open deck that is level with the small-boat deck at `deckLevel`, and the
        // highest point of the ship (it must be under water at the end).
        public float flightTop, exteriorDeck, shipTop;

        public float Duration(BalanceConfig balance) => kind==ShipKind.Cruise?balance.cruiseDuration:balance.duration;

        public static readonly ShipProfile Cargo=new() {
            kind=ShipKind.Cargo,title="KAPAL KARGO S-04",name="kapal kargo",
            columns=9,rows=13,decks=3,minRooms=24,maxRooms=30,minRoomsPerDeck=8,maxRoomsPerDeck=10,minLocks=2,maxLocks=4,maxRoomSide=3,stairsPerPair=2,
            roomTypes=new[]{0,1,2,3,4,5,6},flights=true,lootMin=18,lootMax=25,stalkers=1,
            commonLoot=new[]{0,1,2,3,4,5},midLoot=new[]{6,7,8},valuableLoot=new[]{7,8},largeLoot=new[]{9,10,11},hazardEvery=7,decorDensity=1,corridorLamps=.12f,
            nominal=600,times=new float[]{0,240,330,420,500,540,600},depths=new float[]{0,0,2,5,8,9,14.5f},
            sinkStart=240,waterDeck2=420,waterDeck3=500,deckLevel=540,flightTop=10,exteriorDeck=10,shipTop=14,
        };
        // The abandoned cruise ship: 13 x 30 cells (52 x 120 m) on five decks, 75-90 rooms, a fixed two-storey lobby on deck 2
        // behind the main door. 12 minutes: steady for 5, then sinking for 7. Deck 2 (the lobby and its gangway door) floods at
        // 7:30, the promenade deck (y 11) is level with the small boat at 9:45, and the funnel goes under at 12:00.
        public static readonly ShipProfile Cruise=new() {
            kind=ShipKind.Cruise,title="KAPAL PESIAR BINTANG SAMUDRA",name="kapal pesiar",
            columns=13,rows=30,decks=5,minRooms=75,maxRooms=90,minRoomsPerDeck=14,maxRoomsPerDeck=20,minLocks=4,maxLocks=7,maxRoomSide=5,stairsPerPair=3,
            roomTypes=new[]{1,2,3,4,6,7,8,9,10,11,12,13,14,15,16},flights=false,lootMin=46,lootMax=60,stalkers=2,
            // Passenger rooms: champagne, silver tray, casino chips; ship's bell, jewellery box; gramophone. Crew, galley,
            // storage and machinery rooms keep the cargo ship's industrial scrap.
            commonLoot=new[]{19,20,21},midLoot=new[]{22,23},valuableLoot=new[]{23},largeLoot=new[]{24},industrialRooms=new[]{1,2,3,4,5,6},hazardEvery=12,decorDensity=.45f,corridorLamps=.07f,
            nominal=720,times=new float[]{0,300,380,450,520,585,600,650,720},depths=new float[]{0,0,2.9f,5,8,10,11,14,27.5f},
            sinkStart=300,waterDeck2=450,waterDeck3=520,deckLevel=585,flightTop=16,exteriorDeck=11,shipTop=27,
        };
        public static ShipProfile Current { get; private set; }=Cargo;
        public static ShipProfile Of(ShipKind kind) => kind==ShipKind.Cruise?Cruise:Cargo;
        public static ShipProfile Use(ShipKind kind) => Current=Of(kind);
        public bool AllowsType(int type) => System.Array.IndexOf(roomTypes,type)>=0;
        // The scrap tables used for a room of this type (see `industrialRooms`).
        public ShipProfile LootFor(int roomType) => System.Array.IndexOf(industrialRooms,roomType)>=0?Cargo:this;
    }
}
