using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Campaign;
using NapoleonicWars.Data;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// HoI4-style visual tech tree. Shows techs in a grid (5 eras × 5 categories) with
    /// prerequisite lines, color-coded status, and clickable nodes.
    /// </summary>
    public class ResearchTreeUI : MonoBehaviour
    {
        private CampaignManager campaignManager;
        private FactionData playerFaction;
        private NavigationBar navBar;
        private float colWidth; // computed dynamically to fill screen
        private Dictionary<string, RectTransform> techNodes = new Dictionary<string, RectTransform>();

        // Colors
        private static readonly Color LockedColor = new Color(0.25f, 0.25f, 0.25f, 0.90f);
        private static readonly Color AvailableColor = new Color(0.15f, 0.30f, 0.55f, 0.95f);
        private static readonly Color ResearchingColor = new Color(0.15f, 0.50f, 0.20f, 0.95f);
        private static readonly Color CompletedColor = new Color(0.55f, 0.45f, 0.15f, 0.95f);

        public static ResearchTreeUI Create(NavigationBar navBar, CampaignManager manager)
        {
            var overlay = navBar.CreateOverlayPanel("RECHERCHE & TECHNOLOGIE");
            if (overlay == null) return null;

            var ui = overlay.AddComponent<ResearchTreeUI>();
            ui.navBar = navBar;
            ui.campaignManager = manager;
            ui.playerFaction = manager.GetPlayerFaction();
            navBar.RegisterPanel(NavigationBar.NavPanel.Research, overlay);

            ui.BuildContent(overlay.transform);
            return ui;
        }

        private void BuildContent(Transform parent)
        {
            TechTree tree = playerFaction.techTree;

            // === ACTIVE RESEARCH SLOTS ===
            RectTransform slotsPanel = UIFactory.CreatePanel(parent, "SlotsPanel", new Color(0.09f, 0.06f, 0.05f, 0.95f));
            UIFactory.AddLayoutElement(slotsPanel.gameObject, preferredHeight: 55);
            
            HorizontalLayoutGroup slotsHLG = UIFactory.AddHorizontalLayout(slotsPanel.gameObject, 10f, new RectOffset(15, 15, 8, 8));
            slotsHLG.childControlWidth = false;
            slotsHLG.childControlHeight = true;

            Text slotsLabel = UIFactory.CreateText(slotsPanel, "Label", "Slots de recherche:", 14, TextAnchor.MiddleLeft, UIFactory.GoldAccent);
            UIFactory.AddLayoutElement(slotsLabel.gameObject, preferredWidth: 170, preferredHeight: 38);

            // Show active research slots
            int maxSlots = 2; // Base 2, +1 per university
            var researching = tree.GetAvailableTechnologies();
            for (int i = 0; i < maxSlots; i++)
            {
                string slotText = i < 1 && tree.IsResearching() ? "■ En cours" : "□ Vide";
                Color slotColor = slotText.StartsWith("■") ? ResearchingColor : LockedColor;
                
                RectTransform slotRT = UIFactory.CreateBorderedPanel(slotsPanel, $"Slot{i}", slotColor, UIFactory.BorderGold, 1.5f);
                UIFactory.AddLayoutElement(slotRT.gameObject, preferredWidth: 180, preferredHeight: 38);
                
                Text slotLabel = UIFactory.CreateText(slotRT.transform.GetChild(0), "Text", slotText, 12, TextAnchor.MiddleCenter, UIFactory.TextWhite);
                RectTransform slotLabelRT = slotLabel.GetComponent<RectTransform>();
                slotLabelRT.anchorMin = Vector2.zero;
                slotLabelRT.anchorMax = Vector2.one;
                slotLabelRT.offsetMin = new Vector2(5, 0);
                slotLabelRT.offsetMax = new Vector2(-5, 0);
            }

            // === TECH TREE SCROLL ===
            var (scroll, content) = UIFactory.CreateScrollView(parent, "TechTreeScroll");
            UIFactory.AddLayoutElement(scroll.gameObject, flexibleHeight: 1, preferredHeight: 800);
            scroll.horizontal = true;
            scroll.vertical = true;

            content.GetComponent<VerticalLayoutGroup>().enabled = false;
            // Use absolute positioning: anchors at top-left so sizeDelta.x = absolute width
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(0, 1);
            content.pivot = new Vector2(0, 1);

            // Group ALL techs by subcategory and compute tier from prerequisite depth
            var allTechs = tree.GetAllTechs();
            var subcategories = GroupTechsBySubcategory(allTechs);
            var techTiers = ComputeAllTechTiers(allTechs);

            int maxTier = 0;
            foreach (var t in techTiers.Values)
                if (t > maxTier) maxTier = t;
            int numCols = maxTier + 1;

            string[] subcatNames = { "Infanterie", "Artillerie", "Cavalerie", "Naval / Fort", "Économie", "Diplomatie" };

            float labelWidth = 105f;
            colWidth = (1850f - labelWidth - 20f) / Mathf.Max(numCols, 1);
            float nodeHeight = 52f;
            float nodeGapY = 6f;
            float rowGap = 14f;
            float headerHeight = 28f;

            // === TIER COLUMN HEADERS ===
            for (int c = 0; c < numCols; c++)
            {
                RectTransform hdr = UIFactory.CreatePanel(content, $"TierHdr_{c}",
                    new Color(0.14f, 0.10f, 0.06f, 0.85f));
                hdr.anchorMin = new Vector2(0, 1);
                hdr.anchorMax = new Vector2(0, 1);
                hdr.pivot = new Vector2(0, 1);
                hdr.anchoredPosition = new Vector2(labelWidth + c * colWidth, 0);
                hdr.sizeDelta = new Vector2(colWidth - 5, headerHeight);
                string tierLabel = c == 0 ? "Base" : $"Niveau {c}";
                Text hdrText = UIFactory.CreateText(hdr, "T", tierLabel, 11,
                    TextAnchor.MiddleCenter, UIFactory.GoldAccent);
                hdrText.fontStyle = FontStyle.Bold;
                RectTransform hdrRT = hdrText.GetComponent<RectTransform>();
                hdrRT.anchorMin = Vector2.zero; hdrRT.anchorMax = Vector2.one;
                hdrRT.offsetMin = Vector2.zero; hdrRT.offsetMax = Vector2.zero;
            }

            float currentY = headerHeight + 5;

            // === SUBCATEGORY ROWS ===
            for (int s = 0; s < subcategories.Length; s++)
            {
                var techs = subcategories[s];
                if (techs.Count == 0) continue;

                // Find max techs stacked in any tier column for this subcategory
                int[] countPerTier = new int[numCols];
                foreach (var tech in techs)
                {
                    int tier = techTiers.ContainsKey(tech.id) ? techTiers[tech.id] : 0;
                    tier = Mathf.Clamp(tier, 0, numCols - 1);
                    countPerTier[tier]++;
                }
                int maxPerTier = 1;
                for (int c = 0; c < numCols; c++)
                    if (countPerTier[c] > maxPerTier) maxPerTier = countPerTier[c];

                float rowHeight = maxPerTier * (nodeHeight + nodeGapY);

                // Category label (left column)
                RectTransform catLabel = UIFactory.CreatePanel(content, $"Cat_{subcatNames[s]}",
                    new Color(0.16f, 0.18f, 0.15f, 0.95f));
                catLabel.anchorMin = new Vector2(0, 1);
                catLabel.anchorMax = new Vector2(0, 1);
                catLabel.pivot = new Vector2(0, 1);
                catLabel.anchoredPosition = new Vector2(3, -currentY);
                catLabel.sizeDelta = new Vector2(labelWidth - 6, rowHeight);
                Text catText = UIFactory.CreateText(catLabel, "Text", subcatNames[s], 12,
                    TextAnchor.MiddleCenter, UIFactory.GoldAccent);
                catText.fontStyle = FontStyle.Bold;
                RectTransform catTextRT = catText.GetComponent<RectTransform>();
                catTextRT.anchorMin = Vector2.zero; catTextRT.anchorMax = Vector2.one;
                catTextRT.offsetMin = Vector2.zero; catTextRT.offsetMax = Vector2.zero;

                // Place tech nodes in tier columns
                int[] tierIdx = new int[numCols];
                foreach (var tech in techs)
                {
                    int tier = techTiers.ContainsKey(tech.id) ? techTiers[tech.id] : 0;
                    tier = Mathf.Clamp(tier, 0, numCols - 1);
                    float x = labelWidth + tier * colWidth;
                    float y = currentY + tierIdx[tier] * (nodeHeight + nodeGapY);
                    CreateTechNode(content, tech, tree, x, y);
                    tierIdx[tier]++;
                }

                currentY += rowHeight + rowGap;
            }

            // === INTERACTIVE DOCTRINE SECTION ===
            float docWidth = Mathf.Max(labelWidth + numCols * colWidth, 1830f);
            DoctrineType chosenDoctrine = DoctrineTree.GetChosenDoctrine(playerFaction.factionType);

            RectTransform docHeader = UIFactory.CreatePanel(content, "DocHeader",
                new Color(0.16f, 0.09f, 0.06f, 0.95f));
            docHeader.anchorMin = new Vector2(0, 1);
            docHeader.anchorMax = new Vector2(0, 1);
            docHeader.pivot = new Vector2(0, 1);
            docHeader.anchoredPosition = new Vector2(3, -currentY);
            docHeader.sizeDelta = new Vector2(docWidth, 32);

            string headerStr = chosenDoctrine != DoctrineType.None
                ? $"DOCTRINE MILITAIRE: {DoctrineTree.GetDoctrineName(chosenDoctrine)}"
                : "DOCTRINES MILITAIRES (choisir une — choix irréversible)";
            Text docTitle = UIFactory.CreateText(docHeader, "Title", headerStr, 14,
                TextAnchor.MiddleCenter, UIFactory.GoldAccent);
            docTitle.fontStyle = FontStyle.Bold;
            RectTransform docTitleRT = docTitle.GetComponent<RectTransform>();
            docTitleRT.anchorMin = Vector2.zero; docTitleRT.anchorMax = Vector2.one;
            docTitleRT.offsetMin = Vector2.zero; docTitleRT.offsetMax = Vector2.zero;
            currentY += 38;

            // Show cumulative bonuses if doctrine chosen
            if (chosenDoctrine != DoctrineType.None)
            {
                var (atk, def, spd, org, mp) = DoctrineTree.GetTotalBonuses(playerFaction.factionType);
                string bonusStr = $"Bonus cumulés:  Attaque +{atk * 100:F0}%  |  Défense +{def * 100:F0}%  |  Vitesse +{spd * 100:F0}%  |  Org +{org:F0}  |  Manpower +{mp * 100:F0}%";
                RectTransform bonusRT = UIFactory.CreatePanel(content, "DocBonuses",
                    new Color(0.08f, 0.14f, 0.08f, 0.90f));
                bonusRT.anchorMin = new Vector2(0, 1); bonusRT.anchorMax = new Vector2(0, 1);
                bonusRT.pivot = new Vector2(0, 1);
                bonusRT.anchoredPosition = new Vector2(3, -currentY);
                bonusRT.sizeDelta = new Vector2(docWidth, 26);
                Text bonusText = UIFactory.CreateText(bonusRT, "Text", bonusStr, 11,
                    TextAnchor.MiddleCenter, new Color(0.5f, 0.9f, 0.5f));
                RectTransform btRT = bonusText.GetComponent<RectTransform>();
                btRT.anchorMin = Vector2.zero; btRT.anchorMax = Vector2.one;
                btRT.offsetMin = Vector2.zero; btRT.offsetMax = Vector2.zero;
                currentY += 30;
            }

            // 4 doctrine columns side by side
            DoctrineType[] docTypes = { DoctrineType.GrandeBatterie, DoctrineType.GuerreDeMouvement,
                                        DoctrineType.DoctrineDefensive, DoctrineType.DoctrineDeLaMasse };
            float docColW = (1830f - 45f) / 4f;
            float docNodeH = 55f;
            float docStartY = currentY;

            for (int d = 0; d < docTypes.Length; d++)
            {
                float docX = 5f + d * (docColW + 10f);
                float docY = docStartY;
                DoctrineType dt = docTypes[d];
                bool isChosen = chosenDoctrine == dt;
                bool isLocked = chosenDoctrine != DoctrineType.None && !isChosen;

                // Doctrine header card
                Color docBg = isChosen ? new Color(0.12f, 0.25f, 0.12f, 0.95f) :
                              isLocked ? new Color(0.10f, 0.10f, 0.10f, 0.60f) :
                              AvailableColor;
                Color docBorder = isChosen ? new Color(0.3f, 0.8f, 0.3f) :
                                  isLocked ? new Color(0.3f, 0.3f, 0.3f) :
                                  UIFactory.BorderGold;

                RectTransform docCard = UIFactory.CreateBorderedPanel(content, $"DocHead_{d}",
                    docBg, docBorder, 2f);
                docCard.anchorMin = new Vector2(0, 1); docCard.anchorMax = new Vector2(0, 1);
                docCard.pivot = new Vector2(0, 1);
                docCard.anchoredPosition = new Vector2(docX, -docY);
                docCard.sizeDelta = new Vector2(docColW, 40);

                Transform docInner = docCard.transform.GetChild(0);
                string docPrefix = isChosen ? "✓ " : isLocked ? "✗ " : "";
                Text dName = UIFactory.CreateText(docInner, "Name",
                    docPrefix + DoctrineTree.GetDoctrineName(dt), 13,
                    TextAnchor.MiddleCenter, isChosen ? new Color(0.5f, 1f, 0.5f) :
                    isLocked ? UIFactory.TextGrey : UIFactory.GoldAccent);
                dName.fontStyle = FontStyle.Bold;
                RectTransform dNameRT = dName.GetComponent<RectTransform>();
                dNameRT.anchorMin = Vector2.zero; dNameRT.anchorMax = Vector2.one;
                dNameRT.offsetMin = new Vector2(5, 0); dNameRT.offsetMax = new Vector2(-5, 0);

                // Click to choose doctrine (only if none chosen)
                if (chosenDoctrine == DoctrineType.None)
                {
                    DoctrineType capturedType = dt;
                    Button chooseBtn = docCard.gameObject.AddComponent<Button>();
                    chooseBtn.targetGraphic = docCard.GetComponent<Image>();
                    chooseBtn.onClick.AddListener(() => {
                        DoctrineTree.ChooseDoctrine(playerFaction.factionType, capturedType);
                        navBar.TogglePanel(NavigationBar.NavPanel.Research);
                        navBar.TogglePanel(NavigationBar.NavPanel.Research);
                    });
                }
                docY += 46;

                // Doctrine nodes (vertical stack)
                var nodes = DoctrineTree.GetDoctrineNodes(dt);
                foreach (var node in nodes)
                {
                    Color nodeBg = node.isResearched ? CompletedColor :
                                   (isChosen && CanResearchDoctrineNode(nodes, node)) ? AvailableColor :
                                   LockedColor;
                    Color nodeBorder = node.isResearched ? new Color(0.7f, 0.6f, 0.2f) :
                                       (isChosen && CanResearchDoctrineNode(nodes, node)) ? UIFactory.BorderGold :
                                       new Color(0.3f, 0.3f, 0.3f);

                    RectTransform nRT = UIFactory.CreateBorderedPanel(content, $"DocNode_{node.nodeId}",
                        nodeBg, nodeBorder, 1.5f);
                    nRT.anchorMin = new Vector2(0, 1); nRT.anchorMax = new Vector2(0, 1);
                    nRT.pivot = new Vector2(0, 1);
                    nRT.anchoredPosition = new Vector2(docX, -docY);
                    nRT.sizeDelta = new Vector2(docColW, docNodeH);

                    Transform nodeInner = nRT.transform.GetChild(0);
                    string nIcon = node.isResearched ? "✓" : "○";
                    Text nName = UIFactory.CreateText(nodeInner, "Name",
                        $"{nIcon} {node.displayName}", 11, TextAnchor.UpperLeft,
                        node.isResearched ? new Color(1f, 0.9f, 0.5f) : UIFactory.TextWhite);
                    nName.fontStyle = FontStyle.Bold;
                    RectTransform nnRT = nName.GetComponent<RectTransform>();
                    nnRT.anchorMin = new Vector2(0, 0.45f); nnRT.anchorMax = new Vector2(0.75f, 1);
                    nnRT.offsetMin = new Vector2(8, 0); nnRT.offsetMax = new Vector2(0, -3);

                    Text nCost = UIFactory.CreateText(nodeInner, "Cost",
                        $"{node.researchCost} tours", 9, TextAnchor.UpperRight, UIFactory.TextGrey);
                    RectTransform ncRT = nCost.GetComponent<RectTransform>();
                    ncRT.anchorMin = new Vector2(0.75f, 0.5f); ncRT.anchorMax = new Vector2(1, 1);
                    ncRT.offsetMin = new Vector2(0, 0); ncRT.offsetMax = new Vector2(-6, -3);

                    Text nDesc = UIFactory.CreateText(nodeInner, "Desc", node.description, 9,
                        TextAnchor.UpperLeft, UIFactory.ParchmentBeige);
                    RectTransform ndRT = nDesc.GetComponent<RectTransform>();
                    ndRT.anchorMin = new Vector2(0, 0); ndRT.anchorMax = new Vector2(1, 0.45f);
                    ndRT.offsetMin = new Vector2(8, 3); ndRT.offsetMax = new Vector2(-6, 0);

                    // Click to research node
                    if (isChosen && !node.isResearched && CanResearchDoctrineNode(nodes, node))
                    {
                        string capturedNodeId = node.nodeId;
                        Button nodeBtn = nRT.gameObject.AddComponent<Button>();
                        nodeBtn.targetGraphic = nRT.GetComponent<Image>();
                        nodeBtn.onClick.AddListener(() => {
                            ResearchDoctrineNode(capturedNodeId);
                        });
                    }

                    docY += docNodeH + 5;
                }
            }

            float maxDocBottom = docStartY;
            foreach (var dt in docTypes)
            {
                float colH = 46 + DoctrineTree.GetDoctrineNodes(dt).Count * (docNodeH + 5);
                if (docStartY + colH > maxDocBottom) maxDocBottom = docStartY + colH;
            }
            currentY = maxDocBottom + 10;

            // Set content size to fit all rows + columns
            float totalWidth = Mathf.Max(labelWidth + numCols * colWidth + 20, 1830f);
            content.sizeDelta = new Vector2(Mathf.Max(totalWidth, 1600f), currentY + 30);
        }

        private void CreateTechNode(Transform parent, Technology tech, TechTree tree, float x, float y)
        {
            // Determine state and color
            Color bgColor;
            string statusIcon;
            if (tree.IsResearched(tech.id)) { bgColor = CompletedColor; statusIcon = "✓"; }
            else if (tree.IsResearching(tech.id)) { bgColor = ResearchingColor; statusIcon = "⟳"; }
            else if (tree.CanResearch(tech.id)) { bgColor = AvailableColor; statusIcon = "○"; }
            else { bgColor = LockedColor; statusIcon = "🔒"; }

            RectTransform nodeRT = UIFactory.CreateBorderedPanel(parent, $"Tech_{tech.id}", bgColor, UIFactory.BorderGold, 1.5f);
            nodeRT.anchorMin = new Vector2(0, 1);
            nodeRT.anchorMax = new Vector2(0, 1);
            nodeRT.pivot = new Vector2(0, 1);
            nodeRT.anchoredPosition = new Vector2(x, -y);
            nodeRT.sizeDelta = new Vector2(colWidth - 10f, 55);

            Transform inner = nodeRT.transform.GetChild(0);

            // Button for click
            Button btn = nodeRT.gameObject.AddComponent<Button>();
            btn.targetGraphic = nodeRT.GetComponent<Image>();
            string capturedId = tech.id;
            btn.onClick.AddListener(() => OnTechClicked(capturedId));

            // Tech name
            Text nameText = UIFactory.CreateText(inner, "Name", $"{statusIcon} {tech.name}", 12, TextAnchor.MiddleLeft, UIFactory.TextWhite);
            nameText.fontStyle = FontStyle.Bold;
            RectTransform nameRT = nameText.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0, 0.4f);
            nameRT.anchorMax = new Vector2(1, 1);
            nameRT.offsetMin = new Vector2(8, 0);
            nameRT.offsetMax = new Vector2(-8, -3);

            // Cost/time
            Text costText = UIFactory.CreateText(inner, "Cost", $"{tech.turnsToResearch} tours", 10, TextAnchor.MiddleLeft, UIFactory.TextGrey);
            RectTransform costRT = costText.GetComponent<RectTransform>();
            costRT.anchorMin = new Vector2(0, 0);
            costRT.anchorMax = new Vector2(1, 0.4f);
            costRT.offsetMin = new Vector2(8, 2);
            costRT.offsetMax = new Vector2(-8, 0);

            techNodes[tech.id] = nodeRT;
        }

        private void OnTechClicked(string techId)
        {
            TechTree tree = playerFaction.techTree;
            if (tree.CanResearch(techId) && !tree.IsResearching())
            {
                tree.StartResearch(techId, playerFaction);
                Debug.Log($"[ResearchTreeUI] Started research: {techId}");
                // Refresh UI
                navBar.TogglePanel(NavigationBar.NavPanel.Research);
                navBar.TogglePanel(NavigationBar.NavPanel.Research);
            }
        }

        private bool CanResearchDoctrineNode(List<DoctrineNode> allNodes, DoctrineNode node)
        {
            // Can research if all previous depth nodes are researched
            foreach (var n in allNodes)
            {
                if (n.depth < node.depth && !n.isResearched)
                    return false;
            }
            return true;
        }

        private void ResearchDoctrineNode(string nodeId)
        {
            DoctrineType chosen = DoctrineTree.GetChosenDoctrine(playerFaction.factionType);
            if (chosen == DoctrineType.None) return;
            var nodes = DoctrineTree.GetDoctrineNodes(chosen);
            var node = nodes.Find(n => n.nodeId == nodeId);
            if (node != null && !node.isResearched && CanResearchDoctrineNode(nodes, node))
            {
                node.isResearched = true;
                Debug.Log($"[ResearchTreeUI] Researched doctrine node: {node.displayName}");
                navBar.TogglePanel(NavigationBar.NavPanel.Research);
                navBar.TogglePanel(NavigationBar.NavPanel.Research);
            }
        }

        // =============== TECH CLASSIFICATION HELPERS ===============

        private List<Technology>[] GroupTechsBySubcategory(Dictionary<string, Technology> allTechs)
        {
            var infantry  = new List<Technology>();
            var artillery = new List<Technology>();
            var cavalry   = new List<Technology>();
            var navalFort = new List<Technology>();
            var economy   = new List<Technology>();
            var diplomacy = new List<Technology>();

            foreach (var tech in allTechs.Values)
            {
                if (tech.category == TechCategory.Economy)
                    economy.Add(tech);
                else if (tech.category == TechCategory.Diplomacy)
                    diplomacy.Add(tech);
                else // Military — subcategorize
                {
                    string id = tech.id;
                    if (IsNavalFortTech(id))       navalFort.Add(tech);
                    else if (IsArtilleryTech(id))  artillery.Add(tech);
                    else if (IsCavalryTech(id))    cavalry.Add(tech);
                    else                           infantry.Add(tech);
                }
            }

            // Sort each list by research cost so tier-0 techs come first
            infantry.Sort((a, b) => a.researchCost.CompareTo(b.researchCost));
            artillery.Sort((a, b) => a.researchCost.CompareTo(b.researchCost));
            cavalry.Sort((a, b) => a.researchCost.CompareTo(b.researchCost));
            navalFort.Sort((a, b) => a.researchCost.CompareTo(b.researchCost));
            economy.Sort((a, b) => a.researchCost.CompareTo(b.researchCost));
            diplomacy.Sort((a, b) => a.researchCost.CompareTo(b.researchCost));

            return new[] { infantry, artillery, cavalry, navalFort, economy, diplomacy };
        }

        private static bool IsArtilleryTech(string id)
        {
            return id.Contains("artillery") || id.Contains("cannon") || id.Contains("gunpowder") ||
                   id.Contains("canister") || id.Contains("shrapnel") || id.Contains("gribeauval") ||
                   id.Contains("elevation") || id.Contains("percussion") || id.Contains("rocket") ||
                   id.Contains("siege") || id.Contains("recoil") || id.Contains("indirect_fire") ||
                   id.Contains("explosive") || id == "bronze_casting" || id == "iron_barrel" ||
                   id == "steel_barrels" || id == "rifled_cannons";
        }

        private static bool IsCavalryTech(string id)
        {
            return id.Contains("cavalry") || id.Contains("riding") || id.Contains("carbine") ||
                   id.Contains("hussar") || id.Contains("lancer") || id.Contains("dragoon") ||
                   id.Contains("shock_tactics") || id.Contains("cuirassier") || id.Contains("mounted");
        }

        private static bool IsNavalFortTech(string id)
        {
            return id.Contains("ship") || id.Contains("fort") || id.Contains("polygonal");
        }

        // =============== TIER COMPUTATION ===============

        private Dictionary<string, int> ComputeAllTechTiers(Dictionary<string, Technology> allTechs)
        {
            var tiers = new Dictionary<string, int>();
            foreach (var tech in allTechs.Values)
                GetTechDepth(tech.id, allTechs, tiers);
            return tiers;
        }

        private int GetTechDepth(string techId, Dictionary<string, Technology> allTechs, Dictionary<string, int> cache)
        {
            if (cache.ContainsKey(techId)) return cache[techId];
            if (!allTechs.ContainsKey(techId)) { cache[techId] = 0; return 0; }

            Technology tech = allTechs[techId];
            int maxDepth = 0;
            if (tech.prerequisites != null)
            {
                foreach (string prereq in tech.prerequisites)
                {
                    int d = GetTechDepth(prereq, allTechs, cache) + 1;
                    if (d > maxDepth) maxDepth = d;
                }
            }
            cache[techId] = maxDepth;
            return maxDepth;
        }
    }
}
