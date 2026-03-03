using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class ColorCountData
{
    public string colorName;
    public int count;
}

[CreateAssetMenu(fileName = "ImageConfig", menuName = "Create Image Config")]
public class ImageConfig : ScriptableObject
{
    [Header("Level Info")]
    public int levelIndex;
    
    [Header("Map Setting")]
    public Texture2D textureInput;
    public int width;
    public int height;
    public float spacing;

    [Header("Generated Data")]
    public List<ColorCountData> colorCounts = new List<ColorCountData>();
}