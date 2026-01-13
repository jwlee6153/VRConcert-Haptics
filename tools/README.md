# Beat Extraction and Low-Frequency RMS Filtering from VR Concert Videos

This repository provides a reproducible pipeline for extracting beat timestamps
from VR concert videos and filtering them based on low-frequency RMS energy.
The pipeline is designed for music and VR research contexts, where beat-aligned
events (e.g., vibrotactile feedback) should be restricted to perceptually salient
musical moments.

---

## Overview

The overall workflow consists of three main steps:

1. **Audio extraction from VR video (`.mp4 → .wav`)**
2. **Beat extraction from the audio (`.wav → beats.csv`)**
3. **Filtering beats using low-frequency RMS energy**

---

## 1. Audio Extraction from VR Video

The original stimulus is a VR concert video in MP4 format.
Audio is extracted and converted into a mono WAV file with a 48 kHz sampling rate
using `ffmpeg`.

```bash
ffmpeg -i targetsong_CUT.mp4 -map a:0 -vn -ac 1 -ar 48000 -c:a pcm_s16le targetsong.wav
```

### Command details

- `-map a:0` : select the first audio stream
- `-vn` : disable video output
- `-ac 1` : convert audio to mono
- `-ar 48000` : resample audio to 48 kHz
- `pcm_s16le` : uncompressed 16-bit PCM WAV format

---

## 2. Beat Extraction

Beat timestamps are extracted from the WAV audio using a Python-based beat
detection script (e.g., based on `librosa`).

The output of this step is a CSV file containing beat timestamps in seconds:

```csv
time_sec
0.534
1.012
1.488
...
```

This CSV file serves as the input to the RMS-based beat filtering script provided
in this repository.

**Note**  
The specific beat extraction method can be adapted depending on the musical
material (e.g., pop, EDM, K-pop). The filtering step described below is
independent of the beat detection algorithm.

---

## 3. Beat Filtering by Low-Frequency RMS

The script `filter_beats_by_rms.py` filters beat timestamps based on
low-frequency (≤ 200 Hz) RMS energy.

### Method Summary

- The audio signal is low-pass filtered (default cutoff: 200 Hz).
- A moving RMS is computed across the entire track.
- A global RMS threshold is defined using a quantile of the RMS distribution.
- Beats whose local RMS falls below the threshold are discarded.
- A minimum temporal separation between retained beats is enforced.

This procedure retains beats that are more likely to correspond to strong bass
or drum-driven musical events.

---

## Usage

### Install dependencies

```bash
pip install -r requirements.txt
```

### Run the filtering script

```bash
python filter_beats_by_rms.py \
  --audio targetsong.wav \
  --beats_csv beats.csv \
  --out_csv beats_filtered.csv \
  --sr 48000 \
  --lp_hz 200 \
  --win 0.10 \
  --hop_sec 0.02 \
  --q 0.20 \
  --min_sep 0.10
```

### Arguments

- `--audio` : input audio file (wav/mp3/m4a)
- `--beats_csv` : CSV file containing beat timestamps (seconds)
- `--out_csv` : output CSV file with filtered beat timestamps
- `--sr` : target sampling rate for analysis (default: 48000)
- `--lp_hz` : low-pass cutoff frequency in Hz (default: 200)
- `--win` : RMS window length in seconds (default: 0.10)
- `--hop_sec` : hop size for RMS computation in seconds (default: 0.02)
- `--q` : quantile threshold for RMS filtering (default: 0.20)
- `--min_sep` : minimum separation between retained beats in seconds (default: 0.10)

---

## Output

The output is a CSV file containing filtered beat timestamps:

```csv
time_sec
1.012
2.496
3.984
...
```

These timestamps can be directly used for beat-synchronous applications such as
vibrotactile feedback, event triggering in Unity, or VR music experiments.

---

## Dependencies

- numpy
- scipy
- librosa

---

## Notes

- Audio, video, and large CSV files are intentionally excluded from version control.
- This pipeline is suitable for research applications involving rhythmic salience,
  bass-driven perception, or multimodal synchronization in immersive music environments.
