using UnityEngine;

/// <summary>
/// Starts the game in any scene (even an empty one): it opens the 3D table, whose main menu is the only menu.
/// The rules live in PokerDemo.Core; everything on screen is built at runtime by Game3D.
/// </summary>
public class DemoApp : MonoBehaviour
{
    static DemoApp _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (_instance != null) return;
        var go = new GameObject("PokerDemo");
        _instance = go.AddComponent<DemoApp>();
    }

    void Start()
    {
        var go = new GameObject("Game3D");
        go.AddComponent<Game3D>().Open(Quit);
    }

    static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
