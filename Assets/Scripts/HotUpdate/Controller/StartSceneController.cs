using UnityEngine;

/// <summary>
/// 开始场景入口 —— 逻辑全部在 Lua：Assets/LuaScripts/controller/start_scene_controller.lua.txt
/// 只做一件事：打开开始面板。
/// </summary>
public class StartSceneController : LuaBehaviour
{
    protected override string DefaultLuaModule => "start_scene_controller";
}
