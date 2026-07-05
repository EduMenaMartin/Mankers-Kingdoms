using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

public class ProjectileStateTests
{
    [Fact]
    public void Constructor_StoresAllFields()
    {
        var ps = new ProjectileState(1L, 2L, "weapon.shortbow",
            10f, 1.5f, 5f, 3f, 0f, -2f);

        Assert.Equal(1L,              ps.Id);
        Assert.Equal(2L,              ps.OriginPeerId);
        Assert.Equal("weapon.shortbow", ps.WeaponId);
        Assert.Equal(10f,             ps.PosX);
        Assert.Equal(1.5f,            ps.PosY);
        Assert.Equal(5f,              ps.PosZ);
        Assert.Equal(3f,              ps.VelX);
        Assert.Equal(0f,              ps.VelY);
        Assert.Equal(-2f,             ps.VelZ);
    }

    [Fact]
    public void RecordEquality_SameValues_Equal()
    {
        var a = new ProjectileState(1L, 2L, "weapon.shortbow", 0, 0, 0, 1, 0, 0);
        var b = new ProjectileState(1L, 2L, "weapon.shortbow", 0, 0, 0, 1, 0, 0);
        Assert.Equal(a, b);
    }

    [Fact]
    public void RecordEquality_DifferentId_NotEqual()
    {
        var a = new ProjectileState(1L, 2L, "weapon.shortbow", 0, 0, 0, 1, 0, 0);
        var b = new ProjectileState(2L, 2L, "weapon.shortbow", 0, 0, 0, 1, 0, 0);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void RecordEquality_DifferentOriginPeer_NotEqual()
    {
        var a = new ProjectileState(1L, 1L, "weapon.shortbow", 0, 0, 0, 1, 0, 0);
        var b = new ProjectileState(1L, 2L, "weapon.shortbow", 0, 0, 0, 1, 0, 0);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void RecordEquality_DifferentPosition_NotEqual()
    {
        var a = new ProjectileState(1L, 1L, "weapon.shortbow", 1f, 0f, 0f, 0f, 0f, 0f);
        var b = new ProjectileState(1L, 1L, "weapon.shortbow", 0f, 0f, 0f, 0f, 0f, 0f);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void RecordEquality_DifferentVelocity_NotEqual()
    {
        var a = new ProjectileState(1L, 1L, "weapon.shortbow", 0f, 0f, 0f, 5f, 0f, 0f);
        var b = new ProjectileState(1L, 1L, "weapon.shortbow", 0f, 0f, 0f, 0f, 0f, 0f);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void NegativeVelocity_StoredCorrectly()
    {
        var ps = new ProjectileState(1L, 1L, "weapon.shortbow",
            0f, 0f, 0f, -5f, -9.8f, -3f);

        Assert.Equal(-5f,   ps.VelX);
        Assert.Equal(-9.8f, ps.VelY);
        Assert.Equal(-3f,   ps.VelZ);
    }

    [Fact]
    public void WeaponId_StoredCorrectly()
    {
        var ps = new ProjectileState(5L, 3L, "weapon.hunting_knife",
            1f, 2f, 3f, 0f, 0f, 0f);
        Assert.Equal("weapon.hunting_knife", ps.WeaponId);
    }
}
