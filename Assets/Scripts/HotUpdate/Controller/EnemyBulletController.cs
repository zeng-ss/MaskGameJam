using UnityEngine;

/// <summary>
/// 敌人子弹 —— 逻辑全部在 Lua：Assets/LuaScripts/controller/enemy_bullet_controller.lua.txt
///
/// OnTriggerEnter 是 Unity 的物理回调，LuaBehaviour 只转发生命周期消息，
/// 所以碰撞这类「只有部分组件需要」的消息由子类自己转发（见下面的 OnTriggerEnter）。
/// </summary>
public class EnemyBulletController : LuaBehaviour
{
    protected override string DefaultLuaModule => "enemy_bullet_controller";

    /// <summary>物理回调转发：打到玩家身上</summary>
    private void OnTriggerEnter(Collider other) => _binding.InvokeLua("ontriggerenter", other);

    /// <summary>
    /// 发射时由 RobotController 调用（保留 C# 入口，内部转给 Lua 的 init）
    /// </summary>
    public void Init(string bulletType, float hit) => _binding.InvokeLua("init", bulletType, hit);
}
