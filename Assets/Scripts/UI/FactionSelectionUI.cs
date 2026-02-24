using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Collections;
using NapoleonicWars.Data;

namespace NapoleonicWars.UI
{
    public class FactionSelectionUI : MonoBehaviour
    {
        private Canvas canvas;
        private FactionType selectedFaction = FactionType.France;

        // References for dynamic updates
        private GameObject[] factionCards = new GameObject[24];
        private Image[] cardFlags = new Image[24];
        private Image[] cardGlows = new Image[24];
        private GameObject[] cardCheckmarks = new GameObject[24];
        private Sprite[] flagSprites = new Sprite[24];
        private Text[] cardNames = new Text[24];

        // Detail panel references
        private Image detailFlagImage;
        private Text detailFactionName;
        private Text detailMotto;
        private Text detailDescription;
        private Text detailStats;
        private Image detailAccentBar;
        private Image detailAccentBarBot;
        private Image[] difficultyPips = new Image[5];
        private Text difficultyLabel;

        // Stat bar references
        private Image[] statBarFills = new Image[6];
        private Text[] statBarLabels = new Text[6];

        // Buttons
        private Button startCampaignBtn;
        private Text startBtnLabel;

        // Audio
        private AudioSource hoverSound;
        private AudioSource selectSound;

        // Animation state
        private int previousSelection = -1;
        private Coroutine typewriterCoroutine;

        // ─── FACTION DATA ───────────────────────────────────────────────

        private static readonly string[] FactionNames = {
            "FRENCH EMPIRE", "UNITED KINGDOM", "KINGDOM OF PRUSSIA",
            "RUSSIAN EMPIRE", "AUSTRIAN EMPIRE", "KINGDOM OF SPAIN", "OTTOMAN EMPIRE",
            "KINGDOM OF PORTUGAL", "SWEDISH EMPIRE", "DENMARK-NORWAY", "POLISH-LITHUANIAN COMMONWEALTH",
            "REPUBLIC OF VENICE", "DUTCH REPUBLIC", "ELECTORATE OF BAVARIA", "ELECTORATE OF SAXONY",
            "PAPAL STATES", "DUCHY OF SAVOY", "SWISS CONFEDERATION", "REPUBLIC OF GENOA",
            "GRAND DUCHY OF TUSCANY", "ELECTORATE OF HANOVER", "DUCHY OF MODENA", "DUCHY OF PARMA", "DUCHY OF LORRAINE"
        };

        private static readonly string[] FactionShortNames = {
            "FRANCE", "BRITAIN", "PRUSSIA", "RUSSIA", "AUSTRIA", "SPAIN", "OTTOMAN",
            "PORTUGAL", "SWEDEN", "DENMARK", "POLAND",
            "VENICE", "DUTCH", "BAVARIA", "SAXONY",
            "PAPAL", "SAVOY", "SWISS", "GENOA",
            "TUSCANY", "HANOVER", "MODENA", "PARMA", "LORRAINE"
        };

        private static readonly string[] FactionMottos = {
            "\"Liberté, Égalité, Fraternité\"",
            "\"Rule Britannia!\"",
            "\"Gott mit uns\"",
            "\"С нами Бог\"",
            "\"Alles Erdreich ist Österreich untertan\"",
            "\"Plus Ultra\"",
            "\"Devlet-i Alîye-i Osmânîye\"",
            "\"In Hoc Signo Vinces\"",
            "\"För Sverige i tiden\"",
            "\"Guds hjælp, Folkets kærlighed, Danmarks styrke\"",
            "\"Si Deus nobiscum quis contra nos\"",
            "\"Pax tibi Marce, evangelista meus\"",
            "\"Je Maintiendrai\"",
            "\"In Treue fest\"",
            "\"Providentiae Memor\"",
            "\"Pax Christi\"",
            "\"Fert\"",
            "\"Unus pro omnibus, omnes pro uno\"",
            "\"Libertas\"",
            "\"Pulsus Resurgo\"",
            "\"Suscipere et Finiere\"",
            "\"Avia Pervia\"",
            "\"Dirige Domine gressus meos\"",
            "\"Fidelis et Fortis\""
        };

        private static readonly string[] FactionDescriptions = {
            "Led by Napoleon Bonaparte, France commands the most powerful army in Europe. Starting with strong positions in Western Europe and the legendary Grande Armée, you must conquer and hold the continent against a coalition of enemies.",
            "Master of the seas with an unrivaled navy and wealth from global colonies. Project power from your island fortress, blockade enemies, and support continental allies to maintain the balance of power.",
            "Elite military tradition with the finest disciplined troops in Europe. A smaller territory demands tactical brilliance on the battlefield. Quality over quantity — every Prussian soldier is worth three of the enemy.",
            "Vast endless territory and massive manpower reserves make Russia an unstoppable juggernaut. Harsh winters defend your lands naturally. Slow to mobilize but once the bear awakens, nothing can stop it.",
            "Positioned at the heart of Europe, surrounded by both allies and enemies. Strong defensive fortifications and centuries of imperial diplomacy. Navigate complex politics to preserve the Habsburg legacy.",
            "Colonial wealth from the Americas still flows, but military power wanes. Allied with France against Britain, you must fight to restore Spain's former glory and defend the homeland from invaders.",
            "Once the terror of Europe, the Ottoman Empire now seeks to modernize. A massive territory spanning three continents offers unique strategic depth. Reform your military before it's too late.",
            "A proud maritime nation allied closely with Britain. Defend the homeland against Spanish and French ambitions while maintaining a global empire.",
            "The dominant power of the North, controlling the Baltic Sea. You must defend your empire against the rising power of Russia and its allies.",
            "Controlling the straits of the Baltic, Denmark-Norway seeks to balance great power politics while maintaining its maritime traditions.",
            "A vast commonwealth facing encirclement by powerful neighbors. You must reform the state and modernize the army to survive.",
            "A legendary maritime republic past its prime. Use diplomacy and naval power to protect your trade routes from the Ottomans.",
            "A wealthy trading republic with a powerful navy and strong fortifications. Allied with Britain against French expansionism.",
            "A major German electorate allied with France. Expand your influence within the Holy Roman Empire.",
            "A prosperous German state caught between Prussia, Austria, and Russia. Diplomacy is key to survival.",
            "The spiritual center of Catholicism. Use your moral authority and wealth to navigate Italian politics.",
            "Guardian of the Alpine passes. A strategic pivot point between France, Austria, and the Italian states.",
            "Fiercely independent cantons protected by formidable mountains and legendary mercenaries.",
            "A wealthy merchant republic heavily reliant on diplomacy and its banking connections.",
            "A prosperous and enlightened state in central Italy, historically tied to the Habsburgs.",
            "A German electorate in personal union with Great Britain, making it a target for French aggression.",
            "A small but proud Italian duchy striving to maintain independence amidst great power conflicts.",
            "A minor Italian state often used as a bargaining chip by the Bourbon and Habsburg dynasties.",
            "A strategically vital duchy on the border of France and the Holy Roman Empire."
        };

