# Drowned hunter (rebuilt 2026-10-16): a pale, bloated deep-sea anglerfish about 3.2 m long. One continuous skinned body
# (charbody.py) from the huge head down to a ragged tail, an underslung lower jaw (Jaw bone) packed with needle teeth that
# gapes open on the scream, cloudy dead eyes, a glowing lure on a stalk, torn translucent fins. Origin at the body centre,
# mouth toward -Y. Bones: Root, Head, Jaw, Lure1/Lure2, Body1..3, Tail, Fin.L/.R (unchanged, so NetEnemy/EnemyRig find
# Lure2 for the glow). Clips: idle (hover), swim, chase (fast swim), bite (0.65 s wind-up, snap), scream (1.3 s).
exec(open(r"D:/UNITYProjecy/SinkScrapt/SinkScrapt/ArtSource/Blender/scripts/charlib.py").read())
exec(open(r"D:/UNITYProjecy/SinkScrapt/SinkScrapt/ArtSource/Blender/scripts/charbody.py").read())
NAME = "mon_drowned_hunter"
for o in [o for o in bpy.data.objects if o.name in (NAME, NAME + "_rig", NAME + "_parts")]:
    bpy.data.objects.remove(o, do_unlink=True)
for a in [a for a in bpy.data.actions if a.name.startswith(NAME)]:
    bpy.data.actions.remove(a)

BONES = mirror([
    ("Root", (0, 0.0, 0), (0, 0.25, 0), None),
    ("Head", (0, -0.1, 0), (0, -0.7, 0.1), "Root"),
    ("Jaw", (0, -0.15, -0.18), (0, -0.85, -0.2), "Head"),
    ("Lure1", (0, -0.45, 0.5), (0, -0.7, 1.0), "Head"),
    ("Lure2", (0, -0.7, 1.0), (0, -1.15, 0.95), "Lure1"),
    ("Body1", (0, 0.25, 0), (0, 0.75, 0), "Root"),
    ("Body2", (0, 0.75, 0), (0, 1.25, -0.05), "Body1"),
    ("Body3", (0, 1.25, -0.05), (0, 1.7, -0.1), "Body2"),
    ("Tail", (0, 1.7, -0.1), (0, 2.2, -0.15), "Body3"),
    ("Fin.L", (0.42, 0.1, -0.15), (0.75, 0.3, -0.3), "Root"),
])
arm = make_armature(NAME + "_rig", BONES, "mon")

SKIN = (0.62, 0.65, 0.63)
BELLY = (0.76, 0.77, 0.73)
BLOTCH = (0.40, 0.44, 0.43)
FIN = (0.50, 0.55, 0.56)
MOUTH = (0.20, 0.05, 0.06)
TOOTH = (0.90, 0.88, 0.78)
EYE = (0.78, 0.84, 0.82)
LURE = (0.55, 1.0, 0.85)

# Body rings along the spine (+Y): (y, centre z, half-width, half-height), from the mouth to the tail.
RINGS = [(-0.74, 0.12, 0.3, 0.2), (-0.62, 0.11, 0.46, 0.36), (-0.38, 0.1, 0.58, 0.5), (-0.06, 0.07, 0.6, 0.52),
         (0.3, 0.0, 0.5, 0.45), (0.65, -0.03, 0.38, 0.35), (1.0, -0.06, 0.27, 0.25), (1.35, -0.09, 0.17, 0.17),
         (1.7, -0.11, 0.1, 0.11), (1.95, -0.13, 0.05, 0.06), (2.04, -0.14, 0.02, 0.03)]

def _noise(p, k=5.0):
    return 0.5 + 0.5 * math.sin(p.x * k * 1.9 + 0.4) * math.sin(p.y * k * 1.1 + 2.0) * math.sin(p.z * k * 2.3 + 1.1)

def bloat(v):
    # A sagging belly under the head and gut, a flatter back.
    if v.z < -0.05 and -0.4 < v.y < 0.9:
        v.z -= 0.08 * (1 - abs(v.y - 0.25) / 0.65)
    if v.z > 0.2 and v.y > -0.3:
        v.z -= 0.03
    return v

