using UnityEngine;

/// <summary>
/// 动画事件接收转发器 —— 逻辑全部在 Lua：
/// Assets/LuaScripts/xql/player_model_anim_event_receiver.lua.txt
///
/// 下面这些方法**全部由动画事件调用，方法名一个字都不能改**，
/// 所以它们必须留在壳上，内部再转给 Lua。
/// </summary>
public class PlayerModelAnimEventReceiver : LuaBehaviour
{
    [Tooltip("拖拽Player根节点（挂载PlayerController的对象）")]
    public PlayerController playerController;

    [Header("【音效配置】")]
    [Tooltip("射击音效Clip（普通射击/散射共用）")]
    public AudioClip shootAudioClip;
    [Tooltip("奔跑脚步声Clip（循环播放）")]
    public AudioClip runStepAudioClip;
    [Tooltip("音效播放音量（0-1）")]
    [Range(0f, 1f)] public float audioVolume = 0.5f;

    protected override string DefaultLuaModule => "player_model_anim_event_receiver";

    public void OnNormalShootAnimEvent() => _binding.InvokeLua("on_normal_shoot_anim_event");
    public void OnNormalShootEndAnimEvent() => _binding.InvokeLua("on_normal_shoot_end_anim_event");
    public void OnBurstShootAnimEvent() => _binding.InvokeLua("on_burst_shoot_anim_event");
    public void OnBurstShootEndAnimEvent() => _binding.InvokeLua("on_burst_shoot_end_anim_event");
    public void OnRunStepStart() => _binding.InvokeLua("on_run_step_start");
    public void OnRunStepEnd() => _binding.InvokeLua("on_run_step_end");
}
