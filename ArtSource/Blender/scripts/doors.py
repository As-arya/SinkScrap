# Interior room doors (Docs/BlenderAssets.md, "Doors"): one 1.04 x 2.13 m leaf per 1.1 m doorway, four styles mixed per
# door by WorldFactory.DoorStyleOf, plus the matching 1.1 x 2.25 m frame. References: a grey watertight door with a round
# porthole, rounded corners and a dog linkage on its back, and a varnished plank cabin door with a chrome porthole.
# Leaf frame: hinge axis on x = 0, the leaf runs along Blender -X (Unity +X) to x = -W, bottom at z = 0.07 (clears the
# sill), thickness centred on y = 0; Blender -Y (Unity +Z) is the handle face, +Y the back. Run with interior_kit.blend open.
exec(open(r"D:/UNITYProjecy/SinkScrapt/SinkScrapt/ArtSource/Blender/scripts/kitlib.py").read())

W, Z0, H, T = 1.04, 0.07, 2.2, 0.07
CX = -W / 2
FRONT, BACK = -T / 2, T / 2
OUT = r"D:/UNITYProjecy/SinkScrapt/SinkScrapt/Assets/SinkScrapt/Resources/SinkScrapt/Models/Kit/"
CHROME, STEEL, FRAME, BRASS, DARK = "Plate__Chrome", "Plate__Steel", "Plate__DoorFrame", "Plate__Brass", "Col__151515"

def porthole(k, cz, r_glass, r_ring, proud=0.025, bolts=8, ring_mat=CHROME, glass="Glass__Porthole"):
    for s in (-1, 1):
        y = s * T / 2
        k.annulus(CX, cz, r_glass - 0.004, r_ring, y, y + s * proud, ring_mat, seg=20)
        for i in range(bolts):
            a = 2 * math.pi * (i + 0.5) / bolts
            r = (r_glass + r_ring) / 2
            k.box((CX + math.cos(a) * r, y + s * (proud + 0.004), cz + math.sin(a) * r), (0.018, 0.01, 0.018), STEEL)
    # Glass recessed a little, both faces (transparent: the far room shows through).
    k.slab(circle(CX, cz, r_glass + 0.002, 20), [], -0.008, 0.008, glass)

def lever(k, x, z, length=0.22, toward=1, both=True, mat=CHROME, plate=STEEL):
    """Lever handle: a rose plate and a horizontal grip pointing `toward` (+1 = toward the hinge) on each face."""
    for s in ((-1, 1) if both else (-1,)):
        y = s * T / 2
        k.box((x, y + s * 0.008, z), (0.055, 0.016, 0.14), plate)
        k.box((x, y + s * 0.03, z + 0.03), (0.028, 0.04, 0.028), mat)
        k.box((x + toward * length / 2, y + s * 0.05, z + 0.03), (length, 0.024, 0.03), mat)
        k.box((x, y + s * 0.017, z - 0.07), (0.012, 0.006, 0.03), DARK)

def hinge_blocks(k, zs, mat=FRAME, knuckle=STEEL, face=FRONT):
    for z in zs:
        k.box((-0.075, face - 0.009, z), (0.14, 0.018, 0.13), mat)
        k.cyl((0.012, face - 0.018, z - 0.085), (0.012, face - 0.018, z + 0.085), 0.024, 0.024, knuckle, seg=8)

def dog_linkage(k, face=BACK, mat=STEEL, hub_z=1.08, top=2.12):
    """Watertight-door dogs on the back face: six clamps at the edges joined by a linkage ring and a bar to the handle hub."""
    s = 1 if face > 0 else -1
    y = face + s * 0.012
    dogs = [(CX, top), (-0.05, 1.62), (-0.05, 0.55), (CX, Z0 + 0.08), (-W + 0.05, 0.55), (-W + 0.05, 1.62)]
    for i in range(len(dogs)):
        k.bar(dogs[i], dogs[(i + 1) % len(dogs)], 0.034, 0.016, y, mat)
    for x, z in dogs:
        k.box((x, face + s * 0.022, z), (0.075, 0.03, 0.075), FRAME)
        k.cyl((x, face, z), (x, face + s * 0.045, z), 0.018, 0.018, STEEL, seg=6)
    hub = (-W + 0.16, hub_z)
    k.bar(hub, (-0.05, hub_z), 0.04, 0.016, y, mat)
    k.cyl((hub[0], face, hub[1]), (hub[0], face + s * 0.05, hub[1]), 0.035, 0.035, FRAME, seg=10)

