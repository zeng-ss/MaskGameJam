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

### 迁移策略（2026-10-11 最终决定：全量迁移，分三批）
用户明确要求「整体迁移」，并选择：枚举/纯数据类**全部搬进 Lua**、节奏**分三批推进**。
（早期我曾建议只迁 UI + 配置、每帧逻辑留 C#，用户决定全迁，按用户要求执行。）

**迁移边界 —— 服务/基础设施层必须留 C#**：
- AOT 全部（EventCenter / SceneMgr / ResMgr / PoolMgr / SceneObjectManager / 单例基类 /
  BasePanel / Lua 桥接 / HotUpdateSystem）
- HotUpdate 里的服务型单例：`UIManager`（泛型 `openPanel<T>` + Addressables 面板加载 + Canvas 管理）、
  `SoundAudioPool`（被 Addressables 实例化的常驻单例）
理由：被 C# 与 Lua 共同调用，重度依赖泛型/协程/Addressables，迁走收益低风险高。

**迁移模板**（每迁一个逻辑类 = 两件事）：
1. C# 壳：`Xxx : LuaPanel` / `Xxx : LuaBehaviour`，**只保留预制体上的序列化字段（名字不能改）**
   + `protected override string DefaultLuaModule => "xxx";` → **预制体零改动**
2. Lua 模块 `Assets/LuaScripts/<目录>/xxx.lua.txt`（地址 = 文件名），
   用 `behaviour.new(cs, M)` 导出工厂函数

**新增的基础设施（后续批次复用）**：
- `LuaBinding`：`show`/`hide` 转发、`GetLua/GetLuaByTag/GetLuaByName`、`CallLua(组件,"lua方法名",参数...)`
  （调用失败会打可读 warning，不再静默返回 null）、**`IsAlive(obj)`**（跨帧缓存场景对象的存活校验）、
  **`DisposeAll()`**（退出前统一释放，必须早于 `LuaEnv.Dispose()`）
- `LuaPanel`：override `Show()`/`Hide()`
- `LuaTween`（AOT/Lua）：**xLua 调不了 C# 扩展方法**，把 DOTween 的 DOLocalMove/DOFade 等包成静态方法
- `Assets/LuaScripts/framework/event.lua.txt`：Lua 事件中心（C# 的 EventCenter 全是泛型，Lua 调不了）
- `behaviour.lua` 的 LIFECYCLE 增加 `show` / `hide`

**跨批次过渡写法**：后续批次的类还没迁时，Lua 里先按 C# 方式访问
（`go:GetComponent(typeof(CS.PlayerStats))`、`CS.MaskSystemManager.Instance.xxx`），
等迁完再改成 Lua 形式。每个受影响文件顶部都写了「⚠️ 过渡说明」。

**已知限制**：壳里 `DefaultLuaModule` 是硬编码的，「哪个物体挂哪个 Lua 脚本」这件事热更不了，
只能热更脚本内容；**C# 字段名与 Lua 的 `self.cs.xxx` 是隐式契约，改名必须同步**。

**第 2 批（Controller 层 13 个）新增的迁移手法**（后续批次继续用）：
- **协程一律改成 update 状态机**（延时回收 / 循环间隔生成 / 等 1 秒再追击）。
  不引入 `xlua.util.cs_generator`，避免跨语言协程的额外不确定性。
  典型：`EnemySpawner` 的「每 0.2 秒生成一个」= `pendingSpawnCount` + 计时器 + `batchSpawnPos` 位置表。
- **Unity 消息由子类转发**。基类只转发生命周期；`OnTriggerEnter/Stay`、`IDragHandler.OnDrag`
  这类「只有部分组件需要」的消息，在壳里实现并 `_binding.InvokeLua("ontriggerenter", other)`。
  参数可以是 C# 对象（Collider / PointerEventData），xLua 会 push 成 userdata（未注册类型走反射也可用）。
  新增实例级 `LuaBinding.InvokeLua()`（带 `_luaFuncCache` 缓存，适合每帧/每次碰撞触发）。
- **动画事件 / 外部调用的 public 方法必须留在壳上**（方法名不能改），内部转给 Lua。
  例：`EnemyController.StartAttackHit/StopAttackHit`（动画事件）、`DecreaseHealth`（被 Bullet 调）。
- **集合访问一律摊平成下标方法**。`List<EnemyType>`、`List<GameObject> rains`、`List<string>` 这类
  在 Lua 侧不可靠，C# 壳提供 `GetEnemyTypeCount/GetEnemyPrefabName(i)/SetEnemyWeight(i,v)` 等。
  `GameManager.RuntimeEnemyConfigDict`（`Dictionary<string, EnemyConfigRuntime>`）同样摊平：
  `GetEnemyDamage/SetEnemyHealth/GetEnemyConfigNames/ApplyDifficultyUpgrade`。
- **`UnityEngine.Random` 的重载有歧义坑**：Lua 只有 number，`Random.Range(0, 1)` 会命中 int 重载恒定返回 0。
  统一走新的 `CS.LuaUtil`（`Value01/Range/RangeInt/OnUnitSphere/ParticleDuration/Distance`）。
