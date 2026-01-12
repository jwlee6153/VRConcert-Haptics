using System.Collections.Generic;  
using System.Globalization;
using UnityEngine;
using UnityEngine.Video;
using Bhaptics.SDK2;

public class BeatHapticSync : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public TextAsset csvFile;

    [SerializeField] private float offsetSeconds = 0f;
    [SerializeField] private float syncThreshold = 0.04f;
    [SerializeField] private float minGap = 0.12f;

    private readonly List<float> beatTimes = new();
    private int currentIndex = 0;
    private float lastTrig = -999f;

    void OnEnable()
    {
        Debug.Log($"[BeatHapticSync] ENABLED vp={(videoPlayer ? videoPlayer.url : "null")} beats={beatTimes.Count}");
    }

    void Start()
    {
        LoadCSV();       // CSV는 '초' 단위로 그대로 로드
        enabled = false; // 기본 비활성
    }

    void Update()
    {
        if (!videoPlayer || !videoPlayer.isPlaying || currentIndex >= beatTimes.Count) return;

        float now = (float)videoPlayer.time + offsetSeconds;

        // 이미 지나간 비트는 건너뛰기 (프레임 스킵/초반 튐 방지)
        while (currentIndex < beatTimes.Count && beatTimes[currentIndex] < now - syncThreshold)
            currentIndex++;

        if (currentIndex >= beatTimes.Count) return;

        float beat = beatTimes[currentIndex];

        // 1초에 한두 번 현재-다음비트 차이 찍기 (원인 파악)
        if ((Time.frameCount % 20) == 0)
            Debug.Log($"[BeatHapticSync] now={now:F3} next={beat:F3} Δ={now - beat:F3} idx={currentIndex}/{beatTimes.Count}");

        if (Mathf.Abs(now - beat) <= syncThreshold)
        {
            // 최소 간격(minGap) 체크 (원한다면 주석 해제)
            // if (now - lastTrig >= minGap)
            // {
            //     TriggerHaptic();
            //     lastTrig = now;
            //     currentIndex++;
            // }

            TriggerHaptic();
            lastTrig = now;
            currentIndex++;
        }
    }

    void LoadCSV()
    {
        beatTimes.Clear();

        if (csvFile == null)
        {
            Debug.LogWarning("[BeatHapticSync] CSV is null.");
            return;
        }

        var lines = csvFile.text.Split('\n');
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // 첫 컬럼만 사용 (쉼표/세미콜론/탭 모두 대응)
            var parts = line.Split(new[] { ',', ';', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            // 헤더 회피: 숫자가 아니면 스킵
            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float t))
                continue;

            // ✅ CSV는 '초' 단위라고 가정 — 추가 변환 없음
            beatTimes.Add(t);
        }

        beatTimes.Sort();
        currentIndex = 0;

        // 디버그: 어느 CSV를 불렀고 앞부분 값은 어떤지
        int n = Mathf.Min(5, beatTimes.Count);
        string preview = n > 0 ? string.Join(", ", beatTimes.GetRange(0, n).ConvertAll(v => v.ToString("F3"))) : "(none)";
        Debug.Log($"✅ 비트 시점 {beatTimes.Count}개 불러옴 (csv='{csvFile?.name}', unit=seconds); first=[{preview}]");
    }

    public void SetCSV(TextAsset newCsv)
    {
        csvFile = newCsv;
        LoadCSV();
        currentIndex = 0;
    }

    void TriggerHaptic()
    {
        int[] motors = new int[32] {
            0,0,0,0,0,100,100,0, 0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,  0,0,0,0,0,0,0,0
        };
        BhapticsLibrary.PlayMotors((int)PositionType.Vest, motors, 100);
        Debug.Log($"💥 진동 at {videoPlayer.time:F2}s (offset={offsetSeconds*1000f:F0}ms)");
    }

    public void ResetSync()
    {
        currentIndex = 0;
        Debug.Log("🔄 BeatHapticSync 인덱스 리셋");
    }
}
