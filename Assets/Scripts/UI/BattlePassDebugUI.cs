using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BattlePass.Data;

namespace BattlePass.UI
{
    /// <summary>
    /// In-game debug panel for testing the Battle Pass at runtime.
    /// Provides buttons to add XP, reset progress, and toggle premium.
    ///
    /// Attach to a Canvas child GameObject. Wire the buttons + label in Inspector.
    ///
    /// HIERARCHY:
    /// DebugPanel (this)
    ///  ├── AddXPButton    (Button)
    ///  ├── ResetButton     (Button)
    ///  ├── PremiumButton   (Button)
    ///  └── StatusText      (TMP)
    /// </summary>
    public class BattlePassDebugUI : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button addXPButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button premiumButton;

        [Header("Settings")]
        [Tooltip("Amount of XP added per AddXP button press.")]
        [SerializeField] private int xpPerClick = 200;

        [Header("Display")]
        [SerializeField] private TextMeshProUGUI statusText;

        private bool _isPremium;

        private void Start()
        {
            if (addXPButton != null)
                addXPButton.onClick.AddListener(OnAddXPClicked);

            if (resetButton != null)
                resetButton.onClick.AddListener(OnResetClicked);

            if (premiumButton != null)
                premiumButton.onClick.AddListener(OnPremiumClicked);

            // Subscribe to XP changes to keep the status label fresh
            var mgr = BattlePassManager.Instance;
            if (mgr != null)
                mgr.OnXPChanged += (_, _, _) => RefreshStatus();
        }

        private void OnDestroy()
        {
            var mgr = BattlePassManager.Instance;
            if (mgr != null)
                mgr.OnXPChanged -= (_, _, _) => RefreshStatus();
        }

        private void OnEnable() => RefreshStatus();

        // ── Button Handlers ────────────────────────────────────────

        private void OnAddXPClicked()
        {
            var mgr = BattlePassManager.Instance;
            if (mgr != null) mgr.AddXP(xpPerClick);
            RefreshStatus();
        }

        private void OnResetClicked()
        {
            var mgr = BattlePassManager.Instance;
            if (mgr != null) mgr.ResetXP();
            _isPremium = false;
            RefreshStatus();
        }

        private void OnPremiumClicked()
        {
            var mgr = BattlePassManager.Instance;
            if (mgr == null) return;

            _isPremium = !_isPremium;
            if (_isPremium)
                mgr.UnlockPremium();
            else
                Debug.Log("[DebugUI] Premium cannot be revoked via UnlockPremium; use Reset to clear.");

            RefreshStatus();
        }

        // ── Status Label ───────────────────────────────────────────

        private void RefreshStatus()
        {
            var mgr = BattlePassManager.Instance;
            if (mgr == null || statusText == null) return;

            statusText.text =
                $"XP: {mgr.XPInCurrentLevel}/{mgr.XPNeededPerLevel}\n" +
                $"Level: {mgr.CurrentLevel}\n" +
                $"Premium: {(mgr.HasPremium ? "ON" : "OFF")}";
        }
    }
}