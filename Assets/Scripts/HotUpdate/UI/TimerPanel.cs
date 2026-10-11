using TMPro;
using UnityEngine;

/// <summary>
/// 下一波敌人倒计时面板 —— 逻辑全部在 Lua：Assets/LuaScripts/ui/timer_panel.lua.txt
/// C# 侧只保留预制体上的序列化字段 + luaModule 声明，预制体零改动
/// </summary>
public class TimerPanel : LuaPanel
{
    public TMP_Text timerText;

    protected override string DefaultLuaModule => "timer_panel";
}
