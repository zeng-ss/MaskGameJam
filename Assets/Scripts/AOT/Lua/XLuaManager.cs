using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using XLua;

// TODO：Lua 热更管线说明
//    1. Lua 脚本源文件放在 Assets/LuaScripts 下，后缀必须是 .lua.txt（Unity 才能识别为 TextAsset）
//    2. 用菜单 Build/PrepareLuaAssets 把脚本注册进 Addressables 的 "Lua" 分组
//    3. 运行时先下载 "Lua" 分组，再调用 XLuaManager.Instance.StartLua()
//    4. Addressables 地址就是「文件名」，不带目录：
//         require "main"   -> main.lua
//         require "greet"  -> greet.lua
//       写成带点的 require "demo.greet" 也能命中 greet.lua（只取最后一段，方便兼容旧写法）

/// <summary>
/// xLua 热更新管理器
/// 负责 LuaEnv 生命周期、Lua 脚本加载（走 Addressables）和入口执行
/// </summary>
public class XLuaManager : MonoBehaviour
{
    public static XLuaManager Instance { get; private set; }

    /// <summary>默认入口模块名</summary>
    private const string DefaultEntry = "main";

    private LuaEnv _luaEnv;

    // 已加载的脚本缓存：地址（= require 的名字） -> TextAsset
    // 同时用资源名再存一份别名，两条路都能命中，持有引用避免被 Addressables 回收
    private readonly Dictionary<string, TextAsset> _luaCache = new();

    // 真正向 Addressables 借来的资源（去重），释放时按这个列表来，避免同一份资源被释放两次
    private readonly List<TextAsset> _luaOwned = new();

    public LuaEnv LuaEnv => _luaEnv;
    public bool IsInited => _luaEnv != null;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 初始化 LuaEnv 并注册自定义加载器
    /// </summary>
    public void InitLuaEnv()
    {
        if (_luaEnv != null) return;

        _luaEnv = new LuaEnv();
        _luaEnv.AddLoader(AddressablesLuaLoader);
        Debug.Log("[XLuaManager] LuaEnv 初始化完成");
    }

    /// <summary>
    /// xLua 自定义加载器：把 require 的名字映射为 Addressables 地址
    /// 地址就是文件名：require "greet" -> greet.lua
    /// 返回 null 表示本加载器无法提供该脚本，xLua 会继续尝试其它加载器
    /// </summary>
    private byte[] AddressablesLuaLoader(ref string filepath)
    {
        // 只取最后一段：require "greet" 和 require "demo.greet" 都映射到 greet.lua
        string name = filepath.Replace('.', '/');
        int slash = name.LastIndexOf('/');
        if (slash >= 0) name = name.Substring(slash + 1);

        //string address = name + ".lua";
        string address = name;
        TextAsset asset = LoadLuaAsset(address);
        if (asset == null)
        {
            Debug.LogError(
                $"[XLuaManager] 找不到 Lua 脚本，require '{filepath}' -> Addressables 地址 {address}" +
                "（是否漏跑 Build/PrepareLuaAssets，或地址与文件名不一致？）");
            return null;
        }

        return asset.bytes;
    }

    /// <summary>
    /// 预加载 Lua 分组里的全部 TextAsset，填满 _luaCache。
    ///
    /// ⚠️ 为什么必须预加载：
    ///    原来 LoadLuaAsset 走的是 Addressables.LoadAssetAsync(...).WaitForCompletion()，
    ///    而 WaitForCompletion 内部会调用 ResourceManager.Update()。
    ///    Addressables 对「在回调里再进 Update」是直接抛异常的：
    ///      Reentering the Update method is not allowed.
    ///      This can happen when calling WaitForCompletion on an operation while inside of a callback.
    ///    也就是说，只要某个 require 恰好发生在 Addressables 的回调链里
    ///    （场景加载完成、Instantiate 完成、面板加载完成的瞬间 —— 这些都是常态），
    ///    这条路径就有炸的风险。
    ///    预加载之后，运行时 require 只查字典，一次 Addressables 调用都不会发生。
    ///
    /// 时序要求：必须在「进入游戏场景之前」、且在 Addressables 回调之外（协程里）完成。
    /// </summary>
    public IEnumerator PreloadLuaAssets(string label)
    {
        _luaCache.Clear();
        _luaOwned.Clear();

        BootWatchdog.Enter($"XLuaManager.PreloadLuaAssets({label})");

        var locHandle = Addressables.LoadResourceLocationsAsync(label);
        yield return locHandle;

        if (locHandle.Status != AsyncOperationStatus.Succeeded || locHandle.Result == null)
        {
            Debug.LogWarning(
                $"[XLuaManager] 预加载 Lua 失败：找不到 label '{label}' 的资源定位" +
                "（是否漏跑 Build/PrepareLuaAssets，或没重新构建 Addressables？）");
            BootWatchdog.Leave();
            yield break;
        }

        int count = 0;
        foreach (var loc in locHandle.Result)
        {
            var assetHandle = Addressables.LoadAssetAsync<TextAsset>(loc);
            yield return assetHandle;

            if (assetHandle.Status == AsyncOperationStatus.Succeeded && assetHandle.Result != null)
            {
                TextAsset asset = assetHandle.Result;
                // 地址就是文件名（require 的名字），这是主键
                _luaCache[loc.PrimaryKey] = asset;
                // 再按 Unity 资源名兜一层（TextAsset 的 name 是 "xxx.lua"）
                if (!_luaCache.ContainsKey(asset.name)) _luaCache[asset.name] = asset;
                _luaOwned.Add(asset);
                count++;
            }
            else
            {
                Debug.LogWarning($"[XLuaManager] Lua 脚本加载失败: {loc.PrimaryKey}");
                Addressables.Release(assetHandle);
            }
        }

        Addressables.Release(locHandle);
        BootWatchdog.Leave();

        Debug.Log($"[XLuaManager] Lua 脚本预加载完成：{count} 个（label: {label}）");
    }

