# Smoke Checklist (run before each release)

Run the published `MarkdownReader.exe` on a clean machine (or after a full
`Remove-Item bin -Recurse` from your dev box). Tick each item.

## 启动

- [ ] 双击 `test_sample/windows_user_guide/windows_user_guide.md` → 1 s 内出现内容
- [ ] 再双击 `test_sample/internet_images/README.md` → 同窗口新标签页 < 200 ms 打开
- [ ] 直接双击 exe 不带参数 → 空窗或显示拖入提示

## 渲染

- [ ] `internet_images/README.md` 中的 shields.io badge 正常显示
- [ ] `windows_user_guide/windows_user_guide.md` 全部 99 张本地图片显示
- [ ] `windows_user_guide_python/windows_user_guide_python.md` 全部 90 张图片显示
- [ ] `docCenter/docCenter.md` 大文档：首屏 < 2.5 s，滚动到底流畅，图片渐进加载

## 主题

- [ ] 系统主题切换（设置→个性化→颜色），reader 自动跟随
- [ ] 菜单 视图→主题 切换至强制亮/暗，验证生效并持久化（重启后保持）

## 文件交互

- [ ] 用 VS Code 修改并保存其中一个文档，reader 自动重渲染、滚动位置保留
- [ ] 用资源管理器重命名打开中的文件，标签页继续工作
- [ ] 删除打开中的文件，橙色横条提示但内容仍可见

## 缓存

- [ ] 删除 `%LocalAppData%\MarkdownReader\image-cache` 后再开 `internet_images` → 能重新拉取
- [ ] 离线状态打开已缓存的 `internet_images` → badge 正常

## 大文件

- [ ] 构造一个 ~10 MB 的 .md（可以多次拼接 docCenter.md） → 看到「此文件较大」横条 + 继续/关闭按钮
- [ ] 构造一个 ~60 MB 的 .md → 看到「文件过大」错误

## 错误兜底

- [ ] 命令行传不存在的路径 → 红色横条 + 从最近列表移除按钮可用
- [ ] 卸载 WebView2 Runtime 后运行 → 弹出引导对话框，点击打开下载页

## 文件关联

- [ ] 工具菜单 → 设为 .md 默认打开 → 注册成功提示
- [ ] 资源管理器双击 .md 文件 → 用 MarkdownReader 打开
- [ ] 工具菜单 → 取消文件关联 → 取消成功

## 最近文件

- [ ] 最近菜单显示最近打开的文件
- [ ] 点击最近条目 → 重新打开
- [ ] 清空最近 → 列表为空

## DPI

- [ ] 在 100% / 125% / 150% DPI 下启动 → 文字清晰、布局正常
- [ ] 跨多显示器（不同 DPI）拖动窗口 → 渲染自动调整

---

## Not Covered Here (Deferred Tests)

The following were originally planned (Phase 5 of the implementation plan) but
deferred for v1 in favor of the manual smoke list + 56 unit tests:

- **WebView2 STA-dispatcher integration test fixture** (Task 5.1 in the plan).
  Setting up a real WebView2 inside an xUnit STA thread requires nontrivial
  infrastructure (Microsoft.UI.Xaml.Testing, custom test runners, etc.) and
  the resulting tests are notoriously flaky across machines.

- **End-to-end markdown rendering through WebView2** (Task 5.2). Covered manually
  in this checklist's "渲染" section.

- **mdimg:// scheme bytes parity test through WebView2** (Task 5.3 second half).
  Bytes parity for local files is covered by `LocalImageResolver` direct calls
  (the resolver doesn't need WebView2). The WebView2 round-trip is exercised
  by the manual "渲染" smoke.

Revisit when there's a specific regression that the manual checklist misses.
