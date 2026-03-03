using TMPro;
using UnityEngine;

public class PigComponent : MonoBehaviour
{
    public int bulletCount;
    public TextMeshProUGUI text;
    public string color;
    public void SetBulletCount(int count,string color)
    {
        bulletCount = count;
        text.text = bulletCount.ToString();
        this.color = color;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}