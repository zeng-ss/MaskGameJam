using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 开始面板
/// C# 侧只保留序列化字段（预制体引用不变），全部逻辑在 Assets/LuaScripts/ui/start_panel.lua.txt
/// </summary>
public class StartPanel : LuaPanel
{
    public Button startBtn;
    public TMP_Text startText;
    public Button endlessBtn;
    public TMP_Text endlessText;
    public Button exitBtn;
    public TMP_Text exitText;

    protected override string DefaultLuaModule => "start_panel";
}
