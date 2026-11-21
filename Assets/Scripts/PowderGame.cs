using System.IO;
using UnityEngine;

namespace PowderMan
{
    /// <summary>
    /// Runs the simulation, draws it to a texture, handles mouse/touch/keyboard and the toolbar.
    ///
    /// Controls: left mouse / touch = draw, right mouse = erase, mouse wheel or [ ] = brush size,
    /// Space = pause, . = single step while paused, R = replace mode, C = clear, 1-9 = pick element,
    /// F5 = save, F9 = load, Esc / Android back = home menu (quits from the menu).
    ///
    /// The home menu runs a little demo in the world behind it until you press Play.
    /// </summary>
    public class PowderGame : MonoBehaviour
    {
        [Header("World")]
        [SerializeField] int gridWidth = 320;
        [SerializeField] int gridHeight = 180;
        [Tooltip("Simulation ticks per second at speed x1.")]
        [SerializeField] float ticksPerSecond = 60f;

        [Header("Brush")]
        [SerializeField] int brushSize = 3;
        [SerializeField] int maxBrushSize = 30;

        static readonly float[] Speeds = { 0.25f, 0.5f, 1f, 2f, 4f };

        Simulation sim;
        Texture2D texture;
        Color32[] pixels;
        Color32[] palette;
        Camera cam;

        byte selected = El.Sand;
        bool paused, replaceMode, drawing;
        int speedIndex = 2;
        float tickTimer;
        Vector2Int lastCell;
        int particleCount;
        float fps;
        string message;
        float messageUntil;

        // Home menu
        bool inMenu = true;
        bool started;           // false until the first Play, while the demo runs behind the menu
        float demoSwapAt;
        readonly byte[] demoSpouts = new byte[3];
        static readonly byte[] DemoElements = { El.Sand, El.Water, El.Oil, El.Snow, El.Lava, El.Gunpowder, El.Acid, El.Fire };

        string SavePath => Path.Combine(Application.persistentDataPath, "sandbox.sav");

        void Start()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            sim = new Simulation(gridWidth, gridHeight);
            BuildPalette();

            texture = new Texture2D(gridWidth, gridHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            pixels = new Color32[gridWidth * gridHeight];

            var view = new GameObject("World View");
            view.transform.SetParent(transform, false);
            var sr = view.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(texture, new Rect(0, 0, gridWidth, gridHeight), new Vector2(0.5f, 0.5f), 1f);

            cam = Camera.main;
            if (cam == null) cam = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" }.GetComponent<Camera>();
            cam.orthographic = true;
            cam.transform.position = new Vector3(0, 0, -10);
            cam.transform.rotation = Quaternion.identity;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(8, 9, 13, 255);

            StartDemo();
            Render();
        }

        // ------------------------------------------------------------------ home menu

        /// <summary>Stone ledges and a Void floor, with three spouts pouring random elements on them.</summary>
        void StartDemo()
        {
            sim.Clear();
            for (int x = 0; x < gridWidth; x++) sim.Set(x, 0, El.Void);
            int ledge = gridWidth / 4;
            for (int s = 0; s < ledge; s++)
            {
                sim.Paint(gridWidth / 8 + s, gridHeight * 3 / 5 - s / 3, 1, El.Stone, true);
                sim.Paint(gridWidth * 7 / 8 - s, gridHeight * 2 / 5 - s / 3, 1, El.Stone, true);
            }
            demoSwapAt = 0f;
        }

        void UpdateDemo()
        {
            if (Time.time >= demoSwapAt)
            {
                for (int n = 0; n < demoSpouts.Length; n++)
                    demoSpouts[n] = DemoElements[Random.Range(0, DemoElements.Length)];
                demoSwapAt = Time.time + 4f;
            }
            for (int n = 0; n < demoSpouts.Length; n++)
                sim.Paint(gridWidth * (n + 1) / (demoSpouts.Length + 1), gridHeight - 3, 1, demoSpouts[n], false);

            if (particleCount > gridWidth * gridHeight / 2) StartDemo(); // getting clogged, start over
        }

        void Play()
        {
            if (!started)
            {
                sim.Clear();
                started = true;
                paused = false;
            }
            inMenu = false;
            drawing = false;
        }

        void OpenMenu()
        {
            inMenu = true;
            drawing = false;
        }

