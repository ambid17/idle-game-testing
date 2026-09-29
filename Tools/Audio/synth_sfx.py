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


# ---------- whimsical building blocks ----------
# House style (2026-09-29): soft and toy-like rather than harsh chiptune. Sines/triangles over
# squares/saws, pitch-bent "bloops", marimba/chime notes, rounded attacks, and almost no
# energy above ~2.5kHz (hiss up there and fast amplitude flutter read as harsh). Tonal "reward"
# notes stay at or below ~800Hz: bright 1-2kHz chimes read as harsh even without any hiss.

PENTA = [523.25, 587.33, 659.25, 783.99, 880.0]  # C major pentatonic, C5-A5


def marimba(freq, dur, decay):
    """Wooden mallet note: sine fundamental plus a quickly-fading 4th-harmonic knock."""
    return (osc(freq, dur) * env_exp(dur, decay, attack=0.002)
            + osc(freq * 4, dur) * env_exp(dur, decay / 5, attack=0.001) * 0.18)


def chime(freq, dur, decay):
    """Soft chime: near-pure sine with a ~12ms swell-in. A quick attack or bright overtones
    made the earlier music-box version sound harsh."""
    return (osc(freq, dur) * env_exp(dur, decay, attack=0.012)
            + osc(freq * 2, dur) * env_exp(dur, decay / 2, attack=0.012) * 0.08)


def bloop(f0, f1, dur, attack=0.006):
    """Cartoon bubble/pop: a pitch-bent sine with a rounded attack."""
    return osc(sweep(f0, f1, dur), dur) * env_exp(dur, dur / 3, attack=attack)


def boing(freq, dur, depth=0.12, rate=14, attack=0.004):
    """Spring 'boing': a pitch wobble that settles as the note decays."""
    t = t_axis(dur)
    f = freq * (1 + depth * np.sin(2 * np.pi * rate * t) * np.exp(-t / (dur / 3)))
    return osc(f, dur) * env_exp(dur, dur / 3, attack=attack)


def slide_whistle(f0, f1, dur, vib_rate=6.0, vib_depth=0.025):
    t = t_axis(dur)
    f = sweep(f0, f1, dur) * (1 + vib_depth * np.sin(2 * np.pi * vib_rate * t))
    return mix(osc(f, dur), osc(f * 2, dur) * 0.12)


def poof(dur, rng, cutoff=900, decay=None):
    """Soft cartoon puff of air/dust: low-passed noise with a rounded start."""
    return filt(noise(dur, rng), "lowpass", cutoff, order=4) * env_exp(dur, decay or dur / 4, attack=0.008)


def plinks(dur, rng, count, notes, gain=0.3, decay=0.05):
    """Scattered pebble 'plinks' tuned to a scale, so debris sounds cute instead of gritty."""
    buf = np.zeros(int(SR * dur))
    for _ in range(count):
        f = rng.choice(notes) * rng.uniform(0.99, 1.01)
        place(buf, marimba(f, 0.12, decay) * rng.uniform(0.5, 1.0) * gain, rng.uniform(0, dur - 0.12))
    return buf


# ---------- sounds ----------

def mining_hit(i):
    # Wooden "tok" of a toy pickaxe: marimba knock over a soft low thump.
    rng = np.random.default_rng(100 + i)
    d = 0.16
    tok = marimba([392.0, 440.0, 349.23][i], d, 0.035)
    thump = bloop(180 - 15 * i, 80, d, attack=0.002)
    return mix(tok * 0.8, thump, poof(0.03, rng, 1500, 0.006) * 0.3)


def mine_dirt(i):
    # Soft "pomf" of dirt giving way, plus a little plop.
    rng = np.random.default_rng(200 + i)
    d = 0.3
    pomf = soft(crunch(d, rng, 150, 900 + 100 * i, grain_rate=70, decay=0.07), 1200)
    return mix(pomf, bloop(260 - 20 * i, 110, 0.14) * 0.7)


def mine_ore(i):
    # Dirt pomf + a soft two-note chime (grace note a fourth below).
    d = 0.55
    note = [392.0, 440.0, 523.25][i]  # G4 / A4 / C5
    buf = np.zeros(int(SR * d))
    place(buf, chime(note * 0.75, 0.3, 0.06) * 0.3, 0.02)
    place(buf, chime(note, 0.5, 0.14) * 0.6, 0.08)
    return mix(mine_dirt(i) * 0.5, buf)


