# Hull stalker (rebuilt 2026-10-16): a gaunt, too-tall grey humanoid, about 2.35 m, hunched, with a sunken belly, ribs
# standing out under wet skin, arms that hang to the knees and long clawed fingers. No eyes to speak of: two dim pinpricks
# in deep sockets. Its lower jaw is on its own bone and drops wide for the scream. One continuous skinned body
# (charbody.py) plus rigid parts (ribs, head, jaw, hands, feet). Clips: idle, walk, run, attack (wind-up 0.65 s, matches
# `attackWindUp`), grab (lunge 0.45 s, hold, throw at ~1.9 s, matches NetEnemy.GrabLunge/GrabHold) and scream (1.3 s,
# matches `screamSeconds`). Pose angles are armature-axis Euler degrees: +X tilts an upright bone forward, swings a
# hanging limb back (-X forward), and opens the jaw.
exec(open(r"D:/UNITYProjecy/SinkScrapt/SinkScrapt/ArtSource/Blender/scripts/charlib.py").read())
exec(open(r"D:/UNITYProjecy/SinkScrapt/SinkScrapt/ArtSource/Blender/scripts/charbody.py").read())
NAME = "mon_hull_stalker"
for o in [o for o in bpy.data.objects if o.name in (NAME, NAME + "_rig", NAME + "_parts")]:
    bpy.data.objects.remove(o, do_unlink=True)
for a in [a for a in bpy.data.actions if a.name.startswith(NAME)]:
    bpy.data.actions.remove(a)

BONES = mirror([
    ("Hips", (0, 0, 0.98), (0, 0, 1.18), None),
    ("Spine", (0, 0, 1.18), (0, -0.04, 1.5), "Hips"),
    ("Chest", (0, -0.04, 1.5), (0, -0.14, 1.86), "Spine"),
    ("Neck", (0, -0.14, 1.86), (0, -0.3, 1.98), "Chest"),
    ("Head", (0, -0.3, 1.98), (0, -0.44, 2.3), "Neck"),
    ("Jaw", (0, -0.33, 2.0), (0, -0.5, 1.95), "Head"),
    ("UpperArm.L", (0.25, -0.1, 1.77), (0.43, -0.12, 1.3), "Chest"),
    ("LowerArm.L", (0.43, -0.12, 1.3), (0.52, -0.17, 0.83), "UpperArm.L"),
    ("Hand.L", (0.52, -0.17, 0.83), (0.55, -0.21, 0.5), "LowerArm.L"),
    ("UpperLeg.L", (0.12, 0, 0.99), (0.18, -0.05, 0.55), "Hips"),
    ("LowerLeg.L", (0.18, -0.05, 0.55), (0.19, 0.03, 0.12), "UpperLeg.L"),
    ("Foot.L", (0.19, 0.03, 0.12), (0.19, -0.22, 0.03), "LowerLeg.L"),
])
arm = make_armature(NAME + "_rig", BONES, "mon")

SKIN = (0.42, 0.45, 0.42)
SKIN_DARK = (0.24, 0.26, 0.25)
RIB = (0.52, 0.53, 0.49)
MOUTH = (0.24, 0.05, 0.05)
TOOTH = (0.80, 0.76, 0.60)
CLAW = (0.12, 0.10, 0.08)
EYE = (0.95, 0.80, 0.50)

nodes = {
    "pelvis": ((0, 0.0, 1.0), (0.12, 0.092)),
    "waist": ((0, 0.018, 1.17), (0.092, 0.066)),
    "ribs": ((0, -0.03, 1.43), (0.17, 0.13)),
    "upper": ((0, -0.1, 1.71), (0.2, 0.118)),
    "neck0": ((0, -0.17, 1.86), (0.056, 0.056)),
    "neck1": ((0, -0.27, 1.96), (0.046, 0.048)),
}
edges = [("pelvis", "waist"), ("waist", "ribs"), ("ribs", "upper"), ("upper", "neck0"), ("neck0", "neck1")]
for s, t in ((1, ".L"), (-1, ".R")):
    nodes["sh" + t] = ((s * 0.25, -0.1, 1.76), (0.058, 0.058))
    nodes["el" + t] = ((s * 0.43, -0.12, 1.3), (0.037, 0.037))
    nodes["wr" + t] = ((s * 0.52, -0.17, 0.84), (0.03, 0.028))
    nodes["hip" + t] = ((s * 0.12, 0.0, 0.98), (0.072, 0.076))
    nodes["kn" + t] = ((s * 0.18, -0.05, 0.55), (0.044, 0.047))
    nodes["calf" + t] = ((s * 0.185, -0.01, 0.36), (0.05, 0.056))
    nodes["an" + t] = ((s * 0.19, 0.03, 0.14), (0.032, 0.034))
    edges += [("upper", "sh" + t), ("sh" + t, "el" + t), ("el" + t, "wr" + t), ("pelvis", "hip" + t), ("hip" + t, "kn" + t), ("kn" + t, "calf" + t), ("calf" + t, "an" + t)]

