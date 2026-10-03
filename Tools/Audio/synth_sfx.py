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


def chest_poof():
    # A chest giving up its ore: a soft, round puff of air over a small low thump. No notes.
    rng = np.random.default_rng(30)
    d = 0.45
    buf = np.zeros(int(SR * d))
    place(buf, poof(0.45, rng, 800, 0.1), 0)
    place(buf, poof(0.2, rng, 1500, 0.03) * 0.3, 0)
    place(buf, bloop(190, 80, 0.25, attack=0.004) * 0.6, 0)
    return buf


def ore_collect(i):
    # One ore chunk landing in the bag: a tiny pebble "tup". Played per chunk as a chest's loot
    # arrives, so a burst of them reads as a clatter. Non-melodic, like MineOre's pop.
    rng = np.random.default_rng(310 + i)
    d = 0.09
    f = [470.0, 530.0, 600.0][i]
    return mix(bloop(f, f * 0.6, d, attack=0.003),
               marimba(f / 2, d, 0.02) * 0.5,
               poof(0.03, rng, 1400, 0.006) * 0.25)


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


def player_land():
    # Feet hitting the ground after a fall: a short, round thump with a soft scuff of dust.
    # Lighter and higher than RockLand's boulder thud. No notes.
    rng = np.random.default_rng(27)
    d = 0.28
    buf = np.zeros(int(SR * d))
    place(buf, bloop(150, 60, 0.2, attack=0.003), 0)
    place(buf, poof(0.28, rng, 700, 0.06) * 0.55, 0)
    place(buf, poof(0.08, rng, 1400, 0.015) * 0.2, 0)
    return buf


def respawn_portal():
    # Portal woosh for the respawn arrival, timed to PlayerPortalTravel.ArrivalRoutine: air
    # swells in as the portal opens (0-0.3s), sweeps up to a peak as the player is spat out
    # (~0.45s), then falls away as it closes. The sweep is three noise bands taking turns, over
    # a quiet swirling hum. No notes - the old arpeggio was removed.
    rng = np.random.default_rng(9)
    d = 1.1
    t = t_axis(d)
    nz = noise(d, rng)

    def hump(center, width):
        return np.exp(-0.5 * ((t - center) / width) ** 2)

    low = filt(nz, "bandpass", [150, 500]) * hump(0.30, 0.20)
    mid = filt(nz, "bandpass", [400, 1100]) * hump(0.42, 0.13)
    high = filt(nz, "bandpass", [900, 2000]) * hump(0.48, 0.08)
    swirl_f = (130 + 190 * hump(0.45, 0.16)) * (1 + 0.05 * np.sin(2 * np.pi * 11 * t))
    swirl = osc(swirl_f, d) * hump(0.42, 0.22)
    buf = low * 0.7 + mid * 1.0 + high * 0.7 + swirl * 0.05
    place(buf, bloop(260, 120, 0.22, attack=0.01) * 0.07, 0.44)
    return soft(buf, 2200) * np.minimum(1, t / 0.08) * np.minimum(1, (d - t) / 0.3)


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


def rare_ore():
    # A rare ore landing in the bag: a quick sparkly rising arpeggio over a soft low "bloom".
    # Played an octave-ish higher for the merely rare tier (AudioService.PlayPitched).
    d = 0.9
    buf = np.zeros(int(SR * d))
    place(buf, bloop(196, 392, 0.3, attack=0.01) * 0.35, 0.0)
    for k, f in enumerate([392.0, 493.88, 587.33, 783.99]):
        place(buf, chime(f, 0.5, 0.14) * 0.5, 0.03 + k * 0.055)
    place(buf, chime(587.33, 0.6, 0.25) * 0.25, 0.25)
    return buf


def milestone():
    # Title card sting (new biome, layer bonus): a warm two-chord swell that resolves upward,
    # topped with a soft held chime.
    d = 1.8
    buf = np.zeros(int(SR * d))
    for chord, at in (([196.0, 246.94, 293.66], 0.0), ([261.63, 329.63, 392.0], 0.32)):
        for f in chord:
            tone = mix(osc(f, 1.3, "tri"), osc(f * 1.004, 1.3))
            place(buf, soft(tone, 1600) * env_adsr(1.3, 0.06, 0.3, 0.55, 0.7) * 0.2, at)
    place(buf, chime(523.25, 1.1, 0.4) * 0.45, 0.34)
    place(buf, chime(783.99, 0.9, 0.3) * 0.25, 0.42)
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


