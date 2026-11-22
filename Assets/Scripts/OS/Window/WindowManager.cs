using System.Collections.Generic;
using UnityEngine;

namespace ColdCaseOS.OS
{
    /// <summary>
    /// 싱글톤이 아닌 AppRoot 소속 OS 윈도우 레지스트리 및 Z-Order 포커스 매니저.
    /// </summary>
    public class WindowManager : MonoBehaviour
    {
        [SerializeField] private RectTransform desktopBounds;
        [SerializeField] private List<OSWindow> registeredWindows = new List<OSWindow>();

        private readonly Dictionary<string, OSWindow> _windowMap = new Dictionary<string, OSWindow>();
        public OSWindow FocusedWindow { get; private set; }

        private void Awake()
        {
            foreach (var win in registeredWindows)
            {
                RegisterWindow(win);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && FocusedWindow != null && FocusedWindow.gameObject.activeSelf)
            {
                FocusedWindow.Close();
                FocusedWindow = null;
            }
        }

        public void RegisterWindow(OSWindow window)
        {
            if (window == null || string.IsNullOrEmpty(window.WindowId)) return;
            window.BindManager(this, desktopBounds);
            _windowMap[window.WindowId] = window;
        }

        public OSWindow OpenWindow(string windowId)
        {
            if (!_windowMap.TryGetValue(windowId, out var win))
            {
                Debug.LogWarning($"[WindowManager] 등록되지 않은 windowId: {windowId}");
                return null;
            }

            win.Open();
            return win;
        }

        public T GetWindow<T>() where T : OSWindow
        {
            foreach (var win in _windowMap.Values)
            {
                if (win is T typed) return typed;
            }
            return null;
        }

        public T OpenWindow<T>() where T : OSWindow
        {
            var win = GetWindow<T>();
            if (win != null) win.Open();
            return win;
        }

        public void ToggleWindow(string windowId)
        {
            if (!_windowMap.TryGetValue(windowId, out var win)) return;

            if (win.State == WindowDisplayState.Closed || win.State == WindowDisplayState.Minimized)
                win.Open();
            else if (FocusedWindow == win)
                win.Minimize();
            else
                BringToFront(win);
        }

        public void BringToFront(OSWindow window)
        {
            if (window == null) return;
            if (FocusedWindow != null && FocusedWindow != window)
                FocusedWindow.SetFocusVisual(false);

            window.transform.SetAsLastSibling();
            FocusedWindow = window;
            window.SetFocusVisual(true);
        }

        public void CloseAllWindowsImmediate()
        {
            foreach (var win in _windowMap.Values)
            {
                win.CloseImmediate();
            }
            FocusedWindow = null;
        }

        /// <summary>
        /// OS 바탕화면에서 상부 지시로 다음 에피소드를 이어받을 때,
        /// 메신저/메일 등 지정된 창은 유지하고 이전 사건의 조사 창들만 깔끔하게 닫습니다.
        /// </summary>
        public void CloseAllWindowsExcept(params string[] keepOpenWindowIds)
        {
            var keepSet = new HashSet<string>(keepOpenWindowIds ?? System.Array.Empty<string>());
            foreach (var kv in _windowMap)
            {
                if (keepSet.Contains(kv.Key)) continue;
                kv.Value.CloseImmediate();
                if (FocusedWindow == kv.Value) FocusedWindow = null;
            }
        }
    }
}
