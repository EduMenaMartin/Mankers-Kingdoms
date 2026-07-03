using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

public class MessageTests
{
    [Fact]
    public void MsgPlayerInput_stores_direction_and_tick()
    {
        var msg = new MsgPlayerInput(0.5f, -0.5f, 42u);

        Assert.Equal(0.5f, msg.DirectionX);
        Assert.Equal(-0.5f, msg.DirectionZ);
        Assert.Equal(42u, msg.Tick);
    }

    [Fact]
    public void MsgPlayerInput_zero_direction_is_valid()
    {
        var msg = new MsgPlayerInput(0f, 0f, 0u);

        Assert.Equal(0f, msg.DirectionX);
        Assert.Equal(0f, msg.DirectionZ);
    }

    [Fact]
    public void MsgPlayerInput_direction_values_are_clamped_by_convention()
    {
        // Convention: GetVector() already returns values in [-1, 1].
        // This test documents the expected range, not an enforced clamp.
        var msg = new MsgPlayerInput(-1f, 1f, 99u);

        Assert.InRange(msg.DirectionX, -1f, 1f);
        Assert.InRange(msg.DirectionZ, -1f, 1f);
    }

    [Fact]
    public void MsgPlayerState_stores_all_fields()
    {
        var msg = new MsgPlayerState(2L, 1.0f, 0.5f, -3.0f, 100u);

        Assert.Equal(2L, msg.PeerId);
        Assert.Equal(1.0f, msg.X);
        Assert.Equal(0.5f, msg.Y);
        Assert.Equal(-3.0f, msg.Z);
        Assert.Equal(100u, msg.Tick);
    }

    [Fact]
    public void MsgPlayerState_peer_id_is_positive()
    {
        // ENet peer IDs start at 1; 0 and negative values are invalid.
        var msg = new MsgPlayerState(1L, 0f, 0f, 0f, 0u);

        Assert.True(msg.PeerId > 0);
    }

    [Fact]
    public void GameSession_defaults_to_none_intent_and_loopback()
    {
        GameSession.Reset();

        Assert.Equal(GameSession.SessionIntent.None, GameSession.Intent);
        Assert.Equal("127.0.0.1", GameSession.JoinAddress);
    }

    [Fact]
    public void GameSession_round_trips_host_intent()
    {
        GameSession.Reset();
        GameSession.Intent = GameSession.SessionIntent.Host;

        Assert.Equal(GameSession.SessionIntent.Host, GameSession.Intent);
    }

    [Fact]
    public void GameSession_round_trips_join_address()
    {
        GameSession.Reset();
        GameSession.JoinAddress = "192.168.1.42";

        Assert.Equal("192.168.1.42", GameSession.JoinAddress);
    }
}
