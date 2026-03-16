using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProxiCraft;

/// <summary>
/// Wraps the game's land claim data for ProxiCraft's "same claim" restriction feature.
///
/// DESIGN: Instead of calling World.GetLandClaimOwner() twice independently and checking
/// that both the player and container are in "any" accessible claim, this helper builds a
/// list of all accessible claim block positions once and then verifies that a single claim
/// block covers BOTH the player AND the container — enforcing the "same claim" requirement.
///
/// HOW THE GAME'S CLAIM SYSTEM WORKS (from World.cs source):
///   A land claim stone at position B covers position P when:
///     Math.Abs(B.x - P.x) &lt;= (LandClaimSize - 1) / 2
///     Math.Abs(B.z - P.z) &lt;= (LandClaimSize - 1) / 2   (Y axis is NOT checked)
///   "Accessible" means the local player owns the stone (Self) or is in the owner's ACL (Ally).
///
/// PERFORMANCE MODEL:
///   Expensive step: building the accessible claim blocks list (enumerates all player PPDs).
///     Cached for CLAIM_CACHE_DURATION (5s). Called once per rebuild cycle, not per container.
///   Cheap step: per-container same-claim check — O(claim_count) abs() comparisons.
///     No separate cache needed; ~4 integer comparisons per accessible claim block (1-5 typical).
///   Claim radius: cached once at game load — LandClaimSize cannot change mid-session.
///
/// CALLER PATTERN (ContainerManager prefilter):
///   var localPPD         = anyClaimRestriction ? LandClaimHelper.GetLocalPPD() : null;
///   var accessibleClaims = anyClaimRestriction ? LandClaimHelper.GetAccessibleClaimBlocks(localPPD) : null;
///   int claimRadius      = anyClaimRestriction ? LandClaimHelper.GetClaimRadius() : 0;
///   var playerBlockPos   = new Vector3i(playerPos);
///   bool playerInClaim   = LandClaimHelper.IsPlayerInAnyClaim(playerBlockPos, accessibleClaims, claimRadius);
///   // Per container:
///   bool ok = LandClaimHelper.IsContainerInSameClaim(playerBlockPos, containerBlockPos, accessibleClaims, claimRadius);
/// </summary>
public static class LandClaimHelper
{
    private const float PPD_CACHE_DURATION   = 5f;    // PersistentPlayerData reference rarely changes
    private const float CLAIM_CACHE_DURATION = 0.5f;  // Accessible claim blocks list — refresh every 500ms

    // PPD cache (GetLocalPPD)
    private static float _lastPPDTime = -1f;
    private static PersistentPlayerData _cachedPPD;

    // Accessible claim blocks cache (GetAccessibleClaimBlocks)
    private static float _lastClaimBlocksTime = -1f;
    private static PersistentPlayerData _lastClaimBlocksPPD;
    private static List<Vector3i> _cachedClaimBlocks;

    // Claim radius cache (GetClaimRadius) — set once at game load, never changes during a session
    private static int _cachedClaimRadius = -1;

    // Returned when localPPD is null — never null, never modified
    private static readonly List<Vector3i> _emptyClaimList = new List<Vector3i>();

    /// <summary>
    /// Clears all caches. Call on config reload and game start/stop.
    /// </summary>
    public static void ResetCache()
    {
        _lastPPDTime         = -1f;
        _cachedPPD           = null;
        _lastClaimBlocksTime = -1f;
        _lastClaimBlocksPPD  = null;
        _cachedClaimBlocks   = null;
        _cachedClaimRadius   = -1;
    }

    /// <summary>
    /// Gets the local player's PersistentPlayerData, cached for 5 seconds.
    /// Returns null if game not fully loaded.
    /// </summary>
    public static PersistentPlayerData GetLocalPPD()
    {
        if (_cachedPPD == null || Time.time - _lastPPDTime > PPD_CACHE_DURATION)
        {
            _cachedPPD   = GameManager.Instance?.GetPersistentLocalPlayer();
            _lastPPDTime = Time.time;
        }
        return _cachedPPD;
    }

    /// <summary>
    /// Returns the land claim coverage radius in blocks.
    /// Coverage radius = (LandClaimSize - 1) / 2  (matches game's World.GetLandClaimOwner).
    /// Cached once at game load — LandClaimSize is set at world creation and never changes mid-session.
    /// </summary>
    public static int GetClaimRadius()
    {
        if (_cachedClaimRadius < 0)
            _cachedClaimRadius = (GameStats.GetInt(EnumGameStats.LandClaimSize) - 1) / 2;
        return _cachedClaimRadius;
    }

