using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace ProxiCraft;

/// <summary>
/// Comprehensive performance profiler for ProxiCraft operations.
/// 
/// FEATURES:
/// - Microsecond precision timing (not just milliseconds)
/// - Frame timing to detect hitches/stalls
/// - Harmony patch entry/exit timing
/// - Lock contention tracking
/// - GC pressure indicators
/// - Spike detection with timestamps
/// 
/// Usage:
/// - Enable with "pc perf on"
/// - Play normally to collect data
/// - View with "pc perf report"
/// 
/// RUBBER-BANDING DETECTION:
/// If you experience rubber-banding/desync in singleplayer, this profiler will:
/// 1. Track frame-to-frame timing gaps
/// 2. Record when spikes occur (with timestamps)
/// 3. Show which operations caused the spike
/// </summary>
public static class PerformanceProfiler
{
    /// <summary>
    /// Whether profiling is currently enabled.
    /// </summary>
    public static bool IsEnabled { get; set; } = false;

    /// <summary>
    /// Whether any profiling data has been collected.
    /// </summary>
    public static bool HasData
    {
        get
        {
            lock (_lock)
            {
                return _stats.Count > 0 || _frameTimings.Count > 0;
            }
        }
    }

    // Sample buffer sizes
    private const int ALL_TIME_SAMPLES = 10000;
    private const int RECENT_SAMPLES = 1000;
    private const int FRAME_TIMING_SAMPLES = 500;  // Track last 500 frames
    private const int SPIKE_LOG_SIZE = 50;         // Remember last 50 spikes

    // Spike threshold - anything over 16ms is a potential frame hitch
    private const double SPIKE_THRESHOLD_MS = 16.0;
    private const double SEVERE_SPIKE_MS = 50.0;

    /// <summary>
    /// Records a spike event with timestamp and context.
    /// </summary>
    public class SpikeEvent
    {
        public DateTime Timestamp { get; set; }
        public string Operation { get; set; }
        public double DurationMs { get; set; }
        public double DurationUs { get; set; }  // Microseconds for precision
        public string Context { get; set; }
        public long FrameCount { get; set; }
    }

    /// <summary>
    /// Tracks statistics for a single operation type with microsecond precision.
    /// </summary>
    public class OperationStats
    {
        public string Name { get; set; }
        public long CallCount { get; set; }
        public double TotalUs { get; set; }      // Total microseconds
        public double MinUs { get; set; } = double.MaxValue;
        public double MaxUs { get; set; } = double.MinValue;
        public double LastUs { get; set; }
        public int CacheHits { get; set; }
        public int CacheMisses { get; set; }
        
        // Track when max occurred
        public DateTime MaxOccurredAt { get; set; }
        public long MaxAtFrame { get; set; }
        
        // Spike counts
        public int SpikeCount { get; set; }       // > 16ms
        public int SevereSpikeCount { get; set; } // > 50ms
        
        // All-time sample buffer (stores microseconds)
        private readonly double[] _allSamples = new double[ALL_TIME_SAMPLES];
        private int _allIndex = 0;
        private int _allCount = 0;
        
        // Recent sample buffer
        private readonly double[] _recentSamples = new double[RECENT_SAMPLES];
        private int _recentIndex = 0;
        private int _recentCount = 0;
        
        public double TotalMs => TotalUs / 1000.0;
        public double AvgUs => CallCount > 0 ? TotalUs / CallCount : 0;
        public double AvgMs => AvgUs / 1000.0;
        public double MinMs => MinUs / 1000.0;
        public double MaxMs => MaxUs / 1000.0;
        public double LastMs => LastUs / 1000.0;
        public double HitRate => (CacheHits + CacheMisses) > 0 
            ? (double)CacheHits / (CacheHits + CacheMisses) * 100 
            : 0;
        
        public void RecordSample(double us, long frameCount)
        {
            // All-time buffer
            _allSamples[_allIndex] = us;
            _allIndex = (_allIndex + 1) % ALL_TIME_SAMPLES;
            if (_allCount < ALL_TIME_SAMPLES) _allCount++;
            
            // Recent buffer
            _recentSamples[_recentIndex] = us;
            _recentIndex = (_recentIndex + 1) % RECENT_SAMPLES;
            if (_recentCount < RECENT_SAMPLES) _recentCount++;
            
            // Track max with timestamp
            if (us > MaxUs)
            {
                MaxUs = us;
                MaxOccurredAt = DateTime.Now;
                MaxAtFrame = frameCount;
            }
            
            // Track spikes
            double ms = us / 1000.0;
            if (ms >= SPIKE_THRESHOLD_MS) SpikeCount++;
            if (ms >= SEVERE_SPIKE_MS) SevereSpikeCount++;
        }
        
