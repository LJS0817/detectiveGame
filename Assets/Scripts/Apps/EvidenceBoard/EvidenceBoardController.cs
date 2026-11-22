using System;
using System.Collections.Generic;
using ColdCaseOS.Core;
using ColdCaseOS.Data;
using ColdCaseOS.OS;
using UnityEngine;
using UnityEngine.UI;

namespace ColdCaseOS.Apps.EvidenceBoard
{
    /// <summary>
    /// OSWindow를 상속하는 디지털 증거판(Evidence Board) 메인 컨트롤러.
    /// 단서 카드 배치, 핀-실(선) 연결, 독립 포스트잇 생성, 폐기함 토글을 관리합니다.
    /// </summary>
    public class EvidenceBoardController : OSWindow
    {
        [Header("Board Containers & Prefabs")]
        [SerializeField] private RectTransform cardContainer;
        [SerializeField] private RectTransform wireContainer;
        [SerializeField] private ClueCardView clueCardPrefab;
        [SerializeField] private StickyNoteView stickyNotePrefab;
        [SerializeField] private PinWireRenderer pinWirePrefab;

        [Header("Board Toolbar")]
        [SerializeField] private Button addStickyNoteButton;
        [SerializeField] private Button toggleTrashBinButton;
        [SerializeField] private TrashBinPanel trashBinPanel;

        private readonly Dictionary<string, ClueCardView> _cardViews = new Dictionary<string, ClueCardView>();
        private readonly List<PinWireRenderer> _activeWires = new List<PinWireRenderer>();
        private readonly List<StickyNoteView> _stickyViews = new List<StickyNoteView>();

        private string _wireSourceCardId;

        protected override void Awake()
        {
            base.Awake();
            if (addStickyNoteButton != null)
                addStickyNoteButton.onClick.AddListener(CreateStickyNote);
            if (toggleTrashBinButton != null && trashBinPanel != null)
                toggleTrashBinButton.onClick.AddListener(() => trashBinPanel.TogglePanel());
        }

        private void OnEnable()
        {
            GameEvents.OnClueCardAdded += HandleClueCardAdded;
            GameEvents.OnClueCardUpdated += HandleClueCardUpdated;
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded += HandleEpisodeLoaded;
        }

        private void OnDisable()
        {
            GameEvents.OnClueCardAdded -= HandleClueCardAdded;
            GameEvents.OnClueCardUpdated -= HandleClueCardUpdated;
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded -= HandleEpisodeLoaded;
        }

        private void HandleEpisodeLoaded(EpisodeData _)
        {
            RebuildBoardFromState();
        }

        public void RebuildBoardFromState()
        {
            ClearAllViews();
            var state = AppRoot.Instance?.RuntimeState;
            if (state == null) return;

            foreach (var card in state.ClueCards)
            {
                SpawnCardView(card);
            }

            foreach (var note in state.StickyNotes)
            {
                SpawnStickyNoteView(note);
            }

            RebuildAllWires();
            trashBinPanel?.RefreshList();
        }

        private void HandleClueCardAdded(UserClueCard card)
        {
            if (card == null || _cardViews.ContainsKey(card.cardId)) return;

            // 신규 카드가 겹치지 않도록 계단식 기본 위치 배정
            int count = _cardViews.Count;
            card.boardPosition = new SerializableVector2((count % 4) * 240f - 360f, -(count / 4) * 180f + 160f);
            SpawnCardView(card);
        }

        private void HandleClueCardUpdated(UserClueCard card)
        {
            if (card == null) return;
            if (_cardViews.TryGetValue(card.cardId, out var view))
            {
                view.RefreshVisual();
            }

            RebuildAllWires();
            trashBinPanel?.RefreshList();
        }

        private void SpawnCardView(UserClueCard card)
        {
            if (clueCardPrefab == null || cardContainer == null) return;
            var view = Instantiate(clueCardPrefab, cardContainer);
            view.Initialize(card, this);
            _cardViews[card.cardId] = view;
        }

