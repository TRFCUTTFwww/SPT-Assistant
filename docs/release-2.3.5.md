# 2.3.5 — Independent seasonal character

The Seasons character slot now starts a separate local character instead of reusing the existing PVE character. Select **Create seasonal character**, complete the native character creation flow, and select **PVE** whenever you want to return to the original character.

首次选择赛季入口时建立独立空白账号，再进入游戏原生的新建角色流程。仓库、任务、等级、藏身处、对话、保险和交易记录均保存在独立的完整 SPT 档案中。原有 PVE 档案保留，选择 PVE 即可切回。不会自动重置或覆盖已创建的赛季角色。

The server captures the selected account ID at the HTTP request boundary, so native creation, services, caches and asynchronous saves receive the same account ID throughout each request. It retains the native PVE backend response and local raid pipeline. The client passes the seasonal selection through without rewriting it to PVE or substituting the original profile descriptor.

A durable association under LocalGearMail/seasons links the two accounts. The original full profile is backed up before first selection. Independent account IDs and Scav IDs, ownership validation, an atomic association file, repeat-selection recovery and out-of-raid checks protect both profiles. Nothing creates a new live character until the user selects the seasonal slot.

This provides a local seasonal character slot using SPT PVE gameplay. It does not add official online matchmaking, rankings, remote rewards or an automatic seasonal wipe schedule. Existing local season features remain subject to SPT support.

Validation: client build and full probe; server build and detached integration suite, including real Harmony hooks for native game mode and duplicate profile creation. Tests cover blank startup, separate identities, unchanged original/other accounts, request routing, persistence, interrupted selection recovery, and raid/ownership guards. Native first-time character creation and in-game visuals remain for user verification; the agent did not create, reset or select the user's real character.

After the update, leave the current character-selection screen and open it again to fetch the new slot data. Host version remains 1.6.2. Custom corner-version text remains available.
