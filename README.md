# VRConcert-Haptics (Unity 6)

Unity project for synchronizing bHaptics vibrotactile feedback with VR concert video playback using beat timing data.

## Overview
This repository contains a Unity project (`UnityConcert/`) that plays VR concert videos and triggers vibrotactile feedback via **bHaptics SDK2**. Haptic patterns are synchronized to musical beat timestamps and used to implement experimental conditions such as **sync/chorus/base/random** mappings.

## Technology Stack
- **Unity:** 6000.1.6f1 (downloaded 2025-07-30)
- **Haptics:** bHaptics SDK2 (`Bhaptics.SDK2`)
- **VR/XR:** XR / XRI (Unity XR stack)

## Repository Structure
- `UnityConcert/Assets/Scenes/` – Unity scenes
- `UnityConcert/Assets/Scripts/` – Core scripts
  - `BeatHapticSync.cs`
  - `BeatHapticRandomWindows.cs`
  - `SequentialVideoPlayer.cs`
  - `SequentialVideoPlayerrandom.cs`
- `UnityConcert/Assets/StreamingAssets/` – (Empty in repo) expected location for video files
- `UnityConcert/Assets/beat.csv` – Beat timing data (TextAsset)

## Important Note (Video files not included)
VR video files (e.g., `LOVEDIVE.mp4`, `resting.mp4`) are **not included** in this repository.
Place your local video files in:
`UnityConcert/Assets/StreamingAssets/`

## Getting Started
1. Clone the repo:
   - `git clone https://github.com/jwlee6153/VRConcert-Haptics.git`
2. Open `UnityConcert/` in Unity **6000.1.6f1**
3. Install/verify bHaptics SDK2 in the project (if required by your local setup)
4. Add required video files to `Assets/StreamingAssets/`
5. Open the main scene from `Assets/Scenes/` and press Play

## Experimental Conditions (example)
- **Random:** vibrations in randomized windows
- **Chorus/Base:** beat-synchronized patterns (mapping differs by condition)

## License
(Choose a license or add a note here)
