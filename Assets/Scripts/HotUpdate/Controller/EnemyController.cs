using UnityEngine;

/// <summary>
/// 近战敌人（牛 / 海星 / 乌龟壳 / 蓝史莱姆）—— 逻辑全部在 Lua：
/// Assets/LuaScripts/controller/enemy_controller.lua.txt
///
/// 原 StartCoroutine(StartMoveToPlayer()) 是「等 1 秒再开始追玩家」，已改成 update 计时状态机。
/// StartAttackHit / StopAttackHit 是动画事件的方法名，不能改。
/// </summary>
public class EnemyController : LuaBehaviour
{
    public WeaponController weapon;
    public SphereCollider sphereCollider;
    public CapsuleCollider capsuleCollider;

    protected override string DefaultLuaModule => "enemy_controller";

    /// <summary>动画事件：攻击动画命中帧 → 打开武器碰撞</summary>
    public void StartAttackHit() => _binding.InvokeLua("start_attack_hit");

    /// <summary>动画事件：攻击动画收招帧 → 关闭武器碰撞</summary>
    public void StopAttackHit() => _binding.InvokeLua("stop_attack_hit");

    /// <summary>受击入口（原本被 EnemySpawner / Bullet 调用）</summary>
    public void DecreaseHealth(float amount) => _binding.InvokeLua("decrease_health", amount);
}
