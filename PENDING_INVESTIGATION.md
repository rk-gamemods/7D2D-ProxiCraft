# ProxiCraft - Pending Investigation Items

Internal tracking document for unresolved issues and deferred hypotheses.
**Not for user documentation** - this is for development review.

---

## v1.2.9 MP Container Crash - Unverified Hypotheses

**Status:** Defensive fixes applied, awaiting user feedback

### Applied Fixes (v1.2.9)
1. ✅ Added `IsModAllowed()` check to TELockServer/TEUnlockServer patches
2. ✅ Added defensive try-catch for dictionary access in BroadcastLockState
3. ✅ Added defensive try-catch for dictionary access in RetryBroadcastLock
4. ✅ Added throttled diagnostic logging (`[MP-Safety]` prefix)

### Still Unknown
These could still be the actual cause if v1.2.9 doesn't resolve the issue:

| # | Hypothesis | Confidence | Why Deferred |
|---|-----------|------------|--------------|
| 1 | `(ITileEntity)(object)tileEntity` double-cast pattern | MEDIUM | Unusual but used elsewhere without issues. Would need stack trace to confirm. |
| 2 | Race condition in packet deserialization | LOW | No evidence in logs. Would need server crash log. |
| 3 | Unity main thread violation | LOW | All patches run on main thread. Unlikely. |

### Required to Verify
- **Server `output_log.txt`** at time of crash - would show exact exception and stack trace
- **`pc_debug.log`** from server - would show `[MP-Safety]` messages if hypothesis #1 was correct
- **Reproduction steps** - specific timing (e.g., "crash happens within 5 sec of client joining")

---

## Other Pending Items

### Code Quality
- [ ] Review all dictionary accesses to `lockedTileEntities` for consistency
  - ContainerManager.cs L811: `foreach (var kvp in lockedTileEntities)` - has try-catch wrapper but no snapshot
  - ContainerManager.cs L2523: `lockedTileEntities.ContainsKey()` - has try-catch wrapper
  - ContainerManager.cs L2563: `foreach (var kvp in lockedTileEntities)` - has try-catch wrapper but no snapshot
  - Note: These are less risky than ProxiCraft.cs because they're wrapped in try-catch, but iteration without snapshot could still throw
- [ ] Consider adding a thread-safe wrapper for `lockedTileEntities` access
- [ ] Audit other network patches for missing `IsModAllowed()` checks

### Documentation
- [ ] Add multiplayer architecture diagram to TECHNICAL_REFERENCE.md
- [ ] Document the "Guilty Until Proven Innocent" safety model more clearly

### Testing
- [ ] Create multiplayer test scenario documentation
- [ ] Add timing-based tests for early connection window behavior

---

## Resolution Log

| Date | Version | Issue | Resolution |
|------|---------|-------|------------|
| 2026-01-14 | v1.2.9 | MP container crash | Defensive fixes applied, awaiting feedback |

---

*Last updated: 2026-01-14*
