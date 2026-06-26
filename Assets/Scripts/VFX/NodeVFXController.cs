using UnityEngine;
using BattlePass.Data;

namespace BattlePass.VFX
{
    /// <summary>
    /// Manages all particle systems and Material property block overrides
    /// for a single Battle Pass node.
    ///
    /// HIERARCHY UNDER VFXContainer:
    ///   ParticleGlow    — soft ambient shimmer (Claimable / Current)
    ///   ParticleShine   — looping diagonal sweep (Claimable)
    ///   ParticleClaim   — one-shot burst (triggered on claim)
    ///   ParticlePremium — floating gem dust (premium track only)
    ///   GlowImage       — UI Image with the GlowPulse shader (optional)
    ///
    /// All particles use World Space / UI Layer — tweak Sorting Layer to
    /// sit above the card background but below the icon.
    ///
    /// Performance notes
    /// ─────────────────
    /// • Particle counts are intentionally low (≤ 20 per system).
    /// • ParticleShine uses GPU instancing and simple additive blend.
    /// • No overdraw on particles covering the full screen — masked to
    ///   the node's RectTransform via a custom Stencil mask on the parent.
    /// • All systems share a single Material (BPNode_VFX) that supports
    ///   MaterialPropertyBlock tinting per-node.
    /// </summary>
    public class NodeVFXController : MonoBehaviour
    {
        [Header("Particle Systems")]
        [SerializeField] private ParticleSystem particleGlow;
        [SerializeField] private ParticleSystem particleShine;
        [SerializeField] private ParticleSystem particleClaim;
        [SerializeField] private ParticleSystem particlePremium;

        [Header("Glow Image (optional shader-driven glow ring)")]
        [SerializeField] private UnityEngine.UI.Image glowImage;

        [Header("Tint Colors")]
        [SerializeField] private Color freeClaimableColor   = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private Color premiumClaimableColor = new Color(0.7f, 0.3f, 1f, 1f);
        [SerializeField] private Color currentNodeColor     = new Color(1f, 1f, 0.6f, 1f);
        [SerializeField] private Color claimBurstColor      = new Color(1f, 0.9f, 0.3f, 1f);

        private MaterialPropertyBlock _mpb;

        // ── Lifecycle ──────────────────────────────────────────────

        private void Awake()
        {
            // Stop all particles but keep GameObjects active so children/sub-emitters work
            StopAll();
            SafeStop(particleClaim);
        }

        // ── Public API ─────────────────────────────────────────────

        public void SetState(NodeState state, bool isPremium)
        {
            // Stop all looping systems first
            StopAll();

            switch (state)
            {
                case NodeState.Locked:
                    // No VFX — intentionally quiet
                    break;

                case NodeState.Unlocked:
                    // Subtle shimmer to draw attention, short burst then idle
                    PlayShine(isPremium ? premiumClaimableColor : freeClaimableColor, looping: false);
                    break;

                case NodeState.Claimable:
                    // Ambient glow + looping shine sweep
                    PlayGlow(isPremium ? premiumClaimableColor : freeClaimableColor);
                    PlayShine(isPremium ? premiumClaimableColor : freeClaimableColor, looping: true);
                    if (isPremium && particlePremium != null)
                    {
                        ActivateWithChildren(particlePremium);
                        TintSystem(particlePremium, premiumClaimableColor);
                        particlePremium.Play(withChildren: true);
                    }
                    SetGlowImageActive(true, isPremium ? premiumClaimableColor : freeClaimableColor);
                    break;

                case NodeState.Claimed:
                    // All quiet — state is visually communicated by the stamp overlay
                    SetGlowImageActive(false, Color.clear);
                    break;

                case NodeState.Current:
                    // Slow pulse glow, no shine sweep
                    PlayGlow(currentNodeColor);
                    SetGlowImageActive(true, currentNodeColor);
                    break;
            }
        }

