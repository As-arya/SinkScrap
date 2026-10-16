# Builds SinkScrapt's recorded sound set from the CC0 downloads (sources and licences: Docs/Assets.md).
# Run in Blender:  SRC = r"<folder with bsb_*.ogg, kenney/, creature/>"; exec(open(r".../ArtSource/Audio/build_audio.py").read())
# Writes <Snd name>_<variant>.wav into Assets/SinkScrapt/Resources/SinkScrapt/Audio; SoundBank prefers these clips over its
# synthesised recipes (one clip per variant). The downloads stay outside the project; only these processed clips ship.
import os
exec(open(r"D:/UNITYProjecy/SinkScrapt/SinkScrapt/ArtSource/Audio/audiolib.py").read())
OUT = r"D:/UNITYProjecy/SinkScrapt/SinkScrapt/Assets/SinkScrapt/Resources/SinkScrapt/Audio"
os.makedirs(OUT, exist_ok=True)
for f in os.listdir(OUT):
    if f.endswith((".wav", ".wav.meta", ".ogg", ".ogg.meta")):
        os.remove(os.path.join(OUT, f))

_cache = {}
def S(name, mono=True):
    key = (name, mono)
    if key not in _cache:
        # "k:<file>" Kenney Impact Sounds, "c:<file>" 80 CC0 creature SFX, "<id>" BigSoundBank recording.
        if name.startswith("k:"):
            path = os.path.join(SRC, "kenney", "Audio", name[2:] + ".ogg")
        elif name.startswith("c:"):
            path = os.path.join(SRC, "creature", name[2:] + ".ogg")
        else:
            path = os.path.join(SRC, "bsb_" + name + ".ogg")
        _cache[key] = load(path, mono)
    return _cache[key]

made = {}
def out(snd, clips, peak=0.85, rms=None):
    """Writes <snd>_<i>.ogg. With `rms`, every variant is levelled to the same loudness instead of the same peak."""
    for i, c in enumerate(clips):
        write(os.path.join(OUT, "%s_%d.ogg" % (snd, i)), level(c, rms) if rms else c, None if rms else peak)
    made[snd] = len(clips)

def slices(src, starts, dur, pre=0.015, tail=True, fout=0.06):
    res = []
    for t in starts:
        a = cut(src, max(0, t - pre), dur, 0.002, fout)
        res.append(trim_tail(a) if tail else a)
    return res

hull = dict(seconds=1.6, bright=2600, metal=0.6, seed=3)
room = dict(seconds=1.1, bright=3500, metal=0.25, seed=5)

# ---- Footsteps ------------------------------------------------------------------------------------------------------------
pontoon = highpass(S("1845"), 70)
STEP = 0.075
out("StepWood", [peak_eq(s, 180, 3, 0.8) for s in slices(pontoon, [0.34, 0.99, 1.6, 2.23, 2.81, 3.38], 0.36)], rms=STEP)

def metal_step(i, plate="impactPlate_light", ring=0.4, pitch=1.0):
    shoe = S("k:footstep_concrete_%03d" % (i % 5))
    hit = lowpass(S("k:%s_%03d" % (plate, (i + 2) % 5)), 4200)
    a = mix((shoe, 1.0), (hit, ring, 0.004))
    a = resample(a, pitch)
    return trim_tail(reverb(a, 0.22, seconds=0.6, bright=3000, metal=0.8, seed=i))
out("StepMetal", [metal_step(i, pitch=p) for i, p in enumerate([1.0, 0.94, 1.05, 0.9, 0.98, 1.03])], rms=STEP)
out("StepGrate", [metal_step(i, "impactMetal_light", 0.5, p) for i, p in enumerate([1.0, 0.93, 1.06, 0.97])], rms=STEP)
out("StepGround", slices(highpass(S("0514"), 60), [0.14, 1.11, 1.9, 2.51, 3.64, 4.64], 0.3), rms=STEP)
out("StepGravel", slices(highpass(S("3216"), 60), [0.33, 1.23, 2.11, 3.12, 4.41, 5.94], 0.42), rms=STEP * 0.8)
wade = highpass(S("0690"), 90)
out("StepWet", slices(wade, [5.42, 8.19, 12.91, 18.03, 21.32, 27.97], 0.7, fout=0.2), rms=STEP * 0.8)

