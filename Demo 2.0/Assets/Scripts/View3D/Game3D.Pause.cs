using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Esc (or the Menu button) opens a small pause card: continue, back to the main menu, or quit to the desktop
/// (over the main menu itself: continue or quit).
/// While it is open the game stands still (Time.timeScale 0).
/// </summary>
public partial class Game3D
{
    GameObject _pausePanel;
    bool _paused;
    float _timeScale0 = 1f;
    UiBtn _pauseContinue, _pauseMenu, _pauseQuit;

    bool EscPressed()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#elif ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && kb.escapeKey.wasPressedThisFrame;
#else
        return false;
#endif
    }

    void TogglePause() { SetPaused(!_paused); }

    void SetPaused(bool on)
    {
        if (on == _paused) return;
        _paused = on;
        if (on) _timeScale0 = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = on ? 0f : _timeScale0;
        if (_pausePanel == null) return;
        _pausePanel.SetActive(on);
        ClearSelection();                 // Tab starts fresh on the card; closing it leaves nothing selected behind
        if (!on) return;
        // over the main menu there is no menu to go back to: just continue or quit
        bool menu = _mode == Mode.Menu;
        _pauseMenu.Go.SetActive(!menu);
        PlacePause(_pauseQuit, menu ? 0.36f : 0.14f);
    }

    static void PlacePause(UiBtn b, float y0)
    {
        var rt = (RectTransform)b.Go.transform;
        rt.anchorMin = V(0.08f, y0);
        rt.anchorMax = V(0.92f, y0 + 0.18f);
    }

    void QuitApp()
    {
        SetPaused(false);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void BuildPausePanel(Transform h)
    {
        // a dark layer over everything (it also stops clicks reaching the buttons below), with a card in the middle
        Image dim = NewPanel(h, "Pause", V(0f, 0f), V(1f, 1f), new Color(0f, 0f, 0f, 0.55f));
        dim.raycastTarget = true;
        _pausePanel = dim.gameObject;
        Image card = NewPanel(_pausePanel.transform, "PauseCard", V(0.35f, 0.27f), V(0.65f, 0.73f), new Color(0.06f, 0.025f, 0.035f, 0.97f));
        card.raycastTarget = true;
        Transform c = card.transform;
        Gold(c, V(0f, 0f), V(1f, 0.008f)); Gold(c, V(0f, 0.992f), V(1f, 1f)); Gold(c, V(0f, 0f), V(0.005f, 1f)); Gold(c, V(0.995f, 0f), V(1f, 1f));
        Text title = NewLabel(c, "Title", V(0.05f, 0.80f), V(0.95f, 0.96f), 26, TextAnchor.MiddleCenter);
        title.text = "<b>PAUZË</b>";
        title.color = new Color(0.95f, 0.78f, 0.35f);
        _pauseContinue = NewButton(c, "Vazhdo", V(0.08f, 0.58f), V(0.92f, 0.76f), delegate { SetPaused(false); }, new Color(0.12f, 0.42f, 0.28f), 18);
        _pauseMenu = NewButton(c, "Kthehu te menuja", V(0.08f, 0.36f), V(0.92f, 0.54f), delegate { SetPaused(false); ShowMenu(); }, new Color(0.17f, 0.28f, 0.45f), 18);
        _pauseQuit = NewButton(c, "Dil nga aplikacioni", V(0.08f, 0.14f), V(0.92f, 0.32f), delegate { QuitApp(); }, new Color(0.62f, 0.10f, 0.17f), 18);
        foreach (UiBtn b in new[] { _pauseContinue, _pauseMenu, _pauseQuit }) Glow(b.B);
        _pausePanel.SetActive(false);
    }

    /// <summary>The button selected with Tab (or under the mouse) gets ~40% brighter; at rest it looks as before.</summary>
    static void Glow(Button b)
    {
        ColorBlock cb = b.colors;
        cb.colorMultiplier = 1.4f;
        cb.normalColor = new Color(0.714f, 0.714f, 0.714f, 1f);
        cb.highlightedColor = Color.white;
        cb.selectedColor = Color.white;
        cb.pressedColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        b.colors = cb;
    }
}
