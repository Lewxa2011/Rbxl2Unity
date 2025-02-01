using UnityEngine;
using UnityEditor;
using System.Xml;
using System;
using System.Collections.Generic;
using System.IO;

public class RobloxMapUtil : EditorWindow
{
    private string mapPath;
    private int partCount;

    [MenuItem("Tools/RobloxMapUtil")]
    public static void ShowWindow()
    {
        GetWindow<RobloxMapUtil>("Roblox Map Utility");
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginVertical();

        GUILayout.Label("Roblox Map Importer Sigma", EditorStyles.boldLabel);
        GUILayout.Label($"Part Count: {partCount}");

        mapPath = EditorGUILayout.TextField(mapPath);

        if (GUILayout.Button("Import!"))
        {
            ImportMap(mapPath);
        }

        EditorGUILayout.EndVertical();
    }

    private void ImportMap(string mapPath)
    {
        partCount = 0;

        XmlDocument doc = new XmlDocument();
        doc.Load(mapPath);

        XmlNode root = doc.DocumentElement;
        Debug.Log("Root node: " + root.Name);

        XmlNodeList parts = doc.SelectNodes("//Item[@class='Part']");

        if (parts == null || parts.Count == 0)
        {
            Debug.LogError("No parts found in the document!");
            return;
        }

        float totalParts = parts.Count;
        GameObject parent = GameObject.Find($"{Path.GetFileNameWithoutExtension(mapPath)} PARENT");

        if (parent == null)
        {
            parent = new GameObject($"{Path.GetFileNameWithoutExtension(mapPath)} PARENT");
        }

        for (int i = 0; i < parts.Count; i++)
        {
            XmlNode part = parts[i];
            partCount++;

            XmlNode properties = part.SelectSingleNode("Properties");
            if (properties == null) continue;

            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            Vector3 size = Vector3.one;
            bool canCollide = true;
            bool anchored = false;
            int brickColorIndex = 26;
            float reflectance = 0.5f;
            float transparency = 0f;
            PrimitiveType shape = PrimitiveType.Cube;

            foreach (XmlNode property in properties.ChildNodes)
            {
                if (property.Attributes["name"] == null) continue;
                string propName = property.Attributes["name"].Value;

                switch (propName)
                {
                    case "CFrame":
                        (position, rotation) = ParseCFrame(property);
                        break;
                    case "size":
                        size = ParseVector3(property);
                        break;
                    case "CanCollide":
                        canCollide = bool.Parse(property.InnerText);
                        break;
                    case "Anchored":
                        anchored = bool.Parse(property.InnerText);
                        break;
                    case "BrickColor":
                        brickColorIndex = int.Parse(property.InnerText);
                        break;
                    case "shape":
                        shape = ParseShape(int.Parse(property.InnerText));
                        break;
                    case "Reflectance":
                        reflectance = float.Parse(property.InnerText);
                        break;
                    case "Transparency":
                        transparency = float.Parse(property.InnerText);
                        break;
                }
            }

            GameObject partObj = GameObject.CreatePrimitive(shape);
            partObj.transform.SetParent(parent.transform, true);
            partObj.name = $"Part_{partCount}";
            partObj.transform.position = position;
            partObj.transform.rotation = rotation;
            partObj.transform.localScale = size;
            partObj.GetComponent<Collider>().enabled = canCollide;
            if (!anchored)
            {
                partObj.AddComponent<Rigidbody>().isKinematic = true;
            }

            Renderer renderer = partObj.GetComponent<Renderer>();
            renderer.sharedMaterial = new Material(renderer.sharedMaterial);
            renderer.sharedMaterial.color = GetBrickColor(brickColorIndex);
            renderer.sharedMaterial.SetFloat("_Smoothness", reflectance);

            if (transparency > 0f)
            {
                renderer.sharedMaterial.SetFloat("_Surface", 1);
                renderer.sharedMaterial.SetFloat("_Blend", 1);
                Color color = renderer.sharedMaterial.color;
                color.a = 1 - transparency;
                renderer.sharedMaterial.color = color;
            }

            float progress = (i + 1) / totalParts;
            EditorUtility.DisplayProgressBar("Importing Map", $"Importing part {i + 1} of {parts.Count}", progress);
        }

        EditorUtility.ClearProgressBar();
    }

