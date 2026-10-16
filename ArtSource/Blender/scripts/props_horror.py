# Debris for the wreck interior (WorldFactory.Dress): what the last crew left behind. Visual only (no colliders), small,
# bottom-centre pivot on the floor, flat "Col__" colours. Placed in room corners and against corridor walls, clear of the
# 1.4 m band the monsters walk. Run with characters.blend open; exports to Models/Props.
exec(open(r"D:/UNITYProjecy/SinkScrapt/SinkScrapt/ArtSource/Blender/scripts/charlib.py").read())
NAMES = ["prop_debris_chair", "prop_debris_bucket", "prop_debris_boots", "prop_debris_papers", "prop_debris_planks", "prop_debris_lifejacket"]
for o in [o for o in bpy.data.objects if o.name in NAMES]:
    bpy.data.objects.remove(o, do_unlink=True)

def finish(b, rot=(0, 0, 0)):
    """Turns the whole piece (degrees XYZ), sets it down on z = 0 and centres it on the origin in plan."""
    m = Euler([math.radians(a) for a in rot]).to_matrix().to_4x4()
    bmesh.ops.transform(b.bm, matrix=m, verts=b.bm.verts[:])
    lo = Vector((min(v.co.x for v in b.bm.verts), min(v.co.y for v in b.bm.verts), min(v.co.z for v in b.bm.verts)))
    hi = Vector((max(v.co.x for v in b.bm.verts), max(v.co.y for v in b.bm.verts), max(v.co.z for v in b.bm.verts)))
    bmesh.ops.translate(b.bm, vec=Vector((-(lo.x + hi.x) / 2, -(lo.y + hi.y) / 2, -lo.z)), verts=b.bm.verts[:])
    ob = b.build(None)
    ob.vertex_groups.clear()
    for c in list(ob.users_collection):
        c.objects.unlink(ob)
    coll("prop").objects.link(ob)
    return ob

STEEL, RUST, DARK, WOOD, WOOD2, PAPER, INK = C((0.36, 0.40, 0.40)), C((0.42, 0.22, 0.12)), C((0.08, 0.08, 0.08)), C((0.46, 0.33, 0.2)), C((0.36, 0.25, 0.15)), C((0.78, 0.76, 0.66)), C((0.25, 0.08, 0.06))
built = {}

# A steel mess chair knocked over onto its back.
b = Builder("prop_debris_chair")
b.box((0, 0, 0.45), (0.42, 0.42, 0.04), STEEL, "x")
b.box((0, 0.2, 0.68), (0.42, 0.03, 0.42), STEEL, "x", rot=(-8, 0, 0))
for x in (-0.19, 0.19):
    for y in (-0.19, 0.19):
        b.cyl((x, y, 0.0), (x, y, 0.45), 0.012, 0.012, STEEL, "x", seg=5)
b.box((0.05, -0.05, 0.47), (0.12, 0.1, 0.004), RUST, "x")
built["chair"] = finish(b, rot=(-82, 0, 20))

# A dented galvanised bucket on its side.
b = Builder("prop_debris_bucket")
b.cyl((0, 0, 0), (0, 0, 0.28), 0.12, 0.145, STEEL, "x", seg=12)
b.cyl((0, 0, 0.272), (0, 0, 0.282), 0.13, 0.13, DARK, "x", seg=12)
b.box((0, 0.13, 0.2), (0.012, 0.02, 0.1), RUST, "x")
built["bucket"] = finish(b, rot=(90, 0, 15))

# A pair of rubber sea boots: one standing, one fallen beside it. Whoever wore them is gone.
b = Builder("prop_debris_boots")
GREEN = C((0.12, 0.2, 0.14))
b.cyl((0, 0, 0.05), (0, 0.01, 0.4), 0.06, 0.065, GREEN, "x", seg=10)
b.ball((0, -0.06, 0.05), (0.055, 0.12, 0.05), GREEN, "x", seg=(8, 5))
b.box((0, -0.05, 0.012), (0.11, 0.28, 0.024), DARK, "x")
b.cyl((0.18, 0.05, 0.065), (0.18, 0.4, 0.065), 0.06, 0.065, GREEN, "x", seg=10)
b.ball((0.18, 0.0, 0.05), (0.055, 0.1, 0.05), GREEN, "x", seg=(8, 5))
built["boots"] = finish(b)

# Loose papers: log sheets and a clipboard, one with a dark stain.
b = Builder("prop_debris_papers")
for k, (x, y, a) in enumerate([(0, 0, 10), (0.18, 0.08, -25), (-0.12, 0.16, 40), (0.08, -0.17, 75)]):
    b.box((x, y, 0.002 + k * 0.002), (0.21, 0.297, 0.002), PAPER, "x", rot=(0, 0, a))
b.box((0.18, 0.08, 0.009), (0.09, 0.07, 0.001), INK, "x", rot=(0, 0, -25))
b.box((-0.25, -0.06, 0.006), (0.23, 0.32, 0.012), WOOD2, "x", rot=(0, 0, -12))
b.box((-0.25, -0.06, 0.016), (0.2, 0.28, 0.002), PAPER, "x", rot=(0, 0, -12))
built["papers"] = finish(b)

# Splintered planks from a smashed crate.
b = Builder("prop_debris_planks")
for k, (x, y, a, l) in enumerate([(0, 0, 15, 0.95), (0.1, 0.12, -30, 0.7), (-0.15, -0.05, 75, 0.6), (0.05, -0.18, 5, 0.45)]):
    b.box((x, y, 0.016 + k * 0.03), (0.1, l, 0.03), WOOD if k % 2 == 0 else WOOD2, "x", rot=(k * 3, 0, a))
built["planks"] = finish(b)

# A faded life jacket lying flat, straps undone.
b = Builder("prop_debris_lifejacket")
ORANGE, STRAP = C((0.72, 0.3, 0.09)), C((0.15, 0.15, 0.17))
for s in (1, -1):
    b.box((s * 0.12, 0, 0.04), (0.2, 0.46, 0.08), ORANGE, "x", rot=(0, s * 6, 0), top=(0.92, 0.95))
b.box((0, 0.2, 0.035), (0.14, 0.1, 0.07), ORANGE, "x")
for y in (-0.08, 0.08):
    b.box((0, y, 0.082), (0.5, 0.035, 0.006), STRAP, "x")
b.box((0.26, -0.08, 0.01), (0.2, 0.035, 0.006), STRAP, "x", rot=(0, 0, -20))
b.box((0.1, 0.12, 0.081), (0.05, 0.04, 0.004), C((0.85, 0.85, 0.8)), "x")
built["lifejacket"] = finish(b, rot=(0, 0, 25))

OUT = r"D:/UNITYProjecy/SinkScrapt/SinkScrapt/Assets/SinkScrapt/Resources/SinkScrapt/Models/Props/"
res = {}
for i, (k, ob) in enumerate(built.items()):
    for o in bpy.context.selected_objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=OUT + ob.name + ".fbx", use_selection=True, object_types={'MESH'}, axis_forward='-Z', axis_up='Y',
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
                             bake_anim=False, mesh_smooth_type='FACE')
    ob.location = (6 + i * 1.0, 3, 0)
    res[ob.name] = tris(ob)
result = res
