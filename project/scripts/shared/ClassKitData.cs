namespace MankersKingdoms.Shared;

/// <summary>One item stack in a class starting kit (itemId + quantity).</summary>
public sealed record ClassKitItem(string ItemId, int Count);

/// <summary>
/// Immutable definition of a player class kit — the items distributed on joining a world.
/// Pure C# — no Godot dependency, testable in xUnit.
///
/// ClassId: stable string ID, e.g. "class.fighter". Referenced by GameSession.ChosenClassId.
/// DisplayNameKey: Loc key for the class name shown in ClassSelectScreen.
/// Str / Dex: base stats for this class, used by CombatResolver (combat.md §2.2/§4).
/// StartingItems: items given to the player on connect. Each entry is (ItemId, Count).
///
/// See docs/gdd/character-creation.md §4 and VERTICAL_SLICE.md §3.5.
/// Kits are authored in ClassKitRegistry.
/// </summary>
public sealed record ClassKitData(
    string         ClassId,
    string         DisplayNameKey,
    int            Str,
    int            Dex,
    ClassKitItem[] StartingItems
);
