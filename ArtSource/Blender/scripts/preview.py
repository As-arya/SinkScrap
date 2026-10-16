# Preview render helper: preview(objects_collection, path, frame, angle) with the Workbench engine.
import bpy, math
from mathutils import Vector

def preview(path, center=(0, 0, 1.1), dist=4.5, yaw=-30, pitch=8, frame=1, lens=50, show=None, size=(640, 640)):
    sc = bpy.context.scene
    if show is not None:
        for c in bpy.data.collections:
            for o in c.objects:
                o.hide_render = c.name not in show
    cam = bpy.data.objects.get("PreviewCam")
    if cam is None:
        cam = bpy.data.objects.new("PreviewCam", bpy.data.cameras.new("PreviewCam"))
        sc.collection.objects.link(cam)
    cam.data.lens = lens
    y, p = math.radians(yaw), math.radians(pitch)
    c = Vector(center)
    cam.location = c + Vector((math.sin(y) * math.cos(p), -math.cos(y) * math.cos(p), math.sin(p))) * dist
    cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.camera = cam
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'
    sc.display.shading.color_type = 'MATERIAL'
    sc.display.shading.show_shadows = False
    sc.display.shading.show_cavity = False
    sc.render.resolution_x, sc.render.resolution_y = size
    sc.render.film_transparent = False
    sc.world = sc.world or bpy.data.worlds.new("World")
    sc.frame_set(frame)
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path
