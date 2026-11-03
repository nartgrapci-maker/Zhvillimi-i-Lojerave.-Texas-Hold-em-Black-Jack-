using UnityEngine;
using PokerDemo.Core;

/// <summary>A playing card built from primitives: a thin white box with the rank and suit symbols, and a red back with a black eagle.</summary>
public class CardView : MonoBehaviour
{
    public const float W = 0.5f, H = 0.7f, T = 0.018f;

    public Card Card { get; private set; }
    public Tweener Tween { get; private set; }

    public static Quaternion FaceUp(float yaw) { return Quaternion.Euler(0f, yaw, 0f); }
    public static Quaternion FaceDown(float yaw) { return Quaternion.Euler(0f, yaw, 180f); }

    public static CardView Create(Transform parent, Card card, Vector3 worldPos, Quaternion worldRot)
    {
        var root = new GameObject("Card " + card);
        root.transform.SetParent(parent, false);
        root.transform.position = worldPos;
        root.transform.rotation = worldRot;

        CardView cv = root.AddComponent<CardView>();
        cv.Card = card;
        cv.Tween = root.AddComponent<Tweener>();

        Prim.Make(PrimitiveType.Cube, root.transform, Vector3.zero, new Vector3(W, T, H), Mats.Lit(new Color(0.96f, 0.95f, 0.91f), 0.35f), "Body");

        Color ink = card.IsRed ? new Color(0.78f, 0.07f, 0.10f) : new Color(0.07f, 0.07f, 0.11f);
        float y = T * 0.5f + 0.0012f;
        Texture2D suit = SuitTextures.Get(card.Suit);

        // front (top side)
        Prim.FlatQuad(root.transform, new Vector3(0.02f, y, -0.07f), 0.30f, 0.30f, ink, suit, true);
        Prim.FlatQuad(root.transform, new Vector3(-0.155f, y, 0.145f), 0.115f, 0.115f, ink, suit, true);
        TextMesh rank = Mats.Text(root.transform, Card.RankLabel(card.Rank), 0.15f, ink, new Vector3(-0.155f, y + 0.0008f, 0.255f));
        rank.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        // back (bottom side): red field, gold frame, black double-headed eagle
        Prim.FlatQuad(root.transform, new Vector3(0f, -y, 0f), W * 0.92f, H * 0.94f, Theme.DarkGold, null, false);
        Prim.FlatQuad(root.transform, new Vector3(0f, -y - 0.0006f, 0f), W * 0.84f, H * 0.87f, Theme.Red, null, false);
        Prim.FlatQuad(root.transform, new Vector3(0f, -y - 0.0012f, 0f), 0.40f, 0.40f, Theme.Black, Theme.Eagle, false);
        return cv;
    }
}
