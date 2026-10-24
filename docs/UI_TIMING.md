# 界面耗时诊断（--trace-ui）

`--trace-ui` 打开设置窗口的耗时埋点，用来回答“打开慢/切页卡”到底慢在哪一段。
默认关闭：这些行对普通用户只是噪音，只在排查时开启。

```powershell
.\Start-GestureClip.bat --trace-ui
```

日志目录 `%LOCALAPPDATA%\GestureClip\logs`，类别 `UiTiming`，共三类：

| 行 | 含义 |
| --- | --- |
| `SettingsWindow constructed in N ms` | `InitializeComponent` 把 8 个页面一次性读进内存 + DI 解析的耗时 |
| `Settings window ready in N ms (cold/warm)` | 从开始建窗到 `Show()` 返回，`cold` 表示本次进程首次打开 |
| `Page 'x' ready in N ms` | 切到某个页面后，到调度器把布局排完为止的耗时（含首次实例化该页视觉树） |

## 已知结论

- 冷启动日志显示 `starting → started` 约 1.1 s，其中设置窗口构造约 0.5 s。
- 8 个 `TabItem` 的内容是内联 XAML，**全部在 `InitializeComponent` 时构造**，只有被选中的那个参与布局。
  因此“构造耗时”≈ 8 个页面的对象树总和，而“切页耗时”主要是把该页塞进视觉树后的首次测量。
- 切页时不再有任何数据重载：`MainSettingsTabControl_SelectionChanged` 只同步左侧导航选中态。

## 下一步的优化前提

要动“构造 0.5 s”只有一条路：把页面内容从内联 XAML 拆成按需创建的独立控件，
否则 `InitializeComponent` 必然构造全部页面。拆分会牵动大量 `x:Name` 引用，属于结构性改造，
先用上面的数字确认收益再决定是否值得。
