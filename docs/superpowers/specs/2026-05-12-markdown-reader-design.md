# Markdown Reader — Design Spec

**日期**：2026-05-12
**目标平台**：Windows 10 / 11（x64）
**目标**：一个轻量、秒开的只读 Markdown 阅读器，支持代码高亮、远程图床与本地图片。

---

## 0. 范围与非目标

### 范围（v1）
- 单文件 `.md` 阅读（无编辑）
- 通过文件关联（双击 .md）启动（exe 直接传文件路径作为参数，因此命令行 `MarkdownReader.exe foo.md` 自然兼容）
- 单实例 + 多标签页
- 基础 CommonMark + GFM（表格、任务列表、删除线、自动链接）
- 代码块语法高亮（highlight.js common 子集）
- 图片三类来源：远程 URL、本地相对路径、本地绝对路径 / `file://`
- 远程图片自动缓存（LRU、500 MB / 5000 文件上限）
- 防盗链兼容（Referer 策略）
- 文件外部修改时自动重渲染，保留阅读位置
- 跟随系统亮 / 暗主题
- 主题、最近文件、缓存目录配置持久化

### 非目标（明确不做）
- 编辑功能
- 实时协作 / 多人查看
- 数学公式（KaTeX / MathJax）
- Mermaid 图表
- Front matter / 脚注 / 自动 TOC 目录
- 全文搜索 / 多文档导航
- 插件系统
- 跨平台（macOS / Linux）
- 网络代理 / 自定义 CA

---

## 1. 整体架构

```
┌──────────────────────────────────────────────────────────┐
│  MarkdownReader.exe  （单文件 AOT 编译产物）             │
│                                                          │
│  启动入口 (Program.cs)                                   │
│   ├─ 解析命令行参数（文件路径）                          │
│   ├─ 抢占命名互斥锁 (Mutex)                              │
│   │    ├─ 抢到：作为主实例继续启动                       │
│   │    └─ 没抢到：通过命名管道把文件路径发给主实例后退出 │
│   └─ 启动 WPF 应用                                       │
│                                                          │
│  WPF 主窗口                                              │
│   ├─ TabControl（标签页容器）                            │
│   │    └─ 每个 Tab = 一个 WebView2 + 一份文档状态        │
│   ├─ 命名管道服务（接收后续启动传来的新文件路径）        │
│   └─ 文件监视 (FileSystemWatcher)：磁盘变化自动重渲染    │
│                                                          │
│  WebView2 内部（前端，bundle 进 exe 的资源）             │
│   ├─ viewer.html  +  viewer.css  +  viewer.js            │
│   ├─ markdown-it（核心解析）                             │
│   ├─ markdown-it-task-lists（GFM 任务列表）              │
│   ├─ highlight.js（代码高亮，common 子集）               │
│   └─ DOMPurify（HTML 净化，防 XSS）                      │
│                                                          │
│  自定义协议处理器  mdimg://                              │
│   ├─ mdimg://local/<encoded-path>  → 解析本地相对路径    │
│   ├─ mdimg://abs/<encoded-path>    → 本地绝对路径        │
│   └─ mdimg://remote/<encoded-url>  → 走原生 HTTP 客户端  │
│        ├─ 自动重写 Referer（防盗链兼容）                 │
│        └─ 磁盘缓存：%LocalAppData%\MarkdownReader\cache\ │
└──────────────────────────────────────────────────────────┘
```

### 分层原则

原生外壳只做五件事：窗口管理 / 标签页 / 文件 IO / 进程间通信 / 图片代理。所有 Markdown 解析和渲染都在 WebView2 内部跑。

- 原生层（C#）：估算 800-1500 行
- 前端层（无框架纯 JS）：估算 400-700 行

### 性能目标

| 操作 | 目标 |
|------|------|
| 冷启动到第一篇文档可见 | < 500 ms |
| 暖启动（开新标签页） | < 100 ms |
| 切换标签页 | < 30 ms |

