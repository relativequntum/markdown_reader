# Markdown Reader

A lightweight, fast, read-only Markdown viewer for Windows 10/11.

Built for one job: double-click a `.md` file, see it rendered instantly. No editing, no plugins, no distractions.

## Features

- **Fast cold start** (~500 ms to first paint); subsequent opens in the same window are ~50 ms
- **Single instance, multi-tab** — second double-click adds a tab to the running window
- **Doesn't lock files** — other processes can write, rename, or delete the file you're viewing
- **Auto-reload** on external save (200 ms debounce); scroll position preserved
- **GFM**: tables, task lists, strikethrough, autolinks
- **Code highlighting** via highlight.js (35+ languages, github theme)
- **Unified image pipeline**:
  - Remote URLs (with anti-hotlink Referer + 15 s timeout)
  - Local relative paths (`./images/foo.png`)
  - Local absolute paths / `file://`
- **LRU disk cache** for remote images (500 MB / 5000 files default); offline-capable once cached
- **Follows system theme** (light/dark); can be forced via menu
- **Per-monitor DPI awareness** (PerMonitorV2)
- **File association** under HKCU (no admin needed)
- **Sandboxed**: WebView2 + DOMPurify + path-whitelist resolver + scheme-restricted custom URI

## Quick Start (Build)

Prerequisites:
- Windows 10 / 11
- .NET 8 SDK ([download](https://dotnet.microsoft.com/download/dotnet/8.0))
- Node.js 20+ ([download](https://nodejs.org))
- Microsoft Edge WebView2 Runtime (already on Win11; [download for Win10](https://developer.microsoft.com/microsoft-edge/webview2/))

Build:

```powershell
git clone https://github.com/relativequntum/markdown_reader.git
cd markdown_reader

# Optional: install viewer deps first (publish.ps1 will do this anyway)
npm --prefix viewer install

# Build a single-file release exe (~170 MB, self-contained)
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/publish.ps1
```

The resulting exe is at `publish/win-x64/MarkdownReader.exe`. Copy it anywhere you like (Documents, Tools folder, etc.) and run.

## Usage

```powershell
MarkdownReader.exe path\to\file.md
```

Or open the exe and **工具 -> 设为 .md 默认打开** to register it as the default opener for `.md`. After that, double-clicking a `.md` in Explorer routes through the single-instance Mutex / named pipe so subsequent files open as new tabs in the running window.

### Menu

| Menu | Function |
|------|----------|
| 视图 -> 主题 | System / Light / Dark |
| 工具 -> 清理图片缓存 | Wipe `%LocalAppData%\MarkdownReader\image-cache` |
| 工具 -> 打开缓存目录 | Open the cache directory in Explorer |
| 工具 -> 设为 .md 默认打开 | Register file association (HKCU) |
| 工具 -> 取消文件关联 | Remove file association |
| 最近 | Recent files (up to 20, persisted) |

## Configuration

Per-user, no roaming:

- **Settings**: `%LocalAppData%\MarkdownReader\settings.json`
- **Image cache**: `%LocalAppData%\MarkdownReader\image-cache\`
- **WebView2 user data**: `%LocalAppData%\MarkdownReader\WebView2\`

You can edit `settings.json` directly if needed (the app overwrites it on changes). Available keys: `Theme`, `ImageCacheMaxBytes`, `ImageCacheMaxFiles`, `RecentFiles`, `ImagePathWhitelist` (extra directories allowed for image resolution), `MaxRecent`.

## Architecture

WPF native shell (window management, file IO, IPC, image proxy) + WebView2 renderer (markdown-it / highlight.js / DOMPurify). Custom `mdimg://` URI scheme unifies remote/local/relative image handling.

See:
- [Design spec](docs/superpowers/specs/2026-05-12-markdown-reader-design.md) — architecture + design decisions
- [Implementation plan](docs/superpowers/plans/2026-05-12-markdown-reader.md) — task-by-task breakdown
- [Smoke checklist](docs/smoke-checklist.md) — release verification
- [AOT spike](docs/spike-2026-05-12-aot.md) — why we don't use Native AOT

## Development

```powershell
# Run unit tests
dotnet test src/MarkdownReader.Tests
# Run viewer tests
npm --prefix viewer test
# Run integration tests
dotnet test src/MarkdownReader.IntegrationTests
```

Debug build (`dotnet build -c Debug`) skips the viewer Vite build to keep iteration fast. Run `npm --prefix viewer run dev` in a separate terminal for live-reload while editing the viewer.

## License

TBD by author.

---

*Built with Claude Code by Anthropic.*
