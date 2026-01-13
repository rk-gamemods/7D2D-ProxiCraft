using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace ProxiCraft;

/// <summary>
/// Flight Recorder - Crash diagnostics for multiplayer issues.
///
/// HOW IT WORKS:
/// 1. Keeps a circular buffer of the last N log entries in memory
/// 2. Periodically writes buffer to MAIN LOG with [FR] tag (every few seconds)
/// 3. On clean shutdown, writes [FR] CLEAN_EXIT marker
/// 4. On crash, the log retains the [FR] entries (no clean exit marker)
/// 5. Users can grep for [FR] in the log to see flight recorder data
///
/// TAG FORMAT: [FR] - easy to grep/filter from main log
/// All flight recorder entries use this prefix for easy identification.
///
/// USAGE:
/// - Call Record() for important events (network, multiplayer state changes)
/// - Call FlushToLog() periodically (handled automatically via coroutine)
/// - Call OnCleanShutdown() when game exits normally
/// </summary>
public static class FlightRecorder
{
    private const int MAX_ENTRIES = 100;
    private const string TAG = "[FR]"; // Flight Recorder tag - grep-friendly
    private const string CLEAN_EXIT_MARKER = "[FR] === SESSION CLEAN EXIT ===";

    private static readonly Queue<string> _buffer = new Queue<string>();
    private static readonly object _lock = new object();
    private static bool _initialized;
    private static float _lastFlushTime;
    // Thread-safe tracking of flushed entries (ConcurrentDictionary as HashSet alternative)
    private static readonly ConcurrentDictionary<string, byte> _flushedEntries = new ConcurrentDictionary<string, byte>();
    private const float FLUSH_INTERVAL = 5f; // Write to log every 5 seconds

    /// <summary>
    /// Initializes the flight recorder. Call once at mod startup.
    /// </summary>
    /// <param name="modFolder">Path to the mod's folder (unused, kept for API compatibility)</param>
    public static void Initialize(string modFolder)
    {
        try
        {
            _initialized = true;
            _lastFlushTime = 0f;
            _flushedEntries.Clear(); // ConcurrentDictionary.Clear() is thread-safe

            lock (_lock)
            {
                _buffer.Clear();
            }

            // Log session start to main log file (always, not debug-dependent)
            ProxiCraft.FileLogAlways($"{TAG} === SESSION START {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            Record("FlightRecorder initialized");
        }
        catch (Exception ex)
        {
            // Don't crash the mod if flight recorder fails
            UnityEngine.Debug.LogWarning($"[ProxiCraft] FlightRecorder init failed: {ex.Message}");
            _initialized = false;
        }
    }

    /// <summary>
    /// Records an entry to the flight recorder buffer.
    /// Use for important events: network activity, state changes, potential crash points.
    /// </summary>
    /// <param name="message">The message to record</param>
    public static void Record(string message)
    {
        if (!_initialized) return;

        try
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var entry = $"{TAG} [{timestamp}] {message}";

            lock (_lock)
            {
                // Add to buffer
                _buffer.Enqueue(entry);

                // Remove oldest if over capacity (circular buffer)
                while (_buffer.Count > MAX_ENTRIES)
                {
                    var removed = _buffer.Dequeue();
                    _flushedEntries.TryRemove(removed, out _); // Allow re-logging if it comes back
                }
            }
        }
        catch
        {
            // Silently ignore - don't let logging crash the game
        }
    }

    /// <summary>
    /// Records an entry with a category prefix for easier filtering.
    /// </summary>
    public static void Record(string category, string message)
    {
        Record($"[{category}] {message}");
    }

    /// <summary>
    /// Call this periodically (e.g., from Update loop or coroutine).
    /// Writes buffer to main log if enough time has passed.
    /// </summary>
    public static void UpdateFlush()
    {
        if (!_initialized) return;

        float currentTime = UnityEngine.Time.time;
        if (currentTime - _lastFlushTime >= FLUSH_INTERVAL)
        {
            FlushToLog();
            _lastFlushTime = currentTime;
        }
    }

    /// <summary>
    /// Forces an immediate flush of the buffer to pc_debug.log.
    /// Only writes entries that haven't been written yet.
    /// Always active - log file self-rotates at 100KB.
    /// </summary>
    public static void FlushToLog()
    {
        if (!_initialized) return;

        try
        {
            string[] entries;
            lock (_lock)
            {
                entries = _buffer.ToArray();
            }

            if (entries.Length == 0) return;

            // Write only entries that haven't been flushed yet (thread-safe check-and-add)
            foreach (var entry in entries)
            {
                // TryAdd returns false if key already exists - atomic check-and-add
                if (_flushedEntries.TryAdd(entry, 0))
                {
                    ProxiCraft.FileLogAlways(entry);
                }
            }
        }
        catch
        {
            // Silently ignore log write failures
        }
    }

    /// <summary>
    /// Call this when the game exits cleanly (GameManager shutdown, etc.)
    /// Writes the clean exit marker so we know next session wasn't a crash.
    /// </summary>
    public static void OnCleanShutdown()
    {
        if (!_initialized) return;

        try
        {
            Record("Clean shutdown");
            FlushToLog(); // Ensure all entries are written

            // Write clean exit marker
            ProxiCraft.FileLogAlways(CLEAN_EXIT_MARKER);
        }
        catch
        {
            // Silently ignore
        }
    }

    /// <summary>
    /// Emergency flush for exception handlers - records the exception and flushes immediately.
    /// Use this in catch blocks to ensure crash context is captured.
    /// </summary>
    /// <param name="ex">The exception that occurred</param>
    /// <param name="context">Additional context about where the exception occurred</param>
    public static void RecordException(Exception ex, string context = null)
    {
        if (!_initialized) return;

        try
        {
            var message = string.IsNullOrEmpty(context)
                ? $"EXCEPTION: {ex?.GetType().Name}: {ex?.Message}"
                : $"EXCEPTION in {context}: {ex?.GetType().Name}: {ex?.Message}";
            
            Record("ERROR", message);
            
            // Force immediate flush - we might crash right after this
            FlushToLog();
        }
        catch
        {
            // Silently ignore - we're already handling an exception
        }
    }

    /// <summary>
    /// Gets the current buffer contents for diagnostics.
    /// </summary>
    public static string[] GetCurrentBuffer()
    {
        lock (_lock)
        {
            return _buffer.ToArray();
        }
    }

    /// <summary>
    /// Gets the number of entries currently in the buffer.
    /// </summary>
    public static int EntryCount
    {
        get
        {
            lock (_lock)
            {
                return _buffer.Count;
            }
        }
    }
}