def emaciate(v):
    # Sunken belly under the ribcage, a jutting collarbone ridge, a knobbly spine line down the back.
    if 1.05 < v.z < 1.3 and v.y < 0:
        v.y += 0.03 * (1 - abs(v.z - 1.18) / 0.13)
    if 1.62 < v.z < 1.8 and v.y < -0.1 and abs(v.x) < 0.2:
        v.y -= 0.012
    if v.y > 0.05 and abs(v.x) < 0.03 and 1.1 < v.z < 1.8:
        v.y += 0.015
    return v

STAIN = (0.30, 0.13, 0.10)

def _noise(p, k=9.0):
    return 0.5 + 0.5 * math.sin(p.x * k * 1.7 + 1.3) * math.sin(p.y * k * 1.3 + 0.7) * math.sin(p.z * k + 2.1)

def rule(c, bone):
    # Dark, dirty extremities; old blood smeared down the chest and over the hands; mottled grey elsewhere.
    if bone.startswith(("Hand", "Foot")) or c.z < 0.3:
        return C(STAIN) if bone.startswith("Hand") and _noise(c, 14) > 0.75 else C(SKIN_DARK)
    if bone.startswith("LowerArm") and c.z < 1.0:
        return C(tuple((a + b) / 2 for a, b in zip(SKIN, SKIN_DARK)))
    return C(SKIN_DARK) if _noise(c, 6) > 0.82 else C(SKIN)

def surface(origin, direction, reach=0.24):
    """Where a ray from the spine axis leaves the torso skin (BVH of the body mesh); None past `reach` (an arm)."""
    hit = BVH.ray_cast(Vector(origin), Vector(direction).normalized(), reach)
    return (hit[0], hit[1]) if hit[0] is not None else None

