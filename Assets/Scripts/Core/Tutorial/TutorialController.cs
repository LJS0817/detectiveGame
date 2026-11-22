using ColdCaseOS.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColdCaseOS.Core
{
    /// <summary>
    /// EP01 최초 진입 시 비침습적 4단계 수사 가이드 배너를 제공하는 튜토리얼 컨트롤러.
    /// 1단계: 문서 드래그 단서 등록 → 2단계: 사진/CCTV 2.0x 확대 캡처 → 3단계: 현장 지도 지시 → 4단계: 송치 보고서 제출.
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
            GameEvents.OnEpisodeCleared += HandleEpisodeCleared;
        }

        private void OnDisable()
        {
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded -= HandleEpisodeLoaded;

            GameEvents.OnClueCardAdded -= HandleClueCardAdded;
            GameEvents.OnActionCompleted -= HandleActionCompleted;
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

        private void HandleClueCardAdded(UserClueCard card)
        {
            if (_currentStep == 1 && card.sourceType == ClueSourceType.TextExcerpt)
            {
                SetStep(2);
            }
            else if (_currentStep == 2 && (card.sourceType == ClueSourceType.PhotoCapture || card.sourceType == ClueSourceType.CctvCapture))
            {
                SetStep(3);
            }
        }

        private void HandleActionCompleted(string _, string __)
        {
            if (_currentStep == 3)
            {
                SetStep(4);
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
                    SetText("수사 가이드 (1/4) — 문서 단서 발췌",
                        "파일 탐색기에서 초동 수사 보고서나 진술서를 열고, 의심되는 문장을 드래그해 증거판에 등록하세요.");
                    break;
                case 2:
                    SetText("수사 가이드 (2/4) — 사진·CCTV 확대 캡처",
                        "현장 사진 또는 CCTV를 열고 마우스 휠로 2.0배 이상 확대한 뒤 [캡처] 버튼으로 증거판에 추가하세요.");
                    break;
                case 3:
                    SetText("수사 가이드 (3/4) — 파트너 현장 지시",
                        "전술 지도를 열어 강민호 경사에게 현장 조사나 감식을 지시하세요. 현장 지시 시에만 시간이 흐릅니다.");
                    break;
                case 4:
                    SetText("수사 가이드 (4/4) — 검찰 송치 보고서 작성",
                        "확보한 단서와 발견된 용의자를 바탕으로 송치 보고서를 작성해 제출하세요. 반려되어도 수정 제출할 수 있습니다.");
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
