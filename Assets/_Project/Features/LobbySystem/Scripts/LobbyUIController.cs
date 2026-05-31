using System;
using System.Net;
using System.Net.Sockets;
using _Project.Core.Audio;
using _Project.Core.Enums;
using _Project.Core.Networking;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UIElements;
using _Project.Features.LobbySystem.Structs;
using _Project.Features.ProxyCharacters.Data;
using _Project.Features.Journal.Scripts;
using System.Collections.Generic;
using System.Linq;

namespace _Project.Features.LobbySystem.Scripts
{
    [RequireComponent(typeof(UIDocument))]
    public class LobbyUIController : MonoBehaviour
    {
        [Header("UXML Screens")]
        [SerializeField] private VisualTreeAsset screenMainMenu;
        [SerializeField] private VisualTreeAsset screenMultiChoice;
        [SerializeField] private VisualTreeAsset screenHostSettings;
        [SerializeField] private VisualTreeAsset screenJoinNetwork;
        [SerializeField] private VisualTreeAsset screenLobby;
        [SerializeField] private VisualTreeAsset screenCharacterSelect;
        [SerializeField] private VisualTreeAsset screenJournal;

        [Header("Journal")]
        [SerializeField] private JournalDatabase journalDatabase;

        private UIDocument _uiDocument;
        private VisualElement _screenContainer;
        private GameObject _characterPreviewInstance;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            _screenContainer = _uiDocument.rootVisualElement.Q<VisualElement>("ScreenContainer");
        }

        private void Start()
        {
            // Start the game on the Main Menu
            ShowScreen(screenMainMenu, BindMainMenu);
        }

        private void OnDestroy()
        {
            ClearCharacterPreview();
        }

        // --- CORE NAVIGATION ENGINE ---
        
        // This clears the container, loads the new UXML, and runs the specific binding function
        private void ShowScreen(VisualTreeAsset newScreen, Action<VisualElement> bindAction = null)
        {
            ClearCharacterPreview();
            _screenContainer.Clear();
            VisualElement screenInstance = newScreen.Instantiate();
            screenInstance.style.flexGrow = 1; // Make sure it fills the container
            _screenContainer.Add(screenInstance);

            bindAction?.Invoke(screenInstance);
            WireButtonSfx(screenInstance);
        }

        // Walks the tree and attaches hover/click SFX to every Button. Re-call when a screen
        // dynamically adds buttons after first bind (see kick button + ready button blocks).
        private static void WireButtonSfx(VisualElement root)
        {
            if (root == null) return;
            root.Query<Button>().ForEach(AttachButtonSfx);
        }