def parts(b):
    sk, dark, rib, mouth, tooth, claw = C(SKIN), C(SKIN_DARK), C(RIB), C(MOUTH), C(TOOTH), C(CLAW)
    # Ribs: thin bars hugging the skin of the ribcage (found by ray casts from the spine axis), running down and
    # forward from the back of the flank to the breastbone, as real ribs do.
    for i, z in enumerate((1.4, 1.46, 1.52, 1.58)):
        for s in (1, -1):
            pts = []
            for k in range(6):
                a = math.radians(-95 + k * 16)
                axis = Vector((0, -0.03 - (z - 1.4) * 0.3, z + 0.05 - k * 0.012))
                hit = surface(axis, (s * math.cos(a), math.sin(a), 0))
                if hit:
                    pts.append(hit[0] + hit[1] * 0.004)
            for p, q in zip(pts, pts[1:]):
                b.cyl(p, q, 0.008, 0.008, rib, "Chest" if z > 1.5 else "Spine", seg=5)
    # The skull: long and narrow, swept back over the neck and tipped forward; hollow cheeks, a heavy brow, no nose.
    hc = (0, -0.33, 2.06)
    tilt = 22
    def skull(x, y, z):
        if y > 0:
            y *= 1.45
            z += 0.25 * y
        if y < -0.2 and -0.45 < z < 0.1 and abs(x) > 0.35:
            x *= 0.82
        if y < -0.3 and 0.12 < z < 0.38:
            y -= 0.12 * (1 - abs(z - 0.25) / 0.13)
        if z < -0.3:
            x *= 0.8
        return x, y, z
    head(b, hc, (0.09, 0.13, 0.12), sk, "Head", seg=(16, 12), jaw=0.1, flat=0.65, tilt=tilt, extra=skull)
    def at(dx, dy, dz):
        return Vector(hc) + Matrix.Rotation(math.radians(tilt), 3, 'X') @ Vector((dx, dy, dz))
    for s in (1, -1):
        # Deep, dark sockets with a dim pinprick of light far inside.
        b.ball(at(s * 0.039, -0.094, 0.022), (0.027, 0.01, 0.017), C((0.07, 0.03, 0.03)), "Head", seg=(8, 5))
        b.ball(at(s * 0.038, -0.104, 0.02), (0.004, 0.003, 0.004), G(EYE), "Head", seg=(5, 3))
        b.box(at(s * 0.011, -0.118, -0.03), (0.006, 0.008, 0.022), C((0.08, 0.06, 0.06)), "Head")
    # Upper jaw: lipless, a dark gash with a ragged row of teeth always showing.
    b.ball(at(0, -0.07, -0.085), (0.06, 0.06, 0.03), mouth, "Head", seg=(10, 6))
    for k in range(11):
        a = math.radians(-70 + k * 14)
        p = at(math.sin(a) * 0.058, -0.06 - math.cos(a) * 0.058, -0.085)
        b.cyl(p, p + Vector((0, -0.004, -0.032 - 0.012 * (k % 2))), 0.007, 0.0015, tooth, "Head", seg=4, smooth=False)
    # Lower jaw (Jaw bone): a long, narrow, tapering mandible with its own teeth; it drops wide on the scream.
    jh = Vector((0, -0.33, 2.0))
    b.ball(jh + Vector((0, -0.09, -0.022)), (0.052, 0.1, 0.022), sk, "Jaw", seg=(10, 6))
    b.ball(jh + Vector((0, -0.18, -0.03)), (0.03, 0.035, 0.022), sk, "Jaw", seg=(8, 5))
    b.ball(jh + Vector((0, -0.1, -0.008)), (0.042, 0.085, 0.012), mouth, "Jaw", seg=(8, 4))
    for k in range(11):
        a = math.radians(-70 + k * 14)
        p = jh + Vector((math.sin(a) * 0.05, -0.11 - math.cos(a) * 0.08, 0.0))
        b.cyl(p, p + Vector((0, 0.0, 0.03 + 0.012 * (k % 2))), 0.006, 0.0015, tooth, "Jaw", seg=4, smooth=False)
    # Hands: a narrow palm and four long two-jointed fingers with black claws, plus a thumb.
    for s, t in ((1, ".L"), (-1, ".R")):
        w = Vector((s * 0.52, -0.17, 0.82))
        d = (Vector((s * 0.55, -0.21, 0.5)) - w).normalized()
        b.cyl(w - d * 0.06, w + d * 0.06, 0.031, 0.032, dark, "Hand" + t, seg=8)
        b.ball(w + d * 0.06, (0.034, 0.05, 0.065), dark, "Hand" + t, seg=(8, 6))
        for f in range(4):
            off = Vector((0, -0.03 + f * 0.022, 0)) + Vector((s * 0.008 * (f - 1.5), 0, 0))
            p0 = w + d * 0.11 + off
            p1 = p0 + d * 0.12 + Vector((0, -0.01, 0))
            p2 = p1 + d * 0.1 + Vector((0, -0.025, 0))
            b.cyl(p0, p1, 0.011, 0.009, dark, "Hand" + t, seg=5)
            b.cyl(p1, p2, 0.009, 0.007, dark, "Hand" + t, seg=5)
            b.cyl(p2, p2 + d * 0.05 + Vector((0, -0.02, 0)), 0.007, 0.001, claw, "Hand" + t, seg=4, smooth=False)
        tb = w + d * 0.06 + Vector((-s * 0.02, -0.04, 0))
        b.cyl(tb, tb + Vector((-s * 0.01, -0.06, -0.06)), 0.01, 0.008, dark, "Hand" + t, seg=5)
        # Feet: an ankle reaching up into the shin, a bony heel and three long toes with claws.
        a = Vector((s * 0.19, 0.03, 0.12))
        b.cyl(a + Vector((0, 0, -0.07)), a + Vector((0, 0, 0.08)), 0.036, 0.034, dark, "LowerLeg" + t, seg=8)
        b.ball(a + Vector((0, -0.02, -0.075)), (0.05, 0.08, 0.045), dark, "Foot" + t, seg=(8, 5))
        for k in range(3):
            x = s * (0.165 + k * 0.025)
            p0 = Vector((x, -0.06, 0.035))
            p1 = Vector((x, -0.2 - 0.02 * (1 - abs(k - 1)), 0.02))
            b.cyl(p0, p1, 0.015, 0.011, dark, "Foot" + t, seg=5)
            b.cyl(p1, p1 + Vector((0, -0.05, -0.015)), 0.009, 0.001, claw, "Foot" + t, seg=4, smooth=False)

