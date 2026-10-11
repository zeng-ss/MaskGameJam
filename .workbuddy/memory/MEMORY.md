# MEMORY.md - Mask_GameJamAndroid 项目长期笔记

## 编辑器版本
- 项目已由团结引擎 `2022.3.62f3c1` 迁移至 Unity 6 `6000.5.9f1`（路径 `D:/APP/Unity Hub/6000.5.9f1`）。
- 迁移首次启动会报 `projectVersion '2022.3.62f3c1' is not a valid Unity version`（UPM 不认 c1 后缀），ProjectVersion.txt 被改成 6000.5.9f1 后即自愈，无需处理。
- Git LFS 必须pull：缺少真实内容时 Unity 会报 "File may be corrupted or was serialized with a newer version of Unity"，实际是 134 字节 LFS 指针文件。

## 热更新方案：xLua（Lua 热更）
- **已放弃 HybridCLR，改为 xLua v2.1.15 做 Lua 热更。**（2026-10-11 用户确认）
- HybridCLR 包 `Packages/com.code-philosophy.hybridclr` 已从工作区删除（git HEAD 里仍有 292 个文件，可 `git checkout` 恢复）；`ProjectSettings/HybridCLRSettings.asset`、两个 HybridCLR csproj 一并删除。
- **架构决策**：`Assets/Scripts/HotUpdate` 里 34 个 C# 游戏逻辑文件**保留不动**（照常编进包），Lua 热更管线作为新机制叠加，后续按模块逐步往 Lua 迁。
- xLua 目录已从 `Assets/Lua/Assets/XLua` 整理为 `Assets/XLua`（Src / Gen / Resources / Doc），原生库移到 `Assets/Plugins/XLua/`。
- 新增程序集划分：`Assets/XLua/XLua.asmdef`（XLua）、`Assets/XLua/Src/Editor/XLua.Editor.asmdef`（XLua.Editor）；`AOT.asmdef` 的 references 里加了 `"XLua"`。asmdef 引用别的 asmdef 直接写程序集名即可。

### Lua 热更管线约定
- 脚本源目录：`Assets/LuaScripts/`，**后缀必须是 `.lua.txt`**（否则 Unity 不识别为 TextAsset）。
- 地址规则：`Assets/LuaScripts/main.lua.txt` → Addressables 地址 `lua/main.lua`；`demo/greet.lua.txt` → `lua/demo/greet.lua`。前缀常量 `XLuaManager.LuaAddressPrefix` 与 `Build.LuaAddressPrefix` 必须一致。
- 运行时：`HotUpdateSystem.LoadLuaScripts()` → 用 label `Lua` 整组下载 → 创建 `XLuaManager` → `InitLuaEnv()` → `StartLua()`（即 `require 'main'`）。
- 打包菜单：`Build/PrepareLuaAssets`（注册脚本到 Addressables "Lua" 分组并打 label）、`Build/ListLuaAddresses`（核对地址）。`NewClient`/`UpdateClient` 会自动先调用它。
- xLua 未生成过任何类型 wrap（Gen 目录只有基础结构），Lua 调用 C# 走反射。要性能需配置 `Assets/XLua/Src/Editor/ExampleConfig.cs` 的 `[LuaCallCSharp]` 并执行 XLua > Generate Code。
- 仍待处理：`HybridCLRData/` 约 2.4 GB 构建产物可删；`Assets/DllBytes` 与 `DllConfig.aot/hotUpdate` 是旧 dll 热更残留，随迁移可清理。

## Unity 6 API 变更（迁移必踩）
- `UnityEngine.Object.GetInstanceID()` 已废弃且为 **error 级别**（CS0619，不是 warning，无法用 pragma 或 nowarn 关闭），必须改为 `GetEntityId()`。
- `GetEntityId()` 返回 `UnityEngine.EntityId`（struct），有 `Equals`/`GetHashCode`/`CompareTo`/`GetRawData()`/`ToULong`/`FromULong`，支持从 `int` 隐式转 `EntityId`，可直接作 Dictionary key；**没有** `EntityId → int` 的隐式转换，所以原来是 `int` 的字典 key 要整体改成 `EntityId`。
- 老版本包（`com.unity.addressables` 1.22.x、`com.unity.visualscripting` 1.9.4）在 Unity 6 下会因该 API 编译失败，需要升级或直接移除。
- 本项目未使用 VisualScripting（0 处引用），可安全移除依赖。

## 排查技巧
- Unity 编辑日志在 `Logs/Editor.log`，Asset 导入 worker 日志在 `Logs/AssetImportWorkerHW*.log`，包管理日志在 `Logs/upm.log`。
- asmdef 用 GUID 引用其他程序集，失效引用会导致整个程序集无法编译；可通过搜索 `guid: xxxxx` 是否命中任意 `.meta` 来判定。
