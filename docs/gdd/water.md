# Water System — River Generation and Rendering

**Status:** Implemented in M10. Architecture locked.  
**Systems:** `RiverGenerator` (shared), `WaterSystem` (server/both peers), `TerrainSystem` (carving pass)

---

## 1. River path generation

### Seed discipline
- Salt: `WorldSeed ^ 0xB1A7E600u` — "BLAZE/RIVER" mnemonic
- Dedicated `System.Random` for path walk decisions
- Separate deterministic hash function for width noise (does not consume path RNG — changes one does not affect the other)

### Source selection
`N_SOURCE_TRIES = 8` random border cells are sampled; the one with the highest terrain height is chosen as the river source. This biases the river toward starting at elevated terrain without requiring the entire map to be scanned.

Border cells = cells on the outermost row/column (gridX = 0, gridX = MapWidth-1, gridZ = 0, gridZ = MapHeight-1).

### Downhill-biased walk (D8 + noise)
At each step, all 8 grid neighbors are evaluated. Unvisited neighbors score:

```
score(n) = descent + noise
descent  = heightmap[current] - heightmap[n]   // positive = downhill
noise    = rng.NextDouble() * WANDER_FACTOR     // WANDER_FACTOR = 0.3f
```

The neighbor with the highest score is selected. `WANDER_FACTOR = 0.3f` adds ~20% of typical slope as
wander, creating mild meanders without fighting the gradient. If all 8 neighbors are visited, the walk
terminates. The path also terminates naturally when a border cell is reached (river exits the map) or
after `MAX_PATH_STEPS = MapWidth * 3` steps.

A `bool[,] visited` guard prevents revisiting cells, avoiding loops.

### Height smoothing pass
After the walk, a single forward sweep ensures the path centre heights are monotonically non-increasing
(river never flows uphill):

```
pathHeights[i] = Math.Min(pathHeights[i], pathHeights[i - 1])
```

This smoothed array is used for carving and for computing `WaterY` per segment — not the raw heightmap.

### Width noise
A 1D smoothstep-interpolated hash gives width variation along arc-length:

- `BASE_HALF_WIDTH_TILES = 1.0f` — minimum half-width (4 m at TileSize=4)
- `WIDTH_VARIATION_TILES = 1.0f` — peak additional half-width (8–16 m total width)
- `WIDTH_NOISE_FREQ = 0.04f` — spatial frequency of width changes
- Salt for hash: `seed ^ 0xB1A7E601u` — sub-salt distinct from path RNG

---

## 2. Terrain carving

### Cosine taper profile
For every grid cell `(cx, cz)` within `halfWidthTiles` of a path centre `(gx, gz)`:

```
d        = sqrt((cx-gx)² + (cz-gz)²) / halfWidthTiles   // [0, 1]
profile  = (1 + cos(π × d)) / 2                          // 1.0 at centre, 0.0 at edge
carveFloor = pathHeight - CHANNEL_DEPTH × profile
heightmap[cx, cz] = min(heightmap[cx, cz], carveFloor)
```

Constants:
- `CHANNEL_DEPTH = 3.0f` — maximum depth carved at channel centre (world units)
- `WATER_SURFACE_OFFSET = 1.5f` — water surface sits this far above channel floor

Water surface Y at each segment:

```
WaterY = pathHeight - CHANNEL_DEPTH + WATER_SURFACE_OFFSET
       = pathHeight - 1.5f
```

With terrain heights in ±3 m (NoiseAmp=6) this produces channels visually below surrounding
terrain at all path positions.

### Channel mask and bank-wall exclusion
`RiverGenerator.Generate()` writes a `bool[,] ChannelMask` alongside carving: every `(cx, cz)` cell
within the cosine profile radius sets `ChannelMask[cx, cz] = true`.

`TerrainSystem.IsInRiverChannel(int gridX, int gridZ)` delegates to `River.IsInChannel(x, z)`.