    /// <summary>
    /// Builds and caches (5s) the list of all land claim block positions accessible to
    /// the local player: their own claim blocks (Self) plus any claim blocks whose owner
    /// has added the local player to their ACL (Ally).
    ///
    /// Returns an empty list (never null) if localPPD is null or on error.
    /// Do not modify the returned list — it is the cached instance.
    /// </summary>
    public static List<Vector3i> GetAccessibleClaimBlocks(PersistentPlayerData localPPD)
    {
        if (localPPD == null)
            return _emptyClaimList;

        if (_cachedClaimBlocks != null &&
            _lastClaimBlocksPPD == localPPD &&
            Time.time - _lastClaimBlocksTime <= CLAIM_CACHE_DURATION)
        {
            PerformanceProfiler.RecordCacheHit(PerformanceProfiler.OP_LAND_CLAIM_CHECK);
            return _cachedClaimBlocks;
        }

        PerformanceProfiler.RecordCacheMiss(PerformanceProfiler.OP_LAND_CLAIM_CHECK);

        var result = new List<Vector3i>();

        try
        {
            // Own claim blocks
            var ownBlocks = localPPD.GetLandProtectionBlocks();
            if (ownBlocks != null)
                result.AddRange(ownBlocks);

            // Ally claim blocks — other players who have the local player in their ACL
            var playerList = GameManager.Instance?.GetPersistentPlayerList();
            if (playerList?.Players != null)
            {
                foreach (var kvp in playerList.Players)
                {
                    var ppd = kvp.Value;
                    if (ppd == localPPD) continue;
                    if (ppd.ACL == null || !ppd.ACL.Contains(localPPD.PrimaryId)) continue;

                    var allyBlocks = ppd.GetLandProtectionBlocks();
                    if (allyBlocks != null)
                        result.AddRange(allyBlocks);
                }
            }
        }
        catch (Exception ex)
        {
            ProxiCraft.LogWarning($"LandClaimHelper.GetAccessibleClaimBlocks: {ex.Message}");
            // Return whatever was collected before the error
        }

        _cachedClaimBlocks   = result;
        _lastClaimBlocksPPD  = localPPD;
        _lastClaimBlocksTime = Time.time;
        return result;
    }

    /// <summary>
    /// Returns true if the player's block position is covered by at least one accessible
    /// claim block. Used as the source-level prefilter: if false, entire claim-restricted
    /// source scans are skipped.
    ///
    /// Cost: O(claim_count) integer abs() comparisons on the pre-built cached list.
    /// </summary>
    public static bool IsPlayerInAnyClaim(Vector3i playerBlockPos, List<Vector3i> accessibleClaims, int radius)
    {
        if (accessibleClaims == null || accessibleClaims.Count == 0)
            return false;

        foreach (var claimPos in accessibleClaims)
        {
            if (Math.Abs(claimPos.x - playerBlockPos.x) <= radius &&
                Math.Abs(claimPos.z - playerBlockPos.z) <= radius)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns true if a single accessible claim block covers BOTH the player's position
    /// AND the container's position — enforcing the "same claim" requirement.
    ///
    /// Call only after IsPlayerInAnyClaim() returns true (the prefilter handles the
    /// source-level skip so this is only reached when the player is in some claim).
    ///
    /// Returns true (permissive) if accessibleClaims is null — don't silently block item
    /// access when the claim list couldn't be built.
    /// </summary>
    public static bool IsContainerInSameClaim(Vector3i playerBlockPos, Vector3i containerBlockPos,
                                               List<Vector3i> accessibleClaims, int radius)
    {
        if (accessibleClaims == null) return true; // permissive: list unavailable

        foreach (var claimPos in accessibleClaims)
        {
            if (Math.Abs(claimPos.x - playerBlockPos.x) <= radius &&
                Math.Abs(claimPos.z - playerBlockPos.z) <= radius &&
                Math.Abs(claimPos.x - containerBlockPos.x) <= radius &&
                Math.Abs(claimPos.z - containerBlockPos.z) <= radius)
                return true;
        }
        return false;
    }
}
