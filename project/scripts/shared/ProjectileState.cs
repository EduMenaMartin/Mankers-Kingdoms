namespace MankersKingdoms.Shared;

/// <summary>
/// Immutable snapshot of a projectile's state at a given point in time.
///
/// Position and velocity use flat floats (not Godot.Vector3) so this record
/// compiles in the xUnit test project, which has no Godot runtime (ADR-0010).
///
/// Server ticks live projectiles in ProjectileSystem. Clients receive
/// spawn/remove events via LocalState and manage arrow ghosts in BowController.
/// </summary>
public sealed record ProjectileState(
    long   Id,
    long   OriginPeerId,
    string WeaponId,
    float  PosX,
    float  PosY,
    float  PosZ,
    float  VelX,
    float  VelY,
    float  VelZ
);
