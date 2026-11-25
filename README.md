# 我的游戏年鉴 · GameAlmanac

我的游戏年鉴是一个 Windows 本地游玩档案，用来整理游戏封面、评语、游玩年份和评分。同一款游戏可以记录多个年份，方便回看不同时间的游玩体验。

## 下载与运行

**当前版本** `v1.0`  
**运行环境** Windows 10 或 Windows 11，使用 .NET Framework 4.8 或更高版本。

发布后，可从仓库的 **Releases** 页面在 `v1.0` 下下载 `GameAlmanac.exe`，双击即可运行。同一页面的 `SHA256SUMS.txt` 提供下载文件的 SHA-256 校验值。也可以按下文从源码构建。

现有发布文件为未签名构建。

## 使用方法

1. 在「游玩档案」点击「添加游戏」，填写游戏名称、类型和首次游玩年份，可选择本地图片作为封面并填写评语。
2. 点击游戏封面打开评分详情。可以为每个年份独立评分，也可以沿用较早年份的评分。原评分更新后，沿用记录会随之更新。
3. 在「评分模板」为游戏类型关联模板，并设置六项一级指标的权重。修改权重后，关联的历年评分会按当前模板重新计算。

评分支持六项一级指标和十八项二级指标，可用滑条或数字框输入 0–10 分，精确到一位小数。清空数字框可恢复为未评分，设为「不适用」的指标会从计算中排除。侧边栏可以按游玩年份浏览，右键封面可以编辑或删除游戏，评分详情页也可以删除单个年份的记录。

## 数据存储

| 路径 | 内容 |
| --- | --- |
| `%LOCALAPPDATA%\GameAlmanac\Library.sqlite3` | 游戏资料、游玩记录、评分、类型和模板 |
| `%LOCALAPPDATA%\GameAlmanac\Covers\` | 导入的封面图片副本 |
| `%LOCALAPPDATA%\GameAlmanac.location` | 使用「存储位置」迁移后保存的数据路径 |

可以在应用里的「存储位置」将数据库和使用中的封面迁移到新文件夹。备份时，关闭应用后复制当前数据目录中的 `Library.sqlite3` 和 `Covers`。

## 从源码构建

在 Windows PowerShell 中进入仓库根目录并运行以下命令。命令中的执行策略只适用于本次启动的 PowerShell 进程。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

生成的 `build\GameAlmanac.exe` 可以直接运行。项目使用 C#、WPF 和 Windows 自带的 `winsqlite3.dll`，构建使用系统中的 .NET Framework C# 编译器与 WPF 程序集，无需下载 NuGet 包。应用代码和 WPF 主题位于 [src](src/)。