from mathutils.bvhtree import BVHTree
me = skin_mesh(NAME, nodes, edges, "pelvis")
shape(me, emaciate)
BVH = BVHTree.FromPolygons([v.co.copy() for v in me.vertices], [tuple(p.vertices) for p in me.polygons])
body = build_character(NAME, arm, me, bone_segments(arm), rule, parts, "mon")

# ---- Clips -------------------------------------------------------------------------------------------------------------------
def walk(t, period=1.2, stride=26, lean=18, arm_swing=14, bob=0.05):
    w = wave(t, period)
    c = wave(t, period, 0.25)
    return {
        "Hips": ('loc', (0, 0, -0.03 + bob * abs(c))),
        "Spine": (lean * 0.5, 0, w * 4), "Chest": (lean * 0.5 + 3, w * 5, 0), "Neck": (-10, 0, 0), "Head": (-6, -w * 4, 0),
        "Jaw": (6 + 3 * abs(w), 0, 0),
        "UpperLeg.L": (-w * stride, 0, 0), "UpperLeg.R": (w * stride, 0, 0),
        "LowerLeg.L": (max(0, -c) * stride * 1.3 + 5, 0, 0), "LowerLeg.R": (max(0, c) * stride * 1.3 + 5, 0, 0),
        "Foot.L": (w * 8, 0, 0), "Foot.R": (-w * 8, 0, 0),
        "UpperArm.L": (w * arm_swing - 6, 0, 4), "UpperArm.R": (-w * arm_swing - 6, 0, -4),
        "LowerArm.L": (-12, 0, 0), "LowerArm.R": (-12, 0, 0), "Hand.L": (-5, 0, 0), "Hand.R": (-5, 0, 0),
    }

def idle(t):
    br = wave(t, 3.0)
    return {
        "Hips": ('loc', (0, 0, -0.04 + 0.01 * br)),
        "Spine": (12 + br * 2, 0, 0), "Chest": (8 + br * 3, 0, 0), "Neck": (-8, wave(t, 3.0, 0.3) * 6, 0),
        "Head": (-4, wave(t, 6.0) * 14, wave(t, 4.5) * 6), "Jaw": (4 + 3 * max(0, br), 0, 0),
        "UpperArm.L": (-4 + br * 2, 0, 6), "UpperArm.R": (-4 + br * 2, 0, -6),
        "LowerArm.L": (-14, 0, 0), "LowerArm.R": (-14, 0, 0),
        "UpperLeg.L": (-8, 0, 0), "UpperLeg.R": (-8, 0, 0), "LowerLeg.L": (14, 0, 0), "LowerLeg.R": (14, 0, 0),
        "Foot.L": (-6, 0, 0), "Foot.R": (-6, 0, 0),
    }

sampled(arm, "idle", 3.0, idle, steps=12)
sampled(arm, "walk", 1.2, lambda t: walk(t), steps=12)
sampled(arm, "run", 0.75, lambda t: walk(t, 0.75, 40, 34, 34, 0.09), steps=10)

base = idle(0)
def attack_pose(phase):
    if phase == "wind":
        return {"Spine": (2, 0, -16), "Chest": (-6, 0, -22), "Neck": (-14, 0, 0), "Head": (-12, 0, 10), "Jaw": (38, 0, 0),
                "UpperArm.R": (-170, 0, -30), "LowerArm.R": (-40, 0, 0), "Hand.R": (-20, 0, 0),
                "UpperArm.L": (-40, 0, 20), "LowerArm.L": (-30, 0, 0),
                "UpperLeg.L": (-22, 0, 0), "LowerLeg.L": (20, 0, 0), "UpperLeg.R": (6, 0, 0), "LowerLeg.R": (18, 0, 0),
                "Hips": ('loc', (0, 0.05, -0.08))}
    if phase == "strike":
        return {"Spine": (24, 0, 18), "Chest": (22, 0, 26), "Neck": (-4, 0, 0), "Head": (0, 0, -8), "Jaw": (20, 0, 0),
                "UpperArm.R": (-60, 0, 40), "LowerArm.R": (-8, 0, 0), "Hand.R": (10, 0, 0),
                "UpperArm.L": (10, 0, 10), "LowerArm.L": (-20, 0, 0),
                "UpperLeg.L": (-34, 0, 0), "LowerLeg.L": (30, 0, 0), "UpperLeg.R": (14, 0, 0), "LowerLeg.R": (10, 0, 0),
                "Hips": ('loc', (0, -0.18, -0.12))}
    return base
