using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Features.ProxyAbilities.Data;
using _Project.Features.ProxyAbilities.Structs;

namespace _Project.Features.ProxyAbilities.Scripts
{
    public class AbilityController : NetworkBehaviour, IAbilityController
    {
        [Header("Current Loadout")]
        [SerializeField] private ProxyAbilityLoadout _currentLoadout;

        [Header("Audio Settings")]
        [SerializeField] private AudioSource _audioSource; // Attach an AudioSource to the Base Player Prefab

        // Local Cooldown Tracking (Client prediction & UI)
        private Dictionary<AbilitySlot, float> _localCooldownEnds = new Dictionary<AbilitySlot, float>
        {
            { AbilitySlot.Secondary, 0f },
            { AbilitySlot.Utility, 0f },
            { AbilitySlot.Special, 0f }
        };

        // Server Cooldown Tracking (Anti-cheat / Source of truth)
        private Dictionary<AbilitySlot, float> _serverCooldownEnds = new Dictionary<AbilitySlot, float>
        {
            { AbilitySlot.Secondary, 0f },
            { AbilitySlot.Utility, 0f },
            { AbilitySlot.Special, 0f }
        };

        public event Action<AbilitySlot, ProxyAbilityData> OnAbilityEquipped;
        public event Action<AbilitySlot, float> OnAbilityUsed;

        // --- Special-ability charges (Military Frame, etc.) ---
        // Server-authoritative + replicated so non-host owners read correct values despite the
        // CharacterStats "modifiers don't replicate" latent bug. The Special slot only uses
        // charges when SpecialMaxCharges > 1; at 1 it keeps the original single-cooldown path
        // untouched. The recharge clock reuses _serverCooldownEnds/_localCooldownEnds[Special],
        // so the existing cooldown overlay shows the next charge filling.
        public readonly NetworkVariable<int> SpecialMaxCharges = new NetworkVariable<int>(1);
        public readonly NetworkVariable<int> SpecialCharges = new NetworkVariable<int>(1);

        private CharacterStats _stats;
        private bool _serverSpecialRechargeActive;     // a charge is currently regenerating (server)
        private float _localSpecialUseLockoutEnd;      // owner-side anti-flood between charge uses

        public override void OnNetworkSpawn()
        {
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();

            // The player's CharacterStats may sit on a sibling of this controller, so search
            // from the NetworkObject root (matches how InGameUIBinder resolves it).
            _stats = transform.root.GetComponentInChildren<CharacterStats>();

            if (IsServer)
            {
                int max = ComputeMaxSpecialCharges();
                SpecialMaxCharges.Value = max;
                SpecialCharges.Value = max;
            }
        }

        public void EquipAbility(AbilitySlot slot, IProxyAbilityData newAbility)
        {
            ProxyAbilityData internalNewAbility = (ProxyAbilityData)newAbility;
            switch (slot)
            {
                case AbilitySlot.Secondary: _currentLoadout.secondaryAbility = internalNewAbility; break;
                case AbilitySlot.Utility: _currentLoadout.utilityAbility = internalNewAbility; break;
                case AbilitySlot.Special: _currentLoadout.specialAbility = internalNewAbility; break;
            }

            // Reset local and server cooldowns when a new item/ability is equipped
            _localCooldownEnds[slot] = 0f;
            if (IsServer) _serverCooldownEnds[slot] = 0f;

            OnAbilityEquipped?.Invoke(slot, internalNewAbility);
        }

        public float GetRemainingCooldown(AbilitySlot slot)
        {
            if (_localCooldownEnds.TryGetValue(slot, out float endTime))
            {
                return Mathf.Max(0f, endTime - Time.time);
            }
            return 0f;
        }

        public void ReduceCooldown(AbilitySlot slot, float amount)
        {
            if (IsServer)
            {
                _serverCooldownEnds[slot] = Mathf.Max(Time.time, _serverCooldownEnds[slot] - amount);
                ReduceCooldownClientRpc(slot, amount);
            }
        }

