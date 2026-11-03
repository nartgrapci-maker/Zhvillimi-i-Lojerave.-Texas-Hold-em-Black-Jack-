using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PokerDemo.Core;

/// <summary>
/// The 3D table in an Albanian setting: builds the room, the table, the players, cards and chips at runtime
/// and plays the tournament across four Albanian cities, free play, the three teaching levels and Blackjack
/// (free and scripted). All rules come from PokerDemo.Core; all text comes from Sq.
/// </summary>
public partial class Game3D : MonoBehaviour
{
    enum Mode { Menu, Level, FreePlay, Tournament, Blackjack, BjFree }

    const float TableTop = 0.95f;
    const float SeatR = 3.0f, CardR = 1.75f, StackR = 2.1f;
    static readonly Vector3 DeckPos = new Vector3(0.95f, 0.99f, 0.9f);
    static readonly Vector3 PotPos = new Vector3(0f, TableTop + 0.02f, 1.0f);
    // the overview camera (menu, and the "from above" view) and the first-person camera at the player's seat
    static readonly Vector3 CamBase = new Vector3(0f, 5.2f, -6.8f);
    static readonly Vector3 CamTarget = new Vector3(0f, 1.0f, 0.4f);
    const float CamFov = 58f;
    static readonly Vector3 FpPos = new Vector3(0f, 1.66f, -3.05f);
    static readonly Vector3 FpTarget = new Vector3(0f, 0.93f, 0.25f);
    // the first-person view is chosen by its HORIZONTAL angle (a monitor-style 100 / 120 / 140 degrees); Unity wants the vertical one
    static readonly float[] FovChoices = { 100f, 120f, 140f };
    int _fovIdx = 1;
    float _yawT, _pitchT, _yawS, _pitchS;        // looking around: where the player wants to look and where the head is now (degrees)
    const float MaxYaw = 140f, MinPitch = -40f, MaxPitch = 60f;
    bool _lookUsed;

    System.Action _onExit;
    Mode _mode = Mode.Menu;

    // camera + scene
    Camera _cam;
    bool _camCreated, _orth0;
    Vector3 _camPos0;
    Quaternion _camRot0;
    CameraClearFlags _clear0;
    Color _bg0, _ambient0;
    float _fov0;
    UnityEngine.Rendering.AmbientMode _ambientMode0;
    GameObject _world, _dyn;
    AudioSource _audio, _music;
    bool _musicOn = true;
    int _musicMode;             // 0 = Himni i Flamurit (only if the recording is there), 1 = folk loop, 2 = off
    bool _fpv = true;                    // first-person view (the default); false = the view from above
    Vector3 _camLook = CamTarget;
    readonly List<Sway> _sways = new List<Sway>();

    /// <summary>Something that hangs and moves a little in the air: a flag or a lantern.</summary>
    class Sway
    {
        public Transform T;
        public float Phase, Amp, Speed;
        public bool AroundY;
    }
    float _spd = 1f;                     // 1 normal, 0.45 fast
    readonly System.Random _fxRng = new System.Random(5);
    readonly List<Transform> _billboards = new List<Transform>();

    // seats
    readonly List<AvatarView> _avatars = new List<AvatarView>();
    float[] _angles = new float[0];
    GameObject _dealerButton;

    // hold'em state
    LevelDef _level;
    int _levelIndex;
    FreePlaySession _fp;
    HoldemHand _hand;
    int[] _seatSlot = new int[0];
    bool _botsActing, _handOver, _hintOn;
    int _eventsShown, _boardShown, _betAmount;
    readonly List<CardView> _board = new List<CardView>();
    List<CardView>[] _hole = new List<CardView>[0];
    GameObject[] _betPile = new GameObject[0];
    int[] _betShown = new int[0];
    GameObject[] _stackPile = new GameObject[0];
    int[] _stackShown = new int[0];
    GameObject _potPile;
    int _potShown;
    TextMesh _potText;
    readonly List<string> _log = new List<string>();
    string _banner = "";
    string _configError;

    // =====================================================================================
    // lifecycle
    // =====================================================================================

    /// <summary>Starts the 3D demo. onExit is called when the player leaves it.</summary>
    public void Open(System.Action onExit)
    {
        _onExit = onExit;
        EnsureEventSystem();
        SetupCamera();
        Gfx.Apply(_cam);
        BuildWorld();
        SetCity(_map);
        BuildHud();
        BuildFpProps();
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;
        _music = gameObject.AddComponent<AudioSource>();
        _music.playOnAwake = false;
        _music.spatialBlend = 0f;
        _music.loop = true;
        _musicMode = Music.Anthem != null ? 0 : 1;
        ApplyMusic();
        ShowMenu();
    }

