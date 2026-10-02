"""Build the MineOre one-shots from raw fal.ai (stable-audio-25) generations.

Concept: a tactile "pop-clink" rather than a melody. A fat, warm bubble pop (~530Hz) is the
body, and a metallic ore clink, pitched down an octave and kept quiet, is the tick on top.
Each raw clip is 4s stereo holding several separate hits; a different hit goes into each of
the three variants, which are also pitched slightly apart.

Writes over Assets/Audio/SFX/MineOre_1..3.wav (existing .meta files / GUIDs are kept).

    python Tools/Audio/make_mine_ore.py
"""
import os
import numpy as np
from scipy.io import wavfile
from scipy.signal import resample_poly

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
RAW = os.path.join(ROOT, "Tools", "Audio", "raw", "MineOre_gen_{}.wav")
OUT = os.path.join(ROOT, "Assets", "Audio", "SFX", "MineOre_{}.wav")

PRE_ROLL = 0.003     # keep a hair before each onset so the attack isn't clipped
POP_LEN = 0.32
CLINK_LEN = 0.20
CLINK_SEMITONES = -12  # raw clink rings at ~3.9kHz, which is piercing at full pitch
CLINK_GAIN = 0.28
CLINK_DELAY = 0.012    # clink lands just inside the pop's attack
PEAK = 0.85

# (pop hit index, clink hit index, semitones for the whole variant)
VARIANTS = [(0, 0, -1.5), (1, 1, 0.0), (0, 2, 1.5)]


def load(name):
    sr, d = wavfile.read(RAW.format(name))
    x = d.astype(np.float64) / np.iinfo(d.dtype).max
    return sr, (x.mean(axis=1) if x.ndim > 1 else x)


def hits(x, sr, length, min_gap=0.3):
    """Slice out each separate hit, starting just before its onset."""
    thresh = 0.1 * np.abs(x).max()
    out, i = [], 0
    while i < len(x):
        if abs(x[i]) > thresh:
            start = max(0, i - int(PRE_ROLL * sr))
            out.append(x[start:start + int(length * sr)].copy())
            i += int(min_gap * sr)
        else:
            i += 1
    return out


def pitch(x, semitones):
    # Resampling shifts pitch and length together, which is fine for a one-shot.
    if semitones == 0:
        return x
    return resample_poly(x, 1000, int(round(1000 * 2 ** (semitones / 12))))


def fade(x, sr, fade_in=0.001, fade_out=0.04):
    n_in, n_out = int(fade_in * sr), int(fade_out * sr)
    x[:n_in] *= np.linspace(0, 1, n_in)
    x[-n_out:] *= np.linspace(1, 0, n_out) ** 2
    return x


def main():
    sr, pop_raw = load("Pop")
    _, clink_raw = load("Clink")
    pops = [p / np.abs(p).max() for p in hits(pop_raw, sr, POP_LEN)]
    clinks = [c / np.abs(c).max() for c in hits(clink_raw, sr, CLINK_LEN)]
    print(f"{len(pops)} pops, {len(clinks)} clinks found")

    for i, (p, c, st) in enumerate(VARIANTS):
        body = fade(pops[p].copy(), sr)
        tick = fade(pitch(clinks[c], CLINK_SEMITONES), sr) * CLINK_GAIN
        at = int(CLINK_DELAY * sr)
        y = np.zeros(max(len(body), at + len(tick)))
        y[:len(body)] += body
        y[at:at + len(tick)] += tick
        y = fade(pitch(y, st), sr)
        y *= PEAK / np.abs(y).max()
        wavfile.write(OUT.format(i + 1), sr, (y * 32767).astype(np.int16))
        print(f"MineOre_{i + 1}.wav  {st:+.1f}st  {len(y) / sr:.2f}s")


if __name__ == "__main__":
    main()
