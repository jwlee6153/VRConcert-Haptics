// Generic video–haptics playback script.
// "LOVEDIVE.mp4" is used as an example.
// Replace the video file name and CSV to use a different song.
using System;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

public class SequentialVideoPlayerrandom : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public BeatHapticRandomWindows haptics;

    [Header("CSV")]
    public TextAsset csvFile;   // Beat timestamps (seconds), header allowed

    [Header("Playlist (StreamingAssets file names)")]
    public string[] videoFileNames = { "resting.mp4", "LOVEDIVE.mp4", "resting.mp4" };

    private int currentIndex = 0;

    [Header("Delays")]
    [SerializeField] private float nextDelay = 0.5f;
    [SerializeField] private float quitDelay = 0.25f;

    [Header("Haptic Config")]
    public float oneBarSec = 1.8f;            // One bar length (seconds)
    public float targetDurationSec = 73.63f;  // Target section duration (seconds) — adjust if needed
    public int regionSeed = 0;                // Seed (0 = random each run; set a fixed value for reproducibility)

    void Start()
    {
        if (!videoPlayer)
        {
            Debug.LogError("[SVP] VideoPlayer is null.");
            return;
        }
        if (haptics && haptics.videoPlayer != videoPlayer)
            haptics.videoPlayer = videoPlayer;

        videoPlayer.isLooping = false;
        videoPlayer.loopPointReached += OnVideoFinished;
        videoPlayer.errorReceived += OnVideoError;

        PlayCurrentVideo();
    }

    void PlayCurrentVideo()
    {
        if (currentIndex >= videoFileNames.Length)
        {
            Debug.Log("All videos finished playing.");
            Invoke(nameof(QuitApp), quitDelay);
            return;
        }

        string filename = videoFileNames[currentIndex];
        string path = Path.Combine(Application.streamingAssetsPath, filename);

        videoPlayer.prepareCompleted -= OnVideoPrepared;
        videoPlayer.url = path;
        videoPlayer.Prepare();
        videoPlayer.prepareCompleted += OnVideoPrepared;

        Debug.Log($"▶ Preparing: {filename} | url={path}");
    }

    void OnVideoPrepared(VideoPlayer vp)
    {
        videoPlayer.prepareCompleted -= OnVideoPrepared;
        videoPlayer.Play();

        string just = Path.GetFileName(videoPlayer.url);
        bool isLovedive = just.Equals("LOVEDIVE.mp4", StringComparison.OrdinalIgnoreCase);

        if (haptics)
        {
            if (haptics.videoPlayer != videoPlayer)
                haptics.videoPlayer = videoPlayer;

            if (isLovedive)
            {
                if (csvFile) haptics.SetCSV(csvFile);
                haptics.Rebuild(oneBarSec, targetDurationSec, regionSeed);
                haptics.enabled = true;
                Debug.Log("Haptics ENABLED (LOVEDIVE)");
            }
            else
            {
                haptics.enabled = false;
                Debug.Log("⏸ Haptics DISABLED (RESTING)");
            }
        }
    }

    void OnVideoFinished(VideoPlayer vp)
    {
        currentIndex++;
        if (currentIndex < videoFileNames.Length)
            Invoke(nameof(PlayCurrentVideo), nextDelay);
        else
            Invoke(nameof(QuitApp), quitDelay);
    }

    void OnVideoError(VideoPlayer vp, string msg)
    {
        Debug.LogWarning($"[SVP] Video error: {msg} → Moving to the next video.");
        currentIndex++;
        if (currentIndex < videoFileNames.Length)
            Invoke(nameof(PlayCurrentVideo), nextDelay);
        else
            Invoke(nameof(QuitApp), quitDelay);
    }

    private void QuitApp() => Application.Quit();
}
