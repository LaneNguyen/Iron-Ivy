using UnityEngine;
using UnityEngine.Playables;

namespace IronIvy.Core
{
    public class GuidePanelManager : BaseManager<GuidePanelManager>
    {
        private const string KEY_PREFIX = "ironivy.guide.shown.";
        private int _pauseStack = 0;

        public bool HasShown(string stepId)
        {
            return PlayerPrefs.GetInt(KEY_PREFIX + stepId, 0) == 1;
        }

        public void MarkShown(string stepId)
        {
            if (string.IsNullOrEmpty(stepId)) return;

            PlayerPrefs.SetInt(KEY_PREFIX + stepId, 1);
            PlayerPrefs.Save();
            Debug.Log("[Guide] MarkShown: " + stepId);
        }

        public void PauseGame()
        {
            _pauseStack++;
            if (_pauseStack == 1)
                Time.timeScale = 0f;
        }

        public void ResumeGame()
        {
            _pauseStack--;
            if (_pauseStack < 0) _pauseStack = 0;

            if (_pauseStack == 0)
                Time.timeScale = 1f;
        }

        // Trong Editor, em có thể chọn ignore prefs để test (guide luôn hiện)
        public bool ShouldIgnorePrefsForTesting(bool ignorePrefsInEditor)
        {
#if UNITY_EDITOR
            if (ignorePrefsInEditor) return true;
#endif
            return false;
        }

        // NEW: show nhưng CHƯA mark. Mark sẽ xảy ra khi CompleteAndClose().
        // ignorePrefsInEditor: nếu true (và đang UNITY_EDITOR) thì show luôn, không check HasShown.
        // disableMarkInEditor: nếu true (và đang UNITY_EDITOR) thì CompleteAndClose sẽ KHÔNG MarkShown.
        public GuidePanelView ShowPanelIfNotComplete(
            string stepId,
            GameObject panel,
            bool pauseGameWhenShow = false,
            bool forceShowOnTop = true,
            int sortingOrderOverride = 5000,
            bool ignorePrefsInEditor = true,
            bool disableMarkInEditor = true)
        {
            if (panel == null) return null;

            bool ignorePrefs = ShouldIgnorePrefsForTesting(ignorePrefsInEditor);

            // Chỉ chặn bằng prefs khi KHÔNG ignore
            if (!ignorePrefs && HasShown(stepId))
                return null;

            panel.SetActive(true);

            var view = panel.GetComponent<GuidePanelView>();
            if (view == null)
            {
                Debug.LogWarning("[Guide] Panel missing GuidePanelView: " + stepId);
                return null;
            }

            view.Setup(stepId, pauseGameWhenShow, forceShowOnTop, sortingOrderOverride, disableMarkInEditor);
            return view;
        }

        // Backward compatible: show + mark ngay (giữ code cũ không gãy)
        public bool ShowPanelOnce(
            string stepId,
            GameObject panel,
            bool pauseGameWhenShow = false,
            bool forceShowOnTop = true,
            int sortingOrderOverride = 5000)
        {
            if (panel == null) return false;
            if (HasShown(stepId)) return false;

            panel.SetActive(true);

            var view = panel.GetComponent<GuidePanelView>();
            if (view != null)
                view.Setup(stepId, pauseGameWhenShow, forceShowOnTop, sortingOrderOverride, false);

            MarkShown(stepId);
            return true;
        }

        public bool PlayTimelineOnce(string stepId, PlayableDirector director)
        {
            if (director == null) return false;
            if (HasShown(stepId)) return false;

            director.Play();
            MarkShown(stepId);
            return true;
        }

        public void ResetStep(string stepId)
        {
            PlayerPrefs.DeleteKey(KEY_PREFIX + stepId);
            PlayerPrefs.Save();
        }
    }
}
