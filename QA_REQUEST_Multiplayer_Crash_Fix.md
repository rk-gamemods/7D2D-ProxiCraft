# ProxiCraft QA Request: Multiplayer Crash Fix (v1.2.8)

## Issue Summary

**Bug Reports Received:**

1. **Report 1 (Russian translation):** "In multiplayer mode, the host gets kicked after the person joining the server opens containers (mod installed on both computers)"

2. **Report 2:** "I play with son and have same issue. Game will close without error when he opens inventory. There is no information in log why this happens."

**Last Log Entry Before Crash:**

```
[ProxiCraft] [Network] Handshake from 'PetarOG' latency: 6ms
[ProxiCraft] [Multiplayer] Player 'Unknown' confirmed ProxiCraft ✓ (Unverified remaining: 0)
[ProxiCraft] ======================================================================
[ProxiCraft] [Multiplayer] All clients verified - Mod RE-ENABLED
[ProxiCraft] ======================================================================
[ProxiCraft] [Multiplayer] Player 'PetarOG' joined with ProxiCraft v1.2.7
```

**Key Observations:**

- Crash occurs AFTER handshake completes successfully
- HOST crashes when CLIENT opens inventory/containers
- No error message in logs (suggests unhandled exception or native crash)
- Player name shows as "Unknown" despite handshake coming from "PetarOG" (timing issue)

---

## Root Cause Analysis

Three potential issues were identified:

### Issue #1: Entity ID Tracking Race Condition

**Location:** `MultiplayerModTracker.cs` - `OnClientHandshakeReceived()`

**Problem:** When a client's handshake packet arrives, the server looks up their entity ID in `_pendingClients` dictionary. If the handshake arrives before the player spawn event fires (race condition), the entity ID isn't found, and the player name defaults to "Unknown".

**Evidence:** Log shows `Player 'Unknown' confirmed` but handshake was from `PetarOG`.

### Issue #2: Network Broadcasting During Unstable Connection

**Location:** `ProxiCraft.cs` - `BroadcastLockState()`

**Problem:** When the mod re-enables after handshake, any container interaction triggers a network broadcast. If this happens within milliseconds of the handshake completing, the network connection may be in a transitional state, causing `SendPackage()` to crash.

### Issue #3: Dictionary Thread-Safety

**Location:** `ContainerManager.cs` - `_knownStorageDict`, `_currentStorageDict`

**Problem:** These were regular `Dictionary<>` objects accessed from multiple code paths (UI updates, network events). If a network event modifies the dictionary while the UI is iterating it, a "collection was modified during enumeration" exception occurs, potentially crashing the game.

---

## Changes Made

### 1. Flight Recorder System (NEW)

**File:** `FlightRecorder.cs` (new file)

**Purpose:** Crash diagnostics for multiplayer issues

**How It Works:**

- Maintains circular buffer of last 100 significant events
- Writes to main ProxiCraft log with `[FR]` tag every 5 seconds
- On clean shutdown, writes `[FR] === SESSION CLEAN EXIT ===` marker
- If crash occurs, `[FR]` entries remain in log without clean exit marker

**Log Format:**

```
[ProxiCraft] [FR] === SESSION START 2024-01-12 19:30:00 ===
[ProxiCraft] [FR] [19:30:03.456] [MP] CLIENT CONNECTING: PetarOG
[ProxiCraft] [FR] [19:30:03.457] [MP] MOD LOCKED - unverified clients: 1
[ProxiCraft] [FR] [19:30:06.789] [MP] Player 'PetarOG' verified - 0 remaining
[ProxiCraft] [FR] [19:30:06.790] [MP] All clients verified - MOD RE-ENABLED
[ProxiCraft] [FR] === SESSION CLEAN EXIT ===
```

**Testing:** Search log for `[FR]` to see flight recorder entries. Missing `CLEAN EXIT` marker indicates crash.

---

### 2. Entity ID Tracking Fix (Issue #1)

**File:** `MultiplayerModTracker.cs`

**Changes:**

- `OnClientHandshakeReceived()` now accepts optional `packetPlayerName` parameter
- If entity ID not found in `_pendingClients`, uses player name from handshake packet instead of "Unknown"
- Added FlightRecorder logging at key multiplayer events

**Before:**

