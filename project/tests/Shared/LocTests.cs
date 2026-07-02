using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

public class LocTests : IDisposable
{
    public void Dispose() => Loc.Reset();

    [Fact]
    public void T_returns_key_in_brackets_when_not_loaded()
    {
        Assert.Equal("[splash.title]", Loc.T("splash.title"));
    }

    [Fact]
    public void T_returns_value_after_json_loaded()
    {
        Loc.LoadJson("{\"splash.title\": \"Mankers Kingdoms\"}");
        Assert.Equal("Mankers Kingdoms", Loc.T("splash.title"));
    }

    [Fact]
    public void T_returns_fallback_for_missing_key_after_load()
    {
        Loc.LoadJson("{\"splash.title\": \"Mankers Kingdoms\"}");
        Assert.Equal("[missing.key]", Loc.T("missing.key"));
    }
}
