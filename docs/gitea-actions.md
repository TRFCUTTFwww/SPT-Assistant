# Gitea Actions 构建

推送到 `main` 自动构建，也可在仓库 Actions 页选择 `SPT 48558 build` 后手动运行。相同分支的新任务会取消上一轮构建。

构建由账户共享 Runner 的 `spt-dotnet-10` 标签执行，使用 .NET 10 和 `/opt/spt-references` 中的 EFT 48558 / SPT 5.0 引用。Runner 同时运行一个任务。

- Server modules：构建服务端 DLL 和 `LocalGearAdmin.module`。
- Client modules：构建客户端宿主和 `Local.Spt5.ESP.Core.module`。
- 产物在对应运行页下载，文件名包含源码提交与提交时间，保留 7 天。

本工作流只构建和上传本仓库产物；不会创建 Release、同步 GitHub 或部署到游戏目录。
