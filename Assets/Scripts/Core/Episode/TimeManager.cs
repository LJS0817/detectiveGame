using System;
using ColdCaseOS.Data;
using UnityEngine;

namespace ColdCaseOS.Core
{
    public struct ActionTimeBreakdown
    {
        public int travelMinutes;
        public int workMinutes;
        public int reportMinutes;
        public int TotalMinutes => travelMinutes + workMinutes + reportMinutes;
    }

    /// <summary>
    /// 3중 시간 규칙(연장 수사 허용, 휘발성 골든 타임, 전담팀 신뢰도 보정)을 담당하는 결정론적 시간 매니저.
    /// 책상/OS 분석 중에는 시간이 흐르지 않으며, 오직 현장 지시 '시간 보내기' 실행 시에만 차감됩니다.
    /// </summary>
    public class TimeManager
    {
        private readonly EpisodeRuntimeState _state;
        private readonly Func<int> _getTrustLevel;

        public TimeManager(EpisodeRuntimeState state, Func<int> getTrustLevel)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _getTrustLevel = getTrustLevel ?? (() => CampaignSaveData.DefaultTrustLevel);
        }

        public DateTime CurrentDateTime => _state.Episode.GetStartDateTime().AddMinutes(_state.ElapsedMinutes);
        public string FormattedCurrentClock => CurrentDateTime.ToString("HH:mm");
        public string FormattedCurrentFull => CurrentDateTime.ToString("yyyy.MM.dd HH:mm");

        public int RemainingSubmissionMinutes => _state.Episode.submissionDeadlineMinutes - _state.ElapsedMinutes;
        public int RemainingVolatileMinutes => _state.Episode.volatileDeadlineMinutes - _state.ElapsedMinutes;
        public bool IsWithinVolatileWindow => _state.ElapsedMinutes <= _state.Episode.volatileDeadlineMinutes;

        /// <summary>
        /// 03_SYSTEM_UX_SPEC.md 3.1절 신뢰도(Lv 0~5) 단계별 작업 시간 보정표.
        /// </summary>
        public int GetTrustWorkModifier(ActionCategory category)
        {
            int lv = _getTrustLevel();
            if (category == ActionCategory.EXTERNAL_AGENCY)
            {
                return lv switch
                {
                    0 => +20,
                    1 => +10,
                    2 => 0,
                    3 => -15,
                    4 => -20,
                    5 => -25,
                    _ => 0
                };
            }

            if (category == ActionCategory.FORENSIC)
            {
                return lv switch
                {
                    4 => -15,
                    5 => -20,
                    _ => 0
                };
            }

            return 0;
        }

        public int GetTrustReportModifier()
        {
            return _getTrustLevel() >= 5 ? -5 : 0;
        }

        public ActionTimeBreakdown CalculateBreakdown(int travelMinutes, FieldActionData action)
        {
            int work = Mathf.Max(15, action.baseWorkMinutes + GetTrustWorkModifier(action.GetCategoryEnum()));
            int report = Mathf.Max(5, action.reportMinutes + GetTrustReportModifier());
            return new ActionTimeBreakdown
            {
                travelMinutes = Mathf.Max(0, travelMinutes),
                workMinutes = work,
                reportMinutes = report
            };
        }

        public void AdvanceMinutes(int deltaMinutes)
        {
            if (deltaMinutes <= 0) return;

            bool wasOverVolatile = _state.VolatileDeadlineNotified;
            bool wasOvertime = _state.IsOvertime;

            _state.ElapsedMinutes += deltaMinutes;
            GameEvents.RaiseSfxRequested("SFX_CLOCK_FASTFORWARD");
            GameEvents.RaiseTimeAdvanced(deltaMinutes, _state.ElapsedMinutes);

            if (!wasOverVolatile && _state.ElapsedMinutes > _state.Episode.volatileDeadlineMinutes)
            {
                _state.VolatileDeadlineNotified = true;
                GameEvents.RaiseVolatileDeadlinePassed();
                _state.AddMessengerMessage(
                    "강민호 경사",
                    "경위님, 휘발성 골든 타임이 지났습니다. 디지털 로그 일부가 덮어쓰기/삭제됐을 가능성이 높습니다.",
                    FormattedCurrentClock,
                    isSystemAlert: true);
            }

            if (!wasOvertime && _state.ElapsedMinutes > _state.Episode.submissionDeadlineMinutes)
            {
                _state.IsOvertime = true;
                GameEvents.RaiseOvertimeEntered();
                _state.AddMessengerMessage(
                    "강민호 경사",
                    "마감 시한을 넘겨 일단 송치 보류 및 연장 수사 사유서를 올렸습니다. 위에서 좀 깨지겠지만... 진범 잡을 때까지 계속 갑시다.",
                    FormattedCurrentClock,
                    isSystemAlert: true);
            }
        }

        public static string FormatMinutesKorean(int minutes)
        {
            int abs = Mathf.Abs(minutes);
            int h = abs / 60;
            int m = abs % 60;
            if (h > 0 && m > 0) return $"{h}시간 {m}분";
            if (h > 0) return $"{h}시간";
            return $"{m}분";
        }
    }
}
