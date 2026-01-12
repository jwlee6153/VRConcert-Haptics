// BeatHapticRandomWindows.cs
// - no maxWindows
// - SetCSV / Rebuild 제공
// - maxIterations 가드
// - minWindows 보장
// - 로그: 실행마다 타임스탬프 파일명 + (옵션) 종료 시 /Android/media 로 자동 백업

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Video;
using Bhaptics.SDK2;

public class BeatHapticRandomWindows : MonoBehaviour
{
    [Header("Video / CSV")]
    public VideoPlayer videoPlayer;
    public TextAsset csvFile; // 비트 타임스탬프(초) CSV (헤더 가능)

    [Header("Timing")]
    public float offsetSeconds = 0f;      // 오프셋 (초 단위)
    public float syncThreshold = 0.04f;   // 동기화 허용 오차 (초)

    [Header("Bar / Gaps")]
    public float oneBarSec = 1.8f;        // 1마디 길이 (초)
    public float totalDurationSec = 0f;   // 전체 길이 추정 (0이면 자동)

    [Header("Random Windows")]
    public float targetDurationSec = 73.63f; // 코러스 총 DURATION(초)
    public float minWindowSec = 0.5f;        // 개별 윈도우 최소 길이(초)
    public int regionSeed = 0;               // 시드 (0이면 매 실행 랜덤)
    [Tooltip("랜덤 윈도우 최소 개수(분할/추가로 보장)")]
    public int minWindows = 3;

    [Header("Haptics")]
    public float minGap = 0.12f;             // 트리거 최소 간격(초)

    [Header("Logging (timestamp)")]
    public bool enableLogging = true;
    [Tooltip("로그 파일 프리픽스 (예: 'haptic' → haptic_YYYYMMDD_HHMMSS.csv)")]
    public string logPrefix = "haptic";
    [Tooltip("앱 종료 시 /sdcard/Android/media/<pkg>/exports/logs 로 자동 백업")]
    public bool exportToMediaOnQuit = true;

    [Header("Generation Guardrail")]
    [Tooltip("랜덤 윈도우 생성 반복 상한")]
    public int maxIterations = 200;

    // 내부 상태
    private readonly List<float> beatTimes = new();
    private List<Vector2> allowedSegments = new();
    private List<Vector2> randomWindows = new();
    private List<float> playTimes = new();
    private int currentIndex = 0;
    private float lastTrig = -999f;

    // 로그
    private StreamWriter logWriter;
    private string logPath;
    private string logStamp; // yyyyMMdd_HHmmss

    // ===== Unity =====
    void Start()
    {
        LoadCSV();
        BuildAllowedSegments();
        BuildRandomWindowsAndPlayTimes();
        InitLog();
        enabled = false; // 외부(SequentialVideoPlayer 등)에서 켜줌
    }

    void Update()
    {
        if (!videoPlayer || !videoPlayer.isPlaying || playTimes.Count == 0) return;
        float now = (float)videoPlayer.time + offsetSeconds;

        // 이미 지난 비트 스킵
        while (currentIndex < playTimes.Count && playTimes[currentIndex] < now - syncThreshold)
            currentIndex++;
        if (currentIndex >= playTimes.Count) return;

        float next = playTimes[currentIndex];
        if (Mathf.Abs(now - next) <= syncThreshold && (now - lastTrig >= minGap))
        {
            TriggerHaptic();
            lastTrig = now;
            currentIndex++;
        }
    }

    void OnApplicationQuit()
    {
        CloseLog();
        if (exportToMediaOnQuit && !string.IsNullOrEmpty(logPath))
            TryExportLogToMedia(logPath);
    }

    void OnDestroy() => CloseLog();
    void OnApplicationPause(bool pause) { if (pause) CloseLog(); }

    // ===== Public API =====
    public void SetCSV(TextAsset newCsv)
    {
        csvFile = newCsv;
        LoadCSV();
        BuildAllowedSegments();
        BuildRandomWindowsAndPlayTimes();
        currentIndex = 0;
    }

