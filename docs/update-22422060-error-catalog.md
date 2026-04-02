# ProxiCraft Error Catalog for 7DTD Build 22422060

## Scope

This file tracks confirmed and suspected compatibility issues introduced by
the 7 Days to Die update to build 22422060.

Functional intent to preserve:

- Nearby storage augments vanilla inventory instead of replacing it.
- Consumption order remains bag, then toolbelt, then nearby storage.
- The mod degrades toward inventory-only behavior under multiplayer safety
  lock instead of crashing.
- Storage-aware UI counts, crafting, reload, refuel, repair, and related
  features stay additive.

## Baseline Evidence

- Game code refreshed in `GameMods/7D2DMods/7D2DCodebase` with
  `VERSION.json` showing build `22422060`.
- Analyzer pass identified a hard ProxiCraft break around dew collector
  storage access.
- Release build of `ProxiCraft.csproj` now succeeds after the collector
  compatibility fix.
- Fresh jCodeMunch audit confirms the highest-risk patched vanilla methods
  still exist with compatible signatures after the update.
- Changed vanilla risk surfaces include `GameManager`, `Inventory`,
  `EntityPlayerLocal`, `EntityVehicle`, `ItemActionRanged`, `XUiC_ItemStack`,
  and `TileEntityCollector`.

## Comprehensive QA Review (2026-04-01)

### Methodology

All 48 Harmony patches in ProxiCraft were cataloged and cross-referenced
against the 97 files changed in the game update. Each patch analyzed for:

1. **Signature compatibility** — do Harmony param injections still match?
2. **Transpiler IL stability** — does the target method still contain the
   expected call instructions?
3. **Behavioral assumptions** — do runtime data flows, enums, and field
   accesses still work as expected?
4. **Graceful degradation** — what happens if a patch target fails at
   runtime?

### Changed vs Unchanged Target Summary

**11 target classes CHANGED** (require detailed analysis):

- `GameManager.cs` — 6 patches (StartGame, SaveAndCleanupWorld, Update,
  TELockServer, TEUnlockServer, ClearTileEntityLockForClient)
- `Inventory.cs` — 1 patch (DecItem postfix)
- `EntityVehicle.cs` — 2 patches (hasGasCan postfix, takeFuel transpiler)
- `ItemActionRanged.cs` — 1 patch (CanReload postfix)
- `XUiC_ItemStack.cs` — 2 patches (HandleSlotChangeEvent postfix,
  UserLockedSlot setter postfix)
- `TileEntityCollector.cs` — accessed via ContainerManager (fixed)
- `EntityPlayerLocal.cs` — not directly patched, but used by many patches
- `XUiC_ChallengeEntryList/Window/GroupEntry/GroupList.cs` — 4 challenge
  UI files (but `ChallengeObjectiveGather.cs` itself did NOT change)

**18 target classes SAFE** (unchanged in update):

- `ItemActionEntryCraft.cs` (transpiler target)
- `XUiC_RecipeList.cs`, `XUiC_RecipeCraftCount.cs` (transpiler target)
- `XUiM_PlayerInventory.cs` (6 patches)
- `XUiC_IngredientEntry.cs` (transpiler target)
- `AnimatorRangedReloadState.cs` (transpiler target)
- `ItemActionEntryPurchase.cs` (transpiler target)
- `XUiC_PowerSourceStats.cs` (transpiler target)
- `ChallengeObjectiveGather.cs` (3 reflection-resolved patches)
- `XUiC_LootContainer.cs`, `XUiC_VehicleStorageWindowGroup.cs`,
  `XUiC_VehicleContainer.cs`
- `XUiC_WorkstationWindowGroup.cs`, `XUiC_WorkstationOutputGrid.cs`
- `ItemActionTextureBlock.cs`, `ItemActionRepair.cs`
- `XUiM_Vehicle.cs`, `XUiC_HUDStatBar.cs`, `Bag.cs`

### Transpiler Safety Assessment

All 7 transpilers use `RobustTranspiler.SafeTranspile` which returns
original unmodified IL on any failure — a transpiler target change
**cannot crash the game**, it can only disable the feature with a
log warning.

| Transpiler target | File changed? | IL target present? | Verdict |
| --- | --- | --- | --- |
| `ItemActionEntryCraft.hasItems` | No | N/A | SAFE |
| `XUiC_RecipeCraftCount.calcMaxCraftable` | No | N/A | SAFE |
| `XUiC_IngredientEntry.GetBindingValueInternal` | No | N/A | SAFE |
| `AnimatorRangedReloadState.GetAmmoCountToReload` | No | N/A | SAFE |
| `ItemActionEntryPurchase.RefreshEnabled` | No | N/A | SAFE |
| `XUiC_PowerSourceStats.BtnRefuel_OnPress` | No | N/A | SAFE |
| `EntityVehicle.takeFuel` | **Yes** | `Bag.DecItem` call confirmed | LIKELY SAFE |

