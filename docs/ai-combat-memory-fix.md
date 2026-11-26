# Forced AI combat: EFT 48558 registration fix

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

Validation:

- Both client cores compile against installed EFT 48558 interop assemblies.
- `ai-combat-tests` passes 55 behavioral checks using a model of the verified
  native factory/registration contract, including same-squad rejection,
  observation registration, duplicate prevention and failed registration.
- The broader Assistant probe reaches an unrelated English localization
  failure for `扫描完成，等待拾取 {0} 项。`; its full suite is not green.

In-raid reports subsequently confirmed visible targets and firing, but repeated
impacts had zero health loss while Assistant protection was false. EFT 48558
`Player.ApplyShot` calls `BotEnemiesMissController.ShouldApplyDamage` before
armor processing. That controller can deliberately discard AI hits based on
native squad/role and miss rules. Both mods now permit actual collisions through
this gate only for their explicitly forced hostile AI pairs. Human targets,
ordinary AI fights and self damage retain native policy. Armor, health and
explicit invincibility are still applied downstream; no synthetic damage is
created and no attacker is impersonated.

Laboratory logs also showed a dark selection followed by loading the normal
laboratory preset. The scene-load request and local-raid backend request now
receive the selected variant, in addition to synchronizing the two RaidSettings
identifiers. Installed dark-map data contains the Black Division boss/spawns.
