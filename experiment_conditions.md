# Experimental Conditions for VR Concert Haptics

This document describes the experimental conditions implemented in this project.
The goal is to examine how different vibrotactile timing strategies influence
user experience during VR concert video viewing.

All conditions share identical audiovisual content. Only the **temporal mapping
of vibrotactile feedback** differs between conditions.

---

## Experimental Design Overview

- Design: within-subjects
- Factors: vibrotactile timing strategy
- Conditions: Baseline, Chorus-aligned, Random-window
- Dependent measures: subjective ratings 

Each participant experiences multiple songs across all conditions.
Song–condition order can be counterbalanced.

---

## Beat Timing Representation

Vibrotactile feedback is driven by beat timing data stored as CSV files.

### Beat CSV Format

```csv
time_sec
0.534
1.012
1.488
...
- Each row represents a beat onset in seconds.
- CSV files are imported into Unity as `TextAsset`s.
- Beat timestamps serve as triggers for vibrotactile pulses.

---

## Vibrotactile Conditions

### 1. Baseline (Continuous Beat-Synchronized)

#### Description
Vibrotactile pulses are delivered at every detected beat throughout the entire song.

#### Purpose
This condition serves as a reference baseline and reflects a conventional
signal-driven mapping strategy in VR music haptics.

#### Temporal Rule
- All beat timestamps in the CSV trigger vibrotactile feedback.
- No temporal gating or structural filtering is applied.

---

### 2. Chorus-Aligned (Structure-Aligned)

#### Description
Vibrotactile pulses are delivered only during chorus sections of the song.
Beats outside chorus segments do not trigger feedback.

#### Purpose
This condition emphasizes musically salient structural highlights by concentrating
vibrotactile feedback in climactic sections of the music.

#### Temporal Rule
- Beat timestamps are filtered to include only those within predefined
  chorus start–end intervals.
- Vibrotactile pulses remain beat-synchronous within chorus sections.

---

### 3. Random-Window (Non-Structural, Duration-Matched)

#### Description
Vibrotactile pulses are delivered within pseudo-randomly selected time windows
that are not aligned with musical structure.

#### Purpose
This condition controls for total stimulation amount while removing meaningful
alignment with musical form, introducing temporal unpredictability.

#### Temporal Rule
- Random time windows are generated across the song duration.
- Total window duration is matched to the Chorus-aligned condition.
- Vibrotactile pulses are triggered only for beats that fall inside these windows.

---

## Condition Summary

| Condition        | Beat-Synchronous | Structure-Aware | Temporal Predictability |
|------------------|------------------|------------------|-------------------------|
| Baseline         | Yes              | No               | High                    |
| Chorus-aligned   | Yes              | Yes              | Medium                  |
| Random-window    | Yes              | No               | Low                     |

---

## Reproducibility Notes

- Beat CSV files used for each condition should be stored and versioned.
- If random windows are generated programmatically, their parameters or outputs
  should be logged or saved.
- Large media files (e.g., video, audio) are excluded from version control.
