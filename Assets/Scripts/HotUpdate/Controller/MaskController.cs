using UnityEngine;

/// <summary>
/// 面具控制器（绕原点旋转 + 定时发射抛物线炮弹）—— 逻辑全部在 Lua：
/// Assets/LuaScripts/controller/mask_controller.lua.txt
/// </summary>
public class MaskController : LuaBehaviour
{
    protected override string DefaultLuaModule => "mask_controller";
}
