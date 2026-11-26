# 当前主仓库

主 MOD 的活动仓库为 local-spt5-assistant-next；本机源码位于 `D:/EFT SPT 5.0BE/EFT SPT 5.0BE/_spt5_assistant_next`。旧 local-spt5-assistant 为只读归档，新仓库以当前源码建立独立初始提交，后续提交只记录新仓库的发展，不包含旧仓库 Git 历史。继续维护时不得重新导入旧 Git 历史或 history 源码快照。

# AI 提交与协作

当前本机的共享说明位于仓库上一级：`../README-AI-LOCAL.md`。用户明确要求将 token 和提交方法共享给本机后续 AI，请先读取该说明，再执行需要认证的 Git 操作。

- 用户已授权本项目的常规提交和推送：完成当前任务并通过适当验证后，可自行提交并推送 `origin main`，无需重复确认。
- 修改前查看 `git status`，保留其他协作者的未提交改动；提交实际完成的工作，提交说明写清行为变化和验证范围。
- 本机说明包含 token，位于仓库之外。不要将其复制到仓库，不要提交凭据、游戏文件、存档、日志或编译产物，也不要回显 token。
- 如果该本机说明不存在，仍可进行不依赖认证的开发；认证时使用当前环境已配置的凭据，不要从历史提交寻找 token。
- 当前运行环境、构建命令和验证说明见本仓库 `README.md`。
- 用户要求以后的新独立客户端 MOD 使用通用热更新加载器。新功能从 `universal-example` / `universal-sdk` 开始，按 `docs/universal-mod-host.md` 的生命周期契约开发，用 `scripts/Publish-HotModule.ps1` 发布。不要再为每个新 MOD 建一个专用宿主。
- **禁止修改主 README：** 除非用户当次对话明确要求修改主 README，否则后续任何 AI **不得修改**仓库根目录的 `README.md` 和 `README.zh-CN.md`（包括在顶部追加版本简报、插入更新段落或改写结构）。版本更新、功能说明与验证记录请写入 `CHANGELOG.md` 与 `docs/release-*.md`。

# GitHub 发布

GitHub 使用当前主仓库的独立源码快照。Release 仅发布本体安装器、手动安装 ZIP、核心热更新模块及这三个文件的 SHA-256 校验清单。发布前同步包内 README 与文档，不保留旧 Markdown 副本或追加其他模块附件。
