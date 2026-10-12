using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;

// TODO：资源热更新流程总结：
//    1. 初始化 → 2. 检查 catalog 更新 → 3. 更新 catalog → 4. 计算下载大小 → 5. 下载资源 → 6. 进入游戏
// TODO：关键概念解释：
//    Catalog（目录）：Addressables的索引文件，记录所有资源的位置、依赖关系和版本信息
//    ResourceLocator（资源定位器）：用于定位和加载具体资源
//    AsyncOperationHandle（异步操作句柄）：管理异步操作的状态、进度和结果，必须正确释放
//    断点续传：通过PlayerPrefs记录更新状态，失败时清理缓存重新下载

public class HotUpdateSystem : MonoBehaviour
{
    private IHotUpdateWindow _hotUpdateWindow; // 热更新窗口接口引用，用于显示下载进度
    private const string DLLConfigKey = "DllConfig"; // DLL配置文件的 Addressables Key
    private const string GameLanuchKey = "GameLanuch"; // 游戏启动器的 Addressables Key
    private const string HotUpdateWindowKey = "HotUpdateWindow"; // 热更新窗口的 Addressable Key
    private const string LuaGroupKey = "Lua"; // Lua脚本分组的 Addressables Label

    [Header("调试开关")] [Tooltip("勾上后，即使没有需要下载的资源，也强制把热更窗口显示一次（仅用于预览 UI；正式发包请保持关闭）")]
    public bool debugForceHotUpdateWindow = false;

    // 异步读取到的版本信息（见 LoadVersionInfo），展示到热更窗口上
    private string _cachedVersionInfo;
    private string _cachedVersion;

    private string PersistentDataPath =>
        $"{Application.persistentDataPath}/com.unity.addressables"; // 持久化数据路径，Addressables的缓存目录

    private void Start()
    {
        // 启动「主线程停顿看门狗」。
        // 编辑器卡死有两类：死循环（CPU 满）和死锁/同步阻塞（CPU 空闲、日志停住、无报错）。
        // 后者最难查 —— 因为主线程卡住后连 Debug.Log 都写不出来。
        // 看门狗跑在后台线程上，主线程卡住时它照样能把「最后阶段」写进
        // Logs/boot_watchdog.log，这样看一眼日志就知道卡在哪一行。
        BootWatchdog.Start();

        // 构建 Addressables 缓存目录路径
        // 格式：C:\Users\用户名\AppData\LocalLow\公司名\应用名\com.unity.addressables
        StartCoroutine(HotUpdate()); // 开始热更新流程
    }

    /// <summary>
    /// 热更新主流程协程
    /// 这是整个热更新系统的核心入口，控制所有热更新步骤的顺序
    /// </summary>
    private IEnumerator HotUpdate()
    {
        // 第一步：断点续传处理
        // 从 PlayerPrefs读取上次热更新状态
        int hotUpdateState = PlayerPrefs.GetInt("HotUpdateSucceed", 0);
        if (hotUpdateState == 0) // 0代表上一次热更失败
        {
            print("断点续传 - 检测到上次热更新失败，清理缓存");
            //Addressables.LoadContentCatalogAsync(persistentDataPath);  // 可选的：加载本地的catalog
            // 清理缓存目录，确保重新下载
            if (Directory.Exists(PersistentDataPath))
            {
                Directory.Delete(PersistentDataPath, true); // 递归删除整个目录
            }
        }

        // 设置当前热更新状态为"进行中"
        PlayerPrefs.SetInt("HotUpdateSucceed", 0);

        //第二步：初始化 Addressables系统 
        yield return Addressables.InitializeAsync(); // 初始化 Addressables，必须调用

        // 第三步：检测 catalog更新 
        yield return CheckForCatalogUpdates();

        // 第四步：标记热更新完成
        PlayerPrefs.SetInt("HotUpdateSucceed", 1); // 更新成功，设置为1

        // 第五步：加载 Lua脚本并启动 Lua入口（Lua热更）
        // 无论是否热更，都需要进入此处
        yield return LoadLuaScripts();

        // 【调试】没有需要下载的资源时，也强制把热更窗口走一遍，方便预览它的样子。
        // 正常流程下窗口只在「算出要下载的字节数 > 0」时才会被创建 ——
        // 本地 catalog 已是最新时根本看不到它（这就是它"没加载出来"的原因）。
        if (_hotUpdateWindow == null && debugForceHotUpdateWindow)
        {
            yield return ShowHotUpdateWindowForDebug();
            yield break; // 不自动进游戏，等玩家点窗口上的「进入游戏」
        }

        if (_hotUpdateWindow == null) // 如果有窗口就按照窗口的进度来，如果没有窗口就直接开始游戏
        {
            yield return GameLanuchCoroutine(); // 直接启动游戏（异步等待，见方法注释）
            // 这样开始游戏也可以通过什么东西（组件）来实现开始逻辑    Addressables.InstantiateAsync("xxx").WaitForCompletion();
        }
    }

