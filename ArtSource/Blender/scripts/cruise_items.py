# Cruise-ship scrap (ItemCatalog ids 19-24, product owner 2026-10-16): champagne bottle, silver tray with cloche, casino
# chip carrier, ship's bell, jewellery box and a two-handed gramophone. Same conventions as items_extra.py: one mesh each,
# origin at the bounding-box centre, flat "Col__" colours (and one "Glow__" gem), one-handed items inside a 0.4 m cube and
# the gramophone inside 0.8 x 0.6 x 0.6 m. Run with items.blend open; exports to Resources/SinkScrapt/Models/Items.
exec(open(r"D:/UNITYProjecy/SinkScrapt/SinkScrapt/ArtSource/Blender/scripts/charlib.py").read())
import math
from mathutils import Vector

IDS = ["scrap_champagne", "scrap_silver_tray", "scrap_casino_chips", "scrap_ship_bell", "scrap_jewellery_box", "scrap_gramophone"]
for o in [o for o in bpy.data.objects if o.name in IDS]:
    bpy.data.objects.remove(o, do_unlink=True)

def static(builder, collection="Items"):
    ob = builder.build(None)
    ob.vertex_groups.clear()
    for c in list(ob.users_collection):
        c.objects.unlink(ob)
    coll(collection).objects.link(ob)
    # Origin at the bounding-box centre (the game spins items round it).
    vs = [v.co for v in ob.data.vertices]
    lo = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
    hi = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
    mid = (lo + hi) / 2
    for v in ob.data.vertices:
        v.co -= mid
    return ob

def ring(b, z, r, h, mat, seg=16, inner=None):
    b.cyl((0, 0, z), (0, 0, z + h), r, r, mat, "x", seg=seg, smooth=False)

GLASS, FOIL, LABEL, LABEL_RED = C((0.10, 0.24, 0.14)), C((0.80, 0.64, 0.26)), C((0.88, 0.84, 0.70)), C((0.55, 0.10, 0.10))
SILVER, SILVER_DARK = C((0.78, 0.79, 0.80)), C((0.50, 0.52, 0.54))
BRASS, BRASS_DARK, ROPE = C((0.74, 0.56, 0.24)), C((0.46, 0.34, 0.14)), C((0.80, 0.74, 0.58))
WOOD, WOOD_DARK, VELVET, PEARL, GOLD = C((0.42, 0.22, 0.12)), C((0.24, 0.12, 0.07)), C((0.46, 0.08, 0.12)), C((0.92, 0.90, 0.84)), C((0.86, 0.68, 0.26))
CHIP = [C((0.70, 0.12, 0.10)), C((0.14, 0.24, 0.55)), C((0.12, 0.45, 0.22)), C((0.08, 0.08, 0.09)), C((0.88, 0.86, 0.80))]
BLACK, STEEL = C((0.07, 0.07, 0.08)), C((0.62, 0.64, 0.66))

made = {}

# 19 Botol sampanye: dark green bottle, gold foil neck, cream label with a red band. 0.09 x 0.09 x 0.36.
b = Builder("scrap_champagne")
b.cyl((0, 0, 0), (0, 0, 0.012), 0.040, 0.045, GLASS, "x", seg=12)
b.cyl((0, 0, 0.012), (0, 0, 0.20), 0.045, 0.045, GLASS, "x", seg=12)
b.cyl((0, 0, 0.20), (0, 0, 0.26), 0.045, 0.016, GLASS, "x", seg=12)
b.cyl((0, 0, 0.26), (0, 0, 0.36), 0.016, 0.016, FOIL, "x", seg=10)
b.cyl((0, 0, 0.235), (0, 0, 0.27), 0.034, 0.017, FOIL, "x", seg=12)
b.cyl((0, 0, 0.07), (0, 0, 0.15), 0.0465, 0.0465, LABEL, "x", seg=12)
b.cyl((0, 0, 0.09), (0, 0, 0.105), 0.047, 0.047, LABEL_RED, "x", seg=12)
made["scrap_champagne"] = static(b)

# 20 Nampan perak: oval silver tray with two handles and a domed cloche. 0.40 x 0.28 x 0.17.
b = Builder("scrap_silver_tray")
b.cyl((0, 0, 0), (0, 0, 0.012), 0.15, 0.16, SILVER, "x", seg=16, squash=(1.0, 0.82))
b.cyl((0, 0, 0.012), (0, 0, 0.022), 0.162, 0.164, SILVER_DARK, "x", seg=16, squash=(1.0, 0.82))
for s in (-1, 1):
    b.box((s * 0.18, 0, 0.018), (0.04, 0.09, 0.012), SILVER_DARK, "x")
    b.box((s * 0.195, 0, 0.018), (0.012, 0.09, 0.03), SILVER, "x")
