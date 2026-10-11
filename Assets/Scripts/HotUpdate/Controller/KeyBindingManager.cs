using UnityEngine;

/// <summary>
/// 按键绑定管理器 —— 逻辑全部在 Lua：Assets/LuaScripts/controller/key_binding_manager.lua.txt
///
/// 原来继承 UnitySingleTonMono&lt;KeyBindingManager&gt;，换成 LuaBehaviour 后单继承被占用，
/// 所以在这里自己实现等价的单例（静态 Instance + 跨场景保留 + 重复实例自毁）。
///
/// 对外的查询方法保留成 C# 入口（内部转发给 Lua），这样原来的调用方写法不用改。
/// </summary>
public class KeyBindingManager : LuaBehaviour
{
    // 是否打开设置面板（打开就禁止输入，等面板关闭）
    [HideInInspector] public bool isEscKey;

    public static KeyBindingManager Instance { get; private set; }

    protected override string DefaultLuaModule => "key_binding_manager";

    protected override void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        base.Awake(); // 绑定 Lua 模块
    }

    // ---------------- 以下为对外接口（转发给 Lua） ----------------

    /// <summary>初始化默认按键映射</summary>
    public void InitDefaultBindings() => _binding.InvokeLua("init_default_bindings");

    /// <summary>重置按键映射为默认</summary>
    public void ResetBindings() => _binding.InvokeLua("reset_bindings");

    /// <summary>更新单按键绑定（重复按键会弹提示并拒绝）</summary>
    public void UpdateBinding(GameAction action, KeyCode keyCode, string actionName = "未知")
        => _binding.InvokeLua("update_binding", action, keyCode, actionName);

    /// <summary>检测操作是否被触发（按下）</summary>
    public bool GetActionDown(GameAction action) => CallBool("get_action_down", action);

    /// <summary>检测操作是否持续触发（按住）</summary>
    public bool GetAction(GameAction action) => CallBool("get_action", action);

    /// <summary>获取操作当前绑定的按键（用于面板显示）</summary>
    public KeyCode GetBindedKey(GameAction action)
    {
        object[] ret = LuaBinding.CallLua(this, "get_binded_key", action);
        if (ret != null && ret.Length > 0 && ret[0] is double d) return (KeyCode)(int)d;
        if (ret != null && ret.Length > 0 && ret[0] is int i) return (KeyCode)i;
        return KeyCode.None;
    }

    /// <summary>取按键名的可读形式（Lua 里拿不到枚举名，这里代劳）</summary>
    public string GetKeyCodeName(KeyCode keyCode) => keyCode.ToString();

    private bool CallBool(string method, object arg)
    {
        object[] ret = LuaBinding.CallLua(this, method, arg);
        return ret != null && ret.Length > 0 && ret[0] is bool b && b;
    }
}
