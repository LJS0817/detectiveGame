using System.Collections.Generic;
using System.Text.RegularExpressions;
using ColdCaseOS.Core;
using ColdCaseOS.Data;
using ColdCaseOS.OS;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColdCaseOS.Apps.PoliceDB
{
    /// <summary>
    /// OSWindow를 상속하는 K-POL 경찰 통합 조회 DB 앱 창 (확정 B안 — 좌측 최근 조회 이력 사이드바 + 우측 검색·결과 시트).
    /// 이름, 차량번호, 법인명, 계좌번호를 정규화 검색하고 신규 지도 노드·용의자를 동적으로 해금합니다.
    /// </summary>
    public class PoliceDatabaseController : OSWindow
    {
        [Header("Left History Sidebar (Layout B)")]
        [SerializeField] private Transform historyListContainer;
        [SerializeField] private Button historyItemPrefab;

        [Header("Right Search & Result UI")]
        [SerializeField] private TMP_InputField searchInputField;
        [SerializeField] private Button searchButton;
        [SerializeField] private TMP_Text resultTitleText;
        [SerializeField] private TMP_Text resultBodyText;
        [SerializeField] private Button addResultToBoardButton;

        private readonly List<GameObject> _historyRows = new List<GameObject>();
        private PoliceDbEntry _lastEntry;

        protected override void Awake()
        {
            base.Awake();
            if (searchButton != null)
                searchButton.onClick.AddListener(ExecuteSearch);
            if (searchInputField != null)
                searchInputField.onSubmit.AddListener(_ => ExecuteSearch());
            if (addResultToBoardButton != null)
            {
                addResultToBoardButton.onClick.AddListener(AddLastResultToBoard);
                addResultToBoardButton.interactable = false;
            }
        }

        private void OnEnable()
        {
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded += HandleEpisodeLoaded;
            RefreshHistorySidebar();
        }

        private void OnDisable()
        {
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded -= HandleEpisodeLoaded;
        }

        private void HandleEpisodeLoaded(EpisodeData _)
        {
            _lastEntry = null;
            if (resultTitleText != null) resultTitleText.text = "조회 대기 중";
            if (resultBodyText != null) resultBodyText.text = "인물명, 차량번호, 법인명을 입력해 조회하세요.";
            if (addResultToBoardButton != null) addResultToBoardButton.interactable = false;
            RefreshHistorySidebar();
        }

        public void ExecuteSearch()
        {
            var state = AppRoot.Instance?.RuntimeState;
            if (state?.Episode == null || searchInputField == null) return;

            string normalizedQuery = NormalizeKeyword(searchInputField.text);
            if (string.IsNullOrEmpty(normalizedQuery)) return;

            GameEvents.RaiseSfxRequested("SFX_MOUSE_CLICK");

            PoliceDbEntry matched = null;
            foreach (var entry in state.Episode.dbEntries)
            {
                foreach (var kw in entry.keywords)
                {
                    if (NormalizeKeyword(kw) == normalizedQuery)
                    {
                        matched = entry;
                        break;
                    }
                }
                if (matched != null) break;
            }

            if (matched == null)
            {
                _lastEntry = null;
                if (resultTitleText != null) resultTitleText.text = "조회 결과 없음";
                if (resultBodyText != null) resultBodyText.text = $"'{searchInputField.text}'에 일치하는 시민·차량·법인 기록이 없습니다.";
                if (addResultToBoardButton != null) addResultToBoardButton.interactable = false;
                return;
            }

            DisplayEntry(matched, recordInHistory: true);
        }

        private void DisplayEntry(PoliceDbEntry matched, bool recordInHistory)
        {
            var state = AppRoot.Instance?.RuntimeState;
            if (matched == null || state == null) return;

            _lastEntry = matched;
            if (resultTitleText != null) resultTitleText.text = matched.displayName;
            if (resultBodyText != null) resultBodyText.text = matched.recordText;
            if (addResultToBoardButton != null) addResultToBoardButton.interactable = true;

            if (!string.IsNullOrEmpty(matched.unlocksNodeId))
                state.UnlockNode(matched.unlocksNodeId);

            if (!string.IsNullOrEmpty(matched.revealsSuspectId))
                state.DiscoverSuspect(matched.revealsSuspectId, matched.displayName);

            if (recordInHistory)
            {
                state.RecordDbQuery(matched.displayName);
                RefreshHistorySidebar();
            }
        }

        private void RefreshHistorySidebar()
        {
            foreach (var r in _historyRows)
                if (r != null) Destroy(r);
            _historyRows.Clear();

            var state = AppRoot.Instance?.RuntimeState;
            if (state?.Episode == null || historyListContainer == null || historyItemPrefab == null) return;

            foreach (var displayName in state.QueriedDbDisplayNames)
            {
                var entry = state.Episode.dbEntries.Find(e => e.displayName == displayName);
                if (entry == null) continue;

                var captured = entry;
                var btn = Instantiate(historyItemPrefab, historyListContainer);
                var lbl = btn.GetComponentInChildren<TMP_Text>();
                if (lbl != null) lbl.text = captured.displayName;
                btn.onClick.AddListener(() =>
                {
                    GameEvents.RaiseSfxRequested("SFX_MOUSE_CLICK");
                    DisplayEntry(captured, recordInHistory: false);
                });
                _historyRows.Add(btn.gameObject);
            }
        }

        private void AddLastResultToBoard()
        {
            if (_lastEntry == null || AppRoot.Instance == null) return;

            var card = UserClueCard.Create(
                sourceId: "KPOL_DB",
                sourceType: ClueSourceType.DbRecord,
                displayTitle: $"[K-POL 조회] {_lastEntry.displayName}",
                hiddenEvidenceId: "NONE",
                extractedText: _lastEntry.recordText);

            AppRoot.Instance.RuntimeState.AddClueCard(card);
        }

        private static string NormalizeKeyword(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            return Regex.Replace(raw.Trim(), @"[\s\-]+", "").ToUpperInvariant();
        }
    }
}
