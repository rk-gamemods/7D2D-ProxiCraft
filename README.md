# ProxiCraft

A 7 Days to Die mod that allows crafting, reloading, refueling, and repairs using items from nearby storage containers.

## ⬇️ [Download ProxiCraft-1.2.1.zip](https://github.com/rk-gamemods/7D2D-ProxiCraft/raw/master/Release/ProxiCraft-1.2.1.zip)

## Features

### Core Features (Stable)
- ✅ **Craft from Containers** - Use materials from nearby chests for crafting
- ✅ **Block Repair/Upgrade** - Use materials from containers for repairs
- ✅ **Challenge Tracker Integration** - Container items count toward challenges
- ✅ **Lockpicking** - Use lockpicks from containers to pick locks
- ✅ **Item Repair** - Use repair kits from containers to repair weapons/tools
- ✅ **Painting** - Use paint from containers when using paint brush
- ✅ **HUD Ammo Counter** - Shows total ammo from containers in HUD stat bar
- ✅ **Recipe Tracker Updates** - Real-time ingredient count updates from containers
- ✅ **Trader Purchases** - Pay with dukes stored in containers
- ✅ **Locked Slot Respect** - Items in locked container slots are excluded from all operations

### Extended Features (Transpiler-based)
- ✅ **Weapon Reload** - Reload weapons using ammo from containers
- ✅ **Vehicle Refuel** - Refuel vehicles using gas cans from containers
- ✅ **Generator Refuel** - Refuel generators from containers

### Runtime Configuration
- ✅ **Live Config Reload** - Changes to config.json apply without restart
- ✅ **Console Config Commands** - View and modify settings in-game

### Storage Sources
- ✅ **Standard Containers** - Chests, boxes, storage crates
- ✅ **Vehicle Storage** - Minibike, motorcycle, 4x4, gyrocopter bags
- ✅ **Drone Storage** - Player's drone cargo
- ✅ **Dew Collectors** - Water from dew collectors
- ✅ **Workstation Outputs** - Items in forge/campfire/chemistry output slots
- ✅ **Configurable Priority** - Control which storage types are searched first

### 🧪 EXPERIMENTAL: Multiplayer Support

ProxiCraft includes **experimental multiplayer support** with a unique architecture designed for stability:

#### Virtual Inventory System

Unlike traditional approaches that patch individual game methods, ProxiCraft uses a **centralized Virtual Inventory Provider** that acts as the single source of truth for all storage-aware operations:

```
┌─────────────────────────────────────────────────────────────┐
│ Virtual Inventory Provider (Central Hub)                    │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  All features route through ONE provider:                   │
│  ┌─────────────┐   ┌─────────────┐   ┌─────────────┐       │
│  │  Crafting   │   │   Reload    │   │   Refuel    │       │
│  └──────┬──────┘   └──────┬──────┘   └──────┬──────┘       │
│         │                 │                 │               │
│         └─────────────────┼─────────────────┘               │
│                           ▼                                 │
│                ┌──────────────────────┐                     │
│                │ VirtualInventory     │                     │
│                │ Provider             │ ◄── MP Safety Gate  │
│                └──────────┬───────────┘                     │
│                           │                                 │
│         ┌─────────────────┼─────────────────┐               │
│         ▼                 ▼                 ▼               │
│    ┌─────────┐      ┌─────────┐      ┌──────────┐          │
│    │   Bag   │      │ Toolbelt│      │ Storage  │          │
│    └─────────┘      └─────────┘      └──────────┘          │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

**Why This Matters:**
- **Single Point of Control** - All storage access flows through one class
- **Consistent Safety Checks** - Multiplayer validation happens in ONE place
- **Bug Fixes Apply Globally** - Fix a bug once, it's fixed everywhere
- **Zero Crash Window** - The provider gates ALL storage access when unsafe

#### How Multiplayer Protection Works

1. **Immediate Lock** - When ANY client connects, storage access is instantly blocked
2. **Verification Handshake** - Client must prove they have ProxiCraft installed
3. **Unlock on Success** - Once verified (~100-300ms), normal operation resumes
4. **Culprit Identification** - If verification fails, the player without the mod is identified by name

```
Client Connects → IMMEDIATE LOCK → Handshake Sent → Verified? → UNLOCK
                        │                              │
                        │                              └─ No → STAY LOCKED
                        │                                      (Culprit identified)
                        └── Storage blocked during this entire process
