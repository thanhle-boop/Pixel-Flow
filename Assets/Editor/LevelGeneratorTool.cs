using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class LevelGeneratorTool : EditorWindow
{
    private Texture2D sourceImage;
    private Vector2Int gridSize = new Vector2Int(16, 16);
    private int maxColors = 5;
    [Range(0f, 1f)]
    private float ditheringAmount = 0.0f; // 0 = Dễ (Mảng to), 1 = Khó (Lốm đốm)

    // Kết quả phân tích
    private int totalIslands = 0;
    private float complexityScore = 0f;
    private Texture2D previewTexture;

    [MenuItem("Tools/Pixel Flow Level Generator")]
    public static void ShowWindow()
    {
        GetWindow<LevelGeneratorTool>("Level Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Cấu Hình Map Đầu Vào", EditorStyles.boldLabel);
        
        sourceImage = (Texture2D)EditorGUILayout.ObjectField("Ảnh Gốc", sourceImage, typeof(Texture2D), false);
        gridSize = EditorGUILayout.Vector2IntField("Kích Thước Grid (X x Y)", gridSize);
        maxColors = EditorGUILayout.IntSlider("Số Màu Tối Đa (Max Colors)", maxColors, 2, 10);
        ditheringAmount = EditorGUILayout.Slider("Độ Phân Tán Màu (Dithering)", ditheringAmount, 0f, 1f);

        if (GUILayout.Button("Tạo Map & Đánh Giá Độ Khó") && sourceImage != null)
        {
            GenerateLevel();
        }

        if (previewTexture != null)
        {
            GUILayout.Space(10);
            GUILayout.Label("Kết Quả Đánh Giá:", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox($"Tổng số block: {gridSize.x * gridSize.y}\nTổng số mảng màu (Islands): {totalIslands}\nĐiểm Độ Khó (Complexity Score): {complexityScore:F2} / 100", MessageType.Info);
            
            // Hiển thị độ khó ước tính cho Level
            string estimatedDifficulty = complexityScore < 20 ? "Dễ (Early Levels)" : (complexityScore < 50 ? "Trung Bình (Mid Levels)" : "Khó (High Levels)");
            EditorGUILayout.HelpBox($"Khuyến nghị: {estimatedDifficulty}", MessageType.Warning);

            GUILayout.Label("Preview Map:");
            GUILayout.Label(previewTexture, GUILayout.Width(256), GUILayout.Height(256));
        }
    }

    private void GenerateLevel()
    {
        // 1. Resize ảnh gốc về kích thước Grid (Ví dụ 16x16)
        Color[] pixels = GetResizedPixels(sourceImage, gridSize.x, gridSize.y);

        // 2. Giới hạn màu (Mô phỏng K-Means & Dithering ở đây)
        // Trong thực tế, bạn sẽ chạy K-Means clustering trên mảng 'pixels'.
        // Ở đây để demo nhanh thuật toán tính độ khó, tôi sẽ giả định ảnh đã được ép màu.
        Color[,] gridColors = new Color[gridSize.x, gridSize.y];
        for (int y = 0; y < gridSize.y; y++)
        {
            for (int x = 0; x < gridSize.x; x++)
            {
                gridColors[x, y] = pixels[y * gridSize.x + x];
            }
        }

        // 3. Chạy thuật toán Flood Fill để tính số lượng "Color Islands"
        totalIslands = CalculateColorIslands(gridColors);

        // 4. Tính điểm Complexity (Càng nhiều đảo màu so với tổng số ô -> Càng khó)
        int totalBlocks = gridSize.x * gridSize.y;
        complexityScore = ((float)totalIslands / totalBlocks) * 100f;

        // 5. Tạo ảnh Preview
        CreatePreviewTexture(gridColors);
    }

    // THUẬT TOÁN LÕI: Đếm số lượng mảng màu (Connected-Component Labeling / Flood Fill)
    private int CalculateColorIslands(Color[,] grid)
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);
        bool[,] visited = new bool[width, height];
        int islandCount = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!visited[x, y])
                {
                    // Tìm thấy một pixel chưa thăm -> Bắt đầu một hòn đảo mới
                    FloodFill(grid, visited, x, y, grid[x, y]);
                    islandCount++;
                }
            }
        }

        return islandCount;
    }

    // Thuật toán loang (Breadth-First Search)
    private void FloodFill(Color[,] grid, bool[,] visited, int startX, int startY, Color targetColor)
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);
        
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(new Vector2Int(startX, startY));
        visited[startX, startY] = true;

        // Các hướng di chuyển: Lên, Xuống, Trái, Phải
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            foreach (var dir in directions)
            {
                int nx = current.x + dir.x;
                int ny = current.y + dir.y;

                // Kiểm tra biên và xem đã thăm chưa
                if (nx >= 0 && nx < width && ny >= 0 && ny < height && !visited[nx, ny])
                {
                    // Nếu có màu tương đồng (bạn có thể dùng dung sai Color Tolerance ở đây)
                    if (IsColorSimilar(grid[nx, ny], targetColor)) 
                    {
                        visited[nx, ny] = true;
                        queue.Enqueue(new Vector2Int(nx, ny));
                    }
                }
            }
        }
    }

    // So sánh màu (Có thể thêm độ lệch màu - Tolerance nếu ảnh không phải pixel art chuẩn)
    private bool IsColorSimilar(Color a, Color b)
    {
        float tolerance = 0.05f; 
        return Mathf.Abs(a.r - b.r) < tolerance && 
               Mathf.Abs(a.g - b.g) < tolerance && 
               Mathf.Abs(a.b - b.b) < tolerance;
    }

    // Hàm phụ trợ Resize ảnh
    private Color[] GetResizedPixels(Texture2D src, int width, int height)
    {
        RenderTexture rt = RenderTexture.GetTemporary(width, height);
        RenderTexture.active = rt;
        Graphics.Blit(src, rt);
        Texture2D result = new Texture2D(width, height);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply();
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        return result.GetPixels();
    }

    private void CreatePreviewTexture(Color[,] grid)
    {
        previewTexture = new Texture2D(gridSize.x, gridSize.y);
        previewTexture.filterMode = FilterMode.Point; // Để nhìn rõ pixel

        for (int y = 0; y < gridSize.y; y++)
        {
            for (int x = 0; x < gridSize.x; x++)
            {
                previewTexture.SetPixel(x, y, grid[x, y]);
            }
        }
        previewTexture.Apply();
    }
}