# SinkScrapt character toolkit for Blender 5.x (run inside Blender: exec(open(path).read())).
# Builds low-poly, rigid-skinned characters: every primitive is weighted 100% to one bone (PS1-style segmented skinning),
# joined into one mesh under one armature. Clips are NLA strips; the FBX exporter writes each strip as a take named after it.
# Conventions (Docs/BlenderAssets.md): 1 unit = 1 m, front faces Blender -Y, origin at the feet (fish: body centre).
# Material slots: "Col__RRGGBB" (flat colour) and "Glow__RRGGBB" (emissive); ModelLibrary parses the hex in Unity.
import bpy, bmesh, math
from mathutils import Matrix, Vector, Euler, Quaternion

FPS = 30

def hexcol(rgb):
    return "%02X%02X%02X" % tuple(max(0, min(255, int(round(c * 255)))) for c in rgb)

def material(key):
    m = bpy.data.materials.get(key)
    if m is None:
        m = bpy.data.materials.new(key)
        h = key.split("__")[1]
        c = tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))
        m.diffuse_color = (c[0], c[1], c[2], 1)
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = (c[0], c[1], c[2], 1)
            if key.startswith("Glow"):
                bsdf.inputs["Emission Color"].default_value = (c[0], c[1], c[2], 1)
                bsdf.inputs["Emission Strength"].default_value = 2.0
    return m

def C(rgb):
    return "Col__" + hexcol(rgb)

def G(rgb):
    return "Glow__" + hexcol(rgb)

def look(a, b):
    """Rotation matrix whose local +Z points from a to b."""
    d = (Vector(b) - Vector(a))
    if d.length < 1e-6:
        return Matrix.Identity(3)
    return d.normalized().to_track_quat('Z', 'Y').to_matrix()

class Builder:
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.deform = self.bm.verts.layers.deform.verify()
        self.mats = []
        self.groups = []

    def _mi(self, key):
        if key not in self.mats:
            self.mats.append(key)
        return self.mats.index(key)

    def _gi(self, bone):
        if bone not in self.groups:
            self.groups.append(bone)
        return self.groups.index(bone)

    def _tag(self, verts, mat, bone, smooth):
        verts = list(verts)
        mi, gi = self._mi(mat), self._gi(bone)
        vs = set(verts)
        for v in verts:
            v[self.deform][gi] = 1.0
        for f in {f for v in verts for f in v.link_faces}:
            if all(x in vs for x in f.verts):
                f.material_index = mi
                f.smooth = smooth
        return verts

    def box(self, center, size, mat, bone, rot=(0, 0, 0), top=(1, 1), smooth=False, bevel=0):
        """Box; `top` scales the +Z face (x, y) for a taper. rot in degrees (XYZ)."""
        r = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = r["verts"]
        for v in verts:
            if v.co.z > 0:
                v.co.x *= top[0]
                v.co.y *= top[1]
        m = Matrix.Translation(center) @ Euler([math.radians(a) for a in rot]).to_matrix().to_4x4() @ Matrix.Diagonal((size[0], size[1], size[2], 1))
        bmesh.ops.transform(self.bm, matrix=m, verts=verts)
        return self._tag(verts, mat, bone, smooth)

    def ball(self, center, radii, mat, bone, seg=(10, 6), rot=(0, 0, 0), smooth=True):
        r = bmesh.ops.create_uvsphere(self.bm, u_segments=seg[0], v_segments=seg[1], radius=1.0)
        verts = r["verts"]
        m = Matrix.Translation(center) @ Euler([math.radians(a) for a in rot]).to_matrix().to_4x4() @ Matrix.Diagonal((radii[0], radii[1], radii[2], 1))
        bmesh.ops.transform(self.bm, matrix=m, verts=verts)
        return self._tag(verts, mat, bone, smooth)

    def cyl(self, a, b, r0, r1, mat, bone, seg=8, smooth=True, squash=(1, 1)):
        """Tapered cylinder from point a (radius r0) to point b (radius r1). squash scales its cross-section (x, y)."""
        a, b = Vector(a), Vector(b)
        depth = (b - a).length
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r0, radius2=r1, depth=depth)
        verts = res["verts"]
        m = Matrix.Translation((a + b) / 2) @ look(a, b).to_4x4() @ Matrix.Diagonal((squash[0], squash[1], 1, 1))
        bmesh.ops.transform(self.bm, matrix=m, verts=verts)
        return self._tag(verts, mat, bone, smooth)

    def build(self, armature=None):
        me = bpy.data.meshes.new(self.name)
        self.bm.normal_update()
        self.bm.to_mesh(me)
        self.bm.free()
        ob = bpy.data.objects.new(self.name, me)
        coll(self.name.split("_")[0]).objects.link(ob)
        for key in self.mats:
            me.materials.append(material(key))
        for g in self.groups:
            ob.vertex_groups.new(name=g)
        if armature is not None:
            ob.parent = armature
            mod = ob.modifiers.new("Armature", 'ARMATURE')
            mod.object = armature
        return ob

def coll(name):
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(c)
    return c

def mirror(spec):
    """Expand bone specs with '.L' into .L/.R pairs (R mirrored to -X)."""
    out = []
    for name, head, tail, parent in spec:
        if name.endswith(".L"):
            out.append((name, head, tail, parent))
            rn = name[:-2] + ".R"
            rp = parent[:-2] + ".R" if parent and parent.endswith(".L") else parent
            out.append((rn, (-head[0], head[1], head[2]), (-tail[0], tail[1], tail[2]), rp))
        else:
            out.append((name, head, tail, parent))
    return out