The `takeFuel` transpiler replaces `Bag.DecItem` → `DecItemForRefuel`.
The current decompiled `takeFuel` still calls `entityPlayer.bag.DecItem`
at the expected location. `Bag.cs` itself did not change. The adaptive
fallback (`AdaptiveMethodFinder`) provides additional resilience.

### Dew Collector Fix Verification

The `CreateDewCollectorItemsAccessor` resolution chain is correct:

1. `AccessTools.PropertyGetter("Items")` — **MATCHES** current build
   (uppercase `Items` property on `TileEntityCollector`)
2. Fallback to `"items"` property → old builds
3. Fallback to `"itemsInternal"` / `"itemsArr"` / `"items"` fields
4. Final fallback returns null accessor (avoids crash)

The `Items` property getter returns the `itemsInternal` backing array
directly (no copy), so modifications to `ItemStack.count` within the
array persist. The setter uses `safeIstackArrayCopy` (copy-into, not
replace), but ProxiCraft only reads, never sets. **Correct.**

### New Game Feature: Dew Collector UI Overhaul

The update introduced 5 new dew collector UI classes:

- `XUiC_DewCollectorContainer.cs`
- `XUiC_DewCollectorModGrid.cs`
- `XUiC_DewCollectorWindow.cs`
- `XUiC_DewCollectorWindowGroup.cs`
- `XUiC_CollectorFuelGrid.cs`

These replace the previous workstation-style handling with a dedicated
dew collector window. The `XUiC_DewCollectorWindowGroup.OnClose` calls
`GameManager.Instance.TEUnlockServer(...)` which ProxiCraft patches —
this connection still works.

**New concern:** When ProxiCraft removes items from a dew collector's
`TileEntityCollector` data (via `StorageSourceInfo.MarkModified()` →
`TileEntity.SetModified()`), the new dew collector UI may not refresh
until closed/reopened if the `OnTileEntityChanged` callback isn't
triggered. See PC-22422060-006 below.

### StackLocationTypes Enum Change

`XUiC_ItemStack.StackLocationTypes` gained 3 new entries: `DewCollector`,
`Cosmetics`, `Part`. Existing values (`Backpack`, `ToolBelt`,
`LootContainer`, `Equipment`, `Creative`, `Vehicle`, `Workstation`,
`Merge`) are unchanged. ProxiCraft's comparisons use explicit equality
checks against existing values — **no impact from new entries**.

Note: `XUiC_DewCollectorContainer.SetSlots` sets its stacks to
`StackLocationTypes.LootContainer`, not the new `DewCollector` type.
So ProxiCraft's `LootContainer` filter in `ItemStack_SlotChanged_Patch`
correctly skips dew collector UI slot changes.

### Signature Compatibility Detail

All prefix/postfix patches use Harmony subset parameter matching. Verified
compatible pairs:

| Patch | Param subset | Game method full signature | Result |
| --- | --- | --- | --- |
| TELockServer Postfix | `(GM, int, V3i, int)` | `(int, V3i, int, int, string=)` | OK |
| TEUnlockServer Postfix | `(GM, int, V3i, int)` | `(int, V3i, int, bool=)` | OK |
| ClearTELock Prefix | `(GM, int, out V3i)` | `(int)` | OK |
| CanReload Postfix | `(IAR, IAD, ref bool)` | `(IAD)` | OK |
| HandleSlotChange Postfix | `(XIS)` | `()` | OK |
| UserLockedSlot Postfix | `(XIS, bool)` | `(bool)` setter | OK |
| Inventory.DecItem Postfix | `()` | `(IV, int, bool=, IList=)` | OK |

### `CanReload` Behavioral Analysis

ProxiCraft's postfix accesses `__instance.MagazineItemNames`,
`BulletsPerMagazine`, and `isJammed()` — all confirmed present with
unchanged signatures in the updated `ItemActionRanged.cs`. The postfix
mirrors vanilla's magazine-full check before adding container ammo to
its count. **COMPATIBLE.**

### `lockedTileEntities` Dictionary Access

ProxiCraft's `ClearTileEntityLockForClient` prefix reads
`__instance.lockedTileEntities` as `Dictionary<ITileEntity, int>`. The
updated game code uses this same dictionary in `TELockServer`,
`TEUnlockServer`, and `ClearTileEntityLockForClient`. The dictionary
type is unchanged. ProxiCraft defensively uses `.ToArray()` snapshot
before iterating. **COMPATIBLE.**

## Status Legend

- `open`: confirmed issue not fixed yet
- `in-progress`: implementation started, validation pending
- `blocked`: cannot proceed until a prerequisite is resolved
- `verified`: fixed and validated
- `watch`: changed vanilla behavior — targeted testing required
- `new-risk`: identified during deep QA, no break confirmed yet

## Catalog

