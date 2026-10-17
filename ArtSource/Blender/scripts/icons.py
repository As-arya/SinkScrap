# Item icons for the HUD slots, the belt slot and the boat computer shop: every ItemCatalog item (ids 0..24) rendered from
# its game model in a three-quarter view with a key/fill/rim light on a transparent background, then outlined in near
# black with a soft drop shadow and box-filtered from 256 to 128 px. Output: Resources/SinkScrapt/Icons/item_<id>.png
# (Editor/MediaImportRules.cs imports them as sprites). Run with items.blend open; the newer torch and the supply crate
# are appended from characters.blend.
import bpy, math, os
import numpy as np
from mathutils import Vector

OUT = r"D:/UNITYProjecy/SinkScrapt/SinkScrapt/Assets/SinkScrapt/Resources/SinkScrapt/Icons/"
os.makedirs(OUT, exist_ok=True)
# The item palettes moved with the models to Resources/SinkScrapt/Models/Items.
for name in ("items_palette", "items_palette_emission"):
    img = bpy.data.images.get(name)
    if img is not None:
        img.filepath = r"D:/UNITYProjecy/SinkScrapt/SinkScrapt/Assets/SinkScrapt/Resources/SinkScrapt/Models/Items/" + name + ".png"
        img.reload()
TMP = OUT + "_render.png"
ITEMS = ["scrap_bolt_box", "scrap_steel_plate", "scrap_valve", "scrap_pressure_gauge", "scrap_ship_lamp", "scrap_old_battery",
         "scrap_broken_radio", "scrap_circuit_board", "scrap_brass_compass", "scrap_electric_motor", "scrap_industrial_pump",
         "scrap_generator", "tool_flashlight", "tool_radio", "tool_oxygen_tank", "tool_flare", "tool_battery", "item_key", "prop_supply_crate",
         "scrap_champagne", "scrap_silver_tray", "scrap_casino_chips", "scrap_ship_bell", "scrap_jewellery_box", "scrap_gramophone"]
# Set ONLY = [ids] before exec() to re-render just those icons (for example the cruise scrap, 19-24).
ONLY = globals().get("ONLY")

# The game's torch and crate live in characters.blend.
with bpy.data.libraries.load(r"D:/UNITYProjecy/SinkScrapt/SinkScrapt/ArtSource/Blender/characters.blend") as (src, dst):
    dst.objects = [n for n in src.objects if n in ("tool_flashlight", "prop_supply_crate")]
appended = {o.name.split(".")[0] if o.name.startswith("tool_flashlight") else o.name: o for o in dst.objects}

scene = bpy.data.scenes.get("Icons") or bpy.data.scenes.new("Icons")
scene.render.engine = 'BLENDER_EEVEE'
scene.render.resolution_x = scene.render.resolution_y = 256
scene.render.film_transparent = True
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'None'
world = bpy.data.worlds.get("IconWorld") or bpy.data.worlds.new("IconWorld")
world.use_nodes = True
bg = world.node_tree.nodes.get("Background")
bg.inputs[0].default_value = (0.32, 0.34, 0.36, 1)
bg.inputs[1].default_value = 0.6
scene.world = world
for o in list(scene.collection.objects):
    scene.collection.objects.unlink(o)

cam = bpy.data.objects.get("IconCam") or bpy.data.objects.new("IconCam", bpy.data.cameras.new("IconCam"))
cam.data.type = 'ORTHO'
scene.collection.objects.link(cam)
scene.camera = cam
def sun(name, energy, rot, color=(1, 1, 1)):
    ob = bpy.data.objects.get(name) or bpy.data.objects.new(name, bpy.data.lights.new(name, 'SUN'))
    ob.data.energy = energy
    ob.data.color = color
    ob.rotation_euler = [math.radians(a) for a in rot]
    scene.collection.objects.link(ob)
sun("IconKey", 3.2, (50, 0, -40), (1, 0.96, 0.9))
sun("IconFill", 1.1, (60, 0, 70), (0.8, 0.88, 1))
sun("IconRim", 2.4, (-60, 0, 170))

