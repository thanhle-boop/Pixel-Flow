using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class ImageToConfigTool : EditorWindow
{
    private Texture2D _inputImage;
    private float _spacing = 1.0f;
    private int _targetWidthCount = 20;
    private int _targetHighCount = 20;
    private GameObject _blockGroup;
    private GameObject _pigGroup;
    
    [Header("Pigs")]
    private readonly Dictionary<string, List<GameObject>> _pigs = new Dictionary<string, List<GameObject>>();
    private readonly Dictionary<string, GameObject> _pigPrefabs = new Dictionary<string, GameObject>();
    
    [Header("Blocks")]
    private readonly Dictionary<string, int> _colorCount = new Dictionary<string, int>();
    private readonly Dictionary<string, int> _edgeColorCount = new Dictionary<string, int>();
    private readonly Dictionary<string, GameObject> _blockPrefabs = new Dictionary<string, GameObject>();


    [MenuItem("Tools/Advanced Color Generator")]
    public static void ShowWindow() => GetWindow<ImageToConfigTool>("Color Tool");

    private void OnGUI()
    {
        _inputImage = (Texture2D)EditorGUILayout.ObjectField("Input Image", _inputImage, typeof(Texture2D), false);
        _spacing = EditorGUILayout.FloatField("Spacing", _spacing);
        _targetWidthCount = EditorGUILayout.IntField("Target Width Count",_targetWidthCount);
        _targetHighCount = EditorGUILayout.IntField("Target High Count",_targetHighCount);
        LoadAsset();
        if (GUILayout.Button($"Generate {_targetWidthCount}x{_targetHighCount} Grid")) GenerateMapAndPig();
    }

    private async void LoadAsset()
    {
        try
        {
            string[] colors = { "Red", "Green", "Yellow", "Orange", "Black", "White", "Pink","Blue" };
        
            foreach (var c in colors)
            {
                // Load Block Prefab
                var opBlock = Addressables.LoadAssetAsync<GameObject>($"{c}");
                await opBlock.Task;
                if (opBlock.Status == AsyncOperationStatus.Succeeded)
                    _blockPrefabs[c.ToLower()] = opBlock.Result;

                // Load Pig Prefab
                var opPig = Addressables.LoadAssetAsync<GameObject>($"Pig {c}");
                await opPig.Task;
                if (opPig.Status == AsyncOperationStatus.Succeeded)
                    _pigPrefabs[c.ToLower()] = opPig.Result;
            }
        }
        catch (Exception e)
        {
            // Debug.Log("Can't load asset: " + e.Message);
        }
    }
    
    private void GenerateMapAndPig()
    {
        ResetData();
        GenerateMapBaseOnConfig();
        GeneratePigBaseOnConfig();
    }
    
    private void GeneratePigBaseOnConfig()
{
    _pigGroup = new GameObject("Pig Configuration")
    {
        transform =
        {
            position = new Vector3(0, -10, 0)
        }
    };

    var thresholdVeryHigh = 350;
    var thresholdHigh = 100;
    var totalPigsGenerated = 0; 

    foreach (var data in _colorCount)
    {
        var pigPrefab = _pigPrefabs[data.Key];
        var remainingBullet = Mathf.CeilToInt(data.Value / 10f) * 10;
        
        if (remainingBullet <= 0) continue;

        int targetBulletPerPig;
        
        if (remainingBullet >= thresholdVeryHigh)
        {
            targetBulletPerPig = 50;
        }
        else if (remainingBullet >= thresholdHigh)
        {
            targetBulletPerPig = 40;
        }
        else
        {
            targetBulletPerPig = 20;
        }
        
        var pigCountForColor = Mathf.CeilToInt((float)remainingBullet / targetBulletPerPig);
        
        var minPigsRequired = Mathf.CeilToInt(remainingBullet / 50f);
        pigCountForColor = Mathf.Max(pigCountForColor, minPigsRequired);
        
        var baseChunk = (remainingBullet / pigCountForColor) / 10 * 10;
        var remainderBullet = (remainingBullet - (baseChunk * pigCountForColor)) / 10;

        var pigComponents = new List<GameObject>();

        for (var i = 0; i < pigCountForColor; i++)
        {
            var bulletCount = baseChunk + (i < remainderBullet ? 10 : 0);
            
            var spawnedPig = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(pigPrefab);
            
            if (spawnedPig.TryGetComponent<PigComponent>(out var pigComp))
            {
                pigComp.SetBulletCount(bulletCount);
            }
            
            pigComponents.Add(spawnedPig);
            
            var pigTransform = spawnedPig.transform;
            pigTransform.SetParent(_pigGroup.transform);
            pigTransform.localPosition = Vector3.zero;
            
            totalPigsGenerated++;
        }
        
        _pigs[data.Key] = pigComponents;
    }
    

    if (totalPigsGenerated is < 10 or > 20)
    {
        Debug.LogWarning($"Chú ý: Tổng số heo tạo ra là {totalPigsGenerated}. Nó đang nằm ngoài khoảng 10-20 do số lượng đạn config từ level.");
    }

    ArrangePig();
}

    private void  GenerateMapBaseOnConfig()
    {
        _blockGroup = new GameObject("Image Configuration")
        {
            transform =
            {
                position = new Vector3(0, 5, 0)
            }
        };
        var stepX = (float)_inputImage.width / _targetWidthCount;
        var stepY = (float)_inputImage.height / _targetHighCount;
        
        var gridWidth = (_targetWidthCount - 1) * _spacing;
        var gridHeight = (_targetHighCount - 1) * _spacing;
        var offset = new Vector3(-gridWidth / 2f, -gridHeight / 2f, 0);

        for (var i = 0; i < _targetHighCount; i++)
        {
            for (var j = 0; j < _targetWidthCount; j++)
            {
                var x = Mathf.FloorToInt(j * stepX + (stepX / 2f));
                var y = Mathf.FloorToInt(i * stepY + (stepY / 2f));
                var pixelColor = _inputImage.GetPixel(x, y);
                
                var color = GetClosestColor(pixelColor);
                var selectedPrefab = _blockPrefabs[color];
                
                if (!_colorCount.TryAdd(color, 1))
                {
                    _colorCount[color]++;
                }

                if (i == 0 || i == _targetHighCount - 1 || j == 0 || j == _targetWidthCount - 1)
                {
                    if (!_edgeColorCount.TryAdd(color, 1))
                    {
                        _edgeColorCount[color]++;
                    }
                }
                var pos = new Vector3(j * _spacing, i * _spacing, 0) + offset;
                var block = (GameObject)PrefabUtility.InstantiatePrefab(selectedPrefab);
                block.transform.parent = _blockGroup.transform;
                block.transform.localPosition = pos;
            }
        }
    }

    private void ArrangePig()
{
    var primaryColor = MostColoredAtEdge(_edgeColorCount);
    var pigsPrimary = _pigs[primaryColor];
    
    var totalPigs = _pigs.Sum(pigColor => pigColor.Value.Count);
    
    var currentLv = 10;
    var maxLv = 50;

    var maxCols = GetColumnCountBasedOnLevel(currentLv);
    
    // SỬA LỖI 1: Bắt buộc để maxRows = totalPigs để mảng đủ dài, 
    // tránh lỗi tràn mảng khi posY nhảy cóc (step) xuống các hàng dưới.
    var maxRows = totalPigs; 
    var queue2Matrix = new GameObject[maxCols, maxRows];

    var maxDistance = Mathf.Min(2, maxRows / 2 + 1);
    const int stepLimit = 3;

    var dl = (float)currentLv / maxLv;
    var lastY = -1;

    for (var n = 0; n < pigsPrimary.Count; n++)
    {
        var posX = UnityEngine.Random.Range(0, maxCols);
        var step = UnityEngine.Random.Range(1 + n / pigsPrimary.Count, stepLimit);
        var posY = 0;
        
        if (n == 0 || lastY >= maxDistance)
        {
            posY = (int)(dl * UnityEngine.Random.Range(0, maxDistance));
        }
        else
        {
            posY = lastY + step;
        }

        // Đảm bảo posY không bao giờ vượt quá giới hạn mảng
        if (posY >= maxRows) posY = maxRows - 1;

        if (queue2Matrix[posX, posY] == null)
        {
            queue2Matrix[posX, posY] = pigsPrimary[n];
        }
        else
        {
            // SỬA LỖI 2: Dùng for để tìm chỗ trống an toàn thay vì dùng while(random)
            bool isPlaced = false;

            // Thử random 10 lần xem có ăn may trúng ô trống không
            for (int attempt = 0; attempt < 10; attempt++)
            {
                var reRandomX = UnityEngine.Random.Range(0, maxCols);
                if (queue2Matrix[reRandomX, posY] == null)
                {
                    queue2Matrix[reRandomX, posY] = pigsPrimary[n];
                    isPlaced = true;
                    break;
                }
            }

            // Nếu random 10 lần vẫn xui (hoặc hàng đã chật), ta quét tuần tự từ trái qua phải để nhét heo vào ô trống đầu tiên thấy được
            if (!isPlaced)
            {
                for (int x = 0; x < maxCols; x++)
                {
                    if (queue2Matrix[x, posY] == null)
                    {
                        queue2Matrix[x, posY] = pigsPrimary[n];
                        isPlaced = true;
                        break;
                    }
                }
            }

            // Trường hợp cực đoan: Nếu hàng này đã đầy 100%, ta bắt buộc đẩy con heo này xuống hàng tiếp theo (posY + 1)
            if (!isPlaced)
            {
                posY++;
                if (posY >= maxRows) posY = maxRows - 1; 

                for (int x = 0; x < maxCols; x++)
                {
                    if (queue2Matrix[x, posY] == null)
                    {
                        queue2Matrix[x, posY] = pigsPrimary[n];
                        break;
                    }
                }
            }
        }
        lastY = posY;
    }
    
    FillRemainingSlots(queue2Matrix, maxRows, maxCols, primaryColor);
}
    
    private int GetColumnCountBasedOnLevel(int currentLevel)
    {
        // Lấy một số ngẫu nhiên từ 0 đến 99 để làm tỉ lệ phần trăm
        int randomChance = UnityEngine.Random.Range(0, 100);

        if (currentLevel <= 15)
        {
            // 20% ra cột 2 | 70% ra cột 3 | 10% ra cột 4
            if (randomChance < 20) return 2;
            if (randomChance < 90) return 3; // 20 + 70 = 90
            return 4;
        }
        else if (currentLevel <= 30)
        {
            // 47% ra cột 3 | 53% ra cột 4
            if (randomChance < 47) return 3;
            return 4;
        }
        else // Level 31 trở đi
        {
            // Có thể lác đác vài cột 3 (vd 10%), 65% cột 4, 25% cột 5
            if (randomChance < 10) return 3;
            if (randomChance < 75) return 4;
            return 5;
        }
    }

    private void FillRemainingSlots(GameObject[,] queue2Matrix, int maxRows, int maxCols,string primaryColor)
    {
        var allFillerPigs = new List<GameObject>();
        foreach (var pair in _pigs.Where(pair => pair.Key != primaryColor))
        {
            allFillerPigs.AddRange(pair.Value);
        }
        
        ShuffleList(allFillerPigs);
        
        for (var y = 0; y < maxRows; y++)
        {
            for (var x = 0; x < maxCols; x++)
            {
                if (queue2Matrix[x, y] != null) continue;
                if (allFillerPigs.Count <= 0) continue;
                var filterPig = allFillerPigs[0];
                allFillerPigs.RemoveAt(0);
                    
                queue2Matrix[x, y] = filterPig;
            }
        }
        ApplyPhysicalPositions(queue2Matrix, maxCols, maxRows);
    }
    
    private static void ApplyPhysicalPositions(GameObject[,] queue2Matrix, int maxCols, int maxRows)
    {
        const float spacing = 2.5f;
        var totalWidth = (maxCols - 1) * spacing;
        var startX = -totalWidth / 2f;

        for (var y = 0; y < maxRows; y++)
        {
            for (var x = 0; x < maxCols; x++)
            {
                var pigObj = queue2Matrix[x, y];
                if (pigObj == null) continue;
                var posX = startX + (x * spacing);
                var posY = y * -spacing;
                pigObj.transform.localPosition = new Vector3(posX, posY, 0);
            }
        }
    }
    
    private static void ShuffleList(List<GameObject> list)
    {
        for (var i = 0; i < list.Count; i++)
        {
            var temp = list[i];
            var randomIndex = UnityEngine.Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    private static string GetClosestColor(Color c)
    {
        var targetRed = new Color(0.82f, 0.14f, 0.13f);
        var targetGreen = new Color(0.53f, 0.83f, 0.36f);
        var targetYellow = new Color(0.98f, 0.87f, 0.24f);
        var targetOrange = new Color(0.95f, 0.57f, 0.14f);
        var targetBlack = Color.black;
        var targetWhite = Color.white;
        var targetPink = new Color(0.97f, 0f, 0.9f);
        var targetBlue = new Color(0.26f, 0.95f, 0.95f);
        
        var distRed = ColorDistance(c, targetRed);
        var distGreen = ColorDistance(c, targetGreen);
        var distYellow = ColorDistance(c, targetYellow);
        var distOrange = ColorDistance(c, targetOrange);
        var distBlack = ColorDistance(c, targetBlack);
        var distWhite = ColorDistance(c, targetWhite);
        var distPink = ColorDistance(c, targetPink);
        var distBlue = ColorDistance(c, targetBlue);

        var minDist = Mathf.Min(distRed, distGreen, distYellow, distOrange, distBlack, distWhite, distPink,distBlue);
        
        if (minDist > 0.75f) return null;

        if (Mathf.Approximately(minDist, distRed)) return "red";
        if (Mathf.Approximately(minDist, distGreen)) return "green";
        if (Mathf.Approximately(minDist, distYellow)) return "yellow";
        if (Mathf.Approximately(minDist, distOrange)) return "orange";
        if (Mathf.Approximately(minDist, distBlack)) return "black";
        if (Mathf.Approximately(minDist, distBlue)) return "blue";
        return Mathf.Approximately(minDist, distWhite) ? "white" : "pink";
    }

    private static string MostColoredAtEdge(Dictionary<string, int> dict)
    {
        var maxCount = 0;
        var maxColor = "";
        foreach (var pair in dict.Where(pair => pair.Value > maxCount))
        {
            maxCount = pair.Value;
            maxColor = pair.Key;
        }
        return maxColor;
    }

    private static float ColorDistance(Color c1, Color c2)
    {
        return Vector4.Distance(c1, c2);
    }
    
    private void ResetData()
    {
        _colorCount.Clear();
        _edgeColorCount.Clear();
        _pigs.Clear();
        DestroyImmediate(_blockGroup);
        DestroyImmediate(_pigGroup);
    }
}