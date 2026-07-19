shader_type spatial;
render_mode blend_mix, depth_draw_always, cull_back, diffuse_lambert, specular_schlick_ggx;

// ── Uniforms ──────────────────────────────────────────────────────────────────
// Assign these in the ShaderMaterial Inspector panel on the WaterSystem MeshInstance3D.

/// Normal map texture. hint_normal sets sRGB=false automatically.
/// Recommended: create a NoiseTexture2D (FastNoiseLite, FBm, seamless=true, as_normal_map=true).
uniform sampler2D normal_texture : hint_normal, repeat_enable;

/// How fast the normal map scrolls downstream (UV units per second).
/// 0.5 = gentle flow, 1.5 = fast rapids.
uniform float flow_speed : hint_range(0.0, 2.0) = 0.5;

/// Normal map depth multiplier. 0 = flat mirror, 1 = moderate ripple, 2 = rough chop.
uniform float normal_strength : hint_range(0.0, 3.0) = 0.8;

/// How many times the normal texture tiles per UV unit. Higher = finer ripple detail.
uniform float tiling : hint_range(0.1, 10.0) = 3.0;

/// Water body colour and alpha. Default: semi-opaque river blue.
uniform vec4 water_color : source_color = vec4(0.12, 0.45, 0.75, 0.82);

// ── Vertex ────────────────────────────────────────────────────────────────────
// Pass-through. UV, NORMAL, and TANGENT come from the ArrayMesh built by WaterSystem.cs.
// UV.x = [0, 1] across river width (left bank = 0, right bank = 1).
// UV.y = [0, 1] along flow direction (source = 0, mouth = 1) — scrolling this creates flow.
// TANGENT is the per-vertex flow direction vector; required for NORMAL_MAP to work correctly.
void vertex() {
}

// ── Fragment ──────────────────────────────────────────────────────────────────
void fragment() {
	// Tile the UV and scroll along the flow axis (UV.y = downstream direction).
	vec2 uv = UV * tiling;
	uv.y += TIME * flow_speed;

	// NORMAL_MAP expects tangent-space normals in [0,1] range (Godot converts internally).
	// Requires per-vertex TANGENT data in the mesh — WaterSystem.cs provides this.
	NORMAL_MAP = texture(normal_texture, uv).xyz;
	NORMAL_MAP_DEPTH = normal_strength;

	ALBEDO = water_color.rgb;
	ALPHA = water_color.a;
	ROUGHNESS = 0.05;
	METALLIC = 0.0;
	SPECULAR = 0.8;
}
