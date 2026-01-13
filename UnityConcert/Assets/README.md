# Assets Overview and Usage Guide

This directory contains all Unity-side assets used to run the VR concert
experiment, including scenes, scripts, and beat timing data.

This document explains how experimental conditions are configured inside Unity,
focusing on the relationship between **CSV files** and **scripts** for each
condition.

---

## Assets Directory Structure

- `Scenes/`  
  Unity scenes used to run the experiment.

- `Scripts/`  
  C# scripts controlling video playback and vibrotactile timing logic.

- `StreamingAssets/`  
  Local directory for VR concert video files (not included in the repository).

- `*.csv`  
  Beat timing CSV files corresponding to different experimental conditions.

---

## Beat Timing CSV Files

- Each experimental condition uses its own CSV file.
- CSV files contain beat timestamps in seconds.
- CSV files are imported into Unity as `TextAsset`s and assigned via the Inspector.

Example format:

```csv
time_sec
0.534
1.012
1.488
...

## Experimental Conditions and Script Mapping

Each experimental condition is defined by a combination of:

- a condition-specific CSV file  
- a video playback script  
- a vibrotactile control script  

Only these elements change across conditions.  
The VR video content itself remains identical.

---

## Chorus-Aligned Condition

### Concept

Vibrotactile feedback is delivered only during chorus sections of the song.

### CSV

- Chorus-specific beat CSV  
  (contains only beats within chorus intervals)

### Scripts Used

- `SequentialVideoPlayer.cs`  
  Handles standard VR video playback.

- `BeatHapticSync.cs`  
  Triggers vibrotactile feedback synchronously at beat timestamps.
 

---

## Random-Window Condition

### Concept

Vibrotactile feedback is delivered within pseudo-randomly selected beat windows
that are not aligned with musical structure.

### CSV

- Random-condition beat CSV  
  (derived from the full beat CSV, with beats around chorus onsets removed)

This CSV contains all valid beat positions except those within predefined
exclusion windows around chorus onsets. No randomness is encoded in the CSV
itself.

### Scripts Used

- `SequentialVideoPlayerrandom.cs`  
  Handles VR video playback for the random condition.

- `BeatHapticRandomWindows.cs`  
  Randomly selects beat windows at runtime and triggers vibrotactile feedback
  based on the provided beat CSV.

---

## Baseline Condition 

### Concept

Vibrotactile feedback is delivered continuously throughout the song.

### CSV

- Full beat CSV  
  (contains all detected beats)

### Scripts Used

- `SequentialVideoPlayer.cs`  
- `BeatHapticSync.cs`
 

---

## Running a Condition in Unity

1. Open a scene from `Assets/Scenes/`.
2. Assign the VR video file to the `VideoPlayer` component.
3. Place the video file in:
4. Assign the condition-specific CSV file as a `TextAsset`.
5. Attach scripts according to the desired condition:
6. Enter Play Mode to run the experiment.

---

## Notes

- VR video and audio files are excluded from version control.
- CSV and script combinations define experimental conditions.
- Scripts are modular to allow easy switching between conditions.
