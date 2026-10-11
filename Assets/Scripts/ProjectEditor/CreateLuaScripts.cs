using System.IO;
using UnityEditor;

public static class CreateLuaScripts
{
    // 默认文件名
    private const string FileName = "NewLuaScript.lua";

    // 脚本默认内容
    private const string DefaultContent = "-- Lua脚本的默认内容\nprint(\"Hello, World!\")";

    [MenuItem("Assets/Create/Lua Script", false, 80)]
    public static void CreateNewLuaScript()
    {
        // 获取当前选中的对象的路径
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);

        if (path == "")
        {
            path = "Assets";
        }
        // 如果选中的对象不是文件夹，则在其所在目录下创建Lua脚本
        else if (!AssetDatabase.IsValidFolder(path))
        {
            path = Path.GetDirectoryName(path);
        }

        // 设置新Lua脚本的完整路径和名称
        path = Path.Combine(path ?? "", FileName);
        string newFilePath = AssetDatabase.GenerateUniqueAssetPath(path);

        // 创建Asset
        ProjectWindowUtil.CreateAssetWithTextContent(newFilePath, DefaultContent);
    }
}