using System;
using System.Collections.Generic;
using System.Threading;
using ColdCaseOS.Core;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace ColdCaseOS.OS
{
    /// <summary>
    /// UniTask + DOTween 기반 OS 우측 하단 알림 토스트 매니저 (AppRoot 소속).
    /// </summary>
    public class NotificationToastManager : MonoBehaviour
    {
        [SerializeField] private RectTransform toastRect;
        [SerializeField] private CanvasGroup toastGroup;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private float displaySeconds = 2.6f;

        private readonly Queue<(string title, string body)> _queue = new Queue<(string, string)>();
        private bool _isShowing;
        private CancellationTokenSource _cts;
        private Vector2 _restPos;

        private void Awake()
        {
            if (toastRect != null) _restPos = toastRect.anchoredPosition;
            if (toastGroup != null)
            {
                toastGroup.alpha = 0f;
                toastGroup.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            GameEvents.OnToastRequested += EnqueueToast;
        }

        private void OnDisable()
        {
            GameEvents.OnToastRequested -= EnqueueToast;
            _cts?.Cancel();
        }

        public void EnqueueToast(string title, string body)
        {
            _queue.Enqueue((title, body));
            if (!_isShowing)
            {
                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                ProcessQueueAsync(_cts.Token).Forget();
            }
        }

        private async UniTaskVoid ProcessQueueAsync(CancellationToken ct)
        {
            _isShowing = true;
            try
            {
                while (_queue.Count > 0 && !ct.IsCancellationRequested)
                {
                    var (title, body) = _queue.Dequeue();
                    if (titleText != null) titleText.text = title;
                    if (bodyText != null) bodyText.text = body;

                    toastGroup.gameObject.SetActive(true);
                    toastGroup.DOKill();
                    toastRect.DOKill();

                    toastGroup.alpha = 0f;
                    toastRect.anchoredPosition = _restPos + new Vector2(40f, 0f);

                    toastGroup.DOFade(1f, 0.2f).SetEase(Ease.OutQuad);
                    await toastRect.DOAnchorPos(_restPos, 0.2f).SetEase(Ease.OutCubic).AsyncWaitForCompletion();

                    await UniTask.Delay(TimeSpan.FromSeconds(displaySeconds), cancellationToken: ct);

                    toastGroup.DOFade(0f, 0.2f).SetEase(Ease.InQuad);
                    await toastRect.DOAnchorPos(_restPos + new Vector2(40f, 0f), 0.2f).SetEase(Ease.InCubic).AsyncWaitForCompletion();
                    toastGroup.gameObject.SetActive(false);
                }
            }
            catch (OperationCanceledException)
            {
                // 씬 종료/비활성화 시 정상 취소
            }
            finally
            {
                _isShowing = false;
            }
        }
    }
}
