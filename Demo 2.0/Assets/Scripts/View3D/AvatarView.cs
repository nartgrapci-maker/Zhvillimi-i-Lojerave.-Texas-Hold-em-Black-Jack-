using UnityEngine;

/// <summary>
/// A stylised seated player built from primitives, dressed in traditional Albanian clothes only:
/// the white felt cap (plis, round in the north, conical in the south), the white shirt with wide sleeves, the
/// embroidered vest (xhamadan / jelek) with gold braid (gajtan), the wide red belt (brez), the white wool trousers (tirq)
/// or the pleated kilt (fustanella) with leather sandals (opinga, with red pompoms), and for the women the white lace
/// headscarf (shami), the red embroidered bodice, the coin necklace (gjerdan), the bell skirt (xhubleta) with an apron
/// and plaits. Every player holds a glass of tea (çaj rusi) and some of them smoke. Each avatar has a name label,
/// a stack label and a speech bubble.
/// </summary>
public class AvatarView : MonoBehaviour
{
    public enum Mood { Idle, Thinking, Win, Lose, Folded, Out }
    enum Act { None, Sip, Smoke }

    public static Transform CameraT;

    static readonly Color[] Skin =
    {
        new Color(0.96f, 0.80f, 0.66f), new Color(0.88f, 0.67f, 0.50f), new Color(0.78f, 0.56f, 0.40f), new Color(0.92f, 0.74f, 0.60f)
    };
    static readonly Color[] Hair =
    {
        new Color(0.22f, 0.13f, 0.07f), new Color(0.07f, 0.07f, 0.09f), new Color(0.38f, 0.24f, 0.11f), new Color(0.62f, 0.62f, 0.64f)
    };
    static readonly Color[] Iris =
    {
        new Color(0.30f, 0.17f, 0.08f), new Color(0.22f, 0.36f, 0.56f), new Color(0.28f, 0.38f, 0.28f), new Color(0.12f, 0.08f, 0.06f)
    };

    public Mood CurrentMood { get; private set; }
    public bool TurnOn { get; private set; }
    public bool AutoLife;                         // bots and the people at the other tables sip tea and smoke now and then
    public bool Smoker { get; private set; }
    public bool Busy { get { return _act != Act.None; } }

    Transform _rig, _label, _mouth;
    TextMesh _name, _info, _bubble;
    GameObject _ring;
    float _seed, _bubbleUntil;
    bool _ambient;

    // arms: shoulder and elbow pivots (0 = left, 1 = right); the right hand holds a glass of tea, the left one a cigarette
    readonly Transform[] _shoulder = new Transform[2];
    readonly Transform[] _elbow = new Transform[2];
    Transform _glassPivot, _liquid, _cigPivot;
    Renderer _ember;
    GameObject _cig;
    float _tea = 1f;
    Act _act;
    float _actT, _actLen, _nextAuto, _nextPuff;
    bool _drankNow;

    static Vector3 P(float x, float y, float z) { return new Vector3(x, y - 0.5f, z); }

    static GameObject Mk(Transform parent, PrimitiveType t, float x, float y, float z, float sx, float sy, float sz, Material m, string name)
    {
        return Prim.Make(t, parent, P(x, y, z), new Vector3(sx, sy, sz), m, name);
    }

    static GameObject Rot(GameObject g, float x, float y, float z) { g.transform.localRotation = Quaternion.Euler(x, y, z); return g; }