clip(arm, "attack", 2.1, [(0, base), (0.55, attack_pose("wind")), (0.68, attack_pose("wind")), (0.85, attack_pose("strike")),
                         (1.15, attack_pose("strike")), (2.1, base)], loop=False)

reach = {"Spine": (30, 0, 0), "Chest": (20, 0, 0), "Neck": (-20, 0, 0), "Head": (-10, 0, 0), "Jaw": (45, 0, 0),
         "UpperArm.L": (-80, 0, -10), "UpperArm.R": (-80, 0, 10), "LowerArm.L": (-10, 0, 0), "LowerArm.R": (-10, 0, 0),
         "Hand.L": (-10, 0, 0), "Hand.R": (-10, 0, 0),
         "UpperLeg.L": (-36, 0, 0), "LowerLeg.L": (34, 0, 0), "UpperLeg.R": (16, 0, 0), "LowerLeg.R": (12, 0, 0),
         "Hips": ('loc', (0, -0.2, -0.14))}
lift = {"Spine": (-6, 0, 0), "Chest": (-14, 0, 0), "Neck": (-26, 0, 0), "Head": (-24, 0, 0), "Jaw": (50, 0, 0),
        "UpperArm.L": (-160, 0, -16), "UpperArm.R": (-160, 0, 16), "LowerArm.L": (-24, 0, 0), "LowerArm.R": (-24, 0, 0),
        "Hand.L": (-20, 0, 0), "Hand.R": (-20, 0, 0),
        "UpperLeg.L": (-12, 0, 0), "LowerLeg.L": (14, 0, 0), "UpperLeg.R": (-4, 0, 0), "LowerLeg.R": (10, 0, 0),
        "Hips": ('loc', (0, 0, -0.04))}
def shake(k):
    p = dict(lift)
    p["Chest"] = (-14, 0, 10 * k)
    p["UpperArm.L"] = (-160 + 10 * k, 0, -16)
    p["UpperArm.R"] = (-160 - 10 * k, 0, 16)
    return p
throw = {"Spine": (30, 0, -24), "Chest": (26, 0, -30), "Neck": (-6, 0, 0), "Head": (0, 0, 0), "Jaw": (30, 0, 0),
         "UpperArm.L": (-40, 0, -50), "UpperArm.R": (-30, 0, -10), "LowerArm.L": (-6, 0, 0), "LowerArm.R": (-6, 0, 0),
         "Hand.L": (10, 0, 0), "Hand.R": (10, 0, 0),
         "UpperLeg.L": (-30, 0, 0), "LowerLeg.L": (24, 0, 0), "UpperLeg.R": (12, 0, 0), "LowerLeg.R": (10, 0, 0),
         "Hips": ('loc', (0, -0.12, -0.1))}
clip(arm, "grab", 2.8, [(0, base), (0.4, reach), (0.5, reach), (1.05, lift), (1.25, shake(1)), (1.4, shake(-1)), (1.55, shake(1)),
                       (1.7, lift), (1.92, throw), (2.15, throw), (2.8, base)], loop=False)

# scream: rears back with the arms thrown wide, jaw hanging open, the whole body shuddering, then drops into the chase.
def scream_pose(k, open_=60):
    return {"Hips": ('loc', (0, 0.06, -0.02)), "Spine": (-8, 0, 2 * k), "Chest": (-16, 4 * k, 0), "Neck": (-26, 0, 0),
            "Head": (-24, 5 * k, 3 * k), "Jaw": (open_, 0, 0),
            "UpperArm.L": (-30, -60, 0), "UpperArm.R": (-30, 60, 0), "LowerArm.L": (-35, 0, 0), "LowerArm.R": (-35, 0, 0),
            "Hand.L": (-25, 0, 0), "Hand.R": (-25, 0, 0),
            "UpperLeg.L": (-14, 0, 6), "UpperLeg.R": (-6, 0, -6), "LowerLeg.L": (16, 0, 0), "LowerLeg.R": (10, 0, 0)}
clip(arm, "scream", 1.3, [(0, base), (0.22, scream_pose(0, 55)), (0.4, scream_pose(1, 62)), (0.55, scream_pose(-1, 64)),
                          (0.7, scream_pose(1, 66)), (0.85, scream_pose(-1, 64)), (1.0, scream_pose(1, 60)), (1.3, walk(0, 0.75, 40, 34, 34, 0.09))], loop=False)

result = {"tris": tris(body), "clips": [t.name for t in arm.animation_data.nla_tracks]}
