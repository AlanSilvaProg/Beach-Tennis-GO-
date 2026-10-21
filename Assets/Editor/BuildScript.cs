using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Headless WebGL build used by scripts/build-release.sh:
//   Unity -batchmode -nographics -quit -projectPath . -executeMethod BuildScript.BuildWebGL
public static class BuildScript
{
    const string DefaultOutput = "WebGLBuild";

    [MenuItem("Tools/Beach Tennis GO/Build WebGL")]
    public static void BuildWebGL()
    {
        string output = Environment.GetEnvironmentVariable("BUILD_OUTPUT");
        if (string.IsNullOrEmpty(output)) output = DefaultOutput;

        VoiceManifest.Generate(); // voice file lists (StreamingAssets/Voice/<lang>/index.txt) must be current

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0) { Fail("No enabled scenes in Build Settings."); return; }

        if (Directory.Exists(output)) Directory.Delete(output, true);
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            Fail("WebGL build failed: " + report.summary.result);
            return;
        }
        Debug.Log("WebGL build succeeded: " + output + " (" + (report.summary.totalSize / (1024 * 1024)) + " MB)");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static void Fail(string message)
    {
        Debug.LogError(message);
        if (Application.isBatchMode) EditorApplication.Exit(1);
    }
}
