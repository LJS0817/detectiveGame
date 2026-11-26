using ColdCaseOS.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColdCaseOS.Core
{
    /// <summary>
    /// EP01 최초 진입 시 비침습적 3단계 수사 가이드 배너를 제공하는 튜토리얼 컨트롤러.
    /// 1단계: 문서 드래그 단서 등록 → 2단계: 교차 분석 및 단서 수집 → 3단계: 최종 보고서 4슬롯 제출.
    /// </summary>
    public class TutorialController : MonoBehaviour
    {
        private const string PrefTutorialDone = "KPOL_EP01_TUTORIAL_DONE";

        [Header("Guide Banner UI")]
        [SerializeField] private GameObject guidePanel;
        [SerializeField] private TMP_Text stepTitleText;
        [SerializeField] private TMP_Text stepDescriptionText;
        [SerializeField] private Button skipButton;

        private int _currentStep = -1;

        private void Awake()
        {
            if (skipButton != null)
                skipButton.onClick.AddListener(CompleteTutorial);
            if (guidePanel != null)
                guidePanel.SetActive(false);
        }

        private void OnEnable()
        {
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded += HandleEpisodeLoaded;

            GameEvents.OnClueCardAdded += HandleClueCardAdded;
            GameEvents.OnEpisodeCleared += HandleEpisodeCleared;
        }

        private void OnDisable()
        {
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded -= HandleEpisodeLoaded;

            GameEvents.OnClueCardAdded -= HandleClueCardAdded;
            GameEvents.OnEpisodeCleared -= HandleEpisodeCleared;
        }

        public static void ResetTutorialProgress()
        {
            PlayerPrefs.DeleteKey(PrefTutorialDone);
        }

        private void HandleEpisodeLoaded(EpisodeData episode)
        {
            bool isEp01 = episode != null && episode.episodeId == "EP01";
            bool alreadyDone = PlayerPrefs.GetInt(PrefTutorialDone, 0) == 1;

            if (isEp01 && !alreadyDone)
            {
                SetStep(1);
            }
            else
            {
                _currentStep = -1;
                if (guidePanel != null) guidePanel.SetActive(false);
            }
        }

        private void HandleClueCardAdded(UserClueCard _)
        {
            var count = AppRoot.Instance?.RuntimeState?.ClueCards?.Count ?? 0;
            if (_currentStep == 1 && count >= 1)
            {
                SetStep(2);
            }
            else if (_currentStep == 2 && count >= 4)
            {
                SetStep(3);
            }
        }

        private void HandleEpisodeCleared(ArrestReportTier _, int __, VolatileClueState ___)
        {
            if (_currentStep > 0) CompleteTutorial();
        }

        private void SetStep(int step)
        {
            _currentStep = step;
            if (guidePanel != null) guidePanel.SetActive(true);

            switch (step)
            {
                case 1:
                    SetText("수사 가이드 (1/3) — 문서 단서 발췌",
                        "파일 탐색기에서 보고서·진술서·이동 시간표·도면을 열고, 핵심 문장을 드래그해 증거판에 등록하세요.");
                    break;
                case 2:
                    SetText("수사 가이드 (2/3) — 교차 분석 및 노이즈 필터링",
                        "경찰 DB 조회 결과와 여러 문서의 시간대·물증을 대조해 범인·동기·흉기·수법 단서를 증거판에 모으세요.");
                    break;
                case 3:
                    SetText("수사 가이드 (3/3) — 최종 보고서 제출",
                        "최종 보고서를 열어 증거판에 등록한 카드 중에서 [범인 / 범행 동기 / 사용 흉기 / 침입·은폐 수법]을 선택해 제출하세요.");
                    break;
            }
        }

        private void SetText(string title, string desc)
        {
            if (stepTitleText != null) stepTitleText.text = title;
            if (stepDescriptionText != null) stepDescriptionText.text = desc;
        }

        public void CompleteTutorial()
        {
            _currentStep = -1;
            PlayerPrefs.SetInt(PrefTutorialDone, 1);
            if (guidePanel != null) guidePanel.SetActive(false);
        }
    }
}
