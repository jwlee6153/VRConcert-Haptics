import argparse, csv
import numpy as np
import librosa
from scipy.signal import butter, filtfilt

def butter_lowpass(cut_hz, sr, order=4):
    ny = 0.5 * sr
    wn = min(cut_hz / ny, 0.999)
    b, a = butter(order, wn, btype="low")
    return b, a

def lowpass(y, sr, cut_hz=200.0, order=4):
    if cut_hz is None or cut_hz >= sr / 2:
        return y
    b, a = butter_lowpass(cut_hz, sr, order)
    return filtfilt(b, a, y)

def frame_rms(x):
    return np.sqrt(np.mean(x**2) + 1e-12)

def moving_rms_series(y, sr, win_sec=0.10, hop_sec=0.02):
    """Scan the whole track with hop_sec and compute low-frequency RMS at each center time."""
    W = max(1, int(win_sec * sr))
    H = max(1, int(hop_sec * sr))
    half = W // 2
    vals, centers = [], []
    for c in range(half, len(y) - half, H):
        vals.append(frame_rms(y[c - half : c + half]))
        centers.append(c / sr)
    return np.array(centers, dtype=float), np.array(vals, dtype=float)

def enforce_min_sep(times, min_sep):
    if len(times) <= 1 or min_sep <= 0:
        return times
    kept = [times[0]]
    for t in times[1:]:
        if t - kept[-1] >= min_sep:
            kept.append(t)
    return np.array(kept, dtype=float)

def read_beats(csv_path):
    times = []
    with open(csv_path, "r", encoding="utf-8") as f:
        r = csv.reader(f)
        header = next(r, None)  # optional
        for row in r:
            if not row:
                continue
            times.append(float(row[0]))
    return np.array(times, dtype=float)

def write_times(csv_path, times):
    with open(csv_path, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["time_sec"])
        for t in times:
            w.writerow([f"{t:.6f}"])

def main():
    ap = argparse.ArgumentParser(description="Filter beat timestamps by low-frequency RMS (global quantile).")
    ap.add_argument("--audio", required=True, help="Input audio (wav/mp3/m4a...)")
    ap.add_argument("--beats_csv", required=True, help="Beat CSV (first column: time_sec)")
    ap.add_argument("--out_csv", required=True, help="Output CSV path")

    ap.add_argument("--sr", type=int, default=48000, help="Target sampling rate for analysis")
    ap.add_argument("--lp_hz", type=float, default=200.0, help="Low-pass cutoff (Hz)")
    ap.add_argument("--win", type=float, default=0.10, help="RMS window length (sec)")
    ap.add_argument("--hop_sec", type=float, default=0.02, help="RMS scan hop (sec)")

    # Core parameter: keep only beats whose RMS is above the q-quantile of the global RMS distribution
    ap.add_argument("--q", type=float, default=0.20, help="Keep beats whose RMS >= quantile q (0~1).")
    ap.add_argument("--min_sep", type=float, default=0.10, help="Minimum separation between kept beats (sec)")
    args = ap.parse_args()

    # 1) load audio and apply low-pass filter
    y, sr = librosa.load(args.audio, sr=args.sr, mono=True)
    y_lp = lowpass(y, sr, args.lp_hz, order=4)

    # 2) read beat timestamps
    beat_times = read_beats(args.beats_csv)

    # 3) compute global RMS series and threshold
    centers, rms_seq = moving_rms_series(y_lp, sr, win_sec=args.win, hop_sec=args.hop_sec)
    thr = float(np.quantile(rms_seq, args.q))

    keep_times = []
    for t in beat_times:
        idx = int(np.argmin(np.abs(centers - t)))
        if rms_seq[idx] >= thr:
            keep_times.append(t)

    keep_times = enforce_min_sep(np.array(keep_times, dtype=float), args.min_sep)
    write_times(args.out_csv, keep_times)

    print(f"[OK] kept {len(keep_times)} / {len(beat_times)} beats → {args.out_csv}")
    print(f"lp≤{args.lp_hz}Hz  q={args.q} thr≈{thr:.6e}  win={args.win}s hop={args.hop_sec}s  min_sep={args.min_sep}s")

if __name__ == "__main__":
    main()
