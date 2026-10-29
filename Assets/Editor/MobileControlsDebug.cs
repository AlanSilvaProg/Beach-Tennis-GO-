using UnityEditor;
using UnityEngine;

// Look at (and try) the phone controls in the editor: Tools / Beach Tennis GO / Debug mobile controls.
//  - On: the phone layout (joystick on the left, swing button on the right) is shown in the Game view, also in the menu, and the mouse acts as a finger
//    (drag on the left half to move the stick, click on the right half to swing). In edit mode a preview of the controls is drawn in the Game view.
//  - Off: the controls are what the device would show (nothing on a computer).
public static class MobileControlsDebug
{
    const string MenuPath = "Tools/Beach Tennis GO/Debug mobile controls";
    const string PreviewName = "MobileControlsPreview";

    [MenuItem(MenuPath)]
    static void Toggle()
    {
        EditorPrefs.SetBool(PlayerControls.DebugPref, !PlayerControls.ForceMobile);
        Refresh();
    }

    [MenuItem(MenuPath, true)]
    static bool ToggleCheck()
    {
        Menu.SetChecked(MenuPath, PlayerControls.ForceMobile);
        return true;
    }

    [InitializeOnLoadMethod]
    static void Init()
    {
        EditorApplication.playModeStateChanged += _ => Refresh();
        EditorApplication.delayCall += Refresh;
    }

    static PlayerControls FindPreview()
    {
        foreach (var c in Resources.FindObjectsOfTypeAll<PlayerControls>()) if (c.name == PreviewName) return c;
        return null;
    }

    // Edit mode: the preview object exists only while the debug is on. Play mode: the game makes its own controls.
    static void Refresh()
    {
        var old = FindPreview();
        bool want = PlayerControls.ForceMobile && !EditorApplication.isPlayingOrWillChangePlaymode;
        if (!want)
        {
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }
        else if (old == null)
        {
            var pc = PlayerControls.Create(null, null);
            pc.gameObject.name = PreviewName;
            pc.gameObject.hideFlags = HideFlags.HideAndDontSave;
            pc.SetVisible(true);
        }
        InternalEditorUtility_Repaint();
    }

    static void InternalEditorUtility_Repaint() { UnityEditorInternal.InternalEditorUtility.RepaintAllViews(); }
}
