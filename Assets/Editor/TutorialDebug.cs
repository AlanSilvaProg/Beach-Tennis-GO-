using UnityEditor;
using UnityEngine;

// Tools / Beach Tennis GO / Show tutorial: opens the controls tutorial again (play mode). Which variant it shows (keys and mouse, or the phone controls)
// follows Tools / Beach Tennis GO / Debug mobile controls.
public static class TutorialDebug
{
    [MenuItem("Tools/Beach Tennis GO/Show tutorial")]
    static void Show()
    {
        var gm = Object.FindFirstObjectByType<GameManager>();
        if (!Application.isPlaying || gm == null) { Debug.Log("[Tutorial] Enter play mode first."); return; }
        gm.ShowTutorial();
    }
}
