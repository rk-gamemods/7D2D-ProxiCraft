# ProxiCraft QA Analysis Report

**Date:** December 2024  
**Branch:** feature/network-diagnostics (v1.2.1)  
**Analysis Method:** Systematic toolkit queries against callgraph.db

## Executive Summary

Used the 7D2D Mod Maintenance Toolkit to analyze ProxiCraft's patch coverage. 

**UPDATE:** Initial analysis flagged 2 "bugs" that turned out to be false positives. User testing confirmed features work correctly. **This led to major toolkit enhancements - see below.**

## ✅ Toolkit Enhancements Implemented

The false positives led to implementing proper event flow analysis:

### New Tables Added (schema.sql)
- `event_subscriptions` - Tracks who subscribes to events (`+=` pattern)
- `event_fires` - Tracks where events are invoked
- `event_declarations` - Catalogs all event definitions

### New CLI Commands (QueryDb)

**1. `events <name>`** - Show event subscriptions and fires
```bash
QueryDb callgraph.db events DragAndDropItemChanged
```
**Key Finding:** All 3 challenge objective types subscribe to the SAME event!

**2. `flow <event-or-method>`** - Trace complete behavioral flow
```bash
QueryDb callgraph.db flow DragAndDropItemChanged
```

**3. `effective <method>`** - Show patched vs vanilla behavior
```bash
QueryDb callgraph.db effective GetItemCount
```

### Extraction Results (After Enhancement)
```
Event declarations: 307
Event subscriptions: 1,917
Event fires: 407
```

---

## ⚠️ Original Toolkit Limitations (Now Fixed)

This analysis revealed a gap in the original toolkit's capabilities:

**The toolkit traced STATIC call graphs only.** It couldn't trace:
1. **Harmony patch behavior** - Postfix patches that modify results after vanilla runs
2. **Event subscriptions** - What callbacks fire when events are invoked
3. **Mod-to-game interaction patterns** - How mod patches change the effective behavior

**Example of False Positive:**
- Toolkit showed `ChallengeObjectiveGatherByTag.HandleUpdatingCurrent` calls `Bag.GetItemCount()` directly
- This was flagged as "bypassing ProxiCraft's patches"
- **Reality:** ProxiCraft patches `ChallengeObjectiveGather.HandleUpdatingCurrent` with a Postfix that adds container counts AFTER vanilla runs
- The event-driven architecture (`DragAndDropItemChanged`) triggers all challenge objectives to recount
- User testing confirmed tag-based challenges DO count container items correctly

**Enhancements now implemented to address this.**

## Analysis Results

### ✅ GetItemCount Overloads - COMPLETE
Both `XUiM_PlayerInventory.GetItemCount` overloads are patched:
- `GetItemCount(ItemValue)` - patched in `XUiM_PlayerInventory_GetItemCount_Patch`
- `GetItemCount(int)` - patched in `XUiM_PlayerInventory_GetItemCountInt_Patch`

**Toolkit Query:**
```
dotnet run -- callgraph.db impl GetItemCount
```

### ✅ HasItems/HasItem - COMPLETE
Both overloads are covered:
- `HasItem(ItemStack)` delegates to `HasItems(IList<ItemStack>)` which IS patched
- `HasItem(ItemValue)` is patched directly in `XUiM_PlayerInventory_HasItem_ItemValue_Patch`

### ✅ RemoveItems - COMPLETE
- `XUiM_PlayerInventory.RemoveItems` is patched in `XUiM_PlayerInventory_RemoveItems_Patch`
- `RemoveItem(ItemStack)` delegates to `RemoveItems` - covered

### ✅ CanReload Inheritance - COMPLETE
Inheritance chain verified:
```
ItemActionAttack.CanReload (base virtual)
  └── ItemActionRanged.CanReload (override) ← ProxiCraft patches HERE
        └── ItemActionLauncher
              └── ItemActionCatapult.CanReload (override)
                    └── calls base.CanReload() ← STILL PATCHED (chains up)
```

All ranged weapons including catapults are covered because `ItemActionCatapult.CanReload` calls `base.CanReload()`.

### ✅ Trader (CanSwapItems) - COMPLETE
- `CanSwapItems` patched for both buying and selling
- `RemoveItem` delegates to `RemoveItems` which is patched

### ✅ Workstation Output Sync - COMPLETE
- Proper `SetModified()` calls on `TileEntityWorkstation`
- UI-based removal via `RemoveFromWorkstationOutputUI()` handles live edits

---

## ~~🐛 BUGS FOUND~~ FALSE POSITIVES

The following were initially flagged as bugs but **user testing confirmed they work correctly**:

### ~~Bug #1:~~ ChallengeObjectiveGatherByTag - WORKS CORRECTLY

**Initial Analysis:** Flagged as not patched because it directly calls `Bag.GetItemCount(FastTags)`.

**Why It Actually Works:**
1. ProxiCraft fires `DragAndDropItemChanged` when container contents change
2. `ChallengeObjectiveGatherByTag` subscribes to this event (line 134 in game code)
3. Event triggers `HandleUpdatingCurrent()` which recounts
4. ProxiCraft's `ChallengeObjectiveGather_HandleUpdatingCurrent_Patch` postfix adds container counts

**Toolkit Gap:** Static call analysis can't see event-driven behavior or postfix modifications.

---

### ~~Bug #2:~~ ChallengeObjectiveGatherIngredient - WORKS CORRECTLY  

**Initial Analysis:** Flagged as not patched because it directly calls `Bag.GetItemCount(ItemValue)`.

**Why It Actually Works:** Same event-driven architecture as above. The `DragAndDropItemChanged` event triggers recounting, and postfix patches correct the totals.

---

## Recommendations

### Toolkit Improvements Needed

1. **Mod Patch Registry** - Track Harmony patches applied at runtime
2. **Event Flow Analysis** - Map event subscriptions and their handlers  
3. **Effective Call Graph** - Show call relationships AS MODIFIED by patches, not just vanilla
4. **Integration Testing Hooks** - Ability to validate actual runtime behavior, not just static analysis

---

## Validation Methodology

All queries executed against `7D2D-DecompilerScript/callgraph.db`:

| Query | Purpose | Result |
|-------|---------|--------|
| `impl GetItemCount` | Find all overloads | Found 12 implementations |
| `impl HasItem` | Find all overloads | Found 7 implementations |
| `impl RemoveItems` | Find removal methods | Found 2 implementations |
| `impl CanReload` | Check inheritance | Found 3 implementations |
| `impl HandleUpdatingCurrent` | Find challenge handlers | Found 5 implementations ← KEY |
| `callers "XUiM_PlayerInventory.CanSwapItems"` | Verify trader usage | Confirmed 2 callers |

---

## Files Analyzed

- `ProxiCraft/ProxiCraft/ProxiCraft.cs` (3,240 lines)
- `ProxiCraft/ProxiCraft/ContainerManager.cs` (2,298 lines)
- `7D2DCodebase/Assembly-CSharp/Challenges/ChallengeObjectiveGatherByTag.cs`
- `7D2DCodebase/Assembly-CSharp/Challenges/ChallengeObjectiveGatherIngredient.cs`
- `7D2DCodebase/Assembly-CSharp/XUiM_PlayerInventory.cs`
