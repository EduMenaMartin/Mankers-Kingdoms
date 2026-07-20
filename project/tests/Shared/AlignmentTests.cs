using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

/// <summary>
/// Tests for the Alignment enum and AlignmentExtensions
/// (character-creation.md §11).
/// </summary>
public class AlignmentTests
{
    // ── FromString ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("lawful",  Alignment.Lawful)]
    [InlineData("LAWFUL",  Alignment.Lawful)]
    [InlineData("Lawful",  Alignment.Lawful)]
    [InlineData("chaotic", Alignment.Chaotic)]
    [InlineData("CHAOTIC", Alignment.Chaotic)]
    [InlineData("neutral", Alignment.Neutral)]
    [InlineData("NEUTRAL", Alignment.Neutral)]
    public void FromString_KnownValues_ParseCorrectly(string input, Alignment expected)
    {
        Assert.Equal(expected, AlignmentExtensions.FromString(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("evil")]
    [InlineData("good")]
    [InlineData("random_garbage")]
    public void FromString_UnknownOrNull_DefaultsToNeutral(string? input)
    {
        Assert.Equal(Alignment.Neutral, AlignmentExtensions.FromString(input));
    }

    // ── ToLocKey ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(Alignment.Lawful,  "alignment.lawful")]
    [InlineData(Alignment.Neutral, "alignment.neutral")]
    [InlineData(Alignment.Chaotic, "alignment.chaotic")]
    public void ToLocKey_ReturnsExpectedKey(Alignment alignment, string expectedKey)
    {
        Assert.Equal(expectedKey, alignment.ToLocKey());
    }

    // ── Round-trip ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(Alignment.Lawful)]
    [InlineData(Alignment.Neutral)]
    [InlineData(Alignment.Chaotic)]
    public void RoundTrip_ToStringAndBack_Preserves(Alignment alignment)
    {
        string serialized = alignment.ToString().ToLowerInvariant();
        Assert.Equal(alignment, AlignmentExtensions.FromString(serialized));
    }
}
