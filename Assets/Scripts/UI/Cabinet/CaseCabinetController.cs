using System;
using System.Collections.Generic;
using ColdCaseOS.Core;
using ColdCaseOS.Data;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColdCaseOS.UI
{
    /// <summary>
    /// 단일 씬(MainScene)의 사건 캐비닛(EP01~EP07 선택 및 진행도 확인) 화면 컨트롤러.
    /// </summary>
    public class CaseCabinetController : MonoBehaviour
    {
        [Serializable]
        public class EpisodeCardSlot
        {
            public string episodeId;
            public string displayTitle;
            public Button selectButton;
            public TMP_Text statusText;
            public TMP_Text volatileBadgeText;
        }

        [Header("Cabinet UI")]
        [SerializeField] private TMP_Text trustLevelText;
        [SerializeField] private TMP_Text volatileSummaryText;
        [SerializeField] private Button backToTitleButton;
        [SerializeField] private List<EpisodeCardSlot> episodeSlots = new List<EpisodeCardSlot>();

        private static readonly string[] EpisodeSequence =
        {
            "EP01", "EP02", "EP03", "EP04", "EP05", "EP06", "EP07"
        };

        private void Awake()
        {
            if (backToTitleButton != null)
                backToTitleButton.onClick.AddListener(() => AppRoot.Instance?.SwitchScreenAsync(AppScreenState.Title).Forget());

            foreach (var slot in episodeSlots)
            {
                if (slot?.selectButton == null) continue;
                string epId = slot.episodeId;
                slot.selectButton.onClick.AddListener(() => OnEpisodeSelectedAsync(epId).Forget());
            }
        }

        private void OnEnable()
        {
            RefreshCabinet();
        }

        public void RefreshCabinet()
        {
            var save = AppRoot.Instance?.Campaign?.SaveData;
            if (save == null) return;

            if (trustLevelText != null)
                trustLevelText.text = $"전담팀 신뢰도: Lv.{save.trustLevel} / {CampaignSaveData.MaxTrustLevel}";

            if (volatileSummaryText != null)
                volatileSummaryText.text = $"휘발성 단서 원본 확보: {save.CountCompleteVolatileClues()} / 6";

            foreach (var slot in episodeSlots)
            {
                if (slot == null) continue;
                bool unlocked = IsEpisodeUnlocked(save, slot.episodeId);
                save.episodeRecords.TryGetValue(slot.episodeId, out var rec);

                if (slot.selectButton != null)
                    slot.selectButton.interactable = unlocked;

                if (slot.statusText != null)
                {
                    if (!unlocked)
                        slot.statusText.text = "미배정 (이전 사건 해결 필요)";
                    else if (rec != null && rec.isCleared)
                        slot.statusText.text = $"해결 완료 (최단 {TimeManager.FormatMinutesKorean(rec.bestElapsedMinutes)})";
                    else if (save.activeEpisodeSnapshot != null && save.activeEpisodeSnapshot.episodeId == slot.episodeId)
                        slot.statusText.text = "수사 진행 중";
                    else
                        slot.statusText.text = "수사 착수 가능";
                }

                if (slot.volatileBadgeText != null)
                {
                    slot.volatileBadgeText.text = (rec != null ? rec.volatileClueState : VolatileClueState.None) switch
                    {
                        VolatileClueState.Complete => "휘발성 원본",
                        VolatileClueState.Damaged => "일부 훼손",
                        _ => "-"
                    };
                }
            }
        }

        private static bool IsEpisodeUnlocked(CampaignSaveData save, string episodeId)
        {
            int idx = Array.IndexOf(EpisodeSequence, episodeId?.ToUpperInvariant());
            if (idx <= 0) return true; // EP01은 항상 개방
            string prevId = EpisodeSequence[idx - 1];
            return save.episodeRecords.TryGetValue(prevId, out var prevRec) && prevRec.isCleared;
        }

        private async UniTaskVoid OnEpisodeSelectedAsync(string episodeId)
        {
            var app = AppRoot.Instance;
            if (app == null || string.IsNullOrEmpty(episodeId)) return;

            bool hasSnap = app.Campaign.HasActiveSnapshot(episodeId);
            await app.LoadAndStartEpisodeAsync(episodeId, resumeSnapshot: hasSnap);
        }
    }
}
