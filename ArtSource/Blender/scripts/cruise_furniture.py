# Cruise-ship furniture (Docs/BlenderAssets.md, build list 7): models that replace the primitive pieces in
# Runtime/WorldCruise.cs. Colliders stay in code; every model fits inside the code's blocks.
# Frame: bottom centre pivot (the chandelier: its ring centre), front toward Blender -Y (Unity +Z), back against the wall
# on +Y. Material slots follow the kit convention: "<Surface>__<Tint>" fixed tints (ModelLibrary.Tints), a bare "<Surface>"
# takes the caller's tint (blanket and upholstery fabric, the slot machine's cabinet colour), "Glow__Crystal" is emissive.
# Run with cruise.blend open (or an empty file: it is saved as ArtSource/Blender/cruise.blend).
exec(open(r"D:/UNITYProjecy/SinkScrapt/SinkScrapt/ArtSource/Blender/scripts/kitlib.py").read())
import bmesh, math
from mathutils import Matrix, Vector

OUT = r"D:/UNITYProjecy/SinkScrapt/SinkScrapt/Assets/SinkScrapt/Resources/SinkScrapt/Models/Furniture/"
DWOOD, MAHOG, LINEN, FABRIC, GOLD = "Wood__DarkWood", "Wood__Mahogany", "Sand__Linen", "Carpet", "Plate__Gold"
PIANO, IVORY, KEYS_BLACK, MARBLE = "Wood__PianoBlack", "Plate__Porcelain", "Col__0E0E0E", "Marble__MarbleWhite"
BODY, CHROME, DARK, CRYSTAL, DEAD = "Plate", "Plate__Chrome", "Col__151515", "Glow__Crystal", "Plate__DeadCrystal"
PORCELAIN, RED, AMBER = "Plate__Porcelain", "Col__B01E1A", "Col__D89A2A"

def new_verts(k, before):
    return [v for v in k.bm.verts if v not in before]

def hslab(k, outline, holes, z0, z1, mat, side=None):
    """A slab whose outline (x, y) lies in the horizontal plane, from z0 to z1. Returns its verts."""
    before = set(k.bm.verts)
    k.slab([(x, -y) for x, y in outline], [[(x, -y) for x, y in h] for h in holes], z0, z1, mat, side)
    vs = new_verts(k, before)
    bmesh.ops.rotate(k.bm, verts=vs, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, 'X'))
    return vs

def hring(k, r_in, r_out, z0, z1, mat, seg=16, cx=0.0, cy=0.0):
    return hslab(k, circle(cx, cy, r_out, seg), [circle(cx, cy, r_in, seg)], z0, z1, mat)

def xform(k, verts, m):
    bmesh.ops.transform(k.bm, matrix=m, verts=verts)

def track(k, fn):
    before = set(k.bm.verts)
    fn()
    return new_verts(k, before)

def crystal(k, at, h=0.28, r=0.06, mat=CRYSTAL):
    """A hanging prism: two four-sided cones tip to tip."""
    x, y, z = at
    k.cyl((x, y, z), (x, y, z - h * 0.35), 0.0005, r, mat, seg=4)
    k.cyl((x, y, z - h * 0.35), (x, y, z - h), r, 0.0005, mat, seg=4)

made = {}

# ---- Beds -------------------------------------------------------------------------------------------------------------
# cruise_bed: the single cabin berth (code Bed: 2.0 x 0.95, along the wall, headboard on Unity -X = Blender +X).
k = Kit("cruise_bed")
k.box((0, 0, 0.03), (1.9, 0.85, 0.06), DARK)
k.box((0, 0, 0.23), (2.0, 0.95, 0.34), DWOOD)
k.box((0, -0.476, 0.23), (1.84, 0.01, 0.2), MAHOG)
k.box((0, 0, 0.48), (1.92, 0.88, 0.16), LINEN)
k.box((-0.3, -0.005, 0.575), (1.34, 0.9, 0.05), FABRIC)
k.box((-0.3, -0.46, 0.47), (1.34, 0.03, 0.2), FABRIC)
k.box((0.4, -0.005, 0.57), (0.12, 0.9, 0.035), LINEN)
k.box((0.72, 0, 0.62), (0.4, 0.62, 0.12), LINEN)
k.box((1.0, 0, 0.6), (0.08, 0.95, 1.2), MAHOG)
k.box((1.0, 0, 1.22), (0.13, 1.0, 0.05), MAHOG)
k.box((0.955, 0, 0.86), (0.02, 0.78, 0.5), FABRIC)
k.box((-0.98, 0, 0.4), (0.06, 0.95, 0.5), MAHOG)
made["cruise_bed"] = k.build("Cruise")

