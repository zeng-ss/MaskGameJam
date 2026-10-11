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
- （早期曾加过 `XLua.asmdef` / `XLua.Editor.asmdef`，后全部删除，见下方「程序集结构的最终决策」。）

### ⚠️ xLua 生成代码的程序集约束（血泪坑，别再踩）
1. **生成代码必须和 xLua 源码在同一个程序集**。xLua 生成出来的 `WrapPusher.cs` 里有
   `public partial class ObjectTranslator` / `InternalGlobals` / `StaticLuaCallbacks`，
   **partial 不能跨程序集**。曾经把 Gen 拆成独立的 `XLua.Gen.asmdef`，结果
   `Push` / `GetObject` / `FastGetCSObj` / `Assignable` / `getTypeId` / `objectCasters` 全部报
   "does not contain a definition"（上万个 CS1061/CS0029）。
2. **ExampleConfig 只能列 UnityEngine / TMPro / System 的类型**。列项目类型（BasePanel、UIManager...）
   会让生成代码引用 AOT/HotUpdate，而 AOT 又引用 XLua（XLuaManager 用了 LuaEnv），
   形成 `XLua -> AOT -> XLua` 循环依赖，Unity 直接拒绝编译。项目类型让 xLua 走反射调用即可。
3. 同理 `CSharpCallLua` 只能用 UnityEngine/System 的委托，所以 UIManager 的回调签名是
   `Action<MonoBehaviour>` 而不是 `Action<BasePanel>`。
4. `XLua.asmdef` 和 `XLua.Editor.asmdef` 都要显式加 `"Unity.TextMeshPro"`，否则 TMPro 类型解析不到。
5. `Assets/XLua/Gen/` 里如果残留**别的 xLua 版本**生成的代码，会因 API 不匹配报一堆错
   （Gen_Flag / RegisterPushAndGetAndUpdate / DelegateBridgeBase 签名）。换版本时整个 Gen 目录清空重生成。

### 程序集结构的最终决策：不用 asmdef（对齐 LuaDemo）
参照 `D:\Unity\LuaDemo`（同一个 Unity 6000.5.9f1，xLua 生成正常）确认：**它一个 asmdef 都没有**，
xLua 源码、生成代码、项目代码全部在 Assembly-CSharp，所以生成代码能直接引用项目类型，
partial / internal 都不成问题。

本项目已删除 `AOT.asmdef`、`HotUpdate.asmdef`、`PriorityHotUpdate.asmdef`、
`XLua.asmdef`、`XLua.Editor.asmdef`。理由：
- AOT / HotUpdate / PriorityHotUpdate 的划分本来是给 HybridCLR 做 dll 热更的，换 Lua 后毫无意义
- 保留它们会逼着 xLua 独立成程序集，进而和 AOT 形成循环依赖（XLuaManager 要用 LuaEnv）
保留的 asmdef：第三方插件（NSJ.AutoPool、DOTweenRun、Joystick、Unity.UOS.Encrypt）
以及 `ProjectEditor.asmdef`（**必须保留**——目录名不是 "Editor"，没 asmdef 的话 Build.cs
会被编进运行时程序集，而它用了 UnityEditor）。

### 项目里的 EventInfo 已改名
`Assets/Scripts/AOT/Manager/EventCenter.cs` 里的 `EventInfo` / `EventInfo<T>` / `EventInfo<T,K>`
原来在全局命名空间，会遮蔽 `System.Reflection.EventInfo`，导致 xLua 的 CodeEmit.cs / Generator.cs
报一堆 CS1061。已全部改名为 `GameEventInfo`（只在 EventCenter.cs 内部使用，零风险）。

### xLua 2.1.15 在 Unity 6 下的生成缺陷与正确解法
**正确解法是配置层面的 `[BlackList] MethodFilter`，不是改 xLua 源码。**（LuaDemo 就是这么做的，见
`Assets/XLua/Src/Editor/ExampleConfig.cs`，该文件顶部注释写清了所有坑）

xLua 2.1.15 是 2020 年的版本，生成器不认识 Unity 6 的新 API，Generate Code 后必炸：
1. **`in` 参数被生成成 `ref`** —— 模板 `Src/Editor/Template/LuaClassWrap.tpl.txt`（及 GCM / LuaDelegateWrap）
   里对 `IsByRef 且非 out` 的参数一律输出 `ref`，而 Unity 6 大量 API 用 `in`（如 `Vector2.SignedAngle(in, in)`）。
   C# 9 下 `ref` 传 `in` 参数报 CS1615（要 C# 12 才行）。
   已改模板：`IsIn && IsByRef` 时不输出任何修饰符（C# 允许直接传值给 in 参数）。
   另加 `Assets/XLua/csc.rsp`（`-langversion:12.0`）作第二道保险。
2. **`Span<T>` 参数的重载会被生成 wrap** —— `Span<T>` 是 ref struct，不能作泛型类型参数，报 CS0306。

