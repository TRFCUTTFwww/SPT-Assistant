# 动态左下角文字

在“设置与热键 → JavaScript”页面编写脚本，设置刷新间隔，点击“保存并立即刷新”应用。脚本可以用 `return` 或 `cornerText = value` 输出，也可以在“显示表达式”里填写变量名、属性路径或表达式。原有静态文字作为首次请求完成前的备用内容。“测试运行一次”可以查看返回值、日志和报错。脚本编辑器和输出框都支持选中文字后右键复制。

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

脚本页的刷新间隔支持“时、分、秒、毫秒”，可以输入小数。比如 `100` 毫秒、`0.1` 秒表示相同的间隔。切换单位会换算当前数值，保持实际间隔不变。有效间隔为 1 毫秒～1 小时。

原有配置项 `Corner script interval` 继续以秒保存，支持小数；旧值 `30` 仍表示 30 秒。显示单位单独保存在 `Corner script display unit`，默认选择“秒”。

默认模式下，脚本和网络请求在后台串行执行，不在绘制回调中请求网络。上一次执行结果处理后再计时，到达间隔后在游戏更新帧中启动下一次；使用双精度实时时钟，实际更新频率受游戏帧率和脚本/网络耗时影响。失败时保留上次成功内容，错误直接显示在设置页。每次执行最多 64 个请求、总计最多 30 秒，每个请求超时 8 秒、请求正文和响应各最多 1 MiB。脚本最多 16384 字符，执行有限额，关闭或热更新时会取消任务。后台脚本不暴露 Unity、CLR、文件或进程接口。文字最多显示 160 字符，关闭富文本。

脚本保存在当前本机配置中，不加入“配置方案”的保存或套用。Jint 4.4.1 与 Acornima 1.2.0 及其许可证嵌入核心热更新模块；许可证原文位于 [Jint](licenses/Jint.txt) 和 [Acornima](licenses/Acornima.txt)。

## `game` 数据

点击测试输出下方的 **查看 game 数据**，可在输出框查看当前完整的 JSON。每次脚本运行都读取当时的只读快照。`game.state` 为 `menu`、`hideout` 或 `raid`，`game.capturedAtUtc` 是快照的 UTC 时间。数据未加载或当前场景不适用时，对象或字段为 `null`。撤离点列表为空时返回 `[]`。

| 路径 | 字段 |
| --- | --- |
| `game.raid` | `mapId`、`mapName`、`gameTime`（游戏内时刻）、`durationSeconds`、`elapsedSeconds`、`remainingSeconds`、`overtimeSeconds`、`countdownPaused` |
| `game.raid.weather` | `rain`、`fog`、`clouds`、`temperature`、`windX`、`windZ`，来自游戏天气曲线 |
| `game.raid.population` | 当前已加载的存活角色数 `alive`、`ai`、`humans`，真人数含自己 |
| `game.raid.exits` | 当前角色的撤离点，按直线距离排列，每项包含 `id`、`name`、`status`、`distanceMeters`。游戏原生状态值由 `status` 提供 |
| `game.raid.train` | `state`（`pending`、`approaching`、`boarding`、`departed`）、`arrivalInSeconds`、`departureInSeconds` |
| `game.raid.airdrop` | `naturalEnabled`、`nextAttemptInSeconds`、`attempts`、`maxAttempts`、`pendingFlares`。倒计时是下一次投放尝试 |
| `game.player` | `name`、`side`、`level`、`experience`，战局内另有 `alive`、`position`（`x/y/z`）、`headingDegrees`、`stance`、`aiming` |
| `game.player.vitals` | `health`、`energy`、`hydration`、`stamina`、`handsStamina`，每项含 `current`、`max`。`bodyParts` 下有 `head`、`chest`、`stomach`、`leftArm`、`rightArm`、`leftLeg`、`rightLeg` |
| `game.player.weapon` | 当前手持枪械的 `name`、`templateId`、`magazineRounds`、`magazineCapacity`、`chamberRounds`、`nextAmmo` |
| `game.hideout` | `craftsReady`，以及已有同步状态时的 `cultist.active`、`cultist.ready` |
| `game.performance` | 性能监测开启并完成采样后提供 `fps`、`frameMs`、`p99Ms`、`peakMs`、`slowFrames`、`sampleCount`、`sampleSeconds`、`assistantMs` |

`client.serverUrl` 和 `client.version` 提供 SPT 服务端地址和 EFT 客户端产品版本，也可以从 `game.client` 读取。

旧脚本的顶层字段继续可用。`game.raidRemainingSeconds` 保留原先的战局总时长行为。实时倒计时使用 `game.raid?.remainingSeconds`。

例如在“显示表达式”中直接填入下方内容，脚本可以留空：

```javascript
game.player?.vitals?.health?.current ?? "等待进入战局"
```

也可以把撤离点与剩余时间拼成一行，在“显示表达式”里填 `status`：

```javascript
const raid = game.raid;
const exit = raid?.exits[0];
const status = raid
  ? `${raid.mapName} · ${Math.ceil(raid.remainingSeconds ?? 0)}s${exit ? ` · ${exit.name} ${Math.round(exit.distanceMeters ?? 0)}m` : ""}`
  : `制作可领 ${game.hideout.craftsReady}`;
```

## 直接访问游戏对象

在 JavaScript 编辑器选择“实时”，或在脚本首行写 `// @game-thread`。测试和动态刷新都会在 Unity 主线程执行该脚本。`world`、`player`、`profile` 分别指向当前 GameWorld、本地玩家和可用角色档案。对象随场景变化，缺少时为 `null`。

```javascript
// @game-thread
return player?.Profile?.Nickname ?? "当前没有战局角色";
```

使用 C# 成员原名可以读取属性、给有公开 setter 的属性赋值，以及调用公开方法。对实时对象的写入会影响客户端，“测试运行一次”也会执行写入。动态显示会按刷新间隔重复运行。能否保存到角色档案取决于游戏和服务端的保存逻辑。`game` 仍提供冻结的快照。

实时脚本的 JS 执行预算为 150 毫秒，调用游戏方法本身会占用主线程。需要 `fetch` 或 `httpGet` 的脚本使用默认后台模式。