        private static readonly string[] FactionCapitals = {
            "Paris", "London", "Berlin", "Moscow", "Vienna", "Madrid", "Constantinople",
            "Lisbon", "Stockholm", "Copenhagen", "Warsaw", "Venice", "Amsterdam", "Munich", "Dresden",
            "Rome", "Turin", "Bern", "Genoa", "Florence", "Hanover", "Modena", "Parma", "Nancy"
        };

        // Stats: Army, Navy, Economy, Population, Forts, Diplomacy (0-10 scale)
        private static readonly int[,] FactionStatValues = {
            { 10, 5, 8, 8, 7, 4 }, // France
            { 7, 10, 10, 5, 3, 7 }, // Britain
            { 9, 2, 5, 3, 4, 4 },  // Prussia
            { 7, 5, 3, 10, 5, 3 }, // Russia
            { 6, 1, 6, 6, 8, 7 },  // Austria
            { 4, 5, 6, 4, 4, 3 },  // Spain
            { 5, 4, 4, 7, 3, 2 },  // Ottoman
            { 4, 6, 5, 3, 4, 6 },  // Portugal
            { 7, 5, 4, 2, 5, 4 },  // Sweden
            { 5, 6, 5, 3, 4, 5 },  // Denmark
            { 6, 1, 3, 6, 3, 2 },  // Poland
            { 4, 7, 6, 2, 6, 8 },  // Venice
            { 5, 8, 9, 3, 7, 7 },  // Dutch
            { 6, 0, 5, 3, 4, 5 },  // Bavaria
            { 5, 0, 6, 3, 4, 5 },  // Saxony
            { 3, 1, 4, 2, 5, 9 },  // Papal States
            { 6, 2, 4, 2, 7, 6 },  // Savoy
            { 8, 0, 4, 2, 8, 7 },  // Switzerland
            { 3, 6, 7, 2, 5, 8 },  // Genoa
            { 4, 2, 6, 3, 4, 7 },  // Tuscany
            { 5, 1, 5, 2, 4, 6 },  // Hanover
            { 3, 0, 4, 1, 4, 5 },  // Modena
            { 3, 0, 4, 1, 4, 5 },  // Parma
            { 4, 0, 5, 2, 5, 4 }   // Lorraine
        };

        private static readonly string[] StatNames = {
            "Army", "Navy", "Economy", "Population", "Forts", "Diplomacy"
        };

        private static readonly Color[] StatColors = {
            new Color(0.85f, 0.30f, 0.20f), // Army - red
            new Color(0.30f, 0.55f, 0.85f), // Navy - blue
            new Color(0.85f, 0.75f, 0.25f), // Economy - gold
            new Color(0.40f, 0.80f, 0.40f), // Population - green
            new Color(0.65f, 0.50f, 0.35f), // Forts - brown
            new Color(0.70f, 0.50f, 0.80f), // Diplomacy - purple
        };

        // Difficulty: 1-5 pips
        private static readonly int[] FactionDifficulty = { 
            2, 3, 4, 3, 4, 4, 5, // France, Britain, Prussia, Russia, Austria, Spain, Ottoman
            4, 3, 4, 5,          // Portugal, Sweden, Denmark, Poland
            4, 3, 4, 4,          // Venice, Dutch, Bavaria, Saxony
            5, 4, 3, 4,          // Papal States, Savoy, Switzerland, Genoa
            4, 4, 5, 5, 5        // Tuscany, Hanover, Modena, Parma, Lorraine
        };

        private static readonly Color[] FactionColors = {
            new Color(0.15f, 0.30f, 0.75f),    // France — Deep Blue
            new Color(0.75f, 0.15f, 0.15f),    // Britain — Red
            new Color(0.20f, 0.20f, 0.22f),    // Prussia — Dark Grey
            new Color(0.15f, 0.55f, 0.20f),    // Russia — Forest Green
            new Color(0.92f, 0.82f, 0.35f),    // Austria — Warm Yellow
            new Color(0.90f, 0.55f, 0.15f),    // Spain — Bright Orange
            new Color(0.55f, 0.15f, 0.20f),    // Ottoman — Dark Maroon
            new Color(0.20f, 0.80f, 0.20f),    // Portugal
            new Color(0.20f, 0.40f, 0.80f),    // Sweden
            new Color(0.80f, 0.20f, 0.20f),    // Denmark
            new Color(0.80f, 0.20f, 0.40f),    // Poland
            new Color(0.80f, 0.20f, 0.20f),    // Venice
            new Color(0.90f, 0.50f, 0.10f),    // Dutch
            new Color(0.20f, 0.60f, 0.90f),    // Bavaria
            new Color(0.20f, 0.80f, 0.40f),    // Saxony
            new Color(0.90f, 0.80f, 0.20f),    // Papal States
            new Color(0.80f, 0.20f, 0.20f),    // Savoy
            new Color(0.80f, 0.20f, 0.20f),    // Switzerland
            new Color(0.80f, 0.20f, 0.20f),    // Genoa
            new Color(0.80f, 0.20f, 0.20f),    // Tuscany
            new Color(0.80f, 0.20f, 0.20f),    // Hanover
            new Color(0.80f, 0.20f, 0.20f),    // Modena
            new Color(0.80f, 0.20f, 0.20f),    // Parma
            new Color(0.80f, 0.20f, 0.20f)     // Lorraine
        };

