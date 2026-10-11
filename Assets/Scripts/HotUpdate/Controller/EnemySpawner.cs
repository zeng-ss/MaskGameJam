using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 无尽模式敌人波次管理器 —— 逻辑全部在 Lua：
/// Assets/LuaScripts/controller/enemy_spawner.lua.txt
///
/// C# 侧只保留预制体上的序列化字段（名字一字未改，预制体零改动）+ 供 Lua 用的扁平访问方法。
///
/// 为什么要有「扁平访问方法」：xLua 访问不了 List&lt;EnemyType&gt;（EnemyType 是自定义内嵌类型，
/// 没进 wrap 表），所以把遍历改成 下标 + 取值/赋值 的普通方法。
/// </summary>
public class EnemySpawner : LuaBehaviour
{
    [System.Serializable]
    public class EnemyType
    {
        public string prefabName; // 预制体名称
        public int weight;        // 权重（越高越容易生成）
        public int initWeight;    // 初始权重（用于重置）
    }

    [Header("敌人类型")]
    public List<EnemyType> enemyTypes = new();

    [Header("基础配置")]
    [Tooltip("初始每波生成数量")] public int initBatchSize = 5;
    [Tooltip("初始最大敌人数")] public int initMaxEnemies = 30;
    [Tooltip("初始生成间隔")] public float initSpawnInterval = 5f;
    [Tooltip("生成半径")] public float spawnRadius = 30;
    [Tooltip("同批次生成敌人的最小间距（单位）")] public float minEnemyDistance = 4f;

    [Header("难度递进配置")]
    [Tooltip("每波增加的最大敌人数")] public int waveAddMaxEnemies = 5;
    [Tooltip("每波减少的生成间隔（秒）")] public float waveReduceInterval = 0.2f;
    [Tooltip("生成间隔最低限制（秒）")] public float minSpawnInterval = 2f;
    [Tooltip("高权重敌人类型名称")] public List<string> highWeightEnemyNames;
    [Tooltip("每波给高权重敌人增加的权重")] public int waveAddHighWeight = 2;

    [Tooltip("雨列表")] public List<GameObject> rains;
    public Light enviormentLight;
    public Light playerLight;

    protected override string DefaultLuaModule => "enemy_spawner";

    #region Lua 友好接口（摊平集合访问）

    public int GetEnemyTypeCount() => enemyTypes != null ? enemyTypes.Count : 0;
    public string GetEnemyPrefabName(int index) => enemyTypes[index].prefabName;
    public int GetEnemyWeight(int index) => enemyTypes[index].weight;
    public void SetEnemyWeight(int index, int value) { enemyTypes[index].weight = value; }
    public int GetEnemyInitWeight(int index) => enemyTypes[index].initWeight;
    public void SetEnemyInitWeight(int index, int value) { enemyTypes[index].initWeight = value; }

    public bool IsHighWeightName(string prefabName)
        => highWeightEnemyNames != null && highWeightEnemyNames.Contains(prefabName);

    public int GetRainCount() => rains != null ? rains.Count : 0;
    public GameObject GetRain(int index) => rains[index];

    #endregion

    #region 外部入口（保留 C# 签名，内部转发 Lua）

    /// <summary>取当前波次</summary>
    public int GetCurrentWave()
    {
        object[] ret = LuaBinding.CallLua(this, "get_current_wave");
        return (ret != null && ret.Length > 0 && ret[0] != null) ? Convert.ToInt32(ret[0]) : 0;
    }

    /// <summary>重置无尽模式</summary>
    public void ResetEndlessMode() => _binding.InvokeLua("reset_endless_mode");

    #endregion
}
