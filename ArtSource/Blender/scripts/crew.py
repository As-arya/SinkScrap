# Crew characters and the island's diver merchant (rebuilt 2026-10-16 on charbody.py: one continuous skinned body per
# character instead of stacked primitives). One shared 1.8 m skeleton (charlib.CREW_BONES); each outfit is its own mesh +
# armature so every FBX carries its own clips. Crew clips: idle, walk, run, crouch, swim, climb. Merchant: idle, wave.
exec(open(r"D:/UNITYProjecy/SinkScrapt/SinkScrapt/ArtSource/Blender/scripts/charlib.py").read())
exec(open(r"D:/UNITYProjecy/SinkScrapt/SinkScrapt/ArtSource/Blender/scripts/charbody.py").read())

SKIN = (0.72, 0.55, 0.44)
SKIN_DARK = (0.58, 0.42, 0.33)
BLACK = (0.07, 0.07, 0.08)
SOLE = C((0.05, 0.05, 0.05))
HEAD_C = (0, -0.004, 1.70)
HEAD_S = (0.088, 0.104, 0.118)

def side_name(base, s):
    return base + (".L" if s > 0 else ".R")

# ---- Body ---------------------------------------------------------------------------------------------------------------------
def crew_nodes(k=1.0, a=1.0, l=1.0, belly=0.0):
    """Skin nodes for the crew skeleton: k = torso bulk, a = arm bulk, l = leg bulk (clothing thickness)."""
    n = {
        "pelvis": ((0, 0.0, 0.93), (0.15 * k, 0.115 * k)),
        "waist": ((0, 0.004, 1.08), (0.145 * k, (0.105 + belly) * k)),
        "chest": ((0, 0.0, 1.27), (0.178 * k, 0.122 * k)),
        "upper": ((0, 0.01, 1.40), (0.165 * k, 0.106 * k)),
        "neck0": ((0, 0.012, 1.48), (0.064, 0.064)),
        "neck1": ((0, 0.0, 1.59), (0.05, 0.055)),
    }
    for s in (1, -1):
        t = ".L" if s > 0 else ".R"
        n["sh" + t] = ((s * 0.178, 0.0, 1.415), (0.068 * a, 0.068 * a))
        n["el" + t] = ((s * 0.27, 0.0, 1.13), (0.051 * a, 0.051 * a))
        n["wr" + t] = ((s * 0.309, -0.019, 0.888), (0.041 * a, 0.039 * a))
        n["hip" + t] = ((s * 0.098, 0.0, 0.89), (0.09 * l, 0.092 * l))
        n["kn" + t] = ((s * 0.11, 0.0, 0.5), (0.063 * l, 0.066 * l))
        n["calf" + t] = ((s * 0.11, 0.014, 0.33), (0.06 * l, 0.064 * l))
        n["an" + t] = ((s * 0.11, 0.02, 0.13), (0.048 * l, 0.053 * l))
    return n

CREW_EDGES = [("pelvis", "waist"), ("waist", "chest"), ("chest", "upper"), ("upper", "neck0"), ("neck0", "neck1")]
for _t in (".L", ".R"):
    CREW_EDGES += [("upper", "sh" + _t), ("sh" + _t, "el" + _t), ("el" + _t, "wr" + _t),
                   ("pelvis", "hip" + _t), ("hip" + _t, "kn" + _t), ("kn" + _t, "calf" + _t), ("calf" + _t, "an" + _t)]

def square_shoulders(v):
    """Fills the slope between neck and shoulder a little and gives the chest some front."""
    if 1.3 < v.z < 1.47 and abs(v.x) < 0.22:
        v.z += 0.012 * (1 - abs(abs(v.x) - 0.12) / 0.12) if abs(v.x) < 0.24 else 0
    if 1.18 < v.z < 1.40 and v.y < 0:
        v.y -= 0.012 * (1 - abs(v.z - 1.29) / 0.11)
    if 0.84 < v.z < 1.0 and v.y > 0:
        v.y += 0.012 * (1 - abs(v.z - 0.92) / 0.08)
    return v

