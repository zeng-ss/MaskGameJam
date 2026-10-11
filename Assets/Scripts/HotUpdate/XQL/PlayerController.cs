using UnityEngine;

/// <summary>
/// 玩家控制器（移动 / 转向 / 普通射击 / 散射）—— 逻辑全部在 Lua：
/// Assets/LuaScripts/xql/player_controller.lua.txt
///
/// OnNormalShootAnimEvent 等是**动画事件**调用的方法名，不能改。
/// </summary>
public class PlayerController : LuaBehaviour
{
    [Header("旋转设置")]
    public float RotateSpeed = 18f;

    [Header("重力设置")]
    public float Gravity = -9.81f;

    [Header("动画组件（必须赋值）")]
    public Animator PlayerAnimator;
    [Tooltip("普通射击动画状态名称")]
    public string normalShootAnimStateName = "Shoot";
    [Tooltip("散射射击动画状态名称")]
    public string burstShootAnimStateName = "ShootBurst";
    [Tooltip("射击动画层索引")]
    public int shootAnimLayerIndex = 1;

    [Header("射击设置")]
    public float ShootLayerWeight = 1f;
    [Tooltip("子弹预制体（ZiDan）")]
    public GameObject BulletPrefab;
    [Tooltip("子弹生成点")]
    public Transform BulletSpawnPoint;
    [Tooltip("散射扇形角度（越大扩散越广，默认15度）")]
    public float BurstSpreadAngle = 15f;

    [Header("核心组件")]
    public Camera MainCamera;
    public Transform PlayerModleTransform;

    [Header("数值管理")]
    public PlayerStats playerStats;

    protected override string DefaultLuaModule => "player_controller";

    // ---------------- 动画事件（方法名不能改） ----------------

    public void OnNormalShootAnimEvent() => _binding.InvokeLua("on_normal_shoot_anim_event");
    public void OnNormalShootEndAnimEvent() => _binding.InvokeLua("on_normal_shoot_end_anim_event");
    public void OnBurstShootAnimEvent() => _binding.InvokeLua("on_burst_shoot_anim_event");
    public void OnBurstShootEndAnimEvent() => _binding.InvokeLua("on_burst_shoot_end_anim_event");
}
