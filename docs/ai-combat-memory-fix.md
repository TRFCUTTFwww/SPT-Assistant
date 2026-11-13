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