    void Close()
    {
        SetPaused(false);
        StopAllCoroutines();
        if (_cam != null)
        {
            if (_camCreated) Destroy(_cam.gameObject);
            else
            {
                _cam.transform.position = _camPos0;
                _cam.transform.rotation = _camRot0;
                _cam.clearFlags = _clear0;
                _cam.backgroundColor = _bg0;
                _cam.fieldOfView = _fov0;
                _cam.orthographic = _orth0;
            }
        }
        if (_fpRoot != null) Destroy(_fpRoot);
        Smoke.Clear();
        RenderSettings.ambientMode = _ambientMode0;
        RenderSettings.ambientLight = _ambient0;
        Gfx.Restore();
        if (_world != null) Destroy(_world);
        if (_hudRoot != null) Destroy(_hudRoot);
        System.Action cb = _onExit;
        Destroy(gameObject);
        if (cb != null) cb();
    }

    void OnDestroy() { Gfx.Restore(); }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        System.Type t = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (t != null) go.AddComponent(t);
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }

    void SetupCamera()
    {
        _cam = Camera.main;
        if (_cam == null)
        {
            var go = new GameObject("Game3D Camera");
            go.tag = "MainCamera";
            _cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
            _camCreated = true;
        }
        _camPos0 = _cam.transform.position;
        _camRot0 = _cam.transform.rotation;
        _clear0 = _cam.clearFlags;
        _bg0 = _cam.backgroundColor;
        _fov0 = _cam.fieldOfView;
        _orth0 = _cam.orthographic;

        _cam.orthographic = false;
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = Theme.Look(-1).Sky;
        _cam.fieldOfView = CamFov;
        _cam.transform.position = CamBase;
        _cam.transform.LookAt(CamTarget);
        AvatarView.CameraT = _cam.transform;

        _ambientMode0 = RenderSettings.ambientMode;
        _ambient0 = RenderSettings.ambientLight;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.36f, 0.33f, 0.38f);
    }

    /// <summary>Vertical field of view (degrees) that gives the chosen horizontal angle on this screen.</summary>
    float FpFovNow
    {
        get
        {
            float aspect = _cam != null && _cam.aspect > 0.2f ? _cam.aspect : 16f / 9f;
            float h = FovChoices[_fovIdx] * Mathf.Deg2Rad;
            float v = 2f * Mathf.Atan(Mathf.Tan(h * 0.5f) / aspect) * Mathf.Rad2Deg;
            return Mathf.Clamp(v, 50f, 120f);
        }
    }

    /// <summary>Turns the head (called by dragging on the picture, the mouse and the arrow keys).</summary>
    void Look(float yawDelta, float pitchDelta)
    {
        _yawT = Mathf.Clamp(_yawT + yawDelta, -MaxYaw, MaxYaw);
        _pitchT = Mathf.Clamp(_pitchT + pitchDelta, MinPitch, MaxPitch);
        if (!_lookUsed)
        {
            _lookUsed = true;
            RefreshHud();
        }
    }

    /// <summary>Drag on the picture: a full screen width turns the head by the visible horizontal angle.</summary>
    void LookDrag(Vector2 pixels)
    {
        float w = Mathf.Max(1f, Screen.width);
        float perPixel = FovChoices[_fovIdx] / w;
        Look(pixels.x * perPixel, -pixels.y * perPixel);
    }

    void CenterLook() { _yawT = 0f; _pitchT = 0f; }

    void ReadLookInput()
    {
        if (_paused) return;
#if ENABLE_LEGACY_INPUT_MANAGER
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        float yaw = 0f, pitch = 0f;
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.Q)) yaw -= 1f;
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.E)) yaw += 1f;
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) pitch += 1f;
        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) pitch -= 1f;
        if (yaw != 0f || pitch != 0f) Look(yaw * 90f * dt, pitch * 60f * dt);
        if (Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.Home)) CenterLook();
        // hold the right mouse button and move the mouse, like in a shooter
        if (Input.GetMouseButton(1)) Look(Input.GetAxis("Mouse X") * 3f, Input.GetAxis("Mouse Y") * 3f);
