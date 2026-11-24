using ColdCaseOS.OS;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class WindowHeader : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private OSWindow _targetWindow;
    [SerializeField] private Button _minimum;
    [SerializeField] private Button _close;
    private Canvas _rootCanvas;
    private bool _isResizingCorner;

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
        _isResizingCorner = _targetWindow != null && _targetWindow.TryBeginResize(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_targetWindow == null) return;
        if (_isResizingCorner)
        {
            _targetWindow.ContinueResize(eventData);
            return;
        }

        float scale = (_rootCanvas != null && _rootCanvas.scaleFactor > 0f) ? _rootCanvas.scaleFactor : 1f;
        _targetWindow.MoveByDelta(eventData.delta / scale);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_isResizingCorner)
        {
            _targetWindow?.EndResize();
            _isResizingCorner = false;
        }
    }
}
