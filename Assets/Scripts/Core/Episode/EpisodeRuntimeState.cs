using System;
using System.Collections.Generic;
using System.Linq;
using ColdCaseOS.Data;
using UnityEngine;

namespace ColdCaseOS.Core
{
    /// <summary>
    /// 현재 진행 중인 에피소드의 단일 진실 공급원(Single Source of Truth).
    /// 모든 앱은 이 상태를 읽고 메서드를 통해 변경하며, 변경 시 GameEvents를 발행합니다.
    /// </summary>
    public class EpisodeRuntimeState
    {
        public EpisodeData Episode { get; private set; }
        public int ElapsedMinutes { get; set; }
        public bool IsOvertime { get; set; }
        public bool VolatileDeadlineNotified { get; set; }
        public bool IsCaseFilesDownloaded { get; private set; }
        public string CurrentPartnerNodeId { get; set; }
        public VolatileClueState CurrentVolatileClueState { get; set; } = VolatileClueState.None;

        public HashSet<string> UnlockedNodeIds { get; } = new HashSet<string>();
        public HashSet<string> UnlockedDocIds { get; } = new HashSet<string>();
        public HashSet<string> UnlockedPhotoIds { get; } = new HashSet<string>();
        public HashSet<string> UnlockedCctvIds { get; } = new HashSet<string>();
        public HashSet<string> DiscoveredSuspectIds { get; } = new HashSet<string>();
        public List<string> QueriedDbDisplayNames { get; } = new List<string>();
        public HashSet<string> ReadItemIds { get; } = new HashSet<string>();
        public HashSet<string> CompletedActionKeys { get; } = new HashSet<string>();

        public List<UserClueCard> ClueCards { get; } = new List<UserClueCard>();
        public List<StickyNoteData> StickyNotes { get; } = new List<StickyNoteData>();
        public List<MessengerMessageData> MessengerLogs { get; } = new List<MessengerMessageData>();
        public Dictionary<string, InterrogationRuntimeState> InterrogationStates { get; } = new Dictionary<string, InterrogationRuntimeState>();
        public List<FieldCommandMessageData> FieldCommandLogs { get; } = new List<FieldCommandMessageData>();

        public void InitializeNew(EpisodeData episode)
        {
            Episode = episode ?? throw new ArgumentNullException(nameof(episode));
            ElapsedMinutes = 0;
            IsOvertime = false;
            VolatileDeadlineNotified = false;
            IsCaseFilesDownloaded = true;
            CurrentPartnerNodeId = episode.startNodeId;
            CurrentVolatileClueState = VolatileClueState.None;

            UnlockedNodeIds.Clear();
            UnlockedDocIds.Clear();
            UnlockedPhotoIds.Clear();
            UnlockedCctvIds.Clear();
            DiscoveredSuspectIds.Clear();
            QueriedDbDisplayNames.Clear();
            ReadItemIds.Clear();
            CompletedActionKeys.Clear();
            ClueCards.Clear();
            StickyNotes.Clear();
            MessengerLogs.Clear();
            InterrogationStates.Clear();
            FieldCommandLogs.Clear();

            foreach (var d in episode.documents)
                if (d.isInitial) UnlockedDocIds.Add(d.docId);

            foreach (var p in episode.photos)
                if (p.isInitial) UnlockedPhotoIds.Add(p.photoId);

            foreach (var c in episode.cctvClips)
                if (c.isInitial) UnlockedCctvIds.Add(c.clipId);

            foreach (var n in episode.mapNodes)
                if (n.isInitiallyUnlocked) UnlockedNodeIds.Add(n.nodeId);

            foreach (var act in episode.actions)
            {
                if (act.targetOptions == null) continue;
                foreach (var opt in act.targetOptions)
                    if (opt.isInitiallyDiscovered) DiscoveredSuspectIds.Add(opt.targetId);
            }

            if (episode.interrogationProfiles != null)
            {
                foreach (var prof in episode.interrogationProfiles)
                {
                    if (prof == null || string.IsNullOrEmpty(prof.suspectId)) continue;
                    DiscoveredSuspectIds.Add(prof.suspectId);
                    InterrogationStates[prof.suspectId] = CreateInitialInterrogationState(prof);
                }
            }
        }

