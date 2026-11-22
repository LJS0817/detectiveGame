using System;
using System.Threading;
using ColdCaseOS.Core;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace ColdCaseOS.Apps.TacticalMap
{
    /// <summary>
    /// '조사 완료까지 시간 보내기' 실행 시 1~1.5초간 파트너 이동 및 경과 시간을 보여주는 연출 오버레이.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class TimeSkipOverlay : MonoBehaviour
    {
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text elapsedDetailText;
        [SerializeField] private float skipVisualSeconds = 1.1f;

        private CanvasGroup _group;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            gameObject.SetActive(false);
        }

        public async UniTask PlaySkipAsync(string destinationName, ActionTimeBreakdown breakdown, CancellationToken ct = default)
        {
            if (statusText != null) statusText.text = $"강민호 경사 — [{destinationName}] 출동 및 조사 진행 중...";
            if (elapsedDetailText != null)
            {
                elapsedDetailText.text =
                    $"이동 {TimeManager.FormatMinutesKorean(breakdown.travelMinutes)} + " +
                    $"조사 {TimeManager.FormatMinutesKorean(breakdown.workMinutes)} + " +
                    $"보고 {TimeManager.FormatMinutesKorean(breakdown.reportMinutes)} = " +
                    $"총 {TimeManager.FormatMinutesKorean(breakdown.TotalMinutes)} 경과";
            }

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _group.DOKill();
            _group.alpha = 0f;

            await _group.DOFade(1f, 0.18f).SetEase(Ease.OutQuad).AsyncWaitForCompletion();
            await UniTask.Delay(TimeSpan.FromSeconds(skipVisualSeconds), cancellationToken: ct);
            await _group.DOFade(0f, 0.18f).SetEase(Ease.InQuad).AsyncWaitForCompletion();

            gameObject.SetActive(false);
        }
    }
}