def steel_door():
    """Grey watertight door: rounded corners, round porthole, lever handle and two hinges in front, dog linkage behind."""
    k = Kit("kit_door_steel")
    pz = 1.62
    k.slab(rrect(-W, 0, Z0, H, 0.11), [circle(CX, pz, 0.165, 20)], FRONT, BACK, "Plate__DoorGrey")
    # A raised stiffening rim around the face, as on pressed steel doors.
    for s in (-1, 1):
        y = s * T / 2
        rim = rrect(-W + 0.035, -0.035, Z0 + 0.035, H - 0.035, 0.085)
        inner = rrect(-W + 0.065, -0.065, Z0 + 0.065, H - 0.065, 0.06)
        k.slab(rim, [inner], y, y + s * 0.008, "Plate__DoorGrey")
    porthole(k, pz, 0.165, 0.225)
    lever(k, -W + 0.13, 1.05, 0.3)
    hinge_blocks(k, (0.42, 1.88))
    dog_linkage(k)
    return k.build("Doors")

def rusted_door():
    """Heavy rusted watertight door without a window: stiffener ribs, a small handwheel, dogs on both faces."""
    k = Kit("kit_door_rusted")
    k.slab(rrect(-W, 0, Z0, H, 0.16), [], FRONT, BACK, "RustPlate__DoorRust")
    for z in (0.62, 1.55):
        k.box((CX, FRONT - 0.02, z), (W - 0.14, 0.04, 0.06), "RustPlate__DoorRust")
    k.box((CX, FRONT - 0.02, 1.1), (0.06, 0.04, 1.7), "RustPlate__DoorRust")
    # Handwheel on the front: rim, hub and four spokes.
    wz = 1.1
    k.annulus(CX, wz, 0.13, 0.155, FRONT - 0.07, FRONT - 0.095, "Plate__RedPaint", seg=16)
    k.cyl((CX, FRONT - 0.04, wz), (CX, FRONT - 0.1, wz), 0.035, 0.035, STEEL, seg=8)
    for a in range(4):
        ang = math.pi / 4 + a * math.pi / 2
        k.bar((CX, wz), (CX + math.cos(ang) * 0.14, wz + math.sin(ang) * 0.14), 0.02, 0.016, FRONT - 0.082, "Plate__RedPaint")
    k.box((CX, FRONT - 0.004, 1.86), (0.34, 0.008, 0.12), "DeckPaint__Cream")
    for x, z in ((-W + 0.07, 0.4), (-W + 0.07, 1.8), (-0.07, 0.4), (-0.07, 1.8)):
        k.box((x, FRONT - 0.03, z), (0.05, 0.05, 0.14), FRAME, rot=(0, 25 if x > CX else -25, 0))
    hinge_blocks(k, (0.4, 1.0, 1.85), mat="RustPlate__DoorRust")
    dog_linkage(k, mat="RustPlate__DoorRust", hub_z=1.1, top=2.1)
    return k.build("Doors")

def wood_door():
    """Varnished plank cabin door: vertical boards, top and bottom rails, chrome porthole, brass plates, lever handle."""
    k = Kit("kit_door_wood")
    pz = 1.5
    # One body with the porthole hole; vertical grain (uv rotated) at about 0.12 m per board.
    k.slab(rrect(-W, 0, Z0, H, 0.0, corners=(False,) * 4), [circle(CX, pz, 0.175, 20)], FRONT, BACK, "Wood__DoorWood", uv=2.4, uvrot=True)
    boards = 8
    for s in (-1, 1):
        y = s * T / 2
        # Board joints: dark grooves, interrupted by the porthole ring.
        for i in range(1, boards):
            x = -W + W * i / boards
            spans = [(Z0 + 0.2, H - 0.16)]
            if abs(x - CX) < 0.25:
                dz = math.sqrt(max(0.0, 0.25 ** 2 - (x - CX) ** 2))
                spans = [(Z0 + 0.2, pz - dz), (pz + dz, H - 0.16)]
            for z0, z1 in spans:
                if z1 - z0 > 0.02:
                    k.box((x, y + s * 0.001, (z0 + z1) / 2), (0.007, 0.004, z1 - z0), "Col__24170E")
        # Top rail and the kick rail (horizontal grain), slightly proud.
        k.box((CX, y + s * 0.006, H - 0.08), (W - 0.02, 0.012, 0.15), "Wood__DoorWood", uv=2.4)
        k.box((CX, y + s * 0.006, Z0 + 0.1), (W - 0.02, 0.012, 0.2), "Wood__DoorWood", uv=2.4)
    porthole(k, pz, 0.175, 0.25, proud=0.03, bolts=10)
    k.box((CX, FRONT - 0.005, 1.17), (0.2, 0.008, 0.055), BRASS)
    k.box((CX, FRONT - 0.014, H - 0.08), (0.22, 0.008, 0.06), BRASS)
    lever(k, -W + 0.12, 1.0, 0.13, mat=CHROME, plate=CHROME)
    for z in (0.3, 1.1, 1.9):
        k.box((0.004, 0.0, z), (0.012, 0.075, 0.11), BRASS)
    return k.build("Doors")