（实际性能基准 fixture 见 §6。）

### 文件 IO 策略：不锁文件

读取 `.md` 时**显式开放共享**，允许其他进程同时写、改名、删除：

```csharp
using var fs = new FileStream(
    path,
    FileMode.Open,
    FileAccess.Read,
    FileShare.ReadWrite | FileShare.Delete,
    bufferSize: 4096,
    FileOptions.SequentialScan);
var text = await new StreamReader(fs, detectEncoding: true).ReadToEndAsync();
// 出 using 立即释放句柄；之后渲染都用内存里的字符串副本
```

`FileSystemWatcher` 监听**目录**，本身不持有任何文件句柄。

**重命名场景**：`Renamed` 事件触发时，标签页内部记的"当前文件路径"自动更新到新路径，继续监听。
**删除场景**：`Deleted` 事件触发时，标签页顶部显示橙色横条 `⚠ 文件已被删除（最后一次内容仍在显示）`，但不关闭标签页——内容已在内存中。

---

## 2. 单实例 IPC

### 资源命名（用当前用户 SID 隔离，多用户登录互不干扰）

- Mutex：`Local\MarkdownReader.SingleInstance.{userSID}`
- Named Pipe：`MarkdownReader.OpenFile.{userSID}`

### 启动流程

```
启动 → 尝试创建 Mutex
        │
        ├─ 创建成功（我是主实例）
        │   ├─ 启动 WPF 主窗口
        │   ├─ 后台开 NamedPipeServerStream，循环接收消息
        │   └─ 命令行有文件路径时，在第一个 Tab 打开它
        │
        └─ 创建失败（已有主实例在跑）
            ├─ 用 NamedPipeClientStream 连接主实例（500 ms 超时）
            │   ├─ 连接成功：发送 UTF-8 文件路径 → 退出（exit code 0）
            │   └─ 连接超时/失败：主实例可能挂了，等待 1 s 后自己升格为主实例
            └─ 没命令行参数时：发送 "FOCUS" 让主实例置顶
```

### 消息协议

一条消息一行 UTF-8 文本，制表符分隔参数：

- `OPEN\t<absolute-path>\n` — 开新标签页（或切到已有 Tab）
- `FOCUS\n` — 把主窗口拉到前台
- 第一行若不是已知命令，整行（去掉换行）当作文件路径兜底处理

### 主实例收到消息后

1. Pipe 服务线程收到消息 → `Dispatcher.InvokeAsync` 切到 UI 线程
2. UI 线程：`AllowSetForegroundWindow` + `SetForegroundWindow` 把窗口拉到最前
3. 检查目标路径是否已有标签页（按规范化后的绝对路径比对）：
   - 已有 → 切到该 Tab
   - 没有 → 新建 Tab，走 §1 的文件读取与渲染流程

### 孤儿 Pipe 处理

服务端 `using NamedPipeServerStream` 在主实例正常退出时自动清理。异常退出留下名字时，下次启动新主实例创建同名 pipe 会复用该名字（`PipeOptions.CurrentUserOnly` + `maxInstances=1`）。

### 安全性

`PipeOptions.CurrentUserOnly` 限定只接受同一用户的连接，防止低权限恶意进程通过 pipe 注入路径。

---

## 3. 图片管道（图床 + 本地路径统一处理）

### 为什么不直接用 `<img src="https://...">`

两个原因：
1. 大多数图床（GitHub user-images、知乎、微博等）有防盗链，需要特定的 `Referer`；WebView2 默认带的 `Referer` 会被拒。
2. 我们要做本地缓存，而 WebView2 的 HTTP 缓存我们无法精细控制。

### 方案

注册自定义 URI scheme `mdimg://`，在 Markdown 解析时把所有 `<img src>` 改写到这个 scheme 上，原生侧拦截并代理请求。

### URL 编码格式

所有 payload 用 **base64url** 编码（无 `+/=`），避免 URL 转义问题：

