# VRConcert-Haptics

Unity project for synchronizing bHaptics vibrotactile feedback with VR concert
video playback using beat timing data.

---

## Overview

This repository contains a Unity project (`UnityConcert/`) for conducting
VR concert experiments with vibrotactile augmentation. The system plays
pre-recorded VR concert videos and delivers chest-centered vibrotactile
feedback via **bHaptics SDK2**, synchronized to musical beat timing data.

The project is designed to support **experimental comparisons of different
vibrotactile timing strategies**, including structure-aware and non-structural
mappings.

---

## Experimental Overview


### Motivation

VR concerts provide immersive audiovisual experiences but often lack the bodily
sensations characteristic of live music performances. In live concerts,
low-frequency vibrations driven by bass and rhythmic structure are strongly felt
in the body, particularly in the chest.

This project investigates whether adding vibrotactile feedback to VR concerts—
and how that feedback is temporally aligned with music—can shape the overall
music experience.

---

### Experimental Design

- **Design:** Within-subjects
- **Manipulated factor:** Vibrotactile timing strategy
- **Conditions:** Baseline, Chorus-aligned, Random-window
- **Stimuli:** Pre-recorded VR concert videos (3–4 minutes)

All conditions use identical audiovisual content. Only the **timing of
vibrotactile feedback relative to musical structure** differs between conditions.

---

### Vibrotactile Signal Pipeline

1. Audio is extracted from VR concert videos.
2. Musical beat timestamps are obtained and stored as CSV files.
3. Beat timestamps are imported into Unity as `TextAsset`s.
4. Vibrotactile pulses are triggered at beat onsets.
5. Condition-specific logic determines when beat-triggered feedback is delivered.

This design allows the same beat timing data to be reused across conditions while
varying only the temporal delivery strategy.

---

### Vibrotactile Conditions

#### Baseline (Continuous)

- Vibrotactile pulses are delivered at every beat.
- Feedback is present throughout the entire song.

#### Chorus-Aligned

- Vibrotactile pulses are delivered only during predefined chorus sections.
- Beats outside chorus intervals do not trigger feedback.

#### Random-Window

- Vibrotactile pulses are delivered within pseudo-random time windows.
- Total vibration duration is matched to the Chorus-aligned condition.
- Windows are not aligned with musical structure.

---

### Experimental Flow (Per Trial)

1. Short resting phase
2. VR concert video playback with one vibrotactile condition
3. Brief subjective evaluation of the music experience
4. Short break before the next trial

Participants experience multiple songs across all conditions, with
song–condition order counterbalanced.

---

## Technology Stack

- **Unity:** 6000.1.6f1 (downloaded 2025-07-30)
- **Haptics:** bHaptics SDK2 (`Bhaptics.SDK2`)
- **VR/XR:** Unity XR / XRI stack

---

## Repository Structure

- `UnityConcert/` – Unity project root
  - `Assets/Scenes/` – Unity scenes
  - `Assets/Scripts/` – Core scripts
    - `BeatHapticSync.cs`
    - `BeatHapticRandomWindows.cs`
    - `SequentialVideoPlayer.cs`
    - `SequentialVideoPlayerrandom.cs`
  - `Assets/StreamingAssets/` – Local VR video files (not versioned)
  - `Assets/beat.csv` – Beat timing data (`TextAsset`)
- `tools/` – Audio and beat extraction utilities
- `experiment_conditions.md` – Detailed description of vibrotactile conditions

---

## Important Note (Video Files Not Included)

VR video files (e.g., `LOVEDIVE.mp4`, `resting.mp4`) are **not included**
in this repository.

Place your local video files in: `UnityConcert/Assets/StreamingAssets/`

---

## Unity Scenes and Branch Organization

Unity scenes corresponding to different experimental conditions are organized
across separate branches.

- Each branch contains scene configurations specific to a given condition
  (e.g., Baseline, Chorus-aligned, Random-window).
- This separation allows condition-specific setups (scene objects, script
  attachments, Inspector settings) to be managed independently.
- To inspect or run a particular condition, switch to the corresponding branch
  and open the scene located in `Assets/Scenes/`.

This branch-based organization supports clear separation of experimental
conditions while sharing a common codebase.