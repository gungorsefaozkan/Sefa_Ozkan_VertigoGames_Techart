using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BattlePass.Data;

namespace BattlePass.UI
{
    /// <summary>
    /// Drives the scrollable Battle Pass Road screen.
    ///
    /// SCENE HIERARCHY EXPECTED:
    /// BattlePassScreen (Canvas)
    ///  ├── Header
    ///  │    ├── SeasonLabel        (TMP)
    ///  │    ├── SeasonThemeLabel   (TMP)
    ///  │    ├── XPBar              (Slider)
    ///  │    ├── XPText             (TMP — "80/200")
    ///  │    ├── LevelText          (TMP — "4")
    ///  │    └── TimeLeftText       (TMP — "17d 20h")
    ///  │
    ///  ├── PremiumBanner (left panel — season hero art)
    ///  │    ├── HeroImage          (Image)
    ///  │    ├── MythicLabel        (TMP)
    ///  │    └── GetButton          (Button)
    ///  │
    ///  ├── ScrollView (horizontal)
    ///  │    └── Viewport
    ///  │         └── Content
    ///  │              ├── PremiumTrackRow   (HorizontalLayoutGroup)
    ///  │              ├── LevelConnector    (track line + level bubbles)
    ///  │              └── FreeTrackRow      (HorizontalLayoutGroup)
    ///  │
    ///  └── TabBar
    ///       ├── ArenaPassTab       (Button)
    ///       └── MissionsTab        (Button)
    /// </summary>
    public class BattlePassRoadScreen : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────
        [Header("Header")]
        [SerializeField] private TextMeshProUGUI seasonLabel;
        [SerializeField] private TextMeshProUGUI seasonThemeLabel;
        [SerializeField] private Slider           xpBar;
        [SerializeField] private TextMeshProUGUI xpText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI timeLeftText;

        [Header("Road Layout")]
        [SerializeField] private ScrollRect       roadScrollRect;
        [SerializeField] private RectTransform    premiumTrackContainer;
        [SerializeField] private RectTransform    freeTrackContainer;
        [SerializeField] private RectTransform    levelConnectorContainer;

        [Header("Prefabs")]
        [SerializeField] private BattlePassNodeUI nodePrefab;
        [SerializeField] private LevelBubbleUI    levelBubblePrefab;

        [Header("XP Bar Animation")]
        [SerializeField] private float xpBarAnimDuration = 0.6f;
        [SerializeField] private AnimationCurve xpBarCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Scroll Snap")]
        [SerializeField] private float snapToCurrentLevelDelay = 0.3f;

        // ── Runtime ────────────────────────────────────────────────
        private readonly List<BattlePassNodeUI> _freeNodes    = new();
        private readonly List<BattlePassNodeUI> _premiumNodes = new();
        private readonly List<LevelBubbleUI>    _levelBubbles = new();

        private Coroutine _xpBarCoroutine;

        // ── Lifecycle ──────────────────────────────────────────────

        private void OnEnable()
        {
            // Try to subscribe immediately if the manager is already awake.
            TrySubscribe();
        }

        private void OnDisable()
        {
            if (_subscribed && BattlePassManager.Instance != null)
            {
                BattlePassManager.Instance.OnStateLoaded  -= HandleStateLoaded;
                BattlePassManager.Instance.OnRewardClaimed -= HandleRewardClaimed;
                BattlePassManager.Instance.OnXPChanged    -= HandleXPChanged;
                _subscribed = false;
            }
        }

        private bool _subscribed;

        private void TrySubscribe()
        {
            if (_subscribed) return;
            var mgr = BattlePassManager.Instance;
            if (mgr == null) return;

            mgr.OnStateLoaded  += HandleStateLoaded;
            mgr.OnRewardClaimed += HandleRewardClaimed;
            mgr.OnXPChanged    += HandleXPChanged;
            _subscribed = true;
            Debug.Log("[RoadScreen] Subscribed to BattlePassManager events.");
        }

        private void Start()
        {
            // Manager might not have been ready in OnEnable — subscribe now.
            TrySubscribe();

            var mgr = BattlePassManager.Instance;
            if (mgr == null) return;

            // Render immediately with whatever state is loaded
            HandleStateLoaded(mgr.GetFreeTrack(), mgr.GetPremiumTrack());
            RefreshHeader();
        }

        // ── Event Handlers ─────────────────────────────────────────

        private void HandleStateLoaded(List<BattlePassNode> freeTrack, List<BattlePassNode> premiumTrack)
        {
            // First load — build the road. Subsequent calls (e.g. premium unlock)
            // just refresh states without destroying existing nodes/animations.
            if (_freeNodes.Count == 0 && _premiumNodes.Count == 0)
            {
                BuildRoad(freeTrack, premiumTrack);
            }
            else
            {
                RefreshAllNodeStates(freeTrack, premiumTrack);
            }
            RefreshHeader();
            StartCoroutine(ScrollToCurrentLevelDelayed());
        }

