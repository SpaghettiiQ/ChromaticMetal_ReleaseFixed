using Unity.Netcode;
using UnityEngine;
using _Project.Features.DifficultySystem.Scripts;
using _Project.Core.Interfaces;
using _Project.Features.StageSystem.Scripts;
using _Project.Core.Stats;
using _Project.Features.BurdenSystem.Scripts;
using _Project.Core.Enums;
using _Project.Features.WeaponCore.Scripts;
using _Project.Features.ProxyAbilities.Scripts;
using _Project.Features.WeaponCore.Enums;
using _Project.Features.Items.Scripts;
using _Project.Features.Items.Data;
using _Project.Features.LobbySystem.Scripts;
using _Project.Features.Player.Scripts;
using System.Collections.Generic;
using _Project.Core.Networking;

namespace _Project.Features.InGameUI
{
    public class InGameUIBinder : NetworkBehaviour
    {
        private float _matchTime;
        private ExtractionTerminal _activeTerminal;
        
        private CharacterStats _localPlayerStats;
        private BurdenController _localPlayerBurden;
        private AbilityController _localAbilityController;
        private NetworkWeapon _localWeapon;
        private CharacterInventory _localInventory;
        private PlayerInteractor _localInteractor;
        private _Project.Core.Events.CharacterEventBus _subscribedPickupBus;

        private TeamAffiliation? _appliedTeamAccent = null;

        private void OnEnable()
        {
            if (InGameUIController.Instance != null)
            {
                InGameUIController.Instance.OnResumeClicked += HandleResumeClicked;
                InGameUIController.Instance.OnLeaveMatchClicked += HandleLeaveMatchClicked;
            }
        }

        private void OnDisable()
        {
            if (InGameUIController.Instance != null)
            {
                InGameUIController.Instance.OnResumeClicked -= HandleResumeClicked;
                InGameUIController.Instance.OnLeaveMatchClicked -= HandleLeaveMatchClicked;
            }

            if (_subscribedPickupBus != null)
            {
                _subscribedPickupBus.OnItemPickedUp -= HandleItemPickedUp;
                _subscribedPickupBus = null;
            }
        }

        private void HandleItemPickedUp(ushort itemID)
        {
            if (InGameUIController.Instance == null || _localInventory == null || _localInventory.Database == null) return;
            ItemDefinition def = _localInventory.Database.GetItem(itemID);
            if (def == null) return;

            InGameUIController.Instance.ShowItemPickupPopup(
                def.icon,
                string.IsNullOrEmpty(def.itemName) ? def.name : def.itemName,
                def.description ?? string.Empty,
                def.rarity.GetColor(),
                def.rarity.GetDisplayName()
            );
        }

        private void RebindPickupSubscription()
        {
            var bus = _localInventory != null ? _localInventory.GetComponent<_Project.Core.Events.CharacterEventBus>() : null;
            if (bus == _subscribedPickupBus) return;

            if (_subscribedPickupBus != null)
                _subscribedPickupBus.OnItemPickedUp -= HandleItemPickedUp;

            _subscribedPickupBus = bus;

            if (_subscribedPickupBus != null)
                _subscribedPickupBus.OnItemPickedUp += HandleItemPickedUp;
        }

        private void HandleResumeClicked()
        {
            // The InGameUIController already hides the pause menu. 
            // We just need to make sure the mouse cursor is handled correctly if we are managing it based on pause state.
        }

