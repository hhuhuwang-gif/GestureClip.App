# 界面耗时诊断（--trace-ui）

`--trace-ui` 打开设置窗口的耗时埋点，用来回答“打开慢/切页卡”到底慢在哪一段。
默认关闭：这些行对普通用户只是噪音，只在排查时开启。

```powershell
.\Start-GestureClip.bat --trace-ui
```

日志目录 `%LOCALAPPDATA%\GestureClip\logs`，类别 `UiTiming`，共五类：

| 行 | 含义 |
| --- | --- |
| `SettingsWindow constructed in N ms` | `InitializeComponent` 把 8 个页面一次性读进内存 + DI 解析的耗时 |
| `Settings window ready in N ms (cold/warm)` | 从开始建窗到 `Show()` 返回，`cold` 表示本次进程首次打开 |
| `Page 'x' ready in N ms` | 切到某个页面后，到调度器把布局排完为止的耗时（含首次实例化该页视觉树） |
| `Startup blacklist N ms, theme N ms, main window N ms` | 建窗之前的三步：黑名单刷新、主题资源加载、`MainWindow` 构造 |
| `Tray menu N ms, icon N ms, notify icon N ms` | 托盘初始化三段：`ContextMenuStrip` 创建、ICO 解码、`NotifyIcon` 注册 |

## 实测基线（2026-10-27，Release，`--smoke-exit-after-startup` 采样 4 次）

| 指标 | 冷启动首次 | 热（同一构建连续启动） |
| --- | --- | --- |
| `starting → started` | 1182 ms | 727 ~ 812 ms（中位 **759**） |
| `SettingsWindow XAML load` | 101 ms | 74 ~ 88 ms |
| `SettingsWindow constructed in` | 197 ms | 152 ~ 173 ms（中位 **155**） |
| `Settings window ready in [cold]` | 494 ms | 374 ~ 451 ms（中位 **388**） |
| `Page 'home' ready in` | — | 见 8 月基线 377 ms（smoke 模式采样不到） |

## 启动分阶段实测（中位，单次 759 ms 的那一档）

| 区间 | 耗时 | 内容 |
| --- | --- | --- |
| `starting` → DB migrations | 96 ms | SQLite 迁移 |
| → default data seed | 25 ms | 默认数据播种 |
| → blacklist / theme / MainWindow | 26 ms | 9 黑名单 + 17 主题资源 |
| → tray icon initialized | 78 ms | 21 菜单 + 5 图标 + 8 NotifyIcon，余下是 WinForms 首次初始化 |
| → clipboard listener started | 80 ms | 剪贴板监听 + 历史服务 |
| → hotkeys / edge trigger | 12 ms | 三组热键 + 边缘触发 |
| → SettingsWindow constructed | 155 ms | XAML 74 + 其余 81 |
| → Settings window ready | 233 ms | `Show()` + 231KB 视觉树首次布局 |

合计约 705 ms，与 759 ms 的差值即日志写入与调度抖动。
**结论：启动没有单点巨坑**，最大三块是窗口首次布局 233、建窗 155、DB 96。

`constructed − XAML load ≈ 86 ms`，即 DI 解析 ViewModel、恢复窗口位置、初始化集合的成本。
也就是说：**XAML 内联页面只占建窗的一半，另一半在 ViewModel**。

一键复测：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-ui-performance.ps1
```

## 已知结论

- 8 个 `TabItem` 的内容是内联 XAML，**全部在 `InitializeComponent` 时构造**，只有被选中的那个参与布局。
- 但实测 XAML 载入只有 82~88 ms（热）——**拆页面的收益上限约 90 ms**，且要牵动 49 处代码引用的
  `x:Name`，收益与风险不成比例。**结论：不做这项重构**；除非将来页面继续膨胀到 XAML 明显超过
  ViewModel 成本，再用 `check-ui-performance.ps1` 重测。
- 切页时不再有任何数据重载：`MainSettingsTabControl_SelectionChanged` 只同步左侧导航选中态；
  切页开销来自新页面首次进视觉树后的测量。
- `MainWindow` 从不 `Show()`、也没有任何地方读 `Application.MainWindow`，看上去是纯浪费，
  但 **A/B 实测结论相反**：保留它启动 759 ms（建窗 155 / 显示 388），删掉后 835 ms
  （建窗 207 / 显示 514）——它其实是最便宜的一次 WPF「首次建窗」预热，删掉等于让 231KB 的
  设置窗自己付这笔钱。**结论：保留，并继续单独计时**，防止以后被当成冗余再删一次。
- 托盘菜单原来在 `Initialize` 里预建一遍，而 `ContextMenuStrip.Opening` 每次都会 `RebuildMenu()`，
  那次预建是纯重复。已移除，首次右键时按当时的设置快照生成；顺带清空启动路径上的菜单项构建。
  实测菜单项构建只占 20~25 ms，**它不是托盘 78 ms 的大头**，大头是 WinForms 首次初始化。
- 启动的大头已经定位清楚（见上表），没有「单点巨坑」。剩余空间只能靠**重排启动顺序**
  ——把 DB 迁移、播种、剪贴板监听挪到设置窗 `Show()` 之后（它们都不是首帧必需），
  预计可省 200 ms 左右，但要先确认没有服务在启动期依赖这些数据。