def critter_caught():
    # Scooped into the jar: a rising bubble "bwoop" landing on a two-note marimba "got it!".
    d = 0.45
    buf = np.zeros(int(SR * d))
    place(buf, bloop(300, 620, 0.16, attack=0.01) * 0.7, 0.0)
    place(buf, marimba(523.25, 0.25, 0.08) * 0.45, 0.12)
    return place(buf, marimba(659.25, 0.3, 0.1) * 0.45, 0.2)


def critter_turn_in():
    # Happy wooden arpeggio up to C5 over a soft poof of the jar lid coming off.
    rng = np.random.default_rng(900)
    d = 1.0
    buf = np.zeros(int(SR * d))
    place(buf, poof(0.25, rng, cutoff=600) * 0.25, 0.0)
    for i, f in enumerate([261.63, 329.63, 392.0, 523.25]):
        place(buf, marimba(f, 0.35, 0.12) * 0.5, 0.05 + i * 0.09)
    return buf


def hat_unlocked():
    # A springy "boing" (the hat popping on) then a gentle three-note chime fanfare.
    d = 1.3
    buf = np.zeros(int(SR * d))
    place(buf, boing(220, 0.4, depth=0.18, rate=11) * 0.6, 0.0)
    for i, f in enumerate([392.0, 523.25, 659.25]):
        place(buf, chime(f, 0.7, 0.25) * 0.45, 0.3 + i * 0.12)
    return buf


def dialog_blip():
    # Tiny soft "bop" per few typed characters - SoundLibrary pitch variance makes it chatter.
    d = 0.05
    return bloop(460, 380, d, attack=0.004)


def bound_voice(i):
    # The Bound's "voice" while its dialog types out: one murmured syllable per clip. A breathy
    # whisper shaped by two vowel formants over a low, slightly detuned hum that droops in pitch,
    # swelling in rather than striking (reads as a voice from far below), with a faint echo.
    # Everything stays under ~2kHz so it is eerie without being hissy.
    rng = np.random.default_rng(60 + i)
    formants = [(320, 900), (480, 1250), (280, 720), (560, 1500), (400, 1050)][i]
    pitch = [98.0, 110.0, 87.3, 116.5, 103.8][i]
    d = 0.2
    n = int(SR * d)
    t = t_axis(d)
    swell = np.sin(np.pi * np.clip(t / d, 0, 1)) ** 1.5
    breath = noise(d, rng)
    breath = (filt(breath, "bandpass", [formants[0] * 0.8, formants[0] * 1.25])
              + filt(breath, "bandpass", [formants[1] * 0.85, formants[1] * 1.18]) * 0.6)
    f = sweep(pitch * 1.06, pitch * 0.94, d) * (1 + 0.012 * np.sin(2 * np.pi * 7 * t))
    hum = osc(f, d) + osc(f * 1.012, d) * 0.8 + osc(f * 2.0, d, "tri") * 0.25
    hum = filt(hum, "lowpass", formants[1], order=2)
    syllable = (breath / np.max(np.abs(breath)) + hum / np.max(np.abs(hum)) * 0.22) * swell
    buf = np.zeros(n + int(SR * 0.16))
    place(buf, syllable, 0.0)
    place(buf, filt(syllable, "lowpass", 700) * 0.3, 0.09)
    return filt(buf, "lowpass", 2000, order=4)


def building_portal():
    # The reveal portal tearing open in the sky: a soft whoosh that swells in under a rising,
    # warbling slide whistle, settling into a low hum.
    rng = np.random.default_rng(40)
    d = 1.2
    t = t_axis(d)
    swell = np.where(t < 0.35, (t / 0.35) ** 2, np.exp(-(t - 0.35) / 0.3))
    whoosh = filt(noise(d, rng), "bandpass", [180, 900]) * swell
    rise = slide_whistle(170, 520, 0.5, vib_rate=9.0, vib_depth=0.04) * env_adsr(0.5, 0.08, 0.1, 0.8, 0.2)
    hum = mix(osc(130.81, d), osc(196.0, d) * 0.5) * env_adsr(d, 0.3, 0.2, 0.7, 0.5)
    return mix(whoosh * 0.9, rise * 0.45, hum * 0.35)


