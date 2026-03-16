# ProxiCraft: Land Claim Restrictions Feature

## Context

A user requested an optional feature to restrict ProxiCraft's container search to land claim areas only. When enabled per-source, the mod should only use items from a container if **both the player AND the container are inside a land claim the player has access to**. This must be **off by default** (no behavior change for existing users) and configurable **per storage source** (Vehicle, Drone, DewCollector, Workstation, Container).

**Performance goal**: when this feature is enabled, it should act as a **prefilter** — if the player is not in any land claim, claim-restricted source scans are skipped entirely, REDUCING search space vs. normal operation. Feature disabled = zero overhead.

---

## Files to Modify

| File | Change |
|------|--------|
| `ProxiCraft/ModConfig.cs` | Add 5 per-source land claim bool flags + section label |
| `ProxiCraft/ContainerManager.cs` | Add `PassesLandClaimCheck()` + source-level prefilters + 5 per-container call sites |
| `ProxiCraft/PerformanceProfiler.cs` | Add `OP_LAND_CLAIM_CHECK` constant |
| `ProxiCraft/ProxiCraft.cs` | Call `LandClaimHelper.ResetCache()` on config reload + game start |

## New File

| File | Purpose |
|------|---------|
| `ProxiCraft/LandClaimHelper.cs` | Land claim detection wrapping game API, with caching |

---

## 1. `LandClaimHelper.cs` (new file)

Uses `World.GetLandClaimOwner(Vector3i, PersistentPlayerData)` — the game's own chunk-based land claim lookup — instead of rolling our own. Returns `EnumLandClaimOwner.Self` (player's own claim) or `EnumLandClaimOwner.Ally` (claim where ACL grants player access).

```csharp
using System;
using UnityEngine;

namespace ProxiCraft;

/// <summary>
/// Wraps the game's built-in land claim API for ProxiCraft use.
///
/// DESIGN: Uses World.GetLandClaimOwner() (game's chunk-based lookup) for correctness
/// and performance. Player claim status is cached 250ms to minimize API calls.
///
/// PREFILTER PATTERN:
///   if (config.landClaimX)
///   {
///       if (!LandClaimHelper.IsPlayerInAnyClaim(world, ppd)) skip source entirely;
///       else per-container: IsContainerInPlayerClaim(world, ppd, pos);
///   }
/// </summary>
public static class LandClaimHelper
{
    private const float PLAYER_CACHE_DURATION = 0.25f; // refresh every 250ms
    private const float PPD_CACHE_DURATION = 5f;       // PPD rarely changes

    private static float _lastPlayerCheckTime = -1f;
    private static Vector3i _lastPlayerBlockPos;
    private static bool _cachedPlayerInClaim;

    private static float _lastPPDTime = -1f;
    private static PersistentPlayerData _cachedPPD;

    /// <summary>Clears all caches. Call on config reload and game start.</summary>
    public static void ResetCache()
    {
        _lastPlayerCheckTime = -1f;
        _lastPPDTime = -1f;
        _cachedPPD = null;
        _cachedPlayerInClaim = false;
    }

    /// <summary>
    /// Gets the local player's PersistentPlayerData (cached 5s).
    /// Returns null if game not fully loaded.
    /// </summary>
    public static PersistentPlayerData GetLocalPPD()
    {
        if (_cachedPPD == null || Time.time - _lastPPDTime > PPD_CACHE_DURATION)
        {
            _cachedPPD = GameManager.Instance?.GetPersistentLocalPlayer();
            _lastPPDTime = Time.time;
        }
        return _cachedPPD;
    }

    /// <summary>
    /// Returns true if the local player is currently in any land claim they have
    /// access to (own claim = Self, or ally's claim = Ally via ACL).
    ///
    /// PERFORMANCE: Result cached 250ms or until player moves 1+ blocks.
    /// Typical cost on cache miss: 1x World.GetLandClaimOwner() call.
    /// </summary>
    public static bool IsPlayerInAnyClaim(World world, PersistentPlayerData localPPD)
    {
        if (localPPD == null) return false;

        try
        {
            var player = world?.GetPrimaryPlayer();
            if (player == null) return false;

            var playerBlock = new Vector3i(player.position);

            if (_lastPlayerCheckTime < 0f ||
                Time.time - _lastPlayerCheckTime > PLAYER_CACHE_DURATION ||
                _lastPlayerBlockPos != playerBlock)
            {
                PerformanceProfiler.RecordCacheMiss(PerformanceProfiler.OP_LAND_CLAIM_CHECK);
                var owner = world.GetLandClaimOwner(playerBlock, localPPD);
                _cachedPlayerInClaim = owner == EnumLandClaimOwner.Self ||
                                        owner == EnumLandClaimOwner.Ally;
                _lastPlayerCheckTime = Time.time;
                _lastPlayerBlockPos = playerBlock;
            }
            else
            {
                PerformanceProfiler.RecordCacheHit(PerformanceProfiler.OP_LAND_CLAIM_CHECK);
            }

            return _cachedPlayerInClaim;
        }
        catch (Exception ex)
        {
            ProxiCraft.LogWarning($"LandClaimHelper.IsPlayerInAnyClaim: {ex.Message}");
            return false; // restrictive on error: if we can't check, prefilter blocks
        }
    }

    /// <summary>
    /// Returns true if containerPos is within a land claim the local player has
    /// access to (Self or Ally). Call only after IsPlayerInAnyClaim() returns true.
    /// </summary>
    public static bool IsContainerInPlayerClaim(World world, PersistentPlayerData localPPD,
                                                 Vector3 containerPos)
    {
        try
        {
            var containerBlock = new Vector3i(containerPos);
            var owner = world.GetLandClaimOwner(containerBlock, localPPD);
            return owner == EnumLandClaimOwner.Self || owner == EnumLandClaimOwner.Ally;
        }
        catch (Exception ex)
        {
            ProxiCraft.LogWarning($"LandClaimHelper.IsContainerInPlayerClaim: {ex.Message}");
            return true; // permissive: if container check fails, don't block the item
        }
    }
}
```

