using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Data;
using NapoleonicWars.UI;

namespace NapoleonicWars.Campaign.UI
{
    /// <summary>
    /// UI Panel for managing research assignments.
    /// Shows researchers, active projects, and allows assignment/unassignment.
    /// </summary>
    public class ResearchAssignmentPanel : MonoBehaviour
    {
        public static ResearchAssignmentPanel Instance { get; private set; }
        
        [Header("UI References")]
        public GameObject panelRoot;
        public Transform researchersContainer;
        public Transform projectsContainer;
        public Transform availableTechContainer;
        public Text totalResearchOutputText;
        
        // Internal state
        private Canvas canvas;
        private FactionType currentFaction;
        private List<GameObject> currentCards = new List<GameObject>();
        private Researcher selectedResearcher;
        private ResearchProject selectedProject;
        
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
                Debug.LogError("[ResearchAssignmentPanel] No Canvas found in parent!");
                return;
            }
            
            // Subscribe to events
            if (ResearchAssignmentManager.Instance != null)
            {
                ResearchAssignmentManager.Instance.OnResearcherRecruited += OnResearcherRecruited;
                ResearchAssignmentManager.Instance.OnProjectStarted += OnProjectStarted;
                ResearchAssignmentManager.Instance.OnProjectCompleted += OnProjectCompleted;
                ResearchAssignmentManager.Instance.OnResearcherAssigned += OnResearcherAssigned;
                ResearchAssignmentManager.Instance.OnResearcherUnassigned += OnResearcherUnassigned;
            }
            
            currentFaction = CampaignManager.Instance?.PlayerFaction ?? FactionType.France;
            