def building_land():
    # A building plopping onto the ground: a big round thump, a springy wobble as it settles,
    # a long puff of dust and a few pebbles.
    rng = np.random.default_rng(41)
    d = 1.1
    buf = np.zeros(int(SR * d))
    place(buf, bloop(110, 36, 0.6, attack=0.003) * 1.4, 0.0)
    place(buf, poof(0.8, rng, 500, 0.22) * 0.7, 0.01)
    place(buf, boing(98, 0.45, depth=0.18, rate=11) * 0.3, 0.08)
    place(buf, plinks(0.5, rng, 6, [196.0, 220.0, 261.63, 293.66], gain=0.14, decay=0.04), 0.3)
    return buf


# ---------- ambient loops ----------
# 24s beds, seamless by construction: tones use whole cycles per loop, noise is FFT-circular,
# and one-shot events wrap around the end. Everything sits under ~1.2kHz and stays quiet -
# they play under the music for as long as the player is in a layer.

AMBIENCE_SECONDS = 24.0


def place_wrap(buf, x, at):
    i = int(SR * at) % len(buf)
    first = min(len(x), len(buf) - i)
    buf[i:i + first] += x[:first]
    if first < len(x):
        buf[:len(x) - first] += x[first:]
    return buf


def loop_tone(freq, kind="sine"):
    # Snap to a whole number of cycles over the loop so the wrap is click-free.
    cycles = max(1, round(freq * AMBIENCE_SECONDS))
    return osc(cycles / AMBIENCE_SECONDS, AMBIENCE_SECONDS, kind)


def loop_lfo(cycles, depth, phase=0.0):
    t = t_axis(AMBIENCE_SECONDS)
    return 1 - depth * 0.5 * (1 - np.cos(2 * np.pi * cycles * t / AMBIENCE_SECONDS + phase))


def drips(buf, rng, count, gain=0.2):
    for _ in range(count):
        f0 = rng.uniform(650, 850)
        place_wrap(buf, bloop(f0, f0 * 0.55, 0.09, attack=0.002) * gain * rng.uniform(0.5, 1.0), rng.uniform(0, AMBIENCE_SECONDS))
    return buf


def ambience_shallow():
    # Earthy topsoil: low rumble that breathes, far-off pebble plinks, a few drips.
    rng = np.random.default_rng(1001)
    buf = loop_noise(AMBIENCE_SECONDS, rng, (40, 220)) * 0.5 * loop_lfo(3, 0.5)
    buf = buf / np.max(np.abs(buf)) * 0.35
    for _ in range(9):
        f = rng.choice([196.0, 220.0, 261.63, 293.66])
        place_wrap(buf, marimba(f, 0.2, 0.05) * 0.12 * rng.uniform(0.5, 1.0), rng.uniform(0, AMBIENCE_SECONDS))
    return drips(buf, rng, 6, gain=0.14)


def ambience_crystal():
    # Crystal caverns: a hollow open-fifth pad with slow swells, sparse soft glassy chimes, drips.
    rng = np.random.default_rng(1002)
    pad = (loop_tone(110) * loop_lfo(2, 0.6) + loop_tone(165) * loop_lfo(3, 0.7, 1.0) * 0.7
           + loop_tone(220, "tri") * loop_lfo(4, 0.8, 2.0) * 0.3)
    buf = pad * 0.18 + loop_noise(AMBIENCE_SECONDS, rng, (150, 700)) * 0.04
    for _ in range(7):
        f = rng.choice([523.25, 587.33, 659.25, 783.99])
        place_wrap(buf, chime(f, 2.0, 0.6) * 0.1, rng.uniform(0, AMBIENCE_SECONDS))
    return drips(buf, rng, 8, gain=0.16)


def ambience_moss():
    # Mossy glow depths: warm breathing hum, gentle leafy rustle, little bug "bloops".
    rng = np.random.default_rng(1003)
    hum = loop_tone(98, "tri") * 0.6 + loop_tone(147) * 0.35 + loop_tone(196) * 0.12
    # No IIR filtering here - it isn't circular and would put a click at the loop point.
    buf = hum * loop_lfo(3, 0.5) * 0.2
    buf += loop_noise(AMBIENCE_SECONDS, rng, (250, 1000)) * loop_lfo(2, 0.8, 0.5) * 0.05
    for _ in range(12):
        f = rng.uniform(320, 520)
        place_wrap(buf, bloop(f, f * rng.choice([0.7, 1.4]), 0.07) * 0.09, rng.uniform(0, AMBIENCE_SECONDS))
    return drips(buf, rng, 5, gain=0.12)