        /// <summary>
        /// Trigger the one-shot claim burst.
        /// Called by BattlePassNodeUI after the claim animation punch.
        /// </summary>
        public void PlayClaimBurst()
        {
            StopAll();
            SetGlowImageActive(false, Color.clear);

            if (particleClaim == null) return;

            // Enable the claim particle GameObject and all its children, then play
            ActivateWithChildren(particleClaim);
            TintSystem(particleClaim, claimBurstColor);
            particleClaim.Play(withChildren: true);
        }

        // ── Internal ───────────────────────────────────────────────

        private void PlayGlow(Color color)
        {
            if (particleGlow == null) return;
            ActivateWithChildren(particleGlow);
            TintSystem(particleGlow, color);
            particleGlow.Play(withChildren: true);
        }

        private void PlayShine(Color color, bool looping)
        {
            if (particleShine == null) return;
            ActivateWithChildren(particleShine);
            TintSystem(particleShine, color);

            // Adjust loop setting at runtime
            var main = particleShine.main;
            main.loop = looping;

            particleShine.Play(withChildren: true);
        }

        private void StopAll()
        {
            SafeStop(particleGlow);
            SafeStop(particleShine);
            SafeStop(particlePremium);
            // Do NOT stop particleClaim here — it may be mid-burst
        }

        private void SafeStop(ParticleSystem ps)
        {
            if (ps != null && ps.isPlaying)
                ps.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        /// <summary>Enables or disables all particle GameObjects at once.</summary>
        private void SetParticlesActive(bool active)
        {
            if (particleGlow != null)    particleGlow.gameObject.SetActive(active);
            if (particleShine != null)   particleShine.gameObject.SetActive(active);
            if (particleClaim != null)   particleClaim.gameObject.SetActive(active);
            if (particlePremium != null) particlePremium.gameObject.SetActive(active);
        }

        /// <summary>
        /// Ensures a particle system's GameObject and all child ParticleSystem
        /// GameObjects are active, then plays with children.
        /// </summary>
        private void ActivateWithChildren(ParticleSystem ps)
        {
            if (ps == null) return;
            if (!ps.gameObject.activeSelf)
                ps.gameObject.SetActive(true);

            // Explicitly activate all child particle GameObjects
            var childParticles = ps.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            foreach (var child in childParticles)
            {
                if (!child.gameObject.activeSelf)
                    child.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Tints the Start Color of a particle system via MaterialPropertyBlock
        /// to avoid creating material instances per node.
        /// </summary>
        private void TintSystem(ParticleSystem ps, Color color)
        {
            if (ps == null) return;

            // Unity's Particle System Start Color can be set directly
            // on the main module without allocating new materials:
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(color);
        }

        private void SetGlowImageActive(bool active, Color color)
        {
            if (glowImage == null) return;
            glowImage.gameObject.SetActive(active);
            if (!active) return;

            // Use MaterialPropertyBlock to tint the glow image shader
            // without creating a new Material instance per node.
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            var renderer = glowImage.GetComponent<CanvasRenderer>();
            // For UI images use color property directly (no MPB support on CanvasRenderer)
            glowImage.color = color;
        }

#if UNITY_EDITOR
        [ContextMenu("Preview: Set Claimable (Free)")]
        private void PreviewClaimableFree()    => SetState(NodeState.Claimable, false);
        [ContextMenu("Preview: Set Claimable (Premium)")]
        private void PreviewClaimablePremium() => SetState(NodeState.Claimable, true);
        [ContextMenu("Preview: Play Claim Burst")]
        private void PreviewClaimBurst()       => PlayClaimBurst();
        [ContextMenu("Preview: Set Current")]
        private void PreviewCurrent()          => SetState(NodeState.Current, false);
        [ContextMenu("Preview: Set Locked")]
        private void PreviewLocked()           => SetState(NodeState.Locked, false);
#endif
    }
}
