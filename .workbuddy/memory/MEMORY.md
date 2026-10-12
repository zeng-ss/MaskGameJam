# MEMORY.md —— Mask_GameJamAndroid 项目长期笔记

> 详细历史见 `2026-10-11.md` / `2026-10-12.md`（按日追加）。本文只留**跨会话仍然有效**的结论与坑。

## 1. 环境与版本
- Unity **6000.5.9f1**（`D:/APP/Unity Hub/6000.5.9f1`）；由团结引擎 `2022.3.62f3c1` 迁来。
- 迁移首次启动会报 `projectVersion '2022.3.62f3c1' is not a valid Unity version`，改 ProjectVersion.txt 后自愈。
- **Git LFS 必须 `git lfs pull`**：否则 Unity 报 "File may be corrupted or was serialized with a newer version of Unity"，实际是 134 字节的 LFS 指针。

## 2. 热更方案：xLua（Lua 热更）
- **放弃 HybridCLR，改用 xLua v2.1.15**。HybridCLR 包已删（git HEAD 里仍有，可 checkout 恢复）；`HybridCLRData/`（约 2.4 GB）、`Assets/DllBytes`、`DllConfig.*` 是旧 dll 热更残留，可清理。
- xLua 目录：源码 `Assets/XLua`（Src/Gen/Resources/Doc），原生库 `Assets/Plugins/XLua/`。
- **迁移已全量完成（2026-10-11）**：UI 层 6 面板 + Controller 13 + XQL 10 + GameLanuch。
  `Assets/Scripts/HotUpdate/` 下只剩 `SoundAudioPool` 一个直继承 `MonoBehaviour` 的类。

### 2.1 程序集结构：**不用 asmdef**（对齐 `D:\Unity\LuaDemo`）
xLua 源码 + 生成代码 + 项目代码全部在 `Assembly-CSharp`。已删除 `AOT/HotUpdate/PriorityHotUpdate/XLua/XLua.Editor` 等 asmdef。理由：
1. 生成代码里的 `public partial class ObjectTranslator / InternalGlobals / StaticLuaCallbacks` —— **partial 不能跨程序集**（曾拆过 `XLua.Gen.asmdef`，上万个 CS1061/CS0029）。
2. `ExampleConfig` 若列项目类型 → 生成代码引用 AOT，而 AOT 又引用 XLua（`XLuaManager` 用 `LuaEnv`）→ **循环依赖**，Unity 拒绝编译。项目类型让 xLua 走反射即可。
3. 保留的 asmdef 只有第三方插件，以及 **`Assets/Scripts/ProjectEditor/ProjectEditor.asmdef`（必须保留）**——它的目录名不是 "Editor"，没有 asmdef 的话用了 `UnityEditor` 的 `Build.cs` 会被编进运行时程序集。

### 2.2 项目里的 `EventInfo` 已改名 `GameEventInfo`
原来在全局命名空间，遮蔽 `System.Reflection.EventInfo`，导致 xLua 的 `CodeEmit.cs` / `Generator.cs` 报一堆 CS1061。

### 2.3 xLua 生成代码：配置层面的 `[BlackList]` 才是正解
**不要改 xLua 源码，改 `Assets/XLua/Src/Editor/ExampleConfig.cs`**（该文件顶部注释写清了全部坑）。
1. **`in` 参数被生成成 `ref`** → CS1615。已改模板 `LuaClassWrap.tpl.txt`（及 GCM / LuaDelegateWrap）：
   `IsIn && IsByRef` 时不输出修饰符；另加 `Assets/XLua/csc.rsp`（`-langversion:12.0`）兜底。
2. **`Span<T>` / ref struct 参数或返回值** 会被生成 wrap → CS0306。用 `MethodFilter` 挡：
   ```csharp
   [BlackList] public static Func<MemberInfo,bool> MethodFilter = mi => {
       if (!(mi is MethodInfo m)) return false;
       if (IsRefStruct(m.ReturnType)) return true;
       foreach (ParameterInfo p in m.GetParameters()) {
           if (IsRefStruct(p.ParameterType)) return true;
           if (p.IsIn && p.ParameterType.IsByRef) return true;
       }
       return false; };
   ```
