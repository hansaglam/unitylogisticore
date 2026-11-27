# DEPOT-ANIM-01 — Evidence-gated sprite motion (isolated foundation)

## Status
C# policy, rig and EditMode contracts committed on a separate GitHub branch based on the last pushed ART-02 code. **No ART-02C source PNGs, ART-02C layout scene or final v2 gate/fence have been pushed yet.** The animation code is not yet wired into the live depot, and no Unity/device tests have been run by this remote connection.

## Runtime contract
- `DepotMotionEvidence` holds flags provided by a caller that has already checked authenticated, server-confirmed operation state.
- `DepotMotionPolicy.Resolve` fails closed for unverified reads and for all facility stages other than `StorageOperational`. It never fabricates gate/dock opening because a warehouse merely exists.
- `DepotMotionRig` operates on **separate transforms** for gate leaf and a selected dock door leaf. It keeps rest positions, animates local positional displacement via MoveTowards, restores rest on losing verified evidence or disabling, and never initiates an operation/vehicle movement/backend write. Rest positions must be configured on authored sprite layers.
- Visibilities of company-owned facilities are controlled by the existing verified company visual layer, **not** by the rig. No new owned building appears simply by adding a motion component.

## ART-02D production asset contract for Codex
Codex is working in a separate local worktree `D:\Projects\unitylogisticore-depot-art-02`, current ART-02C commit `1ea0ff8820dfbc393e4bc4254a6a3c4c1578000c`. Before any rebasing, merging or pulling, preserve local unpushed ART-02, ART-02B and ART-02C commits and three older main-worktree stashes.

Read local `Docs/DepotArt02/Art02C/ProductionSpecification.md` as the authoritative camera/measurements contract. Do not assume exact 30-degree isometry. Author separate, editable source/PNG layers:
- fence straight and orthogonal orientation variants: genuinely open-grid fence openings, clean frame, ground post anchors, both ends tile without duplicate posts;
- coherent corner section: matching posts, height, physical scale and wall-plane direction;
- gate frame/pillars **stationary**, sliding gate leaf **separate**, with a closed and open travel range; transparent bar openings and independent shadow where needed;
- warehouse loading doors: each shutter is an independent moving sprite **only when reliable layered source is available**, avoiding destructive cutouts to the currently approved warehouse image;
- security booth, signs and bollards rescaled against a shared ground-contact convention.

Keep existing source candidates as versioned originals. Reject unsafe editing/redraw that changes perspective, details or visible contact shadows; report what cannot be rebuilt without editable source. No faux vehicle arrival, loading event, stock changes or gameplay authorization.

## Next integration gate
1. Codex produces truly separate alpha-clean v2 sprites and records layout/pivot/PPU/scale in a manifest; Unity generates .meta and verifies in review-only scene.
2. Merge the GitHub ANIM-01 branch into the **Codex feature worktree only after saving local commits**, or cherry-pick its commit after inspect/review; do not overwrite local art.
3. Link the rig to movable sprite layers in **review-only** hierarchy. Verify gate open/close and dock open/close using isolated, explicitly flagged **test evidence**, never live user account state.
4. After backend supplies authenticated event identifiers and a reliable lifecycle, connect the evidence adapter to production verified state. Gate remains static if event source is unavailable.
5. Add Unity EditMode, PlayMode and landscape mobile visual/performance tests. Do not update release Build Profiles or shipping scenes before validation.

## Non-goals
No artificial traffic, random workers, pretend load animation, server-side mutators or fake contract progress. No Firebase, save, economy, main branch or published game changes.
