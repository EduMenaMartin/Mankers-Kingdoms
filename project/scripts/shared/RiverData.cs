using System.Collections.Generic;

namespace MankersKingdoms.Shared;

/// <summary>
/// Output of RiverGenerator.Generate(). Contains the river path and the channel mask.
///
/// The channel mask marks every grid cell that was modified by the cosine carving pass
/// (not just the path centre — the full carved width on both banks). Both TreeGenerator
/// and BushGenerator accept this mask to exclude placement inside the carved channel.
/// </summary>
public sealed class RiverData
{
    /// <summary>Ordered list of river segments from source to mouth.</summary>
    public IReadOnlyList<RiverSegment> Segments { get; }

    /// <summary>
    /// [gridX, gridZ] — true for every cell that was touched by the carving pass.
    /// Use IsInChannel(x, z) for bounds-safe lookup.
    /// </summary>
    public bool[,] ChannelMask { get; }

    public RiverData(IReadOnlyList<RiverSegment> segments, bool[,] channelMask)
    {
        Segments    = segments;
        ChannelMask = channelMask;
    }

    /// <summary>Returns true if the given grid cell is inside the carved river channel.</summary>
    public bool IsInChannel(int gridX, int gridZ)
    {
        if (gridX < 0 || gridX >= ChannelMask.GetLength(0)) return false;
        if (gridZ < 0 || gridZ >= ChannelMask.GetLength(1)) return false;
        return ChannelMask[gridX, gridZ];
    }
}
