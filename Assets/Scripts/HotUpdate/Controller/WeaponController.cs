using UnityEngine;

/// <summary>
/// 武器碰撞检测 —— 逻辑全部在 Lua：Assets/LuaScripts/controller/weapon_controller.lua.txt
///
/// OnTriggerStay 是 Unity 的物理回调，由子类转发（LuaBehaviour 只转发生命周期消息）。
/// StartAttackHit / StopAttackHit 由动画事件调用，方法名必须保留。
/// </summary>
public class WeaponController : LuaBehaviour
{
    public string curEnemyName; // 当前武器持有者的名字

    [Header("武器碰撞器")] public new Collider collider;

    protected override string DefaultLuaModule => "weapon_controller";

    /// <summary>物理回调转发：武器持续碰到玩家</summary>
    private void OnTriggerStay(Collider other) => _binding.InvokeLua("ontriggerstay", other);

    /// <summary>动画事件：技能释放开始（打开碰撞器）</summary>
    public void StartAttackHit() => _binding.InvokeLua("start_attack_hit");

    /// <summary>动画事件：技能释放结束（关闭碰撞器）</summary>
    public void StopAttackHit() => _binding.InvokeLua("stop_attack_hit");
}
