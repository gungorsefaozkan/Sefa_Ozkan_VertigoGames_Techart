using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BattlePass.Data;

namespace BattlePass.UI
{
    /// <summary>
    /// The numbered level bubble sitting between the Premium and Free tracks.
    /// Shows the tier number and highlights the current player level.
    ///
    /// HIERARCHY:
    /// LevelBubble (this)
    ///  ├── Locked      (Image — shown when the level is locked)
    ///  ├── Unlocked    (Image — shown when the level is unlocked)
    ///  ├── Current     (Image — shown when this is the current level)
    ///  ├── NumberText  (TMP)
    ///  ├── CurrentGlow (Image — extra ring shown at current level)
    ///  └── ProgressSlider (Slider — fills when the level is passed; hidden on the last level)
    /// </summary>
    public class LevelBubbleUI : MonoBehaviour
    {
        [Header("Bubble Variations")]
        [Tooltip("Image shown when the level is locked.")]
        [SerializeField] private Image variationLocked;
        [Tooltip("Image shown when the level is unlocked (Claimed or Claimable).")]
        [SerializeField] private Image variationUnlocked;
        [Tooltip("Image shown when this is the current level.")]
        [SerializeField] private Image variationCurrent;

        [SerializeField] private TextMeshProUGUI  numberText;
        [SerializeField] private GameObject       currentGlow;
        [Header("Progress Bar (to next level)")]
        [Tooltip("Slider that fills when this level is passed. Leave null if not used.")]
        [SerializeField] private Slider            progressSlider;
        [Tooltip("Handle (knob) shown only on the last claimed level bubble. Leave null if not used.")]
        [SerializeField] private GameObject       sliderHandle;

        [Header("Handle Animation")]
        [Tooltip("Y-scale curve played when the handle first appears. Evaluated 0→1 over the duration.")]
        [SerializeField] private AnimationCurve handleScaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Tooltip("Duration of the handle Y-scale animation in seconds.")]
        [SerializeField] private float handleScaleDuration = 0.3f;
        [Tooltip("Peak Y-scale multiplier at the top of the curve (e.g. 1.3 = 30% taller).")]
        [SerializeField] private float handlePeakScaleY = 1.3f;

        [Header("Colors")]
        [SerializeField] private Color colorLocked    = new Color(0.25f, 0.25f, 0.35f);
        [SerializeField] private Color colorUnlocked  = new Color(0.6f,  0.5f,  0.1f);
        [SerializeField] private Color colorCurrent   = new Color(1.0f,  0.85f, 0.2f);

        [Header("Animation")]
        [Tooltip("Transform that gets scaled by animations. Assign a parent holding only the bubble images (Locked/Unlocked/Current) so the slider and text are not affected. Leave null to scale the whole bubble.")]
        [SerializeField] private Transform animationTarget;
        [SerializeField] private float currentPulseSpeed    = 1.8f;
        [SerializeField] private float currentPulseMin      = 1.1f;
        [SerializeField] private float currentPulseMax      = 1.25f;
        [SerializeField] private float unlockedBounceScale  = 1.2f;
        [SerializeField] private float unlockedBounceDur     = 0.25f;
        [SerializeField] private AnimationCurve bounceCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        /// <summary>Whether this bubble is the final level (no bar to the right).</summary>
        private bool _isLastLevel;

        /// <summary>Base scale set by BattlePassRoadScreen.ApplyBubbleScale. Animations multiply on top of this.</summary>
        private float _baseScale = 1f;

        /// <summary>Currently running animation coroutine (null when idle).</summary>
        private Coroutine _animCoroutine;

        /// <summary>Last state applied — used to detect transitions.</summary>
        private NodeState _lastState = NodeState.Locked;

        /// <summary>Whether the handle was active last frame — used to detect first appearance.</summary>
        private bool _handleWasActive;

        /// <summary>Handle's original local scale (captured on first appearance).</summary>
        private Vector3 _handleBaseScale = Vector3.one;

        /// <summary>Called by BattlePassRoadScreen to set the config-driven scale. Animations preserve this.</summary>
        public void SetBaseScale(float scale)
        {
            _baseScale = Mathf.Max(scale, 0.01f);
            if (_animCoroutine == null)
                AnimTarget.localScale = Vector3.one * _baseScale;
        }

        /// <summary>Transform that animations scale — the visible variation's transform, or animationTarget if assigned, or root.</summary>
        private Transform AnimTarget
        {
            get
            {
                if (animationTarget != null) return animationTarget;
                // Use the currently visible variation's transform so the slider/text are not affected
                if (variationCurrent != null && variationCurrent.gameObject.activeSelf) return variationCurrent.transform;
                if (variationUnlocked != null && variationUnlocked.gameObject.activeSelf) return variationUnlocked.transform;
                if (variationLocked != null && variationLocked.gameObject.activeSelf) return variationLocked.transform;
                return transform;
            }
        }

        /// <summary>
        /// Configures the bubble for a given level and state.
        /// </summary>
        /// <param name="level">1-based level number.</param>
        /// <param name="adjacentState">State of the free-track node at this level.</param>
        /// <param name="isLastLevel">True if this is the final level — hides the progress bar.</param>
        public void SetLevel(int level, NodeState adjacentState, bool isLastLevel = false, bool isLastClaimed = false)
        {
            numberText.text = level.ToString();
            _isLastLevel = isLastLevel;

            bool isCurrent  = adjacentState == NodeState.Current;
            bool isUnlocked = adjacentState == NodeState.Claimed || adjacentState == NodeState.Claimable;

            // 3 bubble variations — only one visible at a time
            if (variationLocked   != null) variationLocked.gameObject.SetActive(!isCurrent && !isUnlocked);
            if (variationUnlocked != null) variationUnlocked.gameObject.SetActive(isUnlocked);
            if (variationCurrent  != null) variationCurrent.gameObject.SetActive(isCurrent);

            // currentGlow is a child of variationCurrent — only show when current.
            // Guard against it being accidentally assigned to another variation's object.
            if (currentGlow != null && currentGlow != variationLocked && currentGlow != variationUnlocked)
                currentGlow.SetActive(isCurrent);

            // Trigger animation on state change
            TriggerStateAnimation(adjacentState);

            UpdateProgressBar(adjacentState, isLastClaimed);
        }

