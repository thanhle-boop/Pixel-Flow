using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

public class LevelGenerateConfig : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [Header("Image DataConfig")] [SerializeField] private ImageConfig[] imageConfigList;

    #region Level Setting
    
    private int imageIndex = 0;
    private Texture2D inputImage;
    private float _spacing = 0;
    private int _targetWidthCount = 20;
    private int _targetHightCount = 20;
    private float _currentDifficulty = 0;
    private const int maxLevel = 100;

    private int maxCols = 0;

    private GameObject _blockGroup;
    
    [Header("UI References")]
    [SerializeField] private RectTransform pigScrollContent;
    [SerializeField] private Slider difficultySlider;

    private readonly Dictionary<string, List<GameObject>> _pigs = new Dictionary<string, List<GameObject>>();
    private readonly Dictionary<string, GameObject> _pigPrefabs = new Dictionary<string, GameObject>();
    
    private readonly Dictionary<string, int> _colorCount = new Dictionary<string, int>();
    private readonly Dictionary<string, int> _edgeColorCount = new Dictionary<string, int>();
    private readonly Dictionary<string, GameObject> _blockPrefabs = new Dictionary<string, GameObject>();
    
    #endregion

    private void LoadAndSetImageConfig(int index)
    {
        inputImage = imageConfigList[index].textureInput;
        _spacing =  imageConfigList[index].spacing;
        _targetHightCount = imageConfigList[index].height;
        _targetWidthCount = imageConfigList[index].width;
        _currentDifficulty = difficultySlider.value;
        titleText.text = $"Level {imageConfigList[index].levelIndex}";
        GenerateMapAndPig();
    }
    
    private async void Start()
    {
        await LoadAssetAsync();
        difficultySlider.value = 0.5f;
        LoadAndSetImageConfig(imageIndex);
    }

    private async Task LoadAssetAsync()
    {
        try
        {
            string[] colors = { "Red", "Green", "Yellow", "Orange", "Black", "White", "Pink", "Blue", "Dark Green", "Dark Pink", "Dark Blue" };
            var loadTasks = new List<Task>();

            foreach (var c in colors)
            {
                loadTasks.Add(LoadColorDataAsync(c));
            }
            
            await Task.WhenAll(loadTasks);
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message);
        }
    }
    
    private async Task LoadColorDataAsync(string colorName)
    {
        var key = colorName.ToLower();
        var blockPrefab = await Addressables.LoadAssetAsync<GameObject>($"{colorName}").Task;
        _blockPrefabs[key] = blockPrefab;

        var pigPrefab = await Addressables.LoadAssetAsync<GameObject>($"Pig {colorName}").Task;
        _pigPrefabs[key] = pigPrefab;
    }
    
    private void GenerateMapAndPig()
    {
        ResetData();
        GenerateMapBaseOnConfig();
        GeneratePigBaseOnConfig();
    }
    
private void GeneratePigBaseOnConfig()
{
    maxCols = GetColumnCountBasedOnDifficulty(_currentDifficulty);
    ResetData();
    GenerateMapBaseOnConfig();

    var primaryColor = Helper.MostColoredAtEdge(_edgeColorCount);
    
    var filterColors = _colorCount.Where(x => x.Key != primaryColor).ToList();
    var totalFilterPigs = 0;
    var filterPigCounts = new Dictionary<string, int>();

    foreach (var data in filterColors)
    {
        var bullets = Mathf.CeilToInt(data.Value / 10f) * 10;
        if (bullets <= 0) continue;
        
        var targetBullet = (bullets >= 350) ? 50 : (bullets > 100 ? 40 : 20);
        var pigCount = Mathf.Max(Mathf.CeilToInt((float)bullets / targetBullet), Mathf.CeilToInt(bullets / 50f));
        
        filterPigCounts[data.Key] = pigCount;
        totalFilterPigs += pigCount;
    }
    
    var primaryBullets = 0;
    var primaryPigCount = 0;
    if (_colorCount.TryGetValue(primaryColor, out var value))
    {
        primaryBullets = Mathf.CeilToInt(value / 10f) * 10;
        var primaryTarget = (primaryBullets >= 350) ? 50 : (primaryBullets >= 100 ? 40 : 20);
        primaryPigCount = Mathf.Max(Mathf.CeilToInt((float)primaryBullets / primaryTarget), Mathf.CeilToInt(primaryBullets / 50f));
    }
    
    var currentTotalPigs = totalFilterPigs + primaryPigCount;
    var remainder = currentTotalPigs % maxCols;
    if (remainder != 0)
    {
        var neededSlots = maxCols - remainder;
        primaryPigCount += neededSlots; 
    }

    CreatePigsForColor(primaryColor, primaryBullets, primaryPigCount);
    
    foreach (var kvp in filterPigCounts)
    {
        var bullets = Mathf.CeilToInt(_colorCount[kvp.Key] / 10f) * 10;
        CreatePigsForColor(kvp.Key, bullets, kvp.Value);
    }

    ArrangePig();
}

