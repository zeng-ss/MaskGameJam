using UnityEngine;

/// <summary>
/// 玩家模型（Animator + 根运动 + 脚步音效回调）—— 逻辑全部在 Lua：
/// Assets/LuaScripts/xql/player_modle.lua.txt
///
/// OnAnimatorMove 是 Unity 的 Animator 根运动回调，由子类转发给 Lua。
/// footStep 是动画事件的方法名，不能改。
/// </summary>
public class PlayerModle : LuaBehaviour
{
    public Animator playerAnimator;
    public Animator Animator => playerAnimator;

    protected override string DefaultLuaModule => "player_modle";

    /// <summary>Animator 根运动回调 → Lua</summary>
    private void OnAnimatorMove() => _binding.InvokeLua("on_animator_move");

    /// <summary>动画事件：脚步声</summary>
    public void footStep() => _binding.InvokeLua("foot_step");
}
