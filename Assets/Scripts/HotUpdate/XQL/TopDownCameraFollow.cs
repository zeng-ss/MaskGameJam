using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// 俯视角摄像机跟随 —— 逻辑全部在 Lua：Assets/LuaScripts/xql/top_down_camera_follow.lua.txt
///
/// Gizmos 绘制保留在壳里（Lua 侧画不了编辑器辅助图形）。
/// 高度调节 Slider 的回调由壳创建 UnityAction 再转发，避免依赖 UnityAction&lt;float&gt; 的 Lua 委托注册。
/// </summary>
[RequireComponent(typeof(Camera))]
public class TopDownCameraFollow : LuaBehaviour
{
    [Header("【核心跟随配置】")]
    public Transform playerTarget;
    public Vector3 xzOffset = new(0, 0, 0);
    public float followSmoothSpeed = 0.2f;

    [Header("【高度调节配置（手机端核心）】")]
    public float minCameraHeight = 4f;
    public float maxCameraHeight = 10f;
    public bool keepScrollWheelForPC = true;
    public float scrollHeightSensitivity = 0.5f;

    [Header("【边界限制配置】")]
    public bool limitFollowArea = true;
    public Vector2 followRangeX = new(-20f, 20f);
    public Vector2 followRangeZ = new(-20f, 20f);

    protected override string DefaultLuaModule => "top_down_camera_follow";

    // ---- Slider 回调转发（Lua 传 Slider 进来，C# 侧创建/保存 UnityAction）----
    private UnityEngine.Events.UnityAction<float> _sliderAction;

    /// <summary>绑定高度滑动条的值变化回调</summary>
    public void BindSliderValueChanged(Slider slider)
    {
        if (slider == null) return;
        if (_sliderAction == null) _sliderAction = v => _binding.InvokeLua("on_slider_value_changed", v);
        slider.onValueChanged.AddListener(_sliderAction);
    }

    /// <summary>解绑（OnDestroy 时调用，防止场景切换重复绑定）</summary>
    public void UnbindSliderValueChanged(Slider slider)
    {
        if (slider == null || _sliderAction == null) return;
        slider.onValueChanged.RemoveListener(_sliderAction);
        _sliderAction = null;
    }

    /// <summary>Gizmos 跟随范围（编辑器调试用，保留在壳里）</summary>
    private void OnDrawGizmosSelected()
    {
        if (limitFollowArea && playerTarget != null)
        {
            Gizmos.color = Color.cyan;
            Vector3 center = new Vector3((followRangeX.x + followRangeX.y) / 2, playerTarget.position.y,
                (followRangeZ.x + followRangeZ.y) / 2);
            Vector3 size = new Vector3(followRangeX.y - followRangeX.x, 0.1f, followRangeZ.y - followRangeZ.x);
            Gizmos.DrawWireCube(center, size);
        }
    }
}
