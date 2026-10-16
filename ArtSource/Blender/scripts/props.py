# Props: the revamped flashlight (Docs/References/items/tool_flashlight.png: black knurled body, red end rings, chrome
# reflector, lens toward -Y = Unity +Z), and the parachute supply drop (crate + canopy) for computer orders.
exec(open(r"D:/UNITYProjecy/SinkScrapt/SinkScrapt/ArtSource/Blender/scripts/charlib.py").read())
for o in [o for o in bpy.data.objects if o.name.startswith(("tool_flashlight", "prop_supply_crate", "prop_parachute"))]:
    bpy.data.objects.remove(o, do_unlink=True)

def static(builder):
    ob = builder.build(None)
    ob.vertex_groups.clear()
    return ob

BODY = C((0.10, 0.10, 0.11))
GRIP = C((0.17, 0.18, 0.19))
RED = C((0.62, 0.10, 0.08))
CHROME = C((0.72, 0.73, 0.74))
LENS = G((0.95, 0.95, 0.88))

# Flashlight: 0.3 m long, centred on its origin, lens at -Y.
f = Builder("tool_flashlight")
f.cyl((0, 0.15, 0), (0, 0.13, 0), 0.026, 0.028, RED, "x", seg=10)
f.cyl((0, 0.13, 0), (0, -0.04, 0), 0.028, 0.028, BODY, "x", seg=10)
for k in range(5):
    y = 0.11 - k * 0.032
    f.cyl((0, y, 0), (0, y - 0.016, 0), 0.0295, 0.0295, GRIP, "x", seg=10)
f.box((0, 0.02, 0.03), (0.012, 0.035, 0.008), RED, "x")
f.cyl((0, -0.04, 0), (0, -0.09, 0), 0.028, 0.04, BODY, "x", seg=10)
f.cyl((0, -0.09, 0), (0, -0.13, 0), 0.04, 0.042, BODY, "x", seg=12)
f.cyl((0, -0.13, 0), (0, -0.15, 0), 0.043, 0.043, RED, "x", seg=12)
f.cyl((0, -0.149, 0), (0, -0.152, 0), 0.036, 0.036, CHROME, "x", seg=12)
f.cyl((0, -0.151, 0), (0, -0.153, 0), 0.03, 0.03, LENS, "x", seg=12, smooth=False)
torch = static(f)

# Supply crate: 0.75 x 0.75 x 0.6 m, bottom-centre origin, steel straps and a stencilled lid.
WOOD = C((0.55, 0.42, 0.27))
WOOD2 = C((0.45, 0.33, 0.20))
STRAP = C((0.22, 0.24, 0.24))
PAINT = C((0.85, 0.80, 0.68))
ORANGE = C((0.90, 0.42, 0.12))
c = Builder("prop_supply_crate")
c.box((0, 0, 0.3), (0.75, 0.75, 0.6), WOOD, "x")
for z in (0.1, 0.3, 0.5):
    c.box((0, 0, z), (0.77, 0.77, 0.02), WOOD2, "x")
for s in (1, -1):
    c.box((s * 0.2, 0, 0.3), (0.06, 0.78, 0.62), STRAP, "x")
c.box((0, 0, 0.61), (0.6, 0.6, 0.02), ORANGE, "x")
c.box((0, -0.381, 0.38), (0.32, 0.01, 0.14), PAINT, "x")
c.box((0, -0.383, 0.38), (0.06, 0.01, 0.1), RED, "x")
c.box((0, -0.383, 0.38), (0.1, 0.01, 0.035), RED, "x")
c.box((0, 0, 0.66), (0.08, 0.08, 0.08), STRAP, "x")
crate = static(c)

# Parachute: canopy dome about 3.2 m across, 3 m above the crate, with eight lines to the crate's lifting eye at the origin.
p = Builder("prop_parachute")
segs = 12
for i in range(segs):
    a0 = 2 * math.pi * i / segs
    a1 = 2 * math.pi * (i + 1) / segs
    col = ORANGE if i % 2 == 0 else PAINT
    for ring in range(3):
        e0 = math.radians(10 + ring * 22)
        e1 = math.radians(10 + (ring + 1) * 22)
        r = 1.6
        pts = []
        for (a, e) in ((a0, e0), (a1, e0), (a1, e1), (a0, e1)):
            pts.append(Vector((math.cos(a) * math.cos(math.pi / 2 - e) * r, math.sin(a) * math.cos(math.pi / 2 - e) * r, 2.9 + math.sin(math.pi / 2 - e) * r * 0.55)))
        verts = [p.bm.verts.new(v) for v in pts]
        face_ = p.bm.faces.new(verts)
        back = p.bm.faces.new(list(reversed([p.bm.verts.new(v) for v in pts])))
        p._tag(verts + list(back.verts), col, "x", True)
for i in range(8):
    a = 2 * math.pi * i / 8
    rim = (math.cos(a) * 1.55, math.sin(a) * 1.55, 3.05)
    p.cyl((0, 0, 0.05), rim, 0.008, 0.008, C((0.80, 0.78, 0.70)), "x", seg=3, smooth=False)
chute = static(p)

OUT = r"D:/UNITYProjecy/SinkScrapt/SinkScrapt/Assets/SinkScrapt/Resources/SinkScrapt/Models"
import os
os.makedirs(OUT + "/Props", exist_ok=True)
for ob, path in ((torch, "/Items/tool_flashlight.fbx"), (crate, "/Props/prop_supply_crate.fbx"), (chute, "/Props/prop_parachute.fbx")):
    for o in bpy.context.selected_objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=OUT + path, use_selection=True, object_types={'MESH'}, axis_forward='-Z', axis_up='Y',
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
                             bake_anim=False, mesh_smooth_type='FACE')
result = {"torch": tris(torch), "crate": tris(crate), "chute": tris(chute)}