        /// <summary>Updates existing node + bubble states without rebuilding the road.</summary>
        private void RefreshAllNodeStates(List<BattlePassNode> freeTrack, List<BattlePassNode> premiumTrack)
        {
            for (int i = 0; i < _freeNodes.Count && i < freeTrack.Count; i++)
            {
                if (freeTrack[i].state == NodeState.Claimed) continue;
                _freeNodes[i].SetState(freeTrack[i].state);
            }

            for (int i = 0; i < _premiumNodes.Count && i < premiumTrack.Count; i++)
            {
                if (premiumTrack[i].state == NodeState.Claimed) continue;
                _premiumNodes[i].SetState(premiumTrack[i].state);
            }

            int lastClaimedIdx = FindLastClaimedIndex(freeTrack);
            int bubbleCount = Mathf.Min(_levelBubbles.Count, freeTrack.Count);
            for (int i = 0; i < bubbleCount; i++)
            {
                bool isLast = (i == bubbleCount - 1);
                bool isLastClaimed = (i == lastClaimedIdx);
                _levelBubbles[i].SetLevel(freeTrack[i].level, freeTrack[i].state, isLast, isLastClaimed);
            }
        }

        private void HandleRewardClaimed(BattlePassNode node)
        {
            // Find the matching node UI and trigger its claim animation
            var mgr = BattlePassManager.Instance;
            if (mgr == null) return;

            var list  = node.isPremiumTrack ? _premiumNodes : _freeNodes;
            var track = node.isPremiumTrack ? mgr.GetPremiumTrack() : mgr.GetFreeTrack();

            for (int i = 0; i < track.Count && i < list.Count; i++)
            {
                if (track[i] == node)
                {
                    list[i].PlayClaimAnimation();
                    break;
                }
            }

            Debug.Log($"[Screen] Reward claimed: {node.rewardData?.rewardName}");
        }

        private void HandleXPChanged(int current, int needed, int level)
        {
            Debug.Log($"[RoadScreen] HandleXPChanged: xp={current}/{needed} level={level} freeNodes={_freeNodes.Count} premiumNodes={_premiumNodes.Count} bubbles={_levelBubbles.Count}");

            AnimateXPBar(current, needed);
            levelText.text = level.ToString();
            xpText.text    = $"{current}/{needed}";

            // Refresh all node states to reflect new level
            var mgr = BattlePassManager.Instance;
            var freeTracks    = mgr.GetFreeTrack();
            var premiumTracks = mgr.GetPremiumTrack();

            for (int i = 0; i < _freeNodes.Count; i++)
            {
                // Don't override Claimed nodes — they stay claimed forever
                if (freeTracks[i].state == NodeState.Claimed) continue;
                _freeNodes[i].SetState(freeTracks[i].state);
            }

            for (int i = 0; i < _premiumNodes.Count; i++)
            {
                if (premiumTracks[i].state == NodeState.Claimed) continue;
                _premiumNodes[i].SetState(premiumTracks[i].state);
            }

            // Refresh level bubbles so progress bars reflect the new level
            int lastClaimedIndex = FindLastClaimedIndex(freeTracks);
            int bubbleCount = Mathf.Min(_levelBubbles.Count, freeTracks.Count);
            for (int i = 0; i < bubbleCount; i++)
            {
                bool isLast = (i == bubbleCount - 1);
                bool isLastClaimed = (i == lastClaimedIndex);
                _levelBubbles[i].SetLevel(freeTracks[i].level, freeTracks[i].state, isLast, isLastClaimed);
            }

            // Scroll to the current level node
            ScrollToCurrentLevel();
        }

        /// <summary>Returns the index of the last Claimed/Claimable bubble, or -1 if none.</summary>
        private int FindLastClaimedIndex(List<BattlePassNode> track)
        {
            int last = -1;
            for (int i = 0; i < track.Count; i++)
            {
                if (track[i].state == NodeState.Claimed || track[i].state == NodeState.Claimable)
                    last = i;
            }
            return last;
        }

        // ── Road Construction ───────────────────────────────────────

