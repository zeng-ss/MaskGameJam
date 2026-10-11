using UnityEngine.UI;

/// <summary>
/// 暂停/退出面板 —— 逻辑全部在 Lua：Assets/LuaScripts/ui/esc_panel.lua.txt
/// C# 侧只保留预制体上的序列化字段 + luaModule 声明，预制体零改动
/// </summary>
public class EscPanel : LuaPanel
{
    public Button backBtn;
    public Button pauseBtn;
    public Button closeBtn;

    protected override string DefaultLuaModule => "esc_panel";
}