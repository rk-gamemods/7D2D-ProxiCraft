using System;
using System.Collections.Generic;
using System.Linq;

namespace ProxiCraft;

/// <summary>
/// Storage source types that can be prioritized for item retrieval.
/// Default order values are used when config is missing or incomplete.
/// </summary>
public enum StorageType
{
    Drone = 1,
    DewCollector = 2,
    Workstation = 3,
    Container = 4,
    Vehicle = 5
}

/// <summary>
/// Manages storage source priority ordering.
/// Initialized once at mod startup, provides cached ordering for all operations.
/// </summary>
public static class StoragePriority
{
    private static List<StorageType> _cachedOrder;
    private static bool _initialized;

    /// <summary>
    /// All valid storage type names for matching (case-insensitive).
    /// </summary>
    private static readonly Dictionary<string, StorageType> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Drone", StorageType.Drone },
        { "DewCollector", StorageType.DewCollector },
        { "Workstation", StorageType.Workstation },
        { "Container", StorageType.Container },
        { "Vehicle", StorageType.Vehicle }
    };

    /// <summary>
    /// Default priority order (matches Beyond Storage 2's order).
    /// </summary>
    private static readonly List<StorageType> DefaultOrder = new()
    {
        StorageType.Drone,
        StorageType.DewCollector,
        StorageType.Workstation,
        StorageType.Container,
        StorageType.Vehicle
    };

    /// <summary>
    /// Initialize storage priority from config. Called once at mod startup.
    /// </summary>
    public static void Initialize(ModConfig config)
    {
        _cachedOrder = ComputeOrder(config?.storagePriority);
        _initialized = true;

        ProxiCraft.Log($"Storage priority order: {string.Join(" → ", _cachedOrder)}");
    }

    /// <summary>
    /// Gets the cached priority order. Must call Initialize() first.
    /// </summary>
    public static IReadOnlyList<StorageType> GetOrder()
    {
        if (!_initialized)
        {
            ProxiCraft.LogWarning("StoragePriority.GetOrder() called before Initialize() - using defaults");
            return DefaultOrder;
        }
        return _cachedOrder;
    }

    /// <summary>
    /// Orders a storage dictionary by priority. Returns enumerable in priority order.
    /// </summary>
    public static IEnumerable<KeyValuePair<Vector3i, object>> OrderStorages(
        Dictionary<Vector3i, object> storageDict,
        Func<object, StorageType> getStorageType)
    {
        if (!_initialized || storageDict == null || storageDict.Count == 0)
            return storageDict ?? Enumerable.Empty<KeyValuePair<Vector3i, object>>();

        // Create lookup for priority index
        var priorityIndex = new Dictionary<StorageType, int>();
        for (int i = 0; i < _cachedOrder.Count; i++)
            priorityIndex[_cachedOrder[i]] = i;

        // Sort by priority order
        return storageDict
            .OrderBy(kvp =>
            {
                var type = getStorageType(kvp.Value);
                return priorityIndex.TryGetValue(type, out int idx) ? idx : int.MaxValue;
            });
    }

    /// <summary>
    /// Computes the priority order from config, handling missing/invalid entries.
    /// </summary>
    private static List<StorageType> ComputeOrder(Dictionary<string, string> configPriority)
    {
        // Case 1: Empty or null config - use full defaults
        if (configPriority == null || configPriority.Count == 0)
        {
            ProxiCraft.LogWarning("storagePriority config section is empty or missing - using defaults");
            return new List<StorageType>(DefaultOrder);
        }

        var result = new List<StorageType>();
        var assigned = new HashSet<StorageType>();
        var unrecognized = new List<string>();

        // Parse entries with their sort values
        var entries = new List<(StorageType type, string sortKey)>();

        foreach (var kvp in configPriority)
        {
            string key = kvp.Key?.Trim();
            string value = kvp.Value?.Trim() ?? "999";

            if (string.IsNullOrEmpty(key))
                continue;

            // Try exact match first (case-insensitive)
            if (ValidTypes.TryGetValue(key, out StorageType exactMatch))
            {
                if (!assigned.Contains(exactMatch))
                {
                    entries.Add((exactMatch, value));
                    assigned.Add(exactMatch);
                }
                continue;
            }

            // Try fuzzy match against missing types only
            var missingTypes = ValidTypes.Values.Where(t => !assigned.Contains(t)).ToList();
            var fuzzyMatch = TryFuzzyMatch(key, missingTypes);

            if (fuzzyMatch.HasValue)
            {
                ProxiCraft.LogWarning($"Config: \"{key}\" interpreted as \"{fuzzyMatch.Value}\" (typo corrected)");
                entries.Add((fuzzyMatch.Value, value));
                assigned.Add(fuzzyMatch.Value);
            }
            else
            {
                unrecognized.Add(key);
            }
        }

        // Warn about unrecognized keys
        foreach (var key in unrecognized)
        {
            ProxiCraft.LogWarning($"Unrecognized storagePriority key \"{key}\" - ignored");
        }

        // Sort entries by their sort key (alphanumeric safe)
        entries.Sort((a, b) => CompareAlphanumeric(a.sortKey, b.sortKey));

        // Add sorted entries to result
        foreach (var entry in entries)
            result.Add(entry.type);

        // Append missing types in default order with warning
        var missing = DefaultOrder.Where(t => !assigned.Contains(t)).ToList();
        if (missing.Count > 0)
        {
            var missingNames = string.Join(", ", missing);
            var nextIndex = entries.Count > 0 ? entries.Count + 1 : 1;
            var fixSuggestions = string.Join("\n  ", 
                missing.Select((m, i) => $"\"{m}\": \"{nextIndex + i}\""));

            ProxiCraft.LogWarning($"storagePriority missing: {missingNames}");
            ProxiCraft.LogWarning($"These will be checked LAST. To fix, add to config.json:\n  {fixSuggestions}");

            result.AddRange(missing);
        }

        return result;
    }

    /// <summary>
    /// Attempts fuzzy matching of input against missing storage types.
    /// Uses sequential character matching from start of string.
    /// </summary>
    private static StorageType? TryFuzzyMatch(string input, List<StorageType> candidates)
    {
        if (string.IsNullOrEmpty(input) || candidates.Count == 0)
            return null;

        var scores = new List<(StorageType type, int score)>();

        foreach (var candidate in candidates)
        {
            string candidateName = candidate.ToString();
            int matchCount = GetSequentialMatchCount(input, candidateName);
            
            if (matchCount > 0)
                scores.Add((candidate, matchCount));
        }

        if (scores.Count == 0)
            return null; // No matches at all

        // Sort by score descending
        scores.Sort((a, b) => b.score.CompareTo(a.score));

        // Check for ambiguity (tie at top)
        if (scores.Count > 1 && scores[0].score == scores[1].score)
        {
            var tied = scores.Where(s => s.score == scores[0].score).Select(s => s.type);
            ProxiCraft.LogWarning($"Ambiguous key \"{input}\" could match: {string.Join(" or ", tied)} - ignored");
            return null;
        }

        return scores[0].type;
    }

    /// <summary>
    /// Counts sequential matching characters from start of input against target.
    /// Case-insensitive comparison.
    /// </summary>
    private static int GetSequentialMatchCount(string input, string target)
    {
        int matchCount = 0;
        int minLen = Math.Min(input.Length, target.Length);

        for (int i = 0; i < minLen; i++)
        {
            if (char.ToLowerInvariant(input[i]) == char.ToLowerInvariant(target[i]))
                matchCount++;
            else
                break; // Stop at first mismatch
        }

        return matchCount;
    }

    /// <summary>
    /// Compares two strings alphanumerically (handles "1", "2", "10", "A", "B").
    /// Numbers sort before letters, numbers sort numerically.
    /// </summary>
    private static int CompareAlphanumeric(string a, string b)
    {
        // Try to parse as numbers first
        bool aIsNum = int.TryParse(a, out int aNum);
        bool bIsNum = int.TryParse(b, out int bNum);

        if (aIsNum && bIsNum)
            return aNum.CompareTo(bNum);

        if (aIsNum)
            return -1; // Numbers before letters

        if (bIsNum)
            return 1; // Letters after numbers

        // Both are non-numeric, compare as strings
        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