    private Vector3 ParseVector3(XmlNode vectorNode)
    {
        float x = float.Parse(vectorNode["X"].InnerText);
        float y = float.Parse(vectorNode["Y"].InnerText);
        float z = float.Parse(vectorNode["Z"].InnerText);
        return new Vector3(x, y, z);
    }

    private (Vector3, Quaternion) ParseCFrame(XmlNode cframeNode)
    {
        float x = float.Parse(cframeNode["X"].InnerText);
        float y = float.Parse(cframeNode["Y"].InnerText);
        float z = float.Parse(cframeNode["Z"].InnerText);
        Vector3 position = new Vector3(x, y, z);

        float r00 = float.Parse(cframeNode["R00"].InnerText);
        float r01 = float.Parse(cframeNode["R01"].InnerText);
        float r02 = float.Parse(cframeNode["R02"].InnerText);
        float r10 = float.Parse(cframeNode["R10"].InnerText);
        float r11 = float.Parse(cframeNode["R11"].InnerText);
        float r12 = float.Parse(cframeNode["R12"].InnerText);
        float r20 = float.Parse(cframeNode["R20"].InnerText);
        float r21 = float.Parse(cframeNode["R21"].InnerText);
        float r22 = float.Parse(cframeNode["R22"].InnerText);

        Matrix4x4 m = new Matrix4x4();
        m.SetColumn(0, new Vector4(r00, r10, r20, 0));
        m.SetColumn(1, new Vector4(r01, r11, r21, 0));
        m.SetColumn(2, new Vector4(r02, r12, r22, 0));
        m.SetColumn(3, new Vector4(x, y, z, 1));

        Quaternion rotation = m.rotation;
        return (position, rotation);
    }

    private PrimitiveType ParseShape(int shapeId)
    {
        return shapeId switch
        {
            0 => PrimitiveType.Cube,
            1 => PrimitiveType.Cube,
            2 => PrimitiveType.Cube,
            _ => PrimitiveType.Cube,
        };
    }

    private Color GetBrickColor(int brickColorIndex)
    {
        if (brickColorMapping.TryGetValue(brickColorIndex, out Color color))
        {
            return color;
        }
        else
        {
            Debug.LogWarning($"BrickColor index {brickColorIndex} not found. Using default gray.");
            return Color.gray;
        }
    }

    // giant ass mappings of all brick colors to unity colors