b.cyl((0, 0, 0.022), (0, 0, 0.075), 0.12, 0.105, SILVER, "x", seg=14, squash=(1.0, 0.84))
b.cyl((0, 0, 0.075), (0, 0, 0.115), 0.105, 0.06, SILVER, "x", seg=14, squash=(1.0, 0.84))
b.cyl((0, 0, 0.115), (0, 0, 0.13), 0.06, 0.02, SILVER, "x", seg=14, squash=(1.0, 0.84))
b.cyl((0, 0, 0.012), (0, 0, 0.03), 0.124, 0.124, SILVER_DARK, "x", seg=14, squash=(1.0, 0.84))
b.ball((0, 0, 0.14), (0.018, 0.018, 0.018), SILVER_DARK, "x", seg=(8, 4))
made["scrap_silver_tray"] = static(b)

# 21 Keping kasino: an open chip carrier (a wooden tray with a handle) holding five stacks of coloured chips. 0.32 x 0.16 x 0.15.
b = Builder("scrap_casino_chips")
b.box((0, 0, 0.012), (0.32, 0.15, 0.024), WOOD_DARK, "x")
for s in (-1, 1):
    b.box((s * 0.155, 0, 0.04), (0.012, 0.15, 0.06), WOOD, "x")
    b.box((0, s * 0.071, 0.03), (0.30, 0.008, 0.04), WOOD, "x")
b.box((0, 0, 0.13), (0.012, 0.03, 0.03), BRASS, "x")
for s in (-1, 1):
    b.box((s * 0.14, 0, 0.1), (0.012, 0.02, 0.07), BRASS, "x")
b.box((0, 0, 0.14), (0.30, 0.025, 0.014), BRASS, "x")
for i in range(5):
    x = -0.112 + i * 0.056
    h = 0.05 + (i * 37 % 5) * 0.008
    b.cyl((x, 0.0, 0.024), (x, 0.0, 0.024 + h), 0.024, 0.024, CHIP[i], "x", seg=8, smooth=False)
# Two loose chips on top.
b.cyl((-0.06, -0.02, 0.09), (-0.06, -0.02, 0.098), 0.024, 0.024, CHIP[0], "x", seg=8)
b.cyl((0.07, 0.01, 0.10), (0.07, 0.01, 0.108), 0.024, 0.024, CHIP[3], "x", seg=8)
made["scrap_casino_chips"] = static(b)

# 22 Lonceng kapal: brass ship's bell under a wall bracket, with a lanyard on the clapper. 0.30 x 0.24 x 0.38.
b = Builder("scrap_ship_bell")
b.cyl((0, 0, 0.0), (0, 0, 0.03), 0.125, 0.12, BRASS, "x", seg=16)
b.cyl((0, 0, 0.03), (0, 0, 0.19), 0.115, 0.075, BRASS, "x", seg=16)
b.cyl((0, 0, 0.19), (0, 0, 0.215), 0.075, 0.05, BRASS, "x", seg=16)
b.cyl((0, 0, 0.06), (0, 0, 0.08), 0.112, 0.112, BRASS_DARK, "x", seg=16, smooth=False)
b.cyl((0, 0, 0.215), (0, 0, 0.25), 0.02, 0.02, BRASS_DARK, "x", seg=8)
# Bracket: a curved arm from a wall plate on +Y over the crown.
b.box((0, 0.11, 0.27), (0.12, 0.02, 0.12), BRASS_DARK, "x")
b.box((0, 0.05, 0.27), (0.04, 0.12, 0.03), BRASS_DARK, "x")
b.box((0, 0.0, 0.255), (0.06, 0.04, 0.02), BRASS_DARK, "x")
# Clapper lanyard hanging below the lip.
b.cyl((0, 0, 0.04), (0, 0, -0.03), 0.008, 0.008, ROPE, "x", seg=6)
b.ball((0, 0, -0.04), (0.016, 0.016, 0.018), ROPE, "x", seg=(8, 4))
made["scrap_ship_bell"] = static(b)

# 23 Kotak perhiasan: a mahogany jewellery box with its lid open, red velvet lining, a pearl necklace spilling over the
# front and a glinting gem. 0.26 x 0.22 x 0.22.
b = Builder("scrap_jewellery_box")
b.box((0, 0, 0.05), (0.26, 0.17, 0.10), WOOD, "x")
b.box((0, 0, 0.098), (0.235, 0.145, 0.008), VELVET, "x")
b.box((0, 0, 0.005), (0.27, 0.18, 0.01), GOLD, "x")
b.box((0, 0, 0.1), (0.27, 0.18, 0.006), GOLD, "x")
for s in (-1, 1):
    for t in (-1, 1):
        b.ball((s * 0.12, t * 0.075, -0.008), (0.014, 0.014, 0.014), GOLD, "x", seg=(6, 4))
