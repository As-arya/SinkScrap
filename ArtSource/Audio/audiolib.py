# SinkScrapt audio toolkit, run inside Blender's Python (numpy + Blender's `aud` module decode OGG/MP3/WAV).
# Turns the downloaded CC0 recordings (Docs/Assets.md) into short game clips: slicing by onset, pitch by resampling,
# FFT filters, synthetic room/hull reverb, soft clipping, loop cross-fades. Output is 16-bit PCM WAV that Unity imports
# from Assets/SinkScrapt/Resources/SinkScrapt/Audio (Editor/AudioImportRules.cs sets the import options).
import aud, wave, math
import numpy as np

SR = 44100

def load(path, mono=True):
    """Decoded samples as float32 (n,) mono or (n, 2) stereo at SR."""
    s = aud.Sound(path)
    rate, ch = s.specs[0], s.specs[1]
    a = s.data().astype(np.float32)
    if a.ndim == 1:
        a = a[:, None]
    if mono:
        a = a.mean(axis=1)
    elif a.shape[1] == 1:
        a = np.repeat(a, 2, axis=1)
    if rate != SR:
        a = resample(a, rate / SR)
    return a

def resample(x, factor):
    """Plays x `factor` times faster (factor < 1 lowers pitch and lengthens). Linear interpolation."""
    n = len(x)
    m = max(1, int(round(n / factor)))
    src = np.arange(m) * factor
    if x.ndim == 1:
        return np.interp(src, np.arange(n), x).astype(np.float32)
    return np.stack([np.interp(src, np.arange(n), x[:, c]) for c in range(x.shape[1])], axis=1).astype(np.float32)

def secs(t):
    return int(round(t * SR))

def env_rms(x, win=0.01):
    """RMS envelope, one value per `win` seconds."""
    if x.ndim > 1:
        x = x.mean(axis=1)
    w = max(1, secs(win))
    n = len(x) // w
    return np.sqrt((x[:n * w].reshape(n, w) ** 2).mean(axis=1) + 1e-12)

def onsets(x, thresh=0.25, gap=0.25, win=0.01, rel=True):
    """Start times (s) where the envelope rises through `thresh` (relative to the clip's peak envelope when rel)."""
    e = env_rms(x, win)
    t = thresh * e.max() if rel else thresh
    out, last = [], -1e9
    for i in range(1, len(e)):
        if e[i] >= t and e[i - 1] < t and (i * win - last) >= gap:
            # Walk back to where the rise starts.
            j = i
            while j > 0 and e[j - 1] < e[j] and e[j - 1] > t * 0.15:
                j -= 1
            out.append(j * win)
            last = i * win
    return out

def cut(x, start, dur, fade_in=0.003, fade_out=0.05):
    a = x[secs(start):secs(start + dur)].copy()
    return fade(a, fade_in, fade_out)