解法（已落地到 ExampleConfig.cs，照抄即可）：
```csharp
[BlackList]
public static Func<MemberInfo, bool> MethodFilter = (memberInfo) =>
{
    if (!(memberInfo is MethodInfo method)) return false;
    if (IsRefStruct(method.ReturnType)) return true;
    foreach (ParameterInfo p in method.GetParameters())
    {
        if (IsRefStruct(p.ParameterType)) return true;
        if (p.IsIn && p.ParameterType.IsByRef) return true;   // in 参数 -> CS1615
    }
    return false;
};
```
3. **别把 `UnityEngine.Object` 加进 LuaCallCSharp** —— 会生成空的 wrap 文件，导致
   `CS0103: The name 'UnityEngineObjectWrap' does not exist`。
4. **类型依赖要连带列出**：A 类型的成员用到 B 类型时，B 也要在 LuaCallCSharp 里，否则会报
   `CS1061: 'ObjectTranslator' does not contain a definition for 'PushXXX'`（B 的 Push 方法没生成）。
   典型：`EventTrigger.Entry.eventID` 是 `EventTriggerType`，两个必须一起列；
   EventTrigger / Entry / EventTriggerType / TriggerEvent 这四个是一组。
5. **【最容易漏、最坑】编辑器里反射看得见、但打包(player)编译里不存在的成员，必须在 BlackList 里挡掉。**
   现象：编辑器编译完全正常，一执行 **Addressables 构建 / Player 构建** 就在 `CompilePlayerScripts`
   阶段报 `CS1061: 'Xxx' does not contain a definition for 'Yyy'`，构建直接中断
   （错误堆栈里能看到 `BuildPlayerScripts -> CompilePlayerScripts -> Failed to compile player scripts`）。
   原因：`#if UNITY_EDITOR` 的成员在编辑器程序集里存在，player 编译时被剥掉。
   本项目已知清单（**统一写进 `ExampleConfig.cs` 的 `MethodFilter` → `EditorOnlyMembers` 集合**）：
   - `UnityEngine.MonoBehaviour.runInEditMode`
   - `UnityEngine.UI.Text.OnRebuildRequested` / `UnityEngine.UI.Graphic.OnRebuildRequested`
   - `UnityEngine.Material` 的材质变体一族：`parent` / `isVariant` / `SetPropertyLock` /
     `ApplyPropertyOverride` / `RevertPropertyOverride` / `RevertAllPropertyOverrides` /
     `IsPropertyLocked` / `IsPropertyLockedByAncestor` / `IsPropertyOverriden` / `IsChildOf`

   🔴 **两种黑名单的判断规则不同，这是本项目连踩两次的坑（2026-10-11 二次复发）**：
   - `isMemberInBlackList`（**属性 / 字段**）：`DeclaringType == exclude[0] && Name == exclude[1]`，**不看参数**，
     所以 `List<List<string>>` 里的 2 元素条目对属性有效。
   - `isMethodInBlackList`（**方法**）：额外要求 `exclude.Count - 2 == 方法的参数个数`！
     ```csharp
     if (parameters.Length != exclude.Count - 2) continue;   // 2 元素条目 → 只匹配「无参方法」
     ```
     → **凡是带参方法，写进 `List<List<string>>` 等于没写**（`SetPropertyLock(nameID, value)` 就这么漏的）。
     想屏蔽任意重载，必须写进 `MethodFilter`（它对属性和方法都会调用，且不受参数签名限制）。
   - 口诀：**带参方法 → `MethodFilter`；属性/字段/无参方法 → 两种都行**。
   - 顺带：`List<List<string>>` 里若非要写带参方法，必须把每个重载的**完整参数类型**都列出来
     （如官方的 `{"System.IO.FileInfo","GetAccessControl","System.Security.AccessControl.AccessControlSections"}`）。

   ⚠️ **xLua 官方那 20 多条 `[BlackList]` 绝对不能删**（`runInEditMode` 就在里面）。
   曾经把官方 `ExampleConfig.cs` 整个覆盖成自定义的精简版，等于删掉了官方黑名单，
   于是编辑器编译看着没问题、Addressables 一构建就炸。
   以后凡是「编辑器编译过、构建报 CS1061」，就往这个 list 里加 `{成员声明类型全名, 成员名}`。
   匹配规则见 `Generator.cs`：`mb.DeclaringType.ToString() == exclude[0] && mb.Name == exclude[1]`，
   所以 `parent` 这种通用名必须带类型全名（`Transform.parent` 是要保留的）。

（注：早期曾直接改 xLua 源码/模板来处理上面 1、2——模板改 in 参数、Utils.cs 加 ref struct 检查。
这些补丁保留着当双保险，但真正起作用的是 BlackList 配置，日后升级 xLua 只需保留配置即可。）

### 迁移策略（2026-10-11 评估结论）
框架跑通后给出的建议，**不主张把 34 个 C# 文件全量翻译成 Lua**：
- 该迁：UI 面板逻辑（8 个）、流程/关卡/数值配置 —— 调整需求最多、非每帧执行，热更价值最高，约 1200 行。
- 不该迁：PlayerController、TopDownCameraFollow、EnemyController / EnemySpawner —— 每帧逻辑，
  跨语言调用有开销且敌人数量多时会放大，留在 C#，用 Lua 调参数/开关控制行为。