# cruise_bed_king: the suite's bed on a room's middle point (code: 2.1 x 2.2, headboard on Unity -Z = Blender +Y).
k = Kit("cruise_bed_king")
k.box((0, 0, 0.03), (2.0, 2.1, 0.06), DARK)
k.box((0, 0, 0.27), (2.1, 2.2, 0.42), DWOOD)
k.box((0, 0, 0.56), (2.0, 2.1, 0.16), LINEN)
k.box((0, -0.3, 0.655), (2.04, 1.42, 0.05), FABRIC)
k.box((0, -1.06, 0.5), (2.04, 0.04, 0.3), FABRIC)
for s in (-1, 1):
    k.box((s * 1.03, -0.3, 0.5), (0.04, 1.42, 0.3), FABRIC)
    k.box((s * 0.5, 0.8, 0.69), (0.7, 0.4, 0.14), LINEN)
k.box((0, 1.08, 0.8), (2.2, 0.1, 1.3), MAHOG)
k.box((0, 1.08, 1.47), (2.26, 0.14, 0.06), MAHOG)
k.box((0, 1.025, 1.0), (1.9, 0.02, 0.6), FABRIC)
k.box((0, 1.02, 1.32), (1.9, 0.03, 0.03), GOLD)
made["cruise_bed_king"] = k.build("Cruise")

# ---- Sofas --------------------------------------------------------------------------------------------------------------
# cruise_sofa (2.8 m nominal, code scales X to the length) and cruise_armchair (0.85 m): seat on +-L/2, depth 0.84,
# back on +Y. Upholstery is the caller's fabric colour.
def sofa(name, L, cushions):
    k = Kit(name)
    for sx in (-1, 1):
        for sy in (-1, 1):
            k.box((sx * (L / 2 - 0.08), sy * 0.33, 0.04), (0.06, 0.06, 0.08), DWOOD)
    k.box((0, 0, 0.2), (L, 0.84, 0.24), FABRIC)
    inner = L - 0.4
    w = inner / cushions
    for i in range(cushions):
        x = -inner / 2 + w * (i + 0.5)
        k.box((x, -0.06, 0.38), (w - 0.02, 0.66, 0.12), FABRIC)
        k.box((x, 0.2, 0.62), (w - 0.03, 0.14, 0.4), FABRIC, rot=(-10, 0, 0))
    k.box((0, 0.33, 0.62), (L, 0.18, 0.6), FABRIC)
    k.box((0, 0.33, 0.93), (L, 0.2, 0.04), FABRIC)
    for s in (-1, 1):
        k.box((s * (L / 2 - 0.1), 0, 0.46), (0.2, 0.84, 0.28), FABRIC)
        k.cyl((s * (L / 2 - 0.1), -0.42, 0.6), (s * (L / 2 - 0.1), 0.42, 0.6), 0.11, 0.11, FABRIC, seg=8)
    return k.build("Cruise")
made["cruise_sofa"] = sofa("cruise_sofa", 2.8, 3)
made["cruise_armchair"] = sofa("cruise_armchair", 0.85, 1)

# ---- Dining table and chair ---------------------------------------------------------------------------------------------
# cruise_dining_table: pedestal table in a long linen cloth, three place settings (code DiningTable; its candle stays in code).
k = Kit("cruise_dining_table")
for a in (0, 90):
    k.box((0, 0, 0.04), (0.9, 0.12, 0.08), DWOOD, rot=(0, 0, a + 45))
