using UnityEngine;

/// <summary>
/// 玩家数值 —— 逻辑全部在 Lua：Assets/LuaScripts/xql/player_stats.lua.txt
///
/// 数值字段保留在壳上（Inspector 配置），Lua 侧用 self.cs.xxx 读写。
/// CurrentHealth 从「私有字段 + 属性」改成自动属性，同样不进序列化，行为不变。
/// </summary>
public class PlayerStats : LuaBehaviour
{
    [Header("生命数值")]
    [Tooltip("玩家最大血量")]
    public float maxHealth = 100f;

    /// <summary>玩家当前血量（运行时动态变化，初始值等于最大血量）</summary>
    public float CurrentHealth { get; set; }

    [Header("移动数值")]
    [Tooltip("玩家基础移动速度（供PlayerController调用）")]
    public float moveSpeed = 6f;

    [Header("射击数值（攻速=1秒内可发射子弹数）")]
    [Tooltip("普通射击基础攻速")]
    public float normalShootRate = 1f;
    [Tooltip("散射射击基础攻速")]
    public float burstShootRate = 0.5f;
    [Tooltip("射速倍率")]
    public float shootRateMultiplier = 1f;
    [Tooltip("射击攻击力")]
    public float attackDamage = 10f;
    [Tooltip("普通射击弹道数")]
    public int normalBallisticCount = 1;
    [Tooltip("散射射击弹道数")]
    public int burstBallisticCount = 1;
    [Tooltip("子弹最大射程（超出则自动销毁）")]
    public float bulletMaxRange = 20f;
    [Tooltip("普通射击子弹飞行速度")]
    public float normalBulletSpeed = 30f;
    [Tooltip("散射射击子弹飞行速度")]
    public float burstBulletSpeed = 15f;

    [Header("其他预留数值")]
    [Tooltip("玩家当前护甲值（减免伤害）")]
    public float armor;
    [Tooltip("玩家可视化模型")]
    public PlayerModle playerModel;

    protected override string DefaultLuaModule => "player_stats";
}
