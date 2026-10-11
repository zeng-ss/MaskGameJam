using UnityEngine;

/// <summary>
/// 爆炸特效 —— 逻辑全部在 Lua：Assets/LuaScripts/controller/explo_controller.lua.txt
///
/// 原来用协程 StartCoroutine(RecycleAfterDelay(2)) 延时回收，
/// 现在改成 update 计时（跨语言调协程没必要，也更容易排查）。
/// </summary>
public class ExploController : LuaBehaviour
{
    protected override string DefaultLuaModule => "explo_controller";
}
