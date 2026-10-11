using UnityEngine.UI;

/// <summary>
/// 失败面板 —— 逻辑全部在 Lua：Assets/LuaScripts/ui/fail_panel.lua.txt
/// C# 侧只保留预制体上的序列化字段 + luaModule 声明，预制体零改动
/// </summary>
public class FailPanel : LuaPanel
{
    public Button backButton;

    protected override string DefaultLuaModule => "fail_panel";
}