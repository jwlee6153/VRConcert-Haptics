
// BeatHapticRandomWindows.cs
// - no maxWindows
// - provides SetCSV / Rebuild
// - maxIterations guard
// - guarantees minWindows
// - log: timestamped filename per run + (optional) auto-backup to /Android/media on quit

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
    public TextAsset csvFile; // Beat timestamps (sec) CSV (header allowed)

    [Header("Timing")]
    public float offsetSeconds = 0f;      // Offset (seconds)
    public float syncThreshold = 0.04f;   // Sync tolerance (seconds)

    [Header("Bar / Gaps")]
    public float oneBarSec = 1.8f;        // One-bar length (seconds)
    public float totalDurationSec = 0f;   // Estimated total duration (0 = auto)

    [Header("Random Windows")]
    public float targetDurationSec = 73.63f; // Total chorus duration (sec): varies by song
    public float minWindowSec = 0.5f;        // Minimum length per window (sec)
    public int regionSeed = 0;               // Seed (0 = random each run)
    [Tooltip("Minimum number of random windows (guaranteed via split/add)")]
    public int minWindows = 3;

    [Header("Haptics")]
    public float minGap = 0.12f;             // Minimum trigger interval (sec)

    [Header("Logging (timestamp)")]
    public bool enableLogging = true;
    [Tooltip("Log file prefix (e.g., 'haptic' → haptic_YYYYMMDD_HHMMSS.csv)")]
    public string logPrefix = "haptic";
    [Tooltip("Auto-backup on quit to /sdcard/Android/media/<pkg>/exports/logs")]
    public bool exportToMediaOnQuit = true;

    [Header("Generation Guardrail")]
    [Tooltip("Upper bound on random window generation iterations")]
    public int maxIterations = 200;

    // Internal state
    private readonly List<float> beatTimes = new();
    private List<Vector2> allowedSegments = new();
    private List<Vector2> randomWindows = new();
    private List<float> playTimes = new();
    private int currentIndex = 0;
    private float lastTrig = -999f;

    // Log
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
        enabled = false; // Enabled externally (SequentialVideoPlayer, etc.)
    }

    void Update()
    {
        if (!videoPlayer || !videoPlayer.isPlaying || playTimes.Count == 0) return;
        float now = (float)videoPlayer.time + offsetSeconds;

        // Skip past beats
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
    /// Rebuild by changing bar length / target / seed externally
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
        Debug.Log($"Beats loaded: {beatTimes.Count}");
    }

    // ===== Allowed segments =====
    void BuildAllowedSegments()
    {
        float total = EstimateTotalDuration();
        allowedSegments = BuildAllowedByRemovingLongGaps(beatTimes, total, oneBarSec);
        Debug.Log($"[Allowed] segs={allowedSegments.Count}, dur={SumDuration(allowedSegments):F2}");
    }

    // ===== Random windows & play times =====
    void BuildRandomWindowsAndPlayTimes()
    {
        // Clamp target to allowed sum
        float allowedSum = SumDuration(allowedSegments);
        float target = Mathf.Min(targetDurationSec, Mathf.Max(0f, allowedSum));

        randomWindows = SampleRandomWindowsMatchingDuration_NoMax(
            allowedSegments, target, minWindowSec, regionSeed, maxIterations);

        // Guarantee minimum count up to minWindows via split/add
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

    // ===== Logging =====
    void InitLog()
    {
        if (!enableLogging) return;

        string dir = Application.persistentDataPath; // Internal (removed with APK uninstall)
        Directory.CreateDirectory(dir);

        logStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        // Unique filename per run: haptic_YYYYMMDD_HHMMSS.csv
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

    // ===== Safe backup to /sdcard/Android/media =====
    void TryExportLogToMedia(string srcFullPath)
    {
        try
        {
            if (string.IsNullOrEmpty(srcFullPath) || !File.Exists(srcFullPath)) return;

            // Persists after APK uninstall
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

    // ===== Utils =====
    // Remove all gaps >= oneBarSec and return allowed segments
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

    // === Core: generate random windows until targetDuration is filled (iteration guard only) ===
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
            // Length: uniform in [minLen, remain]
            float len = Mathf.Clamp((float)rg.NextDouble() * remain, minLen, remain);

            // Candidate segments that fit the length
            var cands = allowed.Where(s => (s.y - s.x) >= len + 1e-4f).ToList();
            Vector2 baseSeg;
            if (cands.Count > 0) baseSeg = cands[rg.Next(cands.Count)];
            else
            {
                // If none fit, clamp into the longest segment
                baseSeg = allowed.OrderByDescending(s => s.y - s.x).First();
                len = Mathf.Min(len, baseSeg.y - baseSeg.x);
                if (len <= 1e-4f) break;
            }

            float room = (baseSeg.y - baseSeg.x) - len;
            if (room <= 0f) continue;

            float off = (float)rg.NextDouble() * room;
            var add = new Vector2(baseSeg.x + off, baseSeg.x + off + len);
            outSegs.Add(add);

            // Merge and update remaining duration
            outSegs = MergeIntervals(outSegs);
            float cur = SumDuration(outSegs);
            remain = Mathf.Max(0f, target - cur);
        }

        // If overshoot, slightly trim (from the longest segment first)
        float over = SumDuration(outSegs) - target;
        int safetyCut = 0;
        while (over > 1e-3f && safetyCut++ < 50 && outSegs.Count > 0)
        {
            // Find longest segment
            int idx = 0;
            float bestLen = -1f;
            for (int i = 0; i < outSegs.Count; i++)
            {
                float L = outSegs[i].y - outSegs[i].x;
                if (L > bestLen) { bestLen = L; idx = i; }
            }

            var s = outSegs[idx];
            float cut = Mathf.Min(over, bestLen * 0.5f); // Symmetric trim
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

    // ===== minWindows guarantee utils =====
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

        // 1) Split the longest window to increase count
        int guard = 0;
        while (windows.Count < minWindows && guard++ < 100)
        {
            if (!TrySplitLongestWindow(windows, minLen))
                break; // Cannot split → try adding
            windows = MergeIntervals(windows);
        }

        // 2) If still insufficient, add minLen windows in free regions
        guard = 0;
        while (windows.Count < minWindows && guard++ < 100)
        {
            if (!TryAddSmallWindow(windows, allowed, minLen, rng))
                break; // No more space
            windows = MergeIntervals(windows);
        }

        // 3) If total duration exceeds target, trim longest segments
        float over = SumDuration(windows) - target;
        int safetyCut = 0;
        while (over > 1e-3f && safetyCut++ < 50 && windows.Count > 0)
        {
            // Longest segment
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

        // Find longest segment
        int idx = 0; float best = -1f;
        for (int i = 0; i < windows.Count; i++)
        {
            float L = windows[i].y - windows[i].x;
            if (L > best) { best = L; idx = i; }
        }

        // Check if splittable (both halves >= minLen)
        if (best < 2f * minLen + 1e-4f) return false;

        var s = windows[idx];
        float mid = (s.x + s.y) * 0.5f;

        var left  = new Vector2(s.x, mid);
        var right = new Vector2(mid, s.y);

        // Verify both sides >= minLen
        if ((left.y - left.x) < minLen || (right.y - right.x) < minLen) return false;

        // Replace
        windows.RemoveAt(idx);
        windows.Add(left);
        windows.Add(right);
        return true;
    }

    static bool TryAddSmallWindow(List<Vector2> windows, List<Vector2> allowed, float minLen, System.Random rng)
    {
        if (allowed == null || allowed.Count == 0) return false;

        // Compute free regions (allowed - windows)
        var free = ComplementIntervalsMulti(allowed, MergeIntervals(windows));
        // Only free segments with sufficient length
        var cands = free.Where(s => (s.y - s.x) >= minLen + 1e-4f).ToList();
        if (cands.Count == 0) return false;

        var baseSeg = cands[rng.Next(cands.Count)];
        float room = (baseSeg.y - baseSeg.x) - minLen;
        float off = (float)rng.NextDouble() * Mathf.Max(0f, room);
        var add = new Vector2(baseSeg.x + off, baseSeg.x + off + minLen);
        windows.Add(add);
        return true;
    }

    // Subtract subs (already used windows) from allowed segments to compute free regions
    static List<Vector2> ComplementIntervalsMulti(List<Vector2> allowed, List<Vector2> subs)
    {
        var free = new List<Vector2>();
        var mergedSubs = MergeIntervals(subs);
        foreach (var a in allowed)
        {
            // Subtract subs from segment a
            var insideSubs = mergedSubs
                .Where(s => s.x < a.y && s.y > a.x) // intersection
                .Select(s => new Vector2(Mathf.Max(s.x, a.x), Mathf.Min(s.y, a.y)))
                .ToList();
            var comp = ComplementIntervals(a, MergeIntervals(insideSubs));
            free.AddRange(comp);
        }
        return MergeIntervals(free);
    }

    // ===== Haptic trigger =====
    void TriggerHaptic()
    {
        int[] motors = new int[32] {
            0,0,0,0,0,90,90,0, 0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,  0,0,0,0,0,0,0,0
        }; // Motors array defines per-actuator intensity (0–100); 90 indicates strong vibration.
        BhapticsLibrary.PlayMotors((int)PositionType.Vest, motors, 100); 
        // The final argument (100) sets the vibration duration in milliseconds (100 ms pulse)

        float real = Time.realtimeSinceStartup;
        float video = (float)videoPlayer.time;
        if (logWriter != null)
        {
            logWriter.WriteLine($"{real:F3},{video:F3},pulse,{currentIndex}");
            logWriter.Flush(); // Immediate write per event
        }
        Debug.Log($"Haptic at {video:F2}s");
    }
}
