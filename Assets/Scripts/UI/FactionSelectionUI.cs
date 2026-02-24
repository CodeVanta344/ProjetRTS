using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Collections;
using NapoleonicWars.Data;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// Total War-inspired faction selection screen.
    /// Left panel: scrollable faction list with flag thumbnails.
    /// Center: large ornate flag of selected faction.
    /// Right: detailed faction info, stats, and campaign options.
    /// </summary>
    public class FactionSelectionUI : MonoBehaviour
    {
        private Canvas canvas;
        private FactionType selectedFaction = FactionType.France;

        // Faction list entries (left panel)
        private GameObject[] listEntries = new GameObject[24];
        private Image[] listFlags = new Image[24];
        private Text[] listNames = new Text[24];
        private Image[] listBgs = new Image[24];
        private Image[] listSelectIndicators = new Image[24];

        // Detail panel (right side)
        private Image detailFlagImage;
        private Text detailFactionName;
        private Text detailMotto;
        private Text detailDescription;
        private Text detailCapital;
        private Text difficultyLabel;
        private Image[] statBarFills = new Image[6];
        private Text[] statBarLabels = new Text[6];


        // Buttons
        private Button startCampaignBtn;
        private Sprite[] flagSprites = new Sprite[24];

        // Audio
        private AudioSource hoverSound;
        private AudioSource selectSound;

        // Animation
        private Coroutine typewriterCoroutine;
        private Coroutine pulseCoroutine;

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
            "France", "Britain", "Prussia", "Russia", "Austria", "Spain", "Ottoman Empire",
            "Portugal", "Sweden", "Denmark", "Poland",
            "Venice", "Netherlands", "Bavaria", "Saxony",
            "Papal States", "Savoy", "Switzerland", "Genoa",
            "Tuscany", "Hanover", "Modena", "Parma", "Lorraine"
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

        private static readonly string[] StatIcons = {
            "⚔", "⚓", "💰", "👥", "🏰", "📜"
        };

        private static readonly Color[] StatColors = {
            new Color(0.86f, 0.27f, 0.22f), // Army - matte red
            new Color(0.24f, 0.35f, 0.50f), // Navy - steel blue
            new Color(0.90f, 0.73f, 0.13f), // Economy - matte yellow
            new Color(0.40f, 0.72f, 0.35f), // Population - matte green
            new Color(0.50f, 0.50f, 0.50f), // Forts - grey
            new Color(0.60f, 0.40f, 0.80f), // Diplomacy - purple
        };

        // Difficulty: 1-5 pips
        private static readonly int[] FactionDifficulty = {
            2, 3, 4, 3, 4, 4, 5,
            4, 3, 4, 5,
            4, 3, 4, 4,
            5, 4, 3, 4,
            4, 4, 5, 5, 5
        };

        private static readonly string[] FlagResourcePaths = {
            "UI/Flags/flag_france", "UI/Flags/flag_britain", "UI/Flags/flag_prussia",
            "UI/Flags/flag_russia", "UI/Flags/flag_austria", "UI/Flags/flag_spain",
            "UI/Flags/flag_ottoman", "UI/Flags/flag_portugal", "UI/Flags/flag_sweden",
            "UI/Flags/flag_denmark", "UI/Flags/flag_poland", "UI/Flags/flag_venice",
            "UI/Flags/flag_dutch", "UI/Flags/flag_bavaria", "UI/Flags/flag_saxony",
            "UI/Flags/flag_papal", "UI/Flags/flag_savoy", "UI/Flags/flag_switzerland",
            "UI/Flags/flag_genoa", "UI/Flags/flag_tuscany", "UI/Flags/flag_hanover",
            "UI/Flags/flag_modena", "UI/Flags/flag_parma", "UI/Flags/flag_lorraine"
        };

        private static readonly Color[] FactionAccentColors = {
            new Color(0.15f, 0.30f, 0.75f),    // France
            new Color(0.75f, 0.15f, 0.15f),    // Britain
            new Color(0.25f, 0.25f, 0.28f),    // Prussia
            new Color(0.15f, 0.55f, 0.20f),    // Russia
            new Color(0.85f, 0.75f, 0.25f),    // Austria
            new Color(0.90f, 0.55f, 0.15f),    // Spain
            new Color(0.55f, 0.15f, 0.20f),    // Ottoman
            new Color(0.20f, 0.70f, 0.25f),    // Portugal
            new Color(0.25f, 0.45f, 0.80f),    // Sweden
            new Color(0.80f, 0.20f, 0.22f),    // Denmark
            new Color(0.80f, 0.22f, 0.40f),    // Poland
            new Color(0.80f, 0.25f, 0.22f),    // Venice
            new Color(0.90f, 0.50f, 0.12f),    // Dutch
            new Color(0.25f, 0.60f, 0.90f),    // Bavaria
            new Color(0.22f, 0.75f, 0.40f),    // Saxony
            new Color(0.90f, 0.82f, 0.22f),    // Papal
            new Color(0.80f, 0.22f, 0.22f),    // Savoy
            new Color(0.80f, 0.22f, 0.22f),    // Switzerland
            new Color(0.80f, 0.22f, 0.22f),    // Genoa
            new Color(0.80f, 0.22f, 0.22f),    // Tuscany
            new Color(0.80f, 0.22f, 0.22f),    // Hanover
            new Color(0.80f, 0.22f, 0.22f),    // Modena
            new Color(0.80f, 0.22f, 0.22f),    // Parma
            new Color(0.80f, 0.22f, 0.22f)     // Lorraine
        };

        // ─── LIFECYCLE ──────────────────────────────────────────────────

        private void SetupAudio()
        {
            hoverSound = gameObject.AddComponent<AudioSource>();
            hoverSound.volume = 0.3f;
            selectSound = gameObject.AddComponent<AudioSource>();
            selectSound.volume = 0.5f;
        }

        private void Awake()
        {
            // Singleton guard — destroy any other instances BEFORE canvas rebuild
            var all = Object.FindObjectsByType<FactionSelectionUI>(FindObjectsSortMode.None);
            foreach (var other in all)
            {
                if (other != this)
                {
                    Debug.Log("[FactionSelectionUI] Destroying duplicate instance");
                    Object.DestroyImmediate(other.gameObject);
                }
            }
            
            // Clean up any stale canvases from prior loads
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c.gameObject.name == "FactionSelectCanvas")
                {
                    Debug.Log("[FactionSelectionUI] Destroying stale FactionSelectCanvas");
                    Object.DestroyImmediate(c.gameObject);
                }
            }
        }

        private void Start()
        {
            SetupAudio();
            LoadFlagSprites();
            BuildUI();
            SelectFaction(FactionType.France);
            pulseCoroutine = StartCoroutine(PulseStartButton());
        }

        private void LoadFlagSprites()
        {
            for (int i = 0; i < 24; i++)
            {
                Texture2D tex = Resources.Load<Texture2D>(FlagResourcePaths[i]);
                if (tex != null)
                {
                    // Force high-quality rendering
                    tex.filterMode = FilterMode.Bilinear;
                    tex.anisoLevel = 8;
                    tex.wrapMode = TextureWrapMode.Clamp;
                    flagSprites[i] = Sprite.Create(tex,
                        new Rect(0, 0, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f),
                        100f, 0, SpriteMeshType.FullRect);
                }
            }
        }

        // ─── BUILD UI ───────────────────────────────────────────────────

        private void BuildUI()
        {
            canvas = UIFactory.CreateCanvas("FactionSelectCanvas", 10);

            // === FULL-SCREEN BACKGROUND (Pure Pitch Black) ===
            GameObject bgGO = new GameObject("Background");
            bgGO.transform.SetParent(canvas.transform, false);
            RectTransform bgRT = bgGO.AddComponent<RectTransform>();
            UIFactory.SetAnchors(bgRT, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.04f, 0.04f, 0.04f, 1f); // Almost pure black
            
            // Main Content Area - clustered in the center ~76% width
            RectTransform mainContent = UIFactory.CreatePanel(bgRT, "MainContent", Color.clear);
            UIFactory.SetAnchors(mainContent, new Vector2(0.12f, 0.10f), new Vector2(0.88f, 0.92f), Vector2.zero, Vector2.zero);

            // Build the three columns inside the centered container
            BuildFactionList(mainContent);
            BuildFlagDisplay(mainContent);
            BuildInfoPanel(mainContent);

            // Action bar sits below
            BuildActionBar(bgRT);
        }

        // ─── LEFT PANEL: FACTION LIST ───────────────────────────────────

        private void BuildFactionList(RectTransform parent)
        {
            // Container — compact, ~26% width of the clustered area
            RectTransform listPanel = UIFactory.CreatePanel(parent, "FactionListPanel", new Color(0.03f, 0.025f, 0.02f, 0.8f));
            listPanel.anchorMin = new Vector2(0.02f, 0.10f);
            listPanel.anchorMax = new Vector2(0.28f, 0.92f);
            listPanel.offsetMin = Vector2.zero;
            listPanel.offsetMax = Vector2.zero;

            // Scroll area (no separate header — just scrollable list)
            GameObject scrollGO = new GameObject("ListScroll");
            scrollGO.transform.SetParent(listPanel, false);
            RectTransform scrollRT = scrollGO.AddComponent<RectTransform>();
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMin = Vector2.zero;
            scrollRT.offsetMax = Vector2.zero;

            ScrollRect scrollRect = scrollGO.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;

            // Viewport
            GameObject viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(scrollGO.transform, false);
            RectTransform vpRT = viewportGO.AddComponent<RectTransform>();
            UIFactory.SetAnchors(vpRT, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            viewportGO.AddComponent<RectMask2D>();
            scrollRect.viewport = vpRT;

            // Content
            GameObject contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform, false);
            RectTransform contentRT = contentGO.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.offsetMin = Vector2.zero;
            contentRT.offsetMax = Vector2.zero;

            VerticalLayoutGroup vlg = contentGO.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 1f;
            vlg.padding = new RectOffset(4, 4, 4, 4);

            ContentSizeFitter csf = contentGO.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRT;

            // Create entries
            for (int i = 0; i < 24; i++)
                CreateFactionListEntry(contentGO.transform, i);
        }

        private void CreateFactionListEntry(Transform parent, int index)
        {
            float entryHeight = 40f;

            GameObject entryGO = new GameObject($"Entry_{(FactionType)index}");
            entryGO.transform.SetParent(parent, false);
            RectTransform entryRT = entryGO.AddComponent<RectTransform>();
            entryRT.sizeDelta = new Vector2(0, entryHeight);

            // Background
            Image entryBg = entryGO.AddComponent<Image>();
            entryBg.color = new Color(0.055f, 0.05f, 0.04f, 0.7f);
            listBgs[index] = entryBg;

            // Layout element
            LayoutElement le = entryGO.AddComponent<LayoutElement>();
            le.preferredHeight = entryHeight;

            // Selection Outline Indicator (Mockup has a gold box around selected item, not just a left bar)
            GameObject outlineGO = new GameObject("SelectIndicator");
            outlineGO.transform.SetParent(entryRT, false);
            RectTransform indRT = outlineGO.AddComponent<RectTransform>();
            UIFactory.SetAnchors(indRT, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image indImg = outlineGO.AddComponent<Image>();
            indImg.color = new Color(0, 0, 0, 0f); // We'll toggle alpha
            indImg.raycastTarget = false;
            Outline selOut = outlineGO.AddComponent<Outline>();
            selOut.effectColor = new Color(0.85f, 0.65f, 0.25f, 0f); // Toggled in script later
            selOut.effectDistance = new Vector2(2, -2);
            listSelectIndicators[index] = indImg; // Using this array to hold the object we get Outline from

            // Small flag thumbnail (compact rectangle)
            GameObject flagGO = new GameObject("FlagThumb");
            flagGO.transform.SetParent(entryRT, false);
            RectTransform flagRT = flagGO.AddComponent<RectTransform>();
            flagRT.anchorMin = new Vector2(0.05f, 0.12f);
            flagRT.anchorMax = new Vector2(0.25f, 0.88f);
            flagRT.offsetMin = Vector2.zero;
            flagRT.offsetMax = Vector2.zero;

            Image flagBorImg = flagGO.AddComponent<Image>();
            flagBorImg.color = new Color(0.85f, 0.65f, 0.25f, 1f); // Always gold border like the mockup

            GameObject flagInnerGO = new GameObject("FlagImg");
            flagInnerGO.transform.SetParent(flagGO.transform, false);
            UIFactory.SetAnchors(flagInnerGO, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f), Vector2.zero, Vector2.zero);
            Image flagImg = flagInnerGO.AddComponent<Image>();
            flagImg.preserveAspect = false;
            flagImg.raycastTarget = false;
            if (flagSprites[index] != null)
                flagImg.sprite = flagSprites[index];
            else
                flagImg.color = FactionAccentColors[index] * 0.5f;
            listFlags[index] = flagImg;

            // Faction name
            Text nameText = UIFactory.CreateText(entryRT, "Name", FactionShortNames[index],
                14, TextAnchor.MiddleLeft, new Color(0.55f, 0.55f, 0.5f));
            UIFactory.SetAnchors(nameText.gameObject, new Vector2(0.28f, 0.05f), new Vector2(0.82f, 0.95f), Vector2.zero, Vector2.zero);
            nameText.resizeTextForBestFit = true;
            nameText.resizeTextMinSize = 10;
            nameText.resizeTextMaxSize = 16;
            listNames[index] = nameText;

            // Difficulty dots (small, right side)
            int diff = FactionDifficulty[index];
            string dots = "";
            for (int d = 0; d < diff; d++) dots += "★";
            Text diffText = UIFactory.CreateText(entryRT, "Diff", dots, 7, TextAnchor.MiddleRight, GetDifficultyColor(diff));
            UIFactory.SetAnchors(diffText.gameObject, new Vector2(0.83f, 0.15f), new Vector2(0.97f, 0.85f), Vector2.zero, Vector2.zero);

            // Button + Hover
            Button btn = entryGO.AddComponent<Button>();
            btn.targetGraphic = entryBg;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.4f, 1.3f, 1.15f);
            cb.pressedColor = new Color(0.8f, 0.7f, 0.5f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            int capturedIndex = index;
            btn.onClick.AddListener(() => { PlaySelectSound(); SelectFaction((FactionType)capturedIndex); });

            // Hover events
            EventTrigger trigger = entryGO.AddComponent<EventTrigger>();

            EventTrigger.Entry enterEntry = new EventTrigger.Entry();
            enterEntry.eventID = EventTriggerType.PointerEnter;
            enterEntry.callback.AddListener((data) =>
            {
                PlayHoverSound();
                if ((int)selectedFaction != capturedIndex)
                    entryBg.color = new Color(0.08f, 0.07f, 0.05f, 0.85f);
            });
            trigger.triggers.Add(enterEntry);

            EventTrigger.Entry exitEntry = new EventTrigger.Entry();
            exitEntry.eventID = EventTriggerType.PointerExit;
            exitEntry.callback.AddListener((data) =>
            {
                if ((int)selectedFaction != capturedIndex)
                    entryBg.color = new Color(0.055f, 0.05f, 0.04f, 0.7f);
            });
            trigger.triggers.Add(exitEntry);

            listEntries[index] = entryGO;
        }

        // ─── CENTER: LARGE FLAG DISPLAY ─────────────────────────────────

        private Text detailFactionNameOnFlag; // Name below the flag frame

        private void BuildFlagDisplay(RectTransform parent)
        {
            // Center area - takes up a massive portion of the center
            RectTransform flagArea = UIFactory.CreatePanel(parent, "FlagArea", Color.clear);
            flagArea.anchorMin = new Vector2(0.32f, 0.0f);
            flagArea.anchorMax = new Vector2(0.66f, 1.0f);
            flagArea.offsetMin = Vector2.zero;
            flagArea.offsetMax = Vector2.zero;

            // Ornate gold outer frame
            RectTransform outerFrame = UIFactory.CreatePanel(flagArea, "OuterFrame", UIFactory.BorderGold);
            outerFrame.anchorMin = new Vector2(0.05f, 0.05f);
            outerFrame.anchorMax = new Vector2(0.95f, 0.95f);
            outerFrame.offsetMin = Vector2.zero;
            outerFrame.offsetMax = Vector2.zero;

            // Dark inset frame
            RectTransform darkFrame = UIFactory.CreatePanel(outerFrame, "DarkFrame", new Color(0.02f, 0.02f, 0.02f, 1f));
            UIFactory.SetAnchors(darkFrame, Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -6));

            // Inner gold frame
            RectTransform innerFrame = UIFactory.CreatePanel(darkFrame, "InnerFrame",
                new Color(UIFactory.BorderGold.r * 0.7f, UIFactory.BorderGold.g * 0.7f, UIFactory.BorderGold.b * 0.7f, 1f));
            UIFactory.SetAnchors(innerFrame, Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4));

            // Flag surface
            RectTransform flagSurface = UIFactory.CreatePanel(innerFrame, "FlagSurface", new Color(0.01f, 0.01f, 0.01f, 1f));
            UIFactory.SetAnchors(flagSurface, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2));

            // Flag image
            GameObject flagImgGO = new GameObject("FlagImage");
            flagImgGO.transform.SetParent(flagSurface, false);
            UIFactory.SetAnchors(flagImgGO, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            detailFlagImage = flagImgGO.AddComponent<Image>();
            detailFlagImage.preserveAspect = false;

            // Corner ornaments (Pale yellow drafting tape style)
            CreateCornerOrnament(outerFrame, new Vector2(0, 0), new Vector2(0.03f, 0.025f));
            CreateCornerOrnament(outerFrame, new Vector2(0.97f, 0), new Vector2(1f, 0.025f));
            CreateCornerOrnament(outerFrame, new Vector2(0, 0.975f), new Vector2(0.03f, 1f));
            CreateCornerOrnament(outerFrame, new Vector2(0.97f, 0.975f), new Vector2(1f, 1f));

            // Faction name INSIDE the flag bottom, exactly like the mockup
            detailFactionNameOnFlag = UIFactory.CreateText(flagSurface, "FlagFactionName",
                "FRANCE", 36, TextAnchor.MiddleCenter, new Color(0.85f, 0.75f, 0.45f));
            detailFactionNameOnFlag.fontStyle = FontStyle.Normal; // Mockup non-bold serif style
            UIFactory.SetAnchors(detailFactionNameOnFlag.gameObject, new Vector2(0.0f, 0.05f), new Vector2(1.0f, 0.18f), Vector2.zero, Vector2.zero);
            Shadow fns = detailFactionNameOnFlag.gameObject.AddComponent<Shadow>();
            fns.effectColor = new Color(0, 0, 0, 0.9f);
            fns.effectDistance = new Vector2(3, -3);
        }

        private void CreateCornerOrnament(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            RectTransform orn = UIFactory.CreatePanel(parent, "Corner", new Color(0.96f, 0.89f, 0.65f, 1f));
            orn.anchorMin = anchorMin;
            orn.anchorMax = anchorMax;
            orn.offsetMin = Vector2.zero;
            orn.offsetMax = Vector2.zero;
            orn.GetComponent<Image>().raycastTarget = false;
        }

        // ─── RIGHT: INFO + STATS ────────────────────────────────────────

        private void BuildInfoPanel(RectTransform parent)
        {
            // Right column — faction info and stats (matches mockup exactly)
            RectTransform infoArea = UIFactory.CreatePanel(parent, "InfoArea", Color.clear);
            infoArea.anchorMin = new Vector2(0.70f, 0.0f);
            infoArea.anchorMax = new Vector2(1.00f, 1.0f);
            infoArea.offsetMin = Vector2.zero;
            infoArea.offsetMax = Vector2.zero;

            // ── FACTION NAME (large, top) ──
            detailFactionName = UIFactory.CreateText(infoArea, "FactionName",
                FactionNames[0], 42, TextAnchor.UpperLeft, new Color(0.85f, 0.75f, 0.45f));
            detailFactionName.fontStyle = FontStyle.Normal;
            UIFactory.SetAnchors(detailFactionName.gameObject, new Vector2(0.0f, 0.88f), new Vector2(1.0f, 1.0f), Vector2.zero, Vector2.zero);
            Shadow fnShad = detailFactionName.gameObject.AddComponent<Shadow>();
            fnShad.effectColor = new Color(0, 0, 0, 0.9f);
            fnShad.effectDistance = new Vector2(2, -2);

            // ── MOTTO (italic, small) ──
            detailMotto = UIFactory.CreateText(infoArea, "Motto",
                FactionMottos[0], 16, TextAnchor.UpperLeft, new Color(0.6f, 0.6f, 0.6f));
            detailMotto.fontStyle = FontStyle.Italic;
            UIFactory.SetAnchors(detailMotto.gameObject, new Vector2(0.0f, 0.79f), new Vector2(1.0f, 0.86f), Vector2.zero, Vector2.zero);

            // ── DESCRIPTION (large block, main content) ──
            detailDescription = UIFactory.CreateText(infoArea, "Description",
                "Led by", 12, TextAnchor.UpperLeft, new Color(0.8f, 0.8f, 0.8f));
            UIFactory.SetAnchors(detailDescription.gameObject, new Vector2(0.0f, 0.65f), new Vector2(1.0f, 0.78f), Vector2.zero, Vector2.zero);

            // Hidden elements we still need references to (not shown in mockup)
            detailCapital = UIFactory.CreateText(infoArea, "Capital", "", 1, TextAnchor.UpperLeft, Color.clear);
            UIFactory.SetAnchors(detailCapital.gameObject, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            difficultyLabel = UIFactory.CreateText(infoArea, "Difficulty", "", 1, TextAnchor.UpperLeft, Color.clear);
            UIFactory.SetAnchors(difficultyLabel.gameObject, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);

            // ── STATS SECTION (4 bars like mockup: Army, Navy, Economy, Population) ──
            // Note: The mockup has no "NATIONAL STRENGTHS" header, just the bars
            Text statsH = UIFactory.CreateText(infoArea, "StatsH", "", 1, TextAnchor.MiddleCenter, Color.clear);
            UIFactory.SetAnchors(statsH.gameObject, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);

            // Only show 4 stats like the mockup
            int[] displayedStats = { 0, 1, 2, 3 }; // Army, Navy, Economy, Population
            float barTop = 0.40f;
            float barH = 0.055f;
            float gap = 0.025f;

            for (int si = 0; si < displayedStats.Length; si++)
            {
                int i = displayedStats[si];
                float y1 = barTop - si * (barH + gap);
                float y0 = y1 - barH;

                // Stat label (ARMY, NAVY, etc.)
                Text label = UIFactory.CreateText(infoArea, $"StatLabel{i}",
                    StatNames[i].ToUpper(), 13, TextAnchor.MiddleLeft, UIFactory.SilverText);
                label.fontStyle = FontStyle.Bold;
                UIFactory.SetAnchors(label.gameObject, new Vector2(0.04f, y0), new Vector2(0.24f, y1), Vector2.zero, Vector2.zero);
                statBarLabels[i] = label;

                // Bar background
                RectTransform barBg = UIFactory.CreatePanel(infoArea, $"BarBg{i}",
                    new Color(0.1f, 0.08f, 0.07f, 1f));
                barBg.anchorMin = new Vector2(0.26f, y0 + 0.01f);
                barBg.anchorMax = new Vector2(0.97f, y1 - 0.01f);
                barBg.offsetMin = Vector2.zero;
                barBg.offsetMax = Vector2.zero;

                // Bar fill
                GameObject fillGO = new GameObject($"BarFill{i}");
                fillGO.transform.SetParent(barBg, false);
                RectTransform fillRT = fillGO.AddComponent<RectTransform>();
                fillRT.anchorMin = Vector2.zero;
                fillRT.anchorMax = new Vector2(0.8f, 1f);
                fillRT.offsetMin = Vector2.zero;
                fillRT.offsetMax = Vector2.zero;
                Image fillImg = fillGO.AddComponent<Image>();
                fillImg.color = StatColors[i];
                fillImg.raycastTarget = false;
                statBarFills[i] = fillImg;
            }

            // Create hidden fills for stats 4 & 5 (Forts, Diplomacy) so arrays don't break
            for (int i = 4; i < 6; i++)
            {
                statBarLabels[i] = detailCapital; // dummy reference
                GameObject dummyFill = new GameObject($"DummyFill{i}");
                dummyFill.transform.SetParent(infoArea, false);
                Image dImg = dummyFill.AddComponent<Image>();
                dImg.color = Color.clear;
                dImg.raycastTarget = false;
                statBarFills[i] = dImg;
            }
        }

        // ─── ACTION BAR ─────────────────────────────────────────────────

        private void BuildActionBar(RectTransform parent)
        {
            // Bottom bar background
            RectTransform barBg = UIFactory.CreatePanel(parent, "ActionBar", new Color(0.035f, 0.025f, 0.02f, 0.98f));
            barBg.anchorMin = new Vector2(0, 0);
            barBg.anchorMax = new Vector2(1, 0.085f);
            barBg.offsetMin = Vector2.zero;
            barBg.offsetMax = Vector2.zero;

            // Top gold line
            RectTransform topLine = UIFactory.CreatePanel(barBg, "TopLine", UIFactory.BorderGold);
            topLine.anchorMin = new Vector2(0, 0.92f);
            topLine.anchorMax = Vector2.one;
            topLine.offsetMin = Vector2.zero;
            topLine.offsetMax = Vector2.zero;
            topLine.GetComponent<Image>().raycastTarget = false;

            // START CAMPAIGN button (center, prominent, red/crimson in mockup)
            startCampaignBtn = UIFactory.CreateWarhammerButton(barBg, "BtnStart",
                "START CAMPAIGN", 24, OnStartCampaign);
            UIFactory.SetAnchors(startCampaignBtn.gameObject,
                new Vector2(0.40f, 0.15f), new Vector2(0.60f, 0.85f), Vector2.zero, Vector2.zero);
            
            // Note: The mockup has no "BACK" button visible, but we need one for UX.
            // We'll keep it small on the left.
            Button backBtn = UIFactory.CreateButton(barBg, "BtnBack", "← BACK", 13, () =>
            {
                PlaySelectSound();
                LoadingScreenUI.LoadSceneWithScreen("MainMenu");
            });
            UIFactory.SetAnchors(backBtn.gameObject,
                new Vector2(0.01f, 0.15f), new Vector2(0.12f, 0.85f), Vector2.zero, Vector2.zero);
        }

        // ─── SELECTION LOGIC ────────────────────────────────────────────

        private void SelectFaction(FactionType faction)
        {
            selectedFaction = faction;
            int idx = (int)faction;
            PlayerPrefs.SetInt("SelectedFaction", idx);
            PlayerPrefs.Save();

            // Update list visuals
            for (int i = 0; i < 24; i++)
            {
                bool sel = (i == idx);

                if (sel)
                {
                    listBgs[i].color = new Color(0.15f, 0.10f, 0.05f, 0.9f);
                    listNames[i].color = new Color(0.85f, 0.65f, 0.25f);
                    listNames[i].fontStyle = FontStyle.Bold;
                    
                    Outline olIndicator = listSelectIndicators[i].GetComponent<Outline>();
                    if (olIndicator != null) olIndicator.effectColor = new Color(0.85f, 0.65f, 0.25f, 1f);
                }
                else
                {
                    listBgs[i].color = new Color(0f, 0f, 0f, 0f);
                    listNames[i].color = new Color(0.55f, 0.55f, 0.5f);
                    listNames[i].fontStyle = FontStyle.Normal;

                    Outline olIndicator = listSelectIndicators[i].GetComponent<Outline>();
                    if (olIndicator != null) olIndicator.effectColor = new Color(0, 0, 0, 0f);
                }
            }

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
                detailFlagImage.color = FactionAccentColors[idx] * 0.5f;
            }

            detailFactionName.text = FactionNames[idx];
            if (detailFactionNameOnFlag != null)
                detailFactionNameOnFlag.text = FactionShortNames[idx].ToUpper();
            detailMotto.text = FactionMottos[idx];
            detailCapital.text = $"⚜ Capital: {FactionCapitals[idx]}";

            // Difficulty
            int diff = FactionDifficulty[idx];
            string stars = "";
            for (int d = 0; d < 5; d++)
                stars += d < diff ? "★" : "☆";
            difficultyLabel.text = $"Difficulty: {stars}";
            difficultyLabel.color = GetDifficultyColor(diff);

            // Stat bars (only 4 displayed: Army, Navy, Economy, Population)
            for (int i = 0; i < 4; i++)
            {
                int val = FactionStatValues[idx, i];
                float norm = val / 10f;
                RectTransform fillRT = statBarFills[i].GetComponent<RectTransform>();
                fillRT.anchorMax = new Vector2(Mathf.Clamp01(norm), 1f);

                Color baseCol = StatColors[i];
                statBarFills[i].color = norm > 0.7f ? baseCol :
                    new Color(baseCol.r * 0.7f, baseCol.g * 0.7f, baseCol.b * 0.7f, 0.85f);
            }

            // Description is simplified entirely to "Led by" to match the mockup
            detailDescription.text = "Led by";
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
                default: return UIFactory.SilverText;
            }
        }

        // ─── ANIMATIONS ─────────────────────────────────────────────────

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
                    float intensity = 1f + Mathf.Sin(t * Mathf.PI * 2) * 0.15f;
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
                    yield return new WaitForSeconds(0.005f);
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
