using System;
using ColdCaseOS.Core;
using ColdCaseOS.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ColdCaseOS.Apps.EvidenceBoard
{
    /// <summary>
    /// 증거판 빈 공간에 자유롭게 붙여두는 독립 포스트잇 메모 UI.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class StickyNoteView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private TMP_InputField noteInputField;
        [SerializeField] private Button deleteButton;

        private RectTransform _rect;
        private Canvas _rootCanvas;
        private StickyNoteData _data;
        private Action<StickyNoteView, StickyNoteData> _onDelete;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _rootCanvas = GetComponentInParent<Canvas>();

            if (noteInputField != null)
                noteInputField.onEndEdit.AddListener(HandleTextChanged);
            if (deleteButton != null)
                deleteButton.onClick.AddListener(() => _onDelete?.Invoke(this, _data));
        }

        public void Initialize(StickyNoteData data, Action<StickyNoteView, StickyNoteData> onDelete)
        {
            _data = data;
            _onDelete = onDelete;
            _rect.anchoredPosition = data.boardPosition.ToVector2();
            if (noteInputField != null)
                noteInputField.SetTextWithoutNotify(data.text ?? string.Empty);
        }

        private void HandleTextChanged(string text)
        {
            if (_data == null) return;
            _data.text = text;
            AppRoot.Instance?.Campaign.AutoSaveActiveState(AppRoot.Instance.RuntimeState);
        }

        public void OnBeginDrag(PointerEventData eventData) => transform.SetAsLastSibling();

        public void OnDrag(PointerEventData eventData)
        {
            float scale = (_rootCanvas != null && _rootCanvas.scaleFactor > 0f) ? _rootCanvas.scaleFactor : 1f;
            _rect.anchoredPosition += eventData.delta / scale;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_data == null) return;
            _data.boardPosition = SerializableVector2.FromVector2(_rect.anchoredPosition);
            AppRoot.Instance?.Campaign.AutoSaveActiveState(AppRoot.Instance.RuntimeState);
        }
    }
}
