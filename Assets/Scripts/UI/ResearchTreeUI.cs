using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Campaign;
using NapoleonicWars.Data;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// Full branching tech tree UI with prerequisite connection lines,
    /// category swimlanes, and visual node states.
    /// </summary>
    public class ResearchTreeUI : MonoBehaviour
    {
        private CampaignManager campaignManager;
        private FactionData playerFaction;
        private NavigationBar navBar;

        // Layout
        private Dictionary<string, RectTransform> techNodes = new Dictionary<string, RectTransform>();
        private Dictionary<string, Vector2> nodeCenters = new Dictionary<string, Vector2>();
        private RectTransform contentRT;

        // Colors
        private static readonly Color LockedBg    = new Color(0.12f, 0.12f, 0.14f, 0.92f);
        private static readonly Color LockedBorder = new Color(0.30f, 0.30f, 0.30f);
        private static readonly Color AvailableBg  = new Color(0.10f, 0.22f, 0.45f, 0.95f);
        private static readonly Color AvailableBorder = new Color(0.30f, 0.55f, 0.90f);
        private static readonly Color ResearchingBg = new Color(0.10f, 0.40f, 0.15f, 0.95f);
        private static readonly Color ResearchingBorder = new Color(0.30f, 0.80f, 0.30f);
        private static readonly Color CompletedBg  = new Color(0.45f, 0.38f, 0.12f, 0.95f);
        private static readonly Color CompletedBorder = new Color(0.85f, 0.70f, 0.25f);

        private static readonly Color LineCompleted  = new Color(0.85f, 0.70f, 0.20f, 0.80f);
        private static readonly Color LineAvailable  = new Color(0.40f, 0.60f, 0.90f, 0.60f);
        private static readonly Color LineLocked     = new Color(0.35f, 0.35f, 0.35f, 0.40f);
        private static readonly Color LineResearching = new Color(0.30f, 0.80f, 0.30f, 0.70f);

        // Layout constants
        private const float NODE_W = 175f;
        private const float NODE_H = 62f;
        private const float COL_GAP = 45f;    // horizontal gap between tier columns
        private const float ROW_GAP = 12f;    // vertical gap between nodes in same column
        private const float CAT_GAP = 24f;    // extra gap between category swimlanes
        private const float LABEL_W = 110f;   // category label width
        private const float HEADER_H = 32f;   // tier header height
        private const float LEFT_PAD = 8f;
        private const float TOP_PAD = 8f;
        private const float LINE_THICKNESS = 2.5f;
        private const float ARROW_SIZE = 6f;

        public static ResearchTreeUI Create(NavigationBar navBar, CampaignManager manager)
        {
            var overlay = navBar.CreateOverlayPanel("ARBRE DE RECHERCHE");
            if (overlay == null) return null;

            // Enable height control so the scroll view fills available space
            var parentVLG = overlay.GetComponent<VerticalLayoutGroup>();
            if (parentVLG != null)
            {
                parentVLG.childControlHeight = true;
                parentVLG.childForceExpandHeight = false;
            }

            var ui = overlay.AddComponent<ResearchTreeUI>();
            ui.navBar = navBar;
            ui.campaignManager = manager;
            ui.playerFaction = manager.GetPlayerFaction();
            navBar.RegisterPanel(NavigationBar.NavPanel.Research, overlay);

            ui.BuildTree(overlay.transform);
            return ui;
        }

        // ================================================================
        //  MAIN BUILD
        // ================================================================
        private void BuildTree(Transform parent)
        {
            TechTree tree = playerFaction.techTree;
            var allTechs = tree.GetAllTechs();

            // === ACTIVE RESEARCH HEADER ===
            BuildResearchHeader(parent, tree);

            // === SCROLL AREA ===
            var (scroll, content) = UIFactory.CreateScrollView(parent, "TechTreeScroll");
            UIFactory.AddLayoutElement(scroll.gameObject, flexibleHeight: 1, preferredHeight: 800);
            scroll.horizontal = true;
            scroll.vertical = true;

            // Disable VLG + ContentSizeFitter, use absolute positioning
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) vlg.enabled = false;
            var csf = content.GetComponent<ContentSizeFitter>();
            if (csf != null) csf.enabled = false;
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(0, 1);
            content.pivot = new Vector2(0, 1);
            contentRT = content;

            // Compute tiers (depth from prerequisites)
            var techTiers = ComputeAllTechTiers(allTechs);
            int maxTier = 0;
            foreach (var t in techTiers.Values)
                if (t > maxTier) maxTier = t;
            int numCols = maxTier + 1;

            // Group by subcategory
            var subcategories = GroupTechsBySubcategory(allTechs);
            string[] subcatNames = { "⚔ Infanterie", "💣 Artillerie", "🐎 Cavalerie",
                                     "🏰 Naval / Fort", "💰 Économie", "📜 Diplomatie" };
            Color[] subcatColors = {
                new Color(0.20f, 0.15f, 0.10f, 0.90f),
                new Color(0.18f, 0.12f, 0.10f, 0.90f),
                new Color(0.15f, 0.18f, 0.12f, 0.90f),
                new Color(0.12f, 0.15f, 0.20f, 0.90f),
                new Color(0.18f, 0.18f, 0.10f, 0.90f),
                new Color(0.15f, 0.12f, 0.18f, 0.90f),
            };

            float colWidth = NODE_W + COL_GAP;

            // === TIER COLUMN HEADERS ===
            for (int c = 0; c < numCols; c++)
            {
                float hdrX = LEFT_PAD + LABEL_W + c * colWidth;
                RectTransform hdr = CreateAbsPanel(content, $"TierHdr_{c}",
                    new Color(0.18f, 0.14f, 0.08f, 0.90f), hdrX, TOP_PAD, NODE_W, HEADER_H);
                string label = c == 0 ? "Base" : $"Ère {c}";
                Text hdrText = UIFactory.CreateText(hdr, "T", label, 12,
                    TextAnchor.MiddleCenter, UIFactory.GoldAccent);
                hdrText.fontStyle = FontStyle.Bold;
                FillAnchors(hdrText);
            }

            float currentY = TOP_PAD + HEADER_H + 8f;

            // === SUBCATEGORY SWIMLANES ===
            for (int s = 0; s < subcategories.Length; s++)
            {
                var techs = subcategories[s];
                if (techs.Count == 0) continue;

                // Count max stacked nodes per tier
                int[] countPerTier = new int[numCols];
                foreach (var tech in techs)
                {
                    int tier = GetTier(techTiers, tech.id, numCols);
                    countPerTier[tier]++;
                }
                int maxPerTier = 1;
                for (int c = 0; c < numCols; c++)
                    if (countPerTier[c] > maxPerTier) maxPerTier = countPerTier[c];

                float swimlaneH = maxPerTier * (NODE_H + ROW_GAP) + 4f;

                // Swimlane background
                float totalW = LABEL_W + numCols * colWidth + 20f;
                RectTransform swimBg = CreateAbsPanel(content, $"Swim_{s}",
                    new Color(subcatColors[s].r, subcatColors[s].g, subcatColors[s].b, 0.25f),
                    LEFT_PAD, currentY - 2, totalW, swimlaneH + 4);
                swimBg.GetComponent<Image>().raycastTarget = false;

                // Category label
                RectTransform catLabel = CreateAbsPanel(content, $"Cat_{s}",
                    subcatColors[s], LEFT_PAD, currentY, LABEL_W - 4, swimlaneH);
                Text catText = UIFactory.CreateText(catLabel, "Text", subcatNames[s], 11,
                    TextAnchor.MiddleCenter, UIFactory.GoldAccent);
                catText.fontStyle = FontStyle.Bold;
                FillAnchors(catText);

                // Place tech nodes
                int[] tierIdx = new int[numCols];
                foreach (var tech in techs)
                {
                    int tier = GetTier(techTiers, tech.id, numCols);
                    float x = LEFT_PAD + LABEL_W + tier * colWidth;
                    float y = currentY + tierIdx[tier] * (NODE_H + ROW_GAP);
                    CreateTechNode(content, tech, tree, x, y);
                    tierIdx[tier]++;
                }

                currentY += swimlaneH + CAT_GAP;
            }

            // === DOCTRINE SECTION ===
            currentY = BuildDoctrineSection(content, currentY, numCols, colWidth);

            // === DRAW CONNECTION LINES (behind nodes, in front of background) ===
            // Create a line container that sits behind nodes
            GameObject lineContainer = new GameObject("Lines");
            lineContainer.transform.SetParent(content, false);
            RectTransform lineRT = lineContainer.AddComponent<RectTransform>();
            lineRT.anchorMin = new Vector2(0, 1);
            lineRT.anchorMax = new Vector2(0, 1);
            lineRT.pivot = new Vector2(0, 1);
            lineRT.anchoredPosition = Vector2.zero;
            lineRT.sizeDelta = new Vector2(4000, 4000);
            // Move lines behind nodes (first sibling)
            lineContainer.transform.SetSiblingIndex(0);

            // Draw prerequisite lines
            foreach (var tech in allTechs.Values)
            {
                if (tech.prerequisites == null || tech.prerequisites.Length == 0) continue;
                if (!nodeCenters.ContainsKey(tech.id)) continue;

                Vector2 toCenter = nodeCenters[tech.id];
                float toLeft = toCenter.x - NODE_W / 2f;

                foreach (string prereqId in tech.prerequisites)
                {
                    if (!nodeCenters.ContainsKey(prereqId)) continue;

                    Vector2 fromCenter = nodeCenters[prereqId];
                    float fromRight = fromCenter.x + NODE_W / 2f;

                    // Determine line color based on state
                    Color lineColor = GetLineColor(prereqId, tech.id, tree);

                    // Draw L-shaped connector: horizontal from prereq right → midpoint, then vertical, then horizontal to target left
                    float midX = (fromRight + toLeft) / 2f;

                    // Horizontal segment from prereq → midX
                    DrawLine(lineContainer.transform, fromCenter.x + NODE_W / 2f, fromCenter.y,
                             midX, fromCenter.y, lineColor);

                    // Vertical segment from prereq.y → target.y at midX
                    if (Mathf.Abs(fromCenter.y - toCenter.y) > 2f)
                    {
                        DrawLine(lineContainer.transform, midX, fromCenter.y,
                                 midX, toCenter.y, lineColor);
                    }

                    // Horizontal segment from midX → target
                    DrawLine(lineContainer.transform, midX, toCenter.y,
                             toCenter.x - NODE_W / 2f, toCenter.y, lineColor);

                    // Arrow head at target
                    DrawArrowHead(lineContainer.transform, toCenter.x - NODE_W / 2f, toCenter.y, lineColor);
                }
            }

            // Set content size
            float totalContentW = LEFT_PAD + LABEL_W + numCols * colWidth + 40f;
            content.sizeDelta = new Vector2(Mathf.Max(totalContentW, 1800f), currentY + 30f);
        }

        // ================================================================
        //  RESEARCH HEADER (active slots)
        // ================================================================
        private void BuildResearchHeader(Transform parent, TechTree tree)
        {
            RectTransform slotsPanel = UIFactory.CreatePanel(parent, "SlotsPanel",
                new Color(0.09f, 0.06f, 0.05f, 0.95f));
            UIFactory.AddLayoutElement(slotsPanel.gameObject, preferredHeight: 55);

            HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(slotsPanel.gameObject,
                10f, new RectOffset(15, 15, 8, 8));
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            Text label = UIFactory.CreateText(slotsPanel, "Label",
                "Recherche active:", 14, TextAnchor.MiddleLeft, UIFactory.GoldAccent);
            UIFactory.AddLayoutElement(label.gameObject, preferredWidth: 170, preferredHeight: 38);

            if (tree.IsResearching())
            {
                string techId = tree.CurrentResearchId;
                var allTechs = tree.GetAllTechs();
                string techName = allTechs.ContainsKey(techId) ? allTechs[techId].name : techId;
                int turnsLeft = tree.CurrentResearchTurnsLeft;

                RectTransform slotRT = UIFactory.CreateBorderedPanel(slotsPanel, "ActiveSlot",
                    ResearchingBg, ResearchingBorder, 2f);
                UIFactory.AddLayoutElement(slotRT.gameObject, preferredWidth: 320, preferredHeight: 38);

                Text slotText = UIFactory.CreateText(slotRT.transform.GetChild(0), "Text",
                    $"⟳ {techName}  ({turnsLeft} tours restants)", 12,
                    TextAnchor.MiddleCenter, new Color(0.5f, 1f, 0.5f));
                slotText.fontStyle = FontStyle.Bold;
                FillAnchors(slotText, 8);
            }
            else
            {
                RectTransform slotRT = UIFactory.CreateBorderedPanel(slotsPanel, "EmptySlot",
                    LockedBg, LockedBorder, 1.5f);
                UIFactory.AddLayoutElement(slotRT.gameObject, preferredWidth: 220, preferredHeight: 38);

                Text slotText = UIFactory.CreateText(slotRT.transform.GetChild(0), "Text",
                    "□ Aucune recherche en cours", 12,
                    TextAnchor.MiddleCenter, UIFactory.TextGrey);
                FillAnchors(slotText, 5);
            }
        }

        // ================================================================
        //  TECH NODE
        // ================================================================
        private void CreateTechNode(Transform parent, Technology tech, TechTree tree, float x, float y)
        {
            bool researched = tree.IsResearched(tech.id);
            bool researching = tree.IsResearching(tech.id);
            bool available = tree.CanResearch(tech.id);

            Color bgColor, borderColor;
            string statusIcon;
            Color nameColor;

            if (researched)
            {
                bgColor = CompletedBg; borderColor = CompletedBorder;
                statusIcon = "✓"; nameColor = new Color(1f, 0.90f, 0.50f);
            }
            else if (researching)
            {
                bgColor = ResearchingBg; borderColor = ResearchingBorder;
                statusIcon = "⟳"; nameColor = new Color(0.50f, 1f, 0.50f);
            }
            else if (available)
            {
                bgColor = AvailableBg; borderColor = AvailableBorder;
                statusIcon = "○"; nameColor = UIFactory.TextWhite;
            }
            else
            {
                bgColor = LockedBg; borderColor = LockedBorder;
                statusIcon = "🔒"; nameColor = UIFactory.TextGrey;
            }

            // Main bordered node
            RectTransform nodeRT = UIFactory.CreateBorderedPanel(parent, $"Tech_{tech.id}",
                bgColor, borderColor, 2f);
            nodeRT.anchorMin = new Vector2(0, 1);
            nodeRT.anchorMax = new Vector2(0, 1);
            nodeRT.pivot = new Vector2(0, 1);
            nodeRT.anchoredPosition = new Vector2(x, -y);
            nodeRT.sizeDelta = new Vector2(NODE_W, NODE_H);

            Transform inner = nodeRT.transform.GetChild(0);

            // Button for click
            Button btn = nodeRT.gameObject.AddComponent<Button>();
            btn.targetGraphic = nodeRT.GetComponent<Image>();
            string capturedId = tech.id;
            btn.onClick.AddListener(() => OnTechClicked(capturedId));

            // Hover color feedback
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.3f, 1.2f, 1.1f);
            cb.pressedColor = new Color(0.8f, 0.7f, 0.5f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            // Status icon (left corner)
            Text iconText = UIFactory.CreateText(inner, "Icon", statusIcon, 14,
                TextAnchor.MiddleCenter, nameColor);
            RectTransform iconRT = iconText.GetComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0, 0.45f);
            iconRT.anchorMax = new Vector2(0, 1);
            iconRT.offsetMin = new Vector2(4, 0);
            iconRT.offsetMax = new Vector2(22, -2);

            // Tech name
            Text nameText = UIFactory.CreateText(inner, "Name", tech.name, 11,
                TextAnchor.MiddleLeft, nameColor);
            nameText.fontStyle = FontStyle.Bold;
            RectTransform nameRT = nameText.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0, 0.45f);
            nameRT.anchorMax = new Vector2(1, 1);
            nameRT.offsetMin = new Vector2(22, 0);
            nameRT.offsetMax = new Vector2(-4, -2);

            // Cost / turns
            string costStr = $"{tech.turnsToResearch} tours";
            if (researching) costStr = $"{tree.CurrentResearchTurnsLeft} tours restants";
            Text costText = UIFactory.CreateText(inner, "Cost", costStr, 9,
                TextAnchor.MiddleLeft, researching ? new Color(0.5f, 1f, 0.5f) : UIFactory.TextGrey);
            RectTransform costRT = costText.GetComponent<RectTransform>();
            costRT.anchorMin = new Vector2(0, 0);
            costRT.anchorMax = new Vector2(0.55f, 0.45f);
            costRT.offsetMin = new Vector2(6, 2);
            costRT.offsetMax = new Vector2(0, 0);

            // Bonus summary (right side, bottom)
            string bonus = GetBonusSummary(tech);
            if (!string.IsNullOrEmpty(bonus))
            {
                Text bonusText = UIFactory.CreateText(inner, "Bonus", bonus, 8,
                    TextAnchor.MiddleRight, UIFactory.ParchmentBeige);
                RectTransform bonusRT = bonusText.GetComponent<RectTransform>();
                bonusRT.anchorMin = new Vector2(0.45f, 0);
                bonusRT.anchorMax = new Vector2(1, 0.45f);
                bonusRT.offsetMin = new Vector2(0, 2);
                bonusRT.offsetMax = new Vector2(-6, 0);
            }

            // Progress bar if researching
            if (researching)
            {
                var allTechs = tree.GetAllTechs();
                if (allTechs.ContainsKey(tech.id))
                {
                    int total = allTechs[tech.id].turnsToResearch;
                    int remaining = tree.CurrentResearchTurnsLeft;
                    float pct = total > 0 ? 1f - (float)remaining / total : 0f;

                    RectTransform barBg = CreateAbsPanel(nodeRT, "BarBg",
                        new Color(0.05f, 0.05f, 0.05f, 0.8f), 0, 0, 0, 0);
                    barBg.anchorMin = new Vector2(0, 0);
                    barBg.anchorMax = new Vector2(1, 0);
                    barBg.offsetMin = new Vector2(2, 2);
                    barBg.offsetMax = new Vector2(-2, 6);

                    RectTransform barFill = UIFactory.CreatePanel(barBg, "Fill",
                        new Color(0.3f, 0.85f, 0.3f, 0.9f));
                    barFill.anchorMin = Vector2.zero;
                    barFill.anchorMax = new Vector2(Mathf.Clamp01(pct), 1f);
                    barFill.offsetMin = Vector2.zero;
                    barFill.offsetMax = Vector2.zero;
                }
            }

            // Store position for line drawing (center of node in content-space)
            techNodes[tech.id] = nodeRT;
            nodeCenters[tech.id] = new Vector2(x + NODE_W / 2f, y + NODE_H / 2f);
        }

        // ================================================================
        //  LINE DRAWING
        // ================================================================
        private void DrawLine(Transform parent, float x1, float y1, float x2, float y2, Color color)
        {
            // Y is inverted in content space (positive = downward)
            GameObject lineGO = new GameObject("Line");
            lineGO.transform.SetParent(parent, false);
            RectTransform rt = lineGO.AddComponent<RectTransform>();

            float dx = x2 - x1;
            float dy = y2 - y1;
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            if (length < 1f) return;

            float angle = Mathf.Atan2(-dy, dx) * Mathf.Rad2Deg;
            float cx = (x1 + x2) / 2f;
            float cy = -(y1 + y2) / 2f; // negate for UI space

            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(cx, cy);
            rt.sizeDelta = new Vector2(length, LINE_THICKNESS);
            rt.localRotation = Quaternion.Euler(0, 0, angle);

            Image img = lineGO.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        private void DrawArrowHead(Transform parent, float tipX, float tipY, Color color)
        {
            // Small triangle pointing right at (tipX, tipY)
            // Approximate with a diamond/square rotated 45°
            GameObject arrowGO = new GameObject("Arrow");
            arrowGO.transform.SetParent(parent, false);
            RectTransform rt = arrowGO.AddComponent<RectTransform>();

            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(tipX - ARROW_SIZE / 2, -tipY);
            rt.sizeDelta = new Vector2(ARROW_SIZE, ARROW_SIZE);
            rt.localRotation = Quaternion.Euler(0, 0, 45f);

            Image img = arrowGO.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        private Color GetLineColor(string fromId, string toId, TechTree tree)
        {
            if (tree.IsResearched(fromId) && tree.IsResearched(toId))
                return LineCompleted;
            if (tree.IsResearched(fromId) && tree.IsResearching(toId))
                return LineResearching;
            if (tree.IsResearched(fromId) && tree.CanResearch(toId))
                return LineAvailable;
            return LineLocked;
        }

        // ================================================================
        //  DOCTRINE SECTION (kept similar to original)
        // ================================================================
        private float BuildDoctrineSection(RectTransform content, float startY, int numCols, float colWidth)
        {
            float currentY = startY;
            float docWidth = Mathf.Max(LABEL_W + numCols * colWidth, 1800f);
            DoctrineType chosenDoctrine = DoctrineTree.GetChosenDoctrine(playerFaction.factionType);

            // Section header
            RectTransform docHeader = CreateAbsPanel(content, "DocHeader",
                new Color(0.16f, 0.09f, 0.06f, 0.95f), LEFT_PAD, currentY, docWidth, 34);
            string headerStr = chosenDoctrine != DoctrineType.None
                ? $"DOCTRINE MILITAIRE: {DoctrineTree.GetDoctrineName(chosenDoctrine)}"
                : "DOCTRINES MILITAIRES (choix irréversible)";
            Text docTitle = UIFactory.CreateText(docHeader, "Title", headerStr, 15,
                TextAnchor.MiddleCenter, UIFactory.GoldAccent);
            docTitle.fontStyle = FontStyle.Bold;
            FillAnchors(docTitle);
            currentY += 40;

            // Cumulative bonuses
            if (chosenDoctrine != DoctrineType.None)
            {
                var (atk, def, spd, org, mp) = DoctrineTree.GetTotalBonuses(playerFaction.factionType);
                string bonusStr = $"Bonus cumulés:  ATK +{atk * 100:F0}%  |  DÉF +{def * 100:F0}%  |  VIT +{spd * 100:F0}%  |  ORG +{org:F0}  |  MP +{mp * 100:F0}%";
                RectTransform bonusRT = CreateAbsPanel(content, "DocBonuses",
                    new Color(0.08f, 0.14f, 0.08f, 0.90f), LEFT_PAD, currentY, docWidth, 28);
                Text bonusText = UIFactory.CreateText(bonusRT, "Text", bonusStr, 11,
                    TextAnchor.MiddleCenter, new Color(0.5f, 0.9f, 0.5f));
                FillAnchors(bonusText);
                currentY += 32;
            }

            // 4 doctrine columns
            DoctrineType[] docTypes = { DoctrineType.GrandeBatterie, DoctrineType.GuerreDeMouvement,
                                        DoctrineType.DoctrineDefensive, DoctrineType.DoctrineDeLaMasse };
            float docColW = (docWidth - 50f) / 4f;
            float docNodeH = 58f;
            float docStartY = currentY;

            for (int d = 0; d < docTypes.Length; d++)
            {
                float docX = LEFT_PAD + d * (docColW + 10f);
                float docY = docStartY;
                DoctrineType dt = docTypes[d];
                bool isChosen = chosenDoctrine == dt;
                bool isLocked = chosenDoctrine != DoctrineType.None && !isChosen;

                Color docBg = isChosen ? new Color(0.12f, 0.25f, 0.12f, 0.95f) :
                              isLocked ? new Color(0.10f, 0.10f, 0.10f, 0.60f) :
                              AvailableBg;
                Color docBorder = isChosen ? new Color(0.3f, 0.8f, 0.3f) :
                                  isLocked ? LockedBorder : UIFactory.BorderGold;

                // Doctrine header card
                RectTransform docCard = UIFactory.CreateBorderedPanel(content, $"DocHead_{d}",
                    docBg, docBorder, 2f);
                docCard.anchorMin = new Vector2(0, 1);
                docCard.anchorMax = new Vector2(0, 1);
                docCard.pivot = new Vector2(0, 1);
                docCard.anchoredPosition = new Vector2(docX, -docY);
                docCard.sizeDelta = new Vector2(docColW, 42);

                Transform docInner = docCard.transform.GetChild(0);
                string prefix = isChosen ? "✓ " : isLocked ? "✗ " : "";
                Text dName = UIFactory.CreateText(docInner, "Name",
                    prefix + DoctrineTree.GetDoctrineName(dt), 13,
                    TextAnchor.MiddleCenter, isChosen ? new Color(0.5f, 1f, 0.5f) :
                    isLocked ? UIFactory.TextGrey : UIFactory.GoldAccent);
                dName.fontStyle = FontStyle.Bold;
                FillAnchors(dName, 5);

                if (chosenDoctrine == DoctrineType.None)
                {
                    DoctrineType capturedType = dt;
                    Button chooseBtn = docCard.gameObject.AddComponent<Button>();
                    chooseBtn.targetGraphic = docCard.GetComponent<Image>();
                    chooseBtn.onClick.AddListener(() =>
                    {
                        DoctrineTree.ChooseDoctrine(playerFaction.factionType, capturedType);
                        navBar.TogglePanel(NavigationBar.NavPanel.Research);
                        navBar.TogglePanel(NavigationBar.NavPanel.Research);
                    });
                }
                docY += 48;

                // Doctrine nodes (vertical chain with lines)
                var nodes = DoctrineTree.GetDoctrineNodes(dt);
                DoctrineNode prevNode = null;
                float prevY = 0;
                foreach (var node in nodes)
                {
                    bool canRes = isChosen && CanResearchDoctrineNode(nodes, node);
                    Color nodeBg = node.isResearched ? CompletedBg :
                                   canRes ? AvailableBg : LockedBg;
                    Color nodeBorder = node.isResearched ? CompletedBorder :
                                       canRes ? AvailableBorder : LockedBorder;

                    RectTransform nRT = UIFactory.CreateBorderedPanel(content, $"DocNode_{node.nodeId}",
                        nodeBg, nodeBorder, 1.5f);
                    nRT.anchorMin = new Vector2(0, 1);
                    nRT.anchorMax = new Vector2(0, 1);
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
                    nnRT.anchorMin = new Vector2(0, 0.45f);
                    nnRT.anchorMax = new Vector2(0.75f, 1);
                    nnRT.offsetMin = new Vector2(8, 0);
                    nnRT.offsetMax = new Vector2(0, -3);

                    Text nCost = UIFactory.CreateText(nodeInner, "Cost",
                        $"{node.researchCost} tours", 9, TextAnchor.UpperRight, UIFactory.TextGrey);
                    RectTransform ncRT = nCost.GetComponent<RectTransform>();
                    ncRT.anchorMin = new Vector2(0.75f, 0.5f);
                    ncRT.anchorMax = new Vector2(1, 1);
                    ncRT.offsetMin = new Vector2(0, 0);
                    ncRT.offsetMax = new Vector2(-6, -3);

                    Text nDesc = UIFactory.CreateText(nodeInner, "Desc", node.description, 9,
                        TextAnchor.UpperLeft, UIFactory.ParchmentBeige);
                    RectTransform ndRT = nDesc.GetComponent<RectTransform>();
                    ndRT.anchorMin = new Vector2(0, 0);
                    ndRT.anchorMax = new Vector2(1, 0.45f);
                    ndRT.offsetMin = new Vector2(8, 3);
                    ndRT.offsetMax = new Vector2(-6, 0);

                    if (isChosen && !node.isResearched && canRes)
                    {
                        string capturedNodeId = node.nodeId;
                        Button nodeBtn = nRT.gameObject.AddComponent<Button>();
                        nodeBtn.targetGraphic = nRT.GetComponent<Image>();
                        nodeBtn.onClick.AddListener(() => ResearchDoctrineNode(capturedNodeId));
                    }

                    // Draw vertical connector line from previous node
                    if (prevNode != null)
                    {
                        Color lineCol = prevNode.isResearched && node.isResearched ? LineCompleted :
                                        prevNode.isResearched ? LineAvailable : LineLocked;
                        float lineX = docX + docColW / 2f;
                        float lineTop = prevY + docNodeH;
                        float lineBot = docY;
                        if (lineBot > lineTop)
                        {
                            RectTransform connector = CreateAbsPanel(content, "DocLine",
                                lineCol, lineX - 1.5f, lineTop, 3f, lineBot - lineTop);
                            connector.GetComponent<Image>().raycastTarget = false;
                        }
                    }

                    prevNode = node;
                    prevY = docY;
                    docY += docNodeH + 6;
                }
            }

            // Find tallest doctrine column
            float maxDocBottom = docStartY;
            foreach (var dt in docTypes)
            {
                float colH = 48 + DoctrineTree.GetDoctrineNodes(dt).Count * (docNodeH + 6);
                if (docStartY + colH > maxDocBottom) maxDocBottom = docStartY + colH;
            }
            return maxDocBottom + 10;
        }

        // ================================================================
        //  CLICK HANDLERS
        // ================================================================
        private void OnTechClicked(string techId)
        {
            TechTree tree = playerFaction.techTree;
            if (tree.CanResearch(techId) && !tree.IsResearching())
            {
                tree.StartResearch(techId, playerFaction);
                Debug.Log($"[ResearchTreeUI] Started research: {techId}");
                navBar.TogglePanel(NavigationBar.NavPanel.Research);
                navBar.TogglePanel(NavigationBar.NavPanel.Research);
            }
        }

        private bool CanResearchDoctrineNode(List<DoctrineNode> allNodes, DoctrineNode node)
        {
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
                Debug.Log($"[ResearchTreeUI] Researched doctrine: {node.displayName}");
                navBar.TogglePanel(NavigationBar.NavPanel.Research);
                navBar.TogglePanel(NavigationBar.NavPanel.Research);
            }
        }

        // ================================================================
        //  CLASSIFICATION HELPERS
        // ================================================================
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
                else
                {
                    string id = tech.id;
                    if (IsNavalFortTech(id))       navalFort.Add(tech);
                    else if (IsArtilleryTech(id))  artillery.Add(tech);
                    else if (IsCavalryTech(id))    cavalry.Add(tech);
                    else                           infantry.Add(tech);
                }
            }

            // Sort by research cost for consistent ordering
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

        // ================================================================
        //  TIER COMPUTATION
        // ================================================================
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

        private int GetTier(Dictionary<string, int> tiers, string techId, int maxCols)
        {
            int tier = tiers.ContainsKey(techId) ? tiers[techId] : 0;
            return Mathf.Clamp(tier, 0, maxCols - 1);
        }

        // ================================================================
        //  BONUS SUMMARY
        // ================================================================
        private string GetBonusSummary(Technology tech)
        {
            var parts = new List<string>();
            if (tech.damageBonus > 0)      parts.Add($"+{tech.damageBonus * 100:F0}% dmg");
            if (tech.accuracyBonus > 0)    parts.Add($"+{tech.accuracyBonus * 100:F0}% pré");
            if (tech.moraleBonus > 0)      parts.Add($"+{tech.moraleBonus:F0} moral");
            if (tech.speedBonus > 0)       parts.Add($"+{tech.speedBonus * 100:F0}% vit");
            if (tech.goldIncomeBonus > 0)  parts.Add($"+{tech.goldIncomeBonus * 100:F0}% or");
            if (tech.foodBonus > 0)        parts.Add($"+{tech.foodBonus * 100:F0}% nour");
            if (tech.ironBonus > 0)        parts.Add($"+{tech.ironBonus * 100:F0}% fer");
            if (tech.unlocksUnitType)      parts.Add($"🔓 {tech.unlockedUnit}");
            if (tech.unlocksBuilding)      parts.Add($"🏗 bâtiment");
            if (tech.recruitCostReduction > 0) parts.Add($"-{tech.recruitCostReduction * 100:F0}% coût");
            if (tech.maintenanceReduction > 0) parts.Add($"-{tech.maintenanceReduction * 100:F0}% maint");
            if (tech.diplomacyBonus > 0)   parts.Add($"+{tech.diplomacyBonus:F0} diplo");

            if (parts.Count == 0) return "";
            if (parts.Count <= 2) return string.Join(" | ", parts);
            return string.Join(" | ", parts.GetRange(0, 2)) + "…";
        }

        // ================================================================
        //  UI HELPERS
        // ================================================================
        private RectTransform CreateAbsPanel(Transform parent, string name, Color color,
            float x, float y, float w, float h)
        {
            RectTransform rt = UIFactory.CreatePanel(parent, name, color);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        private void FillAnchors(Text text, float pad = 0)
        {
            RectTransform rt = text.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pad, 0);
            rt.offsetMax = new Vector2(-pad, 0);
        }
    }
}
