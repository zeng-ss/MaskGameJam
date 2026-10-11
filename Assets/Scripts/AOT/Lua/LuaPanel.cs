using UnityEngine;
using XLua;

/// <summary>
/// UI 面板的 Lua 桥接基类（继承 BasePanel，保留 Show/Hide 行为）
/// 子类保留序列化字段并声明 luaModule，面板逻辑全部写在 Lua 里
/// </summary>
public class LuaPanel : BasePanel
{
    [Tooltip("Lua 模块路径，对应 Assets/LuaScripts 下的 require 路径，例如 ui.start_panel")]
    public string luaModule = "";

    protected LuaBinding _binding = new LuaBinding();

    public LuaTable LuaInstance => _binding.LuaInstance;

    /// <summary>
    /// 子类覆盖这个属性声明 Lua 模块路径，这样预制体不用改
    /// Inspector 里的 luaModule 字段优先级更高（留空则用这里的值）
    /// </summary>
    protected virtual string DefaultLuaModule => "";

    public override void Awake()
    {
        base.Awake();
        _binding.Bind(this, string.IsNullOrEmpty(luaModule) ? DefaultLuaModule : luaModule);
        _binding.Awake();
    }

    protected virtual void Start() => _binding.Start();
    protected virtual void Update() => _binding.Update();
    protected virtual void LateUpdate() => _binding.LateUpdate();
    protected virtual void FixedUpdate() => _binding.FixedUpdate();

    protected virtual void OnEnable()
    {
        if (_binding.IsBound) _binding.OnEnable();
    }

    protected virtual void OnDisable() => _binding.OnDisable();

    protected virtual void OnDestroy()
    {
        _binding.OnDestroy();
        _binding.Dispose();
    }
}
