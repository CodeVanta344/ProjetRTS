using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace NapoleonicWars.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        private Canvas canvas;
        private Image diffRecruitImg, diffEasyImg, diffNormalImg, diffHardImg, diffLegendaryImg;

        private void Start()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            canvas = UIFactory.CreateCanvas("MainMenuCanvas", 10);
            canvas.transform.SetParent(transform);

            // Background - Solid Prestige Deep Charcoal
            RectTransform bg = UIFactory.CreatePanel(canvas.transform, "Background", UIFactory.DeepCharcoal);
            bg.anchorMin = Vector2.zero;
            bg.anchorMax = Vector2.one;
            bg.offsetMin = Vector2.zero;
            bg.offsetMax = Vector2.zero;

            // Subtle warm overlay for depth
            RectTransform overlay = UIFactory.CreatePanel(bg, "Overlay", new Color(0.05f, 0.04f, 0.03f, 0.3f));
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;

            // Center content container
            RectTransform center = UIFactory.CreatePanel(bg, "CenterPanel", Color.clear);
            center.anchorMin = new Vector2(0.5f, 0.5f);
            center.anchorMax = new Vector2(0.5f, 0.5f);
            center.sizeDelta = new Vector2(400, 600);
            center.anchoredPosition = new Vector2(0, -20);

            UIFactory.AddVerticalLayout(center.gameObject, 12f, new RectOffset(20, 20, 20, 20));

            // ===== TITLE =====
            Text title = UIFactory.CreateText(center, "Title", "NAPOLEONIC WARS", 48, TextAnchor.MiddleCenter, UIFactory.EmpireGold);
            title.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(title.gameObject, preferredHeight: 60);
            
            Outline titleOutline = title.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0.2f, 0.15f, 0.05f, 0.8f);
            titleOutline.effectDistance = new Vector2(2, 2);

            Text subtitle = UIFactory.CreateText(center, "Subtitle", "GRAND STRATEGY & RTS", 14, TextAnchor.MiddleCenter, UIFactory.SilverText);
            subtitle.fontStyle = FontStyle.Normal;
            UIFactory.AddLayoutElement(subtitle.gameObject, preferredHeight: 20);

            UIFactory.CreateSeparator(center);
            UIFactory.AddLayoutElement(center.GetChild(center.childCount-1).gameObject, preferredHeight: 20);

            // ===== MENU BUTTONS =====
            Button btnCampaign = UIFactory.CreateButton(center, "BtnCampaign", "BEGIN CAMPAIGN", 16,
                () => LoadingScreenUI.LoadSceneWithScreen("FactionSelect"));
            UIFactory.AddLayoutElement(btnCampaign.gameObject, preferredHeight: 48);
            btnCampaign.GetComponent<Image>().color = UIFactory.ImperialCrimson;

            Button btnBattle = UIFactory.CreateButton(center, "BtnBattle", "QUICK BATTLE", 16,
                () => LoadingScreenUI.LoadSceneWithScreen("Battle"));
            UIFactory.AddLayoutElement(btnBattle.gameObject, preferredHeight: 48);

            Button btnMulti = UIFactory.CreateButton(center, "BtnMultiplayer", "MULTIPLAYER", 16,
                () => LoadingScreenUI.LoadSceneWithScreen("Lobby"));
            UIFactory.AddLayoutElement(btnMulti.gameObject, preferredHeight: 48);

            // ===== DIFFICULTY =====
            GameObject diffRow = new GameObject("DifficultyRow");
            diffRow.transform.SetParent(center, false);
            UIFactory.AddHorizontalLayout(diffRow, 4f);
            UIFactory.AddLayoutElement(diffRow, preferredHeight: 32);

            string[] diffs = { "Recruit", "Easy", "Normal", "Hard", "Legend" };
            NapoleonicWars.Core.DifficultyLevel[] levels = { 
                NapoleonicWars.Core.DifficultyLevel.Recruit, 
                NapoleonicWars.Core.DifficultyLevel.Easy, 
                NapoleonicWars.Core.DifficultyLevel.Normal, 
                NapoleonicWars.Core.DifficultyLevel.Hard, 
                NapoleonicWars.Core.DifficultyLevel.Legendary 
            };
            
            Button[] diffBtns = new Button[5];
            for(int i=0; i<5; i++)
            {
                int idx = i;
                diffBtns[i] = UIFactory.CreateButton(diffRow.transform, "Diff_" + diffs[i], diffs[i].Substring(0, 1), 11, 
                    () => SetDifficulty(levels[idx]));
                UIFactory.AddLayoutElement(diffBtns[idx].gameObject, flexibleWidth: 1, preferredHeight: 28);
                if (i == 0) diffRecruitImg = diffBtns[i].GetComponent<Image>();
                else if (i == 1) diffEasyImg = diffBtns[i].GetComponent<Image>();
                else if (i == 2) diffNormalImg = diffBtns[i].GetComponent<Image>();
                else if (i == 3) diffHardImg = diffBtns[i].GetComponent<Image>();
                else if (i == 4) diffLegendaryImg = diffBtns[i].GetComponent<Image>();
            }

            // ===== QUIT =====
            UIFactory.CreateSeparator(center);
            UIFactory.AddLayoutElement(center.GetChild(center.childCount-1).gameObject, preferredHeight: 20);

            Button btnQuit = UIFactory.CreateButton(center, "BtnQuit", "EXIT TO DESKTOP", 14, () =>
            {
                Application.Quit();
                #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
                #endif
            });
            UIFactory.AddLayoutElement(btnQuit.gameObject, preferredHeight: 40);
            btnQuit.GetComponentInChildren<Text>().color = UIFactory.SilverText;

            // Footer
            Text credits = UIFactory.CreateText(bg, "Credits", "PRESTIGE EDITION  |  v2.5", 10, TextAnchor.MiddleCenter, UIFactory.SilverText);
            UIFactory.SetAnchors(credits.gameObject, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 10), new Vector2(0, 30));
        }
        private void SetDifficulty(NapoleonicWars.Core.DifficultyLevel level)
        {
            if (NapoleonicWars.Core.DifficultySettings.Instance != null)
                NapoleonicWars.Core.DifficultySettings.Instance.SetDifficulty(level);
            UpdateDifficultyButtons();
        }

        private void UpdateDifficultyButtons()
        {
            var diff = NapoleonicWars.Core.DifficultySettings.Instance;
            NapoleonicWars.Core.DifficultyLevel current = diff != null ? diff.CurrentDifficulty : NapoleonicWars.Core.DifficultyLevel.Normal;

            Color active = UIFactory.ImperialCrimson;
            Color inactive = UIFactory.DeepCharcoal;

            if (diffRecruitImg != null) diffRecruitImg.color = current == NapoleonicWars.Core.DifficultyLevel.Recruit ? active : inactive;
            if (diffEasyImg != null) diffEasyImg.color = current == NapoleonicWars.Core.DifficultyLevel.Easy ? active : inactive;
            if (diffNormalImg != null) diffNormalImg.color = current == NapoleonicWars.Core.DifficultyLevel.Normal ? active : inactive;
            if (diffHardImg != null) diffHardImg.color = current == NapoleonicWars.Core.DifficultyLevel.Hard ? active : inactive;
            if (diffLegendaryImg != null) diffLegendaryImg.color = current == NapoleonicWars.Core.DifficultyLevel.Legendary ? active : inactive;
        }
    }
}