```
mdimg://local/<base64url(相对路径)>?base=<base64url(.md 所在目录绝对路径)>
mdimg://abs/<base64url(绝对路径或 file:// URL)>
mdimg://remote/<base64url(完整 https?:// URL)>
```

`local` 和 `abs` 分开是因为 base 目录每个标签页独立，需要跟着文档走。

### 三类请求的处理

**1. 本地路径（`local` / `abs`）**

- 解码 → 拼绝对路径 → 校验**没有越出**白名单目录（base 目录及其子目录、用户 home、`%TEMP%`、显式列入的目录）
- 用 `FileShare.ReadWrite | FileShare.Delete` 打开（同 §1 策略）
- Content-Type 按扩展名映射：`png` / `jpg` / `jpeg` / `gif` / `webp` / `svg` / `avif` / `bmp` / `ico`

**2. 远程图片（`remote`）**

查询缓存键 = `SHA256(原始 URL)`：

- **命中**：直接读 `cache/<hash 前 2 位>/<hash>.bin` 返回 + 异步刷新 access-time（用于 LRU）
- **未命中**：走原生 `HttpClient` 拉取 → 写入缓存 → 返回。元数据（`Content-Type`, `ETag`, `fetched-at`, 原始 URL）写同目录下 `<hash>.meta.json`

**Referer 策略**（按顺序尝试）：

1. 第一次：`Referer: <URL 的 origin>`，例：`https://i.imgur.com/foo.png` → `Referer: https://i.imgur.com/`。这能搞定 90%+ 的图床。
2. 收到 403 / 401 时：去掉 Referer 重试一次。
3. 仍失败：标记此 URL 在本次会话内不再重试，返回占位图。

UA 固定为最新 Edge 的常见 UA 字符串，减少被服务端按 UA 拒掉的概率。

### 缓存策略

- **位置**：`%LocalAppData%\MarkdownReader\image-cache\`
- **分片**：按 hash 前两位分子目录，避免单目录文件过多
- **上限**：默认 **500 MB / 最多 5000 个文件**，**LRU 淘汰**
- **清理时机**：App 启动后**异步**扫描（不阻塞 UI），超额时按 access-time 删
- **手动清理入口**：菜单"清理图片缓存"

### 错误回退

任何阶段失败（文件不存在、网络错、解码错、越界路径）→ 返回 inline SVG 占位图：

```
┌────────────────────┐
│   ⚠ 图片加载失败    │
│   <原 src 的省略>   │
└────────────────────┘
```

点击占位图可在浮层显示完整错误（HTTP 状态、网络异常等）。

---

## 4. 渲染管道（WebView2 内部）

```
.md 文件 (UTF-8 bytes, in memory)
        │
        ▼
┌───────────────────────────────────────┐
│  1. 解析 (markdown-it)                │
│     - html: false（默认禁内联 HTML）  │
│     - linkify: true（自动链接）       │
│     - typographer: false              │
│     - 插件: markdown-it-task-lists    │
│     - 表格/删除线/自动链接：核心选项  │
└───────────────────────────────────────┘
        │  token 流
        ▼
┌───────────────────────────────────────┐
│  2. 渲染规则定制                      │
│     - image: rewrite to mdimg://…     │
│     - fence/code_block: 调 highlight  │
│     - link: target="_blank" + 校验    │
└───────────────────────────────────────┘
        │  HTML 字符串
        ▼
┌───────────────────────────────────────┐
│  3. 净化 (DOMPurify)                  │
│     - 允许 mdimg:// 协议              │
│     - 禁止 <script>/<iframe>/onclick  │
│     - 保留 class（高亮需要）          │
└───────────────────────────────────────┘
        │  安全 HTML
        ▼
