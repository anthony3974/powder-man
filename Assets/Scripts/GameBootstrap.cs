using UnityEngine;

namespace PowderMan
{
    /// <summary>
    /// Spawns the game when a scene loads, so pressing Play in any scene works. If a PowderGame
    /// is already in the scene (the Main scene has one, so you can tweak it in the Inspector), this does nothing.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindAnyObjectByType<PowderGame>() != null) return;
            new GameObject("Powder Game").AddComponent<PowderGame>();
        }
    }
}
