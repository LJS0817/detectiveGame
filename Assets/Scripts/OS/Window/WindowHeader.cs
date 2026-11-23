using ColdCaseOS.OS;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class WindowHeader : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    [SerializeField] private OSWindow _targetWindow;
    [SerializeField] Button _minimum;
    [SerializeField] Button _close;
    private Canvas _rootCanvas;

    private void Awake()
    {
        if (_targetWindow == null)
            _targetWindow = transform.parent.GetComponent<OSWindow>();
        _rootCanvas = GetComponentInParent<Canvas>();
        _minimum.onClick.AddListener(_targetWindow.Minimize);
        _close.onClick.AddListener(_targetWindow.Close);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _targetWindow?.OnPointerDown(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_targetWindow == null) return;
        float scale = (_rootCanvas != null && _rootCanvas.scaleFactor > 0f) ? _rootCanvas.scaleFactor : 1f;
        _targetWindow.MoveByDelta(eventData.delta / scale);
    }
}