def rule(c, bone):
    if c.z < -0.2 and c.y < 1.2:
        return C(BELLY)
    return C(BLOTCH) if c.z > 0.0 and _noise(c) > 0.7 else C(SKIN)

def parts(b):
    sk, belly, fin, mouth, tooth = C(SKIN), C(BELLY), C(FIN), C(MOUTH), C(TOOTH)
    # Mouth: a dark cavity across the front under a heavy upper lip.
    b.ball((0, -0.66, -0.02), (0.38, 0.2, 0.2), mouth, "Head", seg=(12, 6))
    b.ball((0, -0.64, 0.2), (0.42, 0.22, 0.1), sk, "Head", seg=(12, 5))
    # Underslung lower jaw (Jaw bone): juts past the upper lip; needle teeth curve up and in from both jaws.
    b.ball((0, -0.5, -0.28), (0.46, 0.44, 0.17), belly, "Jaw", seg=(12, 6))
    b.ball((0, -0.86, -0.22), (0.34, 0.12, 0.1), belly, "Jaw", seg=(10, 5))
    for i in range(11):
        x = -0.38 + i * 0.076
        y = -0.9 + x * x * 1.5
        h = 0.2 + (0.08 if i % 2 else 0) + (0.05 if i in (3, 7) else 0)
        b.cyl((x, y + 0.02, -0.17), (x * 1.04, y - 0.05, -0.17 + h), 0.026, 0.003, tooth, "Jaw", seg=4, smooth=False)
        if i < 10:
            xu = x + 0.038
            yu = -0.8 + xu * xu * 1.3
            b.cyl((xu, yu, 0.16), (xu * 1.02, yu - 0.05, 0.16 - 0.16 - (0.07 if i % 2 == 0 else 0)), 0.022, 0.003, tooth, "Head", seg=4, smooth=False)
    # Small cloudy eyes, high and far apart, ringed with dark skin.
    for s in (1, -1):
        b.ball((s * 0.36, -0.52, 0.36), (0.075, 0.06, 0.065), C(BLOTCH), "Head", seg=(8, 5))
        b.ball((s * 0.385, -0.55, 0.37), (0.05, 0.04, 0.045), C(EYE), "Head", seg=(8, 5))
        b.ball((s * 0.4, -0.575, 0.37), (0.014, 0.008, 0.014), C((0.1, 0.12, 0.12)), "Head", seg=(5, 3))
    # Lure: a stalk curving forward over the mouth and the glowing bulb (Lure2 carries the light in the game).
    b.cyl((0, -0.42, 0.52), (0, -0.7, 1.0), 0.026, 0.02, fin, "Lure1", seg=6)
    b.cyl((0, -0.7, 1.0), (0, -1.1, 0.96), 0.02, 0.015, fin, "Lure2", seg=6)
    b.ball((0, -1.15, 0.88), (0.07, 0.07, 0.1), G(LURE), "Lure2", seg=(8, 5))
    b.ball((0, -1.15, 0.97), (0.035, 0.035, 0.03), fin, "Lure2", seg=(6, 4))
    # Ragged dorsal fin and a few spines along the back.
    for k, (y, h, bone) in enumerate([(-0.1, 0.6, "Head"), (0.35, 0.52, "Body1"), (0.8, 0.38, "Body2"), (1.25, 0.24, "Body3")]):
        b.box((0, y, h + 0.1), (0.025, 0.3, 0.22), fin, bone, top=(1, 0.35))
        b.cyl((0, y - 0.08, h), (0, y + 0.02, h + 0.28 - k * 0.04), 0.02, 0.003, fin, bone, seg=4, smooth=False)
    for k, (y, bone) in enumerate([(0.55, "Body1"), (1.0, "Body2")]):
        b.box((0, y, -0.42 + k * 0.12), (0.02, 0.26, 0.16), fin, bone, top=(1, 1.4))
    # Tail: a torn fan of three rays.
    for s in (-1, 0, 1):
        b.box((0, 2.12, -0.14 + s * 0.17), (0.025, 0.42, 0.24 if s else 0.2), fin, "Tail", rot=(s * 22, 0, 0), top=(1, 1.3))
    # Pectoral fins on the Fin bones and small ventral fins under the head.
    for s in (1, -1):
        bone = "Fin.L" if s > 0 else "Fin.R"
        b.box((s * 0.62, 0.22, -0.22), (0.46, 0.34, 0.025), fin, bone, rot=(0, s * -20, s * 25))
        b.box((s * 0.48, -0.08, -0.42), (0.26, 0.22, 0.025), fin, "Head", rot=(0, s * -40, s * 10))

