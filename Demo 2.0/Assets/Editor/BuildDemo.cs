#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Turns the demo into a normal app (an .exe, a Mac app or a Linux program) at your screen's full resolution.
/// Menu: Demo > ... . The game builds everything at runtime, so the only things a build needs are one scene and the
/// shaders the game looks up by name (Shader.Find), which Unity would otherwise leave out of the build.
/// From the command line: Unity -batchmode -quit -projectPath . -executeMethod BuildDemo.BuildWindows
/// </summary>
public static class BuildDemo
{
    const string SceneFolder = "Assets/Scenes";
    const string ScenePath = "Assets/Scenes/Demo.unity";
    const string AppName = "Poker dhe Blackjack";

    static readonly string[] ShaderNames =
    {
        "Universal Render Pipeline/Lit", "Universal Render Pipeline/Simple Lit", "Universal Render Pipeline/Unlit",
        "Standard", "HDRP/Lit", "Sprites/Default", "Unlit/Color", "UI/Default"
    };

    [MenuItem("Demo/Ndërto aplikacionin për Windows (.exe)")]
    public static void BuildWindows() { Build(BuildTarget.StandaloneWindows64, "Windows", AppName + ".exe"); }

    [MenuItem("Demo/Ndërto aplikacionin për Mac")]
    public static void BuildMac() { Build(BuildTarget.StandaloneOSX, "Mac", AppName + ".app"); }

    [MenuItem("Demo/Ndërto aplikacionin për Linux")]
    public static void BuildLinux() { Build(BuildTarget.StandaloneLinux64, "Linux", "PokerDemo.x86_64"); }

    [MenuItem("Demo/Vetëm përgatit projektin (skena, shader-at, cilësimet)")]
    public static void PrepareOnly()
    {
        Prepare();
        Tell("Projekti u përgatit. Tani mund të përdorësh File > Build Settings, ose menunë Demo për të ndërtuar aplikacionin.");
    }

    static void Build(BuildTarget target, string folder, string fileName)
    {
        Prepare();
        string dir = Path.Combine("Builds", folder);
        Directory.CreateDirectory(dir);
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = Path.Combine(dir, fileName),
            target = target,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded)
        {
            Tell("Aplikacioni u ndërtua:\n" + options.locationPathName);
            if (!Application.isBatchMode) EditorUtility.RevealInFinder(options.locationPathName);
        }
        else
        {
            Tell("Ndërtimi dështoi (" + report.summary.result + "). Shiko Console. Nëse mungon moduli për këtë platformë, shtoje nga Unity Hub > Installs > Add modules.");
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    static void Tell(string message)
    {
        Debug.Log("[Demo] " + message);
        if (!Application.isBatchMode) EditorUtility.DisplayDialog("Demo", message, "Në rregull");
    }

    /// <summary>One scene, the shaders in the build, and window settings for a sharp, resizable full-screen app.</summary>
    static void Prepare()
    {
        // 1. a scene with a camera and a light; the game itself starts from RuntimeInitializeOnLoadMethod
        if (!Directory.Exists(SceneFolder)) AssetDatabase.CreateFolder("Assets", "Scenes");
        if (!File.Exists(ScenePath))
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        // 2. the shaders the game finds by name must be in the build
        var graphics = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
        if (graphics != null)
        {
            var so = new SerializedObject(graphics);
            SerializedProperty list = so.FindProperty("m_AlwaysIncludedShaders");
            if (list != null)
            {
                foreach (string name in ShaderNames)
                {
                    Shader s = Shader.Find(name);
                    if (s == null) continue;
                    bool there = false;
                    for (int i = 0; i < list.arraySize; i++)
                        if (list.GetArrayElementAtIndex(i).objectReferenceValue == s) { there = true; break; }
                    if (there) continue;
                    list.InsertArrayElementAtIndex(list.arraySize);
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = s;
                }
                so.ApplyModifiedProperties();
            }
        }

        // 3. window: starts borderless full screen at the monitor's own resolution, can be resized, keeps running when unfocused
        PlayerSettings.productName = AppName;
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;

        AssetDatabase.SaveAssets();
    }
}
#endif