def artifact_found():
    # Gentle chime "ta-da": a rising run into a held two-note top.
    d = 1.3
    buf = np.zeros(int(SR * d))
    for k, f in enumerate([261.63, 329.63, 392.0, 523.25]):
        place(buf, chime(f, 0.6, 0.2) * 0.55, k * 0.09)
    place(buf, chime(659.25, 0.9, 0.35) * 0.6, 0.36)
    place(buf, chime(329.63, 0.9, 0.35) * 0.35, 0.36)
    return buf


def powerup():
    # Low slide-whistle swoop up, then a soft two-note "bling".
    d = 0.8
    buf = np.zeros(int(SR * d))
    place(buf, slide_whistle(260, 620, 0.35) * env_adsr(0.35, 0.03, 0.05, 0.8, 0.08) * 0.5, 0)
    place(buf, chime(523.25, 0.35, 0.1) * 0.5, 0.3)
    place(buf, chime(783.99, 0.4, 0.14) * 0.5, 0.38)
    return buf


def player_hurt():
    # Cartoon "bonk": a springy boing that sags in pitch, over a soft thump.
    rng = np.random.default_rng(5)
    d = 0.35
    t = t_axis(d)
    f = sweep(420, 220, d) * (1 + 0.1 * np.sin(2 * np.pi * 16 * t) * np.exp(-t / 0.12))
    bonk = osc(f, d) * env_exp(d, 0.1, attack=0.003)
    return mix(bonk * 0.8, bloop(150, 70, 0.15, attack=0.002) * 0.8, poof(0.08, rng, 1000, 0.02) * 0.3)


def shield_block():
    # Bouncy "bwoing" off a bubble shield, with a soft glassy ding on top.
    d = 0.7
    ding = mix(chime(1318.5, d, 0.18), chime(1975.53, d, 0.12) * 0.4)
    return mix(boing(330, d, depth=0.15, rate=12) * 0.8, ding * 0.35)


def explosion():
    # Cartoon "ka-POOF": a big soft puff with a deep thump, then debris bouncing to a stop.
    rng = np.random.default_rng(7)
    d = 1.4
    buf = np.zeros(int(SR * d))
    place(buf, poof(1.0, rng, 700, 0.22) * 1.2, 0)
    place(buf, poof(0.5, rng, 1600, 0.06) * 0.4, 0)
    place(buf, bloop(110, 38, 0.8, attack=0.004) * 1.4, 0)
    at, gap = 0.18, 0.16
    for k in range(6):
        place(buf, bloop(rng.uniform(240, 360), 120, 0.1) * 0.4 * 0.8 ** k, at)
        at += gap
        gap *= 0.75
    return buf


def player_death():
    # Sad slide-whistle droop, ending in a soft poof.
    rng = np.random.default_rng(8)
    d = 1.4
    buf = np.zeros(int(SR * d))
    droop = slide_whistle(620, 160, 1.0, vib_rate=5.0, vib_depth=0.04) * env_adsr(1.0, 0.02, 0.1, 0.8, 0.35)
    place(buf, droop * 0.5, 0)
    place(buf, poof(0.5, rng, 600, 0.12) * 0.8, 0.9)
    place(buf, bloop(120, 45, 0.4) * 0.8, 0.9)
    return buf


def player_revive():
    # Gentle music-box arpeggio up over a soft chorused glow.
    d = 1.2
    buf = np.zeros(int(SR * d))
    for k, f in enumerate([392.0, 523.25, 659.25, 783.99, 1046.5]):
        place(buf, chime(f, 0.6, 0.22) * 0.55, k * 0.1)
    glow = mix(osc(523.25, d, "tri"), osc(523.25 * 1.004, d, "tri"), osc(783.99 * 0.998, d, "tri") * 0.6)
    return mix(buf, soft(glow, 1500) * env_adsr(d, 0.35, 0.2, 0.5, 0.5) * 0.12)


