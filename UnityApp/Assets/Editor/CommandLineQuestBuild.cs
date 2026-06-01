using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class CommandLineQuestBuild
{
    public static void BuildQuestApk()
    {
        string outputPath = GetArgument("-apkPath");
        if (string.IsNullOrWhiteSpace(outputPath))
            outputPath = "BuildTest/HandTracking_latest.apk";

        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
            throw new InvalidOperationException("No enabled scenes found in EditorBuildSettings.");

        string absoluteOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutputPath));

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        EditorUserBuildSettings.buildAppBundle = false;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        RemoveDesktopAssimpPluginsFromPackageCache();
        AssetDatabase.Refresh();
        FixPluginImportSettings.Fix();

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = absoluteOutputPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException($"Quest APK build failed: {summary.result}");

        UnityEngine.Debug.Log($"Quest APK build succeeded: {absoluteOutputPath} ({summary.totalSize} bytes)");
    }

    private static string GetArgument(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
                return args[i + 1];
        }

        return null;
    }

    private static void RemoveDesktopAssimpPluginsFromPackageCache()
    {
        string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
        string packageCache = Path.Combine(projectRoot, "Library", "PackageCache");
        if (!Directory.Exists(packageCache))
            return;

        foreach (string importerRoot in Directory.GetDirectories(packageCache, "com.unity.robotics.urdf-importer@*"))
        {
            string nativeRoot = Path.Combine(importerRoot, "Runtime", "UnityMeshImporter", "Plugins", "AssimpNet", "Native");
            if (!Directory.Exists(nativeRoot))
                continue;

            DeleteFiles(Path.Combine(nativeRoot, "win"), "assimp.dll");
            DeleteFiles(Path.Combine(nativeRoot, "linux"), "libassimp.so");
            DeleteFiles(Path.Combine(nativeRoot, "osx"), "libassimp.bundle");
        }
    }

    private static void DeleteFiles(string root, string fileName)
    {
        if (!Directory.Exists(root))
            return;

        foreach (string file in Directory.GetFiles(root, fileName, SearchOption.AllDirectories))
        {
            File.Delete(file);
            string meta = file + ".meta";
            if (File.Exists(meta))
                File.Delete(meta);

            UnityEngine.Debug.Log($"Removed desktop-only URDF Importer plugin from Quest build cache: {file}");
        }
    }
}