3. **别把 `UnityEngine.Object` 加进 `LuaCallCSharp`**（生成空 wrap → CS0103 `UnityEngineObjectWrap` 不存在）。
4. **类型依赖要连带列出**：A 的成员用到 B，B 也要列，否则 `CS1061 PushXXX 不存在`。
   例：EventTrigger / Entry / **EventTriggerType** 三个必须一起列。
5. 🔴 **「编辑器编译正常，构建报 CS1061」= `#if UNITY_EDITOR` 成员在 player 编译里被剥掉。**
   现象：编辑器全绿，一跑 `Addressables/Player 构建` 就在 `CompilePlayerScripts` 阶段报
   `CS1061: 'Xxx' does not contain a definition for 'Yyy'`（堆栈含 `BuildPlayerScripts -> CompilePlayerScripts`）。
   本项目清单（写进 `ExampleConfig.cs` 的 `MethodFilter` → `EditorOnlyMembers`）：
   `MonoBehaviour.runInEditMode`、`UI.Text/UI.Graphic.OnRebuildRequested`、
   `Material` 的 `parent / isVariant / SetPropertyLock / ApplyPropertyOverride / RevertPropertyOverride /
   RevertAllPropertyOverrides / IsPropertyLocked / IsPropertyLockedByAncestor / IsPropertyOverriden / IsChildOf`。
6. 🔴 **两种黑名单的判断规则不同（连踩两次）**：
   - `isMemberInBlackList`（**属性/字段**）：只比 `DeclaringType + Name`，**不看参数** → 2 元素条目有效。
   - `isMethodInBlackList`（**方法**）：额外要求 `exclude.Count - 2 == 参数个数`。
     **凡带参方法，写进 `List<List<string>>` 等于没写**（`SetPropertyLock(id,value)` 就这么漏的）。
   - **口诀：带参方法 → `MethodFilter`；属性/字段/无参方法 → 两种都行。**
   - 通用名（如 `parent`）必须带类型全名（`Transform.parent` 要保留）。匹配见 `Generator.cs`。
7. ⚠️ **官方那 20 多条 `[BlackList]` 绝对不能删**（曾在一次「精简」里被覆盖，导致构建必炸）。
8. 换 xLua 版本时 **`Assets/XLua/Gen/` 整个清空重生成**（残留旧版生成代码会因 API 不匹配报一堆错）。

## 3. Lua 热更管线约定
- 脚本源目录 `Assets/LuaScripts/`，**后缀必须 `.lua.txt`**（Unity 才认成 TextAsset）。
  IDE 里按 Lua 解析：Rider → 装 Lua 插件后在 `Settings > Editor > File Types` 给 `LuaLanguage` 加 `*.lua.txt`；VSCode 配置见 `.vscode/settings.json`（已提交）。
- **Addressables 地址 = 纯文件名**（`main.lua.txt` → `main`）。好处：`require 'greet'` 直接对应地址；
  代价：**不同目录不能有同名 Lua 文件**。`Build.PrepareLuaAssets` 用 `Path.GetFileName`；loader 取
  `filepath` 最后一段（旧写法 `require 'demo.greet'` 仍能命中）。
- 打包/运行菜单：`Build/PrepareLuaAssets`（注册进 "Lua" 分组并打 label）、`Build/ListLuaAddresses`（核对）。
- 运行时链路：`HotUpdateSystem.Start` → `BootWatchdog.Start()` → 热更流程 →
  **`LoadLuaScripts()`：下载 label `Lua` → 建 `XLuaManager` → `PreloadLuaAssets("Lua")` → `StartLua()`（require 'main'）**
  → `GameLanuchCoroutine()`（预热 UIManager、实例化 SoundAudioPool/GameManager/GameLanuch）。
