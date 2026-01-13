// Generic video–haptics playback script.
// "LOVEDIVE.mp4" is used as an example.
// Replace the video file name and CSV to use a different song.
using System;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

public class SequentialVideoPlayer : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public BeatHapticSync beatHapticSync;

    [Header("CSV")]
    public TextAsset csvFile; // Beat timestamps (seconds), header allowed

    [Header("Playlist")]
    [Tooltip("Video files to play (relative to StreamingAssets)")]
    private string[] videoFileNames = { "resting.mp4", "LOVEDIVE.mp4", "resting.mp4" };

    private int currentIndex = 0;

    [Header("Delays")]
    [SerializeField] private float nextDelay = 0.5f;  // Small buffer before playing the next video
    [SerializeField] private float quitDelay = 0.25f; // Small buffer before quitting

    void Start()
    {
        if (!videoPlayer)
        {
            Debug.LogError("[SVP] VideoPlayer is null.");
            return;
        }

        // Ensure BeatHapticSync references the same VideoPlayer
        if (beatHapticSync && beatHapticSync.videoPlayer != videoPlayer)
            beatHapticSync.videoPlayer = videoPlayer;

        videoPlayer.isLooping = false;
        videoPlayer.loopPointReached += OnVideoFinished;
        videoPlayer.errorReceived += OnVideoError;

        PlayCurrentVideo();
    }

    void PlayCurrentVideo()
    {
        if (currentIndex >= videoFileNames.Length)
        {
            Debug.Log(" All videos finished playing.");
            Invoke(nameof(QuitApp), quitDelay);
            return;
        }

        string filename = videoFileNames[currentIndex];
        string path = Path.Combine(Application.streamingAssetsPath, filename);

        // Prevent duplicate subscription
        videoPlayer.prepareCompleted -= OnVideoPrepared;

        videoPlayer.url = path;
        videoPlayer.isLooping = false;
        videoPlayer.Prepare();
        videoPlayer.prepareCompleted += OnVideoPrepared;

        Debug.Log($"▶ Preparing: {filename} | url={path}");
    }

    void OnVideoPrepared(VideoPlayer vp)
    {
        videoPlayer.prepareCompleted -= OnVideoPrepared;
        videoPlayer.Play();

        string full = videoPlayer.url ?? "";
        string just = Path.GetFileName(full);
        bool isLovedive = just.Equals("LOVEDIVE.mp4", StringComparison.OrdinalIgnoreCase);

        Debug.Log($"▶ Playback started: full='{full}', file='{just}', isLovedive={isLovedive}");

        if (beatHapticSync)
        {
            // Ensure the same VideoPlayer reference
            if (beatHapticSync.videoPlayer != videoPlayer)
                beatHapticSync.videoPlayer = videoPlayer;

            if (isLovedive)
            {
                if (csvFile) beatHapticSync.SetCSV(csvFile); // Ensure LOVEDIVE CSV is used
                beatHapticSync.enabled = true;
                beatHapticSync.ResetSync();
                Debug.Log(" BeatHapticSync ENABLED (LOVEDIVE)");
            }
            else
            {
                beatHapticSync.enabled = false;
                Debug.Log("⏸ BeatHapticSync DISABLED (RESTING)");
            }
        }
    }

    void OnVideoFinished(VideoPlayer vp)
    {
        currentIndex++;
        if (currentIndex < videoFileNames.Length)
        {
            Invoke(nameof(PlayCurrentVideo), nextDelay);
        }
        else
        {
            Debug.Log(" All videos finished playing.");
            Invoke(nameof(QuitApp), quitDelay);
        }
    }

    void OnVideoError(VideoPlayer vp, string msg)
    {
        Debug.LogWarning($"[SVP] Video error: {msg} → Moving to the next video.");
        currentIndex++;
        if (currentIndex < videoFileNames.Length)
        {
            Invoke(nameof(PlayCurrentVideo), nextDelay);
        }
        else
        {
            Invoke(nameof(QuitApp), quitDelay);
        }
    }

    private void QuitApp()
    {
        Application.Quit();
    }

    void OnDestroy()
    {
        if (!videoPlayer) return;
        videoPlayer.loopPointReached -= OnVideoFinished;
        videoPlayer.errorReceived -= OnVideoError;
        videoPlayer.prepareCompleted -= OnVideoPrepared;
    }
}