    public static AvatarView Create(Transform parent, Vector3 floorPos, float yaw, string displayName, int style)
    {
        var root = new GameObject("Avatar " + displayName);
        root.transform.SetParent(parent, false);
        root.transform.position = floorPos;
        root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        AvatarView av = root.AddComponent<AvatarView>();
        av._seed = style * 1.7f + displayName.Length * 0.37f;
        bool female = Sq.IsFemale(displayName);
        int look = style % 4;
        av.Smoker = !female && (style % 3 != 1);

        Color skin = Skin[(style + (female ? 1 : 0)) % Skin.Length];
        Color hair = female ? Hair[(style + 1) % 3] : Hair[look == 3 ? 3 : (style + 1) % 3];
        Material skinM = Mats.Lit(skin, 0.3f), hairM = Mats.Lit(hair, 0.2f);
        Material noseM = Mats.Lit(Color.Lerp(skin, new Color(0.75f, 0.38f, 0.32f), 0.18f), 0.3f);
        Material lipM = Mats.Lit(Color.Lerp(skin, new Color(0.7f, 0.2f, 0.2f), 0.45f), 0.3f);
        Material shirtM = Mats.Lit(Theme.Cream, 0.3f), vestM = Mats.Lit(Theme.Black, 0.25f);
        Material redM = Mats.Lit(Theme.Red, 0.35f), goldM = Mats.Lit(Theme.Gold, 0.5f, 0.3f), deepRed = Mats.Lit(Theme.DeepRed, 0.3f);
        Material white = Mats.Lit(Color.white, 0.5f), dark = Mats.Lit(new Color(0.05f, 0.05f, 0.07f), 0.2f);
        Material felt = Mats.Lit(new Color(0.96f, 0.95f, 0.92f), 0.05f);
        Material wool = Mats.Lit(new Color(0.94f, 0.92f, 0.88f), 0.05f);
        Material leather = Mats.Lit(new Color(0.34f, 0.20f, 0.12f), 0.25f);
        Material iris = Mats.Lit(Iris[(style / 2 + look) % Iris.Length], 0.5f);

        // stool and turn ring stay still
        Prim.Make(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.25f, 0f), new Vector3(0.65f, 0.25f, 0.65f), Mats.Lit(new Color(0.25f, 0.16f, 0.10f), 0.3f), "Stool");
        av._ring = Prim.Make(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.012f, 0f), new Vector3(1.5f, 0.01f, 1.5f), Mats.Unlit(new Color(0.9f, 0.7f, 0.2f, 0.6f)), "TurnRing");
        av._ring.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        av._ring.SetActive(false);

        // the rig is what sways and leans; its origin is the seat
        var rigGo = new GameObject("Rig");
        rigGo.transform.SetParent(root.transform, false);
        rigGo.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        av._rig = rigGo.transform;
        Transform rig = av._rig;

        // ---- legs and the lower garment -------------------------------------------------------------
        bool kilt = !female && look == 1;
        if (female)
        {
            // xhubleta: a black bell skirt of wool with a red-and-black striped apron (përparëse)
            Prim.Cone(rig, P(0f, 0.43f, 0.02f), new Vector3(1.15f, 0.30f, 1.15f), Mats.Lit(new Color(0.07f, 0.06f, 0.08f), 0.1f), "Xhubleta");
            Mk(rig, PrimitiveType.Cube, 0f, 0.44f, 0.47f, 0.34f, 0.56f, 0.03f, redM, "Apron");
            for (int i = -1; i <= 1; i++) Mk(rig, PrimitiveType.Cube, i * 0.10f, 0.44f, 0.485f, 0.025f, 0.56f, 0.012f, dark, "ApronStripe");
            Mk(rig, PrimitiveType.Cube, 0f, 0.17f, 0.47f, 0.36f, 0.04f, 0.035f, goldM, "ApronHem");
        }
        else if (kilt)
        {
            // fustanella: the white pleated kilt of the south, with white stockings
            Prim.Cone(rig, P(0f, 0.46f, 0.02f), new Vector3(0.95f, 0.27f, 0.95f), wool, "Fustanella");
            for (int i = -3; i <= 3; i++)
            {
                float a = i * 0.42f;
                Mk(rig, PrimitiveType.Cube, Mathf.Sin(a) * 0.40f, 0.44f, 0.02f + Mathf.Cos(a) * 0.40f, 0.012f, 0.50f, 0.03f, Mats.Lit(new Color(0.82f, 0.80f, 0.76f), 0.05f), "Pleat").transform.localRotation = Quaternion.Euler(-22f, a * Mathf.Rad2Deg, 0f);
            }
            Mk(rig, PrimitiveType.Cylinder, 0f, 0.70f, 0.02f, 0.80f, 0.03f, 0.56f, deepRed, "KiltBelt");
        }

        for (int side = -1; side <= 1; side += 2)
        {
            if (!female)
            {
                if (!kilt)
                {
                    // white wool trousers (tirq) with black braid
                    Rot(Mk(rig, PrimitiveType.Capsule, side * 0.15f, 0.64f, 0.19f, 0.20f, 0.25f, 0.20f, wool, "Thigh"), 90f, 0f, 0f);
                    Mk(rig, PrimitiveType.Cube, side * 0.15f, 0.745f, 0.19f, 0.035f, 0.012f, 0.46f, dark, "Braid");
                }
                Material shin = kilt ? wool : wool;
                Mk(rig, PrimitiveType.Capsule, side * 0.15f, 0.30f, 0.40f, 0.15f, 0.31f, 0.15f, shin, "Shin");
                if (!kilt) Mk(rig, PrimitiveType.Cube, side * 0.15f, 0.30f, 0.475f, 0.03f, 0.50f, 0.012f, dark, "ShinBraid");
                else Rot(Mk(rig, PrimitiveType.Capsule, side * 0.15f, 0.64f, 0.19f, 0.17f, 0.25f, 0.17f, wool, "Thigh"), 90f, 0f, 0f);
            }
            // leather sandals (opinga), curled up at the toe, with a red pompom (tufa) on the kilt wearers
            Mk(rig, PrimitiveType.Sphere, side * 0.15f, 0.06f, 0.47f, 0.15f, 0.09f, 0.30f, leather, "Opinga");
            Mk(rig, PrimitiveType.Sphere, side * 0.15f, 0.10f, 0.60f, 0.07f, 0.07f, 0.07f, leather, "OpingaToe");
            if (kilt) Mk(rig, PrimitiveType.Sphere, side * 0.15f, 0.12f, 0.64f, 0.07f, 0.07f, 0.07f, redM, "Tufa");
        }

        // ---- torso: white shirt, embroidered vest, belt ----------------------------------------------
        Mk(rig, PrimitiveType.Capsule, 0f, 0.95f, 0f, 0.64f, 0.45f, 0.44f, shirtM, "Torso");
        Material vest = female ? redM : (look == 2 ? Mats.Lit(new Color(0.42f, 0.07f, 0.10f), 0.25f) : vestM);
        for (int side = -1; side <= 1; side += 2)
        {
            Mk(rig, PrimitiveType.Cube, side * 0.19f, 1.00f, 0.175f, 0.20f, 0.60f, 0.12f, vest, "Vest");
            Mk(rig, PrimitiveType.Cube, side * 0.095f, 1.00f, 0.238f, 0.018f, 0.58f, 0.02f, goldM, "VestTrim");
            Mk(rig, PrimitiveType.Cube, side * 0.30f, 1.00f, 0.19f, 0.018f, 0.55f, 0.04f, goldM, "VestTrim2");
            for (int k = 0; k < 3; k++) Mk(rig, PrimitiveType.Sphere, side * 0.17f, 0.88f + k * 0.14f, 0.25f, 0.035f, 0.035f, 0.03f, goldM, "VestButton");
            // gold embroidery (gajtan) in a curl on each side of the chest
            Rot(Mk(rig, PrimitiveType.Cube, side * 0.19f, 1.18f, 0.238f, 0.10f, 0.012f, 0.012f, goldM, "Gajtan"), 0f, 0f, side * 25f);
            Rot(Mk(rig, PrimitiveType.Cube, side * 0.19f, 1.10f, 0.238f, 0.10f, 0.012f, 0.012f, goldM, "Gajtan"), 0f, 0f, -side * 25f);
        }
        Mk(rig, PrimitiveType.Cylinder, 0f, 0.66f, 0f, 0.70f, 0.045f, 0.50f, female ? Mats.Lit(new Color(0.08f, 0.08f, 0.10f), 0.3f) : redM, "Brez");
        Mk(rig, PrimitiveType.Cylinder, 0f, 0.625f, 0f, 0.71f, 0.012f, 0.51f, goldM, "BrezTrim");

        // neck, collar, shoulders
        Mk(rig, PrimitiveType.Cylinder, 0f, 1.36f, 0.01f, 0.17f, 0.07f, 0.17f, skinM, "Neck");
        Mk(rig, PrimitiveType.Cylinder, 0f, 1.31f, 0.0f, 0.30f, 0.025f, 0.26f, shirtM, "Collar");

        // ---- arms: wide white sleeves with a pivot at the shoulder and at the elbow -------------------
        for (int idx = 0; idx < 2; idx++)
        {
            float side = idx == 0 ? -1f : 1f;
            var sh = new GameObject("Shoulder" + idx);
            sh.transform.SetParent(rig, false);
            sh.transform.localPosition = P(side * 0.33f, 1.27f, 0.02f);
            sh.transform.localRotation = Quaternion.Euler(-65f, 0f, 0f);
            av._shoulder[idx] = sh.transform;
            Prim.Make(PrimitiveType.Sphere, sh.transform, Vector3.zero, new Vector3(0.20f, 0.20f, 0.20f), shirtM, "ShoulderBall");
            Prim.Make(PrimitiveType.Capsule, sh.transform, new Vector3(0f, -0.17f, 0f), new Vector3(0.15f, 0.20f, 0.15f), shirtM, "UpperArm");
            var el = new GameObject("Elbow" + idx);
            el.transform.SetParent(sh.transform, false);
            el.transform.localPosition = new Vector3(0f, -0.34f, 0f);
            av._elbow[idx] = el.transform;
            Prim.Make(PrimitiveType.Sphere, el.transform, Vector3.zero, new Vector3(0.14f, 0.14f, 0.14f), shirtM, "ElbowBall");
            Prim.Make(PrimitiveType.Capsule, el.transform, new Vector3(0f, -0.18f, 0f), new Vector3(0.17f, 0.21f, 0.17f), shirtM, "Forearm");        // the wide sleeve
            Prim.Make(PrimitiveType.Cylinder, el.transform, new Vector3(0f, -0.335f, 0f), new Vector3(0.19f, 0.012f, 0.19f), female ? redM : goldM, "Cuff");
            var hand = new GameObject("Hand" + idx);
            hand.transform.SetParent(el.transform, false);
            hand.transform.localPosition = new Vector3(0f, -0.38f, 0f);
            Prim.Make(PrimitiveType.Sphere, hand.transform, Vector3.zero, new Vector3(0.13f, 0.14f, 0.13f), skinM, "Hand");
            Prim.Make(PrimitiveType.Sphere, hand.transform, new Vector3(side * 0.05f, 0.01f, 0.04f), new Vector3(0.06f, 0.09f, 0.06f), skinM, "Thumb");

            if (idx == 1)
            {
                // the glass of tea (çaj rusi) in a brass holder, always in the right hand
                var gp = new GameObject("TeaGlass");
                gp.transform.SetParent(hand.transform, false);
                av._glassPivot = gp.transform;
                Material amber = Mats.Lit(new Color(0.72f, 0.34f, 0.08f), 0.7f);
                Material glassRim = Mats.Lit(new Color(0.85f, 0.92f, 0.95f), 0.9f);
                Material brass = Mats.Lit(new Color(0.80f, 0.62f, 0.22f), 0.7f, 0.6f);
                Prim.Make(PrimitiveType.Cylinder, gp.transform, new Vector3(0f, 0.045f, 0f), new Vector3(0.064f, 0.045f, 0.064f), glassRim, "Glass");
                GameObject liquid = Prim.Make(PrimitiveType.Cylinder, gp.transform, new Vector3(0f, 0.04f, 0f), new Vector3(0.056f, 0.04f, 0.056f), amber, "Tea");
                av._liquid = liquid.transform;
                Prim.Make(PrimitiveType.Cylinder, gp.transform, new Vector3(0f, 0.03f, 0f), new Vector3(0.074f, 0.028f, 0.074f), brass, "Holder");
                Prim.Make(PrimitiveType.Cylinder, gp.transform, new Vector3(0f, 0.093f, 0f), new Vector3(0.070f, 0.004f, 0.070f), brass, "HolderRim");
                av.SetTea(1f);
            }
            else
            {
                // a cigarette, only seen while smoking
                var cp = new GameObject("Cigarette");
                cp.transform.SetParent(hand.transform, false);
                av._cigPivot = cp.transform;
                av._cig = cp;
                Prim.Make(PrimitiveType.Cylinder, cp.transform, new Vector3(0f, 0.045f, 0f), new Vector3(0.014f, 0.045f, 0.014f), Mats.Lit(new Color(0.97f, 0.97f, 0.95f), 0.2f), "Cigarette");
                Prim.Make(PrimitiveType.Cylinder, cp.transform, new Vector3(0f, 0.015f, 0f), new Vector3(0.0145f, 0.016f, 0.0145f), Mats.Lit(new Color(0.78f, 0.52f, 0.22f), 0.2f), "Filter");
                GameObject ember = Prim.Make(PrimitiveType.Sphere, cp.transform, new Vector3(0f, 0.093f, 0f), new Vector3(0.016f, 0.016f, 0.016f), Mats.Unlit(new Color(1f, 0.45f, 0.1f)), "Ember");
                av._ember = ember.GetComponent<Renderer>();
                cp.SetActive(false);
            }
        }

        // ---- head -------------------------------------------------------------------------------------
        Mk(rig, PrimitiveType.Sphere, 0f, 1.62f, 0f, 0.50f, 0.54f, 0.50f, skinM, "Head");
        Mk(rig, PrimitiveType.Sphere, 0f, 1.50f, 0.07f, 0.34f, 0.26f, 0.34f, skinM, "Jaw");
        Mk(rig, PrimitiveType.Sphere, 0f, 1.58f, 0.245f, 0.075f, 0.10f, 0.10f, noseM, "Nose");
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * 0.105f;
            Mk(rig, PrimitiveType.Sphere, x, 1.665f, 0.205f, 0.125f, 0.115f, 0.07f, white, "Eye");
            Mk(rig, PrimitiveType.Sphere, x, 1.665f, 0.238f, 0.07f, 0.07f, 0.04f, iris, "Iris");
            Mk(rig, PrimitiveType.Sphere, x, 1.665f, 0.256f, 0.035f, 0.035f, 0.02f, dark, "Pupil");
            Mk(rig, PrimitiveType.Sphere, x + side * 0.018f, 1.68f, 0.262f, 0.014f, 0.014f, 0.01f, white, "Glint");
            Rot(Mk(rig, PrimitiveType.Cube, x, 1.74f, 0.21f, 0.125f, 0.024f, 0.03f, hairM, "Brow"), 0f, 0f, side * -9f);
            Mk(rig, PrimitiveType.Sphere, side * 0.25f, 1.62f, 0.0f, 0.05f, 0.10f, 0.05f, skinM, "Ear");
            Mk(rig, PrimitiveType.Sphere, side * 0.155f, 1.55f, 0.215f, 0.085f, 0.05f, 0.03f, Mats.Lit(Color.Lerp(skin, new Color(0.9f, 0.4f, 0.4f), 0.22f), 0.3f), "Cheek");
        }
        GameObject mouth = Mk(rig, PrimitiveType.Sphere, 0f, 1.485f, 0.237f, 0.105f, 0.030f, 0.035f, lipM, "Mouth");
        av._mouth = mouth.transform;

        // ---- hair and headwear ------------------------------------------------------------------------
        if (female)
        {
            // a white lace headscarf (shami) with a red border and a fringe, a coin necklace (gjerdan), earrings and two plaits
            Material shami = look % 2 == 0 ? felt : Mats.Lit(new Color(0.96f, 0.88f, 0.70f), 0.05f);
            Mk(rig, PrimitiveType.Sphere, 0f, 1.74f, -0.02f, 0.57f, 0.40f, 0.57f, shami, "Shami");
            Mk(rig, PrimitiveType.Capsule, 0f, 1.50f, -0.20f, 0.52f, 0.36f, 0.20f, shami, "ShamiDrape");
            Mk(rig, PrimitiveType.Cylinder, 0f, 1.755f, 0.01f, 0.575f, 0.012f, 0.575f, redM, "ShamiBorder");
            for (int k = -3; k <= 3; k++) Mk(rig, PrimitiveType.Sphere, k * 0.075f, 1.69f, 0.255f - Mathf.Abs(k) * 0.02f, 0.035f, 0.035f, 0.03f, goldM, "ShamiCoin");
            Mk(rig, PrimitiveType.Sphere, 0f, 1.735f, 0.15f, 0.40f, 0.10f, 0.30f, hairM, "Parting");
            for (int side = -1; side <= 1; side += 2)
            {
                Rot(Mk(rig, PrimitiveType.Capsule, side * 0.20f, 1.18f, 0.16f, 0.075f, 0.24f, 0.075f, hairM, "Plait"), 0f, 0f, side * 6f);
                Mk(rig, PrimitiveType.Sphere, side * 0.215f, 0.93f, 0.17f, 0.06f, 0.06f, 0.06f, redM, "PlaitTassel");
                Mk(rig, PrimitiveType.Sphere, side * 0.255f, 1.54f, 0.02f, 0.05f, 0.05f, 0.05f, goldM, "Earring");
            }
            Prim.Torus(rig, P(0f, 1.335f, 0.03f), 0.19f, 0.016f, goldM, "Necklace");
            for (int k = -2; k <= 2; k++) Mk(rig, PrimitiveType.Sphere, k * 0.065f, 1.265f - Mathf.Abs(k) * 0.012f, 0.205f, 0.055f, 0.055f, 0.025f, goldM, "Coin");
        }
        else
        {
            Mk(rig, PrimitiveType.Sphere, 0f, 1.76f, -0.02f, 0.53f, 0.28f, 0.53f, hairM, "Hair");
            for (int side = -1; side <= 1; side += 2) Mk(rig, PrimitiveType.Sphere, side * 0.235f, 1.60f, 0.06f, 0.05f, 0.12f, 0.09f, hairM, "Sideburn");
            // the moustache (mustaqe): two curled halves
            for (int side = -1; side <= 1; side += 2)
                Rot(Mk(rig, PrimitiveType.Capsule, side * 0.065f, 1.535f, 0.255f, 0.05f, 0.075f, 0.05f, hairM, "Moustache"), 0f, 0f, side * -82f);
            Mk(rig, PrimitiveType.Sphere, 0f, 1.548f, 0.248f, 0.07f, 0.03f, 0.03f, hairM, "MoustacheMid");
            if (look == 0 || look == 3)
            {
                // the round white felt cap of the north (plis)
                Mk(rig, PrimitiveType.Cylinder, 0f, 1.80f, 0f, 0.50f, 0.04f, 0.50f, felt, "PlisBase");
                Mk(rig, PrimitiveType.Sphere, 0f, 1.84f, 0f, 0.46f, 0.42f, 0.46f, felt, "Plis");
                Mk(rig, PrimitiveType.Cylinder, 0f, 1.805f, 0f, 0.515f, 0.012f, 0.515f, Mats.Lit(new Color(0.88f, 0.86f, 0.82f), 0.05f), "PlisBand");
            }
            else if (look == 1)
            {
                // the conical white cap of the south (qeleshe)
                Mk(rig, PrimitiveType.Cylinder, 0f, 1.79f, 0f, 0.50f, 0.03f, 0.50f, felt, "QeleshBase");
                Prim.Cone(rig, P(0f, 1.97f, 0f), new Vector3(0.47f, 0.17f, 0.47f), felt, "Qelesh");
            }
            // look 2: a young man with his head bare, dark hair and a dark red vest
        }

        // billboard labels
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(root.transform, false);
        labelGo.transform.localPosition = new Vector3(0f, 2.45f, 0f);
        av._label = labelGo.transform;
        av._name = Mats.Text(av._label, displayName, 0.20f, Color.white, new Vector3(0f, 0.20f, 0f));
        av._info = Mats.Text(av._label, "", 0.17f, new Color(1f, 0.85f, 0.4f), Vector3.zero);
        av._bubble = Mats.Text(av._label, "", 0.22f, new Color(1f, 0.93f, 0.7f), new Vector3(0f, 0.58f, 0f));
        av._nextAuto = 4f + (av._seed * 3.1f) % 9f;
        av.PoseArms(0f, 0f);
        return av;
    }

    public void SetInfo(string text) { if (_info != null) _info.text = text; }
    public void SetTurn(bool on) { TurnOn = on; if (_ring != null) _ring.SetActive(on && !_firstPerson); }

    bool _firstPerson;
    public bool FirstPerson { get { return _firstPerson; } }

    /// <summary>First person: the player's own body and labels are hidden (the camera is where the head would be).</summary>
    public void SetFirstPerson(bool on)
    {
        _firstPerson = on;
        if (_rig != null) _rig.gameObject.SetActive(!on);
        if (_label != null) _label.gameObject.SetActive(!on);
        if (_ring != null) _ring.SetActive(TurnOn && !on);
    }

    /// <summary>A player at another table: no labels, no shadows, and a mood that changes now and then by itself.</summary>
    public void MakeAmbient()
    {
        _ambient = true;
        AutoLife = true;
        if (_label != null) _label.gameObject.SetActive(false);
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    Mood AmbientMood(float t)
    {
        int phase = Mathf.FloorToInt(t / 3.2f);
        int h = (phase * 7919 + (int)(_seed * 97f)) & 63;
        if (h < 34) return Mood.Idle;
        if (h < 50) return Mood.Thinking;
        if (h < 56) return Mood.Win;
        if (h < 60) return Mood.Lose;
        return Mood.Folded;
    }

    public void SetMood(Mood m) { CurrentMood = m; }

    public void Say(string text, float seconds)
    {
        if (_bubble == null) return;
        _bubble.text = text;
        _bubbleUntil = Time.time + seconds;
    }

    // ---- tea and cigarettes -----------------------------------------------------------------------------

    /// <summary>How full the glass is, 0..1.</summary>
    public void SetTea(float level)
    {
        _tea = Mathf.Clamp01(level);
        if (_liquid == null) return;
        float h = 0.075f * Mathf.Max(0.02f, _tea);
        _liquid.localScale = new Vector3(0.056f, h * 0.5f, 0.056f);
        _liquid.localPosition = new Vector3(0f, 0.012f + h * 0.5f, 0f);
        _liquid.gameObject.SetActive(_tea > 0.04f);
    }

    public float Tea { get { return _tea; } }

    /// <summary>Raises the right hand and takes a sip of tea. Returns false if the player is busy.</summary>
    public bool Sip()
    {
        if (_act != Act.None) return false;
        _act = Act.Sip; _actT = 0f; _actLen = 3.0f; _drankNow = false;
        return true;
    }

    /// <summary>Raises the left hand and takes a drag from a cigarette.</summary>
    public bool Cigarette()
    {
        if (_act != Act.None) return false;
        _act = Act.Smoke; _actT = 0f; _actLen = 4.6f; _drankNow = false;
        if (_cig != null) _cig.SetActive(true);
        return true;
    }

    static float Envelope(float u)
    {
        if (u <= 0f || u >= 1f) return 0f;
        if (u < 0.22f) return Mathf.SmoothStep(0f, 1f, u / 0.22f);
        if (u < 0.72f) return 1f;
        return Mathf.SmoothStep(1f, 0f, (u - 0.72f) / 0.28f);
    }

    /// <summary>Angles of the upper arm and forearm (degrees, 0 = hanging down, -65 = lying on the table) for a raised amount a.</summary>
    void PoseArms(float right, float left)
    {
        SetArm(1, Mathf.Lerp(-65f, -50f, right), Mathf.Lerp(-65f, -175f, right));
        SetArm(0, Mathf.Lerp(-65f, -50f, left), Mathf.Lerp(-65f, -172f, left));
    }

    void SetArm(int idx, float upper, float fore)
    {
        if (_shoulder[idx] == null) return;
        _shoulder[idx].localRotation = Quaternion.Euler(upper, idx == 1 ? -4f : 4f, 0f);
        _elbow[idx].localRotation = Quaternion.Euler(fore - upper, 0f, 0f);
    }

    void UpdateAct()
    {
        float right = 0f, left = 0f, tilt = 0f;
        if (_act != Act.None)
        {
            _actT += Time.deltaTime;
            float u = _actT / _actLen;
            float a = Envelope(u);
            if (_act == Act.Sip)
            {
                right = a;
                tilt = -30f * Mathf.Clamp01((a - 0.7f) / 0.3f);
                if (!_drankNow && u > 0.40f) { _drankNow = true; SetTea(_tea - 0.2f); }
                if (a > 0.9f && Time.time >= _nextPuff && _glassPivot != null)
                {
                    _nextPuff = Time.time + 0.45f;
                    Smoke.Puff(_glassPivot.position + Vector3.up * 0.12f, new Vector3(0f, 0.12f, 0f), 0.05f, 1.6f, 0.95f);
                }
            }
            else
            {
                left = a;
                if (_ember != null) _ember.sharedMaterial = Mats.Unlit(Color.Lerp(new Color(0.85f, 0.25f, 0.05f), new Color(1f, 0.8f, 0.3f), a));
                if (a > 0.9f && Time.time >= _nextPuff && _cigPivot != null)
                {
                    _nextPuff = Time.time + 0.5f;
                    Smoke.Puff(_cigPivot.position + Vector3.up * 0.1f, new Vector3(0f, 0.10f, 0f), 0.04f, 2f, 0.8f);
                }
                if (!_drankNow && u > 0.78f && _mouth != null)
                {
                    // the exhale: a cloud in front of the face
                    _drankNow = true;
                    Vector3 fwd = transform.forward;
                    for (int i = 0; i < 4; i++)
                        Smoke.Puff(_mouth.position + fwd * 0.12f, fwd * (0.22f + i * 0.04f) + new Vector3(0f, 0.10f, 0f), 0.10f + i * 0.02f, 2.6f);
                }
            }
            if (_actT >= _actLen)
            {
                _act = Act.None;
                if (_cig != null) _cig.SetActive(false);
                _nextAuto = Time.time + 8f + (_seed * 5.3f) % 10f;
            }
        }
        PoseArms(right, left);
        // the glass and the cigarette keep their own orientation in space, whatever the hand does
        if (_glassPivot != null)
            _glassPivot.rotation = transform.rotation * Quaternion.Euler(tilt, 0f, 0f);
        if (_cigPivot != null)
            _cigPivot.rotation = transform.rotation * Quaternion.Euler(-20f, 0f, 0f);

        // the bots and the other tables live their own lives
        if (AutoLife && _act == Act.None && !TurnOn && !_firstPerson && CurrentMood == Mood.Idle && Time.time >= _nextAuto)
        {
            bool smoke = Smoker && ((int)(Time.time * 7f + _seed * 13f)) % 5 < 2;
            if (smoke) Cigarette(); else { if (_tea < 0.25f) SetTea(1f); Sip(); }
            _nextAuto = Time.time + 12f;
        }
    }

    void Update()
    {
        float t = Time.time + _seed;
        if (_ambient) CurrentMood = AmbientMood(t);
        float bob = Mathf.Sin(t * 1.7f) * 0.012f;
        float lean = 0f, tilt = 0f, lift = 0f;
        switch (CurrentMood)
        {
            case Mood.Idle: lean = Mathf.Sin(t * 0.9f) * 1.5f; break;
            case Mood.Thinking: lean = 7f + Mathf.Sin(t * 3f) * 1.5f; tilt = Mathf.Sin(t * 1.3f) * 3f; break;
            case Mood.Win: lift = Mathf.Abs(Mathf.Sin(t * 6f)) * 0.16f; lean = -6f; break;
            case Mood.Lose: lean = 16f; tilt = Mathf.Sin(t * 1.5f) * 2f; break;
            case Mood.Folded: lean = -9f; break;
            case Mood.Out: lean = 26f; tilt = 9f; break;
        }
        if (_rig != null)
        {
            _rig.localPosition = new Vector3(0f, 0.5f + bob + lift, 0f);
            _rig.localRotation = Quaternion.Slerp(_rig.localRotation, Quaternion.Euler(lean, 0f, tilt), Mathf.Clamp01(Time.deltaTime * 6f));
        }
        if (_bubbleUntil > 0f && Time.time > _bubbleUntil)
        {
            _bubble.text = "";
            _bubbleUntil = 0f;
        }
        UpdateAct();
    }

    void LateUpdate()
    {
        if (CameraT != null && _label != null)
        {
            _label.rotation = CameraT.rotation;
            // far seats keep a readable label, the near seat's label does not cover the table
            float dist = (CameraT.position - _label.position).magnitude;
            _label.localScale = Vector3.one * Mathf.Clamp(dist / 9f, 0.55f, 1.2f);
        }
    }
}
