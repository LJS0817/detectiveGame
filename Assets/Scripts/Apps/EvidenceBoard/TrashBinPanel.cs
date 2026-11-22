using System.Collections.Generic;
using ColdCaseOS.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColdCaseOS.Apps.EvidenceBoard
{
    /// <summary>
    /// 폐기함으로 이동된 단서 카드 목록을 표시하고 언제든 원클릭 복구할 수 있는 패널.
    /// </summary>
    public class TrashBinPanel : MonoBehaviour
    {
        [SerializeField] private Transform listContainer;
        [SerializeField] private Button restoreItemButtonPrefab;
        [SerializeField] private TMP_Text emptyLabel;

        private readonly List<GameObject> _spawnedRows = new List<GameObject>();

        public void TogglePanel()
        {
            gameObject.SetActive(!gameObject.activeSelf);
            if (gameObject.activeSelf) RefreshList();
        }

        public void RefreshList()
        {
            foreach (var row in _spawnedRows)
                if (row != null) Destroy(row);
            _spawnedRows.Clear();

            var state = AppRoot.Instance?.RuntimeState;
            if (state == null || listContainer == null || restoreItemButtonPrefab == null) return;

            int trashedCount = 0;
            foreach (var card in state.ClueCards)
            {
                if (!card.isInTrash) continue;
                trashedCount++;

                string capturedId = card.cardId;
                var btn = Instantiate(restoreItemButtonPrefab, listContainer);
                var label = btn.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = $"[복구] {card.displayTitle}";
                btn.onClick.AddListener(() => AppRoot.Instance?.RuntimeState.SetTrashState(capturedId, false));
                _spawnedRows.Add(btn.gameObject);
            }

            if (emptyLabel != null)
                emptyLabel.gameObject.SetActive(trashedCount == 0);
        }
    }
}
