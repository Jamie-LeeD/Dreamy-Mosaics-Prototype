using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace DreamyMosaics.Puzzle
{
    public sealed class PuzzleApp : MonoBehaviour
    {
        enum AppScreen
        {
            Menu,
            HowTo,
            Levels,
            Play,
            Pause,
            Settings,
            Victory,
            Defeat
        }

        static readonly Color Deep = new Color(0.15f, 0.08f, 0.12f, 1f);
        static readonly Color Ink = new Color(0.2f, 0.1f, 0.14f, 1f);
        static readonly Color Cream = new Color(0.98f, 0.95f, 0.92f, 1f);
        static readonly Color Rose = new Color(0.76f, 0.32f, 0.48f, 1f);
        static readonly Color RoseDark = new Color(0.45f, 0.18f, 0.28f, 1f);
        static readonly Color Gold = new Color(1f, 0.86f, 0.42f, 1f);
        static readonly Color Warn = new Color(1f, 0.78f, 0.62f, 1f);
        static readonly Color Guide = new Color(0.38f, 0.72f, 0.95f, 1f);
        static readonly Color ButtonInk = new Color(0.45f, 0.12f, 0.72f, 1f);
        static readonly Color[] RegionColors =
        {
            new Color(0.95f, 0.62f, 0.70f, 1f),
            new Color(0.73f, 0.62f, 0.90f, 1f),
            new Color(0.98f, 0.78f, 0.55f, 1f),
            new Color(0.64f, 0.82f, 0.68f, 1f),
            new Color(0.58f, 0.75f, 0.92f, 1f),
            new Color(0.95f, 0.84f, 0.46f, 1f),
            new Color(0.92f, 0.55f, 0.48f, 1f),
            new Color(0.55f, 0.84f, 0.80f, 1f),
            new Color(0.80f, 0.58f, 0.76f, 1f)
        };

        const string HowToText =
            "Place one memory in every row, every column, and every coloured region.\n\n" +
            "Two memories cannot share a row, a column, or a region. Two memories also cannot touch, even diagonally. " +
            "They may sit on the same diagonal when at least one cell is between them.\n\n" +
            "Left-click a cell to place a memory. It appears as a white glow. " +
            "Right-click an empty cell to mark it with an X. Right-click that X again to remove it.\n\n" +
            "A legal memory dims every cell in its row, column, and region, plus every cell it touches. " +
            "Those cells are no longer available.\n\n" +
            "If a move already breaks a rule, the game explains why and does not spend a heart. " +
            "If a move follows the rules but is not part of the solution, it costs a heart. " +
            "Undo takes a memory back, and the heart comes back with it. Right-click an X again to remove it. " +
            "At zero hearts the puzzle stops. You can undo the last mistake or restart.\n\n" +
            "A hint explains one forced step. It does not fill the board in for you.";

        LevelData[] levels;
        bool[][] solutions;
        string[] problems;
        PlaySession session;
        AudioManager audio;
        GameContent content;
        Sprite catSprite;
        Sprite heartSprite;
        Sprite rounded;
        TMP_FontAsset font;

        AppScreen screen = AppScreen.Menu;
        AppScreen settingsReturn = AppScreen.Menu;
        int levelIndex;
        int selected = -1;
        int guideCell = -1;
        int stepIndex;
        float elapsed;
        bool showTimer = true;
        bool muted;
        Vector2 lastBoardSize;

        [SerializeField] Image background;
        [SerializeField] GameObject menuPanel;
        [SerializeField] GameObject howPanel;
        [SerializeField] GameObject levelPanel;
        [SerializeField] GameObject playPanel;
        [SerializeField] GameObject pausePanel;
        [SerializeField] GameObject settingsPanel;
        [SerializeField] GameObject victoryPanel;
        [SerializeField] GameObject defeatPanel;
        [SerializeField] GameObject heartRow;
        [SerializeField] GameObject practiceLabel;
        [SerializeField] TextMeshProUGUI messageLabel;
        [SerializeField] TextMeshProUGUI tutorialLabel;
        [SerializeField] TextMeshProUGUI titleLabel;
        [SerializeField] TextMeshProUGUI movesLabel;
        [SerializeField] TextMeshProUGUI timerLabel;
        [SerializeField] TextMeshProUGUI infoLabel;
        [SerializeField] TextMeshProUGUI victoryBody;
        [SerializeField] TextMeshProUGUI defeatBody;
        [SerializeField] TextMeshProUGUI hintButtonLabel;
        [SerializeField] Button tutorialContinue;
        [SerializeField] RectTransform boardArea;
        [SerializeField] RectTransform gridRect;
        [SerializeField] RectTransform levelContent;
        readonly List<CellView> cells = new List<CellView>();
        [SerializeField] Image[] heartImages;
        [SerializeField] Slider musicSlider;
        [SerializeField] Slider effectsSlider;

        void Awake()
        {
            rounded = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            font = TMP_Settings.defaultFontAsset;
            if (font == null)
                font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            catSprite = ArtFactory.CreateMemorySprite();
            heartSprite = ArtFactory.CreateHeartSprite();
            content = Resources.Load<GameContent>("GameContent");
            EnsureEventSystem();
            EnsureAudio();
            LoadProgressPrefs();
            LoadCampaign();
            if (playPanel != null)
                BindSceneUi();
            else
                BuildInterface();
            if (levels.Length == 0)
                ShowMenu();
            else
                ShowContinue();
        }

        void Update()
        {
            if (screen == AppScreen.Play)
                elapsed += Time.deltaTime;
            if (timerLabel != null)
                timerLabel.gameObject.SetActive(showTimer);
            if (timerLabel != null && showTimer)
                timerLabel.text = QueensRules.FormatClock(elapsed);

            if (EventSystem.current != null && screen == AppScreen.Play)
                EventSystem.current.SetSelectedGameObject(null);

            ReadKeys();
            if (boardArea != null && (screen == AppScreen.Play || screen == AppScreen.Pause || screen == AppScreen.Victory || screen == AppScreen.Defeat))
            {
                if ((boardArea.rect.size - lastBoardSize).sqrMagnitude > 4f)
                {
                    lastBoardSize = boardArea.rect.size;
                    LayoutGrid();
                }
            }
        }

        void LoadCampaign()
        {
            levels = PuzzleLibrary.Load();
            if (levels == null || levels.Length == 0)
            {
                levels = new LevelData[0];
                solutions = new bool[0][];
                problems = new string[0];
                return;
            }

            solutions = new bool[levels.Length][];
            problems = new string[levels.Length];
            for (int i = 0; i < levels.Length; i++)
            {
                Analysis analysis = QueensSearch.Analyze(levels[i], QueensSearch.DefaultNodeCap);
                if (analysis.SolutionCount == 1 && analysis.LogicSolved && analysis.Solution != null)
                    solutions[i] = analysis.Solution;
                else
                    problems[i] = string.IsNullOrEmpty(analysis.Error) ? "This puzzle failed verification." : analysis.Error;
            }
        }

        void LoadProgressPrefs()
        {
            showTimer = PlayerPrefs.GetInt("dm.timer", 1) == 1;
            muted = PlayerPrefs.GetInt("dm.mute", 0) == 1;
            float music = PlayerPrefs.GetFloat("dm.music", 0.35f);
            float sfx = PlayerPrefs.GetFloat("dm.sfx", 0.8f);
            if (audio != null)
            {
                audio.SetMusicVolume(music);
                audio.SetSfxVolume(sfx);
                audio.SetMuted(muted);
            }
        }

        void EnsureEventSystem()
        {
            EventSystem current = FindFirstObjectByType<EventSystem>();
            if (current == null)
            {
                GameObject systemObject = new GameObject("EventSystem");
                current = systemObject.AddComponent<EventSystem>();
                systemObject.AddComponent<StandaloneInputModule>();
            }

            current.sendNavigationEvents = false;
        }

        void EnsureAudio()
        {
            if (FindFirstObjectByType<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();

            audio = gameObject.AddComponent<AudioManager>();
            if (content != null)
                audio.Configure(content.music, content.button, content.exit, content.wrong, content.right);
        }

        void BindSceneUi()
        {
            if (heartImages != null)
            {
                for (int i = 0; i < heartImages.Length; i++)
                {
                    if (heartImages[i] == null)
                        continue;
                    heartImages[i].sprite = heartSprite;
                    heartImages[i].preserveAspect = true;
                }
            }

            Wire(menuPanel, "Play", ShowContinue, true);
            Wire(menuPanel, "Levels", ShowLevels, true);
            Wire(menuPanel, "How to Play", ShowHowTo, true);
            Wire(menuPanel, "Settings", delegate { OpenSettings(AppScreen.Menu); }, true);
            Wire(menuPanel, "Quit", QuitGame, false);
            Wire(howPanel, "Back", ShowMenu, true);
            Wire(levelPanel, "Back", ShowMenu, true);
            Wire(playPanel, "Pause", ShowPause, false);
            Wire(playPanel, "Undo", UndoMove, false);
            Wire(playPanel, "Hint", UseHint, false);
            Wire(playPanel, "Restart", RestartLevel, false);
            Wire(playPanel, "Continue", ContinueTutorial, true);
            Wire(pausePanel, "Resume", Resume, true);
            Wire(pausePanel, "Restart", RestartLevel, true);
            Wire(pausePanel, "Settings", delegate { OpenSettings(AppScreen.Pause); }, true);
            Wire(pausePanel, "Levels", ShowLevels, true);
            Wire(pausePanel, "Main Menu", ReturnToStart, true);
            Wire(settingsPanel, "Timer", ToggleTimer, true);
            Wire(settingsPanel, "Sound", ToggleMute, true);
            Wire(settingsPanel, "Back", CloseSettings, true);
            Wire(victoryPanel, "Next Level", NextLevel, true);
            Wire(victoryPanel, "Replay", RestartLevel, true);
            Wire(victoryPanel, "Levels", ShowLevels, true);
            Wire(defeatPanel, "Undo", UndoMove, true);
            Wire(defeatPanel, "Restart", RestartLevel, true);
            Wire(defeatPanel, "Levels", ShowLevels, true);
            BindSlider(musicSlider, "dm.music", 0.35f, delegate (float value)
            {
                PlayerPrefs.SetFloat("dm.music", value);
                if (audio != null)
                    audio.SetMusicVolume(value);
            });
            BindSlider(effectsSlider, "dm.sfx", 0.8f, delegate (float value)
            {
                PlayerPrefs.SetFloat("dm.sfx", value);
                if (audio != null)
                    audio.SetSfxVolume(value);
            });
        }

        void Wire(GameObject root, string objectName, UnityEngine.Events.UnityAction action, bool clickSound)
        {
            Button button = FindNamedButton(root == null ? null : root.transform, objectName);
            if (button == null)
                return;
            button.onClick.AddListener(delegate
            {
                if (clickSound)
                    PlayClick();
                action();
            });
        }

        static Button FindNamedButton(Transform root, string objectName)
        {
            if (root == null)
                return null;
            if (root.name == objectName)
            {
                Button button = root.GetComponent<Button>();
                if (button != null)
                    return button;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Button found = FindNamedButton(root.GetChild(i), objectName);
                if (found != null)
                    return found;
            }

            return null;
        }

        void BindSlider(Slider slider, string prefsKey, float fallback, UnityEngine.Events.UnityAction<float> changed)
        {
            if (slider == null)
                return;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(PlayerPrefs.GetFloat(prefsKey, fallback));
            slider.onValueChanged.AddListener(changed);
        }

        void BuildInterface()
        {
            GameObject canvasObject = new GameObject("PuzzleCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            background = CreateImage("Background", canvasObject.transform, Color.white);
            Stretch(background.rectTransform);
            background.raycastTarget = true;
            if (content != null && content.menuBackground != null)
                background.sprite = content.menuBackground;
            else
                background.color = Deep;

            menuPanel = CreatePanel(canvasObject.transform, "Menu");
            BuildMenu(menuPanel.transform);
            howPanel = CreatePanel(canvasObject.transform, "HowTo");
            BuildHowTo(howPanel.transform);
            levelPanel = CreatePanel(canvasObject.transform, "Levels");
            BuildLevelSelect(levelPanel.transform);
            playPanel = CreatePanel(canvasObject.transform, "Play");
            BuildPlay(playPanel.transform);
            pausePanel = CreateOverlay(canvasObject.transform, "Pause", out RectTransform pauseCard);
            BuildPause(pauseCard);
            settingsPanel = CreateOverlay(canvasObject.transform, "Settings", out RectTransform settingsCard);
            BuildSettings(settingsCard);
            victoryPanel = CreateOverlay(canvasObject.transform, "Victory", out RectTransform victoryCard);
            BuildVictory(victoryCard);
            defeatPanel = CreateOverlay(canvasObject.transform, "Defeat", out RectTransform defeatCard);
            BuildDefeat(defeatCard);
        }

        void BuildMenu(Transform parent)
        {
            VerticalGroup(parent, 18f, new RectOffset(80, 80, 80, 80));
            TextMeshProUGUI title = MakeText(parent, "Dreamy Mosaics", 78, Cream, FontStyles.Bold, TextAlignmentOptions.Center);
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 100f;
            string subtitle = levels.Length == 0
                ? "The puzzle campaign did not load from Resources/Puzzles/campaign."
                : "Place one memory in every row, column, and region.";
            TextMeshProUGUI subtitleLabel = MakeText(parent, subtitle, 30, Cream, FontStyles.Normal, TextAlignmentOptions.Center);
            subtitleLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;
            AddSpace(parent, 20f);
            MenuButton(parent, "Play", ShowContinue);
            MenuButton(parent, "Levels", ShowLevels);
            MenuButton(parent, "How to Play", ShowHowTo);
            MenuButton(parent, "Settings", delegate { OpenSettings(AppScreen.Menu); });
            MenuButton(parent, "Quit", QuitGame);
        }

        void BuildHowTo(Transform parent)
        {
            VerticalGroup(parent, 12f, new RectOffset(70, 70, 40, 40));
            TextMeshProUGUI title = MakeText(parent, "How to Play", 58, Cream, FontStyles.Bold, TextAlignmentOptions.Center);
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 80f;
            TextMeshProUGUI body = MakeText(parent, HowToText, 28, Cream, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            LayoutElement bodyLayout = body.gameObject.AddComponent<LayoutElement>();
            bodyLayout.flexibleHeight = 1f;
            bodyLayout.minHeight = 200f;
            MenuButton(parent, "Back", ShowMenu);
        }

        void BuildLevelSelect(Transform parent)
        {
            VerticalGroup(parent, 12f, new RectOffset(40, 40, 30, 30));
            TextMeshProUGUI title = MakeText(parent, "Levels", 58, Cream, FontStyles.Bold, TextAlignmentOptions.Center);
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 76f;

            GameObject scrollObject = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollObject.transform.SetParent(parent, false);
            scrollObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.18f);
            LayoutElement scrollLayout = scrollObject.AddComponent<LayoutElement>();
            scrollLayout.flexibleHeight = 1f;
            scrollLayout.minHeight = 200f;

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(scrollObject.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);

            GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 0f);
            VerticalLayoutGroup group = content.GetComponent<VerticalLayoutGroup>();
            group.spacing = 10f;
            group.padding = new RectOffset(12, 12, 12, 12);
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            levelContent = contentRect;

            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            MenuButton(parent, "Back", ShowMenu);
        }

        void BuildPlay(Transform parent)
        {
            VerticalLayoutGroup column = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(24, 24, 16, 16);
            column.spacing = 10f;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            GameObject header = new GameObject("Header", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            header.transform.SetParent(parent, false);
            HorizontalLayoutGroup headerGroup = header.GetComponent<HorizontalLayoutGroup>();
            headerGroup.spacing = 16f;
            headerGroup.childAlignment = TextAnchor.MiddleLeft;
            headerGroup.childControlWidth = true;
            headerGroup.childControlHeight = true;
            headerGroup.childForceExpandWidth = false;
            headerGroup.childForceExpandHeight = true;
            header.AddComponent<LayoutElement>().preferredHeight = 78f;

            titleLabel = MakeText(header.transform, "Level", 34, Cream, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            titleLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            heartRow = new GameObject("Hearts", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            heartRow.transform.SetParent(header.transform, false);
            HorizontalLayoutGroup heartsGroup = heartRow.GetComponent<HorizontalLayoutGroup>();
            heartsGroup.spacing = 6f;
            heartsGroup.childAlignment = TextAnchor.MiddleCenter;
            heartRow.AddComponent<LayoutElement>().preferredWidth = 150f;
            heartImages = new Image[3];
            for (int i = 0; i < heartImages.Length; i++)
            {
                Image heart = CreateImage("Heart", heartRow.transform, Color.white);
                heart.sprite = heartSprite;
                heart.preserveAspect = true;
                LayoutElement heartLayout = heart.gameObject.AddComponent<LayoutElement>();
                heartLayout.preferredWidth = 42f;
                heartLayout.preferredHeight = 42f;
                heartImages[i] = heart;
            }

            practiceLabel = MakeText(header.transform, "Practice", 28, Gold, FontStyles.Bold, TextAlignmentOptions.Center).gameObject;
            practiceLabel.AddComponent<LayoutElement>().preferredWidth = 150f;
            movesLabel = MakeText(header.transform, "Moves 0", 28, Cream, FontStyles.Normal, TextAlignmentOptions.Center);
            movesLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 160f;
            timerLabel = MakeText(header.transform, "00:00", 28, Cream, FontStyles.Normal, TextAlignmentOptions.Center);
            timerLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 120f;
            Button pause = MakeButton(header.transform, "Pause", ShowPause, RoseDark, false);
            pause.gameObject.AddComponent<LayoutElement>().preferredWidth = 210f;

            GameObject middle = new GameObject("Middle", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            middle.transform.SetParent(parent, false);
            HorizontalLayoutGroup middleGroup = middle.GetComponent<HorizontalLayoutGroup>();
            middleGroup.spacing = 16f;
            middleGroup.childControlWidth = true;
            middleGroup.childControlHeight = true;
            middleGroup.childForceExpandHeight = true;
            middleGroup.childForceExpandWidth = false;
            middle.AddComponent<LayoutElement>().flexibleHeight = 1f;

            GameObject boardObject = new GameObject("Board", typeof(RectTransform));
            boardObject.transform.SetParent(middle.transform, false);
            boardArea = boardObject.GetComponent<RectTransform>();
            boardObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            GameObject gridObject = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridObject.transform.SetParent(boardObject.transform, false);
            gridRect = gridObject.GetComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0.5f, 0.5f);
            gridRect.anchorMax = new Vector2(0.5f, 0.5f);
            gridRect.pivot = new Vector2(0.5f, 0.5f);
            GridLayoutGroup grid = gridObject.GetComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.MiddleCenter;

            GameObject side = new GameObject("Side", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(Image));
            side.transform.SetParent(middle.transform, false);
            side.GetComponent<Image>().color = new Color(0.1f, 0.05f, 0.08f, 0.45f);
            VerticalLayoutGroup sideGroup = side.GetComponent<VerticalLayoutGroup>();
            sideGroup.padding = new RectOffset(16, 16, 16, 16);
            sideGroup.spacing = 8f;
            sideGroup.childControlWidth = true;
            sideGroup.childControlHeight = true;
            sideGroup.childForceExpandWidth = true;
            sideGroup.childForceExpandHeight = false;
            LayoutElement sideLayout = side.AddComponent<LayoutElement>();
            sideLayout.preferredWidth = 360f;
            sideLayout.minWidth = 280f;
            infoLabel = MakeText(side.transform, "", 24, Cream, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            infoLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 150f;
            SideButton(side.transform, "Undo", UndoMove);
            Button hint = SideButton(side.transform, "Hint", UseHint);
            hintButtonLabel = hint.GetComponentInChildren<TextMeshProUGUI>();
            SideButton(side.transform, "Restart", RestartLevel);

            messageLabel = MakeText(parent, "", 26, Cream, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            messageLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 92f;

            GameObject tutorial = new GameObject("Tutorial", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            tutorial.transform.SetParent(parent, false);
            tutorial.GetComponent<Image>().color = new Color(0.12f, 0.07f, 0.1f, 0.72f);
            VerticalLayoutGroup tutorialGroup = tutorial.GetComponent<VerticalLayoutGroup>();
            tutorialGroup.padding = new RectOffset(16, 16, 12, 12);
            tutorialGroup.spacing = 8f;
            tutorialGroup.childControlWidth = true;
            tutorialGroup.childControlHeight = true;
            tutorialGroup.childForceExpandWidth = true;
            tutorial.AddComponent<LayoutElement>().preferredHeight = 150f;
            tutorialLabel = MakeText(tutorial.transform, "", 26, Cream, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            tutorialLabel.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            tutorialContinue = MakeButton(tutorial.transform, "Continue", ContinueTutorial, Rose, true);
            tutorialContinue.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;
            tutorial.SetActive(false);
        }

        void BuildPause(RectTransform card)
        {
            CardTitle(card, "Paused");
            CardButton(card, "Resume", Resume);
            CardButton(card, "Restart", RestartLevel);
            CardButton(card, "Settings", delegate { OpenSettings(AppScreen.Pause); });
            CardButton(card, "Levels", ShowLevels);
            CardButton(card, "Main Menu", ReturnToStart);
        }

        void BuildSettings(RectTransform card)
        {
            CardTitle(card, "Settings");
            DefaultControls.Resources skin = Skin();
            if (skin.standard != null)
            {
                CreateSlider(card, "Music", PlayerPrefs.GetFloat("dm.music", 0.35f), delegate (float value)
                {
                    PlayerPrefs.SetFloat("dm.music", value);
                    if (audio != null)
                        audio.SetMusicVolume(value);
                });
                CreateSlider(card, "Effects", PlayerPrefs.GetFloat("dm.sfx", 0.8f), delegate (float value)
                {
                    PlayerPrefs.SetFloat("dm.sfx", value);
                    if (audio != null)
                        audio.SetSfxVolume(value);
                });
            }

            CardButton(card, showTimer ? "Timer: On" : "Timer: Off", ToggleTimer);
            CardButton(card, muted ? "Sound: Off" : "Sound: On", ToggleMute);
            CardButton(card, "Back", CloseSettings);
        }

        void BuildVictory(RectTransform card)
        {
            CardTitle(card, "Solved");
            victoryBody = MakeText(card, "", 28, Ink, FontStyles.Normal, TextAlignmentOptions.Center);
            victoryBody.gameObject.AddComponent<LayoutElement>().preferredHeight = 180f;
            CardButton(card, "Next Level", NextLevel);
            CardButton(card, "Replay", RestartLevel);
            CardButton(card, "Levels", ShowLevels);
        }

        void BuildDefeat(RectTransform card)
        {
            CardTitle(card, "Out of Hearts");
            defeatBody = MakeText(card, "Undo the last mistake to take a heart back, or restart the puzzle.", 28, Ink, FontStyles.Normal, TextAlignmentOptions.Center);
            defeatBody.gameObject.AddComponent<LayoutElement>().preferredHeight = 120f;
            CardButton(card, "Undo", UndoMove);
            CardButton(card, "Restart", RestartLevel);
            CardButton(card, "Levels", ShowLevels);
        }

        void ShowMenu()
        {
            screen = AppScreen.Menu;
            SetBackground(false);
            SetPanels(menuPanel);
        }

        void ShowHowTo()
        {
            screen = AppScreen.HowTo;
            SetPanels(howPanel);
        }

        void ShowLevels()
        {
            screen = AppScreen.Levels;
            SetBackground(false);
            RebuildLevelButtons();
            SetPanels(levelPanel);
        }

        void ShowContinue()
        {
            int unlocked = ProgressionStore.UnlockedCount(levels.Length);
            int next = 0;
            for (int i = 0; i < unlocked && i < levels.Length; i++)
            {
                if (ProgressionStore.Stars(i) == 0 && solutions[i] != null)
                {
                    next = i;
                    break;
                }

                next = i;
            }

            BeginLevel(next);
        }

        void ShowPause()
        {
            if (session == null)
                return;
            screen = AppScreen.Pause;
            pausePanel.SetActive(true);
        }

        void Resume()
        {
            if (session == null)
            {
                ShowMenu();
                return;
            }

            screen = AppScreen.Play;
            pausePanel.SetActive(false);
            settingsPanel.SetActive(false);
        }

        void OpenSettings(AppScreen back)
        {
            settingsReturn = back;
            screen = AppScreen.Settings;
            settingsPanel.SetActive(true);
            RefreshSettingsLabels();
        }

        void CloseSettings()
        {
            PlayerPrefs.Save();
            settingsPanel.SetActive(false);
            if (settingsReturn == AppScreen.Pause)
                ShowPause();
            else
                ShowMenu();
        }

        void BeginLevel(int index)
        {
            if (index < 0 || index >= levels.Length)
                return;
            if (solutions[index] == null)
            {
                ShowLevels();
                return;
            }

            levelIndex = index;
            session = new PlaySession(levels[index], solutions[index]);
            selected = -1;
            guideCell = -1;
            stepIndex = 0;
            elapsed = 0f;
            lastBoardSize = Vector2.zero;
            SetBackground(true);
            if (boardArea != null)
            {
                Transform boardLabel = boardArea.Find("Board Label");
                if (boardLabel != null)
                    boardLabel.gameObject.SetActive(false);
            }

            BuildCells();
            SetPanels(playPanel);
            screen = AppScreen.Play;
            SetMessage(levels[index].intro, Cream);
            RefreshAll();
            Canvas.ForceUpdateCanvases();
            LayoutGrid();
        }

        void BuildCells()
        {
            cells.Clear();
            if (gridRect != null)
            {
                for (int i = gridRect.childCount - 1; i >= 0; i--)
                    Destroy(gridRect.GetChild(i).gameObject);
            }
            int count = session.Size * session.Size;
            for (int i = 0; i < count; i++)
            {
                GameObject cellObject = new GameObject("Cell " + i, typeof(RectTransform), typeof(Image), typeof(CellView));
                cellObject.transform.SetParent(gridRect, false);
                CellView view = cellObject.GetComponent<CellView>();
                view.Index = i;
                view.Frame = cellObject.GetComponent<Image>();
                view.Frame.sprite = rounded;
                view.Frame.type = Image.Type.Sliced;
                view.Clicked = OnCellClicked;

                Image fill = CreateImage("Fill", cellObject.transform, Color.white);
                fill.sprite = rounded;
                fill.type = Image.Type.Sliced;
                fill.raycastTarget = false;
                AnchorInset(fill.rectTransform, 5f);
                view.Fill = fill;

                Image wash = CreateImage("Wash", cellObject.transform, new Color(1f, 1f, 1f, 0f));
                wash.raycastTarget = false;
                AnchorInset(wash.rectTransform, 5f);
                view.Wash = wash;

                Image cat = CreateImage("Cat", cellObject.transform, Color.white);
                cat.sprite = catSprite;
                cat.preserveAspect = true;
                cat.raycastTarget = false;
                cat.enabled = false;
                AnchorInset(cat.rectTransform, 6f);
                view.CatImage = cat;

                TextMeshProUGUI mark = MakeText(cellObject.transform, "", 32, Ink, FontStyles.Bold, TextAlignmentOptions.Center);
                mark.raycastTarget = false;
                Stretch(mark.rectTransform);
                view.Mark = mark;
                cells.Add(view);
            }
        }

        void OnCellClicked(CellView view, PointerEventData eventData)
        {
            if (screen != AppScreen.Play || session == null)
                return;
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                selected = view.Index;
                Handle(session.ToggleMark(view.Index), true);
                return;
            }

            selected = view.Index;
            Handle(session.PlaceCat(view.Index), true);
        }

        void UndoMove()
        {
            if (session == null)
                return;
            if (screen != AppScreen.Play && screen != AppScreen.Defeat)
                return;
            ActionResult result = session.Undo();
            Handle(result, false);
            if (screen == AppScreen.Defeat && session.Hearts > 0)
                Resume();
        }

        void UseHint()
        {
            if (!CanAct())
                return;
            ActionResult result = session.Hint();
            Handle(result, false);
            if (result.HighlightCell >= 0)
            {
                guideCell = result.HighlightCell;
                selected = result.HighlightCell;
                RefreshCells();
            }
        }

        void RestartLevel()
        {
            if (session == null)
                return;
            session.Restart();
            selected = -1;
            guideCell = -1;
            stepIndex = 0;
            elapsed = 0f;
            screen = AppScreen.Play;
            pausePanel.SetActive(false);
            victoryPanel.SetActive(false);
            defeatPanel.SetActive(false);
            settingsPanel.SetActive(false);
            SetMessage(session.Level.intro, Cream);
            RefreshAll();
        }

        void ContinueTutorial()
        {
            if (session == null || session.Level.steps == null || stepIndex >= session.Level.steps.Length)
                return;
            if (session.Level.steps[stepIndex].waitFor == "continue")
            {
                stepIndex++;
                PlayClick();
                RefreshAll();
            }
        }

        void NextLevel()
        {
            int next = levelIndex + 1;
            if (next < levels.Length && next < ProgressionStore.UnlockedCount(levels.Length) && solutions[next] != null)
                BeginLevel(next);
            else
                ShowLevels();
        }

        void Handle(ActionResult result, bool sound)
        {
            if (sound)
                guideCell = -1;
            if (!string.IsNullOrEmpty(result.Message))
                SetMessage(result.Message, result.Applied && !result.Mistake ? Cream : Warn);
            if (result.HighlightCell >= 0)
                selected = result.HighlightCell;
            AdvanceGuidedSteps();
            RefreshAll();
            if (sound && audio != null)
            {
                if (result.Won || (result.Applied && !result.Mistake))
                    audio.PlayRightSFX();
                else
                    audio.PlayWrongSFX();
            }

            if (result.Won)
                ShowVictory();
            else if (result.Lost)
                ShowDefeat();
        }

        void ShowVictory()
        {
            screen = AppScreen.Victory;
            int stars = ProgressionStore.Rate(session.Mistakes, session.HintsUsed);
            ProgressionStore.NoteWin(levelIndex, levels.Length, stars, elapsed);
            string timeLine = showTimer ? "Time " + QueensRules.FormatClock(elapsed) + "\n" : "";
            victoryBody.text = session.Level.name + "\n" + timeLine
                + "Moves " + session.Moves + "\n"
                + "Mistakes " + session.Mistakes + "\n"
                + "Hints " + session.HintsUsed + "\n"
                + StarText(stars);
            victoryPanel.SetActive(true);
        }

        void ShowDefeat()
        {
            screen = AppScreen.Defeat;
            defeatBody.text = "That mistake spent the last heart. Undo puts the heart back. Restart clears the board.";
            defeatPanel.SetActive(true);
        }

        void RefreshAll()
        {
            if (session == null)
                return;
            titleLabel.text = (levelIndex + 1) + ". " + session.Level.name + "  " + session.Size + "x" + session.Size + "  " + QueensRules.DifficultyLabel(session.Level);
            movesLabel.text = "Moves " + session.Moves;
            bool practice = !session.HeartsEnabled;
            heartRow.SetActive(!practice);
            practiceLabel.SetActive(practice);
            if (!practice)
            {
                for (int i = 0; i < heartImages.Length; i++)
                    heartImages[i].color = i < session.Hearts ? Color.white : new Color(1f, 1f, 1f, 0.22f);
            }

            if (hintButtonLabel != null)
                hintButtonLabel.text = "Hint " + session.HintsUsed + "/" + session.HintLimit;
            if (selected >= 0)
                infoLabel.text = session.Describe(selected);
            else
                infoLabel.text = session.Level.intro + "\n\nOne memory in every row, column, and region. Memories cannot touch.";
            RefreshTutorial();
            RefreshCells();
        }

        void RefreshTutorial()
        {
            Transform tutorial = tutorialLabel.transform.parent;
            bool show = session.Level.tutorial == 1 && session.Level.steps != null && stepIndex < session.Level.steps.Length && screen == AppScreen.Play;
            tutorial.gameObject.SetActive(show);
            if (!show)
                return;
            TutorialStep step = session.Level.steps[stepIndex];
            tutorialLabel.text = step.text;
            tutorialContinue.gameObject.SetActive(step.waitFor == "continue");
            guideCell = step.cell;
        }

        void RefreshCells()
        {
            int n = session.Size;
            for (int i = 0; i < cells.Count; i++)
            {
                int region = session.Level.regions[i];
                Color baseColor = RegionColors[region % RegionColors.Length];
                bool cat = session.Cats[i];
                bool eliminated = !cat && QueensRules.PlacementBlockReason(n, session.Level.regions, session.Cats, null, i) != null;
                Color fill = eliminated ? Color.Lerp(baseColor, Deep, 0.62f) : baseColor;
                Color frame = Color.Lerp(baseColor, Ink, 0.45f);
                if (i == selected)
                    frame = Gold;
                else if (i == guideCell)
                    frame = Guide;
                if (session.WrongCats[i] || session.WrongMarks[i])
                    frame = new Color(0.82f, 0.18f, 0.24f, 1f);

                Color wash = WashFor(i);
                string mark = "";
                Color markColor = Ink;
                if (session.Marks[i])
                {
                    mark = "X";
                    markColor = session.WrongMarks[i] ? new Color(0.75f, 0.12f, 0.16f, 1f) : Ink;
                }
                else if (eliminated)
                {
                    mark = "x";
                    markColor = new Color(0.15f, 0.08f, 0.1f, 0.4f);
                }

                Color catTint = session.WrongCats[i] ? new Color(1f, 0.45f, 0.45f, 1f) : Color.white;
                float markSize = gridRect != null && session.Size > 0 ? Mathf.Max(18f, gridRect.sizeDelta.x / session.Size * 0.38f) : 28f;
                cells[i].Apply(frame, fill, wash, cat, catTint, mark, markColor, markSize);
            }
        }

        Color WashFor(int cell)
        {
            if (selected < 0 || session == null)
                return new Color(1f, 1f, 1f, 0f);
            if (cell == selected)
                return new Color(1f, 0.9f, 0.55f, 0.18f);
            int n = session.Size;
            int row = QueensRules.RowOf(n, cell);
            int col = QueensRules.ColOf(n, cell);
            int selectedRow = QueensRules.RowOf(n, selected);
            int selectedCol = QueensRules.ColOf(n, selected);
            bool neighbor = Mathf.Abs(row - selectedRow) <= 1 && Mathf.Abs(col - selectedCol) <= 1;
            if (neighbor && !session.Cats[selected])
                return new Color(0.7f, 0.22f, 0.28f, 0.28f);
            if (session.Level.regions[cell] == session.Level.regions[selected])
                return new Color(1f, 1f, 1f, 0.2f);
            if (row == selectedRow || col == selectedCol)
                return new Color(1f, 1f, 1f, 0.12f);
            return new Color(1f, 1f, 1f, 0f);
        }

        void LayoutGrid()
        {
            if (session == null || gridRect == null || boardArea == null)
                return;
            int n = session.Size;
            Canvas.ForceUpdateCanvases();
            Rect area = boardArea.rect;
            if (area.width < 20f || area.height < 20f)
                return;
            float spacing = Mathf.Clamp(Mathf.Min(area.width, area.height) * 0.012f, 3f, 8f);
            float side = Mathf.Min(area.width, area.height) - 8f;
            float cell = (side - spacing * (n - 1)) / n;
            if (cell < 12f)
                cell = 12f;
            float total = n * cell + (n - 1) * spacing;
            gridRect.sizeDelta = new Vector2(total, total);
            GridLayoutGroup grid = gridRect.GetComponent<GridLayoutGroup>();
            grid.constraintCount = n;
            grid.cellSize = new Vector2(cell, cell);
            grid.spacing = new Vector2(spacing, spacing);
            RefreshCells();
        }

        void AdvanceGuidedSteps()
        {
            if (session == null || session.Level.tutorial != 1 || session.Level.steps == null)
                return;
            while (stepIndex < session.Level.steps.Length)
            {
                TutorialStep step = session.Level.steps[stepIndex];
                bool done = false;
                if (step.waitFor == "place" && step.cell >= 0 && step.cell < session.Cats.Length && session.Cats[step.cell])
                    done = true;
                else if (step.waitFor == "mark" && step.cell >= 0 && step.cell < session.Marks.Length && session.Marks[step.cell])
                    done = true;
                else if (step.waitFor == "complete" && session.IsSolved())
                    done = true;
                if (!done)
                    break;
                stepIndex++;
            }
        }

        void RebuildLevelButtons()
        {
            for (int i = levelContent.childCount - 1; i >= 0; i--)
                Destroy(levelContent.GetChild(i).gameObject);

            int unlocked = ProgressionStore.UnlockedCount(levels.Length);
            for (int i = 0; i < levels.Length; i++)
            {
                int index = i;
                LevelData level = levels[i];
                bool verified = solutions[i] != null;
                bool open = verified && i < unlocked;
                string status = !verified ? "Unverified" : !open ? "Locked" : ProgressionStore.Stars(i) == 0 ? "New" : StarText(ProgressionStore.Stars(i));
                string label = (i + 1) + "  " + level.name + "    " + level.size + "x" + level.size + "  " + QueensRules.DifficultyLabel(level) + "    " + status;
                Button button = MakeButton(levelContent, label, delegate { BeginLevel(index); }, open ? Rose : RoseDark, true);
                button.interactable = open;
                button.gameObject.AddComponent<LayoutElement>().preferredHeight = 72f;
            }
        }

        void ReadKeys()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (screen == AppScreen.Play)
                    ShowPause();
                else if (screen == AppScreen.Pause || screen == AppScreen.Settings)
                    Resume();
                return;
            }

            if (screen != AppScreen.Play || session == null)
                return;

            if (Input.GetKeyDown(KeyCode.Z))
                UndoMove();
            else if (Input.GetKeyDown(KeyCode.H))
                UseHint();
            else if (Input.GetKeyDown(KeyCode.R))
                RestartLevel();
        }

        void ToggleTimer()
        {
            showTimer = !showTimer;
            PlayerPrefs.SetInt("dm.timer", showTimer ? 1 : 0);
            RefreshSettingsLabels();
        }

        void ToggleMute()
        {
            muted = !muted;
            PlayerPrefs.SetInt("dm.mute", muted ? 1 : 0);
            if (audio != null)
                audio.SetMuted(muted);
            RefreshSettingsLabels();
        }

        void RefreshSettingsLabels()
        {
            Button[] buttons = settingsPanel.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                TextMeshProUGUI label = buttons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (label == null)
                    continue;
                if (label.text.StartsWith("Timer"))
                    label.text = showTimer ? "Timer: On" : "Timer: Off";
                if (label.text.StartsWith("Sound"))
                    label.text = muted ? "Sound: Off" : "Sound: On";
            }
        }

        bool CanAct()
        {
            return screen == AppScreen.Play && session != null;
        }

        void SetMessage(string message, Color color)
        {
            if (messageLabel == null)
                return;
            messageLabel.text = message;
            messageLabel.color = color;
        }

        void SetBackground(bool playing)
        {
            if (background == null)
                return;
            Sprite sprite = null;
            if (content != null)
                sprite = playing ? content.playBackground : content.menuBackground;
            background.sprite = sprite;
            background.color = sprite == null ? Deep : Color.white;
        }

        void SetPanels(GameObject active)
        {
            menuPanel.SetActive(active == menuPanel);
            howPanel.SetActive(active == howPanel);
            levelPanel.SetActive(active == levelPanel);
            playPanel.SetActive(active == playPanel);
            pausePanel.SetActive(false);
            settingsPanel.SetActive(false);
            victoryPanel.SetActive(false);
            defeatPanel.SetActive(false);
        }

        void QuitGame()
        {
            PlayClick();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void PlayClick()
        {
            if (audio != null)
                audio.PlayBtnGeneralSFX();
        }

        static string StarText(int stars)
        {
            if (stars <= 0)
                return "New";
            if (stars == 1)
                return "1 star";
            return stars + " stars";
        }

        void MenuButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            Button button = MakeButton(parent, label, action, Rose, true);
            LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 74f;
            layout.minHeight = 64f;
        }

        Button SideButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            Button button = MakeButton(parent, label, action, Rose, false);
            button.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;
            return button;
        }

        void CardTitle(RectTransform card, string title)
        {
            VerticalLayoutGroup group = card.gameObject.AddComponent<VerticalLayoutGroup>();
            group.padding = new RectOffset(28, 28, 24, 24);
            group.spacing = 12f;
            group.childAlignment = TextAnchor.UpperCenter;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            TextMeshProUGUI label = MakeText(card, title, 48, Ink, FontStyles.Bold, TextAlignmentOptions.Center);
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;
        }

        void CardButton(RectTransform card, string label, UnityEngine.Events.UnityAction action)
        {
            Button button = MakeButton(card, label, action, Rose, true);
            button.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;
        }

        void CreateSlider(RectTransform card, string label, float value, UnityEngine.Events.UnityAction<float> changed)
        {
            GameObject row = new GameObject(label, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(card, false);
            HorizontalLayoutGroup group = row.GetComponent<HorizontalLayoutGroup>();
            group.spacing = 12f;
            group.childAlignment = TextAnchor.MiddleCenter;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandHeight = true;
            row.AddComponent<LayoutElement>().preferredHeight = 48f;
            TextMeshProUGUI caption = MakeText(row.transform, label, 26, Ink, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            caption.gameObject.AddComponent<LayoutElement>().preferredWidth = 120f;
            GameObject sliderObject = DefaultControls.CreateSlider(Skin());
            sliderObject.transform.SetParent(row.transform, false);
            sliderObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Slider slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value;
            slider.onValueChanged.AddListener(changed);
        }

        void ReturnToStart()
        {
            SceneManager.LoadScene("Game Start");
        }

        Button MakeButton(Transform parent, string label, UnityEngine.Events.UnityAction action, Color color, bool clickSound)
        {
            GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            Image image = buttonObject.GetComponent<Image>();
            Sprite face = ButtonFace(label, out bool painted);
            image.sprite = face;
            image.type = face == rounded ? Image.Type.Sliced : Image.Type.Simple;
            image.color = face == rounded || face == null ? color : Color.white;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.38f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(delegate
            {
                if (clickSound)
                    PlayClick();
                action();
            });
            if (!painted)
            {
                TextMeshProUGUI text = MakeText(buttonObject.transform, label, label.Length > 22 ? 20f : 26f, ButtonInk, FontStyles.Bold, TextAlignmentOptions.Center);
                RectTransform rect = text.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(16f, 12f);
                rect.offsetMax = new Vector2(-16f, -12f);
            }

            return button;
        }

        Sprite ButtonFace(string label, out bool painted)
        {
            painted = false;
            if (content == null)
                return rounded;
            Sprite labeled = null;
            if (label == "Pause")
                labeled = content.pauseButton;
            else if (label == "Restart" || label == "Replay")
                labeled = content.resetButton;
            else if (label == "Main Menu")
                labeled = content.menuButton;
            else if (label == "Next Level")
                labeled = content.nextButton;
            else if (label == "Back")
                labeled = content.backButton;
            else if (label == "Resume")
                labeled = content.resumeButton;
            else if (label == "Quit")
                labeled = content.quitButton;
            if (labeled != null)
            {
                painted = true;
                return labeled;
            }

            if (content.buttonFace != null)
                return content.buttonFace;
            return rounded;
        }

        TextMeshProUGUI MakeText(Transform parent, string value, float size, Color color, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        GameObject CreatePanel(Transform parent, string name)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            Stretch(panel.GetComponent<RectTransform>());
            panel.SetActive(false);
            return panel;
        }

        GameObject CreateOverlay(Transform parent, string name, out RectTransform card)
        {
            GameObject overlay = new GameObject(name, typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(parent, false);
            Stretch(overlay.GetComponent<RectTransform>());
            overlay.GetComponent<Image>().color = new Color(0.05f, 0.02f, 0.04f, 0.72f);
            GameObject cardObject = new GameObject("Card", typeof(RectTransform), typeof(Image));
            cardObject.transform.SetParent(overlay.transform, false);
            card = cardObject.GetComponent<RectTransform>();
            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(640f, 720f);
            Image cardImage = cardObject.GetComponent<Image>();
            cardImage.color = Cream;
            if (rounded != null)
            {
                cardImage.sprite = rounded;
                cardImage.type = Image.Type.Sliced;
            }

            overlay.SetActive(false);
            return overlay;
        }

        void VerticalGroup(Transform parent, float spacing, RectOffset padding)
        {
            VerticalLayoutGroup group = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            group.padding = padding;
            group.childAlignment = TextAnchor.UpperCenter;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
        }

        void AddSpace(Transform parent, float height)
        {
            GameObject space = new GameObject("Space", typeof(RectTransform));
            space.transform.SetParent(parent, false);
            space.AddComponent<LayoutElement>().preferredHeight = height;
        }

        DefaultControls.Resources Skin()
        {
            return new DefaultControls.Resources
            {
                standard = rounded,
                background = rounded,
                inputField = rounded,
                knob = rounded,
                checkmark = rounded,
                dropdown = rounded,
                mask = rounded
            };
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void AnchorInset(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