def cabin_door():
    """Painted steel cabin door: a small square wired-glass window, a louvred vent and a kick plate."""
    k = Kit("kit_door_cabin")
    wz = 1.55
    window = rrect(CX - 0.15, CX + 0.15, wz - 0.15, wz + 0.15, 0.025)
    k.slab(rrect(-W, 0, Z0, H, 0.04), [window], FRONT, BACK, "DeckPaint__DoorCream")
    for s in (-1, 1):
        y = s * T / 2
        k.slab(rrect(CX - 0.2, CX + 0.2, wz - 0.2, wz + 0.2, 0.04), [rrect(CX - 0.152, CX + 0.152, wz - 0.152, wz + 0.152, 0.02)], y, y + s * 0.018, FRAME)
        # Louvred vent: a dark recess with angled slats.
        k.box((CX, y + s * 0.002, 0.42), (0.46, 0.004, 0.3), "Col__0B0C0C")
        for i in range(6):
            k.box((CX, y + s * 0.012, 0.3 + i * 0.048), (0.46, 0.012, 0.03), FRAME, rot=(s * 35, 0, 0))
        k.slab(rrect(CX - 0.26, CX + 0.26, 0.25, 0.59, 0.01), [rrect(CX - 0.23, CX + 0.23, 0.28, 0.56, 0.005)], y, y + s * 0.014, FRAME)
        k.box((CX, y + s * 0.004, Z0 + 0.12), (W - 0.12, 0.008, 0.2), STEEL)
    k.slab(window, [], -0.006, 0.006, "Glass__Wired")
    for i in range(1, 4):
        k.box((CX - 0.15 + 0.3 * i / 4, 0, wz), (0.004, 0.014, 0.3), STEEL)
        k.box((CX, 0, wz - 0.15 + 0.3 * i / 4), (0.3, 0.014, 0.004), STEEL)
    k.box((CX, FRONT - 0.004, 1.93), (0.16, 0.008, 0.07), "DeckPaint__Cream")
    lever(k, -W + 0.12, 1.02, 0.15)
    hinge_blocks(k, (0.4, 1.85), mat=FRAME)
    return k.build("Doors")

def door_frame():
    """Frame for the 1.1 x 2.25 m doorway on a 0.1 m wall (scaled x2 in depth where two skins meet): a lining 6 mm inside
    the opening, rounded trim flanges standing proud of both wall faces, and a hazard-striped sill."""
    k = Kit("kit_door_frame")
    ow, oh, depth = 0.55, 2.25, 0.05
    inset = 0.006
    for s in (-1, 1):
        k.box((s * (ow + 0.012 - inset), 0, oh / 2), (0.024, 2 * depth, oh), FRAME)
    k.box((0, 0, oh + 0.012 - inset), (2 * ow + 0.048, 2 * depth, 0.024), FRAME)
    outline = rrect(-0.67, 0.67, 0.0, 2.36, 0.12, corners=(False, False, True, True))
    inner = rrect(-ow + inset, ow - inset, -0.2, oh - inset, 0.08, corners=(False, False, True, True))
    # The flange is a U: cut the inner rounded opening out of the outer plate, opening downward past the floor line.
    u = outline[:1] + [(-ow + inset, 0.0)] + list(reversed(inner[2:])) + [(ow - inset, 0.0)] + outline[1:]
    for s in (-1, 1):
        k.slab(u, [], s * (depth + 0.002), s * (depth + 0.03), FRAME)
    k.box((0, 0, 0.025), (2 * ow - 2 * inset, 2 * depth + 0.02, 0.05), "Hazard")
    return k.build("Doors")

built = {}
for fn in (steel_door, rusted_door, wood_door, cabin_door, door_frame):
    ob = fn()
    built[ob.name] = tris(ob)
result = built