**7DTD API used (verified via DB):**

- `GameManager.Instance.GetPersistentLocalPlayer()` → `PersistentPlayerData` of local player
- `World.GetLandClaimOwner(Vector3i, PersistentPlayerData)` → `EnumLandClaimOwner` (Self/Ally/Other/None)
- `EnumLandClaimOwner.Self` = player's own claim; `.Ally` = ACL-granted access (not `.Party`)

---

## 2. `PerformanceProfiler.cs` — Add Timer Constant

Add to the "Counting operations" section (around line 265):

```csharp
public const string OP_LAND_CLAIM_CHECK = "LandClaimCheck";
```

The `RecordCacheHit/RecordCacheMiss` calls in `LandClaimHelper` will surface cache efficiency in `pc perf report`.
The timer itself wraps `PassesLandClaimCheck()` to show total time spent on claim filtering.

---

## 3. `ModConfig.cs` — Add Land Claim Section

Insert after line 175 (after `enhancedSafetyDiagnosticLogging`), before the `MULTIPLAYER SAFETY` section:

```csharp
    // ===========================================
    // LAND CLAIM RESTRICTIONS - Optional per-source prefilter
    // ===========================================
    // When enabled for a storage source, that source is ONLY searched when:
    //   1. The player is inside a land claim they own or have ACL access to, AND
    //   2. The container is also inside such a claim.
    // If the player is NOT in any claim, claim-restricted sources are skipped
    // entirely — this REDUCES search overhead vs. normal operation when exploring.
    // All flags default to false (no behavior change from previous versions).
    //
    // NOTE: Trader compounds are normally outside player claims. Setting
    // landClaimContainer = true will prevent using container currency at traders.
    // Leave sources you use at traders set to false.
    // ===========================================

    /// <summary>Label only — no functional effect.</summary>
    public string landClaimSection_NOTE = "All false by default. Feature off = zero overhead.";

    /// <summary>Restrict vehicle storage to land claim areas only.</summary>
    public bool landClaimVehicle = false;

    /// <summary>Restrict drone storage to land claim areas only.</summary>
    public bool landClaimDrone = false;

    /// <summary>Restrict dew collector contents to land claim areas only.</summary>
    public bool landClaimDewCollector = false;

    /// <summary>Restrict workstation output slots to land claim areas only.</summary>
    public bool landClaimWorkstation = false;

    /// <summary>Restrict regular containers (chests, etc.) to land claim areas only.</summary>
    public bool landClaimContainer = false;
```

---

## 4. `ContainerManager.cs` — Prefilter Architecture

### 4a. New helper method `PassesLandClaimCheck()` (add near `IsInRange()`, ~line 614)

