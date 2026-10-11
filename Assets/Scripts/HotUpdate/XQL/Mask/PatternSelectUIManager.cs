using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 花纹选择UI管理器（3选1）—— 逻辑全部在 Lua：
/// Assets/LuaScripts/xql/pattern_select_ui_manager.lua.txt
///
/// 原来继承 MonoBehaviour 并自己维护 static Instance，改 LuaBehaviour 后保持同样的单例写法。
/// </summary>
public class PatternSelectUIManager : LuaBehaviour
{
    [Header("【花纹选择按钮配置】")]
    public Button btn_Pattern1;
    public Button btn_Pattern2;
    public Button btn_Pattern3;

    [Header("【花纹信息显示配置】")]
    public Text txt_Pattern1Name;
    public Text txt_Pattern1Desc;
    public Text txt_Pattern2Name;
    public Text txt_Pattern2Desc;
    public Text txt_Pattern3Name;
    public Text txt_Pattern3Desc;

    [Header("【面板配置】")]
    public GameObject patternSelectPanel;

    public static PatternSelectUIManager Instance { get; private set; }

    protected override string DefaultLuaModule => "pattern_select_ui_manager";

    protected override void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        base.Awake();
    }
}
