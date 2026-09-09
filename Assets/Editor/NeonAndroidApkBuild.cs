using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Builds a self-contained phone-test APK without enabling development tools.</summary>
[InitializeOnLoad]
public static class NeonAndroidApkBuild
{
    private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    private static readonly string RequestPath = Path.Combine(ProjectRoot, ".utmp", "neon-android-build.request");
    private static readonly string StatusPath = Path.Combine(ProjectRoot, ".utmp", "neon-android-build-status.json");
    private static double nextPoll;
    private static bool building;

    [Serializable]
    private sealed class BuildStatus
    {
        public string state;
        public string apk;
        public string package;
        public string version;
        public string architecture;
        public string backend;
        public string message;
        public string utc;
        public long bytes;
        public int warnings;
        public int errors;
    }

    static NeonAndroidApkBuild() => EditorApplication.update += Poll;

    [MenuItem("Neon Reflex/Build/Android APK")]
    public static void RequestBuild()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RequestPath));
        File.WriteAllText(RequestPath, "Build a non-development Android APK.");
    }

    private static void Poll()
    {
        if (building || EditorApplication.timeSinceStartup < nextPoll ||
            EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;
        nextPoll = EditorApplication.timeSinceStartup + 0.5;
        if (!File.Exists(RequestPath)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            return;
        }
        File.Delete(RequestPath);
        Build();
    }

    private static void Build()
    {
        building = true;
        bool appBundle = EditorUserBuildSettings.buildAppBundle;
        bool exportProject = EditorUserBuildSettings.exportAsGoogleAndroidProject;
        bool development = EditorUserBuildSettings.development;
        bool debugging = EditorUserBuildSettings.allowDebugging;
        bool profiler = EditorUserBuildSettings.connectProfiler;
        bool expansionFiles = PlayerSettings.Android.splitApplicationBinary;
        var status = new BuildStatus { state = "building" };
        try
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new InvalidOperationException("Select the Android build profile before building this APK.");
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("No enabled build scenes.");

            string directory = Path.Combine(ProjectRoot, "Builds", "Android");
            Directory.CreateDirectory(directory);
            status.apk = Path.Combine(directory, "NeonReflex-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".apk");
            status.package = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
            status.version = PlayerSettings.bundleVersion;
            status.architecture = PlayerSettings.Android.targetArchitectures.ToString();
            status.backend = PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android).ToString();
            WriteStatus(status);

            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;
            PlayerSettings.Android.splitApplicationBinary = false;

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = status.apk,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });
            status.state = report.summary.result == BuildResult.Succeeded ? "succeeded" : "failed";
            // Unity's totalSize also counts symbol archives and IL2CPP backups.
            status.bytes = File.Exists(status.apk) ? new FileInfo(status.apk).Length : 0;
            status.errors = (int)report.summary.totalErrors;
            status.warnings = (int)report.summary.totalWarnings;
            status.message = report.summary.result + " in " + report.summary.totalTime + ". " +
                string.Join("\n", report.steps.SelectMany(s => s.messages)
                    .Where(m => m.type == LogType.Error || m.type == LogType.Exception)
                    .Select(m => m.content));
            status.utc = DateTime.UtcNow.ToString("O");
            File.WriteAllText(Path.ChangeExtension(status.apk, ".build.json"), JsonUtility.ToJson(status, true));
        }
        catch (Exception exception)
        {
            status.state = "failed";
            status.message = exception.ToString();
            Debug.LogException(exception);
        }
        finally
        {
            EditorUserBuildSettings.buildAppBundle = appBundle;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = exportProject;
            EditorUserBuildSettings.development = development;
            EditorUserBuildSettings.allowDebugging = debugging;
            EditorUserBuildSettings.connectProfiler = profiler;
            PlayerSettings.Android.splitApplicationBinary = expansionFiles;
            WriteStatus(status);
            building = false;
        }
    }

    private static void WriteStatus(BuildStatus status)
    {
        status.utc = DateTime.UtcNow.ToString("O");
        Directory.CreateDirectory(Path.GetDirectoryName(StatusPath));
        File.WriteAllText(StatusPath, JsonUtility.ToJson(status, true));
    }
}
