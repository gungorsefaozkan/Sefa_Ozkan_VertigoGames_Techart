using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using BattlePass.Data;
using BattlePass.VFX;

namespace BattlePass.UI
{
    /// <summary>
    /// Controls a single reward node card on the Battle Pass Road.
    /// Attach to the root of the node prefab.
    ///
    /// HIERARCHY EXPECTED:
    /// NodeRoot (this script)
    ///  ├── Background          (Image)
    ///  ├── LockIcon            (GameObject — shown when Locked)
    ///  ├── IconImage           (Image — reward sprite)
    ///  ├── LabelText           (TMP — reward name)
    ///  ├── AmountText          (TMP — "x100", "♦10" etc.)
    ///  ├── ClaimButton         (Button — visible when Claimable)
    ///  ├── ClaimedStamp        (GameObject — checkmark overlay)
    ///  ├── CurrentIndicator    (GameObject — glowing ring for current node)
    ///  ├── PremiumBadge        (GameObject — crown icon for premium tier)
    ///  └── VFXContainer        (parent for particle systems)
    ///       ├── ParticleGlow   (ambient shimmer — always-on for Claimable)
    ///       ├── ParticleShine  (sweep highlight)
    ///       └── ParticleClaim  (burst on claim — one-shot)
    /// </summary>
    public class BattlePassNodeUI : MonoBehaviour, IPointerClickHandler
    {
        // ── Inspector refs ─────────────────────────────────────────
        [Header("Core UI")]
        [Tooltip("Background shown when the node is locked.")]
        [SerializeField] private Image          backgroundLocked;
        [Tooltip("Background shown when the node is unlocked/claimable/claimed.")]
        [SerializeField] private Image          backgroundUnlocked;
        [SerializeField] private Image          iconImage;
        [SerializeField] private TextMeshProUGUI labelText;
        [SerializeField] private TextMeshProUGUI amountText;
        [SerializeField] private Button         claimButton;

        [Header("Currency Icon")]
        [Tooltip("Image element that displays the currency icon next to the amount (e.g. coin/gem). Optional.")]
        [SerializeField] private Image currencyIconImage;

        [Header("State Objects")]
        [SerializeField] private GameObject lockIcon;
        [SerializeField] private GameObject claimedStamp;
        [SerializeField] private GameObject currentIndicator;
        [SerializeField] private GameObject premiumBadge;

        [Header("VFX (NodeVFXController handles internals)")]
        [SerializeField] private NodeVFXController vfx;

        [Header("Claimable Anim")]
        [Tooltip("Scale curve for the claimable pulse animation. Y-axis is the scale multiplier (1 = base scale). Evaluated over 'claimableDuration' seconds, looping.")]
        [SerializeField] private AnimationCurve claimableCurve = AnimationCurve.EaseInOut(0, 1f, 0.5f, 1.05f);
        [Tooltip("Duration of one claimable pulse cycle in seconds.")]
        [SerializeField] private float claimableDuration = 1f;

        [Header("Unlocked Anim")]
        [Tooltip("Scale curve for the unlocked bounce animation. Y-axis is the scale multiplier (1 = base scale). Evaluated over 'unlockedDuration' seconds.")]
        [SerializeField] private AnimationCurve unlockedCurve = AnimationCurve.EaseInOut(0, 0f, 0.6f, 1.15f);
        [Tooltip("Duration of the unlocked bounce animation in seconds.")]
        [SerializeField] private float unlockedDuration = 0.3f;

        [Header("Claim Anim")]
        [Tooltip("Scale curve for the claim punch. Y-axis is the scale multiplier (1 = base scale). Evaluated over 'claimDuration' seconds.")]
        [SerializeField] private AnimationCurve claimCurve = AnimationCurve.EaseInOut(0, 1f, 0.3f, 1.25f);
        [Tooltip("Duration of the claim punch animation in seconds.")]
        [SerializeField] private float claimDuration = 0.4f;


        [Header("Locked Rotate Anim")]
        [Tooltip("Continuous rotation speed for locked nodes (degrees per second). 0 = no rotation.")]

