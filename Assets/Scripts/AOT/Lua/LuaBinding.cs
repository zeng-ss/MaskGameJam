using System;
using System.Collections.Generic;
using UnityEngine;
using XLua;

// TODO：Lua 组件桥接机制说明
//    1. 预制体上的 MonoBehaviour 改成继承 LuaBehaviour（普通组件）或 LuaPanel（UI 面板）
//    2. 子类只负责声明 luaModule（Lua 模块路径）+ 保留字段，具体逻辑全部写在 Lua 里
//    3. 生命周期按名字转发：awake / start / update / lateupdate / fixedupdate / onenable / ondisable / ondestroy
//       面板额外转发 show / hide（对应 BasePanel.Show / Hide）
//    4. Lua 侧通过 self.cs 拿到 C# 组件本体，用 self.cs.xxx 访问序列化字段
//    5. Lua 侧要拿别的组件的 Lua 对象：
//         CS.LuaBinding.GetLuaInstance(csComponent)   —— 已知组件
//         CS.LuaBinding.GetLuaByTag("Player")          —— 按 Tag（替代调不了的泛型 GetObjectByTag<T>）
//         CS.LuaBinding.GetLuaByName("Canvas")         —— 按名字
//    6. C# 侧要调用 Lua 实例的方法：LuaBinding.CallLua(组件, "lua方法名", 参数...)
//    7. ⚠️ Lua 里跨帧缓存 C# 场景对象（玩家/敌人…）时，每帧用之前必须先
//         CS.LuaBinding.IsAlive(缓存的obj)
//       被销毁的对象在 Lua 里仍是「非 nil 的 userdata」，直接调方法会抛
//       "Non-static method requires a target"。（面板会被 UIManager 跨场景复用，最容易踩）
//    Lua 模块写法见 Assets/LuaScripts/framework/behaviour.lua.txt

/// <summary>
/// 单个 MonoBehaviour 与 Lua 模块之间的绑定关系，负责加载 Lua 模块并转发生命周期 LuaBehaviour 和 LuaPanel 都通过组合的方式使用它
/// </summary>
public class LuaBinding : IDisposable
{
    // C# 组件 -> Lua 实例，供 Lua 侧反查
    private static readonly Dictionary<MonoBehaviour, LuaTable> s_instanceMap = new();

    // 当前所有活着的绑定，退出时统一释放（否则 LuaEnv.Dispose 会因为「还有 C# 回调」抛异常）
    private static readonly List<LuaBinding> s_activeBindings = new();

    // Lua 模块名 -> 工厂函数（模块文件最后 `return function(cs) ... end` 的那个 function）
    // 缓存起来复用，避免每绑定一个组件就 DoString 编译一遍 Lua 源码。
    // 对象池开场会一次实例化几百个同类型组件，不缓存的话主线程会被编译开销堵死。
    private static readonly Dictionary<string, LuaFunction> s_factoryCache = new();

    /// <summary>
    /// 通过 C# 组件反查它绑定的 Lua 实例表
    /// </summary>
    public static LuaTable GetLuaInstance(MonoBehaviour mb)
    {
        return mb == null ? null : s_instanceMap.GetValueOrDefault(mb);
    }

    /// <summary>
    /// 判断一个 UnityEngine.Object 是否还「活着」（用 Unity 的 null 语义：已销毁算 false）
    ///
    /// ⚠️ 为什么必须有这个函数：Lua 里跨帧缓存的场景对象（玩家、敌人、子弹…）被销毁后，
    ///    Lua 变量仍然是**非 nil 的 userdata**，`obj ~= nil` 判断不出来；
    ///    这时再调它的方法，xLua 反射调用拿到的 target 是 null，
    ///    会抛 `Non-static method requires a target`。
    ///    所以凡是跨帧缓存场景对象，每帧用之前都要先 IsAlive 确认一次。
    /// </summary>
    public static bool IsAlive(UnityEngine.Object obj)
    {
        return obj != null;
    }

    /// <summary>
    /// 释放所有绑定。必须在 LuaEnv.Dispose() **之前**调用。
    /// 否则 C# 侧还持有 LuaTable / 生命周期委托（xLua 视其为 "C# callback"），
    /// LuaEnv.Dispose() 会直接抛 InvalidOperationException: try to dispose a LuaEnv with C# callback!
    /// </summary>
    public static void DisposeAll()
    {
        if (s_activeBindings.Count > 0)
        {
            // 复制一份再遍历，Dispose 内部会 Remove
            LuaBinding[] snapshot = s_activeBindings.ToArray();
            foreach (LuaBinding binding in snapshot)
            {
                binding.Dispose();
            }
            s_activeBindings.Clear();
        }

        foreach (LuaFunction factory in s_factoryCache.Values)
        {
            if (factory != null) factory.Dispose();
        }
        s_factoryCache.Clear();

        s_instanceMap.Clear();
    }