private void CreatePigsForColor(string colorKey, int totalBullets, int pigCount)
{
    if (!_pigPrefabs.TryGetValue(colorKey, out var prefab) || pigCount <= 0) return;

    var pigList = new List<GameObject>();
    
    var baseChunk = (totalBullets / pigCount) / 10 * 10;
    if (baseChunk < 10 && totalBullets >= 10) baseChunk = 10; 
    
    var remainderTenUnits = (totalBullets - (baseChunk * pigCount)) / 10;

    for (var i = 0; i < pigCount; i++)
    {
        var bulletCount = baseChunk + (i < remainderTenUnits ? 10 : 0);
        if (bulletCount <= 0 && totalBullets > 0) bulletCount = 10; 

        var spawnedPig = Instantiate(prefab, pigScrollContent);
        if (spawnedPig.TryGetComponent<PigComponent>(out var pigComp))
        {
            pigComp.SetBulletCount(bulletCount);
        }
        pigList.Add(spawnedPig);
    }
    _pigs[colorKey] = pigList;
}

    private void GenerateMapBaseOnConfig()
    {
        _blockGroup = new GameObject("Image Configuration") { transform = { position = new Vector3(0, 5, 0) } };
        var stepX = (float)inputImage.width / _targetWidthCount;
        var stepY = (float)inputImage.height / _targetHightCount;
        var gridWidth = (_targetWidthCount - 1) * _spacing;
        var gridHeight = (_targetHightCount - 1) * _spacing;
        var offset = new Vector3(-gridWidth / 2f, -gridHeight / 2f, 0);

        for (var i = 0; i < _targetHightCount; i++)
        {
            for (var j = 0; j < _targetWidthCount; j++)
            {
                var x = Mathf.FloorToInt(j * stepX + (stepX / 2f));
                var y = Mathf.FloorToInt(i * stepY + (stepY / 2f));
                var pixelColor = inputImage.GetPixel(x, y);
                var color = Helper.GetClosestColor(pixelColor);
                if (color == null || !_blockPrefabs.ContainsKey(color)) continue;

                if (!_colorCount.TryAdd(color, 1)) _colorCount[color]++;
                if (i == 0 || i == _targetHightCount - 1 || j == 0 || j == _targetWidthCount - 1)
                {
                    if (!_edgeColorCount.TryAdd(color, 1)) _edgeColorCount[color]++;
                }

                var pos = new Vector3(j * _spacing, i * _spacing, 0) + offset;
                var block = Instantiate(_blockPrefabs[color], _blockGroup.transform);
                block.transform.localScale = (_targetWidthCount > 20 || _targetHightCount > 20) ? new Vector3(0.25f, 0.25f, 0.25f) : new Vector3(0.4f, 0.4f, 0.4f);
                block.transform.localPosition = pos;
            }
        }
    }

    private void ArrangePig()
    {
        var primaryColor = Helper.MostColoredAtEdge(_edgeColorCount);
        if (!_pigs.TryGetValue(primaryColor, out var pigsPrimary)) return;

        var totalPigs = _pigs.Sum(pigColor => pigColor.Value.Count);

        var maxRows = Mathf.CeilToInt((float)totalPigs/maxCols);
        var queue2Matrix = new GameObject[maxCols, maxRows];

        var maxDistance = maxRows / 2 + 1;
        var stepLimit = Mathf.CeilToInt(3 * _currentDifficulty);
        
        var lastY = -1;

        for (var n = 0; n < pigsPrimary.Count; n++)
        {
            var posX = UnityEngine.Random.Range(0, maxCols);
            var step = UnityEngine.Random.Range(1 + n / pigsPrimary.Count, stepLimit);
            var posY = 0;
            
            if (n == 0 || lastY >= maxDistance)
                posY = (int)(_currentDifficulty * UnityEngine.Random.Range(0, maxDistance));
            else
                posY = lastY + step;

            if (posY >= maxRows) posY = maxRows - 1;

            if (queue2Matrix[posX, posY] == null)
                queue2Matrix[posX, posY] = pigsPrimary[n];
            else
            {
                var isPlaced = false;
                for (var attempt = 0; attempt < 10; attempt++)
                {
                    var reX = UnityEngine.Random.Range(0, maxCols);
                    if (queue2Matrix[reX, posY] == null) { queue2Matrix[reX, posY] = pigsPrimary[n]; isPlaced = true; break; }
                }
                if (!isPlaced)
                {
                    for (var x = 0; x < maxCols; x++)
                    {
                        if (queue2Matrix[x, posY] == null) { queue2Matrix[x, posY] = pigsPrimary[n]; isPlaced = true; break; }
                    }
                }
                if (!isPlaced)
                {
                    posY++;
                    if (posY >= maxRows) posY = maxRows - 1; 
                    for (var x = 0; x < maxCols; x++)
                    {
                        if (queue2Matrix[x, posY] == null) { queue2Matrix[x, posY] = pigsPrimary[n]; break; }
                    }
                }
            }
            lastY = posY;
        }

        FillRemainingSlots(queue2Matrix, maxRows, maxCols, primaryColor);
    }

    private void FillRemainingSlots(GameObject[,] queue2Matrix, int maxRows, int maxCols, string primaryColor)
    {
        var allFillerPigs = new List<GameObject>();
        foreach (var pair in _pigs.Where(pair => pair.Key != primaryColor))
            allFillerPigs.AddRange(pair.Value);
        
        Helper.ShuffleList(allFillerPigs);
        for (var y = 0; y < maxRows; y++)
        {
            for (var x = 0; x < maxCols; x++)
            {
                if (queue2Matrix[x, y] != null || allFillerPigs.Count <= 0) continue;
                var filterPig = allFillerPigs[0];
                allFillerPigs.RemoveAt(0);
                queue2Matrix[x, y] = filterPig;
            }
        }
        ApplyPhysicalPositions(queue2Matrix, maxCols, maxRows);
    }
    