def shift(a, dx, dy):
    out = np.zeros_like(a)
    h, w = a.shape[:2]
    out[max(0, dy):h + min(0, dy), max(0, dx):w + min(0, dx)] = a[max(0, -dy):h + min(0, -dy), max(0, -dx):w + min(0, -dx)]
    return out

def finish(path_in, path_out):
    img = bpy.data.images.load(path_in, check_existing=False)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(img)
    alpha = px[..., 3]
    # Outline: alpha dilated by 3 px, near black, behind the item.
    grown = alpha.copy()
    for dx in range(-3, 4):
        for dy in range(-3, 4):
            if dx * dx + dy * dy <= 10:
                grown = np.maximum(grown, shift(alpha, dx, dy))
    # Drop shadow: the outline shape, offset down-right, soft and half transparent.
    shadow = shift(grown, 5, -5)
    blur = sum(shift(shadow, dx, dy) for dx in (-2, 0, 2) for dy in (-2, 0, 2)) / 9
    out = np.zeros_like(px)
    out[..., 3] = np.clip(blur * 0.45, 0, 1)
    edge = np.clip(grown, 0, 1)
    out[..., :3] = out[..., :3] * (1 - edge[..., None]) + np.array([0.03, 0.03, 0.035]) * edge[..., None]
    out[..., 3] = out[..., 3] * (1 - edge) + edge
    out[..., :3] = out[..., :3] * (1 - alpha[..., None]) + px[..., :3] * alpha[..., None]
    out[..., 3] = np.maximum(out[..., 3], alpha)
    # 256 -> 128 box filter on premultiplied colour.
    pre = out.copy()
    pre[..., :3] *= pre[..., 3:4]
    small = pre.reshape(h // 2, 2, w // 2, 2, 4).mean(axis=(1, 3))
    a = small[..., 3:4]
    small[..., :3] = np.where(a > 1e-4, small[..., :3] / np.maximum(a, 1e-4), 0)
    res = bpy.data.images.new("icon_out", w // 2, h // 2, alpha=True)
    res.pixels[:] = small.ravel().tolist()
    res.filepath_raw = path_out
    res.file_format = 'PNG'
    res.save()
    bpy.data.images.remove(res)

made = []
for i, name in enumerate(ITEMS):
    if ONLY is not None and i not in ONLY:
        continue
    src_ob = appended.get(name) if name in ("tool_flashlight", "prop_supply_crate") else bpy.data.objects.get(name)
    if src_ob is None:
        continue
    ob = src_ob.copy()
    ob.data = src_ob.data
    ob.location = (0, 0, 0)
    ob.rotation_euler = (0, 0, math.radians(-28))
    scene.collection.objects.link(ob)
    # The copy only lives in the icon scene, whose matrices the active view layer does not update: frame it from its
    # local bounds and its own rotation.
    turn = ob.rotation_euler.to_matrix()
    corners = [turn @ Vector(c) for c in ob.bound_box]
    lo = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
    hi = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
    centre, size = (lo + hi) / 2, (hi - lo)
    el = math.radians(30)
    d = Vector((0, -math.cos(el), math.sin(el)))
    cam.location = centre + d * 10
    cam.rotation_euler = (centre - cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam.data.ortho_scale = max(size.x, size.y * 0.9, size.z * 1.1) * 1.32
    cam.data.clip_end = 30
    scene.render.filepath = TMP
    with bpy.context.temp_override(scene=scene):
        bpy.ops.render.render(write_still=True, scene=scene.name)
    finish(TMP, OUT + "item_%d.png" % i)
    scene.collection.objects.unlink(ob)
    bpy.data.objects.remove(ob)
    made.append(i)
if os.path.exists(TMP):
    os.remove(TMP)
for o in dst.objects:
    bpy.data.objects.remove(o, do_unlink=True)
result = {"icons": made}
