"""Procedural retro SFX for idle-game-testing. Writes 16-bit mono 44.1kHz WAVs.

Usage (from repo root): python Tools/Audio/synth_sfx.py Assets/Audio/SFX
Music goes to the sibling Music/ folder. Needs numpy + scipy.
Deterministic (seeded per sound) so re-running reproduces the same files.
"""
import sys, os
import numpy as np
from scipy import signal
from scipy.io import wavfile

SR = 44100
OUT = sys.argv[1]


# ---------- building blocks ----------

def t_axis(dur):
    return np.arange(int(SR * dur)) / SR


def osc(freq, dur, kind="sine", duty=0.5):
    """freq: scalar or array (per-sample Hz). Phase-accumulated so sweeps are smooth."""
    n = int(SR * dur)
    f = np.broadcast_to(np.asarray(freq, dtype=float), (n,)) if np.ndim(freq) == 0 else np.asarray(freq)[:n]
    phase = np.cumsum(f) / SR
    p = phase % 1.0
    if kind == "sine":
        return np.sin(2 * np.pi * phase)
    if kind == "square":
        return np.where(p < duty, 1.0, -1.0)
    if kind == "saw":
        return 2 * p - 1
    if kind == "tri":
        return 2 * np.abs(2 * p - 1) - 1
    raise ValueError(kind)


def sweep(f0, f1, dur, curve="exp"):
    x = np.linspace(0, 1, int(SR * dur))
    if curve == "exp":
        return f0 * (f1 / f0) ** x
    return f0 + (f1 - f0) * x


def noise(dur, rng):
    return rng.uniform(-1, 1, int(SR * dur))


def filt(x, kind, cutoff, order=2):
    sos = signal.butter(order, cutoff, btype=kind, fs=SR, output="sos")
    return signal.sosfilt(sos, x)


def env_exp(dur, decay, attack=0.002):
    t = t_axis(dur)
    e = np.exp(-t / decay)
    a = int(SR * attack)
    if a > 0:
        e[:a] *= np.linspace(0, 1, a)
    return e


def env_adsr(dur, a, d, s, r):
    n = int(SR * dur)
    na, nd, nr = int(SR * a), int(SR * d), int(SR * r)
    ns = max(0, n - na - nd - nr)
    e = np.concatenate([np.linspace(0, 1, na, endpoint=False),
                        np.linspace(1, s, nd, endpoint=False),
                        np.full(ns, s),
                        np.linspace(s, 0, nr)])
    return np.pad(e, (0, max(0, n - len(e))))[:n]


def pad_to(x, n):
    return np.pad(x, (0, max(0, n - len(x))))[:n]


def mix(*parts):
    n = max(len(p) for p in parts)
    return sum(pad_to(p, n) for p in parts)


def place(buf, x, at):
    i = int(SR * at)
    end = min(len(buf), i + len(x))
    buf[i:end] += x[:end - i]
    return buf


def bell(freq, dur, decay, kind="sine", harmonics=((1, 1.0), (2, 0.35), (3, 0.12))):
    return sum(osc(freq * m, dur, kind) * g for m, g in harmonics) * env_exp(dur, decay)


def crunch(dur, rng, lo, hi, grain_rate=90, decay=0.06):
    """Crumbly debris: band-passed noise gated by random grains."""
    n = int(SR * dur)
    nz = filt(noise(dur, rng), "bandpass", [lo, hi])
    gate = np.zeros(n)
    t = 0.0
    while t < dur:
        g_len = rng.uniform(0.004, 0.02)
        seg = env_exp(g_len, g_len / 3) * rng.uniform(0.3, 1.0)
        place(gate, seg, t)
        t += rng.exponential(1 / grain_rate)
    return nz * gate * env_exp(dur, decay, attack=0.001)


def finish(x, peak=0.85, fade_ms=4):
    if fade_ms > 0:
        x = filt(x, "highpass", 25)  # DC/subsonic removal without shifting the first sample
    m = np.max(np.abs(x))
    if m > 0:
        x = x / m * peak
    f = int(SR * fade_ms / 1000)
    if f > 0 and len(x) > 2 * f:
        fi = int(SR * 0.001)
        x[:fi] *= np.linspace(0, 1, fi)
        x[-f:] *= np.linspace(1, 0, f)
    return x


def soft(x, cutoff=5000):
    """Tames raw square/saw harshness - reads as 'retro' rather than 'painful'."""
    return filt(x, "lowpass", cutoff, order=2)