    /// <summary>
    /// 외부에서 1마디/타겟/시드 바꿔 재구성
    /// </summary>
    public void Rebuild(float newOneBarSec, float newTargetDuration, int newSeed = -1)
    {
        oneBarSec = newOneBarSec;
        targetDurationSec = newTargetDuration;
        if (newSeed >= 0) regionSeed = newSeed;

        BuildAllowedSegments();
        BuildRandomWindowsAndPlayTimes();
        currentIndex = 0;
    }

    // ===== CSV Load =====
    void LoadCSV()
    {
        beatTimes.Clear();
        if (!csvFile) return;

        foreach (var raw in csvFile.text.Split('\n'))
        {
            var line = raw.Trim();
            if (string.IsNullOrEmpty(line)) continue;
            if (float.TryParse(line.Split(',', ';', '\t')[0],
                NumberStyles.Float, CultureInfo.InvariantCulture, out float t))
                beatTimes.Add(t);
        }
        beatTimes.Sort();
        Debug.Log($"✅ Beats loaded: {beatTimes.Count}");
    }

    // ===== 허용 구간 =====
    void BuildAllowedSegments()
    {
        float total = EstimateTotalDuration();
        allowedSegments = BuildAllowedByRemovingLongGaps(beatTimes, total, oneBarSec);
        Debug.Log($"[Allowed] segs={allowedSegments.Count}, dur={SumDuration(allowedSegments):F2}");
    }

    // ===== 랜덤 윈도우 & 플레이타임 =====
    void BuildRandomWindowsAndPlayTimes()
    {
        // target이 허용합보다 크면 클램프
        float allowedSum = SumDuration(allowedSegments);
        float target = Mathf.Min(targetDurationSec, Mathf.Max(0f, allowedSum));

        randomWindows = SampleRandomWindowsMatchingDuration_NoMax(
            allowedSegments, target, minWindowSec, regionSeed, maxIterations);

        // 최소 개수 보장: 분할/추가로 minWindows까지 맞추기
        randomWindows = EnforceMinWindows(
            randomWindows, allowedSegments, Mathf.Max(1, minWindows), minWindowSec, target, regionSeed
        );

        playTimes = FilterBeatsByWindows(beatTimes, randomWindows);
        playTimes.Sort();
        currentIndex = 0;

        Debug.Log($"[Windows] {randomWindows.Count} windows, {playTimes.Count} beats, winDur={SumDuration(randomWindows):F2}/{target:F2}s (allowed={allowedSum:F2}s)");
    }

    float EstimateTotalDuration()
    {
        float csvEnd = beatTimes.Count > 0 ? beatTimes[^1] : 0f;
        float vpDur = (videoPlayer && videoPlayer.length > 0) ? (float)videoPlayer.length : 0f;
        return totalDurationSec > 0f ? totalDurationSec : Mathf.Max(csvEnd + 5f, vpDur);
    }