```

**This "Guilty Until Proven Innocent" approach means:**
- No crashes during the verification window (unlike timeout-based approaches)
- Server operator knows exactly who needs to install the mod
- Mod auto-enables when the problematic player disconnects

#### Experimental Status

This feature is marked **EXPERIMENTAL** because:
- Multiplayer has many edge cases that are difficult to test
- Dedicated server configurations vary widely
- Network conditions affect handshake timing

**What's tested:**
- ✅ Single player (works perfectly)
- ✅ Co-op hosting (one player hosts, friends join)
- ✅ Basic dedicated server setup

**What needs more testing:**
- ⚠️ High-latency connections
- ⚠️ Large player counts (8+)
- ⚠️ Various dedicated server configurations

**Report issues with multiplayer** using `pc fullcheck` output - this helps us improve the system.

### Reliability Features
- ✅ **Startup Health Check** - Validates all features on load
- ✅ **Silent by Default** - No output unless there are issues
- ✅ **Auto-Adaptation** - Attempts to recover from game updates
- ✅ **Conflict Detection** - Warns about mod conflicts
- ✅ **Graceful Degradation** - Features disable individually if broken

## Stability Philosophy

ProxiCraft is designed to survive game updates through multiple layers of protection:

### 1. Patch Stability Tiers

| Tier | Features | Risk Level |
|------|----------|------------|
| **Stable** | Crafting, Quests, Block Repair, Lockpicking, Item Repair, Painting, HUD Ammo, Recipe Tracker, Locked Slots | Low - Uses simple postfix patches |
| **Less Stable** | Reload, Vehicle Refuel, Generator Refuel, Trader Purchases | Medium - Uses transpiler patches that modify IL code |
| **Storage Sources** | Vehicles, Drones, Dew Collectors, Workstations | Low - Only reads game data, doesn't patch |

### 2. Startup Health Check

On every game start, ProxiCraft validates all 24 patches:
- **Silent when OK** - No console spam if everything works
- **Warns on issues** - Clear messages if features break
- **Detailed diagnostics** - Use `pc fullcheck` for bug reports

### 3. Adaptive Recovery

When game updates change method names or signatures:
1. **Primary lookup** - Try exact method match
2. **Signature matching** - Match by parameter types
3. **Name pattern search** - Look for renamed methods
4. **Graceful failure** - Disable feature, not crash game

## Installation

1. Download the zip using the link above
2. Extract to `7 Days To Die/Mods/ProxiCraft/`
3. Ensure EAC is disabled (required for all DLL mods)

## Console Commands

Open the console with F1 and use:

| Command | Description |
|---------|-------------|
| `pc status` | Show mod status and configuration |
| `pc health` | Show startup health check results |
| `pc recheck` | Re-run health check |
| `pc fullcheck` | Full diagnostic report (for bug reports) |
| `pc diag` | Show mod compatibility report |
| `pc test` | Test container scanning (shows nearby items) |
| `pc perf` | Performance profiler (see below) |
| `pc reload` | Reload configuration from config.json |
| `pc conflicts` | Show detected mod conflicts |
| `pc toggle` | Enable/disable mod temporarily |
| `pc debug` | Toggle debug logging |

### Configuration Commands

| Command | Description |
|---------|-------------|
| `pc config list` | List all settings with current values |
| `pc config get <setting>` | Get a specific setting value |
| `pc config set <setting> <value>` | Set a setting value (temporary) |
| `pc config save` | Save current settings to config.json |
| `pc set <setting> <value>` | Shortcut for config set |
| `pc get <setting>` | Shortcut for config get |

**Example:**
```
pc set range 30       # Change range to 30 blocks
pc config save        # Save changes to config.json
```

### Troubleshooting Workflow

1. **Check status**: `pc status` - Is the mod enabled?
2. **Check health**: `pc health` - Are all features working?
3. **Test scanning**: `pc test` - Can mod see your containers?
4. **Full report**: `pc fullcheck` - Copy this for bug reports

### Performance Profiling

If you experience lag, use the built-in profiler:

| Command | Description |
|---------|-------------|
| `pc perf` | Show brief performance status |
| `pc perf on` | Enable profiling (collects timing data) |
| `pc perf off` | Disable profiling |
| `pc perf reset` | Clear collected data |
| `pc perf report` | Show detailed performance report |

The profiler tracks timing for container scans, item counting, and cache operations. Share the output when reporting performance issues.

## Multiplayer

### 🧪 EXPERIMENTAL - Read This First

Multiplayer support is experimental. See the "EXPERIMENTAL: Multiplayer Support" section above for how it works.

### Requirements

**IMPORTANT:** ProxiCraft must be installed on BOTH client AND server for multiplayer games.

**This includes private/co-op games** where one player hosts:
- The hosting player IS the server
- ALL players (host + friends) must have ProxiCraft installed
- If any player doesn't have it, they'll be identified and the mod stays locked for safety

### Server Config Sync

When joining a server with ProxiCraft, **your local settings are automatically overridden** with the server's settings:

- **Range** - All players use the server's range setting
- **Storage sources** - Which containers are searched (vehicles, drones, etc.)
- **Feature toggles** - Crafting, reload, refuel, etc.
- **Storage priority** - Order of container searching

This prevents desync issues where Player A sees items in containers but Player B doesn't.

Example log when settings differ:
```
[Multiplayer] Received server configuration - synchronizing settings...
Settings changed from server (3 differences):
  range: 30 → 15
  pullFromVehicles: false → true
  enableForReload: true → false