        private void HandleLeaveMatchClicked()
        {
            // The end-screen sets Time.timeScale = 0. PlayerInputHandler subscribes to the same
            // OnLeaveMatchClicked event and also resets it, but if our handler runs first we
            // Shutdown() the NetworkManager and destroy the player object before its handler
            // gets a chance — leaving the main menu (and the next match's CharacterSelectTimer)
            // stuck at deltaTime 0.
            Time.timeScale = 1f;

            if (NetworkManager.Singleton != null)
            {
                NetworkSessionWatcher.Singleton?.MarkIntentionalShutdown();
                NetworkManager.Singleton.Shutdown();
            }
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        /// <summary>
        /// Redirects all HUD data to the spectated player instead of the local (dead) player.
        /// </summary>
        public void SetSpectateTarget(GameObject target)
        {
            _localPlayerStats       = target.GetComponentInChildren<CharacterStats>();
            _localPlayerBurden      = target.GetComponentInChildren<BurdenController>();
            _localAbilityController = target.GetComponentInChildren<AbilityController>();
            _localWeapon            = target.GetComponentInChildren<NetworkWeapon>();
            _localInventory         = target.GetComponentInChildren<CharacterInventory>();
            _localInteractor        = null; // No interact prompts while spectating
        }

        /// <summary>
        /// Forces a re-resolution of all HUD references back to the local player on the next Update tick.
        /// </summary>
        public void ClearSpectateTarget()
        {
            _localPlayerStats = null; // Triggers lazy re-resolve in Update
        }

        private void Update()
        {
            if (InGameUIController.Instance == null) return;

            if (_localPlayerStats == null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
            {
                if (NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
                {
                    _localPlayerStats = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponentInChildren<CharacterStats>();
                    _localPlayerBurden = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponentInChildren<BurdenController>();
                    _localAbilityController = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponentInChildren<AbilityController>();
                    _localWeapon = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponentInChildren<NetworkWeapon>();
                    _localInventory = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponentInChildren<CharacterInventory>();
                    _localInteractor = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponentInChildren<PlayerInteractor>();
                    
                    InGameUIController.Instance.ToggleEntireUI(true);
                }
            }

            RebindPickupSubscription();

            if (_localPlayerStats == null || !_localPlayerStats.IsSpawned) return;

            // 1. Update Match Info (Time, Stage, Loop)
            string timeStr = "00:00";
            
            if (DifficultyNetworkController.Singleton != null)
            {
                // Format match time: MM:SS
                _matchTime = DifficultyNetworkController.Singleton.MatchTime;
                int minutes = Mathf.FloorToInt(_matchTime / 60f);
                int seconds = Mathf.FloorToInt(_matchTime % 60f);
                timeStr = $"{minutes:00}:{seconds:00}";
            }

            string stageStr = "Stage 1";
            string loopStr = "Loop 1";

            if (RunNetworkController.Singleton != null)
            {
                loopStr = "Loop " + RunNetworkController.Singleton.currentLoop.Value.ToString();
                
                // Determine which stage index to show based on the local player's team. Only
                // PvP keeps the two teams on independent indices — Coop / singleplayer always
                // uses team1StageIndex as the canonical progression for whichever team is
                // playing, matching RunNetworkController.GetCurrentMapForTeam.
                bool isPvPMode = RunNetworkController.Singleton.activeRunMode.Value == GameMode.PvP;
                int stageIndex = (isPvPMode && _localPlayerStats != null && _localPlayerStats.Team == TeamAffiliation.Thrive)
                    ? RunNetworkController.Singleton.team2StageIndex.Value
                    : RunNetworkController.Singleton.team1StageIndex.Value;

                stageStr = "Stage " + (stageIndex + 1).ToString();
            }

            string objectiveStr = "Find the exit!";
            if (_activeTerminal != null)
            {
                if (_activeTerminal.IsDoorOpen.Value)
                    objectiveStr = "Enter the exit doors!";
                else if (_activeTerminal.IsCharging.Value)
                    objectiveStr = "Survive until door is charged!";
            }

            InGameUIController.Instance.UpdateMatchInfo(timeStr, stageStr, loopStr, objectiveStr);

            IDifficultyManager difficulty = DifficultyNetworkController.Singleton;
            if (difficulty != null)
            {
                InGameUIController.Instance.SetDifficulty(difficulty.CurrentLevel, difficulty.DifficultyCoefficient);
            }

            // 2. Update Teleporter/Extraction Charge
            float chargePercent = 0f;
            if (_activeTerminal == null)
            {
                // Try to find the active terminal in the scene
                _activeTerminal = FindAnyObjectByType<ExtractionTerminal>();
            }

            if (_activeTerminal != null)
            {
                if (_activeTerminal.IsDoorOpen.Value)
                {
                    chargePercent = 100f;
                }
                else if (_activeTerminal.IsCharging.Value)
                {
                    // Calculate percentage based on charge progress and total time
                    chargePercent = (_activeTerminal.ChargeProgress.Value / _activeTerminal.chargeTime) * 100f;
                }
            }
            
            InGameUIController.Instance.UpdateTeleporterCharge(chargePercent);

            // 3. Update Player Stats (HP, XP, Money, Burden, Power)
            if (_localPlayerStats != null)
            {
                if (_appliedTeamAccent != _localPlayerStats.Team)
                {
                    _appliedTeamAccent = _localPlayerStats.Team;
                    Color teamColor = Color.white;
                    if (_localPlayerStats.Team == TeamAffiliation.Cleansers)
                        teamColor = new Color(0.8805609f, 1f, 0f, 1f); // Hex #E0FF00
                    else if (_localPlayerStats.Team == TeamAffiliation.Thrive)
                        teamColor = new Color(0.9308176f, 0.061468955f, 0.061468955f, 1f); // Hex #ED1010
                    
                    InGameUIController.Instance.SetAccentColor(teamColor);
                }

                // Health bar + overshield. Scale = max(maxHp, health + shield): Shield extends the
                // bar to the right (squishing green when it overflows max); Barrier is a separate
                // amber overlay from the left, sized on the SAME scale (so 100 barrier @ 200 HP =
                // half the green). Shield/Barrier are NetworkVariables, so this reads correctly on
                // remote clients.
                int health = _localPlayerStats.CurrentHealth.Value;
                int shield = _localPlayerStats.CurrentShield.Value;
                int barrier = _localPlayerStats.CurrentBarrier.Value;
                float maxHp = _localPlayerStats.GetEffectiveMaxHealth();

                float healthPct = 1f, shieldPct = 0f, barrierPct = 0f;
                string hpText = "100 / 100";

                float scale = Mathf.Max(maxHp, health + shield);
                if (scale > 0f)
                {
                    healthPct = health / scale;
                    shieldPct = shield / scale;
                    barrierPct = barrier / scale; // overlay fraction on the same scale
                    hpText = $"{health + shield + barrier} / {Mathf.RoundToInt(maxHp)}";
                }

                float hpPercent = healthPct;

                int money = _localPlayerStats.CurrentMoney.Value;

                float xpPercent = 0f;
                string xpText = "0xp - Level 1";
                if (DifficultyNetworkController.Singleton != null)
                {
                    int currentXp = DifficultyNetworkController.Singleton.CurrentXP;
                    int level = DifficultyNetworkController.Singleton.CurrentLevel;
                    int previousLevelXp = DifficultyNetworkController.Singleton.GetXPForLevel(level);
                    int nextLevelXp = DifficultyNetworkController.Singleton.GetXPForLevel(level + 1);

                    int xpNeededForThisLevel = nextLevelXp - previousLevelXp;
                    int xpProgressCurrentLevel = currentXp - previousLevelXp;

                    if (xpNeededForThisLevel > 0)
                    {
                        xpPercent = Mathf.Clamp01((float)xpProgressCurrentLevel / xpNeededForThisLevel);
                    }
                    
                    xpText = $"{currentXp}xp - Level {level}";
                }

                float burdenPercent = 0f;
                string burdenText = "0%";
                if (_localPlayerBurden != null)
                {
                    float rawBurden = _localPlayerBurden.CurrentBurden.Value;
                    burdenPercent = Mathf.Clamp01(rawBurden / 100f);
                    
                    int bInt = Mathf.RoundToInt(rawBurden);
                    if (bInt <= 0)
                    {
                        burdenText = "0%";
                    }
                    else
                    {
                        int tier;
                        if (rawBurden < 25f) tier = 1;
                        else if (rawBurden < 50f) tier = 2;
                        else if (rawBurden < 80f) tier = 3;
                        else tier = 4;

                        burdenText = $"{bInt}% - Tier {tier}";
                    }
                }

                InGameUIController.Instance.UpdatePlayerStats(hpPercent * 100f, xpPercent * 100f, money, burdenPercent * 100f, hpText, xpText, burdenText);
                InGameUIController.Instance.UpdateOverHealth(healthPct, shieldPct, barrierPct);

                // Presence Power bar: only visible once a Burden Filter has enabled it on a
                // Thrive player. The BurdenController owns the replicated state.
                if (_localPlayerBurden != null && _localPlayerBurden.PresencePowerEnabled.Value)
                {
                    float power = _localPlayerBurden.CurrentPresencePower.Value;
                    float maxPower = Mathf.Max(0.01f, _localPlayerBurden.MaxPresencePower);
                    float powerPercent = Mathf.Clamp01(power / maxPower) * 100f;
                    InGameUIController.Instance.UpdatePower(true, powerPercent, $"{Mathf.RoundToInt(power)}");
                }
                else
                {
                    InGameUIController.Instance.UpdatePower(false);
                }
            }

            // 4. Update Abilities & Weapon
            if (_localAbilityController != null)
            {
                float GetCooldownPercent(AbilitySlot slot)
                {
                    var ability = _localAbilityController.GetAbility(slot);
                    if (ability == null || ability.cooldownTime <= 0f) return 0f;

                    float remaining = _localAbilityController.GetRemainingCooldown(slot);
                    return Mathf.Clamp01(remaining / ability.cooldownTime);
                }

                Sprite GetAbilityIcon(AbilitySlot slot)
                {
                    var ability = _localAbilityController.GetAbility(slot);
                    return ability != null ? ability.icon : null;
                }

                bool GetAbilityCanBeUsed(AbilitySlot slot)
                {
                    var ability = _localAbilityController.GetAbility(slot);
                    if (ability == null || ability.Effect == null) return false;
                    
                    return ability.Effect.CanBeUsed(_localAbilityController.gameObject);
                }

                InGameUIController.Instance.UpdateAbilities(
                    GetCooldownPercent(AbilitySlot.Secondary), GetAbilityIcon(AbilitySlot.Secondary), GetAbilityCanBeUsed(AbilitySlot.Secondary),
                    GetCooldownPercent(AbilitySlot.Utility), GetAbilityIcon(AbilitySlot.Utility), GetAbilityCanBeUsed(AbilitySlot.Utility),
                    GetCooldownPercent(AbilitySlot.Special), GetAbilityIcon(AbilitySlot.Special), GetAbilityCanBeUsed(AbilitySlot.Special)
                );

                InGameUIController.Instance.UpdateSpecialCharges(
                    _localAbilityController.GetSpecialCurrentCharges(),
                    _localAbilityController.GetSpecialMaxCharges());
            }

            if (_localWeapon != null && _localWeapon.weaponData != null)
            {
                string weaponName = _localWeapon.weaponData.weaponName;
                // Read EFFECTIVE mechanic/max so abilities like Bloodthirster (Overheat → Magazine 6)
                // flip the HUD's bar/text without mutating the weapon SO.
                AmmoMechanic effectiveMechanic = _localWeapon.EffectiveAmmoMechanic;
                bool isHeat = effectiveMechanic == AmmoMechanic.Overheat;
                bool isInfinite = effectiveMechanic == AmmoMechanic.Infinite;
                string ammoText = "";
                float heatPercent = 0f;

                if (isHeat)
                {
                    heatPercent = Mathf.Clamp01(_localWeapon.GetCurrentHeat() / _localWeapon.weaponData.maxHeat);
                }
                else if (isInfinite)
                {
                    ammoText = "∞"; // ∞
                }
                else
                {
                    ammoText = $"{_localWeapon.GetCurrentAmmo()} / {_localWeapon.EffectiveMaxAmmo}";
                }

                InGameUIController.Instance.UpdateWeapon(weaponName, ammoText, isHeat, heatPercent * 100f);
            }

            // 5. Update Tab Overview (Teams, Scoreboard, Inventory)
            UpdateTabOverviewData();

            // 6. Update Interact Prompt
            if (_localInteractor != null)
            {
                var interactable = _localInteractor.CurrentInteractable;
                if (interactable != null)
                {
                    string actionSuffix = interactable.GetPromptSuffix();
                    
                    if (interactable.CanInteract(_localInteractor.gameObject))
                    {
                        InGameUIController.Instance.UpdateInteractPrompt(true, $"Press E to {actionSuffix}");
                    }
                    else
                    {
                        InGameUIController.Instance.UpdateInteractPrompt(true, actionSuffix);
                    }
                }
                else
                {
                    InGameUIController.Instance.UpdateInteractPrompt(false);
                }
            }
            else
            {
                InGameUIController.Instance.UpdateInteractPrompt(false);
            }
        }

        private void UpdateTabOverviewData()
        {
            if (InGameUIController.Instance == null) return;

            string teamProgressText = "";
            List<string> cleansers = new List<string>();
            List<string> thrives = new List<string>();
            List<(Sprite icon, int count, string name, string description, Color rarityColor, string rarityName)> items =
                new List<(Sprite, int, string, string, Color, string)>();

            // Build Team Progress Text
            if (RunNetworkController.Singleton != null && LobbyNetworkController.Singleton != null)
            {
                bool isPvP = RunNetworkController.Singleton.activeRunMode.Value == GameMode.PvP;

                // In PvP, the two teams progress independently across stage indices. In Coop,
                // team1StageIndex is the single canonical index for whichever team is playing.
                int cleansersStage = RunNetworkController.Singleton.team1StageIndex.Value + 1;
                int thrivesStage = isPvP
                    ? RunNetworkController.Singleton.team2StageIndex.Value + 1
                    : RunNetworkController.Singleton.team1StageIndex.Value + 1;

                if (isPvP)
                {
                    teamProgressText = $"Cleansers: Stage {cleansersStage}\nTHRIVE: Stage {thrivesStage}";
                }
                else
                {
                    // Coop: show the label for whichever team the local player belongs to.
                    bool isThriveCoop = _localPlayerStats != null && _localPlayerStats.Team == TeamAffiliation.Thrive;
                    teamProgressText = isThriveCoop
                        ? $"THRIVE: Stage {thrivesStage}"
                        : $"Cleansers: Stage {cleansersStage}";
                }
                
                // Populate player lists
                foreach (var p in LobbyNetworkController.Singleton.LobbyPlayers)
                {
                    if (p.Team == TeamAffiliation.Thrive)
                        thrives.Add(p.PlayerName.ToString());
                    else
                        cleansers.Add(p.PlayerName.ToString()); // Default/Cleansers
                }
                
                // If it's not PvP and thriving is actually the active team, maybe don't show thrives as standard list?
                // Wait, the UI has two separate containers. Let the UI handle disabling it if thrives is empty (which happens in Cleanser-only coop).
                // Or if it's singleplayer/Coop Thrive, then Cleansers list is empty.
            }

            // Build Inventory
            if (_localInventory != null && _localInventory.Database != null)
            {
                foreach (var stack in _localInventory.NetworkedItems)
                {
                    ItemDefinition def = _localInventory.Database.GetItem(stack.ItemID);
                    if (def != null)
                    {
                        items.Add((
                            def.icon,
                            stack.Stacks,
                            string.IsNullOrEmpty(def.itemName) ? def.name : def.itemName,
                            def.description ?? string.Empty,
                            def.rarity.GetColor(),
                            def.rarity.GetDisplayName()
                        ));
                    }
                }
            }

            InGameUIController.Instance.UpdateTabOverviewData(teamProgressText, cleansers, thrives, items);
        }
    }
}