┌───────────────────────────────────────┐
│  4. 注入 DOM                          │
│     contentEl.innerHTML = html        │
│     保存/恢复 scrollTop               │
└───────────────────────────────────────┘
```

### 各步骤要点

**markdown-it 配置**

- `html: false`：用户的 `.md` 里直接写 `<script>` 不会执行（双保险，DOMPurify 是第二道）
- GFM 表格、删除线、自动链接都是核心选项，不需要额外插件
- 任务列表用 `markdown-it-task-lists` 插件（< 2 KB）

**代码高亮**

- 用 `highlight.js/lib/common` 子集（C / C++ / C# / Python / JS / TS / Java / Go / Rust / Bash / JSON / YAML / SQL / HTML / CSS / Markdown 等约 35 种，压缩后 ≈ 50 KB）
- 显式声明语言（` ```python `）走对应 parser；未声明走 `auto-detect`
- 找不到的语言：原样输出 + 一行小灰字 `未识别语言: foo`
- 主题：跟着应用主题切（light → `github.css`、dark → `github-dark.css`）

**链接处理**

- `http(s)://` → 加 `target="_blank" rel="noopener noreferrer"`，点击时拦截 `WebView2.NewWindowRequested`，调原生层用默认浏览器打开
- `#anchor` → 文档内跳转
- `file://` 或相对路径 `.md` → 用本 App 打开（新标签页）
- 其他类型本地文件 → 用系统默认程序打开（弹一次确认窗）

**图片改写**（在 markdown-it 渲染规则里做）

```js
md.renderer.rules.image = (tokens, idx) => {
  const src = tokens[idx].attrGet('src');
  const alt = tokens[idx].content;
  const rewritten = rewriteSrc(src, currentDocDir);  // → mdimg://...
  return `<img src="${rewritten}" alt="${escapeHtml(alt)}" loading="lazy">`;
};
```

`loading="lazy"`：长文里下方图片晚一点请求，进一步提速首屏。

### 主题与样式

- 三档：跟随系统 / 强制亮 / 强制暗（保存在 `%LocalAppData%\MarkdownReader\settings.json`）
- 跟随系统：监听 `SystemEvents.UserPreferenceChanged`，给 WebView2 切 `PreferredColorScheme`，CSS 用 `@media (prefers-color-scheme: dark)`
- 字体：正文默认 `Segoe UI, "Microsoft YaHei UI", sans-serif`；代码 `Cascadia Mono, Consolas, monospace`
- 最大正文宽度 ≈ 820 px 居中；可在设置里调

### 文件变化时的重渲染

```
FileSystemWatcher.Changed 触发
  → debounce 200 ms（编辑器保存可能连续触发多次）
  → 重新读文件（FileShare.ReadWrite | Delete）
  → 重新解析 + 渲染
  → JS 端：保存 scrollTop% → innerHTML 替换 → 恢复 scrollTop%
```

`scrollTop%` = `scrollTop / scrollHeight`。在文档前面增删内容时阅读位置基本能保持住。

### 大文件策略

- **文件 ≤ 8 MB**：直接走完整管道，无任何提示
- **文件 8–50 MB**：标签页显示一个温和提示 `此文件较大 (X.X MB)，渲染可能需要几秒` + `[继续渲染]` `[关闭标签页]` 按钮；点继续后正常渲染
- **文件 > 50 MB**：拒绝渲染，提示"文件过大（XX MB），可能不是文本"

### 海量内容渲染优化（针对 docCenter 类文档）

- 图片全部 `loading="lazy"`
- 段落容器加 `content-visibility: auto` + `contain-intrinsic-size`，让屏幕外段落延迟计算布局
- markdown-it 解析放到 **Web Worker**，避免大文档解析阻塞 UI 线程
- 解析 → 渲染分块：每 ~500 个顶层 block 用一次 `requestIdleCallback` 让出主线程

---

## 5. 数据流与错误处理

### 关键数据流（端到端轨迹）

**冷启动 + 文件参数（双击 `report.md`）**

