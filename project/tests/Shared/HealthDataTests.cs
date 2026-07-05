using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

public class HealthDataTests
{
    [Fact]
    public void IsAlive_WhenHpPositive()
    {
        var h = new HealthData(50f, 100f);
        Assert.False(h.IsDead);
    }

    [Fact]
    public void IsDead_WhenHpZero()
    {
        var h = new HealthData(0f, 100f);
        Assert.True(h.IsDead);
    }

    [Fact]
    public void IsDead_WhenHpNegative()
    {
        // Guard: damage overshoot should still read as dead.
        var h = new HealthData(-5f, 100f);
        Assert.True(h.IsDead);
    }

    [Fact]
    public void Fraction_IsCorrect()
    {
        var h = new HealthData(25f, 100f);
        Assert.Equal(0.25f, h.Fraction);
    }

    [Fact]
    public void Fraction_IsOneAtFullHp()
    {
        var h = new HealthData(100f, 100f);
        Assert.Equal(1f, h.Fraction);
    }

    [Fact]
    public void Fraction_IsZeroWhenMaxHpIsZero()
    {
        // Degenerate guard — should not divide by zero.
        var h = new HealthData(0f, 0f);
        Assert.Equal(0f, h.Fraction);
    }

    [Fact]
    public void MsgEntityHealth_ToHealthData_RoundTrips()
    {
        var msg = new MsgEntityHealth(EntityId: 1L, CurrentHp: 60f, MaxHp: 100f);
        var data = msg.ToHealthData();
        Assert.Equal(60f, data.CurrentHp);
        Assert.Equal(100f, data.MaxHp);
        Assert.False(data.IsDead);
    }

    [Fact]
    public void MsgEntityHealth_ToHealthData_DeadAtZero()
    {
        var msg  = new MsgEntityHealth(EntityId: 2L, CurrentHp: 0f, MaxHp: 100f);
        var data = msg.ToHealthData();
        Assert.True(data.IsDead);
    }
}
