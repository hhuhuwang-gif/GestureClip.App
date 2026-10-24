# 设置页滚动诊断

设置页四个常用界面（首页 / 剪贴板 / 手势 / 动作绑定）使用 `SmoothScrollViewer`。
滚轮回弹已在 `Controls/SmoothScrollViewer.cs` 中修复：滚轮输入始终累积到一个目标偏移，
每帧单调逼近且不会越过目标，反向滚动保留已累积的行程，外部滚动（滚动条拖动、键盘、
`BringIntoView`、代码调用）会立即接管目标而不是被动画拉回。

## 前提：窗口必须是非分层窗口

设置窗口使用 `WindowStyle=None` + `AllowsTransparency=False` + `WindowChrome`。
`AllowsTransparency=True` 会把窗口变成分层窗口，WPF 对分层窗口强制软件渲染，
整窗每帧由 CPU 重绘，滚动会掉到 ~50fps 并表现为一卡一卡。`ThemeResourceTests`
里有断言锁住这一点。

## 开启追踪

正常启动不记录滚动追踪。需要复核手感时，先从托盘退出程序，然后运行：

```powershell
.\Start-GestureClip.bat --trace-scroll
```

在页面上下滚动。本地日志目录 `%LOCALAPPDATA%\GestureClip\logs` 会出现 `ScrollTrace`：

- `render tier=…`：启动时输出一次，`tier=2` 表示完整硬件加速，`layered=False` 表示不是分层窗口。
- `wheel`：收到的滚轮方向、当前位置与累积目标。
- `motion-start`：开始一次持续渲染过程。连续滚轮只改变目标，不重新启动过程。
- `complete`：一次滚动的汇总，`frames` 帧数、`avg` 平均帧间隔、`max` 最大帧间隔、
  `layout` 单帧布局提交的最大耗时。
- `slow-frame`：帧间隔超过 30ms 的帧，带帧序号与当时的视觉位置，用来判断卡在起步、中段还是收敛处。
- `takeover`：滚动位置被非本控件的原因改变（滚动条、键盘、代码定位）时的位置。
- `bring`：请求定位的控件类型和名称，不包含文本内容。

## 看什么

- `avg` 接近 16.7ms（60Hz 屏）说明动画跟上了刷新率；长期 20ms 以上说明渲染成本过高。
- `layout` 长期接近 0 说明 UI 线程不忙，瓶颈在渲染而不是布局。
- `slow-frame` 集中在起步 → 首帧提交太晚；集中在中段 → 视口内容重绘成本高。
- `wheel target=` 在反向滚动时应以上一次 `target` 为基准加减，而不是退回当前 `actual`。
- 到达边界后继续滚动，`target` 保持不变且不再提交无效帧。

## 逐帧日志

每帧一条 `frame desired=` 默认关闭（`FrameLogging = false`）：同步写盘会拖慢 UI 线程，
把测量结果本身搞坏。只有单元测试（`SmoothScrollViewerTests`）会临时打开它来验证单调性。

追踪只用于诊断，不改变滚动行为。记录保存在本机。退出程序后不带此参数重新启动，即关闭追踪。
