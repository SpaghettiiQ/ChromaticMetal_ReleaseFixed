using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace _Project.Features.InGameUI
{
    public class InGameUIController : MonoBehaviour
    {
        public static InGameUIController Instance { get; private set; }

        [SerializeField] private UIDocument _uiDocument;

        // --- HUD Elements ---
        private ProgressBar _teleporterChargeBar;
        private Label _matchTimeLabel;
        private Label _stageCounterLabel;
        private Label _loopCounterLabel;
        private Label _objectiveLabel;
        private Label _difficultyLevelLabel;
        private Label _difficultyCoefficientLabel;
    
        private Label _moneyLabel;
        private ProgressBar _powerBar;
        private ProgressBar _burdenBar;
        private ProgressBar _hpBar;
        private ProgressBar _xpBar;

        // Overshield fragments overlaid on the HP bar: blue Shield + amber Barrier, drawn to the
        // right of the (red/green) health fill on the SAME bar.
        private VisualElement _shieldSegment;
        private VisualElement _barrierSegment;
    
        private Label _powerLabel;
        private Label _burdenLabel;
        private Label _hpLabel;
        private Label _xpLabel;

        private VisualElement _powerContainer;

        private ProgressBar _weaponHeatBar;
        private VisualElement _ammoContainer;
        private Label _ammoLabel;
        private Label _weaponNameLabel;
    
        private VisualElement _slot1Container;
        private VisualElement _slot2Container;
        private VisualElement _slot3Container;

        private Label _ability1KeyLabel;
        private VisualElement _ability1CooldownOverlay;
        private Label _ability2KeyLabel;
        private VisualElement _ability2CooldownOverlay;
        private Label _ability3KeyLabel;
        private VisualElement _ability3CooldownOverlay;
        private Label _specialChargeLabel;

        private Label _interactPromptLabel;

        // --- Tab Overview Elements ---
        private VisualElement _tabOverviewContainer;
        private Label _teamProgressLabel;
        private VisualElement _playerListCleansersContainer;
        private VisualElement _playerListThriveContainer;
        private ScrollView _playerInventoryContainer;

        // --- Pause Menu Elements ---
        private VisualElement _pauseMenuContainer;
        private Button _resumeButton;
        private Button _leaveMatchButton;

        // --- Spectating Overlay ---
        private VisualElement _spectatingOverlay;
        private Label _spectatingLabel;

        // --- End Screen Elements ---
        private VisualElement _endScreenContainer;
        private Label _resultLabel;
        private ScrollView _endScreenItemsContainer;
        private VisualElement _statsContainer;
        private Button _endScreenLeaveMatchButton;

        // Events
        public event Action OnResumeClicked;
        public event Action OnLeaveMatchClicked;

        private InGameUIBinder _uiBinder;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            if (_uiDocument == null)
                _uiDocument = GetComponent<UIDocument>();

            var root = _uiDocument.rootVisualElement;

            // Bind HUD
            _teleporterChargeBar = root.Q<ProgressBar>("TeleporterChargeBar");
            _matchTimeLabel = root.Q<Label>("MatchTimeLabel");
            _stageCounterLabel = root.Q<Label>("StageCounterLabel");
            _loopCounterLabel = root.Q<Label>("LoopCounterLabel");
            _objectiveLabel = root.Q<Label>("ObjectiveLabel");
            _difficultyLevelLabel = root.Q<Label>("DifficultyLevelLabel");
            _difficultyCoefficientLabel = root.Q<Label>("DifficultyCoefficientLabel");
        
            _moneyLabel = root.Q<Label>("MoneyLabel");
            _powerBar = root.Q<ProgressBar>("PowerBar");
            _burdenBar = root.Q<ProgressBar>("BurdenBar");
            _hpBar = root.Q<ProgressBar>("HpBar");
            _xpBar = root.Q<ProgressBar>("XpBar");

            // Overshield visuals as absolute, picking-ignored children of the HP bar. Positions /
            // widths are set each frame in UpdateOverHealth.
            //   Shield  : blue, appended to the RIGHT of the green health fill (squishes green).
            //   Barrier : amber, an OVERLAY starting from the LEFT, drawn on top of the health fill.
            // Added shield first then barrier, so the amber overlay draws on top.
            if (_hpBar != null)
            {
                _shieldSegment = new VisualElement { name = "HpShieldSegment", pickingMode = PickingMode.Ignore };
                _shieldSegment.style.position = Position.Absolute;
                _shieldSegment.style.top = 0; _shieldSegment.style.bottom = 0;
                _shieldSegment.style.backgroundColor = new StyleColor(new Color(0.31f, 0.72f, 1f, 1f)); // #4FB8FF blue
                _shieldSegment.style.display = DisplayStyle.None;
                _hpBar.Add(_shieldSegment);

                _barrierSegment = new VisualElement { name = "HpBarrierSegment", pickingMode = PickingMode.Ignore };
                _barrierSegment.style.position = Position.Absolute;
                _barrierSegment.style.top = 0; _barrierSegment.style.bottom = 0; _barrierSegment.style.left = 0;
                _barrierSegment.style.backgroundColor = new StyleColor(new Color(1f, 0.70f, 0f, 0.85f)); // #FFB300 amber overlay
                _barrierSegment.style.display = DisplayStyle.None;
                _hpBar.Add(_barrierSegment);
            }
        
            _powerLabel = root.Q<Label>("PowerLabel");
            _burdenLabel = root.Q<Label>("BurdenLabel");
            _hpLabel = root.Q<Label>("HpLabel");
            _xpLabel = root.Q<Label>("XpLabel");

            _powerContainer = root.Q<VisualElement>("Power");

            _weaponHeatBar = root.Q<ProgressBar>("WeaponHeatBar");
            _ammoContainer = root.Q<VisualElement>("AmmoContainer");
            _ammoLabel = root.Q<Label>("AmmoLabel");
            _weaponNameLabel = root.Q<Label>("WeaponNameLabel");

            _slot1Container = root.Q<VisualElement>("Slot1Container");
            _slot2Container = root.Q<VisualElement>("Slot2Container");
            _slot3Container = root.Q<VisualElement>("Slot3Container");

            _ability1KeyLabel = root.Q<Label>("Ability1KeyLabel");
            _ability1CooldownOverlay = root.Q<VisualElement>("Ability1CooldownOverlay");
            _ability2KeyLabel = root.Q<Label>("Ability2KeyLabel");
            _ability2CooldownOverlay = root.Q<VisualElement>("Ability2CooldownOverlay");
            _ability3KeyLabel = root.Q<Label>("Ability3KeyLabel");
            _ability3CooldownOverlay = root.Q<VisualElement>("Ability3CooldownOverlay");

            // Charge counter centered in the Special slot (Slot3). Hidden unless the player has
            // more than one charge (e.g. Military Frame). Added as a child of the slot so it
            // overlays the icon + cooldown shade; picking-ignored so it never blocks input.
            if (_slot3Container != null)
            {
                _specialChargeLabel = new Label { name = "SpecialChargeLabel", pickingMode = PickingMode.Ignore };
                _specialChargeLabel.style.position = Position.Absolute;
                _specialChargeLabel.style.left = 0;
                _specialChargeLabel.style.right = 0;
                _specialChargeLabel.style.top = 0;
                _specialChargeLabel.style.bottom = 0;
                _specialChargeLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                _specialChargeLabel.style.fontSize = 30;
                _specialChargeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                _specialChargeLabel.style.color = Color.white;
                _specialChargeLabel.style.textShadow = new TextShadow
                {
                    offset = new Vector2(1f, 1f),
                    blurRadius = 2f,
                    color = new Color(0f, 0f, 0f, 0.85f)
                };
                _specialChargeLabel.style.display = DisplayStyle.None;
                _slot3Container.Add(_specialChargeLabel);
            }

            _interactPromptLabel = root.Q<Label>("InteractPromptLabel");

            // Bind Tab Overview
            _tabOverviewContainer = root.Q<VisualElement>("TabOverviewContainer");
            _teamProgressLabel = root.Q<Label>("TeamProgressLabel");
            _playerListCleansersContainer = root.Q<VisualElement>("PlayerListCleansersContainer");
            _playerListThriveContainer = root.Q<VisualElement>("PlayerListThriveContainer");
            _playerInventoryContainer = root.Q<ScrollView>("PlayerInventoryContainer");

            // Bind Pause Menu
            _pauseMenuContainer = root.Q<VisualElement>("PauseMenuContainer");
            _resumeButton = root.Q<Button>("ResumeButton");
            _leaveMatchButton = root.Q<Button>("LeaveMatchButton");

            // Bind End Screen
            _endScreenContainer = root.Q<VisualElement>("EndScreenContainer");
            _resultLabel = root.Q<Label>("ResultLabel");
            _endScreenItemsContainer = _endScreenContainer?.Q<ScrollView>("ItemsContainer");
            _statsContainer = root.Q<VisualElement>("StatsContainer");
            _endScreenLeaveMatchButton = _endScreenContainer?.Q<Button>("LeaveMatchButton");

            // Build spectating overlay dynamically (not in UXML so it always renders on top)
            _spectatingOverlay = new VisualElement();
            _spectatingOverlay.style.position = Position.Absolute;
            _spectatingOverlay.style.top = 0; _spectatingOverlay.style.left = 0;
            _spectatingOverlay.style.right = 0; _spectatingOverlay.style.bottom = 0;
            _spectatingOverlay.style.justifyContent = Justify.FlexEnd;
            _spectatingOverlay.style.alignItems = Align.Center;
            _spectatingOverlay.style.paddingBottom = 80;
            _spectatingOverlay.style.display = DisplayStyle.None;
            _spectatingOverlay.pickingMode = PickingMode.Ignore;

            _spectatingLabel = new Label("Spectating");
            _spectatingLabel.style.fontSize = 22;
            _spectatingLabel.style.color = new Color(0.9f, 0.9f, 0.9f, 0.85f);
            _spectatingLabel.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
            _spectatingLabel.style.paddingLeft = 12; _spectatingLabel.style.paddingRight = 12;
            _spectatingLabel.style.paddingTop = 4; _spectatingLabel.style.paddingBottom = 4;
            _spectatingOverlay.Add(_spectatingLabel);
            root.Add(_spectatingOverlay);

            if (_resumeButton != null) _resumeButton.clicked += HandleResumeClicked;
            if (_leaveMatchButton != null) _leaveMatchButton.clicked += HandleLeaveMatchClicked;
            if (_endScreenLeaveMatchButton != null) _endScreenLeaveMatchButton.clicked += HandleLeaveMatchClicked;

            // Initialize state
            ToggleTabOverview(false);
            TogglePauseMenu(false);
            ToggleEndScreen(false);
            ToggleEntireUI(false); // Hide initially until player loads

            if (_uiBinder == null)
            {
                _uiBinder = gameObject.AddComponent<InGameUIBinder>();
            }
        }

        private void OnDisable()
        {
            if (_resumeButton != null) _resumeButton.clicked -= HandleResumeClicked;
            if (_leaveMatchButton != null) _leaveMatchButton.clicked -= HandleLeaveMatchClicked;
            if (_endScreenLeaveMatchButton != null) _endScreenLeaveMatchButton.clicked -= HandleLeaveMatchClicked;
        }

        private void HandleResumeClicked()
        {
            TogglePauseMenu(false);
            OnResumeClicked?.Invoke();
        }

        private void HandleLeaveMatchClicked()
        {
            OnLeaveMatchClicked?.Invoke();
        }

        // --- Update Methods ---

        public void UpdateMatchInfo(string time, string stage, string loop, string objective)
        {
            if (_matchTimeLabel != null) _matchTimeLabel.text = time;
            if (_stageCounterLabel != null) _stageCounterLabel.text = stage;
            if (_loopCounterLabel != null) _loopCounterLabel.text = loop;
            if (_objectiveLabel != null) _objectiveLabel.text = objective;
        }

        public void SetDifficulty(int level, float coefficient)
        {
            if (_difficultyLevelLabel != null) _difficultyLevelLabel.text = $"Lv {level}";
            if (_difficultyCoefficientLabel != null) _difficultyCoefficientLabel.text = $"x{coefficient:0.00}";
        }

        public void UpdateTeleporterCharge(float chargePercent)
        {
            if (_teleporterChargeBar != null) _teleporterChargeBar.value = chargePercent;
        }

        public void UpdatePlayerStats(float hpPercent, float xpPercent, int money, float burdenPercent, string hpText, string xpText, string burdenText)
        {
            if (_hpBar != null) _hpBar.value = hpPercent;
            if (_xpBar != null) _xpBar.value = xpPercent;
            if (_moneyLabel != null) _moneyLabel.text = $"${money}";
            if (_burdenBar != null) _burdenBar.value = burdenPercent;

            if (_hpLabel != null) _hpLabel.text = hpText;
            if (_xpLabel != null) _xpLabel.text = xpText;
            if (_burdenLabel != null) _burdenLabel.text = burdenText;
        }

        // Positions the overshield visuals on the HP bar. Each pct is 0..1 of the bar width on the
        // shared scale = max(maxHp, health + shield). The green health fill is the bar's own fill
        // (driven by hpPercent in UpdatePlayerStats).
        //   Shield  : sits immediately right of green  [healthPct .. healthPct+shieldPct].
        //   Barrier : amber overlay from the left       [0 .. barrierPct], drawn over green.
        public void UpdateOverHealth(float healthPct, float shieldPct, float barrierPct)
        {
            if (_shieldSegment != null)
            {
                if (shieldPct > 0f)
                {
                    _shieldSegment.style.display = DisplayStyle.Flex;
                    _shieldSegment.style.left = Length.Percent(Mathf.Clamp01(healthPct) * 100f);
                    _shieldSegment.style.width = Length.Percent(Mathf.Clamp01(shieldPct) * 100f);
                }
                else
                {
                    _shieldSegment.style.display = DisplayStyle.None;
                }
            }

            if (_barrierSegment != null)
            {
                if (barrierPct > 0f)
                {
                    _barrierSegment.style.display = DisplayStyle.Flex;
                    _barrierSegment.style.width = Length.Percent(Mathf.Clamp01(barrierPct) * 100f);
                }
                else
                {
                    _barrierSegment.style.display = DisplayStyle.None;
                }
            }
        }

        public void UpdatePower(bool show, float powerPercent = 0f, string powerText = "")
        {
            if (_powerContainer != null)
            {
                _powerContainer.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_powerBar != null)
            {
                if (_powerContainer == null) _powerBar.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (show) 
                {
                    _powerBar.value = powerPercent;
                }
            }

            if (_powerLabel != null)
            {
                if (_powerContainer == null) _powerLabel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (show)
                {
                    _powerLabel.text = powerText;
                }
            }
        }

        public void UpdateWeapon(string name, string ammoText, bool isHeatWeapon, float heatPercent = 0f)
        {
            if (_weaponNameLabel != null) _weaponNameLabel.text = name;
        
            // Heat weapons don't use ammo: hide the whole AmmoContainer (label + ammo icon),
            // not just the label, so the icon doesn't linger next to the heat bar.
            if (_ammoContainer != null)
            {
                _ammoContainer.style.display = isHeatWeapon ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (_ammoLabel != null && !isHeatWeapon)
            {
                _ammoLabel.text = ammoText;
            }

            if (_weaponHeatBar != null)
            {
                _weaponHeatBar.style.display = isHeatWeapon ? DisplayStyle.Flex : DisplayStyle.None;
                if (isHeatWeapon) _weaponHeatBar.value = heatPercent;
            }
        }

        // Show the Special-slot charge count centered in Slot3. Hidden when the player only has
        // the default single charge (max <= 1), so it only appears once a charge item is held.
        public void UpdateSpecialCharges(int current, int max)
        {
            if (_specialChargeLabel == null) return;
            if (max > 1)
            {
                _specialChargeLabel.style.display = DisplayStyle.Flex;
                _specialChargeLabel.text = current.ToString();
            }
            else
            {
                _specialChargeLabel.style.display = DisplayStyle.None;
            }
        }

        public void UpdateAbilities(
            float ability1Percent, Sprite ability1Icon, bool canUse1,
            float ability2Percent, Sprite ability2Icon, bool canUse2,
            float ability3Percent, Sprite ability3Icon, bool canUse3)
        {
            // 100% means fully on cooldown (overlay covers slot), 0% means ready
            if (_ability1CooldownOverlay != null) _ability1CooldownOverlay.style.height = Length.Percent(ability1Percent * 100f);
            if (_ability2CooldownOverlay != null) _ability2CooldownOverlay.style.height = Length.Percent(ability2Percent * 100f);
            if (_ability3CooldownOverlay != null) _ability3CooldownOverlay.style.height = Length.Percent(ability3Percent * 100f);

            void SetIcon(VisualElement slot, Sprite icon, bool canUse, float cdPercent)
            {
                if (slot != null)
                {
                    if (icon != null) {
                        try { slot.style.backgroundImage = new StyleBackground(icon); } catch {}
                    } else {
                        slot.style.backgroundImage = null;
                        slot.style.backgroundColor = new Color(0, 0, 0, 0.5f);
                    }
                    
                    // Dim the slot if it cannot be used currently (and isn't just on cooldown)
                    if (!canUse && cdPercent <= 0f)
                    {
                        slot.style.opacity = 0.3f; // Grayed out
                    }
                    else
                    {
                        slot.style.opacity = 1.0f; // Normal
                    }
                }
            }

            SetIcon(_slot1Container, ability1Icon, canUse1, ability1Percent);
            SetIcon(_slot2Container, ability2Icon, canUse2, ability2Percent);
            SetIcon(_slot3Container, ability3Icon, canUse3, ability3Percent);
        }

        public void UpdateInteractPrompt(bool show, string text = "")
        {
            if (_interactPromptLabel != null)
            {
                _interactPromptLabel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (show)
                {
                    _interactPromptLabel.text = text;
                }
            }
        }

        // --- Toggles ---

        public void ToggleTabOverview(bool show)
        {
            if (_tabOverviewContainer != null)
                _tabOverviewContainer.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) HideItemTooltip();
        }

        // Hover tooltip card for the tab-overview inventory. Lazily built on first hover.
        private VisualElement _itemTooltipCard;
        private VisualElement _itemTooltipIcon;
        private Label _itemTooltipName;
        private Label _itemTooltipRarity;
        private Label _itemTooltipDescription;

        private void EnsureItemTooltipCard()
        {
            if (_itemTooltipCard != null) return;

            _itemTooltipCard = new VisualElement { name = "ItemTooltipCard", pickingMode = PickingMode.Ignore };
            _itemTooltipCard.style.position = Position.Absolute;
            _itemTooltipCard.style.minWidth = 260;
            _itemTooltipCard.style.maxWidth = 320;
            _itemTooltipCard.style.paddingTop = 10; _itemTooltipCard.style.paddingBottom = 10;
            _itemTooltipCard.style.paddingLeft = 12; _itemTooltipCard.style.paddingRight = 12;
            _itemTooltipCard.style.backgroundColor = new StyleColor(new Color(0.06f, 0.06f, 0.08f, 0.95f));
            _itemTooltipCard.style.borderTopWidth = 1; _itemTooltipCard.style.borderBottomWidth = 1;
            _itemTooltipCard.style.borderLeftWidth = 1; _itemTooltipCard.style.borderRightWidth = 1;
            var border = new StyleColor(new Color(0.4f, 0.4f, 0.45f, 1f));
            _itemTooltipCard.style.borderTopColor = border; _itemTooltipCard.style.borderBottomColor = border;
            _itemTooltipCard.style.borderLeftColor = border; _itemTooltipCard.style.borderRightColor = border;
            _itemTooltipCard.style.display = DisplayStyle.None;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 6;

            _itemTooltipIcon = new VisualElement();
            _itemTooltipIcon.style.width = 40; _itemTooltipIcon.style.height = 40;
            _itemTooltipIcon.style.marginRight = 8;
            header.Add(_itemTooltipIcon);

            _itemTooltipName = new Label();
            _itemTooltipName.style.fontSize = 16;
            _itemTooltipName.style.unityFontStyleAndWeight = FontStyle.Bold;
            _itemTooltipName.style.color = Color.white;
            _itemTooltipName.style.flexGrow = 1;
            _itemTooltipName.style.whiteSpace = WhiteSpace.Normal;
            header.Add(_itemTooltipName);

            _itemTooltipCard.Add(header);

            _itemTooltipRarity = new Label();
            _itemTooltipRarity.style.fontSize = 11;
            _itemTooltipRarity.style.unityFontStyleAndWeight = FontStyle.Bold;
            _itemTooltipRarity.style.marginBottom = 6;
            _itemTooltipCard.Add(_itemTooltipRarity);

            _itemTooltipDescription = new Label();
            _itemTooltipDescription.style.fontSize = 13;
            _itemTooltipDescription.style.color = new StyleColor(new Color(0.85f, 0.85f, 0.85f, 1f));
            _itemTooltipDescription.style.whiteSpace = WhiteSpace.Normal;
            _itemTooltipCard.Add(_itemTooltipDescription);

            // Attach to root so the card floats over everything in the tab overview.
            if (_uiDocument != null && _uiDocument.rootVisualElement != null)
                _uiDocument.rootVisualElement.Add(_itemTooltipCard);
        }

        private void ShowItemTooltip(VisualElement anchor, Sprite icon, string name, string description, Color rarityColor, string rarityName)
        {
            EnsureItemTooltipCard();
            if (_itemTooltipCard == null) return;

            if (icon != null)
            {
                try { _itemTooltipIcon.style.backgroundImage = new StyleBackground(icon); } catch {}
                _itemTooltipIcon.style.display = DisplayStyle.Flex;
            }
            else
            {
                _itemTooltipIcon.style.display = DisplayStyle.None;
            }

            _itemTooltipName.text = name;
            _itemTooltipName.style.color = rarityColor;
            _itemTooltipRarity.text = rarityName;
            _itemTooltipRarity.style.color = rarityColor;
            _itemTooltipDescription.text = description;

            var border = new StyleColor(rarityColor);
            _itemTooltipCard.style.borderTopColor = border; _itemTooltipCard.style.borderBottomColor = border;
            _itemTooltipCard.style.borderLeftColor = border; _itemTooltipCard.style.borderRightColor = border;

            // Position the card to the right of the hovered slot, clamped to the root bounds.
            var root = _uiDocument.rootVisualElement;
            Rect anchorRect = anchor.worldBound;
            float cardWidth = _itemTooltipCard.resolvedStyle.width > 0 ? _itemTooltipCard.resolvedStyle.width : 280f;
            float cardHeight = _itemTooltipCard.resolvedStyle.height > 0 ? _itemTooltipCard.resolvedStyle.height : 120f;
            float rootWidth = root.worldBound.width;
            float rootHeight = root.worldBound.height;

            float x = anchorRect.xMax + 8f;
            if (x + cardWidth > rootWidth) x = anchorRect.xMin - cardWidth - 8f;
            if (x < 0) x = Mathf.Max(0, anchorRect.xMin);

            float y = anchorRect.yMin;
            if (y + cardHeight > rootHeight) y = Mathf.Max(0, rootHeight - cardHeight - 4f);

            _itemTooltipCard.style.left = x;
            _itemTooltipCard.style.top = y;
            _itemTooltipCard.style.display = DisplayStyle.Flex;
            _itemTooltipCard.BringToFront();
        }

        private void HideItemTooltip()
        {
            if (_itemTooltipCard != null) _itemTooltipCard.style.display = DisplayStyle.None;
        }

        // --- HUD pickup popup ---

        private VisualElement _pickupPopupCard;
        private VisualElement _pickupPopupIcon;
        private Label _pickupPopupName;
        private Label _pickupPopupRarity;
        private Label _pickupPopupDescription;
        private Coroutine _pickupPopupHideRoutine;

        private void EnsurePickupPopupCard()
        {
            if (_pickupPopupCard != null) return;

            _pickupPopupCard = new VisualElement { name = "ItemPickupPopup", pickingMode = PickingMode.Ignore };
            _pickupPopupCard.style.position = Position.Absolute;
            _pickupPopupCard.style.bottom = 80;
            _pickupPopupCard.style.minWidth = 280;
            _pickupPopupCard.style.maxWidth = 360;
            _pickupPopupCard.style.paddingTop = 10; _pickupPopupCard.style.paddingBottom = 10;
            _pickupPopupCard.style.paddingLeft = 12; _pickupPopupCard.style.paddingRight = 12;
            _pickupPopupCard.style.backgroundColor = new StyleColor(new Color(0.06f, 0.06f, 0.08f, 0.95f));
            _pickupPopupCard.style.borderTopWidth = 1; _pickupPopupCard.style.borderBottomWidth = 1;
            _pickupPopupCard.style.borderLeftWidth = 1; _pickupPopupCard.style.borderRightWidth = 1;
            _pickupPopupCard.style.display = DisplayStyle.None;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 6;

            _pickupPopupIcon = new VisualElement();
            _pickupPopupIcon.style.width = 40; _pickupPopupIcon.style.height = 40;
            _pickupPopupIcon.style.marginRight = 8;
            header.Add(_pickupPopupIcon);

            _pickupPopupName = new Label();
            _pickupPopupName.style.fontSize = 16;
            _pickupPopupName.style.unityFontStyleAndWeight = FontStyle.Bold;
            _pickupPopupName.style.flexGrow = 1;
            _pickupPopupName.style.whiteSpace = WhiteSpace.Normal;
            header.Add(_pickupPopupName);

            _pickupPopupCard.Add(header);

            _pickupPopupRarity = new Label();
            _pickupPopupRarity.style.fontSize = 11;
            _pickupPopupRarity.style.unityFontStyleAndWeight = FontStyle.Bold;
            _pickupPopupRarity.style.marginBottom = 6;
            _pickupPopupCard.Add(_pickupPopupRarity);

            _pickupPopupDescription = new Label();
            _pickupPopupDescription.style.fontSize = 13;
            _pickupPopupDescription.style.color = new StyleColor(new Color(0.85f, 0.85f, 0.85f, 1f));
            _pickupPopupDescription.style.whiteSpace = WhiteSpace.Normal;
            _pickupPopupCard.Add(_pickupPopupDescription);

            if (_uiDocument != null && _uiDocument.rootVisualElement != null)
                _uiDocument.rootVisualElement.Add(_pickupPopupCard);
        }

        public void ShowItemPickupPopup(Sprite icon, string name, string description, Color rarityColor, string rarityName)
        {
            EnsurePickupPopupCard();
            if (_pickupPopupCard == null) return;

            if (icon != null)
            {
                try { _pickupPopupIcon.style.backgroundImage = new StyleBackground(icon); } catch {}
                _pickupPopupIcon.style.display = DisplayStyle.Flex;
            }
            else
            {
                _pickupPopupIcon.style.display = DisplayStyle.None;
            }

            _pickupPopupName.text = name;
            _pickupPopupName.style.color = rarityColor;
            _pickupPopupRarity.text = rarityName;
            _pickupPopupRarity.style.color = rarityColor;
            _pickupPopupDescription.text = description;

            var border = new StyleColor(rarityColor);
            _pickupPopupCard.style.borderTopColor = border; _pickupPopupCard.style.borderBottomColor = border;
            _pickupPopupCard.style.borderLeftColor = border; _pickupPopupCard.style.borderRightColor = border;

            // Center horizontally. Done each show so it survives resolution changes between popups.
            var root = _uiDocument.rootVisualElement;
            float rootWidth = root.worldBound.width;
            float cardWidth = _pickupPopupCard.resolvedStyle.width > 0 ? _pickupPopupCard.resolvedStyle.width : 320f;
            _pickupPopupCard.style.left = Mathf.Max(0f, (rootWidth - cardWidth) * 0.5f);

            _pickupPopupCard.style.display = DisplayStyle.Flex;
            _pickupPopupCard.BringToFront();

            if (_pickupPopupHideRoutine != null) StopCoroutine(_pickupPopupHideRoutine);
            _pickupPopupHideRoutine = StartCoroutine(HidePickupPopupAfter(2f));
        }

        private System.Collections.IEnumerator HidePickupPopupAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (_pickupPopupCard != null) _pickupPopupCard.style.display = DisplayStyle.None;
            _pickupPopupHideRoutine = null;
        }

        public void UpdateTabOverviewData(string teamProgressText, System.Collections.Generic.List<string> cleansers, System.Collections.Generic.List<string> thrives, System.Collections.Generic.List<(Sprite icon, int count, string name, string description, Color rarityColor, string rarityName)> items)
        {
            if (_tabOverviewContainer == null || _tabOverviewContainer.style.display == DisplayStyle.None)
                return; // Optimization: Only update UI elements if the tab overview is actually visible. 

            if (_teamProgressLabel != null)
                _teamProgressLabel.text = teamProgressText;

            // ...existing code for lists
            if (_playerListCleansersContainer != null)
            {
                _playerListCleansersContainer.Clear();
                foreach (var p in cleansers)
                {
                    var label = new Label(p);
                    label.style.color = Color.yellow;
                    _playerListCleansersContainer.Add(label);
                }
            }

            if (_playerListThriveContainer != null)
            {
                if (thrives == null || thrives.Count == 0)
                {
                    _playerListThriveContainer.style.display = DisplayStyle.None;
                }
                else
                {
                    _playerListThriveContainer.style.display = DisplayStyle.Flex;
                    _playerListThriveContainer.Clear();
                    foreach (var p in thrives)
                    {
                        var label = new Label(p);
                        label.style.color = Color.red; // Or whatever color represents Thrive
                        _playerListThriveContainer.Add(label);
                    }
                }
            }

            if (_playerInventoryContainer != null && items != null)
            {
                _playerInventoryContainer.Clear();
                HideItemTooltip(); // Hide any leftover hover card from a prior frame's slot.

                VisualElement itemContainer = new VisualElement();
                itemContainer.style.flexDirection = FlexDirection.Row;
                itemContainer.style.flexWrap = Wrap.Wrap;

                foreach (var item in items)
                {
                    VisualElement slot = new VisualElement();
                    slot.style.width = 64;
                    slot.style.height = 64;
                    slot.style.marginRight = 4;
                    slot.style.marginBottom = 4;
                    // Border tinted by rarity so the slot reads as the item's tier even before hover.
                    slot.style.borderTopWidth = 2; slot.style.borderBottomWidth = 2;
                    slot.style.borderLeftWidth = 2; slot.style.borderRightWidth = 2;
                    var slotBorder = new StyleColor(item.rarityColor);
                    slot.style.borderTopColor = slotBorder; slot.style.borderBottomColor = slotBorder;
                    slot.style.borderLeftColor = slotBorder; slot.style.borderRightColor = slotBorder;

                    if (item.icon != null)
                    {
                        try {
                            slot.style.backgroundImage = new StyleBackground(item.icon);
                        } catch {}
                    }
                    else
                    {
                        slot.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                    }

                    // Rarity indicator pip in the upper-right corner.
                    var rarityPip = new VisualElement { pickingMode = PickingMode.Ignore };
                    rarityPip.style.position = Position.Absolute;
                    rarityPip.style.top = 2;
                    rarityPip.style.right = 2;
                    rarityPip.style.width = 10;
                    rarityPip.style.height = 10;
                    rarityPip.style.backgroundColor = new StyleColor(item.rarityColor);
                    var pipBorder = new StyleColor(new Color(0f, 0f, 0f, 0.8f));
                    rarityPip.style.borderTopWidth = 1; rarityPip.style.borderBottomWidth = 1;
                    rarityPip.style.borderLeftWidth = 1; rarityPip.style.borderRightWidth = 1;
                    rarityPip.style.borderTopColor = pipBorder; rarityPip.style.borderBottomColor = pipBorder;
                    rarityPip.style.borderLeftColor = pipBorder; rarityPip.style.borderRightColor = pipBorder;
                    slot.Add(rarityPip);

                    if (item.count > 1)
                    {
                        Label countLabel = new Label($"x{item.count}");
                        countLabel.style.position = Position.Absolute;
                        countLabel.style.bottom = 0;
                        countLabel.style.right = 0;
                        countLabel.style.color = Color.white;
                        countLabel.style.backgroundColor = new Color(0, 0, 0, 0.5f);
                        countLabel.pickingMode = PickingMode.Ignore;
                        slot.Add(countLabel);
                    }

                    // Capture for closure — tuple fields aren't directly closurable across iterations.
                    Sprite icon = item.icon;
                    string name = item.name;
                    string desc = item.description;
                    Color rColor = item.rarityColor;
                    string rName = item.rarityName;

                    slot.RegisterCallback<MouseEnterEvent>(_ => ShowItemTooltip(slot, icon, name, desc, rColor, rName));
                    slot.RegisterCallback<MouseLeaveEvent>(_ => HideItemTooltip());

                    itemContainer.Add(slot);
                }

                _playerInventoryContainer.Add(itemContainer);
            }
        }

        public void TogglePauseMenu(bool show)
        {
            if (_pauseMenuContainer != null)
                _pauseMenuContainer.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void ToggleEndScreen(bool show)
        {
            if (_endScreenContainer != null)
                _endScreenContainer.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void ShowSpectatingOverlay(string targetPlayerName)
        {
            if (_spectatingLabel != null) _spectatingLabel.text = $"Spectating  {targetPlayerName}";
            if (_spectatingOverlay != null) _spectatingOverlay.style.display = DisplayStyle.Flex;
        }

        public void HideSpectatingOverlay()
        {
            if (_spectatingOverlay != null) _spectatingOverlay.style.display = DisplayStyle.None;
        }

        public void ShowEndScreen(bool isVictory, System.Collections.Generic.List<(Sprite icon, int count)> items, string statsText)
        {
            HideSpectatingOverlay();
            ToggleEndScreen(true);
            TogglePauseMenu(false);
            ToggleTabOverview(false);

            if (_resultLabel != null)
            {
                _resultLabel.text = isVictory ? "Victory" : "Defeat";
                _resultLabel.style.color = isVictory ? Color.green : Color.red;
            }

            if (_statsContainer != null)
            {
                _statsContainer.Clear();
                var statsLabel = new Label(statsText);
                statsLabel.style.whiteSpace = WhiteSpace.Normal;
                statsLabel.style.color = Color.white;
                statsLabel.style.fontSize = 24;
                _statsContainer.Add(statsLabel);
            }

            if (_endScreenItemsContainer != null && items != null)
            {
                _endScreenItemsContainer.Clear();
                
                VisualElement itemContainer = new VisualElement();
                itemContainer.style.flexDirection = FlexDirection.Row;
                itemContainer.style.flexWrap = Wrap.Wrap;

                foreach (var item in items)
                {
                    VisualElement slot = new VisualElement();
                    slot.style.width = 64;
                    slot.style.height = 64;
                    slot.style.marginRight = 4;
                    slot.style.marginBottom = 4;
                    
                    if (item.icon != null)
                    {
                        try { slot.style.backgroundImage = new StyleBackground(item.icon); } catch {}
                    }
                    else
                    {
                        slot.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                    }

                    if (item.count > 1)
                    {
                        Label countLabel = new Label($"x{item.count}");
                        countLabel.style.position = Position.Absolute;
                        countLabel.style.bottom = 0;
                        countLabel.style.right = 0;
                        countLabel.style.color = Color.white;
                        countLabel.style.backgroundColor = new Color(0, 0, 0, 0.5f);
                        slot.Add(countLabel);
                    }

                    itemContainer.Add(slot);
                }

                _endScreenItemsContainer.Add(itemContainer);
            }
        }

        public void ToggleEntireUI(bool show)
        {
            if (_uiDocument != null && _uiDocument.rootVisualElement != null)
            {
                _uiDocument.rootVisualElement.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public void SetAccentColor(Color color)
        {
            if (_uiDocument == null || _uiDocument.rootVisualElement == null) return;

            var glassPanels = _uiDocument.rootVisualElement.Query<VisualElement>(className: "glass-panel").ToList();
            foreach (var panel in glassPanels)
            {
                panel.style.borderTopColor = color;
            }

            var hudPanels = _uiDocument.rootVisualElement.Query<VisualElement>(className: "hud-panel").ToList();
            foreach (var panel in hudPanels)
            {
                panel.style.borderTopColor = color;
            }

            if (_resumeButton != null)
            {
                _resumeButton.style.borderTopColor = color;
                _resumeButton.style.borderLeftColor = color;
                _resumeButton.style.borderRightColor = color;
                _resumeButton.style.borderBottomColor = color;
            }
            if (_leaveMatchButton != null)
            {
                _leaveMatchButton.style.borderTopColor = color;
                _leaveMatchButton.style.borderLeftColor = color;
                _leaveMatchButton.style.borderRightColor = color;
                _leaveMatchButton.style.borderBottomColor = color;
            }
        }
    }
}
