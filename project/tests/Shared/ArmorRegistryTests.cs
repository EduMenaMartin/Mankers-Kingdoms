using MankersKingdoms.Shared;
using Xunit;

namespace MankersKingdoms.Tests.Shared;

public class ArmorRegistryTests
{
    // ── Catalog completeness ──────────────────────────────────────────────────

    [Fact]
    public void All_Contains13Entries()
    {
        // 3 light + 5 medium + 4 heavy + 1 shield = 13.
        Assert.Equal(13, ArmorRegistry.All.Count);
    }

    [Fact]
    public void AllArmor_HaveStableItemDotArmorId()
    {
        foreach (var a in ArmorRegistry.All)
            Assert.StartsWith("item.armor.", a.Id);
    }

    [Fact]
    public void AllArmor_HaveNonEmptyDisplayNameKey()
    {
        foreach (var a in ArmorRegistry.All)
            Assert.False(string.IsNullOrEmpty(a.DisplayNameKey), $"{a.Id} missing DisplayNameKey");
    }

    // ── Category counts ───────────────────────────────────────────────────────

    [Fact]
    public void LightArmor_Count()
    {
        int count = 0;
        foreach (var a in ArmorRegistry.All)
            if (a.ArmorCategory == ArmorCategory.Light) count++;
        Assert.Equal(3, count);
    }

    [Fact]
    public void MediumArmor_Count()
    {
        int count = 0;
        foreach (var a in ArmorRegistry.All)
            if (a.ArmorCategory == ArmorCategory.Medium) count++;
        Assert.Equal(5, count);
    }

    [Fact]
    public void HeavyArmor_Count()
    {
        int count = 0;
        foreach (var a in ArmorRegistry.All)
            if (a.ArmorCategory == ArmorCategory.Heavy) count++;
        Assert.Equal(4, count);
    }

    // ── Armor values match equipment.md §2 ───────────────────────────────────

    [Fact]
    public void PlateMail_HasHighestArmorValue()
    {
        var plate = ArmorRegistry.Find("item.armor.plate_mail")!;
        Assert.Equal(8, plate.ArmorValue);
        // Confirm it's the max in the catalog.
        int maxAv = 0;
        foreach (var a in ArmorRegistry.All)
            if (a.ArmorValue > maxAv) maxAv = a.ArmorValue;
        Assert.Equal(8, maxAv);
    }

    [Fact]
    public void PlateMail_RequiresStr15()
    {
        var plate = ArmorRegistry.Find("item.armor.plate_mail")!;
        Assert.Equal(15, plate.StrRequirement);
        Assert.True(plate.StealthDisadvantage);
    }

    [Fact]
    public void ChainMail_HasCorrectValues()
    {
        // SRD: Chain Mail base AC 16 → armor_value = 6 (16 − 10).
        var cm = ArmorRegistry.Find("item.armor.chain_mail")!;
        Assert.Equal(6, cm.ArmorValue);
        Assert.Equal(ArmorCategory.Heavy, cm.ArmorCategory);
        Assert.Equal(13, cm.StrRequirement);
    }

    [Fact]
    public void Leather_HasArmorValue1_NoStrReq()
    {
        var l = ArmorRegistry.Find("item.armor.leather")!;
        Assert.Equal(1, l.ArmorValue);
        Assert.Equal(ArmorCategory.Light, l.ArmorCategory);
        Assert.Equal(0, l.StrRequirement);
        Assert.False(l.StealthDisadvantage);
    }

    [Fact]
    public void HalfPlate_IsMediumWithStealthDisadvantage()
    {
        var hp = ArmorRegistry.Find("item.armor.half_plate")!;
        Assert.Equal(5, hp.ArmorValue);
        Assert.Equal(ArmorCategory.Medium, hp.ArmorCategory);
        Assert.True(hp.StealthDisadvantage);
    }

    // ── Shield ────────────────────────────────────────────────────────────────

    [Fact]
    public void Shield_IsShieldCategory()
    {
        var s = ArmorRegistry.Find("item.armor.shield")!;
        Assert.Equal(ArmorCategory.Shield, s.ArmorCategory);
    }

    [Fact]
    public void Shield_HasShieldBonus2_ArmorValue0()
    {
        // Shield adds +2 to Target Number via ShieldBonus, not ArmorValue.
        var s = ArmorRegistry.Find("item.armor.shield")!;
        Assert.Equal(2, s.ShieldBonus);
        Assert.Equal(0, s.ArmorValue);
    }

    [Fact]
    public void Shield_NoStrRequirementOrStealthPenalty()
    {
        var s = ArmorRegistry.Find("item.armor.shield")!;
        Assert.Equal(0, s.StrRequirement);
        Assert.False(s.StealthDisadvantage);
    }

    // ── Lookup ────────────────────────────────────────────────────────────────

    [Fact]
    public void Unknown_ReturnsNull()
    {
        Assert.Null(ArmorRegistry.Find("item.armor.does_not_exist"));
    }
}