        public int AllSampleCount => _allCount;
        public int RecentSampleCount => _recentCount;
        
        public double AllAvgUs
        {
            get
            {
                if (_allCount == 0) return 0;
                double sum = 0;
                for (int i = 0; i < _allCount; i++) sum += _allSamples[i];
                return sum / _allCount;
            }
        }
        
        public double AllMaxUs
        {
            get
            {
                if (_allCount == 0) return 0;
                double max = 0;
                for (int i = 0; i < _allCount; i++)
                    if (_allSamples[i] > max) max = _allSamples[i];
                return max;
            }
        }
        
        public double GetAllPercentile(double percentile) => GetPercentileFromBuffer(_allSamples, _allCount, percentile);
        
        public double RecentAvgUs
        {
            get
            {
                if (_recentCount == 0) return 0;
                double sum = 0;
                for (int i = 0; i < _recentCount; i++) sum += _recentSamples[i];
                return sum / _recentCount;
            }
        }
        
        public double RecentMaxUs
        {
            get
            {
                if (_recentCount == 0) return 0;
                double max = 0;
                for (int i = 0; i < _recentCount; i++)
                    if (_recentSamples[i] > max) max = _recentSamples[i];
                return max;
            }
        }
        
        public double GetRecentPercentile(double percentile) => GetPercentileFromBuffer(_recentSamples, _recentCount, percentile);
        
        private static double GetPercentileFromBuffer(double[] buffer, int count, double percentile)
        {
            if (count == 0) return 0;
            var samples = new double[count];
            Array.Copy(buffer, samples, count);
            Array.Sort(samples);
            double rank = (percentile / 100.0) * (count - 1);
            int lower = (int)Math.Floor(rank);
            int upper = (int)Math.Ceiling(rank);
            if (lower == upper) return samples[lower];
            double weight = rank - lower;
            return samples[lower] * (1 - weight) + samples[upper] * weight;
        }
    }

    // Operation statistics storage
    private static readonly Dictionary<string, OperationStats> _stats = new();
    
    // Active timers with high-resolution timestamps
    private static readonly Dictionary<string, long> _activeTimers = new();
    
    // Frame timing tracking
    private static readonly List<double> _frameTimings = new();
    private static readonly List<double> _proxiCraftFrameTimes = new();  // ProxiCraft time per frame (us)
    private static long _lastFrameTicks = 0;
    private static long _frameCount = 0;
    private static double _currentFrameProxiCraftUs = 0;  // Accumulator for current frame
    
    // Spike log
    private static readonly List<SpikeEvent> _spikeLog = new();
    
    // Lock contention tracking
    private static long _lockWaitTotalUs = 0;
    private static long _lockAcquisitions = 0;
    private static long _lockContentions = 0;  // Times we had to wait
    
    // GC tracking
    private static int _lastGcCount0 = 0;
    private static int _lastGcCount1 = 0;
    private static int _lastGcCount2 = 0;
    private static int _gcCount0 = 0;
    private static int _gcCount1 = 0;
    private static int _gcCount2 = 0;
    
    // High-resolution timer frequency
    private static readonly double _ticksPerUs = Stopwatch.Frequency / 1_000_000.0;
    private static readonly double _ticksPerMs = Stopwatch.Frequency / 1_000.0;
    
    // Thread safety
    private static readonly object _lock = new();
    private static int _lockHeld = 0;  // For contention detection

    // ========== OPERATION NAMES ==========
    
    // Core operations
    public const string OP_REBUILD_CACHE = "RebuildItemCountCache";
    public const string OP_GET_ITEM_COUNT = "GetItemCount";
    public const string OP_REFRESH_STORAGES = "RefreshStorages";
    public const string OP_GET_STORAGE_ITEMS = "GetStorageItems";
    public const string OP_REMOVE_ITEMS = "RemoveItems";
    
    // Scanning operations
    public const string OP_SCAN_ENTITIES = "ScanEntities";
    public const string OP_SCAN_TILE_ENTITIES = "ScanTileEntities";
    public const string OP_CHUNK_SCAN = "ChunkScan";
    public const string OP_IS_IN_RANGE = "IsInRange";
    
    // Counting operations
    public const string OP_COUNT_VEHICLES = "CountVehicles";
    public const string OP_COUNT_DRONES = "CountDrones";
    public const string OP_COUNT_DEWCOLLECTORS = "CountDewCollectors";
    public const string OP_COUNT_WORKSTATIONS = "CountWorkstations";
    public const string OP_COUNT_CONTAINERS = "CountContainers";
    
    // Cleanup operations
    public const string OP_CLEANUP_STALE = "CleanupStale";
    public const string OP_LOCK_CLEANUP = "LockCleanup";
    public const string OP_PREWARM_CACHE = "PreWarmCache";
    
