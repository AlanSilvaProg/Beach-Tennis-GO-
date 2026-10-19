using UnityEditor;
using UnityEngine;

// Pressing Play used to reload every script assembly and the scene, which freezes the editor for seconds.
// Turns on "Enter Play Mode Options" with both reloads disabled, once per machine. Change it back in
// Project Settings > Editor > Enter Play Mode Settings if you ever want the old behaviour.
// The game code keeps no static state that would break with this (the only static cache checks for destroyed objects).
static class FastPlayMode
{
    const string Key = "BeachTennisGO.FastPlayMode.v1";

    [InitializeOnLoadMethod]
    static void Apply()
    {
        string key = Key + "." + Application.dataPath;
        if (EditorPrefs.GetBool(key, false)) return;
        EditorPrefs.SetBool(key, true);
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
        Debug.Log("[BeachTennisGO] Fast Play Mode enabled (no domain/scene reload when pressing Play).");
    }
}
