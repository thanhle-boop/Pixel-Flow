using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public class ImageToConfigToolEditor : EditorWindow
{
    private Texture2D _textureInput;
    private int _targetWidth = 20;
    private ImageConfig _targetConfig;

    private GameObject _blockGroup;
    private string[,] _finalGridMap;
    private int _finalWidth, _finalHeight;
    private float _spacing, _scale;
    private readonly Dictionary<string, GameObject> _blockPrefabs = new Dictionary<string, GameObject>();
    private Dictionary<string, int> _finalColorCounts = new Dictionary<string, int>();

    private string _reportText = "Sẵn sàng...";
    private Vector2 _scrollPosition;

    [MenuItem("Tools/Level Generator Tool")]
    public static void ShowWindow() => GetWindow<ImageToConfigToolEditor>("Level Generator");

    private void OnGUI()
    {
        GUILayout.Label("1. Input Settings", EditorStyles.boldLabel);
        _targetConfig = (ImageConfig)EditorGUILayout.ObjectField("Target Config File", _targetConfig, typeof(ImageConfig), false);
        _textureInput = (Texture2D)EditorGUILayout.ObjectField("Input Image", _textureInput, typeof(Texture2D), false);
        _targetWidth = EditorGUILayout.IntSlider("Target Width", _targetWidth, 10, 60);

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Generate & Save", GUILayout.Height(40)))
        {
            GenerateLevelFast(); 
        }

        if (GUILayout.Button("Clear Scene")) { ResetData(); }

        EditorGUILayout.Space(10);
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, EditorStyles.helpBox);
        EditorGUILayout.LabelField(_reportText, EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
    }

    private void GenerateLevelFast()
    {
        if (_textureInput == null || _targetConfig == null) return;

        LoadAssetsImmediate();
        ResetData();

        // 1. TÍNH TOÁN KÍCH THƯỚC (ÉP TỔNG CHIA HẾT CHO 10)
        _finalWidth = _targetWidth;
        float aspect = (float)_textureInput.width / _textureInput.height;
        _finalHeight = Mathf.RoundToInt(_finalWidth / aspect);
        
        // Đảm bảo (Width * Height) là bội số của 10 để có thể Balance từng màu
        while ((_finalWidth * _finalHeight) % 10 != 0) _finalHeight++;

        _spacing = (_finalWidth * _finalHeight >= 600) ? 0.35f : 0.45f;
        _scale = _spacing * 0.9f;

        _finalGridMap = new string[_finalWidth, _finalHeight];
        Dictionary<string, List<Vector2Int>> colorMap = new Dictionary<string, List<Vector2Int>>();

        float stepX = (float)_textureInput.width / _finalWidth;
        float stepY = (float)_textureInput.height / _finalHeight;

        // 2. QUÉT PIXEL
        for (int y = 0; y < _finalHeight; y++)
        {
            for (int x = 0; x < _finalWidth; x++)
            {
                Color col = _textureInput.GetPixel(Mathf.FloorToInt(x * stepX + stepX / 2f), Mathf.FloorToInt(y * stepY + stepY / 2f));
                string cName = (col.a < 0.1f) ? "white" : Helper.GetClosestColor(col);
                if (!_blockPrefabs.ContainsKey(cName)) cName = "white";

                _finalGridMap[x, y] = cName;
                if (!colorMap.ContainsKey(cName)) colorMap[cName] = new List<Vector2Int>();
                colorMap[cName].Add(new Vector2Int(x, y));
            }
        }

        // 3. CÂN BẰNG MÀU (ÉP CHẾT SỐ LẺ)
        BalanceColors(colorMap);
        
        // 4. LƯU & SPAWN
        SaveToConfig();
        SpawnBlocks();
        UpdateUIReports();
    }

    private void BalanceColors(Dictionary<string, List<Vector2Int>> colorMap)
    {
        // Kiểm tra tổng diện tích trước khi bắt đầu loop
        int total = colorMap.Values.Sum(list => list.Count);
        if (total % 10 != 0) {
            Debug.LogError($"LỖI: Tổng số block ({total}) không chia hết cho 10. Không thể cân bằng!");
            return;
        }

        int maxFailsafe = 1000; // Tránh treo Unity
        
        while (maxFailsafe-- > 0)
        {
            // Tìm màu đầu tiên bị lẻ
            var targetColor = colorMap.Keys.FirstOrDefault(c => colorMap[c].Count % 10 != 0);
            if (targetColor == null) break; // Tất cả đã tròn!

            int currentCount = colorMap[targetColor].Count;
            int remainder = currentCount % 10;
            int need = 10 - remainder;

            // Tìm donor (nhà tài trợ): Ưu tiên màu cũng đang lẻ để triệt tiêu nhau
            string donorColor = colorMap.Keys
                .Where(c => c != targetColor && colorMap[c].Count > 10)
                .OrderByDescending(c => colorMap[c].Count % 10 != 0) 
                .ThenByDescending(c => colorMap[c].Count)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(donorColor)) break;

            // Chuyển block
            int toTake = Mathf.Min(need, colorMap[donorColor].Count - 1); 
            for (int i = 0; i < toTake; i++)
            {
                if (colorMap[donorColor].Count <= 0) break;
                Vector2Int pos = colorMap[donorColor][0];
                colorMap[donorColor].RemoveAt(0);
                colorMap[targetColor].Add(pos);
                _finalGridMap[pos.x, pos.y] = targetColor;
                need--;
                if (need == 0) break;
            }
        }

        _finalColorCounts.Clear();
        foreach (var kvp in colorMap) if (kvp.Value.Count > 0) _finalColorCounts[kvp.Key] = kvp.Value.Count;
    }

    private void LoadAssetsImmediate()
    {
        _blockPrefabs.Clear();
        string[] colors = { "Red", "Green", "Yellow", "Orange", "Black", "White", "Pink", "Blue", "Dark Green", "Dark Pink", "Dark Blue" };
        foreach (var c in colors)
        {
            string[] guids = AssetDatabase.FindAssets(c + " t:Prefab");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null && go.name.Equals(c, System.StringComparison.OrdinalIgnoreCase))
                {
                    _blockPrefabs[c.ToLower()] = go;
                    break;
                }
            }
        }
    }

    private void SaveToConfig()
    {
        if (_targetConfig == null) return;
        Undo.RecordObject(_targetConfig, "Update Config");
        _targetConfig.textureInput = _textureInput;
        _targetConfig.width = _finalWidth;
        _targetConfig.height = _finalHeight;
        _targetConfig.spacing = _spacing;
        _targetConfig.colorCounts.Clear();
        foreach (var kvp in _finalColorCounts.OrderByDescending(x => x.Value))
            _targetConfig.colorCounts.Add(new ColorCountData { colorName = kvp.Key, count = kvp.Value });

        EditorUtility.SetDirty(_targetConfig);
        AssetDatabase.SaveAssets();
    }

    private void SpawnBlocks()
    {
        _blockGroup = new GameObject("GeneratedMap_Editor");
        _blockGroup.transform.position = new Vector3(0, 5, 0);
        float offsetW = (_finalWidth - 1) * _spacing / 2f;
        float offsetH = (_finalHeight - 1) * _spacing / 2f;

        for (int y = 0; y < _finalHeight; y++) {
            for (int x = 0; x < _finalWidth; x++) {
                string c = _finalGridMap[x, y];
                if (_blockPrefabs.ContainsKey(c)) {
                    GameObject b = (GameObject)PrefabUtility.InstantiatePrefab(_blockPrefabs[c], _blockGroup.transform);
                    b.transform.localPosition = new Vector3(x * _spacing - offsetW, y * _spacing - offsetH, 0);
                    b.transform.localScale = Vector3.one * _scale;
                }
            }
        }
    }

    private void ResetData()
    {
        var old = GameObject.Find("GeneratedMap_Editor");
        if (old != null) DestroyImmediate(old);
        _finalColorCounts.Clear();
    }

    private void UpdateUIReports()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"SUCCESS: {_finalWidth}x{_finalHeight} (Total: {_finalWidth*_finalHeight})");
        foreach (var item in _finalColorCounts.OrderByDescending(x => x.Value))
            sb.AppendLine($"- {item.Key}: {item.Value}");
        _reportText = sb.ToString();
    }
}