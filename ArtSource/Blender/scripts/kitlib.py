# Static kit builder for Blender 5.x (exec(open(path).read())): bmesh primitives with metre UVs (cube projection,
# 1 UV unit = 1 m unless scaled) and material slots named for ModelLibrary (Docs/BlenderAssets.md):
#   "<Surface>__<Tint>"  procedural surface texture with a fixed tint ("Plate__DoorGrey", "Wood__DoorWood")
#   "Col__RRGGBB"        flat colour,  "Glow__..." emissive,  "Glass__<Tint>" transparent glass.
# Slabs are flat outlines in the XZ plane (with optional holes) extruded along Y; Blender -Y is the model's front.
import bpy, bmesh, math
from mathutils import Vector, Matrix, Euler

def rrect(x0, x1, z0, z1, r, n=4, corners=(True, True, True, True)):
    """Rounded rectangle outline, counter-clockwise seen from -Y. corners: (bottom-left, bottom-right, top-right, top-left)."""
    pts = []
    spec = [((x0 + r, z0 + r), math.pi, corners[0]), ((x1 - r, z0 + r), 1.5 * math.pi, corners[1]),
            ((x1 - r, z1 - r), 0.0, corners[2]), ((x0 + r, z1 - r), 0.5 * math.pi, corners[3])]
    sharp = [(x0, z0), (x1, z0), (x1, z1), (x0, z1)]
    for i, (c, a0, rounded) in enumerate(spec):
        if rounded and r > 0:
            for k in range(n + 1):
                a = a0 + 0.5 * math.pi * k / n
                pts.append((c[0] + math.cos(a) * r, c[1] + math.sin(a) * r))
        else:
            pts.append(sharp[i])
    return pts

def circle(cx, cz, r, seg=16, phase=0.0):
    return [(cx + math.cos(phase + 2 * math.pi * i / seg) * r, cz + math.sin(phase + 2 * math.pi * i / seg) * r) for i in range(seg)]

def _area(pts):
    return 0.5 * sum(pts[i][0] * pts[(i + 1) % len(pts)][1] - pts[(i + 1) % len(pts)][0] * pts[i][1] for i in range(len(pts)))

class Kit:
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.mats = []
        self.uvmode = {}

    def _mi(self, key):
        if key not in self.mats:
            self.mats.append(key)
        return self.mats.index(key)

    def _tag(self, faces, mat, uv=1.0, rot=False):
        mi = self._mi(mat)
        for f in faces:
            f.material_index = mi
            f.smooth = False
            self.uvmode[f] = (uv, rot)
        return faces

    def box(self, center, size, mat, rot=(0, 0, 0), uv=1.0, uvrot=False):
        r = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = r["verts"]
        m = Matrix.Translation(center) @ Euler([math.radians(a) for a in rot]).to_matrix().to_4x4() @ Matrix.Diagonal((size[0], size[1], size[2], 1))
        bmesh.ops.transform(self.bm, matrix=m, verts=verts)
        faces = list({f for v in verts for f in v.link_faces})
        return self._tag(faces, mat, uv, uvrot)

    def bar(self, a, b, width, depth, y, mat):
        """A flat bar in the XZ plane from point a to b (x, z), `width` across, `depth` along Y centred on y."""
        dx, dz = b[0] - a[0], b[1] - a[1]
        length = math.hypot(dx, dz)
        ang = math.degrees(math.atan2(dz, dx))
        return self.box(((a[0] + b[0]) / 2, y, (a[1] + b[1]) / 2), (length, depth, width), mat, rot=(0, -ang, 0))

    def cyl(self, a, b, r0, r1, mat, seg=10, uv=1.0):
        a, b = Vector(a), Vector(b)
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r0, radius2=r1, depth=(b - a).length)
        verts = res["verts"]
        d = (b - a)
        rot = d.normalized().to_track_quat('Z', 'Y').to_matrix().to_4x4() if d.length > 1e-6 else Matrix.Identity(4)
        bmesh.ops.transform(self.bm, matrix=Matrix.Translation((a + b) / 2) @ rot, verts=verts)
        faces = list({f for v in verts for f in v.link_faces})
        return self._tag(faces, mat, uv)

    def slab(self, outline, holes, y0, y1, mat, side=None, uv=1.0, uvrot=False):
        """Outline (x, z) with optional hole outlines, extruded from y0 to y1. Caps are triangle-filled."""
        bm = self.bm
        loops = [outline] + list(holes)
        fronts, backs, created = [], [], []
        for k, pts in enumerate(loops):
            pts = list(pts)
            # Outer loop counter-clockwise, holes clockwise (seen from -Y), so the side walls face outward.
            if (_area(pts) < 0) == (k == 0):
                pts.reverse()
            f = [bm.verts.new((x, y0, z)) for x, z in pts]
            b = [bm.verts.new((x, y1, z)) for x, z in pts]
            fronts.append(f)
            backs.append(b)
            n = len(pts)
            for i in range(n):
                j = (i + 1) % n
                created.append(bm.faces.new((f[i], f[j], b[j], b[i])))
        side_faces = list(created)
        caps = []
        for ring, normal in ((fronts, Vector((0, -1, 0))), (backs, Vector((0, 1, 0)))):
            edges = []
            for verts in ring:
                n = len(verts)
                for i in range(n):
                    e = bm.edges.get((verts[i], verts[(i + 1) % n]))
                    edges.append(e)
            res = bmesh.ops.triangle_fill(bm, use_beauty=True, use_dissolve=False, edges=edges, normal=normal)
            caps += [g for g in res["geom"] if isinstance(g, bmesh.types.BMFace)]
        allf = side_faces + caps
        bmesh.ops.recalc_face_normals(bm, faces=allf)
        self._tag(caps, mat, uv, uvrot)
        self._tag(side_faces, side or mat, uv, uvrot)
        return allf

    def annulus(self, cx, cz, r_in, r_out, y0, y1, mat, seg=16):
        return self.slab(circle(cx, cz, r_out, seg), [circle(cx, cz, r_in, seg)], y0, y1, mat)

    def build(self, collection="Kit"):
        bm = self.bm
        uvl = bm.loops.layers.uv.verify()
        bm.normal_update()
        for f in bm.faces:
            scale, rot = self.uvmode.get(f, (1.0, False))
            n = f.normal
            ax = max(range(3), key=lambda i: abs(n[i]))
            for loop in f.loops:
                co = loop.vert.co
                if ax == 0:
                    u, v = co.y, co.z
                elif ax == 1:
                    u, v = co.x, co.z
                else:
                    u, v = co.x, co.y
                if rot:
                    u, v = v, u
                loop[uvl].uv = (u * scale, v * scale)
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        bm.free()
        old = bpy.data.objects.get(self.name)
        if old is not None:
            bpy.data.objects.remove(old, do_unlink=True)
        ob = bpy.data.objects.new(self.name, me)
        col = bpy.data.collections.get(collection) or bpy.data.collections.new(collection)
        if col.name not in bpy.context.scene.collection.children:
            try:
                bpy.context.scene.collection.children.link(col)
            except RuntimeError:
                pass
        col.objects.link(ob)
        for key in self.mats:
            m = bpy.data.materials.get(key) or bpy.data.materials.new(key)
            me.materials.append(m)
        return ob

def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)

def export_static(ob, path):
    """Kit settings: Forward -Z, Up Y, Apply Transform, FBX All (Unity x = -Blender x, z = -Blender y)."""
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