Both `TreeGenerator.Generate()` and `BushGenerator.Generate()` accept an optional `bool[,]? riverMask`
parameter. When provided, they skip placement at masked cells. This prevents trees and bushes from
spawning inside the channel even when wall terrain heights happen to pass the `MinHeight` check.

### Sequencing in TerrainSystem._Ready()
```
1. TerrainGenerator.GenerateHeightmap()  →  float[,] rawHeightmap
2. RiverGenerator.Generate(rawHeightmap) →  RiverData  (carves rawHeightmap in-place)
3. TerrainSystem.Heightmap = rawHeightmap          ← now the carved version
4. TerrainSystem.River = riverData                 ← WaterSystem reads this
5. Build HeightMapShape3D from carved heightmap
```

All downstream nodes that read `TerrainSystem.Heightmap` (TreeSystem, BushSystem) initialise
after TerrainSystem in scene order and automatically receive the carved version. No extra
notification needed.

---

## 3. WaterSystem architecture

`WaterSystem` is a Godot Node that runs on **both peers** (same as TerrainSystem/TreeSystem — spawned
deterministically from the same RiverData, no sync RPC required). It must appear in GameWorld.tscn
**after TerrainSystem**.

### Ribbon mesh construction (ArrayMesh)
For each `RiverSegment` (index `i`):

```
normalX = -seg.TangentZ
normalZ =  seg.TangentX

leftVert  = (seg.WorldX - normalX × seg.HalfWidthM,  seg.WaterY,  seg.WorldZ - normalZ × seg.HalfWidthM)
rightVert = (seg.WorldX + normalX × seg.HalfWidthM,  seg.WaterY,  seg.WorldZ + normalZ × seg.HalfWidthM)
```

UV layout (critical for shader):
- `UV.x = 0.0` at left bank, `UV.x = 1.0` at right bank
- `UV.y = arcLength / totalLength` at each segment (0.0 at source, 1.0 at mouth)

**UV.y increases in the downstream direction.** The shader scrolls `UV.y` over time to produce apparent
downstream flow. Because the mesh is constructed so that UV.y aligns with the flow tangent, this
is equivalent to scrolling along the path-tangent direction at every point — no per-fragment tangent
lookup required.

Per-vertex data included in the ArrayMesh:
- `Mesh.ArrayType.Vertex` — world-space positions (Vector3[])
- `Mesh.ArrayType.Normal` — `Vector3.Up` for all vertices (river surface is flat)
- `Mesh.ArrayType.Tangent` — `(seg.TangentX, 0, seg.TangentZ, 1.0)` per vertex;
  required for `NORMAL_MAP` in the spatial shader (Godot 4 expects tangent data for normal-map sampling)
- `Mesh.ArrayType.TexUV` — (UV.x, UV.y) per vertex
- `Mesh.ArrayType.Index` — two CCW triangles per quad: `(i*2, i*2+2, i*2+1)` and `(i*2+1, i*2+2, i*2+3)`

### "Player is in water" collision (Area3D)
One `Area3D` node, with per-segment `Node3D` pivot children. Each pivot contains a `BoxShape3D`:
- Position: `(seg.WorldX, seg.WaterY - 1.5f, seg.WorldZ)` — centred vertically through water surface
- Rotation: Y-axis rotation = `atan2(seg.TangentX, seg.TangentZ)` (orients box along flow)
- Size: `(seg.HalfWidthM * 2, 5.0f, TileSize)` — full width, 5 m tall, one tile deep along flow

`body_entered` signal fired server-side only. Stub logs the entry; future expansion: apply swim
state, slow movement, reset stamina drain (post-M10 BACKLOG).

### Material (runtime-assigned — no editor task)
`WaterSystem._Ready` loads `res://shaders/water_river.gdshader` via `GD.Load<Shader>()`.
If the shader loads successfully, a `ShaderMaterial` is created in code and a `NoiseTexture2D`
(FastNoiseLite, SimplexSmooth, FBm, 256px, seamless, as_normal_map) is assigned to the
`normal_texture` uniform via `SetShaderParameter`. There is no Inspector slot to assign.

