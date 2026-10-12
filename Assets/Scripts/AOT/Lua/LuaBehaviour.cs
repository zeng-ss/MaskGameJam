using UnityEngine;
using XLua;

/// <summary>
/// 普通 MonoBehaviour 的 Lua 桥接基类
/// 子类保留序列化字段并声明 luaModule，逻辑全部写在 Lua 里
/// </summary>
public class LuaBehaviour : MonoBehaviour
{
    [Tooltip("Lua 模块名，对应 Addressables 地址（即文件名），例如 enemy_controller")]
    public string luaModule = "";

    protected LuaBinding _binding = new LuaBinding();

    public LuaTable LuaInstance => _binding.LuaInstance;

    /// <summary>
    /// 子类覆盖这个属性声明 Lua 模块路径，这样预制体不用改
    /// Inspector 里的 luaModule 字段优先级更高（留空则用这里的值）
    /// </summary>
    protected virtual string DefaultLuaModule => "";

    protected virtual void Awake()
    {
        _binding.Bind(this, string.IsNullOrEmpty(luaModule) ? DefaultLuaModule : luaModule);
        _binding.Awake();
    }

    protected virtual void Start() => _binding.Start();
    protected virtual void Update() => _binding.Update();
    protected virtual void LateUpdate() => _binding.LateUpdate();
    protected virtual void FixedUpdate() => _binding.FixedUpdate();

    protected virtual void OnEnable()
    {
        // Awake 之前不会绑 Lua，这里避免空调用
        if (_binding.IsBound) _binding.OnEnable();
    }

    protected virtual void OnDisable() => _binding.OnDisable();

    protected virtual void OnDestroy()
    {
        _binding.OnDestroy();
        _binding.Dispose();
    }
}