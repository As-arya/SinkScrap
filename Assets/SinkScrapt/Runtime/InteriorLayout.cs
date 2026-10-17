using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SinkScrapt
{
    // The wreck interior is a sealed grid of 4 m cells (three decks on the cargo ship, five on the cruise ship). Every cell
    // belongs to exactly one module: the shared corridor network, a room, a stair shaft (which spans two decks), the open air of
    // the cruise lobby's atrium (the deck above the lobby, never walkable), or solid void. Walls stand on every edge between
    // different modules unless the blueprint declares an opening there, so the physical geometry and the logical graph come
    // from the same data and nothing outside the modules is reachable.
    public static class ShipGrid
    {
        // Grid size of the active wreck (ShipProfile.Current): 9 x 13 x 3 on the cargo ship, 13 x 30 x 5 on the cruise ship.
        public static int Columns => ShipProfile.Current.columns;
        public static int Rows => ShipProfile.Current.rows;
        public static int Decks => ShipProfile.Current.decks;
        // Room doorways take one 1.04 m leaf (InteriorDoor); 1.1 m still clears the hull stalker's 0.84 m body on the
        // navigation line through the door centre.
        public const float Cell=4, Wall=.2f, DoorWidth=1.1f, DoorHeight=2.25f;
        public static float HalfWidth => Columns*Cell*.5f;
        public static float HalfLength => Rows*Cell*.5f;
        // +x, +z, -x, -z
        public static readonly Vector2Int[] Dirs={new(1,0),new(0,1),new(-1,0),new(0,-1)};
        public static float X(int column) => (column-(Columns-1)*.5f)*Cell;
        public static float Z(int row) => (row-(Rows-1)*.5f)*Cell;
        public static Vector3 Center(int deck,int column,int row) => new(X(column),ShipTimeline.FloorHeight(deck),Z(row));
        public static bool Inside(int column,int row) => column>=0&&column<Columns&&row>=0&&row<Rows;
        public static int Opposite(int dir) => (dir+2)%4;
        // Stair shafts are two cells long and rise one deck; airlock flights rise from the top deck to the cargo deck. Entries on
        // the lower decks are flat hatch vestibules, HatchLength deep.
        public const float StairRun=6, AirlockRun=4, AirlockLength=10, AirlockEntry=7, HatchLength=4, HatchEntry=2.2f;
        // Whether cell (x,z) lies on the hull edge facing `dir`.
        public static bool OnEdge(int x,int z,int dir) {var n=new Vector2Int(x,z)+Dirs[dir];return Inside(x,z)&&!Inside(n.x,n.y);}
    }

    [Serializable] public class RoomData { public int id; public int floor; public int x; public int z; public int w; public int d; public int type; public bool Contains(int cx,int cz) => cx>=x&&cx<x+w&&cz>=z&&cz<z+d; }
    // An opening from cell (x,z) on `floor` toward `dir`. A locked door is the only way into a dead-end room and needs a key.
    // An arch (the cruise lobby's wide openings) has no leaf: it is always open and never locked.
    [Serializable] public class DoorData { public int floor; public int x; public int z; public int dir; public bool locked; public bool arch; }
    // An access from the outside: the cell (x,z) on `floor` at the hull edge facing `dir`. On the cargo ship, entries on the top
    // deck climb an airlock flight to the cargo deck, entries on lower decks are hatch vestibules, and entry 0 (A) is always on
    // the top deck. On the cruise ship every entry is a flat vestibule: A is the main door into the lobby, B and C emergency exits.
    [Serializable] public class EntryData
    {
        public int floor; public int x; public int z; public int dir;
        public Vector2Int Cell => new(x,z);
        public bool Flight => ShipProfile.Current.flights&&floor==ShipGrid.Decks-1;
    }
    // Lower cell of a stair shaft on `floor`; the shaft continues one cell along `dir`. It is entered from the cell behind it on
    // `floor` and left through the cell ahead of it on `floor+1`.
    [Serializable] public class StairData
    {
        public int floor; public int x; public int z; public int dir;
        public Vector2Int Low => new(x,z);
        public Vector2Int High => Low+ShipGrid.Dirs[dir];
        public Vector2Int Entry => Low-ShipGrid.Dirs[dir];
        public Vector2Int Exit => High+ShipGrid.Dirs[dir];
    }

    public enum CellKind { Void, Corridor, Room, Stair, Atrium }

    [Serializable]
    public class InteriorLayout
    {
        // Cargo v3: 9 x 13 cells per deck, 24-30 rooms, three entries anywhere on the hull (A on the top deck), locked dead-end rooms.
        public const int GeneratorVersion=3;
        // Cruise v1: 13 x 30 cells on five decks, 75-90 rooms, the fixed two-storey lobby behind main door A on deck 2.
        public const int CruiseGeneratorVersion=1;
        public static int VersionOf(ShipKind kind) => kind==ShipKind.Cruise?CruiseGeneratorVersion:GeneratorVersion;
        public const int EntryCount=3;
        public static readonly string[] EntryNames={"A","B","C"};
        public int version=GeneratorVersion;
        public int seed;
        // Which wreck this blueprint belongs to; old blueprints without the field are cargo-ship layouts.
        public ShipKind ship;
        // Cruise ship: id of the lobby room whose space continues one deck up as an open atrium (its cells on floor+1 are
        // CellKind.Atrium), or -1.
        public int atrium=-1;
        public ShipProfile Profile => ShipProfile.Of(ship);
        public string Describe => "seed "+seed+", "+ship+" generator v"+version;
        // One row-major mask per deck ('1' = corridor), index x+z*Columns.
        public List<string> corridors=new();
        public List<RoomData> rooms=new();
        public List<DoorData> doors=new();
        public List<StairData> stairs=new();
        public List<EntryData> entries=new();
        public bool fallback;

        // Per-cell lookup tables, rebuilt whenever Validate runs (tests edit blueprints in place) or on first use.
        [NonSerialized] CellKind[] kinds;
        [NonSerialized] int[] modules;
        [NonSerialized] HashSet<int> doorEdges;
        static int Key(int floor,int x,int z) => floor*ShipGrid.Columns*ShipGrid.Rows+x+z*ShipGrid.Columns;
        // Grid queries answer for this blueprint's ship, so indexing makes it the active profile (only one wreck is ever loaded).
        void Index()
        {
            ShipProfile.Use(ship);
            int n=ShipGrid.Decks*ShipGrid.Columns*ShipGrid.Rows;kinds=new CellKind[n];modules=new int[n];doorEdges=new HashSet<int>();
            for(int f=0;f<ShipGrid.Decks;f++)for(int z=0;z<ShipGrid.Rows;z++)for(int x=0;x<ShipGrid.Columns;x++) {modules[Key(f,x,z)]=-1;if(IsCorridor(f,x,z))kinds[Key(f,x,z)]=CellKind.Corridor;}
            foreach(var r in rooms)if(r.floor>=0&&r.floor<ShipGrid.Decks)for(int x=r.x;x<r.x+r.w;x++)for(int z=r.z;z<r.z+r.d;z++)if(ShipGrid.Inside(x,z)) {kinds[Key(r.floor,x,z)]=CellKind.Room;modules[Key(r.floor,x,z)]=r.id;}
            for(int i=0;i<stairs.Count;i++) {
                var s=stairs[i];
                foreach(int f in new[]{s.floor,s.floor+1})foreach(var c in new[]{s.Low,s.High})if(f>=0&&f<ShipGrid.Decks&&ShipGrid.Inside(c.x,c.y)) {kinds[Key(f,c.x,c.y)]=CellKind.Stair;modules[Key(f,c.x,c.y)]=i;}
            }
            var lobby=Atrium;
            if(lobby!=null&&lobby.floor+1<ShipGrid.Decks)for(int x=lobby.x;x<lobby.x+lobby.w;x++)for(int z=lobby.z;z<lobby.z+lobby.d;z++)if(ShipGrid.Inside(x,z)) {kinds[Key(lobby.floor+1,x,z)]=CellKind.Atrium;modules[Key(lobby.floor+1,x,z)]=lobby.id;}
            foreach(var d in doors) {
                if(d.dir<0||d.dir>3)continue;var n2=new Vector2Int(d.x,d.z)+ShipGrid.Dirs[d.dir];
                doorEdges.Add(Key(d.floor,d.x,d.z)*4+d.dir);doorEdges.Add(Key(d.floor,n2.x,n2.y)*4+ShipGrid.Opposite(d.dir));
            }
        }
        public RoomData Atrium => atrium>=0&&rooms!=null&&atrium<rooms.Count?rooms[atrium]:null;
        // The cruise lobby (the room under the atrium), where main entry A and the grand stairs open.
        public bool IsLobby(RoomData room) => room!=null&&room.id==atrium;
        // Cell (x,z) on `floor` is walkable and its edge toward `dir` is the balustrade over the lobby atrium.
        public bool Balcony(int floor,int x,int z,int dir)
        {
            var n=new Vector2Int(x,z)+ShipGrid.Dirs[dir];var a=Kind(floor,x,z,out _);var b=Kind(floor,n.x,n.y,out _);
            return (a==CellKind.Atrium&&b==CellKind.Corridor)||(a==CellKind.Corridor&&b==CellKind.Atrium);
        }
        public bool IsCorridor(int floor,int x,int z) => floor>=0&&floor<corridors.Count&&ShipGrid.Inside(x,z)&&corridors[floor].Length==ShipGrid.Columns*ShipGrid.Rows&&corridors[floor][x+z*ShipGrid.Columns]=='1';
        public RoomData RoomAt(int floor,int x,int z) {var kind=Kind(floor,x,z,out int id);return kind==CellKind.Room&&id>=0&&id<rooms.Count?rooms[id]:null;}
        public CellKind Kind(int floor,int x,int z,out int module)
        {
            module=-1;if(!ShipGrid.Inside(x,z)||floor<0||floor>=ShipGrid.Decks)return CellKind.Void;
            if(kinds==null||ShipProfile.Current.kind!=ship)Index();
            module=modules[Key(floor,x,z)];return kinds[Key(floor,x,z)];
        }
        // Index of the entry that opens from cell (x,z) on `floor` toward `dir`, or -1.
        public int EntryAt(int floor,int x,int z,int dir)
        {
            if(entries==null)return -1;
            for(int i=0;i<entries.Count;i++) {var e=entries[i];if(e.floor==floor&&e.x==x&&e.z==z&&e.dir==dir)return i;}
            return -1;
        }
        public bool HasDoor(int floor,int x,int z,int dir)
        {
            if(kinds==null||ShipProfile.Current.kind!=ship)Index();
            return ShipGrid.Inside(x,z)&&doorEdges.Contains(Key(floor,x,z)*4+dir);
        }
        // Index in `doors` of the door on that edge (either side), or -1.
        public int DoorAt(int floor,int x,int z,int dir)
        {
            var n=new Vector2Int(x,z)+ShipGrid.Dirs[dir];
            for(int i=0;i<doors.Count;i++) {
                var d=doors[i];if(d.floor!=floor)continue;
                if((d.x==x&&d.z==z&&d.dir==dir)||(d.x==n.x&&d.z==n.y&&d.dir==ShipGrid.Opposite(dir)))return i;
            }
            return -1;
        }
        // True when the edge of cell (x,z) facing `dir` is physically passable (a locked door counts: it opens with a key).
        // Geometry, navigation and validation all use this.
        public bool Open(int floor,int x,int z,int dir)
        {
            var n=new Vector2Int(x,z)+ShipGrid.Dirs[dir];
            var kind=Kind(floor,x,z,out int a);
            if(!ShipGrid.Inside(n.x,n.y))return (kind==CellKind.Corridor||kind==CellKind.Room)&&EntryAt(floor,x,z,dir)>=0;
            var other=Kind(floor,n.x,n.y,out int b);
            if(kind==CellKind.Void||other==CellKind.Void||kind==CellKind.Atrium||other==CellKind.Atrium)return false;
            if(kind==CellKind.Stair||other==CellKind.Stair) {
                if(kind==CellKind.Stair&&other==CellKind.Stair)return a==b;
                int stair=kind==CellKind.Stair?a:b;var s=stairs[stair];var cell=kind==CellKind.Stair?new Vector2Int(x,z):n;var outside=kind==CellKind.Stair?n:new Vector2Int(x,z);
                if(floor==s.floor)return cell==s.Low&&outside==s.Entry;
                return cell==s.High&&outside==s.Exit;
            }
            if(kind==other&&a==b)return true;
            return HasDoor(floor,x,z,dir);
        }
        // Whether the deck slab at `floor` exists under cell (x,z): it is missing where a stair shaft rises from the deck below and
        // over the lobby (its atrium is open up to the next deck's slab).
        public bool HasFloor(int floor,int x,int z)
        {
            foreach(var s in stairs)if(s.floor+1==floor&&((s.Low.x==x&&s.Low.y==z)||(s.High.x==x&&s.High.y==z)))return false;
            var lobby=Atrium;if(lobby!=null&&floor==lobby.floor+1&&lobby.Contains(x,z))return false;
            return true;
        }
        public Vector3 Position(RoomData room) => ShipGrid.Center(room.floor,room.x,room.z)+new Vector3((room.w-1)*ShipGrid.Cell*.5f,0,(room.d-1)*ShipGrid.Cell*.5f);
        static Vector3 Dir3(int dir) => new(ShipGrid.Dirs[dir].x,0,ShipGrid.Dirs[dir].y);
        // The hull edge where entry `index` leaves the grid (y = 0) and its outward direction.
        public Vector3 EntryOrigin(int index,out Vector3 outward)
        {
            var e=entries[index];outward=Dir3(e.dir);var c=ShipGrid.Center(e.floor,e.x,e.z);c.y=0;return c+outward*ShipGrid.Cell*.5f;
        }
        // Where a diver arrives through entry `index` (interior coordinates).
        public Vector3 EntryPosition(int index)
        {
            var e=entries[index];var origin=EntryOrigin(index,out var outward);
            return e.Flight?origin+outward*ShipGrid.AirlockEntry+Vector3.up*ShipTimeline.CargoDeck:origin+outward*ShipGrid.HatchEntry+Vector3.up*ShipTimeline.FloorHeight(e.floor);
        }
        // Heading (degrees) of someone arriving through entry `index`, looking into the ship.
        public float EntryFacing(int index) {var d=-Dir3(entries[index].dir);return Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;}
        // Middle of door `index` on its wall (interior coordinates, floor height).
        public Vector3 DoorCenter(int index) {var d=doors[index];return ShipGrid.Center(d.floor,d.x,d.z)+Dir3(d.dir)*ShipGrid.Cell*.5f;}
        // The room a door leads into (the room side; for a door between two rooms, the far side).
        public RoomData DoorRoom(int index)
        {
            var d=doors[index];var n=new Vector2Int(d.x,d.z)+ShipGrid.Dirs[d.dir];
            return RoomAt(d.floor,n.x,n.y)??RoomAt(d.floor,d.x,d.z);
        }
        // The longest straight corridor run on a deck (QA places monsters and players along it).
        public bool StraightRun(int floor,int minimum,out Vector2Int start,out int dir,out int length)
        {
            start=default;dir=0;length=0;
            for(int d=0;d<2;d++)for(int z=0;z<ShipGrid.Rows;z++)for(int x=0;x<ShipGrid.Columns;x++) {
                int n=0;var c=new Vector2Int(x,z);
                while(ShipGrid.Inside(c.x,c.y)&&IsCorridor(floor,c.x,c.y)) {n++;c+=ShipGrid.Dirs[d];}
                if(n>length) {length=n;start=new Vector2Int(x,z);dir=d;}
            }
            return length>=minimum;
        }
        // Doors that touch room `r`.
        public int DoorCount(RoomData r) => doors.Count(d=>d.floor==r.floor&&(r.Contains(d.x,d.z)||r.Contains(d.x+ShipGrid.Dirs[d.dir].x,d.z+ShipGrid.Dirs[d.dir].y)));

        // The blueprint travels as gzip-compressed JSON in base64, so the larger v3 layouts still fit one network string.
        public string Pack()
        {
            var json=Encoding.UTF8.GetBytes(JsonUtility.ToJson(this));
            using var output=new MemoryStream();
            using(var zip=new GZipStream(output,System.IO.Compression.CompressionLevel.Optimal,true))zip.Write(json,0,json.Length);
            return Convert.ToBase64String(output.ToArray());
        }
        public static InteriorLayout Unpack(string packed)
        {
            using var input=new MemoryStream(Convert.FromBase64String(packed));using var zip=new GZipStream(input,CompressionMode.Decompress);using var reader=new StreamReader(zip,Encoding.UTF8);
            return JsonUtility.FromJson<InteriorLayout>(reader.ReadToEnd());
        }

        public bool Validate() => Problem()==null;
        // Null when the blueprint is valid, otherwise the first broken rule (logged with the seed and generator version).
        public string Problem()
        {
            kinds=null;
            if(!Enum.IsDefined(typeof(ShipKind),ship))return "ship kind";
            var profile=ShipProfile.Use(ship);int cells=ShipGrid.Columns*ShipGrid.Rows;bool cruise=ship==ShipKind.Cruise;
            if(version!=VersionOf(ship))return "generator version";
            if(corridors==null||rooms==null||doors==null||stairs==null||entries==null||corridors.Count!=ShipGrid.Decks||corridors.Any(c=>c==null||c.Length!=cells||c.Any(ch=>ch!='0'&&ch!='1')))return "corridor masks";
            if(rooms.Count<profile.minRooms||rooms.Count>profile.maxRooms||rooms.Select(x=>x.id).Distinct().Count()!=rooms.Count)return "room count";
            for(int i=0;i<rooms.Count;i++)if(rooms[i]==null||rooms[i].id!=i)return "room ids";
            for(int f=0;f<ShipGrid.Decks;f++) {int count=rooms.Count(r=>r.floor==f);if(count<profile.minRoomsPerDeck||count>profile.maxRoomsPerDeck)return "rooms on deck "+f;}
            // The cruise ship has exactly one lobby (type 7), open as an atrium to the deck above; the cargo ship has none.
            var lobby=Atrium;
            if(cruise!=(lobby!=null)||rooms.Count(r=>r.type==CruiseLayout.Lobby)!=(cruise?1:0)||(lobby!=null&&(lobby.type!=CruiseLayout.Lobby||lobby.floor+1>=ShipGrid.Decks)))return "lobby";
            // Stairs: separate shafts between every pair of decks, inside the hull, not overlapping anything.
            for(int f=0;f<ShipGrid.Decks-1;f++)if(stairs.Count(s=>s.floor==f)<2)return "stairs from deck "+f;
            // A stair landing opens onto a corridor, or (cruise) into the lobby: its grand stairs.
            bool Landing(int floor,Vector2Int c) => IsCorridor(floor,c.x,c.y)||(lobby!=null&&lobby.floor==floor&&lobby.Contains(c.x,c.y));
            var shaft=new HashSet<int>();
            foreach(var s in stairs) {
                if(s==null||s.floor<0||s.floor>=ShipGrid.Decks-1||s.dir<0||s.dir>3)return "stair data";
                foreach(var c in new[]{s.Low,s.High,s.Entry,s.Exit})if(!ShipGrid.Inside(c.x,c.y))return "stair outside hull";
                foreach(int f in new[]{s.floor,s.floor+1})foreach(var c in new[]{s.Low,s.High}) {
                    if(!shaft.Add(f*cells+c.x+c.y*ShipGrid.Columns))return "stairs overlap";
                    if(IsCorridor(f,c.x,c.y))return "stair over corridor";
                }
                if(!Landing(s.floor,s.Entry))return "stair entry not on a corridor";
                if(!Landing(s.floor+1,s.Exit))return "stair exit not on a corridor";
            }
            var used=new HashSet<int>();
            foreach(var r in rooms) {
                int side=r.type==CruiseLayout.Lobby?CruiseLayout.LobbySide:profile.maxRoomSide;
                if(r.floor<0||r.floor>=ShipGrid.Decks||!profile.AllowsType(r.type)||r.w<1||r.d<1||r.w>side||r.d>side||!ShipGrid.Inside(r.x,r.z)||!ShipGrid.Inside(r.x+r.w-1,r.z+r.d-1))return "room bounds";
                for(int x=r.x;x<r.x+r.w;x++)for(int z=r.z;z<r.z+r.d;z++) {
                    int key=r.floor*cells+x+z*ShipGrid.Columns;
                    if(!used.Add(key)||shaft.Contains(key)||IsCorridor(r.floor,x,z))return "room overlap";
                }
            }
            // Nothing stands in the atrium's open air.
            if(lobby!=null)for(int x=lobby.x;x<lobby.x+lobby.w;x++)for(int z=lobby.z;z<lobby.z+lobby.d;z++) {
                int key=(lobby.floor+1)*cells+x+z*ShipGrid.Columns;
                if(used.Contains(key)||shaft.Contains(key)||IsCorridor(lobby.floor+1,x,z))return "atrium overlap";
            }
            foreach(var d in doors) {
                if(d==null||d.floor<0||d.floor>=ShipGrid.Decks||d.dir<0||d.dir>3||!ShipGrid.Inside(d.x,d.z))return "door data";
                var n=new Vector2Int(d.x,d.z)+ShipGrid.Dirs[d.dir];if(!ShipGrid.Inside(n.x,n.y))return "door leads outside";
                var a=Kind(d.floor,d.x,d.z,out int ma);var b=Kind(d.floor,n.x,n.y,out int mb);
                if(a!=CellKind.Room&&b!=CellKind.Room)return "door without a room";
                if(a==CellKind.Void||b==CellKind.Void||a==CellKind.Stair||b==CellKind.Stair||a==CellKind.Atrium||b==CellKind.Atrium||(a==b&&ma==mb))return "door into nothing";
                if(d.arch&&(d.locked||!cruise))return "arch";
            }
            for(int i=0;i<doors.Count;i++)for(int j=i+1;j<doors.Count;j++)if(DoorAt(doors[j].floor,doors[j].x,doors[j].z,doors[j].dir)==i)return "door twice";
            // A locked door must be the only door of a dead-end room, so locking it never cuts anything else off.
            for(int i=0;i<doors.Count;i++) {
                if(!doors[i].locked)continue;var room=DoorRoom(i);
                var other=RoomAt(doors[i].floor,doors[i].x,doors[i].z);
                bool leaf=(room!=null&&DoorCount(room)==1)||(other!=null&&DoorCount(other)==1);
                if(!leaf)return "locked door "+i+" is not a dead end";
            }
            // Entries: three on the hull edge facing outward, all in different cells. Cargo: A on the top deck, every entry on a
            // corridor. Cruise: A opens into the lobby, B and C (emergency exits) on corridors of two different upper decks.
            if(entries.Count!=EntryCount||entries.Any(e=>e==null))return "entry count";
            if(!cruise&&entries[0].floor!=ShipGrid.Decks-1)return "entry A not on the top deck";
            if(cruise&&(entries[0].floor!=lobby.floor||!lobby.Contains(entries[0].x,entries[0].z)))return "entry A not in the lobby";
            for(int i=0;i<entries.Count;i++) {
                var e=entries[i];
                if(e.floor<0||e.floor>=ShipGrid.Decks||e.dir<0||e.dir>3||!ShipGrid.OnEdge(e.x,e.z,e.dir))return "entry "+i+" off the hull";
                if(!(cruise&&i==0)&&!IsCorridor(e.floor,e.x,e.z))return "entry "+i+" not on a corridor";
                for(int j=0;j<i;j++)if(entries[j].floor==e.floor&&Mathf.Abs(entries[j].x-e.x)+Mathf.Abs(entries[j].z-e.z)<3)return "entries "+j+" and "+i+" too close";
            }
            if(cruise&&(entries[1].floor==entries[2].floor||entries[1].floor<=lobby.floor||entries[2].floor<=lobby.floor))return "emergency exits";
            // Every room and corridor cell is reachable from access A through physical openings (with keys for locked doors).
            var seen=Reachable(entries[0].floor,entries[0].x,entries[0].z);
            for(int f=0;f<ShipGrid.Decks;f++)for(int z=0;z<ShipGrid.Rows;z++)for(int x=0;x<ShipGrid.Columns;x++) {
                var kind=Kind(f,x,z,out _);
                if((kind==CellKind.Room||kind==CellKind.Corridor)&&!seen.Contains(f*cells+x+z*ShipGrid.Columns))return "unreachable cell "+f+":"+x+","+z;
            }
            foreach(var r in rooms)if(!Enumerable.Range(0,4).Any(dir=>Edges(r).Any(e=>e.dir==dir&&HasDoor(r.floor,e.x,e.z,dir))))return "room "+r.id+" has no door";
            for(int f=0;f<ShipGrid.Decks;f++)if(!HasLoop(f))return "no loop on deck "+f;
            return null;
        }
        IEnumerable<(int x,int z,int dir)> Edges(RoomData r)
        {
            for(int x=r.x;x<r.x+r.w;x++)for(int z=r.z;z<r.z+r.d;z++)for(int dir=0;dir<4;dir++) {var n=new Vector2Int(x,z)+ShipGrid.Dirs[dir];if(!r.Contains(n.x,n.y))yield return (x,z,dir);}
        }
        public HashSet<int> Reachable(int floor,int x,int z)
        {
            int cells=ShipGrid.Columns*ShipGrid.Rows;
            var seen=new HashSet<int>{floor*cells+x+z*ShipGrid.Columns};var queue=new Queue<(int f,int x,int z)>();queue.Enqueue((floor,x,z));
            while(queue.Count>0) {
                var c=queue.Dequeue();
                void Visit(int f,int nx,int nz) {if(seen.Add(f*cells+nx+nz*ShipGrid.Columns))queue.Enqueue((f,nx,nz));}
                for(int dir=0;dir<4;dir++) {var n=new Vector2Int(c.x,c.z)+ShipGrid.Dirs[dir];if(ShipGrid.Inside(n.x,n.y)&&Open(c.f,c.x,c.z,dir))Visit(c.f,n.x,n.y);}
                // A stair shaft joins its low cell on the lower deck to its high cell on the upper deck.
                foreach(var s in stairs) {
                    if(c.f==s.floor&&c.x==s.Low.x&&c.z==s.Low.y)Visit(s.floor+1,s.High.x,s.High.y);
                    if(c.f==s.floor+1&&c.x==s.High.x&&c.z==s.High.y)Visit(s.floor,s.Low.x,s.Low.y);
                }
            }
            return seen;
        }
        // A loop of at least eight cells through corridors and rooms on one deck (removing any one edge of it keeps both ends connected).
        bool HasLoop(int floor)
        {
            var walkable=new List<Vector2Int>();
            for(int z=0;z<ShipGrid.Rows;z++)for(int x=0;x<ShipGrid.Columns;x++) {var k=Kind(floor,x,z,out _);if(k==CellKind.Corridor||k==CellKind.Room)walkable.Add(new(x,z));}
            foreach(var a in walkable)for(int dir=0;dir<2;dir++) {
                var b=a+ShipGrid.Dirs[dir];if(!ShipGrid.Inside(b.x,b.y)||!Open(floor,a.x,a.y,dir))continue;
                if(Kind(floor,b.x,b.y,out _)==CellKind.Stair)continue;
                // Shortest detour from a to b without the direct edge.
                var distance=new Dictionary<Vector2Int,int>{[a]=0};var queue=new Queue<Vector2Int>();queue.Enqueue(a);
                while(queue.Count>0) {
                    var c=queue.Dequeue();
                    for(int d=0;d<4;d++) {
                        var n=c+ShipGrid.Dirs[d];if(!ShipGrid.Inside(n.x,n.y)||distance.ContainsKey(n)||(c==a&&n==b)||!Open(floor,c.x,c.y,d))continue;
                        if(Kind(floor,n.x,n.y,out _)==CellKind.Stair)continue;
                        distance[n]=distance[c]+1;queue.Enqueue(n);
                    }
                }
                if(distance.TryGetValue(b,out int detour)&&detour>=7)return true;
            }
            return false;
        }
    }

    public static class InteriorGenerator
    {
        // A fixed seed whose blueprint is covered by the generator tests; used only when every attempt for a seed fails.
        public const int FallbackSeed=1;
        // The blueprint for `seed` on the given wreck. Generating makes that wreck's profile the active one.
        public static InteriorLayout Generate(int seed,ShipKind ship=ShipKind.Cargo)
        {
            InteriorLayout Attempt(int s,int attempt) {ShipProfile.Use(ship);return ship==ShipKind.Cruise?CruiseLayout.Build(s,attempt):Build(s,attempt);}
            int version=InteriorLayout.VersionOf(ship);
            for(int attempt=0;attempt<20;attempt++) {var layout=Attempt(seed,attempt);if(layout!=null&&layout.Validate())return layout;}
            Debug.LogWarning("Layout cadangan dipakai: seed "+seed+" "+ship+" generator v"+version+" gagal 20 kali.");
            for(int attempt=0;attempt<20;attempt++) {var fallback=Attempt(FallbackSeed,attempt);if(fallback!=null&&fallback.Validate()) {fallback.seed=seed;fallback.fallback=true;return fallback;}}
            throw new InvalidOperationException("Layout cadangan tidak valid ("+ship+" generator v"+version+").");
        }
        static InteriorLayout Build(int seed,int attempt)
        {
            int C=ShipGrid.Columns,R=ShipGrid.Rows,D=ShipGrid.Decks;
            var random=new System.Random(unchecked(seed+attempt*7919));
            var layout=new InteriorLayout{seed=seed};
            var corridor=new bool[D,C,R];var blocked=new bool[D,C,R];var must=new List<Vector2Int>[D];for(int f=0;f<D;f++)must[f]=new();
            bool Free(int f,Vector2Int c) => ShipGrid.Inside(c.x,c.y)&&!blocked[f,c.x,c.y];
            // 1. Stair shafts: two per deck pair, spread apart, each with a corridor cell at both ends.
            for(int f=0;f<D-1;f++) {
                for(int n=0;n<2;n++) {
                    StairData placed=null;
                    for(int tries=0;tries<300&&placed==null;tries++) {
                        var s=new StairData{floor=f,x=random.Next(C),z=random.Next(R),dir=random.Next(4)};
                        if(!Free(f,s.Low)||!Free(f,s.High)||!Free(f+1,s.Low)||!Free(f+1,s.High)||!Free(f,s.Entry)||!Free(f+1,s.Exit))continue;
                        if(must[f].Contains(s.Low)||must[f].Contains(s.High)||must[f+1].Contains(s.Low)||must[f+1].Contains(s.High))continue;
                        if(n==1) {var first=layout.stairs[^1];if(Vector2.Distance(first.Low,s.Low)<4.5f)continue;}
                        placed=s;
                    }
                    if(placed==null)return null;
                    layout.stairs.Add(placed);
                    foreach(int deck in new[]{f,f+1})foreach(var c in new[]{placed.Low,placed.High})blocked[deck,c.x,c.y]=true;
                    must[f].Add(placed.Entry);must[f+1].Add(placed.Exit);
                }
            }
            // 2. Entries: A on the top deck, B and C on two different random decks, each on a random side of the hull.
            var decks=Enumerable.Range(0,D).OrderBy(_=>random.Next()).Take(2).ToArray();
            foreach(int deck in new[]{D-1,decks[0],decks[1]}) {
                EntryData placed=null;
                for(int tries=0;tries<200&&placed==null;tries++) {
                    int dir=random.Next(4);int along=dir%2==0?random.Next(R):random.Next(C);
                    var cell=dir==0?new Vector2Int(C-1,along):dir==1?new Vector2Int(along,R-1):dir==2?new Vector2Int(0,along):new Vector2Int(along,0);
                    if(!Free(deck,cell))continue;
                    if(layout.entries.Any(e=>e.floor==deck&&Mathf.Abs(e.x-cell.x)+Mathf.Abs(e.z-cell.y)<3))continue;
                    placed=new EntryData{floor=deck,x=cell.x,z=cell.y,dir=dir};
                }
                if(placed==null)return null;
                layout.entries.Add(placed);must[deck].Add(placed.Cell);
            }
            // 3. Corridors: a ring per deck, optional cross passages, links to every stair and entry cell, then short side branches.
            for(int f=0;f<D;f++) {
                bool ring=false;
                for(int tries=0;tries<80&&!ring;tries++) {
                    int x0=random.Next(0,C-3),x1=random.Next(x0+3,C),z0=random.Next(0,R-5),z1=random.Next(z0+5,R);
                    var cells=new List<Vector2Int>();
                    for(int x=x0;x<=x1;x++){cells.Add(new(x,z0));cells.Add(new(x,z1));}
                    for(int z=z0+1;z<z1;z++){cells.Add(new(x0,z));cells.Add(new(x1,z));}
                    if(cells.Any(c=>blocked[f,c.x,c.y]))continue;
                    foreach(var c in cells)corridor[f,c.x,c.y]=true;ring=true;
                    for(int bar=0;bar<2;bar++) {
                        if(random.Next(100)>=50)continue;
                        int zc=random.Next(z0+2,z1-1);var line=Enumerable.Range(x0,x1-x0+1).Select(x=>new Vector2Int(x,zc)).ToList();
                        if(line.All(c=>!blocked[f,c.x,c.y]))foreach(var c in line)corridor[f,c.x,c.y]=true;
                    }
                }
                if(!ring)return null;
                foreach(var target in must[f]) {
                    if(blocked[f,target.x,target.y]||!Connect(corridor,blocked,f,target))return null;
                }
                int branches=random.Next(2,5);
                for(int b=0;b<branches;b++) {
                    var list=Cells(corridor,f);var from=list[random.Next(list.Count)];int dir=random.Next(4);int length=random.Next(1,4);
                    var c=from;
                    for(int k=0;k<length;k++) {c+=ShipGrid.Dirs[dir];if(!Free(f,c)||corridor[f,c.x,c.y])break;corridor[f,c.x,c.y]=true;}
                }
            }
            for(int f=0;f<D;f++) {
                var mask=new char[C*R];for(int z=0;z<R;z++)for(int x=0;x<C;x++)mask[x+z*C]=corridor[f,x,z]?'1':'0';
                layout.corridors.Add(new string(mask));
            }
            // 4. Rooms: rectangles in free space that touch a corridor, each with one or two doors.
            var room=new int[D,C,R];for(int f=0;f<D;f++)for(int x=0;x<C;x++)for(int z=0;z<R;z++)room[f,x,z]=-1;
            for(int f=0;f<D;f++) {
                int target=random.Next(8,11),placed=0;
                for(int tries=0;tries<900&&placed<target;tries++) {
                    int w=random.Next(1,3),d=random.Next(1,4);if(random.Next(2)==0)(w,d)=(d,w);
                    if(random.Next(100)<12){w=3;d=random.Next(2,4);}
                    int x0=random.Next(0,C-w+1),z0=random.Next(0,R-d+1);bool fits=true;
                    for(int x=x0;x<x0+w&&fits;x++)for(int z=z0;z<z0+d&&fits;z++)fits=!blocked[f,x,z]&&!corridor[f,x,z]&&room[f,x,z]<0;
                    if(!fits)continue;
                    var r=new RoomData{id=layout.rooms.Count,floor=f,x=x0,z=z0,w=w,d=d,type=RoomType(random,f)};
                    var exits=new List<(int x,int z,int dir)>();var neighbours=new List<(int x,int z,int dir)>();
                    for(int x=x0;x<x0+w;x++)for(int z=z0;z<z0+d;z++)for(int dir=0;dir<4;dir++) {
                        var n=new Vector2Int(x,z)+ShipGrid.Dirs[dir];if(!ShipGrid.Inside(n.x,n.y)||r.Contains(n.x,n.y))continue;
                        if(corridor[f,n.x,n.y])exits.Add((x,z,dir));else if(room[f,n.x,n.y]>=0)neighbours.Add((x,z,dir));
                    }
                    if(exits.Count==0)continue;
                    layout.rooms.Add(r);placed++;
                    for(int x=x0;x<x0+w;x++)for(int z=z0;z<z0+d;z++)room[f,x,z]=r.id;
                    var door=exits[random.Next(exits.Count)];layout.doors.Add(new DoorData{floor=f,x=door.x,z=door.z,dir=door.dir});
                    // A second way in or out makes small loops through rooms.
                    if(random.Next(100)<35) {
                        var options=exits.Where(e=>Mathf.Abs(e.x-door.x)+Mathf.Abs(e.z-door.z)>=1||e.dir!=door.dir).Concat(neighbours).ToList();
                        if(options.Count>0) {var extra=options[random.Next(options.Count)];if(!(extra.x==door.x&&extra.z==door.z&&extra.dir==door.dir))layout.doors.Add(new DoorData{floor=f,x=extra.x,z=extra.z,dir=extra.dir});}
                    }
                }
                if(placed<8)return null;
            }
            // A door added from a neighbouring room can duplicate an earlier one on the same edge; keep the first.
            for(int i=layout.doors.Count-1;i>=0;i--){var d=layout.doors[i];int first=layout.DoorAt(d.floor,d.x,d.z,d.dir);if(first>=0&&first<i)layout.doors.RemoveAt(i);}
            // 5. Locks: two to four dead-end rooms (exactly one door) get a locked door; keys are spawned elsewhere by the host.
            var leaves=layout.doors.Select((d,i)=>i).Where(i=>{var r=layout.DoorRoom(i);var o=layout.RoomAt(layout.doors[i].floor,layout.doors[i].x,layout.doors[i].z);return (r!=null&&layout.DoorCount(r)==1)||(o!=null&&layout.DoorCount(o)==1);}).OrderBy(_=>random.Next()).ToList();
            foreach(int i in leaves.Take(random.Next(2,5)))layout.doors[i].locked=true;
            return layout;
        }
        // Deck 1 (lowest) holds most of the machinery; the crew lives up top.
        static int RoomType(System.Random random,int floor)
        {
            int[][] weights={new[]{3,2,0,0,4,4,2},new[]{3,3,2,2,2,1,2},new[]{1,2,4,3,0,1,3}};
            var w=weights[floor];int roll=random.Next(w.Sum());
            for(int i=0;i<w.Length;i++) {if(roll<w[i])return i;roll-=w[i];}
            return 0;
        }
        internal static List<Vector2Int> Cells(bool[,,] corridor,int f)
        {
            var list=new List<Vector2Int>();for(int z=0;z<ShipGrid.Rows;z++)for(int x=0;x<ShipGrid.Columns;x++)if(corridor[f,x,z])list.Add(new(x,z));return list;
        }
        // Shortest corridor extension from `target` to the existing network through unblocked cells.
        internal static bool Connect(bool[,,] corridor,bool[,,] blocked,int f,Vector2Int target)
        {
            if(corridor[f,target.x,target.y])return true;
            var previous=new Dictionary<Vector2Int,Vector2Int>{[target]=target};var queue=new Queue<Vector2Int>();queue.Enqueue(target);
            while(queue.Count>0) {
                var c=queue.Dequeue();
                if(corridor[f,c.x,c.y]) {for(var p=c;p!=target;p=previous[p])corridor[f,p.x,p.y]=true;corridor[f,target.x,target.y]=true;return true;}
                foreach(var d in ShipGrid.Dirs) {var n=c+d;if(!ShipGrid.Inside(n.x,n.y)||blocked[f,n.x,n.y]||previous.ContainsKey(n))continue;previous[n]=c;queue.Enqueue(n);}
            }
            return false;
        }
    }
}
