using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class AdvancedLevelGenerator : MonoBehaviour
{
    [Header("1. UI & Scene References")]
    [SerializeField] private TextMeshProUGUI infoText;
    [SerializeField] private RectTransform pigScrollContent;

    [Header("2. Input Data")]
    [SerializeField] private ImageConfig[] imageConfigList;
    [SerializeField] private int imageIndex = 0;

    [Header("3. Settings")]
    [Range(2, 100)] public int targetStepsInput = 12;
    [Range(2, 5)] public int queueColumns = 3;

    private GameObject _blockContainer;
    private string[,] _finalGridMap;
    private int _w, _h;
    private float _spacing, _scale;

    private readonly Dictionary<string, GameObject> _blockPrefabs = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, GameObject> _pigPrefabs = new Dictionary<string, GameObject>();
    private readonly List<GameObject> _generatedPigs = new List<GameObject>();

    private const int MAX_QUEUE_1 = 5;

    private class PigDataPool {
        public string color;
        public int bullets;
        public bool isUsed;
    }

    private async void Start()
    {
        await LoadAllAssets();
        GenerateLevel();
    }

    private async Task LoadAllAssets()
    {
        string[] colors = { "Red", "Green", "Yellow", "Orange", "Black", "White", "Pink", "Blue", "Dark Green", "Dark Pink", "Dark Blue" };
        foreach (var c in colors)
        {
            try {
                string key = c.ToLower();
                _blockPrefabs[key] = await Addressables.LoadAssetAsync<GameObject>(c).Task;
                _pigPrefabs[key] = await Addressables.LoadAssetAsync<GameObject>($"Pig {c}").Task;
            } catch { }
        }
    }

    public void GenerateLevel()
    {
        ResetData();
        var config = imageConfigList[imageIndex];
        Texture2D img = config.textureInput;

        _w = config.width; _h = config.height;
        _spacing = config.spacing;
        _scale = (_w * _h > 600) ? 0.25f : 0.45f;

        _finalGridMap = new string[_w, _h];
        float stepX = (float)img.width / _w;
        float stepY = (float)img.height / _h;

        for (int y = 0; y < _h; y++) {
            for (int x = 0; x < _w; x++) {
                Color col = img.GetPixel((int)(x * stepX + stepX / 2f), (int)(y * stepY + stepY / 2f));
                string cName = Helper.GetClosestColor(col);
                if (string.IsNullOrEmpty(cName) || !_blockPrefabs.ContainsKey(cName)) cName = "white";
                _finalGridMap[x, y] = cName;
            }
        }

        SpawnVisualBlocks();
        ProcessPigsAdaptiveExact();
    }

   private void ProcessPigsAdaptiveExact()
{
    var config = imageConfigList[imageIndex];
    List<PigDataPool> currentPigPool = new List<PigDataPool>();

    // 1. Khởi tạo heo theo tỉ lệ ban đầu (11 con)
    foreach (var data in config.colorCounts) {
        var colorKey = data.colorName.ToLower();
        int remaining = Mathf.CeilToInt(data.count / 10f) * 10;
        if (remaining <= 0) continue;
        int targetPerPig = (remaining >= 350) ? 50 : (remaining >= 100) ? 40 : 20;
        int pigCount = Mathf.Max(Mathf.CeilToInt((float)remaining / targetPerPig), 1);
        int baseChunk = (remaining / pigCount) / 10 * 10;
        int remainder = (remaining - (baseChunk * pigCount)) / 10;
        for (int i = 0; i < pigCount; i++) {
            currentPigPool.Add(new PigDataPool { color = colorKey, bullets = baseChunk + (i < remainder ? 10 : 0) });
        }
    }

    int requiredColors = currentPigPool.Select(p => p.color).Distinct().Count();
    int effectiveTarget = Mathf.Max(targetStepsInput, requiredColors);

    List<string> finalDeck = null;
    int finalSteps = -1;
    bool isFound = false;
    int adjustLimit = 40; 

    while (adjustLimit-- > 0 && !isFound)
    {
        if (currentPigPool.Count > effectiveTarget) {
            if (MergeTwoPigs(currentPigPool)) {
                continue;
            }
        }

        List<string> poolNames = currentPigPool.Select(p => p.color).ToList();
        int bestInLoop = -1;
        int worstInLoop = int.MaxValue;

        // --- THỬ SHUFFLE (200 lần) ---
        for (int i = 0; i < 200; i++) {
            var result = ExecuteSimulation(poolNames, currentPigPool);
            if (result.steps == effectiveTarget) {
                finalSteps = result.steps; finalDeck = result.deck;
                isFound = true; break;
            }
            if (result.steps != -1) {
                bestInLoop = Mathf.Max(bestInLoop, result.steps);
                worstInLoop = Mathf.Min(worstInLoop, result.steps);
            }
        }

        if (isFound) break;
        
        if (bestInLoop < effectiveTarget) {
            if (!SplitOneRandomColorPig(currentPigPool)) break;
        } 
        else if (worstInLoop > effectiveTarget) {
            if (!MergeTwoPigs(currentPigPool)) break;
        } 
        else {
            // Săn lùng 800 lần nếu Target nằm trong dải [Worst, Best]
            for (int i = 0; i < 800; i++) {
                var result = ExecuteSimulation(poolNames, currentPigPool);
                if (result.steps == effectiveTarget) {
                    finalSteps = result.steps; finalDeck = result.deck;
                    isFound = true; break;
                }
            }
        }
    }

    // Hiển thị kết quả
    if (finalDeck != null) {
        SpawnPigsFromSimulation(finalDeck, currentPigPool);
        infoText.text = $"Pigs: {currentPigPool.Count} | Target: {effectiveTarget}\n" +
                        $"<color=yellow>Actual: {finalSteps}</color>";
    }
    ArrangePigsUIFixed();
}
   
    private bool MergeTwoPigs(List<PigDataPool> pool)
    {
        int requiredColors = pool.Select(p => p.color).Distinct().Count();
        
        if (pool.Count <= requiredColors) return false;

        // Tìm màu nào đang có nhiều heo nhất để gộp
        var targetGroup = pool.GroupBy(p => p.color)
            .Where(g => g.Count() > 1)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        if (targetGroup != null)
        {
            var pigs = targetGroup.OrderBy(p => p.bullets).ToList();
            pigs[0].bullets += pigs[1].bullets;
            pool.Remove(pigs[1]);
            return true;
        }
        return false;
    }
    
    private (List<string> deck, int steps) ExecuteSimulation(List<string> poolNames, List<PigDataPool> pool)
    {
        var testDeck = poolNames.OrderBy(x => UnityEngine.Random.value).ToList();
        var tempBullets = pool.GroupBy(p => p.color)
            .ToDictionary(g => g.Key, g => g.Select(p => p.bullets).OrderByDescending(b => b).ToList());
        
        List<int> bulletsInOrder = new List<int>();
        foreach(var c in testDeck) {
            bulletsInOrder.Add(tempBullets[c][0]);
            tempBullets[c].RemoveAt(0);
        }

        int sim = RunFullSimulationEnhanced(new List<string>(testDeck), bulletsInOrder, (string[,])_finalGridMap.Clone());
        return (testDeck, sim);
    }

    private bool SplitOneRandomColorPig(List<PigDataPool> pool)
    {
        var splittableColors = pool.Where(p => p.bullets >= 20).Select(p => p.color).Distinct().ToList();
        if (splittableColors.Count == 0) return false;

        string rColor = splittableColors[UnityEngine.Random.Range(0, splittableColors.Count)];
        var target = pool.Where(p => p.color == rColor && p.bullets >= 20).OrderByDescending(p => p.bullets).First();

        int original = target.bullets;
        int p1 = (original / 2) / 10 * 10;
        if (p1 < 10) p1 = 10;
        int p2 = original - p1;

        target.bullets = p1;
        pool.Add(new PigDataPool { color = rColor, bullets = p2 });
        return true;
    }

    private int RunFullSimulationEnhanced(List<string> playDeck, List<int> pigBullets, string[,] grid)
    {
        int steps = 0;
        List<string> q1Color = new List<string>();
        List<int> q1Bullets = new List<int>();
        int failsafe = 0;

        List<string>[] colsC = new List<string>[queueColumns];
        List<int>[] colsB = new List<int>[queueColumns];
        for (int i = 0; i < queueColumns; i++) { colsC[i] = new List<string>(); colsB[i] = new List<int>(); }
        for (int i = 0; i < playDeck.Count; i++) {
            int idx = i % queueColumns;
            colsC[idx].Add(playDeck[i]); colsB[idx].Add(pigBullets[i]);
        }

        while ((colsC.Any(c => c.Count > 0) || q1Color.Count > 0) && failsafe++ < 1500)
        {
            bool moved = false;
            // 1. Ưu tiên Queue 1
            for (int i = 0; i < q1Color.Count; i++) {
                if (IsExposed(q1Color[i], grid)) {
                    steps++; ClearGrid(q1Color[i], q1Bullets[i], grid);
                    q1Color.RemoveAt(i); q1Bullets.RemoveAt(i);
                    moved = true; break;
                }
            }
            if (moved) continue;

            // 2. Ưu tiên đầu cột Deck
            int bestCol = -1;
            for (int i = 0; i < queueColumns; i++) {
                if (colsC[i].Count > 0 && IsExposed(colsC[i][0], grid)) {
                    bestCol = i; break;
                }
            }

            if (bestCol != -1) {
                steps++; ClearGrid(colsC[bestCol][0], colsB[bestCol][0], grid);
                colsC[bestCol].RemoveAt(0); colsB[bestCol].RemoveAt(0);
                moved = true;
            } else {
                // 3. Soi hàng 2 (Look-ahead)
                int colToForce = -1;
                for (int i = 0; i < queueColumns; i++) {
                    if (colsC[i].Count >= 2 && IsExposed(colsC[i][1], grid)) { colToForce = i; break; }
                }
                if (colToForce == -1) {
                    for (int i = 0; i < queueColumns; i++) if (colsC[i].Count > 0) { colToForce = i; break; }
                }

                if (colToForce != -1) {
                    steps++;
                    if (q1Color.Count < MAX_QUEUE_1) {
                        q1Color.Add(colsC[colToForce][0]); q1Bullets.Add(colsB[colToForce][0]);
                        colsC[colToForce].RemoveAt(0); colsB[colToForce].RemoveAt(0);
                        moved = true;
                    } else return -1;
                }
            }
            if (!moved) break;
        }
        return steps;
    }

    private void ClearGrid(string color, int amount, string[,] grid) {
        int cleared = 0;
        for (int i = 0; i < _h && cleared < amount; i++)
            for (int j = 0; j < _w && cleared < amount; j++)
                if (grid[j, i] == color) { grid[j, i] = null; cleared++; }
    }

    private void SpawnPigsFromSimulation(List<string> deck, List<PigDataPool> pool) {
        foreach (var p in pool) p.isUsed = false;
        foreach (string color in deck) {
            var data = pool.FirstOrDefault(p => p.color == color && !p.isUsed);
            if (data == null) continue;
            data.isUsed = true;
            if (_pigPrefabs.ContainsKey(color)) {
                var pigObj = Instantiate(_pigPrefabs[color], pigScrollContent);
                pigObj.GetComponent<PigComponent>().SetBulletCount(data.bullets, color);
                _generatedPigs.Add(pigObj);
            }
        }
    }

    private void ArrangePigsUIFixed() {
        float sp = 160f; float startX = -((queueColumns - 1) * sp) / 2f;
        for (int i = 0; i < _generatedPigs.Count; i++) {
            int x = i % queueColumns; int y = i / queueColumns;
            RectTransform rt = _generatedPigs[i].GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(startX + (x * sp), -(y * sp) - 100f);
        }
        pigScrollContent.sizeDelta = new Vector2(pigScrollContent.sizeDelta.x, (_generatedPigs.Count / queueColumns + 1) * sp + 200f);
    }

    private bool IsExposed(string color, string[,] grid) {
        for (int i = 0; i < _h; i++) {
            for (int j = 0; j < _w; j++) if (grid[j, i] != null) { if (grid[j, i] == color) return true; break; }
            for (int j = _w - 1; j >= 0; j--) if (grid[j, i] != null) { if (grid[j, i] == color) return true; break; }
        }
        return false;
    }

    private void SpawnVisualBlocks() {
        _blockContainer = new GameObject("Level_Grid");
        _blockContainer.transform.localPosition = new Vector3(0, 5, 0);
        Vector3 offset = new Vector3(-(_w - 1) * _spacing / 2f, -(_h - 1) * _spacing / 2f, 0);
        for (int y = 0; y < _h; y++) {
            for (int x = 0; x < _w; x++) {
                var block = Instantiate(_blockPrefabs[_finalGridMap[x, y].ToLower()], _blockContainer.transform);
                block.transform.localPosition = new Vector3(x * _spacing, y * _spacing, 0) + offset;
                block.transform.localScale = Vector3.one * _scale;
            }
        }
    }

    private void ResetData() {
        if (_blockContainer != null) Destroy(_blockContainer);
        foreach (var p in _generatedPigs) Destroy(p);
        _generatedPigs.Clear();
    }

    public void NextLevelOnClick() { if (imageIndex < imageConfigList.Length - 1) { imageIndex++; GenerateLevel(); } }
    public void PrevLevelOnClick() { if (imageIndex > 0) { imageIndex--; GenerateLevel(); } }
}