    /// <summary>
    /// 【调试用】强制显示热更窗口，方便在「没有真实更新」时预览 UI。
    ///
    /// 正常流程里热更窗口只在两件事同时成立时才被创建：
    ///   1. CheckForCatalogUpdates 发现远程 catalog 与本地不同
    ///   2. GetDownloadSizeAsync 算出要下载的字节数 > 0
    /// 所以本地开发时基本看不到它。勾上 HotUpdateSystem 上的
    /// debugForceHotUpdateWindow 就会走这里：直接把窗口拉出来并置成 100%，
    /// 走满进度后出现「进入游戏」按钮，点它同样能进游戏。
    /// </summary>
    private IEnumerator ShowHotUpdateWindowForDebug()
    {
        Debug.Log("[HotUpdateSystem] 调试开关开启：强制显示热更窗口（仅用于预览 UI）");

        AsyncOperationHandle<GameObject> windowHandle = Addressables.InstantiateAsync(HotUpdateWindowKey);
        yield return windowHandle;

        if (windowHandle.Status != AsyncOperationStatus.Succeeded || windowHandle.Result == null)
        {
            Debug.LogError(
                $"[HotUpdateSystem] 热更窗口加载失败（地址 '{HotUpdateWindowKey}'）: " +
                $"{windowHandle.OperationException?.Message}");
            yield break;
        }

        _hotUpdateWindow = windowHandle.Result.GetComponent<IHotUpdateWindow>();
        if (_hotUpdateWindow == null)
        {
            Debug.LogError($"[HotUpdateSystem] 地址 '{HotUpdateWindowKey}' 的预制体上没有 IHotUpdateWindow 组件");
            yield break;
        }

        // 故意不去读远程 VersionInfo：那两个方法内部用的是 WaitForCompletion，
        // 远程不通时会阻塞主线程。预览用直接写死一行说明即可。
        _hotUpdateWindow.GetVersion("调试模式", "强制显示热更窗口（未读取真实版本信息）");
        // 200MB 是假数据，只是让进度文本看起来自然些
        _hotUpdateWindow.Show(200L * 1024 * 1024, () => StartCoroutine(GameLanuchCoroutine()));
        _hotUpdateWindow.UpdateDownloadProgress(1f); // 直接满进度 → 窗口 Update 里会亮出「进入游戏」按钮
    }

    /// <summary>
    /// 检测catalog目录更新 catalog是Addressables的目录文件，记录所有资源的位置、依赖关系和版本信息
    /// 检查是否有新的资源版本需要下载
    /// </summary>
    private IEnumerator CheckForCatalogUpdates()
    {
        // 检查是否有新的 catalog版本可用
        // false参数表示只检查不自动更新
        AsyncOperationHandle<List<string>> checkForCatalogUpdatesHandle =
            Addressables.CheckForCatalogUpdates(false); // false: 不自动更新，只检查
        yield return checkForCatalogUpdatesHandle; // 等待检查完成

        if (checkForCatalogUpdatesHandle.Status != AsyncOperationStatus.Succeeded)
        {
            // 检查失败，抛出异常
            Debug.LogError($"CheckForCatalogUpdates 失败: {checkForCatalogUpdatesHandle.OperationException}");
        }
        else
        {
            Debug.Log("CheckForCatalogUpdates成功 - 已检查到catalog更新状态");
            // 拿到需要更新的 catalog列表
            List<string> catalogResult = checkForCatalogUpdatesHandle.Result;

            if (catalogResult.Count > 0) // 有 catalog需要更新
            {
                // foreach (var result in catalogResult) { print(result); }  // 打印需要更新的 catalog名称
                yield return UpdateCatalogs(catalogResult); // 更新 catalog
            }
            else // 没有需要更新的 catalog
            {
                print("无需更新 - catalog已是最新版本");
            }
        }

        // 释放Addressables操作句柄，避免内存泄漏
        Addressables.Release(checkForCatalogUpdatesHandle);
    }

