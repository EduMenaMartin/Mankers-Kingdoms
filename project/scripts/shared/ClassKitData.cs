namespace MankersKingdoms.Shared;

/// <summary>One item stack in a class starting kit (itemId + quantity).</summary>
public sealed record ClassKitItem(string ItemId, int Count);

/// <summary>
/// Immutable definition of a player class kit — the items distributed on joining a world.
/// Pure C# — no Godot dependency, testable in xUnit.
///
/// ClassId: stable string ID, e.g. "class.fighter". Referenced by GameSession.ChosenClassId.
/// DisplayNameKey: Loc key for the class name shown in ClassSelectScreen.
/// StartingItems: items given to the player on connect. Each entry is (ItemId, Count).
///
/// See docs/gdd/character-creation.md §4 for class kit design intent.
/// Kits are authored in ClassKitRegistry.
/// </summary>
public sealed record ClassKitData(
    string        ClassId,
    string        DisplayNameKey,
    ClassKitItem[] StartingItems
);
