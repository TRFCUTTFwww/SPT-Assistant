# Changelog

## 2.4 — Updated GitHub release package (2026-11-27)

- Include the current changelog, flea pause fixes and linked release notes in both the offline installer and manual ZIP. Update the English and Chinese 2.4 notes for current offer state, unlimited-stock pause/resume and immediate list refresh.

## 2.4 — Refresh native offers immediately after editing (2026-11-27)

- Force the open native flea list to reload immediately after an offer is saved. Pausing or resuming replaces the native offer ID; the visible list now loads that replacement and applies the already updated state without waiting for the periodic metadata poll or a manual refresh.

[Immediate offer refresh / 报价状态立即刷新](docs/release-flea-immediate-refresh-20261127.md)

## 2.4 — Preserve paused offers with unlimited stock (2026-11-27)

- Preserve `RagfairOffer.ExtensionData` when projecting unlimited stock, paused expiry or Rouble-only requirements. SPT's record copy constructor drops that base dictionary; losing its pause marker made the editor report permanent lifetime and kept a permanent expiry when resuming temporary quotes.
- Remove the offer editor's bottom explanation paragraph and shorten its save confirmation.
- Reproduce the failure with unlimited stock, verify metadata survives each projection, and cover saved and temporary pause/resume flows. Repeat both flows through the running server: 1020 frozen seconds resume as 1019 remaining seconds with `paused=false` and `permanent=false`.

[Unlimited-stock pause fix / 无限库存报价暂停修复](docs/release-flea-pause-metadata-20261127.md)

## 2.4 — Read current flea offer state when editing (2026-11-27)

- Read the selected offer from the server whenever either editor entry opens, resolve saved and native IDs, and fill all fields from the current result. Show paused state explicitly and reject failed or missing reads before saving.
- Refresh native offer metadata while the market is visible, including when a previous metadata cache already exists. Read active saved offers from the live pool so pause markers, stock and other editable values reflect the current offer.
- Verify temporary/native offer lookup, stale-ID rejection, pause/resume state, current stock, inactive records and SQLite restoration across two processes.

[Offer editor state fix / 修改窗口报价状态修复](docs/release-flea-editor-state-20261127.md)

## 2.4 — Paused flea countdown display (2026-11-27)

- Project paused offer expiry as `now + pausedRemainingSeconds` in native ragfair responses and quote descriptions so paused quotes never expose `PermanentEndTime` (`9999-12-31`) as permanent lifetime while remaining protected from server expiration.
- Compute native offer expiry directly from `offer.EndTime - DateTimeExtensions.UtcNow` (fixing local timezone offset distortion in `UTC+8`), refresh visible `OfferView` rows immediately when quote metadata arrives, lock paused native countdown labels every frame, and prevent negative permanent minutes from silently overriding paused quotes.

[Paused countdown fix / 暂停报价剩余时间](docs/release-flea-paused-time-20261127.md)

## 2.4 — Main package and menu polish (2026-11-26)

- Keep uninstall/reimport support without deleting saved module data. Discover settings from module metadata, including legacy installed manifests, without compiling optional module identities into the core.
- Move the raid Map mode button to the end, add the supplied server page background and update installer preservation checks.
- Publish 2.4 from the current next source. Publish reviewed main-package documentation and a fresh source snapshot.

## 2.4 — Dedicated night vision page (2026-11-26)

- Add Visuals → Night vision for all full-screen and optic night vision controls, including hotkey, profiles, light suppression and custom rendering parameters. Remove duplicated night vision entries from Character display and the scope settings page.
- Rename Optics to Scope stickers, retaining sticker, zoom and rangefinder controls. Route search results and legacy scope page navigation to the appropriate page while preserving existing configuration keys and values.

[Night vision page / 夜视独立页面](docs/release-night-vision-page-20261126.md)

## 2.4 — Custom flea offer layout (2026-11-26)

- Place Add Offer, Show Edit and Show Withdraw in equal-sized toolbar cells. Use the same control height, spacing and alignment for search and status filters.
- Align each offer’s item, seller, price, stock, rating and status; use equal action buttons in a side column on wide panels and a bottom row on compact panels. Return to the top when changing the search.

[Custom flea offer layout / 自定义报价排版](docs/release-flea-layout-20261126.md)

## 2.4 — Fun module uninstall (2026-11-26)

- Add Delete / Confirm delete next to each installed fun module. Wait for the universal host to finish its existing busy-operation and shutdown lifecycle before removing installed releases.
- Keep module configuration and persistent state for later reimport. Persist pending removals across main-module hot reloads, reject stale host confirmations and protect paths outside the selected module.

[Fun module uninstall / 趣味模块卸载](docs/release-fun-uninstall-20261126.md)

## 2.4 — New repository baseline (2026-11-26)

- Start this repository from the current SPT Assistant source with one independent root commit. Prior Git commits, branches, tags and the old history directory are not imported. The previous repository is archived.
- Include native flea editing and withdrawal behind independent default-off switches, compact custom-offer management, SQLite persistence, complete item-tree restoration and pausable remaining time.
- Remove redundant source archives and legacy installers/packaging scripts; build current modules using the current source tree and CI.

[Withdrawal, cleanup and new repository / 下架、清理与新仓库](docs/release-flea-withdraw-new-repository-20261126.md)
[Flea editing and module separation / 报价编辑与功能拆分](docs/release-flea-edit-20261126.md)
