# Mask 项目：Lua 热更上手指南

> 面向「半年没碰这个项目 + 刚接触 Lua」的你。
> 目标：30 分钟内重新建立全局认知，之后能独立改逻辑、加组件、打包验证。
>
> 最后更新：2026-10-11（Lua 迁移全部完成）

---

## 目录

1. [这个项目现在是什么状态](#1-这个项目现在是什么状态)
2. [30 秒理解架构](#2-30-秒理解架构)
3. [目录地图](#3-目录地图)
4. [游戏是怎么启动起来的](#4-游戏是怎么启动起来的)
5. [核心机制：C# 壳 ↔ Lua 模块](#5-核心机制c-壳--lua-模块)
6. [Lua 语法 10 分钟速成（给 C# 开发者）](#6-lua-语法-10-分钟速成给-c-开发者)
7. [C# ↔ Lua 写法对照表（最常查的一节）](#7-c--lua-写法对照表最常查的一节)
8. [项目硬约定（必须遵守）](#8-项目硬约定必须遵守)
9. [踩坑清单（血泪史，改代码前先扫一遍）](#9-踩坑清单血泪史改代码前先扫一遍)
10. [实战一：加一个新的 Lua 组件](#10-实战一加一个新的-lua-组件)
11. [实战二：改一段现有逻辑](#11-实战二改一段现有逻辑)
12. [打包与调试流程](#12-打包与调试流程)
13. [可用 API 速查](#13-可用-api-速查)
14. [模块对照表（C# 壳 ↔ Lua 模块）](#14-模块对照表c-壳--lua-模块)

---

## 1. 这个项目现在是什么状态

| 项目 | 说明 |
|---|---|
| 引擎 | Unity 6 `6000.5.9f1`（原先从团结引擎 `2022.3.62f3c1` 迁过来） |
| 热更方案 | **xLua v2.1.15 + Addressables**（Lua 热更） |
| 游戏逻辑 | **几乎全部在 Lua**（`Assets/LuaScripts/`） |
| C# 的角色 | 只当「壳」：保存 Inspector 上配的数据 + 转发 Unity 消息 |
| 迁移状态 | 2026-10-11 全部完成。`Assets/Scripts/HotUpdate/` 下**只剩 `SoundAudioPool` 一个还是纯 C# 组件** |

**一句话理解这次迁移做了什么**：

> 原来每个脚本的**数据**（Inspector 里的字段）和**逻辑**（方法体）都在 C# 里，改一行逻辑就要重新打包发版。
> 现在把**逻辑**搬到了 Lua 文件里，C# 只剩一个空壳负责「接住 Unity 的回调」和「存数据」。
> 改逻辑 = 改 Lua 文本 = 重新打一个 Addressables 资源包 = 玩家不用重新下载安装包。

---

## 2. 30 秒理解架构

```
┌──────────────────────────────────────────────────────────────┐
│  Assets/Scripts/AOT/          【框架底座 · C# · 编进安装包】    │
│                                                              │
│  HotUpdateSystem  热更流程 + 加载 Lua                          │
│  SceneMgr / ResMgr / PoolMgr / SceneObjectManager            │
│  UIManager        面板加载/缓存/显示                           │
│  EventCenter       C# 事件中心（泛型，Lua 用不了）              │
│                                                              │
│  ── Lua 桥接层 ──                                             │
│  LuaBehaviour / LuaPanel   把 Unity 消息转发给 Lua            │
│  LuaBinding                C# 与 Lua 互相调用的入口            │
│  XLuaManager               LuaEnv 生命周期 + 脚本按需加载       │
│  LuaTween / LuaUtil        给 Lua 补上「扩展方法」和工具        │
└───────────────────────────┬──────────────────────────────────┘
                            │  按名字转发 / CallLua / GetLuaInstance
┌───────────────────────────▼──────────────────────────────────┐
│  Assets/LuaScripts/           【游戏逻辑 · Lua · 可热更】       │
│                                                              │
│  framework/  behaviour.lua（组件基类）· event.lua（事件中心）  │
│  ui/         6 个面板                                         │
│  controller/ 13 个敌人/武器/特效控制器                         │
│  xql/        10 个玩家/面具系统逻辑                            │
│  main.lua    热更入口                                         │
└──────────────────────────────────────────────────────────────┘
```

**记住一句话就够了**：

> **C# 负责「被 Unity 调用」和「存数据」，Lua 负责「做事情」。**

---

## 3. 目录地图

### 3.1 C# 侧（`Assets/Scripts/`）

```
Assets/Scripts/
├── AOT/                      框架底座（保留 C#，不迁移）
│   ├── HotUpdateSystem.cs    热更主流程
│   ├── BasePanel.cs          面板基类（Show/Hide）
│   ├── GameEvent.cs          事件枚举
│   ├── Lua/                  【Lua 桥接层，最该看的 5 个文件】
│   │   ├── LuaBehaviour.cs   普通组件的壳基类
│   │   ├── LuaPanel.cs       面板的壳基类
│   │   ├── LuaBinding.cs     双向调用 + 生命周期转发
│   │   ├── XLuaManager.cs    LuaEnv + Addressables 加载 Lua
│   │   ├── LuaTween.cs       DOTween 的 Lua 包装
│   │   └── LuaUtil.cs        随机数/距离/粒子时长等工具
│   ├── Manager/              UIManager 以外的管理器
│   │   ├── GameManager.cs        敌人运行时数值（含 Lua 友好接口）
│   │   ├── PoolMgr.cs            对象池
│   │   ├── ResMgr.cs             Addressables 资源加载
│   │   ├── SceneMgr.cs           场景切换
│   │   ├── SceneObjectManager.cs 按 Tag/Name 找场景对象（带缓存）
│   │   ├── EventCenter.cs        C# 事件中心（泛型）
│   │   └── GameData.cs           数据类 + 枚举（GameAction/KeyBinding/EnemyConfig）
│   └── Json/                 LitJson
│
└── HotUpdate/                游戏逻辑壳（多数已变成空壳）
    ├── GameLanuch.cs         启动器
    ├── UI/                   7 个面板 + UIManager（UIManager 是服务层，保留 C#）
    ├── Controller/           14 个（13 个已迁 Lua，SoundAudioPool 保留 C#）
    └── XQL/                  13 个（10 个已迁 Lua，3 个数据类保留 C#）
```

### 3.2 Lua 侧（`Assets/LuaScripts/`）

```
Assets/LuaScripts/
├── main.lua.txt                  热更入口
├── framework/
│   ├── behaviour.lua.txt         组件基类（所有模块都 return 它）
│   └── event.lua.txt             Lua 事件中心
├── ui/            （6 个面板）    start_panel / timer_panel / tip_panel /
│                                 esc_panel / fail_panel / player_pro_panel
├── controller/    （13 个）       enemy_spawner / enemy_controller / robot_controller /
│                                 parabolic_bullet / weapon_controller / ...
├── xql/           （10 个）       player_controller / player_stats / bullet /
│                                 mask_system_manager / pattern_select_ui_manager / ...
└── demo/          （1 个演示）    greet.lua.txt
```

---

## 4. 游戏是怎么启动起来的

```
游戏启动
   │
   ▼
HotUpdateSystem（AOT/C#）
   │ 1. 检查/下载 Addressables 更新
   │ 2. 下载 label 为 "Lua" 的整组资源（一堆 TextAsset）
   │ 3. 创建 XLuaManager 对象 → InitLuaEnv() → 建立 LuaEnv + 注册自定义加载器
   │ 4. StartLua()  ← 等价于 Lua 里执行 `require 'main'`
   ▼
main.lua  开始跑 Lua
   │
   ▼
实例化 GameLanuch 对象 → 它的 C# 壳 LuaBehaviour.Awake()
   │ 壳按 DefaultLuaModule 去 require 'game_lanuch'
   │ 拿到 Lua 模块返回的实例表，存进 LuaBinding
   │ 调用 Lua 的 awake()
   ▼
Lua 的 game_lanuch:awake()
   │ SceneMgr 加载 StartScene
   │ 释放自己
   ▼
场景里的各组件（StartSceneController 等）逐个被 Unity 唤醒 → 各自绑定自己的 Lua 模块
```

**关键点**：Lua 脚本是 `Addressables` 资源，**不是** 打进安装包的。所以：

- 改了 Lua → 必须重新 `PrepareLuaAssets` + `Addressables Build`
- Lua 的地址（Addressables Address）= **纯文件名**（不含目录），见 [第 8 章](#8-项目硬约定必须遵守)

---

## 5. 核心机制：C# 壳 ↔ Lua 模块

这是整个项目最需要理解的一件事。**每一个原来的 MonoBehaviour，现在都是这样一对文件**：

### 5.1 C# 壳（`Assets/Scripts/HotUpdate/XQL/PlayerStats.cs`）

```csharp
using UnityEngine;

public class PlayerStats : LuaBehaviour          // ← 基类从 MonoBehaviour 变成 LuaBehaviour
{
    [Header("生命数值")]
    public float maxHealth = 100f;                // ← 数据留着，Inspector 照旧能配
    public float moveSpeed = 6f;
    public float attackDamage = 10f;

    public float CurrentHealth { get; set; }      // 运行时数值

    // ↓ 这一行告诉框架：我的逻辑在哪个 Lua 文件里
    protected override string DefaultLuaModule => "player_stats";
}
```

### 5.2 Lua 模块（`Assets/LuaScripts/xql/player_stats.lua.txt`）

```lua
local behaviour = require 'behaviour'   -- 组件基类（地址=文件名，所以直接 require 'behaviour'）

local PlayerStats = {}

-- 对应原来的 Awake()
function PlayerStats:awake()
    self.cs.CurrentHealth = self.cs.maxHealth   -- self.cs 就是 C# 组件本体
end

-- 对应原来的 TakeDamage()
function PlayerStats:take_damage(damageValue)
    if damageValue <= 0 then return end
    local cs = self.cs
    local finalDamage = math.max(damageValue - cs.armor, 1)
    cs.CurrentHealth = math.max(cs.CurrentHealth - finalDamage, 0)
    if cs.CurrentHealth <= 0 then self:on_player_die() end
end

-- 文件最后必须这样返回一个工厂函数
return function(cs)
    return behaviour.new(cs, PlayerStats)
end
```

### 5.3 生命周期是怎么被调用的

C# 壳继承 `LuaBehaviour` 后，框架会自动把这些 Unity 消息**按名字转发**给 Lua（名字全小写）：

| Unity 消息 | Lua 里的函数名 |
|---|---|
| `Awake` | `awake` |
| `Start` | `start` |
| `Update` | `update` |
| `LateUpdate` | `lateupdate` |
| `FixedUpdate` | `fixedupdate` |
| `OnEnable` | `onenable` |
| `OnDisable` | `ondisable` |
| `OnDestroy` | `ondestroy` |
| `Show` / `Hide`（面板专用） | `show` / `hide` |

**Lua 里没写的生命周期就不调用**，不需要空函数占位。

### 5.4 Unity 里「只有部分组件需要」的消息怎么办

`OnTriggerEnter`、`OnDrag`、动画事件这类消息，框架**没有**自动转发。做法是：**在壳里写一个方法转发过去**。

```csharp
public class Bullet : LuaBehaviour
{
    protected override string DefaultLuaModule => "bullet";

    // Unity 会调用这个（因为壳上有），再转给 Lua 的 ontriggerenter
    private void OnTriggerEnter(Collider other) => _binding.InvokeLua("ontriggerenter", other);

    // 对外的方法也保留，内部转给 Lua（这样别的 C#/Lua 调起来写法不变）
    public void InitBullet(float speed, float range, float damage, Vector3 direction)
        => _binding.InvokeLua("init_bullet", speed, range, damage, direction);
}
```

对应的 Lua：

```lua
function Bullet:ontriggerenter(other)   -- 注意名字对得上
    if not other:CompareTag("Enemy") then return end
    -- ...
end

function Bullet:init_bullet(speed, range, damage, direction)
    -- ...
end
```

---

## 6. Lua 语法 10 分钟速成（给 C# 开发者）

### 6.1 变量

```lua
local a = 1              -- 局部变量（默认都该用 local）
local b = 1.5            -- 只有一种数字类型 number（内部是 double）
local s = "hello"        -- 字符串（单双引号都行）
local flag = true        -- boolean：只有 true / false
local nothing = nil      -- nil 表示「空」

-- 没有 int / float / bool / string 关键字，也没有类型声明
-- 没有声明就等于 nil，读未定义的变量不报错，返回 nil（这也是坑的来源）
```

### 6.2 字符串

```lua
local s = "abc" .. "def"                    -- 拼接用 ..（不是 +）
print(#s)                                    -- # 取长度 → 6
print(string.format("%.1f", 3.14159))        -- 格式化，和 C 的 printf 一样
print(("%d"):format(10))                     -- 也可以这样写
local n = tonumber("12.5")                   -- 字符串转数字
local str = tostring(123)                    -- 数字转字符串
```

### 6.3 表（table）—— Lua 唯一的数据结构

表既能当数组，也能当字典，还能当对象：

```lua
-- 数组（下标从 1 开始！不是 0）
local arr = { "a", "b", "c" }
print(arr[1])            -- "a"
print(#arr)              -- 3

-- 字典
local dict = { name = "Mask", hp = 100 }
print(dict.name)         -- "Mask"
print(dict["hp"])        -- 100

-- 混合
local mixed = { 10, 20, key = "value" }

-- 遍历
for i = 1, #arr do print(arr[i]) end          -- 数组用数值 for
for k, v in pairs(dict) do print(k, v) end    -- 字典用 pairs

-- 增删
table.insert(arr, "d")   -- 追加
table.remove(arr, 1)     -- 按下标删除
dict.newKey = 1          -- 直接赋值就是新增
dict.name = nil          -- 赋 nil 就是删除
```

### 6.4 函数

```lua
-- 普通函数
local function add(a, b)
    return a + b
end

-- 多返回值
local function two()
    return 1, 2
end
local x, y = two()

-- 可选参数（Lua 没有默认参数，用 or 兜底）
local function greet(name, title)
    title = title or "玩家"
    return title .. name
end
```

### 6.5 用表模拟「类」（项目里到处是这个写法）

```lua
local PlayerStats = {}          -- 这张表就是「类」

-- 用冒号定义方法：冒号会自动把调用者作为第一个参数 self
function PlayerStats:awake()
    self.cs.CurrentHealth = self.cs.maxHealth
end

function PlayerStats:take_damage(value)
    -- self 就是「谁调用的」
end

return PlayerStats
```

调用时也用冒号：

```lua
PlayerStats:take_damage(10)     -- 等价于 PlayerStats.take_damage(PlayerStats, 10)
```

**记住**：`function A:foo()` 里的 `self` = `A`；`a:foo()` 调用等价于 `a.foo(a)`。

### 6.6 闭包与作用域

```lua
local function make_counter()
    local count = 0                      -- 被闭包捕获
    return function()
        count = count + 1
        return count
    end
end

-- for 循环变量每轮都是「新的局部变量」，闭包捕获正确（不像 C# 5 之前的 for）
local fns = {}
for i = 1, 3 do
    fns[i] = function() return i end
end
print(fns[1](), fns[2](), fns[3]())      -- 1  2  3
```

### 6.7 条件与循环

```lua
-- 条件：只有 false 和 nil 是「假」，0 和 "" 都是「真」！
if x > 0 then
    -- ...
elseif x == 0 then
    -- ...
else
    -- ...
end

-- 三元运算符的替代（Lua 没有 ?:）
local max = a > b and a or b

-- 数值 for
for i = 1, 10 do end
for i = 10, 1, -1 do end          -- 倒序

-- 泛型 for
for i, v in ipairs(arr) do end    -- 数组（遇到 nil 停止）
for k, v in pairs(dict) do end    -- 字典（无序）

-- while
while cond do end
```

### 6.8 常用标准库

```lua
math.max(1, 2)          math.min(1, 2)        math.floor(1.9)
math.abs(-1)            math.sqrt(4)          math.pi
math.random()           -- 不要用！项目里请用 CS.LuaUtil（见第 7 章）

string.format / string.find / string.sub / string.rep
table.insert / table.remove / table.sort / table.concat

tostring(v) / tonumber(s) / type(v) / pcall(fn, ...)
```

### 6.9 错误处理

```lua
local ok, err = pcall(function()
    -- 可能出错的操作
end)
if not ok then
    CS.UnityEngine.Debug.LogError("出错了：" .. tostring(err))
end
```

### 6.10 和 C# 的思维差异速查

| 概念 | C# | Lua |
|---|---|---|
| 数组下标 | 从 0 开始 | **从 1 开始** |
| 长度 | `arr.Length` | `#arr` |
| 拼接 | `+` | `..` |
| 空值 | `null` | `nil` |
| 判假 | `false` 和 `null` | `false` 和 `nil`（**0 和 "" 是真**） |
| 类型 | 强类型、编译期检查 | 动态类型、运行时才报错 |
| 逻辑运算符 | `&&` `\|\|` `!` | `and` `or` `not` |
| 不等 | `!=` | `~=` |
| 成员访问 | `obj.foo` | `obj.foo`（调方法用 `obj:foo()`） |
| 注释 | `//` `/* */` | `--` `--[[ ]]` |

---

## 7. C# ↔ Lua 写法对照表（最常查的一节）

### 7.1 在 Lua 里调用 C#

Lua 里用 `CS.` 前缀访问所有 C# 类型（`CS` = CSharp 的缩写）。

| 想做的事 | C# 写法 | Lua 写法 |
|---|---|---|
| 访问静态类/单例 | `GameManager.Instance` | `CS.GameManager.Instance` |
| 访问静态方法 | `Mathf.Max(1, 2)` | `CS.UnityEngine.Mathf.Max(1, 2)` |
| 访问枚举 | `BuffType.MoveSpeed` | `CS.BuffType.MoveSpeed` |
| 调用实例方法 | `obj.GetValue()` | `obj:GetValue()` ← **冒号** |
| 读写字段/属性 | `obj.moveSpeed` | `obj.moveSpeed` ← 点号 |
| 构造对象 | `new Vector3(1, 2, 3)` | `CS.UnityEngine.Vector3(1, 2, 3)` |
| 取类型 | `typeof(Animator)` | `typeof(CS.UnityEngine.Animator)` |
| 取组件 | `GetComponent<Animator>()` | `self.cs:GetComponent(typeof(CS.UnityEngine.Animator))` |
| 空引用判断 | `obj != null` | **`CS.LuaBinding.IsAlive(obj)`** ← 千万别用 `~= nil` |

**常用命名空间前缀**：

```lua
CS.UnityEngine.*            大多数 Unity 类型（Vector3 / GameObject / Time / Mathf / Debug ...）
CS.UnityEngine.UI.*         UGUI（Button / Image / Text / Slider ...）
CS.TMPro.*                  TextMeshPro
CS.DG.Tweening.Ease.*       DOTween 的缓动曲线
CS.System.*                 .NET 里的（比如 CS.System.Convert）
```

**小技巧：把常用的提到文件顶部**，代码会干净很多：

```lua
local UnityEngine = CS.UnityEngine
local Vector3 = UnityEngine.Vector3
local Time = UnityEngine.Time
local Mathf = UnityEngine.Mathf
local PoolMgr = CS.PoolMgr
```

### 7.2 在 Lua 里调用另一个 Lua 组件

**这是迁移后最常用的写法**（因为逻辑都在 Lua，直接调 Lua 更快、也不用经过 C#）。

```lua
-- 场景里拿到了 C# 组件 obj（比如 PlayerStats 组件）
local statsLua = CS.LuaBinding.GetLuaInstance(statsComp)
if statsLua ~= nil then
    statsLua:take_damage(10)                 -- 直接调它的 Lua 方法
    local hp = statsLua:get_final_max_health_value()
end
```

也可以反向从**物体**拿：

```lua
local luaTable = CS.LuaBinding.GetLuaByTag("Player")     -- 按 Tag
local luaTable = CS.LuaBinding.GetLuaByName("Canvas")    -- 按名字
local luaTable = CS.LuaBinding.GetLua(gameObject)        -- 按 GameObject
```

### 7.3 在 C# 里调用 Lua

```csharp
// 静态调用（简单场景）
LuaBinding.CallLua(timerPanelComp, "start_countdown", 5f);

// 实例调用（每帧/每次碰撞触发时用，带缓存，更快）
_binding.InvokeLua("ontriggerenter", other);
```

Lua 侧收到的第一个参数永远是实例表（`self`），所以你按普通 `function M:foo(x)` 写就行。

### 7.4 泛型方法调不了 —— 怎么办

**xLua 无法调用 C# 的泛型方法。** 项目里统一的做法是：**在壳或管理器上加一个「非泛型的 ByName 版本」**。

| C# 里原本的写法 | Lua 里的写法 |
|---|---|
| `UIManager.Instance.openPanel<TimerPanel>(cb)` | `CS.UIManager.Instance:openPanelByName("TimerPanel", cb)` |
| `UIManager.Instance.getPanel<PlayerProPanel>()` | `CS.UIManager.Instance:getPanelByName("PlayerProPanel")` |
| `UIManager.Instance.closePanel<EscPanel>()` | `CS.UIManager.Instance:closePanelByName("EscPanel")` |
| `SceneObjectManager.Instance.GetObjectByTag<PlayerStats>("Player")` | `CS.SceneObjectManager.Instance:GetObjectByTag("Player")` 再 `:GetComponent(typeof(CS.PlayerStats))` |

> 参数是**类名**（PascalCase，和 C# 类名一字不差），因为内部就是 `typeof(T).Name`。

### 7.5 泛型集合也访问不了 —— 扁平接口

Lua 访问不了 `List<自定义类型>`、`Dictionary<string, 自定义类型>`。项目里统一在壳上加**下标访问方法**：

```csharp
// MaskSystemManager 壳里
public int GetPatternDataCount() => allPatternDatas.Count;
public string GetPatternName(int i) => allPatternDatas[i].patternName;
public float GetPatternBuffValue(int i, int j) => allPatternDatas[i].patternBuffs[j].buffValue;
```

```lua
-- Lua 侧
for i = 0, cs:GetPatternDataCount() - 1 do
    print(cs:GetPatternName(i))
end
```

`GameManager` 的敌人数值字典也是同样的处理（`GetEnemyDamage/SetEnemyHealth/GetEnemyConfigNames`）。

### 7.6 扩展方法调不了 —— 用 LuaTween / LuaUtil

`transform.DOLocalMoveY(...)` 这种 **C# 扩展方法在 Lua 里调不了**，项目已包装：

```lua
local Tween = CS.LuaTween

Tween.LocalMoveY(tr, 112, 0.3, CS.DG.Tweening.Ease.OutBack)   -- 原来是 tr.DOLocalMoveY(...)
Tween.LocalMove(tr, CS.UnityEngine.Vector3.zero, 0.5)
Tween.Fade(canvasGroup, 1, 0.6)
Tween.Delay(1.5, function() ... end)                           -- 延时回调
Tween.LightIntensity(light, 0, 5)                              -- 灯光强度
Tween.DeathBounce(transform, function() ... end)               -- 机器人死亡表现
Tween.Kill(tr)
```

Unity 的 `Random` 重载有歧义（`Random.Range(0, 1)` 在 Lua 里会命中 int 重载恒定返回 0），所以统一用：

```lua
local U = CS.LuaUtil
U.Value01()                 -- 0~1 浮点随机
U.Range(1.5, 3.5)           -- float 范围
U.RangeInt(1, 10)           -- int 范围
U.OnUnitSphere()            -- 球面随机方向
U.Distance(a, b)            -- 两点距离
U.ParticleDuration(go)      -- 粒子一轮播放时长
```

### 7.7 协程怎么办

Lua 侧**不引入跨语言协程**（容易出玄学问题）。项目统一用两种方式替代：

```lua
-- 方式 1：update 计时器（适合「每帧推进」的逻辑，比如倒计时、间隔生成）
function EnemySpawner:update()
    self.timer = self.timer + Time.deltaTime
    if self.timer >= self.spawnInterval then
        self:spawn_one_enemy()
        self.timer = 0
    end
end

-- 方式 2：Tween.Delay（适合「等一会儿做一件事」）
Tween.Delay(2, function()
    CS.UIManager.Instance:openPanelByName("FailPanel")
end)
```

### 7.8 Lua 事件中心

C# 的 `EventCenter` 全是泛型方法，Lua 调不了；迁移后事件的发送方和接收方都在 Lua，所以有 `framework/event.lua`：

```lua
local event = require 'event'

-- 监听
local function on_changed(stats) ... end
event.on('player_stats_changed', on_changed)

-- 触发
event.emit('player_stats_changed', self)

-- 移除（不传 fn 表示清空该事件的所有监听）
event.off('player_stats_changed', on_changed)
event.off('player_stats_changed')
event.clear()
```

---

## 8. 项目硬约定（必须遵守）

### 8.1 文件后缀必须是 `.lua.txt`

```
正确：player_stats.lua.txt      Unity 才会把它当 TextAsset 处理
错误：player_stats.lua
```

### 8.2 Addressables 地址 = 纯文件名（不带目录）

| 源文件 | Addressables 地址 | Lua 里 require 的写法 |
|---|---|---|
| `LuaScripts/main.lua.txt` | `main.lua` | `require 'main'` |
| `LuaScripts/ui/start_panel.lua.txt` | `start_panel.lua` | `require 'start_panel'` |
| `LuaScripts/xql/player_stats.lua.txt` | `player_stats.lua` | `require 'player_stats'` |
| `LuaScripts/framework/behaviour.lua.txt` | `behaviour.lua` | `require 'behaviour'` |

**好处**：Lua 里 `require` 直接对应地址，不用管目录层级。
**代价（重要）**：**不同目录下不能有同名文件**！加文件前先确认名字没被占用。

### 8.3 方法名用 snake_case，C# 字段保持原样

```lua
-- 名字状态：C# 壳里的字段/属性，保持原样（原代码里的拼写错误也别改，改了要同步预制体）
self.cs.maxHealth
self.cs.CurrentHealth
self.cs.damaageText      -- ← 历史遗留拼写，别"顺手"改

-- 方法状态：Lua 侧自己写的，用 snake_case
function PlayerStats:take_damage(v)      -- C# 原来是 TakeDamage
function PlayerStats:get_final_move_speed()  -- C# 原来是 GetFinalMoveSpeed
```

> 为什么要改方法名？因为「C# 字段名」是预制体序列化的契约（不能动），而「Lua 方法名」是新写的，统一风格更清晰。

### 8.4 每个 Lua 模块都要这样收尾

```lua
return function(cs)
    return behaviour.new(cs, 你的类名)
end
```

**漏了这一行，这个组件就完全不会工作。**

### 8.5 Unity 对象的空判断必须用 `IsAlive`

```lua
-- 正确
if CS.LuaBinding.IsAlive(obj) then ... end

-- 错误！空 Unity 对象在 Lua 里不是 nil
if obj ~= nil then ... end
```

（原因见 [第 9 章第 1 条](#9-踩坑清单血泪史改代码前先扫一遍)）

---

## 9. 踩坑清单（血泪史，改代码前先扫一遍）

### 坑 1（最影蔽）：空的 Unity 对象在 Lua 里**不是 nil**

```lua
-- 预制体上 sphereCollider 没赋值（是空引用）
if cs.sphereCollider ~= nil then        -- ← 竟然成立！
    cs.sphereCollider.enabled = true    -- ← 这里炸：The variable sphereCollider has not been assigned
end
```

**为什么**：xLua 把空的 Unity 对象推给 Lua 时，是一个**非 nil 的包装**。

**正确做法**：用 Unity 的 null 语义，也就是 `CS.LuaBinding.IsAlive(obj)`。

```lua
if CS.LuaBinding.IsAlive(cs.sphereCollider) then
    cs.sphereCollider.enabled = true
end
```

**更稳的做法**：涉及 Unity 组件字段的，直接在 C# 壳里包一个方法：

```csharp
public void SetBodyColliderEnabled(bool value)
{
    if (sphereCollider != null) { sphereCollider.enabled = value; return; }
    if (capsuleCollider != null) capsuleCollider.enabled = value;
}
```

```lua
cs:SetBodyColliderEnabled(true)     -- Lua 完全不用碰那两个可能为空的字段
```

> 这个坑一次造成了三个看起来毫不相关的 bug：敌人打不死、玩家不掉血、倒计时面板不动 ——
> 因为异常发生在 `onenable` 的前几行，后面给 `currentHealth` / `hitBlood` 赋值的代码全被跳过了。

### 坑 2：被销毁的对象在 Lua 里也**不是 nil**

切场景后，Lua 里缓存的玩家/敌人引用**仍然是「非 nil 的 userdata」**，但 C# 那边已经没了：

```
LuaException: c# exception: Non-static method requires a target.
  at XLua.OverloadMethodWrap.Call
  player_pro_panel:58
```

**正确做法**：**跨帧缓存了场景对象，用之前每帧校验一次**。

```lua
function Panel:ensure_player_stats()
    local stats = self.playerStats
    if stats == nil or not CS.LuaBinding.IsAlive(stats) then
        self:resolve_player_stats()    -- 重新找
        stats = self.playerStats
    end
    return stats
end
```

### 坑 3：面板会被 UIManager 缓存，重开**不会**重跑 `awake`

`UIManager` 用 `UIPanelDict` 缓存面板，第二次打开走的是 `panel.Show()`，**不经过 `Awake`**。

所以「每次打开都要做的事」写在 `show` 或 `onenable` 里，不要写在 `awake` 里：

```lua
function PlayerProPanel:awake() end          -- 只跑一次

function PlayerProPanel:show()               -- 每次显示都跑
    self:ensure_player_stats()
end
```

### 坑 4：泛型方法 / 泛型集合访问不了

见 [7.4](#74-泛型方法调不了--怎么办) 和 [7.5](#75-泛型集合也访问不了--扁平接口)。**结论：找/加带 `ByName` 或带下标的非泛型接口。**

### 坑 5：C# 的扩展方法调不了

`transform.DOLocalMoveY()` / `gameObject.AddComponent<T>()` 之类。→ 用 `CS.LuaTween`，或者壳里包一层。

### 坑 6：`const` 字段在 Lua 里取不到

```lua
CS.UnityEngine.Mathf.Deg2Rad        -- 取不到
CS.UnityEngine.Mathf.Deg2Rad  →  改用 math.rad(1) 或直接写 0.0174533

CS.UnityEngine.AI.NavMesh.AllAreas  -- 取不到
                                    → 直接写 -1
```

### 坑 7：`Random.Range` 的重载陷阱

```lua
CS.UnityEngine.Random.Range(0, 1)   -- Lua 只有一种 number，会命中 int 重载 → 恒定返回 0
```

→ 一律用 `CS.LuaUtil.Value01()` / `Range` / `RangeInt`。

### 坑 8：改了 Lua 不重新打包 = 没改

Lua 是 Addressables 资源，**编辑器里 `Use Asset Database` 模式能直接读源文件**，但一旦是打包模式，就必须重新构建。

### 坑 9：`behaviour.new()` 只暴露生命周期（已修，但要知道原理）

如果哪天你想给 `behaviour.lua` 加东西，记住它的 `new()` 必须做两件事：

1. 把生命周期挂成**绑好 self 的无参闭包**（C# 侧按 `Action` 取）
2. 把模块的**普通方法原样挂到实例表上**（这样 C# 的 `CallLua` 和 Lua 的 `obj:method()` 都能调到）

漏了第 2 条，所有跨语言调用都会失败，日志里会刷：

```
[LuaBinding] CallLua 调用失败：TimerPanel 的 Lua 模块里没有方法 start_countdown()
```

### 坑 10：调试时不要被旧日志带偏

`Logs/Editor.log` 是**跨会话累积**的（几十 MB）。找「这次运行报什么错」，先定位最后一次：

```
LUA: [lua] main.lua loaded
```

这一行之后的内容才是当前会话的。

### 坑 11（新手最容易踩）：点号调 C# 实例方法 → `invalid arguments to xxx`

```
LuaException: invalid arguments to openPanelByName
stack traceback:
  [C]: in field 'openPanelByName'
```

**报错长这样（结尾没有 `!`、名字不带命名空间）= xLua 的反射调用参数对不上**，九成是把 `:` 写成了 `.`：

```lua
-- ❌ 错：Lua 不会自动补 self，"TipPanel" 被当成了 self，函数被当成了 panelName
UIManager.Instance.openPanelByName("TipPanel", function(panel) ... end)

-- ✅ 对：实例方法一律用冒号
UIManager.Instance:openPanelByName("TipPanel", function(panel) ... end)
```

原因：xLua 里 **C# 的实例方法第一个参数就是 `self`（那个 C# 对象本身）**。
`obj:foo(a)` 等价于 `obj.foo(obj, a)`；写成 `obj.foo(a)` 就等于只传了 `a`，
于是参数位置整体错位一格，类型自然对不上。

**判断口诀**：

| 调什么 | 用什么 |
|---|---|
| C# 实例方法（`UIManager.Instance:openPanelByName(...)`） | `:` |
| 自己 Lua 表的方法（`self:do_thing()`） | `:` |
| C# 静态方法（`GameObject.FindGameObjectWithTag(...)`、`Mathf.Lerp(...)`） | `.` |
| 字段 / 属性（`cs.healthImage`、`pool.hitPlayerClip`） | `.` |
| 命名空间 / 类访问（`CS.UIManager`、`UnityEngine.Time`） | `.` |

> 另一个区分办法：**报错里带 `!` 且是全限定名**（如 `invalid arguments to UnityEngine.Screen.SetResolution!`）
> = 生成代码那套（wrap）报的；**不带 `!`** = 反射那套报的。两者的参数匹配规则不同，
> 但「先检查冒号」总是没错的。

---

## 10. 实战一：加一个新的 Lua 组件

假设要加一个「宝箱」：玩家靠近就打开。

### 第 1 步：写 C# 壳

`Assets/Scripts/HotUpdate/Controller/TreasureChest.cs`

```csharp
using UnityEngine;

public class TreasureChest : LuaBehaviour
{
    [Header("宝箱配置")]
    public GameObject chestModel;
    public int coinCount = 10;

    protected override string DefaultLuaModule => "treasure_chest";

    // 只有"部分组件需要"的 Unity 消息才需要在这里转发
    private void OnTriggerEnter(Collider other) => _binding.InvokeLua("ontriggerenter", other);

    // 对外接口（可选，方便以后 C# 调用）
    public void Open() => _binding.InvokeLua("open");
}
```

### 第 2 步：写 Lua 模块

`Assets/LuaScripts/controller/treasure_chest.lua.txt`

```lua
local behaviour = require 'behaviour'

local UnityEngine = CS.UnityEngine
local LuaBinding = CS.LuaBinding
local PoolMgr = CS.PoolMgr

local TreasureChest = {}

-- 只跑一次
function TreasureChest:awake()
    self.isOpened = false
    self.openTimer = 0
end

function TreasureChest:ontriggerenter(other)
    if self.isOpened then return end
    if not other:CompareTag("Player") then return end
    self:open()
end

function TreasureChest:open()
    if self.isOpened then return end
    self.isOpened = true

    UnityEngine.Debug.Log(string.format("宝箱打开，获得 %d 金币", self.cs.coinCount))

    -- 用延时做开箱动画后再隐藏
    CS.LuaTween.Delay(0.5, function()
        if LuaBinding.IsAlive(self.cs.chestModel) then
            self.cs.chestModel:SetActive(false)
        end
    end)
end

return function(cs)
    return behaviour.new(cs, TreasureChest)
end
```

### 第 3 步：注册到 Addressables

Unity 菜单 → **`Build > PrepareLuaAssets`**

### 第 4 步：重新构建 Addressables

Unity 菜单 → **`Addressables > Build > New Build > Default Build Script`**

### 第 5 步：挂到预制体

把 `TreasureChest` 组件挂到宝箱预制体上，**填好 Inspector 字段**（`chestModel` / `coinCount`），保存预制体。

> 注意：这次是新加的脚本，预制体需要手动挂一次。
> 而本次迁移的 33 个组件都是**原地替换**，预制体引用没变，所以不用重新挂。

---

## 11. 实战二：改一段现有逻辑

假设要把「敌人死亡后 2 秒回收」改成 3 秒。

### 方法 A：编辑器里热改（推荐，最快）

1. 找到对应的 Lua 文件（下面第 14 章有对照表）
   - `Assets/LuaScripts/controller/explo_controller.lua.txt`
2. 直接改数字：
   ```lua
   local RECYCLE_DELAY = 2   →   local RECYCLE_DELAY = 3
   ```
3. 如果 Play Mode Script 是 `Use Asset Database`，重新 Play 就生效了
4. 否则走[第 12 章](#12-打包与调试流程)的打包流程

### 方法 B：想确认「这个逻辑到底在哪个文件」

从 C# 壳出发：

```
1. 场景里看到组件是 ExploController
2. 打开 Assets/Scripts/HotUpdate/Controller/ExploController.cs
3. 看 protected override string DefaultLuaModule => "explo_controller";
4. 去 Assets/LuaScripts/ 下搜 explo_controller.lua.txt
```

一条命令搞定（在项目根目录）：

```bash
grep -rn "系统提示里的方法名" Assets/LuaScripts/
```

---

## 12. 打包与调试流程

### 12.1 编辑器里调试（日常用这个）

1. 打开 **Addressables Groups** 窗口，把 **Play Mode Script** 切成 **`Use Asset Database (fastest)`**
   - 这样 Lua 直接从源文件读，改完 Play 一次就生效，**不用每次构建**
2. 直接 Play

### 12.2 验证「打包后是否正常」（发布前必做）

```
1. 菜单 Build > PrepareLuaAssets
     作用：把 Assets/LuaScripts 下的 .lua.txt 注册进 Addressables 的 "Lua" 分组，并打上 Lua 标签
     什么时候要跑：新增/删除/重命名了 Lua 文件之后
     幂等，重复跑没关系

2. 菜单 Addressables > Build > New Build > Default Build Script
     作用：重新生成 catalog 和资源内容
     什么时候要跑：任何 Lua 内容改动之后（改了内容也要跑，因为它要打进包里）

3. Play（把 Play Mode Script 切回 Use Existing Build）
```

> 记不住就记住一句：**改了 Lua → 跑 1 和 2。只是新增文件 → 也必须跑 1。**

> 正式发布时用菜单 **`Build > NewClient`**（打新客户端）/ **`Build > UpdateClient`**（打热更包），
> 这两个会自动先调用 `PrepareLuaAssets`。

### 12.3 看日志

| 关键词 | 含义 |
|---|---|
| `LUA: [lua] main.lua loaded` | Lua 入口执行成功（也是分割「本次会话」的标记） |
| `[XLuaManager] Lua 入口执行完成` | 同上 |
| `[LuaBinding] CallLua 调用失败：... 没有方法 xxx()` | Lua 模块里没这个方法（检查名字拼写 / `behaviour.new`） |
| `[LuaBinding] CallLua 调用失败：... 还没有绑定 Lua 实例` | LuaEnv 没初始化，或模块加载失败 |
| `Non-static method requires a target` | 用了已销毁对象的引用（坑 2） |
| `The variable xxx has not been assigned` | 访问了空的 Unity 对象字段（坑 1） |
| `Value cannot be null. Parameter name: key` | 给字典传了 nil 的 key（通常是上一行有变量没赋值） |
| `module 'xxx' not found` | Addressables 里没有这个 Lua（跑 `PrepareLuaAssets`） |

### 12.4 改完 Lua 先自检语法（省一次编译等待）

```bash
# 在项目根目录执行
NODE_PATH="C:/Users/ASUS/.workbuddy/binaries/node/workspace/node_modules" \
"C:/Users/ASUS/.workbuddy/binaries/node/versions/22.22.2-6/node.exe" Tools/lua_check.js
```

它会用 `luaparse` 把 `Assets/LuaScripts/**/*.lua.txt` 全部解析一遍，有语法错会直接指出文件和错误信息。
**在切回 Unity 之前跑一遍**，能省掉「等 Unity 编译 → 发现拼错了 → 再改」的来回。

---

## 13. 可用 API 速查

### 13.1 从 C# 拿到的工具类

```lua
local Tween = CS.LuaTween      -- 补间动画（替代 DOTween 扩展方法）
local U     = CS.LuaUtil       -- 随机数/距离/粒子时长
local LB    = CS.LuaBinding    -- 跨语言调用
```

### 13.2 UI

```lua
local UIManager = CS.UIManager

-- 打开面板（回调参数是 C# 的 BasePanel）
UIManager.Instance:openPanelByName("TimerPanel", function(panel)
    if not CS.LuaBinding.IsAlive(panel) then return end
    -- panel 是 C# 组件，要调 Lua 方法就：
    local luaPanel = CS.LuaBinding.GetLuaInstance(panel)
    if luaPanel ~= nil then luaPanel:start_countdown(5) end
end)

UIManager.Instance:closePanelByName("TimerPanel")
UIManager.Instance:DestroyPanelByName("TimerPanel")   -- 彻底销毁（释放资源）
local panel = UIManager.Instance:getPanelByName("PlayerProPanel")  -- 取已打开的面板
```

**面板基类 `BasePanel` 提供**：`Show()` / `Hide()`（Lua 侧对应 `show` / `hide` 钩子）。

### 13.3 对象池

```lua
local PoolMgr = CS.PoolMgr

local obj = PoolMgr.Instance:GetObj("Cow")            -- 同步取（池子空会返回 nil 并打警告）
PoolMgr.Instance:GetObjAsync("Cow", function(o)       -- 异步取（池子空会自动加载）
    if not CS.LuaBinding.IsAlive(o) then return end
    -- ...
end)
PoolMgr.Instance:PushObj("Cow", obj)                  -- 回收
PoolMgr.Instance:Clear()                              -- 清空所有池子
```

### 13.4 资源加载

```lua
local ResMgr = CS.ResMgr

ResMgr.Instance:LoadAndInstantiateAsync("Mask", nil, function(obj)
    if not CS.LuaBinding.IsAlive(obj) then return end
    obj.transform.position = CS.UnityEngine.Vector3(0, 1, 0)
end)
ResMgr.Instance:LoadAssetAsync(address, function(asset) ... end)   -- 泛型重载，Lua 里少用
```

### 13.5 场景 / 找物体

```lua
local SceneMgr = CS.SceneMgr
local SceneObjectManager = CS.SceneObjectManager

SceneMgr.Instance:LoadSceneAsync("StartScene")                    -- 直接切
SceneMgr.Instance:LoadSceneAsync("StartScene", function()         -- 切完回调
    CS.PoolMgr.Instance:Clear()
end)

-- 按 Tag / 名字找（内部带缓存 + 有效性校验，优于 GameObject.Find）
local go = SceneObjectManager.Instance:GetObjectByTag("Player")
local go = SceneObjectManager.Instance:GetObjectByName("Canvas")

-- 或者 Lua 实例一步到位
local luaPlayer = CS.LuaBinding.GetLuaByTag("Player")
```

### 13.6 音效

```lua
local SoundAudioPool = CS.SoundAudioPool

local pool = SoundAudioPool.Instance
pool:PlaySound(pool.hitPlayerClip, self.cs.transform.position)          -- 默认音量 1
pool:PlaySound(pool.robotExploClip, pos, 0.5)                            -- 指定音量
pool.GlobalSoundVolume = 0.5                                             -- 全局音量（属性）
```

**可用的 clip 字段名**（都在 `SoundAudioPool` 上）：

```
singleFireClip  multiFireClip  parabolicClip  parabolicExploClip
robotExploClip  showPanelClip  btnSeletClip  panelHideClip
warningClip     hoverClip      hitPlayerClip  cowDieClip
playerDieClip   enemyDieClip
```

### 13.7 敌人数值（`GameManager`）

`GameManager.RuntimeEnemyConfigDict` 是 `Dictionary<string, EnemyConfigRuntime>`，Lua 访问不了，所以壳上开了一组扁平接口：

```lua
local GM = CS.GameManager

GM.Instance:HasEnemyConfig("Cow")
GM.Instance:GetEnemyDamage("Cow")
GM.Instance:GetEnemyMoveSpeed("Cow")
GM.Instance:GetEnemyHealth("Cow")
GM.Instance:SetEnemyHealth("Cow", 200)
GM.Instance:GetEnemyConfigNames()           -- string[]，Lua 里用 [0]..[n-1] 索引
GM.Instance:ApplyDifficultyUpgrade(10)      -- 全敌人血量 +10 并钳制上限
GM.Instance:ResetEnemyConfig("Cow")
GM.Instance:ResetAllEnemyConfig()
```

### 13.8 输入

```lua
local Input = CS.UnityEngine.Input
local KeyCode = CS.UnityEngine.KeyCode

if Input.GetKeyDown(KeyCode.Space) then end
if Input.GetKey(KeyCode.W) then end
local x = Input.GetAxis("Horizontal")

-- 摇杆（UI 组件，从面板上拿）
local h = panel.moveJoystick.Horizontal
local v = panel.moveJoystick.Vertical
```

### 13.9 Mathf / Vector3 常用

```lua
local Mathf = CS.UnityEngine.Mathf
local Vector3 = CS.UnityEngine.Vector3

Mathf.Clamp(v, min, max)      Mathf.Clamp01(v)
Mathf.Lerp(a, b, t)           Mathf.MoveTowards(a, b, d)
Mathf.Abs(v)                  Mathf.Max(a, b)      Mathf.Min(a, b)
Mathf.Round(v)                Mathf.FloorToInt(v)

Vector3.Distance(a, b)        Vector3.Lerp(a, b, t)
Vector3(1, 2, 3)              Vector3.zero         Vector3.one
v.magnitude                   v.normalized         v.sqrMagnitude
a + b   a - b   a * 2   -a    -- 运算符都支持
```

---

## 14. 模块对照表（C# 壳 ↔ Lua 模块）

> 用法：改了某个 Lua 想确认它对应哪个组件，或者反过来查。

### 14.1 UI 面板（6）

| C# 壳（`HotUpdate/UI/`） | Lua 模块（`LuaScripts/ui/`） |
|---|---|
| `StartPanel` | `start_panel` |
| `TimerPanel` | `timer_panel` |
| `TipPanel` | `tip_panel` |
| `EscPanel` | `esc_panel` |
| `FailPanel` | `fail_panel` |
| `PlayerProPanel` | `player_pro_panel` |
| `UIManager` | **保留 C#**（服务层：泛型加载 + Canvas 管理） |

### 14.2 控制器（14）

| C# 壳（`HotUpdate/Controller/`） | Lua 模块（`LuaScripts/controller/`） |
|---|---|
| `EnemySpawner` | `enemy_spawner`（波次/生成/难度/下雨） |
| `EnemyController` | `enemy_controller`（近战敌人 AI） |
| `RobotController` | `robot_controller`（机器人 AI + 发射子弹） |
| `EnemyBulletController` | `enemy_bullet_controller` |
| `ParabolicBullet` | `parabolic_bullet`（抛物线炮弹） |
| `WeaponController` | `weapon_controller`（敌人武器碰撞） |
| `MaskSpawner` | `mask_spawner` |
| `MaskController` | `mask_controller` |
| `ExploController` | `explo_controller` |
| `AutoGenerateNavPath` | `auto_generate_nav_path` |
| `ModelRotate` | `model_rotate` |
| `KeyBindingManager` | `key_binding_manager` |
| `StartSceneController` | `start_scene_controller` |
| `SoundAudioPool` | **保留 C#**（服务层：常驻单例） |

### 14.3 XQL（玩家 / 面具系统）（13）

| C# 壳（`HotUpdate/XQL/`） | Lua 模块（`LuaScripts/xql/`） |
|---|---|
| `PlayerController` | `player_controller`（移动/转向/射击） |
| `PlayerStats` | `player_stats`（血量/属性/死亡） |
| `PlayerModle` | `player_modle`（Animator + 根运动 + 脚步） |
| `PlayerModelAnimEventReceiver` | `player_model_anim_event_receiver`（动画事件转发 + 音效） |
| `TopDownCameraFollow` | `top_down_camera_follow` |
| `Bullet` | `bullet`（玩家子弹） |
| `MaskSystemManager` | `mask_system_manager`（面具/花纹/增益） |
| `MaskSelectUIManager` | `mask_select_ui_manager` |
| `PatternSelectUIManager` | `pattern_select_ui_manager` |
| `GameLanuch`（在 `HotUpdate/` 根目录） | `game_lanuch` |
| `MaskType.cs`（MaskFaction/BuffType 枚举） | **保留 C#**（Inspector 数据） |
| `BuffData` / `PatternData` / `MaskData` | **保留 C#**（Inspector 序列化数据） |

### 14.4 框架（`LuaScripts/framework/`）

| 文件 | 作用 |
|---|---|
| `behaviour.lua` | 组件基类，所有模块都用它构造实例 |
| `event.lua` | Lua 事件中心 |
| `main.lua` | 热更入口（`require 'main'` 就是从这里开始） |

---

## 附录 A：常见问题（FAQ）

**Q：我改了 Lua，Play 了但没生效？**
A：把 Addressables 的 **Play Mode Script** 切成 `Use Asset Database (fastest)`。如果已经在用这个模式还不生效，检查是不是改错了文件（用第 14 章对照表确认）。

**Q：控制台报 `module 'xxx' not found`？**
A：Addressables 里没有这个 Lua。跑一次 `Build > PrepareLuaAssets` 再试。另外确认**不同目录下没有同名文件**。

**Q：控制台报 `没有方法 xxx()`？**
A：三种可能 —— ① Lua 里的函数名拼错（注意 snake_case）；② 这个模块没写 `return function(cs) return behaviour.new(cs, X) end`；③ 改的 Lua 没重新打包。

**Q：怎么知道某个 C# 组件的逻辑在哪个 Lua 文件？**
A：打开那个 `.cs`，看 `DefaultLuaModule => "xxx"`，然后去 `Assets/LuaScripts/` 搜 `xxx.lua.txt`。

**Q：我想改数值（比如敌人血量、玩家移速）怎么办？**
A：分两种 ——
- **Inspector 上配的初始值**（`PlayerStats.maxHealth`、`EnemySpawner.initBatchSize`）：在预制体/场景的 Inspector 里改，不用动代码。
- **运行时计算公式**：改 Lua（比如 `player_stats.lua` 的 `get_final_move_speed`）。

**Q：想给某个敌人换个行为，改哪里？**
A：近战敌人 → `enemy_controller.lua`；机器人 → `robot_controller.lua`；面具/炮弹 → `mask_controller.lua` / `parabolic_bullet.lua`。

**Q：`self.cs` 到底是什么？**
A：就是挂在这个物体上的**那个 C# 壳组件本身**。比如 `player_stats.lua` 里的 `self.cs` 就是 `PlayerStats` 组件，所以能 `self.cs.maxHealth`、`self.cs:GetComponent(...)`。

**Q：Lua 里怎么打印日志？**
A：`CS.UnityEngine.Debug.Log("中文消息")`（`print` 也能用，但会走 Lua 自己的输出通道）。

**Q：改完记得做什么？**
A：新增文件 → `Build > PrepareLuaAssets`；任何 Lua 改动 → `Addressables > Build > New Build > Default Build Script`。

---

## 附录 B：一页速查卡片

```lua
-- ── 一个 Lua 模块的标准骨架 ──────────────────────────
local behaviour = require 'behaviour'

local UnityEngine = CS.UnityEngine          -- 常用类型提到顶部，别名短一点
local Vector3 = UnityEngine.Vector3

local MyThing = {}                          -- 1. 类表

function MyThing:awake()                    -- 2. 生命周期（全小写）
    self.timer = 0
end

function MyThing:update()
    self.timer = self.timer + UnityEngine.Time.deltaTime
end

function MyThing:my_method(v)               -- 3. 自己的方法（snake_case）
    if not CS.LuaBinding.IsAlive(v) then return end    -- 4. 空判断必须 IsAlive
end

return function(cs)                         -- 5. 收尾，必须写
    return behaviour.new(cs, MyThing)
end
```

```csharp
// ── 对应的 C# 壳骨架 ─────────────────────────────────
using UnityEngine;

public class MyThing : LuaBehaviour
{
    public float someValue = 1f;                    // Inspector 上配的数据

    protected override string DefaultLuaModule => "my_thing";   // 指向 Lua 文件名

    private void OnTriggerEnter(Collider other)     // Unity 消息 → 转发
        => _binding.InvokeLua("ontriggerenter", other);

    public void DoSomething(float v)                // 对外接口 → 转发
        => _binding.InvokeLua("do_something", v);
}
```

```lua
-- ── 最高频的 6 个写法 ───────────────────────────────
CS.GameManager.Instance:GetEnemyDamage("Cow")                 -- 调 C# 单例
self.cs.maxHealth                                             -- 读 C# 字段
local lb = CS.LuaBinding.GetLuaInstance(comp); lb:foo()        -- 调另一个 Lua 组件
CS.LuaBinding.IsAlive(obj)                                    -- 空判断
CS.UIManager.Instance:openPanelByName("TimerPanel", cb)        -- 开面板（泛型替代）
CS.LuaTween.Delay(1, function() ... end)                       -- 延时
```

---

## 附录 C：框架层维护（进阶，平时用不到）

只在你需要**给 Lua 开放新的 C# 类型/方法**时才看这节。

### C.1 xLua 的生成代码

xLua 把「C# 类型 ↔ Lua」的转换代码生成在 `Assets/XLua/Gen/`（目前约 40+ 个 `*Wrap.cs`）。

- **平时改游戏逻辑完全不用管它**（Lua 调用 C# 时，没生成 wrap 的类型会自动走反射，能用只是稍慢）
- 只有当你把新类型加进 `[LuaCallCSharp]` 列表、想提升性能时才需要重新生成

配置在 `Assets/XLua/Src/Editor/ExampleConfig.cs`。改完之后：

```
Unity 菜单 → XLua → Generate Code
```

### C.2 这个项目在 xLua 配置上踩过的坑（改配置前务必知道）

**1. 别把项目自己的类型加进 `LuaCallCSharp`**，会形成 `XLua → AOT → XLua` 的循环依赖，编译直接失败。让它们走反射就好。

**2. Unity 6 的编辑器专用成员必须进黑名单。**
`UnityEngine.Material` 的材质变体一族（`SetPropertyLock` / `ApplyPropertyOverride` / `IsChildOf` …）、
`MonoBehaviour.runInEditMode`、`UI.Text.OnRebuildRequested` 这些 API 只在编辑器里存在，
一旦生成 wrap，打 Player 包时会报 `CS1061` 导致构建中断。

**3. 黑名单有两种匹配规则，这是最容易记混的地方**：

| 黑名单写法 | 匹配规则 | 适用范围 |
|---|---|---|
| `[BlackList] List<List<string>>` | **属性/字段**：只看 `{类型全名, 成员名}`；方法额外要求「元素个数 - 2 == 参数个数」 | 属性、字段、**无参**方法 |
| `[BlackList] MethodFilter` | 自己写判断逻辑，不受参数签名限制 | **任意方法（含所有重载）** |

> 口诀：**带参方法一律写进 `MethodFilter`**；属性/字段/无参方法两种都行。
> 官方那 20 多条 `[BlackList]` 千万别删（`runInEditMode` 就在里面）。

**4. 症状对照**：

| 症状 | 原因 |
|---|---|
| 编辑器编译过，一 Build Player / Addressables 就报 `CS1061` | 编辑器专用成员没进黑名单 |
| 生成代码一大堆 `CS0019`/`CS1615`（`in` 参数、`Span<T>`） | 需要 `MethodFilter` 过滤 ref struct / in 参数 |
| `The name 'XxxWrap' does not exist` | 把 `UnityEngine.Object` 之类加进了 `LuaCallCSharp` |

### C.3 想深入了解 xLua

- 官方向导：`Assets/XLua/Doc/`（项目里就有）
- 本项目最值得读的 4 个文件（都在 `Assets/Scripts/AOT/Lua/`）：
  - `LuaBehaviour.cs`（40 行，看懂「壳」是什么）
  - `LuaPanel.cs`（同上，面板版）
  - `LuaBinding.cs`（看懂生命周期转发 + CallLua 的 self 处理）
  - `XLuaManager.cs`（看懂 LuaEnv 生命周期 + 脚本怎么从 Addressables 加载）

---

*文档结束。有看不懂或想补充的地方，随时提。*
