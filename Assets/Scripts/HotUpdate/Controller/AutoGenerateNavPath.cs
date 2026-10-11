using UnityEngine;

/// <summary>
/// 自动生成 NavMesh 寻路路径并画到 LineRenderer —— 逻辑全部在 Lua：
/// Assets/LuaScripts/controller/auto_generate_nav_path.lua.txt
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class AutoGenerateNavPath : LuaBehaviour
{
    [Header("路径配置")]
    public float lineWidth = 0.2f; // 导航线宽度
    public Transform startTarget;  // 寻路起点
    public Transform endTarget;    // 寻路终点
    [HideInInspector] public bool isStart;

    protected override string DefaultLuaModule => "auto_generate_nav_path";

    /// <summary>外部也可显式生成路径（保留 C# 入口）</summary>
    public void GenerateAutoNavPath(Vector3 startPos, Vector3 endPos)
        => _binding.InvokeLua("generate_auto_nav_path", startPos, endPos);
}
