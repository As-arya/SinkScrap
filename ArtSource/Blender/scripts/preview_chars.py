# Lineup render of rigged characters for review: rest pose (or one clip at a time), viewport colours from the slot names.
# preview_lineup(names, path, yaw, clip=None, t=0.0, spacing=1.0)
import bpy
exec(open(r"D:/UNITYProjecy/SinkScrapt/SinkScrapt/ArtSource/Blender/scripts/preview.py").read())

def slot_colours():
    for m in bpy.data.materials:
        p = m.name.split("__")
        if len(p) > 1 and len(p[1]) >= 6:
            try:
                m.diffuse_color = tuple(int(p[1][i:i + 2], 16) / 255 for i in (0, 2, 4)) + (1,)
            except ValueError:
                pass

def preview_lineup(names, path, yaw=-15, clip=None, t=0.0, spacing=1.0, center_z=0.95, dist=6.2, pitch=6, size=(1400, 800), lens=45):
    slot_colours()
    keep = {}
    for i, n in enumerate(names):
        rig = bpy.data.objects[n + "_rig"]
        keep[n] = (rig.location.copy(), rig.data.pose_position, [tr.mute for tr in rig.animation_data.nla_tracks], [tr.is_solo for tr in rig.animation_data.nla_tracks])
        rig.location = ((i - (len(names) - 1) / 2) * spacing, 0, 0)
        for tr in rig.animation_data.nla_tracks:
            tr.mute = clip is None or tr.name != clip
        rig.data.pose_position = 'POSE' if clip else 'REST'
    show = set(names) | {n + "_rig" for n in names} | {"PreviewCam"}
    for o in bpy.data.objects:
        o.hide_render = o.name not in show
    preview(path, center=(0, 0, center_z), dist=dist, yaw=yaw, pitch=pitch, frame=1 + round(t * 30), lens=lens, size=size)
    for n in names:
        rig = bpy.data.objects[n + "_rig"]
        loc, pose, mutes, solos = keep[n]
        rig.location = loc
        rig.data.pose_position = pose
        for tr, m in zip(rig.animation_data.nla_tracks, mutes):
            tr.mute = m
    for o in bpy.data.objects:
        o.hide_render = False
    return path
