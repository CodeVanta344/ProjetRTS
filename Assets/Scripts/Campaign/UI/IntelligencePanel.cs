using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Data;
using NapoleonicWars.UI;

namespace NapoleonicWars.Campaign.UI
{
    /// <summary>
    /// UI Panel for displaying intelligence information and managing agents.
    /// Shows on the left side of the campaign screen.
    /// </summary>
    public class IntelligencePanel : MonoBehaviour
    {
        public static IntelligencePanel Instance { get; private set; }
        
        [Header("UI References")]
        public GameObject panelRoot;
        public Transform contentContainer;
        public ScrollRect scrollRect;
        
        [Header("Prefabs")]
        public GameObject agentCardPrefab;
        public GameObject intelReportPrefab;
        public GameObject networkCardPrefab;
        public GameObject sabotageButtonPrefab;
        
        // Internal state
        private Canvas canvas;
        private FactionType currentFaction;
        private List<GameObject> currentCards = new List<GameObject>();
        private AgentData selectedAgent;
        private string selectedProvinceId;
        
        // Cached components
        private Dictionary<string, GameObject> agentCards = new Dictionary<string, GameObject>();
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
        
        private void Start()
        {
            canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[IntelligencePanel] No Canvas found in parent!");
                return;
            }
            
            // Subscribe to events
            if (AgentManager.Instance != null)
            {
                AgentManager.Instance.OnIntelGathered += OnIntelReceived;
                AgentManager.Instance.OnNetworkEstablished += OnNetworkCreated;
                AgentManager.Instance.OnAgentCaptured += OnAgentCapturedEvent;
                AgentManager.Instance.OnAgentMoved += OnAgentMovedEvent;
            }
            
            currentFaction = CampaignManager.Instance?.PlayerFaction ?? FactionType.France;
            
            CreatePanelUI();
            Hide();
        }
        
        private void OnDestroy()
        {
            if (AgentManager.Instance != null)
            {
                AgentManager.Instance.OnIntelGathered -= OnIntelReceived;
                AgentManager.Instance.OnNetworkEstablished -= OnNetworkCreated;
                AgentManager.Instance.OnAgentCaptured -= OnAgentCapturedEvent;
                AgentManager.Instance.OnAgentMoved -= OnAgentMovedEvent;
            }
        }
        
        /// <summary>
        /// Show the intelligence panel
        /// </summary>
        public void Show()
        {
            panelRoot?.SetActive(true);
            RefreshPanel();
        }
        
        /// <summary>
        /// Hide the panel
        /// </summary>
        public void Hide()
        {
            panelRoot?.SetActive(false);
        }
        
        /// <summary>
        /// Toggle panel visibility
        /// </summary>
        public void Toggle()
        {
            if (panelRoot != null)
            {
                if (panelRoot.activeSelf) Hide();
                else Show();
            }
        }
        
