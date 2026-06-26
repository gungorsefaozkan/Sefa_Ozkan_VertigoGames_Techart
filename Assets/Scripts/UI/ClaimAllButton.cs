using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BattlePass.Data;

namespace BattlePass.UI
{
    /// <summary>
    /// "Claim All" button that sweeps through every Claimable node
    /// with a staggered animation — the feel of rewards popping in sequence.
    ///
    /// Attach to the Claim All Button GameObject.
    /// Wire ClaimableNodes via BattlePassRoadScreen.
    /// </summary>
    public class ClaimAllButton : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Button              button;
        [SerializeField] private TextMeshProUGUI     label;
        [SerializeField] private BattlePassRoadScreen roadScreen;

        [Header("Timing")]
        [SerializeField] private float staggerDelay = 0.12f; // seconds between each node claim

        private bool _isClaiming;

        private void Awake()
        {
            button.onClick.AddListener(OnClaimAllClicked);
        }

        private void Start()
        {
            // Subscribe in Start — manager is guaranteed awake by then
            var mgr = BattlePassManager.Instance;
            if (mgr != null)
            {
                mgr.OnStateLoaded  += (_, _) => RefreshVisibility();
                mgr.OnRewardClaimed += _ => RefreshVisibility();
                mgr.OnXPChanged    += (_, _, _) => RefreshVisibility();
            }
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            int count = CountClaimable();
            // Always keep the button visible — just update the label
            label.text = count > 0 ? (count > 1 ? $"Claim All ({count})" : "Claim") : "Claim All";
            button.interactable = count > 0;
        }

        private void OnClaimAllClicked()
        {
            if (_isClaiming) return;
            StartCoroutine(ClaimAllSequence());
        }

        private IEnumerator ClaimAllSequence()
        {
            _isClaiming = true;
            button.interactable = false;

            var mgr       = BattlePassManager.Instance;
            var freeTrack = mgr.GetFreeTrack();
            var premTrack = mgr.GetPremiumTrack();

            var allClaimable = new List<BattlePassNode>();
            foreach (var n in freeTrack)
                if (n.state == NodeState.Claimable) allClaimable.Add(n);
            foreach (var n in premTrack)
                if (n.state == NodeState.Claimable && mgr.HasPremium) allClaimable.Add(n);

            // Sort by level so claims sweep left to right
            allClaimable.Sort((a, b) => a.level.CompareTo(b.level));

            foreach (var node in allClaimable)
            {
                mgr.TryClaimReward(node);
                yield return new WaitForSeconds(staggerDelay);
            }

            _isClaiming = false;
            button.interactable = true;
            RefreshVisibility();
        }

        private int CountClaimable()
        {
            if (BattlePassManager.Instance == null) return 0;

            int count = 0;
            var mgr = BattlePassManager.Instance;
            foreach (var n in mgr.GetFreeTrack())
                if (n.state == NodeState.Claimable) count++;
            if (mgr.HasPremium)
                foreach (var n in mgr.GetPremiumTrack())
                    if (n.state == NodeState.Claimable) count++;
            return count;
        }
    }
}
