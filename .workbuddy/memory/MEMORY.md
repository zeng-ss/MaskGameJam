# MEMORY.md - Mask_GameJamAndroid 项目长期笔记

## 编辑器版本
- 项目已由团结引擎 `2022.3.62f3c1` 迁移至 Unity 6 `6000.5.9f1`（路径 `D:/APP/Unity Hub/6000.5.9f1`）。
- 迁移首次启动会报 `projectVersion '2022.3.62f3c1' is not a valid Unity version`（UPM 不认 c1 后缀），ProjectVersion.txt 被改成 6000.5.9f1 后即自愈，无需处理。
- Git LFS 必须pull：缺少真实内容时 Unity 会报 "File may be corrupted or was serialized with a newer version of Unity"，实际是 134 字节 LFS 指针文件。

## 热更新方案
- **已决定：放弃 HybridCLR，后续改用 Lua 热更。**（2026-10-11 用户确认）
- HybridCLR 包 `Packages/com.code-philosophy.hybridclr` 已从工作区彻底删除（git HEAD 里仍有 292 个文件，需要时可 `git checkout` 恢复）。
- 清理范围：`AOT.asmdef` / `ProjectEditor.asmdef` 中失效 GUID、`Build.cs`（删除 GenerateDllFiles 菜单）、`HotUpdateSystem.cs`（删除 LoadMetadataForAOTAssemblies）、`ProjectSettings/HybridCLRSettings.asset`、两个 HybridCLR csproj。
- `Assets/Scripts/AOT/HotUpdateSystem.cs` 中保留的 Addressables 资源下载流程（catalog 检查/更新/下载/断点续传）与 HybridCLR 无关，Lua 方案可继续复用。
- 待用户自行处理：`HybridCLRData/` 目录约 2.4 GB，属 HybridCLR 构建产物，可安全删除释放空间。

## Unity 6 API 变更（迁移必踩）
- `UnityEngine.Object.GetInstanceID()` 已废弃且为 **error 级别**（CS0619，不是 warning，无法用 pragma 或 nowarn 关闭），必须改为 `GetEntityId()`。
- `GetEntityId()` 返回 `UnityEngine.EntityId`（struct），有 `Equals`/`GetHashCode`/`CompareTo`/`GetRawData()`/`ToULong`/`FromULong`，支持从 `int` 隐式转 `EntityId`，可直接作 Dictionary key；**没有** `EntityId → int` 的隐式转换，所以原来是 `int` 的字典 key 要整体改成 `EntityId`。
- 老版本包（`com.unity.addressables` 1.22.x、`com.unity.visualscripting` 1.9.4）在 Unity 6 下会因该 API 编译失败，需要升级或直接移除。
- 本项目未使用 VisualScripting（0 处引用），可安全移除依赖。

## 排查技巧
- Unity 编辑日志在 `Logs/Editor.log`，Asset 导入 worker 日志在 `Logs/AssetImportWorkerHW*.log`，包管理日志在 `Logs/upm.log`。
- asmdef 用 GUID 引用其他程序集，失效引用会导致整个程序集无法编译；可通过搜索 `guid: xxxxx` 是否命中任意 `.meta` 来判定。