def loop_noise(dur, rng, band):
    """Seamlessly looping band-limited noise (FFT-domain filter = circular, no seam)."""
    n = int(SR * dur)
    spec = np.fft.rfft(rng.standard_normal(n))
    freqs = np.fft.rfftfreq(n, 1 / SR)
    lo, hi = band
    shape = np.exp(-0.5 * ((np.log(np.maximum(freqs, 1)) - np.log(np.sqrt(lo * hi))) / (np.log(hi / lo) / 2)) ** 2)
    return np.fft.irfft(spec * shape, n)


def write(name, x, peak=0.85, fade_ms=4):
    x = finish(x, peak, fade_ms)
    path = os.path.join(OUT, name + ".wav")
    wavfile.write(path, SR, (x * 32767).astype(np.int16))
    print(f"{name}.wav  {len(x) / SR:.2f}s")


# ---------- sounds ----------

def mining_hit(i):
    rng = np.random.default_rng(100 + i)
    d = 0.14
    click = filt(noise(0.03, rng), "bandpass", [1800 + 300 * i, 5000]) * env_exp(0.03, 0.006, 0)
    thud = osc(sweep(170 - 15 * i, 70, d), d) * env_exp(d, 0.035)
    grit = crunch(d, rng, 600, 2500, grain_rate=140, decay=0.04) * 0.5
    return mix(click * 0.9, thud, grit)


def mine_dirt(i):
    rng = np.random.default_rng(200 + i)
    d = 0.28
    body = crunch(d, rng, 150, 1400 + 200 * i, grain_rate=110, decay=0.08)
    thump = osc(sweep(110, 55, d), d) * env_exp(d, 0.05) * 0.6
    return mix(body, thump)


def mine_ore(i):
    rng = np.random.default_rng(300 + i)
    d = 0.5
    base = mine_dirt(i) * 0.6
    notes = [1318.5, 1480.0, 1568.0][i]  # E6 / F#6 / G6
    ding = bell(notes, d, 0.12, harmonics=((1, 1.0), (2.76, 0.25), (5.4, 0.08)))
    return mix(base, place(np.zeros(int(SR * d)), ding * 0.7, 0.02))


def artifact_found():
    d = 1.1
    buf = np.zeros(int(SR * d))
    for k, f in enumerate([523.25, 659.25, 783.99, 1046.5, 1318.5]):
        place(buf, bell(f, 0.6, 0.18, kind="tri") * (0.8 if k < 4 else 1.0), k * 0.08)
    rng = np.random.default_rng(4)
    for _ in range(14):  # sparkles
        place(buf, bell(rng.uniform(2500, 4500), 0.08, 0.02) * 0.25, rng.uniform(0.25, 0.9))
    return buf


def powerup():
    d = 0.55
    t = t_axis(d)
    f = sweep(300, 1400, d) * (1 + 0.04 * np.sin(2 * np.pi * 24 * t))
    return soft(osc(f, d, "square", 0.25), 4500) * env_adsr(d, 0.005, 0.05, 0.7, 0.25) * 0.6


def player_hurt():
    rng = np.random.default_rng(5)
    d = 0.3
    tone = osc(sweep(520, 140, d), d, "square", 0.4) * env_exp(d, 0.09)
    grit = filt(noise(d, rng), "lowpass", 2500) * env_exp(d, 0.05) * 0.5
    return soft(mix(tone * 0.7, grit), 4000)


def shield_block():
    rng = np.random.default_rng(6)
    d = 0.7
    clang = sum(osc(f, d) * g * env_exp(d, dec) for f, g, dec in
                [(620, 1.0, 0.25), (1487, 0.6, 0.15), (2210, 0.45, 0.1), (3190, 0.3, 0.07)])
    hit = filt(noise(0.05, rng), "bandpass", [2000, 6000]) * env_exp(0.05, 0.01, 0) * 0.6
    zap = soft(osc(sweep(400, 1800, 0.18), 0.18, "square", 0.3), 4000) * env_exp(0.18, 0.06) * 0.25
    return mix(clang, hit * 0.8, zap)


def explosion_core(rng, d, low=45):
    nz = noise(d, rng)
    # Low-pass sweeping down over time: filter in chunks with decreasing cutoff.
    out = np.zeros_like(nz)
    chunks = 40
    size = len(nz) // chunks + 1
    zi = None
    for c in range(chunks):
        cutoff = 3000 * (0.05 ** (c / chunks)) + 120
        sos = signal.butter(2, cutoff, "lowpass", fs=SR, output="sos")
        if zi is None:
            zi = signal.sosfilt_zi(sos) * 0
        seg = nz[c * size:(c + 1) * size]
        out[c * size:c * size + len(seg)], zi = signal.sosfilt(sos, seg, zi=zi)
    body = out * env_exp(d, d / 4, attack=0.003)
    thump = osc(sweep(90, low, d), d) * env_exp(d, 0.22) * 1.6
    return mix(body, thump)


