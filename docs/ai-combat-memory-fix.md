# Forced AI combat: EFT 48558 registration fix

Cyberpsychosis and Assistant's forced AI combat share `NativeEnemyMemory`.
The installed client logged missing target keys from `EnsurePersonal`; earlier
diagnostics also showed a commanded goal without visibility or shooting.

Inspection of EFT 48558's native methods confirmed that `BotMemory.AddEnemyInfo`
skips native squad members. `BotEnemiesController.AddNew` constructs and returns
an `EnemyInfo` without registering it. `SetInfo` inserts the dictionary entry,
appends the sorted observation entry, sets its index and increments the count.
The fallback now calls both methods and checks registration before reading it.
Repeated updates reuse the existing entry and do not reset the same goal.

Assistant also honors explicit individual hostility before the default squad
friendship during native group evaluation, and synchronizes native hostility
collections before commanding an enemy. Per-owner relationship checks remain
responsible for other squad members.

The manual Relic malfunction notice, key binding, optional Assistant setting,
rendering path and packaging references have been removed.

Validation:

- Both client cores compile against installed EFT 48558 interop assemblies.
- `ai-combat-tests` passes 55 behavioral checks using a model of the verified
  native factory/registration contract, including same-squad rejection,
  observation registration, duplicate prevention and failed registration.
- Quickhack rules and installed API checks pass (93,464 checks).
- Built assembly inspection confirms `SetInfo` in both cores and no Relic UI
  type or binding strings in the quickhack core.
- The broader Assistant probe reaches an unrelated English localization
  failure for `扫描完成，等待拾取 {0} 项。`; its full suite is not green.

Offline checks and successful hot reload do not establish in-raid firing.
User verification: select two living nearby AI with clear sight, apply
Cyberpsychosis or set the pair hostile in Assistant, and verify engagement.
Also verify the removed keypad-0 notice no longer appears. Temporary combat
overrides are cleared by hot reload, so apply the command again after updating.

## Follow-up: actual damage, Blackwall stalls and dark laboratory

In-raid reports subsequently confirmed visible targets and firing, but repeated
impacts had zero health loss while Assistant protection was false. EFT 48558
`Player.ApplyShot` calls `BotEnemiesMissController.ShouldApplyDamage` before
armor processing. That controller can deliberately discard AI hits based on
native squad/role and miss rules. Both mods now permit actual collisions through
this gate only for their explicitly forced hostile AI pairs. Human targets,
ordinary AI fights and self damage retain native policy. Armor, health and
explicit invincibility are still applied downstream; no synthetic damage is
created and no attacker is impersonated.

Blackwall's 2048x2048 atlas previously normalized each pixel through IL2CPP array
indexers on first use. It now uses bulk span copies and a managed byte-array
conversion. The atlas, death audio and first effect are prepared at module
startup. Blackwall visuals use a bounded reusable pool without primitive
colliders; effects avoid per-frame list copies and repeated camera lookups.
Protection checks bind only the live Assistant generation and cache the delegate
instead of scanning every loaded assembly for every potential chain target.
The first installed optimized core logged 261.1 ms of resource preparation at
startup. This is a startup measurement, not a before/after in-raid frame-rate
benchmark.

Laboratory logs also showed a dark selection followed by loading the normal
laboratory preset. The scene-load request and local-raid backend request now
receive the selected variant, in addition to synchronizing the two RaidSettings
identifiers. Installed dark-map data contains the Black Division boss/spawns.
Scene changes require the next raid; this does not replace an already loaded map.

Follow-up validation: both cores compile; 10,063 combat/atlas model checks and
93,465 quickhack rules/native API checks pass. The broader unrelated localization
failure above remains outside this change. In-game damage, perceived Blackwall
smoothness, and the next raid's dark scene/spawn result still require user checks.
