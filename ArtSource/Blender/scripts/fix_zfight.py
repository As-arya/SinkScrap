# Z-fighting fix for the static kit/exterior models: finds faces of different materials that lie in the same plane and
# overlap (a trim piece flush with the surface behind it, e.g. container corner castings, funnel bands, door-leaf plates)
# and grows the smaller of the two mesh islands by PUSH metres on every side, so the trim stands just proud of the surface.
# Run inside Blender with the .blend open: fix(names) then export_static(name, path).
import bpy, bmesh
from mathutils import Vector

PUSH = 0.006

def _islands(bm):
    seen, islands = set(), []
    for f in bm.faces:
        if f.index in seen:
            continue
        stack, isl = [f], []
        seen.add(f.index)
        while stack:
            g = stack.pop()
            isl.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen:
                        seen.add(h.index)
                        stack.append(h)
        islands.append(isl)
    return islands

def _box(verts):
    lo = Vector((min(v.co.x for v in verts), min(v.co.y for v in verts), min(v.co.z for v in verts)))
    hi = Vector((max(v.co.x for v in verts), max(v.co.y for v in verts), max(v.co.z for v in verts)))
    return lo, hi

def fix(name):
    ob = bpy.data.objects[name]
    me = ob.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.faces.ensure_lookup_table()
    islands = _islands(bm)
    island_of, size_of = {}, []
    for i, isl in enumerate(islands):
        vs = {v for f in isl for v in f.verts}
        lo, hi = _box(vs)
        size_of.append((hi - lo).length)
        for f in isl:
            island_of[f.index] = i
    buckets = {}
    for f in bm.faces:
        n = f.normal
        if n.length < 1e-6:
            continue
        key = (round(n.x * 50), round(n.y * 50), round(n.z * 50), round(n.dot(f.verts[0].co) * 500))
        buckets.setdefault(key, []).append(f)
    grow = set()
    lift = set()
    count = {}
    for f in bm.faces:
        count[f.material_index] = count.get(f.material_index, 0) + 1
    for faces in buckets.values():
        if len(faces) < 2:
            continue
        for i in range(len(faces)):
            a = faces[i]
            alo, ahi = _box(a.verts)
            for b in faces[i + 1:i + 300]:
                if a.material_index == b.material_index:
                    continue
                blo, bhi = _box(b.verts)
                d = Vector((min(ahi.x, bhi.x) - max(alo.x, blo.x), min(ahi.y, bhi.y) - max(alo.y, blo.y), min(ahi.z, bhi.z) - max(alo.z, blo.z)))
                if min(d) < -0.001 or sum(1 for c in d if c > 0.02) < 2:
                    continue
                ia, ib = island_of[a.index], island_of[b.index]
                if ia == ib:
                    # Same connected mesh (a band painted over a body): lift the rarer material's face off the surface.
                    lift.add(a if count[a.material_index] <= count[b.material_index] else b)
                    continue
                grow.add(ia if size_of[ia] <= size_of[ib] else ib)
    if lift:
        faces = list(lift)
        normals = {f: f.normal.copy() for f in faces}
        res = bmesh.ops.split(bm, geom=faces, use_only_faces=True)
        moved = set()
        for f in [g for g in res["geom"] if isinstance(g, bmesh.types.BMFace)]:
            n = f.normal.copy()
            for v in f.verts:
                if v not in moved:
                    v.co += n * PUSH
                    moved.add(v)
    for i in grow:
        vs = {v for f in islands[i] for v in f.verts}
        lo, hi = _box(vs)
        c = (lo + hi) / 2
        ext = hi - lo
        s = Vector(((ext.x + 2 * PUSH) / ext.x if ext.x > 1e-4 else 1, (ext.y + 2 * PUSH) / ext.y if ext.y > 1e-4 else 1, (ext.z + 2 * PUSH) / ext.z if ext.z > 1e-4 else 1))
        for v in vs:
            v.co = c + Vector(((v.co.x - c.x) * s.x, (v.co.y - c.y) * s.y, (v.co.z - c.z) * s.z))
    bm.to_mesh(me)
    bm.free()
    me.update()
    return len(grow) + len(lift)

def _layer(lc, coll):
    if lc.collection == coll:
        return lc
    for child in lc.children:
        found = _layer(child, coll)
        if found:
            return found
    return None

def export_static(name, path):
    """Exports one mesh at the origin with the kit settings (Forward -Z, Up Y, Apply Transform, FBX All). A collection
    hidden in the viewport (exterior.blend hides "Exterior") is shown for the export, else nothing can be selected."""
    ob = bpy.data.objects[name]
    layers = [_layer(bpy.context.view_layer.layer_collection, c) for c in ob.users_collection]
    hidden = [lc for lc in layers if lc and lc.hide_viewport]
    for lc in hidden:
        lc.hide_viewport = False
    keep = ob.location.copy()
    ob.location = (0, 0, 0)
    for o in bpy.context.selected_objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, axis_forward='-Z', axis_up='Y',
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
                             bake_anim=False, mesh_smooth_type='FACE')
    ob.location = keep
    for lc in hidden:
        lc.hide_viewport = True
