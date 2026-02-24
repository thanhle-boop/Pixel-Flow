using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class Helper 
{
    public static string GetClosestColor(Color c)
    {
        var targetRed = new Color(0.82f, 0.14f, 0.13f);
        var targetGreen = new Color(0.36f, 0.96f, 0.23f);
        var targetDarkGreen = new Color(0.12f, 0.67f, 0.09f);
        var targetYellow = new Color(0.98f, 0.87f, 0.24f);
        var targetOrange = new Color(0.95f, 0.57f, 0.14f);
        var targetBlack = new Color(0.25f, 0.27f, 0.29f);
        var targetWhite = new Color(0.96f, 0.99f, 0.97f);
        var targetPink = new Color(1f, 0.77f, 1f);
        var targetDarkPink = new Color(0.945f, 0.34f, 0.71f);
        var targetBlue = new Color(0.26f, 0.95f, 0.95f);
        
        var distRed = Vector4.Distance(c, targetRed);
        var distGreen = Vector4.Distance(c, targetGreen);
        var distYellow = Vector4.Distance(c, targetYellow);
        var distOrange = Vector4.Distance(c, targetOrange);
        var distBlack = Vector4.Distance(c, targetBlack);
        var distWhite = Vector4.Distance(c, targetWhite);
        var distPink = Vector4.Distance(c, targetPink);
        var distDarkPink = Vector4.Distance(c, targetDarkPink);
        var distDarkGreen = Vector4.Distance(c, targetDarkGreen);
        var distBlue = Vector4.Distance(c, targetBlue);

        var minDist = Mathf.Min(distRed, distGreen, distYellow, distOrange, distBlack, distWhite, distPink, distBlue, distDarkPink, distDarkGreen);
        
        if (minDist > 0.75f) return null;

        if (Mathf.Approximately(minDist, distRed)) return "red";
        if (Mathf.Approximately(minDist, distGreen)) return "green";
        if (Mathf.Approximately(minDist, distYellow)) return "yellow";
        if (Mathf.Approximately(minDist, distOrange)) return "orange";
        if (Mathf.Approximately(minDist, distBlack)) return "black";
        if (Mathf.Approximately(minDist, distBlue)) return "blue";
        if (Mathf.Approximately(minDist , distDarkGreen)) return "dark green";
        if (Mathf.Approximately(minDist, distDarkPink)) return "dark pink";
        return Mathf.Approximately(minDist, distWhite) ? "white" : "pink";
    }

    public static string MostColoredAtEdge(Dictionary<string, int> dict)
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

    public static void ShuffleList(List<GameObject> list)
    {
        for (var i = 0; i < list.Count; i++)
        {
            var temp = list[i];
            var randomIndex = UnityEngine.Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
}