k.cyl((0, 0, 0.0), (0, 0, 0.45), 0.08, 0.06, DWOOD, seg=8)
k.cyl((0, 0, 0.45), (0, 0, 0.76), 0.69, 0.655, LINEN, seg=16)
k.cyl((0, 0, 0.76), (0, 0, 0.775), 0.655, 0.655, LINEN, seg=16)
for i in range(3):
    t = math.radians(i * 120)
    x, y = -math.sin(t) * 0.4, -math.cos(t) * 0.4
    k.cyl((x, y, 0.775), (x, y, 0.79), 0.11, 0.13, PORCELAIN, seg=10)
    gx, gy = -math.sin(t + 0.45) * 0.42, -math.cos(t + 0.45) * 0.42
    k.cyl((gx, gy, 0.775), (gx, gy, 0.86), 0.025, 0.03, "Glass__Porthole", seg=6)
    for s in (-1, 1):
        cx, cy = -math.sin(t + s * 0.32) * 0.42, -math.cos(t + s * 0.32) * 0.42
        k.box((cx, cy, 0.78), (0.015, 0.015, 0.012), CHROME)
made["cruise_dining_table"] = k.build("Cruise")

# cruise_chair: a dining chair (code Chair: seat 0.45 at 0.45 m, back on Unity -Z = Blender +Y), seat in the caller's colour.
k = Kit("cruise_chair")
for sx in (-1, 1):
    k.box((sx * 0.19, -0.19, 0.21), (0.04, 0.04, 0.42), DWOOD)
    k.box((sx * 0.19, 0.2, 0.47), (0.045, 0.045, 0.94), DWOOD, rot=(-6, 0, 0))
k.box((0, 0, 0.43), (0.46, 0.46, 0.05), DWOOD)
k.box((0, -0.01, 0.48), (0.44, 0.42, 0.06), FABRIC)
k.box((0, 0.225, 0.74), (0.4, 0.03, 0.36), FABRIC, rot=(-6, 0, 0))
k.box((0, 0.24, 0.93), (0.46, 0.05, 0.06), DWOOD, rot=(-6, 0, 0))
k.box((0, 0.0, 0.16), (0.38, 0.02, 0.02), DWOOD)
made["cruise_chair"] = k.build("Cruise")

# ---- Lobby pieces -------------------------------------------------------------------------------------------------------
# cruise_piano: a grand piano, keyboard to the front (-Y), lid propped open on the curved side. Fits the code body block
# (1.45 x 1.9, the case from 0.52 to 0.87 m); code turns it 180 degrees because the code's keys face Unity -Z.
k = Kit("cruise_piano")
case = [(-0.72, -0.95), (0.72, -0.95), (0.72, -0.4), (0.66, -0.12), (0.48, 0.12), (0.32, 0.38), (0.24, 0.62), (0.12, 0.84),
        (-0.08, 0.95), (-0.42, 0.97), (-0.64, 0.93), (-0.72, 0.82)]
hslab(k, case, [], 0.52, 0.87, PIANO)
hslab(k, [(x * 0.95, y * 0.95 + 0.01) for x, y in case], [], 0.87, 0.875, GOLD)
lid = track(k, lambda: hslab(k, [(x + 0.72, y) for x, y in case], [], 0.0, 0.025, PIANO))
xform(k, lid, Matrix.Translation((-0.72, 0, 0.875)) @ Matrix.Rotation(math.radians(-38), 4, 'Y'))
k.cyl((0.4, -0.2, 0.87), (0.4, -0.2, 0.87 + 1.12 * math.sin(math.radians(38)) - 0.02), 0.012, 0.012, GOLD, seg=6)
for x, y in ((-0.6, -0.78), (0.6, -0.78), (-0.25, 0.78)):
    k.cyl((x, y, 0.0), (x, y, 0.52), 0.035, 0.06, PIANO, seg=6)
    k.box((x, y, 0.02), (0.07, 0.07, 0.04), GOLD)