# Lid hinged on the back edge (+Y), opened past upright.
b.box((0, 0.1, 0.17), (0.26, 0.03, 0.17), WOOD, "x", rot=(-12, 0, 0))
b.box((0, 0.085, 0.17), (0.23, 0.006, 0.14), VELVET, "x", rot=(-12, 0, 0))
b.box((0, 0.115, 0.19), (0.07, 0.008, 0.05), GOLD, "x", rot=(-12, 0, 0))
# Pearls draped across the velvet and over the front edge.
for k in range(7):
    a = k / 6.0
    x = -0.09 + a * 0.18
    y = -0.02 - math.sin(a * math.pi) * 0.06
    z = 0.108 if y > -0.07 else 0.108 - (-0.07 - y) * 2.0
    b.ball((x, y, z), (0.013, 0.013, 0.013), PEARL, "x", seg=(5, 3))
b.ball((0.05, 0.03, 0.112), (0.022, 0.022, 0.016), G((0.85, 0.12, 0.20)), "x", seg=(6, 4))
b.cyl((0.05, 0.03, 0.1), (0.05, 0.03, 0.108), 0.026, 0.026, GOLD, "x", seg=8)
made["scrap_jewellery_box"] = static(b)

# 24 Gramofon (two-handed): a wooden cabinet with a turntable and record, a crank, a tone arm and a big brass horn
# flaring over the front. Fits 0.8 x 0.6 x 0.6.
b = Builder("scrap_gramophone")
b.box((0, 0, 0.08), (0.36, 0.36, 0.16), WOOD, "x")
b.box((0, 0, 0.005), (0.38, 0.38, 0.01), WOOD_DARK, "x")
b.box((0, 0, 0.162), (0.38, 0.38, 0.012), WOOD_DARK, "x")
b.box((0, -0.181, 0.08), (0.28, 0.004, 0.1), WOOD_DARK, "x")
b.cyl((0, -0.02, 0.168), (0, -0.02, 0.178), 0.15, 0.15, BLACK, "x", seg=16, smooth=False)
b.cyl((0, -0.02, 0.178), (0, -0.02, 0.182), 0.05, 0.05, LABEL_RED, "x", seg=10, smooth=False)
# Crank on the right side.
b.cyl((0.18, 0.05, 0.09), (0.24, 0.05, 0.09), 0.01, 0.01, STEEL, "x", seg=6)
b.box((0.245, 0.05, 0.06), (0.012, 0.012, 0.07), STEEL, "x")
b.cyl((0.245, 0.05, 0.025), (0.28, 0.05, 0.025), 0.012, 0.012, WOOD_DARK, "x", seg=6)
# Tone arm from the back-right pillar to the record, then the horn neck rising from the back and flaring forward.
b.cyl((0.13, 0.13, 0.17), (0.13, 0.13, 0.24), 0.018, 0.018, BRASS_DARK, "x", seg=8)
b.cyl((0.13, 0.13, 0.235), (0.05, -0.06, 0.205), 0.012, 0.01, BRASS, "x", seg=6)
b.box((0.05, -0.07, 0.195), (0.04, 0.03, 0.025), STEEL, "x")
b.cyl((0.13, 0.13, 0.24), (0.06, 0.06, 0.32), 0.022, 0.03, BRASS, "x", seg=10)
b.cyl((0.06, 0.06, 0.32), (0.0, -0.10, 0.38), 0.03, 0.08, BRASS, "x", seg=12)
b.cyl((0.0, -0.10, 0.38), (-0.03, -0.22, 0.40), 0.08, 0.16, BRASS, "x", seg=12)
b.cyl((-0.03, -0.22, 0.40), (-0.033, -0.235, 0.402), 0.16, 0.172, BRASS_DARK, "x", seg=12)
made["scrap_gramophone"] = static(b)

OUT = r"D:/UNITYProjecy/SinkScrapt/SinkScrapt/Assets/SinkScrapt/Resources/SinkScrapt/Models/Items/"
report = {}
for i, name in enumerate(IDS):
    ob = made[name]
    ob.location = (0, 0, 0)
    for o in bpy.context.selected_objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=OUT + name + ".fbx", use_selection=True, object_types={'MESH'}, axis_forward='-Z', axis_up='Y',
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
                             bake_anim=False, mesh_smooth_type='FACE')
    ob.location = (5 + i * 0.9, 2, 0)
    report[name] = {"tris": tris(ob), "size": [round(d, 3) for d in ob.dimensions]}
result = report
