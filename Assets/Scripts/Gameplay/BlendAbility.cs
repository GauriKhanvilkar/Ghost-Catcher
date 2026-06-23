using System.Collections;
using UnityEngine;
using Photon.Pun;

namespace GhostCatcher
{
    /// <summary>
    /// Ghost-only ability. Press B to blend into a nearby prop.
    /// Requires Ghost to be within BLEND_RANGE of a SabotageObject.
    /// Syncs sprite change to all clients via RPC.
    /// </summary>
    public class BlendAbility : MonoBehaviourPun
    {
        public bool IsBlended       { get; private set; }
        public float CooldownRemaining { get; private set; }
        public float DurationRemaining { get; private set; }

        // Events
        public System.Action<bool>  OnBlendStateChanged;  // true = blended, false = unblended
        public System.Action<float> OnCooldownUpdated;    // 0–1 normalized
        public System.Action<float> OnDurationUpdated;    // 0–1 normalized

        private PlayerNetwork playerNetwork;
        private bool          onCooldown;
        private Coroutine     blendTimer;
        private Coroutine     cooldownTimer;

        private void Awake()
        {
            playerNetwork = GetComponent<PlayerNetwork>();
        }

        private void Update()
        {
            // Update cooldown UI
            if (onCooldown)
                OnCooldownUpdated?.Invoke(1f - CooldownRemaining / Constants.BLEND_COOLDOWN);
        }

        // ─── Public Interface ────────────────────────────────────────────────

        public void ToggleBlend()
        {
            if (IsBlended)
                Unblend();
            else
                TryBlend();
        }

        private void TryBlend()
        {
            if (onCooldown)
            {
                Debug.Log("[BlendAbility] Still on cooldown.");
                return;
            }

            // Find nearest SabotageObject in range
            SabotageObject target = FindNearestSabotageObject();
            if (target == null)
            {
                Debug.Log("[BlendAbility] No sabotage object in range.");
                return;
            }

            // Pick a prop sprite index from the object's list
            int[] indices = target.GetBlendSpriteIndices();
            if (indices == null || indices.Length == 0)
            {
                Debug.LogWarning("[BlendAbility] No blend sprites assigned to this object.");
                return;
            }

            int chosenIndex = indices[Random.Range(0, indices.Length)];
            ActivateBlend(chosenIndex);
        }

        private void ActivateBlend(int spriteIndex)
        {
            IsBlended = true;
            OnBlendStateChanged?.Invoke(true);

            // Sync sprite to all clients
            photonView.RPC(nameof(PlayerNetwork.RPC_SetBlendSprite),
                           RpcTarget.All, true, spriteIndex);

            // Start duration countdown
            if (blendTimer != null) StopCoroutine(blendTimer);
            blendTimer = StartCoroutine(BlendDurationTimer());

            Debug.Log($"[BlendAbility] Blended as sprite index {spriteIndex}");
        }

        private void Unblend()
        {
            if (!IsBlended) return;

            IsBlended = false;
            OnBlendStateChanged?.Invoke(false);

            // Sync restore to all clients (spriteIndex -1 = restore)
            photonView.RPC(nameof(PlayerNetwork.RPC_SetBlendSprite),
                           RpcTarget.All, false, -1);

            // Start cooldown
            if (blendTimer != null)
            {
                StopCoroutine(blendTimer);
                blendTimer = null;
            }
            if (cooldownTimer != null) StopCoroutine(cooldownTimer);
            cooldownTimer = StartCoroutine(CooldownTimer());

            Debug.Log("[BlendAbility] Unblended.");
        }

        // ─── Timers ──────────────────────────────────────────────────────────

        private IEnumerator BlendDurationTimer()
        {
            float elapsed = 0f;
            while (elapsed < Constants.BLEND_DURATION)
            {
                elapsed          += Time.deltaTime;
                DurationRemaining = Constants.BLEND_DURATION - elapsed;
                OnDurationUpdated?.Invoke(elapsed / Constants.BLEND_DURATION);
                yield return null;
            }
            // Auto-unblend when duration expires
            Unblend();
        }

        private IEnumerator CooldownTimer()
        {
            onCooldown        = true;
            CooldownRemaining = Constants.BLEND_COOLDOWN;

            while (CooldownRemaining > 0f)
            {
                CooldownRemaining -= Time.deltaTime;
                OnCooldownUpdated?.Invoke(1f - CooldownRemaining / Constants.BLEND_COOLDOWN);
                yield return null;
            }

            onCooldown        = false;
            CooldownRemaining = 0f;
            Debug.Log("[BlendAbility] Cooldown ready.");
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        private SabotageObject FindNearestSabotageObject()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(
                transform.position, Constants.BLEND_RANGE);

            SabotageObject nearest     = null;
            float          nearestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                if (!hit.CompareTag(Constants.TAG_SABOTAGE_OBJECT)) continue;

                var obj  = hit.GetComponent<SabotageObject>();
                if (obj == null) continue;

                float dist = Vector2.Distance(transform.position, hit.transform.position);
                if (dist < nearestDist)
                {
                    nearest     = obj;
                    nearestDist = dist;
                }
            }

            return nearest;
        }
    }
}
