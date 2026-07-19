// Occlusion fade shader — applied as a material override to MeshInstance3D nodes
// that sit between the camera and the player (detected by OcclusionFader.cs).
//
// Uses a 2-pixel checkerboard discard so half the pixels are invisible.
// discard-based transparency avoids depth-sorting issues and works correctly
// when multiple overlapping objects are all occluding simultaneously.
// cull_disabled so the object silhouette shows regardless of which face is front.
//
// See ADR-0025.

shader_type spatial;
render_mode cull_disabled;

void fragment() {
    // Checkerboard: discard every other pixel in a 2×2 screen-space grid.
    // The result looks like a ghost / dither outline — unambiguously "see-through"
    // without requiring alpha blending or a depth sort pass.
    ivec2 px = ivec2(FRAGCOORD.xy);
    if ((px.x + px.y) % 2 == 0) {
        discard;
    }

    ALBEDO = vec3(0.4, 0.4, 0.4);
}
