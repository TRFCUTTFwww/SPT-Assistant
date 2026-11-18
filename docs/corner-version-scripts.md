# 动态左下角文字

在“设置与热键 → JavaScript”独立页开启“动态 JavaScript”，输入脚本，设置刷新间隔，再点“保存并立即刷新”。脚本使用 `return` 返回显示内容；原有静态文字作为首次请求完成前的备用内容。静态文字设置保留在“主题与布局”；应用静态文字会停止动态脚本，点“恢复原始”可恢复游戏版本显示。脚本页显示运行状态和当前显示内容，小窗口可滚动。

此处使用内嵌 Jint JavaScript 引擎，无需安装 Node.js 或 Python。JSON、字符串处理和 HTTP API 是这个功能的主要用途，JavaScript 可以直接完成。Lua 更适合已有 Lua 接口的游戏逻辑扩展；Python 更适合需要其数据处理库的外部工具。

显示时间：

```javascript
return "SPT · " + new Date().toLocaleTimeString();
```

显示 API 返回字段（保留原有同步辅助接口）：

```javascript
const data = JSON.parse(httpGet("https://example.com/api/status"));
return "SPT · " + data.version + " · " + data.status;
```

`httpGet(url)` 返回 HTTP / HTTPS 响应正文。使用自己的 API 地址替换示例地址；请求非成功状态会报告失败。返回 JSON 对象时先取字段，或使用 `JSON.stringify` 转换为文字。

也支持常见的异步 API 写法，可以在脚本顶层使用 `await`，或返回异步函数的 Promise。页面提供“填入异步联网示例”按钮：

```javascript
const url = new URL("https://example.com/api/status");
url.search = new URLSearchParams({ page: 1, page_size: 100 });
const response = await fetch(url);
if (!response.ok) throw new Error(`HTTP ${response.status}`);
const data = await response.json();
return data.version;
```

运行时为纯 .NET 的 Jint 解释器，未嵌入 Chrome、Chromium 或 WebView。Jint 执行 JavaScript 语言；Mod 提供 `fetch`、`URL`、`URLSearchParams`、`Headers` 和 `Response` 的常用数据接口。`fetch` 支持方法、请求头与字符串正文，响应提供 `ok`、`status`、`statusText`、`url`、`headers`、`text()`、`json()` 和 `clone()`。HTTP 404 等响应不会自动抛错，可自行检查 `ok`；网络和解析错误会使 Promise 拒绝。旧 `httpGet` 仍在非成功 HTTP 状态时抛错。

这些接口在后台工作线程中执行；`fetch` 返回 Promise，但网络传输保持串行。当前未提供 DOM、Node.js 的 `require`/npm、定时器、流式响应或完整浏览器 Web API。没有外部完成来源的未决 Promise 会直接报告错误，避免阻塞关闭或热更新。

脚本页的刷新间隔由数值输入框和单位下拉框组成，支持“时、分、秒、毫秒”，可以输入小数。比如 `100` 毫秒、`0.1` 秒表示相同的间隔；`1` 分等于 `60` 秒。切换单位会换算当前数值，保持实际间隔不变；点击“保存并立即刷新”应用设置。有效间隔为 1 毫秒～1 小时。

原有配置项 `Corner script interval` 继续以秒保存，支持小数；旧值 `30` 仍表示 30 秒。显示单位单独保存在 `Corner script display unit`，默认选择“秒”。

脚本和网络请求在后台串行执行，不在绘制回调中请求网络。上一次执行结果处理后再计时，到达间隔后在游戏更新帧中启动下一次；使用双精度实时时钟，实际更新频率受游戏帧率和脚本/网络耗时影响。设置 1 毫秒不代表每秒执行 1000 次，也不会积压并发请求。失败时保留上次成功内容，错误直接显示在设置页。每次执行最多 64 个请求、总计最多 30 秒，每个请求超时 8 秒、请求正文和响应各最多 1 MiB。脚本最多 16384 字符，执行有限额，关闭或热更新时会取消任务。脚本不暴露 Unity、CLR、文件或进程接口。文字最多显示 160 字符，关闭富文本。

脚本保存在当前本机配置中，不加入“配置方案”的保存或套用。Jint 4.4.1 与 Acornima 1.2.0 及其许可证嵌入核心热更新模块；许可证原文位于 [Jint](licenses/Jint.txt) 和 [Acornima](licenses/Acornima.txt)。