            CreatePanelUI();
            Hide();
        }
        
        private void OnDestroy()
        {
            if (ResearchAssignmentManager.Instance != null)
            {
                ResearchAssignmentManager.Instance.OnResearcherRecruited -= OnResearcherRecruited;
                ResearchAssignmentManager.Instance.OnProjectStarted -= OnProjectStarted;
                ResearchAssignmentManager.Instance.OnProjectCompleted -= OnProjectCompleted;
                ResearchAssignmentManager.Instance.OnResearcherAssigned -= OnResearcherAssigned;
                ResearchAssignmentManager.Instance.OnResearcherUnassigned -= OnResearcherUnassigned;
            }
        }
        
        public void Show()
        {
            panelRoot?.SetActive(true);
            RefreshPanel();
        }
        
        public void Hide()
        {
            panelRoot?.SetActive(false);
        }
        
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
            if (panelRoot == null)
            {
                panelRoot = new GameObject("ResearchAssignmentPanel");
                panelRoot.transform.SetParent(canvas.transform, false);
                
                RectTransform rect = panelRoot.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(900, 700);
                
                Image bg = panelRoot.AddComponent<Image>();
                bg.color = new Color(0.08f, 0.05f, 0.04f, 0.98f);
                
                // Create layout
                VerticalLayoutGroup mainVlg = panelRoot.AddComponent<VerticalLayoutGroup>();
                mainVlg.padding = new RectOffset(20, 20, 20, 20);
                mainVlg.spacing = 15f;
                mainVlg.childAlignment = TextAnchor.UpperCenter;
                
                // Title
                Text title = UIFactory.CreateText(panelRoot.transform, "Title", "🔬 RESEARCH MANAGEMENT", 24, TextAnchor.MiddleCenter, new Color(0.9f, 0.8f, 0.6f));
                title.fontStyle = FontStyle.Bold;
                
                // Create three-column layout
                GameObject columns = new GameObject("Columns");
                columns.transform.SetParent(panelRoot.transform, false);
                RectTransform colsRect = columns.AddComponent<RectTransform>();
                colsRect.sizeDelta = new Vector2(0, 550);
                HorizontalLayoutGroup hlg = columns.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 15f;
                hlg.childControlWidth = true;
                
                // Column 1: Researchers
                CreateColumn(columns.transform, "Researchers", "🎩 Researchers", out researchersContainer);
                
                // Column 2: Active Projects
                CreateColumn(columns.transform, "Projects", "📋 Active Projects", out projectsContainer);
                
                // Column 3: Available Tech
                CreateColumn(columns.transform, "AvailableTech", "📚 Available Research", out availableTechContainer);
                
                // Total output display
                totalResearchOutputText = UIFactory.CreateText(panelRoot.transform, "TotalOutput", "", 14, TextAnchor.MiddleCenter, new Color(0.6f, 0.9f, 0.6f));
                
                // Close button
                Button closeBtn = UIFactory.CreateGoldButton(panelRoot.transform, "BtnClose", "Close", 14, () => Hide());
                RectTransform closeRect = closeBtn.GetComponent<RectTransform>();
                closeRect.sizeDelta = new Vector2(120, 35);
            }
        }
        
        private void CreateColumn(Transform parent, string name, string header, out Transform container)
        {
            GameObject col = new GameObject(name + "Column");
            col.transform.SetParent(parent, false);
            RectTransform colRect = col.AddComponent<RectTransform>();
            colRect.sizeDelta = new Vector2(280, 0);
            
            VerticalLayoutGroup vlg = col.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.padding = new RectOffset(10, 10, 10, 10);
            
            // Header
            Text headerText = UIFactory.CreateText(col.transform, "Header", header, 16, TextAnchor.MiddleCenter, new Color(0.9f, 0.7f, 0.4f));
            headerText.fontStyle = FontStyle.Bold;
            
            // Scroll view for content
            GameObject scrollGO = new GameObject(name + "Scroll");
            scrollGO.transform.SetParent(col.transform, false);
            RectTransform scrollRect = scrollGO.AddComponent<RectTransform>();
            scrollRect.sizeDelta = new Vector2(0, 500);
            
            ScrollRect sr = scrollGO.AddComponent<ScrollRect>();
            sr.horizontal = false;
            
            GameObject content = new GameObject("Content");
            content.transform.SetParent(scrollGO.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 500);
            
            VerticalLayoutGroup contentVlg = content.AddComponent<VerticalLayoutGroup>();
            contentVlg.spacing = 6f;
            contentVlg.padding = new RectOffset(5, 5, 5, 5);
            contentVlg.childControlWidth = true;
            
            ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            sr.content = contentRect;
            
            container = content.transform;
        }
        
        public void RefreshPanel()
        {
            ClearCards();
            
            if (ResearchAssignmentManager.Instance == null) return;
            
            currentFaction = CampaignManager.Instance?.PlayerFaction ?? FactionType.France;
            
            // === RESEARCHERS ===
            var researchers = ResearchAssignmentManager.Instance.GetResearchersForFaction(currentFaction);
            
            // Add recruit button
            Button recruitBtn = UIFactory.CreateGoldButton(researchersContainer, "BtnRecruit", 
                $"+ Recruit ({Researcher.GetRecruitmentCost(ResearcherType.Gentleman)}g)", 11, () => ShowRecruitMenu());
            currentCards.Add(recruitBtn.gameObject);
            
            if (researchers.Count == 0)
            {
                CreateInfoText(researchersContainer, "No researchers. Recruit gentlemen to start research.");
            }
            else
                       {
                foreach (var researcher in researchers)
                {
                    CreateResearcherCard(researcher);
                }
            }
            
            // === ACTIVE PROJECTS ===
            var projects = ResearchAssignmentManager.Instance.GetProjectsForFaction(currentFaction);
            if (projects.Count == 0)
            {
                CreateInfoText(projectsContainer, "No active research projects.");
            }
            else
            {
                foreach (var project in projects)
                {
                    CreateProjectCard(project);
                }
            }
            
            // === AVAILABLE TECH ===
            ShowAvailableTechnologies();
            
            // Update total output
            float totalOutput = ResearchAssignmentManager.Instance.GetTotalResearchOutput(currentFaction);
            totalResearchOutputText.text = $"📊 Total Research Output: {totalOutput:F1} points/turn";
        }
        
        private void CreateResearcherCard(Researcher researcher)
        {
            GameObject card = new GameObject($"Researcher_{researcher.researcherId}");
            card.transform.SetParent(researchersContainer, false);
            
            Image bg = card.AddComponent<Image>();
            bg.color = researcher.isAssigned 
                ? new Color(0.15f, 0.3f, 0.2f, 0.9f)  // Green when assigned
                : new Color(0.2f, 0.15f, 0.12f, 0.9f); // Brown when idle
            
            RectTransform rect = card.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 70);
            
            VerticalLayoutGroup vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 6, 6);
            vlg.spacing = 2f;
            
            // Name row
            GameObject nameRow = new GameObject("NameRow");
            nameRow.transform.SetParent(card.transform, false);
            UIFactory.AddHorizontalLayout(nameRow, 5f);
            
            Text nameText = UIFactory.CreateText(nameRow.transform, "Name",
                $"{Researcher.GetTypeIcon(researcher.type)} {researcher.name}", 12, TextAnchor.MiddleLeft, Color.white);
            nameText.fontStyle = FontStyle.Bold;
            
            // Skill stars
            string stars = new string('★', researcher.skill);
            Text skillText = UIFactory.CreateText(nameRow.transform, "Skill", stars, 10, TextAnchor.MiddleRight, new Color(1f, 0.8f, 0.2f));
            
            // Stats
            Text statsText = UIFactory.CreateText(card.transform, "Stats",
                $"{Researcher.GetTypeName(researcher.type)} | Output: {researcher.baseResearchPoints:F0}/turn", 
                10, TextAnchor.MiddleLeft, new Color(0.7f, 0.7f, 0.7f));
            
            // Assignment status
            if (researcher.isAssigned)
            {
                var project = ResearchAssignmentManager.Instance.GetProjectsForFaction(currentFaction)
                    .FirstOrDefault(p => p.projectId == researcher.assignedProjectId);
                if (project != null)
                {
                    Text assignText = UIFactory.CreateText(card.transform, "Assignment",
                        $"→ {project.techName}", 10, TextAnchor.MiddleLeft, new Color(0.4f, 0.9f, 0.4f));
                }
            }
            else
            {
                Text idleText = UIFactory.CreateText(card.transform, "Status",
                    "[Idle - Click to Assign]", 10, TextAnchor.MiddleLeft, new Color(0.7f, 0.5f, 0.3f));
            }
            
            // Click to select/assign
            Button btn = card.AddComponent<Button>();
            btn.onClick.AddListener(() => OnResearcherClicked(researcher));
            
            currentCards.Add(card);
        }
        
        private void CreateProjectCard(ResearchProject project)
        {
            GameObject card = new GameObject($"Project_{project.projectId}");
            card.transform.SetParent(projectsContainer, false);
            
            Image bg = card.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.18f, 0.25f, 0.9f);
            
            RectTransform rect = card.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 100);
            
            VerticalLayoutGroup vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 6, 6);
            vlg.spacing = 4f;
            
            // Tech name
            Text nameText = UIFactory.CreateText(card.transform, "Name",
                project.techName, 13, TextAnchor.MiddleLeft, new Color(0.6f, 0.8f, 1f));
            nameText.fontStyle = FontStyle.Bold;
            
            // Category and researchers
            Text infoText = UIFactory.CreateText(card.transform, "Info",
                $"{project.category} | {project.GetResearcherCount()} researcher(s)", 
                10, TextAnchor.MiddleLeft, new Color(0.7f, 0.7f, 0.7f));
            
            // Progress bar
            CreateProgressBar(card.transform, project.GetProgressPercent() / 100f, 
                $"{(int)project.GetProgressPercent()}%");
            
            // Estimated completion
            var researchers = ResearchAssignmentManager.Instance.GetResearchersForFaction(currentFaction);
            int turnsLeft = project.GetEstimatedTurnsRemaining(researchers);
            Text etaText = UIFactory.CreateText(card.transform, "ETA",
                turnsLeft > 0 ? $"~{turnsLeft} turns remaining" : "Stalled - no researchers",
                10, TextAnchor.MiddleLeft, turnsLeft > 0 ? new Color(0.5f, 0.9f, 0.5f) : Color.red);
            
            // Upkeep cost
            Text upkeepText = UIFactory.CreateText(card.transform, "Upkeep",
                $"Upkeep: {project.GetUpkeepCostPerTurn()}g/turn", 9, TextAnchor.MiddleRight, new Color(0.9f, 0.7f, 0.3f));
            
            // Click to select project for assignment
            Button btn = card.AddComponent<Button>();
            btn.onClick.AddListener(() => OnProjectClicked(project));
            
            currentCards.Add(card);
        }
        
        private void ShowAvailableTechnologies()
        {
            TechTree techTree = CampaignManager.Instance?.GetPlayerTechTree();
            if (techTree == null) return;
            
            var available = techTree.GetAvailableTechnologies();
            
            if (available.Count == 0)
            {
                CreateInfoText(availableTechContainer, "No technologies available for research.");
                return;
            }
            
            foreach (var tech in available)
            {
                // Skip if already being researched
                if (ResearchAssignmentManager.Instance.IsResearchingTech(tech.id, currentFaction))
                    continue;
                
                GameObject card = new GameObject($"Tech_{tech.id}");
                card.transform.SetParent(availableTechContainer, false);
                
                Image bg = card.AddComponent<Image>();
                bg.color = new Color(0.2f, 0.15f, 0.1f, 0.9f);
                
                RectTransform rect = card.AddComponent<RectTransform>();
                rect.sizeDelta = new Vector2(0, 80);
                
                VerticalLayoutGroup vlg = card.AddComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(8, 8, 6, 6);
                vlg.spacing = 3f;
                
                // Tech name
                Text nameText = UIFactory.CreateText(card.transform, "Name",
                    tech.name, 12, TextAnchor.MiddleLeft, Color.white);
                nameText.fontStyle = FontStyle.Bold;
                
                // Description (truncated)
                string desc = tech.description;
                if (desc.Length > 60) desc = desc.Substring(0, 57) + "...";
                Text descText = UIFactory.CreateText(card.transform, "Desc",
                    desc, 9, TextAnchor.MiddleLeft, new Color(0.7f, 0.7f, 0.7f));
                
                // Cost and category
                Text costText = UIFactory.CreateText(card.transform, "Cost",
                    $"{tech.category} | Cost: {tech.researchCost}g | Turns: ~{tech.turnsToResearch}",
                    10, TextAnchor.MiddleLeft, new Color(0.9f, 0.7f, 0.4f));
                
                // Start research button
                Button startBtn = UIFactory.CreateGoldButton(card.transform, "BtnStart", "Start Research", 10, () =>
                {
                    StartResearchProject(tech);
                });
                
                currentCards.Add(card);
            }
        }
        
        private void CreateProgressBar(Transform parent, float fillAmount, string label)
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
            fillRect.anchorMax = new Vector2(fillAmount, 1f);
            fillRect.offsetMin = new Vector2(2, 2);
            fillRect.offsetMax = new Vector2(-2, -2);
            
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.3f, 0.7f, 0.3f);
            
            // Label
            Text labelText = UIFactory.CreateText(barBg.transform, "Label",
                label, 10, TextAnchor.MiddleCenter, Color.white);
            RectTransform labelRect = labelText.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
        }
        
        private void CreateInfoText(Transform parent, string text)
        {
            GameObject info = new GameObject("Info");
            info.transform.SetParent(parent, false);
            
            Text t = info.AddComponent<Text>();
            t.text = text;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 11;
            t.color = new Color(0.5f, 0.5f, 0.5f);
            t.alignment = TextAnchor.MiddleCenter;
            
            RectTransform rect = info.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 40);
            
            currentCards.Add(info);
        }
        
        private void ShowRecruitMenu()
        {
            // Simple implementation - recruit a gentleman in current selected province
            string provinceId = CampaignUI.Instance?.selectedProvinceId;
            if (string.IsNullOrEmpty(provinceId))
            {
                Debug.Log("[ResearchPanel] Select a province with a research building first!");
                return;
            }
            
            var researcher = ResearchAssignmentManager.Instance?.RecruitResearcher(
                ResearcherType.Gentleman, currentFaction, provinceId);
            
            if (researcher != null)
            {
                RefreshPanel();
            }
        }
        
        private void StartResearchProject(Technology tech)
        {
            string provinceId = CampaignUI.Instance?.selectedProvinceId;
            if (string.IsNullOrEmpty(provinceId))
            {
                Debug.Log("[ResearchPanel] Select a province with a research building first!");
                return;
            }
            
            var project = ResearchAssignmentManager.Instance?.StartResearchProject(
                tech, currentFaction, provinceId);
            
            if (project != null)
            {
                RefreshPanel();
            }
        }
        
        private void OnResearcherClicked(Researcher researcher)
        {
            if (researcher.isIdle)
            {
                // Show assignment dialog or auto-assign to selected project
                selectedResearcher = researcher;
                
                // If a project is selected, assign to it
                if (selectedProject != null)
                {
                    ResearchAssignmentManager.Instance?.AssignResearcherToProject(
                        researcher.researcherId, selectedProject.projectId);
                    selectedResearcher = null;
                    selectedProject = null;
                    RefreshPanel();
                }
                else
                {
                    Debug.Log($"[ResearchPanel] Selected {researcher.name}. Now select a project to assign them to.");
                }
            }
            else
            {
                // Unassign
                ResearchAssignmentManager.Instance?.UnassignResearcher(researcher.researcherId);
                RefreshPanel();
            }
        }
        
        private void OnProjectClicked(ResearchProject project)
        {
            selectedProject = project;
            
            // If a researcher is selected, assign them
            if (selectedResearcher != null)
            {
                ResearchAssignmentManager.Instance?.AssignResearcherToProject(
                    selectedResearcher.researcherId, project.projectId);
                selectedResearcher = null;
                selectedProject = null;
                RefreshPanel();
            }
            else
            {
                Debug.Log($"[ResearchPanel] Selected project '{project.techName}'. Click a researcher to assign them.");
            }
        }
        
        // Event handlers
        private void OnResearcherRecruited(Researcher r)
        {
            if (panelRoot != null && panelRoot.activeSelf)
                RefreshPanel();
        }
        
        private void OnProjectStarted(ResearchProject p)
        {
            if (panelRoot != null && panelRoot.activeSelf)
                RefreshPanel();
        }
        
        private void OnProjectCompleted(ResearchProject p)
        {
            if (panelRoot != null && panelRoot.activeSelf)
            {
                RefreshPanel();
                // Show completion notification
                Debug.Log($"🎉 Research completed: {p.techName}!");
            }
        }
        
        private void OnResearcherAssigned(Researcher r, ResearchProject p)
        {
            if (panelRoot != null && panelRoot.activeSelf)
                RefreshPanel();
        }
        
        private void OnResearcherUnassigned(Researcher r, ResearchProject p)
        {
            if (panelRoot != null && panelRoot.activeSelf)
                RefreshPanel();
        }
        
        private void ClearCards()
        {
            foreach (var card in currentCards)
            {
                if (card != null) Destroy(card);
            }
            currentCards.Clear();
        }
    }
}
