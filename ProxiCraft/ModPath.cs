using System;
using System.IO;
using System.Reflection;

namespace ProxiCraft;

/// <summary>
/// Centralized mod path detection for ProxiCraft.
/// Handles various hosting environments including:
/// - Standard game installation
/// - CubeCoders AMP server panel
/// - Pterodactyl/Pelican panels
/// - Docker containers
/// - Other managed hosting services
/// 
/// The challenge: Assembly.GetExecutingAssembly().Location can return empty string in:
/// - .NET 5+ bundled/single-file assemblies
/// - Shadow-copied assemblies
/// - Some managed hosting environments
/// 
/// This module provides multiple fallback strategies to reliably find the mod folder.
/// </summary>
public static class ModPath
{
    private static string _cachedModFolder;
    private static string _cachedLogPath;
    private static bool _initialized;
    private static string _detectionMethod; // For diagnostics

    /// <summary>
    /// Gets the absolute path to the mod's folder.
    /// This is where config.json, log files, and other mod assets are located.
    /// Thread-safe and cached after first call.
    /// </summary>
    public static string ModFolder
    {
        get
        {
            if (!_initialized)
                Initialize();
            return _cachedModFolder;
        }
    }

    /// <summary>
    /// Gets the detection method used to find the mod folder.
    /// Useful for diagnostics when troubleshooting path issues.
    /// </summary>
    public static string DetectionMethod
    {
        get
        {
            if (!_initialized)
                Initialize();
            return _detectionMethod;
        }
    }

    /// <summary>
    /// Gets the path for the debug log file.
    /// </summary>
    public static string DebugLogPath
    {
        get
        {
            if (_cachedLogPath == null)
                _cachedLogPath = Path.Combine(ModFolder, "pc_debug.log");
            return _cachedLogPath;
        }
    }

    /// <summary>
    /// Gets the path for the config.json file.
    /// </summary>
    public static string ConfigPath => Path.Combine(ModFolder, "config.json");

    /// <summary>
    /// Gets the path for the network diagnostics log.
    /// </summary>
    public static string NetworkLogPath => Path.Combine(ModFolder, "network_log.txt");

    /// <summary>
    /// Gets a path to a file within the mod folder.
    /// </summary>
    /// <param name="filename">The filename (e.g., "fullcheck_report.txt")</param>
    /// <returns>Full path to the file</returns>
    public static string GetFilePath(string filename) => Path.Combine(ModFolder, filename);

    /// <summary>
    /// Initialize mod path detection. Called automatically on first access.
    /// Can also be called explicitly during mod startup for early initialization.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized)
            return;

