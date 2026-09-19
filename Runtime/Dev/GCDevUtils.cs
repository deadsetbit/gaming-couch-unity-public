using UnityEngine;

namespace DSB.GC.Dev
{
    public enum FluctuateFpsMode
    {
        Disabled,
        EditorOnly,
        EditorAndBuild
    }

    public class GCDevUtils : MonoBehaviour
    {
        [Header("Time Scale Shortcuts")]
        [SerializeField]
        private bool enableTimeScaleShortcuts = true;

        [SerializeField, Tooltip("Decreases time scale by 0.1")]
        private KeyCode decreaseTimeScalePrimary = KeyCode.KeypadMinus;

        [SerializeField, Tooltip("Alternative key to decrease time scale by 0.1")]
        private KeyCode decreaseTimeScaleAlternative = KeyCode.Comma;

        [SerializeField, Tooltip("Increases time scale by 0.1")]
        private KeyCode increaseTimeScalePrimary = KeyCode.KeypadPlus;

        [SerializeField, Tooltip("Alternative key to increase time scale by 0.1")]
        private KeyCode increaseTimeScaleAlternative = KeyCode.Period;

        [SerializeField, Tooltip("Toggles pause. Hold Shift to restore previous time scale instead of 1.0")]
        private KeyCode togglePausePrimary = KeyCode.KeypadEnter;

        [SerializeField, Tooltip("Alternative key to toggle pause. Hold Shift to restore previous time scale instead of 1.0")]
        private KeyCode togglePauseAlternative = KeyCode.Minus;

        [Header("Fluctuate FPS")]
        [SerializeField]
        private FluctuateFpsMode enableFluctuateFps = FluctuateFpsMode.Disabled;

        [SerializeField]
        private int fluctuateMaxMsPerFrame = 16;

        [Header("Play Mode Restart")]
        [SerializeField]
        private bool enablePlayModeRestart = true;

        [SerializeField]
        private KeyCode playModeRestartKey = KeyCode.Tab;

        private void OnEnable()
        {
            if (ShouldFluctuateFps())
            {
                Debug.LogWarning("[GCDevUtils] Fluctuate FPS is enabled and set to '" + enableFluctuateFps + "' with max ms per frame of '" + fluctuateMaxMsPerFrame + "'");
            }

            previouslySetTimescale = Time.timeScale;
        }

        private void Update()
        {
            HandleFluctuateFps();
            HandlePlayModeRestart();
        }

        void OnGUI()
        {
#if UNITY_EDITOR
            Event e = Event.current;
            if (e.isKey && e.type == EventType.KeyDown)
            {
                HandleTimeScale(e.keyCode);
            }
#endif
        }

        private bool ShouldFluctuateFps()
        {
            if (enableFluctuateFps == FluctuateFpsMode.EditorAndBuild)
            {
                return true;
            }
#if UNITY_EDITOR
            else if (enableFluctuateFps == FluctuateFpsMode.EditorOnly)
            {
                return true;
            }
#endif
            return false;
        }

        private void HandleFluctuateFps()
        {
#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.F7))
            {
                enableFluctuateFps = (FluctuateFpsMode)(((int)enableFluctuateFps + 1) % 3);
            }
#endif

            bool shouldFluctuate = ShouldFluctuateFps();

            if (shouldFluctuate && fluctuateMaxMsPerFrame > 0)
            {
                var t = (Mathf.Sin(Time.time * 7.0f) + 1.0f) * 0.5f;
                var jitter = UnityEngine.Random.Range(0, 4);
                var targetMs = Mathf.Clamp(Mathf.RoundToInt(t * fluctuateMaxMsPerFrame) + jitter, 0, fluctuateMaxMsPerFrame + 3);

                var start = System.Diagnostics.Stopwatch.StartNew();
                while (start.ElapsedMilliseconds < targetMs)
                {
                }
                start.Stop();
            }
        }

        private float previouslySetTimescale = 1.0f;

        private float GetCurrentTimescale()
        {
            if (GamingCouch.Instance != null)
            {
                return GamingCouch.Instance.CurrentTimescale;
            }

            return Time.timeScale;
        }

        private bool IsPaused()
        {
            if (GamingCouch.Instance != null)
            {
                return GamingCouch.Instance.IsPaused;
            }

            return Mathf.Approximately(Time.timeScale, 0.0f);
        }

        // Shared with GCDevAppIntegration so the "GamingCouch owns the timescale, otherwise drive
        // Time.timeScale directly" rule -- clamp included -- lives in one place.
        internal static void ApplyTimescale(float nextTimescale)
        {
            if (GamingCouch.Instance != null)
            {
                GamingCouch.Instance.ApplyDevTimescale(nextTimescale);
                return;
            }

            Time.timeScale = Mathf.Clamp(nextTimescale, 0.1f, 10.0f);
        }

        private void ApplyPause(bool nextPaused)
        {
            if (GamingCouch.Instance != null)
            {
                GamingCouch.Instance.ApplyDevPause(nextPaused);
                return;
            }

            Time.timeScale = nextPaused ? 0.0f : Mathf.Max(previouslySetTimescale, 0.1f);
        }

        private void HandleTimeScale(KeyCode keyCode)
        {
#if UNITY_EDITOR
            if (!enableTimeScaleShortcuts)
            {
                return;
            }

            if (keyCode == decreaseTimeScalePrimary || keyCode == decreaseTimeScaleAlternative)
            {
                var currentTimescale = GetCurrentTimescale();
                previouslySetTimescale = currentTimescale;
                ApplyTimescale(Mathf.Max(currentTimescale - 0.1f, 0.1f));
            }
            if (keyCode == increaseTimeScalePrimary || keyCode == increaseTimeScaleAlternative)
            {
                var currentTimescale = GetCurrentTimescale();
                previouslySetTimescale = currentTimescale;
                ApplyTimescale(Mathf.Min(currentTimescale + 0.1f, 10.0f));
            }
            if (keyCode == togglePausePrimary || keyCode == togglePauseAlternative)
            {
                var isPaused = IsPaused();
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    if (isPaused)
                    {
                        ApplyTimescale(Mathf.Max(previouslySetTimescale, 0.1f));
                        ApplyPause(false);
                    }
                    else
                    {
                        previouslySetTimescale = GetCurrentTimescale();
                        ApplyPause(true);
                    }
                }
                else
                {
                    if (isPaused)
                    {
                        ApplyTimescale(1.0f);
                        ApplyPause(false);
                    }
                    else
                    {
                        previouslySetTimescale = GetCurrentTimescale();
                        ApplyPause(true);
                    }
                }
            }
#endif
        }

        private void HandlePlayModeRestart()
        {
            if (Input.GetKeyDown(playModeRestartKey) && GamingCouch.Instance != null && !GamingCouch.Instance.IsRestarting)
            {
                if (enablePlayModeRestart)
                {
                    GamingCouch.Instance._InternalHandleGamePlayModeRestart();
                }
            }
        }
    }
}
