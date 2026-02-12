using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class ImageToConfigTool : EditorWindow
{
    private Texture2D inputImage;
    private float spacing = 1.0f;
    private int targetCount = 20;

    [Header("Block Prefabs")]
    public GameObject redPrefab;
    public GameObject greenPrefab;
    public GameObject yellowPrefab;
    public GameObject orangePrefab;
    public GameObject blackPrefab;
    public GameObject whitePrefab;
    public GameObject pinkPrefab;

    [Header("Pig Prefabs")]
    public GameObject pigRedPrefab;
    public GameObject pigGreenPrefab;
    public GameObject pigYellowPrefab;
    public GameObject pigOrangePrefab;
    public GameObject pigBlackPrefab;
    public GameObject pigWhitePrefab;
    public GameObject pigPinkPrefab;

    private Dictionary<string, int> colorCount = new Dictionary<string, int>();
    private Dictionary<string, int> edgeColorCount = new Dictionary<string, int>();
    private Dictionary<string, List<GameObject>> Pigs = new Dictionary<string, List<GameObject>>();

    [MenuItem("Tools/Advanced Color Generator")]
    public static void ShowWindow() => GetWindow<ImageToConfigTool>("Color Tool");

    void OnGUI()
    {
        inputImage = (Texture2D)EditorGUILayout.ObjectField("Input Image", inputImage, typeof(Texture2D), false);
        redPrefab = (GameObject)EditorGUILayout.ObjectField("Red Prefab", redPrefab, typeof(GameObject), false);
        greenPrefab = (GameObject)EditorGUILayout.ObjectField("Green Prefab", greenPrefab, typeof(GameObject), false);
        yellowPrefab = (GameObject)EditorGUILayout.ObjectField("Yellow Prefab", yellowPrefab, typeof(GameObject), false);
        orangePrefab = (GameObject)EditorGUILayout.ObjectField("Orange Prefab", orangePrefab, typeof(GameObject), false);
        blackPrefab = (GameObject)EditorGUILayout.ObjectField("Black Prefab", blackPrefab, typeof(GameObject), false);
        whitePrefab = (GameObject)EditorGUILayout.ObjectField("White Prefab", whitePrefab, typeof(GameObject), false);
        pinkPrefab = (GameObject)EditorGUILayout.ObjectField("Pink Prefab", pinkPrefab, typeof(GameObject), false);

        pigRedPrefab = (GameObject)EditorGUILayout.ObjectField("Pig Red Prefab", pigRedPrefab, typeof(GameObject), false);
        pigGreenPrefab = (GameObject)EditorGUILayout.ObjectField("Pig Green Prefab", pigGreenPrefab, typeof(GameObject), false);
        pigYellowPrefab = (GameObject)EditorGUILayout.ObjectField("Pig Yellow Prefab", pigYellowPrefab, typeof(GameObject), false);
        pigOrangePrefab = (GameObject)EditorGUILayout.ObjectField("Pig Orange Prefab", pigOrangePrefab, typeof(GameObject), false);
        pigBlackPrefab = (GameObject)EditorGUILayout.ObjectField("Pig Black Prefab", pigBlackPrefab, typeof(GameObject), false);
        pigWhitePrefab = (GameObject)EditorGUILayout.ObjectField("Pig White Prefab", pigWhitePrefab, typeof(GameObject), false);
        pigPinkPrefab = (GameObject)EditorGUILayout.ObjectField("Pig Pink Prefab", pigPinkPrefab, typeof(GameObject), false);


        spacing = EditorGUILayout.FloatField("Spacing", spacing);
        targetCount = EditorGUILayout.IntField("Target Count", targetCount);

        if (GUILayout.Button($"Generate {targetCount}x{targetCount} Grid")) Generate();
    }

    void Generate()
    {
        if (inputImage == null) return;

        GameObject container = new GameObject("Generated_Art");
        float stepX = (float)inputImage.width / targetCount;
        float stepY = (float)inputImage.height / targetCount;

        for (int i = 0; i < targetCount; i++)
        {
            for (int j = 0; j < targetCount; j++)
            {
                // Lấy mẫu tại tâm của ô để đạt độ chính xác cao nhất
                int x = Mathf.FloorToInt(j * stepX + (stepX / 2f));
                int y = Mathf.FloorToInt(i * stepY + (stepY / 2f));
                Color pixelColor = inputImage.GetPixel(x, y);
                
                string color = GetClosestPrefab(pixelColor);
                GameObject selectedPrefab = null;
                switch (color)
                {
                    case "red":
                        selectedPrefab = redPrefab;
                        break;
                    case "green":
                        selectedPrefab = greenPrefab;
                        break;
                    case "yellow":
                        selectedPrefab = yellowPrefab;
                        break;
                    case "orange":
                        selectedPrefab = orangePrefab;
                        break;
                    case "black":
                        selectedPrefab = blackPrefab;
                        break;
                    case "white":
                        selectedPrefab = whitePrefab;
                        break;
                    case "pink":
                        selectedPrefab = pinkPrefab;
                        break;
                    
                }

                if (colorCount.ContainsKey(color))
                {
                    colorCount[color]++;
                }
                else
                {
                    colorCount[color] = 1;
                }

                if (i == 0 || i == targetCount - 1 || j == 0 || j == targetCount - 1)
                {
                    if (edgeColorCount.ContainsKey(color))
                    {
                        edgeColorCount[color]++;
                    }
                    else
                    {
                        edgeColorCount[color] = 1;
                    }
                }

                if (selectedPrefab != null)
                {
                    Vector3 pos = new Vector3(j * spacing, i * spacing, 0);
                    GameObject block = (GameObject)PrefabUtility.InstantiatePrefab(selectedPrefab);
                    block.transform.position = pos;
                    block.transform.parent = container.transform;
                }
            }
        }

        // create pig components

        foreach (var data in colorCount)
        {
            GameObject pigPrefab = null;
            switch (data.Key)
            {
                case "red":
                    pigPrefab = pigRedPrefab;
                    break;
                case "green":
                    pigPrefab = pigGreenPrefab;
                    break;
                case "yellow":
                    pigPrefab = pigYellowPrefab;
                    break;
                case "orange":
                    pigPrefab = pigOrangePrefab;
                    break;
                case "black":
                    pigPrefab = pigBlackPrefab;
                    break;
                case "white":
                    pigPrefab = pigWhitePrefab;
                    break;
                case "pink":
                    pigPrefab = pigPinkPrefab;
                    break;
            }

            int remainingBullet = Mathf.CeilToInt(data.Value/10f) * 10;
            int[] bulletNumbers = {10,20,30,40};
            List<GameObject> pigComponents = new List<GameObject>();
            while(remainingBullet > 0)
            {
                int bullet = bulletNumbers[UnityEngine.Random.Range(0, bulletNumbers.Length)];
                GameObject spawnedPig = (GameObject)PrefabUtility.InstantiatePrefab(pigPrefab);
                if(bullet > remainingBullet)
                {
                    bullet = remainingBullet;
                }
                spawnedPig.GetComponent<PigComponent>().SetBulletCount(bullet);
                remainingBullet -= bullet;
                pigComponents.Add(spawnedPig);
            }

            // PigComponent pigComponent = container.AddComponent<PigComponent>();
            // pigComponent.pigColor = mostColoredPrefab.GetComponent<Renderer>().sharedMaterial.color;
            Pigs[data.Key] = pigComponents;
        }
        
    }

    string GetClosestPrefab(Color c)
    {
        Color targetRed = new Color(0.82f, 0.14f, 0.13f);
        Color targetGreen = new Color(0.53f, 0.83f, 0.36f);
        Color targetYellow = new Color(0.98f, 0.87f, 0.24f);
        Color targetOrange = new Color(0.95f, 0.57f, 0.14f);
        Color targetBlack = Color.black;
        Color targetWhite = Color.white;
        Color targetPink = new Color(0.97f, 0f, 0.9f);

        // Tính khoảng cách Euclidean giữa màu pixel và các màu mục tiêu
        float distRed = ColorDistance(c, targetRed);
        float distGreen = ColorDistance(c, targetGreen);
        float distYellow = ColorDistance(c, targetYellow);
        float distOrange = ColorDistance(c, targetOrange);
        float distBlack = ColorDistance(c, targetBlack);
        float distWhite = ColorDistance(c, targetWhite);
        float distPink = ColorDistance(c, targetPink);

        // Tìm khoảng cách nhỏ nhất
        float minDist = Mathf.Min(distRed, distGreen, distYellow, distOrange, distBlack, distWhite, distPink);

        // Giới hạn sai số (nếu ảnh cực sắc nét thì minDist sẽ gần bằng 0)
        if (minDist > 0.75f) return null; // Không khớp màu nào thì không spawn

        if (minDist == distRed) return "red";
        if (minDist == distGreen) return "green";
        if (minDist == distYellow) return "yellow";
        if (minDist == distOrange) return "orange";
        if (minDist == distBlack) return "black";
        if (minDist == distWhite) return "white";
        return "pink";
    }

    private GameObject MostColoredPrefab(Dictionary<GameObject, int> dict)
    {
        GameObject mostColored = null;
        int maxCount = 0;
        foreach (var pair in dict)
        {
            if (pair.Value > maxCount)
            {
                maxCount = pair.Value;
                mostColored = pair.Key;
            }
        }
        return mostColored;
    }

    float ColorDistance(Color c1, Color c2)
    {
        // Tính khoảng cách giữa 2 Vector4 (R, G, B, A)
        return Vector4.Distance(c1, c2);
    }
}