Fallback if the shader file is missing: `StandardMaterial3D` flat blue stub (unchanged from before).

**No editor task is needed for this uniform assignment.** See `WaterSystem.cs BuildRibbonMesh`.

---

## 4. Shader spec — water_river.gdshader

This section is the **authoritative spec for Edu to author the shader in the Godot editor.**
Per `docs/scene_workflow.md`: Claude Code does not write `.gdshader` files. Edu creates the
file in the Godot editor; the spec below describes exactly what it must contain.

### File to create
`res://shaders/water_river.gdshader`

(The project's shader convention is `res://shaders/` — `occlusion_fade.gdshader` already lives
there. There is no `res://assets/shaders/` directory.)

### Full shader source
```glsl
shader_type spatial;
render_mode blend_mix, depth_draw_always, cull_back, diffuse_lambert, specular_schlick_ggx;

// ── Uniforms ─────────────────────────────────────────────────────────────────
// Assign these in the ShaderMaterial Inspector panel on the WaterSystem's MeshInstance3D.

/// Normal map texture — use Godot's built-in "WaterNormal" from the asset library,
/// or any seamless water normal map. hint_normal sets sRGB=false automatically.
uniform sampler2D normal_texture : hint_normal, repeat_enable;

/// How fast the normal map scrolls downstream (UV units per second).
/// 0.5 = gentle flow; 1.5 = fast rapids.
uniform float flow_speed : hint_range(0.0, 2.0) = 0.5;

/// Normal map depth multiplier. 0 = flat mirror, 1 = moderate ripple, 2 = rough chop.
uniform float normal_strength : hint_range(0.0, 3.0) = 0.8;

/// Tiling: how many times the normal texture repeats per UV unit (across/along river).
/// Higher = finer ripple detail. 3.0 is a good starting point.
uniform float tiling : hint_range(0.1, 10.0) = 3.0;

/// Water body colour and alpha. Default: semi-opaque river blue.
uniform vec4 water_color : source_color = vec4(0.12, 0.45, 0.75, 0.82);

// ── Vertex ───────────────────────────────────────────────────────────────────
// Pass-through. UV, NORMAL, and TANGENT come from the ArrayMesh built by WaterSystem.
// UV.x = [0,1] across river width (left=0, right=1).
// UV.y = [0,1] along flow direction (source=0, mouth=1) — scrolling this creates flow.
// TANGENT = per-vertex flow direction vector; required by Godot for NORMAL_MAP to work.
void vertex() {
    // intentionally empty — no vertex displacement
}

// ── Fragment ─────────────────────────────────────────────────────────────────
void fragment() {
    // Tile the UV and scroll along the flow axis (UV.y = downstream direction).
    vec2 uv = UV * tiling;
    uv.y   += TIME * flow_speed;

    // Sample normal map. Godot's NORMAL_MAP expects tangent-space normals in [0,1] range
    // (the engine converts to [-1,1] and applies TANGENT/BINORMAL from the mesh).
    NORMAL_MAP       = texture(normal_texture, uv).xyz;
    NORMAL_MAP_DEPTH = normal_strength;

    ALBEDO    = water_color.rgb;
    ALPHA     = water_color.a;
    ROUGHNESS = 0.05;   // near-mirror surface
    METALLIC  = 0.0;
    SPECULAR  = 0.8;    // strong specular highlight from sun/directional light
}
```

### Why TANGENT data is required
`NORMAL_MAP` in a Godot spatial shader transforms the sampled normal from tangent space to
world/view space using the mesh's per-vertex `TANGENT` and `BINORMAL` attributes. If the mesh
has no tangent data, `NORMAL_MAP` silently produces incorrect or flat output. `WaterSystem.cs`
includes tangent data in `Mesh.ArrayType.Tangent` (the per-vertex flow direction), so the
normal map will transform correctly.

### Expected visual result
- River surface is a semi-transparent blue ribbon following the carved channel.
- Normal map ripples appear to scroll in the downstream direction (UV.y increases toward mouth).
- At bends the ripple direction follows the bend smoothly, because UV.y is aligned with
  arc-length along the path — no abrupt tangent discontinuities.