        private static InterrogationRuntimeState CreateInitialInterrogationState(InterrogationProfileData prof)
        {
            var st = new InterrogationRuntimeState
            {
                suspectId = prof.suspectId,
                composure = prof.initialComposure,
                sessionsUsed = 0,
                turnsRemainingInSession = 0,
                isSessionActive = false,
                lastEmotion = "calm"
            };
            if (prof.layers != null)
            {
                foreach (var layer in prof.layers)
                {
                    if (layer != null && layer.isInitial)
                        st.UnlockLayer(layer.layerId);
                }
            }
            return st;
        }

        public InterrogationRuntimeState GetOrCreateInterrogationState(InterrogationProfileData prof)
        {
            if (prof == null || string.IsNullOrEmpty(prof.suspectId)) return null;
            if (!InterrogationStates.TryGetValue(prof.suspectId, out var st) || st == null)
            {
                st = CreateInitialInterrogationState(prof);
                InterrogationStates[prof.suspectId] = st;
            }
            return st;
        }

        public List<UserClueCard> GetActiveClueCards()
        {
            var list = new List<UserClueCard>();
            foreach (var c in ClueCards)
            {
                if (c != null && !c.isInTrash && !c.isDismissed)
                    list.Add(c);
            }
            return list;
        }

        public bool HasEvidenceIdInActiveCards(string evidenceId)
        {
            if (string.IsNullOrEmpty(evidenceId)) return false;
            foreach (var c in ClueCards)
            {
                if (c != null && !c.isInTrash && !c.isDismissed &&
                    string.Equals(c.hiddenEvidenceId, evidenceId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 메신저 사건 배정 메시지 내 [사건 파일 다운로드] 클릭 시 호출.
        /// 초기 문서·사진·CCTV·오디오를 해금하고 파일 탐색기 좌측 사건 목록에 등록합니다.
        /// </summary>
        public bool DownloadCurrentCaseFiles()
        {
            if (Episode == null || IsCaseFilesDownloaded) return false;
            IsCaseFilesDownloaded = true;

            foreach (var d in Episode.documents)
                if (d.isInitial) UnlockedDocIds.Add(d.docId);

            foreach (var p in Episode.photos)
                if (p.isInitial) UnlockedPhotoIds.Add(p.photoId);

            foreach (var c in Episode.cctvClips)
                if (c.isInitial) UnlockedCctvIds.Add(c.clipId);

            GameEvents.RaiseCaseFilesDownloaded(Episode.episodeId);
            GameEvents.RaiseSfxRequested("SFX_MOUSE_CLICK");
            GameEvents.RaiseToastRequested("사건 파일 다운로드 완료", $"{Episode.title} ({Episode.caseNumber})");
            return true;
        }

        public void RecordDbQuery(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return;
            QueriedDbDisplayNames.Remove(displayName);
            QueriedDbDisplayNames.Insert(0, displayName);
        }

        public void RestoreFromSnapshot(EpisodeData episode, EpisodeRuntimeSnapshot snap)
        {
            InitializeNew(episode);
            if (snap == null || snap.episodeId != episode.episodeId) return;

            ElapsedMinutes = snap.elapsedMinutes;
            IsOvertime = snap.isOvertime;
            VolatileDeadlineNotified = snap.volatileDeadlineNotified;
            IsCaseFilesDownloaded = snap.isCaseFilesDownloaded;
            CurrentPartnerNodeId = string.IsNullOrEmpty(snap.currentPartnerNodeId) ? episode.startNodeId : snap.currentPartnerNodeId;

            UnlockedNodeIds.UnionWith(snap.unlockedNodeIds);
            UnlockedDocIds.UnionWith(snap.unlockedDocIds);
            UnlockedPhotoIds.UnionWith(snap.unlockedPhotoIds);
            UnlockedCctvIds.UnionWith(snap.unlockedCctvIds);
            if (snap.discoveredSuspectIds != null) DiscoveredSuspectIds.UnionWith(snap.discoveredSuspectIds);
            if (snap.queriedDbDisplayNames != null) QueriedDbDisplayNames.AddRange(snap.queriedDbDisplayNames);
            ReadItemIds.UnionWith(snap.readItemIds);
            CompletedActionKeys.UnionWith(snap.completedActionKeys);

            if (snap.clueCards != null) ClueCards.AddRange(snap.clueCards);
            if (snap.stickyNotes != null) StickyNotes.AddRange(snap.stickyNotes);
            if (snap.messengerLogs != null) MessengerLogs.AddRange(snap.messengerLogs);
            if (snap.fieldCommandLogs != null) FieldCommandLogs.AddRange(snap.fieldCommandLogs);
            if (snap.interrogationStates != null)
            {
                foreach (var ist in snap.interrogationStates)
                {
                    if (ist != null && !string.IsNullOrEmpty(ist.suspectId))
                        InterrogationStates[ist.suspectId] = ist;
                }
            }
        }

        public EpisodeRuntimeSnapshot ExportSnapshot()
        {
            return new EpisodeRuntimeSnapshot
            {
                episodeId = Episode.episodeId,
                elapsedMinutes = ElapsedMinutes,
                isOvertime = IsOvertime,
                volatileDeadlineNotified = VolatileDeadlineNotified,
                isCaseFilesDownloaded = IsCaseFilesDownloaded,
                currentPartnerNodeId = CurrentPartnerNodeId,
                unlockedNodeIds = UnlockedNodeIds.ToList(),
                unlockedDocIds = UnlockedDocIds.ToList(),
                unlockedPhotoIds = UnlockedPhotoIds.ToList(),
                unlockedCctvIds = UnlockedCctvIds.ToList(),
                discoveredSuspectIds = DiscoveredSuspectIds.ToList(),
                queriedDbDisplayNames = new List<string>(QueriedDbDisplayNames),
                readItemIds = ReadItemIds.ToList(),
                completedActionKeys = CompletedActionKeys.ToList(),
                clueCards = new List<UserClueCard>(ClueCards),
                stickyNotes = new List<StickyNoteData>(StickyNotes),
                messengerLogs = new List<MessengerMessageData>(MessengerLogs),
                interrogationStates = InterrogationStates.Values.ToList(),
                fieldCommandLogs = new List<FieldCommandMessageData>(FieldCommandLogs)
            };
        }

        public void AddClueCard(UserClueCard card)
        {
            if (card == null) return;
            ClueCards.Add(card);
            GameEvents.RaiseClueCardAdded(card);
            GameEvents.RaiseSfxRequested("SFX_TEXT_DRAG_ADD");
            GameEvents.RaiseToastRequested("증거판에 추가됨", card.displayTitle);
        }

        public UserClueCard GetClueCard(string cardId) => ClueCards.Find(c => c.cardId == cardId);

        public void ToggleDismissCard(string cardId)
        {
            var card = GetClueCard(cardId);
            if (card == null) return;
            card.isDismissed = !card.isDismissed;
            GameEvents.RaiseClueCardUpdated(card);
        }

        public void SetTrashState(string cardId, bool inTrash)
        {
            var card = GetClueCard(cardId);
            if (card == null) return;
            card.isInTrash = inTrash;
            GameEvents.RaiseClueCardUpdated(card);
        }

        public void ToggleWireConnection(string cardIdA, string cardIdB)
        {
            if (string.IsNullOrEmpty(cardIdA) || string.IsNullOrEmpty(cardIdB) || cardIdA == cardIdB) return;
            var a = GetClueCard(cardIdA);
            var b = GetClueCard(cardIdB);
            if (a == null || b == null) return;

            if (a.connectedCardIds.Contains(cardIdB))
            {
                a.connectedCardIds.Remove(cardIdB);
                b.connectedCardIds.Remove(cardIdA);
            }
            else
            {
                a.connectedCardIds.Add(cardIdB);
                b.connectedCardIds.Add(cardIdA);
            }

            GameEvents.RaiseClueCardUpdated(a);
            GameEvents.RaiseClueCardUpdated(b);
        }

        public bool DiscoverSuspect(string suspectId, string displayLabel = "")
        {
            if (string.IsNullOrEmpty(suspectId) || !DiscoveredSuspectIds.Add(suspectId)) return false;

            string resolvedLabel = displayLabel;
            if (string.IsNullOrEmpty(resolvedLabel) && Episode != null)
            {
                var culpritOpt = Episode.answerRule?.culpritOptions?.Find(c => c.id == suspectId);
                if (culpritOpt != null) resolvedLabel = culpritOpt.label;
                else
                {
                    foreach (var act in Episode.actions)
                    {
                        var targetOpt = act.targetOptions?.Find(t => t.targetId == suspectId);
                        if (targetOpt != null)
                        {
                            resolvedLabel = targetOpt.label;
                            break;
                        }
                    }
                }
            }

            GameEvents.RaiseSuspectDiscovered(suspectId);
            if (!string.IsNullOrEmpty(resolvedLabel))
                GameEvents.RaiseToastRequested("관련 인물 등록", resolvedLabel);
            return true;
        }

        public void DiscoverSuspects(IEnumerable<string> suspectIds)
        {
            if (suspectIds == null) return;
            foreach (var sid in suspectIds)
                DiscoverSuspect(sid);
        }

        public bool UnlockNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) || !UnlockedNodeIds.Add(nodeId)) return false;
            var node = Episode.mapNodes.Find(n => n.nodeId == nodeId);
            GameEvents.RaiseNodeUnlocked(nodeId);
            GameEvents.RaiseToastRequested("신규 조사 장소 해금", node != null ? node.name : nodeId);
            return true;
        }

        public void UnlockDocument(string docId)
        {
            if (!string.IsNullOrEmpty(docId) && UnlockedDocIds.Add(docId))
                GameEvents.RaiseItemUnlocked(docId);
        }

        public void UnlockPhoto(string photoId)
        {
            if (!string.IsNullOrEmpty(photoId) && UnlockedPhotoIds.Add(photoId))
                GameEvents.RaiseItemUnlocked(photoId);
        }

        public void UnlockCctv(string clipId)
        {
            if (!string.IsNullOrEmpty(clipId) && UnlockedCctvIds.Add(clipId))
                GameEvents.RaiseItemUnlocked(clipId);
        }

        public void MarkItemRead(string itemId)
        {
            if (!string.IsNullOrEmpty(itemId) && ReadItemIds.Add(itemId))
                GameEvents.RaiseItemMarkedRead(itemId);
        }

        public static string BuildActionKey(string actionId, string targetId = "")
            => string.IsNullOrEmpty(targetId) ? actionId : $"{actionId}::{targetId}";

        public bool IsActionCompleted(string actionId, string targetId = "")
            => CompletedActionKeys.Contains(BuildActionKey(actionId, targetId));

        public void MarkActionCompleted(string actionId, string targetId = "")
        {
            CompletedActionKeys.Add(BuildActionKey(actionId, targetId));
            GameEvents.RaiseActionCompleted(actionId, targetId);
        }

        public void AddMessengerMessage(
            string sender,
            string body,
            string timestamp,
            bool isSystemAlert = false,
            List<string> revealsSuspectIds = null,
            string channelId = "PARTNER",
            string downloadableEpisodeId = "")
        {
            var msg = new MessengerMessageData
            {
                channelId = string.IsNullOrEmpty(channelId) ? "PARTNER" : channelId,
                sender = sender,
                body = body,
                timestamp = timestamp,
                isSystemAlert = isSystemAlert,
                downloadableEpisodeId = downloadableEpisodeId ?? string.Empty,
                revealsSuspectIds = revealsSuspectIds ?? new List<string>()
            };
            MessengerLogs.Add(msg);
            DiscoverSuspects(msg.revealsSuspectIds);
            GameEvents.RaiseMessengerMessageAdded(msg);
            GameEvents.RaiseSfxRequested("SFX_MSG_NOTIFY");
        }
    }
}
