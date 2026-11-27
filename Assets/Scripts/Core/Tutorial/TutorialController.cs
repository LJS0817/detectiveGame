using ColdCaseOS.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColdCaseOS.Core
{
    /// <summary>
    /// EP01 최초 진입 시 비침습적 4단계 수사 가이드 배너를 제공하는 튜토리얼 컨트롤러.
    /// 1단계: 문서 드래그(3버튼 툴바) → 2단계: 현장수사반 자연어 지시 & 확정 → 3단계: 관련자 심문 & 증거 제시 → 4단계: 최종 보고서 제출.
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
            GameEvents.OnActionCompleted += HandleActionCompleted;
            GameEvents.OnInterrogationSessionChanged += HandleInterrogationChanged;
            GameEvents.OnEpisodeCleared += HandleEpisodeCleared;
        }

        private void OnDisable()
        {
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded -= HandleEpisodeLoaded;

            GameEvents.OnClueCardAdded -= HandleClueCardAdded;
            GameEvents.OnActionCompleted -= HandleActionCompleted;
            GameEvents.OnInterrogationSessionChanged -= HandleInterrogationChanged;
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
                SetStep(1);
            else
            {
                _currentStep = -1;
                if (guidePanel != null) guidePanel.SetActive(false);
            }
        }

        private void HandleClueCardAdded(UserClueCard _)
        {
            if (_currentStep == 1)
                SetStep(2);
        }

        private void HandleActionCompleted(string _, string __)
        {
            if (_currentStep == 2)
                SetStep(3);
        }

        private void HandleInterrogationChanged(string _)
        {
            if (_currentStep == 3)
                SetStep(4);
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
                    SetText("수사 가이드 (1/4) — 초동 문서 분석 및 단서 발췌",
                        "파일 탐색기에서 초동 보고서·의료 소견서·CCTV를 열고, 핵심 문장을 드래그해 [보드에 추가 / DB 조회 / 현장반 지시]를 실행하세요.");
                    break;
                case 2:
                    SetText("수사 가이드 (2/4) — 현장수사반 자연어 지휘",
                        "[현장수사반] 창에서 강민호 경사에게 뒷문 재감식, NVR 복구, 관련자 알리바이 조회 등을 지시하고 [지시 확정]을 눌러 추가 자료를 확보하세요.");
                    break;
                case 3:
                    SetText("수사 가이드 (3/4) — 사건 관련자 전원 심문",
                        "[용의자 심문] 창에서 관련자 7인 중 대상을 소환(-45분/8턴)하고, 확보한 증거판 카드를 첨부해 거짓말을 무너뜨리세요.");
                    break;
                case 4:
                    SetText("수사 가이드 (4/4) — 최종 송치 보고서 제출",
                        "충분한 물증과 진술을 증거판에 모았다면 [최종 보고서]에서 [범인 / 범행 동기 / 사용 흉기 / 침입·은폐 수법] 4슬롯을 선택해 제출하세요.");
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
