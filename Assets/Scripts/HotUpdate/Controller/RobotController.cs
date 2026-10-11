using UnityEngine;

/// <summary>
/// 机器人敌人（单发 / 多发）—— 逻辑全部在 Lua：Assets/LuaScripts/controller/robot_controller.lua.txt
///
/// 死亡表现原来是一串 DOTween 扩展方法（DOShakeScale / DORotate / DOJump / DOScale），
/// Lua 调不了扩展方法，已在 LuaTween 里整体封成 DeathBounce。
/// </summary>
public class RobotController : LuaBehaviour
{
    public enum RobotType
    {
        Single,
        Multi
    }

    public GameObject fireEffect;
    public SphereCollider sphereCollider;
    public RobotType robotType;

    protected override string DefaultLuaModule => "robot_controller";

    /// <summary>受击入口（原本被 EnemySpawner / Bullet 调用）</summary>
    public void DecreaseHealth(float amount) => _binding.InvokeLua("decrease_health", amount);
}