def explosion():
    rng = np.random.default_rng(7)
    d = 1.4
    boom = explosion_core(rng, d)
    debris = crunch(d, rng, 300, 3000, grain_rate=60, decay=0.5) * 0.35
    return mix(boom, place(np.zeros(int(SR * d)), debris, 0.1))


def player_death():
    rng = np.random.default_rng(8)
    d = 1.3
    boom = explosion_core(rng, d, low=35) * 0.8
    fall = soft(osc(sweep(700, 60, 0.9), 0.9, "square", 0.35), 3500) * env_adsr(0.9, 0.01, 0.1, 0.6, 0.5) * 0.35
    return mix(fall, boom)


def player_revive():
    d = 1.0
    buf = np.zeros(int(SR * d))
    for k, f in enumerate([392.0, 523.25, 659.25, 783.99]):
        tone = soft(osc(f, 0.45, "square", 0.25), 4500) * env_adsr(0.45, 0.005, 0.08, 0.5, 0.3) * 0.4
        place(buf, tone, k * 0.09)
    t = t_axis(d)
    shimmer = osc(sweep(1500, 3200, d), d) * (0.5 + 0.5 * np.sin(2 * np.pi * 18 * t)) * env_adsr(d, 0.3, 0.2, 0.4, 0.5) * 0.15
    return mix(buf, shimmer)


def jetpack_loop():
    rng = np.random.default_rng(9)
    d = 1.0  # integer number of flutter cycles below keeps the loop seamless
    t = t_axis(d)
    roar = loop_noise(d, rng, (180, 900))
    hiss = loop_noise(d, rng, (2500, 7000)) * 0.25
    flutter = 1 + 0.18 * np.sin(2 * np.pi * 14 * t) + 0.08 * np.sin(2 * np.pi * 31 * t)
    roar /= np.max(np.abs(roar))
    hiss /= np.max(np.abs(hiss))
    return (roar + hiss * 0.3) * flutter


def warning():
    d = 0.5
    buf = np.zeros(int(SR * d))
    for k, f in enumerate([880, 659.25]):
        place(buf, soft(osc(f, 0.11, "square", 0.5), 3500) * env_adsr(0.11, 0.003, 0.02, 0.8, 0.03) * 0.5, k * 0.14)
    return buf


def explosive_fuse():
    rng = np.random.default_rng(10)
    d = 1.0
    t = t_axis(d)
    hiss = filt(noise(d, rng), "bandpass", [2500, 7000])
    crackle = np.zeros(len(t))
    for _ in range(60):
        place(crackle, filt(noise(0.006, rng), "highpass", 1500) * env_exp(0.006, 0.0015, 0) * rng.uniform(0.5, 1.5), rng.uniform(0, d))
    rising = np.linspace(0.4, 1.0, len(t))  # builds toward the detonation
    beep = np.zeros(len(t))
    for k in range(8):  # quickening ticks, echoing the flash telegraph
        at = 1.0 - 0.9 * (0.78 ** k)
        place(beep, soft(osc(1200, 0.03, "square", 0.5), 4000) * env_exp(0.03, 0.01) * 0.3, at)
    return (hiss * 0.35 + crackle * 0.6) * rising + beep


def rock_rumble():
    rng = np.random.default_rng(11)
    d = 0.6
    t = t_axis(d)
    rumble = filt(noise(d, rng), "lowpass", 220, order=4)
    rumble /= np.max(np.abs(rumble))
    tremor = 0.6 + 0.4 * np.sin(2 * np.pi * 22 * t)
    grit = crunch(d, rng, 400, 2000, grain_rate=50, decay=0.4) * 0.4
    return (rumble * tremor + grit) * env_adsr(d, 0.05, 0.1, 0.9, 0.2)


def rock_land():
    rng = np.random.default_rng(12)
    d = 0.7
    thud = osc(sweep(95, 38, d), d) * env_exp(d, 0.12, attack=0.001) * 1.2
    impact = filt(noise(0.08, rng), "lowpass", 1800) * env_exp(0.08, 0.02, 0)
    debris = crunch(d, rng, 250, 2500, grain_rate=80, decay=0.2) * 0.5
    return mix(thud, impact, place(np.zeros(int(SR * d)), debris, 0.03))