    /// <summary>
    /// 从 GameObject 上取 Lua 实例（LuaBehaviour / LuaPanel 都认）
    /// </summary>
    public static LuaTable GetLua(GameObject go)
    {
        if (go == null) return null;

        LuaBehaviour behaviour = go.GetComponent<LuaBehaviour>();
        if (behaviour != null) return GetLuaInstance(behaviour);

        LuaPanel panel = go.GetComponent<LuaPanel>();
        return panel != null ? GetLuaInstance(panel) : null;
    }

    /// <summary>
    /// 按 Tag 取场景对象的 Lua 实例（替代 C# 的 GetObjectByTag&lt;T&gt;，Lua 调不了泛型）
    /// </summary>
    public static LuaTable GetLuaByTag(string tag)
    {
        return GetLua(GameObject.FindGameObjectWithTag(tag));
    }

    /// <summary>
    /// 按名字取场景对象的 Lua 实例
    /// </summary>
    public static LuaTable GetLuaByName(string name)
    {
        return GetLua(GameObject.Find(name));
    }

    /// <summary>
    /// C# 侧调用 Lua 实例上的方法
    /// 例：LuaBinding.CallLua(timerPanel, "start_countdown", 10f);
    ///
    /// ⚠️ Lua 侧的方法都是 `function M:foo(...)` 形式，第一个参数是 self，
    ///    所以这里会把实例表本身插到实参最前面（对应 Lua 里的 `inst:foo(...)`）。
    /// </summary>
    public static object[] CallLua(MonoBehaviour mb, string method, params object[] args)
    {
        if (mb == null)
        {
            Debug.LogWarning($"[LuaBinding] CallLua(\"{method}\") 调用失败：目标组件为空");
            return null;
        }

        LuaTable inst = GetLuaInstance(mb);
        if (inst == null)
        {
            Debug.LogWarning(
                $"[LuaBinding] CallLua(\"{method}\") 调用失败：{mb.GetType().Name} 还没有绑定 Lua 实例" +
                "（LuaEnv 未初始化，或面板/Lua 模块尚未加载完成）");
            return null;
        }

        LuaFunction fn = inst.Get<LuaFunction>(method);
        if (fn == null)
        {
            Debug.LogWarning($"[LuaBinding] CallLua 调用失败：{mb.GetType().Name} 的 Lua 模块里没有方法 {method}()");
            return null;
        }

        object[] ret = fn.Call(BuildCallArgs(inst, args));
        fn.Dispose();
        return ret;
    }

    /// <summary>把实例表作为 self 放到实参最前面</summary>
    private static object[] BuildCallArgs(LuaTable self, object[] args)
    {
        int extra = args != null ? args.Length : 0;
        object[] callArgs = new object[extra + 1];
        callArgs[0] = self;
        if (extra > 0) Array.Copy(args, 0, callArgs, 1, extra);
        return callArgs;
    }

    private MonoBehaviour _owner;
    private LuaTable _scriptEnv;

    private Action _luaAwake, _luaStart, _luaUpdate, _luaLateUpdate, _luaFixedUpdate, _luaOnEnable, _luaOnDisable, _luaOnDestroy;
    private Action _luaShow, _luaHide;

    // 供 Unity 消息转发用的 LuaFunction 缓存（OnTriggerXxx / OnDrag / 动画事件…）
    private readonly Dictionary<string, LuaFunction> _luaFuncCache = new();

    public LuaTable LuaInstance { get; private set; }

    public bool IsBound => LuaInstance != null;

    /// <summary>
    /// 调用 Lua 实例上的任意方法（结果会缓存）。
    ///
    /// 用途：基类只转发了 Unity 的生命周期消息，像 OnTriggerEnter / OnDrag /
    /// 动画事件这类「只有部分组件需要」的回调，由子类在自己的壳里实现并转发过来：
    ///   private void OnTriggerStay(Collider other) => _binding.InvokeLua("ontriggerstay", other);
    ///
    /// 与静态的 LuaBinding.CallLua 的区别：这个是实例方法，用实例级缓存，适合每帧/每次碰撞都触发的场景。
    /// 参数同样会自动补上 self（实例表）。
    /// </summary>
    public void InvokeLua(string method, params object[] args)
    {
        LuaFunction fn = GetLuaFunction(method);
        fn?.Call(BuildCallArgs(LuaInstance, args));
    }

    private LuaFunction GetLuaFunction(string method)
    {
        if (_luaFuncCache.TryGetValue(method, out LuaFunction cached)) return cached;
        LuaFunction fn = LuaInstance?.Get<LuaFunction>(method);
        if (LuaInstance != null) _luaFuncCache[method] = fn;
        return fn;
    }