        private void BuildRoad(List<BattlePassNode> freeTrack, List<BattlePassNode> premiumTrack)
        {
            // Clear existing nodes
            DestroyChildren(premiumTrackContainer);
            DestroyChildren(freeTrackContainer);
            DestroyChildren(levelConnectorContainer);
            _freeNodes.Clear();
            _premiumNodes.Clear();
            _levelBubbles.Clear();

            int count = Mathf.Min(freeTrack.Count, premiumTrack.Count);

            var cfg = BattlePassManager.Instance.SeasonConfig;
            Vector2 baseSize   = cfg != null ? cfg.nodeBaseSize       : new Vector2(140, 160);
            float freeScale    = cfg != null ? cfg.freeNodeScale    : 1f;
            float premiumScale = cfg != null ? cfg.premiumNodeScale : 1f;
            float bubbleScale  = cfg != null ? cfg.levelBubbleScale : 1f;
            float freeSpacing    = cfg != null ? cfg.freeNodeSpacing    : 8f;
            float premiumSpacing = cfg != null ? cfg.premiumNodeSpacing : 8f;

            // Apply config-driven spacing to the track row layout groups
            SetRowSpacing(premiumTrackContainer, premiumSpacing);
            SetRowSpacing(freeTrackContainer, freeSpacing);
            SetRowSpacing(levelConnectorContainer, Mathf.Max(freeSpacing, premiumSpacing));

            int lastClaimedIdx = FindLastClaimedIndex(freeTrack);

            for (int i = 0; i < count; i++)
            {
                // Premium node (top row)
                var premNode = Instantiate(nodePrefab, premiumTrackContainer);
                ApplyNodeScale(premNode, baseSize, premiumScale);
                premNode.Initialize(premiumTrack[i]);
                _premiumNodes.Add(premNode);

                // Free node (bottom row)
                var freeNode = Instantiate(nodePrefab, freeTrackContainer);
                ApplyNodeScale(freeNode, baseSize, freeScale);
                freeNode.Initialize(freeTrack[i]);
                _freeNodes.Add(freeNode);

                // Level bubble (connector row between the two tracks)
                var bubble = Instantiate(levelBubblePrefab, levelConnectorContainer);
                ApplyBubbleScale(bubble, baseSize, bubbleScale);
                bool isLast = (i == count - 1);
                bool isLastClaimed = (i == lastClaimedIdx);
                bubble.SetLevel(freeTrack[i].level, freeTrack[i].state, isLast, isLastClaimed);
                _levelBubbles.Add(bubble);
            }

            // Ensure ContentSizeFitter on track rows grows horizontally
            EnsureHorizontalFit(premiumTrackContainer);
            EnsureHorizontalFit(freeTrackContainer);
            EnsureHorizontalFit(levelConnectorContainer);

            // Ensure Content itself grows horizontally to fit the widest row
            if (roadScrollRect != null && roadScrollRect.content != null)
                EnsureHorizontalFit(roadScrollRect.content);
        }

        /// <summary>Adds or fixes a ContentSizeFitter with HorizontalFit = Preferred on a row.</summary>
        private void EnsureHorizontalFit(RectTransform row)
        {
            if (row == null) return;

            if (!row.TryGetComponent(out ContentSizeFitter fitter))
                fitter = row.gameObject.AddComponent<ContentSizeFitter>();

            fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        }

        /// <summary>
        /// Sets a fixed base size on the node root RectTransform and applies a
        /// scale multiplier. The base size matches the prefab Background so the
        /// layout group can measure the node correctly; scale grows/shrinks it.
        /// </summary>
        private void ApplyNodeScale(BattlePassNodeUI node, Vector2 baseSize, float scale)
        {
            var rt = (RectTransform)node.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = baseSize;
            node.SetBaseScale(scale);
        }

        /// <summary>
        /// Sets the level bubble to the same base width as the nodes so it
        /// aligns horizontally with the reward cards, then applies a scale
        /// multiplier (bubbles are usually smaller than nodes).
        /// </summary>
        private void ApplyBubbleScale(LevelBubbleUI bubble, Vector2 nodeBaseSize, float scale)
        {
            var rt = (RectTransform)bubble.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            // Match the node width so the layout group places it at the same X
            rt.sizeDelta = new Vector2(nodeBaseSize.x, nodeBaseSize.x);
            bubble.SetBaseScale(scale);
        }

        /// <summary>Updates the spacing of a HorizontalLayoutGroup on a track row.</summary>
        private void SetRowSpacing(RectTransform row, float spacing)
        {
            if (row == null) return;
            if (row.TryGetComponent(out HorizontalLayoutGroup layout))
                layout.spacing = spacing;
        }

        // ── Header ─────────────────────────────────────────────────

        private void RefreshHeader()
        {
            var mgr = BattlePassManager.Instance;
            var cfg = mgr.SeasonConfig;

            seasonLabel.text      = $"SEASON {cfg.seasonNumber}";
            seasonThemeLabel.text = cfg.seasonName.ToUpper();

            xpBar.maxValue   = mgr.XPNeededPerLevel;
            xpBar.value      = mgr.XPInCurrentLevel;
            xpText.text      = $"{mgr.XPInCurrentLevel}/{mgr.XPNeededPerLevel}";
            levelText.text   = mgr.CurrentLevel.ToString();

            // TODO: wire real timer from backend
            timeLeftText.text = "17d 20h";
        }

