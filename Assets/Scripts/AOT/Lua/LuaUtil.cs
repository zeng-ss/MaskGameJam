using UnityEngine;

/// <summary>
/// 给 Lua 用的小工具集。
///
/// 为什么需要它：
///   1. UnityEngine.Random.Range 有 int / float 两组重载，Lua 只有「number」一种数字类型，
///      传 5 和 10 时 xLua 选中的是哪个重载并不直观（写 Range(0, 1) 本意想要 0~1 浮点，
///      实际会命中 int 重载恒定返回 0）。这里按参数类型分开，语义确定。
///   2. ParticleSystem.MainModule 这种嵌套 struct 在 Lua 里取字段不可靠，包一层更稳。
///
/// Lua 用法：CS.LuaUtil.Value01() / CS.LuaUtil.Range(1.5, 3.5) / CS.LuaUtil.RangeInt(1, 10)
/// </summary>
public static class LuaUtil
{
    /// <summary>球面随机方向（等价 UnityEngine.Random.onUnitSphere）</summary>
    public static Vector3 OnUnitSphere() => Random.onUnitSphere;

    /// <summary>float 范围随机（含 min，不含 max）</summary>
    public static float Range(float min, float max) => Random.Range(min, max);

    /// <summary>int 范围随机（含 min，不含 max）</summary>
    public static int RangeInt(int min, int max) => Random.Range(min, max);

    /// <summary>0~1 浮点随机</summary>
    public static float Value01() => Random.Range(0f, 1f);

    /// <summary>两点距离</summary>
    public static float Distance(Vector3 a, Vector3 b) => Vector3.Distance(a, b);

    /// <summary>取粒子系统一轮播放时长（Lua 拿不到 MainModule，包一层）</summary>
    public static float ParticleDuration(GameObject go)
    {
        if (go == null) return 0f;
        ParticleSystem ps = go.GetComponent<ParticleSystem>();
        return ps != null ? ps.main.duration : 0f;
    }
}
