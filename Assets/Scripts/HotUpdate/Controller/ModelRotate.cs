using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 拖拽旋转模型 —— 逻辑全部在 Lua：Assets/LuaScripts/controller/model_rotate.lua.txt
///
/// OnDrag 来自 IDragHandler（EventSystem 的 UI 事件），由子类转发。
/// 接口必须保留在壳上，否则 EventSystem 不会回调。
/// </summary>
public class ModelRotate : LuaBehaviour, IDragHandler
{
    [Header("旋转控制")]
    public GameObject targetModel; // 要旋转的模型容器
    public float rotateSpeed;
    public float currentRotationY;
    public float smoothness; // 平滑度，0=无平滑，越大越平滑

    protected override string DefaultLuaModule => "model_rotate";

    /// <summary>UI 拖拽回调转发</summary>
    public void OnDrag(PointerEventData eventData) => _binding.InvokeLua("ondrag", eventData);

    /// <summary>重置旋转（外部可能调用）</summary>
    public void ResetRotation() => _binding.InvokeLua("reset_rotation");
}