    // ===== 로깅 =====
    void InitLog()
    {
        if (!enableLogging) return;

        string dir = Application.persistentDataPath; // 내부 (APK 삭제 시 함께 삭제)
        Directory.CreateDirectory(dir);

        logStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        // 실행마다 고유 파일명: haptic_YYYYMMDD_HHMMSS.csv
        logPath = Path.Combine(dir, $"{logPrefix}_{logStamp}.csv");

        logWriter = new StreamWriter(logPath, false, System.Text.Encoding.UTF8);
        logWriter.WriteLine("# Haptic Log");
        logWriter.WriteLine($"# app={Application.identifier}");
        logWriter.WriteLine($"# start={DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        logWriter.WriteLine($"# oneBarSec={oneBarSec:F3}, targetDur={targetDurationSec:F3}, minWindows={minWindows}, seed={regionSeed}");
        foreach (var w in randomWindows) logWriter.WriteLine($"# window,{w.x:F3},{w.y:F3}");
        logWriter.WriteLine("realtime_s,video_s,event,idx");
        logWriter.Flush();

        Debug.Log($"[LOG OPEN] {logPath}");
    }

    void CloseLog()
    {
        if (logWriter != null)
        {
            try { logWriter.Flush(); logWriter.Close(); } catch {}
            finally { logWriter = null; }
        }
    }

    // ===== /sdcard/Android/media 로 안전 백업 =====
    void TryExportLogToMedia(string srcFullPath)
    {
        try
        {
            if (string.IsNullOrEmpty(srcFullPath) || !File.Exists(srcFullPath)) return;

            // APK 삭제 후에도 남는 위치
            string mediaDir = $"/sdcard/Android/media/{Application.identifier}/exports/logs";
            Directory.CreateDirectory(mediaDir);

            string dst = Path.Combine(mediaDir, Path.GetFileName(srcFullPath));
            File.Copy(srcFullPath, dst, true);
            Debug.Log($"[LOG EXPORT] {srcFullPath} -> {dst}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LOG EXPORT] failed: {e.Message}");
        }
    }

    // ===== 유틸 =====
    // ≥ oneBarSec 이상 비는 모든 공백을 제외하고 허용구간만 반환
    static List<Vector2> BuildAllowedByRemovingLongGaps(List<float> ts, float total, float gapThr)
    {
        var excludes = new List<Vector2>();
        if (ts.Count == 0) return new List<Vector2>();
        if (ts[0] > gapThr) excludes.Add(new Vector2(0, ts[0]));
        for (int i = 0; i < ts.Count - 1; i++)
            if (ts[i + 1] - ts[i] >= gapThr) excludes.Add(new Vector2(ts[i], ts[i + 1]));
        if (total - ts[^1] >= gapThr) excludes.Add(new Vector2(ts[^1], total));
        return ComplementIntervals(new Vector2(0, total), MergeIntervals(excludes));
    }

    // === 핵심: maxWindows 없이 targetDuration을 채울 때까지 랜덤 윈도우 생성 (반복 상한만 가드) ===
    static List<Vector2> SampleRandomWindowsMatchingDuration_NoMax(
        List<Vector2> allowed, float target, float minLen, int seed, int maxIterations)
    {
        var outSegs = new List<Vector2>();
        if (allowed == null || allowed.Count == 0 || target <= 0f) return outSegs;

        System.Random rg = new System.Random(seed == 0 ? UnityEngine.Random.Range(1, int.MaxValue) : seed);

        float remain = target;
        int iters = 0;

        while (remain > 1e-3f && iters++ < Mathf.Max(1, maxIterations))
        {
            // 길이: [minLen, remain] 균등
            float len = Mathf.Clamp((float)rg.NextDouble() * remain, minLen, remain);

            // 길이 맞는 후보 seg
            var cands = allowed.Where(s => (s.y - s.x) >= len + 1e-4f).ToList();
            Vector2 baseSeg;
            if (cands.Count > 0) baseSeg = cands[rg.Next(cands.Count)];
            else
            {
                // 전혀 없다면 가장 긴 seg에 클램프해서 넣기
                baseSeg = allowed.OrderByDescending(s => s.y - s.x).First();
                len = Mathf.Min(len, baseSeg.y - baseSeg.x);
                if (len <= 1e-4f) break;
            }

            float room = (baseSeg.y - baseSeg.x) - len;
            if (room <= 0f) continue;

            float off = (float)rg.NextDouble() * room;
            var add = new Vector2(baseSeg.x + off, baseSeg.x + off + len);
            outSegs.Add(add);

            // 병합 후 remain 갱신
            outSegs = MergeIntervals(outSegs);
            float cur = SumDuration(outSegs);
            remain = Mathf.Max(0f, target - cur);
        }

        // 오버슈트면 살짝 컷 (가장 긴 세그먼트부터)
        float over = SumDuration(outSegs) - target;
        int safetyCut = 0;
        while (over > 1e-3f && safetyCut++ < 50 && outSegs.Count > 0)
        {
            // 가장 긴 세그먼트 찾기
            int idx = 0;
            float bestLen = -1f;
            for (int i = 0; i < outSegs.Count; i++)
            {
                float L = outSegs[i].y - outSegs[i].x;
                if (L > bestLen) { bestLen = L; idx = i; }
            }

            var s = outSegs[idx];
            float cut = Mathf.Min(over, bestLen * 0.5f); // 가운데 남기고 양쪽 컷
            if (cut <= 1e-4f) break;

            float newX = s.x + cut * 0.5f;
            float newY = s.y - cut * 0.5f;
            if ((newY - newX) <= 1e-4f) outSegs.RemoveAt(idx);
            else outSegs[idx] = new Vector2(newX, newY);

            over = SumDuration(outSegs) - target;
        }

        return MergeIntervals(outSegs);
    }

    static List<float> FilterBeatsByWindows(List<float> beats, List<Vector2> windows)
    {
        var res = new List<float>();
        if (beats == null || windows == null) return res;
        windows = MergeIntervals(windows);
        foreach (var b in beats)
        {
            for (int i = 0; i < windows.Count; i++)
            {
                if (b >= windows[i].x && b <= windows[i].y) { res.Add(b); break; }
            }
        }
        return res;
    }

    static List<Vector2> MergeIntervals(List<Vector2> segs)
    {
        if (segs == null || segs.Count == 0) return new List<Vector2>();
        var s = segs.OrderBy(v => v.x).ToList();
        var outSegs = new List<Vector2> { s[0] };
        for (int i = 1; i < s.Count; i++)
        {
            var cur = s[i]; var last = outSegs[^1];
            if (cur.x <= last.y) outSegs[^1] = new Vector2(last.x, Mathf.Max(last.y, cur.y));
            else outSegs.Add(cur);
        }
        outSegs.RemoveAll(v => v.y - v.x <= 1e-4f);
        return outSegs;
    }

    static List<Vector2> ComplementIntervals(Vector2 whole, List<Vector2> subs)
    {
        var outSegs = new List<Vector2>();
        float cursor = whole.x;
        foreach (var s in subs)
        {
            if (s.x > cursor) outSegs.Add(new Vector2(cursor, s.x));
            cursor = Mathf.Max(cursor, s.y);
        }
        if (cursor < whole.y) outSegs.Add(new Vector2(cursor, whole.y));
        outSegs.RemoveAll(v => v.y - v.x <= 1e-4f);
        return outSegs;
    }

    static float SumDuration(List<Vector2> segs)
    {
        float s = 0f;
        if (segs == null) return 0f;
        for (int i = 0; i < segs.Count; i++) s += Mathf.Max(0f, segs[i].y - segs[i].x);
        return s;
    }

    // ===== minWindows 보장 유틸 =====
    static List<Vector2> EnforceMinWindows(
        List<Vector2> windows,
        List<Vector2> allowed,
        int minWindows,
        float minLen,
        float target,
        int seed)
    {
        var rng = new System.Random(seed == 0 ? UnityEngine.Random.Range(1, int.MaxValue) : seed);
        windows = MergeIntervals(windows);

        // 1) 가장 긴 윈도우를 분할해서 개수 늘리기
        int guard = 0;
        while (windows.Count < minWindows && guard++ < 100)
        {
            if (!TrySplitLongestWindow(windows, minLen))
                break; // 분할 불가 → 추가 생성 시도
            windows = MergeIntervals(windows);
        }

        // 2) 그래도 부족하면 빈 구간에 minLen 윈도우 추가
        guard = 0;
        while (windows.Count < minWindows && guard++ < 100)
        {
            if (!TryAddSmallWindow(windows, allowed, minLen, rng))
                break; // 더 이상 넣을 곳 없음
            windows = MergeIntervals(windows);
        }

        // 3) 총 길이가 타겟을 넘으면 가장 긴 세그먼트부터 잘라 맞춤
        float over = SumDuration(windows) - target;
        int safetyCut = 0;
        while (over > 1e-3f && safetyCut++ < 50 && windows.Count > 0)
        {
            // 가장 긴 세그먼트
            int idx = 0; float best = -1f;
            for (int i = 0; i < windows.Count; i++)
            {
                float L = windows[i].y - windows[i].x;
                if (L > best) { best = L; idx = i; }
            }
            var s = windows[idx];
            float cut = Mathf.Min(over, best * 0.5f);
            if (cut <= 1e-4f) break;

            float newX = s.x + cut * 0.5f;
            float newY = s.y - cut * 0.5f;
            if ((newY - newX) <= 1e-4f) windows.RemoveAt(idx);
            else windows[idx] = new Vector2(newX, newY);

            over = SumDuration(windows) - target;
        }

        return MergeIntervals(windows);
    }

    static bool TrySplitLongestWindow(List<Vector2> windows, float minLen)
    {
        if (windows == null || windows.Count == 0) return false;

        // 가장 긴 세그먼트 찾기
        int idx = 0; float best = -1f;
        for (int i = 0; i < windows.Count; i++)
        {
            float L = windows[i].y - windows[i].x;
            if (L > best) { best = L; idx = i; }
        }

        // 분할 가능한지 체크(분할 후 두 조각 모두 minLen 이상)
        if (best < 2f * minLen + 1e-4f) return false;

        var s = windows[idx];
        float mid = (s.x + s.y) * 0.5f;

        var left  = new Vector2(s.x, mid);
        var right = new Vector2(mid, s.y);

        // 양쪽 길이가 minLen 이상인지 확인
        if ((left.y - left.x) < minLen || (right.y - right.x) < minLen) return false;

        // 교체
        windows.RemoveAt(idx);
        windows.Add(left);
        windows.Add(right);
        return true;
    }

    static bool TryAddSmallWindow(List<Vector2> windows, List<Vector2> allowed, float minLen, System.Random rng)
    {
        if (allowed == null || allowed.Count == 0) return false;

        // 현재 윈도우를 제외한 "가용 구간" 계산 (allowed - windows)
        var free = ComplementIntervalsMulti(allowed, MergeIntervals(windows));
        // 길이가 충분한 free seg 만 후보로
        var cands = free.Where(s => (s.y - s.x) >= minLen + 1e-4f).ToList();
        if (cands.Count == 0) return false;

        var baseSeg = cands[rng.Next(cands.Count)];
        float room = (baseSeg.y - baseSeg.x) - minLen;
        float off = (float)rng.NextDouble() * Mathf.Max(0f, room);
        var add = new Vector2(baseSeg.x + off, baseSeg.x + off + minLen);
        windows.Add(add);
        return true;
    }

    // allowed 여러 구간에서 subs(이미 사용중인 윈도우) 를 빼서 free를 계산
    static List<Vector2> ComplementIntervalsMulti(List<Vector2> allowed, List<Vector2> subs)
    {
        var free = new List<Vector2>();
        var mergedSubs = MergeIntervals(subs);
        foreach (var a in allowed)
        {
            // a 구간에서 subs를 빼기
            var insideSubs = mergedSubs
                .Where(s => s.x < a.y && s.y > a.x) // 교차
                .Select(s => new Vector2(Mathf.Max(s.x, a.x), Mathf.Min(s.y, a.y)))
                .ToList();
            var comp = ComplementIntervals(a, MergeIntervals(insideSubs));
            free.AddRange(comp);
        }
        return MergeIntervals(free);
    }

    // ===== 해프틱 트리거 =====
    void TriggerHaptic()
    {
        int[] motors = new int[32] {
            0,0,0,0,0,90,90,0, 0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,  0,0,0,0,0,0,0,0
        };
        BhapticsLibrary.PlayMotors((int)PositionType.Vest, motors, 100);

        float real = Time.realtimeSinceStartup;
        float video = (float)videoPlayer.time;
        if (logWriter != null)
        {
            logWriter.WriteLine($"{real:F3},{video:F3},pulse,{currentIndex}");
            logWriter.Flush(); // 이벤트마다 즉시 기록
        }
        Debug.Log($"💥 Haptic at {video:F2}s");
    }
}