# ---- Water -----------------------------------------------------------------------------------------------------------------
out("SplashIn", [cut(S("1519"), 0.22, 1.8, 0.003, 0.6)])
out("SplashOut", [cut(S("1529"), 0.03, 1.3, 0.003, 0.5)])
rowing = lowpass(S("1514"), 3200)
out("Swim", slices(rowing, [17.82, 19.85, 23.02], 1.1, fout=0.3), 0.7)
bubbles = S("0150")
out("BreathOut", slices(lowpass(bubbles, 2500), [5.54, 11.8, 15.63], 1.3, fout=0.4), 0.6)
drops = S("1384")
out("Drip", [reverb(d, 0.35, **room) for d in slices(drops, [0.16, 1.59, 3.02, 4.45, 5.88, 7.31], 0.45)], 0.6)

# ---- Outdoor ambience -------------------------------------------------------------------------------------------------------
out("Sea", [loop(lowpass(cut(S("0698", False), 20, 44, 0.5, 0.5), 9000), 2.5)], 0.75)
out("Lap", [loop(lowpass(cut(S("1514", False), 26, 32, 0.4, 0.4), 2600), 2.0)], 0.75)
gulls = highpass(S("2573"), 550)
out("Gull", slices(gulls, [0.93, 4.27, 8.33, 15.44, 20.17, 26.48, 33.91, 45.99], 1.15, pre=0.04, fout=0.25), 0.8)
out("Wind", [loop(lowpass(cut(S("2443", False), 12, 34, 0.5, 0.5), 7000), 2.5)], 0.7)

# ---- Wreck interior ----------------------------------------------------------------------------------------------------------
cave = lowpass(cut(S("2135"), 5, 34, 0.5, 0.5), 900)
out("RoomTone", [loop(resample(cave, 0.85), 2.5)], 0.6)
door = S("0616")
def creak(src, t, dur, pitch, lp=3000):
    a = resample(cut(src, t, dur, 0.02, 0.25), pitch)
    return trim_tail(reverb(lowpass(a, lp), 0.45, **hull), 0.01)
out("Creak", [creak(door, 0.1, 1.1, 0.68), creak(door, 12.45, 1.4, 0.6), creak(door, 30.5, 1.4, 0.72), creak(door, 40.35, 1.3, 0.62),
              creak(S("0303"), 0.03, 1.3, 0.5), creak(S("0539"), 8.2, 1.8, 0.7), creak(S("0306"), 0.0, 4.4, 0.66), creak(S("1159"), 5.6, 0.9, 0.6)], 0.8)
def groan(src, t, dur, pitch):
    a = resample(cut(src, t, dur, 0.05, 0.4), pitch)
    return trim_tail(reverb(lowpass(a, 900, 3), 0.6, seconds=3.0, bright=1200, metal=0.9, seed=11), 0.01)
out("Groan", [groan(door, 15.05, 2.4, 0.32), groan(S("0539"), 10.7, 2.4, 0.3), groan(S("0306"), 0.0, 4.4, 0.36), groan(door, 46.7, 2.2, 0.28)], 0.85)
def clank(a, pitch):
    return trim_tail(reverb(lowpass(resample(a, pitch), 2600), 0.55, seconds=2.4, bright=2200, metal=0.7, seed=7), 0.01)
out("Thunk", [clank(cut(S("1774"), 0.08, 2.6, 0.002, 0.5), 0.7), clank(cut(S("0235"), 0.0, 1.2, 0.002, 0.3), 0.6),
              clank(S("k:impactMetal_heavy_000"), 0.55), clank(S("k:impactPlate_heavy_002"), 0.5)], 0.8)
out("BoatCreak", [trim_tail(lowpass(resample(cut(S("3051"), 0.2, 1.0, 0.01, 0.2), 0.8), 4000)),
                  lowpass(cut(S("0539"), 3.0, 1.3, 0.02, 0.3), 4500),
                  lowpass(resample(cut(S("1159"), 5.6, 0.9, 0.01, 0.2), 0.85), 4500),
                  lowpass(cut(S("0306"), 4.66, 1.3, 0.02, 0.3), 4500)], 0.7)
