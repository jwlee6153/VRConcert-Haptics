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
        LoadCSV();       // Load CSV as-is in seconds
        enabled = false; // Disabled by default
    }

    void Update()
    {
        if (!videoPlayer || !videoPlayer.isPlaying || currentIndex >= beatTimes.Count) return;

        float now = (float)videoPlayer.time + offsetSeconds;

        // Skip beats that have already passed (prevents frame skips / initial jumps)
        while (currentIndex < beatTimes.Count && beatTimes[currentIndex] < now - syncThreshold)
            currentIndex++;

        if (currentIndex >= beatTimes.Count) return;

        float beat = beatTimes[currentIndex];

        // Log current–next beat difference once or twice per second (for debugging)
        if ((Time.frameCount % 20) == 0)
            Debug.Log($"[BeatHapticSync] now={now:F3} next={beat:F3} Δ={now - beat:F3} idx={currentIndex}/{beatTimes.Count}");

        if (Mathf.Abs(now - beat) <= syncThreshold)
        {
            // Minimum gap (minGap) check (uncomment if needed)
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

            // Use only the first column (comma / semicolon / tab supported)
            var parts = line.Split(new[] { ',', ';', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            // Skip header: ignore non-numeric values
            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float t))
                continue;

            // CSV is assumed to be in seconds — no additional conversion
            beatTimes.Add(t);
        }

        beatTimes.Sort();
        currentIndex = 0;

        // Debug: which CSV was loaded and a preview of the first values
        int n = Mathf.Min(5, beatTimes.Count);
        string preview = n > 0 ? string.Join(", ", beatTimes.GetRange(0, n).ConvertAll(v => v.ToString("F3"))) : "(none)";
        Debug.Log($"Loaded {beatTimes.Count} beat timestamps (csv='{csvFile?.name}', unit=seconds); first=[{preview}]");
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
            0,0,0,0,0,90,90,0, 0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,  0,0,0,0,0,0,0,0
        }; // Motors array defines per-actuator intensity (0–100); 90 indicates strong vibration.
        BhapticsLibrary.PlayMotors((int)PositionType.Vest, motors, 100); 
        // The final argument (100) sets the vibration duration in milliseconds (100 ms pulse)
        Debug.Log($"Haptic triggered at {videoPlayer.time:F2}s (offset={offsetSeconds*1000f:F0}ms)");
    }

    public void ResetSync()
    {
        currentIndex = 0;
        Debug.Log("BeatHapticSync index reset");
    }
}
