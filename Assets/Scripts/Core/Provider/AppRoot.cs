using System;
using System.Threading;
using ColdCaseOS.Data;
using ColdCaseOS.OS;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace ColdCaseOS.Core
{
    public enum AppScreenState
    {
        Title,
        CaseCabinet,
        InvestigationOS
    }

    /// <summary>
    /// 프로젝트 전체의 유일한 전역 루트 진입점(Composition Root).
    /// 싱글톤 남발을 방지하기 위해 CampaignService, EpisodeRuntimeState, TimeManager, WindowManager를
    /// 한곳에서 소유·관리하며, 단일 씬(MainScene) 내에서 LoadScene 없이 화면 및 에피소드를 전환합니다.
    /// </summary>
    public class AppRoot : MonoBehaviour
    {
        public static AppRoot Instance { get; private set; }

        [Header("Single-Scene Root Panels")]
        [SerializeField] private CanvasGroup titlePanel;
        [SerializeField] private CanvasGroup caseCabinetPanel;
        [SerializeField] private CanvasGroup investigationOsPanel;
        [SerializeField] private float panelFadeDuration = 0.25f;

        [Header("Core Scene Sub-Managers (Serialized Injection)")]
        [SerializeField] private WindowManager windowManager;
        [SerializeField] private NotificationToastManager toastManager;

        public CampaignController Campaign { get; private set; }
        public EpisodeRuntimeState RuntimeState { get; private set; }
        public TimeManager Time { get; private set; }
        public WindowManager Windows => windowManager;
        public NotificationToastManager Toasts => toastManager;

        private static readonly string[] EpisodeOrder =
        {
            "EP01", "EP02", "EP03", "EP04", "EP05", "EP06", "EP07"
        };

        public AppScreenState CurrentScreen { get; private set; } = AppScreenState.Title;
        public event Action<EpisodeData> OnEpisodeLoaded;
        public event Action<AppScreenState> OnScreenChanged;

        private CancellationTokenSource _loadCts;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Campaign = new CampaignController();
            RuntimeState = new EpisodeRuntimeState();
            Time = new TimeManager(RuntimeState, () => Campaign.SaveData.trustLevel);

            SubscribeAutoSaveEvents();
            SetScreenImmediate(AppScreenState.Title);
        }

        private void OnDestroy()
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            UnsubscribeAutoSaveEvents();
            GameEvents.ClearAll();
            if (Instance == this) Instance = null;
        }

        private void SubscribeAutoSaveEvents()
        {
            GameEvents.OnClueCardAdded += HandleAutoSaveClue;
            GameEvents.OnActionCompleted += HandleAutoSaveAction;
        }

        private void UnsubscribeAutoSaveEvents()
        {
            GameEvents.OnClueCardAdded -= HandleAutoSaveClue;
            GameEvents.OnActionCompleted -= HandleAutoSaveAction;
        }

        private void HandleAutoSaveClue(UserClueCard _) => Campaign.AutoSaveActiveState(RuntimeState);
        private void HandleAutoSaveAction(string _, string __) => Campaign.AutoSaveActiveState(RuntimeState);

        public async UniTask SwitchScreenAsync(AppScreenState target)
        {
            CurrentScreen = target;
            await UniTask.WhenAll(
                FadePanelAsync(titlePanel, target == AppScreenState.Title),
                FadePanelAsync(caseCabinetPanel, target == AppScreenState.CaseCabinet),
                FadePanelAsync(investigationOsPanel, target == AppScreenState.InvestigationOS)
            );
            OnScreenChanged?.Invoke(target);
        }

        private void SetScreenImmediate(AppScreenState target)
        {
            CurrentScreen = target;
            ApplyCanvasGroupImmediate(titlePanel, target == AppScreenState.Title);
            ApplyCanvasGroupImmediate(caseCabinetPanel, target == AppScreenState.CaseCabinet);
            ApplyCanvasGroupImmediate(investigationOsPanel, target == AppScreenState.InvestigationOS);
        }

        public static string GetNextEpisodeId(string currentEpisodeId)
        {
            int idx = Array.IndexOf(EpisodeOrder, currentEpisodeId?.ToUpperInvariant());
            return (idx >= 0 && idx + 1 < EpisodeOrder.Length) ? EpisodeOrder[idx + 1] : null;
        }

        /// <summary>
        /// 타이틀/사건 캐비닛에서 최초 진입하거나 재플레이할 때 에피소드를 로드합니다.
        /// </summary>
        public async UniTask LoadAndStartEpisodeAsync(string episodeId, bool resumeSnapshot = false)
        {
            await LoadEpisodeInternalAsync(
                episodeId,
                resumeSnapshot: resumeSnapshot,
                keepOSAlive: false,
                keepOpenWindowIds: null);
        }

        private async UniTask<EpisodeData> LoadEpisodeInternalAsync(
            string episodeId,
            bool resumeSnapshot,
            bool keepOSAlive,
            string[] keepOpenWindowIds)
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = new CancellationTokenSource();

            var episode = await EpisodeLoader.LoadEpisodeAsync(episodeId, _loadCts.Token);
            if (episode == null) return null;

            if (keepOSAlive)
                windowManager?.CloseAllWindowsExcept(keepOpenWindowIds);
            else
                windowManager?.CloseAllWindowsImmediate();

            bool restored = resumeSnapshot && Campaign.HasActiveSnapshot(episodeId);
            if (restored)
            {
                RuntimeState.RestoreFromSnapshot(episode, Campaign.SaveData.activeEpisodeSnapshot);
            }
            else
            {
                RuntimeState.InitializeNew(episode);
                Campaign.AutoSaveActiveState(RuntimeState);
            }

            if (!keepOSAlive || CurrentScreen != AppScreenState.InvestigationOS)
            {
                await SwitchScreenAsync(AppScreenState.InvestigationOS);
            }

            OnEpisodeLoaded?.Invoke(episode);
            windowManager?.OpenWindow("FileExplorer");
            GameEvents.RaiseToastRequested("사건 파일 로드 완료", $"[{episode.title}] 모든 사건 문서와 도면이 파일 탐색기에 준비되었습니다.");
            return episode;
        }

        private async UniTask FadePanelAsync(CanvasGroup group, bool visible)
        {
            if (group == null) return;
            group.DOKill();

            if (visible)
            {
                group.gameObject.SetActive(true);
                group.interactable = true;
                group.blocksRaycasts = true;
                await group.DOFade(1f, panelFadeDuration).SetEase(Ease.OutQuad).AsyncWaitForCompletion();
            }
            else
            {
                group.interactable = false;
                group.blocksRaycasts = false;
                await group.DOFade(0f, panelFadeDuration).SetEase(Ease.InQuad).AsyncWaitForCompletion();
                group.gameObject.SetActive(false);
            }
        }

        private static void ApplyCanvasGroupImmediate(CanvasGroup group, bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
            group.gameObject.SetActive(visible);
        }
    }
}