def outfit_rule(jacket, pants, boots, cuff=None, hem=0.8, boot_top=0.42, neck=SKIN, collar_top=1.53, belt=None, belt_z=(0.9, 0.97), apron=None):
    def rule(c, bone):
        z = c.z
        # A work apron painted on the front of the body, so it bends with it (chest bib to mid-thigh).
        if apron and c.y < -0.04 and abs(c.x) < 0.13 - max(0.0, z - 1.3) * 0.5 and 0.7 < z < 1.42 and not bone.startswith(("UpperArm", "LowerArm", "Hand")):
            return C(apron)
        if bone in ("Neck", "Head"):
            return C(neck) if z > collar_top else C(jacket)
        if bone.startswith(("Hand",)):
            return C(cuff or jacket)
        if bone.startswith(("UpperArm", "LowerArm")):
            return C(cuff) if cuff and z < 0.93 else C(jacket)
        if bone.startswith("LowerLeg") or bone.startswith("Foot"):
            return C(boots) if z < boot_top else C(pants)
        if bone.startswith("UpperLeg"):
            return C(jacket) if z > hem else C(pants)
        if belt and belt_z[0] < z < belt_z[1]:
            return C(belt)
        if bone == "Hips":
            return C(jacket) if z > hem else C(pants)
        return C(jacket)
    return rule

def common_parts(b, skin, gloves, boots, glove_size=1.0, boot_size=1.0, cap=None, head_size=HEAD_S, beard=None, brow=None):
    front = head(b, HEAD_C, head_size, C(skin), brow=0.06, jaw=0.3)
    face(b, HEAD_C, front, C(skin), brow_col=brow or (0.25, 0.18, 0.12))
    if beard:
        b.ball((0, front + 0.035, HEAD_C[2] - 0.075), (0.085, 0.06, 0.07), C(beard), "Head", seg=(10, 6))
        b.box((0, front - 0.004, HEAD_C[2] - 0.035), (0.075, 0.02, 0.022), C(beard), "Head")
    for s in (1, -1):
        glove(b, (s * 0.309, -0.019, 0.875), (0.01 * s, -0.01, -1), C(gloves), s, side_name("Hand", s), glove_size)
        boot(b, (s * 0.11, 0.02, 0.1), C(boots), SOLE, side_name("Foot", s), boot_size, C(cap) if cap else None, side_name("LowerLeg", s))
    return front

def beanie(b, color, lamp=False, z=1.775):
    """Knit cap: the rim sits above the brow (eyes at ~1.72), the crown rounds off over the skull."""
    b.ball((0, 0.006, z), (0.094, 0.108, 0.082), C(color), "Head", seg=(12, 6))
    b.cyl((0, 0.006, z - 0.022), (0, 0.006, z + 0.012), 0.097, 0.099, C(color), "Head", seg=12)
    if lamp:
        b.cyl((0, 0.006, z - 0.006), (0, 0.006, z + 0.016), 0.102, 0.102, C((0.15, 0.15, 0.15)), "Head", seg=12)
        b.box((0, -0.104, z + 0.004), (0.065, 0.036, 0.045), C((0.2, 0.2, 0.2)), "Head")
        b.cyl((0, -0.12, z + 0.004), (0, -0.134, z + 0.004), 0.018, 0.018, G((1.0, 0.95, 0.75)), "Head", seg=8)

# ---- Animation ----------------------------------------------------------------------------------------------------------------
def gait(t, period, stride, swing, lean, bob, knee=1.2):
    w = wave(t, period)
    c = wave(t, period, 0.25)
    return {
        "Hips": ('loc', (0, 0, bob * abs(c) - bob * 0.5)),
        "Spine": (lean * 0.6, 0, w * 4), "Chest": (lean * 0.4, w * 3, -w * 4), "Neck": (-lean * 0.5, 0, 0), "Head": (-lean * 0.3, 0, w * 3),
        "UpperLeg.L": (-w * stride, 0, 0), "UpperLeg.R": (w * stride, 0, 0),
        "LowerLeg.L": (max(0, -c) * stride * knee + 4, 0, 0), "LowerLeg.R": (max(0, c) * stride * knee + 4, 0, 0),
        "Foot.L": (w * 6, 0, 0), "Foot.R": (-w * 6, 0, 0),
        "UpperArm.L": (w * swing, 0, 4), "UpperArm.R": (-w * swing, 0, -4),
        "LowerArm.L": (-10 - max(0, -w) * swing * 0.6, 0, 0), "LowerArm.R": (-10 - max(0, w) * swing * 0.6, 0, 0),
    }

