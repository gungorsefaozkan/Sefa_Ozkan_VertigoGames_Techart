using System;
using System.Collections.Generic;
using UnityEngine;
using BattlePass.Data;

namespace BattlePass
{
    /// <summary>
    /// Owns all runtime Battle Pass state.
    /// No backend required — a real game would populate
    /// "currentXP", "hasPremium", etc. from its save/server layer.
    ///
    /// Drop on a persistent GameObject and wire via BattlePassManager.Instance.
    /// </summary>
    public class BattlePassManager : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────
        public static BattlePassManager Instance { get; private set; }

        // ── Inspector ──────────────────────────────────────────────
        [Header("Season")]
        [SerializeField] private BattlePassSeasonConfig seasonConfig;

        [Header("Player State (for testing — replace with your save system)")]
        [SerializeField] [Range(0, 6000)] private int currentXP = 80;
        [SerializeField] private bool hasPremiumPass = false;

        // ── Events (UI listens to these) ───────────────────────────
        public event Action<List<BattlePassNode>, List<BattlePassNode>> OnStateLoaded;
        // args: freeTrack, premiumTrack

        public event Action<BattlePassNode> OnRewardClaimed;
        public event Action<int, int, int> OnXPChanged;   // current, needed, level

        // ── Runtime data ───────────────────────────────────────────
        private List<BattlePassNode> _freeTrack    = new List<BattlePassNode>();
        private List<BattlePassNode> _premiumTrack = new List<BattlePassNode>();

        // Convenience properties
        public int  CurrentLevel     => Mathf.FloorToInt((float)currentXP / seasonConfig.xpPerLevel) + 1;
        public int  XPInCurrentLevel => currentXP % seasonConfig.xpPerLevel;
        public int  XPNeededPerLevel => seasonConfig.xpPerLevel;
        public bool HasPremium       => hasPremiumPass;
        public BattlePassSeasonConfig SeasonConfig => seasonConfig;

        // ── Lifecycle ──────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            BuildTracks();
        }

        // ── Public API ─────────────────────────────────────────────

        /// <summary>Call this after loading player data from your backend.</summary>
        public void Initialize(int xp, bool premiumOwned)
        {
            currentXP       = xp;
            hasPremiumPass  = premiumOwned;
            BuildTracks();
        }

        /// <summary>Claim a reward node. Fires OnRewardClaimed on success.</summary>
        public bool TryClaimReward(BattlePassNode node)
        {
            if (node.state != NodeState.Claimable) return false;
            if (node.isPremiumTrack && !hasPremiumPass) return false;

            node.state = NodeState.Claimed;

            // TODO: hook into your inventory/currency system here
            Debug.Log($"[BattlePass] Claimed: {node.rewardData?.rewardName} (L{node.level})");

            OnRewardClaimed?.Invoke(node);
            return true;
        }

        /// <summary>Simulate XP gain — useful for in-editor testing.</summary>
        [ContextMenu("Debug: Add 200 XP")]
        public void DebugAddXP()
        {
            AddXP(200);
        }

        public void AddXP(int amount)
        {
            int oldLevel = CurrentLevel;
            currentXP += amount;
            currentXP  = Mathf.Clamp(currentXP, 0, seasonConfig.xpPerLevel * seasonConfig.totalLevels);

            RefreshNodeStates();

            OnXPChanged?.Invoke(XPInCurrentLevel, XPNeededPerLevel, CurrentLevel);

            if (CurrentLevel > oldLevel)
                Debug.Log($"[BattlePass] Level Up! Now level {CurrentLevel}");
        }

        /// <summary>Reset XP to zero and rebuild tracks. Useful for testing.</summary>
        public void ResetXP()
        {
            currentXP = 0;
            hasPremiumPass = false;
            BuildTracks();
            Debug.Log("[BattlePass] XP reset to 0, premium revoked.");
        }

        /// <summary>Unlock premium pass — call after purchase IAP.</summary>
        public void UnlockPremium()
        {
            hasPremiumPass = true;
            RefreshNodeStates();
            OnStateLoaded?.Invoke(_freeTrack, _premiumTrack);
        }

        public List<BattlePassNode> GetFreeTrack()    => _freeTrack;
        public List<BattlePassNode> GetPremiumTrack() => _premiumTrack;

        // ── Internal ───────────────────────────────────────────────

        private void BuildTracks()
        {
            _freeTrack.Clear();
            _premiumTrack.Clear();

            int levels = seasonConfig.totalLevels;

            for (int i = 0; i < levels; i++)
            {
                var freeNode = new BattlePassNode
                {
                    level         = i + 1,
                    rewardData    = i < seasonConfig.freeRewards.Length ? seasonConfig.freeRewards[i] : null,
                    isPremiumTrack = false
                };

                var premNode = new BattlePassNode
                {
                    level          = i + 1,
                    rewardData     = i < seasonConfig.premiumRewards.Length ? seasonConfig.premiumRewards[i] : null,
                    isPremiumTrack = true
                };

                _freeTrack.Add(freeNode);
                _premiumTrack.Add(premNode);
            }

            RefreshNodeStates();
            OnStateLoaded?.Invoke(_freeTrack, _premiumTrack);
        }

        private void RefreshNodeStates()
        {
            int playerLevel = CurrentLevel;

            for (int i = 0; i < _freeTrack.Count; i++)
            {
                _freeTrack[i].state    = GetNodeState(_freeTrack[i],    playerLevel, true);
                _premiumTrack[i].state = GetNodeState(_premiumTrack[i], playerLevel, hasPremiumPass);
            }
        }

        private NodeState GetNodeState(BattlePassNode node, int playerLevel, bool trackAccessible)
        {
            // Already claimed — preserve
            if (node.state == NodeState.Claimed) return NodeState.Claimed;

            if (!trackAccessible)
                return NodeState.Locked;

            int nodeLevel = node.level;

            if (nodeLevel > playerLevel)
                return NodeState.Locked;

            if (nodeLevel == playerLevel)
                return NodeState.Locked;  // Current level shows as Locked — rewards not yet claimable

            // nodeLevel < playerLevel — claimable or already claimed
            return node.state == NodeState.Claimed ? NodeState.Claimed : NodeState.Claimable;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Live preview in editor without entering play mode
            if (seasonConfig == null) return;
            BuildTracks();
        }
#endif
    }
}
