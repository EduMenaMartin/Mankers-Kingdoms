# ADR-0025: Camera angle, orbit, and occlusion shader (refining ADR-0003)

**Status:** Accepted
**Date:** 2026-07-09
**Deciders:** Edu + Claude

## Context

Playtesting the literal 90°-straight-down camera from M1 surfaced two real
problems: (1) buildings and trees fully occlude things directly behind
them from the player's view, (2) a pure overhead angle wastes a 16:9
screen's width, since nothing is seen from the side to use horizontal
space naturally. This prompted considering full third-person and
first-person camera modes as a fix.

Full third/first-person modes were evaluated and rejected — see
IDEAS_BACKLOG.md entry 2026-07-09 — due to the cost of parallel aiming
systems alongside docs/gdd/combat.md's existing mouse-aim-on-ground-plane
model, and conflict with ADR-0003's coop-presence reasoning.

The actual problems have cheaper, genre-standard fixes that don't touch
the aiming model at all.

## Decision

Three changes to the existing top-down camera, all client-side only
(ARCHITECTURE.md §3.2), none altering the mouse-aim-on-ground-plane
targeting model already locked in docs/gdd/combat.md:

1. **Angled, not literal 90°.** Camera tilts to a steep-but-not-vertical
   downward angle (exact value tuned visually in-engine, not fixed here)
   — the same convention used by Diablo, Path of Exile, and most
   tilted-top-down ARPGs, none of which break mouse-to-ground-plane
   aiming by using this angle.
2. **Free orbit.** Player can rotate the camera's yaw around themselves
   to look around occluding geometry, independent of movement/aim
   direction.
3. **Occlusion fade shader.** A camera-to-player raycast each frame
   detects geometry between the camera and the player; hit objects get a
   transparency/dither material applied while occluding, reverted when
   clear.

## Consequences

**Positive:** solves the actual reported problems (hidden objects, screen
space) at a fraction of the cost of full camera-mode switching; doesn't
touch combat.md's aiming math at all; genre-proven approach, low risk.

**Negative:** camera positioning logic becomes runtime-computed (orbit +
tilt combined) rather than a static Transform set once in the editor —
slightly more script complexity than the M1 fixed camera, but contained
entirely to client-side presentation code.

## Alternatives considered

- **Full third-person + first-person toggle modes** — rejected, see
  IDEAS_BACKLOG.md entry. Logged as [slice-affecting] for possible later
  reconsideration, not a current plan.
- **Keep literal 90°, do nothing** — rejected, real playtesting complaint.

## References

- ADR-0003 (top-down perspective, parent principle this refines)
- docs/gdd/combat.md §2.2 (mouse-aim model, confirmed unaffected)
- IDEAS_BACKLOG.md, 2026-07-09 entry
