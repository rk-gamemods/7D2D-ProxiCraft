# ProxiCraft

A 7 Days to Die mod that allows crafting, reloading, refueling, and repairs using items from nearby storage containers.

**[Nexus Mods](https://www.nexusmods.com/7daystodie/mods/9269)** • **[GitHub](https://github.com/rk-gamemods/7D2D-ProxiCraft)**

## ⬇️ [Download ProxiCraft-1.2.13.zip](https://github.com/rk-gamemods/7D2D-ProxiCraft/raw/master/Release/ProxiCraft-1.2.13.zip)

---

## Features

### Single-Player Features (Stable ✅)

Use items from nearby containers for:

| Feature | Description |
|---------|-------------|
| **Crafting** | Use materials from nearby storage for crafting recipes |
| **Block Repair/Upgrade** | Repair and upgrade blocks using container materials |
| **Weapon Reload** | Reload weapons with ammo from containers |
| **Vehicle Refuel** | Refuel vehicles using gas cans from containers |
| **Generator Refuel** | Refuel generators from nearby storage |
| **Item Repair** | Use repair kits from containers to fix weapons/tools |
| **Lockpicking** | Use lockpicks from containers to pick locks |
| **Painting** | Use paint from containers with paint brush |
| **Trader Purchases** | Pay with dukes stored in containers |
| **Challenge Tracker** | Container items count toward "Gather X" challenges |
| **HUD Ammo Counter** | Shows total container ammo in weapon stat bar |
| **Recipe Tracker** | Real-time ingredient counts from containers |
| **Locked Slot Respect** | Items in locked container slots are excluded |

**Storage Sources:**

- Standard containers (chests, boxes, storage crates)
- Vehicle storage (minibike, motorcycle, 4x4, gyrocopter)
- Drone cargo compartment
- Dew collector contents
- Workstation output slots (forge, campfire, chemistry station)

### Multiplayer Features (🧪 EXPERIMENTAL)

ProxiCraft includes experimental multiplayer support with automatic crash protection.

**Requirements:** ProxiCraft must be installed on BOTH server AND all clients (same version).

**How It Works - "Guilty Until Proven Innocent":**

```text
┌─────────────────────────────────────────────────────────────┐
│ Multiplayer Safety Flow                                     │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  Client connects to server                                  │
│  ├─ multiplayerImmediateLock = true? (default)              │
│  │   └─ YES → IMMEDIATE LOCK (storage disabled) ↓           │
│  │                                                          │
│  │   Server sends handshake request                         │
│  │   ├─ Client responds (has ProxiCraft)?                   │
│  │   │   └─ YES → UNLOCK → Normal operation ✓               │
│  │   └─ NO response after timeout?                          │
│  │       └─ STAY LOCKED + show culprit name ✗               │
│  │                                                          │
│  └─ NO → Trust mode (honor system) ⚠️                       │
│      └─ No lock, relies on all players having mod           │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

**Safety Features:**

- **Immediate Lock** - Storage access blocked instantly when any client connects (zero crash window)
- **Quick Unlock** - Resumes in ~100-300ms after verifying client has mod
- **Culprit Identification** - Shows exactly which player needs to install the mod
- **Auto Re-enable** - Mod unlocks when the player without ProxiCraft disconnects
- **Server Config Sync** - Clients automatically use server's settings

**Safety Settings (config.json):**

```json
{
  "multiplayerImmediateLock": true,
  "multiplayerHandshakeTimeoutSeconds": 10
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `multiplayerImmediateLock` | true | Lock mod when clients connect. Set to `false` for honor system (NOT RECOMMENDED). |
| `multiplayerHandshakeTimeoutSeconds` | 10 | How long to wait before declaring a player doesn't have the mod. |

⚠️ **WARNING:** Setting `multiplayerImmediateLock=false` removes crash protection. Only use on moderated servers where you enforce mod installation externally.

**Settings Sync Behavior:**

- **Server-synced:** `range`, all `pullFrom*`, all `enableFor*`, `storagePriority`, `respectLockedSlots`, `allowLockedContainers`
- **Local-only (never synced):** `isDebug`, `modEnabled`, `verboseHealthCheck`, `multiplayerImmediateLock`, `multiplayerHandshakeTimeoutSeconds`, all `enhancedSafety*` settings

Debug logging cannot be enabled remotely by a server - each player controls their own logging.

**Testing Status:**

- Single player ✅
- Basic dedicated server ✅
- Co-op hosting ⚠️ (needs more testing)
- High-latency connections ⚠️ (needs more testing)
- Large player counts (8+) ⚠️ (needs more testing)

**🐛 Please Report Bugs!** Multiplayer has many edge cases. Run `pc fullcheck` - it saves `fullcheck_report.txt` to the mod folder. Attach that file to your report on GitHub or Nexus.

---

## Installation

### Single-Player

1. Download the zip using the link above
2. Extract the zip into your Mods folder
3. Launch game with EAC disabled

### Multiplayer

1. Install on the **server**
2. Install on **ALL clients** (same version)
3. If hosting co-op: the host IS the server, so all players need the mod

---

## Configuration

Edit `config.json` in the mod folder:

```json
{
  "modEnabled": true,
  "isDebug": false,
  "range": 15,

  "pullFromVehicles": true,
  "pullFromDrones": true,
  "pullFromDewCollectors": true,
  "pullFromWorkstationOutputs": true,
  "allowLockedContainers": true,

  "storagePriority": {
    "Drone": "1",
    "DewCollector": "2",
    "Workstation": "3",
    "Container": "4",
    "Vehicle": "5"
  },

  "enableForCrafting": true,
  "enableForQuests": true,
  "enableForRepairAndUpgrade": true,
  "enableForLockpicking": true,
  "enableForItemRepair": true,
  "enableForPainting": true,
  "enableForReload": true,
  "enableForRefuel": true,
  "enableForTrader": true,
  "enableForGeneratorRefuel": true,

  "enableHudAmmoCounter": true,
  "enableRecipeTrackerUpdates": true,
  "respectLockedSlots": true
}
```

### Key Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `range` | 15 | Search radius in blocks. Use 5 (same room), 15 (same floor), 30 (entire building), or -1 (unlimited). |
| `isDebug` | false | Enable verbose logging to `pc_debug.log` in the mod folder. Use for troubleshooting. |
| `storagePriority` | See above | Lower number = checked first. Items pulled from first available source. |
| `respectLockedSlots` | true | Skip items in user-locked container slots. |
| `pullFromWorkstationOutputs` | true | Only OUTPUT slots counted (not input/fuel/tool). |

---

## Console Commands

Open console with F1:

| Command | Description |
|---------|-------------|
| `pc status` | Show mod status, config, and multiplayer state |
| `pc health` | Show startup health check results |
| `pc test` | Test container scanning (shows what's found) |
| `pc fullcheck` | Full diagnostic (saves to `fullcheck_report.txt` in mod folder) |
| `pc conflicts` | Check for mod conflicts |
| `pc toggle` | Enable/disable mod temporarily |
| `pc debug` | Toggle debug logging (writes to `pc_debug.log` in mod folder) |
| `pc reload` | Reload config from file |
| `pc perf on` | Start performance profiling (resets data) |
| `pc perf off` | Stop performance profiling |
| `pc perf report` | Show performance report (also saves to `perf_report.txt` in mod folder) |

**About the Performance Profiler:**

- **Proves if ProxiCraft causes lag** - All operations timed with microsecond precision
- **Measures overall game metrics** - Frame timing, GC pressure for comparing mod combinations
- **Does NOT identify other mods** - Only determines if ProxiCraft is the problem
- Share reports from: `7 Days To Die/Mods/ProxiCraft/perf_report.txt`

### Configuration Commands

```text
pc config list              # List all settings
pc set range 30             # Change range to 30
pc config save              # Save changes to file
```

---

## Troubleshooting

### Single-Player Issues

1. **Check status:** `pc status` - Is mod enabled?
2. **Check health:** `pc health` - Are features working?
3. **Test scanning:** `pc test` - Can mod see containers?
4. **Full report:** `pc fullcheck` - Saves `fullcheck_report.txt` for bug reports
5. **Enable debug:** `pc debug` or set `isDebug: true` - Writes detailed logs to `pc_debug.log` in mod folder

### Multiplayer Issues

| Symptom | Solution |
|---------|----------|
| "Multiplayer: LOCKED" in `pc status` | A player doesn't have ProxiCraft installed. Check who. |
| Features don't work for some players | Ensure ALL players have same mod version |
| Settings different than expected | Server settings override client. Check server's config.json |
| CTD on player join | Run `pc fullcheck`, attach `fullcheck_report.txt` |

---

## Technical Details

<details>
<summary>Click to expand technical documentation</summary>

### Stability Philosophy

ProxiCraft is designed to survive game updates:

| Tier | Features | Risk |
|------|----------|------|
| **Stable** | Crafting, Quests, Block Repair, Lockpicking, Item Repair, Painting, HUD, Recipe Tracker | Low - Simple postfix patches |
| **Less Stable** | Reload, Vehicle Refuel, Generator Refuel, Trader | Medium - Transpiler patches |
| **Storage Sources** | Vehicles, Drones, Dew Collectors, Workstations | Low - Read-only, no patches |

**Startup Health Check:** Validates all 24 patches on game load. Silent when OK, warns on issues.

**Adaptive Recovery:** If game updates change method names, the mod attempts to find renamed methods automatically.

### Design Decisions

**Workstation Slots:** Only OUTPUT slots are counted. Input (being processed), fuel (being burned), and tool slots (in use) are ignored.

**Virtual Inventory Architecture:** All storage operations flow through `VirtualInventoryProvider` - centralizes multiplayer safety, ensures consistent behavior, enables global bug fixes.

### Multiplayer Network Architecture

<details>
<summary>Click to expand network flow documentation</summary>

#### Understanding Container Locks

**Important:** ProxiCraft's container lock system is a **UX courtesy**, not crash prevention.

**When are locks created?**

| Action | Lock Created? | Why |
|--------|---------------|-----|
| Player opens container UI (press E) | ✅ YES | Vanilla game creates lock |
| Player crafts using nearby storage | ❌ NO | No UI opened |
| Player repairs/upgrades block | ❌ NO | No UI opened |
| Player reloads weapon | ❌ NO | No UI opened |
| Player refuels vehicle/generator | ❌ NO | No UI opened |

**What locks protect:**

- When Player A has a container UI open, ProxiCraft tells other players to **skip** that container
- This prevents "where did my items go?" confusion when items disappear from an open UI
- **This is NOT required to prevent crashes** - just for better UX

**What if there were no locks?**

- Player A opens container, sees 10 concrete
- Player B repairs a block, ProxiCraft removes 1 concrete from that container
- Player A's UI still shows 10 until they close and reopen
- No crash, just mild confusion

**Multiple players using remote features (repair, reload, etc.):**

- These features **never create locks** because they never open container UIs
- Multiple players CAN pull from the same storage simultaneously
- The game processes requests sequentially (single-threaded)
- If two players need the last item, one gets it, one doesn't - normal resource contention
- **No crashes, no duplication, no issues**

#### Network Packets

| Packet | Direction | Purpose |
|--------|-----------|---------|
| `NetPackagePCHandshake` | Both | Announces mod presence, version, conflicts |
| `NetPackagePCLock` | Server→Clients | Broadcasts container lock/unlock state |
| `NetPackagePCConfigSync` | Server→Client | Sends server config to joining clients |

#### Container Lock Flow

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│ PLAYER OPENS CONTAINER                                                       │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Player → Opens container (press E)                                         │
│    │                                                                        │
│    ├─→ [SERVER] Game calls TELockServer()                                   │
│    │     └─→ Adds to lockedTileEntities dictionary                          │
│    │                                                                        │
│    ├─→ [PROXICRAFT] TELockServer patch fires                                │
│    │     ├─→ Connection OK? → Broadcast NetPackagePCLock (unlock=false)     │
│    │     └─→ Connection hiccup? → Schedule retry in 500ms                   │
│    │                                                                        │
│    └─→ [ALL CLIENTS] Receive lock packet                                    │
│          ├─→ Check timestamp (last-write-wins ordering)                     │
│          ├─→ Add to LockedList with timestamp                               │
│          └─→ Container excluded from all operations                         │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│ PLAYER CLOSES CONTAINER                                                      │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Player → Closes container (Tab or walk away)                               │
│    │                                                                        │
│    ├─→ [SERVER] Game calls TEUnlockServer()                                 │
│    │     └─→ Removes from lockedTileEntities                                │
│    │                                                                        │
│    ├─→ [PROXICRAFT] TEUnlockServer patch fires                              │
│    │     ├─→ Connection OK? → Broadcast NetPackagePCLock (unlock=true)      │
│    │     └─→ Connection hiccup? → Schedule retry in 500ms                   │
│    │                                                                        │
│    └─→ [ALL CLIENTS] Receive unlock packet                                  │
│          ├─→ Check timestamp (last-write-wins ordering)                     │
│          └─→ Remove from LockedList → Container available                   │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│ PLAYER DISCONNECTS WITH CONTAINER OPEN                                       │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Player → Disconnects (crash, leave, kick)                                  │
│    │                                                                        │
│    ├─→ [SERVER] Game calls ClearTileEntityLockForClient()                   │
│    │                                                                        │
│    ├─→ [PROXICRAFT] Patch captures container position BEFORE clear          │
│    │     └─→ Broadcasts unlock packet to all clients                        │
│    │                                                                        │
│    └─→ [ALL CLIENTS] Receive orphan unlock                                  │
│          └─→ Remove from LockedList → Container available                   │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

#### Race Condition Handling

| Scenario | Problem | Solution |
|----------|---------|----------|
| Unlock arrives before Lock | Ghost lock | **Last-write-wins**: Packets have timestamps, only newer packets apply |
| Lock fails, retry collides with Unlock | Stale lock | **Retry cancellation**: Lock retry checks if container still locked before sending |
| Player disconnects abruptly | Orphan lock | **Orphan cleanup**: ClearTileEntityLockForClient patch broadcasts unlock |
| All above fail | Permanent ghost lock | **Lock expiration**: Locks auto-expire after 30 seconds (LOCK_EXPIRATION_SECONDS = 30) |
| High latency (>500ms) | Stale state | **Eventual consistency**: Acceptable trade-off; logged for diagnostics |

#### Lock Expiration (Self-Healing)

```text
┌────────────────────────────────────────────────────────────────┐
│ Lock added at T=0      T=30s                                   │
│      │                     │                                   │
│      ▼                     ▼                                   │
│  ┌───────┐            ┌───────┐                                │
│  │LOCKED │────────────│EXPIRED│ → Auto-removed on next access  │
│  └───────┘            └───────┘                                │
│                                                                │
│  Locks auto-expire after 30 seconds (internal default).        │
│  30 seconds is plenty for any normal container interaction.    │
│  Ghost locks self-heal quickly without blocking functionality. │
└────────────────────────────────────────────────────────────────┘
```

- Expiration checked lazily (only when accessing container)
- No background threads or heartbeats
- Self-healing: ghost locks eventually resolve without intervention
- 30 seconds is long enough for any normal operation

#### Error Handling Philosophy

| Priority | Behavior |
|----------|----------|
| 1st | **Never crash** - All network operations wrapped in try-catch |
| 2nd | **Prefer "available" over "locked"** - Ghost unlocks better than ghost locks |
| 3rd | **Log everything** - Errors logged for diagnostics |
| 4th | **Eventual consistency** - Brief item duplication better than permanent dysfunction |

**Packet deserialization failure:** Defaults to `unlock=true` (safe default)

**Connection unavailable:** Retry once after 500ms, then give up with warning

**High latency (>500ms):** Warning logged, packet still processed

</details>

### Project Structure

```text
ProxiCraft/
├── ProxiCraft/
│   ├── ProxiCraft.cs              # Main mod, Harmony patches (~4,200 lines)
│   ├── ContainerManager.cs        # Container discovery/operations (~2,700 lines)
│   ├── VirtualInventoryProvider.cs # Central inventory hub (MP-safe)
│   ├── MultiplayerModTracker.cs   # MP handshake and safety
│   ├── ModConfig.cs               # Configuration
│   ├── ConsoleCmdProxiCraft.cs    # Console commands
│   ├── ModCompatibility.cs        # Conflict detection & diagnostics
│   ├── AdaptivePatching.cs        # Dynamic patch strategy selection
│   ├── AdaptiveMethodFinder.cs    # Renamed method discovery
│   ├── SafePatcher.cs             # Error-wrapped Harmony operations
│   ├── RobustTranspiler.cs        # Transpiler utilities
│   ├── PerformanceProfiler.cs     # Performance profiling
│   ├── FlightRecorder.cs          # Diagnostic flight recorder
│   ├── NetworkDiagnostics.cs      # Network latency diagnostics
│   ├── NetPackagePCLock.cs        # Multiplayer lock sync packet
│   ├── StartupHealthCheck.cs      # Startup validation
│   ├── StoragePriority.cs         # Storage priority ordering
│   └── ModPath.cs                 # Mod path resolution
├── Properties/
│   └── AssemblyInfo.cs            # Assembly metadata
└── Release/ProxiCraft/            # Distribution package
```

### Building

```powershell
dotnet build -c Release
```

</details>

---

## Inspiration & Prior Art

ProxiCraft builds on concepts from these mods:

| Mod | Author | Notes |
|-----|--------|-------|
| [CraftFromContainers](https://www.nexusmods.com/7daystodie/mods/2196) | aedenthorn | Original concept for A20 |
| [BeyondStorage2](https://www.nexusmods.com/7daystodie/mods/7809) | gazorper ([GitHub](https://github.com/superguru/7d2d_mod_BeyondStorage2)) | Expanded features, storage priority |

If you prefer their versions, check them out! ProxiCraft is a from-scratch implementation with different architecture.

**Use only ONE craft-from-containers mod.** Remove CraftFromContainers, BeyondStorage2, or similar mods before using ProxiCraft.

---

## Changelog

### v1.2.13 - Drone Inventory Double-Count Fix

**Fixed:**

- **Fixed drone inventory being counted multiple times** — Drone items were double/triple-counted in crafting calculations because the storage dictionary keyed by position. Since drones follow the player and constantly change position, each movement created a new dict entry referencing the same lootContainer while stale entries persisted. Craft-max would inflate (e.g., 56 drone items counted as 112+), but actual crafting would fail because the real items only existed once. Fixed by keying mobile entity storage (drones and vehicles) by entity ID instead of position.

### v1.2.12 - Block Upgrade Bug Fix

**Fixed:**

- **Fixed double material consumption during block upgrades** — When upgrade materials were split between player inventory and nearby containers, the game would consume items twice (vanilla removed some from inventory, then ProxiCraft removed the full amount again from containers). Now correctly accounts for what vanilla already consumed.
- **Fixed "r" shorthand in upgrade item resolution** — Blocks using the vanilla `"r"` shorthand for `UpgradeBlock.Item` (which means "use the same material as repairs") were not resolved correctly, potentially breaking upgrades on modded blocks using this pattern.

### v1.2.11 - Critical Multiplayer Crash Fix & Performance Diagnostics

**🚨 CRITICAL FIX:**

- **Fixed multiplayer container crash** - Opening any container in multiplayer caused ALL clients to crash instantly. Root cause: infinite recursion in network packet serialization due to incorrect C# base method call syntax (`((NetPackage)this).write()` instead of `base.write()`).

**Bug Fixes:**

- Fixed memory leak in lock dictionaries (periodic cleanup every 15 seconds)
- Fixed coroutine race condition in handshake retry logic
- Optimized NetworkPacketObserver to only examine first 10 packets per batch

**New: Performance Profiler**

- Definitively proves whether lag originates from ProxiCraft (all patches instrumented with microsecond timing)
- Measures overall game metrics (frame timing, GC pressure) for comparing before/after with different mods
- Note: Cannot identify which OTHER mod causes lag - it's an "is ProxiCraft the problem?" diagnostic tool
- Use `pc perf on`, play, then `pc perf report` to diagnose

### v1.2.10 - Full Magazine Reload Fix

**Fixed:**

- Fixed bug where guns could be reloaded even when magazine was full when using ammo from containers
- Vanilla behavior restored: reload is now blocked when magazine is already at capacity

### v1.2.9 - Multiplayer Safety Improvements

Addresses reported issue: "Clients cannot open containers, server crashes when other players open anything"

**Changes:**

- Added missing `IsModAllowed()` safety check to container lock/unlock broadcasts
- Added defensive dictionary access to prevent potential crashes from concurrent modification
- Added throttled diagnostic logging when safety system blocks broadcasts

Please report back if you were experiencing multiplayer container issues!

### v1.2.8 - Multiplayer Stability Improvements

Investigating reported crash: "Host crashes when client opens containers"

**Changes:**

- Added thread-safe counters for client verification
- Added defensive snapshots when iterating container locks during disconnect
- Player name now falls back to handshake packet when entity lookup fails
- Added crash diagnostics to `pc_debug.log`
- Log file now self-manages size

Please report back if you were experiencing multiplayer crashes!

### v1.2.7 - Network Stability & Robustness Improvements

**Fixed:**

- Fixed multiplayer handshake packet loss causing mod to lock up for entire server session
- Fixed orphan container locks when players disconnect (containers no longer stay "locked" forever)
- Fixed race condition where out-of-order packets could cause ghost locks (last-write-wins ordering)
- Added lock expiration (30 sec default) - ghost locks self-heal quickly
- Added retry mechanism for handshake and lock broadcasts on temporary connection hiccups
- Added lock retry cancellation to prevent stale locks after rapid open/close
- Added network latency diagnostics for troubleshooting slow connections
- Improved error handling in packet deserialization

**Robustness Improvements:**

- Added defensive measures for rare edge cases during item removal operations:
  - Pre-check TileEntity.IsRemoving before container access (prevents crash if block destroyed mid-operation)
  - Chunk read locks with proper finally blocks during modifications (prevents crash if chunk unloads)
  - Defensive bounds and null checks in all item iteration loops
  - Try-catch wrappers with automatic cleanup and `[CrashPrevention]` logging to pc_debug.log and Player.log

### v1.2.6 - Config File Bug Fix

**Fixed:**

- Fixed config settings being overwritten on game load
- Fixed race condition in config file watcher
- Fixed potential integer overflow in item count cache

### v1.2.5 - Hosting Panel Compatibility

**Fixed:**

- Fixed mod failing to load on some dedicated server hosting panels (CubeCoders AMP, etc.)

**For Server Hosts (if auto-detection still fails):**

You can manually tell ProxiCraft where it's installed by setting an environment variable called `PROXICRAFT_PATH` to the full folder path (e.g., `C:\GameServers\7DaysToDie\Mods\ProxiCraft`).

**Windows 10/11:**

1. Press `Win + R` to open Run dialog
2. Type `sysdm.cpl` and press Enter
3. Click the **Advanced** tab
4. Click **Environment Variables** button at the bottom
5. Under "System variables", click **New**
6. Variable name: `PROXICRAFT_PATH`
7. Variable value: Full path to your ProxiCraft folder
8. Click OK on all dialogs
9. Restart your server

**Other platforms:** Search your hosting panel or OS documentation for "environment variables".

### v1.2.4 - Enhanced Safety Fix

**Fixed:**

- Fixed enhanced safety mode breaking features (repair, reload, etc.) when switching between game sessions
- Multiplayer state now properly resets when starting a new game, preventing stale flags from blocking functionality

### v1.2.3 - Configuration Defaults Update

**Changed:**

- Enhanced Safety Mode now enabled by default (recommended for multiplayer stability)
- All `enhancedSafety*` settings default to `true` for new installations
- Configuration documentation expanded with all 30+ settings comprehensively documented

**Note:** Existing users with custom `config.json` files are unaffected. Only fresh installations will use the new defaults.

### v1.2.2 - Hotfix

**Fixed:**

- Health check report now shows all features (VehicleRepair, HudAmmoCounter, RecipeTracker, TraderSelling, LockedSlots were missing from grouped output)

### v1.2.1 - Virtual Inventory Architecture & Multiplayer Safety

**New:**

- 🧪 **EXPERIMENTAL Multiplayer Support** with Virtual Inventory architecture
- **Zero-Crash Protection** - "Guilty Until Proven Innocent" instant lock on client connect *(GreenGhost21, optimus0, GeeButtersnaps)*
- **Server Config Sync** - Clients use server's settings automatically
- **Configurable Storage Priority** - Control search order

**Fixed:**

- `pc help` command loop
- Vehicle repair kit loss with full inventory
- Duplicate profiler timer calls

### v1.2.0 - Features & Bug Fixes

**New:**

- HUD ammo counter for container ammo
- Locked slot respect

**Fixed:**

- Radial menu reload greyed out *(falkon311)*
- R-key reload blocked *(falkon311)*
- Block upgrade material consumption *(falkon311)*
- Workstation output crafting exploit *(Kaizlin)*
- "Take Like" button behavior *(Kaizlin)*

### v1.1.0 - Expanded Storage Sources

- Vehicle, drone, dew collector, workstation output support
- Recipe tracker real-time updates
- Performance profiler and optimizations

### v1.0.0 - Initial Release

---

## License

MIT License - See [LICENSE](LICENSE) for details.
