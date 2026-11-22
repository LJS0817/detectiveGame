using System;
using System.Collections.Generic;
using ColdCaseOS.Data;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColdCaseOS.Apps.TacticalMap
{
    /// <summary>
    /// EP 2~5 대상 선택 영장(금융·메일) 집행 시 플레이어가 용의자 중 1명을 직접 지정하는 팝업 모달.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class TargetSelectModal : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text emptyNoticeText;
        [SerializeField] private Transform optionContainer;
        [SerializeField] private Button optionButtonPrefab;
        [SerializeField] private Button cancelButton;

        private CanvasGroup _group;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            if (cancelButton != null)
                cancelButton.onClick.AddListener(Close);
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 플레이어가 문서 열람·경찰 DB 조회 등을 통해 실제로 발견한 인물(isDiscovered)만 선택지로 표시합니다.
        /// </summary>
        public void Open(
            FieldActionData action,
            Func<string, bool> isTargetDiscovered,
            Func<string, bool> isTargetCompleted,
            Action<ActionTargetOption> onTargetChosen)
        {
            ClearOptions();
            if (titleText != null) titleText.text = $"{action.title} — 대상 선택";

            int visibleCount = 0;
            foreach (var opt in action.targetOptions)
            {
                if (isTargetDiscovered != null && !isTargetDiscovered(opt.targetId))
                    continue;

                visibleCount++;
                bool done = isTargetCompleted != null && isTargetCompleted(opt.targetId);
                var btn = Instantiate(optionButtonPrefab, optionContainer);
                var label = btn.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = done ? $"{opt.label} (완료)" : opt.label;
                btn.interactable = !done;

                var captured = opt;
                btn.onClick.AddListener(() =>
                {
                    Close();
                    onTargetChosen?.Invoke(captured);
                });
                _spawned.Add(btn.gameObject);
            }

            if (emptyNoticeText != null)
            {
                emptyNoticeText.gameObject.SetActive(visibleCount == 0);
                emptyNoticeText.text = "지정 가능한 발견된 인물이 없습니다. 사건 문서나 K-POL DB에서 인물을 먼저 조사하십시오.";
            }

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _group.DOKill();
            _group.alpha = 0f;
            _group.DOFade(1f, 0.15f).SetEase(Ease.OutQuad);
        }

        public void Close()
        {
            _group.DOKill();
            _group.DOFade(0f, 0.12f).OnComplete(() => gameObject.SetActive(false));
        }

        private void ClearOptions()
        {
            foreach (var go in _spawned)
                if (go != null) Destroy(go);
            _spawned.Clear();
        }
    }
}
