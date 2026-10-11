using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家属性面板 —— 逻辑全部在 Lua：Assets/LuaScripts/ui/player_pro_panel.lua.txt
///
/// C# 侧只保留预制体上的序列化字段 + luaModule 声明，预制体零改动。
/// 字段名必须和原来一致（damaageText 的拼写是历史遗留，别"顺手"改）
/// </summary>
public class PlayerProPanel : LuaPanel
{
    public TMP_Text damaageText;
    public TMP_Text fangyuText;
    public TMP_Text moveSpeedText;
    public TMP_Text shootRateText;
    public TMP_Text currentWaveText;
    public Image healthImage;
    public Button changeShootBtn;

    public float currentMaxHealth;

    public Button openEscPanelBtn;
    public Joystick moveJoystick;
    public Joystick rotateJoystick;
    [HideInInspector] public bool isNormalShoot = true;

    [Tooltip("拖拽手机端高度调节的UI Slider（Bar）")]
    public Slider cameraHeightSlider;

    protected override string DefaultLuaModule => "player_pro_panel";
}
