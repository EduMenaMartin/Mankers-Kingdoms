using System.Collections.Generic;

namespace MankersKingdoms.Shared;

/// <summary>One item stack in a class starting kit (itemId + quantity).</summary>
public sealed record ClassKitItem(string ItemId, int Count);

/// <summary>One flat bonus applied to a skill on class selection (skillId + amount).</summary>
public sealed record ClassSkillBump(string SkillId, int Amount);

/// <summary>
/// Immutable definition of a player class kit — the items and skill bumps distributed on joining.
/// Pure C# — no Godot dependency, testable in xUnit.
///
/// ClassId:        stable string ID, e.g. "class.fighter". Referenced by GameSession.ChosenClassId.
/// DisplayNameKey: Loc key for the class name shown in CharacterCreateScreen.
/// SkillBumps:     flat starting bonuses added on top of race+stat-derived skill caps (M5).
/// StartingItems:  items given to the player on connect. Each entry is (ItemId, Count).
///
/// Note: Str/Dex were removed in M5 — stats are now rolled by the player, not derived from class.
///
/// See docs/gdd/character-creation.md §4 and VERTICAL_SLICE.md §3.5.
/// Kits are authored in ClassKitRegistry.
/// </summary>
public sealed record ClassKitData(
    string           ClassId,
    string           DisplayNameKey,
    ClassSkillBump[] SkillBumps,
    ClassKitItem[]   StartingItems
);
