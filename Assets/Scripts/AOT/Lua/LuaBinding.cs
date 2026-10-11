using System;
using System.Collections.Generic;
using UnityEngine;
using XLua;

// TODO：Lua 组件桥接机制说明
//    1. 预制体上的 MonoBehaviour 改成继承 LuaBehaviour（普通组件）或 LuaPanel（UI 面板）
//       序列化字段（Inspector 里拖的 Button/Text 等）原样保留，预制体不用改
//    2. 子类只负责声明 luaModule（Lua 模块路径）+ 保留字段，具体逻辑全部写在 Lua 里
//    3. 生命周期按名字转发：awake / start / update / lateupdate / fixedupdate / onenable / ondisable / ondestroy
//    4. Lua 侧通过 self.cs 拿到 C# 组件本体，用 self.cs.xxx 访问序列化字段
//    5. Lua 侧要拿别的组件的 Lua 对象：CS.LuaBinding.GetLuaInstance(csComponent)
//    Lua 模块写法见 Assets/LuaScripts/framework/behaviour.lua.txt

/// <summary>
/// 单个 MonoBehaviour 与 Lua 模块之间的绑定关系，负责加载 Lua 模块并转发生命周期
/// LuaBehaviour 和 LuaPanel 都通过组合的方式使用它
/// </summary>
public class LuaBinding : IDisposable
{
    // C# 组件 -> Lua 实例，供 Lua 侧反查
    private static readonly Dictionary<MonoBehaviour, LuaTable> s_instanceMap =
        new Dictionary<MonoBehaviour, LuaTable>();

    /// <summary>
    /// 通过 C# 组件反查它绑定的 Lua 实例表
    /// </summary>
    public static LuaTable GetLuaInstance(MonoBehaviour mb)
    {
        if (mb == null) return null;
        LuaTable t;
        return s_instanceMap.TryGetValue(mb, out t) ? t : null;
    }

    private MonoBehaviour _owner;
    private LuaTable _luaInstance;
    private LuaTable _scriptEnv;

    private Action _luaAwake;
    private Action _luaStart;
    private Action _luaUpdate;
    private Action _luaLateUpdate;
    private Action _luaFixedUpdate;
    private Action _luaOnEnable;
    private Action _luaOnDisable;
    private Action _luaOnDestroy;

    public LuaTable LuaInstance => _luaInstance;
    public bool IsBound => _luaInstance != null;

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

        // 独立环境，避免污染全局表
        _scriptEnv = luaEnv.NewTable();
        LuaTable meta = luaEnv.NewTable();
        meta.Set("__index", luaEnv.Global);
        _scriptEnv.SetMetaTable(meta);
        meta.Dispose();
        _scriptEnv.Set("cs", owner);

        // local factory = require 'xxx'; return factory(cs)
        object[] ret = luaEnv.DoString(
            $"local factory = require '{luaModule}'; return factory(cs)",
            $"LuaBinding:{luaModule}", _scriptEnv);

        if (ret == null || ret.Length == 0 || !(ret[0] is LuaTable))
        {
            Debug.LogError($"[LuaBinding] 模块 {luaModule} 未返回合法的实例表");
            return;
        }

        _luaInstance = ret[0] as LuaTable;
        s_instanceMap[_owner] = _luaInstance;

        _luaAwake = _luaInstance.Get<Action>("awake");
        _luaStart = _luaInstance.Get<Action>("start");
        _luaUpdate = _luaInstance.Get<Action>("update");
        _luaLateUpdate = _luaInstance.Get<Action>("lateupdate");
        _luaFixedUpdate = _luaInstance.Get<Action>("fixedupdate");
        _luaOnEnable = _luaInstance.Get<Action>("onenable");
        _luaOnDisable = _luaInstance.Get<Action>("ondisable");
        _luaOnDestroy = _luaInstance.Get<Action>("ondestroy");
    }

    public void Awake() => _luaAwake?.Invoke();
    public void Start() => _luaStart?.Invoke();
    public void Update() => _luaUpdate?.Invoke();
    public void LateUpdate() => _luaLateUpdate?.Invoke();
    public void FixedUpdate() => _luaFixedUpdate?.Invoke();
    public void OnEnable() => _luaOnEnable?.Invoke();
    public void OnDisable() => _luaOnDisable?.Invoke();
    public void OnDestroy() => _luaOnDestroy?.Invoke();

    public void Dispose()
    {
        if (_owner != null) s_instanceMap.Remove(_owner);

        _luaAwake = null;
        _luaStart = null;
        _luaUpdate = null;
        _luaLateUpdate = null;
        _luaFixedUpdate = null;
        _luaOnEnable = null;
        _luaOnDisable = null;
        _luaOnDestroy = null;

        if (_luaInstance != null)
        {
            _luaInstance.Dispose();
            _luaInstance = null;
        }
        if (_scriptEnv != null)
        {
            _scriptEnv.Dispose();
            _scriptEnv = null;
        }
        _owner = null;
    }
}
