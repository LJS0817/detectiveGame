using ColdCaseOS.Core;
using ColdCaseOS.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColdCaseOS.OS
{
    /// <summary>
    /// K-POL OS 하단 작업표시줄 컨트롤러 (파일 탐색기, 증거판, 경찰 DB, 최종 보고서 + 시각 표시).
    /// </summary>
    public class TaskbarController : MonoBehaviour
    {
        [Header("HUD Texts")]
        [SerializeField] private TMP_Text clockText;

        [Header("App Toggle Buttons")]
        [SerializeField] private Button fileExplorerButton;
        [SerializeField] private Button evidenceBoardButton;
        [SerializeField] private Button policeDbButton;
        [SerializeField] private Button reportButton;

        [Header("Window IDs")]
        [SerializeField] private string fileExplorerWindowId = "FileExplorer";
        [SerializeField] private string evidenceBoardWindowId = "EvidenceBoard";
        [SerializeField] private string policeDbWindowId = "PoliceDB";
        [SerializeField] private string reportWindowId = "Report";

        private void Awake()
        {
            BindToggle(fileExplorerButton, fileExplorerWindowId);
            BindToggle(evidenceBoardButton, evidenceBoardWindowId);
            BindToggle(policeDbButton, policeDbWindowId);
            BindToggle(reportButton, reportWindowId);
        }

        private void OnEnable()
        {
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded += HandleEpisodeLoaded;

            RefreshHud();
        }

        private void OnDisable()
        {
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded -= HandleEpisodeLoaded;
        }

        private void BindToggle(Button btn, string windowId)
        {
            if (btn == null) return;
            btn.onClick.AddListener(() => AppRoot.Instance?.Windows?.ToggleWindow(windowId));
        }

        private void HandleEpisodeLoaded(EpisodeData _) => RefreshHud();

        public void RefreshHud()
        {
            var app = AppRoot.Instance;
            var state = app?.RuntimeState;
            if (app == null || state == null || state.Episode == null) return;

            if (clockText != null)
                clockText.text = app.Time.FormattedCurrentFull;
        }
    }
}
