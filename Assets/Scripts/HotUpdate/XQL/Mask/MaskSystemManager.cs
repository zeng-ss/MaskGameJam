using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 面具系统核心管理器 —— 逻辑全部在 Lua：Assets/LuaScripts/xql/mask_system_manager.lua.txt
///
/// ⚠️ 为什么数据留在 C# 而不是搬进 Lua：
///   allMaskDatas / allPatternDatas / randomBuffTypes 是**在 Inspector 里配置的序列化数据**，
///   搬进 Lua 等于把这些配置丢掉（或必须手工重抄一遍，极易抄错）。
///   所以壳保留数据，并通过下面的「扁平下标接口」暴露给 Lua ——
///   xLua 访问不了 List&lt;MaskData&gt; / List&lt;PatternData&gt; / List&lt;BuffData&gt;。
///
/// 原来继承 UnitySingleTonMono&lt;MaskSystemManager&gt;，改 LuaBehaviour 后单继承被占用，
/// 这里自己实现等价的单例。
/// </summary>
public class MaskSystemManager : LuaBehaviour
{
    [Header("【面具基础配置】")] public List<MaskData> allMaskDatas; // 所有初始面具数据
    public int maxPatternCount = 30; // 最大花纹数量

    [Header("【花纹基础配置】")] public List<PatternData> allPatternDatas; // 所有花纹数据
    public float sameFactionProbability = 0.75f; // 对应面具派系花纹出现概率

    [Header("【变化系配置】")] public List<BuffType> randomBuffTypes; // 变化系可随机的增益类型
    public float randomBuffMinValue = 5f; // 变化系随机增益最小值
    public float randomBuffMaxValue = 15f; // 变化系随机增益最大值

    protected override string DefaultLuaModule => "mask_system_manager";

    /// <summary>是否已选择初始面具（Lua 侧也读写这个属性，保持单一真相源）</summary>
    public bool IsMaskSelected { get; set; }

    /// <summary>最大花纹数量</summary>
    public int MaxPatternCount => maxPatternCount;

    public static MaskSystemManager Instance { get; private set; }

    protected override void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        base.Awake(); // 绑定 Lua 模块
    }

    #region Lua 友好接口（摊平集合访问）

    // ---- 初始面具 ----
    public int GetMaskDataCount() => allMaskDatas != null ? allMaskDatas.Count : 0;
    public MaskFaction GetMaskDataFaction(int i) => allMaskDatas[i].maskFaction;
    public string GetMaskDataName(int i) => allMaskDatas[i].maskName;
    public string GetMaskDataDesc(int i) => allMaskDatas[i].maskDesc;

    public int GetMaskBaseBuffCount(int i)
        => allMaskDatas[i].baseBuffs != null ? allMaskDatas[i].baseBuffs.Count : 0;

    public BuffType GetMaskBaseBuffType(int i, int j) => allMaskDatas[i].baseBuffs[j].buffType;
    public float GetMaskBaseBuffValue(int i, int j) => allMaskDatas[i].baseBuffs[j].buffValue;
    public bool GetMaskBaseBuffIsPercent(int i, int j) => allMaskDatas[i].baseBuffs[j].isPercent;

    // ---- 花纹 ----
    public int GetPatternDataCount() => allPatternDatas != null ? allPatternDatas.Count : 0;
    public bool IsPatternValid(int i) => allPatternDatas[i] != null;
    public int GetPatternId(int i) => allPatternDatas[i].patternId;
    public string GetPatternName(int i) => allPatternDatas[i].patternName;
    public string GetPatternDesc(int i) => allPatternDatas[i].patternDesc;
    public MaskFaction GetPatternFaction(int i) => allPatternDatas[i].patternFaction;
    public bool GetPatternIsRandomEffect(int i) => allPatternDatas[i].isRandomEffect;

    public int GetPatternBuffCount(int i)
        => allPatternDatas[i].patternBuffs != null ? allPatternDatas[i].patternBuffs.Count : 0;

    public BuffType GetPatternBuffType(int i, int j) => allPatternDatas[i].patternBuffs[j].buffType;
    public float GetPatternBuffValue(int i, int j) => allPatternDatas[i].patternBuffs[j].buffValue;
    public bool GetPatternBuffIsPercent(int i, int j) => allPatternDatas[i].patternBuffs[j].isPercent;

    // ---- 变化系可随机增益类型 ----
    public int GetRandomBuffTypeCount() => randomBuffTypes != null ? randomBuffTypes.Count : 0;
    public BuffType GetRandomBuffType(int i) => randomBuffTypes[i];

    #endregion
}