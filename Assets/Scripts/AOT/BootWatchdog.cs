using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// 启动期「主线程停顿」看门狗 + 阶段面包屑。
///
/// 为什么需要它：
///   编辑器卡死分两类，现象完全不同、解法也完全不同 ——
///     A. 死循环 / 自旋等待：CPU 跑满一个核，窗口无响应；
///     B. 死锁 / 同步阻塞：CPU 几乎空闲，窗口无响应，日志停住且无任何报错。
///   只有拿到「卡住那一刻走到哪一行」才能定位。Unity 编辑器用的是 Mono，
///   dotnet-dump / procdump 都读不了它的托管栈，所以这里用最土但最可靠的办法：
///   **后台线程**持续盯着主线程的心跳，一旦停住就把「最后阶段」写进文件。
///   后台线程不受主线程阻塞影响，因此主线程卡死时它照样能记录。
///
/// 用法：
///   1. HotUpdateSystem 启动时调用一次 BootWatchdog.Start()
///   2. 主线程每帧调用一次 BootWatchdog.Beat()（XLuaManager.Update 里已接好）
///   3. 可能在启动期耗时/阻塞的地方用 Enter("阶段名") 打点
///
/// 产物：<项目>/Logs/boot_watchdog.log
/// </summary>
public static class BootWatchdog
{
    /// <summary>主线程停顿多少秒算「卡住」并记录（太小说不准，太大错过现场）</summary>
    private const float STALL_SECONDS = 3f;

    /// <summary>两次记录之间的最小间隔，避免长时间卡住时刷屏</summary>
    private const float REPORT_INTERVAL = 5f;

    private static readonly object s_lock = new object();
    private static readonly List<string> s_stack = new List<string>();

    private static string s_phase = "(尚未开始)";
    private static string s_mark = string.Empty;
    private static long s_beatMs;
    private static long s_lastReportMs;
    private static volatile bool s_running;
    private static Thread s_thread;
    private static string s_logPath;

    /// <summary>当前阶段（供外部查询/调试）</summary>
    public static string CurrentPhase
    {
        get { lock (s_lock) { return s_phase; } }
    }

    /// <summary>
    /// 启动看门狗。重复调用安全（只会启动一次）。
    /// </summary>
    public static void Start()
    {
        if (s_running) return;

        // 只在编辑器 / 开发版构建里开：正式包不需要这个诊断开销
        if (!Application.isEditor && !Debug.isDebugBuild) return;

        s_running = true;

        try
        {
            string logDir = Path.Combine(Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(logDir);
            s_logPath = Path.Combine(logDir, "boot_watchdog.log");
        }
        catch
        {
            s_logPath = null;
        }

        s_beatMs = NowMs();
        s_lastReportMs = 0;

        s_thread = new Thread(Loop)
        {
            IsBackground = true,   // 进程退出时不阻塞
            Name = "BootWatchdog",
        };
        s_thread.Start();

        Enter("BootWatchdog.Start");
    }

    /// <summary>
    /// 进入一个阶段。支持嵌套（例如 Bind → GetOrCreateFactory），
    /// 卡住时记录的是「当前所在的最内层阶段」。
    /// </summary>
    public static void Enter(string phase)
    {
        lock (s_lock)
        {
            s_stack.Add(phase);
            s_phase = JoinPhases();
        }
    }

    /// <summary>离开当前阶段</summary>
    public static void Leave()
    {
        lock (s_lock)
        {
            if (s_stack.Count > 0) s_stack.RemoveAt(s_stack.Count - 1);
            s_phase = JoinPhases();
        }
    }

    /// <summary>
    /// 标记「当前正在做的事」。不参与嵌套计数，适合跨帧的协程里打点 ——
    /// 协程有多个 yield break 早退分支时，用 Enter/Leave 很容易漏配对，Mark 则永远只是覆盖。
    /// </summary>
    public static void Mark(string phase)
    {
        lock (s_lock)
        {
            s_mark = phase ?? string.Empty;
            s_phase = JoinPhases();
        }
    }

    /// <summary>主线程心跳：每帧调一次。开销只是一次 long 写入。</summary>
    public static void Beat()
    {
        Volatile.Write(ref s_beatMs, NowMs());
    }

    private static string JoinPhases()
    {
        string stackPart = s_stack.Count == 0 ? string.Empty : string.Join(" > ", s_stack);

        if (string.IsNullOrEmpty(s_mark)) return stackPart == string.Empty ? "(空闲)" : stackPart;
        if (stackPart == string.Empty) return s_mark;
        return s_mark + " > " + stackPart;
    }

    /// <summary>
    /// 单调递增的毫秒时钟。
    ///
    /// ⚠️ 不要用 `Environment.TickCount64` —— 它是 .NET Core 3.0+ 的 API，
    /// Unity 的 .NET Standard 2.1 / .NET Framework 配置文件里都不存在（会报 CS0117）。
    /// `Stopwatch` 在所有配置下都有，且同样是单调时钟（不受系统时间调整影响）。
    /// </summary>
    private static readonly Stopwatch s_clock = Stopwatch.StartNew();

    private static long NowMs()
    {
        return s_clock.ElapsedMilliseconds;
    }

    private static void Loop()
    {
        while (s_running)
        {
            try
            {
                Thread.Sleep(500);

                long now = NowMs();
                long beat = Volatile.Read(ref s_beatMs);
                long stalledMs = now - beat;

                if (stalledMs < (long)(STALL_SECONDS * 1000f)) continue;
                if (now - Volatile.Read(ref s_lastReportMs) < (long)(REPORT_INTERVAL * 1000f)) continue;

                Volatile.Write(ref s_lastReportMs, now);

                string phase;
                lock (s_lock) { phase = s_phase; }

                string line = $"[watchdog] 主线程已停顿 {stalledMs / 1000f:0.0} 秒（CPU 空闲型阻塞或死循环）。" +
                              $"最后阶段: {phase}";

                WriteLine(line);
                TryUnityLogError(line);
            }
            catch
            {
                // 看门狗自己绝不能抛出去
            }
        }
    }

    private static void WriteLine(string line)
    {
        if (string.IsNullOrEmpty(s_logPath)) return;
        try
        {
            string stamp = DateTime.Now.ToString("HH:mm:ss.fff");
            File.AppendAllText(s_logPath, $"{stamp}  {line}{Environment.NewLine}", Encoding.UTF8);
        }
        catch
        {
            // 忽略写文件失败
        }
    }

    private static void TryUnityLogError(string line)
    {
        // 从后台线程调用 Debug.LogError 在实践中可用，但不保证线程安全，所以兜住异常
        try
        {
            //Debug.LogError(line);
        }
        catch
        {
        }
    }
}