private void ApplyPhysicalPositions(GameObject[,] queue2Matrix, int currentMaxCols, int maxRows)
{
    var spacingX = 150f; 
    var spacingY = 150f; 
    
    float totalWidth = (currentMaxCols - 1) * spacingX;
    float startX = -totalWidth / 2f;

    int lastActiveRow = 0;

    for (var y = 0; y < maxRows; y++)
    {
        bool rowHasPig = false;
        for (var x = 0; x < currentMaxCols; x++)
        {
            var pigObj = queue2Matrix[x, y];
            if (pigObj == null) continue;

            rowHasPig = true;
            RectTransform rect = pigObj.GetComponent<RectTransform>();
            
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            
            var posX = startX + (x * spacingX);
            var posY = -(y * spacingY) - (spacingY / 2f);
            
            rect.anchoredPosition = new Vector2(posX, posY);
        }
        if (rowHasPig) lastActiveRow = y;
    }
    
    float contentHeight = (lastActiveRow + 1) * spacingY + 100f;
    pigScrollContent.sizeDelta = new Vector2(pigScrollContent.sizeDelta.x, contentHeight);
}

private int GetColumnCountBasedOnDifficulty(float difficulty)
{
    difficulty = Mathf.Clamp01(difficulty);
    float columns = 5f - (difficulty * 3f);
    return Mathf.RoundToInt(columns);
}

    private void ResetData()
    {
        _colorCount.Clear();
        _edgeColorCount.Clear();
        _pigs.Clear();
        if (_blockGroup != null) Destroy(_blockGroup);
        foreach (Transform child in pigScrollContent) Destroy(child.gameObject);
    }

    public void NextLevelOnClick() { if(imageIndex < imageConfigList.Length - 1) { imageIndex++; LoadAndSetImageConfig(imageIndex); } }
    public void PrevLevelOnClick() { if(imageIndex > 0) { imageIndex--; LoadAndSetImageConfig(imageIndex); } }
    public void ReLoadPigPosition() => LoadAndSetImageConfig(imageIndex);
}