def ambience_void():
    # The abyss: a deep slowly-beating drone, an airy swell, the occasional far-off deep bloop.
    rng = np.random.default_rng(1004)
    drone = loop_tone(55) + loop_tone(55.25) * 0.9 + loop_tone(82.5) * 0.4
    buf = drone * 0.16 + loop_noise(AMBIENCE_SECONDS, rng, (90, 500)) * loop_lfo(2, 0.9) * 0.08
    for _ in range(4):
        place_wrap(buf, bloop(150, 60, 0.8, attack=0.05) * 0.25, rng.uniform(0, AMBIENCE_SECONDS))
    return buf


AMBIENCE = {
    "Ambience_Shallow_Loop": ambience_shallow,
    "Ambience_Crystal_Loop": ambience_crystal,
    "Ambience_Moss_Loop": ambience_moss,
    "Ambience_Void_Loop": ambience_void,
}


SOUNDS = {
    **{f"MiningHit_{i + 1}": (lambda i=i: mining_hit(i)) for i in range(3)},
    **{f"MineDirt_{i + 1}": (lambda i=i: mine_dirt(i)) for i in range(3)},
    # MineOre_1..3 are cut from an AI-generated clip: see make_mine_ore.py.
    "ArtifactFound": artifact_found,
    "PowerUpCollected": powerup,
    "ChestPoof": chest_poof,
    **{f"OreCollect_{i + 1}": (lambda i=i: ore_collect(i)) for i in range(3)},
    "RareOre": rare_ore,
    "PlayerHurt": player_hurt,
    "ShieldBlock": shield_block,
    "PlayerDeath": player_death,
    "RespawnPortal": respawn_portal,
    "PlayerLand": player_land,
    "Warning": warning,
    "ExplosiveFuse": explosive_fuse,
    "Explosion": explosion,
    "RockRumble": rock_rumble,
    "RockLand": rock_land,
    "GasRelease": gas_release,
    "LavaSizzle": lava_sizzle,
    "Sell": sell,
    "Deposit": deposit,
    "Milestone": milestone,
    "UpgradePurchased": soft_click,
    "PrestigeUpgradeQueued": soft_click,
    "Prestige": prestige,
    "ProcessingStarted": processing_started,
    "ProcessingCompleted": processing_completed,
    "UIClick": ui_click,
    "UIHover": ui_hover,
    "CritterCaught": critter_caught,
    "CritterTurnIn": critter_turn_in,
    "HatUnlocked": hat_unlocked,
    "DialogBlip": dialog_blip,
    **{f"BoundVoice_{i + 1}": (lambda i=i: bound_voice(i)) for i in range(5)},
    "BuildingPortal": building_portal,
    "BuildingLand": building_land,
}

if __name__ == "__main__":
    # Optional extra args: only (re)write the named sounds/ambience loops, e.g.
    #   python Tools/Audio/synth_sfx.py Assets/Audio/SFX CritterCaught Ambience_Void_Loop
    only = set(sys.argv[2:])
    os.makedirs(OUT, exist_ok=True)
    ambience_dir = os.path.join(os.path.dirname(OUT.rstrip("/\\")), "Ambience")
    os.makedirs(ambience_dir, exist_ok=True)
    for name, fn in AMBIENCE.items():
        if only and name not in only:
            continue
        a = finish(fn(), 0.6, 0)
        wavfile.write(os.path.join(ambience_dir, name + ".wav"), SR, (a * 32767).astype(np.int16))
        print(f"Ambience/{name}.wav  {len(a) / SR:.2f}s")
    if only:
        for name in only:
            if name in SOUNDS:
                write(name, SOUNDS[name]())
        sys.exit(0)
    for name, fn in SOUNDS.items():
        write(name, fn())
    # Loops: no fade-out (would click at the loop point).
    write("Jetpack_Loop", jetpack_loop(), peak=0.7, fade_ms=0)
    music_dir = os.path.join(os.path.dirname(OUT.rstrip("/\\")), "Music")
    os.makedirs(music_dir, exist_ok=True)
    m = finish(music_loop(), 0.8, 0)
    wavfile.write(os.path.join(music_dir, "MineTheme_Loop.wav"), SR, (m * 32767).astype(np.int16))
    print(f"Music/MineTheme_Loop.wav  {len(m) / SR:.2f}s")