        private static readonly string[] FlagResourcePaths = {
            "UI/Flags/flag_france",
            "UI/Flags/flag_britain",
            "UI/Flags/flag_prussia",
            "UI/Flags/flag_russia",
            "UI/Flags/flag_austria",
            "UI/Flags/flag_spain",
            "UI/Flags/flag_ottoman",
            "UI/Flags/flag_portugal",
            "UI/Flags/flag_sweden",
            "UI/Flags/flag_denmark",
            "UI/Flags/flag_poland",
            "UI/Flags/flag_venice",
            "UI/Flags/flag_dutch",
            "UI/Flags/flag_bavaria",
            "UI/Flags/flag_saxony",
            "UI/Flags/flag_papal",
            "UI/Flags/flag_savoy",
            "UI/Flags/flag_switzerland",
            "UI/Flags/flag_genoa",
            "UI/Flags/flag_tuscany",
            "UI/Flags/flag_hanover",
            "UI/Flags/flag_modena",
            "UI/Flags/flag_parma",
            "UI/Flags/flag_lorraine"
        };

        private static readonly string[] ModelResourcePaths = {
            "Models/Soldiers/soldier_france_line",
            "Models/Soldiers/soldier_britain_line",
            "Models/Soldiers/soldier_prussia_line",
            "Models/Soldiers/soldier_russia_line",
            "Models/Soldiers/soldier_austria_line",
            "Models/Soldiers/soldier_spain_line",
            "Models/Soldiers/soldier_ottoman_line",
            "Models/Soldiers/soldier_portugal_line",
            "Models/Soldiers/soldier_sweden_line",
            "Models/Soldiers/soldier_denmark_line",
            "Models/Soldiers/soldier_poland_line",
            "Models/Soldiers/soldier_venice_line",
            "Models/Soldiers/soldier_dutch_line",
            "Models/Soldiers/soldier_bavaria_line",
            "Models/Soldiers/soldier_saxony_line",
            "Models/Soldiers/soldier_papal_line",
            "Models/Soldiers/soldier_savoy_line",
            "Models/Soldiers/soldier_switzerland_line",
            "Models/Soldiers/soldier_genoa_line",
            "Models/Soldiers/soldier_tuscany_line",
            "Models/Soldiers/soldier_hanover_line",
            "Models/Soldiers/soldier_modena_line",
            "Models/Soldiers/soldier_parma_line",
            "Models/Soldiers/soldier_lorraine_line"
        };

        private void SetupAudio()
        {
            hoverSound = gameObject.AddComponent<AudioSource>();
            hoverSound.volume = 0.3f;
            selectSound = gameObject.AddComponent<AudioSource>();
            selectSound.volume = 0.5f;
        }

        private void Start()
        {
            SetupAudio();
            LoadFlagSprites();
            BuildUI();
            SelectFaction(FactionType.France);
            StartCoroutine(AnimateSelectedCard());
            StartCoroutine(PulseStartButton());
        }

        private void LoadFlagSprites()
        {
            for (int i = 0; i < 24; i++)
            {
                Texture2D tex = Resources.Load<Texture2D>(FlagResourcePaths[i]);
                if (tex != null)
                {
                    flagSprites[i] = Sprite.Create(tex,
                        new Rect(0, 0, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f));
                }
            }
        }

        // ─── BUILD UI ───────────────────────────────────────────────────

        private void BuildUI()
        {
            canvas = UIFactory.CreateCanvas("FactionSelectCanvas", 10);

            // === FULL-SCREEN DARK BACKGROUND ===
            RectTransform bg = UIFactory.CreatePanel(canvas.transform, "Background", new Color(0.025f, 0.018f, 0.015f, 1f));
            bg.anchorMin = Vector2.zero;
            bg.anchorMax = Vector2.one;
            bg.offsetMin = Vector2.zero;
            bg.offsetMax = Vector2.zero;

            // Subtle warm gradient overlay at top
            RectTransform topGrad = UIFactory.CreatePanel(bg, "TopGrad", new Color(0.08f, 0.04f, 0.02f, 0.4f));
            topGrad.anchorMin = new Vector2(0, 0.7f);
            topGrad.anchorMax = Vector2.one;
            topGrad.offsetMin = Vector2.zero;
            topGrad.offsetMax = Vector2.zero;
            topGrad.GetComponent<Image>().raycastTarget = false;

            // === TOP BANNER ===
            BuildTitleBanner(bg);

            // === FACTION CARDS ROW ===
            BuildFactionCardsRow(bg);

            // === DETAIL PANEL ===
            BuildDetailPanel(bg);

            // === BOTTOM ACTION BAR ===
            BuildActionBar(bg);
        }

        // ─── TITLE BANNER ───────────────────────────────────────────────