def gas_release():
    rng = np.random.default_rng(13)
    d = 2.2
    t = t_axis(d)
    nz = noise(d, rng)
    # Band sweeps upward while building (telegraph), then a broad whoosh as the cloud expands.
    hiss = filt(nz, "bandpass", [1200, 6000])
    body = filt(nz, "bandpass", [300, 1500])
    build = np.clip(t / 1.0, 0, 1) ** 2
    whoosh = np.where(t > 1.0, np.exp(-(t - 1.0) / 0.5), 0) * 1.0
    wobble = 1 + 0.25 * np.sin(2 * np.pi * 7 * t)
    return (hiss * (0.4 * build + 0.5 * whoosh) + body * whoosh * 0.9) * wobble * env_adsr(d, 0.05, 0.1, 1.0, 0.4)


def lava_sizzle():
    rng = np.random.default_rng(14)
    d = 1.2
    sizzle = filt(noise(d, rng), "bandpass", [2500, 8000]) * 0.35
    t = t_axis(d)
    sizzle *= 0.7 + 0.3 * np.abs(filt(rng.standard_normal(len(t)), "lowpass", 12))
    bubbles = np.zeros(len(t))
    for _ in range(9):
        bd = rng.uniform(0.05, 0.09)
        b = osc(sweep(rng.uniform(150, 260), rng.uniform(400, 700), bd), bd) * env_exp(bd, bd / 2.5)
        place(bubbles, b * rng.uniform(0.5, 1.0), rng.uniform(0, d - 0.1))
    return (sizzle + bubbles * 0.7) * env_adsr(d, 0.02, 0.1, 0.9, 0.4)


def sell():
    d = 0.6
    buf = np.zeros(int(SR * d))
    for k, f in enumerate([1568.0, 2093.0]):  # two coin pings
        coin = bell(f, 0.4, 0.09, harmonics=((1, 1.0), (2.4, 0.3), (4.1, 0.12)))
        place(buf, coin * 0.7, k * 0.07)
    return buf


def upgrade_purchased():
    d = 0.6
    buf = np.zeros(int(SR * d))
    for k, f in enumerate([659.25, 830.61, 987.77, 1318.5]):
        place(buf, soft(osc(f, 0.25, "square", 0.25), 4500) * env_exp(0.25, 0.08) * 0.4, k * 0.055)
    return buf


def prestige_upgrade_queued():
    d = 1.0
    buf = np.zeros(int(SR * d))
    for k, f in enumerate([293.66, 440.0, 587.33]):
        tone = mix(osc(f, 0.8, "tri"), osc(f * 1.006, 0.8, "tri")) * env_adsr(0.8, 0.02, 0.2, 0.4, 0.5) * 0.4
        place(buf, tone, k * 0.1)
    place(buf, bell(1760, 0.5, 0.15) * 0.2, 0.3)
    return buf


def prestige():
    rng = np.random.default_rng(15)
    d = 2.6
    t = t_axis(d)
    buf = np.zeros(len(t))
    chord = [261.63, 329.63, 392.0, 523.25, 659.25]
    for k, f in enumerate(chord):
        tone = mix(osc(f, 2.2, "saw"), osc(f * 1.004, 2.2, "saw"), osc(f * 0.996, 2.2, "tri"))
        tone = filt(tone, "lowpass", 2500) * env_adsr(2.2, 0.3, 0.4, 0.6, 1.2) * 0.25
        place(buf, tone, 0.1 + k * 0.06)
    riser = filt(noise(0.8, rng), "bandpass", [800, 5000]) * np.linspace(0, 1, int(SR * 0.8)) ** 2 * 0.4
    place(buf, riser, 0.0)
    for _ in range(25):
        place(buf, bell(rng.uniform(2000, 5000), 0.1, 0.03) * 0.15, rng.uniform(0.5, 2.2))
    buf = place(buf, osc(sweep(80, 40, 0.8), 0.8) * env_exp(0.8, 0.25) * 0.6, 0.75)
    return buf


def processing_started():
    rng = np.random.default_rng(16)
    d = 0.7
    t = t_axis(d)
    whirr = osc(sweep(120, 420, d), d, "saw") * (0.7 + 0.3 * osc(sweep(20, 45, d), d, "square"))
    whirr = filt(whirr, "lowpass", 1800) * env_adsr(d, 0.05, 0.1, 0.8, 0.25) * 0.5
    clunk = mix(osc(sweep(160, 70, 0.15), 0.15) * env_exp(0.15, 0.04),
                filt(noise(0.04, rng), "bandpass", [800, 3000]) * env_exp(0.04, 0.008, 0) * 0.6)
    return place(whirr, clunk, 0.0)