```

Use `pc status` to confirm settings are synced: `Multiplayer: UNLOCKED (server confirmed, config synced ✓)`

### Multiplayer Safety Configuration

For **trusted modded servers** where you enforce mod installation externally (Discord rules, modpack, etc.), you can tune the safety settings in `config.json`:

```json
{
  "multiplayerImmediateLock": true,
  "multiplayerHandshakeTimeoutSeconds": 10
}
```

⚠️ **WARNING**: Setting `multiplayerImmediateLock` to `false` removes crash protection. Only do this on servers where you control who joins.

### Troubleshooting Multiplayer CTD

1. Run `pc status` - check if multiplayer is LOCKED or UNLOCKED
2. Check if server has ProxiCraft installed (same version as client)
3. Run `pc conflicts` to check for mod conflicts
4. If problems persist, run `pc fullcheck` and report the output

## Configuration

Edit `config.json` in the mod folder. The file is organized into sections:

```json
{
  "modEnabled": true,
  "isDebug": false,
  "verboseHealthCheck": false,
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

### Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `modEnabled` | true | Master toggle for entire mod |
| `isDebug` | false | Enable verbose logging (performance impact) |
| `verboseHealthCheck` | false | Always show full health check output |
| `range` | 15 | Search radius in blocks (-1 for unlimited) |
| `pullFromVehicles` | true | Search vehicle storage bags |
| `pullFromDrones` | true | Search drone cargo |
| `pullFromDewCollectors` | true | Use water from dew collectors |
| `pullFromWorkstationOutputs` | true | Count items in forge/campfire output |
| `allowLockedContainers` | true | Search containers even if locked |
| `storagePriority` | See config | Order to search storage types (Drone=1, DewCollector=2, Workstation=3, Container=4, Vehicle=5) |
| `enableHudAmmoCounter` | true | Show container ammo in HUD stat bar |
| `enableRecipeTrackerUpdates` | true | Live recipe tracker ingredient updates |
| `respectLockedSlots` | true | Skip items in user-locked container slots |

### Range Guide

Range is the **radius** from player position:

| Range | Coverage | Notes |
|-------|----------|-------|
| 5 | Same room only | Minimal performance impact |
| 15 | Same floor/area | **Default** - good balance |
| 30 | Entire building | Slight performance impact |
| -1 | All loaded chunks | May cause lag with many containers |

### Feature Notes

| Feature | Range Affected? | Notes |
|---------|-----------------|-------|
| Crafting | Yes | Only containers within range |
| Block Repair | Yes | Only containers within range |
| Trader | **No** | Works anywhere when at trader |
| Vehicle Refuel | Yes | Containers near the vehicle |
| Generator Refuel | Yes | Containers near the generator UI |
| Lockpicking | Yes | Containers within range |

## Design Decisions

### Workstation Slots: Output Only

When counting items in workstations (forge, campfire, workbench, chemistry station), ProxiCraft **only counts OUTPUT slots**. Input, fuel, and tool slots are intentionally ignored.

**Why?**

| Slot Type | Counted? | Reasoning |
|-----------|----------|-----------|
| **Output** | ✅ Yes | Finished products ready to use |
| **Input** | ❌ No | Items actively being processed/consumed |
| **Fuel** | ❌ No | Fuel is being burned, not available |
| **Tool** | ❌ No | Tools are in use (anvil, beaker, etc.) |

### Vehicle/Drone Storage: Full Access

Unlike workstations, vehicle and drone storage containers are fully counted. All slots are available because these are true storage containers - there's no "processing" happening.

### Virtual Inventory Architecture

All storage-aware operations flow through a central `VirtualInventoryProvider` class. This design:

1. **Centralizes multiplayer safety** - One place to gate all storage access
2. **Ensures consistency** - All features use the same counting/consumption logic
3. **Simplifies debugging** - Problems traced to one location
4. **Enables global fixes** - Fix a bug once, fixed everywhere

## Project Structure

```
ProxiCraft/
├── ProxiCraft/                    # Source code
│   ├── ProxiCraft.cs              # Main mod with Harmony patches
│   ├── ContainerManager.cs        # Container scanning and item management
│   ├── VirtualInventoryProvider.cs # Central virtual inventory hub (MP-safe)
│   ├── ModConfig.cs               # Configuration settings
│   ├── MultiplayerModTracker.cs   # MP handshake and safety lock
│   ├── SafePatcher.cs             # Safe patching utilities
│   ├── AdaptivePatching.cs        # Mod compatibility helpers
│   ├── AdaptiveMethodFinder.cs    # Game update recovery
│   ├── RobustTranspiler.cs        # Safe transpiler utilities
│   ├── StartupHealthCheck.cs      # Feature validation system
│   ├── ModCompatibility.cs        # Conflict detection
│   ├── ConsoleCmdProxiCraft.cs    # Console commands (pc)
│   ├── PerformanceProfiler.cs     # Performance profiling system
│   └── NetPackagePCLock.cs        # Multiplayer lock sync packets
├── Release/ProxiCraft/            # Ready-to-deploy mod package
│   ├── ProxiCraft.dll             # Compiled mod
│   ├── ModInfo.xml                # Mod metadata
│   └── config.json                # User configuration
├── TECHNICAL_REFERENCE.md         # Technical documentation
├── RESEARCH_NOTES.md              # Development history
└── LICENSE                        # MIT License
```

## Inspiration & Prior Art

ProxiCraft builds on concepts from:

| Mod | Author | Notes |
|-----|--------|-------|
| [CraftFromContainers](https://www.nexusmods.com/7daystodie/mods/2196) | aedenthorn | Original concept |
| [CraftFromContainers v1.0](https://www.nexusmods.com/7daystodie/mods/4970) | SYN0N1M | Game v1.0 port |

### What's Different in ProxiCraft

| Feature | ProxiCraft | Others |
|---------|------------|--------|
| Startup Health Check | ✅ Validates all features | ❌ |
| Adaptive Recovery | ✅ Survives game updates | ❌ |
| Silent by Default | ✅ No spam | ❌ |
| Challenge Tracker | ✅ Full integration | ❌ |
| Stability Tiers | ✅ Documented risk levels | ❌ |
| Virtual Inventory | ✅ Centralized MP-safe architecture | ❌ |
| Zero-Crash MP Protection | ✅ Immediate lock on connect | ❌ |

## Building

```powershell
dotnet build -c Release
```

Outputs:
- `Release/ProxiCraft/` - The mod folder
- `Release/ProxiCraft.zip` - Distribution package (latest)
- `Release/ProxiCraft-1.2.1.zip` - Versioned release package

**Note:** Version is controlled by `<ModVersion>` in ProxiCraft.csproj. Both zip files are created automatically during build.

## Changelog

### v1.2.1 - Virtual Inventory Architecture & Multiplayer Safety

**New Features:**
- **🧪 EXPERIMENTAL Multiplayer Support** with unique Virtual Inventory architecture
  - Centralized `VirtualInventoryProvider` - ALL storage operations flow through one class
  - Single point of control for multiplayer safety checks
  - Bug fixes apply globally across all features
- **Zero-Crash Multiplayer Protection** - "Guilty Until Proven Innocent" approach
  - **IMMEDIATE LOCK** when ANY client connects (zero crash window)
  - Mod unlocks only after client proves they have ProxiCraft (~100-300ms)
  - Culprit identification - clearly shows which player needs the mod
  - Auto-re-enables when offending player disconnects
- **Server Config Sync** - Clients automatically use server's settings
  - Prevents desync where players see different items
  - Differences logged when settings change
- **Configurable Storage Priority** - Control search order (Drone → DewCollector → Workstation → Container → Vehicle)
- **Enhanced Safety Mode** - Optional per-feature MP safety (experimental, default OFF)

**Bug Fixes:**
- Console command `pc help` now works correctly (was causing "unknown command" loop)
- Vehicle repair with full inventory could lose repair kits - now checks inventory space first
- Fixed duplicate profiler timer calls (inflated statistics)

### v1.2.0 - Features & Bug Fixes
**New Features:**
- HUD ammo counter - shows container ammo in weapon stat bar
- Locked slot respect - items in user-locked container slots excluded from all operations

**Bug Fixes:**
- Radial menu reload - ammo greyed out when only in nearby containers *(reported by falkon311)*
- R-key reload blocked when ammo only in containers *(reported by falkon311)*
- Block upgrades not consuming materials from nearby containers *(reported by falkon311)*
- Workstation output items counted but not consumed - crafting exploit *(reported by Kaizlin)*
- "Take Like" button taking all container contents instead of matching items *(reported by Kaizlin)*

### v1.1.0 - Expanded Storage Sources
- Vehicle storage support - minibike, motorcycle, 4x4, gyrocopter
- Drone storage support - use items from drone cargo
- Dew collector support - water available for crafting
- Workstation output slots - forge, workbench, campfire, chemistry station
- Recipe tracker real-time updates from containers
- Performance profiler - `pc perf` commands for diagnostics
- Container cache performance optimizations

### v1.0.0 - Initial Public Release
- Core crafting from containers
- Weapon reload from containers
- Vehicle/generator refuel from containers
- Block repair from containers
- Lockpicking and item repair from containers
- Challenge tracker integration
- Startup health check system
- Console diagnostics (`pc` commands)

## License

MIT License - See [LICENSE](LICENSE) for details.

Released under MIT to allow community maintenance when game updates break functionality.