```
1. OS 启动 MarkdownReader.exe "C:\Docs\report.md"
2. Program.Main: 解析 argv → 抢 Mutex（成功，我是主实例）
3. WPF App 启动，初始化 WebView2 环境（共享 UserDataFolder）
4. 创建 MainWindow → 创建第一个 Tab
5. Tab 初始化：
   a. 异步读文件 (FileShare.ReadWrite | Delete) → 字符串
   b. WebView2.NavigateToString(viewerShell)
   c. WebView2 加载完成 → postMessage({type:'render', md, baseDir})
   d. 前端：解析 → 改写图片 → 净化 → innerHTML
   e. 启动 FileSystemWatcher 监视目录
6. 同时后台启 NamedPipeServer 接收后续启动消息
```

**第二个文件双击（`design.md`）**

```
1. OS 启动新进程 MarkdownReader.exe "C:\Docs\design.md"
2. Program.Main: 抢 Mutex（失败）
3. 连接 \\.\pipe\MarkdownReader.OpenFile.{userSID}
4. 发送 "OPEN\tC:\Docs\design.md\n" → 进程退出
5. 主实例 pipe server 收到 → Dispatcher.Invoke 到 UI 线程
6. UI 线程检查是否已有该文件的 Tab：
   - 有 → 切到该 Tab
   - 无 → 创建新 Tab，重复冷启动第 5 步
7. SetForegroundWindow 把窗口拉到最前
```

**文件被外部编辑器保存**

```
1. FileSystemWatcher 触发 Changed 事件（可能连发 2-3 次）
2. 200 ms debounce 计时器重置 / 启动
3. 计时器超时 → 重新读文件 → 重新渲染
4. JS 端：保存 scrollTop% → 替换 innerHTML → 恢复 scrollTop%
```

### 每个 Tab 的状态

```csharp
class TabState {
    string FilePath;          // 当前路径（会随重命名更新）
    string BaseDir;           // 用于解析图片相对路径
    string? RawText;          // 内存里的最新内容
    FileSystemWatcher? Watcher;
    DebounceTimer ReloadDebouncer;
    DateTime LoadedAt;
    bool IsDeleted;           // 文件被删后置 true，但 Tab 保留
}
```

### 全局状态

```csharp
class AppState {
    Settings Settings;             // 主题/字体/缓存大小/最近文件
    ImageCacheIndex CacheIndex;    // 懒加载，LRU access-time 表
    List<string> RecentFiles;      // 最多 20 条
    NamedPipeServerStream PipeServer;
    Mutex SingleInstanceMutex;
}
```

`Settings` / `RecentFiles` 写入 `%LocalAppData%\MarkdownReader\settings.json`，**原子写**（写到临时文件 → `File.Replace`），避免崩溃时配置文件被截断。

### 错误处理矩阵

| 错误类型 | 触发场景 | 处理方式 |
|---------|---------|---------|
| 文件不存在 | 命令行路径无效；从最近文件打开但已删除 | 标签页显示红色横条 `找不到文件: <path>`，提供"从最近列表移除"按钮 |
| 权限不足 | 受保护目录的 .md | 同上，消息换成"无权访问" |
| 编码非 UTF-8 | 老旧 GBK 文件 | `StreamReader(detectEncoding: true)` 自动探测；探测不出时按系统 ANSI 解码并在顶部提示"编码自动识别为 GBK，如显示异常请告知" |
| 文件 > 50 MB | 异常巨大文件 | 不读，直接报错"文件过大（XX MB），可能不是文本" |
| Pipe 连接超时 | 主实例僵死 | 当前进程升格为主实例（等 1 s 让旧 Mutex 释放后重抢；仍不行则报错） |
| WebView2 Runtime 缺失 | Win10 老版本未自带 | 启动时检测，弹原生 MessageBox 提示安装并提供[官方下载链接]按钮（用默认浏览器打开） |
| WebView2 渲染进程崩溃 | 罕见的内核 bug | 监听 `ProcessFailed`，自动重建该 Tab 的 WebView2 并重新渲染 |
| 图片加载失败 | 网络 / 防盗链 / 越界路径 | 占位图（§3 已述） |
| 缓存目录写失败 | 磁盘满 / 权限 | 退化为内存缓存（不持久），状态栏给一个图标提示 |
| Settings.json 损坏 | 强制断电等 | 加载失败时改用默认值，原文件备份到 `settings.json.bad-<timestamp>`，弹一次提示 |