        /// <summary>
        /// 카드 상단 핀 클릭 시 선 연결 시작 또는 대상 카드와 연결 토글.
        /// </summary>
        public void OnPinClicked(string cardId)
        {
            if (string.IsNullOrEmpty(_wireSourceCardId))
            {
                _wireSourceCardId = cardId;
                GameEvents.RaiseToastRequested("선 연결 모드", "연결할 다른 단서 카드의 핀을 클릭하십시오.");
                return;
            }

            if (_wireSourceCardId == cardId)
            {
                _wireSourceCardId = null;
                return;
            }

            AppRoot.Instance?.RuntimeState.ToggleWireConnection(_wireSourceCardId, cardId);
            _wireSourceCardId = null;
            GameEvents.RaiseSfxRequested("SFX_MAP_PIN");
        }

        public void NotifyCardMoved()
        {
            foreach (var wire in _activeWires)
            {
                wire.UpdatePositions();
            }
        }

        private void RebuildAllWires()
        {
            foreach (var wire in _activeWires)
                if (wire != null) Destroy(wire.gameObject);
            _activeWires.Clear();

            var state = AppRoot.Instance?.RuntimeState;
            if (state == null || pinWirePrefab == null || wireContainer == null) return;

            var drawnPairs = new HashSet<string>();
            foreach (var card in state.ClueCards)
            {
                if (card.isInTrash || !_cardViews.TryGetValue(card.cardId, out var viewA)) continue;
                foreach (var targetId in card.connectedCardIds)
                {
                    if (!_cardViews.TryGetValue(targetId, out var viewB)) continue;
                    var targetData = state.GetClueCard(targetId);
                    if (targetData == null || targetData.isInTrash) continue;

                    string pairKey = string.CompareOrdinal(card.cardId, targetId) < 0
                        ? $"{card.cardId}__{targetId}"
                        : $"{targetId}__{card.cardId}";
                    if (!drawnPairs.Add(pairKey)) continue;

                    var wire = Instantiate(pinWirePrefab, wireContainer);
                    wire.Bind(viewA.PinAnchor, viewB.PinAnchor);
                    _activeWires.Add(wire);
                }
            }
        }

        private void CreateStickyNote()
        {
            var state = AppRoot.Instance?.RuntimeState;
            if (state == null) return;

            var data = new StickyNoteData
            {
                noteId = Guid.NewGuid().ToString("N"),
                text = "",
                boardPosition = new SerializableVector2(UnityEngine.Random.Range(-120f, 120f), UnityEngine.Random.Range(-80f, 80f))
            };
            state.StickyNotes.Add(data);
            SpawnStickyNoteView(data);
            AppRoot.Instance.Campaign.AutoSaveActiveState(state);
        }

        private void SpawnStickyNoteView(StickyNoteData data)
        {
            if (stickyNotePrefab == null || cardContainer == null) return;
            var view = Instantiate(stickyNotePrefab, cardContainer);
            view.Initialize(data, HandleDeleteStickyNote);
            _stickyViews.Add(view);
        }

        private void HandleDeleteStickyNote(StickyNoteView view, StickyNoteData data)
        {
            AppRoot.Instance?.RuntimeState.StickyNotes.Remove(data);
            _stickyViews.Remove(view);
            if (view != null) Destroy(view.gameObject);
            AppRoot.Instance?.Campaign.AutoSaveActiveState(AppRoot.Instance.RuntimeState);
        }

        private void ClearAllViews()
        {
            foreach (var kv in _cardViews)
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            _cardViews.Clear();

            foreach (var w in _activeWires)
                if (w != null) Destroy(w.gameObject);
            _activeWires.Clear();

            foreach (var n in _stickyViews)
                if (n != null) Destroy(n.gameObject);
            _stickyViews.Clear();

            _wireSourceCardId = null;
        }
    }
}