    private static readonly Dictionary<int, Color> brickColorMapping = new Dictionary<int, Color>()
    {
        { 1, new Color(242f/255f, 243f/255f, 243f/255f) },         // White
        { 5, new Color(215f/255f, 197f/255f, 154f/255f) },         // Brick Yellow
        { 9, new Color(232f/255f, 186f/255f, 200f/255f) },         // Light Reddish Violet
        { 11, new Color(128f/255f, 187f/255f, 219f/255f) },        // Pastel Blue
        { 18, new Color(204f/255f, 142f/255f, 105f/255f) },        // Nougat
        { 21, new Color(196f/255f, 40f/255f, 28f/255f) },          // Bright Red
        { 23, new Color(13f/255f, 105f/255f, 172f/255f) },         // Bright Blue
        { 24, new Color(245f/255f, 208f/255f, 48f/255f) },         // Bright Yellow
        { 26, new Color(27f/255f, 42f/255f, 53f/255f) },           // Black
        { 28, new Color(40f/255f, 127f/255f, 71f/255f) },          // Dark Green
        { 29, new Color(161f/255f, 196f/255f, 140f/255f) },         // Medium Green
        { 37, new Color(75f/255f, 151f/255f, 75f/255f) },          // Bright Green
        { 38, new Color(160f/255f, 95f/255f, 53f/255f) },          // Dark Orange
        { 45, new Color(180f/255f, 210f/255f, 228f/255f) },        // Light Blue
        { 101, new Color(218f/255f, 134f/255f, 122f/255f) },       // Medium Red
        { 102, new Color(110f/255f, 153f/255f, 202f/255f) },       // Medium Blue
        { 104, new Color(107f/255f, 50f/255f, 124f/255f) },        // Bright Violet
        { 105, new Color(226f/255f, 155f/255f, 64f/255f) },        // Br. Yellowish Orange
        { 106, new Color(218f/255f, 133f/255f, 65f/255f) },        // Bright Orange
        { 107, new Color(0f/255f, 143f/255f, 156f/255f) },         // Bright Bluish Green
        { 119, new Color(164f/255f, 189f/255f, 71f/255f) },        // Br. Yellowish Green
        { 125, new Color(234f/255f, 184f/255f, 146f/255f) },       // Light Orange
        { 135, new Color(116f/255f, 134f/255f, 157f/255f) },       // Sand Blue
        { 141, new Color(39f/255f, 70f/255f, 45f/255f) },          // Earth Green
        { 151, new Color(120f/255f, 144f/255f, 130f/255f) },       // Sand Green
        { 153, new Color(149f/255f, 121f/255f, 119f/255f) },       // Sand Red
        { 192, new Color(105f/255f, 64f/255f, 40f/255f) },         // Reddish Brown
        { 194, new Color(163f/255f, 162f/255f, 165f/255f) },       // Medium Stone Grey
        { 199, new Color(99f/255f, 95f/255f, 98f/255f) },          // Dark Stone Grey
        { 208, new Color(229f/255f, 228f/255f, 223f/255f) },       // Light Stone Grey
        { 217, new Color(124f/255f, 92f/255f, 70f/255f) },         // Brown
        { 226, new Color(253f/255f, 234f/255f, 141f/255f) },       // Cool Yellow
        { 1001, new Color(248f/255f, 248f/255f, 248f/255f) },      // Institutional White
        { 1002, new Color(205f/255f, 205f/255f, 205f/255f) },      // Mid Grey
        { 1003, new Color(17f/255f, 17f/255f, 17f/255f) },         // Really Black
        { 1004, new Color(255f/255f, 0f/255f, 0f/255f) },          // Really Red
        { 1005, new Color(213f/255f, 115f/255f, 61f/255f) },       // Neon Orange
        { 1006, new Color(180f/255f, 128f/255f, 255f/255f) },      // Alder
        { 1007, new Color(163f/255f, 75f/255f, 75f/255f) },        // Dusty Rose
        { 1008, new Color(193f/255f, 190f/255f, 66f/255f) },       // Olive
        { 1009, new Color(255f/255f, 255f/255f, 0f/255f) },        // New Yeller
        { 1010, new Color(0f/255f, 0f/255f, 255f/255f) },          // Really Blue
        { 1011, new Color(0f/255f, 32f/255f, 96f/255f) },          // Navy Blue
        { 1012, new Color(33f/255f, 84f/255f, 185f/255f) },        // Deep Blue
        { 1013, new Color(4f/255f, 175f/255f, 236f/255f) },        // Cyan
        { 1014, new Color(170f/255f, 85f/255f, 0f/255f) },         // CGA Brown
        { 1015, new Color(170f/255f, 0f/255f, 170f/255f) },        // Magenta
        { 1016, new Color(255f/255f, 102f/255f, 204f/255f) },      // Pink
        { 1017, new Color(255f/255f, 175f/255f, 0f/255f) },        // Deep Orange
        { 1018, new Color(18f/255f, 238f/255f, 212f/255f) },       // Teal
        { 1019, new Color(0f/255f, 255f/255f, 255f/255f) },        // Toothpaste
        { 1020, new Color(0f/255f, 255f/255f, 0f/255f) },          // Lime Green
        { 1021, new Color(58f/255f, 125f/255f, 21f/255f) },        // Camo
        { 1022, new Color(127f/255f, 142f/255f, 100f/255f) },      // Grime
        { 1023, new Color(140f/255f, 91f/255f, 159f/255f) },       // Lavender
        { 1024, new Color(175f/255f, 221f/255f, 255f/255f) },      // Pastel Light Blue
        { 1025, new Color(255f/255f, 201f/255f, 201f/255f) },      // Pastel Orange
        { 1026, new Color(177f/255f, 167f/255f, 255f/255f) },      // Pastel Violet
        { 1027, new Color(159f/255f, 243f/255f, 233f/255f) },      // Pastel Blue-Green
        { 1028, new Color(204f/255f, 255f/255f, 204f/255f) },      // Pastel Green
        { 1029, new Color(255f/255f, 255f/255f, 204f/255f) },      // Pastel Yellow
        { 1030, new Color(255f/255f, 204f/255f, 153f/255f) },      // Pastel Brown
        { 1031, new Color(98f/255f, 37f/255f, 209f/255f) },        // Royal Purple
        { 1032, new Color(255f/255f, 0f/255f, 191f/255f) },        // Hot Pink

        { 2, new Color(161f/255f, 165f/255f, 162f/255f) },          // Grey
        { 3, new Color(249f/255f, 233f/255f, 153f/255f) },          // Light Yellow
        { 6, new Color(194f/255f, 218f/255f, 184f/255f) },          // Light Green (Mint)
        { 12, new Color(203f/255f, 132f/255f, 66f/255f) },          // Light Orange Brown
        { 22, new Color(196f/255f, 112f/255f, 160f/255f) },         // Med. Reddish Violet
        { 25, new Color(98f/255f, 71f/255f, 50f/255f) },            // Earth Orange
        { 27, new Color(109f/255f, 110f/255f, 108f/255f) },         // Dark Grey
        { 36, new Color(243f/255f, 207f/255f, 155f/255f) },         // Lig. Yellowish Orange
        { 39, new Color(193f/255f, 202f/255f, 222f/255f) },         // Light Bluish Violet
        { 40, new Color(236f/255f, 236f/255f, 236f/255f) },         // Transparent
        { 41, new Color(205f/255f, 84f/255f, 75f/255f) },          // Tr. Red
        { 42, new Color(193f/255f, 223f/255f, 240f/255f) },         // Tr. Lg Blue
        { 43, new Color(123f/255f, 182f/255f, 232f/255f) },         // Tr. Blue
        { 44, new Color(247f/255f, 241f/255f, 141f/255f) },         // Tr. Yellow
        { 47, new Color(217f/255f, 133f/255f, 108f/255f) },         // Tr. Flu. Reddish Orange
        { 48, new Color(132f/255f, 182f/255f, 141f/255f) },         // Tr. Green
        { 49, new Color(248f/255f, 241f/255f, 132f/255f) },         // Tr. Flu. Green
        { 50, new Color(236f/255f, 232f/255f, 222f/255f) },         // Phosph. White
        { 100, new Color(238f/255f, 196f/255f, 182f/255f) },        // Light Red
        { 103, new Color(199f/255f, 193f/255f, 183f/255f) },        // Light Grey
        { 108, new Color(104f/255f, 92f/255f, 67f/255f) },          // Earth Yellow
        { 110, new Color(67f/255f, 84f/255f, 147f/255f) },          // Bright Bluish Violet
        { 111, new Color(191f/255f, 183f/255f, 177f/255f) },        // Tr. Brown
        { 112, new Color(104f/255f, 116f/255f, 172f/255f) },        // Medium Bluish Violet
        { 113, new Color(228f/255f, 173f/255f, 200f/255f) },        // Tr. Medi. Reddish Violet
        { 115, new Color(199f/255f, 210f/255f, 60f/255f) },         // Med. Yellowish Green
        { 116, new Color(85f/255f, 165f/255f, 175f/255f) },         // Med. Bluish Green
        { 118, new Color(183f/255f, 215f/255f, 213f/255f) },        // Light Bluish Green
        { 120, new Color(163f/255f, 162f/255f, 165f/255f) },        // Lig. Yellowish Green
        { 121, new Color(231f/255f, 172f/255f, 88f/255f) },         // Med. Yellowish Orange
        { 123, new Color(211f/255f, 111f/255f, 76f/255f) },         // Br. Reddish Orange
        { 124, new Color(146f/255f, 57f/255f, 120f/255f) },         // Bright Reddish Violet
        { 126, new Color(165f/255f, 165f/255f, 203f/255f) },        // Tr. Bright Bluish Violet
        { 127, new Color(239f/255f, 184f/255f, 56f/255f) },         // Gold
        { 128, new Color(174f/255f, 122f/255f, 89f/255f) },         // Dark Nougat
        { 131, new Color(156f/255f, 163f/255f, 168f/255f) },        // Silver
        { 133, new Color(213f/255f, 115f/255f, 61f/255f) },         // Neon Orange (dup?)
        { 134, new Color(216f/255f, 221f/255f, 86f/255f) },         // Neon Green
        { 136, new Color(135f/255f, 124f/255f, 144f/255f) },        // Sand Violet
        { 137, new Color(224f/255f, 152f/255f, 100f/255f) },        // Medium Orange
        { 138, new Color(149f/255f, 138f/255f, 115f/255f) },        // Sand Yellow
        { 140, new Color(32f/255f, 58f/255f, 86f/255f) },           // Earth Blue
        { 143, new Color(207f/255f, 226f/255f, 247f/255f) },        // Tr. Flu. Blue
        { 145, new Color(121f/255f, 136f/255f, 161f/255f) },        // Sand Blue Metallic
        { 146, new Color(149f/255f, 142f/255f, 163f/255f) },        // Sand Violet Metallic
        { 147, new Color(147f/255f, 135f/255f, 103f/255f) },        // Sand Yellow Metallic
        { 148, new Color(87f/255f, 88f/255f, 87f/255f) },           // Dark Grey Metallic
        { 149, new Color(22f/255f, 29f/255f, 50f/255f) },           // Black Metallic
        { 150, new Color(171f/255f, 173f/255f, 172f/255f) },        // Light Grey Metallic
        { 154, new Color(123f/255f, 46f/255f, 47f/255f) },          // Dark Red
        { 157, new Color(255f/255f, 246f/255f, 123f/255f) },        // Tr. Flu. Yellow
        { 158, new Color(225f/255f, 164f/255f, 194f/255f) },        // Tr. Flu. Red
        { 168, new Color(117f/255f, 108f/255f, 98f/255f) },         // Gun Metallic
        { 176, new Color(151f/255f, 105f/255f, 91f/255f) },         // Red Flip/Flop
        { 178, new Color(180f/255f, 132f/255f, 85f/255f) },         // Yellow Flip/Flop
        { 179, new Color(137f/255f, 135f/255f, 136f/255f) },        // Silver Flip/Flop
        { 180, new Color(215f/255f, 169f/255f, 75f/255f) },         // Curry
        { 190, new Color(249f/255f, 214f/255f, 46f/255f) },         // Fire Yellow
        { 191, new Color(232f/255f, 171f/255f, 45f/255f) },         // Flame Yellowish Orange
        { 193, new Color(207f/255f, 96f/255f, 36f/255f) },          // Flame Reddish Orange
        { 195, new Color(70f/255f, 103f/255f, 164f/255f) },         // Royal Blue
        { 196, new Color(35f/255f, 71f/255f, 139f/255f) },          // Dark Royal Blue
        { 198, new Color(142f/255f, 66f/255f, 133f/255f) },         // Bright Reddish Lilac
        { 200, new Color(130f/255f, 138f/255f, 93f/255f) },         // Lemon Metallic
        { 209, new Color(176f/255f, 142f/255f, 68f/255f) },         // Dark Curry
        { 210, new Color(112f/255f, 149f/255f, 120f/255f) },        // Faded Green
        { 211, new Color(121f/255f, 181f/255f, 181f/255f) },        // Turquoise
        { 212, new Color(159f/255f, 195f/255f, 233f/255f) },        // Light Royal Blue
        { 213, new Color(108f/255f, 129f/255f, 183f/255f) },        // Medium Royal Blue
        { 216, new Color(143f/255f, 76f/255f, 42f/255f) },          // Rust
        { 218, new Color(150f/255f, 112f/255f, 159f/255f) },        // Reddish Lilac
        { 219, new Color(167f/255f, 94f/255f, 155f/255f) },         // Lilac
        { 220, new Color(167f/255f, 169f/255f, 206f/255f) },        // Light Lilac
        { 221, new Color(205f/255f, 98f/255f, 152f/255f) },         // Bright Purple
        { 222, new Color(228f/255f, 173f/255f, 200f/255f) },        // Light Purple
        { 223, new Color(220f/255f, 144f/255f, 149f/255f) },        // Light Pink
        { 224, new Color(240f/255f, 213f/255f, 160f/255f) },        // Light Brick Yellow
        { 225, new Color(235f/255f, 184f/255f, 127f/255f) },        // Warm Yellowish Orange
        { 232, new Color(125f/255f, 187f/255f, 221f/255f) },        // Dove Blue
        { 268, new Color(52f/255f, 43f/255f, 117f/255f) },          // Medium Lilac
    };
}