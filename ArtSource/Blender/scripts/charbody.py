# Continuous low-poly character bodies (Blender 5.x, exec after charlib.py). A stick skeleton of nodes with radii goes through
# the Skin modifier and one subdivision level, so the body is one closed mesh without seams at the joints; it is coloured by
# region (nearest bone plus height rules) and skinned with Blender's automatic weights. Heads, gloves, boots and kit are
# separate low-poly parts weighted 100% to one bone (charlib.Builder) and joined into the same mesh, so every character is
# still one mesh, one armature and the same clip names. Material slots keep the "Col__RRGGBB" / "Glow__RRGGBB" convention.
import bpy, bmesh, math
from mathutils import Vector, Matrix

def skin_mesh(name, nodes, edges, root, subsurf=1, branch=0.0):
    """nodes: {key: ((x, y, z), (rx, ry))}; edges: [(a, b)]. Returns a new, modifier-free mesh."""
    keys = list(nodes)
    src = bpy.data.meshes.new(name + "_skin")
    src.from_pydata([nodes[k][0] for k in keys], [(keys.index(a), keys.index(b)) for a, b in edges], [])
    tmp = bpy.data.objects.new(name + "_skin", src)
    bpy.context.scene.collection.objects.link(tmp)
    sk = tmp.modifiers.new("Skin", 'SKIN')
    sk.use_smooth_shade = True
    sk.branch_smoothing = branch
    for i, k in enumerate(keys):
        sv = src.skin_vertices[0].data[i]
        sv.radius = nodes[k][1]
        sv.use_root = (k == root)
    if subsurf:
        sub = tmp.modifiers.new("Sub", 'SUBSURF')
        sub.levels = subsurf
        sub.render_levels = subsurf
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
    me.name = name
    bpy.data.objects.remove(tmp, do_unlink=True)
    bpy.data.meshes.remove(src)
    return me

def loft_mesh(name, rings, seg=14, subsurf=1):
    """A closed tube through elliptical rings [(y, z, rx, rz), ...] along +Y (x across), capped at both ends, then
    Catmull-Clark subdivided `subsurf` times. Good for fish and other bodies the Skin modifier squares off."""
    bm = bmesh.new()
    loops = []
    for y, z, rx, rz in rings:
        loops.append([bm.verts.new((math.cos(2 * math.pi * i / seg) * rx, y, z + math.sin(2 * math.pi * i / seg) * rz)) for i in range(seg)])
    for a, b2 in zip(loops, loops[1:]):
        for i in range(seg):
            j = (i + 1) % seg
            bm.faces.new((a[i], a[j], b2[j], b2[i]))
    for ring, (y, z, rx, rz), d in ((loops[0], rings[0], -1), (loops[-1], rings[-1], 1)):
        tip = bm.verts.new((0, y + d * 0.02, z))
        for i in range(seg):
            bm.faces.new((ring[i], ring[(i + 1) % seg], tip))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    src = bpy.data.meshes.new(name + "_loft")
    bm.to_mesh(src)
    bm.free()
    tmp = bpy.data.objects.new(name + "_loft", src)
    bpy.context.scene.collection.objects.link(tmp)
    if subsurf:
        sub = tmp.modifiers.new("Sub", 'SUBSURF')
        sub.levels = subsurf
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
    me.name = name
    bpy.data.objects.remove(tmp, do_unlink=True)
    bpy.data.meshes.remove(src)
    return me

def shape(me, fn):
    """Moves every vertex: fn(Vector) -> Vector."""
    for v in me.vertices:
        v.co = fn(v.co.copy())
    me.update()