        private void CreatePanelUI()
        {
            // Create main panel if not assigned
            if (panelRoot == null)
            {
                panelRoot = new GameObject("IntelligencePanel");
                panelRoot.transform.SetParent(canvas.transform, false);
                
                RectTransform rect = panelRoot.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 0.1f);
                rect.anchorMax = new Vector2(0, 0.9f);
                rect.pivot = new Vector2(0, 0.5f);
                rect.anchoredPosition = new Vector2(10, 0);
                rect.sizeDelta = new Vector2(350, 0);
                
                Image bg = panelRoot.AddComponent<Image>();
                bg.color = new Color(0.05f, 0.03f, 0.02f, 0.95f);
                
                // Add scroll view
                GameObject scrollGO = new GameObject("ScrollView");
                scrollGO.transform.SetParent(panelRoot.transform, false);
                RectTransform scrollRect = scrollGO.AddComponent<RectTransform>();
                scrollRect.anchorMin = Vector2.zero;
                scrollRect.anchorMax = Vector2.one;
                scrollRect.offsetMin = new Vector2(10, 10);
                scrollRect.offsetMax = new Vector2(-10, -10);
                
                ScrollRect sr = scrollGO.AddComponent<ScrollRect>();
                sr.horizontal = false;
                sr.vertical = true;
                
                // Create content container
                GameObject content = new GameObject("Content");
                content.transform.SetParent(scrollGO.transform, false);
                RectTransform contentRect = content.AddComponent<RectTransform>();
                contentRect.anchorMin = new Vector2(0, 1);
                contentRect.anchorMax = Vector2.one;
                contentRect.pivot = new Vector2(0.5f, 1);
                contentRect.anchoredPosition = Vector2.zero;
                contentRect.sizeDelta = new Vector2(0, 800);
                
                VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(10, 10, 10, 10);
                vlg.spacing = 8f;
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                
                ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                
                sr.content = contentRect;
                this.scrollRect = sr;
                this.contentContainer = content.transform;
            }
        }
        
        /// <summary>
        /// Refresh all panel content
        /// </summary>
        public void RefreshPanel()
        {
            ClearCards();
            
            if (AgentManager.Instance == null) return;
            
            // Section: Agents
            CreateSectionHeader("🕵️ Active Agents");
            var agents = AgentManager.Instance.GetAgentsForFaction(currentFaction);
            if (agents.Count == 0)
            {
                CreateInfoText("No active agents. Recruit agents from cities with spy buildings.");
            }
            else
            {
                foreach (var agent in agents)
                {
                    CreateAgentCard(agent);
                }
            }
            
            CreateSeparator();
            
            // Section: Spy Networks
            CreateSectionHeader("🕸️ Spy Networks");
            var networks = AgentManager.Instance.GetNetworksForFaction(currentFaction);
            if (networks.Count == 0)
            {
                CreateInfoText("No established networks. Agents in enemy cities can establish networks.");
            }
            else
            {
                foreach (var network in networks)
                {
                    CreateNetworkCard(network);
                }
            }
            
            CreateSeparator();
            
            // Section: Intelligence Reports
            CreateSectionHeader("📜 Intelligence Reports");
            var intel = AgentManager.Instance.GetIntelligenceForFaction(currentFaction);
            if (intel.Count == 0)
            {
                CreateInfoText("No intelligence gathered yet. Send agents to enemy territory.");
            }
            else
            {
                // Group by province
                var grouped = intel.GroupBy(i => i.provinceId).OrderBy(g => g.Key);
                foreach (var group in grouped)
                {
                    string provinceName = CampaignManager.Instance?.Provinces[group.Key]?.provinceName ?? group.Key;
                    CreateSubHeader(provinceName);
                    
                    foreach (var report in group.OrderByDescending(r => r.turnGathered).Take(3))
                    {
                        CreateIntelReportCard(report);
                    }
                }
            }
            
            // Sabotage section if province selected
            if (!string.IsNullOrEmpty(selectedProvinceId))
            {
                CreateSeparator();
                CreateSabotageSection(selectedProvinceId);
            }
        }
        
        private void CreateSectionHeader(string text)
        {
            GameObject header = new GameObject("SectionHeader");
            header.transform.SetParent(contentContainer, false);
            
            Text t = header.AddComponent<Text>();
            t.text = text;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 18;
            t.color = new Color(0.9f, 0.8f, 0.6f);
            t.alignment = TextAnchor.MiddleLeft;
            t.fontStyle = FontStyle.Bold;
            
            RectTransform rect = header.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 30);
            
            currentCards.Add(header);
        }
        
        private void CreateSubHeader(string text)
        {
            GameObject header = new GameObject("SubHeader");
            header.transform.SetParent(contentContainer, false);
            
            Text t = header.AddComponent<Text>();
            t.text = "• " + text;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 14;
            t.color = new Color(0.7f, 0.7f, 0.7f);
            t.alignment = TextAnchor.MiddleLeft;
            
            RectTransform rect = header.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 22);
            
            currentCards.Add(header);
        }
        
        private void CreateInfoText(string text)
        {
            GameObject info = new GameObject("InfoText");
            info.transform.SetParent(contentContainer, false);
            
            Text t = info.AddComponent<Text>();
            t.text = text;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 12;
            t.color = new Color(0.5f, 0.5f, 0.5f);
            t.alignment = TextAnchor.MiddleCenter;
            
            RectTransform rect = info.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 40);
            
            currentCards.Add(info);
        }
        
        private void CreateSeparator()
        {
            GameObject sep = new GameObject("Separator");
            sep.transform.SetParent(contentContainer, false);
            
            Image img = sep.AddComponent<Image>();
            img.color = new Color(0.3f, 0.25f, 0.2f, 0.5f);
            
            RectTransform rect = sep.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 2);
            
            currentCards.Add(sep);
        }
        
        private void CreateAgentCard(AgentData agent)
        {
            GameObject card = new GameObject($"AgentCard_{agent.agentId}");
            card.transform.SetParent(contentContainer, false);
            agentCards[agent.agentId] = card;
            
            // Background
            Image bg = card.AddComponent<Image>();
            bg.color = GetStateColor(agent.currentState);
            
            RectTransform rect = card.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 80);
            
            // Layout
            VerticalLayoutGroup vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.spacing = 4f;
            
            // Name row
            GameObject nameRow = new GameObject("NameRow");
            nameRow.transform.SetParent(card.transform, false);
            UIFactory.AddHorizontalLayout(nameRow, 5f);
            
            Text nameText = UIFactory.CreateText(nameRow.transform, "Name", 
                $"{GetAgentTypeIcon(agent.agentType)} {agent.agentName}", 14, TextAnchor.MiddleLeft, Color.white);
            nameText.fontStyle = FontStyle.Bold;
            
            Text stateText = UIFactory.CreateText(nameRow.transform, "State", 
                agent.currentState.ToString(), 11, TextAnchor.MiddleRight, GetStateTextColor(agent.currentState));
            
            // Location row
            string location = CampaignManager.Instance?.Provinces[agent.currentProvinceId]?.provinceName ?? "Unknown";
            Text locText = UIFactory.CreateText(card.transform, "Location", 
                $"📍 {location}", 12, TextAnchor.MiddleLeft, new Color(0.7f, 0.7f, 0.7f));
            
            // Stats row
            GameObject statsRow = new GameObject("StatsRow");
            statsRow.transform.SetParent(card.transform, false);
            UIFactory.AddHorizontalLayout(statsRow, 10f);
            
            Text statsText = UIFactory.CreateText(statsRow.transform, "Stats",
                $"Skill:{agent.skill} | Subtlety:{agent.subtlety} | XP:{agent.experience}", 
                10, TextAnchor.MiddleLeft, new Color(0.6f, 0.6f, 0.6f));
            
            // Progress bar for network establishment
            if (agent.currentState == AgentState.EstablishingNetwork)
            {
                CreateProgressBar(card.transform, agent.missionProgress, agent.missionTargetTurns, 
                    "Establishing Network...", new Color(0.2f, 0.6f, 0.9f));
            }
            
            // Action buttons
            if (agent.currentState != AgentState.Captured && agent.currentState != AgentState.Dead)
            {
                GameObject buttonRow = new GameObject("ButtonRow");
                buttonRow.transform.SetParent(card.transform, false);
                UIFactory.AddHorizontalLayout(buttonRow, 5f);
                
                // Gather Intel button
                if (agent.currentState != AgentState.GatheringIntel)
                {
                    Button intelBtn = CreateActionButton(buttonRow.transform, "🔍 Intel", () => {
                        AgentManager.Instance?.StartIntelligenceGathering(agent);
                        RefreshPanel();
                    });
                }
                else
                {
                    Text t = UIFactory.CreateText(buttonRow.transform, "Gathering", "Gathering...", 
                        11, TextAnchor.MiddleCenter, new Color(0.4f, 0.7f, 0.4f));
                }
                
                // Establish Network button
                if (!agent.hasEstablishedNetwork && agent.currentState != AgentState.EstablishingNetwork)
                {
                    ProvinceData prov = CampaignManager.Instance?.Provinces[agent.currentProvinceId];
                    if (prov != null && prov.owner != agent.faction)
                    {
                        Button networkBtn = CreateActionButton(buttonRow.transform, "🕸️ Network", () => {
                            AgentManager.Instance?.StartNetworkEstablishment(agent);
                            RefreshPanel();
                        });
                    }
                }
            }
            
            // Select agent on click
            Button cardBtn = card.AddComponent<Button>();
            cardBtn.onClick.AddListener(() => {
                selectedAgent = agent;
                HighlightCard(card);
            });
            
            currentCards.Add(card);
        }
        
        private void CreateNetworkCard(SpyNetwork network)
        {
            ProvinceData prov = CampaignManager.Instance?.Provinces[network.provinceId];
            string provinceName = prov?.provinceName ?? network.provinceId;
            
            GameObject card = new GameObject($"NetworkCard_{network.provinceId}");
            card.transform.SetParent(contentContainer, false);
            
            Image bg = card.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.25f, 0.15f, 0.9f);
            
            RectTransform rect = card.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 70);
            
            VerticalLayoutGroup vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.spacing = 4f;
            
            // Header
            GameObject headerRow = new GameObject("HeaderRow");
            headerRow.transform.SetParent(card.transform, false);
            UIFactory.AddHorizontalLayout(headerRow, 5f);
            
            Text nameText = UIFactory.CreateText(headerRow.transform, "Name",
                $"🕸️ {provinceName}", 13, TextAnchor.MiddleLeft, Color.white);
            nameText.fontStyle = FontStyle.Bold;
            
            string levelStars = new string('★', network.networkLevel);
            Text levelText = UIFactory.CreateText(headerRow.transform, "Level",
                levelStars, 12, TextAnchor.MiddleRight, new Color(1f, 0.8f, 0.2f));
            
            // Bonuses
            Text bonusText = UIFactory.CreateText(card.transform, "Bonuses",
                $"Siege: -{(int)(network.siegeBonus * 100)}% | Morale: +{(int)(network.moraleBonus * 100)}%",
                11, TextAnchor.MiddleLeft, new Color(0.6f, 0.8f, 0.6f));
            
            // Sabotage available
            if (network.canSabotage)
            {
                Text sabotageText = UIFactory.CreateText(card.transform, "Sabotage",
                    "✓ Sabotage operations available", 10, TextAnchor.MiddleLeft, new Color(0.9f, 0.5f, 0.2f));
            }
            
            currentCards.Add(card);
        }
        
        private void CreateIntelReportCard(IntelReport report)
        {
            GameObject card = new GameObject($"IntelCard_{report.reportId}");
            card.transform.SetParent(contentContainer, false);
            
            Image bg = card.AddComponent<Image>();
            float freshnessAlpha = report.freshness / 10f;
            bg.color = new Color(0.2f, 0.2f, 0.25f, 0.5f + freshnessAlpha * 0.5f);
            
            RectTransform rect = card.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 50);
            
            VerticalLayoutGroup vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(6, 6, 6, 6);
            
            // Icon and type
            GameObject headerRow = new GameObject("HeaderRow");
            headerRow.transform.SetParent(card.transform, false);
            UIFactory.AddHorizontalLayout(headerRow, 5f);
            
            string icon = GetIntelIcon(report.intelType);
            Text typeText = UIFactory.CreateText(headerRow.transform, "Type",
                $"{icon} {report.intelType}", 11, TextAnchor.MiddleLeft, new Color(0.8f, 0.8f, 0.9f));
            
            // Freshness indicator
            string freshness = report.isStale ? "📜 Stale" : 
                              report.freshness > 5 ? "🟢 Fresh" : "🟡 Aging";
            Text freshText = UIFactory.CreateText(headerRow.transform, "Freshness",
                freshness, 10, TextAnchor.MiddleRight, 
                report.isStale ? Color.gray : new Color(0.4f, 0.9f, 0.4f));
            
            // Content
            Text contentText = UIFactory.CreateText(card.transform, "Content",
                report.content, 11, TextAnchor.MiddleLeft, Color.white);
            
            currentCards.Add(card);
        }
        
        private void CreateSabotageSection(string provinceId)
        {
            CreateSectionHeader("⚔️ Sabotage Operations");
            
            var options = AgentManager.Instance?.GetAvailableSabotageOptions(provinceId, currentFaction);
            if (options == null || options.Count == 0)
            {
                CreateInfoText("No spy network in this province. Establish a network to unlock sabotage.");
                return;
            }
            
            // Show available sabotage types
            foreach (var option in options)
            {
                GameObject btnObj = new GameObject($"SabotageBtn_{option}");
                btnObj.transform.SetParent(contentContainer, false);
                
                Button btn = btnObj.AddComponent<Button>();
                Image img = btnObj.AddComponent<Image>();
                img.color = new Color(0.3f, 0.2f, 0.15f);
                
                RectTransform rect = btnObj.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(0, 35);
                
                // Text
                Text t = UIFactory.CreateText(btnObj.transform, "Text",
                    GetSabotageDisplayName(option), 12, TextAnchor.MiddleCenter, 
                    new Color(0.9f, 0.7f, 0.5f));
                
                btn.onClick.AddListener(() => {
                    PerformSabotage(option, provinceId);
                });
                
                currentCards.Add(btnObj);
            }
        }
        
        private void CreateProgressBar(Transform parent, int current, int max, string label, Color color)
        {
            GameObject barBg = new GameObject("ProgressBar");
            barBg.transform.SetParent(parent, false);
            
            RectTransform bgRect = barBg.AddComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(0, 16);
            
            Image bg = barBg.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            
            // Fill
            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(barBg.transform, false);
            
            RectTransform fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            float ratio = (float)current / max;
            fillRect.anchorMax = new Vector2(ratio, 1f);
            fillRect.offsetMin = new Vector2(2, 2);
            fillRect.offsetMax = new Vector2(-2, -2);
            
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = color;
            
            // Label
            Text t = UIFactory.CreateText(barBg.transform, "Label",
                $"{label} ({current}/{max})", 10, TextAnchor.MiddleCenter, Color.white);
            RectTransform tRect = t.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
        }
        
        private Button CreateActionButton(Transform parent, string text, UnityEngine.Events.UnityAction action)
        {
            GameObject btnObj = new GameObject($"Btn_{text}");
            btnObj.transform.SetParent(parent, false);
            
            Button btn = btnObj.AddComponent<Button>();
            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.2f, 0.25f, 0.35f);
            
            RectTransform rect = btnObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(80, 25);
            
            Text t = UIFactory.CreateText(btnObj.transform, "Text", text, 
                10, TextAnchor.MiddleCenter, new Color(0.8f, 0.9f, 1f));
            
            btn.onClick.AddListener(action);
            
            return btn;
        }
        
        private void PerformSabotage(SabotageType type, string provinceId)
        {
            // Find an agent with network in this province
            var agents = AgentManager.Instance?.GetAgentsForFaction(currentFaction)
                .Where(a => a.hasEstablishedNetwork && a.networkProvinceId == provinceId)
                .ToList();
            
            if (agents == null || agents.Count == 0) return;
            
            // Use the first available agent
            var result = AgentManager.Instance?.PerformSabotage(agents[0], type, provinceId);
            
            if (result != null)
            {
                // Show result notification
                string msg = result.success 
                    ? $"<color=green>✓ {result.description}</color>"
                    : $"<color=red>✗ {result.description}</color>";
                
                Debug.Log($"[IntelligencePanel] Sabotage result: {msg}");
                
                // Refresh to show updated state
                RefreshPanel();
            }
        }
        
        // Event handlers
        private void OnIntelReceived(AgentData agent, IntelReport report)
        {
            if (agent.faction == currentFaction)
            {
                // Flash notification or sound
                Debug.Log($"[IntelligencePanel] New intel received from {agent.agentName}");
                RefreshPanel();
            }
        }
        
        private void OnNetworkCreated(AgentData agent, string provinceId)
        {
            if (agent.faction == currentFaction)
            {
                Debug.Log($"[IntelligencePanel] Spy network established in {provinceId}!");
                RefreshPanel();
            }
        }
        
        private void OnAgentCapturedEvent(AgentData agent, string provinceId)
        {
            if (agent.faction == currentFaction)
            {
                Debug.Log($"[IntelligencePanel] ALERT: {agent.agentName} captured in {provinceId}!");
                RefreshPanel();
            }
        }
        
        private void OnAgentMovedEvent(AgentData agent, string provinceId)
        {
            if (agent.faction == currentFaction)
            {
                RefreshPanel();
            }
        }
        
        // Helper methods
        private void ClearCards()
        {
            foreach (var card in currentCards)
            {
                if (card != null) Destroy(card);
            }
            currentCards.Clear();
        }
        
        private void HighlightCard(GameObject card)
        {
            // Reset all cards
            foreach (var c in currentCards)
            {
                Image img = c?.GetComponent<Image>();
                if (img != null)
                {
                    Color cCol = img.color;
                    cCol.a = 0.9f;
                    img.color = cCol;
                }
            }
            
            // Highlight selected
            Image selectedImg = card?.GetComponent<Image>();
            if (selectedImg != null)
            {
                selectedImg.color = new Color(0.3f, 0.4f, 0.5f, 1f);
            }
        }
        
        private Color GetStateColor(AgentState state)
        {
            return state switch
            {
                AgentState.Idle => new Color(0.2f, 0.25f, 0.3f, 0.9f),
                AgentState.GatheringIntel => new Color(0.15f, 0.35f, 0.25f, 0.9f),
                AgentState.EstablishingNetwork => new Color(0.15f, 0.25f, 0.4f, 0.9f),
                AgentState.Moving => new Color(0.25f, 0.25f, 0.3f, 0.9f),
                AgentState.Hiding => new Color(0.3f, 0.25f, 0.15f, 0.9f),
                AgentState.Captured => new Color(0.4f, 0.1f, 0.1f, 0.9f),
                AgentState.Dead => new Color(0.2f, 0.2f, 0.2f, 0.5f),
                _ => new Color(0.2f, 0.2f, 0.2f, 0.9f)
            };
        }
        
        private Color GetStateTextColor(AgentState state)
        {
            return state switch
            {
                AgentState.GatheringIntel => new Color(0.4f, 0.9f, 0.4f),
                AgentState.EstablishingNetwork => new Color(0.4f, 0.7f, 0.9f),
                AgentState.Captured => new Color(0.9f, 0.3f, 0.3f),
                _ => new Color(0.7f, 0.7f, 0.7f)
            };
        }
        
        private string GetAgentTypeIcon(AgentType type)
        {
            return type switch
            {
                AgentType.Spy => "🕵️",
                AgentType.Diplomat => "📜",
                AgentType.Assassin => "🗡️",
                AgentType.Saboteur => "⚡",
                _ => "👤"
            };
        }
        
        private string GetIntelIcon(IntelType type)
        {
            return type switch
            {
                IntelType.ArmySize => "⚔️",
                IntelType.ArmyComposition => "👥",
                IntelType.ArmyMorale => "💪",
                IntelType.GarrisonSize => "🛡️",
                IntelType.FortificationStatus => "🏰",
                IntelType.CommanderInfo => "👑",
                IntelType.PublicOrder => "📢",
                IntelType.EconomicStatus => "💰",
                IntelType.DiplomaticRelations => "🤝",
                IntelType.SecretPlans => "📜",
                _ => "ℹ️"
            };
        }
        
        private string GetSabotageDisplayName(SabotageType type)
        {
            return type switch
            {
                SabotageType.OpenGates => "🚪 Open Gates (-25% Fort)",
                SabotageType.PoisonWells => "💧 Poison Wells (-30% Stamina)",
                SabotageType.BurnSupplies => "🔥 Burn Supplies (-50% Ammo)",
                SabotageType.InciteDesertion => "🏃 Incite Desertion",
                SabotageType.AssassinateGeneral => "🗡️ Assassinate General",
                SabotageType.SabotageArtillery => "💥 Sabotage Artillery",
                SabotageType.SpreadDisease => "☠️ Spread Disease",
                SabotageType.BribeGarrison => "💰 Bribe Garrison",
                _ => type.ToString()
            };
        }
        
        /// <summary>
        /// Set the province for sabotage operations
        /// </summary>
        public void SetSelectedProvince(string provinceId)
        {
            selectedProvinceId = provinceId;
            if (panelRoot != null && panelRoot.activeSelf)
            {
                RefreshPanel();
            }
        }
        
        /// <summary>
        /// Show sabotage options for a specific province
        /// </summary>
        public void ShowSabotageForProvince(string provinceId)
        {
            selectedProvinceId = provinceId;
            Show();
        }
    }
}
