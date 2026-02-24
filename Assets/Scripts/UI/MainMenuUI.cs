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

            // Full-screen — dark, warm, near-black
            RectTransform bg = UIFactory.CreatePanel(canvas.transform, "Background", new Color(0.04f, 0.02f, 0.02f, 1f));
            bg.anchorMin = Vector2.zero;
            bg.anchorMax = Vector2.one;
            bg.offsetMin = Vector2.zero;
            bg.offsetMax = Vector2.zero;

            // Subtle warm vignette overlay
            RectTransform vignette = UIFactory.CreatePanel(bg, "Vignette", new Color(0.02f, 0.01f, 0.01f, 0.4f));
            vignette.anchorMin = Vector2.zero;
            vignette.anchorMax = Vector2.one;
            vignette.offsetMin = Vector2.zero;
            vignette.offsetMax = Vector2.zero;

            // Center container - responsive sizing (30% width, 60% height, centered)
            RectTransform center = UIFactory.CreatePanel(bg, "CenterPanel", new Color(0, 0, 0, 0));
            center.anchorMin = new Vector2(0.35f, 0.18f);
            center.anchorMax = new Vector2(0.65f, 0.85f);
            center.offsetMin = Vector2.zero;
            center.offsetMax = Vector2.zero;
            center.GetComponent<Image>().raycastTarget = false;

            UIFactory.AddVerticalLayout(center.gameObject, 8f, new RectOffset(0, 0, 0, 0));

            // ===== TITLE — Imperial gold with deep shadow =====
            Text title = UIFactory.CreateText(center, "Title", "NAPOLEONIC WARS", 56, TextAnchor.MiddleCenter, UIFactory.GoldAccent);
            title.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(title.gameObject, 72);
            // Double outline for impactful title
            Outline titleOutline = title.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0.40f, 0.25f, 0.08f, 0.7f);
            titleOutline.effectDistance = new Vector2(3, 3);
            Shadow titleShadow = title.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0, 0, 0, 0.9f);
            titleShadow.effectDistance = new Vector2(4f, -4f);

            // Subtitle — parchment tone, italic
            Text subtitle = UIFactory.CreateText(center, "Subtitle", "Total War Style RTS", 18, TextAnchor.MiddleCenter, UIFactory.ParchmentBeige);
            subtitle.fontStyle = FontStyle.Italic;
            UIFactory.AddLayoutElement(subtitle.gameObject, 30);

            // ===== Ornamental separator =====
            RectTransform ornSep = UIFactory.CreateOrnamentalSeparator(center);
            UIFactory.AddLayoutElement(ornSep.gameObject, 22);

            // Spacer
            GameObject spacer1 = new GameObject("Spacer");
            spacer1.transform.SetParent(center, false);
            spacer1.AddComponent<RectTransform>();
            UIFactory.AddLayoutElement(spacer1, 12);

            // ===== CAMPAIGN — Ornate Warhammer button =====
            Button btnCampaign = UIFactory.CreateWarhammerButton(center, "BtnCampaign", "CAMPAIGN", 22,
                () => LoadingScreenUI.LoadSceneWithScreen("FactionSelect"));
            UIFactory.AddLayoutElement(btnCampaign.gameObject, 58);

            // ===== QUICK BATTLE =====
            Button btnBattle = UIFactory.CreateWarhammerButton(center, "BtnBattle", "QUICK BATTLE", 22,
                () => LoadingScreenUI.LoadSceneWithScreen("Battle"));
            UIFactory.AddLayoutElement(btnBattle.gameObject, 58);

            // ===== MULTIPLAYER =====
            Button btnMulti = UIFactory.CreateWarhammerButton(center, "BtnMultiplayer", "MULTIPLAYER", 22,
                () => LoadingScreenUI.LoadSceneWithScreen("Lobby"));
            UIFactory.AddLayoutElement(btnMulti.gameObject, 58);

            // ===== Difficulty selector =====
            GameObject diffRow = new GameObject("DifficultyRow");
            diffRow.transform.SetParent(center, false);
            diffRow.AddComponent<RectTransform>();
            UIFactory.AddLayoutElement(diffRow, 42);
            UIFactory.AddHorizontalLayout(diffRow, 6f, new RectOffset(30, 30, 4, 4));

            Text diffLabel = UIFactory.CreateText(diffRow.transform, "DiffLabel", "Difficulty:", 14, TextAnchor.MiddleCenter, UIFactory.TextGrey);
            UIFactory.AddLayoutElement(diffLabel.gameObject, 30, 80);

            Button btnRecruit = UIFactory.CreateGoldButton(diffRow.transform, "BtnRecruit", "Recruit", 11, () => SetDifficulty(NapoleonicWars.Core.DifficultyLevel.Recruit));
            UIFactory.AddLayoutElement(btnRecruit.gameObject, 32, 72);
            Button btnEasy = UIFactory.CreateGoldButton(diffRow.transform, "BtnEasy", "Easy", 11, () => SetDifficulty(NapoleonicWars.Core.DifficultyLevel.Easy));
            UIFactory.AddLayoutElement(btnEasy.gameObject, 32, 72);
            Button btnNormal = UIFactory.CreateGoldButton(diffRow.transform, "BtnNormal", "Normal", 11, () => SetDifficulty(NapoleonicWars.Core.DifficultyLevel.Normal));
            UIFactory.AddLayoutElement(btnNormal.gameObject, 32, 72);
            Button btnHard = UIFactory.CreateGoldButton(diffRow.transform, "BtnHard", "Hard", 11, () => SetDifficulty(NapoleonicWars.Core.DifficultyLevel.Hard));
            UIFactory.AddLayoutElement(btnHard.gameObject, 32, 72);
            Button btnLegendary = UIFactory.CreateGoldButton(diffRow.transform, "BtnLegendary", "Legend", 11, () => SetDifficulty(NapoleonicWars.Core.DifficultyLevel.Legendary));
            UIFactory.AddLayoutElement(btnLegendary.gameObject, 32, 72);

            diffRecruitImg = btnRecruit.GetComponent<Image>();
            diffEasyImg = btnEasy.GetComponent<Image>();
            diffNormalImg = btnNormal.GetComponent<Image>();
            diffHardImg = btnHard.GetComponent<Image>();
            diffLegendaryImg = btnLegendary.GetComponent<Image>();
            UpdateDifficultyButtons();

            // Spacer
            GameObject spacer2 = new GameObject("Spacer2");
            spacer2.transform.SetParent(center, false);
            spacer2.AddComponent<RectTransform>();
            UIFactory.AddLayoutElement(spacer2, 8);

            // ===== QUIT — Deep crimson, menacing =====
            Button btnQuit = UIFactory.CreateGoldButton(center, "BtnQuit", "QUIT", 16, () =>
            {
                Application.Quit();
                #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
                #endif
            });
            UIFactory.AddLayoutElement(btnQuit.gameObject, 44);
            btnQuit.GetComponent<Image>().color = UIFactory.CrimsonDeep;
            btnQuit.GetComponentInChildren<Text>().color = new Color(0.85f, 0.35f, 0.30f);

            // Credits — warm grey
            Text credits = UIFactory.CreateText(bg, "Credits", "Made with Unity 6  |  Napoleonic Wars RTS", 12, TextAnchor.MiddleCenter, UIFactory.TextGrey);
            RectTransform credRT = credits.GetComponent<RectTransform>();
            credRT.anchorMin = new Vector2(0, 0);
            credRT.anchorMax = new Vector2(1, 0);
            credRT.offsetMin = new Vector2(0, 8);
            credRT.offsetMax = new Vector2(0, 32);

            // Version
            Text version = UIFactory.CreateText(bg, "Version", "v2.0", 10, TextAnchor.MiddleRight, UIFactory.TextGrey);
            RectTransform verRT = version.GetComponent<RectTransform>();
            verRT.anchorMin = new Vector2(1, 0);
            verRT.anchorMax = new Vector2(1, 0);
            verRT.pivot = new Vector2(1, 0);
            verRT.anchoredPosition = new Vector2(-15, 10);
            verRT.sizeDelta = new Vector2(60, 20);
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

            Color active = UIFactory.ButtonActive;
            Color inactive = UIFactory.ButtonNormal;

            if (diffRecruitImg != null) diffRecruitImg.color = current == NapoleonicWars.Core.DifficultyLevel.Recruit ? active : inactive;
            if (diffEasyImg != null) diffEasyImg.color = current == NapoleonicWars.Core.DifficultyLevel.Easy ? active : inactive;
            if (diffNormalImg != null) diffNormalImg.color = current == NapoleonicWars.Core.DifficultyLevel.Normal ? active : inactive;
            if (diffHardImg != null) diffHardImg.color = current == NapoleonicWars.Core.DifficultyLevel.Hard ? active : inactive;
            if (diffLegendaryImg != null) diffLegendaryImg.color = current == NapoleonicWars.Core.DifficultyLevel.Legendary ? active : inactive;
        }
    }
}