    // UI operations
    public const string OP_HUD_AMMO_UPDATE = "HudAmmoUpdate";
    public const string OP_RECIPE_LIST_BUILD = "RecipeListBuild";
    public const string OP_RECIPE_CRAFT_COUNT = "RecipeCraftCount";
    
    // Network/Multiplayer operations
    public const string OP_NETWORK_BROADCAST = "NetworkBroadcast";
    public const string OP_PACKET_OBSERVE = "PacketObserve";
    public const string OP_HANDSHAKE_PROCESS = "HandshakeProcess";
    public const string OP_PACKET_SEND = "PacketSend";
    public const string OP_PACKET_RECEIVE = "PacketReceive";
    public const string OP_FILE_LOG = "FileLog";
    
    // Harmony patch operations (track ALL patches)
    public const string OP_PATCH_TELOCK = "Patch_TELock";
    public const string OP_PATCH_TEUNLOCK = "Patch_TEUnlock";
    public const string OP_PATCH_STARTGAME = "Patch_StartGame";
    public const string OP_PATCH_SAVECLEANUP = "Patch_SaveCleanup";
    public const string OP_PATCH_HASITEMS = "Patch_HasItems";
    public const string OP_PATCH_BUILDRECIPES = "Patch_BuildRecipes";
    public const string OP_PATCH_HUDUPDATE = "Patch_HudUpdate";
    
    // Frame timing
    public const string OP_FRAME_TOTAL = "FrameTotal";

    // ========== TIMING METHODS ==========

    /// <summary>
    /// Starts timing an operation with microsecond precision.
    /// </summary>
    public static void StartTimer(string operationName)
    {
        if (!IsEnabled) return;
        
        long startTicks = Stopwatch.GetTimestamp();
        
        // Track lock contention
        bool hadContention = Interlocked.CompareExchange(ref _lockHeld, 1, 0) != 0;
        
        lock (_lock)
        {
            if (hadContention)
            {
                _lockContentions++;
                // Measure how long we waited
                long afterLock = Stopwatch.GetTimestamp();
                _lockWaitTotalUs += (long)((afterLock - startTicks) / _ticksPerUs);
            }
            _lockAcquisitions++;
            
            _activeTimers[operationName] = Stopwatch.GetTimestamp();
        }
        
        Interlocked.Exchange(ref _lockHeld, 0);
    }

    /// <summary>
    /// Stops timing an operation and records with microsecond precision.
    /// </summary>
    public static void StopTimer(string operationName)
    {
        if (!IsEnabled) return;

        long endTicks = Stopwatch.GetTimestamp();
        
        lock (_lock)
        {
            if (!_activeTimers.TryGetValue(operationName, out var startTicks))
                return;

            double us = (endTicks - startTicks) / _ticksPerUs;
            double ms = us / 1000.0;

            if (!_stats.TryGetValue(operationName, out var stats))
            {
                stats = new OperationStats { Name = operationName };
                _stats[operationName] = stats;
            }

            stats.CallCount++;
            stats.TotalUs += us;
            stats.LastUs = us;
            stats.RecordSample(us, _frameCount);
            if (us < stats.MinUs) stats.MinUs = us;
            
            // Accumulate ProxiCraft time for this frame (exclude FrameTotal itself)
            if (operationName != OP_FRAME_TOTAL)
            {
                _currentFrameProxiCraftUs += us;
            }
            
            // Log spikes
            if (ms >= SPIKE_THRESHOLD_MS)
            {
                LogSpike(operationName, us, ms >= SEVERE_SPIKE_MS ? "SEVERE" : "spike");
            }
        }
    }

    /// <summary>
    /// Records a spike event for later analysis.
    /// </summary>
    private static void LogSpike(string operation, double us, string context)
    {
        var spike = new SpikeEvent
        {
            Timestamp = DateTime.Now,
            Operation = operation,
            DurationUs = us,
            DurationMs = us / 1000.0,
            Context = context,
            FrameCount = _frameCount
        };
        
        _spikeLog.Add(spike);
        
        // Keep only last N spikes
        while (_spikeLog.Count > SPIKE_LOG_SIZE)
            _spikeLog.RemoveAt(0);
    }

