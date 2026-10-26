using UnityEngine;
using UnityEngine.SceneManagement;

// Moving between the scenes of the game: Gameplay (the training mode and the main menu) and Online (matches against other players).
public static class SceneFlow
{
    public const string TrainingScene = "Gameplay";
    public const string OnlineScene = "Online";

    public static void OpenOnline() { SceneManager.LoadScene(OnlineScene); }
    public static void OpenTraining() { SceneManager.LoadScene(TrainingScene); }
}
