using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Campaign;
using NapoleonicWars.Data;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// HoI4-style national construction queue panel. Shows all active construction projects
    /// with progress bars, workshop assignments, and priority ordering.
    /// </summary>
    public class ConstructionUI : MonoBehaviour
    {
        private CampaignManager campaignManager;
        private FactionData playerFaction;
        private NavigationBar navBar;

        public static ConstructionUI Create(NavigationBar navBar, CampaignManager manager)
        {
            var overlay = navBar.CreateOverlayPanel("CONSTRUCTION NATIONALE");
            if (overlay == null) return null;

            var ui = overlay.AddComponent<ConstructionUI>();
            ui.navBar = navBar;
            ui.campaignManager = manager;
            ui.playerFaction = manager.GetPlayerFaction();
            navBar.RegisterPanel(NavigationBar.NavPanel.Construction, overlay);

            ui.BuildContent(overlay.transform);
            return ui;
        }

        private void BuildContent(Transform parent)
        {
            var (scroll, content) = UIFactory.CreateScrollView(parent, "ConstructionScroll");
            UIFactory.AddLayoutElement(scroll.gameObject, flexibleHeight: 1, preferredHeight: 900);
            scroll.horizontal = false;
            scroll.vertical = true;

            // === SUMMARY ===
            int totalWorkshops = playerFaction != null ? playerFaction.civilianFactories : 0;
            List<ConstructionProject> projects = null;
            try { projects = ProductionManager.GetConstructionProjects(playerFaction.factionType); }
            catch { projects = new List<ConstructionProject>(); }
            int usedWorkshops = 0;
            if (projects != null)
                foreach (var p in projects) usedWorkshops += p.assignedWorkshops;

            RectTransform summaryRT = UIFactory.CreatePanel(content, "Summary", new Color(0.10f, 0.07f, 0.05f, 0.95f));
            UIFactory.AddLayoutElement(summaryRT.gameObject, preferredHeight: 45);
            Text summaryText = UIFactory.CreateText(summaryRT, "Text", 
                $"Ateliers civils: {usedWorkshops}/{totalWorkshops} assignés", 
                15, TextAnchor.MiddleCenter, UIFactory.GoldAccent);
            summaryText.fontStyle = FontStyle.Bold;
            RectTransform summaryTextRT = summaryText.GetComponent<RectTransform>();
            summaryTextRT.anchorMin = Vector2.zero;
            summaryTextRT.anchorMax = Vector2.one;
            summaryTextRT.offsetMin = new Vector2(15, 0);
            summaryTextRT.offsetMax = new Vector2(-15, 0);

            // === CONSTRUCTION QUEUE ===
            CreateSectionLabel(content, "File d'attente de construction");

            if (projects == null || projects.Count == 0)
            {
                RectTransform emptyRT = UIFactory.CreatePanel(content, "Empty", new Color(0.12f, 0.13f, 0.11f, 0.9f));
                UIFactory.AddLayoutElement(emptyRT.gameObject, preferredHeight: 40);
                Text emptyText = UIFactory.CreateText(emptyRT, "Text", "Aucun projet en cours.", 
                    12, TextAnchor.MiddleCenter, UIFactory.TextGrey);
                RectTransform emptyTextRT = emptyText.GetComponent<RectTransform>();
                emptyTextRT.anchorMin = Vector2.zero; emptyTextRT.anchorMax = Vector2.one;
                emptyTextRT.offsetMin = Vector2.zero; emptyTextRT.offsetMax = Vector2.zero;
            }
            else
            {
                for (int i = 0; i < projects.Count; i++)
                    CreateProjectRow(content, projects[i], i + 1);
            }

            // === ALL BUILDINGS BY CATEGORY ===
            // --- Économie ---
            CreateSectionLabel(content, "Économie");
            CreateBuildingRow(content, new[] {
                BuildingType.Farm, BuildingType.Mine, BuildingType.Market
            });

            // --- Social ---
            CreateSectionLabel(content, "Social & Éducation");
            CreateBuildingRow(content, new[] {
                BuildingType.Church, BuildingType.University
            });

            // --- Militaire de base ---
            CreateSectionLabel(content, "Militaire");
            CreateBuildingRow(content, new[] {
                BuildingType.Barracks, BuildingType.Stables, BuildingType.Armory, BuildingType.Fortress
            });

            // --- Formation militaire (tiered) ---
            CreateSectionLabel(content, "Formation Militaire (progression)");
            CreateBuildingRow(content, new[] {
                BuildingType.VillageBarracks, BuildingType.ProvincialBarracks,
                BuildingType.MilitaryAcademy, BuildingType.RoyalMilitaryCollege, BuildingType.MilitaryUniversity
            });

            // --- Écoles d'artillerie (tiered) ---
            CreateSectionLabel(content, "Écoles d'Artillerie (progression)");
            CreateBuildingRow(content, new[] {
                BuildingType.SmallArtillerySchool, BuildingType.ProvincialArtillerySchool,
                BuildingType.RoyalArtilleryAcademy, BuildingType.GrandArtilleryAcademy, BuildingType.ImperialArtilleryAcademy
            });
        }

        private void CreateSectionLabel(RectTransform content, string title)
        {
            RectTransform hdrRT = UIFactory.CreatePanel(content, $"Hdr_{title}", new Color(0.16f, 0.18f, 0.15f, 0.95f));
            UIFactory.AddLayoutElement(hdrRT.gameObject, preferredHeight: 28);
            Text hdrText = UIFactory.CreateText(hdrRT, "Text", title, 13, TextAnchor.MiddleLeft, UIFactory.GoldAccent);
            hdrText.fontStyle = FontStyle.Bold;
            RectTransform hdrTextRT = hdrText.GetComponent<RectTransform>();
            hdrTextRT.anchorMin = Vector2.zero; hdrTextRT.anchorMax = Vector2.one;
            hdrTextRT.offsetMin = new Vector2(15, 0); hdrTextRT.offsetMax = new Vector2(-15, 0);
        }

        private void CreateBuildingRow(RectTransform content, BuildingType[] types)
        {
            RectTransform rowRT = UIFactory.CreatePanel(content, "BldRow", new Color(0.12f, 0.13f, 0.11f, 0.90f));
            UIFactory.AddLayoutElement(rowRT.gameObject, preferredHeight: 80);
            HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(rowRT.gameObject, 8f, new RectOffset(10, 10, 4, 4));
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;

            foreach (var bType in types)
            {
                CreateBuildingCard(rowRT, bType);
            }
        }

        private void CreateBuildingCard(RectTransform parent, BuildingType bType)
        {
            string icon = BuildingInfo.GetIcon(bType);
            string bName = BuildingInfo.GetName(bType);
            string desc = BuildingInfo.GetDescription(bType);
            if (desc.Length > 55) desc = desc.Substring(0, 52) + "...";
            int cost = BuildingInfo.GetCostGold(bType, 0);
            int turns = BuildingInfo.GetBuildTime(bType);

            bool unlocked = true;
            if (campaignManager != null)
            {
                try { unlocked = campaignManager.IsBuildingUnlocked(bType); }
                catch { unlocked = true; }
            }

            Color cardBg = unlocked
                ? new Color(0.12f, 0.18f, 0.12f, 0.95f)
                : new Color(0.20f, 0.15f, 0.15f, 0.90f);

            RectTransform cardRT = UIFactory.CreateBorderedPanel(parent, $"Bld_{bType}", cardBg, UIFactory.BorderGold, 1.5f);
            UIFactory.AddLayoutElement(cardRT.gameObject, preferredWidth: 185, preferredHeight: 70);

            Transform inner = cardRT.transform.GetChild(0);

            // Title line: icon + name
            string lockIcon = unlocked ? "" : "  [Verrouillé]";
            Text nameText = UIFactory.CreateText(inner, "Name", $"{icon} {bName}{lockIcon}", 11,
                TextAnchor.MiddleLeft, unlocked ? UIFactory.TextWhite : UIFactory.TextGrey);
            nameText.fontStyle = FontStyle.Bold;
            RectTransform nameRT = nameText.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0, 0.60f); nameRT.anchorMax = new Vector2(1, 1);
            nameRT.offsetMin = new Vector2(6, 0); nameRT.offsetMax = new Vector2(-6, -2);

            // Description
            Text descText = UIFactory.CreateText(inner, "Desc", desc, 9,
                TextAnchor.MiddleLeft, UIFactory.TextGrey);
            RectTransform descRT = descText.GetComponent<RectTransform>();
            descRT.anchorMin = new Vector2(0, 0.30f); descRT.anchorMax = new Vector2(1, 0.62f);
            descRT.offsetMin = new Vector2(6, 0); descRT.offsetMax = new Vector2(-6, 0);

            // Cost + time
            Text costText = UIFactory.CreateText(inner, "Cost", $"{cost}g  |  {turns} tour{(turns > 1 ? "s" : "")}", 10,
                TextAnchor.MiddleLeft, UIFactory.GoldAccent);
            RectTransform costRT = costText.GetComponent<RectTransform>();
            costRT.anchorMin = new Vector2(0, 0); costRT.anchorMax = new Vector2(1, 0.32f);
            costRT.offsetMin = new Vector2(6, 2); costRT.offsetMax = new Vector2(-6, 0);
        }

        private void CreateProjectRow(Transform parent, ConstructionProject project, int index)
        {
            Color rowBg = index % 2 == 0 ? new Color(0.09f, 0.07f, 0.05f, 0.90f) : new Color(0.07f, 0.05f, 0.04f, 0.90f);
            RectTransform rowRT = UIFactory.CreatePanel(parent, $"Project_{index}", rowBg);
            UIFactory.AddLayoutElement(rowRT.gameObject, preferredHeight: 45);

            HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(rowRT.gameObject, 8f, new RectOffset(15, 15, 4, 4));
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            // Index
            Text idxText = UIFactory.CreateText(rowRT, "Idx", $"{index}.", 13, TextAnchor.MiddleCenter, UIFactory.GoldAccent);
            UIFactory.AddLayoutElement(idxText.gameObject, preferredWidth: 25, preferredHeight: 36);

            // Icon
            Text icon = UIFactory.CreateText(rowRT, "Icon", "🏗️", 16, TextAnchor.MiddleCenter, UIFactory.TextWhite);
            UIFactory.AddLayoutElement(icon.gameObject, preferredWidth: 30, preferredHeight: 36);

            // Name + Province
            Text nameText = UIFactory.CreateText(rowRT, "Name", $"{project.buildingId} ({project.provinceId})", 
                13, TextAnchor.MiddleLeft, UIFactory.TextWhite);
            UIFactory.AddLayoutElement(nameText.gameObject, preferredWidth: 250, preferredHeight: 36);

            // Progress bar
            float pct = project.totalRequired > 0 ? project.progress / project.totalRequired : 0;
            var (bg, fill) = UIFactory.CreateProgressBar(rowRT, "Progress", UIFactory.GoldAccent);
            UIFactory.AddLayoutElement(bg.gameObject, preferredWidth: 200, preferredHeight: 20);
            fill.GetComponent<RectTransform>().anchorMax = new Vector2(pct, 1);

            // Percentage text
            Text pctText = UIFactory.CreateText(rowRT, "Pct", $"{(int)(pct * 100)}%", 12, TextAnchor.MiddleCenter, UIFactory.ParchmentBeige);
            UIFactory.AddLayoutElement(pctText.gameObject, preferredWidth: 45, preferredHeight: 36);

            // Workshops assigned
            Text wsText = UIFactory.CreateText(rowRT, "WS", $"[{project.assignedWorkshops}→]", 12, TextAnchor.MiddleCenter, UIFactory.TextGrey);
            UIFactory.AddLayoutElement(wsText.gameObject, preferredWidth: 40, preferredHeight: 36);
        }
    }
}
