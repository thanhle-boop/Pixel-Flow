using UnityEngine;
using UnityEngine.UI;

public class ScrollCamera : MonoBehaviour
{
    [SerializeField] private Scrollbar _myScrollbar;

    private void Start()
    {
        _myScrollbar.onValueChanged.AddListener(OnScrollChanged);
    }

    private void OnScrollChanged(float value)
    {
        transform.position = new Vector3(0, Mathf.Clamp(value* -19,-19,0), -10);
    }

    private void OnDestroy()
    {
        _myScrollbar.onValueChanged.RemoveListener(OnScrollChanged);
    }
}
