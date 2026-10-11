/*
 * Tencent is pleased to support the open source community by making xLua available.
 * Copyright (C) 2016 THL A29 Limited, a Tencent company. All rights reserved.
 * Licensed under the MIT License (the "License"); you may not use this file except in compliance with the License. You may obtain a copy of the License at
 * http://opensource.org/licenses/MIT
 * Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions and limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using XLua;

// 配置的详细介绍请看 Doc 下《XLua的配置.doc》
//
// ============================ 本项目必读（踩过的坑） ============================
// 1. 不要加 typeof(UnityEngine.Object)。会生成空的 wrap 文件，导致 CS0103
//    "The name 'UnityEngineObjectWrap' does not exist"。
// 2. 【最重要】下面 [BlackList] 里那份 List<List<string>> 是 xLua 官方黑名单，
//    必须保留。它挡掉的是「编辑器里反射看得见、但打包（player）编译时不存在」的成员。
//    典型：MonoBehaviour.runInEditMode、Graphic.OnRebuildRequested（#if UNITY_EDITOR）。
//    一旦漏掉，编辑器里编译是好的，一执行 Addressables / Player 构建就会在
//    CompilePlayerScripts 阶段报 CS1061，构建直接中断。
// 3. 加类型后必须执行 XLua > Generate Code，并确认「编辑器编译」+「构建时 player 编译」都零错误。
// ================================================================================
public static class ExampleConfig
{
    // ---------------- Lua 要调用的 C# 类型 ----------------
    [LuaCallCSharp]
    public static List<Type> LuaCallCSharp = new List<Type>
    {
        // UnityEngine 基础（注意：不要加 UnityEngine.Object，见文件头说明）
        typeof(GameObject),
        typeof(Transform),
        typeof(RectTransform),
        typeof(Component),
        typeof(Behaviour),
        typeof(MonoBehaviour),

        // 常用值类型
        typeof(Vector2),
        typeof(Vector3),
        typeof(Quaternion),
        typeof(Color),

        // 工具类
        typeof(Time),
        typeof(Mathf),
        typeof(UnityEngine.Random),
        typeof(Debug),
        typeof(Application),
        typeof(Screen),
        typeof(Resources),
        typeof(UnityEngine.SceneManagement.SceneManager),

        typeof(PrimitiveType),
        typeof(Renderer),
        typeof(Material),

        // UGUI
        typeof(Button),
        typeof(Text),
        typeof(Image),
        typeof(Slider),
        typeof(Toggle),
        typeof(Canvas),
        typeof(CanvasScaler),
        typeof(GraphicRaycaster),
        typeof(UnityEngine.EventSystems.EventSystem),
        typeof(UnityEngine.EventSystems.StandaloneInputModule),
        typeof(UnityEngine.EventSystems.UIBehaviour),

        // EventTrigger 一组四个，缺一个都会生成失败
        typeof(UnityEngine.EventSystems.EventTrigger),
        typeof(UnityEngine.EventSystems.EventTrigger.Entry),
        typeof(UnityEngine.EventSystems.EventTriggerType),
        typeof(UnityEngine.EventSystems.EventTrigger.TriggerEvent),
    };

    // 加类型的注意点：
    // 1. 如果 A 类型的成员用到了 B 类型（比如 EventTrigger.Entry.eventID 的类型是 EventTriggerType），
    //    B 也必须一起列进来，否则生成 A 的 wrap 时会报
    //    "ObjectTranslator does not contain a definition for 'PushXXX'"。
    // 2. xLua 2.1.15 的生成器碰到个别类型会报
    //    "gen wrap file fail! ... attempt to index a nil value (field 'FullName')"。
    //    遇到就二分定位，把出问题的类型摘掉即可——不列也只是走反射调用，功能不受影响。
    // 3. Physics / RaycastHit / AudioSource / Animator 这些暂时没列，用到时再加，
    //    不列也能用（走反射，慢一点）。

    // ---------------- C# 要把 Lua 函数转成委托的类型 ----------------
    [CSharpCallLua]
    public static List<Type> CSharpCallLua = new List<Type>
    {
        typeof(Action),
        typeof(Action<MonoBehaviour>),      // UIManager.openPanelByName 的回调
        typeof(Action<LuaTable>),            // LuaBehaviour 生命周期回调
        typeof(Action<LuaTable, float>),     // LuaBehaviour 的 Update(dt)
        typeof(UnityAction),                 // button.onClick:AddListener(luaFunc)
        typeof(UnityAction<UnityEngine.EventSystems.BaseEventData>),  // EventTrigger 回调
        typeof(System.Collections.IEnumerator),
    };

    // ---------------- 成员黑名单（官方 + 本项目补充） ----------------
    // 匹配规则见 Generator.cs：mb.DeclaringType.ToString() == 第一项 && mb.Name == 第二项
    // 所以第一项写「声明该成员的类型全名」。像 parent 这种名字，Transform.parent 要用，
    // 只有 Material.parent 有问题，就必须写全类型名，不能只按名字挡。
    [BlackList]
    public static List<List<string>> BlackList = new List<List<string>>()
    {
        // ---- xLua 官方条目（勿删）----
        new List<string>(){"System.Xml.XmlNodeList", "ItemOf"},
        new List<string>(){"UnityEngine.WWW", "movie"},
#if UNITY_WEBGL
        new List<string>(){"UnityEngine.WWW", "threadPriority"},
#endif
        new List<string>(){"UnityEngine.Texture2D", "alphaIsTransparency"},
        new List<string>(){"UnityEngine.Security", "GetChainOfTrustValue"},
        new List<string>(){"UnityEngine.CanvasRenderer", "onRequestRebuild"},
        new List<string>(){"UnityEngine.Light", "areaSize"},
        new List<string>(){"UnityEngine.Light", "lightmapBakeType"},
        new List<string>(){"UnityEngine.WWW", "MovieTexture"},
        new List<string>(){"UnityEngine.WWW", "GetMovieTexture"},
        new List<string>(){"UnityEngine.AnimatorOverrideController", "PerformOverrideClipListCleanup"},
#if !UNITY_WEBPLAYER
        new List<string>(){"UnityEngine.Application", "ExternalEval"},
#endif
        new List<string>(){"UnityEngine.GameObject", "networkView"}, //4.6.2 not support
        new List<string>(){"UnityEngine.Component", "networkView"},  //4.6.2 not support
        new List<string>(){"System.IO.FileInfo", "GetAccessControl", "System.Security.AccessControl.AccessControlSections"},
        new List<string>(){"System.IO.FileInfo", "SetAccessControl", "System.Security.AccessControl.FileSecurity"},
        new List<string>(){"System.IO.DirectoryInfo", "GetAccessControl", "System.Security.AccessControl.AccessControlSections"},
        new List<string>(){"System.IO.DirectoryInfo", "SetAccessControl", "System.Security.AccessControl.DirectorySecurity"},
        new List<string>(){"System.IO.DirectoryInfo", "CreateSubdirectory", "System.String", "System.Security.AccessControl.DirectorySecurity"},
        new List<string>(){"System.IO.DirectoryInfo", "Create", "System.Security.AccessControl.DirectorySecurity"},
        // ↓ 编辑器专用，player 编译里不存在
        new List<string>(){"UnityEngine.MonoBehaviour", "runInEditMode"},

        // ---- 本项目按 Unity 6 补充 ----
        // ⚠️ 这里只放「属性 / 字段 / 无参方法」。带参方法写在这里不会生效（见下方说明），
        //    它们统一交给 MethodFilter 里的 EditorOnlyMembers 处理。
        new List<string>(){"UnityEngine.Material", "parent"},
        new List<string>(){"UnityEngine.Material", "isVariant"},
        // UnityEngine.UI.Graphic.OnRebuildRequested 整个方法体在 #if UNITY_EDITOR 里，Text 有 override
        new List<string>(){"UnityEngine.UI.Text", "OnRebuildRequested"},
        new List<string>(){"UnityEngine.UI.Graphic", "OnRebuildRequested"},
    };

    // ---------------- 编辑器专用成员（#if UNITY_EDITOR） ----------------
    // ⚠️ 这批成员不能靠上面的 List<List<string>> 挡！
    //    原因见 Generator.cs 的 isMethodInBlackList：它要求
    //        exclude.Count - 2 == 方法的参数个数
    //    2 个元素的条目（{"类型","名字"}）只对「无参方法」生效。而这里全是带参方法
    //    （SetPropertyLock(nameID, value)…），写进 List 里等于没写。
    //    而 isMemberInBlackList（属性/字段）没有参数校验，所以 runInEditMode 这种属性用 List 有效。
    //
    //    统一在下面的 MethodFilter 里按「声明类型全名 + 成员名」屏蔽，一次覆盖所有重载。
    //
    // 现象：编辑器编译完全正常，一执行 Addressables / Player 构建就在 CompilePlayerScripts
    //       阶段报 CS1061（错误堆栈：BuildPlayerScripts -> CompilePlayerScripts -> Failed to compile player scripts）。
    private static readonly HashSet<string> EditorOnlyMembers = new HashSet<string>
    {
        "UnityEngine.MonoBehaviour.runInEditMode",
        // Unity 6 的「材质变体 / 属性覆盖」API，整族都是 UNITY_EDITOR
        "UnityEngine.Material.parent",
        "UnityEngine.Material.isVariant",
        "UnityEngine.Material.SetPropertyLock",
        "UnityEngine.Material.ApplyPropertyOverride",
        "UnityEngine.Material.RevertPropertyOverride",
        "UnityEngine.Material.RevertAllPropertyOverrides",
        "UnityEngine.Material.IsPropertyLocked",
        "UnityEngine.Material.IsPropertyLockedByAncestor",
        "UnityEngine.Material.IsPropertyOverriden",
        "UnityEngine.Material.IsChildOf",
        "UnityEngine.UI.Text.OnRebuildRequested",
        "UnityEngine.UI.Graphic.OnRebuildRequested",
    };

    // ---------------- 方法级过滤器 ----------------
    // xLua 2.1.15 的生成器不认识 Unity 6 / .NET Standard 2.1 的新语法，下面几类方法必须屏蔽，
    // 否则生成的代码在编辑器里就编译不过：
    //   1) Span<T> / ReadOnlySpan<T> 参数或返回值 —— ref struct 不能作泛型实参（CS0306）
    //   2) in 修饰符参数 —— xLua 会生成 ref 调用，C# 9 下报 CS1615
    //   3) 编辑器专用成员（见上面 EditorOnlyMembers）—— player 编译时报 CS1061
    [BlackList]
    public static Func<MemberInfo, bool> MethodFilter = (memberInfo) =>
    {
        // 3) 编辑器专用成员：按「声明类型全名.成员名」精确屏蔽
        if (memberInfo.DeclaringType != null
            && EditorOnlyMembers.Contains(memberInfo.DeclaringType.FullName + "." + memberInfo.Name))
        {
            return true;
        }

        // 官方原有的：Dictionary<,> 的构造函数 / TryAdd / Remove 会生成非法重载
        if (memberInfo.DeclaringType != null
            && memberInfo.DeclaringType.IsGenericType
            && memberInfo.DeclaringType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            if (memberInfo.MemberType == MemberTypes.Constructor)
            {
                var ctor = memberInfo as ConstructorInfo;
                var ctorParams = ctor.GetParameters();
                if (ctorParams.Length > 0
                    && typeof(System.Collections.IEnumerable).IsAssignableFrom(ctorParams[0].ParameterType))
                {
                    return true;
                }
            }
            else if (memberInfo.MemberType == MemberTypes.Method)
            {
                var mi = memberInfo as MethodInfo;
                if (mi.Name == "TryAdd" || (mi.Name == "Remove" && mi.GetParameters().Length == 2))
                {
                    return true;
                }
            }
        }

        if (!(memberInfo is MethodInfo method)) return false;

        if (IsRefStruct(method.ReturnType)) return true;

        foreach (ParameterInfo p in method.GetParameters())
        {
            if (IsRefStruct(p.ParameterType)) return true;

            // in 参数在 IL 上就是 [In] ref，xLua 会生成 ref 调用，编译不过
            if (p.IsIn && p.ParameterType.IsByRef) return true;
        }

        return false;
    };

    private static bool IsRefStruct(Type t)
    {
        // ref struct（Span<T> / ReadOnlySpan<T>）不能作为泛型类型实参
        return t.IsValueType && t.Name.IndexOf("Span", StringComparison.Ordinal) >= 0;
    }
}
