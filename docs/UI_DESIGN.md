# UI 与键盘交互

2026-11-16：延续 GestureClip 蓝色强调与轻玻璃表面，统一浅色和深色主题。

- 表面渐变使用 `Colors.xaml` / `Colors.Dark.xaml` 的语义颜色；侧栏、卡片、内容区随主题切换。
- 常规、次要和提示文字在设置页表面保持至少 4.5:1 对比度。自定义强调色的按钮填充独立推导，避免白字落在过亮底色上。
- `Interaction.xaml` 提供共享键盘焦点环和主题化工具提示；焦点反馈不改变布局。
- `SettingsNavigation.xaml` 管理导航图标、选中指示和搜索结果样式。
- `SettingsCards.xaml` 管理首页功能按钮和更新说明折叠区域。
- `ToggleSwitch.xaml` 与 `ToggleSwitchMotion` 管理开关的悬停、按下、焦点和禁用状态；160ms 动画仅移动滑块，遵循 Windows 客户区动画偏好。

## 设置搜索

`Ctrl+F` 聚焦并选中搜索文字；输入后 `↓` / `↑` 进入结果列表，方向键继续选择，`Enter` 打开目标设置。首项按 `↑` 返回搜索框；结果列表按 `Esc` 收起并返回搜索框。

首页功能卡支持 Tab 聚焦、Enter 或空格激活。更新说明默认折叠，可用键盘展开。

## 剪贴板与快捷动作

剪贴板工具栏使用统一系统图标并提供可访问名称；常显状态同时通过颜色、工具提示和辅助技术状态表达。

快捷动作切换选项会清空旧预览；执行期间暂停换选，结果标明所属动作，避免旧结果显示在新动作下。

## 验证

主题可读性、颜色资源引用和自定义强调色由 `ThemeReadabilityTests`、`AccentButtonContrastTests` 验证。快捷动作异步选择与预览由 `QuickActionCenterViewModelTests` 验证。

实际界面验收使用 `.codex-tasks/20261116-ui-polish/preview/` 中的开发工具，加载真实视图、资源和 ViewModel，使用独立临时数据库，不启动全局捕获服务。
