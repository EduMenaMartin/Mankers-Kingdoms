namespace MankersKingdoms.Shared;

/// <summary>
/// The three equipment slots available to every player (inventory.md §10.1).
/// Cast to/from int (MainHand=0, OffHand=1, BodyArmor=2) for RPC transport via RequestEquipItem.
/// </summary>
public enum EquipSlot
{
    MainHand  = 0,
    OffHand   = 1,
    BodyArmor = 2
}