        /// <summary>
        /// Explicitly start (or reset) the cooldown on a slot. Used by effects that opted out
        /// of the cast-time cooldown via DeferCooldownOnCast — they call this when their
        /// channel/burst/etc. is genuinely complete.
        /// </summary>
        public void SetCooldown(AbilitySlot slot, float duration)
        {
            if (!IsServer) return;
            _serverCooldownEnds[slot] = Time.time + duration;
            // A deferred-cooldown Special finishing here means the consumed charge should now
            // begin regenerating off this duration.
            if (slot == AbilitySlot.Special && SpecialMaxCharges.Value > 1)
                _serverSpecialRechargeActive = true;
            SetCooldownClientRpc(slot, duration);
        }

        [ClientRpc]
        private void SetCooldownClientRpc(AbilitySlot slot, float duration)
        {
            if (IsOwner)
            {
                _localCooldownEnds[slot] = Time.time + duration;
                OnAbilityUsed?.Invoke(slot, duration);
            }
        }

        [ClientRpc]
        private void ReduceCooldownClientRpc(AbilitySlot slot, float amount)
        {
            if (IsOwner)
            {
                _localCooldownEnds[slot] = Mathf.Max(Time.time, _localCooldownEnds[slot] - amount);
                // Depending on UI logic, we could invoke an event:
                // OnAbilityUsed?.Invoke(slot, Mathf.Max(0, _localCooldownEnds[slot] - Time.time));
            }
        }

        public void ResetAllCooldowns()
        {
            float currentTime = Time.time;

            // Refill Special charges alongside the cooldown reset (e.g. between stages).
            if (IsServer)
            {
                SpecialCharges.Value = SpecialMaxCharges.Value;
                _serverSpecialRechargeActive = false;
            }

            // Server cooldowns reset
            _serverCooldownEnds[AbilitySlot.Secondary] = currentTime;
            _serverCooldownEnds[AbilitySlot.Utility] = currentTime;
            _serverCooldownEnds[AbilitySlot.Special] = currentTime;

            // Client predicted cooldowns reset
            _localCooldownEnds[AbilitySlot.Secondary] = currentTime;
            _localCooldownEnds[AbilitySlot.Utility] = currentTime;
            _localCooldownEnds[AbilitySlot.Special] = currentTime;

            // Force notify clients depending on your UI architecture
            // Typically you might invoke OnAbilityUsed with 0f cooldown to snap UI elements back
            OnAbilityUsed?.Invoke(AbilitySlot.Secondary, 0f);
            OnAbilityUsed?.Invoke(AbilitySlot.Utility, 0f);
            OnAbilityUsed?.Invoke(AbilitySlot.Special, 0f);
        }

        public ProxyAbilityData GetAbility(AbilitySlot slot)
        {
            return slot switch
            {
                AbilitySlot.Secondary => _currentLoadout.secondaryAbility,
                AbilitySlot.Utility => _currentLoadout.utilityAbility,
                AbilitySlot.Special => _currentLoadout.specialAbility,
                _ => null
            };
        }

        /// <summary>
        /// CLIENT: Try to use the ability. Predicts success locally.
        /// </summary>
        public void TryUseAbility(AbilitySlot slot)
        {
            if (!IsOwner) return;

            ProxyAbilityData ability = GetAbility(slot);
            if (ability == null || ability.Effect == null) return;

            // 0. Can the ability realistically be executed right now?
            if (!ability.Effect.CanBeUsed(gameObject)) return;

            // Special-slot charge path (e.g. Military Frame): when more than one charge exists
            // the slot is gated on charge count rather than a single cooldown, so it can be
            // fired repeatedly until charges run out. A short local lockout stops a held key
            // from flooding the server before the replicated count catches up. The recharge
            // overlay is driven by the server via SyncSpecialRechargeClientRpc.
            if (slot == AbilitySlot.Special && SpecialMaxCharges.Value > 1)
            {
                if (Time.time < _localSpecialUseLockoutEnd) return;
                if (SpecialCharges.Value <= 0) return;
                _localSpecialUseLockoutEnd = Time.time + 0.15f;
                PlayAbilityEffects(ability);
                RequestUseAbilityServerRpc(slot);
                return;
            }

            // 1. Local Prediction: Are we still on cooldown locally?
            if (Time.time < _localCooldownEnds[slot]) return;

            // 2. Predict execution (UI update & local VFX/SFX). Effects can opt out of the
            //    cast-time cooldown (e.g. Bloodthirster wants the cooldown to start when its
            //    6-shot burst + reload completes, not when the cast happens) by returning true
            //    from DeferCooldownOnCast — those effects MUST call SetCooldown themselves
            //    when they're truly done.
            bool deferCooldown = ability.Effect.DeferCooldownOnCast(gameObject);
            if (!deferCooldown)
            {
                _localCooldownEnds[slot] = Time.time + ability.cooldownTime;
                OnAbilityUsed?.Invoke(slot, ability.cooldownTime);
            }
            PlayAbilityEffects(ability);

            // 3. Ask the server to validate and execute the actual logic
            RequestUseAbilityServerRpc(slot);
        }