- **枚举在 Lua 侧是「带缓存的 userdata」**（`ObjectTranslator.enumMap`），同一枚举值始终是同一对象，
  **可以安全用作 table key**（KeyBindingManager 的绑定表就是这么写的）。
  `enumMap` 分支见 `ObjectTranslator.cs:1252-1267`。
- **`const` 字段 Lua 取不到**（`Mathf.Deg2Rad`、`NavMesh.AllAreas`）→ 直接写字面量 / `math.rad`。
- **单例基类冲突**：`KeyBindingManager` 原继承 `UnitySingleTonMono<T>`，改 `LuaBehaviour` 后单继承被占，
  在壳里自己实现静态 `Instance`（跨场景保留 + 重复实例自毁），行为对齐原基类。
- **`behaviour.new()` 必须把模块的普通方法也挂到实例表上**（否则跨语言调用全部失效）
  - 症状：`LuaBinding.CallLua(comp, "start_countdown", 5)` 报
    「TimerPanel 的 Lua 模块里没有方法 start_countdown()」；
    连带 `EnemyBulletController:init` 调不到 → `self.bulletType` 为 nil →
    `PoolMgr.PushObj(nil, obj)` 抛 `Value cannot be null. Parameter name: key`（日志里刷屏）。
  - 原因：早期 `new()` 只返回一张「生命周期 handlers 表」，普通方法根本没暴露。
  - 现在的 `new()`：**返回的就是实例表 `o` 本身**（不能再另建转发表 —— 否则
    `self.xxx = ...` 写到不同表上，运行时状态会分裂），上面同时挂：
    a) 绑死 self 的生命周期无参闭包（C# 侧按 `Action` 取）；
    b) 原样引用的普通方法（**不绑 self**，由调用方传）。
  - 配套：`LuaBinding.CallLua` / `InvokeLua` 会把**实例表作为 self 插到实参最前面**，
    于是 Lua 侧 `inst:foo(x)` 与 C# 侧 `CallLua(comp,"foo",x)` 的 self 完全一致。
  - ⚠️ `behaviour.lua` 是 Addressables 资源，**改完必须重新 PrepareLuaAssets + 构建**才生效。

- **访问修饰符要放开**：`WeaponController.collider` 原是 `private [SerializeField]`，Lua 读不到 → 改 `public`。

🔴 **【最隐蔽的坑】Lua 里 `field ~= nil` 判断不出「空的 Unity 对象字段」**（2026-10-11 第 2 批验证时踩到）
- 现象：`if cs.sphereCollider ~= nil then cs.sphereCollider.enabled = true end` 走 then 分支，
  抛 `LuaException: c# exception:The variable sphereCollider of EnemyController has not been assigned`，
  堆栈是 `[C]: in metamethod '__newindex'` → `Collider.set_enabled`。
  **xLua 把空的 Unity 对象 push 成「非 nil 的包装」**，所以 `~= nil` 恒为 true。
- 危害链（要牢记）：异常发生在 `onenable` 的**前几行**，导致后面
  `self.currentHealth` / `self.hitBlood` / `self.dataValid` 全部没被赋值 →
  敌人血量算不出来（打不死）、机器人子弹伤害为 nil（玩家掉不了血）；
  同类异常还会让 `EnemySpawner:start` 的后续步骤（打开倒计时面板）被跳过。
  **一个空字段判断失误，表现成「伤害丢失 + 谁都死不了 + 倒计时面板不工作」三个不相关的 bug。**
- 解法（两条，都做）：
  1. **涉及 Unity 组件字段的，在 C# 壳里包成方法**，用 C# 的 null 语义判断：
     `EnemyController.SetBodyColliderEnabled(bool)`（sphereCollider 优先，没配则 capsuleCollider）、
     `RobotController.SetSphereColliderEnabled(bool)`、`WeaponController.SetHitColliderEnabled(bool)`。
  2. **其余一切空判断一律 `CS.LuaBinding.IsAlive(obj)`**，禁止 `obj ~= nil` / `== nil`。
     已全量替换：`PoolMgr.GetObj` 返回值、`GetComponent` 结果、`SceneObjectManager` 结果、
     面板回调的 panel、`getPanelByName` 结果、`startTarget/endTarget`、`targetModel`、`fireEffect`、
     缓存的 `characterController`、`cs.healthImage` …
- 配套加固：`EnemySpawner:start` 里的对象池预加载包了 `pcall`，
  避免「某个敌人预制体缺组件」连带把后面的倒计时面板一起吞掉（异常只记一条 error）。

**第 3 批（XQL 层 10 个 + GameLanuch）的新增手法**：
- **Inspector 序列化的数据类一律保留 C#**：`MaskFaction` / `BuffType` 枚举、
  `BuffData` / `PatternData` / `MaskData`。它们在 Inspector 里配好、被 `List<T>` 序列化，
  搬进 Lua 等于把配置丢掉。→ 壳保留数据 + **扁平下标接口**给 Lua 读：
  `GetMaskDataCount/GetMaskDataFaction(i)/GetPatternBuffValue(i,j)/GetRandomBuffType(i)` …
