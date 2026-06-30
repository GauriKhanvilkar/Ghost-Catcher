using System.Collections;
using UnityEngine;
using Photon.Pun;

namespace GhostCatcher
{
    /// <summary>
    /// Represents a sabotage-able object in the game world.
    /// States: Normal → Sabotaged → Repaired (back to Normal).
    /// Ghost sabotages, Hunters repair.
    /// Also acts as an anchor for the Ghost's Blend ability.
    /// </summary>
    public class SabotageObject : MonoBehaviourPun
    {
        public enum ObjectState { Normal, Sabotaged }

        [Header("Sprites")]
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite sabotagedSprite;

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private ParticleSystem sparkParticles;
        [SerializeField] private GameObject     redGlowObject;
        [SerializeField] private Animator       shakeAnimator;

        [Header("Nearby Props for Blend")]
        [SerializeField] private int[] blendSpriteIndices; // Indices into PropSpriteRegistry

        // State
        public ObjectState State     { get; private set; } = ObjectState.Normal;
        public bool IsNormal         => State == ObjectState.Normal;
        public bool IsSabotaged      => State == ObjectState.Sabotaged;

        // Hold-to-interact
        private Coroutine activeInteraction;
        private float     holdProgress;

        // ─── Public Interface ────────────────────────────────────────────────

        public void StartSabotage()
        {
            if (!IsNormal) return;
            if (activeInteraction != null) return;
            activeInteraction = StartCoroutine(HoldInteraction(Constants.SABOTAGE_HOLD_TIME, DoSabotage));
        }

        public void StartRepair()
        {
            if (!IsSabotaged) return;
            if (activeInteraction != null) return;
            activeInteraction = StartCoroutine(HoldInteraction(Constants.REPAIR_HOLD_TIME, DoRepair));
        }

        public void CancelInteraction()
        {
            if (activeInteraction != null)
            {
                StopCoroutine(activeInteraction);
                activeInteraction = null;
                holdProgress      = 0f;
            }
        }

        /// <summary>Returns sprite indices for props Ghost can blend into near this object.</summary>
        public int[] GetBlendSpriteIndices() => blendSpriteIndices;

        // ─── Hold Interaction ────────────────────────────────────────────────

        private IEnumerator HoldInteraction(float duration, System.Action onComplete)
        {
            holdProgress = 0f;
            while (holdProgress < 1f)
            {
                holdProgress += Time.deltaTime / duration;
                // TODO: Drive progress bar UI from holdProgress
                yield return null;
            }
            holdProgress      = 0f;
            activeInteraction = null;
            onComplete?.Invoke();
        }

        // ─── State Changes ───────────────────────────────────────────────────

        private void DoSabotage()
        {
            photonView.RPC(nameof(RPC_SetState), RpcTarget.All, (int)ObjectState.Sabotaged);
            // Tell GameManager to register this sabotage
            GameManager.Instance?.RegisterSabotage();
        }

        private void DoRepair()
        {
            photonView.RPC(nameof(RPC_SetState), RpcTarget.All, (int)ObjectState.Normal);
        }

        [PunRPC]
        private void RPC_SetState(int stateInt)
        {
            State = (ObjectState)stateInt;
            ApplyVisuals();
        }

        // ─── Visuals ─────────────────────────────────────────────────────────

        private void ApplyVisuals()
        {
            switch (State)
            {
                case ObjectState.Normal:
                    if (spriteRenderer != null) spriteRenderer.sprite = normalSprite;
                    if (spriteRenderer != null) spriteRenderer.color  = Color.white;
                    if (redGlowObject  != null) redGlowObject.SetActive(false);
                    if (sparkParticles != null) sparkParticles.Stop();
                    if (shakeAnimator  != null) shakeAnimator.SetBool("Shaking", false);
                    StartCoroutine(GreenFlash());
                    break;

                case ObjectState.Sabotaged:
                    if (spriteRenderer != null)
                    {
                        spriteRenderer.sprite = sabotagedSprite != null ? sabotagedSprite : normalSprite;
                        spriteRenderer.color  = new Color(1f, 0.3f, 0.3f);
                    }
                    if (redGlowObject  != null) redGlowObject.SetActive(true);
                    if (sparkParticles != null) sparkParticles.Play();
                    if (shakeAnimator  != null) shakeAnimator.SetBool("Shaking", true);
                    break;
            }
        }

        private IEnumerator GreenFlash()
        {
            float duration = 0.5f;
            float elapsed  = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = 1f - (elapsed / duration);
                spriteRenderer.color = Color.Lerp(Color.white, Color.green, t);
                yield return null;
            }
            spriteRenderer.color = Color.white;
        }

        // ─── Gizmo ───────────────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, Constants.BLEND_RANGE);
        }
    }
}
