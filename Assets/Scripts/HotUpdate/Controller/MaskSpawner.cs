using UnityEngine;

/// <summary>
/// 面具护盾生成器 —— 逻辑全部在 Lua：Assets/LuaScripts/controller/mask_spawner.lua.txt
/// </summary>
public class MaskSpawner : LuaBehaviour
{
    protected override string DefaultLuaModule => "mask_spawner";
}