    /// <summary>
    /// 更新catalog目录 当检测到有catalog需要更新时，下载最新的catalog文件
    /// </summary>
    /// <param name="catalogResult">需要更新的catalog名称列表</param>
    private IEnumerator UpdateCatalogs(List<string> catalogResult)
    {
        // 更新指定的 catalog
        // false参数表示不自动下载资源，只更新catalog
        AsyncOperationHandle<List<IResourceLocator>> updateCatalogHandle =
            Addressables.UpdateCatalogs(catalogResult, false); // false: 不自动下载资源
        yield return updateCatalogHandle; // 等待更新完成

        if (updateCatalogHandle.Status != AsyncOperationStatus.Succeeded)
        {
            // 更新失败，抛出异常
            Debug.LogError($"UpdateCatalogs 失败: {updateCatalogHandle.OperationException}");
        }
        else
        {
            Debug.Log("UpdateCatalogs 成功 - catalog已更新");
            // 获取更新后的资源定位器列表
            List<IResourceLocator> locatorList = updateCatalogHandle.Result;

            if (locatorList.Count > 0)
            {
                // 收集所有资源的 Key（标识符）
                List<object> keys = new List<object>(1000); // 预分配容量
                foreach (var result in locatorList)
                {
                    // print(result.LocatorId);  // 打印定位器 ID
                    keys.AddRange(result.Keys); // 添加所有资源 Key
                }

                // ====== 第四步：计算需要下载的资源大小 ======
                yield return GetDownloadSize(keys);
            }
        }

        // 释放Addressables操作句柄
        Addressables.Release(updateCatalogHandle);
    }

    /// <summary>
    /// 获取需要下载的资源总大小 根据收集的资源Key，计算总共需要下载多少数据
    /// </summary>
    /// <param name="keys">所有需要下载的资源Key集合</param>
    private IEnumerator GetDownloadSize(IEnumerable<object> keys)
    {
        // 计算指定资源的总下载大小
        AsyncOperationHandle<long> sizeHandle = Addressables.GetDownloadSizeAsync(keys);
        yield return sizeHandle; // 等待计算完成

        if (sizeHandle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError($"GetDownloadSize 失败: {sizeHandle.OperationException}");
        }
        else
        {
            long downloadSize = sizeHandle.Result; // 获取总大小（字节）
            if (downloadSize > 0) // 有资源需要下载
            {
                // 先执行优先热更（下载并加载必要的 DLL和 UI）
                yield return PriorityHotUpdate();
                // 显示热更新窗口，并传入总下载大小和完成回调
                _hotUpdateWindow.Show(downloadSize, () => { StartCoroutine(GameLanuchCoroutine()); });
                // 版本信息
                yield return LoadVersionInfo();
                _hotUpdateWindow.GetVersion(
                    string.IsNullOrEmpty(_cachedVersion) ? "未知" : _cachedVersion,
                    string.IsNullOrEmpty(_cachedVersionInfo) ? "未知" : _cachedVersionInfo);

                // 开始下载资源 
                yield return DownloadDependencies(keys, downloadSize);
            }
            else
            {
                print("无需更新 - 没有需要下载的资源");
            }
        }

        // 释放Addressables操作句柄
        Addressables.Release(sizeHandle);
    }

