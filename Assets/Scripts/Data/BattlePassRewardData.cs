using UnityEngine;

namespace BattlePass.Data
{
    public enum RewardType
    {
        Skin,
        Weapon,
        Currency,
        Chest,
        Gem,
        BoosterPack,
        Attachment,
        PowerUp
    }

    public enum RewardTier
    {
        Free,
        Premium
    }

    [CreateAssetMenu(fileName = "BattlePassReward", menuName = "BattlePass/Reward Data")]
    public class BattlePassRewardData : ScriptableObject
    {
        [Header("Identity")]
        public string rewardName = "Unnamed Reward";
        public RewardType rewardType = RewardType.Currency;
        public RewardTier tier = RewardTier.Free;

        [Header("Visuals")]
        public Sprite rewardIcon;
        public Sprite nodeBackground;           // gold (free) or purple (premium)
        public Color tierColor = Color.yellow;

        [Header("Currency Icon")]
        [Tooltip("Icon shown next to the amount text for currency-type rewards (e.g. coin, gem). Leave null to hide.")]
        public Sprite currencyIcon;

        [Header("Value")]
        public int amount = 1;
    }

    // -----------------------------------------------------------------
    // Per-node runtime state (NOT a ScriptableObject — lives in memory)
    // -----------------------------------------------------------------
    public enum NodeState
    {
        Locked,
        Unlocked,   // free but not yet claimed
        Claimable,  // player has reached this tier — ready to collect
        Claimed,    // already collected
        Current     // the node the player is currently sitting on
    }

    [System.Serializable]
    public class BattlePassNode
    {
        public int level;                       // 1-based tier number
        public BattlePassRewardData rewardData;
        public NodeState state = NodeState.Locked;

        // Premium track mirrors the free track level but different reward
        public bool isPremiumTrack = false;
    }

    // -----------------------------------------------------------------
    // Season-level config (one ScriptableObject per season)
    // -----------------------------------------------------------------
    [CreateAssetMenu(fileName = "SeasonConfig", menuName = "BattlePass/Season Config")]
    public class BattlePassSeasonConfig : ScriptableObject
    {
        [Header("Season Info")]
        public int seasonNumber = 16;
        public string seasonName = "Cleopatra";
        public string seasonTheme = "Golden Realm";
        public Sprite seasonHeroArt;
        public Color seasonAccentColor = new Color(1f, 0.8f, 0.2f);

        [Header("XP Settings")]
        public int xpPerLevel = 200;
        public int totalLevels = 30;

        [Header("Node Layout")]
        [Tooltip("Base size (width, height) of reward nodes — matches the prefab Background.")]
        public Vector2 nodeBaseSize    = new Vector2(140, 160);
        [Tooltip("Scale multiplier applied to free-track reward nodes.")]
        public float freeNodeScale    = 1f;
        [Tooltip("Scale multiplier applied to premium-track reward nodes.")]
        public float premiumNodeScale = 1f;
        [Tooltip("Scale multiplier applied to level bubbles (connector row).")]
        public float levelBubbleScale = 1f;
        [Tooltip("Horizontal spacing between free-track nodes.")]
        public float freeNodeSpacing    = 8f;
        [Tooltip("Horizontal spacing between premium-track nodes.")]
        public float premiumNodeSpacing = 8f;

        [Header("Rewards — Free Track (index 0 = level 1)")]
        public BattlePassRewardData[] freeRewards;

        [Header("Rewards — Premium Track (must match freeRewards length)")]
        public BattlePassRewardData[] premiumRewards;
    }
}
