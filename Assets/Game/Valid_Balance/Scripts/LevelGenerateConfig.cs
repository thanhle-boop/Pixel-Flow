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
    [SerializeField] private TextMeshProUGUI difficultyLogText; 
    [Header("Image DataConfig")] 
    [SerializeField] private ImageConfig[] imageConfigList;

    #region 1. Nhóm Độ Khó của Map (Board Complexity)
    [Header("1. Map Settings")]
    [Range(2, 10)] 
    [Tooltip("Số lượng màu tối đa (Color Diversity)")]
    [SerializeField] private int targetColorCount = 5; 
    
    [Range(0f, 1f)] 
    [Tooltip("0 = Mảng to dễ ăn, 1 = Màu lốm đốm xen kẽ (Dithering)")]
    [SerializeField] private float colorFragmentation = 0.2f; 
    #endregion

    #region 2. Nhóm Cấu Trúc Hàng Chờ (Queue Structure)
    [Header("2. Queue Settings")]
    [Range(2, 5)] 
    [Tooltip("Số cột hiển thị của Queue 2")]
    [SerializeField] private int maxQueueColumns = 4;
    
    [Range(1, 5)] 
    [Tooltip("Độ trễ/Khoảng cách hàng giữa các Heo chủ đạo")]
    [SerializeField] private int primaryPigDepth = 2; 
    
    [Tooltip("Chỉ số sức chứa hàng chờ đạn thừa (để tính toán log)")]
    [SerializeField] private int queue1MaxCapacity = 5; 
    #endregion

    #region Private Variables
    private int imageIndex = 0;
    private Texture2D inputImage;
    private float _spacing = 0;
    private int _targetWidthCount = 20;
    private int _targetHightCount = 20;

    private GameObject _blockGroup;
    [SerializeField] private RectTransform pigScrollContent;

    private readonly Dictionary<string, List<GameObject>> _pigs = new Dictionary<string, List<GameObject>>();
    private readonly Dictionary<string, GameObject> _pigPrefabs = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, int> _colorCount = new Dictionary<string, int>();
    private readonly Dictionary<string, int> _edgeColorCount = new Dictionary<string, int>();
    private readonly Dictionary<string, GameObject> _blockPrefabs = new Dictionary<string, GameObject>();
    
    private string[,] _gridColorMap; 
    #endregion

    private void LoadAndSetImageConfig(int index)
    {
        inputImage = imageConfigList[index].textureInput;
        _spacing =  imageConfigList[index].spacing;
        _targetHightCount = imageConfigList[index].height;
        _targetWidthCount = imageConfigList[index].width;
        titleText.text = $"Level {imageConfigList[index].levelIndex}";
        GenerateMapAndPig();
    }
    
    private async void Start()
    {
        await LoadAssetAsync();
        LoadAndSetImageConfig(imageIndex);
    }

    private async Task LoadAssetAsync()
    {
        try
        {
            string[] colors = { "Red", "Green", "Yellow", "Orange", "Black", "White", "Pink", "Blue", "Dark Green", "Dark Pink", "Dark Blue" };
            var loadTasks = new List<Task>();
            foreach (var c in colors) loadTasks.Add(LoadColorDataAsync(c));
            await Task.WhenAll(loadTasks);
        }
        catch (Exception e) { Debug.LogError(e.Message); }
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
        EvaluateTotalDifficulty(); 
    }

    private void GenerateMapBaseOnConfig()
    {
        _blockGroup = new GameObject("Image Configuration") { transform = { position = new Vector3(0, 5, 0) } };
        var stepX = (float)inputImage.width / _targetWidthCount;
        var stepY = (float)inputImage.height / _targetHightCount;
        var gridWidth = (_targetWidthCount - 1) * _spacing;
        var gridHeight = (_targetHightCount - 1) * _spacing;
        var offset = new Vector3(-gridWidth / 2f, -gridHeight / 2f, 0);

        _gridColorMap = new string[_targetWidthCount, _targetHightCount];
        Color[,] pixelData = new Color[_targetWidthCount, _targetHightCount];

        Dictionary<string, int> tempColorFreq = new Dictionary<string, int>();
        for (var i = 0; i < _targetHightCount; i++)
        {
            for (var j = 0; j < _targetWidthCount; j++)
            {
                var px = Mathf.FloorToInt(j * stepX + (stepX / 2f));
                var py = Mathf.FloorToInt(i * stepY + (stepY / 2f));
                Color c = inputImage.GetPixel(px, py);
                pixelData[j, i] = c;

                string cName = Helper.GetClosestColor(c);
                if (cName != null)
                {
                    if (!tempColorFreq.ContainsKey(cName)) tempColorFreq[cName] = 0;
                    tempColorFreq[cName]++;
                }
            }
        }

        List<string> allowedColors = tempColorFreq.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).Take(targetColorCount).ToList();

        for (var i = 0; i < _targetHightCount; i++)
        {
            for (var j = 0; j < _targetWidthCount; j++)
            {
                Color oldColor = pixelData[j, i];
                string colorName = Helper.GetClosestColor(oldColor);

                if (colorName == null || !_blockPrefabs.ContainsKey(colorName)) continue;
                if (!allowedColors.Contains(colorName)) colorName = allowedColors[0]; 

                _gridColorMap[j, i] = colorName;

                if (!_colorCount.TryAdd(colorName, 1)) _colorCount[colorName]++;
                if (i == 0 || i == _targetHightCount - 1 || j == 0 || j == _targetWidthCount - 1)
                {
                    if (!_edgeColorCount.TryAdd(colorName, 1)) _edgeColorCount[colorName]++;
                }

                if (colorFragmentation > 0.01f)
                {
                    Color newColor = GetColorValueFromName(colorName);
                    Color error = (oldColor - newColor) * colorFragmentation;
                    if (j + 1 < _targetWidthCount) pixelData[j + 1, i] += error * (7f / 16f);
                    if (i + 1 < _targetHightCount) pixelData[j, i + 1] += error * (5f / 16f);
                }

                var pos = new Vector3(j * _spacing, i * _spacing, 0) + offset;
                var block = Instantiate(_blockPrefabs[colorName], _blockGroup.transform);
                block.transform.localScale = (_targetWidthCount > 20 || _targetHightCount > 20) ? new Vector3(0.25f, 0.25f, 0.25f) : new Vector3(0.4f, 0.4f, 0.4f);
                block.transform.localPosition = pos;
            }
        }
    }

    private void GeneratePigBaseOnConfig()
    {
        var primaryColor = Helper.MostColoredAtEdge(_edgeColorCount);
        var filterColors = _colorCount.Where(x => x.Key != primaryColor).ToList();
        
        var totalFilterPigs = 0;
        var filterPigCounts = new Dictionary<string, int>();

        foreach (var data in filterColors)
        {
            var exactBullets = data.Value; // Lấy chính xác số lượng block
            if (exactBullets <= 0) continue;
            
            var targetBullet = (exactBullets >= 350) ? 50 : (exactBullets > 100 ? 40 : 20);
            var pigCount = Mathf.Max(Mathf.CeilToInt((float)exactBullets / targetBullet), Mathf.CeilToInt(exactBullets / 50f));
            
            filterPigCounts[data.Key] = pigCount;
            totalFilterPigs += pigCount;
        }
        
        var primaryBullets = 0;
        var primaryPigCount = 0;
        if (_colorCount.TryGetValue(primaryColor, out var value))
        {
            primaryBullets = value; // Lấy chính xác số lượng block
            var primaryTarget = (primaryBullets >= 350) ? 50 : (primaryBullets >= 100 ? 40 : 20);
            primaryPigCount = Mathf.Max(Mathf.CeilToInt((float)primaryBullets / primaryTarget), Mathf.CeilToInt(primaryBullets / 50f));
        }
        
        var currentTotalPigs = totalFilterPigs + primaryPigCount;
        var remainder = currentTotalPigs % maxQueueColumns;
        if (remainder != 0) primaryPigCount += (maxQueueColumns - remainder);

        CreatePigsForColor(primaryColor, primaryBullets, primaryPigCount);
        foreach (var kvp in filterPigCounts)
        {
            var exactBullets = _colorCount[kvp.Key];
            CreatePigsForColor(kvp.Key, exactBullets, kvp.Value);
        }

        ArrangePig();
    }

    private void CreatePigsForColor(string colorKey, int totalBullets, int pigCount)
    {
        if (!_pigPrefabs.TryGetValue(colorKey, out var prefab) || pigCount <= 0) return;
        var pigList = new List<GameObject>();
        
        // Chia đạn sao cho tổng số đạn sinh ra BẰNG ĐÚNG totalBullets (không sai một viên)
        int baseBullet = totalBullets / pigCount;
        int bulletRemainder = totalBullets % pigCount;

        for (var i = 0; i < pigCount; i++)
        {
            int bulletCount = baseBullet + (i < bulletRemainder ? 1 : 0);
            if (bulletCount <= 0) continue; // Bỏ qua nếu con lợn này không có đạn

            var spawnedPig = Instantiate(prefab, pigScrollContent);
            spawnedPig.name = $"Pig {colorKey} ({bulletCount})";

            if (spawnedPig.TryGetComponent<PigComponent>(out var pigComp))
            {
                // pigComp.SetBulletCount(bulletCount);
            }
            pigList.Add(spawnedPig);
        }
        _pigs[colorKey] = pigList;
    }

    private void ArrangePig()
    {
        var primaryColor = Helper.MostColoredAtEdge(_edgeColorCount);
        if (!_pigs.TryGetValue(primaryColor, out var pigsPrimary)) return;

        var totalPigs = _pigs.Sum(pigColor => pigColor.Value.Count);
        var maxRows = Mathf.CeilToInt((float)totalPigs / maxQueueColumns);
        var queue2Matrix = new GameObject[maxQueueColumns, maxRows];

        var stepLimit = primaryPigDepth; 
        var lastY = -1;

        for (var n = 0; n < pigsPrimary.Count; n++)
        {
            var posX = UnityEngine.Random.Range(0, maxQueueColumns);
            var step = UnityEngine.Random.Range(1, stepLimit + 1);
            var posY = 0;
            
            if (n == 0) posY = 0;
            else posY = lastY + step;

            if (posY >= maxRows) posY = maxRows - 1;

            if (queue2Matrix[posX, posY] == null) queue2Matrix[posX, posY] = pigsPrimary[n];
            else
            {
                var isPlaced = false;
                for (var x = 0; x < maxQueueColumns; x++)
                {
                    if (queue2Matrix[x, posY] == null) { queue2Matrix[x, posY] = pigsPrimary[n]; isPlaced = true; break; }
                }
                if (!isPlaced)
                {
                    posY = Mathf.Min(posY + 1, maxRows - 1);
                    for (var x = 0; x < maxQueueColumns; x++)
                    {
                        if (queue2Matrix[x, posY] == null) { queue2Matrix[x, posY] = pigsPrimary[n]; break; }
                    }
                }
            }
            lastY = posY;
        }

        FillRemainingSlots(queue2Matrix, maxRows, maxQueueColumns, primaryColor);
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
                rect.anchoredPosition = new Vector2(startX + (x * spacingX), -(y * spacingY) - (spacingY / 2f));
            }
            if (rowHasPig) lastActiveRow = y;
        }
        pigScrollContent.sizeDelta = new Vector2(pigScrollContent.sizeDelta.x, (lastActiveRow + 1) * spacingY + 100f);
    }

    private void EvaluateTotalDifficulty()
    {
        int islands = CalculateColorIslands();
        int totalBlocks = _targetWidthCount * _targetHightCount;
        
        float mapComplexityScore = ((float)islands / totalBlocks) * 100f; 
        float queueRestrictionScore = ((5f - maxQueueColumns) / 3f) * 100f; 

        // Trọng số mới: Bỏ phần tính điểm Heo Đặc Biệt và Kinh Tế, tập trung vào Map và Hàng chờ
        float finalScore = (mapComplexityScore * 0.6f) + (queueRestrictionScore * 0.4f);

        string hexColor = finalScore < 30 ? "#00FF00" : (finalScore < 60 ? "#FFFF00" : (finalScore < 80 ? "#FFA500" : "#FF0000"));
        string label = finalScore < 30 ? "DỄ" : (finalScore < 60 ? "TRUNG BÌNH" : (finalScore < 80 ? "KHÓ" : "ĐỊA NGỤC"));

        int totalPigsGenerated = _pigs.Sum(p => p.Value.Count);

        string logMessage = $"<color=#00FFFF>--- BÁO CÁO ĐỘ KHÓ LEVEL ---</color>\n" +
                            $"<size=90%>Màu cho phép: <b>{targetColorCount}</b> | Màu thực tế trên map: <b>{_colorCount.Count}</b>\n" +
                            $"Tổng Block: <b>{totalBlocks}</b> | Độ phân mảnh (Islands): <b>{islands}</b>\n" +
                            $"Tổng lợn sinh ra: <b>{totalPigsGenerated}</b> | Hàng chờ Queue 1: <b>{queue1MaxCapacity} ô</b>\n" +
                            $"<b>LƯU Ý: Chế độ Hardcore - Số lượng đạn sinh ra vừa khít 100% với số Block!</b></size>\n" +
                            $"<size=110%><b>ĐIỂM TỔNG HỢP: <color={hexColor}>{finalScore:F1}/100 ({label})</color></b></size>";

        if (difficultyLogText != null) difficultyLogText.text = logMessage;
        else Debug.Log(logMessage);
    }

    private int CalculateColorIslands()
    {
        bool[,] visited = new bool[_targetWidthCount, _targetHightCount];
        int islandCount = 0;
        for (int y = 0; y < _targetHightCount; y++)
        {
            for (int x = 0; x < _targetWidthCount; x++)
            {
                if (!visited[x, y] && !string.IsNullOrEmpty(_gridColorMap[x, y]))
                {
                    FloodFill(visited, x, y, _gridColorMap[x, y]);
                    islandCount++;
                }
            }
        }
        return islandCount;
    }

    private void FloodFill(bool[,] visited, int startX, int startY, string targetColor)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(new Vector2Int(startX, startY));
        visited[startX, startY] = true;
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (queue.Count > 0)
        {
            Vector2Int curr = queue.Dequeue();
            foreach (var d in dirs)
            {
                int nx = curr.x + d.x; int ny = curr.y + d.y;
                if (nx >= 0 && nx < _targetWidthCount && ny >= 0 && ny < _targetHightCount)
                {
                    if (!visited[nx, ny] && _gridColorMap[nx, ny] == targetColor)
                    {
                        visited[nx, ny] = true;
                        queue.Enqueue(new Vector2Int(nx, ny));
                    }
                }
            }
        }
    }

    private Color GetColorValueFromName(string colorName)
    {
        switch (colorName.ToLower())
        {
            case "red": return new Color(0.82f, 0.14f, 0.13f);
            case "green": return new Color(0.53f, 0.83f, 0.36f);
            case "yellow": return new Color(0.98f, 0.87f, 0.24f);
            case "orange": return new Color(0.95f, 0.57f, 0.14f);
            case "black": return Color.black;
            case "white": return Color.white;
            case "pink": return new Color(0.97f, 0f, 0.9f);
            case "blue": return new Color(0.26f, 0.95f, 0.95f);
            default: return Color.white;
        }
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