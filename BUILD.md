# 构建说明 / Build guide

作者 / Author: HB叔叔

## 准备 / Prerequisites

- Windows x64；开发构建使用 .NET SDK 8.0.422（或兼容的 .NET 8 SDK）。
- 重建安装窗口使用 Windows 自带的 .NET Framework C# 编译器。
- 修改器的引用必须从自己合法安装的《不问凡尘》Build 25617557 生成，禁止在仓库或发行包中提交原游戏 DLL、metadata 或生成的 interop。
- Native 加载器重建需要 Python 3 与 Windows x64 MinGW GCC。

Build the managed code with .NET SDK 8.0.422 (or a compatible .NET 8 SDK). The GUI uses the Windows .NET Framework compiler. Generate game references from your own WorldApart Build 25617557 installation. Native loader rebuilds require Python 3 and MinGW x64 GCC. Do not commit game binaries, metadata or generated interop.

## 游戏引用 / Game references

完整解压任一发行包，在该包中运行：

```powershell
.\Install.ps1 -GameDirectory 'D:\SteamLibrary\steamapps\common\A1' -PrepareOnly
```

将输出提示的 generation 目录中的 `interop/*.dll` 复制到工程 `refs/interop-build25617557/`。这一步不安装到游戏目录。

Run the release package's installer with `-PrepareOnly`; copy its generated interop DLLs to `refs/interop-build25617557/` for local compilation only.

## 固定依赖 / Pinned dependencies

从瘦身离线发行包取得完整的 `dependencies/`、`source/Dependencies/` 与 `source/UnityDoorstop/`。校验固定哈希后，在工程中准备：

- `vendor/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788.zip`，完整展开到 `vendor/bepinex788/`。
- `vendor/unity-2022.3.43.zip`。
- `artifacts/release-dependency-audit/Il2CppDumper-net6-win-v6.7.46.zip`。
- BepInEx 与 Il2CppInterop 对应源码 ZIP 放到 `vendor/offline-sources/`（文件名和哈希见 `release/offline/THIRD-PARTY-SOURCES.json`）。
- 官方完整 .NET 6.0.7 runtime ZIP 放到 `vendor/offline-sources/dotnet-runtime-6.0.7-win-x64.license-reference.zip`；固定 SHA512 与下载地址见 `vendor/offline-slim/dependencies.json` 的 `dotnet-host.sourceArchive`。
- 联网版打包不要求事先下载 .NET SDK ZIP；它按 `release/dependencies.json` 在用户安装时取得并验证。开发构建仍需本机 SDK。

Use the exact upstream archives and corresponding source snapshots recorded in the manifests. Extract BepInEx to vendor/bepinex788. The source and licenses for bundled LGPL components accompany the offline release. Runtime downloads and cache content remain outside version control.

## 构建 / Build

```powershell
# 修改器与输入、备份检查
.\tools\Build.ps1 -DotNet dotnet

# 图形安装器
.\tools\InstallerUI\Build-InstallerUI.ps1

# 瘦身离线包
.\tools\Package-Release.ps1 -DotNet dotnet -InstallerUI

# 联网下载包
.\tools\Package-Release.ps1 -DotNet dotnet -InstallerUI -Online
```

打包脚本严格检查已验证的插件和加载器哈希。自行修改后，需要先审查变更、验证实际二进制与安装恢复，再更新相应发布校验值；不要直接删除检查。

Packaging verifies the validated plugin and loader hashes. If you modify the code, review and test the changed binaries before updating release hash guards.

## Native 兼容加载器 / Native loader

将 `native/UnityDoorstop/doorstop-source.zip` 完整解压到 `vendor/doorstop-source/`，使源码位于 `vendor/doorstop-source/UnityDoorstop-master/`。`modified-source.zip` 保存已发布加载器的实际修改后源码；辅助修改在 `tools/native/`，打包代理源文件为 `native/UnityDoorstop/proxy.c` 和 `proxy.def`。重建：

```powershell
python .\tools\build_doorstop.py
```

输出为 `artifacts/native-build/`。`tools/build_doorstop.py` 使用上游固定快照和已提交的兼容修改，保留用户显式的 Doorstop 禁用开关。源码及修改受 LGPL-2.1 约束，许可原文保存在 native/UnityDoorstop/LICENSE.UnityDoorstop。

## 验证 / Validation

```powershell
# 在工作区复制游戏文件做安装与恢复测试，不启动游戏
.\tests\Check-InstallerUI.ps1 -GameDirectory 'D:\SteamLibrary\steamapps\common\A1'
.\tests\Check-InstallerUI.ps1 -GameDirectory 'D:\SteamLibrary\steamapps\common\A1' -Online
```

联网测试复用已验证的大体积原始 ZIP，仅让 Unity 依赖从官方地址真实下载，以验证下载和哈希校验。实机 F8 操作与存档重载由使用者测试。