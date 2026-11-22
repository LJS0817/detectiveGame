using ColdCaseOS.Core;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace ColdCaseOS.UI
{
    /// <summary>
    /// 단일 씬(MainScene)의 타이틀 화면 패널 컨트롤러.
    /// </summary>
    public class TitleScreenController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button continueButton;
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button caseCabinetButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Modals")]
        [SerializeField] private SettingsModal settingsModal;

        private void Awake()
        {
            if (continueButton != null) continueButton.onClick.AddListener(() => OnContinueClicked().Forget());
            if (newGameButton != null) newGameButton.onClick.AddListener(() => OnNewGameClicked().Forget());
            if (caseCabinetButton != null) caseCabinetButton.onClick.AddListener(() => OnCabinetClicked().Forget());
            if (settingsButton != null) settingsButton.onClick.AddListener(() => settingsModal?.Open());
            if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);
        }

        private void OnEnable()
        {
            RefreshButtons();
        }

        public void RefreshButtons()
        {
            var save = AppRoot.Instance?.Campaign?.SaveData;
            bool canContinue = save != null && (save.activeEpisodeSnapshot != null || save.episodeRecords.Count > 0);
            if (continueButton != null) continueButton.interactable = canContinue;
        }

        private async UniTaskVoid OnContinueClicked()
        {
            var app = AppRoot.Instance;
            if (app == null) return;

            var snap = app.Campaign.SaveData.activeEpisodeSnapshot;
            if (snap != null && !string.IsNullOrEmpty(snap.episodeId))
            {
                await app.LoadAndStartEpisodeAsync(snap.episodeId, resumeSnapshot: true);
                return;
            }

            await app.SwitchScreenAsync(AppScreenState.CaseCabinet);
        }

        private async UniTaskVoid OnNewGameClicked()
        {
            var app = AppRoot.Instance;
            if (app == null) return;

            app.Campaign.ResetNewCampaign();
            await app.LoadAndStartEpisodeAsync("EP01", resumeSnapshot: false);
        }

        private async UniTaskVoid OnCabinetClicked()
        {
            var app = AppRoot.Instance;
            if (app == null) return;
            await app.SwitchScreenAsync(AppScreenState.CaseCabinet);
        }

        private static void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