def idle(t, period=2.6):
    br = wave(t, period)
    return {"Hips": ('loc', (0, 0, 0.004 * br)), "Spine": (1 + br, 0, 0), "Chest": (br * 1.5, 0, 0), "Neck": (0, 0, 0),
            "Head": (wave(t, period * 2) * 2, wave(t, period * 3) * 6, 0),
            "UpperArm.L": (br * 2, 0, 3), "UpperArm.R": (br * 2, 0, -3), "LowerArm.L": (-8, 0, 0), "LowerArm.R": (-8, 0, 0)}

def crouch(t, period=1.3):
    w = wave(t, period)
    c = wave(t, period, 0.25)
    return {"Hips": ('loc', (0, 0, -0.34 + 0.015 * abs(c))), "Spine": (22, 0, 0), "Chest": (10, 0, 0), "Neck": (-18, 0, 0), "Head": (-10, 0, 0),
            "UpperLeg.L": (-70 - w * 14, 0, 4), "UpperLeg.R": (-70 + w * 14, 0, -4),
            "LowerLeg.L": (100 + max(0, -c) * 12, 0, 0), "LowerLeg.R": (100 + max(0, c) * 12, 0, 0),
            "Foot.L": (-28, 0, 0), "Foot.R": (-28, 0, 0),
            "UpperArm.L": (-25 + w * 10, 0, 6), "UpperArm.R": (-25 - w * 10, 0, -6), "LowerArm.L": (-35, 0, 0), "LowerArm.R": (-35, 0, 0)}

def swim(t, period=1.4):
    w = wave(t, period)
    c = wave(t, period, 0.25)
    k = wave(t, period * 0.5)
    return {"Hips": ('loc', (0, 0.1, 0.25)), "Spine": (55, 0, w * 5), "Chest": (15, 0, 0), "Neck": (-45, 0, 0), "Head": (-20, 0, 0),
            "UpperArm.L": (-150 + 70 * (c * 0.5 + 0.5), 0, 30 * w + 20), "UpperArm.R": (-150 + 70 * (-c * 0.5 + 0.5), 0, -30 * w - 20),
            "LowerArm.L": (-20 - 20 * max(0, w), 0, 0), "LowerArm.R": (-20 - 20 * max(0, -w), 0, 0),
            "UpperLeg.L": (20 + k * 18, 0, 6), "UpperLeg.R": (20 - k * 18, 0, -6),
            "LowerLeg.L": (18 + max(0, k) * 25, 0, 0), "LowerLeg.R": (18 + max(0, -k) * 25, 0, 0),
            "Foot.L": (40, 0, 0), "Foot.R": (40, 0, 0)}

def climb(t, period=1.0):
    w = wave(t, period)
    return {"Hips": ('loc', (0, 0.12, 0)), "Spine": (6, 0, 0), "Chest": (4, 0, 0), "Neck": (-14, 0, 0), "Head": (-20, 0, 0),
            "UpperArm.L": (-150 + w * 25, 0, -10), "UpperArm.R": (-150 - w * 25, 0, 10),
            "LowerArm.L": (-30 - max(0, w) * 30, 0, 0), "LowerArm.R": (-30 - max(0, -w) * 30, 0, 0),
            "UpperLeg.L": (-40 - w * 25, 0, 6), "UpperLeg.R": (-40 + w * 25, 0, -6),
            "LowerLeg.L": (60 + max(0, w) * 20, 0, 0), "LowerLeg.R": (60 + max(0, -w) * 20, 0, 0), "Foot.L": (-10, 0, 0), "Foot.R": (-10, 0, 0)}

