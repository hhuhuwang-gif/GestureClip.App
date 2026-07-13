# UPDATE

GestureClip 使用 **便携 zip** 分发：解压即用，覆盖程序目录即可升级。

## 首次使用

1. 下载 `GestureClip-v*-win-x64.zip`
2. 解压到任意目录（例如桌面 `GestureClip`）
3. 双击 `GestureClip.exe`

self-contained，一般**不需要**单独安装 .NET。

## 覆盖更新

1. 托盘退出 GestureClip（任务管理器确认无 `GestureClip.exe`）
2. 下载新版 zip 并解压
3. **覆盖**旧程序目录（不要覆盖/删除用户数据目录）
4. 再启动 `GestureClip.exe`

应用内「检查更新 / 一键覆盖更新」会下载最新 portable zip，退出后用脚本覆盖当前程序目录并重启。

## 用户数据在哪里

```text
%LOCALAPPDATA%\GestureClip\
```

包括剪贴板历史、设置、手势、工位小熊等。  
**覆盖程序文件不会删除这里。**

## 更新失败

- 若提示连接 GitHub 失败：检查网络/代理，或浏览器打开 Release 手动下载 zip  
- 若提示 exe 被占用：先退出托盘，任务管理器结束残留进程后再覆盖  

## 不要做

- 不要删除 `%LOCALAPPDATA%\GestureClip\`，除非刻意清空全部数据  
- 不要把数据库拷进程序目录  
- 不要在程序运行时覆盖 exe  
