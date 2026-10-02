"""Cuts seamless loops out of the AI-generated music in Assets/Audio/Music/Generated.

For each track: find the real bar length near the prompted tempo, pick a loop start/end a whole
number of bars apart whose following bars sound most alike, then fade the audio just past the
loop end in under the loop start (equal-power) so the wrap is continuous by construction.

Run from the repo root:  python Tools/Audio/make_music_loops.py
"""
import os
import numpy as np
from scipy import signal
from scipy.io import wavfile

from analyze_music import load

RAW_DIR = 'Assets/Audio/Music/Generated'
OUT_DIR = 'Assets/Audio/Music'
XFADE = 3.0          # seconds of post-loop audio faded in under the loop start
PEAK = 10 ** (-1 / 20)
MIN_LEN = 48.0       # seconds; shorter loops get repetitive

# name: (prompted BPM, earliest start s, latest usable s, high-shelf dB above 2.5 kHz)
TRACKS = {
    'MineAdventure4': (80, 1.0, 84.0, 0.0),
    'MineAdventure5': (84, 1.0, 88.0, 0.0),
    'MineAdventure6': (88, 1.0, 84.0, 0.0),
    'MineAdventure7': (76, 25.0, 86.0, 0.0),   # quieter first 25s
    'MineAdventure8': (80, 10.0, 84.0, -5.0),  # glockenspiel lead is bright; soften it
    'MineAdventure9': (92, 5.0, 84.0, 0.0),
}


def onset_envelope(mono, sr, hop=441):
    f, t, Z = signal.stft(mono, sr, nperseg=2048, noverlap=2048 - hop)
    mag = np.log1p(50 * np.abs(Z))
    flux = np.maximum(0, np.diff(mag, axis=1)).sum(axis=0)
    return flux - flux.mean(), sr / hop, mag


def beat_period(env, env_sr, bpm):
    """Refines the beat length by autocorrelation within +-4% of the prompted tempo."""
    nominal = 60.0 / bpm
    ac = signal.correlate(env, env, mode='full')[len(env) - 1:]
    best, best_score = nominal, -np.inf
    for period in np.linspace(nominal * 0.96, nominal * 1.04, 161):
        # Sum the autocorrelation at 4, 8 and 16 beats so small errors show up.
        score = sum(np.interp(period * n * env_sr, np.arange(len(ac)), ac) for n in (4, 8, 16))
        if score > best_score:
            best, best_score = period, score
    return best


def high_shelf(x, sr, gain_db, freq=2500.0):
    if gain_db == 0:
        return x
    # RBJ high shelf, slope 1.
    A = 10 ** (gain_db / 40)
    w = 2 * np.pi * freq / sr
    alpha = np.sin(w) / 2 * np.sqrt(2)
    c = np.cos(w)
    b = [A * ((A + 1) + (A - 1) * c + 2 * np.sqrt(A) * alpha), -2 * A * ((A - 1) + (A + 1) * c),
         A * ((A + 1) + (A - 1) * c - 2 * np.sqrt(A) * alpha)]
    a = [(A + 1) - (A - 1) * c + 2 * np.sqrt(A) * alpha, 2 * ((A - 1) - (A + 1) * c),
         (A + 1) - (A - 1) * c - 2 * np.sqrt(A) * alpha]
    return signal.lfilter(np.array(b) / a[0], np.array(a) / a[0], x, axis=0)


def cut(name, bpm, start_min, end_max, shelf_db):
    sr, d = load(os.path.join(RAW_DIR, name + '_Raw.wav'))
    mono = d.mean(axis=1)
    env, env_sr, mag = onset_envelope(mono, sr)
    beat = beat_period(env, env_sr, bpm)
    bar = beat * 4

    def frames(t, length):
        i = int(round(t * env_sr))
        return mag[:, i:i + int(round(length * env_sr))]

    def similarity(s, e):
        a, b = frames(s, bar), frames(e, bar)
        n = min(a.shape[1], b.shape[1])
        a, b = a[:, :n].ravel(), b[:, :n].ravel()
        return float(a @ b / (np.linalg.norm(a) * np.linalg.norm(b) + 1e-12))

    # Candidate starts: the strongest onsets in the first few bars after start_min.
    lo, hi = int(start_min * env_sr), int((start_min + 4 * bar) * env_sr)
    peaks, _ = signal.find_peaks(env[lo:hi], distance=int(beat * env_sr / 2))
    peaks = peaks[np.argsort(env[lo:hi][peaks])[::-1][:8]] + lo
    best = None
    for p in peaks:
        s = p / env_sr - 0.01
        k = int(np.ceil(MIN_LEN / bar))
        while s + k * bar + max(XFADE, bar) <= end_max:
            e = s + k * bar
            score = similarity(s, e) + 0.0005 * k  # near-ties go to the longer loop
            if best is None or score > best[0]:
                best = (score, s, e, k)
            k += 1
    if best is None:
        raise RuntimeError(f'{name}: no loop candidate fits')
    score, s, e, k = best

    si, ei, xf = int(round(s * sr)), int(round(e * sr)), int(XFADE * sr)
    loop = d[si:ei].copy()
    ramp = np.linspace(0, np.pi / 2, xf)[:, None]
    loop[:xf] = loop[:xf] * np.sin(ramp) + d[ei:ei + xf] * np.cos(ramp)
    loop = high_shelf(loop, sr, shelf_db)
    loop *= PEAK / np.abs(loop).max()
    wavfile.write(os.path.join(OUT_DIR, name + '_Loop.wav'), sr, (loop * 32767).astype(np.int16))

    seam = np.abs(loop[0] - (d[ei] * PEAK / np.abs(d[si:ei]).max())).max()
    print(f'{name}: beat={beat:.4f}s ({60 / beat:.1f} BPM) start={s:.2f}s bars={k} '
          f'len={e - s:.2f}s sim={score - 0.0005 * k:.3f} seam_step={seam:.4f}')


if __name__ == '__main__':
    for name, args in TRACKS.items():
        cut(name, *args)
