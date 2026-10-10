using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// 윈도우 64비트 빌드 메뉴. 유니티 상단 메뉴의 Sand Bingo 에서 실행
// Steam 빌드: Steamworks 에 업로드
// GitHub 빌드: Steam 코드를 빼고 빌드, GitHub Release 로 배포하는 용도
public static class BuildMenu
{
    private const string STEAM_FOLDER_KEY = "SandBingo.BuildMenu.SteamFolder";
    private const string GITHUB_FOLDER_KEY = "SandBingo.BuildMenu.GitHubFolder";
    private const string DISABLE_STEAM_SYMBOL = "DISABLESTEAMWORKS";

    [MenuItem("Sand Bingo/Build Steam Version")]
    static void BuildSteamVersion()
    {
        string projectSymbols = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);
        if (projectSymbols.Split(';').Contains(DISABLE_STEAM_SYMBOL))
        {
            Debug.LogError($"[Build] Remove {DISABLE_STEAM_SYMBOL} from Player Settings before building the Steam version.");
            return;
        }

        string folder = AskFolder("Steam", STEAM_FOLDER_KEY);
        if (folder == null) return;

        Build(folder, null, false);
    }

    [MenuItem("Sand Bingo/Build GitHub Version (No Steam)")]
    static void BuildGitHubVersion()
    {
        string folder = AskFolder("GitHub", GITHUB_FOLDER_KEY);
        if (folder == null) return;

        Build(folder, new[] { DISABLE_STEAM_SYMBOL }, true);
    }

    // 마지막으로 고른 폴더를 기억
    static string AskFolder(string label, string prefsKey)
    {
        string lastFolder = EditorPrefs.GetString(prefsKey, "");
        string folder = EditorUtility.SaveFolderPanel($"Choose {label} build folder", lastFolder, "");

        if (string.IsNullOrEmpty(folder)) return null;

        EditorPrefs.SetString(prefsKey, folder);
        return folder;
    }

    static void Build(string folder, string[] extraDefines, bool removeSteamFiles)
    {
        string exePath = Path.Combine(folder, PlayerSettings.productName + ".exe");

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
            locationPathName = exePath,
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None
        };

        if (extraDefines != null)
            options.extraScriptingDefines = extraDefines;

        BuildReport report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"[Build] Failed: {report.summary.result}");
            return;
        }

        if (removeSteamFiles)
            RemoveSteamFiles(folder);

        RemoveBurstDebugFolder(folder);

        Debug.Log($"[Build] Succeeded: {exePath}");
        EditorUtility.RevealInFinder(exePath);
    }

    // Burst 패키지가 빌드할 때 함께 만드는 디버그 정보 폴더 제거
    // 빌드에 따라 생기지 않을 때도 있음
    static void RemoveBurstDebugFolder(string folder)
    {
        string burstFolder = Path.Combine(folder, PlayerSettings.productName + "_BurstDebugInformation_DoNotShip");
        if (!Directory.Exists(burstFolder)) return;

        Directory.Delete(burstFolder, true);
        Debug.Log($"[Build] Removed {burstFolder}");
    }

    // Steam 코드가 빠진 빌드에서는 불러오지 않는 파일
    // 폴더 전체가 아니라 안에서만 찾아 지움
    static void RemoveSteamFiles(string folder)
    {
        string pluginsFolder = Path.Combine(folder, PlayerSettings.productName + "_Data", "Plugins");
        if (!Directory.Exists(pluginsFolder)) return;

        foreach (string file in Directory.GetFiles(pluginsFolder, "steam_api*", SearchOption.AllDirectories))
        {
            File.Delete(file);
            Debug.Log($"[Build] Removed {file}");
        }
    }
}
