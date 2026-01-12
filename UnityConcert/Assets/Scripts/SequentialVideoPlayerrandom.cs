// SequentialVideoPlayerrandom.cs — PUNCH_CUT 전용
using System;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

public class SequentialVideoPlayerrandom : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public BeatHapticRandomWindows haptics;

    [Header("CSV (PUNCH)")]
    public TextAsset punchCsv;   // Inspector에서 PUNCH CSV 드래그

    [Header("Playlist (StreamingAssets 기준 파일명)")]
    public string[] videoFileNames = { "resting.mp4", "PUNCH_CUT.mp4", "resting.mp4" };

    private int currentIndex = 0;

    [Header("Delays")]
    [SerializeField] private float nextDelay = 0.5f;
    [SerializeField] private float quitDelay = 0.25f;

    [Header("Haptic Config")]
    public float oneBarSec = 1.8f;        // 한 마디 길이(초)
    public float targetDurationSec = 73.63f; // 코러스 총 길이(초) — 필요시 조절
    public int regionSeed = 0;             // 시드(0이면 매번 랜덤, 고정하면 재현 가능)

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
            Debug.Log("✅ 모든 영상 재생 완료");
            Invoke(nameof(QuitApp), quitDelay);
            return;
        }

        string filename = videoFileNames[currentIndex];
        string path = Path.Combine(Application.streamingAssetsPath, filename);

        videoPlayer.prepareCompleted -= OnVideoPrepared;
        videoPlayer.url = path;
        videoPlayer.Prepare();
        videoPlayer.prepareCompleted += OnVideoPrepared;

        Debug.Log($"▶ 준비 중: {filename} | url={path}");
    }

    void OnVideoPrepared(VideoPlayer vp)
    {
        videoPlayer.prepareCompleted -= OnVideoPrepared;
        videoPlayer.Play();

        string just = Path.GetFileName(videoPlayer.url);
        bool isPunch = just.Equals("PUNCH_CUT.mp4", StringComparison.OrdinalIgnoreCase);

        if (haptics)
        {
            if (haptics.videoPlayer != videoPlayer)
                haptics.videoPlayer = videoPlayer;

            if (isPunch)
            {
                if (punchCsv) haptics.SetCSV(punchCsv);
                haptics.Rebuild(oneBarSec, targetDurationSec, regionSeed);
                haptics.enabled = true;
                Debug.Log("✅ Haptics ENABLED (PUNCH)");
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
        Debug.LogWarning($"[SVP] Video error: {msg} → 다음으로 진행");
        currentIndex++;
        if (currentIndex < videoFileNames.Length)
            Invoke(nameof(PlayCurrentVideo), nextDelay);
        else
            Invoke(nameof(QuitApp), quitDelay);
    }

    private void QuitApp() => Application.Quit();
}