        private void BuildTitleBanner(RectTransform parent)
        {
            // Banner background
            RectTransform banner = UIFactory.CreatePanel(parent, "TitleBanner", new Color(0.05f, 0.025f, 0.02f, 0.98f));
            banner.anchorMin = new Vector2(0, 0.925f);
            banner.anchorMax = new Vector2(1, 1f);
            banner.offsetMin = Vector2.zero;
            banner.offsetMax = Vector2.zero;

            // Gold borders (top + bottom)
            RectTransform topLine = UIFactory.CreatePanel(banner, "TopLine", UIFactory.BorderGoldBright);
            topLine.anchorMin = new Vector2(0, 0.94f); topLine.anchorMax = Vector2.one;
            topLine.offsetMin = Vector2.zero; topLine.offsetMax = Vector2.zero;
            topLine.GetComponent<Image>().raycastTarget = false;

            RectTransform botLine = UIFactory.CreatePanel(banner, "BotLine", UIFactory.BorderGold);
            botLine.anchorMin = Vector2.zero; botLine.anchorMax = new Vector2(1, 0.04f);
            botLine.offsetMin = Vector2.zero; botLine.offsetMax = Vector2.zero;
            botLine.GetComponent<Image>().raycastTarget = false;

            // Decorative wings
            Text leftWing = UIFactory.CreateText(banner, "LeftWing", "════════════ ✦ ", 14, TextAnchor.MiddleRight, UIFactory.BorderGold);
            UIFactory.SetAnchors(leftWing.gameObject, new Vector2(0.03f, 0.1f), new Vector2(0.28f, 0.9f), Vector2.zero, Vector2.zero);

            // Title
            Text title = UIFactory.CreateText(banner, "Title", "CHOOSE YOUR NATION", 42, TextAnchor.MiddleCenter, UIFactory.GoldAccent);
            title.fontStyle = FontStyle.Bold;
            UIFactory.SetAnchors(title.gameObject, new Vector2(0.25f, 0.05f), new Vector2(0.75f, 0.95f), Vector2.zero, Vector2.zero);
            Shadow titleShadow = title.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0, 0, 0, 0.9f);
            titleShadow.effectDistance = new Vector2(3, -3);
            Outline titleOutline = title.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0, 0, 0, 0.5f);
            titleOutline.effectDistance = new Vector2(1, 1);