```csharp
public static void OnClientHandshakeReceived(int entityId)
{
    string playerName = "Unknown";
    if (_pendingClients.TryGetValue(entityId, out var pendingInfo))
    {
        playerName = pendingInfo.PlayerName;
    }
    // Even if not found, used "Unknown"
}
```

**After:**

```csharp
public static void OnClientHandshakeReceived(int entityId, string packetPlayerName = null)
{
    string playerName = null;
    if (_pendingClients.TryGetValue(entityId, out var pendingInfo))
    {
        playerName = pendingInfo.PlayerName;
    }
    // Fallback to packet name if lookup fails
    if (string.IsNullOrEmpty(playerName))
    {
        playerName = packetPlayerName ?? "Unknown";
    }
}
```

**Expected Result:** Log should now show correct player name even during timing race conditions.

---

### 3. Early Connection Window (Issue #2)

**Files:** `MultiplayerModTracker.cs`, `ProxiCraft.cs`

**Changes:**

- Added `_modReenabledTime` timestamp tracking
- Added `IsInEarlyConnectionWindow()` method (returns true for 3 seconds after mod re-enable)
- `BroadcastLockState()` skips broadcasting during early window
- `SendPackage()` wrapped in try-catch with failure logging

**New Protection:**

```csharp
private static void BroadcastLockState(...)
{
    // CRASH PREVENTION: Skip during early connection window
    if (MultiplayerModTracker.IsInEarlyConnectionWindow())
    {
        LogDebug("Skipping broadcast - early connection window");
        return;
    }

    // Defensive try-catch around SendPackage
    try
    {
        connManager.SendPackage(...);
    }
    catch (Exception sendEx)
    {
        _lockBroadcastFailCount++;
        LogWarning($"Lock broadcast failed (total: {_lockBroadcastFailCount})");
        // Continue without crashing
    }
}
```

**Expected Result:**

- Container lock broadcasts skip for first 3 seconds after client verification
- If SendPackage fails for any reason, error is logged but game doesn't crash

---

### 4. Thread-Safe Dictionaries (Issue #3)

**Files:** `ContainerManager.cs`, `StoragePriority.cs`

**Changes:**

- Converted `_knownStorageDict` from `Dictionary<>` to `ConcurrentDictionary<>`
- Converted `_currentStorageDict` from `Dictionary<>` to `ConcurrentDictionary<>`
- Converted `_lockedPositions` from `HashSet<>` to `ConcurrentDictionary<,byte>`
- Converted `_lockTimestamps` and `_lockPacketTimestamps` to `ConcurrentDictionary<>`
- Updated `StoragePriority.OrderStorages()` to accept `IDictionary<>` interface
- Changed `.Remove()` calls to `.TryRemove()` for thread-safety

**Expected Result:** No more "collection was modified during enumeration" crashes during concurrent access.

---

## Testing Scenarios

### Scenario 1: Basic Multiplayer Join

**Setup:** Host starts game, client joins with ProxiCraft installed on both

**Steps:**

1. Host creates multiplayer game
2. Client joins server
3. Wait for handshake to complete
4. Client opens their inventory
5. Client opens a storage container
6. Client moves items between inventory and container

**Expected:**

- No crash on host or client
- Log shows correct player name (not "Unknown")
- Log shows `[FR]` entries for multiplayer events

**Verify in Log:**

```
[FR] [MP] CLIENT CONNECTING: <player_name>
[FR] [MP] Player '<player_name>' verified
[FR] [MP] All clients verified - MOD RE-ENABLED
```

---

### Scenario 2: Rapid Container Access After Join

**Purpose:** Test the "early connection window" protection

**Steps:**

1. Host creates multiplayer game
2. Client joins server
3. IMMEDIATELY after "All clients verified" message, client opens a container
4. Repeat 5 times with different containers

**Expected:**

- No crash
- May see log messages about skipping broadcasts during early window
- After 3 seconds, broadcasts should work normally

**Verify in Log:**

```
[Network] Skipping lock broadcast at <pos> - early connection window
```

(This message may or may not appear depending on timing)

---

### Scenario 3: Multiple Clients Joining

**Purpose:** Test concurrent client handling

**Steps:**

1. Host creates multiplayer game
2. Client A joins
3. While Client A is still connecting, Client B joins
4. Both clients open containers simultaneously

