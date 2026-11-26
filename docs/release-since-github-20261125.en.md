# Changelog: GitHub 2.3.5 → Current Development Build

[简体中文](release-since-github-20261125.zh-CN.md)

## Comparison scope

| Item | Snapshot |
| --- | --- |
| Latest GitHub source | `main`, `22fb884a99e1401a8c89250c4c5b7dcecc1d58a9` |
| Latest GitHub release | [v2.3.5 Experimental](https://github.com/TRFCUTTFwww/SPT-Assistant/releases/tag/v2.3.5), November 21, 2026 |
| Current development source | [a91acd9f10fdf98c3f0a4b18b583ecb257f90380](http://172.16.0.1:3000/TRFCUTTF/local-spt5-assistant/commit/a91acd9f10fdf98c3f0a4b18b583ecb257f90380), November 25, 2026 |
| Version number | Both snapshots use `2.3.5`; these development changes have not been published to GitHub |
| Difference | 83 commits ahead; 258 files changed, 23,501 insertions and 1,441 deletions, including source, assets, tests and documentation |

The GitHub branch and release were checked online on November 25, 2026. Entries describe the final source state; reverted intermediate implementations are excluded. These changelog files are excluded from the comparison statistics.

## Added

### Optics and shooting

- **Scope sticker editor:** Open with `Ctrl+F10` while aiming. Import PNG/JPEG images, drag, scale, rotate and adjust opacity. Layouts are saved per optic and image, and each optic remembers its last image.
- **Sticker cycling and effects:** Configurable previous/next shortcuts default to `Up / Down`, with wraparound. Added automatic black/white contrast and confirmed-hit flashes with RGB/HEX colors and adjustable duration.
- **Extended optical zoom:** An independent switch under Visuals → Optics enables `Alt+wheel` zoom beyond the optic's native range, up to the optical camera's accepted limits. Fixed optics are supported. The game's native reticle/magnification shortcut retains the optic's original zoom endpoints.
- **Sensitivity follows magnification:** Native sensitivity remains in use within the supported zoom range. Beyond the endpoints, sensitivity continues to scale with actual optical magnification. Disabling, leaving ADS or unloading restores native behavior.
- **Steady aim:** A separate switch reduces breathing and weapon sway while the local player aims, independently of no recoil.
- **Bullet wall penetration:** Added an optional local-player setting for ordinary bullets to pass through scene cover while retaining native target-hit and armor resolution.
- **Mixed-caliber loading:** Separate magazine and chamber compatibility switches cover the stash, hideout shooting range and local solo raids, including revolver cylinders.

### Character, inventory and profiles

- **Bush and swamp movement:** Added separate No bush slowdown and No swamp slowdown switches under Character → Movement & Noclip. Disabling or unloading restores native restrictions.
- **Individual buffs:** Select native effects and configure duration, strength, and applicable body-part or skill parameters. Expanded effect labels.
- **Owned item editing:** Added an Edit action for owned stash and local-raid items, covering supported durability, remaining uses, key uses, switches and Found-in-Raid state.
- **Dogtag editing:** Edit name, kill time, faction, level, status and killing weapon. Removed the 10,000 level cap consistently across client editing, commands and server validation.
- **Profile JSON editor:** Data → JSON Editor provides tree browsing, search, field descriptions, entity-name resolution, node edits and full-profile import/export, with automatic backups and identity/inventory validation.
- **Raid-history redelivery:** Resend recorded starting gear and extracted or retained items through in-game system mail.
- **Nw11 contact:** Added a chat contact and dedicated avatar, plus recovery when local chat identity decryption fails.

### Flea market and trading

- **Dedicated flea market page:** Sync PvP/PvE reference prices, set per-item prices and listing permissions, filter by scope/category, apply batch changes, and save, switch, rename, delete or import/export named presets.
- **Market rule switches:** Added options to unban non-quest items and high-class armor plates, remove level/category restrictions, mark purchases Found in Raid, keep AI offers at full condition with plates intact, use a Rouble-only market, hide AI barter offers and regenerate AI offers live.
- **Automatic pricing and return comparison:** The native listing window can price from minimum minus one, average or maximum, multiplied by a configurable ratio. Compare the best trader buyback with net flea proceeds after fees.
- **In-raid trading:** Online Trading allows listing owned items through the native window during local solo raids. Direct purchases can go to the backpack, with quantities that cannot fit delivered by system mail.
- **Free Trade and Batch Listings:** Separate options remove the simultaneous active-offer limit and the item-quantity limit per listing.
- **Smuggler:** Added an independent option to remove local solo raid carry-in and non-FIR discard quantity restrictions, including restricted items inside discarded containers.

### Raids, visuals and world editing

- **Raid event notifications:** Added individually configurable popups and reminders for BTR departure/arrival/leaving, aircraft arrival, airdrop release, BOSS death, and armored train arrival/departure.
- **Map-selection clock pause:** Pause or resume the native morning/evening clocks and start at the selected time. Assistant and native `O`-key countdowns now display matching seconds.
- **Sky palette:** Time & Environment supports sky-tint presets and RGB/HEX input.
- **Camera-view marker filter:** Optionally restrict extraction, BTR stop, tracked loot and teleport markers to the current camera view.
- **Loot ESP colors and ammunition details:** Added configurable category colors and an ammunition gradient based on penetration/damage. Details include caliber, damage, penetration, armor damage, velocity, recoil adjustment and adjacent tiers within the same caliber. Cash and seasonal documents are excluded from ESP value estimates.
- **Continuous container placement:** Preview follows the crosshair, with floating/overlapping placement and distance adjustment. Grid snapping defaults off; ground snapping and collision limits are separate settings.
- **Menu artwork:** Added optional category backgrounds, category-switch animation and Story artwork, with improved bottom alignment and image proportions. The existing icon menu is retained.
- **AI difficulty input:** Enter precise nonnegative multipliers beyond the original slider ranges.

### Memory editor and hot updates

- **Dedicated memory workspace:** Added Memory Scan, Address List, Access Monitor, Disassembly, Code Patches and Memory Browser tabs.
- **Scanning and address management:** Supports unknown initial values, comparisons/ranges, rounded floats, wildcard AOB, UTF-8/UTF-16 strings, scan scopes and unaligned scans, plus freezing, pointer chains, table persistence and local hotkeys.
- **Hardware access monitoring:** Added x64 hardware breakpoints, read/write source inspection, general-register and XMM0–XMM15 snapshots, context views and export.
- **Disassembly and patching:** Browse live process addresses with configurable context and history. Apply NOP/arithmetic/branch edits, Intel assembly, AVX instructions, relocated code caves and persistent AOB patch scripts. Active patches are restored on unload.
- **Pointer scanning and structure dissection:** Added static pointer scans, field-change comparisons, pointer drill-down and multiple hex interpretation/write formats.
- **Local hot-update selection:** Choose a core `.module` or `.dll` under Settings & Hotkeys → Version Update for the existing host to load. Added Gitea CI builds for EFT 48558 with commit/UTC identifiers in module artifacts.

## Fixed and improved

- Fixed native filters still rejecting mixed-caliber rounds, failure to feed from magazines, and chambering failures in the hideout or after changing weapons. Reduced repeated inventory scans and stutters while dragging ammunition.
- Fixed inverted, distorted or jittering scope stickers, native optic updates overwriting extended zoom, and sensitivity stopping at native zoom endpoints. Sticker and zoom controls now share the Optics page.
- Fixed independent bush `Swamp_collider` objects being missed because they lacked tree components. Detection now uses native bush overlap state and obstacle slowdown flags.
- Magazine info shows the full chambered ammunition specification instead of “Chamber 1,” distinguishes empty chambers, reads the active revolver chamber, excludes spent cases and wraps long names.
- No-recoil hooks resolve the current local firearm controller at invocation time. Steady-aim temporary values are restored after each calculation.
- Extended Infinite Launcher to every launcher holding a live rocket in a local solo raid.
- Hit feedback reads actual health loss after shot resolution, reducing false zero-damage readings. Positive loss below 0.1 displays as `<0.1`.
- In-raid purchases retry the original order without duplicate charges, roll back on save failure and wait for mail delivery confirmation. Fixed duplicate inventory Edit actions after hot reloads.
- Profile node edits check conflicts at the edited node, preserving unrelated newer profile data and avoiding rejection due to unrelated background updates.
- Reduced repeated corner JavaScript snapshot collection, serialization, parsing and loot-label measurement work.
- Pruned invalid bleeding-decal nodes and raid-teardown player references, blocked late AI spawns during settlement, and fixed menu text focus and overlapping memory-editor controls.

## Removed

- Removed independent seasonal profiles, client season entry points and server account routing.
- Removed automatic pausing on ordinary game errors, the full-screen recovery overlay, emergency rescue extraction and the external crash monitor. Hot-update failure handling, rollback and diagnostic logging remain available.

## Verification record

- The current source passed the EFT 48558 Release build, 29 bush/swamp movement and lifecycle checks, and 164 Probe groups.
