using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class ImageToConfigToolEditor : EditorWindow
{
    // subclass to run Simulation
    public class PigDataPool {
        public string color;
        public int bullets;
        public bool isUsed;
    }

    [Header("Input Settings")]
    private Texture2D _textureInput;
    private int _targetWidth = 20;
    // private ImageConfig _targetConfig;
    private int _targetStepsInput = 12;
    private int _queueColumns = 3;
    private int levelIndex = 0;

    [Header("Internal Data")]
    private string[,] _finalGridMap;
    private int _finalWidth, _finalHeight;
    private readonly Dictionary<string, GameObject> _blockPrefabs = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, GameObject> _pigPrefabs = new Dictionary<string, GameObject>();
    private Dictionary<string, int> _finalColorCounts = new Dictionary<string, int>();
    
    private List<PigLayoutData>[] _multiColumnPigs;
    
    private Vector2 _gridScroll;
    private Vector2 _pigScroll;
    private string _reportText = "Sẵn sàng...";
    private string _infoText = "";
    private const int MaxQueue1 = 5;
    private int _pigsBeforeAdjust = 0;
    
    private string _activeColorBrush = "red"; 

    private List<string[,]> _undoHistory = new List<string[,]>();
    private const int MaxUndoSteps = 10;
    
    
    private (List<string> finalDeck, List<PigDataPool> finalPool, int actualSteps) _lastPigResult;
    
    private int _draggingID = -1;
    
    private readonly string[] ALL_COLORS = { 
        "red", "green", "blue", "yellow", "black","brown","light brown","cream","gray", "purple","light blue","purple",
        "white", "pink", "dark pink", "orange", "dark green", "dark blue", "empty","light green" 
    };
    
    [MenuItem("Tools/Level Generator Tool")]
    public static void ShowWindow() => GetWindow<ImageToConfigToolEditor>("Level Generator");

    private void OnGUI()
    {
        
        Event e = Event.current;
        if (e.type == EventType.KeyDown && e.control && e.keyCode == KeyCode.Z)
        {
            PerformUndo();
            e.Use();
        }
        EditorGUILayout.BeginHorizontal();
        DrawGridPreviewColumn();
        DrawPigListColumn();
        DrawSettingsColumn();
        EditorGUILayout.EndHorizontal();
    }
    
    private void RecordUndo()
    {
        if (_finalGridMap == null) return;
        
        string[,] snapshot = (string[,])_finalGridMap.Clone();
    
        _undoHistory.Add(snapshot);

        if (_undoHistory.Count > MaxUndoSteps)
        {
            _undoHistory.RemoveAt(0);
        }
    }
    
    private void PerformUndo()
    {
        if (_undoHistory.Count == 0) return;
        
        int lastIndex = _undoHistory.Count - 1;
        _finalGridMap = (string[,])_undoHistory[lastIndex].Clone();

        _undoHistory.RemoveAt(lastIndex);
        
        ActionShuffleAndSimulate();
        Repaint();
        Debug.Log("Undo performed!");
    }

    private void DrawGridPreviewColumn()
{

    EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(position.width * 0.40f));
    GUILayout.Label("1. Grid Painter", EditorStyles.boldLabel);
    
    _gridScroll = EditorGUILayout.BeginScrollView(_gridScroll, GUILayout.Height(position.height * 0.7f));
    if (_finalGridMap != null)
    {
        float cellSize = 18f;
        float padding = 1f;
        Event e = Event.current;

        for (int y = _finalHeight - 1; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal();
            for (int x = 0; x < _finalWidth; x++)
            {
                string colorName = _finalGridMap[x, y];
                Rect r = GUILayoutUtility.GetRect(cellSize, cellSize);
                Rect innerRect = new Rect(r.x + padding, r.y + padding, r.width - padding * 2, r.height - padding * 2);

                Color displayColor = GetColorFromName(colorName);
                // Debug.Log(colorName);
                // Debug.Log(displayColor);
                EditorGUI.DrawRect(innerRect, displayColor);

                // Logic Paint
                if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && r.Contains(e.mousePosition))
                {
                    if (_finalGridMap[x, y] != _activeColorBrush)
                    {
                        RecordUndo();
                        UpdateCellColor(x, y, _activeColorBrush);
                    }
                    e.Use();
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }
    EditorGUILayout.EndScrollView();

    GUILayout.Space(15);
    GUILayout.Label("Palette", EditorStyles.boldLabel);

    Vector2 paletteScroll = Vector2.zero; 
    int colorsPerRow = 5;
    
    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
    for (int i = 0; i < ALL_COLORS.Length; i++)
    {
        if (i % colorsPerRow == 0) EditorGUILayout.BeginHorizontal();

        string colorName = ALL_COLORS[i];

        Color originalColor = GUI.backgroundColor;
        if (_activeColorBrush == colorName) GUI.backgroundColor = Color.grey;

        if (GUILayout.Button(colorName.ToUpper(), GUILayout.Width(position.width * 0.07f), GUILayout.Height(25)))
        {
            _activeColorBrush = colorName;
        }
        
        GUI.backgroundColor = originalColor;

        if ((i + 1) % colorsPerRow == 0 || (i + 1) == ALL_COLORS.Length) 
            EditorGUILayout.EndHorizontal();
    }
    EditorGUILayout.EndVertical();

    EditorGUILayout.EndVertical();
}

    private void DrawPigListColumn()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(position.width * 0.3f));
        GUILayout.Label("2. Lane-based Pig Layout", EditorStyles.boldLabel);

        _pigScroll = EditorGUILayout.BeginScrollView(_pigScroll);
        EditorGUILayout.BeginHorizontal();

        int moveFromCol = -1, moveFromIdx = -1;
        int moveToCol = -1, moveToIdx = -1;
        Event e = Event.current;

        if (_multiColumnPigs != null)
        {
            float colWidth = 55f;
            for (int c = 0; c < _queueColumns; c++)
            {
                EditorGUILayout.BeginVertical(GUILayout.Width(colWidth));
                GUILayout.Label($"Col {c}", EditorStyles.centeredGreyMiniLabel);

                int rowCount = _multiColumnPigs[c].Count + 1;
                for (int r = 0; r < rowCount; r++)
                {
                    PigLayoutData pig = (r < _multiColumnPigs[c].Count) ? _multiColumnPigs[c][r] : null;
                    Rect rect = EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(colWidth - 5), GUILayout.Height(60));

                    Rect colorRect = new Rect(rect.x + 4, rect.y + 4, rect.width - 8, 20);
                    EditorGUI.DrawRect(colorRect, GetColorFromName(pig != null ? pig.colorName : "empty"));
                    GUILayout.Space(25);

                    GUIStyle bulletStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 13 };
                    GUILayout.Label(pig != null ? pig.bullets.ToString() : "+", bulletStyle);

                    int currentID = (c * 1000) + r;
                    if (_draggingID == currentID)
                        Handles.DrawSolidRectangleWithOutline(rect, new Color(1, 1, 0, 0.2f), Color.yellow);

                    if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
                    {
                        if (_draggingID == -1) {
                            if (pig != null) _draggingID = currentID;
                        } else {
                            moveFromCol = _draggingID / 1000;
                            moveFromIdx = _draggingID % 1000;
                            moveToCol = c;
                            moveToIdx = r;
                            _draggingID = -1;
                        }
                        e.Use();
                    }
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndVertical();
                GUILayout.Space(5);
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        if (moveFromCol != -1) {
            MovePigBetweenLanes(moveFromCol, moveFromIdx, moveToCol, moveToIdx);
            Repaint();
        }
    }

    private void MovePigBetweenLanes(int fCol, int fIdx, int tCol, int tIdx)
    {
        if (_multiColumnPigs == null || fCol >= _multiColumnPigs.Length) return;
        
        var pig = _multiColumnPigs[fCol][fIdx];
        _multiColumnPigs[fCol].RemoveAt(fIdx);

        int actualTarget = Mathf.Clamp(tIdx, 0, _multiColumnPigs[tCol].Count);
        _multiColumnPigs[tCol].Insert(actualTarget, pig);

        UpdateSimulateFromLanes();
    }

    private void UpdateSimulateFromLanes()
    {
        if (_multiColumnPigs == null) return;

        List<string> flatColors = new List<string>();
        List<int> flatBullets = new List<int>();
        int maxRows = _multiColumnPigs.Max(c => c.Count);

        for (int r = 0; r < maxRows; r++) {
            for (int c = 0; c < _queueColumns; c++) {
                if (r < _multiColumnPigs[c].Count) {
                    flatColors.Add(_multiColumnPigs[c][r].colorName);
                    flatBullets.Add(_multiColumnPigs[c][r].bullets);
                }
            }
        }

        int newSteps = RunFullSimulationEnhanced(flatColors, flatBullets, (string[,])_finalGridMap.Clone());
        _lastPigResult.finalDeck = flatColors; 
        _lastPigResult.actualSteps = newSteps;

        _reportText = (newSteps == -1) ? "<color=red>UNWINNABLE:</color> Deadlock!" : $"<color=green>SOLVABLE:</color> {newSteps} steps";
        
    }

    private void DrawSettingsColumn()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(position.width * 0.3f));
        GUILayout.Label("3. Control Panel", EditorStyles.boldLabel);
        levelIndex = EditorGUILayout.IntField("Level Index",levelIndex);
        _textureInput = (Texture2D)EditorGUILayout.ObjectField("Input Image", _textureInput, typeof(Texture2D), false);
        _targetWidth = EditorGUILayout.IntSlider("Width", _targetWidth, 10, 60);
        _targetStepsInput = EditorGUILayout.IntSlider("Target Steps", _targetStepsInput, 5, 50);
        _queueColumns = EditorGUILayout.IntSlider("Cols", _queueColumns, 2, 5);

        EditorGUILayout.Space(10);
        GUI.backgroundColor = new Color(0.3f, 0.8f, 1f);
        if (GUILayout.Button("1. SCAN IMAGE", GUILayout.Height(30))) ActionScanImage();

        GUI.backgroundColor = Color.yellow;
        EditorGUI.BeginDisabledGroup(_finalGridMap == null);
        if (GUILayout.Button("2. GENERATE TURRET LAYOUT", GUILayout.Height(30))) ActionShuffleAndSimulate();
        EditorGUI.EndDisabledGroup();

        GUI.backgroundColor = Color.green;
        EditorGUI.BeginDisabledGroup(_lastPigResult.finalDeck == null);
        if (GUILayout.Button("3. SAVE TO CONFIG", GUILayout.Height(30))) ActionSaveConfig();
        EditorGUI.EndDisabledGroup();

        GUI.backgroundColor = Color.white;
        if (GUILayout.Button("Clear All Data")) ResetData();

        EditorGUILayout.Space(10);
        
        GUIStyle reportStyle = new GUIStyle(EditorStyles.label);
        
        reportStyle.normal.textColor = Color.yellow;
        reportStyle.fontSize = 15;
        reportStyle.fontStyle = FontStyle.Bold;
        GUILayout.Label("Report:", reportStyle);
        
        GUILayout.Label($"{_infoText}Pigs before adjust: {_pigsBeforeAdjust}", EditorStyles.wordWrappedLabel);
        
        GUIStyle resStyle = new GUIStyle(EditorStyles.label);
        resStyle.normal.textColor = Color.yellow;
        resStyle.fontSize = 15;
        resStyle.fontStyle = FontStyle.Bold;
        GUILayout.Label("Result:", reportStyle);
        
        EditorGUILayout.SelectableLabel(_reportText, EditorStyles.wordWrappedLabel, GUILayout.Height(80));
        EditorGUILayout.EndVertical();
    }

    private void ActionScanImage()
    {
        if (_textureInput == null) return;
        LoadAssetsImmediate();
        ResetData();
        _finalWidth = _targetWidth;
        float aspect = (float)_textureInput.width / _textureInput.height;
        _finalHeight = Mathf.RoundToInt(_finalWidth / aspect);
        while ((_finalWidth * _finalHeight) % 10 != 0) _finalHeight++;
        _finalGridMap = new string[_finalWidth, _finalHeight];
        float stepX = (float)_textureInput.width / _finalWidth;
        float stepY = (float)_textureInput.height / _finalHeight;
        for (int y = 0; y < _finalHeight; y++) {
            for (int x = 0; x < _finalWidth; x++) {
                Color col = _textureInput.GetPixel(Mathf.FloorToInt(x * stepX + stepX / 2f), Mathf.FloorToInt(y * stepY + stepY / 2f));
                if (col.a < 0.1f) _finalGridMap[x, y] = "empty";
                else {
                    string cName = Helper.GetClosestColor(col).ToLower();
                    // Debug.Log(cName);
                    _finalGridMap[x, y] = cName;
                }
            }
        }
        _reportText = "Scan Success!!.";
        Repaint();
    }

    private void ActionShuffleAndSimulate()
    {
        if (_finalGridMap == null) return;
        _finalColorCounts.Clear();
        _infoText = "";
        
        for (int y = 0; y < _finalHeight; y++) {
            for (int x = 0; x < _finalWidth; x++) {
                string c = _finalGridMap[x, y];
                if (c != "empty" && !string.IsNullOrEmpty(c)) {
                    if (!_finalColorCounts.ContainsKey(c)) _finalColorCounts[c] = 0;
                    _finalColorCounts[c]++;
                }
            }
        }

        foreach (var color in _finalColorCounts)
        {
            _infoText += $"{color.Key}: {color.Value}\n";
        }
        
        _lastPigResult = RunAdaptiveSimulation();

        _multiColumnPigs = new List<PigLayoutData>[_queueColumns];
        for (int i = 0; i < _queueColumns; i++) _multiColumnPigs[i] = new List<PigLayoutData>();

        if (_lastPigResult.finalDeck != null) {
            foreach (var p in _lastPigResult.finalPool) p.isUsed = false;
            for (int i = 0; i < _lastPigResult.finalDeck.Count; i++) {
                string color = _lastPigResult.finalDeck[i];
                var data = _lastPigResult.finalPool.FirstOrDefault(x => x.color == color && !x.isUsed);
                if (data != null) {
                    data.isUsed = true;
                    _multiColumnPigs[i % _queueColumns].Add(new PigLayoutData { colorName = data.color, bullets = data.bullets });
                }
            }
            _reportText = $"Simulate success!\nSteps: {_lastPigResult.actualSteps}";
        }
        Repaint();
    }

    private void UpdateCellColor(int x, int y, string newColor)
    {
        _finalGridMap[x, y] = newColor;
        ActionShuffleAndSimulate();
    }

    private void ShowColorPickerMenu(int x, int y)
    {
        GenericMenu menu = new GenericMenu();
        foreach (string color in ALL_COLORS) {
            string selectedColor = color;
            menu.AddItem(new GUIContent(selectedColor.ToUpper()), _finalGridMap[x, y] == selectedColor, () => UpdateCellColor(x, y, selectedColor));
        }
        menu.ShowAsContext();
    }
    // --- SIMULATION ENGINE ---
    private (List<string> finalDeck, List<PigDataPool> finalPool, int actualSteps) RunAdaptiveSimulation()
    {
        List<PigDataPool> pool = new List<PigDataPool>();
        foreach (var item in _finalColorCounts) {
            int total = item.Value;
            if (total <= 0) continue; 

            int perPig = total < 100 ? 10 : 40;

            int count = (total + perPig - 1) / perPig; 
    
            int remaining = total;
            for (int i = 0; i < count; i++) {
                int bullets = Mathf.Min(perPig, remaining);
        
                if (bullets > 0) {
                    pool.Add(new PigDataPool { color = item.Key, bullets = bullets });
                    remaining -= bullets;
                }
            }
        }
        _pigsBeforeAdjust = pool.Count;
        int target = Mathf.Max(_targetStepsInput, pool.Select(p => p.color).Distinct().Count());
        List<string> finalDeck = null;
        int finalSteps = -1;
        int limit = 50;

        while (limit-- > 0) {
            if (pool.Count > target) { MergeTwoPigs(pool); continue; }
            bool found = false;
            int best = -1, worst = 1000;
            for (int i = 0; i < 300; i++) {
                var sim = ExecuteSimulation(pool.Select(p => p.color).ToList(), pool);
                if (sim.steps == target) { finalSteps = sim.steps; finalDeck = sim.deck; found = true; break; }
                if (sim.steps != -1) { best = Mathf.Max(best, sim.steps); worst = Mathf.Min(worst, sim.steps); }
            }
            if (found) break;
            if (best < target) SplitOnePig(pool); else if (worst > target) MergeTwoPigs(pool);
        }
        return (finalDeck, pool, finalSteps);
    }

    private (List<string> deck, int steps) ExecuteSimulation(List<string> poolNames, List<PigDataPool> pool)
    {
        var deck = poolNames.OrderBy(x => Random.value).ToList();
        var tempB = pool.GroupBy(p => p.color).ToDictionary(g => g.Key, g => g.Select(p => p.bullets).OrderByDescending(b => b).ToList());
        List<int> bullets = deck.Select(c => { int b = tempB[c][0]; tempB[c].RemoveAt(0); return b; }).ToList();
        return (deck, RunFullSimulationEnhanced(deck, bullets, (string[,])_finalGridMap.Clone()));
    }

    private int RunFullSimulationEnhanced(List<string> playDeck, List<int> pigBullets, string[,] grid)
{
    int steps = 0;
    List<string> q1C = new List<string>();
    List<int> q1B = new List<int>();
    
    // Khởi tạo các cột
    List<string>[] colsC = new List<string>[_queueColumns];
    List<int>[] colsB = new List<int>[_queueColumns];
    for (int i = 0; i < _queueColumns; i++) {
        colsC[i] = new List<string>();
        colsB[i] = new List<int>();
    }
    
    // Chia heo vào các lane
    for (int i = 0; i < playDeck.Count; i++) {
        colsC[i % _queueColumns].Add(playDeck[i]);
        colsB[i % _queueColumns].Add(pigBullets[i]);
    }

    int failsafe = 0;
    while ((colsC.Any(c => c.Count > 0) || q1C.Count > 0) && failsafe++ < 1000) {
        bool moved = false;

        // ƯU TIÊN 1: Kiểm tra các con heo ĐANG Ở TRONG QUEUE
        // Nếu người chơi thấy heo trong queue khớp, họ sẽ click nó.
        for (int i = 0; i < q1C.Count; i++) {
            if (IsExposed(q1C[i], grid)) { 
                steps++; // Người chơi click vào heo trong queue
                ClearGridSim(q1C[i], q1B[i], grid);
                q1C.RemoveAt(i);
                q1B.RemoveAt(i);
                moved = true;
                break; 
            }
        }
        if (moved) continue;

        // ƯU TIÊN 2: Kiểm tra heo ở ĐẦU CỘT
        int bestCol = -1;
        for (int i = 0; i < _queueColumns; i++) {
            if (colsC[i].Count > 0 && IsExposed(colsC[i][0], grid)) {
                bestCol = i;
                break;
            }
        }

        if (bestCol != -1) {
            steps++; // Người chơi click vào heo ở đầu cột
            ClearGridSim(colsC[bestCol][0], colsB[bestCol][0], grid);
            colsC[bestCol].RemoveAt(0);
            colsB[bestCol].RemoveAt(0);
            moved = true;
        } else {
            // ƯU TIÊN 3: Không có con nào khớp trực tiếp -> Phải đẩy 1 con vào Queue
            int colToForce = -1;
            for (int i = 0; i < _queueColumns; i++) {
                if (colsC[i].Count > 0) {
                    colToForce = i;
                    break;
                }
            }

            if (colToForce != -1) {
                if (q1C.Count < MaxQueue1) {
                    steps++; // Người chơi click đẩy heo vào queue
                    q1C.Add(colsC[colToForce][0]);
                    q1B.Add(colsB[colToForce][0]);
                    colsC[colToForce].RemoveAt(0);
                    colsB[colToForce].RemoveAt(0);
                    moved = true;
                } else {
                    return -1; // Deadlock (Queue đầy mà không con nào khớp)
                }
            }
        }
        if (!moved) break;
    }
    return steps;
}
    
    private Color GetColorFromName(string name)
    {
        switch (name.ToLower())
        {
            case "red": return Color.red;
            case "green": return Color.green;
            case "blue": return Color.blue;
            case "yellow": return Color.yellow;
            case "black": return Color.black;
            case "white": return Color.white;
            case "pink": return new Color(1, 0.6f, 0.7f);
            case "dark pink": return new Color(1, 0.2f, 0.7f);
            case "brown" : return Color.brown;
            case "light brown" : return Color.sandyBrown;
            case "purple": return Color.purple;
            case "light blue" : return Color.lightBlue;
            case "orange": return new Color(1, 0.5f, 0);
            case "dark green": return new Color(0, 0.5f, 0);
            case "light green": return Color.lightGreen;
            case "cream" : return Color.lemonChiffon;
            case "dark blue": return new Color(0, 0, 0.5f);
            case "empty": return new Color(0.2f, 0.2f, 0.2f, 0.5f);
            default: return Color.gray;
        }
    }

    private bool IsExposed(string color, string[,] grid)
    {
        for (int i = 0; i < _finalHeight; i++)
        {

            for (int j = 0; j < _finalWidth; j++)
            {
                if (string.IsNullOrEmpty(grid[j, i]) || grid[j, i] == "empty") continue;
                if (grid[j, i] == color) return true;
                break; 
            }

            for (int j = _finalWidth - 1; j >= 0; j--)
            {
                if (string.IsNullOrEmpty(grid[j, i]) || grid[j, i] == "empty") continue;
                if (grid[j, i] == color) return true;
                break; 
            }
        }
        return false;
    }
    
    private void ClearGridSim(string color, int amount, string[,] grid) {
        int cleared = 0;
        for (int i = 0; i < _finalHeight && cleared < amount; i++) 
            for (int j = 0; j < _finalWidth && cleared < amount; j++) 
                if (grid[j, i] == color) { grid[j, i] = "empty"; cleared++; }
    }

    private void MergeTwoPigs(List<PigDataPool> pool) {
        var group = pool.GroupBy(p => p.color).Where(g => g.Count() > 1).OrderByDescending(g => g.Count()).FirstOrDefault();
        if (group != null) { var items = group.OrderBy(p => p.bullets).ToList(); items[1].bullets += items[0].bullets; pool.Remove(items[0]); }
    }

    private void SplitOnePig(List<PigDataPool> pool) {
        var target = pool.Where(p => p.bullets >= 20).OrderByDescending(p => p.bullets).FirstOrDefault();
        if (target != null) { int b1 = (target.bullets / 2) / 10 * 10; pool.Add(new PigDataPool { color = target.color, bullets = target.bullets - b1 }); target.bullets = b1; }
    }

    private void LoadAssetsImmediate() {
        _blockPrefabs.Clear(); _pigPrefabs.Clear();
        string[] colors = { "Red", "Green", "Yellow", "Orange", "Black", "White", "Pink", "Blue", "Dark Green", "Dark Pink", "Dark Blue" };
        foreach (var c in colors) {
            string colorKey = c.ToLower();
            string[] bGuids = AssetDatabase.FindAssets(c + " t:Prefab");
            foreach (var g in bGuids) { var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)); if (go && go.name.Equals(c, System.StringComparison.OrdinalIgnoreCase)) { _blockPrefabs[colorKey] = go; break; } }
            string[] pGuids = AssetDatabase.FindAssets("Pig " + c + " t:Prefab");
            foreach (var g in pGuids) { var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)); if (go) { _pigPrefabs[colorKey] = go; break; } }
        }
    }

    private void SaveToJson()
    {

        string path = EditorUtility.SaveFilePanel("Save Level JSON", "", $"Level_{levelIndex}", "json");
        if (string.IsNullOrEmpty(path)) return;

        LevelData data = new LevelData();
        data.levelIndex = levelIndex;
        data.width = _finalWidth;
        data.height = _finalHeight;
        data.targetDifficulty = _targetStepsInput;
        
        data.gridData = new List<string>();
        for (int y = _finalHeight - 1; y >= 0; y--)
        {
            for (int x = 0; x < _finalWidth; x++)
            {
                data.gridData.Add(_finalGridMap[x, y]);
            }
        }
        
        data.lanes = new List<LaneData>();
        if (_multiColumnPigs != null)
        {
            for (int i = 0; i < _multiColumnPigs.Length; i++)
            {
                LaneData newLane = new LaneData();
                newLane.pigs = new List<PigLayoutData>(_multiColumnPigs[i]);
                data.lanes.Add(newLane);
            }
        }
        
        string jsonContent = JsonUtility.ToJson(data, true);
        
        try
        {
            System.IO.File.WriteAllText(path, jsonContent);
            _reportText = $"<color=green>EXPORT SUCCESS!</color>\nFile: {System.IO.Path.GetFileName(path)}";
            EditorUtility.DisplayDialog("Export JSON", "Level data exported successfully!", "OK");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Save failed: {ex.Message}");
        }
    }

    private void ActionSaveConfig() {
        if (_lastPigResult.finalDeck == null) return;
        SaveToJson();
        // EditorUtility.DisplayDialog("Save Success", "> O <", "OK");
    }

    private void ResetData() {
        var old = GameObject.Find("GeneratedLevel_Preview"); if (old) DestroyImmediate(old);
        _finalColorCounts.Clear(); _multiColumnPigs = null; _infoText = ""; _finalGridMap = null;
    }
}