- **改了任何 Lua（含 `framework/behaviour.lua`）都必须重新 `PrepareLuaAssets` + 构建 Addressables**，否则跑的是旧脚本。
- **`Tools/lua_check.js`**：luaparse 批量校验 `Assets/LuaScripts/**/*.lua.txt` 语法，改完 Lua 先跑它。
- **Addressables 远程服务**：用户用 HFS，`Remote.LoadPath` 与 `ServerData/` 必须与之一致；起服务脚本
  `Tools/启动Addressables服务器.bat`。日常开发把 Play Mode Script 切 `Use Asset Database (fastest)` 可完全绕开 bundle/远程。

## 4. C# ↔ Lua 桥接约定（迁新代码照这个来）

### 4.1 迁移模板（一个逻辑类 = 两件事）
1. **C# 壳**：`Xxx : LuaPanel`（UI）或 `Xxx : LuaBehaviour`（其他），
   **只保留预制体上的序列化字段（名字一字不能改）** + `protected override string DefaultLuaModule => "xxx";`
   → **预制体零改动**。
2. **Lua 模块** `Assets/LuaScripts/<目录>/xxx.lua.txt`，末尾 `return function(cs) return behaviour.new(cs, X) end`。

### 4.2 桥接基础设施
- `LuaBinding`：生命周期转发（含面板的 `show`/`hide`）、
  `GetLua/GetLuaByTag/GetLuaByName`、`CallLua(组件,"lua方法名",参数…)`、
  **`IsAlive(obj)`**、**`DisposeAll()`**、实例级 **`InvokeLua(method, args…)`**（带缓存，供 `OnTriggerXxx`/`OnDrag`/动画事件转发）。
- **`behaviour.new()` 返回的就是实例表 `o` 本身**（不能再另建转发表——否则 `self.xxx=` 写到不同表上，状态会分裂）。上面同时挂：
  a) 绑死 self 的生命周期无参闭包（C# 按 `Action` 取）；
  b) 普通方法原样引用（**不绑 self**，由调用方传）。
  → `LuaBinding.CallLua/InvokeLua` 会把**实例表作为 self 插到实参最前面**，于是
  Lua 侧 `inst:foo(x)` 与 C# 侧 `CallLua(comp,"foo",x)` 的 self 完全一致。
  ⚠️ 漏掉 (b) 的典型症状：`TimerPanel:start_countdown` 调不到 → 倒计时不动；
  `EnemyBulletController:init` 调不到 → `self.bulletType` 为 nil → `PoolMgr.PushObj(nil,obj)` 抛 `Value cannot be null. Parameter name: key`。
- **`LuaTween`**（AOT/Lua）：xLua **调不了 C# 扩展方法**，把 DOTween 的 `DOLocalMove/DOFade/DelayedCall/LightIntensity/DeathBounce` 包成静态方法。
- **`LuaUtil`**：`Value01/Range/RangeInt/OnUnitSphere/ParticleDuration/Distance`。
  ⚠️ `UnityEngine.Random.Range(0,1)` 在 Lua 里会命中 **int 重载恒返回 0**；`ParticleSystem.MainModule` 字段 Lua 取不到。
- **`framework/event.lua.txt`**：Lua 事件中心（C# 的 `EventCenter` 全是泛型，Lua 调不了）。

### 4.3 迁移套路（踩出来的规则）
- **协程一律改 update 状态机**（延时回收 / 循环间隔生成 / 等 1 秒再追击），不引入 `xlua.util.cs_generator`。
- **Unity 消息由子类壳转发**：基类只转发生命周期，`OnTriggerEnter/Stay`、`IDragHandler.OnDrag` 在壳里实现并 `_binding.InvokeLua(...)`。
- **动画事件 / 外部调用的 public 方法留在壳上**（方法名不能改），内部转给 Lua。例：`EnemyController.StartAttackHit/StopAttackHit`、`RobotController.DecreaseHealth`。
- **集合一律摊平成下标方法**：`List<T>`、`Dictionary<K,V>` 在 Lua 侧不可靠 → 壳提供 `GetXxxCount/GetXxx(i)/SetXxx(i,v)`。
  例：`GameManager.GetEnemyDamage/SetEnemyHealth/GetEnemyConfigNames/ApplyDifficultyUpgrade`；`MaskSystemManager.GetPatternBuffValue(i,j)`。