k.box((0, -1.02, 0.72), (1.44, 0.2, 0.06), PIANO)
k.box((0, -1.03, 0.765), (1.3, 0.16, 0.03), IVORY)
for i in range(22):
    if i % 7 in (2, 6):
        continue
    x = -0.62 + i * 1.24 / 21
    k.box((x, -0.99, 0.79), (0.022, 0.09, 0.02), KEYS_BLACK)
for s in (-1, 1):
    k.box((s * 0.69, -1.02, 0.79), (0.05, 0.22, 0.1), PIANO)
k.box((0, -0.9, 0.84), (1.3, 0.04, 0.06), PIANO)
k.box((0, -0.82, 1.0), (0.9, 0.03, 0.28), PIANO, rot=(-12, 0, 0))
k.box((0, -0.62, 0.27), (0.12, 0.06, 0.5), PIANO)
k.box((0, -0.66, 0.035), (0.2, 0.1, 0.02), GOLD)
made["cruise_piano"] = k.build("Cruise")

# cruise_chandelier: three tiers of crystal prisms on gilded rings (code rings at +0.12, -0.23 and -0.58, radius 1.2, 0.85
# and 0.5), a central stem and the chain up to the ceiling (1.6 m above the pivot). The light stays in code.
def chandelier_tiers(k, fallen=False):
    tiers = ((0.12, 1.2, 10), (-0.23, 0.85, 8), (-0.58, 0.5, 6))
    for n, (z, r, count) in enumerate(tiers):
        hring(k, r - 0.05, r, z, z + 0.04, GOLD, seg=16)
        for a in range(4):
            t = math.radians(a * 90 + n * 30)
            k.box((math.cos(t) * r / 2, math.sin(t) * r / 2, z + 0.02), (r, 0.025, 0.025), GOLD, rot=(0, 0, math.degrees(t)))
        for i in range(count):
            t = 2 * math.pi * i / count
            crystal(k, (math.cos(t) * (r - 0.025), math.sin(t) * (r - 0.025), z), mat=DEAD if fallen else CRYSTAL)
            if n == 0 and i % 2 == 0:
                bx, by = math.cos(t) * (r - 0.025), math.sin(t) * (r - 0.025)
                k.cyl((bx, by, z + 0.04), (bx, by, z + 0.14), 0.02, 0.02, LINEN, seg=6)
                k.cyl((bx, by, z + 0.14), (bx, by, z + 0.2), 0.025, 0.004, DEAD if fallen else CRYSTAL, seg=4)
    k.cyl((0, 0, -0.8), (0, 0, 0.2), 0.05, 0.05, GOLD, seg=8)
    k.cyl((0, 0, -0.95), (0, 0, -0.8), 0.005, 0.09, GOLD, seg=8)

k = Kit("cruise_chandelier")
chandelier_tiers(k)
k.cyl((0, 0, 0.2), (0, 0, 1.55), 0.02, 0.02, GOLD, seg=6)
k.cyl((0, 0, 1.55), (0, 0, 1.6), 0.18, 0.24, GOLD, seg=12)
made["cruise_chandelier"] = k.build("Cruise")

# cruise_chandelier_fallen: the same chandelier crashed onto the floor, tipped over and crushed flat, dark prisms scattered
# round it and a snapped length of chain (code collider: 1.4 x 0.5 x 1.4, turned 30 degrees).
k = Kit("cruise_chandelier_fallen")
body = track(k, lambda: chandelier_tiers(k, fallen=True))
xform(k, body, Matrix.Translation((0, 0, 0.42)) @ Matrix.Rotation(math.radians(24), 4, 'X') @ Matrix.Diagonal((1, 1, 0.45, 1)))
for v in body:
    v.co.z = max(v.co.z, 0.01)
for i in range(9):
    t = i * 2.4
    r = 0.9 + (i % 3) * 0.18
    p = track(k, lambda: crystal(k, (0, 0, 0.0), mat=DEAD))
    xform(k, p, Matrix.Translation((math.cos(t) * r, math.sin(t) * r, 0.04)) @ Matrix.Rotation(math.radians(90), 4, 'X') @ Matrix.Rotation(t, 4, 'Z'))
for i in range(6):
    k.box((0.25 + i * 0.13, 0.55 + i * 0.07, 0.025), (0.1, 0.04, 0.03), GOLD, rot=(0, 0, 25 + (i % 2) * 40))