### 错误展示原则

- **可恢复错误**：标签页内部用顶部横条展示（橙色=警告，红色=错误），不弹原生对话框（不打断用户）
- **致命错误**（WebView2 缺失、磁盘满）：原生对话框，给出明确动作
- **后台错误**（缓存目录写失败、最近文件清理失败）：只写日志 `%LocalAppData%\MarkdownReader\log.txt`，不打扰用户。日志按天滚动，保留 7 天

---

## 6. 测试策略

桌面 GUI 全自动化代价高、收益低，所以策略是**纯逻辑层重度自动化 + WebView2 集成层薄烟测**。

### 分层

```
                        ┌──────────────────────────┐
       高 ROI / 全自动  │  C# 单元测试 (xUnit)      │  ~85% 行覆盖
                        │  JS 单元测试 (vitest)     │  ~85% 行覆盖
                        ├──────────────────────────┤
       中 ROI / 半自动  │  C#-WebView2 集成测试     │  关键路径
                        ├──────────────────────────┤
       低 ROI / 手动    │  发版前手工冒烟清单       │  full UX
                        └──────────────────────────┘
```

### C# 单元测试（项目 `MarkdownReader.Tests`，xUnit）

| 模块 | 测试点 |
|------|--------|
| `PathValidator` | 越界检测：`../`、绝对路径转义、UNC、长路径；白名单内的路径放行 |
| `MdImgUrlCodec` | base64url 往返编码、特殊字符、空 base、畸形 URL 拒绝 |
| `Debouncer` | 200 ms 触发；连续触发只跑一次；并发触发线程安全 |
| `ImageCache` | hit/miss/逐出顺序（LRU）、并发写同 key、磁盘满模拟（mock IFileSystem） |
| `EncodingDetector` | UTF-8 BOM/无 BOM、GBK、UTF-16 LE/BE、空文件 |
| `RefererPolicy` | origin 推导、403 后回退到无 Referer |
| `SingleInstanceProtocol` | 消息解析：OPEN/FOCUS/未知前缀兜底；非法字节 |
| `Settings` | 原子写、损坏文件回退、缺字段用默认值 |

### JS 单元测试（项目 `viewer/`，vitest）

```
viewer/
  src/
    parser.ts        ← markdown-it + 插件包装
    rewriteSrc.ts    ← 图片 src → mdimg://
    linkRules.ts     ← target/blank/相对路径处理
    scrollAnchor.ts  ← scrollTop% 计算
  test/
    parser.test.ts
    rewriteSrc.test.ts
    fixtures/
      gfm-table.md
      task-list.md
      code-blocks.md
      tricky-images.md      ← 含相对/绝对/远程/data:/file://
      malicious.md          ← <script>、onerror、javascript:、SVG XSS
```

恶意输入测试是重点：DOMPurify 配错一个允许标签 = 一个 XSS 漏洞。每个 `malicious.md` 用例断言渲染产物里没有 `<script>`、没有 `on*=`、没有 `javascript:` 协议。

### C#-WebView2 集成测试

在 `MarkdownReader.IntegrationTests` 项目里用真实 `WebView2` 控件 + headless 模式跑：

| 用例 | 验证 |
|------|------|
| 端到端渲染 | 加载 fixture .md → 抓 DOM → 断言含 `<table>`、`<img>`、`class="hljs-*"` |
| 自定义协议 | 请求 `mdimg://local/...` → 拿到正确 bytes + Content-Type |
| 单实例 IPC | 启动主进程 → spawn 第二个 → 主进程 `OnMessage` 触发，消息内容正确 |
| 文件监视 | 写文件 → 200-400 ms 内观察到重渲染信号（前端 postMessage 回 native） |