def make_armature(name, spec, collection):
    arm = bpy.data.armatures.new(name)
    ob = bpy.data.objects.new(name, arm)
    coll(collection).objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    for o in bpy.context.selected_objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    for bname, head, tail, parent in spec:
        eb = arm.edit_bones.new(bname)
        eb.head = head
        eb.tail = tail
        eb.roll = 0
        if parent:
            eb.parent = arm.edit_bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT')
    for pb in ob.pose.bones:
        pb.rotation_mode = 'QUATERNION'
    return ob

def _local(ob, bone, rot_deg):
    """Armature-axis Euler (deg, XYZ) applied about the bone head -> pose-space quaternion."""
    M = ob.data.bones[bone].matrix_local.to_3x3()
    R = Euler([math.radians(a) for a in rot_deg]).to_matrix()
    return (M.inverted() @ R @ M).to_quaternion()

def clip(ob, name, length, keys, loop=True):
    """keys: list of (time_s, {bone: (rx, ry, rz)} or {bone: ('loc', (x,y,z))}). Missing bones rest.
    A looping clip repeats its first key at the end."""
    ad = ob.animation_data or ob.animation_data_create()
    act = bpy.data.actions.new(ob.name + "_" + name)
    ad.action = act
    bones = set()
    for _, pose in keys:
        bones.update(pose.keys())
    keys = sorted(keys, key=lambda k: k[0])
    if loop and keys[-1][0] < length - 1e-4:
        keys = keys + [(length, keys[0][1])]
    for t, pose in keys:
        f = 1 + round(t * FPS)
        for b in bones:
            pb = ob.pose.bones[b]
            v = pose.get(b)
            if isinstance(v, tuple) and len(v) == 2 and v[0] == 'loc':
                M = ob.data.bones[b].matrix_local.to_3x3()
                pb.location = M.inverted() @ Vector(v[1])
                pb.keyframe_insert("location", frame=f)
                continue
            pb.rotation_quaternion = _local(ob, b, v) if v is not None else Quaternion()
            pb.keyframe_insert("rotation_quaternion", frame=f)
    for pb in ob.pose.bones:
        pb.rotation_quaternion = Quaternion()
        pb.location = (0, 0, 0)
    ad.action = None
    tr = ad.nla_tracks.new()
    tr.name = name
    st = tr.strips.new(name, 1, act)
    st.name = name
    st.action_frame_end = 1 + round(length * FPS)
    st.frame_end = 1 + round(length * FPS)
    tr.mute = False
    return act

def wave(t, period, phase=0.0):
    return math.sin(2 * math.pi * (t / period + phase))

def sampled(ob, name, length, fn, steps=None, loop=True):
    """Clip from a function fn(t) -> pose dict, sampled `steps` times."""
    steps = steps or max(4, int(length * 8))
    keys = [(length * i / steps, fn(length * i / steps)) for i in range(steps)]
    if not loop:
        keys.append((length, fn(length)))
    return clip(ob, name, length, keys, loop)

def export(objects, path):
    for o in bpy.context.selected_objects:
        o.select_set(False)
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    # Clips are keyed at FPS; the exporter converts frames to seconds with the scene rate.
    bpy.context.scene.render.fps = FPS
    bpy.context.scene.render.fps_base = 1.0
    # Exported without "Apply Transform" (it breaks armature animation). Unity's axis-conversion bake then imports the
    # character facing -Z whatever axis_forward says; ModelLibrary.Spawn turns Characters/* by 180 degrees.
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH'},
                             axis_forward='Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             bake_space_transform=False, add_leaf_bones=False, use_armature_deform_only=True,
                             bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=True,
                             bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
                             bake_anim_simplify_factor=0.0, mesh_smooth_type='FACE', use_mesh_modifiers=False)

def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)

def clear_collection(name):
    c = bpy.data.collections.get(name)
    if c:
        for o in list(c.objects):
            data = o.data
            bpy.data.objects.remove(o, do_unlink=True)
            if data is not None and data.users == 0:
                if isinstance(data, bpy.types.Mesh):
                    bpy.data.meshes.remove(data)
                elif isinstance(data, bpy.types.Armature):
                    bpy.data.armatures.remove(data)
    for a in list(bpy.data.actions):
        if a.users == 0 and a.name.startswith(name):
            bpy.data.actions.remove(a)

# Shared crew skeleton (1.8 m, A-pose). Front -Y, character's left is +X.
CREW_BONES = mirror([
    ("Hips", (0, 0, 0.92), (0, 0, 1.04), None),
    ("Spine", (0, 0, 1.04), (0, 0, 1.24), "Hips"),
    ("Chest", (0, 0, 1.24), (0, 0, 1.46), "Spine"),
    ("Neck", (0, 0, 1.46), (0, 0, 1.56), "Chest"),
    ("Head", (0, 0, 1.56), (0, 0, 1.86), "Neck"),
    ("UpperArm.L", (0.2, 0, 1.42), (0.27, 0, 1.13), "Chest"),
    ("LowerArm.L", (0.27, 0, 1.13), (0.31, -0.02, 0.87), "UpperArm.L"),
    ("Hand.L", (0.31, -0.02, 0.87), (0.32, -0.03, 0.74), "LowerArm.L"),
    ("UpperLeg.L", (0.1, 0, 0.92), (0.11, 0, 0.5), "Hips"),
    ("LowerLeg.L", (0.11, 0, 0.5), (0.11, 0.02, 0.1), "UpperLeg.L"),
    ("Foot.L", (0.11, 0.02, 0.1), (0.11, -0.14, 0.03), "LowerLeg.L"),
])
