using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

public class WeaponRegistryTests
{
    // ── Catalog completeness ──────────────────────────────────────────────────

    [Fact]
    public void All_Contains37Weapons()
    {
        // SRD 5.1 catalog: 10 simple melee + 4 simple ranged + 18 martial melee + 5 martial ranged.
        Assert.Equal(37, WeaponRegistry.All.Count);
    }

    [Fact]
    public void AllWeapons_HaveStableItemDotWeaponId()
    {
        foreach (var w in WeaponRegistry.All)
            Assert.StartsWith("item.weapon.", w.Id);
    }

    [Fact]
    public void AllWeapons_HavePositiveSwingCooldown()
    {
        foreach (var w in WeaponRegistry.All)
            Assert.True(w.SwingCooldown > 0f, $"{w.Id} has zero or negative swing cooldown");
    }

    [Fact]
    public void AllWeapons_HaveNonEmptyDisplayNameKey()
    {
        foreach (var w in WeaponRegistry.All)
            Assert.False(string.IsNullOrEmpty(w.DisplayNameKey), $"{w.Id} missing DisplayNameKey");
    }

    // ── Fighter starting weapon (longsword) ───────────────────────────────────

    [Fact]
    public void Longsword_FoundById()
    {
        var w = WeaponRegistry.Find("item.weapon.longsword");
        Assert.NotNull(w);
        Assert.Equal("item.weapon.longsword", w.Id);
    }

    [Fact]
    public void Longsword_IsMelee()
    {
        var w = WeaponRegistry.Find("item.weapon.longsword")!;
        Assert.False(w.IsRanged);
    }

    [Fact]
    public void Longsword_DamageDiceIsCorrect()
    {
        // SRD 5.1: longsword is 1d8 slashing.
        var w = WeaponRegistry.Find("item.weapon.longsword")!;
        Assert.Equal("1d8", w.DamageDice);
        Assert.Equal("slashing", w.DamageType);
    }

    [Fact]
    public void Longsword_HasNoAmmo()
    {
        var w = WeaponRegistry.Find("item.weapon.longsword")!;
        Assert.Null(w.AmmoItemId);
    }

    // ── Ranger starting weapon (shortbow) ─────────────────────────────────────

    [Fact]
    public void Shortbow_IsRanged()
    {
        var w = WeaponRegistry.Find("item.weapon.shortbow")!;
        Assert.True(w.IsRanged);
    }

    [Fact]
    public void Shortbow_HasArrowAmmo()
    {
        var w = WeaponRegistry.Find("item.weapon.shortbow")!;
        Assert.Equal("item.arrow", w.AmmoItemId);
    }

    [Fact]
    public void Shortbow_HasPositiveProjectileSpeed()
    {
        var w = WeaponRegistry.Find("item.weapon.shortbow")!;
        Assert.True(w.ProjectileSpeed > 0f);
    }

    [Fact]
    public void Shortbow_DamageDiceIsCorrect()
    {
        // SRD 5.1: shortbow is 1d6 piercing.
        var w = WeaponRegistry.Find("item.weapon.shortbow")!;
        Assert.Equal("1d6", w.DamageDice);
        Assert.Equal("piercing", w.DamageType);
    }

    // ── Dagger (simple melee + throwable) ─────────────────────────────────────

    [Fact]
    public void Dagger_IsMeleeWithNoAmmo()
    {
        var w = WeaponRegistry.Find("item.weapon.dagger")!;
        Assert.False(w.IsRanged);
        Assert.Null(w.AmmoItemId);
    }

    // ── Net (special: no damage dice) ─────────────────────────────────────────

    [Fact]
    public void Net_HasEmptyDamageDice()
    {
        // Net has no damage — restraint is roadmap. DamageDice is empty string.
        var w = WeaponRegistry.Find("item.weapon.net")!;
        Assert.Equal("", w.DamageDice);
        Assert.Equal("special", w.DamageType);
    }

    // ── Heavy crossbow ────────────────────────────────────────────────────────

    [Fact]
    public void CrossbowHeavy_Usesbolt()
    {
        var w = WeaponRegistry.Find("item.weapon.crossbow_heavy")!;
        Assert.Equal("item.bolt", w.AmmoItemId);
    }

    // ── Lookup ────────────────────────────────────────────────────────────────

    [Fact]
    public void Unknown_ReturnsNull()
    {
        Assert.Null(WeaponRegistry.Find("item.weapon.does_not_exist"));
    }

    [Fact]
    public void OldId_ReturnsNull()
    {
        // Ensure old pre-4.7 IDs are gone.
        Assert.Null(WeaponRegistry.Find("weapon.sword"));
        Assert.Null(WeaponRegistry.Find("weapon.hunting_knife"));
        Assert.Null(WeaponRegistry.Find("weapon.shortbow"));
    }
}