def _seg_dist(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
    return (a + ab * t - p).length

def bone_segments(arm):
    return {b.name: (b.head_local.copy(), b.tail_local.copy()) for b in arm.data.bones}

def nearest_bone(p, segs):
    return min(segs, key=lambda n: _seg_dist(p, *segs[n]))

def paint(me, segs, rule):
    """Material per face from rule(centre, nearest_bone_name) -> 'Col__RRGGBB'."""
    keys, picks = [], []
    for poly in me.polygons:
        c = poly.center.copy()
        key = rule(c, nearest_bone(c, segs))
        if key not in keys:
            keys.append(key)
        picks.append(keys.index(key))
    # materials.clear() resets every face's material index, so the slots go in first and the indices after.
    me.materials.clear()
    for k in keys:
        me.materials.append(material(k))
    for poly, i in zip(me.polygons, picks):
        poly.material_index = i
        poly.use_smooth = True

def head(b, c, size, skin, bone="Head", seg=(16, 12), jaw=0.28, flat=0.55, chin=0.0, brow=0.0, tilt=0.0, extra=None):
    """Sculpted low-poly head: a UV sphere scaled to `size` (half-extents x, y, z) with a flattened face (-Y), a tapered
    jaw, a fuller cranium and an optional brow and chin. `tilt` (degrees) pitches it forward about c; `extra(x, y, z)`
    may reshape the unit-sphere point further. Returns the face plane's y (the front of the face, before tilting)."""
    r = bmesh.ops.create_uvsphere(b.bm, u_segments=seg[0], v_segments=seg[1], radius=1.0)
    verts = r["verts"]
    sx, sy, sz = size
    pitch = Matrix.Rotation(math.radians(tilt), 3, 'X')
    for v in verts:
        x, y, z = v.co
        if y < -flat:
            y = -flat - (-y - flat) * 0.4
        if z < 0:
            k = -z
            x *= 1 - jaw * k ** 1.3
            if y < 0:
                y *= 1 - 0.08 * k
                y -= chin * max(0.0, k - 0.6)
        else:
            if y > 0:
                x *= 1.03
                y *= 1.05
        if brow and y < -0.3 and 0.12 < z < 0.42:
            y -= brow * (1 - abs(z - 0.27) / 0.15)
        if extra:
            x, y, z = extra(x, y, z)
        v.co = Vector(c) + pitch @ Vector((x * sx, y * sy, z * sz))
    b._tag(verts, skin, bone, True)
    return c[1] - sy * (flat + (1 - flat) * 0.4)

def face(b, c, front, skin, dark=(0.06, 0.05, 0.05), brow_col=None, lips=(0.45, 0.27, 0.23), bone="Head", eye_y=0.018, width=1.0):
    """Nose, brow, eye sockets, mouth and ears on a head built by head(): `front` is its face plane."""
    cx, cy, cz = c
    b.box((cx, front - 0.012, cz - 0.005), (0.03 * width, 0.03, 0.052), skin, bone, top=(0.55, 0.45))
    for s in (1, -1):
        # Small, slightly sunken eyes under a thin, tilted brow.
        b.box((cx + s * 0.034 * width, front + 0.004, cz + eye_y), (0.019 * width, 0.01, 0.011), C(dark), bone)
        b.box((cx + s * 0.036 * width, front - 0.003, cz + eye_y + 0.022), (0.036 * width, 0.012, 0.01), C(brow_col) if brow_col else skin, bone, rot=(0, s * -8, 0))
        b.ball((cx + s * 0.093 * width, cy + 0.0, cz + 0.0), (0.014, 0.03, 0.034), skin, bone, seg=(6, 4))
    b.box((cx, front + 0.0, cz - 0.05), (0.038 * width, 0.01, 0.008), C(lips), bone)

def glove(b, wrist, hand_dir, col, side, bone, size=1.0):
    """Mitten hand from the wrist along hand_dir (unit, mostly -Z): a rounded palm block and a thumb."""
    w = Vector(wrist)
    d = Vector(hand_dir).normalized()
    k = size
    # A cuff reaching up into the sleeve, so the glove never shows a gap at the wrist.
    b.cyl(w - d * 0.045, w + d * 0.03 * k, 0.04 * k, 0.037 * k, col, bone, seg=8)
    b.ball(w + d * 0.055 * k, (0.034 * k, 0.045 * k, 0.06 * k), col, bone, seg=(8, 6))
    b.ball(w + d * 0.1 * k + Vector((0, -0.006, 0)), (0.028 * k, 0.04 * k, 0.038 * k), col, bone, seg=(8, 5))
    b.cyl(w + d * 0.03 * k + Vector((-side * 0.012, -0.035 * k, 0)), w + d * 0.07 * k + Vector((-side * 0.008, -0.055 * k, 0)), 0.014 * k, 0.012 * k, col, bone, seg=6)

def boot(b, ankle, col, sole, bone, size=1.0, cap=None, shaft_bone=None):
    """Boot foot from the ankle forward (-Y): a shaft up into the trouser leg, rounded upper, toe, heel and a dark sole."""
    a = Vector(ankle)
    k = size
    # The ankle node sits about 0.1 m up; the sole ends on z = 0 (the floor).
    g = -a.z
    b.cyl(a + Vector((0, 0, g + 0.05)), a + Vector((0, 0, 0.09)), 0.054 * k, 0.051 * k, col, shaft_bone or bone, seg=10)
    b.ball(a + Vector((0, -0.02, g + 0.06)), (0.052 * k, 0.075 * k, 0.05 * k), col, bone, seg=(10, 6))
    b.ball(a + Vector((0, -0.11 * k, g + 0.045)), (0.05 * k, 0.07 * k, 0.038 * k), col, bone, seg=(10, 6))
    b.box(a + Vector((0, -0.055 * k, g + 0.016)), (0.105 * k, 0.27 * k, 0.03), sole, bone)
    if cap:
        b.ball(a + Vector((0, -0.15 * k, g + 0.045)), (0.048 * k, 0.045 * k, 0.035 * k), cap, bone, seg=(8, 4))

def build_character(name, arm, body_me, segs, rule, parts_fn, collection):
    """Paints and auto-weights the body, adds the rigid parts, and joins everything into one mesh under `arm`."""
    for o in [o for o in bpy.data.objects if o.name in (name, name + "_parts")]:
        bpy.data.objects.remove(o, do_unlink=True)
    paint(body_me, segs, rule)
    body = bpy.data.objects.new(name, body_me)
    coll(collection).objects.link(body)
    body.location = arm.location
    bpy.context.view_layer.update()
    for o in bpy.context.selected_objects:
        o.select_set(False)
    body.select_set(True)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    b = Builder(name + "_parts")
    parts_fn(b)
    parts = b.build(arm)
    parts.location = (0, 0, 0)
    for o in bpy.context.selected_objects:
        o.select_set(False)
    parts.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    # One armature modifier is enough after the join.
    mods = [m for m in body.modifiers if m.type == 'ARMATURE']
    for m in mods[1:]:
        body.modifiers.remove(m)
    mods[0].object = arm
    body.parent = arm
    body.matrix_parent_inverse = arm.matrix_world.inverted()
    # (The join reuses the body's slot for a material both meshes share, so no slot is duplicated.)
    return body