CI 上跑：GitHub Actions `windows-latest` 自带 WebView2 Runtime，直接能跑。

### 真实世界 Fixtures（性能 + 真实场景基准）

四份样本放在开发机本地的 `test_sample/`（**不入 git**，因为总大小 ~580 MB，主要是图片），路径通过 `appsettings.IntegrationTests.json` 配置。GitHub Actions CI 跑不到这些 fixture，所以**集成基准测试只在开发机本地跑**；CI 上只跑纯逻辑单元测试。

| Fixture | 大小 | 图片数 | 测什么 |
|---------|------|-------|--------|
| `internet_images/README.md` | 0.6 KB | 1 (远程 shields.io) | 远程图片管道、Referer 策略、缓存命中/未命中 |
| `windows_user_guide/windows_user_guide.md` | 76 KB | 99 (本地相对路径) | 中等文档、本地相对路径解析、`images/<sha256>.jpg` 命名模式 |
| `windows_user_guide_python/windows_user_guide_python.md` | 69 KB | 90 (本地相对路径) | 同上，第二份样本防止单文档偶然性 |
| `docCenter/docCenter.md` | 4.7 MB | 1446 (本地相对路径) | 大文档解析、海量图片懒加载、滚动性能 |

性能基准（写进集成测试 + 手工冒烟清单）：

| 操作 | 目标 | Fixture |
|------|------|---------|
| 冷启动到 README 可见 | < 500 ms | internet_images |
| 冷启动到 windows_user_guide 首屏可见 | < 800 ms（含前 5–10 张图） | windows_user_guide |
| 冷启动到 docCenter 首屏可见 | < 2.5 s | docCenter |
| docCenter 滚动到底（持续滚动） | 60 fps 无卡顿，渐进加载图片 | docCenter |
| 第二次打开 docCenter（缓存暖） | < 1.5 s 首屏 | docCenter |
| docCenter 切换到其他标签页再切回 | < 50 ms（不重新渲染） | docCenter |

Fixtures 路径写进 `appsettings.IntegrationTests.json`，避免硬编码。

### 手工冒烟清单（发版前）

写在 `docs/smoke-checklist.md`，每次发版手过一遍：

- [ ] 首次双击 .md：< 1 s 内可见
- [ ] 再双击另一个 .md：新标签页在 < 200 ms 内打开
- [ ] 直接双击 exe 不带参数：开空窗或显示"拖入文件"提示
- [ ] 切换系统亮/暗主题：reader 自动跟随
- [ ] `internet_images/README.md` 的远程图片正常显示
- [ ] `windows_user_guide` / `windows_user_guide_python` 全部本地图片正常显示
- [ ] `docCenter` 大文档：首屏 < 2.5 s、滚动流畅、图片渐进加载
- [ ] 文件正在显示时用 VS Code 保存：自动重渲染、滚动位置保留
- [ ] 文件正在显示时用资源管理器重命名：标签页继续工作
- [ ] 文件正在显示时删除：橙色横条提示，内容仍可见可滚动
- [ ] 缓存目录手动删空：远程图片重新拉取并重建缓存
- [ ] 离线状态打开已缓存过的远程图片：图片全部命中
- [ ] WebView2 Runtime 在测试机上卸载：启动给出友好引导

### 不测什么（YAGNI）

- WPF 控件渲染（依赖 Microsoft 内部，回归概率低）
- markdown-it 自身（人家有自己的测试套件）
- highlight.js 高亮准确性（同上）
- 操作系统 Mutex / NamedPipe 的行为（OS 保证）

---

## 7. 项目结构（建议）