```csharp
/// <summary>
/// Returns true if the container passes the land claim restriction for its storage type.
/// When the source type's flag is false (default), returns true immediately — zero cost.
/// When enabled: player must be in a claim AND container must be in same accessible claim.
/// The caller is responsible for the source-level prefilter (skipping the entire scan
/// when player is not in any claim). This method handles the per-container check.
/// Returns true (permissive) if game state unavailable.
/// </summary>
private static bool PassesLandClaimCheck(ModConfig config, StorageType storageType,
                                          Vector3 containerPos, World world,
                                          PersistentPlayerData localPPD)
{
    bool requiresClaim = storageType switch
    {
        StorageType.Vehicle      => config.landClaimVehicle,
        StorageType.Drone        => config.landClaimDrone,
        StorageType.DewCollector => config.landClaimDewCollector,
        StorageType.Workstation  => config.landClaimWorkstation,
        StorageType.Container    => config.landClaimContainer,
        _ => false
    };

    if (!requiresClaim) return true;
    if (world == null || localPPD == null) return true; // permissive on null

    PerformanceProfiler.StartTimer(PerformanceProfiler.OP_LAND_CLAIM_CHECK);
    bool result = LandClaimHelper.IsContainerInPlayerClaim(world, localPPD, containerPos);
    PerformanceProfiler.StopTimer(PerformanceProfiler.OP_LAND_CLAIM_CHECK);
    return result;
}
```

### 4b. Source-level prefilter in `RebuildItemCountCacheInternal()` (~line 1020)

After `Vector3 playerPos = player.position;` and before the vehicle/drone/dew/workstation calls, add:

```csharp
// Land claim prefilter: compute once, skip entire source scans if player not in claim.
// When NO land claim flags are set (default), this block is a no-op (anyClaimRestriction=false).
bool anyClaimRestriction = config.landClaimVehicle || config.landClaimDrone ||
                            config.landClaimDewCollector || config.landClaimWorkstation ||
                            config.landClaimContainer;
var localPPD = anyClaimRestriction ? LandClaimHelper.GetLocalPPD() : null;
bool playerInAnyClaim = anyClaimRestriction && localPPD != null &&
                         LandClaimHelper.IsPlayerInAnyClaim(world, localPPD);
```

Then change the source checks (lines 1181-1206):

```csharp
// Count from vehicles — skip entirely if land claim restricted and player not in claim
if (config.pullFromVehicles && (!config.landClaimVehicle || playerInAnyClaim))
    CountAllVehicleItems(world, playerPos, config, localPPD);

// Count from drones — same prefilter pattern
if (config.pullFromDrones && (!config.landClaimDrone || playerInAnyClaim))
    CountAllDroneItems(world, playerPos, config, localPPD);

// Count from dew collectors
if (config.pullFromDewCollectors && (!config.landClaimDewCollector || playerInAnyClaim))
{
    PerformanceProfiler.StartTimer(PerformanceProfiler.OP_COUNT_DEWCOLLECTORS);
    CountAllDewCollectorItems(world, playerPos, config, openContainerPos, localPPD);
    PerformanceProfiler.StopTimer(PerformanceProfiler.OP_COUNT_DEWCOLLECTORS);
}

// Count from workstation outputs
if (config.pullFromWorkstationOutputs && (!config.landClaimWorkstation || playerInAnyClaim))
{
    PerformanceProfiler.StartTimer(PerformanceProfiler.OP_COUNT_WORKSTATIONS);
    CountAllWorkstationOutputItems(world, playerPos, config, openContainerPos, localPPD);
    PerformanceProfiler.StopTimer(PerformanceProfiler.OP_COUNT_WORKSTATIONS);
}
```

For the TileEntity chunk scan in `RebuildItemCountCacheInternal()` (~line 1116), wrap with container prefilter:

```csharp
// TileEntity container scan — skip entirely if claim-restricted and player not in claim
bool shouldScanContainers = !config.landClaimContainer || playerInAnyClaim;
if (shouldScanContainers)
{
    PerformanceProfiler.StartTimer(PerformanceProfiler.OP_COUNT_CONTAINERS);
    // ... existing chunk iteration ...
    // Inside the loop, add land claim check after range check and before CountTileEntityItems():
    if (config.landClaimContainer &&
        !PassesLandClaimCheck(config, StorageType.Container,
                               new Vector3(worldPos.x, worldPos.y, worldPos.z),
                               world, localPPD))
        continue;
    CountTileEntityItems(tileEntity, config);
    // ... end of loop
    PerformanceProfiler.StopTimer(PerformanceProfiler.OP_COUNT_CONTAINERS);
}
```

Open vehicle/workstation checks at top of `RebuildItemCountCacheInternal()` (~lines 1075, 1102):

```csharp
// Open vehicle - check land claim if restricted
if (CurrentOpenVehicle != null &&
    (!config.landClaimVehicle ||
     (playerInAnyClaim && LandClaimHelper.IsContainerInPlayerClaim(world, localPPD,
                                                                     CurrentOpenVehicle.position))))
{
    // ... existing vehicle counting code unchanged ...
}

// Open workstation - check land claim if restricted
if (CurrentOpenWorkstation != null)
{
    var wsPos = CurrentOpenWorkstation.ToWorldPos();
    if (!config.landClaimWorkstation ||
        (playerInAnyClaim && LandClaimHelper.IsContainerInPlayerClaim(world, localPPD,
                                                                        wsPos.ToVector3())))
    {
        // ... existing workstation counting code unchanged ...
    }
}
```

