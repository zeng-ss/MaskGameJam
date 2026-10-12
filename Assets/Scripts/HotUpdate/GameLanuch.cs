/// <summary>
/// 游戏启动器 —— 逻辑全部在 Lua：Assets/LuaScripts/xql/game_lanuch.lua.txt
/// 热更完成后被实例化，负责加载主场景并释放自己。
/// </summary>
public class GameLanuch : LuaBehaviour
{
    protected override string DefaultLuaModule => "game_lanuch";
}