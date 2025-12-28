using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace ProxiCraft;

/// <summary>
/// Adaptive method finding with multiple fallback strategies.
/// When exact matches fail (e.g., after game update), tries progressively
/// looser matching strategies and reports what worked for future fixes.
///
/// STRATEGY CHAIN:
/// 1. Exact match (type + name + params) - fastest, most reliable
/// 2. Signature match (type + params, any name) - survives renames
/// 3. Name pattern match (regex on name + compatible params) - survives refactors
/// 4. IL pattern match (finds methods that call target internally) - survives restructures
/// 5. Behavioral heuristic (looks for methods with similar semantics) - last resort
/// </summary>
public static class AdaptiveMethodFinder
{
    /// <summary>
    /// Result of an adaptive method search, including diagnostics.
    /// </summary>
    public class FindResult
    {
        public MethodInfo Method { get; set; }
        public bool Found => Method != null;
        public string Strategy { get; set; } = "None";
        public string DiagnosticInfo { get; set; } = "";
        public string SuggestedFix { get; set; } = "";

        /// <summary>
        /// Logs the result with appropriate level based on what strategy worked.
        /// </summary>
        public void LogResult(string context)
        {
            if (!Found)
            {
                ProxiCraft.LogError($"[{context}] Method not found by any strategy");
                ProxiCraft.LogError($"[{context}] Diagnostic: {DiagnosticInfo}");
                return;
            }

            if (Strategy == "Exact")
            {
                ProxiCraft.LogDebug($"[{context}] Found via exact match: {Method.DeclaringType?.Name}.{Method.Name}");
            }
            else
            {
                // Fallback was used - warn and provide fix info
                ProxiCraft.LogWarning($"[{context}] Primary method lookup failed");
                ProxiCraft.LogWarning($"[{context}] Fallback recovery successful via {Strategy}");
                ProxiCraft.LogWarning($"[{context}] Found: {Method.DeclaringType?.FullName}.{Method.Name}");

                if (!string.IsNullOrEmpty(SuggestedFix))
                {
                    ProxiCraft.LogWarning($"[{context}] Suggested fix: {SuggestedFix}");
                }

                if (!string.IsNullOrEmpty(DiagnosticInfo))
                {
                    ProxiCraft.LogDebug($"[{context}] Diagnostic: {DiagnosticInfo}");
                }
            }
        }
    }

    /// <summary>
    /// Finds a method using adaptive strategies with fallbacks.
    /// </summary>
    /// <param name="targetType">Expected declaring type</param>
    /// <param name="methodName">Expected method name</param>
    /// <param name="parameterTypes">Expected parameter types (null = any)</param>
    /// <param name="returnType">Expected return type (null = any)</param>
    /// <param name="namePatterns">Alternative name patterns to try (e.g., "Dec", "Remove")</param>
    /// <returns>FindResult with method and diagnostics</returns>
    public static FindResult FindMethod(
        Type targetType,
        string methodName,
        Type[] parameterTypes = null,
        Type returnType = null,
        string[] namePatterns = null)
    {
        var result = new FindResult();
        var diagnostics = new List<string>();

        // Strategy 1: Exact match
        try
        {
            var method = parameterTypes != null
                ? AccessTools.Method(targetType, methodName, parameterTypes)
                : AccessTools.Method(targetType, methodName);

            if (method != null && (returnType == null || method.ReturnType == returnType))
            {
                result.Method = method;
                result.Strategy = "Exact";
                return result;
            }

            diagnostics.Add($"Exact match failed: {targetType.Name}.{methodName} not found");
        }
        catch (Exception ex)
        {
            diagnostics.Add($"Exact match error: {ex.Message}");
        }

        // Strategy 2: Signature match (same params, different name)
        try
        {
            var candidates = targetType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                                    BindingFlags.Instance | BindingFlags.Static)
                .Where(m => MatchesSignature(m, parameterTypes, returnType))
                .ToList();

            if (candidates.Count == 1)
            {
                result.Method = candidates[0];
                result.Strategy = "Signature";
                result.SuggestedFix = $"Method renamed: '{methodName}' -> '{candidates[0].Name}'";
                result.DiagnosticInfo = string.Join("; ", diagnostics);
                return result;
            }
            else if (candidates.Count > 1)
            {
                diagnostics.Add($"Signature match ambiguous: {candidates.Count} candidates found");
                // Try to narrow down by name similarity
                var bestMatch = candidates
                    .OrderByDescending(m => NameSimilarity(m.Name, methodName))
                    .First();

                result.Method = bestMatch;
                result.Strategy = "Signature+NameSimilarity";
                result.SuggestedFix = $"Method renamed: '{methodName}' -> '{bestMatch.Name}'";
                result.DiagnosticInfo = string.Join("; ", diagnostics);
                return result;
            }
            else
            {
                diagnostics.Add("Signature match failed: no methods with matching signature");
            }
        }
        catch (Exception ex)
        {
            diagnostics.Add($"Signature match error: {ex.Message}");
        }