    /// <summary>
    /// Called at the start of each frame to track frame timing.
    /// Should be called from a patch on a per-frame method.
    /// </summary>
    public static void OnFrameStart()
    {
        if (!IsEnabled) return;
        
        long now = Stopwatch.GetTimestamp();
        
        lock (_lock)
        {
            if (_lastFrameTicks > 0)
            {
                double frameMs = (now - _lastFrameTicks) / _ticksPerMs;
                _frameTimings.Add(frameMs);
                
                // Store ProxiCraft time for this frame (convert to ms for consistency)
                double proxiCraftMs = _currentFrameProxiCraftUs / 1000.0;
                _proxiCraftFrameTimes.Add(proxiCraftMs);
                
                // Keep only last N frames (both lists stay in sync)
                while (_frameTimings.Count > FRAME_TIMING_SAMPLES)
                {
                    _frameTimings.RemoveAt(0);
                    _proxiCraftFrameTimes.RemoveAt(0);
                }
                
                // Track severe frame hitches
                if (frameMs >= SEVERE_SPIKE_MS)
                {
                    // Include ProxiCraft contribution in the spike context
                    double proxiPercent = frameMs > 0 ? (proxiCraftMs / frameMs * 100) : 0;
                    LogSpike(OP_FRAME_TOTAL, frameMs * 1000, $"Frame hitch: {frameMs:F1}ms (ProxiCraft: {proxiPercent:F1}%)");
                }
            }
            
            _lastFrameTicks = now;
            _frameCount++;
            _currentFrameProxiCraftUs = 0;  // Reset for next frame
            
            // Track GC
            int gc0 = GC.CollectionCount(0);
            int gc1 = GC.CollectionCount(1);
            int gc2 = GC.CollectionCount(2);
            
            if (gc0 > _lastGcCount0) _gcCount0 += gc0 - _lastGcCount0;
            if (gc1 > _lastGcCount1) _gcCount1 += gc1 - _lastGcCount1;
            if (gc2 > _lastGcCount2) _gcCount2 += gc2 - _lastGcCount2;
            
            _lastGcCount0 = gc0;
            _lastGcCount1 = gc1;
            _lastGcCount2 = gc2;
        }
    }

    /// <summary>
    /// Records a cache hit for an operation.
    /// </summary>
    public static void RecordCacheHit(string operationName)
    {
        if (!IsEnabled) return;
        lock (_lock)
        {
            if (!_stats.TryGetValue(operationName, out var stats))
            {
                stats = new OperationStats { Name = operationName };
                _stats[operationName] = stats;
            }
            stats.CacheHits++;
        }
    }

    /// <summary>
    /// Records a cache miss for an operation.
    /// </summary>
    public static void RecordCacheMiss(string operationName)
    {
        if (!IsEnabled) return;
        lock (_lock)
        {
            if (!_stats.TryGetValue(operationName, out var stats))
            {
                stats = new OperationStats { Name = operationName };
                _stats[operationName] = stats;
            }
            stats.CacheMisses++;
        }
    }

    /// <summary>
    /// Clears all recorded statistics.
    /// </summary>
    public static void Reset()
    {
        lock (_lock)
        {
            _stats.Clear();
            _activeTimers.Clear();
            _frameTimings.Clear();
            _proxiCraftFrameTimes.Clear();
            _spikeLog.Clear();
            _lastFrameTicks = 0;
            _frameCount = 0;
            _currentFrameProxiCraftUs = 0;
            _lockWaitTotalUs = 0;
            _lockAcquisitions = 0;
            _lockContentions = 0;
            _gcCount0 = _gcCount1 = _gcCount2 = 0;
        }
    }

    // ========== REPORT GENERATION ==========