| ID | Severity | Status | Subsystem | Evidence | Intended behavior | Action |
| --- | --- | --- | --- | --- | --- | --- |
| PC-22422060-001 | P0 | in-progress | Dew collector storage access | `ContainerManager.cs` used `TileEntityCollector.items`; game now uses `Items` (uppercase) with internalized storage | Dew collector contents count and consume like any nearby storage source | Version-tolerant accessor confirmed correct; `Items` property resolves on current build; runtime behavior test pending |
| PC-22422060-002 | P1 | watch | Game lifecycle patches | 6 GameManager patches target changed methods | Session cleanup, lock sync, network safety must work without desync | Deep signature + behavioral audit PASSED: all param subsets compatible, `lockedTileEntities` dict unchanged, `ConnectionManager` patterns unchanged |
| PC-22422060-003 | P1 | watch | Inventory and recipe count spoofing | `Inventory.DecItem` changed; `XUiC_ItemStack` changed (new enum entries) | UI counts and craftability remain additive and accurate | Signature audit PASSED; `StackLocationTypes` existing values unchanged; new `DewCollector`/`Cosmetics`/`Part` entries don't affect equality checks |
| PC-22422060-004 | P1 | watch | Reload and ammo flows | `ItemActionRanged.CanReload` changed | Reload and ammo display include nearby storage | Behavioral audit PASSED: `MagazineItemNames`, `BulletsPerMagazine`, `isJammed()` all present and unchanged |
| PC-22422060-005 | P2 | watch | Vehicle refuel flows | `EntityVehicle.takeFuel` (transpiler target) changed | Vehicle refuel uses nearby containers when bag insufficient | Transpiler audit: `Bag.DecItem` call confirmed still present in `takeFuel`; `RobustTranspiler` provides safe fallback; `Bag.cs` unchanged |
| PC-22422060-006 | P2 | new-risk | Dew collector UI sync | Game introduced `XUiC_DewCollectorWindowGroup` with dedicated OnClose/OnOpen; ProxiCraft removes items via `TileEntity.SetModified()` | Dew collector UI should refresh after ProxiCraft removes items from it | If the new UI doesn't subscribe to `SetModified`, items could visually remain until window is closed and reopened; test by removing dew collector water via crafting while the dew collector window is open |
| PC-22422060-007 | P3 | new-risk | Challenge reflection cache | `challenges.xml` changed; 4 `XUiC_Challenge*.cs` files changed | Challenge gather objectives should correctly count nearby storage items | `ChallengeObjectiveGather.cs` itself unchanged; reflection targets (`HandleUpdatingCurrent`, `CheckComplete`, status text) should be stable; challenge UI layout may have shifted but data flow is unchanged |

## Verification Matrix

### Compile gate

- `dotnet build` succeeded with `0 Warning(s)` and `0 Error(s)`.

### Transpiler health check (first launch)

Check the ProxiCraft log file for `[FeatureId] Transpiler could not find
injection point` warnings. If any appear, the corresponding feature is
auto-disabled. Specifically watch for `VehicleRefuel` since
`EntityVehicle.takeFuel` changed.

### Single-player behavior checks

- [ ] Craft from nearby containers — ingredient counts correct
- [ ] Recipe list shows combined player + storage counts
- [ ] Take Like behavior preserved
- [ ] Dew collector output counted and consumed correctly
- [ ] Dew collector UI refreshes after ProxiCraft-triggered removal
- [ ] Reload with ammo from nearby storage
- [ ] Ammo HUD count includes nearby storage
- [ ] Refuel vehicle from nearby gas cans
- [ ] Repair with materials from nearby storage
- [ ] Generator refuel from nearby storage
- [ ] Texture block placement uses nearby materials
- [ ] Challenge gather objectives count nearby items

### Multiplayer safety checks

- [ ] Host and client both using ProxiCraft
- [ ] Client without ProxiCraft during handshake window
- [ ] Container lock and unlock synchronization
- [ ] Orphan lock cleanup on disconnect

### Dew collector UI sync test (new)

1. Place dew collector with water output
2. Open dew collector window
3. Place storage crate nearby with crafting materials that include
   the dew collector's output item
4. Craft from the crafting menu — observe whether dew collector
   window visually updates
5. If not: confirm items ARE actually removed (reopen window) and
   note the UI sync gap

## Risk Summary

| Risk Level | Count | Items |
| --- | --- | --- |
| SAFE (no code change or transpiler target) | 37/48 patches | All recipe, repair, workstation, loot, currency, HUD patches |
| LOW (changed target, verified compatible) | 9/48 patches | GameManager (6), Inventory.DecItem, XUiC_ItemStack (2) |
| MEDIUM (runtime verification needed) | 2/48 patches | EntityVehicle.takeFuel transpiler, ChallengeObjectiveGather reflection |
| NEW RISK (update introduced new surface) | 1 concern | Dew collector UI sync with new XUiC_DewCollectorWindowGroup |

## Next Steps

1. Launch game with ProxiCraft loaded; check log for transpiler status
2. Run single-player behavior checks above
3. Specifically test dew collector UI sync (PC-22422060-006)
4. Mark items verified as they pass runtime testing