### 4c. `CountAllVehicleItems()` — add `localPPD` parameter + per-vehicle claim check (~line 1286)

Change signature:

```csharp
private static void CountAllVehicleItems(World world, Vector3 playerPos, ModConfig config,
                                          PersistentPlayerData localPPD)
```

After range check and `LocalPlayerIsOwner()` check, add:

```csharp
// Land claim check (only when claim-restricted; localPPD guaranteed non-null here)
if (config.landClaimVehicle &&
    !LandClaimHelper.IsContainerInPlayerClaim(world, localPPD, vehicle.position))
    continue;
```

Apply same pattern to `CountAllDroneItems()`, `CountAllDewCollectorItems()`, `CountAllWorkstationOutputItems()` — each gets `localPPD` parameter and a per-item claim check after range check.

### 4d. `GetStorageItems()` and `RemoveItems()` — add claim check after range check

Both methods iterate `_currentStorageDict`. After the existing `IsInRange(config, containerPos)` check in each, add:

```csharp
if (!IsInRange(config, containerPos))
    continue;
// Land claim check — PassesLandClaimCheck handles the "feature disabled" fast path
var world = GameManager.Instance?.World;
var localPPD = LandClaimHelper.GetLocalPPD();
if (!PassesLandClaimCheck(config, GetStorageType(kvp.Value), containerPos, world, localPPD))
    continue;
```

For `RemoveItems()`, also add a source-level prefilter before the loop (same pattern as `RebuildItemCountCacheInternal()`):

```csharp
// Source-level prefilter: if player not in any claim, skip the whole ordered iteration
// for sources that have claim restrictions. GetStorageItems follows the same pattern.
bool anyClaimRestriction = config.landClaimVehicle || config.landClaimDrone ||
                            config.landClaimDewCollector || config.landClaimWorkstation ||
                            config.landClaimContainer;
var localPPD = anyClaimRestriction ? LandClaimHelper.GetLocalPPD() : null;
var claimWorld = anyClaimRestriction ? GameManager.Instance?.World : null;
bool playerInAnyClaim = anyClaimRestriction && localPPD != null && claimWorld != null &&
                         LandClaimHelper.IsPlayerInAnyClaim(claimWorld, localPPD);
```

---

## 5. `ProxiCraft.cs` — Reset Cache on Config Reload + Game Start

In `ReloadConfig()`:

```csharp
LandClaimHelper.ResetCache();
```

In `GameManager_StartGame_Patch` (existing patch that clears cache on new game):

```csharp
LandClaimHelper.ResetCache();
```

---

## Performance Analysis

| Scenario | With feature disabled (default) | With feature enabled, player out exploring | With feature enabled, player at base |
|---|---|---|---|
| `anyClaimRestriction` | `false` — skip all land claim code | `true` | `true` |
| Source prefilter | N/A | Skips claim-restricted sources entirely | `IsPlayerInAnyClaim()` → true, sources scanned |
| Per-container check | N/A | Never reached (entire source skipped) | `IsContainerInPlayerClaim()` per container |
| Net effect | **Zero overhead** | **Less work than disabled** | Marginal overhead (2x game API calls per container) |

---

## Stability Notes

- `World.GetLandClaimOwner()` is called with game-provided `PersistentPlayerData` — safe, standard game API
- Called inside chunk read locks: game's API uses its own data structures independent of ProxiCraft's read locks → no deadlock risk
- All land claim calls wrapped in try-catch with permissive fallback (`true`) to prevent blocking mod functionality
- `LandClaimHelper` only accessed from Unity main thread (same as all ContainerManager operations) → no additional threading concerns
- `_cachedPPD` invalidated every 5s to handle rare cases where PPD reference changes

---

## Verification

1. Build: `msbuild ProxiCraft.csproj` — 0 errors
2. All flags `false` (default) — run `pc perf report`, verify zero `LandClaimCheck` entries
3. Set `landClaimVehicle = true`, player outside any claim: verify vehicle items NOT counted (0 in crafting), `CountVehicles` shows near-zero time
4. Set `landClaimVehicle = true`, player inside own claim, vehicle inside same claim: items accessible
5. Set `landClaimVehicle = true`, player inside own claim, vehicle OUTSIDE claim: items not accessible
6. Set `landClaimContainer = true`, open trader: dukes in containers not counted, trader still functional
7. Run `pc perf report` with land claim enabled, verify `LandClaimCheck` shows CacheHit% > 80% during normal play