    /// <summary>
    /// 取一个已预加载的 Lua 脚本。正常路径**只查字典**，不碰 Addressables。
    /// </summary>
    private TextAsset LoadLuaAsset(string address)
    {
        if (_luaCache.TryGetValue(address, out TextAsset cached) && cached != null)
            return cached;

        // 兜底：预加载漏掉了（例如运行期新增脚本）。这里仍然会同步等待，
        // 正常流程不该走到这里 —— 走到就说明预加载没覆盖到，会打警告提示。
        BootWatchdog.Enter($"XLuaManager.LoadLuaAsset 同步兜底({address})");

        Debug.LogWarning(
            $"[XLuaManager] Lua 脚本 '{address}' 不在预加载缓存里，正在同步兜底加载。" +
            "请确认 PreloadLuaAssets 覆盖了全部脚本。");

        var handle = Addressables.LoadAssetAsync<TextAsset>(address);
        TextAsset asset = handle.WaitForCompletion();
        if (asset != null)
        {
            _luaCache[address] = asset;
            if (!_luaCache.ContainsKey(asset.name)) _luaCache[asset.name] = asset;
            if (!_luaOwned.Contains(asset)) _luaOwned.Add(asset);
        }
        else
        {
            Addressables.Release(handle);
        }

        BootWatchdog.Leave();
        return asset;
    }

    /// <summary>
    /// 启动 Lua 入口，等价于 Lua 里的 require "main"
    /// </summary>
    public object[] StartLua(string entry = DefaultEntry)
    {
        if (_luaEnv == null)
        {
            Debug.LogError("[XLuaManager] LuaEnv 未初始化，请先调用 InitLuaEnv()");
            return null;
        }

        object[] ret = _luaEnv.DoString($"require '{entry}'");
        Debug.Log($"[XLuaManager] Lua 入口执行完成: require '{entry}'");
        return ret;
    }

    /// <summary>
    /// 直接执行一段 Lua 代码
    /// </summary>
    public object[] DoString(string chunk, string chunkName = "chunk")
    {
        return _luaEnv?.DoString(chunk, chunkName);
    }

    /// <summary>
    /// 获取 Lua 全局变量
    /// </summary>
    public T GetGlobal<T>(string name) => _luaEnv.Global.Get<T>(name);

    /// <summary>
    /// 设置 Lua 全局变量
    /// </summary>
    public void SetGlobal<T>(string name, T value) => _luaEnv.Global.Set(name, value);

    private void Update()
    {
        // 给「主线程停顿看门狗」喂心跳：卡住时后台线程才能把现场写进
        // Logs/boot_watchdog.log（见 BootWatchdog）
        BootWatchdog.Beat();

        // 驱动 Lua 侧的 GC，必须定期调用
        _luaEnv?.Tick();
    }

    private void OnDestroy()
    {
        Dispose();
    }

    private void OnApplicationQuit()
    {
        Dispose();
    }

    private void Dispose()
    {
        if (_luaEnv == null) return;

        // 1. 先释放所有 Lua 绑定：清掉 C# 侧持有的 LuaTable / 生命周期委托引用。
        //    否则 xLua 认为「还有 C# 回调挂着」，LuaEnv.Dispose() 会直接抛
        //    InvalidOperationException: try to dispose a LuaEnv with C# callback!
        LuaBinding.DisposeAll();

        // 按「去重后的资源列表」释放，避免同一份 TextAsset 被释放两次
        // （缓存里同时存了地址和资源名两个键，都指向同一个对象）
        foreach (TextAsset asset in _luaOwned)
        {
            if (asset != null) Addressables.Release(asset);
        }

        _luaOwned.Clear();
        _luaCache.Clear();

        // 2. 再释放 LuaEnv。
        //    退出阶段可能还有零星回调没被回收（例如没来得及 kill 的 DOTween 回调挂在 Lua 闭包上），
        //    此时 xLua 仍会拒绝释放 —— 进程马上就要结束，记一条警告即可，别再往上抛。
        try
        {
            _luaEnv.Dispose();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[XLuaManager] LuaEnv 释放时仍有未回收的 C# 回调（退出阶段可忽略）: {e.Message}");
        }

        _luaEnv = null;
        Instance = null;
        Debug.Log("[XLuaManager] LuaEnv 已释放");
    }
}