    /// <summary>
    /// Gets a comprehensive performance report with microsecond precision.
    /// </summary>
    public static string GetReport()
    {
        lock (_lock)
        {
            if (_stats.Count == 0 && _frameTimings.Count == 0)
            {
                return "No performance data collected.\nEnable profiling with 'pc perf on' and play for a bit.";
            }

            var sb = new StringBuilder();
            sb.AppendLine("================================================================================");
            sb.AppendLine("           PROXICRAFT COMPREHENSIVE PERFORMANCE REPORT");
            sb.AppendLine("                    (Microsecond Precision Timing)");
            sb.AppendLine("================================================================================");
            sb.AppendLine();
            sb.AppendLine($"Report Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            sb.AppendLine($"Total Frames Tracked: {_frameCount:N0}");
            sb.AppendLine($"Profiler Precision: {1.0 / _ticksPerUs:F3} microseconds");
            sb.AppendLine();

            // ========== FRAME TIMING SECTION ==========
            sb.AppendLine("================================================================================");
            sb.AppendLine("  FRAME TIMING (Rubber-band/Hitch Detection)");
            sb.AppendLine("================================================================================");
            
            if (_frameTimings.Count > 0)
            {
                var sortedFrames = _frameTimings.OrderBy(x => x).ToList();
                double avgFrame = _frameTimings.Average();
                double minFrame = sortedFrames[0];
                double maxFrame = sortedFrames[sortedFrames.Count - 1];
                double p50Frame = GetPercentile(sortedFrames, 50);
                double p90Frame = GetPercentile(sortedFrames, 90);
                double p99Frame = GetPercentile(sortedFrames, 99);
                double p999Frame = GetPercentile(sortedFrames, 99.9);
                
                int hitchCount = _frameTimings.Count(f => f >= SPIKE_THRESHOLD_MS);
                int severeHitchCount = _frameTimings.Count(f => f >= SEVERE_SPIKE_MS);
                double hitchPercent = (double)hitchCount / _frameTimings.Count * 100;
                
                sb.AppendLine($"  Frames Sampled: {_frameTimings.Count}");
                sb.AppendLine($"  Average: {avgFrame:F2}ms | Min: {minFrame:F2}ms | Max: {maxFrame:F2}ms");
                sb.AppendLine($"  P50: {p50Frame:F2}ms | P90: {p90Frame:F2}ms | P99: {p99Frame:F2}ms | P99.9: {p999Frame:F2}ms");
                sb.AppendLine($"  Hitches (>16ms): {hitchCount} ({hitchPercent:F1}%)");
                sb.AppendLine($"  Severe (>50ms): {severeHitchCount}");
                
                // Target FPS analysis
                double targetMs = 16.67;  // 60 FPS target
                int framesOnTarget = _frameTimings.Count(f => f <= targetMs);
                double onTargetPercent = (double)framesOnTarget / _frameTimings.Count * 100;
                sb.AppendLine($"  Frames at 60fps: {onTargetPercent:F1}%");
                
                if (maxFrame > 100)
                {
                    sb.AppendLine();
                    sb.AppendLine($"  !! WARNING: Max frame time {maxFrame:F1}ms indicates SEVERE hitching!");
                    sb.AppendLine($"     This WILL cause rubber-banding/input lag!");
                }
            }
            else
            {
                sb.AppendLine("  No frame timing data. Frame tracking not yet active.");
            }
            sb.AppendLine();

            // ========== PROXICRAFT CONTRIBUTION ANALYSIS ==========
            sb.AppendLine("================================================================================");
            sb.AppendLine("  PROXICRAFT CONTRIBUTION ANALYSIS (% of Frame Time)");
            sb.AppendLine("================================================================================");
            
            if (_frameTimings.Count > 0 && _proxiCraftFrameTimes.Count > 0)
            {
                // Calculate per-frame percentages
                var frameContributions = new List<double>();
                for (int i = 0; i < Math.Min(_frameTimings.Count, _proxiCraftFrameTimes.Count); i++)
                {
                    double frameMs = _frameTimings[i];
                    double proxiMs = _proxiCraftFrameTimes[i];
                    double percent = frameMs > 0.001 ? (proxiMs / frameMs * 100) : 0;
                    frameContributions.Add(percent);
                }
                
                if (frameContributions.Count > 0)
                {
                    var sortedContrib = frameContributions.OrderBy(x => x).ToList();
                    double avgContrib = frameContributions.Average();
                    double maxContrib = sortedContrib[sortedContrib.Count - 1];
                    double p50Contrib = GetPercentile(sortedContrib, 50);
                    double p90Contrib = GetPercentile(sortedContrib, 90);
                    double p99Contrib = GetPercentile(sortedContrib, 99);
                    
                    double totalFrameMs = _frameTimings.Sum();
                    double totalProxiMs = _proxiCraftFrameTimes.Sum();
                    double overallPercent = totalFrameMs > 0 ? (totalProxiMs / totalFrameMs * 100) : 0;
                    
                    sb.AppendLine();
                    sb.AppendLine($"  Overall ProxiCraft Time: {totalProxiMs:F2}ms of {totalFrameMs:F2}ms total");
                    sb.AppendLine($"  ┌─────────────────────────────────────────────────────────────────┐");
                    sb.AppendLine($"  │  PROXICRAFT CONTRIBUTION: {overallPercent,6:F2}% of total frame time       │");
                    sb.AppendLine($"  │  Other Sources:           {100 - overallPercent,6:F2}% of total frame time       │");
                    sb.AppendLine($"  └─────────────────────────────────────────────────────────────────┘");
                    sb.AppendLine();
                    sb.AppendLine($"  Per-Frame Breakdown:");
                    sb.AppendLine($"    Average: {avgContrib:F2}% | Max: {maxContrib:F2}%");
                    sb.AppendLine($"    P50: {p50Contrib:F2}% | P90: {p90Contrib:F2}% | P99: {p99Contrib:F2}%");
                    
                    // Analyze hitch frames specifically
                    int hitchFrames = 0;
                    double hitchProxiContribSum = 0;
                    double hitchOtherContribSum = 0;
                    for (int i = 0; i < Math.Min(_frameTimings.Count, _proxiCraftFrameTimes.Count); i++)
                    {
                        if (_frameTimings[i] >= SPIKE_THRESHOLD_MS)
                        {
                            hitchFrames++;
                            hitchProxiContribSum += _proxiCraftFrameTimes[i];
                            hitchOtherContribSum += _frameTimings[i] - _proxiCraftFrameTimes[i];
                        }
                    }
                    
                    if (hitchFrames > 0)
                    {
                        double hitchProxiPercent = hitchProxiContribSum / (hitchProxiContribSum + hitchOtherContribSum) * 100;
                        sb.AppendLine();
                        sb.AppendLine($"  HITCH ANALYSIS (frames >16ms): {hitchFrames} hitches");
                        sb.AppendLine($"    ProxiCraft caused: {hitchProxiPercent:F1}% of hitch time");
                        sb.AppendLine($"    Other sources:     {100 - hitchProxiPercent:F1}% of hitch time");
                        
                        if (hitchProxiPercent < 5)
                        {
                            sb.AppendLine();
                            sb.AppendLine($"  ✓ VERDICT: ProxiCraft is NOT causing your lag!");
                            sb.AppendLine($"    Only {hitchProxiPercent:F1}% of hitch time is from ProxiCraft.");
                            sb.AppendLine($"    Look for other mods or game issues.");
                        }
                        else if (hitchProxiPercent < 20)
                        {
                            sb.AppendLine();
                            sb.AppendLine($"  ~ VERDICT: ProxiCraft is a MINOR contributor.");
                            sb.AppendLine($"    {hitchProxiPercent:F1}% of hitch time, but other sources are larger.");
                        }
                        else
                        {
                            sb.AppendLine();
                            sb.AppendLine($"  ! VERDICT: ProxiCraft MAY be contributing to lag.");
                            sb.AppendLine($"    {hitchProxiPercent:F1}% of hitch time is from ProxiCraft.");
                            sb.AppendLine($"    Check operation timing above for the culprit.");
                        }
                    }
                    else
                    {
                        sb.AppendLine();
                        sb.AppendLine($"  ✓ No frame hitches detected during profiling!");
                    }
                }
            }
            else
            {
                sb.AppendLine("  Waiting for frame data...");
            }
            sb.AppendLine();

            // ========== OPERATION TIMING SECTION ==========
            sb.AppendLine("================================================================================");
            sb.AppendLine("  OPERATION TIMING (All ProxiCraft Operations)");
            sb.AppendLine("================================================================================");
            sb.AppendLine();
            sb.AppendLine("Operation                | Calls    | Avg(us)  | P90(us)  | P99(us)  | Max(us)  |Spikes");
            sb.AppendLine("-------------------------|----------|----------|----------|----------|----------|------");
            
            var sortedStats = _stats.Values.OrderByDescending(s => s.TotalUs).ToList();
            
            foreach (var stat in sortedStats)
            {
                string name = stat.Name.Length > 24 ? stat.Name.Substring(0, 21) + "..." : stat.Name;
                
                double avgUs = stat.AllSampleCount > 0 ? stat.AllAvgUs : stat.AvgUs;
                double p90Us = stat.AllSampleCount > 0 ? stat.GetAllPercentile(90) : 0;
                double p99Us = stat.AllSampleCount > 0 ? stat.GetAllPercentile(99) : 0;
                double maxUs = stat.AllSampleCount > 0 ? stat.AllMaxUs : stat.MaxUs;
                
                string spikeInfo = stat.SpikeCount > 0 ? $"{stat.SpikeCount}" : "-";
                if (stat.SevereSpikeCount > 0)
                    spikeInfo = $"{stat.SpikeCount}({stat.SevereSpikeCount}!)";
                
                sb.AppendLine($"{name,-24} | {stat.CallCount,8} | {FormatUs(avgUs),8} | {FormatUs(p90Us),8} | {FormatUs(p99Us),8} | {FormatUs(maxUs),8} | {spikeInfo,5}");
                
                // Show cache info if applicable
                if (stat.CacheHits + stat.CacheMisses > 0)
                {
                    sb.AppendLine($"  ^-- Cache: {stat.HitRate:F0}% hit ({stat.CacheHits} hits / {stat.CacheMisses} misses)");
                }
                
                // Show max timestamp for operations with severe spikes
                if (stat.SevereSpikeCount > 0 && stat.MaxOccurredAt != default)
                {
                    sb.AppendLine($"  ^-- Max spike at: {stat.MaxOccurredAt:HH:mm:ss.fff} (frame {stat.MaxAtFrame})");
                }
            }
            sb.AppendLine();
            sb.AppendLine("us = microseconds (1ms = 1000us). Spikes = calls >16ms. (X!) = severe spikes >50ms.");
            sb.AppendLine();

            // ========== SPIKE LOG SECTION ==========
            if (_spikeLog.Count > 0)
            {
                sb.AppendLine("================================================================================");
                sb.AppendLine("  SPIKE LOG (Recent Performance Spikes)");
                sb.AppendLine("================================================================================");
                sb.AppendLine();
                
                var recentSpikes = _spikeLog.OrderByDescending(s => s.Timestamp).Take(20).ToList();
                
                sb.AppendLine("Time         | Frame    | Duration    | Operation               | Context");
                sb.AppendLine("-------------|----------|-------------|-------------------------|--------");
                
                foreach (var spike in recentSpikes)
                {
                    string opName = spike.Operation.Length > 24 ? spike.Operation.Substring(0, 21) + "..." : spike.Operation;
                    sb.AppendLine($"{spike.Timestamp:HH:mm:ss.fff} | {spike.FrameCount,8} | {spike.DurationMs,8:F2}ms | {opName,-24}| {spike.Context}");
                }
                sb.AppendLine();
            }

            // ========== LOCK CONTENTION SECTION ==========
            sb.AppendLine("================================================================================");
            sb.AppendLine("  THREAD SAFETY (Lock Contention Analysis)");
            sb.AppendLine("================================================================================");
            sb.AppendLine();
            
            double contentionRate = _lockAcquisitions > 0 ? (double)_lockContentions / _lockAcquisitions * 100 : 0;
            double avgWaitUs = _lockContentions > 0 ? (double)_lockWaitTotalUs / _lockContentions : 0;
            
            sb.AppendLine($"  Lock Acquisitions: {_lockAcquisitions:N0}");
            sb.AppendLine($"  Lock Contentions: {_lockContentions:N0} ({contentionRate:F2}%)");
            sb.AppendLine($"  Total Wait Time: {_lockWaitTotalUs / 1000.0:F2}ms");
            sb.AppendLine($"  Avg Wait per Contention: {avgWaitUs:F1}us");
            
            if (contentionRate > 5)
            {
                sb.AppendLine();
                sb.AppendLine($"  !! WARNING: High lock contention ({contentionRate:F1}%) may cause stuttering!");
            }
            sb.AppendLine();

            // ========== GC PRESSURE SECTION ==========
            sb.AppendLine("================================================================================");
            sb.AppendLine("  MEMORY (GC Pressure During Profiling)");
            sb.AppendLine("================================================================================");
            sb.AppendLine();
            sb.AppendLine($"  Gen0 Collections: {_gcCount0}");
            sb.AppendLine($"  Gen1 Collections: {_gcCount1}");
            sb.AppendLine($"  Gen2 Collections: {_gcCount2} (full GC - causes hitches!)");
            
            if (_gcCount2 > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"  !! WARNING: {_gcCount2} full GC collections detected!");
                sb.AppendLine($"     Each Gen2 GC can cause 50-200ms hitches.");
            }
            sb.AppendLine();

            // ========== NETWORK/MULTIPLAYER STATUS ==========
            sb.AppendLine("================================================================================");
            sb.AppendLine("  NETWORK/MULTIPLAYER STATUS");
            sb.AppendLine("================================================================================");
            sb.AppendLine();

            int lockCount = ContainerManager.LockedPositionCount;
            int peakLockCount = ContainerManager.PeakLockCount;
            int totalLockCleanup = ContainerManager.TotalLocksCleanedUp;
            int activeCoroutines = MultiplayerModTracker.ActiveCoroutineCount;
            int peakCoroutines = MultiplayerModTracker.PeakCoroutineCount;
            int totalCoroutines = MultiplayerModTracker.TotalCoroutinesStarted;
            int packetsSent = MultiplayerModTracker.TotalPacketsSent;
            int packetsReceived = MultiplayerModTracker.TotalPacketsReceived;
            long totalObserved = NetworkPacketObserver.TotalPacketsObserved;
            int currentStorage = ContainerManager.CurrentStorageCount;
            int knownStorage = ContainerManager.KnownStorageCount;

            sb.AppendLine($"  Container Locks: {lockCount} active | {peakLockCount} peak | {totalLockCleanup} cleaned");
            sb.AppendLine($"  Coroutines: {activeCoroutines} active | {peakCoroutines} peak | {totalCoroutines} started");
            sb.AppendLine($"  Packets: {packetsSent} sent | {packetsReceived} received | {totalObserved} observed");
            sb.AppendLine($"  Storage Cache: {currentStorage} current | {knownStorage} known");

            bool isMP = MultiplayerModTracker.IsMultiplayerSession;
            bool isHosting = MultiplayerModTracker.IsHosting;
            bool isLocked = !MultiplayerModTracker.IsModAllowed();

            string mpStatus = isHosting ? "Hosting" : (isMP ? "Client" : "Single-player");
            sb.AppendLine($"  Mode: {mpStatus}{(isLocked ? " [MOD LOCKED]" : "")}");
            
            if (isLocked)
            {
                string reason = MultiplayerModTracker.GetLockReason() ?? "unknown";
                sb.AppendLine($"  Lock Reason: {reason}");
            }
            sb.AppendLine();

            // ========== ANALYSIS & RECOMMENDATIONS ==========
            sb.AppendLine("================================================================================");
            sb.AppendLine("  ANALYSIS & RECOMMENDATIONS");
            sb.AppendLine("================================================================================");
            sb.AppendLine();

            bool hasIssues = false;

            // Check for frame hitches
            if (_frameTimings.Count > 0)
            {
                double maxFrame = _frameTimings.Max();
                int severeHitches = _frameTimings.Count(f => f >= SEVERE_SPIKE_MS);
                
                if (maxFrame > 100 || severeHitches > 5)
                {
                    sb.AppendLine("  [X] SEVERE FRAME HITCHES DETECTED");
                    sb.AppendLine("      This causes rubber-banding and input lag.");
                    sb.AppendLine("      Check the spike log above for the culprit operation.");
                    hasIssues = true;
                }
            }

            // Check for operation spikes
            var worstSpiker = sortedStats.OrderByDescending(s => s.SevereSpikeCount).FirstOrDefault();
            if (worstSpiker?.SevereSpikeCount > 0)
            {
                sb.AppendLine($"  [X] '{worstSpiker.Name}' caused {worstSpiker.SevereSpikeCount} severe spikes");
                sb.AppendLine($"      Max duration: {worstSpiker.MaxMs:F1}ms at {worstSpiker.MaxOccurredAt:HH:mm:ss}");
                hasIssues = true;
            }

            // Check GC
            if (_gcCount2 > 2)
            {
                sb.AppendLine($"  [X] {_gcCount2} full GC collections - causes major hitches");
                hasIssues = true;
            }

            // Check lock contention
            if (contentionRate > 10)
            {
                sb.AppendLine($"  [X] High lock contention ({contentionRate:F1}%) - thread blocking");
                hasIssues = true;
            }

            // Check coroutines
            if (peakCoroutines > 5)
            {
                sb.AppendLine($"  [!] Peak coroutine count {peakCoroutines} - possible race condition");
                hasIssues = true;
            }

            if (!hasIssues)
            {
                sb.AppendLine("  [OK] No obvious performance issues detected in ProxiCraft.");
                sb.AppendLine("       If lag persists, the issue may be in another mod or the game itself.");
            }

            sb.AppendLine();
            sb.AppendLine("================================================================================");

            return sb.ToString();
        }
    }

