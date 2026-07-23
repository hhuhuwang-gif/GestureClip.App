# 代码签名（真签名）

GestureClip 发布流水线已支持 **Authenticode** 签名。没有证书时会跳过，构建不失败。

## 本地签名

1. 准备 `.pfx` 代码签名证书  
2. 安装 Windows SDK（含 `signtool.exe`）  
3. PowerShell：

```powershell
$env:GESTURECLIP_SIGN_PFX = "C:\path\to\cert.pfx"
$env:GESTURECLIP_SIGN_PASSWORD = "your-password"
.\scripts\publish-win-x64.ps1
.\scripts\build-setup.ps1 -SkipPublish
# 或单独：
.\scripts\sign-release.ps1 -Path .\artifacts\release\GestureClip
```

也可用证书指纹（已导入到本机证书存储）：

```powershell
$env:GESTURECLIP_SIGN_THUMBPRINT = "ABCDEF..."
.\scripts\sign-release.ps1 -Path .\artifacts\release\GestureClip
```

## GitHub Actions secrets

在仓库 **Settings → Secrets and variables → Actions** 添加：

| Secret | 说明 |
| --- | --- |
| `GESTURECLIP_SIGN_PFX_BASE64` | PFX 文件的 Base64（整文件） |
| `GESTURECLIP_SIGN_PASSWORD` | PFX 密码 |

生成 Base64（PowerShell）：

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\path\cert.pfx")) | Set-Clipboard
```

配置后，推送 `v*` 标签触发 `release.yml` 会在打包后自动签名，并构建 Setup zip。

## 注意

- 个人/开源常用 **EV 或标准代码签名证书**（DigiCert、Sectigo、SSL.com 等）  
- 未签名时 Windows SmartScreen 可能提示「未知发布者」——这是预期行为  
- **不要**把 PFX 提交进 git  
- 证书需要你自行购买/申请；仓库无法代替你生成受信任的发布者身份  

## 验证签名

```powershell
Get-AuthenticodeSignature .\artifacts\release\GestureClip\GestureClip.exe
# Status 应为 Valid
```