#endif
    }

    /// <summary>True when the camera sits at the player's seat.</summary>
    bool FirstPersonNow { get { return _fpv && _mode != Mode.Menu; } }

    /// <summary>A little head turn toward the bot whose turn it is (-1 left .. 1 right).</summary>
    float Glance()
    {
        for (int i = 1; i < _avatars.Count; i++)
            if (_avatars[i].TurnOn) return Mathf.Clamp(_avatars[i].transform.position.x / 3f, -1f, 1f);
        return 0f;
    }

    void Update()
    {
        if (EscPressed()) TogglePause();
        if (_cam == null) return;
        float t = Time.time;
        bool fp = FirstPersonNow;
        Vector3 pos, look;
        float fov;
        if (fp)
        {
            float g = Glance();
            pos = FpPos + new Vector3(Mathf.Sin(t * 0.31f) * 0.03f + g * 0.10f, Mathf.Sin(t * 0.9f) * 0.008f, 0f);
            look = FpTarget + new Vector3(g * 0.9f + Mathf.Sin(t * 0.23f) * 0.06f, 0f, 0f);
            fov = FpFovNow;
        }
        else
        {
            pos = CamBase;
            if (_mode == Mode.Menu) pos += new Vector3(Mathf.Sin(t * 0.18f) * 2.2f, 0.35f + Mathf.Sin(t * 0.11f) * 0.2f, 0f);
            else pos += new Vector3(Mathf.Sin(t * 0.25f) * 0.35f, Mathf.Sin(t * 0.17f) * 0.1f, 0f);
            look = _mode == Mode.Menu ? CamTarget + new Vector3(-3.4f, 0f, 0f) : CamTarget;   // the menu card covers the left half, so the table sits on the right
            fov = CamFov;
        }
        float k = Mathf.Clamp01(Time.deltaTime * 2.2f);
        _cam.transform.position = Vector3.Lerp(_cam.transform.position, pos, k);
        _camLook = Vector3.Lerp(_camLook, look, k);
        _cam.transform.LookAt(_camLook);
        if (fp)
        {
            ReadLookInput();
            float kl = Mathf.Clamp01(Time.deltaTime * 9f);
            _yawS = Mathf.Lerp(_yawS, _yawT, kl);
            _pitchS = Mathf.Lerp(_pitchS, _pitchT, kl);
        }
        else
        {
            _yawT = _pitchT = 0f;
            _yawS = Mathf.Lerp(_yawS, 0f, k);
            _pitchS = Mathf.Lerp(_pitchS, 0f, k);
        }
        // the head turn comes on top of the seat view: a turn around the vertical axis, then a nod
        _cam.transform.rotation = Quaternion.Euler(0f, _yawS, 0f) * _cam.transform.rotation * Quaternion.Euler(-_pitchS, 0f, 0f);
        _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, fov, k);

        // the player's own body is hidden in first person
        if (_avatars.Count > 0 && _avatars[0].FirstPerson != fp) _avatars[0].SetFirstPerson(fp);

        if (_fireLight != null) _fireLight.intensity = 1.8f + Mathf.Sin(t * 13f) * 0.25f + Mathf.Sin(t * 7.3f) * 0.2f;

        UpdateHabits();

        // flags and lanterns move in the air
        for (int i = 0; i < _sways.Count; i++)
        {
            Sway w = _sways[i];
            if (w.T == null) continue;
            float a = Mathf.Sin(t * w.Speed + w.Phase) * w.Amp;
            w.T.localRotation = w.AroundY ? Quaternion.Euler(0f, a, 0f) : Quaternion.Euler(0f, 0f, a);
        }
    }

    void LateUpdate()
    {
        if (_cam == null) return;
        for (int i = _billboards.Count - 1; i >= 0; i--)
        {
            if (_billboards[i] == null) { _billboards.RemoveAt(i); continue; }
            _billboards[i].rotation = _cam.transform.rotation;
        }
    }

    void Play(AudioClip clip, float volume = 1f)
    {
        if (_audio != null && clip != null) _audio.PlayOneShot(clip, volume);
    }

    /// <summary>A wait that follows the speed switch ("Shpejt").</summary>
    WaitForSeconds Wait(float seconds) { return new WaitForSeconds(seconds * _spd); }

    int NewSeed() { return System.Environment.TickCount ^ _fxRng.Next(); }

    /// <summary>Himni i Flamurit (if the recording is in Resources/Music), then the folk loop, then silence.</summary>
    void ToggleMusic()
    {
        _musicMode = (_musicMode + 1) % 3;
        if (_musicMode == 0 && Music.Anthem == null) _musicMode = 1;
        ApplyMusic();
        RefreshHud();
    }

    void ApplyMusic()
    {
        _musicOn = _musicMode != 2;
        if (_music == null) return;
        _music.Stop();
        if (_musicMode == 0) { _music.clip = Music.Anthem; _music.volume = 0.6f; }
        else { _music.clip = Music.Loop; _music.volume = 0.2f; }
        if (_musicOn) _music.Play();
    }

    void ToggleView()
    {
        _fpv = !_fpv;
        CenterLook();
        RefreshHud();
    }

    void CycleFov()
    {
        _fovIdx = (_fovIdx + 1) % FovChoices.Length;
        RefreshHud();
    }

    void ToggleSpeed()
    {
        _spd = _spd < 0.99f ? 1f : 0.45f;
        RefreshHud();
    }

    // =====================================================================================
    // seats and positions
    // =====================================================================================

    static float[] AnglesFor(int count)
    {
        switch (count)
        {
            case 2: return new float[] { 270f, 90f };
            case 3: return new float[] { 270f, 150f, 30f };
            default: return new float[] { 270f, 160f, 90f, 20f };
        }
    }

    static Vector3 Polar(float degrees, float radius, float y)
    {
        float r = degrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(r) * radius, y, Mathf.Sin(r) * radius);
    }

    /// <summary>Yaw (degrees) that makes an object at this angle face the table centre.</summary>
    static float YawToCenter(float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        return Mathf.Atan2(-Mathf.Cos(r), -Mathf.Sin(r)) * Mathf.Rad2Deg;
    }

    /// <summary>Where a seat's current bet is piled: beside its cards, clear of the board row and the deck.</summary>
    Vector3 BetPos(int slot)
    {
        float a = _angles[slot] * Mathf.Deg2Rad;
        float side = Mathf.Abs(Mathf.Cos(a));              // 1 for the seats left and right of the table
        float radius = Mathf.Lerp(1.2f, 1.55f, side);
        float offset = Mathf.Lerp(0.75f, 0.92f, side);
        var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
        Vector3 p = outward * radius + tangent * offset;
        p.y = TableTop + 0.02f;
        return p;
    }

    /// <summary>A seat's chip stack: on the other side of its cards from the bet pile.</summary>
    Vector3 StackPos(int slot) { return Polar(_angles[slot] - 26f, StackR, TableTop + 0.02f); }

    /// <summary>The dealer button sits beside the seat's cards, past the bet pile.</summary>
    Vector3 DealerPos(int slot)
    {
        float a = _angles[slot] * Mathf.Deg2Rad;
        var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
        Vector3 p = outward * 1.7f + tangent * 1.4f;
        p.y = TableTop + 0.035f;
        return p;
    }
    static Vector3 BoardPos(int i) { return new Vector3((i - 2) * 0.56f, TableTop + 0.03f, 0.05f); }

    void ClearDynamic()
    {
        if (_dyn != null) Destroy(_dyn);
        _dyn = new GameObject("Dynamic");
        _dyn.transform.SetParent(_world.transform, false);
        _avatars.Clear();
        _board.Clear();
        _billboards.Clear();
        _hole = new List<CardView>[0];
        _betPile = new GameObject[0];
        _stackPile = new GameObject[0];
        _betShown = new int[0];
        _stackShown = new int[0];
        _potPile = null;
        _potShown = 0;
        _potText = null;
        _dealerButton = null;
        _hand = null;
        _handOver = false;
        _revealOn = false;
        _revealHidden = false;
        _botsActing = false;
        _cardUp = false;
    }

    void BuildSeats(string[] names)
    {
        int n = names.Length;
        _angles = AnglesFor(n);
        _hole = new List<CardView>[n];
        _betPile = new GameObject[n];
        _stackPile = new GameObject[n];
        _betShown = new int[n];
        _stackShown = new int[n];
        for (int i = 0; i < n; i++)
        {
            _hole[i] = new List<CardView>();
            Vector3 pos = Polar(_angles[i], SeatR, 0f);
            AvatarView av = AvatarView.Create(_dyn.transform, pos, YawToCenter(_angles[i]), names[i], i);
            av.AutoLife = i > 0;
            _avatars.Add(av);
        }
        SetTeaLevels();
    }

    static string ShortName(string full)
    {
        int p = full.IndexOf('(');
        return p > 0 ? full.Substring(0, p).Trim() : full;
    }
}