    /// <summary>
    /// 下载具体资源
    /// 实际执行资源下载，并更新进度显示
    /// </summary>
    /// <param name="keys">资源Key集合</param>
    /// <param name="downloadSize">总下载大小（字节）</param>
    private IEnumerator DownloadDependencies(IEnumerable<object> keys, long downloadSize)
    {
        // 开始下载资源依赖
        // MergeMode.Union表示合并所有资源的依赖
        // false表示异步下载
        AsyncOperationHandle downloadHandle =
            Addressables.DownloadDependenciesAsync(keys, Addressables.MergeMode.Union, false);

        // ====== 循环监控下载进度 ======
        while (!downloadHandle.IsDone) // 下载未完成时循环
        {
            if (downloadHandle.Status == AsyncOperationStatus.Failed)
            {
                Debug.LogError($"DownloadDependencies 失败: {downloadHandle.OperationException}");
                break; // 下载失败，跳出循环
            }
            // 获取当前下载进度
            // float percentage = downloadHandle.GetDownloadStatus().Percent;  // 下载进度（0 ~ 1）
            // long currentDownloadSize = (long)(downloadSize * percentage);  // 当前已下载量（字节）

            // 更新 UI显示：传入下载进度百分比
            _hotUpdateWindow.UpdateDownloadProgress(downloadHandle.GetDownloadStatus().Percent);
            yield return null; // 等待下一帧继续检查
        }

        // 确保进度显示为100%
        _hotUpdateWindow?.UpdateDownloadProgress(1);
        print("热更完成! - 所有资源已下载完成");
    }

    // 优先热更
    private IEnumerator PriorityHotUpdate()
    {
        // 1. 先下载 dll配置文件
        // true表示强制重新下载
        yield return Addressables.DownloadDependenciesAsync(DLLConfigKey, true);

        // 6. 下载并加载热更新窗口
        // 因为热更新窗口本身也是 Addressable资源，需要先下载才能显示
        // 6.1 先下载热更新窗口所需的资源依赖
        yield return Addressables.DownloadDependenciesAsync(HotUpdateWindowKey, true);

        // 6.2 实例化热更新窗口 GameObject
        AsyncOperationHandle<GameObject> windowHandle = Addressables.InstantiateAsync(HotUpdateWindowKey);
        yield return windowHandle;

        if (windowHandle.Status != AsyncOperationStatus.Succeeded || windowHandle.Result == null)
        {
            Debug.LogError(
                $"[HotUpdateSystem] 热更窗口实例化失败（地址 '{HotUpdateWindowKey}'）: " +
                $"{windowHandle.OperationException?.Message}");
            yield break;
        }

        _hotUpdateWindow = windowHandle.Result.GetComponent<IHotUpdateWindow>();
        if (_hotUpdateWindow == null)
        {
            Debug.LogError(
                $"[HotUpdateSystem] 地址 '{HotUpdateWindowKey}' 的预制体上没有实现 IHotUpdateWindow 的组件");
        }
    }

