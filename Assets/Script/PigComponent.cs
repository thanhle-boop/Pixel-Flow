using TMPro;
using UnityEngine;

public class PigComponent : MonoBehaviour
{
    public int bulletCount;
    public TextMeshProUGUI text;
    public void SetBulletCount(int count)
    {
        bulletCount = count;
        text.text = bulletCount.ToString();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
