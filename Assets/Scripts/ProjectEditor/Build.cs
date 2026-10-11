using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// 构建工具类
/// 提供两个菜单项用于不同阶段的构建：
/// 1. NewClient: 构建完整客户端
/// 2. UpdateClient: 构建增量更新包
/// </summary>
public static class Build
{
    // 构建输出路径：项目根目录/Builds/项目名.exe
    private static string buildPath => Path.Combine(new DirectoryInfo(Application.dataPath).Parent.FullName,
        $"Builds/{Application.productName}.exe");

    /// <summary>
    /// 构建新客户端
    /// 完整构建流程，包含：
    /// 1. 注册 Lua 脚本到 Addressables
    /// 2. 获取构建场景列表
    /// 3. 配置构建选项
    /// 4. 执行构建
    /// </summary>
    [MenuItem("Build/NewClient")]
    public static void NewClient()
    {
        // 1. 注册 Lua 脚本
        PrepareLuaAssets();

        // 2. 获取构建场景列表
        string[] scenes = new string[EditorSceneManager.sceneCountInBuildSettings];
        for (int i = 0; i < EditorSceneManager.sceneCountInBuildSettings; i++)
        {
            scenes[i] = SceneUtility.GetScenePathByBuildIndex(i);
            Debug.Log($"添加场景{scenes[i]}");
        }

        // 3. 配置构建选项
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions()
        {
            scenes = scenes, // 包含的场景
            target = EditorUserBuildSettings.activeBuildTarget, // 目标平台
            locationPathName = buildPath, // 输出路径
            options = BuildOptions.Development | BuildOptions.AllowDebugging, // 开发模式，允许调试
        };

        // 4. 执行构建
        BuildPipeline.BuildPlayer(buildPlayerOptions);

        // 5. 输出构建路径
        Debug.unityLogger.Log(buildPath);
    }

    /// <summary>
    /// 更新客户端（增量更新）
    /// 只构建发生变化的资源，用于热更新
    /// 需要先有content_state.bin文件（由NewClient生成）
    /// </summary>
    [MenuItem("Build/UpdateClient")]
    public static void UpdateClient()
    {
        // 1. 确保 Lua 脚本已注册（改动过的脚本才会被打进更新包）
        PrepareLuaAssets();

        // 2. 获取 content_state.bin路径（记录上次构建状态）
        string path = ContentUpdateScript.GetContentStateDataPath(false);

        // 3. 获取 Addressables设置
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;

        // 4. 执行增量更新构建
        ContentUpdateScript.BuildContentUpdate(settings, path);

        Debug.Log("完成客户端更新构建");
    }

    #region Lua 热更资源

    // Lua 脚本源目录，脚本后缀必须是 .lua.txt（Unity 才能识别为 TextAsset）
    private const string LuaScriptsDir = "Assets/LuaScripts";

    // Addressables 分组名
    private const string LuaGroupName = "Lua";

    // Addressables 标签，运行时用它整组下载
    private const string LuaLabel = "Lua";

    // 脚本地址后缀：Assets/LuaScripts/main.lua.txt -> 地址 main.lua
    private const string TxtSuffix = ".txt";

    /// <summary>
    /// 把 Assets/LuaScripts 下的 Lua 脚本注册进 Addressables 的 "Lua" 分组
    /// 地址规则（只用文件名，不带目录）：
    ///     Assets/LuaScripts/main.lua.txt              -> main.lua
    ///     Assets/LuaScripts/demo/greet.lua.txt        -> greet.lua
    ///     Assets/LuaScripts/ui/start_panel.lua.txt    -> start_panel.lua
    /// 所以 Lua 里直接 require 'greet' / require 'start_panel' 即可，不用写目录。
    /// 每次改完 Lua 都要执行（打更新包时会自动执行一次）
    /// </summary>
    [MenuItem("Build/PrepareLuaAssets")]
    public static void PrepareLuaAssets()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("找不到 AddressableAssetSettings，请先初始化 Addressables");
            return;
        }

        // 1. 确保分组存在
        AddressableAssetGroup luaGroup = settings.FindGroup(LuaGroupName);
        if (luaGroup == null)
        {
            luaGroup = settings.CreateGroup(LuaGroupName, false, false, true, null);
            if (luaGroup == null)
            {
                Debug.LogError($"创建 Addressables 分组 {LuaGroupName} 失败");
                return;
            }

            Debug.Log($"已创建 Addressables 分组: {LuaGroupName}");
        }

        // 2. 确保标签存在
        settings.AddLabel(LuaLabel);

        // 3. 找到所有 Lua 脚本并注册
        string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { LuaScriptsDir });
        int count = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".lua.txt")) continue;

            // 地址 = 文件名去掉 .txt，例如：
            //   Assets/LuaScripts/demo/greet.lua.txt -> greet.lua
            string fileName = Path.GetFileName(path);
            string address = fileName.Substring(0, fileName.Length - TxtSuffix.Length);

            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, luaGroup);
            if (entry == null)
            {
                Debug.LogError($"注册 Lua 脚本失败: {path}");
                continue;
            }

            entry.SetAddress(address);
            entry.SetLabel(LuaLabel, true);

            count++;
            Debug.Log($"注册 Lua 脚本: {address}  <-  {path}");
        }

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Lua 脚本注册完成，共 {count} 个");
    }

    #endregion

    /// <summary>
    /// 列出当前 Addressables 里已注册的 Lua 地址，便于核对
    /// </summary>
    [MenuItem("Build/ListLuaAddresses")]
    public static void ListLuaAddresses()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) return;

        List<AddressableAssetEntry> entries = new List<AddressableAssetEntry>();
        settings.GetAllAssets(entries, false);
        foreach (AddressableAssetEntry entry in entries)
        {
            // 地址现在只是文件名（main.lua / greet.lua），没法按前缀认，改用 label 判断
            if (entry.labels.Contains(LuaLabel))
                Debug.Log($"{entry.address}  ->  {entry.AssetPath}");
        }
    }
}