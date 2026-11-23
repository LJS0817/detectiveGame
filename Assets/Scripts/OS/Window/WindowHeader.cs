using ColdCaseOS.OS;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class WindowHeader : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    [SerializeField] private OSWindow _targetWindow;
    [SerializeField] private Button _minimum;
    [SerializeField] private Button _close;
    private Canvas _rootCanvas;

    private void Awake()
    {
        if (_targetWindow == null)
            _targetWindow = GetComponentInParent<OSWindow>();

        _rootCanvas = GetComponentInParent<Canvas>();

        if (_targetWindow != null)
        {
            if (_minimum != null) _minimum.onClick.AddListener(_targetWindow.Minimize);
            if (_close != null) _close.onClick.AddListener(_targetWindow.Close);
        }
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
