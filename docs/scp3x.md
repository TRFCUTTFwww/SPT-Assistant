# SCP 3-X Particle Disruptor / EFT 48558

3-X is a separate primary-slot rifle, fusion cell and ammunition item in the Developer trader. A purchased weapon includes four reserve charges and one chambered charge. The Developer's base location and localized location/description are `N/A` in every language, including live reload when the trader already exists.

The client uses the existing universal hot module host (`scp3x`). Registration is part of the existing ProjectE server hot module, avoiding a new stable host or a game/server restart. Existing vanilla donor definitions are preserved. Both hosts must already be installed.

## Controls

- Fire: hold the normal trigger to begin charging. Disintegration charges for 2.5 seconds; burst charges for one second, then emits three projectiles over one second and spends one charge.
- Middle mouse: switch disintegration / burst while idle.
- Reload: native inventory reload, backed by the custom fusion cell/charge definitions.
- L: original 3-X inspection motion. Existing EFT key bindings still apply.

Disintegration is selected initially so its body effect is directly accessible. Base projectile damage is 800 / 250. EFT supplies collision, health, kill attribution, ammunition removal, inventory and loot ownership. This is an adaptation of the original mechanics, not an emulation of SCP's shield, role or door systems.

## Original assets and fidelity

The asset conversion reads the user's local SCP: Secret Laboratory install (Unity 6000.4.11f1) and previously extracted 3-X data. EFT uses Unity 2022.3.43f2. Raw vertex buffers, UVs, normals, bone weights and bind poses are converted without changing their coordinate system. All eleven converted clips resolve every generic transform binding to the original hierarchy. Streamed cubic curves and constant/dense tracks are sampled at 60 Hz, with quaternion interpolation at runtime. Original gun PNG textures, inventory icon and WAV sounds are reused.

The original SCP shaders and game code cannot run as an EFT weapon. HDRP materials are mapped to supported EFT/Unity materials; source gun-part animations run on the reconstructed source rig. Native EFT arms, attachment/inventory handling and reload transactions remain EFT systems. The original special night-vision scope, role markers, SCP shield rules and door destruction are not implemented. Visual alignment, hand contact, shader appearance, sound timing and in-raid behavior require user verification. Build success or a running module is not evidence of a pixel-identical port.

## Confirmed kill disintegration

Only a confirmed local 3-X kill schedules disintegration. The created corpse is matched by raid identity and dead profile ID. Its skinned meshes are baked, move upward and progressively break into energy particles over 2.6 seconds. At completion, the matching native corpse and its inventory are destroyed through `GameWorld.DestroyLoot`. Separately dropped equipment is matched only through item IDs captured from that victim at death. A raid change, live/revived player, another corpse or an unknown identity blocks deletion. An unavailable corpse or unreadable geometry is reported and left intact.

The corpse effect is recreated with EFT-compatible meshes/particles; it does not claim to be the original SCP shader. Per the user's choice, a completed effect removes the victim's equipment and loot as well as the corpse. Unrelated nearby loot is excluded. Rendering state and module-owned meshes, audio and materials are restored or disposed during shutdown; reload waits for an active charge/effect to finish.

## Build and install

Game assets remain outside this source repository. Keep the user's extracted source pack and the generated assets locally; do not commit game files.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Install-Scp3X.ps1 -AssetsDirectory 'D:/SCPSL_3X_Integration_20261115/Assets'
```

The installer builds both changed components, runs boundary checks, backs up the previous server logic, replaces that module atomically and publishes an immutable client release with hashes. The independent 3-X catalog synchronization discovers the server hash, refreshes the three templates and Developer data and constructs three native item instances as a check. It does not require the ProjectE client module. Reopen the trader after a refresh if its window was already open.

Runtime initialization also constructs a hidden source rig and samples every converted animation before reporting `nativeRigValidated`. This checks native bone/mesh assignment and animation access without changing player inventory or displaying a test weapon. It does not replace in-raid visual verification.

Diagnostics: `BepInEx/plugins/UniversalModHost/state/scp3x/status.json` is host lifecycle status; `state/scp3x/data/status.json` reports current-process readiness, shots, confirmed kills, completed removals and errors. A `ready` state confirms loading only. The isolated server test covers registration, complete purchase trees, filters, prices, language metadata, donor preservation and repeated reload. The standalone rules cover firing/death/deletion boundaries.

Original behavior reference: https://en.scpslgame.com/index.php?title=3-X_Particle_Disruptor