def processing_completed():
    d = 0.9
    buf = np.zeros(int(SR * d))
    place(buf, bell(1046.5, 0.6, 0.2, harmonics=((1, 1.0), (3, 0.2))) * 0.6, 0.0)
    place(buf, bell(1567.98, 0.7, 0.25, harmonics=((1, 1.0), (3, 0.2))) * 0.6, 0.13)
    return buf


def ui_click():
    d = 0.05
    return soft(osc(sweep(1400, 900, d), d, "square", 0.5), 3500) * env_exp(d, 0.012, 0.001)


def ui_hover():
    d = 0.035
    return osc(2200, d) * env_exp(d, 0.008, 0.001)


def music_loop():
    """16-bar chiptune-ish loop, 100 BPM, A minor: soft pad + bass + sparse arpeggio.
    Kept quiet and low-key so it sits under an idle game for long sessions."""
    bpm = 100
    beat = 60 / bpm
    bars = 16
    d = bars * 4 * beat
    n = int(SR * d)
    buf = np.zeros(n)
    # i - VI - III - VII progression (Am F C G), 4 bars each chord... 2 bars each, twice.
    prog = [(220.0, [220.0, 261.63, 329.63]),   # Am
            (174.61, [174.61, 220.0, 261.63]),  # F
            (130.81, [196.0, 261.63, 329.63]),  # C
            (196.0, [196.0, 246.94, 293.66])]   # G
    bar = 4 * beat
    for rep in range(bars // 2):
        root, triad = prog[rep % 4]
        start = rep * 2 * bar
        # pad: detuned triangles, slow swell
        for f in triad:
            tone = mix(osc(f, 2 * bar, "tri"), osc(f * 1.005, 2 * bar, "tri")) * env_adsr(2 * bar, 0.4, 0.5, 0.6, 0.6)
            place(buf, tone * 0.08, start)
        # bass: root on each beat, pulse wave, lowpassed
        for b in range(8):
            f = root / 2 if b % 4 != 3 else root / 2 * 1.5
            note = filt(osc(f, beat * 0.9, "square", 0.3), "lowpass", 600) * env_adsr(beat * 0.9, 0.005, 0.1, 0.5, 0.15)
            place(buf, note * 0.12, start + b * beat)
        # arpeggio: eighth notes, an octave up, only every other chord to keep it sparse
        if rep % 2 == 1 or rep >= 4:
            pattern = [0, 1, 2, 1, 0, 2, 1, 2]
            for s in range(16):
                f = triad[pattern[s % 8]] * 2
                note = soft(osc(f, beat * 0.45, "square", 0.25), 2500) * env_exp(beat * 0.45, 0.08)
                place(buf, note * 0.05, start + s * beat / 2)
    return soft(buf, 6000)


SOUNDS = {
    **{f"MiningHit_{i + 1}": (lambda i=i: mining_hit(i)) for i in range(3)},
    **{f"MineDirt_{i + 1}": (lambda i=i: mine_dirt(i)) for i in range(3)},
    **{f"MineOre_{i + 1}": (lambda i=i: mine_ore(i)) for i in range(3)},
    "ArtifactFound": artifact_found,
    "PowerUpCollected": powerup,
    "PlayerHurt": player_hurt,
    "ShieldBlock": shield_block,
    "PlayerDeath": player_death,
    "PlayerRevive": player_revive,
    "Warning": warning,
    "ExplosiveFuse": explosive_fuse,
    "Explosion": explosion,
    "RockRumble": rock_rumble,
    "RockLand": rock_land,
    "GasRelease": gas_release,
    "LavaSizzle": lava_sizzle,
    "Sell": sell,
    "UpgradePurchased": upgrade_purchased,
    "PrestigeUpgradeQueued": prestige_upgrade_queued,
    "Prestige": prestige,
    "ProcessingStarted": processing_started,
    "ProcessingCompleted": processing_completed,
    "UIClick": ui_click,
    "UIHover": ui_hover,
}

if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for name, fn in SOUNDS.items():
        write(name, fn())
    # Loops: no fade-out (would click at the loop point).
    write("Jetpack_Loop", jetpack_loop(), peak=0.8, fade_ms=0)
    music_dir = os.path.join(os.path.dirname(OUT.rstrip("/\\")), "Music")
    os.makedirs(music_dir, exist_ok=True)
    m = finish(music_loop(), 0.8, 0)
    wavfile.write(os.path.join(music_dir, "MineTheme_Loop.wav"), SR, (m * 32767).astype(np.int16))
    print(f"Music/MineTheme_Loop.wav  {len(m) / SR:.2f}s")
