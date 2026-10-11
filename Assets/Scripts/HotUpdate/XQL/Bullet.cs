using UnityEngine;

/// <summary>
/// 玩家子弹 —— 逻辑全部在 Lua：Assets/LuaScripts/xql/bullet.lua.txt
/// 直线飞行 + 射程回收 + 命中敌人扣血。
/// </summary>
public class Bullet : LuaBehaviour
{
    protected override string DefaultLuaModule => "bullet";

    /// <summary>物理回调转发：命中敌人</summary>
    private void OnTriggerEnter(Collider other) => _binding.InvokeLua("ontriggerenter", other);

    /// <summary>发射时由 PlayerController 调用（保留 C# 入口）</summary>
    public void InitBullet(float speed, float range, float damage, Vector3 direction)
        => _binding.InvokeLua("init_bullet", speed, range, damage, direction);
}