- 热更的是「内容和交互」，不是「每帧运算」。全迁约 4000 行且需完整回归测试，性价比低。
- 已知限制：壳里 `DefaultLuaModule` 是硬编码的，「哪个物体挂哪个 Lua 脚本」这件事热更不了，
  只能热更脚本内容；C# 字段名与 Lua 的 `self.cs.xxx` 是隐式契约，改名必须同步。

### Lua 热更管线约定
- 脚本源目录：`Assets/LuaScripts/`，**后缀必须是 `.lua.txt`**（否则 Unity 不识别为 TextAsset）。
- **地址规则：Addressables 地址 = 纯文件名**（2026-10-11 改，原为 `lua/ + 相对路径`）：
  `main.lua.txt` → `main.lua`；`demo/greet.lua.txt` → `greet.lua`；`ui/start_panel.lua.txt` → `start_panel.lua`。
  好处：Lua 里 **`require 'greet'` 直接对应地址**，不用管目录层级。代价：不同目录下**不能有同名文件**。
  `Build.PrepareLuaAssets` 用 `Path.GetFileName(path)` 生成地址；`XLuaManager.AddressablesLuaLoader` 取
  `filepath` 最后一段拼 `.lua`（所以旧的 `require 'demo.greet'` 写法仍能命中 `greet.lua`）。
  原 `XLuaManager.LuaAddressPrefix` / `Build.LuaAddressPrefix` 常量已删除。
  `Build/ListLuaAddresses` 改用 label 判断（地址已无统一前缀）。
- 运行时：`HotUpdateSystem.LoadLuaScripts()` → 用 label `Lua` 整组下载 → 创建 `XLuaManager` → `InitLuaEnv()` → `StartLua()`（即 `require 'main'`）。
- ⚠️ **`Addressables.DownloadDependenciesAsync(key, autoReleaseHandle)` 第二个参数必须传 `false`。**
  传 `true` 时句柄会在操作完成后被自动释放，紧接着访问 `.Status` 会抛
  `Exception: Attempting to use an invalid operation handle`（`AsyncOperationHandle.get_InternalOp` → 第 466 行）。
  编辑器 Play / 本地模式（Use Asset Database）下该操作同步完成，等于「刚拿到句柄就被释放」，**必现**。
  正确写法：传 `false` → `yield return handle` → 用 `handle.IsValid()` 兜底 → 用完 `Addressables.Release(handle)`。
- 打包菜单：`Build/PrepareLuaAssets`（注册脚本到 Addressables "Lua" 分组并打 label）、`Build/ListLuaAddresses`（核对地址）。`NewClient`/`UpdateClient` 会自动先调用它。
- xLua 已生成约 40 个类型 wrap（`Assets/XLua/Gen/`）。2026-10-11 因黑名单尚未生效，
  临时摘掉了 3 个「编辑器专用成员」导致 player 编译失败的 wrap：
  `UnityEngine_MaterialWrap` / `UnityEngine_MonoBehaviourWrap` / `UnityEngine_UI_TextWrap`
  （同时注释掉 `XLuaGenAutoRegister.cs` 里对应的 3 行注册）。这三个类型暂时走反射调用。
  重新执行 XLua > Generate Code 会按新配置生成干净版本、自动恢复。
- 仍待处理：`HybridCLRData/` 约 2.4 GB 构建产物可删；`Assets/DllBytes` 与 `DllConfig.aot/hotUpdate` 是旧 dll 热更残留，随迁移可清理。

## Unity 6 API 变更（迁移必踩）
- `UnityEngine.Object.GetInstanceID()` 已废弃且为 **error 级别**（CS0619，不是 warning，无法用 pragma 或 nowarn 关闭），必须改为 `GetEntityId()`。
- `GetEntityId()` 返回 `UnityEngine.EntityId`（struct），有 `Equals`/`GetHashCode`/`CompareTo`/`GetRawData()`/`ToULong`/`FromULong`，支持从 `int` 隐式转 `EntityId`，可直接作 Dictionary key；**没有** `EntityId → int` 的隐式转换，所以原来是 `int` 的字典 key 要整体改成 `EntityId`。
- 老版本包（`com.unity.addressables` 1.22.x、`com.unity.visualscripting` 1.9.4）在 Unity 6 下会因该 API 编译失败，需要升级或直接移除。
- 本项目未使用 VisualScripting（0 处引用），可安全移除依赖。

## 排查技巧
- Unity 编辑日志在 `Logs/Editor.log`，Asset 导入 worker 日志在 `Logs/AssetImportWorkerHW*.log`，包管理日志在 `Logs/upm.log`。
- asmdef 用 GUID 引用其他程序集，失效引用会导致整个程序集无法编译；可通过搜索 `guid: xxxxx` 是否命中任意 `.meta` 来判定。
