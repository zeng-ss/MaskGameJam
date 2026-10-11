using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 给 Lua 用的补间工具
///
/// 为什么需要它：DOTween 的 DOLocalMove / DOFade / DOLocalMoveY 都是「扩展方法」，
/// 而 xLua 无法直接调用 C# 的扩展方法（会被当成静态类的静态方法，Lua 侧写法极其难用）。
/// 这里把常用的几个包成普通静态方法，Lua 侧调用 CS.LuaTween.xxx 即可。
///
/// Lua 用法：
///   local Tween = CS.LuaTween
///   Tween.LocalMove(tr, CS.UnityEngine.Vector3.zero, 0.5, function() ... end)
///   Tween.LocalMoveY(tr, 112, 0.5, CS.DG.Tweening.Ease.OutBack, function() ... end)
///   Tween.Fade(canvasGroup, 1, 0.6, function() ... end)
///   Tween.Delay(1, function() ... end)
/// </summary>
public static class LuaTween
{
    /// <summary>世界坐标移动</summary>
    public static void Move(Transform target, Vector3 to, float duration, Action onComplete = null)
    {
        if (target == null) return;
        target.DOMove(to, duration).OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>本地坐标移动</summary>
    public static void LocalMove(Transform target, Vector3 to, float duration, Action onComplete = null)
    {
        if (target == null) return;
        target.DOLocalMove(to, duration).OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>本地坐标 Y 轴移动</summary>
    public static void LocalMoveY(Transform target, float toY, float duration, Action onComplete = null)
    {
        if (target == null) return;
        target.DOLocalMoveY(toY, duration).OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>本地坐标 Y 轴移动（带缓动曲线）</summary>
    public static void LocalMoveY(Transform target, float toY, float duration, Ease ease, Action onComplete = null)
    {
        if (target == null) return;
        target.DOLocalMoveY(toY, duration).SetEase(ease).OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>CanvasGroup 淡入淡出</summary>
    public static void Fade(CanvasGroup target, float toAlpha, float duration, Action onComplete = null)
    {
        if (target == null) return;
        target.DOFade(toAlpha, duration).OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>延时回调（替代 Lua 里手写计时器）</summary>
    public static void Delay(float seconds, Action onComplete)
    {
        if (onComplete == null) return;
        DOVirtual.DelayedCall(seconds, () => onComplete());
    }

    /// <summary>杀掉某个 Transform 上所有正在跑的补间（切场景/关面板时清理用）</summary>
    public static void Kill(Transform target, bool complete = false)
    {
        if (target == null) return;
        target.DOKill(complete);
    }

    /// <summary>Light 强度补间（下雨时调暗环境光 / 调亮玩家灯）</summary>
    public static void LightIntensity(Light target, float to, float duration, Action onComplete = null)
    {
        if (target == null) return;
        target.DOIntensity(to, duration).OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>缩放抖动（死亡受击表现）</summary>
    public static void ShakeScale(Transform target, float duration, float strength, int vibrato, Action onComplete = null)
    {
        if (target == null) return;
        target.DOShakeScale(duration, strength, vibrato).OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>旋转到指定角度（默认线性）</summary>
    public static void Rotate(Transform target, Vector3 to, float duration, Ease ease = Ease.Linear, Action onComplete = null)
    {
        if (target == null) return;
        target.DORotate(to, duration).SetEase(ease).OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>缩放</summary>
    public static void Scale(Transform target, Vector3 to, float duration, Ease ease = Ease.Linear, Action onComplete = null)
    {
        if (target == null) return;
        target.DOScale(to, duration).SetEase(ease).OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>
    /// 机器人死亡表现：抖动 + 快速自转 + 上抛，收尾缩到 0 后回调。
    /// 原来这段是 C# 里手搓的 DOTween Sequence，扩展方法在 Lua 里调不了，
    /// 而且用 Lua 串接多段补间的时序容易出错，所以整体封成一个方法。
    /// </summary>
    public static void DeathBounce(Transform target, Action onComplete = null)
    {
        if (target == null)
        {
            onComplete?.Invoke();
            return;
        }

        Sequence seq = DOTween.Sequence();
        seq.Append(target.DOShakeScale(0.5f, 0.8f, 20));
        seq.Join(target.DORotate(new Vector3(0, 720, 0), 0.8f, RotateMode.FastBeyond360));
        seq.Join(target.DOJump(target.position + Vector3.up * 4, 1f, 1, 0.8f));
        seq.Append(target.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack));
        seq.OnComplete(() => onComplete?.Invoke());
    }
}