        [SerializeField] private float lockedRotateDuration = 1f;
        [Tooltip("Curve (0→1) shaping the rotation over one full cycle. Default = linear.")]
        [SerializeField] private AnimationCurve lockedRotateCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [Tooltip("Rotation axis — which axis to rotate around.")]
        [SerializeField] private Vector3 lockedRotateAxis = new Vector3(0, 0, 1);

        // ── Runtime ────────────────────────────────────────────────
        private BattlePassNode _node;
        private NodeState      _currentState;
        private Coroutine      _animCoroutine;
        private bool           _isClaiming;
        private bool           _burstPlayed;

        /// <summary>Base scale set by BattlePassRoadScreen.ApplyNodeScale. Animations multiply on top of this.</summary>
        private float _baseScale = 1f;

        /// <summary>Called by BattlePassRoadScreen to set the config-driven scale. Animations preserve this.</summary>
        public void SetBaseScale(float scale)
        {
            _baseScale = Mathf.Max(scale, 0.01f);
            // If no animation is running, apply immediately
            if (_animCoroutine == null)
                transform.localScale = Vector3.one * _baseScale;
        }

        // ── Public API ─────────────────────────────────────────────

        /// <summary>
        /// Populates the node with data and sets the initial visual state.
        /// Call this once after instantiation.
        /// </summary>
        public void Initialize(BattlePassNode node)
        {
            _node = node;

            // Ensure the node root can receive pointer clicks by adding an
            // invisible raycatcher Image if none exists yet.
            EnsureRaycastTarget();

            // Reward art
            if (node.rewardData != null)
            {
                iconImage.sprite  = node.rewardData.rewardIcon;
                labelText.text    = node.rewardData.rewardName;
                amountText.text   = BuildAmountString(node.rewardData);
                iconImage.enabled = node.rewardData.rewardIcon != null;

                // Assign the reward's node background sprite to the locked background
                if (backgroundLocked != null && node.rewardData.nodeBackground != null)
                {
                    backgroundLocked.sprite = node.rewardData.nodeBackground;
                    backgroundLocked.enabled = true;
                }

                // Currency icon placeholder (shown for currency-type rewards with an icon assigned)
                if (currencyIconImage != null)
                {
                    bool showCurrencyIcon = node.rewardData.currencyIcon != null
                        && (node.rewardData.rewardType == RewardType.Currency
                            || node.rewardData.rewardType == RewardType.Gem);
                    currencyIconImage.sprite  = showCurrencyIcon ? node.rewardData.currencyIcon : null;
                    currencyIconImage.enabled = showCurrencyIcon;
                }
            }

            if (premiumBadge != null) premiumBadge.SetActive(node.isPremiumTrack);

            claimButton.onClick.RemoveAllListeners();
            claimButton.onClick.AddListener(OnClaimClicked);

            ApplyState(node.state, animated: false);
        }

        /// <summary>
        /// Updates visual state, optionally with transition animation.
        /// Safe to call at any time (e.g. after XP gain refreshes the track).
        /// </summary>
        public void SetState(NodeState newState, bool animated = true)
        {
            if (_currentState == newState) return;
            _currentState = newState;
            ApplyState(newState, animated);
        }

        // ── Internal ───────────────────────────────────────────────

        private void ApplyState(NodeState state, bool animated)
        {
            _currentState = state;

            // Stop any running animation when entering non-animated states
            if (state == NodeState.Claimed)
            {
                if (_animCoroutine != null) { StopCoroutine(_animCoroutine); _animCoroutine = null; }
                transform.localScale = Vector3.one * _baseScale;
            }

            // Locked state: stop any running animation, reset scale — no continuous rotation
            if (state == NodeState.Locked)
            {
                if (_animCoroutine != null) { StopCoroutine(_animCoroutine); _animCoroutine = null; }
                transform.localScale = Vector3.one * _baseScale;
                transform.localRotation = Quaternion.identity;
            }

            // --- Object visibility ---
            if (lockIcon != null) lockIcon.SetActive(state == NodeState.Locked);
            if (claimedStamp != null) claimedStamp.SetActive(state == NodeState.Claimed);
            if (currentIndicator != null) currentIndicator.SetActive(state == NodeState.Current);
            if (claimButton != null) claimButton.gameObject.SetActive(state == NodeState.Claimable);

            // --- Background swap (locked vs unlocked) ---
            bool isLocked = state == NodeState.Locked;
            if (backgroundLocked != null)   backgroundLocked.gameObject.SetActive(isLocked);
            if (backgroundUnlocked != null) backgroundUnlocked.gameObject.SetActive(!isLocked);

            // --- VFX — skip SetState during claim sequence (PlayClaimBurst handles it) ---
            if (!_isClaiming)
                vfx?.SetState(state, _node.isPremiumTrack);

            // --- Animations ---
            if (animated) TriggerStateAnimation(state);
        }

