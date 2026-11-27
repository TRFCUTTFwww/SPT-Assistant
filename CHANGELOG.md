# Changelog

## 2.4 — Paused flea countdown display (2026-11-27)

- Keep the original frozen duration when editing a paused quote; reserve -1 for permanent quotes. Resolve native quote metadata before treating a long expiry as permanent.
- Hydrate paused timing on the native market without first opening the MOD offer page. Share the frozen-time display between native rows, custom rows and the editor; reject stale background responses.

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
