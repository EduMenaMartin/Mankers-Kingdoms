using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Client;

/// <summary>
/// Handles melee input for the local player.
/// Instantiated as a child of the local player node by PlayerController._Ready().
///
/// LMB click → find nearest attackable entity within weapon range → RequestMeleeAttack RPC.
/// RMB hold  → RequestSetBlocking(true); release → RequestSetBlocking(false).
///
/// Client-side swing cooldown prevents spamming the server RPC.
/// Server re-validates independently — this is UX feedback only.
///
/// Query mask:
///   Layer 6 (bitmask 32) = players     (set in PlayerController._Ready)
///   Layer 7 (bitmask 64) = monsters    (set in MonsterNode._Ready, Phase 4)
/// </summary>
public partial class MeleeController : Node
{
    private const string COMBAT_SYSTEM_PATH = "/root/GameWorld/CombatSystem";

    // Players layer (32) + Monsters layer (64). Phase 4 adds monsters to this mask.
    private const uint COMBAT_MASK = 32u | 64u;

    private bool   _isBlocking;
    private double _swingTimer; // counts down to 0; while > 0, swing is on cooldown

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void _Process(double delta)
    {
        if (_swingTimer > 0) _swingTimer -= delta;
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            switch (mb.ButtonIndex)
            {
                case MouseButton.Left when mb.Pressed:
                    // Only attack in combat mode; build mode owns LMB for placement.
                    // Only consume when a melee weapon is equipped and melee mode is active.
                    // §15: mutual exclusivity — cannot attack while holding block.
                    if (LocalState.InCombatMode && !_isBlocking && GetEquippedMeleeWeapon() != null)
                    {
                        TryMeleeAttack();
                        GetViewport().SetInputAsHandled();
                    }
                    break;

                case MouseButton.Right:
                    // Blocking is only meaningful in combat mode.
                    if (LocalState.InCombatMode)
                    {
                        SetBlocking(mb.Pressed);
                        GetViewport().SetInputAsHandled();
                    }
                    break;
            }
        }
    }

    // ── Attack ────────────────────────────────────────────────────────────────

    private void TryMeleeAttack()
    {
        if (_swingTimer > 0) return; // client-side cooldown guard

        var weaponId = GetEquippedMeleeWeapon();
        if (weaponId == null) return; // no melee weapon in inventory

        var weapon   = WeaponRegistry.Find(weaponId)!;
        var targetId = FindNearestTarget(weapon.Range);
        if (targetId == null) return; // nothing in range

        var cs = GetNodeOrNull(COMBAT_SYSTEM_PATH);
        if (cs == null) return;

        if (Multiplayer.IsServer())
            cs.Call("RequestMeleeAttack", targetId.Value, weaponId);
        else
            cs.RpcId(1, "RequestMeleeAttack", targetId.Value, weaponId);

        _swingTimer = weapon.SwingCooldown;
    }

    // ── Block ─────────────────────────────────────────────────────────────────

    private void SetBlocking(bool blocking)
    {
        // Client guard: must hold a shield to block.
        if (blocking && !LocalState.Inventory.Has("item.armor.shield"))
        {
            if (_isBlocking) SendBlockRpc(false); // release if we lost the shield
            _isBlocking = false;
            return;
        }

        if (_isBlocking == blocking) return;
        _isBlocking = blocking;
        LocalState.SetBlocking(blocking); // keeps BowController's mutual-exclusivity check in sync (§15)
        SendBlockRpc(blocking);
    }

    private void SendBlockRpc(bool blocking)
    {
        var cs = GetNodeOrNull(COMBAT_SYSTEM_PATH);
        if (cs == null) return;

        if (Multiplayer.IsServer())
            cs.Call("RequestSetBlocking", blocking);
        else
            cs.RpcId(1, "RequestSetBlocking", blocking);
    }

    // ── Target detection ──────────────────────────────────────────────────────

    /// <summary>Returns the first melee weapon ID found in the local player's inventory.</summary>
    private static string? GetEquippedMeleeWeapon()
    {
        foreach (var w in WeaponRegistry.All)
        {
            if (!w.IsRanged && LocalState.Inventory.Has(w.Id))
                return w.Id;
        }
        return null;
    }

    /// <summary>
    /// Sphere overlap at the player's position to find the nearest attackable entity.
    /// Returns the entity ID (peer ID for players, monster ID for monsters), or null.
    /// </summary>
    private long? FindNearestTarget(float range)
    {
        var player = GetParent<CharacterBody3D>();
        if (player == null) return null;

        var sphere = new SphereShape3D { Radius = range };
        var query  = new PhysicsShapeQueryParameters3D
        {
            Shape         = sphere,
            Transform     = new Transform3D(Basis.Identity, player.GlobalPosition),
            Exclude       = new Godot.Collections.Array<Rid> { player.GetRid() },
            CollisionMask = COMBAT_MASK
        };

        var hits     = player.GetWorld3D().DirectSpaceState.IntersectShape(query);
        long? nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            var collider = hit["collider"].As<Node>();
            var id       = ParseEntityId(collider);
            if (id == null) continue;

            float dist = player.GlobalPosition.DistanceTo(((Node3D)collider).GlobalPosition);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = id;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Derives entity ID from a physics collider node name.
    /// "Player_{peerId}" → peerId  |  "Monster_{id}" → id (Phase 4).
    /// </summary>
    private static long? ParseEntityId(Node? node)
    {
        if (node == null) return null;
        var name = node.Name.ToString();

        if (name.StartsWith("Player_") &&
            long.TryParse(name["Player_".Length..], out var peerId))
            return peerId;

        if (name.StartsWith("Monster_") &&
            long.TryParse(name["Monster_".Length..], out var monsterId))
            return monsterId;

        return null;
    }
}