        // Strategy 3: Name pattern match
        if (namePatterns != null && namePatterns.Length > 0)
        {
            try
            {
                foreach (var pattern in namePatterns)
                {
                    var candidates = targetType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                                           BindingFlags.Instance | BindingFlags.Static)
                        .Where(m => m.Name.Contains(pattern) && MatchesSignature(m, parameterTypes, returnType))
                        .ToList();

                    if (candidates.Count == 1)
                    {
                        result.Method = candidates[0];
                        result.Strategy = $"NamePattern({pattern})";
                        result.SuggestedFix = $"Method renamed: '{methodName}' -> '{candidates[0].Name}' (matched pattern '{pattern}')";
                        result.DiagnosticInfo = string.Join("; ", diagnostics);
                        return result;
                    }
                    else if (candidates.Count > 1)
                    {
                        // Take first that matches best
                        var best = candidates.OrderByDescending(m => NameSimilarity(m.Name, methodName)).First();
                        result.Method = best;
                        result.Strategy = $"NamePattern({pattern})+Similarity";
                        result.SuggestedFix = $"Method renamed: '{methodName}' -> '{best.Name}'";
                        result.DiagnosticInfo = string.Join("; ", diagnostics);
                        return result;
                    }
                }
                diagnostics.Add($"Name pattern match failed: patterns [{string.Join(", ", namePatterns)}] found no matches");
            }
            catch (Exception ex)
            {
                diagnostics.Add($"Name pattern match error: {ex.Message}");
            }
        }

        // Strategy 4: Search in subclasses/interfaces
        try
        {
            var relatedTypes = GetRelatedTypes(targetType);
            foreach (var relatedType in relatedTypes)
            {
                var method = parameterTypes != null
                    ? AccessTools.Method(relatedType, methodName, parameterTypes)
                    : AccessTools.Method(relatedType, methodName);

                if (method != null && (returnType == null || method.ReturnType == returnType))
                {
                    result.Method = method;
                    result.Strategy = "RelatedType";
                    result.SuggestedFix = $"Method moved to {relatedType.Name}: Update targetType from '{targetType.Name}' to '{relatedType.Name}'";
                    result.DiagnosticInfo = string.Join("; ", diagnostics);
                    return result;
                }
            }
            diagnostics.Add("Related type search failed: method not found in base classes or interfaces");
        }
        catch (Exception ex)
        {
            diagnostics.Add($"Related type search error: {ex.Message}");
        }

        // Strategy 5: Semantic search - look for methods that "do similar things"
        try
        {
            var semanticResult = SemanticMethodSearch(targetType, methodName, parameterTypes, returnType);
            if (semanticResult != null)
            {
                result.Method = semanticResult.Method;
                result.Strategy = $"Semantic({semanticResult.Reason})";
                result.SuggestedFix = semanticResult.SuggestedFix;
                result.DiagnosticInfo = string.Join("; ", diagnostics) + "; " + semanticResult.Diagnostic;
                return result;
            }
        }
        catch (Exception ex)
        {
            diagnostics.Add($"Semantic search error: {ex.Message}");
        }

        // All strategies failed
        result.DiagnosticInfo = string.Join("; ", diagnostics);
        result.SuggestedFix = $"Manual investigation needed. Target: {targetType.FullName}.{methodName}";

        return result;
    }

    /// <summary>
    /// Finds a method call in IL using adaptive strategies.
    /// </summary>
    public static FindResult FindMethodCallInIL(
        List<CodeInstruction> codes,
        Type targetType,
        string methodName,
        Type[] parameterTypes = null,
        string[] namePatterns = null)
    {
        var result = new FindResult();
        var diagnostics = new List<string>();

        // First, try to find the method definition
        var methodResult = FindMethod(targetType, methodName, parameterTypes, null, namePatterns);

        if (!methodResult.Found)
        {
            // Try IL-based discovery - scan for calls that look right
            var ilResult = ScanILForSimilarCalls(codes, targetType, methodName, parameterTypes);
            if (ilResult != null)
            {
                result.Method = ilResult.Method;
                result.Strategy = $"ILScan({ilResult.Reason})";
                result.SuggestedFix = ilResult.SuggestedFix;
                result.DiagnosticInfo = methodResult.DiagnosticInfo + "; " + ilResult.Diagnostic;
                return result;
            }

            result.DiagnosticInfo = methodResult.DiagnosticInfo;
            return result;
        }

        // We found the method definition, now verify it's in the IL
        int callIndex = RobustTranspiler.FindMethodCall(codes, targetType, methodName, parameterTypes);

        if (callIndex >= 0)
        {
            result.Method = methodResult.Method;
            result.Strategy = methodResult.Strategy;
            result.SuggestedFix = methodResult.SuggestedFix;
            result.DiagnosticInfo = methodResult.DiagnosticInfo;
            return result;
        }

        // Method exists but isn't called in this IL - maybe inlined or restructured?
        diagnostics.Add($"Method {methodResult.Method.Name} found but not called in target IL");

        // Try to find any call to the discovered method
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].operand is MethodInfo method && method == methodResult.Method)
            {
                result.Method = methodResult.Method;
                result.Strategy = methodResult.Strategy + "+ILVerified";
                result.DiagnosticInfo = string.Join("; ", diagnostics);
                return result;
            }
        }

        result.DiagnosticInfo = methodResult.DiagnosticInfo + "; " + string.Join("; ", diagnostics);
        return result;
    }

    #region Helper Methods

    private static bool MatchesSignature(MethodInfo method, Type[] parameterTypes, Type returnType)
    {
        if (returnType != null && method.ReturnType != returnType)
            return false;

        if (parameterTypes == null)
            return true;

        var methodParams = method.GetParameters();
        if (methodParams.Length != parameterTypes.Length)
            return false;

        for (int i = 0; i < parameterTypes.Length; i++)
        {
            if (!IsTypeCompatible(methodParams[i].ParameterType, parameterTypes[i]))
                return false;
        }

        return true;
    }

    private static bool IsTypeCompatible(Type actual, Type expected)
    {
        if (actual == expected)
            return true;

        // Allow assignable types (inheritance)
        if (expected.IsAssignableFrom(actual))
            return true;

        // Allow generic type matches
        if (actual.IsGenericType && expected.IsGenericType)
        {
            if (actual.GetGenericTypeDefinition() == expected.GetGenericTypeDefinition())
                return true;
        }

        return false;
    }

    private static double NameSimilarity(string name1, string name2)
    {
        // Simple similarity: count matching characters / max length
        var lower1 = name1.ToLowerInvariant();
        var lower2 = name2.ToLowerInvariant();

        int matches = 0;
        int minLen = Math.Min(lower1.Length, lower2.Length);

        for (int i = 0; i < minLen; i++)
        {
            if (lower1[i] == lower2[i])
                matches++;
        }

        // Also check for substring containment
        if (lower1.Contains(lower2) || lower2.Contains(lower1))
            matches += Math.Min(lower1.Length, lower2.Length);

        return (double)matches / Math.Max(lower1.Length, lower2.Length);
    }

    private static IEnumerable<Type> GetRelatedTypes(Type type)
    {
        var result = new List<Type>();

        // Add base classes
        var baseType = type.BaseType;
        while (baseType != null && baseType != typeof(object))
        {
            result.Add(baseType);
            baseType = baseType.BaseType;
        }

        // Add interfaces
        result.AddRange(type.GetInterfaces());

        return result;
    }

    private class SemanticMatch
    {
        public MethodInfo Method { get; set; }
        public string Reason { get; set; }
        public string SuggestedFix { get; set; }
        public string Diagnostic { get; set; }
    }

    private static SemanticMatch SemanticMethodSearch(Type targetType, string methodName, Type[] parameterTypes, Type returnType)
    {
        // Build semantic understanding of what we're looking for
        var semantics = AnalyzeMethodSemantics(methodName, parameterTypes, returnType);

        if (semantics == null)
            return null;

        // Search for methods that match the semantic profile
        var allMethods = targetType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                                BindingFlags.Instance | BindingFlags.Static);

        foreach (var method in allMethods)
        {
            var methodSemantics = AnalyzeMethodSemantics(method.Name,
                method.GetParameters().Select(p => p.ParameterType).ToArray(),
                method.ReturnType);

            if (methodSemantics != null && SemanticsMatch(semantics, methodSemantics))
            {
                return new SemanticMatch
                {
                    Method = method,
                    Reason = $"Semantic match: {semantics.Action}+{semantics.Target}",
                    SuggestedFix = $"Method semantically matches: '{methodName}' -> '{method.Name}'",
                    Diagnostic = $"Matched action={semantics.Action}, target={semantics.Target}"
                };
            }
        }

        return null;
    }

    private class MethodSemantics
    {
        public string Action { get; set; } // "Get", "Set", "Dec", "Add", "Remove", etc.
        public string Target { get; set; } // "Item", "Count", "Value", etc.
        public bool ReturnsCount { get; set; }
        public bool TakesItemValue { get; set; }
        public bool ModifiesState { get; set; }
    }

    private static MethodSemantics AnalyzeMethodSemantics(string methodName, Type[] parameterTypes, Type returnType)
    {
        var semantics = new MethodSemantics();
        var lowerName = methodName.ToLowerInvariant();

        // Analyze action from name
        if (lowerName.StartsWith("get") || lowerName.Contains("count") || lowerName.Contains("find"))
            semantics.Action = "Get";
        else if (lowerName.StartsWith("dec") || lowerName.Contains("remove") || lowerName.Contains("subtract"))
            semantics.Action = "Decrease";
        else if (lowerName.StartsWith("add") || lowerName.Contains("inc"))
            semantics.Action = "Increase";
        else if (lowerName.StartsWith("set") || lowerName.Contains("update"))
            semantics.Action = "Set";
        else
            return null; // Can't determine action

        // Analyze target from name
        if (lowerName.Contains("item"))
            semantics.Target = "Item";
        else if (lowerName.Contains("ammo"))
            semantics.Target = "Ammo";
        else if (lowerName.Contains("fuel"))
            semantics.Target = "Fuel";
        else if (lowerName.Contains("count"))
            semantics.Target = "Count";
        else
            semantics.Target = "Unknown";

        // Analyze from types
        if (returnType == typeof(int) || returnType == typeof(long))
            semantics.ReturnsCount = true;

        if (parameterTypes != null)
        {
            semantics.TakesItemValue = parameterTypes.Any(t =>
                t.Name.Contains("ItemValue") || t.Name.Contains("ItemStack"));

            semantics.ModifiesState = semantics.Action == "Decrease" ||
                                      semantics.Action == "Increase" ||
                                      semantics.Action == "Set";
        }

        return semantics;
    }

    private static bool SemanticsMatch(MethodSemantics expected, MethodSemantics actual)
    {
        // Actions must match
        if (expected.Action != actual.Action)
            return false;

        // Targets should be compatible
        if (expected.Target != "Unknown" && actual.Target != "Unknown" &&
            expected.Target != actual.Target)
            return false;

        // Return type semantics should match
        if (expected.ReturnsCount != actual.ReturnsCount)
            return false;

        // Item handling should match
        if (expected.TakesItemValue != actual.TakesItemValue)
            return false;

        return true;
    }

    private static SemanticMatch ScanILForSimilarCalls(List<CodeInstruction> codes, Type targetType, string methodName, Type[] parameterTypes)
    {
        // Look for calls in the IL that match our semantic profile
        var expectedSemantics = AnalyzeMethodSemantics(methodName, parameterTypes, null);
        if (expectedSemantics == null)
            return null;

        for (int i = 0; i < codes.Count; i++)
        {
            if ((codes[i].opcode == OpCodes.Call || codes[i].opcode == OpCodes.Callvirt) &&
                codes[i].operand is MethodInfo method)
            {
                // Check if this method is from the same type hierarchy
                if (!IsTypeCompatible(method.DeclaringType, targetType) &&
                    !IsTypeCompatible(targetType, method.DeclaringType))
                    continue;

                var methodSemantics = AnalyzeMethodSemantics(method.Name,
                    method.GetParameters().Select(p => p.ParameterType).ToArray(),
                    method.ReturnType);

                if (methodSemantics != null && SemanticsMatch(expectedSemantics, methodSemantics))
                {
                    return new SemanticMatch
                    {
                        Method = method,
                        Reason = "ILSemanticMatch",
                        SuggestedFix = $"Found semantically similar call in IL: {method.DeclaringType?.Name}.{method.Name}",
                        Diagnostic = $"IL index {i}, action={expectedSemantics.Action}"
                    };
                }
            }
        }

        return null;
    }

    #endregion

    #region Public Diagnostic Helpers

    /// <summary>
    /// Dumps all methods in a type that might be relevant, for debugging.
    /// </summary>
    public static void DumpTypeMethods(Type type, string filter = null)
    {
        if (ProxiCraft.Config?.isDebug != true)
            return;

        ProxiCraft.LogDebug($"=== Methods in {type.FullName} ===");

        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.Instance | BindingFlags.Static);

        foreach (var method in methods.OrderBy(m => m.Name))
        {
            if (filter != null && !method.Name.ToLowerInvariant().Contains(filter.ToLowerInvariant()))
                continue;

            var paramStr = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
            ProxiCraft.LogDebug($"  {method.ReturnType.Name} {method.Name}({paramStr})");
        }

        ProxiCraft.LogDebug($"=== End ===");
    }

    /// <summary>
    /// Reports the status of all adaptive recoveries for diagnostics.
    /// </summary>
    public static string GetRecoveryReport()
    {
        // This could be expanded to track all recoveries in a session
        return "Adaptive method finder active. Enable debug logging for detailed recovery reports.";
    }

    #endregion
}