def jetpack_loop():
    # Whimsical toy-rocket "putt-putt": soft sine bloops (little puffs, pitch dropping) riding on
    # a quiet pillowy airflow, plus a warbly hum like a tiny motor. Everything stays under ~1.2kHz
    # and the puffs have rounded attacks - hiss above 2kHz and sharp flutter read as harsh.
    # 2s loop; puffs wrap around the loop point and every rate/pitch is a multiple of 1/d Hz so
    # the loop stays seamless.
    rng = np.random.default_rng(9)
    d = 2.0
    n = int(SR * d)
    t = t_axis(d)
    air = loop_noise(d, rng, (80, 500))
    air /= np.max(np.abs(air))
    wisp = loop_noise(d, rng, (500, 1200))
    wisp /= np.max(np.abs(wisp))

    # 12 puffs in 2s (6/s), gently swung, with a small repeating pitch pattern for a bouncy feel.
    puffs = np.zeros(n)
    pitches = [330, 294, 349, 294, 330, 262]
    for k in range(12):
        at = k * d / 12 + (0.012 if k % 2 else 0.0)
        pd = 0.13
        f0 = pitches[k % len(pitches)] * rng.uniform(0.98, 1.02)
        bloop = osc(sweep(f0 * 1.35, f0 * 0.8, pd), pd) * env_adsr(pd, 0.012, 0.03, 0.45, 0.085)
        bloop += osc(sweep(f0 * 2.7, f0 * 1.6, pd), pd) * env_exp(pd, 0.025, attack=0.008) * 0.15
        i = int(SR * at)
        idx = (np.arange(len(bloop)) + i) % n  # wrap across the loop point
        np.add.at(puffs, idx, bloop * rng.uniform(0.85, 1.0))

    # Warbly toy-motor hum: 196Hz sine + octave, slow 5Hz vibrato (±2.5%). Pure sines, no IIR
    # filtering, so there's no filter warm-up transient at the loop seam.
    vib = 1 + 0.025 * np.sin(2 * np.pi * 5.0 * t)
    hum = osc(196.0 * vib, d) + osc(392.0 * vib, d) * 0.3

    swell = 1 + 0.06 * np.sin(2 * np.pi * 1.5 * t)
    # Airflow kept well under the puffs - at 0.35 it read as a loud fan.
    return (puffs * 0.55 + air * 0.12 + wisp * 0.02 + hum * 0.12) * swell


def warning():
    # Polite "boop-boop": noticeable, but a rounded triangle rather than a buzzer.
    d = 0.5
    buf = np.zeros(int(SR * d))
    for k, f in enumerate([783.99, 587.33]):
        note = soft(osc(f, 0.14, "tri"), 2500) * env_adsr(0.14, 0.008, 0.03, 0.7, 0.06)
        place(buf, note * 0.6, k * 0.17)
    return buf


def explosive_fuse():
    # Just a fuse sizzle: crackly band-limited noise (capped ~3.5kHz) over a warmer fizz, swelling
    # toward detonation. No ticks or pops.
    rng = np.random.default_rng(10)
    d = 1.0
    t = t_axis(d)
    nz = noise(d, rng)
    sizzle = filt(filt(nz, "bandpass", [1200, 3200]), "lowpass", 3500, order=4)
    warm = filt(nz, "bandpass", [400, 1200])
    crackle = np.abs(filt(rng.standard_normal(len(t)), "lowpass", 30))
    crackle = 0.4 + 0.6 * crackle / np.max(crackle)
    return (sizzle * crackle + warm * 0.3) * np.linspace(0.5, 1.0, len(t)) * env_adsr(d, 0.05, 0.05, 1.0, 0.06)


def rock_rumble():
    # Slow, wobbly low grumble with clonky wooden knocks - a rock jiggling loose.
    rng = np.random.default_rng(11)
    d = 0.6
    t = t_axis(d)
    rumble = filt(noise(d, rng), "lowpass", 200, order=4)
    rumble /= np.max(np.abs(rumble))
    wobble = 0.75 + 0.25 * np.sin(2 * np.pi * 5 * t)
    knocks = np.zeros(len(t))
    for f, at in zip([196.0, 220.0, 174.61, 207.65], [0.03, 0.17, 0.29, 0.43]):
        place(knocks, marimba(f, 0.12, 0.03) * 0.5, at)
    return (rumble * wobble * 0.8 + knocks) * env_adsr(d, 0.04, 0.1, 0.9, 0.2)


def rock_land():
    # Just a thud: a deep, round pitch-dropping thump with a little low dust.
    rng = np.random.default_rng(12)
    d = 0.5
    return mix(bloop(90, 40, d, attack=0.003) * 1.3, poof(0.25, rng, 350, 0.05) * 0.6)


def gas_release():
    # Short soft whoosh: noise that swells in, brightens slightly at the peak, and falls away.
    rng = np.random.default_rng(13)
    d = 0.6
    t = t_axis(d)
    nz = noise(d, rng)
    low = filt(nz, "bandpass", [200, 700])
    high = filt(nz, "bandpass", [600, 1600])
    swell = np.where(t < 0.2, (t / 0.2) ** 2, np.exp(-(t - 0.2) / 0.12))
    return (low * 0.8 + high * swell * 0.7) * swell