out("Rope", [lowpass(resample(cut(S("3051"), 0.22, 0.7, 0.01, 0.2), p), 3500) for p in (0.9, 0.75)], 0.6)

# ---- Doors and hatches -------------------------------------------------------------------------------------------------------
latch = S("k:impactMetal_light_001")
out("DoorOpen", [reverb(mix((latch, 0.6), (resample(cut(door, 0.1, 1.4, 0.01, 0.3), p), 1.0, 0.06)), 0.3, **room) for p in (0.9, 0.8)])
clang = cut(S("2358"), 1.62, 2.2, 0.002, 0.6)
out("DoorClose", [trim_tail(reverb(resample(clang, p), 0.35, **room), 0.01) for p in (0.9, 0.76)])
out("DoorOpenWood", [cut(S("1703"), 0.05, 1.7, 0.005, 0.4)])
out("DoorCloseWood", [cut(S("1704"), 2.5, 1.25, 0.005, 0.4)])
tin = [S("k:impactMetal_light_%03d" % i) for i in (0, 2, 3)]
out("DoorLocked", [reverb(mix((resample(tin[0], 1.3), 0.9), (resample(tin[1], 1.25), 0.7, 0.09), (resample(tin[2], 1.35), 0.8, 0.2), (resample(tin[0], 1.2), 0.5, 0.31)), 0.25, **room),
                   reverb(mix((resample(tin[2], 1.2), 0.9), (resample(tin[0], 1.3), 0.7, 0.11), (resample(tin[1], 1.15), 0.6, 0.24)), 0.25, **room)], 0.7)
out("Hatch", [trim_tail(reverb(mix((resample(cut(door, 0.1, 1.2, 0.01, 0.3), 0.7), 0.8), (resample(clang, 0.65), 1.0, 0.5)), 0.4, **hull), 0.01)])
out("Horn", [reverb(cut(S("0261"), 0.05, 3.8, 0.05, 1.2), 0.3, seconds=3.0, bright=1800, seed=2)], 0.8)

# ---- Hull stalker ------------------------------------------------------------------------------------------------------------
howl = S("1662")
def scream(layers, seed):
    a = mix(*layers)
    a = drive(norm(a, 0.9), 2.3)
    a = bandpass(a, 110, 7500)
    a = peak_eq(a, 900, 4, 1.0)
    return trim_tail(reverb(a, 0.35, seconds=1.5, bright=3200, metal=0.45, seed=seed), 0.01)
out("StalkerScream", [
    scream([(resample(S("c:scream_01"), 0.72), 1.0), (resample(cut(howl, 0.09, 1.7), 0.62), 0.7, 0.05), (resample(S("c:roar_01"), 0.8), 0.6)], 21),
    scream([(resample(S("c:scream_02"), 0.68), 1.0), (resample(cut(howl, 1.85, 0.8), 0.55), 0.6, 0.1), (resample(S("c:roar_02"), 0.75), 0.6)], 22),
    scream([(resample(S("c:scream_02"), 0.8), 0.9), (resample(cut(howl, 0.09, 1.7), 0.7), 0.8), (resample(S("c:roar_03"), 0.7), 0.7, 0.05)], 23)], 0.95)
out("StalkerRoar", [trim_tail(reverb(drive(norm(resample(S("c:roar_%02d" % i), 0.72), 0.9), 1.6), 0.3, **hull), 0.01) for i in (1, 2, 3)], 0.9)
out("StalkerGrowl", [trim_tail(reverb(lowpass(resample(S("c:monster_%02d" % i), 0.7), 4000), 0.25, **hull), 0.01) for i in (1, 3, 4, 6)], 0.85)
breath = resample(S("c:breath"), 0.62)
out("StalkerBreath", [loop(lowpass(mix((breath, 1.0), (resample(S("c:snore"), 0.6), 0.5, 1.2), (breath, 0.8, 2.1), (np.zeros(secs(3.6), np.float32), 0.0)), 2600), 0.3)], 0.7)
def slap(i):
    a = mix((resample(S("k:impactPunch_heavy_%03d" % i), 0.6), 1.0), (resample(S("k:impactSoft_heavy_%03d" % i), 0.7), 0.7), (resample(S("k:impactPlate_light_%03d" % i), 0.5), 0.2, 0.01))
    return trim_tail(reverb(lowpass(a, 2000), 0.25, **hull), 0.01)