- **跨语言不传对象引用，传下标**（如花纹 3 选 1 返回下标，UI 按下标反查名字）。
- **枚举在 Lua 侧是「带缓存的 userdata」**（`ObjectTranslator.enumMap`），同一枚举值始终同一对象，**可安全作 table key**。
- **`const` 字段 Lua 取不到**（`Mathf.Deg2Rad`、`NavMesh.AllAreas`）→ 直接写字面量 / `math.rad`。
- **访问修饰符要放开**：Lua 读不到的 serialized 字段改 `public`（如 `WeaponController.collider`）。
- **单例基类冲突**：原继承 `UnitySingleTonMono<T>` 的类（`KeyBindingManager`、`MaskSystemManager`）改 `LuaBehaviour` 后，
  在壳里自实现静态 `Instance` + `DontDestroyOnLoad`。
- **「单一真相源」放 C# 属性**（如 `IsMaskSelected`），避免 Lua 表与 C# 字段各存一份。
- **`UnityAction<T>` 不赌 Lua 委托注册**：Slider 回调由壳创建并保存引用。
- **`OnDrawGizmosSelected` 留在壳里**；**Inspector 序列化的数据类（枚举 / BuffData / PatternData / MaskData）保留 C#**。
- 已知限制：壳里 `DefaultLuaModule` 硬编码，「哪个物体挂哪个脚本」热更不了；
  **C# 字段名与 Lua 的 `self.cs.xxx` 是隐式契约，改名必须同步**。

## 5. 🔴 xLua 三类必踩的通用坑（务必遵守）

### 5.1 「非 nil 的 userdata」≠「对象还活着」
`XLuaManager.Update()` 每帧 `_luaEnv.Tick()` → `ObjectPool.Check` 把**已销毁的 Unity 对象**槽位置空。
于是 Lua 里 `obj ~= nil` 仍成立，但 C# 侧 target 已 null → 调实例方法抛
`LuaException: c# exception:Non-static method requires a target`（`MethodWarpsCache.cs:242`）。
→ **凡跨帧缓存 C# 场景对象，每帧用前必须 `CS.LuaBinding.IsAlive(obj)`**。
典型触发：`UIManager` 用 `UIPanelDict` 缓存面板、重开走 `Show()` 不重跑 `Awake`，
旧面板里的玩家引用在切场景后成「僵尸 userdata」。

### 5.2 Lua 里 `field ~= nil` 判断不出「空的 Unity 对象字段」
**xLua 把空 Unity 对象 push 成「非 nil 的包装」**，`~= nil` 恒为 true →
`cs.sphereCollider.enabled = true` 抛 `The variable sphereCollider has not been assigned`。
危害链：异常发生在 `onenable` 前几行 → 后面的 `self.currentHealth/hitBlood/dataValid` 全没赋值 →
表现为**「伤害丢失 + 谁都死不了 + 倒计时面板不工作」三个看似无关的 bug**。
→ 解法：涉及 Unity 组件字段的**在壳里包成方法**（用 C# 的 null 语义判断，如 `SetBodyColliderEnabled(bool)`）；
其余一律用 `CS.LuaBinding.IsAlive(obj)`，**禁止 `obj ~= nil`**。

### 5.3 `LuaEnv.Dispose()` 前必须先释放所有绑定
生命周期委托、`AddListener(function() end)` 闭包在 xLua 眼里都是「C# callback」，C# 侧还持有引用时
`LuaEnv.Dispose()` 抛 `try to dispose a LuaEnv with C# callback!`。
→ `XLuaManager.Dispose()`：**先 `LuaBinding.DisposeAll()` 再 `_luaEnv.Dispose()`**，并包 try/catch。