    /// <summary>
    /// Formats microseconds for display.
    /// </summary>
    private static string FormatUs(double us)
    {
        if (us < 1) return "< 1";
        if (us < 1000) return $"{us:F0}";
        if (us < 10000) return $"{us / 1000:F2}k";
        return $"{us / 1000:F1}k";
    }

    /// <summary>
    /// Formats milliseconds for display.
    /// </summary>
    private static string FormatMs(double ms)
    {
        if (ms < 0.01) return "0";
        if (ms < 1) return $"{ms:F2}";
        if (ms < 10) return $"{ms:F1}";
        return $"{ms:F0}";
    }

    /// <summary>
    /// Gets percentile from sorted list.
    /// </summary>
    private static double GetPercentile(List<double> sorted, double percentile)
    {
        if (sorted.Count == 0) return 0;
        double rank = (percentile / 100.0) * (sorted.Count - 1);
        int lower = (int)Math.Floor(rank);
        int upper = Math.Min((int)Math.Ceiling(rank), sorted.Count - 1);
        if (lower == upper) return sorted[lower];
        double weight = rank - lower;
        return sorted[lower] * (1 - weight) + sorted[upper] * weight;
    }

    /// <summary>
    /// Gets a brief one-line status for the pc status command.
    /// </summary>
    public static string GetBriefStatus()
    {
        if (!IsEnabled)
            return "Profiling: Disabled (use 'pc perf on' to enable)";

        lock (_lock)
        {
            if (_stats.Count == 0)
                return "Profiling: Enabled, no data yet";

            var parts = new List<string> { $"Profiling: ON ({_frameCount} frames)" };
            
            if (_frameTimings.Count > 0)
            {
                double maxFrame = _frameTimings.Max();
                parts.Add($"MaxFrame: {maxFrame:F1}ms");
            }

            int totalSpikes = _stats.Values.Sum(s => s.SpikeCount);
            if (totalSpikes > 0)
                parts.Add($"Spikes: {totalSpikes}");

            return string.Join(" | ", parts);
        }
    }
}
