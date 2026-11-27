# DEPOT-ANIM-03 / ART-02D production integration gates

## Actually implemented in this GitHub branch
- `DepotVerifiedVisualAssembly` controls site, warehouse and operational-detail SpriteRenderer visibility using freshly verified `DepotMotionEvidence`. It is not connected to any live scene or backend yet.
- Unverified, unknown, signed-out/expired data fail closed and hide all company-owned sprites.
- ReservedParcel and ActivationReady can display verified site visuals but never warehouse art. StorageOperational may display a warehouse if the caller supplied authenticated verified status.
- The existing `DepotMotionRig` only moves separately layered sprites when it receives verified gate/door events. Reset is explicit on identity change and scene disable.
- `DepotVerifiedVisualAssemblyTests` cover unverified, stage progress, downgrade and invalid stage.
- `LogistiCore > Depot > Art 02 > Audit Sprite Production Readiness` checks local 16 PNG imports without altering them. It blocks approval when separately animated `sliding_gate_leaf_v2` or calibrated `fence_straight_v2` and `fence_corner_v2` are missing. This is a **mechanical check only**, not subjective production-art approval.

## Current real blocking issues
The ART-02B/C commits `8db6328`, `2ac66d2` and `1ea0ff88` were created in the *user's* worktree `D:/Projects/unitylogisticore-depot-art-02` but have not been pushed to origin. The GitHub ART-02 branch still points to `8265d9e`. The production-spec and PNG artwork from these local commits cannot be read or edited by this GitHub connector until pushed.

ART-02C confirmed that:
1. The original warehouse artwork is a useful projection anchor, but not a mathematically exact isometric camera.
2. `sliding_gate` uses the wrong projection and cannot become a sliding *leaf* just by moving the entire flattened sprite including pillars.
3. The two fence sprites are not trustworthy open-grid reusable modules.
4. Booth/pillars/sign scales and ground anchors need review; the prior 16 PNGs are **not final approved art**.
5. Device acceptance and real backend operation-event identifiers are absent.

## Release candidate definition
A real production-candidate review must have ALL of the following:
- New genuine semantically layered, alpha-clean, matched-camera PNGs: gate frame/pillars fixed; gate leaf separate; straight/orthogonal fence, corner, and modular posts; if loading doors animate, each individual door also needs independent approved artwork instead of destructive cropping.
- Authored source provenance and scale/ground anchor manifest. No perspective hacks through nonuniform scaling or mirroring.
- A dedicated Unity art scene with no gameplay authority, calibrated orthographic camera and phone-size frame.
- Verified stage mapping and operation-event adapters connected to approved sources; deterministic rest/open animations. Close/rest on signout, stale event, lost verification and scene disable.
- Unity compile + DEPOT EditMode + scene PlayMode tests + screenshots from Android/iOS representative devices, with no new failing baseline tests.
- Production sign-off **before** modifying a shipping scene or merging to main.

## Safe local sequencing
1. Close Unity; in the Codex worktree ensure `git status` has no unintended changes. Push the existing local ART-02C branch using `git push -u origin feature/depot-art-02-production-sprite-kit` (no force). This does **not** merge to main or affect three stashes in the original worktree.
2. Once GitHub has the real PNG sources and production specification, reconcile this independent ANIM-03 draft on top of those local commits. Do not force-reset the local Codex branch to this remote branch.
3. Re-author the fence and gate using genuinely independent geometry, not an AI image resize/crop that changes perspective or hidden alpha pixels. Review each image on dark/light backgrounds, inspect at native resolution and 800x450.
4. After asset review, configure `DepotVerifiedVisualAssembly` and `DepotMotionRig` only in an isolated non-shipping Unity scene. The motion rig itself does not generate backend evidence.
5. Run checks, review screenshots, then propose a release-oriented PR. Pending art/QA, keep all branches DRAFT.

No production gameplay scenes, Firebase, economy, player saves, Build Profiles or main changed. GitHub-authored code has not been compiled or tested in the user's Unity environment.