        void BuildPalette()
        {
            palette = new Color32[256 * 16];
            for (int t = 0; t < 256; t++)
            {
                var e = ElementDatabase.Table[t];
                if (e == null) continue;
                for (int s = 0; s < 16; s++)
                    palette[t * 16 + s] = Color32.Lerp(e.Color, e.Color2, s / 15f);
            }
        }

        // ------------------------------------------------------------------ update

        void Update()
        {
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.05f);
            FitCamera();
            if (inMenu)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) Application.Quit();
                if (!started) UpdateDemo();
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.Escape)) OpenMenu();
                HandleKeys();
                HandlePointer();
            }

            if (!paused || (inMenu && !started))
            {
                float stepTime = 1f / ticksPerSecond;
                tickTimer += Time.deltaTime * Speeds[speedIndex];
                int steps = 0;
                while (tickTimer >= stepTime && steps < 8)
                {
                    sim.Step();
                    tickTimer -= stepTime;
                    steps++;
                }
                if (steps == 8) tickTimer = 0f; // can't keep up, drop the backlog
            }

            Render();
        }

        void Render()
        {
            var cells = sim.Cells;
            int count = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                byte t = cells[i];
                if (t != El.Empty) count++;
                pixels[i] = palette[t * 16 + sim.Shade(i)];
            }
            particleCount = count;
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        /// <summary>Keeps the whole grid visible above the toolbar, whatever the window size.</summary>
        void FitCamera()
        {
            float barFrac = inMenu ? 0f : Mathf.Clamp01(ToolbarHeight() / Screen.height);
            cam.rect = new Rect(0f, barFrac, 1f, 1f - barFrac);
            float aspect = cam.pixelWidth / (float)Mathf.Max(1, cam.pixelHeight);
            cam.orthographicSize = Mathf.Max(gridHeight / 2f, gridWidth / 2f / aspect);
        }

        void HandleKeys()
        {
            if (Input.GetKeyDown(KeyCode.Space)) paused = !paused;
            if (Input.GetKeyDown(KeyCode.Period) && paused) sim.Step();
            if (Input.GetKeyDown(KeyCode.R)) replaceMode = !replaceMode;
            if (Input.GetKeyDown(KeyCode.C)) sim.Clear();
            if (Input.GetKeyDown(KeyCode.LeftBracket)) ChangeBrush(-1);
            if (Input.GetKeyDown(KeyCode.RightBracket)) ChangeBrush(1);
            if (Input.GetKeyDown(KeyCode.F5)) Save();
            if (Input.GetKeyDown(KeyCode.F9)) Load();

            var bar = ElementDatabase.Toolbar;
            for (int n = 0; n < 9 && n < bar.Count; n++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + n)) selected = bar[n];

            float wheel = Input.mouseScrollDelta.y;
            if (wheel > 0.01f) ChangeBrush(1);
            else if (wheel < -0.01f) ChangeBrush(-1);
        }

        void ChangeBrush(int delta) => brushSize = Mathf.Clamp(brushSize + delta, 0, maxBrushSize);

        void HandlePointer()
        {
            bool left = Input.GetMouseButton(0), right = Input.GetMouseButton(1);
            if (!left && !right) { drawing = false; return; }

            Vector2 mouse = Input.mousePosition;
            bool overUi = mouse.y < ToolbarHeight();
            if (!drawing)
            {
                // Only start a stroke when the press begins over the world, not on the toolbar.
                if (overUi || !(Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))) return;
                drawing = true;
                lastCell = MouseCell();
            }

            Vector2Int cell = MouseCell();
            byte type = right ? El.Empty : selected;
            bool replace = replaceMode || type == El.Empty;

            // Paint along the line from the last position so fast strokes don't leave gaps.
            int dx = cell.x - lastCell.x, dy = cell.y - lastCell.y;
            int steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
            for (int s = 0; s <= steps; s++)
            {
                float f = steps == 0 ? 1f : s / (float)steps;
                sim.Paint(Mathf.RoundToInt(lastCell.x + dx * f), Mathf.RoundToInt(lastCell.y + dy * f), brushSize, type, replace);
            }
            lastCell = cell;
        }

        Vector2Int MouseCell()
        {
            Vector3 w = cam.ScreenToWorldPoint(Input.mousePosition);
            return new Vector2Int(Mathf.FloorToInt(w.x + gridWidth / 2f), Mathf.FloorToInt(w.y + gridHeight / 2f));
        }

        void Save()
        {
            try { sim.Save(SavePath); Flash("Saved"); }
            catch (IOException e) { Flash("Save failed: " + e.Message); }
        }

        void Load()
        {
            try { Flash(sim.Load(SavePath) ? "Loaded" : "No save yet"); }
            catch (IOException e) { Flash("Load failed: " + e.Message); }
        }

        void Flash(string text)
        {
            message = text;
            messageUntil = Time.unscaledTime + 2f;
        }

        // ------------------------------------------------------------------ toolbar (IMGUI)

        GUIStyle labelStyle, buttonStyle, infoStyle, titleStyle, menuButtonStyle;

        /// <summary>UI size multiplier. On phones it's based on DPI so buttons stay finger-sized.</summary>
        float UiScale
        {
            get
            {
                float s = Screen.height / 720f;
                if (Application.isMobilePlatform && Screen.dpi > 0f) s = Mathf.Max(s, Screen.dpi / 170f);
                return Mathf.Max(1f, s);
            }
        }
        float Pad => 6f * UiScale;
        float Gap => 4f * UiScale;
        float ButtonW => 86f * UiScale;
        float ButtonH => 30f * UiScale;

        int ElementButtonsPerRow => Mathf.Max(1, (int)((Screen.width - 2 * Pad + Gap) / (ButtonW + Gap)));
        int ElementRows => Mathf.CeilToInt((ElementDatabase.Toolbar.Count + 1) / (float)ElementButtonsPerRow);

        /// <summary>Toolbar height in pixels: element rows plus one row of tool buttons.</summary>
        float ToolbarHeight() => Pad * 2 + (ElementRows + 1) * (ButtonH + Gap) - Gap;

        void OnGUI()
        {
            if (sim == null) return;
            MakeStyles();
            if (inMenu) { DrawMenu(); return; }

            float barH = ToolbarHeight();
            float top = Screen.height - barH;
            Fill(new Rect(0, top, Screen.width, barH), new Color32(24, 26, 36, 255));
            Fill(new Rect(0, top, Screen.width, Mathf.Max(1f, UiScale)), new Color32(60, 64, 84, 255));

            // Element buttons (plus the eraser at the end).
            var bar = ElementDatabase.Toolbar;
            int perRow = ElementButtonsPerRow;
            for (int n = 0; n <= bar.Count; n++)
            {
                byte id = n < bar.Count ? bar[n] : El.Empty;
                var e = ElementDatabase.Table[id];
                var r = new Rect(Pad + (n % perRow) * (ButtonW + Gap), top + Pad + (n / perRow) * (ButtonH + Gap), ButtonW, ButtonH);
                Color32 c = id == El.Empty ? new Color32(40, 42, 54, 255) : e.Color;
                Fill(r, c);
                if (id == selected) Outline(r, Color.white, 2f * UiScale);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) selected = id;
                labelStyle.normal.textColor = Luminance(c) > 0.55f ? new Color(0.08f, 0.08f, 0.1f) : Color.white;
                GUI.Label(r, id == El.Empty ? "Eraser" : e.Name, labelStyle);
            }

            // Tool row.
            string[] tools =
            {
                "Brush -", $"Brush {brushSize}", "Brush +",
                paused ? "Play" : "Pause", "Step", $"Speed x{Speeds[speedIndex]}",
                replaceMode ? "Replace: on" : "Replace: off", "Clear", "Save", "Load", "Menu",
            };
            float rowY = top + Pad + ElementRows * (ButtonH + Gap);
            float toolW = Mathf.Min(ButtonW * 1.2f, (Screen.width - 2 * Pad - Gap * (tools.Length - 1)) / tools.Length);
            for (int n = 0; n < tools.Length; n++)
            {
                var r = new Rect(Pad + n * (toolW + Gap), rowY, toolW, ButtonH);
                if (!GUI.Button(r, tools[n], buttonStyle)) continue;
                switch (n)
                {
                    case 0: ChangeBrush(-1); break;
                    case 1: brushSize = 3; break;
                    case 2: ChangeBrush(1); break;
                    case 3: paused = !paused; break;
                    case 4: paused = true; sim.Step(); break;
                    case 5: speedIndex = (speedIndex + 1) % Speeds.Length; break;
                    case 6: replaceMode = !replaceMode; break;
                    case 7: sim.Clear(); break;
                    case 8: Save(); break;
                    case 9: Load(); break;
                    case 10: OpenMenu(); break;
                }
            }

            DrawBrushPreview(top);

            // Info line.
            Vector2Int cell = MouseCell();
            string hover = sim.InBounds(cell.x, cell.y) && Input.mousePosition.y >= barH
                ? ElementDatabase.Table[sim.Get(cell.x, cell.y)].Name + "  ·  "
                : "";
            string info = $"{hover}{particleCount:N0} particles  ·  {fps:0} fps{(paused ? "  ·  PAUSED" : "")}";
            if (Time.unscaledTime < messageUntil) info += "  ·  " + message;
            GUI.Label(new Rect(Pad + 2, Pad, Screen.width, ButtonH), info, infoStyle);
        }

        void DrawMenu()
        {
            float s = UiScale;
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.45f));

            float w = 300f * s, bh = 44f * s, gap = 10f * s;
            bool hasSave = File.Exists(SavePath);
            int buttons = 2 + (hasSave ? 1 : 0);
            float titleH = 70f * s;
            float total = titleH + gap * 2 + buttons * (bh + gap);
            float x = (Screen.width - w) / 2f, y = (Screen.height - total) / 2f;

            GUI.Label(new Rect(0, y, Screen.width, titleH), "POWDER MAN", titleStyle);
            y += titleH + gap * 2;

            if (GUI.Button(new Rect(x, y, w, bh), started ? "Resume" : "Play", menuButtonStyle)) Play();
            y += bh + gap;
            if (hasSave)
            {
                if (GUI.Button(new Rect(x, y, w, bh), "Load save", menuButtonStyle)) { Play(); Load(); }
                y += bh + gap;
            }
            if (GUI.Button(new Rect(x, y, w, bh), "Quit", menuButtonStyle)) Application.Quit();

            var version = new Rect(0, Screen.height - Pad - 24f * s, Screen.width - Pad * 2, 24f * s);
            infoStyle.alignment = TextAnchor.LowerRight;
            GUI.Label(version, $"v{BuildInfo.Version}  (build {BuildInfo.VersionCode})", infoStyle);
            infoStyle.alignment = TextAnchor.UpperLeft;
        }

        void DrawBrushPreview(float barTop)
        {
            Vector2 mouse = Input.mousePosition;
            if (mouse.y < ToolbarHeight() || Input.touchCount > 0) return;
            Vector2Int cell = MouseCell();
            Vector3 centre = cam.WorldToScreenPoint(new Vector3(cell.x - gridWidth / 2f + 0.5f, cell.y - gridHeight / 2f + 0.5f, 0f));
            float cellPx = cam.pixelHeight / (2f * cam.orthographicSize);
            float size = (brushSize * 2 + 1) * cellPx;
            var r = new Rect(centre.x - size / 2f, Screen.height - centre.y - size / 2f, size, size);
            if (r.yMax > barTop) r.yMax = barTop;
            Outline(r, new Color(1f, 1f, 1f, 0.5f), 1f);
        }

        void MakeStyles()
        {
            int font = Mathf.RoundToInt(13 * UiScale);
            if (labelStyle != null && labelStyle.fontSize == font) return;
            labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = font, fontStyle = FontStyle.Bold, clipping = TextClipping.Clip };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = font };
            infoStyle = new GUIStyle(GUI.skin.label) { fontSize = font };
            infoStyle.normal.textColor = new Color(0.85f, 0.87f, 0.92f);
            titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(52 * UiScale), fontStyle = FontStyle.Bold };
            titleStyle.normal.textColor = ElementDatabase.Table[El.Sand].Color;
            menuButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(20 * UiScale), fontStyle = FontStyle.Bold };
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void Outline(Rect r, Color c, float w)
        {
            Fill(new Rect(r.x, r.y, r.width, w), c);
            Fill(new Rect(r.x, r.yMax - w, r.width, w), c);
            Fill(new Rect(r.x, r.y, w, r.height), c);
            Fill(new Rect(r.xMax - w, r.y, w, r.height), c);
        }

        static float Luminance(Color32 c) => (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
    }
}