    /// <summary>
    /// 加载 Lua 模块并绑定生命周期回调
    /// </summary>
    public void Bind(MonoBehaviour owner, string luaModule)
    {
        if (owner == null) return;
        _owner = owner;

        if (string.IsNullOrEmpty(luaModule))
        {
            Debug.LogError($"[LuaBinding] {owner.gameObject.name} 未配置 luaModule");
            return;
        }

        XLuaManager manager = XLuaManager.Instance;
        if (manager == null || !manager.IsInited)
        {
            Debug.LogError($"[LuaBinding] LuaEnv 未初始化，无法加载模块 {luaModule}");
            return;
        }

        LuaEnv luaEnv = manager.LuaEnv;

        // ⚠️ 性能关键：不要再「每次绑定都 DoString 编译一遍 Lua 源码」。
        //    对象池开场会一次性实例化几百个同类型组件（EnemySpawner 预加载 575 个），
        //    每次 Bind 都编译一遍 Lua 源码会把主线程堵死，表现就是
        //    「点了开始游戏之后编辑器假死，点哪都没反应」。
        //    改成：require + 取工厂函数只做一次并缓存，之后每次绑定只是一次普通函数调用。
        LuaFunction factory = GetOrCreateFactory(luaEnv, luaModule);
        if (factory == null)
        {
            Debug.LogError($"[LuaBinding] 模块 {luaModule} 未返回可用的工厂函数");
            return;
        }

        BootWatchdog.Enter($"LuaBinding.Bind({luaModule}) 执行模块工厂");
        object[] ret = factory.Call(owner);
        BootWatchdog.Leave();

        if (ret == null || ret.Length == 0 || !(ret[0] is LuaTable))
        {
            Debug.LogError($"[LuaBinding] 模块 {luaModule} 未返回合法的实例表");
            return;
        }

        LuaInstance = ret[0] as LuaTable;
        s_instanceMap[_owner] = LuaInstance;
        if (!s_activeBindings.Contains(this)) s_activeBindings.Add(this);

        _luaAwake = LuaInstance.Get<Action>("awake");
        _luaStart = LuaInstance.Get<Action>("start");
        _luaUpdate = LuaInstance.Get<Action>("update");
        _luaLateUpdate = LuaInstance.Get<Action>("lateupdate");
        _luaFixedUpdate = LuaInstance.Get<Action>("fixedupdate");
        _luaOnEnable = LuaInstance.Get<Action>("onenable");
        _luaOnDisable = LuaInstance.Get<Action>("ondisable");
        _luaOnDestroy = LuaInstance.Get<Action>("ondestroy");
        // 面板专用：BasePanel.Show/Hide 会转发到这里
        _luaShow = LuaInstance.Get<Action>("show");
        _luaHide = LuaInstance.Get<Action>("hide");
    }

    /// <summary>
    /// 取模块的工厂函数（缓存）。
    /// 缓存的是 Lua 模块最后 `return function(cs) return behaviour.new(cs, X) end` 的那个函数，
    /// 之后每个实例只需要 factory.Call(owner) 一次 —— 不用再编译 Lua 源码。
    /// </summary>
    private static LuaFunction GetOrCreateFactory(LuaEnv luaEnv, string luaModule)
    {
        if (s_factoryCache.TryGetValue(luaModule, out LuaFunction cached) && cached != null)
        {
            return cached;
        }

        BootWatchdog.Enter($"LuaBinding.GetOrCreateFactory({luaModule})");

        object[] ret = luaEnv.DoString($"return require '{luaModule}'",
            $"LuaBinding:Factory:{luaModule}");
        BootWatchdog.Leave();

        if (ret == null || ret.Length == 0 || !(ret[0] is LuaFunction fn))
        {
            return null;
        }

        s_factoryCache[luaModule] = fn;
        return fn;
    }

    public void Awake() => _luaAwake?.Invoke();
    public void Start() => _luaStart?.Invoke();
    public void Update() => _luaUpdate?.Invoke();
    public void LateUpdate() => _luaLateUpdate?.Invoke();
    public void FixedUpdate() => _luaFixedUpdate?.Invoke();
    public void OnEnable() => _luaOnEnable?.Invoke();
    public void OnDisable() => _luaOnDisable?.Invoke();
    public void OnDestroy() => _luaOnDestroy?.Invoke();
    public void Show() => _luaShow?.Invoke();
    public void Hide() => _luaHide?.Invoke();

    public void Dispose()
    {
        s_activeBindings.Remove(this);

        if (_owner != null) s_instanceMap.Remove(_owner);

        _luaAwake = null;
        _luaStart = null;
        _luaUpdate = null;
        _luaLateUpdate = null;
        _luaFixedUpdate = null;
        _luaOnEnable = null;
        _luaOnDisable = null;
        _luaOnDestroy = null;
        _luaShow = null;
        _luaHide = null;

        foreach (LuaFunction fn in _luaFuncCache.Values)
        {
            if (fn != null) fn.Dispose();
        }
        _luaFuncCache.Clear();

        if (LuaInstance != null)
        {
            LuaInstance.Dispose();
            LuaInstance = null;
        }

        if (_scriptEnv != null)
        {
            _scriptEnv.Dispose();
            _scriptEnv = null;
        }

        _owner = null;
    }
}