        // ── Animation ──────────────────────────────────────────────

        private void TriggerStateAnimation(NodeState newState)
        {
            // Same state — don't touch the running animation
            if (newState == _lastState) return;
            _lastState = newState;

            Debug.Log($"[Bubble] State changed to {newState} on level {numberText.text} — starting animation");

            // State changed — stop any running animation and reset scale
            if (_animCoroutine != null)
            {
                StopCoroutine(_animCoroutine);
                _animCoroutine = null;
            }
            AnimTarget.localScale = Vector3.one * _baseScale;

            switch (newState)
            {
                case NodeState.Current:
                    _animCoroutine = StartCoroutine(CurrentPulseLoop());
                    break;
                case NodeState.Claimable:
                case NodeState.Claimed:
                    Debug.Log($"[Bubble] Starting BounceIn on level {numberText.text}");
                    _animCoroutine = StartCoroutine(BounceIn(unlockedBounceScale, unlockedBounceDur));
                    break;
                // Locked — no animation, scale already reset above
            }
        }

        /// <summary>Continuous gentle pulse for the current-level bubble.</summary>
        private IEnumerator CurrentPulseLoop()
        {
            Transform t = AnimTarget;
            while (true)
            {
                float s = Mathf.Lerp(currentPulseMin, currentPulseMax,
                    (Mathf.Sin(Time.time * currentPulseSpeed) + 1f) * 0.5f);
                t.localScale = Vector3.one * (s * _baseScale);
                yield return null;
            }
        }

        /// <summary>Quick bounce when a level becomes unlocked/passed.</summary>
        private IEnumerator BounceIn(float peakScale, float duration)
        {
            Transform t = AnimTarget;
            Debug.Log($"[Bubble] BounceIn started — target={t.name} baseScale={_baseScale} active={t.gameObject.activeSelf}");
            float elapsed = 0f;

            // Phase 1: scale up
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float n = elapsed / duration;
                float s = Mathf.Lerp(1f, peakScale, bounceCurve.Evaluate(n));
                t.localScale = Vector3.one * (s * _baseScale);
                yield return null;
            }

            // Phase 2: settle to base
            elapsed = 0f;
            float settleDur = duration * 0.5f;
            while (elapsed < settleDur)
            {
                elapsed += Time.deltaTime;
                float n = elapsed / settleDur;
                float s = Mathf.Lerp(peakScale, 1f, n);
                t.localScale = Vector3.one * (s * _baseScale);
                yield return null;
            }

            t.localScale = Vector3.one * _baseScale;
            _animCoroutine = null;
        }

        /// <summary>
        /// Updates the progress bar based on the node state.
        /// The bar fills completely only when the level is fully passed
        /// (Claimed or Claimable). It stays empty while Locked/Current.
        /// The bar is hidden entirely on the last level.
        /// </summary>
        public void UpdateProgressBar(NodeState adjacentState, bool isLastClaimed = false)
        {
            // Handle visibility — only on the last claimed bubble
            if (sliderHandle != null)
            {
                bool wasActive = _handleWasActive;
                sliderHandle.SetActive(isLastClaimed);

                if (isLastClaimed && !wasActive)
                {
                    // First appearance — capture base scale and play Y-scale animation
                    _handleBaseScale = sliderHandle.transform.localScale;
                    if (_handleBaseScale == Vector3.zero)
                        _handleBaseScale = Vector3.one;
                    StartCoroutine(HandleAppearAnimation());
                }
                _handleWasActive = isLastClaimed;
            }

            if (progressSlider == null) return;

            // Last level has no bar to the right
            if (_isLastLevel)
            {
                progressSlider.gameObject.SetActive(false);
                return;
            }

            progressSlider.gameObject.SetActive(true);

            bool isUnlocked = adjacentState == NodeState.Claimed || adjacentState == NodeState.Claimable;

            // Last claimed → half fill (0.5), other unlocked → full (1.0), locked/current → empty (0)
            if (isLastClaimed)
                progressSlider.value = progressSlider.maxValue * 0.5f;
            else if (isUnlocked)
                progressSlider.value = progressSlider.maxValue;
            else
                progressSlider.value = 0f;
        }

        /// <summary>Y-scale pop animation for the handle when it first appears.</summary>
        private IEnumerator HandleAppearAnimation()
        {
            Transform t = sliderHandle.transform;
            float elapsed = 0f;

            while (elapsed < handleScaleDuration)
            {
                elapsed += Time.deltaTime;
                float n = elapsed / handleScaleDuration;
                float curveVal = handleScaleCurve.Evaluate(n);
                // Y goes from 0 → peak → settle to 1, multiplied by the handle's base scale
                float yScale = Mathf.Lerp(0f, handlePeakScaleY, curveVal) * _handleBaseScale.y;
                t.localScale = new Vector3(_handleBaseScale.x, yScale, _handleBaseScale.z);
                yield return null;
            }

            // Settle to base scale
            t.localScale = _handleBaseScale;
        }
    }
}
