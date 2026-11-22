using System.Collections.Generic;
using System.Linq;
using ColdCaseOS.Core;
using ColdCaseOS.Data;
using ColdCaseOS.OS;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColdCaseOS.Apps.Messenger
{
    /// <summary>
    /// OSWindow를 직접 상속하는 파트너(강민호 경사) 및 상부 지시 메신저 앱 창.
    /// 대기 중인 다음 사건 배정(HasPendingEpisode)이 있을 때 플레이어가 이 창을 열면
    /// 수락 버튼 없이 즉시 다음 에피소드가 로드되고 마감 시한이 시작됩니다.
    /// </summary>
    public class MessengerController : OSWindow
    {
        [Header("Messenger UI")]
        [SerializeField] private Transform messageListContainer;
        [SerializeField] private GameObject messageItemPrefab;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Button askPartnerHintButton;
        [SerializeField] private TMP_Text headerStatusText;

        private readonly List<GameObject> _spawnedItems = new List<GameObject>();
        private int _hintStage;
        private bool _isStartingPendingEpisode;

        protected override void Awake()
        {
            base.Awake();
            if (askPartnerHintButton != null)
                askPartnerHintButton.onClick.AddListener(OnAskPartnerHintClicked);
        }

        private void OnEnable()
        {
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded += HandleEpisodeLoaded;

            GameEvents.OnMessengerMessageAdded += HandleMessageAdded;
            RebuildMessageList();
        }

        private void OnDisable()
        {
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded -= HandleEpisodeLoaded;

            GameEvents.OnMessengerMessageAdded -= HandleMessageAdded;
        }

        protected override void OnWindowFocused()
        {
            base.OnWindowFocused();
            CheckAndStartPendingEpisodeAsync().Forget();
        }

        private async UniTaskVoid CheckAndStartPendingEpisodeAsync()
        {
            var app = AppRoot.Instance;
            if (_isStartingPendingEpisode || app == null || !app.HasPendingEpisode) return;

            _isStartingPendingEpisode = true;
            try
            {
                bool started = await app.TryStartPendingEpisodeOnMessengerOpenedAsync(WindowId);
                if (started)
                {
                    _hintStage = 0;
                    RebuildMessageList();
                }
            }
            finally
            {
                _isStartingPendingEpisode = false;
            }
        }

        private void HandleEpisodeLoaded(EpisodeData episode)
        {
            _hintStage = 0;
            if (headerStatusText != null && episode != null)
                headerStatusText.text = $"강민호 경사 / 서울청 수사심의관 ({episode.caseNumber})";
            RebuildMessageList();
        }

        private void HandleMessageAdded(MessengerMessageData msg)
        {
            SpawnMessageItem(msg);
            ScrollToBottom();
        }

        public void RebuildMessageList()
        {
            foreach (var go in _spawnedItems)
                if (go != null) Destroy(go);
            _spawnedItems.Clear();

            var logs = AppRoot.Instance?.RuntimeState?.MessengerLogs;
            if (logs == null) return;

            foreach (var msg in logs)
                SpawnMessageItem(msg);

            ScrollToBottom();
        }

        private void SpawnMessageItem(MessengerMessageData msg)
        {
            if (messageListContainer == null || messageItemPrefab == null || msg == null) return;

            var go = Instantiate(messageItemPrefab, messageListContainer);
            var text = go.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                string prefix = msg.isSystemAlert ? "[공지] " : string.Empty;
                text.text = $"<b>{prefix}{msg.sender}</b> <size=80%>({msg.timestamp})</size>\n{msg.body}";
            }
            _spawnedItems.Add(go);
        }

        private void ScrollToBottom()
        {
            if (scrollRect == null) return;
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }

        /// <summary>
        /// [강민호 경사에게 의견 묻기] 2단계 상황 맞춤형 힌트.
        /// 1단계: 미열람 파일 / 미발견 인물 안내, 2단계: 미완료 현장 지시 / 증거판 조합 안내.
        /// </summary>
        private void OnAskPartnerHintClicked()
        {
            var app = AppRoot.Instance;
            var state = app?.RuntimeState;
            var episode = state?.Episode;
            if (app == null || state == null || episode == null) return;

            string hintBody;
            if (_hintStage == 0)
            {
                var unreadDoc = episode.documents.FirstOrDefault(d =>
                    state.UnlockedDocIds.Contains(d.docId) && !state.ReadItemIds.Contains(d.docId));

                if (unreadDoc != null)
                {
                    hintBody = $"경위님, 아직 확인하지 않은 문서 [{unreadDoc.title}]가 있습니다. 핵심 인물이나 단서가 있을 수 있으니 먼저 훑어보시죠.";
                }
                else
                {
                    hintBody = "현재 확보한 문서는 다 보신 것 같습니다. 의심되는 이름이나 차량 번호가 있다면 경찰 DB에서 조회해 보시는 게 좋겠습니다.";
                }
                _hintStage = 1;
            }
            else
            {
                var pendingAction = episode.actions.FirstOrDefault(a =>
                    state.UnlockedNodeIds.Contains(a.nodeId) &&
                    !a.requiresTargetSelect &&
                    !state.IsActionCompleted(a.actionId));

                if (pendingAction != null)
                {
                    var node = episode.mapNodes.Find(n => n.nodeId == pendingAction.nodeId);
                    string nodeName = node != null ? node.name : pendingAction.nodeId;
                    hintBody = $"현장 지도에서 [{nodeName}]의 '{pendingAction.title}' 조사가 아직 남아 있습니다. 지시해 주시면 바로 움직이겠습니다.";
                }
                else
                {
                    hintBody = "주요 현장 자료는 거의 모인 듯합니다. 문서의 모순 구절과 사진·CCTV 캡처를 증거판에 정리한 뒤 송치 보고서를 작성해 보시죠.";
                }
                _hintStage = 0;
            }

            state.AddMessengerMessage("강민호 경사", hintBody, app.Time.FormattedCurrentClock, isSystemAlert: false);
        }
    }
}
