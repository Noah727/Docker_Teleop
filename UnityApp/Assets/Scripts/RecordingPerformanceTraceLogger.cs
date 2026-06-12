using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using RosMessageTypes.Std;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;

/// <summary>
/// Evaluation-only logger for measuring frame rate impact during camera recording.
/// It starts a local CSV and ROS trace stream when any GripperCameraRecorder starts
/// recording, then keeps logging for a short tail after recording stops.
/// </summary>
public class RecordingPerformanceTraceLogger : MonoBehaviour
{
    private const string RuntimeObjectName = "RecordingPerformanceTraceLogger";

    [Header("Logging")]
    public bool enableLogging = true;
    public bool writeLocalCsv = true;
    public bool publishRosTrace = true;
    [Min(0.5f)] public float sampleRateHz = 10f;
    [Min(0f)] public float postRecordingLogSeconds = 15f;
    public string outputFolderName = "RecordingPerformanceLogs";

    [Header("ROS Topics")]
    public string recordingStateTopic = "/unity_eval/recording_state";
    public string fpsSampleTopic = "/unity_eval/fps_sample";
    public bool publishIdleHeartbeat = true;
    [Min(0.1f)] public float idleHeartbeatHz = 1f;

    [Header("Discovery")]
    public bool autoDiscoverRecorders = true;
    public float recorderRediscoveryPeriodSec = 1f;
    public GripperCameraRecorder[] recorders;

    private readonly List<GripperCameraRecorder> activeRecorders = new List<GripperCameraRecorder>();
    private ROSConnection ros;
    private bool rosRegistered;
    private bool traceActive;
    private bool wasRecording;
    private float postRecordingDeadline = -1f;
    private float nextSampleTime;
    private float nextRediscoveryTime;
    private float nextIdleHeartbeatTime;
    private float lastSampleTime;
    private int lastSampleFrame;
    private string currentLogPath;
    private StreamWriter csvWriter;
    private string traceSessionId;
    private float traceStartRealtime;

    public bool TraceActive => traceActive;
    public string CurrentLogPath => currentLogPath;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateLoggerAfterSceneLoad()
    {
        if (FindAnyLogger() != null)
            return;

        GameObject root = new GameObject(RuntimeObjectName);
        root.AddComponent<RecordingPerformanceTraceLogger>();
    }

    private void Awake()
    {
        if (Application.isPlaying)
            DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (!enableLogging)
            return;

        ResolveRecorders(force: true);
        RegisterRosTopics();
        Debug.Log($"[RecordingPerformanceTraceLogger] Started. recorders={(recorders == null ? 0 : recorders.Length)}, topics=({recordingStateTopic}, {fpsSampleTopic})");
        PublishState("logger_started", IsAnyRecorderRecording());
    }

    private void OnDisable()
    {
        EndTrace("component_disabled");
    }

    private void OnDestroy()
    {
        EndTrace("component_destroyed");
    }

    private void Update()
    {
        if (!enableLogging)
            return;

        if (autoDiscoverRecorders && Time.unscaledTime >= nextRediscoveryTime)
        {
            ResolveRecorders(force: false);
            nextRediscoveryTime = Time.unscaledTime + Mathf.Max(0.1f, recorderRediscoveryPeriodSec);
        }

        bool isRecording = IsAnyRecorderRecording();
        if (isRecording && !wasRecording)
        {
            BeginTrace();
            PublishState("recording_started", true);
        }
        else if (!isRecording && wasRecording)
        {
            postRecordingDeadline = Time.unscaledTime + Mathf.Max(0f, postRecordingLogSeconds);
            PublishState("recording_stopped", false);
        }

        wasRecording = isRecording;

        if (traceActive)
        {
            if (Time.unscaledTime >= nextSampleTime)
            {
                WriteAndPublishSample(isRecording);
                nextSampleTime = Time.unscaledTime + 1f / Mathf.Max(0.5f, sampleRateHz);
            }

            if (!isRecording && Time.unscaledTime >= postRecordingDeadline)
                EndTrace("post_recording_tail_complete");
        }
        else if (publishIdleHeartbeat && Time.unscaledTime >= nextIdleHeartbeatTime)
        {
            PublishState("idle_heartbeat", isRecording);
            nextIdleHeartbeatTime = Time.unscaledTime + 1f / Mathf.Max(0.1f, idleHeartbeatHz);
        }
    }

