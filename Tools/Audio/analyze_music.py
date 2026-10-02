"""Prints duration, band balance, strongest pitch classes and a 5s loudness envelope for music wavs."""
import sys, warnings
import numpy as np
from scipy.io import wavfile

NAMES = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B']
BANDS = [(0, 120), (120, 500), (500, 2000), (2000, 6000), (6000, 22050)]


def load(path):
    with warnings.catch_warnings():
        warnings.simplefilter('ignore')
        sr, d = wavfile.read(path)
    scale = {np.dtype('int16'): 32768.0, np.dtype('int32'): 2147483648.0}.get(d.dtype, 1.0)
    return sr, d.astype(np.float64) / scale


if __name__ == '__main__':
    for path in sys.argv[1:]:
        sr, d = load(path)
        m = d.mean(axis=1) if d.ndim > 1 else d
        S = np.abs(np.fft.rfft(m)) ** 2
        fr = np.fft.rfftfreq(len(m), 1 / sr)
        bp = [S[(fr >= a) & (fr < b)].sum() / S.sum() for a, b in BANDS]
        sel = (fr > 60) & (fr < 2000)
        ch = np.zeros(12)
        np.add.at(ch, np.round(12 * np.log2(fr[sel] / 440.0) + 9).astype(int) % 12, S[sel])
        top = [NAMES[i] for i in np.argsort(ch)[::-1][:5]]
        n = sr * 5
        env = [20 * np.log10(np.sqrt((m[i:i + n] ** 2).mean()) + 1e-9) for i in range(0, len(m) - n + 1, n)]
        print(f"{path}\n  {len(m) / sr:.2f}s peak={abs(d).max():.2f} bands={' '.join(f'{x:.0%}' for x in bp)} chroma={top}")
        print(f"  env5s={' '.join(f'{e:.0f}' for e in env)}")
