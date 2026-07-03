namespace MankersKingdoms.Shared;

/// <summary>
/// Shared static accessor for local-player runtime state.
/// Written by server-side systems (InventorySystem, NeedsSystem, …) when they
/// receive a state update meant for this peer; read by client-side HUD nodes.
///
/// This pattern avoids a client/ → server/ import while still letting both sides
/// communicate through a shared neutral location. Not persisted — reset on scene change.
/// </summary>
public static class LocalState
{
    public static PlayerInventory Inventory { get; private set; } = new();
    public static float Hunger    { get; private set; } = 100f;
    public static float Rest      { get; private set; } = 100f;
    /// <summary>
    /// True once the local player has successfully planted a Kingdom Marker.
    /// Set by SettlementSystem.SpawnMarker (runs on all peers) when peerId matches
    /// the local peer. Used by BuildMenu to grey out Place buttons for guests.
    /// Server-side enforcement is independent — this is UX only.
    /// </summary>
    public static bool IsFounder  { get; private set; } = false;

    public static void SetFounder() => IsFounder = true;

    /// <summary>
    /// Called by InventorySystem when the authoritative server sends a state snapshot
    /// for the local peer. Replaces the whole inventory (snapshot semantics).
    /// </summary>
    public static void SetInventory(PlayerInventory inv) => Inventory = inv;

    /// <summary>
    /// Called by NeedsSystem when the server broadcasts a needs snapshot for the local peer.
    /// </summary>
    public static void SetNeeds(float hunger, float rest)
    {
        Hunger = hunger;
        Rest   = rest;
    }
}