    private void BeginTrace()
    {
        if (traceActive)
            return;

        traceSessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        traceStartRealtime = Time.realtimeSinceStartup;
        traceActive = true;
        postRecordingDeadline = float.PositiveInfinity;
        lastSampleTime = Time.unscaledTime;
        lastSampleFrame = Time.frameCount;
        nextSampleTime = Time.unscaledTime;

        if (writeLocalCsv)
            OpenLocalCsv();
    }

    private void EndTrace(string reason)
    {
        if (!traceActive)
            return;

        PublishState(reason, IsAnyRecorderRecording());
        traceActive = false;
        postRecordingDeadline = -1f;

        if (csvWriter != null)
        {
            csvWriter.Flush();
            csvWriter.Dispose();
            csvWriter = null;
        }
    }

    private void OpenLocalCsv()
    {
        string folder = Path.Combine(Application.persistentDataPath, outputFolderName);
        Directory.CreateDirectory(folder);
        currentLogPath = Path.Combine(folder, $"recording_performance_{traceSessionId}.csv");
        csvWriter = new StreamWriter(currentLogPath, false, Encoding.UTF8);
        csvWriter.WriteLine("unity_realtime_sec,unix_time_ms,frame_count,recording_active,post_tail_active,fps,frame_time_ms,sample_window_sec,frames_in_window,active_recorders,session_folders");
        Debug.Log($"[RecordingPerformanceTraceLogger] Recording performance log: {currentLogPath}");
    }

    private void WriteAndPublishSample(bool isRecording)
    {
        float now = Time.unscaledTime;
        float window = Mathf.Max(0.0001f, now - lastSampleTime);
        int frames = Mathf.Max(0, Time.frameCount - lastSampleFrame);
        float fps = frames / window;
        float frameTimeMs = fps > 0.001f ? 1000f / fps : 0f;
        bool postTailActive = traceActive && !isRecording;
        string activeNames = GetActiveRecorderNames();
        string sessionFolders = GetSessionFolders();
        long unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (csvWriter != null)
        {
            csvWriter.WriteLine(string.Join(",", new[]
            {
                FormatFloat(Time.realtimeSinceStartup),
                unixMs.ToString(CultureInfo.InvariantCulture),
                Time.frameCount.ToString(CultureInfo.InvariantCulture),
                BoolInt(isRecording),
                BoolInt(postTailActive),
                FormatFloat(fps),
                FormatFloat(frameTimeMs),
                FormatFloat(window),
                frames.ToString(CultureInfo.InvariantCulture),
                Csv(activeNames),
                Csv(sessionFolders),
            }));
            csvWriter.Flush();
        }

        PublishFpsSample(isRecording, postTailActive, fps, frameTimeMs, window, frames, activeNames, sessionFolders, unixMs);
        lastSampleTime = now;
        lastSampleFrame = Time.frameCount;
    }