def crew_clips(arm):
    sampled(arm, "idle", 2.6, idle, steps=8)
    sampled(arm, "walk", 1.0, lambda t: gait(t, 1.0, 26, 22, 4, 0.035), steps=12)
    sampled(arm, "run", 0.66, lambda t: gait(t, 0.66, 40, 40, 14, 0.06, 1.6), steps=10)
    sampled(arm, "crouch", 1.3, crouch, steps=10)
    sampled(arm, "swim", 1.4, swim, steps=10)
    sampled(arm, "climb", 1.0, climb, steps=8)

def merchant_clips(arm):
    sampled(arm, "idle", 3.4, lambda t: idle(t, 3.4), steps=8)
    def wave_hand(t):
        p = idle(t, 3.4)
        up = min(1.0, t / 0.4, (2.4 - t) / 0.5) if t < 2.4 else 0.0
        up = max(0.0, up)
        p["UpperArm.R"] = (-10 * up, 145 * up, 0)
        p["LowerArm.R"] = (0, 15 * up + wave(t, 0.6) * 25 * up, -40 * up)
        p["Head"] = (-4 * up, -10 * up, 0)
        return p
    sampled(arm, "wave", 2.4, wave_hand, steps=16, loop=False)

# ---- Outfits ------------------------------------------------------------------------------------------------------------------
def make(name, nodes, rule, parts, clips, collection="char"):
    for o in [o for o in bpy.data.objects if o.name in (name, name + "_rig", name + "_parts")]:
        bpy.data.objects.remove(o, do_unlink=True)
    for a in [a for a in bpy.data.actions if a.name.startswith(name + "_rig")]:
        bpy.data.actions.remove(a)
    arm = make_armature(name + "_rig", CREW_BONES, collection)
    me = skin_mesh(name, nodes, CREW_EDGES, "pelvis")
    shape(me, square_shoulders)
    body = build_character(name, arm, me, bone_segments(arm), rule, parts, collection)
    clips(arm)
    return arm, body

def parka_parts(b):
    green, dark, tan, fur = C((0.17, 0.31, 0.21)), C((0.11, 0.2, 0.14)), C((0.55, 0.47, 0.32)), C((0.62, 0.56, 0.44))
    common_parts(b, SKIN, BLACK, BLACK)
    beanie(b, BLACK)
    for s in (1, -1):
        b.box((s * 0.084, -0.03, 1.715), (0.018, 0.045, 0.06), C((0.30, 0.20, 0.13)), "Head")
    # Hood bunched behind the neck and a fur-trimmed collar.
    b.ball((0, 0.1, 1.5), (0.13, 0.075, 0.075), green, "Chest", seg=(10, 6))
    b.cyl((0, 0.005, 1.475), (0, 0.005, 1.535), 0.088, 0.082, fur, "Neck", seg=12)
    # Zip, chest pocket flaps, belt and pouches; the coat flares over the hips.
    b.box((0, -0.124, 1.2), (0.016, 0.01, 0.42), dark, "Spine")
    for s in (1, -1):
        b.box((s * 0.085, -0.127, 1.33), (0.09, 0.012, 0.035), dark, "Chest")
        b.box((s * 0.13, -0.135, 0.985), (0.075, 0.035, 0.07), tan, "Hips")
    b.cyl((0, 0.0, 0.955), (0, 0.0, 0.78), 0.172, 0.2, green, "Hips", seg=14, squash=(1, 0.78))
    b.cyl((0, 0.0, 0.945), (0, 0.0, 0.975), 0.176, 0.176, tan, "Hips", seg=14, squash=(1, 0.8))

