using System;

namespace ProxiCraft;

/// <summary>
/// Flight Recorder - Crash diagnostics for multiplayer issues.
/// Writes [FR] tagged entries directly to pc_debug.log.
/// Always active - log file self-rotates at 100KB.
/// </summary>
public static class FlightRecorder
{
    private const string TAG = "[FR]";
    private const string CLEAN_EXIT_MARKER = "[FR] === SESSION CLEAN EXIT ===";
    private static bool _initialized;

    public static void Initialize(string modFolder)
    {
        try
        {
            _initialized = true;
            ProxiCraft.FileLogAlways($"{TAG} === SESSION START {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning($"[ProxiCraft] FlightRecorder init failed: {ex.Message}");
            _initialized = false;
        }
    }

    public static void Record(string message)
    {
        if (!_initialized) return;
        try
        {
            ProxiCraft.FileLogAlways($"{TAG} {message}");
        }
        catch { }
    }

    public static void Record(string category, string message)
    {
        Record($"[{category}] {message}");
    }

    public static void RecordException(Exception ex, string context = null)
    {
        if (!_initialized) return;
        try
        {
            var message = string.IsNullOrEmpty(context)
                ? $"EXCEPTION: {ex?.GetType().Name}: {ex?.Message}"
                : $"EXCEPTION in {context}: {ex?.GetType().Name}: {ex?.Message}";
            Record("ERROR", message);
        }
        catch { }
    }

    public static void OnCleanShutdown()
    {
        if (!_initialized) return;
        try
        {
            Record("Clean shutdown");
            ProxiCraft.FileLogAlways(CLEAN_EXIT_MARKER);
        }
        catch { }
    }
}
