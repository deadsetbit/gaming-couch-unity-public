using System.Collections.Generic;
using DSB.GC;
using UnityEngine;

public struct GCPlayerColorVariants
{
    public Color BaseColor;
    public Color Dark;
    public Color Light;
    public Color OffWhite;

    public GCPlayerColorVariants(Color baseColor, Color dark, Color light, Color offWhite)
    {
        this.BaseColor = baseColor;
        this.Dark = dark;
        this.Light = light;
        this.OffWhite = offWhite;
    }
}

public static class GCPlayerColorData
{
    public static readonly Dictionary<GCPlayerColor, GCPlayerColorVariants> Variants = new()
    {
        { GCPlayerColor.blue, new GCPlayerColorVariants(
            new Color(7f / 255f, 78f / 255f, 234f / 255f), // #074EEA
            new Color(23f / 255f, 37f / 255f, 84f / 255f), // #172554
            new Color(185f / 255f, 229f / 255f, 252f / 255f), // #B9E5FC
            new Color(235f / 255f, 253f / 255f, 255f / 255f) // #EBFDFF
        )},
        { GCPlayerColor.red, new GCPlayerColorVariants(
            new Color(243f / 255f, 63f / 255f, 94f / 255f), // #F33F5E
            new Color(76f / 255f, 5f / 255f, 25f / 255f), // #4C0519
            new Color(253f / 255f, 204f / 255f, 210f / 255f), // #FDCCD2
            new Color(253f / 255f, 241f / 255f, 241f / 255f) // #FDF1F1
        )},
        { GCPlayerColor.green, new GCPlayerColorVariants(
            new Color(75f / 255f, 130f / 255f, 22f / 255f), // #4B8216
            new Color(22f / 255f, 101f / 255f, 52f / 255f), // #166534
            new Color(216f / 255f, 248f / 255f, 156f / 255f), // #D8F89C
            new Color(246f / 255f, 253f / 255f, 230f / 255f) // #F6FDE6
        )},
        { GCPlayerColor.yellow, new GCPlayerColorVariants(
            new Color(250f / 255f, 190f / 255f, 36f / 255f), // #FABE24
            new Color(179f / 255f, 83f / 255f, 9f / 255f), // #B35309
            new Color(253f / 255f, 239f / 255f, 137f / 255f), // #FDEF89
            new Color(253f / 255f, 251f / 255f, 231f / 255f) // #FDFBE7
        )},
        { GCPlayerColor.purple, new GCPlayerColorVariants(
            new Color(138f / 255f, 92f / 255f, 245f / 255f), // #8A5CF5
            new Color(88f / 255f, 28f / 255f, 134f / 255f), // #581C86
            new Color(215f / 255f, 179f / 255f, 253f / 255f), // #D7B3FD
            new Color(242f / 255f, 231f / 255f, 255f / 255f) // #F2E7FF
        )},
        { GCPlayerColor.pink, new GCPlayerColorVariants(
            new Color(251f / 255f, 164f / 255f, 164f / 255f), // #FBA4A4
            new Color(218f / 255f, 39f / 255f, 119f / 255f), // #DA2777
            new Color(255f / 255f, 240f / 255f, 241f / 255f), // #FFF0F1
            new Color(253f / 255f, 251f / 255f, 231f / 255f) // #FDFBE7
        )},
        { GCPlayerColor.cyan, new GCPlayerColorVariants(
            new Color(45f / 255f, 211f / 255f, 190f / 255f), // #2DD3BE
            new Color(3f / 255f, 105f / 255f, 160f / 255f), // #0369A0
            new Color(219f / 255f, 251f / 255f, 230f / 255f), // #DBFBE6
            new Color(239f / 255f, 252f / 255f, 249f / 255f) // #EFFCF9
        )},
        { GCPlayerColor.brown, new GCPlayerColorVariants(
            new Color(158f / 255f, 72f / 255f, 35f / 255f), // #9E4823
            new Color(67f / 255f, 20f / 255f, 7f / 255f), // #431407
            new Color(201f / 255f, 137f / 255f, 4f / 255f), // #C98904
            new Color(252f / 255f, 229f / 255f, 137f / 255f) // #FCE589
        )}
    };
}