### 5.4 `IEnumerator` 方法当语句调用 = 什么都不发生（Unity 也不报错）
`GameLanuchCoroutine();`（返回 `IEnumerator`）单独作语句 → **枚举器创建出来就被丢掉，协程不会执行**。
症状：热更下载到 100%、点了窗口上的「进入游戏」，游戏不启动，**没有任何报错**。
必须 `StartCoroutine(GameLanuchCoroutine())`。
（这是把原来的 `void GameLanuch()` 改成协程时漏改引入的 —— 原写法 `Show(downloadSize, GameLanuch)`
把方法组直接当 `Action` 传，能跑；改成协程后回调里必须显式 StartCoroutine。）

### 5.5 热更窗口什么时候才会出现
`HotUpdateSystem` 的窗口**只在两件事同时成立时**才被创建和显示：
1. `CheckForCatalogUpdates` 发现远程 catalog 的 hash 与本地不同；
2. `GetDownloadSizeAsync > 0`（新 catalog 引用的 bundle 不在本地缓存里）。
本地开发基本看不到它：`Use Asset Database` 不走 bundle；`Use Existing Build` 时本地 catalog
就是刚构建的那份，与服务器一致 → 日志打 `无需更新 - catalog已是最新版本` → 整个下载分支被跳过
→ `_hotUpdateWindow` 始终为 null → 直接 `GameLanuchCoroutine()` 进游戏。**这是设计如此。**
想看窗口：① 走真热更流程（打客户端 → 改东西重建 → 新 ServerData 推到服务器 → 运行**客户端 exe**）；
② 或勾 `HotUpdateSystem.debugForceHotUpdateWindow`（调试开关，强制显示一次窗口供预览 UI）。
窗口的 `OnEnd` 回调是「进度走满后出现的『进入游戏』按钮」被点击时才触发的。

### 5.6 启动链路里不要留 `WaitForCompletion`
已完成清理：`GameLanuch` 的实例化、热更窗口的实例化、`VersionInfo`/`Version` 的读取全部改成异步。
原因不止「阻塞主线程」——同步等待期间主线程不回主循环，而 bundle 下载/`UnityWebRequest` 的完成
要靠主循环推进，**存在「等一个永远不会完成的操作」的死锁风险**。
`Assets/Scripts/AOT/HotUpdateSystem.cs` 里现已无任何真实 `WaitForCompletion`（只剩注释）。

### 5.7 点号调 C# 实例方法 → `invalid arguments to xxx`
```
LuaException: invalid arguments to openPanelByName
  [C]: in field 'openPanelByName'
```
**`obj.method(a)` 是错的，实例方法必须写成 `obj:method(a)`。**
xLua 里 C# 实例方法**第一个参数就是 `self`（C# 对象本身）**，`obj:foo(a)` ≡ `obj.foo(obj, a)`。
写成点号时 Lua 不补 self，所有实参整体往前错一位 → 类型对不上 → 反射调用报 `invalid arguments`。
口诀：**C# 实例方法 / Lua 表方法 → `:`；静态方法 / 字段 / 属性 / 命名空间 → `.`**。
辨别报错来源：带 `!` 且是全限定名（`invalid arguments to UnityEngine.Screen.SetResolution!`）= 生成代码（wrap）报的；
**不带 `!`**（`invalid arguments to openPanelByName`）= 反射报的（即该类型没进 LuaCallCSharp）。

## 6. 启动期「编辑器卡死」的排查方法（2026-10-12 建立）
- **先分类**：死循环 = CPU 跑满一个核；**死锁/同步阻塞 = CPU 空闲、窗口无响应、日志停住且无任何报错**。
  用 `Get-Process Unity | Select CPU` 采样 3 秒即可区分（增量≈3s → 死循环；≈0 → 阻塞）。