        private void TriggerStateAnimation(NodeState state)
        {
            if (_animCoroutine != null) StopCoroutine(_animCoroutine);

            switch (state)
            {
                case NodeState.Claimable:
                    // Bounce in first (unlocked feel), then start the idle pulse loop
                    _animCoroutine = StartCoroutine(UnlockedThenPulse());
                    break;
                case NodeState.Unlocked:
                    _animCoroutine = StartCoroutine(UnlockedBounce());
                    break;
                case NodeState.Current:
                    _animCoroutine = StartCoroutine(ClaimablePulseLoop());
                    break;
                case NodeState.Claimed:
                    // No animation — claimed nodes stay static
                    _animCoroutine = null;
                    break;
                // Locked — no continuous animation; rotation only plays on click
                default:
                    _animCoroutine = null;
                    break;
            }
        }

        /// <summary>Continuous pulse for claimable nodes, driven by claimableCurve over claimableDuration.</summary>
        private IEnumerator ClaimablePulseLoop()
        {
            Transform t = transform;
            float duration = Mathf.Max(claimableDuration, 0.01f);

            while (true)
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float n = elapsed / duration;
                    float curveScale = claimableCurve.Evaluate(n);
                    t.localScale = Vector3.one * (curveScale * _baseScale);
                    yield return null;
                }
            }
        }

        /// <summary>One-shot bounce for unlocked nodes, driven by unlockedCurve over unlockedDuration.</summary>
        private IEnumerator UnlockedBounce()
        {
            Transform t = transform;
            float duration = Mathf.Max(unlockedDuration, 0.01f);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float n = elapsed / duration;
                float curveScale = unlockedCurve.Evaluate(n);
                t.localScale = Vector3.one * (curveScale * _baseScale);
                yield return null;
            }

            t.localScale = Vector3.one * _baseScale;
            _animCoroutine = null;
        }

        /// <summary>Bounce in (unlocked feel), then transition into the idle claimable pulse loop.</summary>
        private IEnumerator UnlockedThenPulse()
        {
            yield return StartCoroutine(UnlockedBounce());
            _animCoroutine = StartCoroutine(ClaimablePulseLoop());
        }

        /// <summary>One-shot rotation animation for locked nodes when clicked.</summary>
        private IEnumerator LockedRotateOnce()
        {
            Transform t = transform;
            Vector3 axis = lockedRotateAxis.normalized;
            float elapsed = 0f;
            float duration = Mathf.Max(lockedRotateDuration, 0.01f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float n = elapsed / duration;
                float shapedAngle = lockedRotateCurve.Evaluate(n) * 360f;
                t.localRotation = Quaternion.AngleAxis(shapedAngle, axis);
                yield return null;
            }

            // Reset rotation back to identity
            t.localRotation = Quaternion.identity;
            _animCoroutine = null;
        }

        private void OnClaimClicked()
        {
            Debug.Log($"[Node] OnClaimClicked — level={_node?.level} _currentState={_currentState} _node.state={_node?.state} isPremium={_node?.isPremiumTrack} hasPremium={BattlePassManager.Instance?.HasPremium}");

            // Find the matching node in the manager's track and sync its state
            var mgr = BattlePassManager.Instance;
            if (mgr != null && _node != null)
            {
                var track = _node.isPremiumTrack ? mgr.GetPremiumTrack() : mgr.GetFreeTrack();
                for (int i = 0; i < track.Count; i++)
                {
                    if (track[i].level == _node.level && track[i].isPremiumTrack == _node.isPremiumTrack)
                    {
                        // Sync the manager's node state with the visual state
                        if (track[i].state != _currentState)
                        {
                            Debug.Log($"[Node] Syncing manager track[{i}].state from {track[i].state} to {_currentState}");
                            track[i].state = _currentState;
                        }
                        // Also sync _node reference to the manager's node
                        _node = track[i];
                        break;
                    }
                }
            }

            if (BattlePassManager.Instance.TryClaimReward(_node))
            {
                // Stop pulse, play claim burst
                if (_animCoroutine != null) StopCoroutine(_animCoroutine);
                _animCoroutine = StartCoroutine(ClaimSequence());
            }
            else
            {
                Debug.Log($"[Node] TryClaimReward returned FALSE — state={_node?.state} isPremium={_node?.isPremiumTrack} hasPremium={BattlePassManager.Instance?.HasPremium}");
            }
        }

        /// <summary>Called by BattlePassRoadScreen when OnRewardClaimed fires.</summary>
        public void PlayClaimAnimation()
        {
            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            _animCoroutine = StartCoroutine(ClaimSequence());
        }

        /// <summary>Click anywhere on the node to claim when Claimable.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            Debug.Log($"[Node] OnPointerClick — state={_currentState} level={_node?.level}");
            if (_currentState == NodeState.Claimable)
                OnClaimClicked();
            else if (_currentState == NodeState.Locked)
            {
                // Play a one-shot locked rotation animation on click
                if (_animCoroutine != null) StopCoroutine(_animCoroutine);
                _animCoroutine = StartCoroutine(LockedRotateOnce());
            }
        }

        // ── Coroutines ─────────────────────────────────────────────

        /// <summary>Claim punch: scale up → VFX burst → fade to Claimed state.</summary>
        private IEnumerator ClaimSequence()
        {
            _isClaiming = true;

            Transform t = transform;
            float duration = Mathf.Max(claimDuration, 0.01f);
            float elapsed = 0f;

            // Scale up via curve, then trigger burst at the peak
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float n = elapsed / duration;
                float curveScale = claimCurve.Evaluate(n);
                t.localScale = Vector3.one * (curveScale * _baseScale);

                // Trigger burst VFX at the curve's peak (roughly mid-point)
                if (n >= 0.5f && !_burstPlayed)
                {
                    vfx?.PlayClaimBurst();
                    _burstPlayed = true;
                }
                yield return null;
            }

            t.localScale = Vector3.one * _baseScale;
            _burstPlayed = false;
            _isClaiming = false;

            // After a short delay apply the Claimed visual state
            yield return new WaitForSeconds(0.3f);
            ApplyState(NodeState.Claimed, animated: false);
        }

        // ── Helpers ────────────────────────────────────────────────

        /// <summary>
        /// Ensures the node root has a Graphic that can catch pointer events.
        /// If no Image exists on the root, adds an invisible full-size one.
        /// </summary>
        private void EnsureRaycastTarget()
        {
            // If there's already an Image on the root with raycastTarget, we're fine
            if (TryGetComponent<Image>(out var img) && img.raycastTarget)
            {
                Debug.Log($"[Node] RaycastTarget already present on {gameObject.name}");
                DisableChildRaycasts(img);
                return;
            }

            // Otherwise add an invisible Image that fills the rect and catches clicks
            var raycatcher = gameObject.GetComponent<Image>();
            if (raycatcher == null)
                raycatcher = gameObject.AddComponent<Image>();

            raycatcher.color = new Color(0, 0, 0, 0); // fully transparent
            raycatcher.raycastTarget = true;

            Debug.Log($"[Node] Added invisible raycatcher Image to {gameObject.name} — rect={((RectTransform)transform).rect}");

            DisableChildRaycasts(raycatcher);
        }

        private void DisableChildRaycasts(Image except)
        {
            var childImages = GetComponentsInChildren<Image>();
            foreach (var childImg in childImages)
            {
                if (childImg != except && childImg.raycastTarget)
                {
                    childImg.raycastTarget = false;
                    Debug.Log($"[Node] Disabled raycast on child: {childImg.gameObject.name}");
                }
            }
        }

        private string BuildAmountString(BattlePassRewardData data)
        {
            if (data.amount <= 0 || data.rewardType == RewardType.Skin || data.rewardType == RewardType.Weapon)
                return "";

            return $"x{data.amount}";
        }
    }
}
