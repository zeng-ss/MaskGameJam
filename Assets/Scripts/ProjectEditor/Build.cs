using UnityEditor;
using UnityEngine;
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
    private static string buildPath => Path.Combine(new DirectoryInfo(Application.dataPath).Parent.FullName, $"Builds/{Application.productName}.exe");

    /// <summary>
    /// 构建新客户端
    /// 完整构建流程，包含：
    /// 1. 获取构建场景列表
    /// 2. 配置构建选项
    /// 3. 执行构建
    /// </summary>
    [MenuItem("Build/NewClient")]
    public static void NewClient()
    {
        // 1. 获取构建场景列表
        string[] scenes = new string[EditorSceneManager.sceneCountInBuildSettings];
        for (int i = 0; i < EditorSceneManager.sceneCountInBuildSettings; i++)
        {
            scenes[i] = SceneUtility.GetScenePathByBuildIndex(i);
            Debug.Log($"添加场景{scenes[i]}");
        }

        // 2. 配置构建选项
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions()
        {
            scenes = scenes,  // 包含的场景
            target = EditorUserBuildSettings.activeBuildTarget,  // 目标平台
            locationPathName = buildPath,  // 输出路径
            options = BuildOptions.Development | BuildOptions.AllowDebugging,  // 开发模式，允许调试
        };
        
        // 3. 执行构建
        BuildPipeline.BuildPlayer(buildPlayerOptions);
        
        // 4. 输出构建路径
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
        // 1. 获取 content_state.bin路径（记录上次构建状态）
        string path = ContentUpdateScript.GetContentStateDataPath(false);
        
        // 2. 获取 Addressables设置
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        
        // 3. 执行增量更新构建
        ContentUpdateScript.BuildContentUpdate(settings, path);
        
        Debug.Log("完成客户端更新构建");
    }
}