        _cachedModFolder = DetectModFolder();
        _initialized = true;
    }

    /// <summary>
    /// Force re-detection of the mod folder.
    /// Useful if the mod has been moved or for testing.
    /// </summary>
    public static void Reset()
    {
        _initialized = false;
        _cachedModFolder = null;
        _cachedLogPath = null;
        _detectionMethod = null;
    }

    /// <summary>
    /// Validates that the mod folder exists and contains expected files.
    /// </summary>
    /// <returns>True if the mod folder appears valid</returns>
    public static bool ValidateModFolder()
    {
        if (string.IsNullOrEmpty(_cachedModFolder))
            return false;

        if (!Directory.Exists(_cachedModFolder))
            return false;

        // Check for the mod DLL as a sanity check
        string dllPath = Path.Combine(_cachedModFolder, "ProxiCraft.dll");
        return File.Exists(dllPath);
    }

    /// <summary>
    /// Gets diagnostic information about path detection.
    /// Useful for troubleshooting hosting environment issues.
    /// </summary>
    public static string GetDiagnosticInfo()
    {
        if (!_initialized)
            Initialize();

        return $"ModPath Diagnostics:\n" +
               $"  Detected Path: {_cachedModFolder}\n" +
               $"  Detection Method: {_detectionMethod}\n" +
               $"  Path Valid: {ValidateModFolder()}\n" +
               $"  Config Exists: {File.Exists(ConfigPath)}\n" +
               $"  Assembly.Location: {GetAssemblyLocation()}\n" +
               $"  Assembly.CodeBase: {GetAssemblyCodeBase()}\n" +
               $"  Current Directory: {Directory.GetCurrentDirectory()}\n" +
               $"  AppDomain.BaseDirectory: {AppDomain.CurrentDomain.BaseDirectory}";
    }

    #region Private Methods

    private static string DetectModFolder()
    {
        // Strategy 1: Assembly.Location (works in most cases)
        string path = TryAssemblyLocation();
        if (IsValidModPath(path))
        {
            _detectionMethod = "Assembly.Location";
            return path;
        }

        // Strategy 2: Assembly.CodeBase (deprecated but works in some cases)
        path = TryAssemblyCodeBase();
        if (IsValidModPath(path))
        {
            _detectionMethod = "Assembly.CodeBase";
            return path;
        }

        // Strategy 3: Search game Mods folder using GameIO
        path = TryGameIOSearch();
        if (IsValidModPath(path))
        {
            _detectionMethod = "GameIO.Mods";
            return path;
        }

        // Strategy 4: Search relative to AppDomain base directory
        path = TryAppDomainSearch();
        if (IsValidModPath(path))
        {
            _detectionMethod = "AppDomain.BaseDirectory";
            return path;
        }

        // Strategy 5: Search relative to current directory
        path = TryCurrentDirectorySearch();
        if (IsValidModPath(path))
        {
            _detectionMethod = "CurrentDirectory";
            return path;
        }

        // Strategy 6: Environment variable (for custom hosting setups)
        path = TryEnvironmentVariable();
        if (IsValidModPath(path))
        {
            _detectionMethod = "Environment.PROXICRAFT_PATH";
            return path;
        }

        // Fallback: Use current directory and log a warning
        _detectionMethod = "Fallback (CurrentDirectory)";
        UnityEngine.Debug.LogWarning(
            $"[ProxiCraft] Could not reliably detect mod folder. Using current directory: {Directory.GetCurrentDirectory()}. " +
            "This may cause issues. Set PROXICRAFT_PATH environment variable to override.");
        
        return Directory.GetCurrentDirectory();
    }

    private static string TryAssemblyLocation()
    {
        try
        {
            string location = Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(location))
            {
                string dir = Path.GetDirectoryName(location);
                if (!string.IsNullOrEmpty(dir))
                    return dir;
            }
        }
        catch (Exception)
        {
            // Swallow - will try next method
        }
        return null;
    }

    private static string TryAssemblyCodeBase()
    {
        try
        {
            // CodeBase is deprecated in .NET Core but may work in Unity/Mono
            #pragma warning disable SYSLIB0012
            string codeBase = Assembly.GetExecutingAssembly().CodeBase;
            #pragma warning restore SYSLIB0012
            
            if (!string.IsNullOrEmpty(codeBase))
            {
                // CodeBase returns a URI like "file:///C:/path/to/mod"
                var uri = new Uri(codeBase);
                string localPath = uri.LocalPath;
                if (!string.IsNullOrEmpty(localPath))
                {
                    string dir = Path.GetDirectoryName(localPath);
                    if (!string.IsNullOrEmpty(dir))
                        return dir;
                }
            }
        }
        catch (Exception)
        {
            // Swallow - will try next method
        }
        return null;
    }

    private static string TryGameIOSearch()
    {
        try
        {
            // Primary: Check UserGameDataDir (AppData location - where game prefers mods)
            // Windows: %AppData%/7DaysToDie/Mods
            // Linux: ~/.local/share/7DaysToDie/Mods  
            // Mac: ~/Library/Application Support/7DaysToDie/Mods
            try
            {
                string userDataDir = GameIO.GetUserGameDataDir();
                if (!string.IsNullOrEmpty(userDataDir))
                {
                    string userModsPath = Path.Combine(userDataDir, "Mods", "ProxiCraft");
                    if (Directory.Exists(userModsPath))
                        return userModsPath;
                }
            }
            catch { /* UserGameDataDir may not be initialized yet */ }

            // Secondary: Check game install directory (legacy location)
            string gameDir = GameIO.GetApplicationPath();
            if (!string.IsNullOrEmpty(gameDir))
            {
                string modsPath = Path.Combine(gameDir, "Mods", "ProxiCraft");
                if (Directory.Exists(modsPath))
                    return modsPath;
            }
        }
        catch (Exception)
        {
            // GameIO may not be available yet during early init
        }
        return null;
    }

    private static string TryAppDomainSearch()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrEmpty(baseDir))
            {
                // Try direct Mods folder
                string modsPath = Path.Combine(baseDir, "Mods", "ProxiCraft");
                if (Directory.Exists(modsPath))
                    return modsPath;

                // Try parent directory (in case working dir is a subdirectory)
                string parentPath = Path.Combine(Directory.GetParent(baseDir)?.FullName ?? "", "Mods", "ProxiCraft");
                if (Directory.Exists(parentPath))
                    return parentPath;

                // Try two levels up (common in some hosting setups)
                var grandParent = Directory.GetParent(baseDir)?.Parent;
                if (grandParent != null)
                {
                    string grandParentPath = Path.Combine(grandParent.FullName, "Mods", "ProxiCraft");
                    if (Directory.Exists(grandParentPath))
                        return grandParentPath;
                }
            }
        }
        catch (Exception)
        {
            // Swallow - will try next method
        }
        return null;
    }

    private static string TryCurrentDirectorySearch()
    {
        try
        {
            string currentDir = Directory.GetCurrentDirectory();
            
            // Try direct Mods folder
            string modsPath = Path.Combine(currentDir, "Mods", "ProxiCraft");
            if (Directory.Exists(modsPath))
                return modsPath;

            // Try parent directory
            var parent = Directory.GetParent(currentDir);
            if (parent != null)
            {
                string parentPath = Path.Combine(parent.FullName, "Mods", "ProxiCraft");
                if (Directory.Exists(parentPath))
                    return parentPath;
            }
        }
        catch (Exception)
        {
            // Swallow
        }
        return null;
    }

    private static string TryEnvironmentVariable()
    {
        try
        {
            // Allow hosting services to explicitly set the mod path
            string envPath = Environment.GetEnvironmentVariable("PROXICRAFT_PATH");
            if (!string.IsNullOrEmpty(envPath) && Directory.Exists(envPath))
                return envPath;
        }
        catch (Exception)
        {
            // Swallow
        }
        return null;
    }

    private static bool IsValidModPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        if (!Directory.Exists(path))
            return false;

        // Check for ProxiCraft.dll as validation
        string dllPath = Path.Combine(path, "ProxiCraft.dll");
        return File.Exists(dllPath);
    }

    private static string GetAssemblyLocation()
    {
        try
        {
            return Assembly.GetExecutingAssembly().Location ?? "(null)";
        }
        catch
        {
            return "(error)";
        }
    }

    private static string GetAssemblyCodeBase()
    {
        try
        {
            #pragma warning disable SYSLIB0012
            return Assembly.GetExecutingAssembly().CodeBase ?? "(null)";
            #pragma warning restore SYSLIB0012
        }
        catch
        {
            return "(error)";
        }
    }

    #endregion
}
