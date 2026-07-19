namespace MankersKingdoms.Shared;

/// <summary>
/// Tier classification for a monster nest. Drives dot size on the world map and minimap,
/// and determines the prestige of loot available from that nest.
///
/// Minor — standard small nest (wolves, goblins). Small dot on map.
/// Major — high-value heavily-guarded nest (raider camp with orc). Large dot on map.
///
/// See VERTICAL_SLICE.md §3.6 and docs/gdd/worldgen.md §5.
/// </summary>
public enum NestTier
{
    Minor = 0,
    Major = 1,
}