def medic_parts(b):
    apron, red, white, bag = C((0.20, 0.31, 0.24)), C((0.62, 0.12, 0.10)), C((0.82, 0.80, 0.74)), C((0.30, 0.31, 0.22))
    common_parts(b, SKIN, BLACK, (0.11, 0.13, 0.11))
    front = HEAD_C[1] - HEAD_S[1] * (0.55 + 0.45 * 0.4)
    b.ball((0, front + 0.045, HEAD_C[2] - 0.07), (0.08, 0.06, 0.055), C((0.36, 0.30, 0.26)), "Head", seg=(8, 5))
    beanie(b, BLACK, lamp=True)
    b.box((0, -0.134, 1.27), (0.1, 0.012, 0.075), C((0.15, 0.24, 0.18)), "Chest")
    b.box((0.13, -0.02, 1.25), (0.03, 0.26, 0.032), C((0.25, 0.27, 0.2)), "Chest", rot=(0, 38, 0))
    b.box((-0.205, -0.02, 0.97), (0.09, 0.24, 0.19), bag, "Hips")
    b.box((-0.252, -0.02, 0.99), (0.01, 0.07, 0.022), red, "Hips")
    b.box((-0.252, -0.02, 0.99), (0.01, 0.022, 0.07), red, "Hips")
    b.cyl((0.217, 0, 1.32), (0.232, 0, 1.25), 0.072, 0.07, white, "UpperArm.L", seg=10)
    b.box((0.29, -0.005, 1.285), (0.008, 0.036, 0.013), red, "UpperArm.L")
    b.box((0.29, -0.005, 1.285), (0.008, 0.013, 0.036), red, "UpperArm.L")

def engineer_parts(b):
    refl, olive, tan, hair = C((0.72, 0.75, 0.74)), C((0.28, 0.27, 0.18)), (0.48, 0.42, 0.30), C((0.27, 0.21, 0.15))
    common_parts(b, SKIN, tan, BLACK)
    front = HEAD_C[1] - HEAD_S[1] * (0.55 + 0.45 * 0.4)
    b.ball((0, front + 0.045, HEAD_C[2] - 0.07), (0.08, 0.06, 0.055), C((0.36, 0.30, 0.26)), "Head", seg=(8, 5))
    b.ball((0, 0.01, 1.752), (0.094, 0.108, 0.078), hair, "Head", seg=(12, 6))
    for i in range(7):
        a = i / 6.0 * math.pi
        b.box((math.cos(a) * 0.062, 0.025 - math.sin(a) * 0.045, 1.81), (0.035, 0.035, 0.045), hair, "Head", rot=(20, 0, i * 25))
    b.cyl((0, 0.004, 1.735), (0, 0.004, 1.758), 0.1, 0.1, C((0.15, 0.15, 0.15)), "Head", seg=12)
    for s in (1, -1):
        b.cyl((s * 0.04, -0.098, 1.765), (s * 0.04, -0.128, 1.772), 0.028, 0.026, C((0.55, 0.6, 0.58)), "Head", seg=8)
        b.cyl((s * 0.04, -0.126, 1.772), (s * 0.04, -0.13, 1.773), 0.02, 0.02, G((0.35, 0.55, 0.55)), "Head", seg=8)
    b.box((0, -0.124, 1.2), (0.018, 0.01, 0.42), C((0.32, 0.34, 0.36)), "Spine")
    b.box((0.08, -0.13, 1.35), (0.065, 0.01, 0.035), refl, "Chest")
    for s in (1, -1):
        b.box((s * 0.19, 0.0, 1.36), (0.03, 0.2, 0.024), refl, "Chest")
        b.box((s * 0.14, -0.135, 0.96), (0.075, 0.05, 0.1), olive, "Hips")
        b.cyl((s * 0.226, 0, 1.31), (s * 0.24, 0, 1.25), 0.073, 0.071, refl, side_name("UpperArm", s), seg=10)
        b.cyl((s * 0.11, 0.004, 0.36), (s * 0.11, 0.008, 0.31), 0.068, 0.068, refl, side_name("LowerLeg", s), seg=10)
    b.cyl((0, 0.0, 0.93), (0, 0.0, 0.965), 0.162, 0.162, olive, "Hips", seg=14, squash=(1, 0.8))
    b.box((-0.2, -0.03, 0.93), (0.03, 0.04, 0.14), C((0.5, 0.5, 0.52)), "Hips")

