using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 面具选择UI管理器 —— 逻辑全部在 Lua：
/// Assets/LuaScripts/xql/mask_select_ui_manager.lua.txt
/// </summary>
public class MaskSelectUIManager : LuaBehaviour
{
    [Header("【按钮配置】")]
    [Tooltip("按钮0：疾风面具（Btn_Wind）")]
    public Button btn_Wind;
    [Tooltip("按钮1：恶鬼面具（Btn_Oni）")]
    public Button btn_Oni;
    [Tooltip("按钮2：变化面具（Btn_Random）")]
    public Button btn_Random;

    [Header("【面板配置】")]
    [Tooltip("面具选择面板（MaskSelectPanel）")]
    public GameObject maskSelectPanel;

    public static MaskSelectUIManager Instance { get; private set; }

    protected override string DefaultLuaModule => "mask_select_ui_manager";

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
