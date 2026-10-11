using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 提示面板 —— 逻辑全部在 Lua：Assets/LuaScripts/ui/tip_panel.lua.txt
///
/// C# 侧只保留「预制体上的序列化字段」+ luaModule 声明，所以预制体零改动：
///   字段名不能改，改了 Inspector 上的引用会丢（canvasGroup1 的名字就是历史原因，
///   用 FormerlySerializedAs 记住旧名字）
/// </summary>
public class TipPanel : LuaPanel
{
    public TMP_Text tipText;

    [FormerlySerializedAs("canvasGroup")]
    public CanvasGroup canvasGroup1;

    public float showTime = 1f;

    protected override string DefaultLuaModule => "tip_panel";
}