me = loft_mesh(NAME, RINGS, seg=14)
shape(me, bloat)
body = build_character(NAME, arm, me, bone_segments(arm), rule, parts, "mon")

def swim(t, period=1.6, amp=1.0, jaw=6):
    w = lambda ph: wave(t, period, ph)
    return {
        "Root": ('loc', (0, 0, 0.05 * wave(t, period * 2))),
        "Head": (2 * w(0.1), 0, -5 * amp * w(0)),
        "Jaw": (jaw + 3 * w(0.3), 0, 0),
        "Lure1": (8 * wave(t, period * 1.5), 0, 6 * w(0.2)),
        "Lure2": (10 * wave(t, period * 1.5, 0.3), 0, 8 * w(0.4)),
        "Body1": (0, 0, 8 * amp * w(0.15)),
        "Body2": (0, 0, 14 * amp * w(0.3)),
        "Body3": (0, 0, 20 * amp * w(0.45)),
        "Tail": (0, 0, 28 * amp * w(0.6)),
        "Fin.L": (0, 25 * w(0.0), 0), "Fin.R": (0, -25 * w(0.0), 0),
    }

sampled(arm, "idle", 3.2, lambda t: swim(t, 3.2, 0.4, 8), steps=12)
sampled(arm, "swim", 1.6, lambda t: swim(t, 1.6, 1.0, 6), steps=12)
sampled(arm, "chase", 0.8, lambda t: swim(t, 0.8, 1.4, 16), steps=10)
rest = swim(0, 1.6, 0.6, 6)
open_wide = dict(rest)
open_wide.update({"Head": (-22, 0, 0), "Jaw": (48, 0, 0), "Lure1": (-20, 0, 0), "Body1": (-6, 0, 0)})
snap = dict(rest)
snap.update({"Head": (14, 0, 0), "Jaw": (-2, 0, 0), "Root": ('loc', (0, -0.35, 0)), "Body1": (6, 0, 0), "Tail": (0, 0, 30)})
clip(arm, "bite", 1.6, [(0, rest), (0.55, open_wide), (0.68, open_wide), (0.8, snap), (1.0, snap), (1.6, rest)], loop=False)
# scream: the jaw gapes to its widest, the head thrashes side to side and the whole body shudders.
def gape(k):
    p = dict(rest)
    p.update({"Head": (-26, 0, 9 * k), "Jaw": (58, 0, 0), "Lure1": (-28, 0, -10 * k), "Lure2": (-18, 0, 12 * k),
              "Body1": (-4, 0, -6 * k), "Body2": (0, 0, 10 * k), "Body3": (0, 0, -14 * k), "Tail": (0, 0, 22 * k),
              "Fin.L": (0, 35, 0), "Fin.R": (0, -35, 0)})
    return p
clip(arm, "scream", 1.3, [(0, rest), (0.2, gape(0)), (0.35, gape(1)), (0.5, gape(-1)), (0.65, gape(1)), (0.8, gape(-1)), (0.95, gape(0.5)),
                          (1.3, swim(0, 0.8, 1.4, 16))], loop=False)
result = {"tris": tris(body), "clips": [t.name for t in arm.animation_data.nla_tracks]}
