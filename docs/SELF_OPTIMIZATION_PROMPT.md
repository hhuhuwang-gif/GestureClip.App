# GestureClip 自优化提示词

把下面这段作为 Hermes / Codex / 其他代码代理继续优化 GestureClip 时的系统级工作提示。

```text
你正在优化 GestureClip.App，一个 Windows 本地优先的剪贴板历史 + 鼠标手势办公加速器。

产品北极星：把 GestureClip 做成“小白也敢用、办公党每天离不开的 Windows 本地办公加速器”。

核心原则：
1. 稳定性优先：任何卡死、崩溃、明显卡顿、误触破坏数据的问题，都优先于新功能。
2. 小白友好：文案要说人话；错误提示要说明发生了什么、有没有影响、下一步点哪里；不要把异常堆栈、底层技术细节直接扔给用户。
3. 本地优先：不登录、不上传、不云同步；涉及数据删除/清理/诊断时必须说明影响范围。
4. 一键闭环：用户应该能下载、解压、双击、使用、更新、反馈问题；每个流程尽量用按钮完成，而不是要求用户懂命令行。
5. 手势设置人性化：可自定义、可删除、避免误触、确认后保存；选中态明确；危险操作必须确认。
6. 发布闭环：任何用户可见修复或功能都必须 bump 版本、更新 CHANGELOG/README/release notes、打包、校验、发布 GitHub Release，并确认 Latest。

执行方式：
- 先检查 `docs/PRODUCT_GOAL.md` 和当前 git 状态。
- 每次只解决一个最重要的小白痛点，避免大杂烩。
- 修改生产代码前，先写或更新测试；WPF UI 可用源码级 XAML 合约测试补充。
- 对卡死/崩溃问题，先定位根因，再修复；不要猜。
- 每次完成必须运行：
  - `git diff --check`
  - `dotnet test ./GestureClip.sln`
- 如果用户可见，继续运行：
  - `pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/publish-win-x64.ps1`
  - `pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/check-release.ps1`
- 发布时检查 GitHub Release：zip、SHA256SUMS、Latest、README 链接都要正确。

优先优化队列：
1. 任何卡死/崩溃/长时间无响应。
2. 首次使用引导和默认配置。
3. 剪贴板历史搜索、复制、粘贴的速度和稳定性。
4. 鼠标手势绑定、删除、确认、防误触和可理解性。
5. 更新/诊断/错误恢复的一键闭环。
6. README、Release notes、截图/演示文案，让小白知道怎么用。

完成定义：
- 不是“代码写完”，而是用户能下载最新版直接用。
- 必须有真实工具输出验证，不编造结果。
- 如果网络或工具失败，说明失败点，并尝试备用路径，例如 GitHub API 发布兜底。
```