```
markdown_reader/
├── src/
│   ├── MarkdownReader/                    # 主程序 (C#, WPF, .NET 8 AOT)
│   │   ├── Program.cs
│   │   ├── App.xaml(.cs)
│   │   ├── MainWindow.xaml(.cs)
│   │   ├── SingleInstance/
│   │   ├── Tabs/
│   │   ├── Files/                         # 文件读取、Watcher、编码探测
│   │   ├── Images/                        # mdimg:// 处理器、缓存、Referer
│   │   ├── Settings/
│   │   └── Resources/viewer/              # 内嵌的前端 bundle
│   ├── MarkdownReader.Tests/              # xUnit 单元测试
│   └── MarkdownReader.IntegrationTests/   # WebView2 集成测试
├── viewer/                                # 前端 (TypeScript, vitest)
│   ├── src/
│   ├── test/
│   ├── package.json
│   └── vite.config.ts                     # 打包到 src/MarkdownReader/Resources/viewer/
├── test_sample/                           # 真实世界 fixtures
│   ├── internet_images/
│   ├── windows_user_guide/
│   ├── windows_user_guide_python/
│   └── docCenter/
├── docs/
│   ├── superpowers/specs/
│   │   └── 2026-05-12-markdown-reader-design.md   ← 本文档
│   └── smoke-checklist.md
└── README.md
```

---

## 8. 依赖与版本

### 原生（C#）

| 依赖 | 用途 | 备注 |
|------|------|------|
| .NET 8 SDK | 编译目标 | Native AOT |
| `Microsoft.Web.WebView2` | 嵌入 Edge 内核 | 1.0.2592+ (支持 CustomSchemeRegistrations) |
| 标准库 | Mutex / NamedPipe / FileSystemWatcher / HttpClient | 不引入额外包 |
| `xunit` + `xunit.runner.visualstudio` | 单元测试框架 | 仅测试项目 |

### 前端（TypeScript）

| 依赖 | 用途 | 大小（min+gz 估算） |
|------|------|----------|
| `markdown-it` | Markdown 解析 | ~25 KB |
| `markdown-it-task-lists` | GFM 任务列表 | < 2 KB |
| `highlight.js` (lib/common) | 代码高亮 | ~50 KB |
| `dompurify` | XSS 净化 | ~20 KB |
| `vite` | 打包工具 | dev only |
| `vitest` | 测试框架 | dev only |

前端 bundle 总大小目标 < 150 KB（gzip 后），全部内嵌进 exe 作为资源。

---

## 9. 风险与待验证项

| 风险 | 影响 | 缓解 |
|------|------|------|
| WebView2 `CustomSchemeRegistrations` API 在某些 WebView2 Runtime 版本不可用 | 自定义协议失效 | 启动检测；要求 WebView2 Runtime ≥ 一定版本，否则提示升级 |
| .NET 8 AOT 对 WebView2 互操作的兼容性 | 启动失败 / 反射相关报错 | 实施计划的第一步做"hello WebView2" AOT 编译 spike，验证 COM 互操作与 trimming/AOT 警告，再决定下一步 |
| docCenter 4.7 MB 解析时间不达标（目标 < 2.5 s 首屏） | 大文档体验差 | Web Worker + 分块渲染（§4 已规划）；实施计划早期 spike 用真实 fixture 实测确认 |
| 极端长行 / 病态 Markdown 让 markdown-it 卡住 | UI 卡死 | Worker 内解析自动隔离主线程；超时 5 s 显示"解析超时，可能是异常文档" |
| GitHub raw 之类的图床改 Referer 策略 | 部分图片失败 | Referer 策略可配置（settings.json 加白名单/黑名单 origin） |

---

## 10. 不影响 v1，但记录的未来方向

- 搜索（Ctrl+F）：先用 WebView2 内置的 Find；如果不够再做自定义
- 打印 / 导出 PDF：WebView2 自带 `PrintAsync`
- 拖入文件夹：列出目录里的 .md 选择打开（最近文件之外的另一种入口）
- 数学公式（KaTeX）：作为可选包，按文档需要懒加载

这些都不进 v1。
