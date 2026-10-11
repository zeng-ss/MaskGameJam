using UnityEngine;

/// <summary>
/// 抛物线炮弹 —— 逻辑全部在 Lua：Assets/LuaScripts/controller/parabolic_bullet.lua.txt
///
/// 原来用 Invoke(nameof(SetTargetPoint), 0.01f) 延迟一帧取玩家位置，
/// 现在改成 update 里的第一帧处理（跨语言用 Invoke 反射字符串不稳）。
/// </summary>
public class ParabolicBullet : LuaBehaviour
{
    [Header("抛物线配置")]
    public float height = 5f;     // 抛物线高度
    public float moveSpeed = 10f; // 移动速度（单位/秒）

    protected override string DefaultLuaModule => "parabolic_bullet";

    /// <summary>发射时由 MaskController 调用（保留 C# 入口，内部转给 Lua 的 init_bullet）</summary>
    public void InitBullet(Vector3 spawnPos) => _binding.InvokeLua("init_bullet", spawnPos);
}
