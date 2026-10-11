using System.Collections.Generic;
using UnityEngine;

public class GameManager : UnitySingleTonMono<GameManager>
{
    public EnemyConfigSO enemyConfig;
    // 原始配置（仅读取，不修改）
    private Dictionary<string, EnemyConfig> originEnemyConfigDict = new();
    // 运行时副本（难度调整只改这个）
    public Dictionary<string, EnemyConfigRuntime> RuntimeEnemyConfigDict = new();

    private new void Awake()
    {
        base.Awake();
        InitEnemyConfig();
    }
    
    /// <summary>
    /// 初始化：从SO读取原始配置，创建运行时副本
    /// </summary>
    private void InitEnemyConfig()
    {
        if (enemyConfig == null || enemyConfig.EnemyConfigs == null || enemyConfig.EnemyConfigs.Count == 0)
        {
            Debug.LogError("EnemyConfigSO 未配置或为空！");
            return;
        }
        // 加载原始配置（仅读取，不修改）
        foreach (var config in enemyConfig.EnemyConfigs)
        {
            if (string.IsNullOrEmpty(config.name))
            {
                Debug.LogWarning("存在名称为空的敌人配置！");
                continue;
            }
            if (!originEnemyConfigDict.ContainsKey(config.name))
            {
                originEnemyConfigDict.Add(config.name, config);
            }
        }
        // 创建运行时副本
        foreach (var kvp in originEnemyConfigDict)
        {
            if (!RuntimeEnemyConfigDict.ContainsKey(kvp.Key))
            {
                RuntimeEnemyConfigDict.Add(kvp.Key, new EnemyConfigRuntime(kvp.Value));
            }
        }
    }

    /// <summary>
    /// 对外提供：重置指定敌人的运行时配置为原始值
    /// </summary>
    public void ResetEnemyConfig(string enemyName)
    {
        if (RuntimeEnemyConfigDict.TryGetValue(enemyName, out var config)) { config.ResetToOrigin(); }
    }

    /// <summary>
    /// 对外提供：重置所有敌人的运行时配置为原始值
    /// </summary>
    public void ResetAllEnemyConfig()
    {
        foreach (var config in RuntimeEnemyConfigDict.Values) { config.ResetToOrigin(); }
    }

    #region Lua 友好接口
    // xLua 调不了泛型方法，也访问不了 Dictionary<string, EnemyConfigRuntime> 的索引器
    // （EnemyConfigRuntime 没进 wrap 表），所以这里把「读/写敌人运行时数值」摊平成普通方法给 Lua 用。

    private bool TryGetConfig(string enemyName, out EnemyConfigRuntime config)
    {
        config = null;
        return !string.IsNullOrEmpty(enemyName) && RuntimeEnemyConfigDict.TryGetValue(enemyName, out config);
    }

    /// <summary>是否存在该敌人的运行时配置</summary>
    public bool HasEnemyConfig(string enemyName) => TryGetConfig(enemyName, out _);

    /// <summary>取敌人当前伤害（不存在返回 0）</summary>
    public float GetEnemyDamage(string enemyName) => TryGetConfig(enemyName, out var c) ? c.damage : 0f;

    /// <summary>取敌人当前移速（不存在返回 0）</summary>
    public float GetEnemyMoveSpeed(string enemyName) => TryGetConfig(enemyName, out var c) ? c.moveSpeed : 0f;

    /// <summary>取敌人当前血量（不存在返回 0）</summary>
    public float GetEnemyHealth(string enemyName) => TryGetConfig(enemyName, out var c) ? c.health : 0f;

    /// <summary>设置敌人当前伤害</summary>
    public void SetEnemyDamage(string enemyName, float value)
    {
        if (TryGetConfig(enemyName, out var c)) c.damage = value;
    }

    /// <summary>设置敌人当前移速</summary>
    public void SetEnemyMoveSpeed(string enemyName, float value)
    {
        if (TryGetConfig(enemyName, out var c)) c.moveSpeed = value;
    }

    /// <summary>设置敌人当前血量</summary>
    public void SetEnemyHealth(string enemyName, float value)
    {
        if (TryGetConfig(enemyName, out var c)) c.health = value;
    }

    /// <summary>所有敌人的名字（Lua 侧遍历用）</summary>
    public string[] GetEnemyConfigNames()
    {
        var names = new string[RuntimeEnemyConfigDict.Count];
        RuntimeEnemyConfigDict.Keys.CopyTo(names, 0);
        return names;
    }

    /// <summary>
    /// 难度递增：给所有敌人加属性。
    /// 对应原 EnemySpawner.UpgradeWave() 里的：
    ///   enemyData.damage = Mathf.Min(100, enemyData.damage++);
    ///   enemyData.health = Mathf.Min(9999, enemyData.health + 10);
    ///   enemyData.moveSpeed = Mathf.Min(50, enemyData.moveSpeed++);
    /// 注意原写法里的「后置 ++ 配 Min」实际等价于「钳制到上限、不递增」，这里保持同样的行为。
    /// </summary>
    /// <param name="addHealth">血量增加量</param>
    public void ApplyDifficultyUpgrade(float addHealth)
    {
        foreach (var c in RuntimeEnemyConfigDict.Values)
        {
            c.damage = Mathf.Min(100f, c.damage);
            c.health = Mathf.Min(9999f, c.health + addHealth);
            c.moveSpeed = Mathf.Min(50f, c.moveSpeed);
        }
    }
    #endregion
}
