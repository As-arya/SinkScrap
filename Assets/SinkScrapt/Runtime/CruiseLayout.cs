using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SinkScrapt
{
    // Generator for the abandoned cruise ship (ShipProfile.Cruise, generator v1): a 13 x 30 grid of 4 m cells on five decks.
    //
    // Fixed every visit: the two-storey lobby on deck 2 (6 x 7 cells, 24 x 28 m, open as an atrium through deck 3), main entry A
    // in its hull wall, three wide arches from the lobby to the corridors, and two grand stairs beside it (down to deck 1 and up
    // to the deck 3 gallery round the atrium). Seeded every visit: the long corridors (two walkways along the ship with cross
    // passages), the other stair shafts, emergency exits B and C on two of the three upper decks, and 75-90 rooms themed by deck:
    // crew, machinery and laundry below; restaurants, kitchens, shops, bars and the casino round the lobby; the theatre and
    // passenger cabins above; suites on the top deck.
    public static class CruiseLayout
    {
        // Room types (indices into WorldFactory.RoomTitles); 1-6 are shared with the cargo ship.
        public const int Lobby=7, Cabin=8, Suite=9, Restaurant=10, Theatre=11, Bar=12, Casino=13, Boutique=14, Clinic=15, Laundry=16;
        // The lobby is the only room allowed to be larger than ShipProfile.maxRoomSide.
        public const int LobbySide=8;
        public const int LobbyFloor=1, LobbyX=7, LobbyZ=12, LobbyW=6, LobbyD=7;
        // Cells just outside the lobby where its arches open (deck 2), and each arch's edge from the lobby side.
        static readonly (Vector2Int lobby,int dir)[] Arches={(new(9,12),3),(new(9,18),1),(new(7,17),2)};

        // (type, weight) per deck, lowest first.
        static readonly (int type,int weight)[][] Weights={
            new[]{(2,4),(1,3),(4,2),(6,2),(Laundry,2),(Clinic,1),(3,1)},
            new[]{(Restaurant,3),(3,2),(Boutique,3),(Bar,2),(Clinic,1),(Cabin,2),(Casino,1)},
            new[]{(Theatre,2),(Casino,2),(Bar,2),(Cabin,5),(Restaurant,1),(Boutique,1)},
            new[]{(Cabin,8),(Suite,2),(Bar,1),(Theatre,1)},
            new[]{(Cabin,6),(Suite,4),(Bar,2)},
        };
        // Footprints (cells) per type; one is picked and possibly turned.
        static (int w,int d)[] Sizes(int type) => type switch {
            1=>new[]{(1,2),(2,2),(1,1)}, 2=>new[]{(1,1),(1,2)}, 3=>new[]{(2,2),(2,3),(3,3)}, 4=>new[]{(3,3),(3,4),(2,3)}, 6=>new[]{(1,2),(2,2)},
            Cabin=>new[]{(1,1),(1,2),(1,1)}, Suite=>new[]{(2,2),(2,3)}, Restaurant=>new[]{(3,3),(3,4),(4,4),(4,5)}, Theatre=>new[]{(4,4),(4,5),(5,5)},
            Bar=>new[]{(2,2),(2,3),(3,3)}, Casino=>new[]{(3,3),(3,4),(4,4)}, Boutique=>new[]{(1,2),(2,2)}, Clinic=>new[]{(2,2),(1,2)}, Laundry=>new[]{(2,2),(2,3)},
            _=>new[]{(1,1)}};
        // At most this many of a showpiece room per deck.
        static int Limit(int type) => type switch {Theatre=>1,Casino=>1,Restaurant=>2,3=>2,4=>2,_=>99};

        public static InteriorLayout Build(int seed,int attempt)
        {
            var profile=ShipProfile.Use(ShipKind.Cruise);int C=ShipGrid.Columns,R=ShipGrid.Rows,D=ShipGrid.Decks;
            var random=new System.Random(unchecked(seed*31+attempt*7919+17));
            var layout=new InteriorLayout{seed=seed,ship=ShipKind.Cruise,version=InteriorLayout.CruiseGeneratorVersion};
            var corridor=new bool[D,C,R];var blocked=new bool[D,C,R];var room=new int[D,C,R];var must=new List<Vector2Int>[D];
            for(int f=0;f<D;f++) {must[f]=new();for(int x=0;x<C;x++)for(int z=0;z<R;z++)room[f,x,z]=-1;}
            bool Free(int f,Vector2Int c) => ShipGrid.Inside(c.x,c.y)&&!blocked[f,c.x,c.y];

            // 1. The lobby (room 0) and its atrium on the deck above.
            var lobby=new RoomData{id=0,floor=LobbyFloor,x=LobbyX,z=LobbyZ,w=LobbyW,d=LobbyD,type=Lobby};
            layout.rooms.Add(lobby);layout.atrium=0;
            for(int x=lobby.x;x<lobby.x+lobby.w;x++)for(int z=lobby.z;z<lobby.z+lobby.d;z++) {room[LobbyFloor,x,z]=0;blocked[LobbyFloor,x,z]=true;blocked[LobbyFloor+1,x,z]=true;}
            // 2. Grand stairs: up from the lobby to the deck 3 gallery, and down from the lobby to deck 1.
            foreach(var s in new[]{new StairData{floor=LobbyFloor,x=6,z=15,dir=2},new StairData{floor=LobbyFloor-1,x=5,z=13,dir=0}}) {
                layout.stairs.Add(s);foreach(int deck in new[]{s.floor,s.floor+1})foreach(var c in new[]{s.Low,s.High})blocked[deck,c.x,c.y]=true;
                if(!lobby.Contains(s.Entry.x,s.Entry.y)||s.floor!=LobbyFloor)must[s.floor].Add(s.Entry);
                if(!lobby.Contains(s.Exit.x,s.Exit.y)||s.floor+1!=LobbyFloor)must[s.floor+1].Add(s.Exit);
            }
            // 3. Main entry A in the lobby's hull wall; the corridor cells the arches open onto.
            layout.entries.Add(new EntryData{floor=LobbyFloor,x=LobbyX+LobbyW-1,z=LobbyZ+LobbyD/2,dir=0});
            foreach(var (cell,dir) in Arches)must[LobbyFloor].Add(cell+ShipGrid.Dirs[dir]);
            // 4. More stair shafts, spread along the ship: three between every pair of decks in all.
            for(int f=0;f<D-1;f++) {
                while(layout.stairs.Count(s=>s.floor==f)<profile.stairsPerPair) {
                    StairData placed=null;
                    for(int tries=0;tries<400&&placed==null;tries++) {
                        var s=new StairData{floor=f,x=random.Next(C),z=random.Next(R),dir=random.Next(4)};
                        if(!Free(f,s.Low)||!Free(f,s.High)||!Free(f+1,s.Low)||!Free(f+1,s.High)||!Free(f,s.Entry)||!Free(f+1,s.Exit))continue;
                        if(must[f].Contains(s.Low)||must[f].Contains(s.High)||must[f+1].Contains(s.Low)||must[f+1].Contains(s.High))continue;
                        if(layout.stairs.Any(o=>o.floor==f&&Vector2.Distance(o.Low,s.Low)<7))continue;
                        placed=s;
                    }
                    if(placed==null)return null;
                    layout.stairs.Add(placed);
                    foreach(int deck in new[]{f,f+1})foreach(var c in new[]{placed.Low,placed.High})blocked[deck,c.x,c.y]=true;
                    must[f].Add(placed.Entry);must[f+1].Add(placed.Exit);
                }
            }
            // 5. Emergency exits B and C on two different upper decks (3-5), each on a random side of the hull.
            foreach(int deck in Enumerable.Range(LobbyFloor+1,D-LobbyFloor-1).OrderBy(_=>random.Next()).Take(2).ToArray()) {
                EntryData placed=null;
                for(int tries=0;tries<200&&placed==null;tries++) {
                    int dir=random.Next(4);int along=dir%2==0?random.Next(R):random.Next(C);
                    var cell=dir==0?new Vector2Int(C-1,along):dir==1?new Vector2Int(along,R-1):dir==2?new Vector2Int(0,along):new Vector2Int(along,0);
                    if(!Free(deck,cell)||must[deck].Contains(cell))continue;
                    placed=new EntryData{floor=deck,x=cell.x,z=cell.y,dir=dir};
                }
                if(placed==null)return null;
                layout.entries.Add(placed);must[deck].Add(placed.Cell);
            }
            // 6. Corridors: two long walkways with cross passages (a ladder), the gallery round the atrium, links to every stair,
            // entry and arch, every piece joined into one network, then a few short side passages.
            for(int f=0;f<D;f++) {
                void Mark(int x,int z) {if(ShipGrid.Inside(x,z)&&!blocked[f,x,z])corridor[f,x,z]=true;}
                int a=random.Next(2,5),b=random.Next(8,11),z0=random.Next(1,4),z1=random.Next(26,29);
                for(int z=z0;z<=z1;z++){Mark(a,z);Mark(b,z);}
                for(int x=a;x<=b;x++){Mark(x,z0);Mark(x,z1);}
                int bars=random.Next(2,5),last=z0;
                for(int k=0;k<bars;k++) {int z=random.Next(last+3,Mathf.Max(last+4,z1-2));if(z>z1-3)break;for(int x=a;x<=b;x++)Mark(x,z);last=z;}
                if(f==LobbyFloor)foreach(int z in new[]{LobbyZ-1,LobbyZ+LobbyD})for(int x=a;x<C;x++)Mark(x,z);
                if(f==LobbyFloor+1) {
                    foreach(int z in new[]{LobbyZ-1,LobbyZ+LobbyD})for(int x=LobbyX-1;x<C;x++)Mark(x,z);
                    for(int z=LobbyZ-1;z<=LobbyZ+LobbyD;z++)Mark(LobbyX-1,z);
                }
                foreach(var target in must[f])if(blocked[f,target.x,target.y]||!InteriorGenerator.Connect(corridor,blocked,f,target))return null;
                if(!Join(corridor,blocked,f))return null;
                int branches=random.Next(3,7);
                for(int n=0;n<branches;n++) {
                    var list=InteriorGenerator.Cells(corridor,f);var from=list[random.Next(list.Count)];int dir=random.Next(4);int length=random.Next(1,4);
                    var c=from;
                    for(int k=0;k<length;k++) {c+=ShipGrid.Dirs[dir];if(!Free(f,c)||corridor[f,c.x,c.y])break;corridor[f,c.x,c.y]=true;}
                }
                var mask=new char[C*R];for(int z=0;z<R;z++)for(int x=0;x<C;x++)mask[x+z*C]=corridor[f,x,z]?'1':'0';
                layout.corridors.Add(new string(mask));
            }
            // The lobby's arches (always open, no leaf).
            foreach(var (cell,dir) in Arches)layout.doors.Add(new DoorData{floor=LobbyFloor,x=cell.x,z=cell.y,dir=dir,arch=true});
            // 7. Rooms: themed rectangles in free space that touch a corridor, each with one or two doors.
            for(int f=0;f<D;f++) {
                int target=random.Next(15,19),placed=layout.rooms.Count(r=>r.floor==f);var counts=new Dictionary<int,int>();
                for(int tries=0;tries<2500&&placed<target;tries++) {
                    int type=Pick(random,f);if(counts.TryGetValue(type,out int had)&&had>=Limit(type))continue;
                    var sizes=Sizes(type);var (w,d)=sizes[random.Next(sizes.Length)];if(random.Next(2)==0)(w,d)=(d,w);
                    int x0=random.Next(0,C-w+1),z0=random.Next(0,R-d+1);bool fits=true;
                    for(int x=x0;x<x0+w&&fits;x++)for(int z=z0;z<z0+d&&fits;z++)fits=!blocked[f,x,z]&&!corridor[f,x,z]&&room[f,x,z]<0;
                    if(!fits)continue;
                    var r=new RoomData{id=layout.rooms.Count,floor=f,x=x0,z=z0,w=w,d=d,type=type};
                    var exits=new List<(int x,int z,int dir)>();var neighbours=new List<(int x,int z,int dir)>();
                    for(int x=x0;x<x0+w;x++)for(int z=z0;z<z0+d;z++)for(int dir=0;dir<4;dir++) {
                        var n=new Vector2Int(x,z)+ShipGrid.Dirs[dir];if(!ShipGrid.Inside(n.x,n.y)||r.Contains(n.x,n.y))continue;
                        if(corridor[f,n.x,n.y])exits.Add((x,z,dir));else if(room[f,n.x,n.y]>=0)neighbours.Add((x,z,dir));
                    }
                    if(exits.Count==0)continue;
                    layout.rooms.Add(r);placed++;counts[type]=had+1;
                    for(int x=x0;x<x0+w;x++)for(int z=z0;z<z0+d;z++)room[f,x,z]=r.id;
                    var door=exits[random.Next(exits.Count)];layout.doors.Add(new DoorData{floor=f,x=door.x,z=door.z,dir=door.dir});
                    // Halls get a second way in more often than cabins; it can lead into a neighbouring room.
                    if(random.Next(100)<(w*d>=6?65:30)) {
                        var options=exits.Where(e=>e.x!=door.x||e.z!=door.z||e.dir!=door.dir).Concat(neighbours).ToList();
                        if(options.Count>0) {var extra=options[random.Next(options.Count)];layout.doors.Add(new DoorData{floor=f,x=extra.x,z=extra.z,dir=extra.dir});}
                    }
                }
                if(placed<profile.minRoomsPerDeck)return null;
            }
            if(layout.rooms.Count<profile.minRooms||layout.rooms.Count>profile.maxRooms)return null;
            // A door added from a neighbouring room can duplicate an earlier one on the same edge; keep the first.
            for(int i=layout.doors.Count-1;i>=0;i--){var d=layout.doors[i];int first=layout.DoorAt(d.floor,d.x,d.z,d.dir);if(first>=0&&first<i)layout.doors.RemoveAt(i);}
            // 8. Locks: four to seven dead-end rooms (exactly one door, never the lobby) get a locked door.
            var leaves=layout.doors.Select((d,i)=>i).Where(i=>{
                var d=layout.doors[i];if(d.arch)return false;var r=layout.DoorRoom(i);var o=layout.RoomAt(d.floor,d.x,d.z);
                return (r!=null&&!layout.IsLobby(r)&&layout.DoorCount(r)==1)||(o!=null&&!layout.IsLobby(o)&&layout.DoorCount(o)==1);
            }).OrderBy(_=>random.Next()).ToList();
            foreach(int i in leaves.Take(random.Next(profile.minLocks,profile.maxLocks+1)))layout.doors[i].locked=true;
            return layout;
        }
        static int Pick(System.Random random,int floor)
        {
            var w=Weights[Mathf.Clamp(floor,0,Weights.Length-1)];int roll=random.Next(w.Sum(x=>x.weight));
            foreach(var (type,weight) in w) {if(roll<weight)return type;roll-=weight;}
            return w[0].type;
        }
        // Joins every separate piece of corridor on deck `f` to the largest one by the shortest path through free cells.
        static bool Join(bool[,,] corridor,bool[,,] blocked,int f)
        {
            int C=ShipGrid.Columns,R=ShipGrid.Rows;
            for(int round=0;round<40;round++) {
                var label=new int[C,R];for(int x=0;x<C;x++)for(int z=0;z<R;z++)label[x,z]=-1;
                var sizes=new List<int>();
                for(int x=0;x<C;x++)for(int z=0;z<R;z++) {
                    if(!corridor[f,x,z]||label[x,z]>=0)continue;
                    int id=sizes.Count;sizes.Add(0);var queue=new Queue<Vector2Int>();queue.Enqueue(new(x,z));label[x,z]=id;
                    while(queue.Count>0) {
                        var c=queue.Dequeue();sizes[id]++;
                        foreach(var dir in ShipGrid.Dirs) {var n=c+dir;if(ShipGrid.Inside(n.x,n.y)&&corridor[f,n.x,n.y]&&label[n.x,n.y]<0){label[n.x,n.y]=id;queue.Enqueue(n);}}
                    }
                }
                if(sizes.Count<=1)return sizes.Count==1;
                // The smallest piece reaches out to any other piece.
                int piece=sizes.IndexOf(sizes.Min());
                var previous=new Dictionary<Vector2Int,Vector2Int>();var open=new Queue<Vector2Int>();
                for(int x=0;x<C;x++)for(int z=0;z<R;z++)if(label[x,z]==piece){var c=new Vector2Int(x,z);previous[c]=c;open.Enqueue(c);}
                bool joined=false;
                while(open.Count>0&&!joined) {
                    var c=open.Dequeue();
                    foreach(var dir in ShipGrid.Dirs) {
                        var n=c+dir;if(!ShipGrid.Inside(n.x,n.y)||previous.ContainsKey(n)||blocked[f,n.x,n.y])continue;
                        previous[n]=c;
                        if(corridor[f,n.x,n.y]&&label[n.x,n.y]!=piece) {for(var p=c;label[p.x,p.y]!=piece;p=previous[p])corridor[f,p.x,p.y]=true;joined=true;break;}
                        open.Enqueue(n);
                    }
                }
                if(!joined)return false;
            }
            return false;
        }
    }
}