        /// <summary>
        /// SERVER: Validate the request and execute the core logic.
        /// </summary>
        [ServerRpc]
        private void RequestUseAbilityServerRpc(AbilitySlot slot)
        {
            ProxyAbilityData ability = GetAbility(slot);
            if (ability == null || ability.Effect == null) return;

            // 0. Can the ability realistically be executed right now?
            if (!ability.Effect.CanBeUsed(gameObject)) return;

            // Special-slot charge path: validate against the replicated charge count instead of
            // the single-slot cooldown, consume one, and (re)start the recharge clock.
            if (slot == AbilitySlot.Special && SpecialMaxCharges.Value > 1)
            {
                if (SpecialCharges.Value <= 0)
                {
                    Debug.LogWarning($"[{gameObject.name}] Special used with no charges remaining!");
                    return;
                }

                SpecialCharges.Value = Mathf.Max(0, SpecialCharges.Value - 1);

                bool deferSpecial = ability.Effect.DeferCooldownOnCast(gameObject);
                if (!deferSpecial && !_serverSpecialRechargeActive)
                    StartSpecialRecharge(ability.cooldownTime);

                ability.Effect.Execute(gameObject);

                if (TryGetComponent(out _Project.Core.Events.CharacterEventBus chargeBus))
                    chargeBus.RaiseOnAbilityUsed(slot);

                BroadcastAbilityVFXClientRpc(slot);
                return;
            }

            // 1. Server Validation (Anti-Cheat): Is the client spamming?
            // We add a tiny network buffer (e.g., 0.1f) to account for slight latency differences
            if (Time.time < _serverCooldownEnds[slot] - 0.1f) 
            {
                Debug.LogWarning($"[{gameObject.name}] Client attempted to trigger {slot} ability while on cooldown!");
                return;
            }

            // 2. Update Server Cooldown — but only if the effect didn't ask to defer the
            //    cooldown until it explicitly calls SetCooldown later.
            bool deferCooldown = ability.Effect.DeferCooldownOnCast(gameObject);
            if (!deferCooldown)
            {
                _serverCooldownEnds[slot] = Time.time + ability.cooldownTime;
            }

            // 3. Execute the actual ability logic (damage, spawning, stat buffs, etc.)
            ability.Effect.Execute(gameObject);

            if (TryGetComponent(out _Project.Core.Events.CharacterEventBus eventBus))
            {
                eventBus.RaiseOnAbilityUsed(slot);
            }

            // 4. Tell all OTHER clients to play the visual/audio effects
            BroadcastAbilityVFXClientRpc(slot);
        }

        /// <summary>
        /// ALL OTHER CLIENTS: Play the VFX/SFX for everyone who isn't the owner.
        /// </summary>
        [ClientRpc]
        private void BroadcastAbilityVFXClientRpc(AbilitySlot slot)
        {
            // The owner already predicted this locally in TryUseAbility(), so we skip them
            if (IsOwner) return;

            ProxyAbilityData ability = GetAbility(slot);
            if (ability != null)
            {
                PlayAbilityEffects(ability);
            }
        }

        [ClientRpc]
        public void ApplyHitStopClientRpc(float duration)
        {
            if (!IsOwner) return;
            _Project.Core.Managers.HitStopManager.Instance.TriggerHitStop(duration);
        }

        /// <summary>
        /// Broadcasts a LineRenderer-based tracer (same look as HitscanWeapon) so every client
        /// sees the beam. Used by abilities like Target Designator that fire a beam without
        /// routing through NetworkWeapon's own tracer path.
        /// </summary>
        [ClientRpc]
        public void PlayTracerClientRpc(Vector3 start, Vector3 end, Color color, float width, float lifetime)
        {
            _Project.Core.Managers.TracerLine.SpawnLocal(start, end, color, width, lifetime);
        }