def lava_sizzle():
    # Gloopy lava: mostly fat rising bubble blorps, with only a faint, low sizzle.
    rng = np.random.default_rng(14)
    d = 1.2
    t = t_axis(d)
    sizzle = filt(noise(d, rng), "bandpass", [1200, 3000]) * 0.12
    sizzle *= 0.6 + 0.4 * np.abs(filt(rng.standard_normal(len(t)), "lowpass", 10))
    bubbles = np.zeros(len(t))
    for _ in range(12):
        bd = rng.uniform(0.06, 0.12)
        place(bubbles, bloop(rng.uniform(140, 240), rng.uniform(380, 650), bd) * rng.uniform(0.5, 1.0), rng.uniform(0, d - 0.13))
    return (sizzle + bubbles * 0.7) * env_adsr(d, 0.02, 0.1, 0.9, 0.4)


def sell():
    # Toy cash-register "ka-ching": a wooden tap then a music-box coin ping.
    d = 0.7
    buf = np.zeros(int(SR * d))
    place(buf, marimba(1046.5, 0.3, 0.06) * 0.5, 0)
    place(buf, chime(1567.98, 0.6, 0.16) * 0.7, 0.08)
    place(buf, chime(2093.0, 0.5, 0.12) * 0.35, 0.08)
    return buf


def deposit():
    # Ore tipped into the crate: a wooden thump, then pebbles settling as tuned little plinks.
    rng = np.random.default_rng(20)
    d = 0.6
    buf = np.zeros(int(SR * d))
    place(buf, marimba(146.83, 0.25, 0.06), 0)
    place(buf, bloop(140, 70, 0.2, attack=0.002) * 0.8, 0)
    settle = plinks(0.5, rng, 7, PENTA, gain=0.35, decay=0.04) * np.linspace(1, 0.4, int(SR * 0.5))
    return place(buf, settle, 0.04)


def soft_click():
    # Market upgrade purchased AND prestige upgrade queued: a soft wooden "tock" - just a click.
    d = 0.09
    tock = osc(sweep(620, 420, d), d) * env_exp(d, 0.018, attack=0.0015)
    body = osc(sweep(240, 160, d), d) * env_exp(d, 0.025, attack=0.0015) * 0.6
    return mix(tock, body)


def prestige():
    # Just the warm synth chord bloom (the earlier slide-whistle/chime intro was unpleasant).
    d = 2.6
    buf = np.zeros(int(SR * d))
    for k, f in enumerate([261.63, 329.63, 392.0, 523.25, 659.25]):
        tone = mix(osc(f, 2.2, "tri"), osc(f * 1.004, 2.2, "tri"), osc(f * 0.996, 2.2))
        place(buf, soft(tone, 1800) * env_adsr(2.2, 0.25, 0.4, 0.6, 1.2) * 0.22, 0.05 + k * 0.05)
    return buf


def processing_started():
    # A little machine happily switching on: two rising soft bloops ("bwoop-bwip") over a brief,
    # warm hum swell.
    d = 0.6
    buf = mix(osc(130.81, d), osc(196.0, d) * 0.4) * env_adsr(d, 0.1, 0.1, 0.6, 0.3) * 0.3
    place(buf, osc(sweep(220, 330, 0.14), 0.14) * env_adsr(0.14, 0.015, 0.03, 0.6, 0.08) * 0.6, 0.0)
    return place(buf, osc(sweep(330, 494, 0.16), 0.16) * env_adsr(0.16, 0.015, 0.03, 0.6, 0.1) * 0.6, 0.15)


def processing_completed():
    # Soft chime "ding-ding", an octave below the earlier too-bright version.
    d = 0.9
    buf = np.zeros(int(SR * d))
    place(buf, chime(523.25, 0.6, 0.18) * 0.6, 0.0)
    return place(buf, chime(783.99, 0.7, 0.22) * 0.6, 0.13)


def ui_click():
    # Soft bubble "plip".
    d = 0.06
    return bloop(900, 600, d, attack=0.002)


def ui_hover():
    d = 0.04
    return osc(1318.5, d) * env_exp(d, 0.01, 0.003)


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
    "Deposit": deposit,
    "UpgradePurchased": soft_click,
    "PrestigeUpgradeQueued": soft_click,
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
    write("Jetpack_Loop", jetpack_loop(), peak=0.7, fade_ms=0)
    music_dir = os.path.join(os.path.dirname(OUT.rstrip("/\\")), "Music")
    os.makedirs(music_dir, exist_ok=True)
    m = finish(music_loop(), 0.8, 0)
    wavfile.write(os.path.join(music_dir, "MineTheme_Loop.wav"), SR, (m * 32767).astype(np.int16))
    print(f"Music/MineTheme_Loop.wav  {len(m) / SR:.2f}s")