**Expected:**

- No crash
- Both clients verified correctly
- Log shows correct player names for both

---

### Scenario 4: Clean Exit Verification

**Purpose:** Test flight recorder clean exit marker

**Steps:**

1. Host creates multiplayer game
2. Client joins and performs some container operations
3. Client leaves game normally (not Alt+F4)
4. Host exits game normally

**Expected:**

- Log contains `[FR] === SESSION CLEAN EXIT ===` marker
- All `[FR]` entries present in log

---

### Scenario 5: Simulated Crash (if possible)

**Purpose:** Verify flight recorder captures crash data

**Steps:**

1. Start game, join multiplayer session
2. Perform some operations to generate `[FR]` entries
3. Force-quit game (Task Manager or Alt+F4)
4. Restart game and check log

**Expected:**

- Log from previous session has `[FR]` entries
- NO `[FR] === SESSION CLEAN EXIT ===` marker
- Next session starts with new `[FR] === SESSION START ===`

---

## Regression Testing

Verify these existing features still work:

1. **Single-player crafting** - Pull items from nearby containers
2. **Single-player reload** - Reload ammo from containers
3. **Recipe tracker** - Shows container items in ingredient counts
4. **Challenge tracking** - Quest objectives count container items
5. **Container locks** - Multiplayer container lock synchronization (after 3 second window)
6. **Config sync** - Host config syncs to clients

---

## Files Changed

| File | Change Type | Description |
|------|-------------|-------------|
| `FlightRecorder.cs` | MODIFIED | Flight recorder crash diagnostics + thread-safe `_flushedEntries` + `RecordException()` method |
| `MultiplayerModTracker.cs` | MODIFIED | Entity ID fix + early connection window + FR logging |
| `ProxiCraft.cs` | MODIFIED | Broadcast protection + FR logging + FR initialization + **version bumped to 1.2.8** |
| `ContainerManager.cs` | MODIFIED | ConcurrentDictionary conversion + **thread-safe `_itemCountCache` with lock** + snapshot iteration |
| `StoragePriority.cs` | MODIFIED | IDictionary interface support + **defensive ToList() snapshot for thread safety** |
| `ProxiCraft.csproj` | MODIFIED | Added FlightRecorder.cs to compile |

### Additional v1.2.8 Safety Improvements (QA Review Follow-up)

1. **`_itemCountCache` Thread Safety**: Added `_itemCountLock` for thread-safe cache access
2. **`_flushedEntries` Thread Safety**: Converted from `HashSet<string>` to `ConcurrentDictionary<string, byte>`
3. **`FlightRecorder.RecordException()`**: New method for exception logging with immediate flush
4. **Defensive Snapshots**: `GetStorageItems()` and `OrderStorages()` now take snapshots before iteration
5. **Version Number**: Updated from 1.2.7 to 1.2.8

---

## Log Entries to Watch For

### Success Indicators

```
[FR] === SESSION START ===
[FR] [MP] Player '<name>' verified
[FR] [MP] All clients verified - MOD RE-ENABLED
[FR] === SESSION CLEAN EXIT ===
```

### Warning Indicators (not errors, but worth noting)

```
[Network] Skipping lock broadcast - early connection window
[Network] Lock broadcast failed (total: N)
[MP] EntityId X not found in pending clients (timing race)
```

### Error Indicators

```
[FR] ... (entries without CLEAN EXIT = crash occurred)
[Network] WARNING: N lock broadcast failures. Network may be unstable.
```

---

## Notes for QA

1. **Testing requires two machines** or two game instances to properly test multiplayer scenarios

2. **The "Unknown" player name bug** was cosmetic but indicated a deeper timing issue that could contribute to crashes

3. **The 3-second early window** may cause brief desync of container locks immediately after joining - this is acceptable tradeoff for stability

4. **Flight recorder entries** (`[FR]`) add ~10-20 lines per multiplayer session to the log - this is intentional for diagnostics

5. **If crashes still occur**, the `[FR]` entries before crash will help identify which operation triggered it

---

## Version Information

- **ProxiCraft Version:** 1.2.8 (pending release)
- **Previous Version:** 1.2.7
- **Target Game:** 7 Days to Die
- **Changes Date:** 2024-01-12