        private static void AttachButtonSfx(Button btn)
        {
            // Avoid double-wiring if a screen is re-bound.
            if (btn.userData as string == "sfx") return;
            btn.userData = "sfx";

            // Borderless redesign: a red '>' appears to the left of the label on hover.
            // We prepend it to the button's own rich-text string rather than adding a child
            // element -- a child element breaks Button auto-sizing (the box collapses to the
            // padding, shrinking the hitbox and letting labels overflow/overlap). Rich text
            // keeps the arrow red while the label keeps its own color.
            const string hoverPrefix = "<color=#FF1E2B>></color> ";

            btn.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (!btn.text.StartsWith(hoverPrefix)) btn.text = hoverPrefix + btn.text;
                var sfx = SfxManager.Instance;
                if (sfx != null && sfx.Library != null)
                    sfx.PlayOneShot2D(sfx.Library.uiHover, 0.5f, 1f, true);
            });
            btn.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (btn.text.StartsWith(hoverPrefix)) btn.text = btn.text.Substring(hoverPrefix.Length);
            });
            btn.clicked += () =>
            {
                var sfx = SfxManager.Instance;
                if (sfx != null && sfx.Library != null)
                    sfx.PlayOneShot2D(sfx.Library.uiClick, 0.7f, 1f, true);
            };
        }

        private void ClearCharacterPreview()
        {
            if (_characterPreviewInstance != null)
            {
                Destroy(_characterPreviewInstance);
                _characterPreviewInstance = null;
            }
        }

        private void ShowCharacterPreview(ProxyCharacterData proxyData)
        {
            ClearCharacterPreview();

            if (proxyData == null || proxyData.visualPrefab == null)
            {
                return;
            }

            var placeholder = GameObject.FindWithTag("CharacterPlaceholder");
            if (placeholder == null)
            {
                Debug.LogWarning("[LobbyUI] No GameObject tagged 'CharacterPlaceholder' was found in the scene.");
                return;
            }

            _characterPreviewInstance = Instantiate(proxyData.visualPrefab, placeholder.transform);
            _characterPreviewInstance.transform.localPosition = Vector3.zero;
            _characterPreviewInstance.transform.localRotation = Quaternion.identity;
            _characterPreviewInstance.transform.localScale = Vector3.one;
        }

        // --- SCREEN BINDERS ---

        private void BindMainMenu(VisualElement root)
        {
            // Belt-and-suspenders for the end-screen timeScale=0 latent bug: any path back to
            // the main menu must leave timeScale at 1f, otherwise the next match's character-
            // select timer stalls and spawners freeze.
            Time.timeScale = 1f;

            root.Q<Button>("Btn_Singleplayer").clicked += () =>
            {
                // Singleplayer bypass: Start host, set to Coop, force team, start timer
                if (NetworkManager.Singleton.StartHost())
                {
                    // 1. Set rules to Co-op so the game loops properly later
                    LobbyNetworkController.Singleton.UpdateSettingsRpc(new LobbySettings 
                    { 
                        Mode = GameMode.Coop, 
                        Factions = AllowedFactions.Both 
                    });

                    // 2. Assign the solo player to Team 1 so they don't spawn in the void
                    LobbyNetworkController.Singleton.RequestChangeTeamRpc(_Project.Core.Enums.TeamAffiliation.Cleansers);

                    // 3. Start the Server's State Machine Timer!
                    MatchNetworkController.Singleton.StartCharacterSelectRpc();

                    ShowScreen(screenCharacterSelect, BindCharacterSelect);
                }
            };

            root.Q<Button>("Btn_Multiplayer").clicked += () => ShowScreen(screenMultiChoice, BindMultiplayerChoice);

            var btnJournal = root.Q<Button>("Btn_Journal");
            if (btnJournal != null)
            {
                btnJournal.clicked += () => ShowScreen(screenJournal, BindJournalScreen);
            }

            root.Q<Button>("Btn_Quit").clicked += () => Application.Quit();

            ShowDisconnectModalIfPending();
        }

        private void ShowDisconnectModalIfPending()
        {
            var reason = SessionEndHandler.ConsumePendingReason();
            if (reason == null) return;

            var overlay = _uiDocument.rootVisualElement;

            var dim = new VisualElement { name = "Modal_Disconnected_Dim" };
            dim.style.position = Position.Absolute;
            dim.style.left = 0; dim.style.right = 0; dim.style.top = 0; dim.style.bottom = 0;
            dim.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.65f));
            dim.style.alignItems = Align.Center;
            dim.style.justifyContent = Justify.Center;

            var panel = new VisualElement();
            panel.style.minWidth = 420;
            panel.style.paddingTop = 24; panel.style.paddingBottom = 24;
            panel.style.paddingLeft = 32; panel.style.paddingRight = 32;
            panel.style.backgroundColor = new StyleColor(new Color(0.08f, 0.08f, 0.1f, 0.98f));
            panel.style.borderTopWidth = 2; panel.style.borderBottomWidth = 2;
            panel.style.borderLeftWidth = 2; panel.style.borderRightWidth = 2;
            var accent = new StyleColor(new Color(0.9308176f, 0.061468955f, 0.061468955f, 1f));
            panel.style.borderTopColor = accent; panel.style.borderBottomColor = accent;
            panel.style.borderLeftColor = accent; panel.style.borderRightColor = accent;
            panel.style.alignItems = Align.Center;

            var title = new Label("Disconnected");
            title.AddToClassList("unity-label");
            title.style.fontSize = 28;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 12;
            panel.Add(title);

            var body = new Label(SessionEndHandler.ReasonToMessage(reason.Value));
            body.AddToClassList("unity-label");
            body.style.fontSize = 18;
            body.style.whiteSpace = WhiteSpace.Normal;
            body.style.marginBottom = 20;
            panel.Add(body);

            var ok = new Button { text = "OK" };
            ok.AddToClassList("unity-button");
            ok.style.minWidth = 120;
            ok.style.fontSize = 18;
            ok.clicked += () => overlay.Remove(dim);
            AttachButtonSfx(ok);
            panel.Add(ok);

            dim.Add(panel);
            overlay.Add(dim);
        }

        private void BindJournalScreen(VisualElement root)
        {
            var btnBack = root.Q<Button>("Btn_Back");
            var containerCategories = root.Q<VisualElement>("Container_Categories");
            var containerEntryList = root.Q<ScrollView>("Container_EntryList");
            var labelTitle = root.Q<Label>("Label_Title");
            var labelBody = root.Q<Label>("Label_Body");
            var imageIcon = root.Q<Image>("Image_Icon");

            btnBack.clicked += () => ShowScreen(screenMainMenu, BindMainMenu);

            if (journalDatabase == null)
            {
                labelTitle.text = "No Journal Database Assigned";
                labelBody.text = "Assign a JournalDatabase asset to LobbyUIController.journalDatabase in the MainMenu scene.";
                return;
            }

            var categoryValues = (JournalCategory[])System.Enum.GetValues(typeof(JournalCategory));
            JournalCategory currentCategory = JournalCategory.Tutorial;
            var categoryButtons = new Dictionary<JournalCategory, Button>();

            void Repaint(JournalEntry entry)
            {
                if (entry == null)
                {
                    labelTitle.text = "";
                    labelBody.text = "No entries in this category yet.";
                    if (imageIcon != null) imageIcon.sprite = null;
                    return;
                }
                labelTitle.text = entry.title;
                labelBody.text = entry.body;
                if (imageIcon != null) imageIcon.sprite = entry.icon;
            }

            void PopulateEntries()
            {
                containerEntryList.Clear();
                var entries = journalDatabase.GetByCategory(currentCategory)
                    .OrderBy(e => e.sortOrder)
                    .ThenBy(e => e.title)
                    .ToList();

                if (entries.Count == 0)
                {
                    Repaint(null);
                    return;
                }

                foreach (var entry in entries)
                {
                    var local = entry;
                    var btn = new Button { text = string.IsNullOrEmpty(entry.title) ? entry.entryID : entry.title };
                    btn.AddToClassList("unity-button");
                    btn.style.marginBottom = 2;
                    btn.style.unityTextAlign = TextAnchor.MiddleLeft;
                    btn.clicked += () => Repaint(local);
                    containerEntryList.contentContainer.Add(btn);
                }

                Repaint(entries[0]);
                WireButtonSfx(containerEntryList);
            }

            void HighlightCategory()
            {
                foreach (var kv in categoryButtons)
                {
                    bool active = kv.Key == currentCategory;
                    kv.Value.style.unityFontStyleAndWeight = active ? FontStyle.Bold : FontStyle.Normal;
                }
            }

            foreach (var cat in categoryValues)
            {
                var local = cat;
                var btn = new Button { text = cat.ToString() };
                btn.AddToClassList("unity-button");
                btn.style.marginBottom = 2;
                btn.style.unityTextAlign = TextAnchor.MiddleLeft;
                btn.clicked += () =>
                {
                    currentCategory = local;
                    HighlightCategory();
                    PopulateEntries();
                };
                categoryButtons[cat] = btn;
                containerCategories.Add(btn);
            }

            HighlightCategory();
            PopulateEntries();
        }

        private void BindMultiplayerChoice(VisualElement root)
        {
            root.Q<Button>("Btn_Host").clicked += () => ShowScreen(screenHostSettings, BindHostSettings);
            root.Q<Button>("Btn_Join").clicked += () => ShowScreen(screenJoinNetwork, BindJoinNetwork);
            root.Q<Button>("Btn_Back").clicked += () => ShowScreen(screenMainMenu, BindMainMenu);
        }

       private void BindHostSettings(VisualElement root)
        {
            var ipLabel = root.Q<TextField>("TxtFld_Address");
            ipLabel.value = GetLocalIPv4(); 

            // --- NEW: UI REFINEMENTS ---
            var radioGroupMode = root.Q<RadioButtonGroup>("RadBtnGrp_GameMode");
            var dropdownFactions = root.Q<EnumField>("Enm_Factions");

            dropdownFactions.Init(AllowedFactions.Both);
            // Co-op is index 1. Disable the dropdown if PvP (index 0) is selected.
            radioGroupMode.RegisterValueChangedCallback(evt => 
            {
                dropdownFactions.SetEnabled(evt.newValue == 1);
            });
            // Set initial state
            dropdownFactions.SetEnabled(radioGroupMode.value == 1);
            // ---------------------------

            root.Q<Button>("Btn_CreateLobby").clicked += () => 
            {
                var portStr = root.Q<TextField>("TxtFld_Port").value;
                ushort port = ushort.TryParse(portStr, out var p) ? p : (ushort)7777;

                // Grab settings from the UI to pass to the Network Controller
                var settings = new LobbySettings
                {
                    Mode = radioGroupMode.value == 0 ? GameMode.PvP : GameMode.Coop,
                    Factions = (AllowedFactions)dropdownFactions.value
                };

                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.SetConnectionData("127.0.0.1", port, "0.0.0.0"); 

                if (NetworkManager.Singleton.StartHost())
                {
                    // Host instantly pushes these settings to the server state
                    LobbyNetworkController.Singleton.UpdateSettingsRpc(settings);
                    ShowScreen(screenLobby, BindLobbyScreen);
                }
                else
                {
                    Debug.LogError("[LobbyUI] Failed to start Host.");
                }
            };

            root.Q<Button>("Btn_Back").clicked += () => ShowScreen(screenMultiChoice, BindMultiplayerChoice);
        }

        private void BindJoinNetwork(VisualElement root)
        {
            var btnConnect = root.Q<Button>("Btn_Connect");
            var btnBack = root.Q<Button>("Btn_Back");

            btnConnect.clicked += () => 
            {
                var ipStr = root.Q<TextField>("Input_IPAddress").value;
                var portStr = root.Q<TextField>("TxtFld_Port").value;
                ushort port = ushort.TryParse(portStr, out var p) ? p : (ushort)7777;

                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.SetConnectionData(ipStr, port);

                // --- NEW: Lock the UI while we wait for the server ---
                btnConnect.SetEnabled(false);
                btnBack.SetEnabled(false);
                btnConnect.text = "Connecting...";

                // Callback for Success
                void OnClientConnected(ulong clientId)
                {
                    if (clientId == NetworkManager.Singleton.LocalClientId)
                    {
                        CleanupCallbacks();
                        ShowScreen(screenLobby, BindLobbyScreen);
                    }
                }

                // Callback for Failure (e.g. bad IP, server offline)
                void OnClientDisconnected(ulong clientId)
                {
                    if (clientId == 0 || clientId == NetworkManager.Singleton.LocalClientId)
                    {
                        CleanupCallbacks();
                        btnConnect.SetEnabled(true);
                        btnBack.SetEnabled(true);
                        btnConnect.text = "Connect";
                        Debug.LogWarning("[LobbyUI] Connection failed or rejected.");
                    }
                }

                void CleanupCallbacks()
                {
                    NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                    NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
                }

                // Subscribe to the network events
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

                if (!NetworkManager.Singleton.StartClient())
                {
                    CleanupCallbacks();
                    btnConnect.SetEnabled(true);
                    btnBack.SetEnabled(true);
                    btnConnect.text = "Connect";
                    Debug.LogError("[LobbyUI] Failed to start Client transport.");
                }
            };

            btnBack.clicked += () => ShowScreen(screenMultiChoice, BindMultiplayerChoice);
        }

        private void BindLobbyScreen(VisualElement root)
        {
            // --- HOST BUTTON LOGIC ---
            var btnHostStart = root.Q<Button>("Btn_HostStart");
            btnHostStart.style.display = NetworkManager.Singleton.IsHost ? DisplayStyle.Flex : DisplayStyle.None;
            btnHostStart.SetEnabled(false); 

            // --- KICK / DISCONNECT LISTENER ---
            void OnClientDisconnected(ulong clientId)
            {
                // In NGO, if the server drops a client, the clientId on the client's end is usually 0 or their own ID.
                if (clientId == 0 || clientId == NetworkManager.Singleton.LocalClientId)
                {
                    // Unexpected disconnect (kick / host-vanish): let NetworkSessionWatcher
                    // raise OnSessionEnded so SessionEndHandler can shut down and show the modal.
                    // We only navigate locally as a fast fallback if no handler picks it up.
                    ShowScreen(screenMainMenu, BindMainMenu);
                }
            }
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            // --- READY BUTTON ---
            var btnReady = root.Q<Button>("Btn_Ready");
            btnReady.clicked += () => 
            {
                LobbyNetworkController.Singleton.ToggleReadyRpc();
            };

            // --- SETTINGS SYNC ---
            var team2Column = root.Q<Label>("Lbl_Team2").parent.parent; 
            var lblTeam1 = root.Q<Label>("Lbl_Team1"); // Find the Team 1 Label

            void OnSettingsChanged(LobbySettings oldSettings, LobbySettings newSettings)
            {
                // Hide/Show Team 2 Column
                team2Column.style.display = newSettings.Mode == GameMode.Coop ? DisplayStyle.None : DisplayStyle.Flex;
                
                // Rename Team 1 Column based on Mode
                lblTeam1.text = newSettings.Mode == GameMode.Coop ? "Players" : "Cleansers";
            }
            
            OnSettingsChanged(default, LobbyNetworkController.Singleton.CurrentSettings.Value);
            LobbyNetworkController.Singleton.CurrentSettings.OnValueChanged += OnSettingsChanged;

            // --- ROSTER SYNC & UI GENERATION ---
            var listTeam1 = root.Q<ScrollView>("List_Team1");
            var listTeam2 = root.Q<ScrollView>("List_Team2");

            void UpdatePlayerLists(NetworkListEvent<LobbyPlayerState> changeEvent = default)
            {
                listTeam1.Clear();
                listTeam2.Clear();

                int assignedPlayers = 0;
                int readyPlayers = 0;
                
                // Track team populations for PvP checks
                int team1Count = 0;
                int team2Count = 0;

                foreach (var player in LobbyNetworkController.Singleton.LobbyPlayers)
                {
                    if (player.Team != _Project.Core.Enums.TeamAffiliation.None)
                    {
                        assignedPlayers++;
                        if (player.IsReady) readyPlayers++;

                        if (player.Team == _Project.Core.Enums.TeamAffiliation.Cleansers) team1Count++;
                        if (player.Team == _Project.Core.Enums.TeamAffiliation.Thrive) team2Count++;
                    }

                    // 1. Create Row
                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.justifyContent = Justify.FlexStart; 
                    row.style.alignItems = Align.Center;
                    row.style.marginBottom = 4;

                    // 2. Create the Name Label
                    var playerLabel = new Label(player.PlayerName.ToString());
                    playerLabel.AddToClassList("unity-label"); 
                    
                    if (player.IsReady)
                    {
                        playerLabel.style.color = new StyleColor(Color.green);
                    }

                    row.Add(playerLabel);

                    // 3. Add Kick Button
                    if (NetworkManager.Singleton.IsHost && player.ClientId != NetworkManager.ServerClientId)
                    {
                        var kickBtn = new Button { text = "Kick" };
                        kickBtn.AddToClassList("unity-button"); 
                        kickBtn.style.height = 24; 
                        kickBtn.style.fontSize = 12;
                        kickBtn.style.marginLeft = 15; 

                        var dangerColor = new StyleColor(new Color(0.8f, 0.2f, 0.2f));
                        kickBtn.style.borderTopColor = dangerColor;
                        kickBtn.style.borderBottomColor = dangerColor;
                        kickBtn.style.borderLeftColor = dangerColor;
                        kickBtn.style.borderRightColor = dangerColor;

                        kickBtn.clicked += () => 
                        {
                            LobbyNetworkController.Singleton.KickPlayerRpc(player.ClientId);
                        };
                        row.Add(kickBtn);
                    }

                    // 4. Add the Row to the correct team column
                    if (player.Team == _Project.Core.Enums.TeamAffiliation.Cleansers)
                    {
                        listTeam1.contentContainer.Add(row);
                    }
                    else if (player.Team == _Project.Core.Enums.TeamAffiliation.Thrive)
                    {
                        listTeam2.contentContainer.Add(row);
                    }
                }

                // 5. Toggle Start Button with Mode-Specific Rules
                if (NetworkManager.Singleton.IsHost)
                {
                    bool isEveryoneReady = (assignedPlayers > 0 && assignedPlayers == readyPlayers);
                    bool meetsTeamRequirements = true;

                    // If PvP, STRICTLY require at least one player on both teams
                    if (LobbyNetworkController.Singleton.CurrentSettings.Value.Mode == GameMode.PvP)
                    {
                        meetsTeamRequirements = (team1Count > 0 && team2Count > 0);
                    }

                    btnHostStart.SetEnabled(isEveryoneReady && meetsTeamRequirements);
                }
            }

            UpdatePlayerLists();
            LobbyNetworkController.Singleton.LobbyPlayers.OnListChanged += UpdatePlayerLists;

            // --- BUTTON BINDINGS ---
            root.Q<Button>("Btn_JoinTeam1")?.RegisterCallback<ClickEvent>(evt => 
            {
                LobbyNetworkController.Singleton.RequestChangeTeamRpc(_Project.Core.Enums.TeamAffiliation.Cleansers);
            });

            root.Q<Button>("Btn_JoinTeam2")?.RegisterCallback<ClickEvent>(evt => 
            {
                LobbyNetworkController.Singleton.RequestChangeTeamRpc(_Project.Core.Enums.TeamAffiliation.Thrive);
            });

            root.Q<Button>("Btn_Leave").clicked += () =>
            {
                NetworkSessionWatcher.Singleton?.MarkIntentionalShutdown();
                NetworkManager.Singleton.Shutdown();
                ShowScreen(screenMainMenu, BindMainMenu);
            };

            btnHostStart.clicked += () => 
            {
                MatchNetworkController.Singleton.StartCharacterSelectRpc();
            };

            // --- STATE TRANSITION LISTENER ---
            void OnMatchStateChanged(MatchState oldState, MatchState newState)
            {
                if (newState == MatchState.CharacterSelect)
                {
                    ShowScreen(screenCharacterSelect, BindCharacterSelect);
                }
            }
            MatchNetworkController.Singleton.CurrentState.OnValueChanged += OnMatchStateChanged;

            // --- CLEANUP ---
            root.RegisterCallback<DetachFromPanelEvent>(evt => 
            {
                if (NetworkManager.Singleton != null)
                {
                    // Clean up our disconnect listener so it doesn't leak memory
                    NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
                }
                if (LobbyNetworkController.Singleton != null)
                {
                    LobbyNetworkController.Singleton.CurrentSettings.OnValueChanged -= OnSettingsChanged;
                    LobbyNetworkController.Singleton.LobbyPlayers.OnListChanged -= UpdatePlayerLists;
                }
                if (MatchNetworkController.Singleton != null)
                {
                    MatchNetworkController.Singleton.CurrentState.OnValueChanged -= OnMatchStateChanged;
                }
            });
        }

        private void BindCharacterSelect(VisualElement root)
        {
            var gridCharacters = root.Q<VisualElement>("Grid_Characters");
            var scrlInfo = root.Q<ScrollView>("Scrl_CharacterInfo");
            var btnLockIn = root.Q<Button>("Btn_LockIn");
            var textTimer = root.Q<Label>("Text_Timer");
            var titleProxies = root.Q<Label>("Title_Proxies");

            // --- FORCE SELECTION FALLBACK RULE ---
            btnLockIn.SetEnabled(false); 
            int selectedCharacterIndex = -1; 
            VisualElement lastSelectedSlot = null;

            // --- 1. EVALUATE SERVER RULES (Once on Load) ---
            _Project.Core.Enums.TeamAffiliation myTeam = _Project.Core.Enums.TeamAffiliation.None;
            if (LobbyNetworkController.Singleton != null)
            {
                foreach (var p in LobbyNetworkController.Singleton.LobbyPlayers)
                {
                    if (p.ClientId == NetworkManager.Singleton.LocalClientId)
                    {
                        myTeam = p.Team;
                        break;
                    }
                }
            }

            var settings = LobbyNetworkController.Singleton.CurrentSettings.Value;
            bool cleansersAllowed = false;
            bool thriveAllowed = false;

            if (settings.Mode == GameMode.Coop)
            {
                if (settings.Factions == AllowedFactions.Both) { cleansersAllowed = true; thriveAllowed = true; }
                else if (settings.Factions == AllowedFactions.CleansersOnly) { cleansersAllowed = true; }
                else if (settings.Factions == AllowedFactions.ThriveOnly) { thriveAllowed = true; }
            }
            else // PvP
            {
                if (myTeam == _Project.Core.Enums.TeamAffiliation.Cleansers) cleansersAllowed = true;
                else if (myTeam == _Project.Core.Enums.TeamAffiliation.Thrive) thriveAllowed = true;
            }

            // Set the Header Title
            if (cleansersAllowed && thriveAllowed) titleProxies.text = "Available Proxies";
            else if (cleansersAllowed) titleProxies.text = "Cleanser Proxies";
            else if (thriveAllowed) titleProxies.text = "THRIVE Proxies";
            else titleProxies.text = "No Proxies Available";


            // --- 2. TIMER SYNC (Runs 10x a second) ---
            root.schedule.Execute(() => 
            {
                if (MatchNetworkController.Singleton != null)
                {
                    float time = MatchNetworkController.Singleton.CharacterSelectTimer.Value;
                    if (time < 0) time = 0;
                    int minutes = Mathf.FloorToInt(time / 60F);
                    int seconds = Mathf.FloorToInt(time - minutes * 60);
                    textTimer.text = string.Format("{0:0}:{1:00}", minutes, seconds);
                }
            }).Every(100);


            // --- 3. GRID GENERATION & SELECTION ---
            gridCharacters.Clear(); 
            scrlInfo.Clear(); 

            var db = MatchNetworkController.Singleton.CharacterDatabase;
            bool hasAutoSelected = false; 

            for (int i = 0; i < db.Length; i++)
            {
                var proxyData = db[i];
                if (proxyData == null || proxyData.teamDefinition == null) continue;

                // --- STRICT TYPE-SAFE FACTION FILTERING ---
                bool isCleanser = proxyData.teamDefinition.teamAffiliation == _Project.Core.Enums.TeamAffiliation.Cleansers;
                bool isThrive = proxyData.teamDefinition.teamAffiliation == _Project.Core.Enums.TeamAffiliation.Thrive;

                if (isCleanser && !cleansersAllowed) continue; // Skip if they aren't allowed to play Cleansers
                if (isThrive && !thriveAllowed) continue;      // Skip if they aren't allowed to play THRIVE
                // ------------------------------------------

                int currentIndex = i; 

                var proxySlot = new VisualElement { name = "ProxySlot" };
                proxySlot.style.flexDirection = FlexDirection.Column;
                proxySlot.style.alignItems = Align.Center;
                proxySlot.style.paddingTop = 10;
                proxySlot.style.paddingBottom = 10;
                proxySlot.style.paddingLeft = 15;
                proxySlot.style.paddingRight = 15;
                proxySlot.style.borderBottomWidth = 2; 

                var icon = new Image { sprite = proxyData.icon };
                icon.style.width = 70;
                icon.style.height = 70;
                proxySlot.Add(icon);

                var nameLbl = new Label(proxyData.proxyName);
                nameLbl.AddToClassList("unity-label");
                nameLbl.style.fontSize = 18;
                nameLbl.style.marginTop = 5;
                proxySlot.Add(nameLbl);

                // Hover feedback: slight translucent wash + UI sounds. Skipped for the
                // currently-selected slot (it keeps its team-color tint) and once the grid
                // is locked. Mirrors AttachButtonSfx so slots feel like the menu buttons.
                var slotHoverColor = new StyleColor(new Color(1f, 1f, 1f, 0.08f));
                proxySlot.RegisterCallback<MouseEnterEvent>(_ =>
                {
                    if (!gridCharacters.enabledSelf) return;
                    var sfx = SfxManager.Instance;
                    if (sfx != null && sfx.Library != null)
                        sfx.PlayOneShot2D(sfx.Library.uiHover, 0.5f, 1f, true);
                    if (proxySlot != lastSelectedSlot)
                        proxySlot.style.backgroundColor = slotHoverColor;
                });
                proxySlot.RegisterCallback<MouseLeaveEvent>(_ =>
                {
                    if (proxySlot != lastSelectedSlot)
                        proxySlot.style.backgroundColor = StyleKeyword.Null;
                });

                // Click Event
                proxySlot.RegisterCallback<ClickEvent>(evt =>
                {
                    if (!gridCharacters.enabledSelf) return;

                    var sfx = SfxManager.Instance;
                    if (sfx != null && sfx.Library != null)
                        sfx.PlayOneShot2D(sfx.Library.uiClick, 0.7f, 1f, true);

                    if (lastSelectedSlot != null)
                    {
                        lastSelectedSlot.style.backgroundColor = StyleKeyword.Null;
                        lastSelectedSlot.style.borderBottomColor = StyleKeyword.Null;
                    }

                    selectedCharacterIndex = currentIndex;
                    lastSelectedSlot = proxySlot;
                    
                    Color tColor = proxyData.teamDefinition.teamColor;
                    proxySlot.style.backgroundColor = new StyleColor(new Color(tColor.r, tColor.g, tColor.b, 0.2f));
                    proxySlot.style.borderBottomColor = new StyleColor(tColor);
                    scrlInfo.style.borderTopColor = new StyleColor(tColor);

                    UpdateCharacterInfo(proxyData, tColor);
                    ShowCharacterPreview(proxyData);
                    btnLockIn.SetEnabled(true); 
                });

                gridCharacters.Add(proxySlot);

                // Auto-Select the *first* valid proxy
                if (!hasAutoSelected)
                {
                    hasAutoSelected = true;
                    root.schedule.Execute(() => {
                        selectedCharacterIndex = currentIndex;
                        lastSelectedSlot = proxySlot;

                        Color tColor = proxyData.teamDefinition.teamColor;
                        proxySlot.style.backgroundColor = new StyleColor(new Color(tColor.r, tColor.g, tColor.b, 0.2f));
                        proxySlot.style.borderBottomColor = new StyleColor(tColor);
                        scrlInfo.style.borderTopColor = new StyleColor(tColor);

                        UpdateCharacterInfo(proxyData, tColor);
                        ShowCharacterPreview(proxyData);
                        btnLockIn.SetEnabled(true); 
                    }).StartingIn(50);
                }
            }

            // --- 4. ABILITY INFO GENERATION ---
            void UpdateCharacterInfo(ProxyCharacterData proxy, Color teamColor)
            {
                scrlInfo.Clear();

                var headerName = new Label(proxy.proxyName) { style = { fontSize = 32, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 5 } };
                headerName.AddToClassList("unity-label");
                scrlInfo.contentContainer.Add(headerName);

                var headerDesc = new Label(proxy.description) { style = { whiteSpace = WhiteSpace.Normal, fontSize = 16, marginBottom = 20 } };
                headerDesc.AddToClassList("unity-label");
                headerDesc.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));
                scrlInfo.contentContainer.Add(headerDesc);

                void AddAbilityRow(string slotName, _Project.Core.Interfaces.IProxyAbilityData baseAbility)
                {
                    if (baseAbility == null) return;
                    
                    var ability = baseAbility as _Project.Features.ProxyAbilities.Data.ProxyAbilityData;
                    if (ability == null) return;

                    var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 15, alignItems = Align.FlexStart } };

                    var abIcon = new Image { sprite = ability.icon, style = { width = 50, height = 50, marginRight = 15 } };
                    row.Add(abIcon);

                    var textCol = new VisualElement { style = { flexDirection = FlexDirection.Column, flexShrink = 1 } };
                    
                    var title = new Label($"[{slotName}] {ability.abilityName}") { style = { fontSize = 18, unityFontStyleAndWeight = FontStyle.Bold } };
                    title.AddToClassList("unity-label");
                    title.style.color = new StyleColor(teamColor); 
                    textCol.Add(title);

                    var desc = new Label(ability.description) { style = { whiteSpace = WhiteSpace.Normal, fontSize = 14, marginTop = 2 } };
                    desc.AddToClassList("unity-label");
                    textCol.Add(desc);

                    row.Add(textCol);
                    scrlInfo.contentContainer.Add(row);
                }

                AddAbilityRow("Secondary", proxy.SecondaryAbility);
                AddAbilityRow("Utility", proxy.UtilityAbility);
                AddAbilityRow("Special", proxy.SpecialAbility);

                if (proxy.passiveAbilities != null)
                {
                    foreach (var pass in proxy.passiveAbilities)
                    {
                        AddAbilityRow("Passive", pass);
                    }
                }
            }

            // --- 5. LOCK IN BUTTON LOGIC ---
            btnLockIn.clicked += () => 
            {
                if (selectedCharacterIndex == -1) return; 

                btnLockIn.SetEnabled(false);
                btnLockIn.text = "Locked In";
                btnLockIn.style.backgroundColor = new StyleColor(Color.green);
                
                gridCharacters.SetEnabled(false); 

                MatchNetworkController.Singleton.LockInCharacterRpc(selectedCharacterIndex);
            };
        }

        // --- UTILITIES ---
        
        private string GetLocalIPv4()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork) return ip.ToString();
                }
            }
            catch (Exception e) { Debug.LogWarning($"Could not get local IP: {e.Message}"); }
            return "Unknown IP";
        }
    }
}