        private void PlayAbilityEffects(ProxyAbilityData ability)
        {
            // Play Sound
            if (ability.activationSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(ability.activationSound);
            }

            // Spawn Particle VFX (e.g., a flash, a healing aura, a dash trail)
            if (ability.activationVfxPrefab != null)
            {
                // Instantiate the VFX at the player's position.
                // Note: The VFX prefab should have a self-destruct script (Destroy(gameObject, lifetime))
                Instantiate(ability.activationVfxPrefab, transform.position, transform.rotation);
            }

            // Camera shake on the casting player, mirroring NetworkWeapon.ExecuteShot's fire
            // shake. PlayAbilityEffects runs locally for the owner (TryUseAbility) and for remote
            // clients (BroadcastAbilityVFXClientRpc, which already skips the owner); gating on
            // IsOwner ensures only the caster's own camera shakes — exactly like primary fire,
            // where PlayerCamera.ApplyShake is itself owner-only.
            if (IsOwner && ability.cameraShakeDuration > 0f
                && TryGetComponent(out _Project.Core.Events.CharacterEventBus shakeBus))
            {
                shakeBus.RaiseOnCameraShake(ability.cameraShakeMagnitude, ability.cameraShakeDuration);
            }
        }

        // --- Special-ability charges ---

        private int ComputeMaxSpecialCharges()
        {
            int bonus = 0;
            if (_stats != null)
                bonus = Mathf.Max(0, Mathf.RoundToInt(_stats.GetStat(StatType.SpecialCharges)));
            return 1 + bonus;
        }

        private float GetSpecialCooldown()
        {
            var ability = GetAbility(AbilitySlot.Special);
            return ability != null ? ability.cooldownTime : 0f;
        }

        /// <summary>
        /// SERVER: (re)start the recharge clock for the next Special charge and mirror it to the
        /// owner so their cooldown overlay fills. A cooldown of 0 means charges never deplete.
        /// </summary>
        private void StartSpecialRecharge(float cd)
        {
            if (!IsServer) return;
            if (cd <= 0f)
            {
                SpecialCharges.Value = SpecialMaxCharges.Value;
                _serverSpecialRechargeActive = false;
                return;
            }
            _serverCooldownEnds[AbilitySlot.Special] = Time.time + cd;
            _serverSpecialRechargeActive = true;
            SyncSpecialRechargeClientRpc(cd);
        }

        [ClientRpc]
        private void SyncSpecialRechargeClientRpc(float cd)
        {
            if (!IsOwner) return;
            _localCooldownEnds[AbilitySlot.Special] = Time.time + cd;
        }

        private void Update()
        {
            if (!IsServer) return;
            ServerSpecialChargeTick();
        }

        /// <summary>
        /// SERVER: keep max charges in sync with the SpecialCharges stat and regenerate one
        /// charge each cooldown interval while below capacity.
        /// </summary>
        private void ServerSpecialChargeTick()
        {
            int max = ComputeMaxSpecialCharges();
            if (SpecialMaxCharges.Value != max)
            {
                int oldMax = SpecialMaxCharges.Value;
                SpecialMaxCharges.Value = max;
                if (SpecialCharges.Value > max) SpecialCharges.Value = max;
                // Gaining capacity (charge item picked up) starts filling the new slot.
                if (max > oldMax && SpecialCharges.Value < max && !_serverSpecialRechargeActive)
                    StartSpecialRecharge(GetSpecialCooldown());
            }

            if (max <= 1)
            {
                _serverSpecialRechargeActive = false;
                return;
            }

            if (_serverSpecialRechargeActive && SpecialCharges.Value < max)
            {
                float cd = GetSpecialCooldown();
                if (cd <= 0f)
                {
                    SpecialCharges.Value = max;
                    _serverSpecialRechargeActive = false;
                }
                else if (Time.time >= _serverCooldownEnds[AbilitySlot.Special])
                {
                    SpecialCharges.Value = Mathf.Min(max, SpecialCharges.Value + 1);
                    if (SpecialCharges.Value < max) StartSpecialRecharge(cd); // begin next + sync overlay
                    else _serverSpecialRechargeActive = false;
                }
            }
            else if (SpecialCharges.Value >= max)
            {
                _serverSpecialRechargeActive = false;
            }
        }

        public int GetSpecialMaxCharges() => SpecialMaxCharges.Value;
        public int GetSpecialCurrentCharges() => SpecialCharges.Value;
    }
}