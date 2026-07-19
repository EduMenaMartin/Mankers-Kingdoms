namespace MankersKingdoms.Shared;

/// <summary>
/// Immutable data for one step along a procedurally generated river path.
/// Produced by RiverGenerator, consumed by WaterSystem (mesh + collision)
/// and by TerrainSystem (carving pass).
///
/// UV convention (drives water_river.gdshader):
///   UV.x = [0, 1] across the river width (left bank = 0, right bank = 1)
///   UV.y = arcLength / totalLength (0 = source, 1 = mouth — scrolling this creates downstream flow)
/// </summary>
public sealed record RiverSegment(
    int   GridX,
    int   GridZ,
    float WorldX,
    float WaterY,      // world-space Y of the water surface at this segment
    float WorldZ,
    float TangentX,    // normalised downstream direction — X component
    float TangentZ,    // normalised downstream direction — Z component
    float HalfWidthM   // half-width in world units (metres); total river width = 2 × HalfWidthM
);
