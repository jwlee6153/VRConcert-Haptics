using System;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

public class SequentialVideoPlayer : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public BeatHapticSync beatHapticSync;

    [Header("CSV (DUM)")]
    public TextAsset dumCsv; // 인스펙터에 DUM CSV 지정

    [Header("Playlist")]
    [Tooltip("재생할 비디오 파일들 (StreamingAssets 기준)")]
    private string[] videoFileNames = { "resting.mp4", "DUM_CUT.mp4", "resting.mp4" };

    private int currentIndex = 0;

    [Header("Delays")]
    [SerializeField] private float nextDelay = 0.5f;  // 다음 영상 재생 전 여유
    [SerializeField] private float quitDelay = 0.25f; // 종료 전 짧은 여유

    void Start()
    {
        if (!videoPlayer)
        {
            Debug.LogError("[SVP] VideoPlayer is null.");
            return;
        }

        // BeatHapticSync가 같은 VideoPlayer를 보도록 보장
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
            Debug.Log("✅ 모든 영상 재생 완료");
            Invoke(nameof(QuitApp), quitDelay);
            return;
        }

        string filename = videoFileNames[currentIndex];
        string path = Path.Combine(Application.streamingAssetsPath, filename);

        // 중복 등록 방지
        videoPlayer.prepareCompleted -= OnVideoPrepared;

        videoPlayer.url = path;
        videoPlayer.isLooping = false;
        videoPlayer.Prepare();
        videoPlayer.prepareCompleted += OnVideoPrepared;

        Debug.Log($"▶ 준비 중: {filename} | url={path}");
    }

    void OnVideoPrepared(VideoPlayer vp)
    {
        videoPlayer.prepareCompleted -= OnVideoPrepared;
        videoPlayer.Play();

        string full = videoPlayer.url ?? "";
        string just = Path.GetFileName(full);
        bool isDum = just.Equals("DUM_CUT.mp4", StringComparison.OrdinalIgnoreCase);

        Debug.Log($"▶ 재생 시작: full='{full}', file='{just}', isDum={isDum}");

        if (beatHapticSync)
        {
            // 항상 같은 VP 참조 보장
            if (beatHapticSync.videoPlayer != videoPlayer)
                beatHapticSync.videoPlayer = videoPlayer;

            if (isDum)
            {
                if (dumCsv) beatHapticSync.SetCSV(dumCsv); // ★ DUM CSV 보장
                beatHapticSync.enabled = true;
                beatHapticSync.ResetSync();
                Debug.Log("✅ BeatHapticSync ENABLED (DUM)");
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
            Debug.Log("✅ 모든 영상 재생 완료");
            Invoke(nameof(QuitApp), quitDelay);
        }
    }

    void OnVideoError(VideoPlayer vp, string msg)
    {
        Debug.LogWarning($"[SVP] Video error: {msg} → 다음으로 진행");
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
