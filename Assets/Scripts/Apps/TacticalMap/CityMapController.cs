using System.Collections.Generic;
using System.Linq;
using ColdCaseOS.Core;
using ColdCaseOS.Data;
using ColdCaseOS.OS;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColdCaseOS.Apps.TacticalMap
{
    /// <summary>
    /// OSWindow를 상속하는 2D 수사 지도 & 현장 파트너 지휘 앱 창.
    /// 파트너 현재 위치 유지, 결정론적 이동/작업/보고 시간 계산, 지시 실행 및 시간 보내기(스킵)를 총괄합니다.
    /// </summary>
    public class CityMapController : OSWindow
    {
        [Header("Map Canvas")]
        [SerializeField] private RectTransform nodeContainer;
        [SerializeField] private MapNodeView nodePrefab;

        [Header("Action Order Panel")]
        [SerializeField] private TMP_Text selectedNodeTitleText;
        [SerializeField] private TMP_Text selectedNodeDescText;
        [SerializeField] private TMP_Text partnerLocationText;
        [SerializeField] private Transform actionListContainer;
        [SerializeField] private Button actionButtonPrefab;

        [Header("Modals & Overlays")]
        [SerializeField] private TargetSelectModal targetSelectModal;
        [SerializeField] private TimeSkipOverlay timeSkipOverlay;

        private readonly Dictionary<string, MapNodeView> _nodeViews = new Dictionary<string, MapNodeView>();
        private readonly List<GameObject> _actionButtons = new List<GameObject>();
        private string _selectedNodeId;
        private bool _isExecutingAction;

        private void OnEnable()
        {
            GameEvents.OnNodeUnlocked += HandleNodeUnlocked;
            GameEvents.OnActionCompleted += HandleActionCompleted;
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded += HandleEpisodeLoaded;
        }

        private void OnDisable()
        {
            GameEvents.OnNodeUnlocked -= HandleNodeUnlocked;
            GameEvents.OnActionCompleted -= HandleActionCompleted;
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded -= HandleEpisodeLoaded;
        }

        private void HandleEpisodeLoaded(EpisodeData _)
        {
            RebuildMap();
        }

        private void HandleNodeUnlocked(string _)
        {
            RebuildMap();
        }

        private void HandleActionCompleted(string _, string __)
        {
            RefreshNodeVisuals();
            if (!string.IsNullOrEmpty(_selectedNodeId))
                SelectNode(_selectedNodeId);
        }

        public void RebuildMap()
        {
            foreach (var kv in _nodeViews)
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            _nodeViews.Clear();

            var state = AppRoot.Instance?.RuntimeState;
            if (state?.Episode == null || nodePrefab == null || nodeContainer == null) return;

            foreach (var node in state.Episode.mapNodes)
            {
                if (!state.UnlockedNodeIds.Contains(node.nodeId)) continue;
                var view = Instantiate(nodePrefab, nodeContainer);
                view.Initialize(node, n => SelectNode(n.nodeId));
                _nodeViews[node.nodeId] = view;
            }

            RefreshNodeVisuals();
            SelectNode(state.CurrentPartnerNodeId);
        }

        public void SelectNode(string nodeId)
        {
            _selectedNodeId = nodeId;
            RefreshNodeVisuals();
            RebuildActionButtons(nodeId);
            GameEvents.RaiseSfxRequested("SFX_MAP_PIN");
        }

        private void RefreshNodeVisuals()
        {
            var state = AppRoot.Instance?.RuntimeState;
            if (state?.Episode == null) return;

            var partnerNode = state.Episode.mapNodes.Find(n => n.nodeId == state.CurrentPartnerNodeId);
            if (partnerLocationText != null && partnerNode != null)
                partnerLocationText.text = $"파트너 현재 위치: {partnerNode.name}";

            foreach (var kv in _nodeViews)
            {
                string nid = kv.Key;
                bool isPartnerHere = state.CurrentPartnerNodeId == nid;
                bool isSelected = _selectedNodeId == nid;
                bool hasUnfinished = state.Episode.actions.Any(a => a.nodeId == nid && !IsActionFullyCompleted(a, state));
                kv.Value.UpdateStatus(isPartnerHere, isSelected, hasUnfinished);
            }
        }

        private void RebuildActionButtons(string nodeId)
        {
            foreach (var go in _actionButtons)
                if (go != null) Destroy(go);
            _actionButtons.Clear();

            var app = AppRoot.Instance;
            var state = app?.RuntimeState;
            if (state?.Episode == null || actionButtonPrefab == null || actionListContainer == null) return;

            var node = state.Episode.mapNodes.Find(n => n.nodeId == nodeId);
            if (selectedNodeTitleText != null) selectedNodeTitleText.text = node != null ? node.name : "";
            if (selectedNodeDescText != null) selectedNodeDescText.text = node != null ? node.description : "";

            int travelMin = GetTravelMinutes(state.Episode, state.CurrentPartnerNodeId, nodeId);

            foreach (var act in state.Episode.actions.Where(a => a.nodeId == nodeId))
            {
                var breakdown = app.Time.CalculateBreakdown(travelMin, act);
                bool completed = IsActionFullyCompleted(act, state);

                var btn = Instantiate(actionButtonPrefab, actionListContainer);
                var label = btn.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    string statusTag = completed ? "[완료] " : "";
                    label.text = $"{statusTag}{act.title}\n" +
                                 $"<size=80%>예상 소요: {TimeManager.FormatMinutesKorean(breakdown.TotalMinutes)} " +
                                 $"(이동 {breakdown.travelMinutes}분 + 조사 {breakdown.workMinutes}분 + 보고 {breakdown.reportMinutes}분)</size>";
                }

                btn.interactable = !completed;
                var capturedAct = act;
                btn.onClick.AddListener(() => OnActionButtonClicked(capturedAct, node, breakdown));
                _actionButtons.Add(btn.gameObject);
            }
        }

        private void OnActionButtonClicked(FieldActionData action, MapNodeData node, ActionTimeBreakdown breakdown)
        {
            if (_isExecutingAction) return;

            if (action.requiresTargetSelect && targetSelectModal != null)
            {
                var state = AppRoot.Instance.RuntimeState;
                targetSelectModal.Open(
                    action,
                    targetId => state.DiscoveredSuspectIds.Contains(targetId),
                    targetId => state.IsActionCompleted(action.actionId, targetId),
                    chosen => ExecuteActionAsync(action, node, breakdown, chosen).Forget());
            }
            else
            {
                ExecuteActionAsync(action, node, breakdown, targetOption: null).Forget();
            }
        }

        private async UniTaskVoid ExecuteActionAsync(
            FieldActionData action,
            MapNodeData destNode,
            ActionTimeBreakdown breakdown,
            ActionTargetOption targetOption)
        {
            _isExecutingAction = true;
            try
            {
                var app = AppRoot.Instance;
                var state = app.RuntimeState;

                if (timeSkipOverlay != null)
                    await timeSkipOverlay.PlaySkipAsync(destNode.name, breakdown, this.GetCancellationTokenOnDestroy());

                // 1. 파트너 위치 갱신 & 결정론적 시간 차감
                state.CurrentPartnerNodeId = destNode.nodeId;
                GameEvents.RaisePartnerMoved(destNode.nodeId);
                app.Time.AdvanceMinutes(breakdown.TotalMinutes);

                // 2. 보상 자료 해금
                bool withinVolatile = app.Time.IsWithinVolatileWindow;
                UnlockRewards(state, action, targetOption, withinVolatile);

                // 3. 파트너 메신저 보고 전송
                string reportMsg = targetOption != null && !string.IsNullOrEmpty(targetOption.partnerReportMessage)
                    ? targetOption.partnerReportMessage
                    : action.partnerReportMessage;

                if (!string.IsNullOrEmpty(reportMsg))
                    state.AddMessengerMessage("강민호 경사", reportMsg, app.Time.FormattedCurrentClock);

                // 4. 액션 완료 처리 (자동 저장 트리거)
                state.MarkActionCompleted(action.actionId, targetOption?.targetId ?? "");
            }
            finally
            {
                _isExecutingAction = false;
            }
        }

        private static void UnlockRewards(
            EpisodeRuntimeState state,
            FieldActionData action,
            ActionTargetOption targetOption,
            bool withinVolatile)
        {
            foreach (var id in action.rewardDocIds) state.UnlockDocument(id);
            foreach (var id in action.rewardPhotoIds) state.UnlockPhoto(id);
            foreach (var id in action.rewardCctvIds) state.UnlockCctv(id);
            foreach (var id in action.rewardAudioIds) state.UnlockAudio(id);

            if (targetOption != null)
            {
                foreach (var id in targetOption.rewardDocIds) state.UnlockDocument(id);
            }

            string volatileDocId = targetOption != null && !string.IsNullOrEmpty(targetOption.volatileBonusDocId)
                ? targetOption.volatileBonusDocId
                : action.volatileBonusDocId;

            if (!string.IsNullOrEmpty(volatileDocId))
            {
                if (withinVolatile)
                {
                    state.UnlockDocument(volatileDocId);
                    state.CurrentVolatileClueState = VolatileClueState.Complete;
                    GameEvents.RaiseToastRequested("휘발성 흑막 단서 확보!", "골든 타임 내 원본 디지털 흔적을 온전히 확보했습니다.");
                }
                else
                {
                    if (state.CurrentVolatileClueState == VolatileClueState.None)
                        state.CurrentVolatileClueState = VolatileClueState.Damaged;
                    GameEvents.RaiseToastRequested("휘발성 단서 일부 손상", "골든 타임이 지나 원본 로그 일부가 삭제되었습니다.");
                }
            }
        }

        private static bool IsActionFullyCompleted(FieldActionData act, EpisodeRuntimeState state)
        {
            if (!act.requiresTargetSelect)
                return state.IsActionCompleted(act.actionId);

            return act.targetOptions.Count > 0 &&
                   act.targetOptions.All(o => state.IsActionCompleted(act.actionId, o.targetId));
        }

        public static int GetTravelMinutes(EpisodeData episode, string fromNodeId, string toNodeId)
        {
            if (string.IsNullOrEmpty(fromNodeId) || fromNodeId == toNodeId) return 0;
            var edge = episode.travelEdges.Find(e => e.Connects(fromNodeId, toNodeId));
            return edge != null ? edge.minutes : 30;
        }
    }
}