def fisher_parts(b):
    yellow, navy, white, brown = C((0.86, 0.68, 0.12)), C((0.10, 0.13, 0.22)), (0.86, 0.85, 0.82), C((0.35, 0.22, 0.12))
    common_parts(b, SKIN_DARK, (0.17, 0.22, 0.17), (0.16, 0.25, 0.16), beard=white, brow=white)
    # Sou'wester: crown, wide brim, long neck flap at the back.
    b.ball((0, 0.012, 1.76), (0.102, 0.114, 0.085), yellow, "Head", seg=(12, 6))
    b.cyl((0, 0.02, 1.728), (0, 0.02, 1.742), 0.17, 0.19, yellow, "Head", seg=14, squash=(1, 1.08))
    b.box((0, 0.15, 1.66), (0.19, 0.07, 0.13), yellow, "Head", rot=(18, 0, 0))
    b.cyl((0, 0.005, 1.47), (0, 0.005, 1.53), 0.085, 0.078, navy, "Neck", seg=12)
    b.box((0, -0.126, 1.2), (0.026, 0.012, 0.44), C((0.62, 0.48, 0.08)), "Spine")
    for z in (1.33, 1.21, 1.09):
        b.box((0.03, -0.132, z), (0.045, 0.012, 0.014), C(BLACK), "Chest" if z > 1.2 else "Spine")
    for s in (1, -1):
        b.box((s * 0.09, -0.124, 1.39), (0.065, 0.01, 0.028), C((0.75, 0.75, 0.7)), "Chest")
        b.cyl((s * 0.215, -0.02, 0.93), (s * 0.215, -0.02, 0.81), 0.035, 0.035, brown, "Hips", seg=6)
        b.ball((s * 0.215, -0.02, 0.87), (0.024, 0.024, 0.035), G((1.0, 0.65, 0.25)), "Hips", seg=(6, 4))
    # Long oilskin coat over the thighs.
    b.cyl((0, 0.0, 0.96), (0, 0.0, 0.66), 0.17, 0.205, yellow, "Hips", seg=14, squash=(1, 0.78))
    b.box((0.12, -0.15, 0.97), (0.075, 0.04, 0.075), C((0.42, 0.36, 0.24)), "Hips")

