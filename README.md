# 不问凡尘 · 凡尘随心 / WorldApart FanchenTrainer

**作者 / Author：HB叔叔** · Windows x64 · 游戏内 **F8** 面板 · 中文图形安装器

为《不问凡尘》（WorldApart）提供主角属性、道途点、灵根属性点、物品添加和人物好感调整。按 F8 打开／关闭面板，Esc 关闭。

An in-game trainer for **WorldApart / 不问凡尘**, with player stats, path points, spirit-root points, inventory items and NPC affinity. Press **F8** to toggle the panel, or **Esc** to close it. Includes a Chinese GUI installer with automatic Steam-directory detection, stage progress, logs and installation rollback.

## 下载 / Downloads

从 [Releases](https://github.com/uncle-b1g/buwenfanchen-trainer/releases/latest) 下载发行 ZIP；不要把 GitHub 自动生成的 Source code ZIP 当作安装包。

| 版本 / Edition | 文件 / File | 依赖 / Dependencies |
| --- | --- | --- |
| 瘦身离线版 / Slim offline | `FanchenTrainer-0.1.5-offline-slim-ui.zip` | 已携带固定依赖，安装时禁止下载 / Bundled, verified dependencies; no downloads |
| 联网下载版 / Online | `FanchenTrainer-0.1.5-online-ui.zip` | 首次从固定官方地址下载并校验 / Downloads pinned upstream dependencies on first install |

两个版本均包含完整源码、构建材料、许可证及中文安装窗口。插件功能相同。下载后可用随附 `.sha256` 文件校验 ZIP。

Both editions contain source code, build materials, license notices and the same GUI installer. Trainer features are identical. SHA-256 checksum files accompany the assets.

## 安装 / Install

1. 正常退出游戏，完整解压发行包到普通可写目录。不要直接在 ZIP 预览或 Steam 工坊订阅目录中运行。
2. 双击 **`FanchenInstaller.exe`**。自动识别失败时点击“选择游戏…”选中 `WorldApart.exe`。
3. 点击 **“安装 / 更新”**，等待下载／校验、引用生成和安装完成。
4. 从 Steam 启动游戏，进入存档后点按 **F8**。

Close the game, extract the complete release package to an ordinary writable folder, and run **FanchenInstaller.exe**. Select **WorldApart.exe** if automatic detection fails, then click **安装 / 更新 (Install / Update)**. When finished, launch the game through Steam, load a save and press F8. Use **恢复上次安装 (Restore previous installation)** to roll back installed files.

**创意工坊订阅仅下载文件，仍需手动运行安装器。** 本工具使用 BepInEx IL2CPP 和 Doorstop 兼容加载器。窗口使用 Windows 自带的 .NET Framework 4.5+；游戏引用生成使用私有运行时，无需给系统安装 SDK 或 .NET。

**Steam Workshop subscription only downloads the package; manual installation is required.** The trainer uses BepInEx IL2CPP and a Doorstop compatibility loader. The GUI uses Windows' built-in .NET Framework 4.5+; interface generation uses a private runtime and does not install a system-wide SDK or .NET runtime.

## 功能 / Features

- 主角属性、战斗和交互数值 / Player combat and interaction stats
- 增加道途点、灵根属性点 / Add unallocated path and spirit-root points
- 搜索并添加物品 / Search and add inventory items
- 搜索人物并调整好感 / Search NPCs and adjust affinity
- 确认修改、回读数值、首次修改前备份磁盘存档 / Confirmation, value read-back and on-disk save backup before the first edit
- 安装文件备份、日志及按游戏目录恢复 / Installation-file backups, logs and per-directory restoration

安装恢复不会撤销已经写入存档的数值修改；首次修改前的存档备份保留供用户自行恢复。

Restoring installation files does not undo edits already saved in the game. Keep pre-edit save backups if you need to restore gameplay values.

## 兼容性 / Compatibility

| 项目 / Item | 支持 / Supported |
| --- | --- |
| OS | Windows x64 |
| Steam App | 4209920 |
| Game Build | 25617557 |
| Unity | 2022.3.43f1 |
| IL2CPP metadata | 31 |

安装器会拒绝不匹配的游戏版本。游戏更新后需重新适配。原有功能已由作者设备上的使用者实测；0.1.5 新增灵根点的分配与保存重载仍待实机反馈。

The installer rejects unsupported game builds. Game updates may require adaptation. Existing features have been tested by the local player; the new 0.1.5 spirit-point allocation and save/reload flow still awaits gameplay feedback.

## 源码与构建 / Source and build

完整可重建工程位于 Release 的 **FanchenTrainer-0.1.5-source.zip**；解压后包含以下目录。主要插件源码也可直接在仓库的 src/FanchenTrainer/ 浏览。

The complete rebuildable project is included in **FanchenTrainer-0.1.5-source.zip** under Releases. The following paths refer to that extracted project. Main plugin sources are also browsable in src/FanchenTrainer/.

- `src/FanchenTrainer/`：插件 / Plugin
- `tools/InstallerUI/`：中文安装窗口 / GUI installer
- `tools/InteropGen/`：本机引用生成工具 / Local interface generator
- `release/`：安装与恢复脚本、文档及许可证 / Installation scripts, documentation and license notices
- `native/UnityDoorstop/`：兼容加载器源码归档 / Compatibility-loader source archives
- [BUILD.md](BUILD.md)：构建说明 / Build instructions

仓库及发行包不包含原游戏 DLL、metadata、已生成的游戏 interop、个人存档或凭据。引用从使用者自己的游戏安装生成。

The repository and release packages do not include game DLLs, metadata, generated game interop, personal saves or credentials. References are generated locally from the user's own installation.

## 许可证 / License

原创托管插件、引用生成工具、安装界面、脚本和文档采用 **MIT**。UnityDoorstop 兼容加载器及修改采用 **LGPL-2.1**；BepInEx 使用 LGPL-2.1，Il2CppInterop 使用 LGPL-3.0。第三方许可证与原作者版权声明均保留，详见 [LICENSE](LICENSE)、[NOTICE.md](NOTICE.md) 和 `release/licenses/`。

Original managed plugin code, generator, GUI, scripts and documentation are **MIT** licensed. The UnityDoorstop loader and its modifications are **LGPL-2.1**. BepInEx is LGPL-2.1 and Il2CppInterop is LGPL-3.0. All upstream license texts and original attributions are retained; see LICENSE, NOTICE.md and release/licenses.