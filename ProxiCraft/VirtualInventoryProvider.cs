using System;
using System.Collections.Generic;

namespace ProxiCraft;

/// <summary>
/// Central provider for "Virtual Inventory" operations (SCVI Architecture).
/// 
/// This class implements the "Count Spoofing" pattern where:
/// - GetTotalItemCount() returns inventory + storage counts for UI display
/// - ConsumeItems() removes items in priority order: Bag → Toolbelt → Storage
/// 
/// DESIGN RATIONALE (from review-FINAL-consensus.md):
/// - Centralizes all storage-aware item operations in one place
/// - Patches become thin wrappers that delegate here
/// - Bugs fixed globally affect all features
/// - Multiplayer safety checks are applied consistently
/// 
/// MULTIPLAYER SAFETY:
/// When MultiplayerModTracker.IsLocked is true:
/// - GetTotalItemCount() returns inventory-only counts (no storage)
/// - ConsumeItems() skips storage entirely
/// This prevents CTD when server doesn't have ProxiCraft installed.
/// 
/// USAGE:
/// - Enhanced Safety features use this provider when enabled
/// - Legacy code paths continue to work when Enhanced Safety is disabled
/// - All methods are safe to call regardless of config state
/// </summary>
public static class VirtualInventoryProvider
{
    // ─────────────────────────────────────────────────────────────────────
    // READ: How many items does the player "virtually" have?
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Gets the total count of an item including inventory and nearby storage.
    /// 
    /// Priority: Bag + Toolbelt + Storage (all combined)
    /// 
    /// SAFETY: Returns inventory-only count if:
    /// - Mod is disabled
    /// - Multiplayer safety lock is active
    /// - Player reference is null
    /// </summary>
    /// <param name="player">The player entity</param>
    /// <param name="item">The item to count</param>
    /// <returns>Total count from all sources</returns>
    public static int GetTotalItemCount(EntityPlayerLocal player, ItemValue item)
    {
        if (player == null || item == null)
            return 0;

        // Count from player's bag and toolbelt
        int bagCount = player.bag?.GetItemCount(item) ?? 0;
        int toolbeltCount = player.inventory?.GetItemCount(item) ?? 0;
        int inventoryCount = bagCount + toolbeltCount;

        // SAFETY: Skip storage if mod disabled or multiplayer locked
        if (!ProxiCraft.Config?.modEnabled == true)
            return inventoryCount;

        if (MultiplayerModTracker.IsLocked)
        {
            LogDiagnostic($"GetTotalItemCount: Multiplayer locked, returning inventory-only count for {item.ItemClass?.GetItemName()}");
            return inventoryCount;
        }

        // Add storage count
        int storageCount = ContainerManager.GetItemCount(ProxiCraft.Config, item);
        int totalCount = inventoryCount + storageCount;

        LogDiagnostic($"GetTotalItemCount: {item.ItemClass?.GetItemName()} = {bagCount} bag + {toolbeltCount} toolbelt + {storageCount} storage = {totalCount}");

        return totalCount;
    }

    /// <summary>
    /// Gets the total count of an item by item ID.
    /// Convenience overload for code paths that use item IDs directly.
    /// </summary>
    public static int GetTotalItemCount(EntityPlayerLocal player, int itemId)
    {
        if (itemId <= 0)
            return 0;

        var itemClass = ItemClass.GetForId(itemId);
        if (itemClass == null)
            return 0;

        return GetTotalItemCount(player, new ItemValue(itemId));
    }

    // ─────────────────────────────────────────────────────────────────────
    // WRITE: Consume items, prioritizing Bag → Toolbelt → Storage
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Consumes items from the player's inventory and nearby storage.
    /// 
    /// Priority order: Bag → Toolbelt → Storage
    /// 
    /// This implements "Optimistic Commit" - removes what's available and returns
    /// the actual amount removed. Callers should check the return value.
    /// 
    /// SAFETY: Skips storage if:
    /// - Mod is disabled
    /// - Multiplayer safety lock is active
    /// - Player reference is null
    /// </summary>
    /// <param name="player">The player entity</param>
    /// <param name="item">The item to consume</param>
    /// <param name="count">The number to consume</param>
    /// <param name="removedItems">Optional list to track removed items (for vanilla compatibility)</param>
    /// <returns>The number of items actually removed</returns>
    public static int ConsumeItems(EntityPlayerLocal player, ItemValue item, int count, IList<ItemStack> removedItems = null)
    {
        if (player == null || item == null || count <= 0)
            return 0;

        int totalRemoved = 0;
        int remaining = count;

        // 1. Remove from Bag first (standard game order)
        if (remaining > 0 && player.bag != null)
        {
            int removed = player.bag.DecItem(item, remaining, false, removedItems);
            totalRemoved += removed;
            remaining -= removed;
            LogDiagnostic($"ConsumeItems: Removed {removed} from bag, {remaining} remaining");
        }

        // 2. Remove from Toolbelt (standard game order)
        if (remaining > 0 && player.inventory != null)
        {
            int removed = player.inventory.DecItem(item, remaining, false, removedItems);
            totalRemoved += removed;
            remaining -= removed;
            LogDiagnostic($"ConsumeItems: Removed {removed} from toolbelt, {remaining} remaining");
        }

        // 3. Remove from Storage (ProxiCraft feature)
        // SAFETY: Skip if mod disabled or multiplayer locked
        if (remaining > 0)
        {
            if (!ProxiCraft.Config?.modEnabled == true)
            {
                LogDiagnostic($"ConsumeItems: Mod disabled, skipping storage");
                return totalRemoved;
            }

            if (MultiplayerModTracker.IsLocked)
            {
                LogDiagnostic($"ConsumeItems: Multiplayer locked, skipping storage");
                return totalRemoved;
            }

            int removed = ContainerManager.RemoveItems(ProxiCraft.Config, item, remaining);
            totalRemoved += removed;
            remaining -= removed;
            LogDiagnostic($"ConsumeItems: Removed {removed} from storage, {remaining} remaining");
        }

        LogDiagnostic($"ConsumeItems: {item.ItemClass?.GetItemName()} - requested {count}, removed {totalRemoved}");
        return totalRemoved;
    }