def diver_parts(b):
    canvas, brass, leather, rust, steel, glass = C((0.60, 0.58, 0.48)), C((0.66, 0.48, 0.20)), C((0.32, 0.20, 0.12)), C((0.45, 0.24, 0.14)), C((0.34, 0.36, 0.36)), C((0.05, 0.08, 0.08))
    for s in (1, -1):
        glove(b, (s * 0.309, -0.019, 0.875), (0.01 * s, -0.01, -1), C((0.52, 0.52, 0.47)), s, side_name("Hand", s), 1.3)
        boot(b, (s * 0.11, 0.02, 0.1), C((0.22, 0.24, 0.22)), SOLE, side_name("Foot", s), 1.25, brass, side_name("LowerLeg", s))
    # Corselet (breastplate) and the copper helmet: a front window with a cross guard, two side ports, a top valve.
    b.cyl((0, 0, 1.4), (0, 0, 1.52), 0.3, 0.17, brass, "Chest", seg=14, squash=(1, 0.8))
    for i in range(10):
        a = 2 * math.pi * i / 10
        b.ball((math.cos(a) * 0.26, math.sin(a) * 0.21, 1.43), (0.015, 0.015, 0.015), steel, "Chest", seg=(4, 3))
    b.ball((0, -0.01, 1.71), (0.2, 0.2, 0.2), brass, "Head", seg=(14, 10))
    b.cyl((0, 0, 1.52), (0, 0, 1.56), 0.14, 0.15, brass, "Neck", seg=12)
    b.cyl((0, -0.17, 1.7), (0, -0.215, 1.7), 0.095, 0.085, brass, "Head", seg=12)
    b.cyl((0, -0.2, 1.7), (0, -0.22, 1.7), 0.075, 0.075, glass, "Head", seg=12)
    for d in (-1, 1):
        b.box((d * 0.035, -0.222, 1.7), (0.01, 0.01, 0.15), brass, "Head")
        b.box((0, -0.222, 1.7 + d * 0.035), (0.15, 0.01, 0.01), brass, "Head")
        b.cyl((d * 0.17, -0.06, 1.72), (d * 0.215, -0.07, 1.72), 0.06, 0.055, brass, "Head", seg=10)
        b.cyl((d * 0.2, -0.065, 1.72), (d * 0.222, -0.07, 1.72), 0.045, 0.045, glass, "Head", seg=10)
    b.cyl((0, -0.01, 1.9), (0, -0.01, 1.94), 0.05, 0.04, brass, "Head", seg=8)
    for s in (1, -1):
        b.box((s * 0.1, -0.2, 1.2), (0.05, 0.02, 0.4), leather, "Chest")
        b.box((s * 0.12, -0.215, 1.04), (0.09, 0.04, 0.07), steel, "Spine")
        b.cyl((s * 0.1, 0.22, 1.0), (s * 0.1, 0.22, 1.42), 0.08, 0.08, rust, "Chest", seg=10)
        b.ball((s * 0.1, 0.22, 1.43), (0.08, 0.08, 0.05), rust, "Chest", seg=(8, 4))
        b.cyl((s * 0.16, 0.2, 1.45), (s * 0.18, 0.02, 1.72), 0.022, 0.022, C((0.22, 0.26, 0.2)), "Chest", seg=6)
    b.cyl((0, 0.0, 0.93), (0, 0.0, 0.98), 0.215, 0.215, leather, "Hips", seg=14, squash=(1, 0.8))
    b.box((0.3, -0.05, 0.96), (0.02, 0.05, 0.22), C((0.5, 0.35, 0.2)), "Hips")

GREEN, OLIVE, NAVY, YELLOW, GREY, CANVAS = (0.17, 0.31, 0.21), (0.20, 0.22, 0.16), (0.15, 0.18, 0.22), (0.86, 0.68, 0.12), (0.20, 0.21, 0.22), (0.60, 0.58, 0.48)
OUTFITS = [
    ("char_crew_parka", crew_nodes(1.12, 1.15, 1.05), outfit_rule(GREEN, OLIVE, BLACK, cuff=(0.11, 0.2, 0.14), hem=0.78, boot_top=0.44, collar_top=1.54), parka_parts, crew_clips),
    ("char_crew_medic", crew_nodes(1.05, 1.05, 1.0), outfit_rule(NAVY, (0.10, 0.10, 0.11), (0.11, 0.13, 0.11), hem=0.86, boot_top=0.42, collar_top=1.55, apron=(0.20, 0.31, 0.24)), medic_parts, crew_clips),
    ("char_crew_engineer", crew_nodes(1.04, 1.05, 1.03), outfit_rule((0.13, 0.19, 0.35), (0.13, 0.19, 0.35), BLACK, hem=0.86, boot_top=0.22, collar_top=1.55), engineer_parts, crew_clips),
    ("char_crew_fisher", crew_nodes(1.1, 1.12, 1.04, belly=0.012), outfit_rule(YELLOW, GREY, (0.16, 0.25, 0.16), hem=0.66, boot_top=0.45, collar_top=1.56), fisher_parts, crew_clips),
    ("npc_diver_merchant", crew_nodes(1.3, 1.35, 1.3), outfit_rule(CANVAS, CANVAS, (0.22, 0.24, 0.22), boot_top=0.3, neck=CANVAS, collar_top=9), diver_parts, merchant_clips),
]

OUT = r"D:/UNITYProjecy/SinkScrapt/SinkScrapt/Assets/SinkScrapt/Resources/SinkScrapt/Models/Characters"
built = {}
for name, nodes, rule, parts, clips in OUTFITS:
    arm, body = make(name, nodes, rule, parts, clips, "npc" if name.startswith("npc") else "char")
    built[name] = tris(body)
result = built
