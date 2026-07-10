namespace MankersKingdoms.Shared;

/// <summary>
/// A tool-tier unlock attached to a skill: when the skill reaches <see cref="MinLevel"/>,
/// the server grants <see cref="GrantedItemId"/> to the player automatically.
///
/// Example: Woodcutting level 15 → "item.tool.bronze_axe"
/// See docs/gdd/skills.md §5.
/// </summary>
public sealed record ToolTierData(int MinLevel, string GrantedItemId);
