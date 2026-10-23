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
        ApplyPokiBuildSettings();

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

    // Poki's build guidelines for Unity WebGL: no compression (Poki's servers compress), hashed file names (cache busting), the smallest exception
    // support, engine code stripped, high managed stripping. Data caching is deliberately OFF (see below). Set before every build so they cannot drift.
    static void ApplyPokiBuildSettings()
    {
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.WebGL.nameFilesAsHashes = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
        PlayerSettings.WebGL.dataCaching = false; // off: with hashed file names the browser's own HTTP cache already does the job, and Unity's IndexedDB cache layer hung page reloads inside the Poki inspector
        PlayerSettings.stripEngineCode = true;
        PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.High);
    }

    static void Fail(string message)
    {
        Debug.LogError(message);
        if (Application.isBatchMode) EditorApplication.Exit(1);
    }
}