        // ── XP Bar Animation ───────────────────────────────────────

        private void AnimateXPBar(int current, int needed)
        {
            if (_xpBarCoroutine != null) StopCoroutine(_xpBarCoroutine);
            _xpBarCoroutine = StartCoroutine(AnimateSlider(xpBar, xpBar.value, current, needed));
        }

        private IEnumerator AnimateSlider(Slider slider, float from, float to, float max)
        {
            slider.maxValue = max;
            float elapsed = 0f;
            while (elapsed < xpBarAnimDuration)
            {
                elapsed += Time.deltaTime;
                float t = xpBarCurve.Evaluate(elapsed / xpBarAnimDuration);
                slider.value = Mathf.Lerp(from, to, t);
                yield return null;
            }
            slider.value = to;

            // Update label
            xpText.text = $"{(int)to}/{(int)max}";
        }

        // ── Scroll to Current Level ─────────────────────────────────

        private IEnumerator ScrollToCurrentLevelDelayed()
        {
            yield return new WaitForSeconds(snapToCurrentLevelDelay);
            yield return null; // wait one frame for layout to settle
            ScrollToCurrentLevel();
        }

        /// <summary>Scrolls to the current level node.</summary>
        private void ScrollToCurrentLevel()
        {
            if (_freeNodes.Count == 0) return;

            var mgr = BattlePassManager.Instance;
            if (mgr == null) return;

            int targetIndex = mgr.CurrentLevel - 1;
            targetIndex = Mathf.Clamp(targetIndex, 0, _freeNodes.Count - 1);

            ScrollToNodeIndex(targetIndex);
        }

        /// <summary>Scrolls so that the node at the given index is centered in the viewport.</summary>
        private void ScrollToNodeIndex(int index)
        {
            if (_freeNodes.Count == 0 || roadScrollRect == null) return;
            index = Mathf.Clamp(index, 0, _freeNodes.Count - 1);

            // Force layout rebuild so positions are current
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)roadScrollRect.content);

            RectTransform target = (RectTransform)_freeNodes[index].transform;
            RectTransform viewport = roadScrollRect.viewport;
            RectTransform content = roadScrollRect.content;

            if (viewport == null || content == null)
            {
                Debug.Log("[Scroll] viewport or content is NULL");
                return;
            }

            // Use world-space positions to find where the target is relative to content left edge
            Vector3 targetWorld = target.position;
            Vector3 contentWorld = content.position;
            Vector3 viewportWorld = viewport.position;

            // Distance from content's left edge to target center (in world units)
            float targetOffset = targetWorld.x - contentWorld.x;
            float viewportWidth = viewport.rect.width;
            float contentWidth = content.rect.width;

            Debug.Log($"[Scroll] index={index} targetOffset={targetOffset} viewportWidth={viewportWidth} contentWidth={contentWidth} contentWorldX={contentWorld.x} targetWorldX={targetWorld.x}");

            // scrollableWidth: how much we can scroll
            float scrollableWidth = contentWidth - viewportWidth;
            if (scrollableWidth <= 0f)
            {
                Debug.Log("[Scroll] scrollableWidth <= 0, no scrolling possible");
                roadScrollRect.horizontalNormalizedPosition = 0f;
                return;
            }

            // Center the target in the viewport:
            // We want target's center at viewport center.
            // Content starts at contentWorld.x, viewport center is at viewportWorld.x.
            // Offset needed = (targetWorld.x - viewportWorld.x) → how far target is from viewport center
            // normalized = (targetOffset - viewportHalfWidth) / scrollableWidth
            float normalized = Mathf.Clamp01((targetOffset - viewportWidth * 0.5f) / scrollableWidth);

            Debug.Log($"[Scroll] normalized={normalized} → scrolling to current level");
            StartCoroutine(SmoothScrollTo(normalized));
        }

        private void ScrollToLevel(int index)
        {
            ScrollToNodeIndex(index);
        }

        private IEnumerator SmoothScrollTo(float targetH)
        {
            float start = roadScrollRect.horizontalNormalizedPosition;
            float elapsed = 0f;
            float dur = 0.5f;

            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                roadScrollRect.horizontalNormalizedPosition =
                    Mathf.Lerp(start, targetH, xpBarCurve.Evaluate(elapsed / dur));
                yield return null;
            }
            roadScrollRect.horizontalNormalizedPosition = targetH;
        }

        // ── Utility ────────────────────────────────────────────────

        private void DestroyChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }
    }
}
