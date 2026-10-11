using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
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

    // 已加载的脚本缓存，持有引用避免被 Addressables 回收
    private readonly Dictionary<string, TextAsset> _luaCache = new();

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

    private TextAsset LoadLuaAsset(string address)
    {
        if (_luaCache.TryGetValue(address, out TextAsset cached) && cached != null)
            return cached;

        var handle = Addressables.LoadAssetAsync<TextAsset>(address);
        TextAsset asset = handle.WaitForCompletion();
        if (asset != null)
        {
            _luaCache[address] = asset;
        }
        else
        {
            Addressables.Release(handle);
        }

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

        foreach (var kv in _luaCache)
        {
            if (kv.Value != null) Addressables.Release(kv.Value);
        }

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