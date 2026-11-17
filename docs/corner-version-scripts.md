# 动态左下角文字

在“设置与热键 → JavaScript”独立页开启“动态 JavaScript”，输入脚本，设置刷新间隔，再点“保存并立即刷新”。脚本使用 `return` 返回显示内容；原有静态文字作为首次请求完成前的备用内容。静态文字设置保留在“主题与布局”；应用静态文字会停止动态脚本，点“恢复原始”可恢复游戏版本显示。脚本页显示运行状态和当前显示内容，小窗口可滚动。

此处使用内嵌 Jint JavaScript 引擎，无需安装 Node.js 或 Python。JSON、字符串处理和 HTTP API 是这个功能的主要用途，JavaScript 可以直接完成。Lua 更适合已有 Lua 接口的游戏逻辑扩展；Python 更适合需要其数据处理库的外部工具。

显示时间：

```javascript
return "SPT · " + new Date().toLocaleTimeString();
```

显示 API 返回字段：

```javascript
const data = JSON.parse(httpGet("https://example.com/api/status"));
return "SPT · " + data.version + " · " + data.status;
```

`httpGet(url)` 返回 HTTP / HTTPS 响应正文。使用自己的 API 地址替换示例地址；请求非成功状态会报告失败。返回 JSON 对象时先取字段，或使用 `JSON.stringify` 转换为文字。

脚本和网络请求在后台执行，不在绘制回调中请求网络。刷新间隔为 5～3600 秒，上一次执行完成后再计时；失败时保留上次成功内容，错误直接显示在设置页。每次执行最多 4 个请求，每个请求超时 3 秒、响应最多 256 KiB。脚本最多 16384 字符，执行有限额，关闭或热更新时会取消任务。脚本不暴露 Unity、CLR、文件或进程接口。文字最多显示 160 字符，关闭富文本。

脚本保存在当前本机配置中，不加入“配置方案”的保存或套用。Jint 4.4.1 与 Acornima 1.2.0 及其许可证嵌入核心热更新模块；许可证原文位于 [Jint](licenses/Jint.txt) 和 [Acornima](licenses/Acornima.txt)。