    /// <summary>
    /// Checks if the player has at least the specified count of an item.
    /// 
    /// This is a convenience method that avoids the overhead of ConsumeItems
    /// when you just need to check availability.
    /// </summary>
    /// <param name="player">The player entity</param>
    /// <param name="item">The item to check</param>
    /// <param name="count">The minimum count required</param>
    /// <returns>True if the player has at least 'count' items available</returns>
    public static bool HasItems(EntityPlayerLocal player, ItemValue item, int count)
    {
        return GetTotalItemCount(player, item) >= count;
    }

    /// <summary>
    /// Checks if the player has all items in the given list.
    /// </summary>
    /// <param name="player">The player entity</param>
    /// <param name="items">List of required items with counts</param>
    /// <param name="multiplier">Multiplier for item counts (e.g., craft count)</param>
    /// <returns>True if the player has all required items</returns>
    public static bool HasAllItems(EntityPlayerLocal player, IList<ItemStack> items, int multiplier = 1)
    {
        if (items == null || items.Count == 0)
            return true;

        foreach (var item in items)
        {
            if (item == null || item.IsEmpty())
                continue;

            int required = item.count * multiplier;
            if (!HasItems(player, item.itemValue, required))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Consumes all items in the given list.
    /// 
    /// WARNING: This is NOT atomic. If consumption fails partway through,
    /// some items will have been removed. Use HasAllItems() first to check
    /// if the operation will succeed.
    /// </summary>
    /// <param name="player">The player entity</param>
    /// <param name="items">List of items to consume</param>
    /// <param name="multiplier">Multiplier for item counts</param>
    /// <returns>True if all items were successfully consumed</returns>
    public static bool ConsumeAllItems(EntityPlayerLocal player, IList<ItemStack> items, int multiplier = 1)
    {
        if (items == null || items.Count == 0)
            return true;

        bool allConsumed = true;

        foreach (var item in items)
        {
            if (item == null || item.IsEmpty())
                continue;

            int required = item.count * multiplier;
            int consumed = ConsumeItems(player, item.itemValue, required);

            if (consumed < required)
            {
                ProxiCraft.LogWarning($"ConsumeAllItems: Only consumed {consumed}/{required} of {item.itemValue.ItemClass?.GetItemName()}");
                allConsumed = false;
            }
        }

        return allConsumed;
    }

    // ─────────────────────────────────────────────────────────────────────
    // DIAGNOSTICS
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Logs a diagnostic message if enhanced safety diagnostic logging is enabled.
    /// </summary>
    private static void LogDiagnostic(string message)
    {
        if (ProxiCraft.Config?.enhancedSafetyDiagnosticLogging == true)
        {
            ProxiCraft.FileLog($"[VIP] {message}");
        }
    }

    /// <summary>
    /// Gets a diagnostic report of current provider state.
    /// Useful for debugging and console commands.
    /// </summary>
    public static string GetDiagnosticReport()
    {
        var player = GameManager.Instance?.World?.GetPrimaryPlayer();
        if (player == null)
            return "VirtualInventoryProvider: No player loaded";

        var config = ProxiCraft.Config;
        bool mpLocked = MultiplayerModTracker.IsLocked;

        return $@"VirtualInventoryProvider Status:
  Mod Enabled: {config?.modEnabled}
  Multiplayer Locked: {mpLocked}
  Enhanced Safety Flags:
    Crafting: {config?.enhancedSafetyCrafting}
    Reload: {config?.enhancedSafetyReload}
    Repair: {config?.enhancedSafetyRepair}
    Vehicle: {config?.enhancedSafetyVehicle}
    Refuel: {config?.enhancedSafetyRefuel}
  Diagnostic Logging: {config?.enhancedSafetyDiagnosticLogging}";
    }
}