    private void RegisterRosTopics()
    {
        if (!publishRosTrace || rosRegistered)
            return;

        try
        {
            ros = ROSConnection.GetOrCreateInstance();
            ros.RegisterPublisher<StringMsg>(recordingStateTopic);
            ros.RegisterPublisher<StringMsg>(fpsSampleTopic);
            rosRegistered = true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RecordingPerformanceTraceLogger] Could not register ROS publishers yet: {ex.Message}");
        }
    }

    private void PublishState(string eventName, bool isRecording)
    {
        if (!publishRosTrace)
            return;

        RegisterRosTopics();
        if (ros == null || !rosRegistered)
            return;

        string payload = "{" +
            $"\"event\":\"{JsonEscape(eventName)}\"," +
            $"\"recording_active\":{JsonBool(isRecording)}," +
            $"\"trace_active\":{JsonBool(traceActive)}," +
            $"\"unity_realtime_sec\":{FormatFloat(Time.realtimeSinceStartup)}," +
            $"\"unix_time_ms\":{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}," +
            $"\"trace_session_id\":\"{JsonEscape(traceSessionId ?? string.Empty)}\"," +
            $"\"local_log_path\":\"{JsonEscape(currentLogPath ?? string.Empty)}\"," +
            $"\"active_recorders\":\"{JsonEscape(GetActiveRecorderNames())}\"" +
            "}";
        ros.Publish(recordingStateTopic, new StringMsg(payload));
    }

    private void PublishFpsSample(bool isRecording, bool postTailActive, float fps, float frameTimeMs, float window, int frames, string activeNames, string sessionFolders, long unixMs)
    {
        if (!publishRosTrace)
            return;

        RegisterRosTopics();
        if (ros == null || !rosRegistered)
            return;

        string payload = "{" +
            $"\"recording_active\":{JsonBool(isRecording)}," +
            $"\"post_tail_active\":{JsonBool(postTailActive)}," +
            $"\"trace_active\":{JsonBool(traceActive)}," +
            $"\"unity_realtime_sec\":{FormatFloat(Time.realtimeSinceStartup)}," +
            $"\"unix_time_ms\":{unixMs}," +
            $"\"frame_count\":{Time.frameCount}," +
            $"\"fps\":{FormatFloat(fps)}," +
            $"\"frame_time_ms\":{FormatFloat(frameTimeMs)}," +
            $"\"sample_window_sec\":{FormatFloat(window)}," +
            $"\"frames_in_window\":{frames}," +
            $"\"trace_session_id\":\"{JsonEscape(traceSessionId ?? string.Empty)}\"," +
            $"\"active_recorders\":\"{JsonEscape(activeNames)}\"," +
            $"\"session_folders\":\"{JsonEscape(sessionFolders)}\"" +
            "}";
        ros.Publish(fpsSampleTopic, new StringMsg(payload));
    }

    private bool IsAnyRecorderRecording()
    {
        activeRecorders.Clear();
        if (recorders == null)
            return false;

        foreach (GripperCameraRecorder recorder in recorders)
        {
            if (recorder == null)
                continue;
            if (recorder.IsRecording)
                activeRecorders.Add(recorder);
        }

        return activeRecorders.Count > 0;
    }

    private string GetActiveRecorderNames()
    {
        if (activeRecorders.Count == 0)
            return string.Empty;

        List<string> names = new List<string>();
        foreach (GripperCameraRecorder recorder in activeRecorders)
        {
            if (recorder != null)
                names.Add(recorder.name);
        }
        return string.Join("|", names);
    }

    private string GetSessionFolders()
    {
        if (activeRecorders.Count == 0)
            return string.Empty;

        List<string> folders = new List<string>();
        foreach (GripperCameraRecorder recorder in activeRecorders)
        {
            if (recorder != null && !string.IsNullOrEmpty(recorder.CurrentSessionFolder))
                folders.Add(recorder.CurrentSessionFolder);
        }
        return string.Join("|", folders);
    }

    private void ResolveRecorders(bool force)
    {
        if (!autoDiscoverRecorders && !force)
            return;

#if UNITY_2023_1_OR_NEWER
        recorders = FindObjectsByType<GripperCameraRecorder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        recorders = FindObjectsOfType<GripperCameraRecorder>(true);
#endif
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static string BoolInt(bool value)
    {
        return value ? "1" : "0";
    }

    private static string JsonBool(bool value)
    {
        return value ? "true" : "false";
    }

    private static string JsonEscape(string value)
    {
        return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");
    }

    private static string Csv(string value)
    {
        return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
    }

    private static RecordingPerformanceTraceLogger FindAnyLogger()
    {
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindFirstObjectByType<RecordingPerformanceTraceLogger>(FindObjectsInactive.Include);
#else
        return UnityEngine.Object.FindObjectOfType<RecordingPerformanceTraceLogger>(true);
#endif
    }
}
