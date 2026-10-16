# The two items that were still code primitives: the torch battery (id 16, "Baterai senter") as a chunky 6 V lantern
# battery that reads at a glance, and the wreck key (id 17, "Kunci") as a brass key on a red tag. Centred on the origin
# like the other items (Docs/BlenderAssets.md, build list 1); flat "Col__" colours. Run with items.blend open.
exec(open(r"D:/UNITYProjecy/SinkScrapt/SinkScrapt/ArtSource/Blender/scripts/charlib.py").read())
for o in [o for o in bpy.data.objects if o.name in ("tool_battery", "item_key")]:
    bpy.data.objects.remove(o, do_unlink=True)

def static(builder, collection="Items"):
    ob = builder.build(None)
    ob.vertex_groups.clear()
    for c in list(ob.users_collection):
        c.objects.unlink(ob)
    coll(collection).objects.link(ob)
    return ob

YELLOW, BLACK, STEEL, RED, CREAM = C((0.86, 0.72, 0.16)), C((0.08, 0.08, 0.09)), C((0.62, 0.64, 0.66)), C((0.68, 0.12, 0.08)), C((0.85, 0.82, 0.70))
b = Builder("tool_battery")
b.box((0, 0, -0.005), (0.11, 0.11, 0.13), BLACK, "x")
b.box((0, 0, -0.012), (0.114, 0.114, 0.07), YELLOW, "x")
b.box((0, -0.0575, -0.012), (0.06, 0.004, 0.03), BLACK, "x")
b.box((0, 0, 0.062), (0.1, 0.1, 0.012), STEEL, "x")
for x in (-0.026, 0.026):
    b.cyl((x, 0, 0.068), (x, 0, 0.078), 0.008, 0.008, STEEL, "x", seg=6)
    for k in range(4):
        z = 0.08 + k * 0.009
        b.box((x, 0, z), (0.024 - k * 0.003, 0.006, 0.004), STEEL, "x", rot=(0, 0, 45 * k))
b.box((0.026, -0.03, 0.07), (0.012, 0.012, 0.004), RED, "x")
battery = static(b)

BRASS, BRASS_DARK = C((0.70, 0.54, 0.24)), C((0.48, 0.36, 0.16))
k = Builder("item_key")
k.cyl((0, 0.06, 0), (0, 0.06, 0.012), 0.032, 0.032, BRASS, "x", seg=12)
k.cyl((0, 0.06, -0.001), (0, 0.06, 0.013), 0.016, 0.016, BRASS_DARK, "x", seg=10)
k.box((0, -0.02, 0.006), (0.014, 0.13, 0.01), BRASS, "x")
k.box((0.012, -0.065, 0.006), (0.022, 0.012, 0.01), BRASS, "x")
k.box((0.014, -0.045, 0.006), (0.018, 0.01, 0.01), BRASS, "x")
k.box((0.008, -0.08, 0.006), (0.012, 0.008, 0.01), BRASS, "x")
k.cyl((0, 0.098, 0.006), (0, 0.112, 0.006), 0.006, 0.006, STEEL, "x", seg=6)
k.box((0, 0.15, 0.006), (0.06, 0.07, 0.004), RED, "x", rot=(0, 0, 12))
k.box((0, 0.15, 0.0085), (0.036, 0.03, 0.002), CREAM, "x", rot=(0, 0, 12))
key = static(k)

OUT = r"D:/UNITYProjecy/SinkScrapt/SinkScrapt/Assets/SinkScrapt/Resources/SinkScrapt/Models/Items/"
for ob, path in ((battery, "tool_battery.fbx"), (key, "item_key.fbx")):
    ob.location = (0, 0, 0)
    for o in bpy.context.selected_objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=OUT + path, use_selection=True, object_types={'MESH'}, axis_forward='-Z', axis_up='Y',
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
                             bake_anim=False, mesh_smooth_type='FACE')
battery.location = (3, 2, 0)
key.location = (3.5, 2, 0)
result = {"battery": tris(battery), "key": tris(key)}
