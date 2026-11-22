using System;
using ColdCaseOS.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColdCaseOS.Apps.TacticalMap
{
    /// <summary>
    /// 2D 수사 지도 위 개별 장소 노드(핀) UI.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class MapNodeView : MonoBehaviour
    {
        [SerializeField] private Button selectButton;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private GameObject partnerHereIndicator;
        [SerializeField] private GameObject selectedHighlight;
        [SerializeField] private GameObject pendingActionDot;

        public MapNodeData Data { get; private set; }
        private RectTransform _rect;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
        }

        public void Initialize(MapNodeData data, Action<MapNodeData> onSelected)
        {
            Data = data;
            _rect.anchoredPosition = new Vector2(data.x, data.y);
            if (nameText != null) nameText.text = data.name;

            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(() => onSelected?.Invoke(Data));
            }
        }

        public void UpdateStatus(bool isPartnerHere, bool isSelected, bool hasUnfinishedAction)
        {
            if (partnerHereIndicator != null) partnerHereIndicator.SetActive(isPartnerHere);
            if (selectedHighlight != null) selectedHighlight.SetActive(isSelected);
            if (pendingActionDot != null) pendingActionDot.SetActive(hasUnfinishedAction);
        }
    }
}
