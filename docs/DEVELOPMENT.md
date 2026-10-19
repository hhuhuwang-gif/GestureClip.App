# 开发指南

## 环境与构建

Windows x64、Git、.NET 8 SDK。只有 .NET 运行时无法编译。

脚本检查 PATH、`DOTNET_ROOT` 和用户目录 `.dotnet` 中的 SDK。以下命令在仓库根目录执行：

```powershell
. .\scripts\build-common.ps1
$sdk = Get-DotNetSdk
Invoke-Checked $sdk @('restore', '.\GestureClip.sln') 'restore'
Invoke-Checked $sdk @('build', '.\GestureClip.sln', '-c', 'Release', '--no-restore') 'build'
Invoke-DotNetTests $sdk @('test', '.\GestureClip.sln', '-c', 'Release', '--no-build', '--no-restore')
```

测试失败直接报错；每次测试硬超时 60 秒。避免在同一工作目录并发执行多个构建或测试进程，WPF 生成文件会互相覆盖。

## 目录职责

| 目录 | 内容 |
| --- | --- |
| `src/GestureClip.Core` | 领域模型与接口 |
| `src/GestureClip.Features` | 剪贴板、手势、工位小熊等业务逻辑 |
| `src/GestureClip.Infrastructure` | SQLite、Windows API、系统集成 |
| `src/GestureClip.App` | WPF 界面与依赖装配 |
| `tests/GestureClip.Tests` | 单元测试与 SQLite 集成测试 |
| `scripts` | 构建、发布、安装与回归验证 |
| `docs` | 使用说明、开发说明、当前发布说明与现有图片 |
| `artifacts/release` | 本地产物，Git 忽略 |

## 发布与验证

```powershell
# 测试 → publish → 签名 → 便携 ZIP → Setup ZIP → SHA256
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-setup.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-release.ps1 -RequireSetup
# 发布工具自身的回归检查，不执行真实安装
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\tests\Test-ReleaseVerification.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\tests\Test-InstallCommand.ps1
# 专项检查
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-gesture-optimization.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-performance.ps1
```

`build-exe.bat` 是便携版构建入口。构建不强制结束正在运行的应用；若输出目录被使用，先从托盘正常退出对应实例。

程序统一从 `artifacts/release/GestureClip/GestureClip.exe` 启动，不再在根目录复制两份 EXE。未配置证书时明确跳过签名；签名配置与故障处理见 [代码签名](CODE_SIGNING.md)。没有 Inno Setup 时生成 Setup ZIP；安装了 Inno Setup 时还会构建 Setup EXE，编译失败会报错。

自动化测试不代替真实桌面交互，发布前按 [回归清单](regression-checklist.md) 验证快捷键、鼠标手势与剪贴板操作。

## 维护约定

旧代理任务、阶段报告和过期发布草稿已移出当前目录；历史可从 Git 记录恢复。保留 CHANGELOG、当前发布说明和全部图片。

本地执行记录放在 `.codex-tasks/`，不提交。提交前使用 `git diff --check` 和 `git diff` 检查具体变化，提交说明应描述本次修改；不使用固定文案自动提交全部文件的脚本。

用户数据始终在 `%LOCALAPPDATA%/GestureClip/`，不放入发布包或 Git，不在源码清理中删除。