            Text rightWing = UIFactory.CreateText(banner, "RightWing", " ✦ ════════════", 14, TextAnchor.MiddleLeft, UIFactory.BorderGold);
            UIFactory.SetAnchors(rightWing.gameObject, new Vector2(0.72f, 0.1f), new Vector2(0.97f, 0.9f), Vector2.zero, Vector2.zero);
        }

        // ─── FACTION CARDS ──────────────────────────────────────────────

        private void BuildFactionCardsRow(RectTransform parent)
        {
            // Container for the scroll view
            GameObject scrollViewGO = new GameObject("CardsScrollView");
            scrollViewGO.transform.SetParent(parent, false);
            RectTransform scrollRT = scrollViewGO.AddComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0.015f, 0.58f);
            scrollRT.anchorMax = new Vector2(0.985f, 0.915f);
            scrollRT.offsetMin = Vector2.zero;
            scrollRT.offsetMax = Vector2.zero;

            ScrollRect scrollRect = scrollViewGO.AddComponent<ScrollRect>();
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.inertia = true;
            scrollRect.scrollSensitivity = 50f;

            // Viewport
            GameObject viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(scrollViewGO.transform, false);
            RectTransform viewportRT = viewportGO.AddComponent<RectTransform>();
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.offsetMin = Vector2.zero;
            viewportRT.offsetMax = Vector2.zero;
            viewportGO.AddComponent<RectMask2D>();
            scrollRect.viewport = viewportRT;

            // Content container
            GameObject container = new GameObject("CardsContainer");
            container.transform.SetParent(viewportRT, false);
            RectTransform cRT = container.AddComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0, 0);
            cRT.anchorMax = new Vector2(0, 1);
            cRT.pivot = new Vector2(0, 0.5f);
            cRT.offsetMin = Vector2.zero;
            cRT.offsetMax = Vector2.zero;

            HorizontalLayoutGroup hlg = container.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.spacing = 15f;
            hlg.padding = new RectOffset(15, 15, 15, 15);

            ContentSizeFitter csf = container.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            scrollRect.content = cRT;

            for (int i = 0; i < 24; i++)
            {
                CreateFactionCard(container.transform, (FactionType)i, i);
                LayoutElement le = factionCards[i].AddComponent<LayoutElement>();
                le.preferredWidth = 160f;
            }
        }

        private void CreateFactionCard(Transform parent, FactionType faction, int index)
        {
            // === CARD ROOT ===
            GameObject cardGO = new GameObject($"Card_{faction}");
            cardGO.transform.SetParent(parent, false);
            RectTransform cardRT = cardGO.AddComponent<RectTransform>();

            Image cardBg = cardGO.AddComponent<Image>();
            cardBg.color = new Color(0.06f, 0.04f, 0.035f, 0.97f);

            Outline cardOutline = cardGO.AddComponent<Outline>();
            cardOutline.effectColor = new Color(UIFactory.BorderGold.r, UIFactory.BorderGold.g, UIFactory.BorderGold.b, 0.25f);
            cardOutline.effectDistance = new Vector2(1.5f, 1.5f);

            factionCards[index] = cardGO;

            // === FACTION COLOR ACCENT BAR (top, thick) ===
            RectTransform accentTop = UIFactory.CreatePanel(cardRT, "AccentTop", FactionColors[index]);
            accentTop.anchorMin = new Vector2(0, 0.94f); accentTop.anchorMax = Vector2.one;
            accentTop.offsetMin = Vector2.zero; accentTop.offsetMax = Vector2.zero;
            accentTop.GetComponent<Image>().raycastTarget = false;

            // === SELECTION GLOW (initially invisible) ===
            GameObject glowGO = new GameObject("Glow");
            glowGO.transform.SetParent(cardRT, false);
            RectTransform glowRT = glowGO.AddComponent<RectTransform>();
            glowRT.anchorMin = Vector2.zero;
            glowRT.anchorMax = Vector2.one;
            glowRT.offsetMin = new Vector2(-4, -4);
            glowRT.offsetMax = new Vector2(4, 4);
            Image glowImg = glowGO.AddComponent<Image>();
            Color gc = FactionColors[index];
            glowImg.color = new Color(gc.r, gc.g, gc.b, 0f);
            glowImg.raycastTarget = false;
            cardGlows[index] = glowImg;

            // === INNER FLAG FRAME ===
            RectTransform flagFrame = UIFactory.CreatePanel(cardRT, "FlagFrame", new Color(UIFactory.BorderGold.r, UIFactory.BorderGold.g, UIFactory.BorderGold.b, 0.5f));
            flagFrame.anchorMin = new Vector2(0.06f, 0.28f);
            flagFrame.anchorMax = new Vector2(0.94f, 0.92f);
            flagFrame.offsetMin = Vector2.zero;
            flagFrame.offsetMax = Vector2.zero;

            // Flag inner bg
            RectTransform flagInner = UIFactory.CreatePanel(flagFrame, "FlagInner", new Color(0.04f, 0.03f, 0.025f, 1f));
            flagInner.anchorMin = Vector2.zero; flagInner.anchorMax = Vector2.one;
            flagInner.offsetMin = new Vector2(2, 2); flagInner.offsetMax = new Vector2(-2, -2);

            // Flag image
            GameObject flagGO = new GameObject("FlagImg");
            flagGO.transform.SetParent(flagInner, false);
            RectTransform flagImgRT = flagGO.AddComponent<RectTransform>();
            flagImgRT.anchorMin = new Vector2(0.03f, 0.03f);
            flagImgRT.anchorMax = new Vector2(0.97f, 0.97f);
            flagImgRT.offsetMin = Vector2.zero;
            flagImgRT.offsetMax = Vector2.zero;

            Image flagImg = flagGO.AddComponent<Image>();
            flagImg.preserveAspect = true;
            flagImg.raycastTarget = false;
            if (flagSprites[index] != null)
                flagImg.sprite = flagSprites[index];
            else
                flagImg.color = FactionColors[index] * 0.5f;
            cardFlags[index] = flagImg;

            // === FACTION NAME ===
            Text nameText = UIFactory.CreateText(cardRT, "Name", FactionShortNames[index],
                14, TextAnchor.MiddleCenter, UIFactory.GoldAccent);
            nameText.fontStyle = FontStyle.Bold;
            nameText.resizeTextForBestFit = true;
            nameText.resizeTextMinSize = 10;
            nameText.resizeTextMaxSize = 16;
            UIFactory.SetAnchors(nameText.gameObject, new Vector2(0.02f, 0.13f), new Vector2(0.98f, 0.27f), Vector2.zero, Vector2.zero);
            Shadow ns = nameText.gameObject.AddComponent<Shadow>();
            ns.effectColor = new Color(0, 0, 0, 0.8f);
            ns.effectDistance = new Vector2(1, -1);
            cardNames[index] = nameText;

            // === DIFFICULTY DOTS ===
            int diff = FactionDifficulty[index];
            string dots = "";
            for (int d = 0; d < 5; d++)
                dots += d < diff ? "★" : "☆";

            Text diffText = UIFactory.CreateText(cardRT, "Diff", dots, 7, TextAnchor.MiddleCenter, GetDifficultyColor(diff));
            UIFactory.SetAnchors(diffText.gameObject, new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.13f), Vector2.zero, Vector2.zero);

            // === CHECKMARK ===
            GameObject checkGO = new GameObject("Checkmark");
            checkGO.transform.SetParent(cardRT, false);
            Text checkTxt = checkGO.AddComponent<Text>();
            checkTxt.text = "✓";
            checkTxt.fontSize = 20;
            checkTxt.color = new Color(0.3f, 1f, 0.3f);
            checkTxt.alignment = TextAnchor.MiddleCenter;
            checkTxt.fontStyle = FontStyle.Bold;
            checkTxt.font = Font.CreateDynamicFontFromOSFont("Arial", 22);
            UIFactory.SetAnchors(checkGO, new Vector2(0.75f, 0.88f), new Vector2(1f, 1.05f), Vector2.zero, Vector2.zero);
            checkGO.SetActive(false);
            cardCheckmarks[index] = checkGO;

            // === BUTTON + HOVER ===
            Button btn = cardGO.AddComponent<Button>();
            btn.targetGraphic = cardBg;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.6f, 1.5f, 1.3f);
            cb.pressedColor = new Color(0.7f, 0.6f, 0.5f);
            cb.fadeDuration = 0.1f;
            btn.colors = cb;

            int capturedIndex = index;
            btn.onClick.AddListener(() => { PlaySelectSound(); SelectFaction((FactionType)capturedIndex); });

            // Hover events
            EventTrigger trigger = cardGO.AddComponent<EventTrigger>();

            EventTrigger.Entry enterEntry = new EventTrigger.Entry();
            enterEntry.eventID = EventTriggerType.PointerEnter;
            enterEntry.callback.AddListener((data) =>
            {
                PlayHoverSound();
                if ((int)selectedFaction != capturedIndex)
                {
                    cardOutline.effectColor = new Color(1f, 0.85f, 0.4f, 0.7f);
                    cardBg.color = new Color(0.09f, 0.06f, 0.05f, 0.98f);
                }
            });
            trigger.triggers.Add(enterEntry);

            EventTrigger.Entry exitEntry = new EventTrigger.Entry();
            exitEntry.eventID = EventTriggerType.PointerExit;
            exitEntry.callback.AddListener((data) =>
            {
                if ((int)selectedFaction != capturedIndex)
                {
                    cardOutline.effectColor = new Color(UIFactory.BorderGold.r, UIFactory.BorderGold.g, UIFactory.BorderGold.b, 0.25f);
                    cardBg.color = new Color(0.06f, 0.04f, 0.035f, 0.97f);
                }
            });
            trigger.triggers.Add(exitEntry);
        }

        // ─── DETAIL PANEL ───────────────────────────────────────────────

        private void BuildDetailPanel(RectTransform parent)
        {
            RectTransform outerPanel = UIFactory.CreateOrnatePanel(parent, "DetailPanel",
                new Color(0.05f, 0.035f, 0.025f, 0.97f));
            outerPanel.anchorMin = new Vector2(0.015f, 0.10f);
            outerPanel.anchorMax = new Vector2(0.985f, 0.565f);
            outerPanel.offsetMin = Vector2.zero;
            outerPanel.offsetMax = Vector2.zero;

            Transform inner = outerPanel.Find("Inner");

            // ── LEFT: FLAG ──
            GameObject leftCol = new GameObject("LeftCol");
            leftCol.transform.SetParent(inner, false);
            UIFactory.SetAnchors(leftCol, new Vector2(0.01f, 0.03f), new Vector2(0.24f, 0.97f), Vector2.zero, Vector2.zero);

            // Flag with ornate gold border
            RectTransform flagBorder = UIFactory.CreatePanel(leftCol.transform, "FlagBorder", UIFactory.BorderGold);
            flagBorder.anchorMin = new Vector2(0.04f, 0.06f);
            flagBorder.anchorMax = new Vector2(0.96f, 0.94f);
            flagBorder.offsetMin = Vector2.zero;
            flagBorder.offsetMax = Vector2.zero;

            // Inner dark frame
            RectTransform flagDark = UIFactory.CreatePanel(flagBorder, "FlagDark", new Color(0.03f, 0.02f, 0.015f, 1f));
            flagDark.anchorMin = Vector2.zero; flagDark.anchorMax = Vector2.one;
            flagDark.offsetMin = new Vector2(3, 3); flagDark.offsetMax = new Vector2(-3, -3);

            // Flag image
            GameObject flagGO = new GameObject("FlagImage");
            flagGO.transform.SetParent(flagDark, false);
            UIFactory.SetAnchors(flagGO, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f), Vector2.zero, Vector2.zero);
            detailFlagImage = flagGO.AddComponent<Image>();
            detailFlagImage.preserveAspect = true;

            // ── CENTER: TEXT INFO ──
            GameObject centerCol = new GameObject("CenterCol");
            centerCol.transform.SetParent(inner, false);
            UIFactory.SetAnchors(centerCol, new Vector2(0.26f, 0.03f), new Vector2(0.64f, 0.97f), Vector2.zero, Vector2.zero);

            // Faction accent bar (top)
            detailAccentBar = UIFactory.CreatePanel(centerCol.transform, "AccentTop", FactionColors[0]).GetComponent<Image>();
            UIFactory.SetAnchors(detailAccentBar.gameObject, new Vector2(0, 0.94f), new Vector2(1, 1f), Vector2.zero, Vector2.zero);
            detailAccentBar.raycastTarget = false;

            // Faction name
            detailFactionName = UIFactory.CreateText(centerCol.transform, "FactionName",
                FactionNames[0], 36, TextAnchor.MiddleLeft, UIFactory.GoldAccent);
            detailFactionName.fontStyle = FontStyle.Bold;
            UIFactory.SetAnchors(detailFactionName.gameObject, new Vector2(0.02f, 0.82f), new Vector2(0.98f, 0.94f), Vector2.zero, Vector2.zero);
            Shadow fnSh = detailFactionName.gameObject.AddComponent<Shadow>();
            fnSh.effectColor = new Color(0, 0, 0, 0.8f);
            fnSh.effectDistance = new Vector2(2, -2);

            // Motto
            detailMotto = UIFactory.CreateText(centerCol.transform, "Motto",
                FactionMottos[0], 18, TextAnchor.MiddleLeft, new Color(0.72f, 0.62f, 0.42f));
            detailMotto.fontStyle = FontStyle.Italic;
            UIFactory.SetAnchors(detailMotto.gameObject, new Vector2(0.02f, 0.73f), new Vector2(0.98f, 0.82f), Vector2.zero, Vector2.zero);

            // Gold separator
            RectTransform sep = UIFactory.CreatePanel(centerCol.transform, "Sep", UIFactory.BorderGold);
            sep.anchorMin = new Vector2(0.02f, 0.715f);
            sep.anchorMax = new Vector2(0.5f, 0.72f);
            sep.offsetMin = Vector2.zero; sep.offsetMax = Vector2.zero;
            sep.GetComponent<Image>().raycastTarget = false;

            // Description
            detailDescription = UIFactory.CreateText(centerCol.transform, "Description",
                FactionDescriptions[0], 18, TextAnchor.UpperLeft, UIFactory.ParchmentBeige);
            UIFactory.SetAnchors(detailDescription.gameObject, new Vector2(0.02f, 0.08f), new Vector2(0.98f, 0.71f), Vector2.zero, Vector2.zero);

            // Accent bar bottom
            detailAccentBarBot = UIFactory.CreatePanel(centerCol.transform, "AccentBot", FactionColors[0]).GetComponent<Image>();
            UIFactory.SetAnchors(detailAccentBarBot.gameObject, new Vector2(0, 0), new Vector2(1, 0.03f), Vector2.zero, Vector2.zero);
            detailAccentBarBot.raycastTarget = false;

            // ── RIGHT: STATS ──
            GameObject rightCol = new GameObject("RightCol");
            rightCol.transform.SetParent(inner, false);
            UIFactory.SetAnchors(rightCol, new Vector2(0.655f, 0.03f), new Vector2(0.99f, 0.97f), Vector2.zero, Vector2.zero);

            // Stats panel background
            RectTransform statsBg = UIFactory.CreatePanel(rightCol.transform, "StatsBg",
                new Color(0.04f, 0.03f, 0.025f, 0.85f));
            statsBg.anchorMin = Vector2.zero; statsBg.anchorMax = Vector2.one;
            statsBg.offsetMin = Vector2.zero; statsBg.offsetMax = Vector2.zero;
            Outline statsOut = statsBg.gameObject.AddComponent<Outline>();
            statsOut.effectColor = new Color(UIFactory.BorderGold.r, UIFactory.BorderGold.g, UIFactory.BorderGold.b, 0.3f);
            statsOut.effectDistance = new Vector2(1, 1);

            // Stats header
            Text statsHeader = UIFactory.CreateText(statsBg, "StatsHeader", "── STATISTICS ──", 18, TextAnchor.MiddleCenter, UIFactory.BorderGoldBright);
            statsHeader.fontStyle = FontStyle.Bold;
            UIFactory.SetAnchors(statsHeader.gameObject, new Vector2(0.02f, 0.90f), new Vector2(0.98f, 1f), Vector2.zero, Vector2.zero);

            // Difficulty row
            difficultyLabel = UIFactory.CreateText(statsBg, "DiffLabel", "Difficulty: ★★☆☆☆", 16, TextAnchor.MiddleCenter, UIFactory.TextGrey);
            UIFactory.SetAnchors(difficultyLabel.gameObject, new Vector2(0.02f, 0.81f), new Vector2(0.98f, 0.90f), Vector2.zero, Vector2.zero);

            // Capital
            Text capitalLabel = UIFactory.CreateText(statsBg, "Capital", "⚜ Capital: Paris", 16, TextAnchor.MiddleLeft, new Color(0.80f, 0.70f, 0.50f));
            UIFactory.SetAnchors(capitalLabel.gameObject, new Vector2(0.06f, 0.72f), new Vector2(0.94f, 0.81f), Vector2.zero, Vector2.zero);
            detailStats = capitalLabel; // Reuse for capital display

            // 6 stat bars
            float barStartY = 0.68f;
            float barHeight = 0.085f;
            float barGap = 0.015f;
            for (int i = 0; i < 6; i++)
            {
                float y0 = barStartY - (i + 1) * (barHeight + barGap);
                float y1 = y0 + barHeight;

                // Label
                Text label = UIFactory.CreateText(statsBg, $"Stat{i}Label", StatNames[i], 14, TextAnchor.MiddleLeft, UIFactory.TextGrey);
                UIFactory.SetAnchors(label.gameObject, new Vector2(0.06f, y0), new Vector2(0.35f, y1), Vector2.zero, Vector2.zero);
                statBarLabels[i] = label;

                // Bar background
                RectTransform barBg = UIFactory.CreatePanel(statsBg, $"StatBar{i}Bg", new Color(0.12f, 0.13f, 0.11f, 0.9f));
                barBg.anchorMin = new Vector2(0.37f, y0 + 0.01f);
                barBg.anchorMax = new Vector2(0.92f, y1 - 0.01f);
                barBg.offsetMin = Vector2.zero; barBg.offsetMax = Vector2.zero;

                // Bar fill
                GameObject fillGO = new GameObject($"StatBar{i}Fill");
                fillGO.transform.SetParent(barBg, false);
                RectTransform fillRT = fillGO.AddComponent<RectTransform>();
                fillRT.anchorMin = Vector2.zero;
                fillRT.anchorMax = new Vector2(0.8f, 1f);
                fillRT.offsetMin = new Vector2(1, 1);
                fillRT.offsetMax = new Vector2(-1, -1);
                Image fillImg = fillGO.AddComponent<Image>();
                fillImg.color = StatColors[i];
                fillImg.raycastTarget = false;
                statBarFills[i] = fillImg;
            }
        }

        // ─── ACTION BAR ─────────────────────────────────────────────────

        private void BuildActionBar(RectTransform parent)
        {
            // Start Campaign button
            startCampaignBtn = UIFactory.CreateWarhammerButton(parent, "BtnStartCampaign",
                "▶  START CAMPAIGN", 26, OnStartCampaign);
            UIFactory.SetAnchors(startCampaignBtn.gameObject,
                new Vector2(0.28f, 0.015f), new Vector2(0.72f, 0.085f), Vector2.zero, Vector2.zero);
            startBtnLabel = startCampaignBtn.GetComponentInChildren<Text>();

            // Back button
            Button backBtn = UIFactory.CreateGoldButton(parent, "BtnBack", "← Back to Menu", 14, () =>
            {
                PlaySelectSound();
                LoadingScreenUI.LoadSceneWithScreen("MainMenu");
            });
            UIFactory.SetAnchors(backBtn.gameObject,
                new Vector2(0.015f, 0.02f), new Vector2(0.13f, 0.075f), Vector2.zero, Vector2.zero);
        }

        // ─── SELECTION LOGIC ────────────────────────────────────────────

        private void SelectFaction(FactionType faction)
        {
            previousSelection = (int)selectedFaction;
            selectedFaction = faction;
            int idx = (int)faction;

            PlayerPrefs.SetInt("SelectedFaction", idx);
            PlayerPrefs.Save();

            // Update card visuals
            for (int i = 0; i < 24; i++)
            {
                bool isSelected = (i == idx);
                cardCheckmarks[i].SetActive(isSelected);

                Outline outline = factionCards[i].GetComponent<Outline>();
                Image bg = factionCards[i].GetComponent<Image>();

                if (isSelected)
                {
                    outline.effectColor = FactionColors[i];
                    outline.effectDistance = new Vector2(3f, 3f);
                    bg.color = new Color(0.10f, 0.07f, 0.05f, 0.99f);
                    cardNames[i].color = Color.white;
                    // Glow
                    Color gc = FactionColors[i];
                    cardGlows[i].color = new Color(gc.r, gc.g, gc.b, 0.25f);
                }
                else
                {
                    outline.effectColor = new Color(UIFactory.BorderGold.r, UIFactory.BorderGold.g, UIFactory.BorderGold.b, 0.25f);
                    outline.effectDistance = new Vector2(1.5f, 1.5f);
                    bg.color = new Color(0.06f, 0.04f, 0.035f, 0.97f);
                    cardNames[i].color = UIFactory.GoldAccent;
                    cardGlows[i].color = new Color(0, 0, 0, 0);
                }
            }

            // Update detail panel
            UpdateDetailPanel(idx);


            Debug.Log($"[FactionSelection] Selected: {faction}");
        }

        private void UpdateDetailPanel(int idx)
        {
            // Flag
            if (flagSprites[idx] != null)
            {
                detailFlagImage.sprite = flagSprites[idx];
                detailFlagImage.color = Color.white;
            }
            else
            {
                detailFlagImage.sprite = null;
                detailFlagImage.color = FactionColors[idx] * 0.5f;
            }

            // Text
            detailFactionName.text = FactionNames[idx];
            detailMotto.text = FactionMottos[idx];
            detailAccentBar.color = FactionColors[idx];
            detailAccentBarBot.color = FactionColors[idx];

            // Capital
            detailStats.text = $"⚜ Capital: {FactionCapitals[idx]}";

            // Difficulty
            int diff = FactionDifficulty[idx];
            string stars = "";
            for (int d = 0; d < 5; d++)
                stars += d < diff ? "★" : "☆";
            difficultyLabel.text = $"Difficulty: {stars}";
            difficultyLabel.color = GetDifficultyColor(diff);

            // Stat bars — animate fill width
            for (int i = 0; i < 6; i++)
            {
                float val = FactionStatValues[idx, i] / 10f;
                RectTransform fillRT = statBarFills[i].GetComponent<RectTransform>();
                fillRT.anchorMax = new Vector2(Mathf.Clamp01(val), 1f);
                // Color intensity based on value
                Color baseCol = StatColors[i];
                statBarFills[i].color = val > 0.7f ? baseCol : new Color(baseCol.r * 0.7f, baseCol.g * 0.7f, baseCol.b * 0.7f, 0.85f);
            }

            // Typewriter description — must stop previous coroutine properly
            if (typewriterCoroutine != null)
                StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = StartCoroutine(TypewriterDescription(FactionDescriptions[idx]));
        }

        private Color GetDifficultyColor(int difficulty)
        {
            switch (difficulty)
            {
                case 1: return new Color(0.3f, 0.85f, 0.3f);
                case 2: return new Color(0.5f, 0.85f, 0.3f);
                case 3: return new Color(0.85f, 0.85f, 0.3f);
                case 4: return new Color(0.85f, 0.55f, 0.2f);
                case 5: return new Color(0.85f, 0.25f, 0.2f);
                default: return UIFactory.TextGrey;
            }
        }

        // ─── ANIMATIONS ─────────────────────────────────────────────────

        private IEnumerator AnimateSelectedCard()
        {
            while (true)
            {
                int idx = (int)selectedFaction;
                if (idx >= 0 && idx < 24 && factionCards[idx] != null)
                {
                    Outline outline = factionCards[idx].GetComponent<Outline>();
                    if (outline != null)
                    {
                        float t = 0;
                        while (t < 1f)
                        {
                            t += Time.deltaTime * 1.8f;
                            float pulse = 0.6f + Mathf.Sin(t * Mathf.PI * 2) * 0.4f;
                            Color fc = FactionColors[idx];
                            outline.effectColor = new Color(fc.r, fc.g, fc.b, pulse);

                            // Pulse the glow too
                            if (cardGlows[idx] != null)
                            {
                                float glowAlpha = 0.15f + Mathf.Sin(t * Mathf.PI * 2) * 0.12f;
                                cardGlows[idx].color = new Color(fc.r, fc.g, fc.b, glowAlpha);
                            }
                            yield return null;
                        }
                    }
                }
                yield return new WaitForSeconds(0.05f);
            }
        }

        private IEnumerator PulseStartButton()
        {
            if (startCampaignBtn == null) yield break;
            Image img = startCampaignBtn.GetComponent<Image>();
            if (img == null) yield break;
            Color baseColor = img.color;

            while (true)
            {
                float t = 0;
                while (t < 1f)
                {
                    t += Time.deltaTime * 1.2f;
                    float intensity = 1f + Mathf.Sin(t * Mathf.PI * 2) * 0.2f;
                    if (img != null)
                        img.color = new Color(
                            Mathf.Clamp01(baseColor.r * intensity),
                            Mathf.Clamp01(baseColor.g * intensity),
                            Mathf.Clamp01(baseColor.b * intensity),
                            baseColor.a);
                    yield return null;
                }
                yield return null;
            }
        }

        private IEnumerator TypewriterDescription(string fullText)
        {
            if (detailDescription == null) yield break;
            detailDescription.text = "";
            for (int i = 0; i < fullText.Length; i++)
            {
                if (detailDescription == null) yield break;
                detailDescription.text += fullText[i];
                if (i % 3 == 0)
                    yield return new WaitForSeconds(0.006f);
            }
        }

        // ─── AUDIO ──────────────────────────────────────────────────────

        private void PlayHoverSound()
        {
            if (hoverSound != null && hoverSound.clip != null)
                hoverSound.Play();
        }

        private void PlaySelectSound()
        {
            if (selectSound != null && selectSound.clip != null)
                selectSound.Play();
        }

        // ─── ACTIONS ────────────────────────────────────────────────────

        private void OnStartCampaign()
        {
            PlayerPrefs.SetInt("SelectedFaction", (int)selectedFaction);
            PlayerPrefs.Save();
            Debug.Log($"[FactionSelection] Starting campaign as {selectedFaction}");
            LoadingScreenUI.LoadSceneWithScreen("Campaign", (int)selectedFaction);
        }
    }
}
