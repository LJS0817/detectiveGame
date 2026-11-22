using ColdCaseOS.Data;

namespace ColdCaseOS.Core
{
    /// <summary>
    /// MonoBehaviour 싱글톤이 아닌 순수 C# 캠페인 컨트롤러.
    /// AppRoot가 단일 소유하며 신뢰도(Lv 0~5), 휘발성 단서 현황, 세이브 갱신을 담당합니다.
    /// </summary>
    public class CampaignController
    {
        public CampaignSaveData SaveData { get; private set; }

        public CampaignController()
        {
            SaveData = SaveManager.LoadOrCreate();
        }

        public void ResetNewCampaign()
        {
            SaveManager.DeleteSave();
            SaveData = new CampaignSaveData();
            SaveManager.Save(SaveData);
        }

        public void ReloadFromDisk()
        {
            SaveData = SaveManager.LoadOrCreate();
        }

        public bool HasActiveSnapshot(string episodeId)
        {
            return SaveData.activeEpisodeSnapshot != null &&
                   SaveData.activeEpisodeSnapshot.episodeId == episodeId;
        }

        public void AutoSaveActiveState(EpisodeRuntimeState state)
        {
            if (state == null || state.Episode == null) return;
            SaveData.activeEpisodeSnapshot = state.ExportSnapshot();
            SaveManager.Save(SaveData);
        }

        public void RecordEpisodeClear(
            string episodeId,
            int elapsedMinutes,
            bool clearedWithinDeadline,
            VolatileClueState volatileState,
            string achievedEndingType = "")
        {
            if (!SaveData.episodeRecords.TryGetValue(episodeId, out var record))
            {
                record = new EpisodeProgressRecord { episodeId = episodeId };
                SaveData.episodeRecords[episodeId] = record;
            }

            if (!record.isCleared)
            {
                SaveData.AdjustTrust(clearedWithinDeadline ? +1 : -1);
            }

            record.isCleared = true;
            if (elapsedMinutes < record.bestElapsedMinutes)
                record.bestElapsedMinutes = elapsedMinutes;
            if (clearedWithinDeadline)
                record.clearedWithinDeadline = true;

            if (volatileState > record.volatileClueState)
                record.volatileClueState = volatileState;

            if (!string.IsNullOrEmpty(achievedEndingType))
                record.achievedEndingType = achievedEndingType;

            SaveData.activeEpisodeSnapshot = null;
            SaveManager.Save(SaveData);
        }
    }
}