made["cruise_chandelier_fallen"] = k.build("Cruise")

# cruise_pillar: a lobby column, 5.78 m (lobby floor to the atrium ceiling; code scales Z to its own height): marble
# plinth, gilded base and capital bands where the code had them (0-0.3 m and the top 0.35 m), round marble shaft.
H = 5.78
k = Kit("cruise_pillar")
k.box((0, 0, 0.06), (0.86, 0.86, 0.12), MARBLE)
k.box((0, 0, 0.21), (0.8, 0.8, 0.18), GOLD)
k.cyl((0, 0, 0.3), (0, 0, 0.42), 0.4, 0.34, MARBLE, seg=12)
k.cyl((0, 0, 0.42), (0, 0, H - 0.6), 0.33, 0.3, MARBLE, seg=12)
for z in (0.9, H - 0.95):
    k.cyl((0, 0, z), (0, 0, z + 0.06), 0.335, 0.335, GOLD, seg=12)
k.cyl((0, 0, H - 0.6), (0, 0, H - 0.35), 0.3, 0.42, GOLD, seg=12)
k.box((0, 0, H - 0.2), (0.86, 0.86, 0.3), GOLD)
k.box((0, 0, H - 0.025), (0.74, 0.74, 0.05), MARBLE)
made["cruise_pillar"] = k.build("Cruise")

# ---- Casino -------------------------------------------------------------------------------------------------------------
# cruise_slot_machine: 0.75 x 0.6 cabinet (code block to 1.7 m plus a gold top to 1.85), the cabinet colour from code.
# The reel screen and its blinking stay in code: the bezel's window (0.5 x 0.35 at 1.25 m) sits just behind it.
k = Kit("cruise_slot_machine")
k.box((0, 0, 0.1), (0.75, 0.6, 0.2), DARK)
k.box((0, 0.0, 0.55), (0.72, 0.56, 0.7), BODY)
k.box((0, -0.3, 0.88), (0.72, 0.18, 0.06), CHROME, rot=(-14, 0, 0))
for i, mat in enumerate((RED, AMBER, AMBER, RED)):
    k.box((-0.21 + i * 0.14, -0.32, 0.925), (0.08, 0.05, 0.03), mat, rot=(-14, 0, 0))
k.box((0, 0.03, 1.3), (0.72, 0.54, 0.8), BODY)
hslab_front = k.slab(rrect(-0.31, 0.31, 1.0, 1.5, 0.04), [rrect(-0.25, 0.25, 1.075, 1.425, 0.02)], -0.3, -0.24, GOLD)
k.box((0, -0.25, 0.68), (0.5, 0.02, 0.25), DARK)
k.box((0, -0.32, 0.5), (0.48, 0.1, 0.06), CHROME)
k.box((0, 0, 1.775), (0.75, 0.6, 0.15), GOLD)
k.slab(rrect(-0.3, 0.3, 1.85, 2.08, 0.1, corners=(False, False, True, True)), [], -0.06, 0.06, BODY)
k.slab(rrect(-0.25, 0.25, 1.88, 2.04, 0.06, corners=(False, False, True, True)), [], -0.075, -0.06, CRYSTAL)
k.box((-0.4, 0, 1.1), (0.06, 0.14, 0.14), CHROME)
k.cyl((-0.43, 0, 1.1), (-0.43, 0, 1.45), 0.014, 0.014, CHROME, seg=6)
k.cyl((-0.43, 0, 1.44), (-0.43, 0, 1.52), 0.045, 0.045, RED, seg=8)
made["cruise_slot_machine"] = k.build("Cruise")

report = {}
for name, ob in made.items():
    export_static(ob, OUT + name + ".fbx")
    report[name] = {"tris": tris(ob), "size": [round(d, 3) for d in ob.dimensions]}
# Lay the pieces out side by side for viewing.
x = 0.0
for name, ob in made.items():
    ob.location = (x, 0, 0)
    x += max(ob.dimensions.x, 1.0) + 0.8
result = report