- **卡住时 `Logs/Editor.log` 会停止写入**（对比文件 mtime 就能确认是否真卡住）。
- **`BootWatchdog`**（`Assets/Scripts/AOT/BootWatchdog.cs`）：跑在**后台线程**上盯主线程心跳，
  主线程卡住超过 3 秒就把「最后阶段」写进 **`Logs/boot_watchdog.log`**（主线程卡死时 `Debug.Log` 写不出来，只有后台线程能记）。
  用法：`BootWatchdog.Start()`（HotUpdateSystem.Start 已接）、`Beat()`（XLuaManager.Update 已接）、
  `Enter/Leave`（可嵌套）、`Mark`（不参与计数，适合有多个早退分支的协程）。
- **`XLuaManager.LoadLuaAsset` 已改为「预加载字典查找」**：
  原来用 `LoadAssetAsync(...).WaitForCompletion()`，而 `WaitForCompletion` 内部会调 `ResourceManager.Update()`，
  Addressables 对「在回调里再进 Update」直接抛
  `Reentering the Update method is not allowed. This can happen when calling WaitForCompletion on an operation while inside of a callback.`
  → 现在在热更阶段 `PreloadLuaAssets("Lua")` 一次性把全部脚本读进字典，运行时 `require` 一次 Addressables 调用都没有。
  若某脚本漏预加载，会打 warning 并走同步兜底（说明预加载没覆盖到）。
- **别在 Addressables 回调链里发起新的 Addressables 操作**：面板打开/场景加载这类动作放在
  `start()` 或下一帧（例：`StartSceneController` 的面板打开从 `awake` 挪到了 `start`）。

## 7. 排查 Editor.log 的正确姿势
- `Logs/Editor.log` **跨会话累积**（已 20+ MB / 14 万行）。里面大量 `error CS` 可能是旧 asmdef 时代的残留
  （特征：路径含 `Library/Bee/artifacts/*/XLua.ref.dll`）。
  **先 `grep -n "main.lua loaded"` 找到最后一次运行的行号，只看它之后的内容**。
- ⚠️ **`Addressables.DownloadDependenciesAsync(key, autoReleaseHandle)` 第二个参数必须传 `false`**：
  传 `true` 时句柄完成后自动释放，紧接着访问 `.Status` 抛 `Attempting to use an invalid operation handle`；
  编辑器/本地模式下该操作同步完成，**必现**。正确写法：`false` → `yield return` → `IsValid()` 兜底 → 手动 `Release`。
- `AddressableAssetSettings` 的 `m_EnableJsonCatalog` 必须为 `1`（否则 catalog 格式不符）。
- 其他日志：Asset 导入 worker → `Logs/AssetImportWorkerHW*.log`；UPM → `Logs/upm.log`。

## 8. Unity 6 API 变更
- **API 兼容级别是 `.NET Standard 2.1`**（`ProjectSettings.asset` 的 `apiCompatibilityLevel`）。
  写代码时注意：`Dictionary.GetValueOrDefault`、`HashSet.TryGetValue`、`Index/Range` 这类**能用**；
  但 `Environment.TickCount64`（.NET Core 3.0+）之类**不能**（CS0117）。要单调时钟用 `Stopwatch.StartNew()` + `ElapsedMilliseconds`。
  另外引入 `using System.Diagnostics;` 后 `Debug` 会和 `UnityEngine.Debug` 撞名 → 在文件头加
  `using Debug = UnityEngine.Debug;` 别名。
- `Object.GetInstanceID()` 已废弃且是 **error 级别**（CS0619，不能用 pragma 关掉）→ 必须改 `GetEntityId()`。
- `EntityId` 是 struct，支持从 `int` 隐式转，可作字典 key；**没有** `EntityId → int` 隐式转换，
  所以原来是 `int` 的字典 key 要整体改成 `EntityId`（AutoPool 插件 4 文件已改）。
- 老包（`addressables` 1.22.x、`visualscripting` 1.9.4）在 Unity 6 下会因该 API 编译失败；本项目未用 VisualScripting。
