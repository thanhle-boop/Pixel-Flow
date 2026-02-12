using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class ImageToConfigTool : EditorWindow
{
    public Texture2D inputImage;
    public GameObject yellowBlockPrefab;
    public GameObject purpleBlockPrefab;
    public float spacing = 1.0f;

    [MenuItem("Tools/Image to Config Generator")]
    public static void ShowWindow()
    {
        GetWindow<ImageToConfigTool>("Image Config Tool");
    }

    void OnGUI()
    {
        GUILayout.Label("Settings", EditorStyles.boldLabel);
        inputImage = (Texture2D)EditorGUILayout.ObjectField("Input Image", inputImage, typeof(Texture2D), false);
        yellowBlockPrefab = (GameObject)EditorGUILayout.ObjectField("Yellow Block Prefab", yellowBlockPrefab, typeof(GameObject), false);
        purpleBlockPrefab = (GameObject)EditorGUILayout.ObjectField("Purple Block Prefab", purpleBlockPrefab, typeof(GameObject), false);
        spacing = EditorGUILayout.FloatField("Block Spacing", spacing);

        if (GUILayout.Button("Generate Config & Preview"))
        {
            Generate();
        }
    }

void Generate()
{
    if (inputImage == null) { Debug.LogError("Please assign an image!"); return; }

    int width = inputImage.width;
    int height = inputImage.height;
    GameObject container = new GameObject("Image_Config_Preview");

    int targetCount = 20;
    float pixelStepX = (float)width / targetCount;
    float pixelStepY = (float)height / targetCount;

    for (int i = 0; i < targetCount; i++)
    {
        for (int j = 0; j < targetCount; j++)
        {

            int sampleX = Mathf.FloorToInt(j * pixelStepX + (pixelStepX / 2f));
            int sampleY = Mathf.FloorToInt(i * pixelStepY + (pixelStepY / 2f));

            Color pixelColor = inputImage.GetPixel(sampleX, sampleY);
            bool isYellow = (pixelColor.r > 0.5f && pixelColor.g > 0.5f && pixelColor.g > pixelColor.b);
            GameObject prefabToSpawn = isYellow ? yellowBlockPrefab : purpleBlockPrefab;

            if (prefabToSpawn != null)
            {
                float posX = j * spacing;
                float posY = i * spacing;

                Vector3 pos = new Vector3(posX, posY, 0);
                GameObject block = (GameObject)PrefabUtility.InstantiatePrefab(prefabToSpawn);
                block.transform.position = pos;
                block.transform.parent = container.transform;
            }
        }
    }
}
}