def fade(a, fin=0.003, fout=0.05):
    n = len(a)
    fi, fo = min(n // 2, secs(fin)), min(n // 2, secs(fout))
    shape = (n,) + (1,) * (a.ndim - 1)
    g = np.ones(n, np.float32)
    if fi > 0:
        g[:fi] = np.linspace(0, 1, fi)
    if fo > 0:
        g[n - fo:] *= np.linspace(1, 0, fo) ** 2
    return a * g.reshape(shape)

def trim_tail(a, floor=0.02, win=0.01, pad=0.03):
    """Drops the quiet end of a slice (envelope under floor x peak)."""
    e = env_rms(a, win)
    if len(e) == 0:
        return a
    keep = np.where(e > floor * e.max())[0]
    if len(keep) == 0:
        return a
    end = min(len(a), secs((keep[-1] + 1) * win + pad))
    return fade(a[:end], 0.001, min(0.06, end / SR * 0.3))

def spectral(x, fn):
    """Applies a gain curve fn(freq_hz_array) in the frequency domain (zero phase)."""
    if x.ndim > 1:
        return np.stack([spectral(x[:, c], fn) for c in range(x.shape[1])], axis=1)
    n = len(x)
    N = 1 << int(math.ceil(math.log2(max(2, n))))
    X = np.fft.rfft(x, N)
    f = np.fft.rfftfreq(N, 1 / SR)
    return np.fft.irfft(X * fn(f), N)[:n].astype(np.float32)

def lowpass(x, fc, order=2):
    return spectral(x, lambda f: 1 / np.sqrt(1 + (f / fc) ** (2 * order)))

def highpass(x, fc, order=2):
    return spectral(x, lambda f: 1 / np.sqrt(1 + (fc / np.maximum(f, 1e-3)) ** (2 * order)))

def bandpass(x, lo, hi, order=2):
    return highpass(lowpass(x, hi, order), lo, order)

def peak_eq(x, fc, gain_db, width=0.5):
    """Bell boost/cut around fc (width in octaves)."""
    g = 10 ** (gain_db / 20)
    return spectral(x, lambda f: 1 + (g - 1) * np.exp(-(np.log2(np.maximum(f, 1) / fc) / width) ** 2))

def convolve(x, ir):
    if x.ndim > 1:
        return np.stack([convolve(x[:, c], ir) for c in range(x.shape[1])], axis=1)
    n = len(x) + len(ir) - 1
    N = 1 << int(math.ceil(math.log2(n)))
    return np.fft.irfft(np.fft.rfft(x, N) * np.fft.rfft(ir, N), N)[:n].astype(np.float32)

def reverb_ir(seconds=1.6, bright=3000, metal=0.0, seed=1, predelay=0.012):
    """Exponentially decaying noise impulse response; `metal` adds resonant ringing modes (a steel hull)."""
    r = np.random.default_rng(seed)
    n = secs(seconds)
    t = np.arange(n) / SR
    ir = r.standard_normal(n).astype(np.float32) * np.exp(-6.9 * t / seconds)
    ir = lowpass(ir, bright, 1)
    if metal > 0:
        for f in (97, 151, 233, 389, 607, 911):
            ir += metal * 0.25 * np.sin(2 * np.pi * f * t + r.uniform(0, 6)) * np.exp(-6.9 * t / (seconds * 0.8)) * r.uniform(0.4, 1)
    ir = np.concatenate([np.zeros(secs(predelay), np.float32), ir])
    ir[0] = 0
    return ir / np.sqrt((ir ** 2).sum())

def reverb(x, wet=0.3, **kw):
    ir = reverb_ir(**kw)
    y = convolve(x, ir)
    dry = np.zeros_like(y)
    dry[:len(x)] = x
    return dry * (1 - wet * 0.5) + y * wet

def drive(x, amount):
    return (np.tanh(x * amount) / np.tanh(amount)).astype(np.float32)

def mix(*layers):
    """Sums (signal, gain, offset_seconds) layers, or bare signals."""
    items = []
    for l in layers:
        if isinstance(l, tuple):
            items.append((l[0], l[1] if len(l) > 1 else 1.0, l[2] if len(l) > 2 else 0.0))
        else:
            items.append((l, 1.0, 0.0))
    n = max(secs(o) + len(a) for a, g, o in items)
    stereo = any(a.ndim > 1 for a, _, _ in items)
    out = np.zeros((n, 2) if stereo else n, np.float32)
    for a, g, o in items:
        if stereo and a.ndim == 1:
            a = np.repeat(a[:, None], 2, axis=1)
        s = secs(o)
        out[s:s + len(a)] += a * g
    return out

def norm(x, peak=0.9):
    m = np.abs(x).max()
    return x * (peak / m) if m > 1e-9 else x

def loop(x, xfade=1.0):
    """Cross-fades the tail into the head so the clip repeats seamlessly (equal-power)."""
    f = min(secs(xfade), len(x) // 3)
    n = len(x) - f
    head = x[:f].copy()
    tail = x[n:].copy()
    w = np.linspace(0, 1, f)
    shape = (f,) + (1,) * (x.ndim - 1)
    a, b = np.sqrt(w).reshape(shape), np.sqrt(1 - w).reshape(shape)
    y = x[:n].copy()
    y[:f] = head * a + tail * b
    return y

def level(x, rms=0.08, peak=0.95):
    """Scales to a target RMS (so variants of one sound play equally loud), limited so the peak stays under `peak`."""
    r = float(np.sqrt((x ** 2).mean()))
    g = rms / max(r, 1e-6)
    m = float(np.abs(x).max()) * g
    return x * (g * min(1.0, peak / max(m, 1e-6)))

def write(path, x, peak=None):
    """.ogg (Vorbis through aud: 96 kbit/s mono, 128 kbit/s stereo) or .wav (16-bit PCM)."""
    if peak is not None:
        x = norm(x, peak)
    x = np.clip(x, -1, 1).astype(np.float32)
    ch = 1 if x.ndim == 1 else x.shape[1]
    if path.endswith(".ogg"):
        data = x[:, None] if x.ndim == 1 else x
        aud.Sound.buffer(np.ascontiguousarray(data), SR).write(path, rate=SR, channels=aud.CHANNELS_MONO if ch == 1 else aud.CHANNELS_STEREO,
                                                               format=aud.FORMAT_FLOAT32, container=aud.CONTAINER_OGG, codec=aud.CODEC_VORBIS,
                                                               bitrate=96000 if ch == 1 else 128000)
        return path
    pcm = (x * 32767).astype('<i2')
    with wave.open(path, 'wb') as w:
        w.setnchannels(ch)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    return path
