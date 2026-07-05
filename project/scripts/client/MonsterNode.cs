using Godot;

namespace MankersKingdoms.Client;

/// <summary>
/// Visual and physics representative of a monster on all peers.
/// Instantiated by MonsterSystem.ClientSpawnMonster; named "Monster_{id}".
///
/// Server: position is set directly by MonsterSystem each tick (_Process is a no-op).
///   The CharacterBody3D collision shape (layer 7 = 64) is what ProjectileSystem and
///   MeleeController's sphere queries detect.
///
/// Clients: lerps toward the target position pushed by MonsterSystem RPCs.
///   Shows a colour-coded CapsuleMesh and flashes red briefly on hit.
///
/// Monster type is stored as node metadata "type_id" (set before AddChild by MonsterSystem)
/// and read in _Ready to configure the mesh colour.
/// </summary>
public partial class MonsterNode : CharacterBody3D
{
	// Layer 7 (bitmask 64) = monster combat layer.
	private const uint MONSTER_LAYER = 64u;

	private const float LERP_WEIGHT  = 0.15f;
	private const float FLASH_SEC    = 0.25f;

	private MeshInstance3D _mesh     = null!;
	private StandardMaterial3D _matNormal = null!;
	private StandardMaterial3D _matFlash  = null!;

	private Vector3 _remoteTarget;
	private bool    _isDead;
	private float   _flashTimer;

	// ── Lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		CollisionLayer = MONSTER_LAYER;
		CollisionMask  = 0u; // monsters don't collide with anything themselves

		// Collision shape.
		var shape = new CapsuleShape3D { Radius = 0.4f, Height = 1.8f };
		var col   = new CollisionShape3D { Shape = shape };
		AddChild(col);

		// Mesh — colour by type so monsters are visually distinct.
		var typeId = GetMeta("type_id", "monster.wolf").AsString();
		var color  = TypeColor(typeId);

		_matNormal       = new StandardMaterial3D { AlbedoColor = color };
		_matFlash        = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.15f, 0.15f) };

		var capsule = new CapsuleMesh { Radius = 0.4f, Height = 1.8f };
		_mesh = new MeshInstance3D
		{
			Mesh             = capsule,
			MaterialOverride = _matNormal,
		};
		AddChild(_mesh);

		_remoteTarget = GlobalPosition;
	}

	public override void _Process(double delta)
	{
		// Server moves us directly — no lerp needed.
		if (Multiplayer.IsServer()) return;
		if (_isDead) return;

		// Lerp toward authoritative position from MonsterSystem.
		GlobalPosition = GlobalPosition.Lerp(_remoteTarget, LERP_WEIGHT);

		// Hit flash timer.
		if (_flashTimer > 0f)
		{
			_flashTimer -= (float)delta;
			if (_flashTimer <= 0f)
				_mesh.MaterialOverride = _matNormal;
		}
	}

	// ── Called by MonsterSystem via untyped .Call() ────────────────────────

	/// <summary>Updates the lerp target (client-side only; server sets GlobalPosition directly).</summary>
	public void SetTarget(Vector3 target) => _remoteTarget = target;

	/// <summary>Briefly turns the mesh red to signal a hit landing.</summary>
	public void TriggerHitFlash()
	{
		_flashTimer            = FLASH_SEC;
		_mesh.MaterialOverride = _matFlash;
	}

	/// <summary>Hides the monster when it dies. MonsterSystem frees the node shortly after.</summary>
	public void HandleDeath()
	{
		_isDead          = true;
		_mesh.Visible    = false;
	}

	// ── Utility ───────────────────────────────────────────────────────────────

	private static Color TypeColor(string typeId) => typeId switch
	{
		"monster.wolf"          => new Color(0.55f, 0.45f, 0.35f),  // brown
		"monster.goblin"        => new Color(0.25f, 0.60f, 0.25f),  // green
		"monster.bandit"        => new Color(0.50f, 0.40f, 0.40f),  // dark red-brown
		"monster.bandit_archer" => new Color(0.40f, 0.30f, 0.55f),  // purple
		_                       => new Color(0.70f, 0.70f, 0.70f),  // grey fallback
	};
}