- `specular_schlick_ggx` produces a bright specular glint from the DirectionalLight3D sun.
- Roughness 0.05 + Specular 0.8 = glassy water surface; tune `normal_strength` to control
  how much the ripples break up the reflection.
- Alpha 0.82 allows faint glimpse of the carved channel bottom through shallow water.

### How to wire up in the editor (after authoring the shader)
1. Select the `MeshInstance3D` named `RiverMesh` inside the `WaterSystem` node.
2. In the Inspector, under `Material Override`, replace the stub `StandardMaterial3D`
   with a new `ShaderMaterial`.
3. Set `Shader` on the ShaderMaterial to `res://shaders/water_river.gdshader`.
4. Assign a water normal-map texture to the `normal_texture` uniform.
5. Tune `flow_speed`, `normal_strength`, `tiling`, and `water_color` to taste.

---

## 5. Terrain resolution constraints — LOCKED (2026-07-21)

### Current grid parameters
- `TileSize = 4 m` per grid cell (`WorldConstants.TILE_SIZE`)
- `MapWidth = MapHeight = 64` cells → 252 × 252 m world footprint
- River ribbon: one grid segment per path step → vertex-pair spacing ~4–5.66 m (cardinal/diagonal)
- River channel width: 8–16 m full width = 2–4 cells across

### Ribbon resolution fix (M10)
`WaterSystem.BuildRibbonMesh` resamples the segment list at `RIBBON_STEP_M = 1 m` before building
geometry, using `UpsampleSegments`. The ribbon now has ~4–5× more vertex pairs, independent of
how coarse the terrain grid is. The channel width in the ribbon mesh stays physically correct.

### Bank terrain fix (M10)
`TerrainRenderer.BuildMesh` subdivides cells in the bank region (channel mask ± 1 cell margin)
into `BANK_SUB = 4` sub-quads per edge (16 sub-quads = 32 triangles per 4 m cell, giving 1 m
visual resolution). Sub-vertex heights are bilinearly interpolated from the heightmap corners.
Open terrain outside the bank region is unchanged (2 triangles per 4 m cell).

### LOCKED constraint — map-size increase must scale cell count, NOT TileSize

> **Do not increase `TileSize` to cover a larger world with the same 64×64 grid.**

The PRD targets a ~1000 m map for M10. Achieving 1000 m by raising `TileSize` from 4 m to ~16 m
while keeping `MapWidth = MapHeight = 64` would silently undo both fixes above:
- Ribbon vertex-pair spacing: 4–5.66 m → **16–22 m** (4× coarser)
- Bank sub-quads: 1 m effective resolution → **4 m** (back to baseline)

When the map-size increase is implemented, scale **cell count** instead and keep `TileSize ≈ 4 m`.
Two options, with explicit tradeoffs to evaluate at implementation time:

| Option | `MapWidth` / `MapHeight` | `TileSize` | World size | Terrain vertices | Resolution degradation |
|--------|--------------------------|------------|------------|-----------------|------------------------|
| A      | 250 × 250                | 4 m        | ~1000 m    | ~62,500          | None — full fix preserved |
| B      | 128 × 128                | 8 m        | ~1024 m    | ~16,384          | 2× (bank sub-quads: 2 m instead of 1 m) |

Option A is visually ideal but increases terrain mesh vertex count by ~15×. Option B is a
practical middle ground (4× increase) with a 2× resolution trade-off at the bank only.
**Do not pick silently — flag the vertex-count/performance tradeoff when starting that task.**

### Normal map asset recommendation
Godot's built-in `res://addons/...` does not include a water normal map. Options:
- **Free**: Poly Haven's "water_normal_map" (CC0, tileable, 1024px) — matches this game's scale.
- **Built-in**: Create a Godot `NoiseTexture2D` (FastNoiseLite, FBm, 256px, seamless=true,
  as_normal_map=true) and assign it directly. Zero external asset dependency.
  The NoiseTexture2D approach is recommended for the vertical slice.
