using System;
using ColdCaseOS.Data;

namespace ColdCaseOS.Core
{
    /// <summary>
    /// 7개 OS 앱과 코어 시스템 간의 직접 참조(결합도)를 제거하는 정적 이벤트 버스.
    /// 씬 전환 시 메모리 누수를 막기 위해 OnDestroy에서 반드시 구독 해제하거나 ClearEpisodeEvents()를 호출합니다.
    /// </summary>
    public static class GameEvents
    {
        // 시간 및 시한 이벤트
        public static event Action<int, int> OnTimeAdvanced;          // (deltaMinutes, totalElapsedMinutes)
        public static event Action OnVolatileDeadlinePassed;
        public static event Action OnOvertimeEntered;

        // 파일 및 단서 이벤트
        public static event Action<string> OnItemUnlocked;            // docId / photoId / cctvId / audioId
        public static event Action<string> OnItemMarkedRead;
        public static event Action<string> OnSuspectDiscovered;       // suspectId / targetId
        public static event Action<UserClueCard> OnClueCardAdded;
        public static event Action<UserClueCard> OnClueCardUpdated;   // 메모, 기각, 폐기/복구, 선 연결 변경

        // 지도 및 현장 지시 이벤트
        public static event Action<string> OnNodeUnlocked;            // nodeId
        public static event Action<string> OnPartnerMoved;            // newNodeId
        public static event Action<string, string> OnActionCompleted; // (actionId, targetId or "")

        // 통신, 알림, 사운드 이벤트
        public static event Action<MessengerMessageData> OnMessengerMessageAdded;
        public static event Action<string, string> OnToastRequested;  // (title, message)
        public static event Action<string> OnSfxRequested;            // SFX key (예: "SFX_HIGHLIGHTER")

        // 3버튼 툴바 연계 & LLM 현장수사반 / 전원 심문 이벤트
        public static event Action<string> OnDbQuickSearchRequested;                 // query
        public static event Action<string> OnFieldCommandQuoteRequested;             // quoted text
        public static event Action<string, string> OnInterrogationLayerUnlocked;     // (suspectId, layerId)
        public static event Action<string> OnInterrogationSessionChanged;            // suspectId

        // 보고서 및 정산 / 다음 사건 배정 이벤트
        public static event Action<ArrestReportTier, int, VolatileClueState> OnEpisodeCleared;
        public static event Action<string, string, string> OnNewCaseOrderReceived; // (nextEpisodeId, caseNumber, summary)
        public static event Action<string> OnCaseFilesDownloaded;                  // episodeId

        public static void RaiseTimeAdvanced(int delta, int total) => OnTimeAdvanced?.Invoke(delta, total);
        public static void RaiseVolatileDeadlinePassed() => OnVolatileDeadlinePassed?.Invoke();
        public static void RaiseOvertimeEntered() => OnOvertimeEntered?.Invoke();

        public static void RaiseItemUnlocked(string itemId) => OnItemUnlocked?.Invoke(itemId);
        public static void RaiseItemMarkedRead(string itemId) => OnItemMarkedRead?.Invoke(itemId);
        public static void RaiseSuspectDiscovered(string suspectId) => OnSuspectDiscovered?.Invoke(suspectId);
        public static void RaiseClueCardAdded(UserClueCard card) => OnClueCardAdded?.Invoke(card);
        public static void RaiseClueCardUpdated(UserClueCard card) => OnClueCardUpdated?.Invoke(card);

        public static void RaiseNodeUnlocked(string nodeId) => OnNodeUnlocked?.Invoke(nodeId);
        public static void RaisePartnerMoved(string nodeId) => OnPartnerMoved?.Invoke(nodeId);
        public static void RaiseActionCompleted(string actionId, string targetId = "") => OnActionCompleted?.Invoke(actionId, targetId);

        public static void RaiseMessengerMessageAdded(MessengerMessageData msg) => OnMessengerMessageAdded?.Invoke(msg);
        public static void RaiseToastRequested(string title, string message) => OnToastRequested?.Invoke(title, message);
        public static void RaiseSfxRequested(string sfxKey) => OnSfxRequested?.Invoke(sfxKey);

        public static void RaiseEpisodeCleared(ArrestReportTier report, int elapsedMinutes, VolatileClueState volatileState)
            => OnEpisodeCleared?.Invoke(report, elapsedMinutes, volatileState);

        public static void RaiseNewCaseOrderReceived(string nextEpisodeId, string caseNumber, string summary)
            => OnNewCaseOrderReceived?.Invoke(nextEpisodeId, caseNumber, summary);

        public static void RaiseCaseFilesDownloaded(string episodeId)
            => OnCaseFilesDownloaded?.Invoke(episodeId);

        public static void RaiseDbQuickSearchRequested(string query)
            => OnDbQuickSearchRequested?.Invoke(query);

        public static void RaiseFieldCommandQuoteRequested(string quote)
            => OnFieldCommandQuoteRequested?.Invoke(quote);

        public static void RaiseInterrogationLayerUnlocked(string suspectId, string layerId)
            => OnInterrogationLayerUnlocked?.Invoke(suspectId, layerId);

        public static void RaiseInterrogationSessionChanged(string suspectId)
            => OnInterrogationSessionChanged?.Invoke(suspectId);

        public static void ClearAll()
        {
            OnTimeAdvanced = null;
            OnVolatileDeadlinePassed = null;
            OnOvertimeEntered = null;
            OnItemUnlocked = null;
            OnItemMarkedRead = null;
            OnSuspectDiscovered = null;
            OnClueCardAdded = null;
            OnClueCardUpdated = null;
            OnNodeUnlocked = null;
            OnPartnerMoved = null;
            OnActionCompleted = null;
            OnMessengerMessageAdded = null;
            OnToastRequested = null;
            OnSfxRequested = null;
            OnEpisodeCleared = null;
            OnNewCaseOrderReceived = null;
            OnCaseFilesDownloaded = null;
            OnDbQuickSearchRequested = null;
            OnFieldCommandQuoteRequested = null;
            OnInterrogationLayerUnlocked = null;
            OnInterrogationSessionChanged = null;
        }
    }
}
