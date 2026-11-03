using UnityEngine;

/// <summary>Builds a pile of chips (lekë) for an amount: a greedy split into chip colours, each topped with a small eagle.</summary>
public static class ChipPile
{
    static readonly int[] Values = { 100, 25, 10, 5, 1 };
    static readonly Color[] Colors =
    {
        new Color(0.10f, 0.10f, 0.12f),   // 100 black
        new Color(0.80f, 0.10f, 0.16f),   // 25 red
        new Color(0.84f, 0.64f, 0.18f),   // 10 gold
        new Color(0.93f, 0.92f, 0.88f),   // 5 cream
        new Color(0.42f, 0.52f, 0.66f)    // 1 steel blue
    };

    public const float ChipHeight = 0.03f;

    public static GameObject Build(Transform parent, int amount, Vector3 worldPos, bool pop = true)
    {
        var root = new GameObject("Chips " + amount);
        root.transform.SetParent(parent, false);
        root.transform.position = worldPos;

        int remaining = amount;
        var counts = new int[Values.Length];
        int columns = 0;
        for (int i = 0; i < Values.Length; i++)
        {
            counts[i] = remaining / Values[i];
            remaining -= counts[i] * Values[i];
            if (counts[i] > 0) columns++;
        }

        int col = 0;
        for (int i = 0; i < Values.Length; i++)
        {
            if (counts[i] <= 0) continue;
            // up to 3 columns per row so a pile with many denominations stays compact
            int perRow = columns > 3 ? 3 : columns;
            int rows = (columns + perRow - 1) / perRow;
            int row = col / perRow;
            int inRow = col % perRow;
            int rowCount = Mathf.Min(perRow, columns - row * perRow);
            float x = (inRow - (rowCount - 1) * 0.5f) * 0.23f;
            float z = (row - (rows - 1) * 0.5f) * 0.23f;
            int shown = Mathf.Min(counts[i], 14);
            for (int k = 0; k < shown; k++)
            {
                Prim.Make(PrimitiveType.Cylinder, root.transform,
                    new Vector3(x, k * ChipHeight + ChipHeight * 0.5f, z),
                    new Vector3(0.20f, ChipHeight * 0.5f, 0.20f), Mats.Lit(Colors[i], 0.5f), "Chip");
                // lighter edge stripe
                Prim.Make(PrimitiveType.Cylinder, root.transform,
                    new Vector3(x, k * ChipHeight + ChipHeight * 0.5f, z),
                    new Vector3(0.205f, ChipHeight * 0.18f, 0.205f), Mats.Lit(Color.Lerp(Colors[i], Color.white, 0.75f), 0.5f), "Stripe");
            }
            // the eagle on the top chip
            Color mark = i == 0 ? Theme.Gold : (i == 3 ? Theme.Black : Color.Lerp(Colors[i], Color.black, 0.7f));
            Prim.FlatQuad(root.transform, new Vector3(x, shown * ChipHeight + 0.002f, z), 0.15f, 0.15f, mark, Theme.Eagle, true);
            col++;
        }

        if (pop)
        {
            var tw = root.AddComponent<Tweener>();
            tw.Scale(Vector3.one * 0.55f, Vector3.one, 0.22f);
        }
        else root.AddComponent<Tweener>();
        return root;
    }
}
