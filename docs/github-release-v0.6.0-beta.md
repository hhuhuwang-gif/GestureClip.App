# GestureClip v0.6.0 Beta - 工位小熊 Hub 版

![工位小熊 Hub](https://github.com/hhuhuwang-gif/GestureClip.App/releases/download/v0.6.0-beta/workbear-hub-v0.6.0-beta.png)

## 这次重点

- 工位小熊升级为真正的办公状态 Hub：实时状态、今日收益、摸鱼、休息提醒、下班冲刺、今日报告。
- HUD 更人性化：复制/粘贴/手势后显示工位短文案，颜色跟随工作阶段，高频复制/粘贴自动合并成“复制 +5”。
- 新增今日牛马生存报告：下班后每天最多自动弹出一次，也可在 Hub 或托盘手动打开。
- 新增 PNG 分享卡：默认生成到桌面 `WorkBear-Report-YYYYMMDD.png`，不包含剪贴板内容。
- 托盘菜单增加“工位小熊”子菜单，一级菜单保持简洁。
- 剪贴板历史新增“常显”按钮：默认点击别处自动关闭，只有选择常显才置顶常驻。
- 数据库 migration 增强：工位小熊 Hub 新字段可重复检查、失败后可恢复。
- 设置页同步新增自动报告、摸鱼、下班冲刺、分享卡、HUD 状态文案、HUD 阶段变色等开关。

![剪贴板常显](https://github.com/hhuhuwang-gif/GestureClip.App/releases/download/v0.6.0-beta/clipboard-always-visible-v0.6.0-beta.png)

## 下载文件

- `GestureClip-v0.6.0-beta-win-x64.zip`：完整包，解压后运行 `GestureClip.exe`。
- `GestureClip-v0.6.0-beta-update-win-x64.zip`：老用户覆盖更新包。
- `SHA256SUMS.txt`：压缩包 SHA256 校验值。
- `workbear-hub-v0.6.0-beta.png`：工位小熊 Hub 展示图。
- `clipboard-always-visible-v0.6.0-beta.png`：剪贴板常显行为展示图。

## 覆盖更新方法

1. 右键托盘图标，选择“退出 GestureClip”。
2. 打开任务管理器，确认没有 `GestureClip.exe`。
3. 解压 update 压缩包。
4. 将新版文件覆盖到旧版程序目录。
5. 双击 `GestureClip.exe` 启动新版。

## 数据与隐私

覆盖更新不会删除用户数据。默认数据目录：

```text
%LOCALAPPDATA%\GestureClip\
```

工位小熊 Hub 只保存本地统计：工资估算、复制/粘贴/手势次数、少点次数、手动摸鱼时长、休息提醒次数、报告摘要和设置。

不会上传数据，不会自动分享，不会把剪贴板正文、图片历史原始内容、浏览器内容、网址、密码、Token、Cookie 写入报告或分享卡。

## 已知问题

- 当前 Beta 包未签名，Windows SmartScreen 可能提示未知发布者。
- 管理员权限窗口中，普通权限运行的 GestureClip 可能无法稳定执行输入模拟。
- 图片历史数量很大时，首次缩略图加载仍可能需要一点时间。
- 覆盖更新前必须先退出程序，否则 `GestureClip.exe` 可能被占用。
