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

    /// <summary>
    /// 开关身体碰撞器（原逻辑：sphereCollider 优先，没配则用 capsuleCollider）。
    ///
    /// ⚠️ 为什么要包成方法，而不是让 Lua 直接写 `cs.sphereCollider.enabled = x`：
    ///    这两个字段在预制体上可能是空引用，而 Lua 里 `field ~= nil` **判断不出「空的 Unity 对象」**
    ///    （xLua 把空 Unity 对象 push 成非 nil 的包装），直接访问 .enabled 会抛
    ///    "The variable sphereCollider of EnemyController has not been assigned"。
    ///    在 C# 里 `!= null` 用的是 Unity 的 null 语义，才是可靠的。
    /// </summary>
    public void SetBodyColliderEnabled(bool value)
    {
        if (sphereCollider != null) { sphereCollider.enabled = value; return; }
        if (capsuleCollider != null) capsuleCollider.enabled = value;
    }

    /// <summary>动画事件：攻击动画命中帧 → 打开武器碰撞</summary>
    public void StartAttackHit() => _binding.InvokeLua("start_attack_hit");

    /// <summary>动画事件：攻击动画收招帧 → 关闭武器碰撞</summary>
    public void StopAttackHit() => _binding.InvokeLua("stop_attack_hit");

    /// <summary>受击入口（原本被 EnemySpawner / Bullet 调用）</summary>
    public void DecreaseHealth(float amount) => _binding.InvokeLua("decrease_health", amount);
}