- **跨语言不传对象引用，传下标**：`MaskSystemManager:unlock_pattern_selection()` 返回
  3 个「在 allPatternDatas 里的下标」，UI 用下标反查名字/描述。比传 `List<PatternData>` 稳得多。
- **单例基类冲突的第二次出现**：`MaskSystemManager` 原继承 `UnitySingleTonMono<T>`，
  改 `LuaBehaviour` 后在壳里自实现 `Instance`（+ `DontDestroyOnLoad`），与 `KeyBindingManager` 同款处理。
- **「单一真相源」放进 C# 属性**：`IsMaskSelected` 保持在壳上（Lua 读写的都是它），
  避免 Lua 表和 C# 字段各存一份导致状态分裂。
- **`UnityAction<float>` 不赌 Lua 委托注册**：Slider 回调由壳创建 `UnityAction<float>` 并保存引用
  （`TopDownCameraFollow.BindSliderValueChanged/Unbind…`），Lua 只传 Slider 对象。
- **`OnDrawGizmosSelected` 留在壳里**（Lua 画不了编辑器辅助图形）。
- **Lua 之间不再用 C# 委托**：`PlayerModle` 的 `footStepAction`/`rootMotionAction`（原 `Action`）
  改成 Lua function 字段，`set_foot_step_handler/set_root_motion_handler`。
- **数据结构改动的连带同步**：迁完后要回头改前两批 Lua 里对被迁类的调用
  （`stats:GetFinalMaxHealthValue()` → `statsLua:get_final_max_health_value()`、`player:TakeDamage` → `playerLua:take_damage`）。
  排查手法：`grep -rn "旧方法名" Assets/LuaScripts/`。

**进度：全部完成 ✅ 2026-10-11**
- 第 1 批：基础设施 + UI 层 6 个面板（已验证）
- 第 2 批：Controller 层 13 个（`SoundAudioPool` 判定为服务层保留 C#，已验证）
- 第 3 批：XQL 层 10 个 + `GameLanuch`；数据/枚举类保留 C#
- 结果：`Assets/Scripts/HotUpdate/` 下**只剩 `SoundAudioPool`** 一个直继承 `MonoBehaviour` 的类

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

### 🔴 xLua 两条必踩的通用坑（2026-10-11 UI 层验证时踩到，务必遵守）
1. **「非 nil 的 userdata」≠「对象还活着」**
   `XLuaManager.Update()` 每帧 `_luaEnv.Tick()` → `ObjectPool.Check` 会把**已销毁的 UnityEngine.Object**
   的槽位置空（`ObjectValidCheck` 对 destroyed 返回 false → `Replace(index, null)`）。
   于是 Lua 里缓存的 `obj` 仍是 userdata、`obj ~= nil` 成立，但 C# 侧 `FastGetCSObj` 得到 null，
   调它的实例方法就抛 **`LuaException: c# exception:Non-static method requires a target`**
   （`MethodWarpsCache.cs:242` 的 `method.Invoke(targetNeeded ? target : null, args)`）。
   → **凡跨帧缓存 C# 场景对象，每帧用之前必须 `CS.LuaBinding.IsAlive(obj)` 校验**。
   最典型的触发场景：**`UIManager` 用 `UIPanelDict` 缓存面板，重开走 `Show()` 而不重跑 `Awake`**，
   旧面板里的玩家引用在切场景后就成了"僵尸 userdata"。Lua 侧的 `show`/`onenable` 钩子要顺手重取一次。
2. **`LuaEnv.Dispose()` 前必须先释放所有绑定**
   我们通过 `LuaTable.Get<Action>("update")` 拿的生命周期委托、以及 `AddListener(function() end)`
   闭包，xLua 都算「C# callback」；C# 侧只要还持有 LuaTable/委托引用，`delegate_bridges`
   里的 WeakReference 就还活着 → `LuaEnv.Dispose()` 抛
   `InvalidOperationException: try to dispose a LuaEnv with C# callback!`。
   → `XLuaManager.Dispose()` 里 **先 `LuaBinding.DisposeAll()` 再 `_luaEnv.Dispose()`**，
   并把 `Dispose()` 包 try/catch（退出时场景 UnityEvent 闭包可能还没回收，记 warning 即可）。

### 排查 Editor.log 的正确姿势
`Logs/Editor.log` 是**跨会话累积**的（当前已 22 MB / 14 万行）。里面大量 `error CS` 可能是
旧 asmdef 时代的残留（特征：报错路径含 `Library/Bee/artifacts/*/XLua.ref.dll`、
`XLua.Gen.asmdef` 之类）。**先定位最后一次 `LUA: [lua] main.lua loaded` 的行号，只看它之后的日志**，
再按 `^[A-Za-z]*Exception: ` 去重统计，才能得到"当前这次运行真正报什么错"。
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