out("StalkerStep", [slap(i) for i in range(4)])
out("Bones", slices(S("0596"), [23.62, 3.85], 0.8, pre=0.0, tail=False, fout=0.2), 0.85)

# ---- Drowned hunter -----------------------------------------------------------------------------------------------------------
dark = dict(seconds=2.2, bright=900, metal=0.0, seed=31)
def under(a):
    return reverb(lowpass(a, 1300, 3), 0.5, **dark)
out("DrownedScream", [trim_tail(under(mix((resample(S("c:scream_02"), 0.5), 1.0), (resample(S("c:weird_02"), 0.6), 0.6, 0.1), (cut(bubbles, 5.5, 1.6), 0.5))), 0.01),
                      trim_tail(under(mix((resample(S("c:scream_01"), 0.55), 1.0), (resample(cut(howl, 0.09, 1.7), 0.5), 0.5), (cut(bubbles, 11.8, 1.6), 0.5))), 0.01)], 0.9)
out("DrownedGurgle", [loop(lowpass(mix((resample(S("c:burble_01"), 0.6), 0.9, 0.3), (resample(S("c:burble_02"), 0.55), 0.8, 3.6), (cut(bubbles, 5, 8), 0.6)), 1200), 0.6)], 0.7)
out("DrownedLunge", [trim_tail(lowpass(mix((resample(S("c:weird_01"), 0.6), 1.0), (resample(cut(S("1519"), 0.22, 1.6), 0.8), 0.8)), 3000), 0.01)], 0.9)
out("DrownedPing", [trim_tail(under(resample(S("c:weird_%02d" % i), 0.45)), 0.005) for i in (3, 5)], 0.7)

# ---- Diver -----------------------------------------------------------------------------------------------------------------------
out("Hurt", [trim_tail(lowpass(mix((S("k:impactPunch_medium_%03d" % i), 1.0), (resample(S("k:impactSoft_medium_%03d" % i), 0.8), 0.5)), 5000)) for i in (0, 2, 4)])
out("Death", [trim_tail(lowpass(mix((resample(S("k:impactSoft_heavy_000"), 0.7), 1.0), (cut(S("0596"), 3.85, 0.8), 0.7, 0.05), (resample(S("k:impactPunch_heavy_002"), 0.6), 0.6)), 3500), 0.01)])
out("Land", [trim_tail(mix((resample(S("k:impactSoft_medium_%03d" % i), 0.8), 1.0), (S("k:footstep_concrete_%03d" % i), 0.6))) for i in (0, 2)])
out("Pickup", [trim_tail(S("k:impactGeneric_light_%03d" % i)) for i in (0, 1, 2)], 0.6)
out("Drop", [trim_tail(reverb(resample(S("k:impactMetal_medium_%03d" % i), 0.85), 0.2, **room), 0.01) for i in (0, 2)], 0.75)
out("Deposit", [trim_tail(mix((S("k:impactWood_heavy_000"), 1.0), (resample(S("k:impactMetal_medium_001"), 0.85), 0.5, 0.01)))], 0.75)

# ---- Fear layers ---------------------------------------------------------------------------------------------------------------
gate = resample(S("0303"), 0.45)
swell = reverb(gate, 1.0, seconds=2.5, bright=2500, metal=0.5, seed=41)[::-1].copy()
out("Stinger", [trim_tail(mix((fade(swell[-secs(2.2):], 0.4, 0.02), 0.8), (drive(norm(gate, 0.9), 1.8), 1.0, 2.1)), 0.01)], 0.8)
drone = mix((resample(bandpass(cut(S("2135"), 40, 24, 0.5, 0.5), 60, 700), 0.5), 1.0), (lowpass(resample(cut(door, 20.5, 4.0, 0.3, 0.3), 0.2), 500), 0.5, 3.0))
out("Tension", [loop(reverb(drone, 0.5, seconds=3.5, bright=700, seed=51), 3.0)], 0.7)

result = made