    /// <summary>
    /// 下载 Lua脚本分组并启动 xLua 环境
    /// 1. 下载所有 Lua 脚本资源
    /// 2. 创建 XLuaManager 并初始化 LuaEnv
    /// 3. 执行 Lua 入口 require 'main'
    /// Lua 失败不应阻断后续进入游戏，所以这里捕获异常
    /// </summary>
    private IEnumerator LoadLuaScripts()
    {
        // 1. 下载 Lua 分组
        //    ⚠️ DownloadDependenciesAsync 的第二个参数是 autoReleaseHandle，这里必须传 false！
        //    传 true 时句柄会在操作完成后被立刻自动释放，紧接着访问 .Status 就会抛
        //    "Attempting to use an invalid operation handle"。
        //    编辑器 Play / 本地模式（Use Asset Database）下该操作会同步完成，
        //    等于「刚拿到句柄就被释放」，所以这个异常是必现的。
        AsyncOperationHandle luaHandle = Addressables.DownloadDependenciesAsync(LuaGroupKey, false);
        yield return luaHandle;

        if (!luaHandle.IsValid())
        {
            // key / label 在 catalog 里根本不存在时会拿到无效句柄（没执行过 Build/PrepareLuaAssets）
            Debug.LogWarning(
                $"[HotUpdateSystem] 找不到 Lua 分组（label: {LuaGroupKey}），" +
                "请先执行菜单 Build/PrepareLuaAssets，然后重新构建 Addressables");
        }
        else
        {
            if (luaHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogWarning(
                    $"[HotUpdateSystem] Lua 分组下载失败: {luaHandle.OperationException?.Message}");
            }

            // 因为上面传了 autoReleaseHandle=false，这里必须手动释放一次
            Addressables.Release(luaHandle);
        }

        // 2. 创建管理器并初始化 LuaEnv
        BootWatchdog.Enter("HotUpdateSystem 创建 XLuaManager");
        GameObject luaGo = new GameObject("XLuaManager", typeof(XLuaManager));
        XLuaManager luaManager = luaGo.GetComponent<XLuaManager>();
        luaManager.InitLuaEnv();
        BootWatchdog.Leave();

        // 2.5 预加载全部 Lua 脚本。
        //     必须在「进游戏场景之前」、且在 Addressables 回调之外（这里是协程）完成。
        //     预加载之后运行时 require 只查字典，不再有 Addressables 同步等待，
        //     也就不会踩到 "Reentering the Update method is not allowed"（在回调里 WaitForCompletion）。
        yield return luaManager.PreloadLuaAssets(LuaGroupKey);

        // 3. 执行 Lua 入口
        BootWatchdog.Enter("HotUpdateSystem 执行 Lua 入口 main");
        luaManager.StartLua();
        BootWatchdog.Leave();
    }

    /// <summary>
    /// 启动游戏
    /// </summary>
    private IEnumerator GameLanuchCoroutine()
    {
        BootWatchdog.Enter("GameLanuchCoroutine 实例化常驻单例");
        Addressables.InstantiateAsync("SoundAudioPool");
        Addressables.InstantiateAsync("GameManager");
        // 清理热更新窗口资源
        if (_hotUpdateWindow != null)
        {
            // 先转换为 Component获取 GameObject，然后释放 Addressables资源
            Addressables.Release(((Component)_hotUpdateWindow).gameObject);
        }

        // 加载游戏主场景
        // 通过 Addressables实例化 GameLanuch对象，它会负责加载游戏场景
        BootWatchdog.Enter("GameLanuchCoroutine 实例化 GameLanuch");
        yield return Addressables.InstantiateAsync(GameLanuchKey);
        BootWatchdog.Leave();
        BootWatchdog.Leave();
    }

    /// <summary>
    /// 异步读取版本信息 / 版本号。
    /// </summary>
    private IEnumerator LoadVersionInfo()
    {
        _cachedVersionInfo = null;
        _cachedVersion = null;

        BootWatchdog.Enter("HotUpdateSystem 读取 VersionInfo");
        AsyncOperationHandle<TextAsset> infoHandle = Addressables.LoadAssetAsync<TextAsset>("VersionInfo");
        yield return infoHandle;
        if (infoHandle.Status == AsyncOperationStatus.Succeeded && infoHandle.Result != null)
        {
            _cachedVersionInfo = infoHandle.Result.text;
        }
        else
        {
            Debug.LogWarning($"[HotUpdateSystem] 读取 VersionInfo 失败: {infoHandle.OperationException?.Message}");
        }

        Addressables.Release(infoHandle);

        BootWatchdog.Enter("HotUpdateSystem 读取 Version");
        AsyncOperationHandle<TextAsset> versionHandle = Addressables.LoadAssetAsync<TextAsset>("Version");
        yield return versionHandle;
        if (versionHandle.Status == AsyncOperationStatus.Succeeded && versionHandle.Result != null)
        {
            _cachedVersion = versionHandle.Result.text;
        }
        else
        {
            Debug.LogWarning($"[HotUpdateSystem] 读取 Version 失败: {versionHandle.OperationException?.Message}");
        }

        Addressables.Release(versionHandle);

        BootWatchdog.Leave();
        BootWatchdog.Leave();
    }
}