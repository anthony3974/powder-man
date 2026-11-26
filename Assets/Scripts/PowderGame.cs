using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PowderMan
{
    /// <summary>
    /// Runs the simulation, draws it to a texture, handles mouse/touch/keyboard and the UI.
    ///
    /// There is one pen with two saved slots, A and B, each holding an element. Tap the other slot to draw
    /// with it; tap the active slot again to pick its element. One finger draws; extra fingers are ignored.
    /// With a mouse, left draws and right switches between A and B.
    ///
    /// Keys: mouse wheel or [ ] = brush size, Tab = switch A/B, Space = pause, . = single step while paused,
    /// R = replace mode, C = clear, 1-9 = element for the active slot, F5 = save, F9 = load,
    /// Esc / Android back = close the element picker, or the home menu (quits from the menu).
    ///
    /// The world fills the screen right of the side strip: its height is fixed and its width follows the
    /// screen shape. The home menu runs a little demo in the world behind it until you press Play.
    /// </summary>
    public class PowderGame : MonoBehaviour
    {
        [Header("World")]
        [Tooltip("Rows of cells. Cell size on screen = screen height / this.")]
        [SerializeField] int gridHeight = 180;
        [Tooltip("Pick the number of columns so the world fills the screen. Off = use Grid Width.")]
        [SerializeField] bool fitWidthToScreen = true;
        [Tooltip("Columns of cells, only used when Fit Width To Screen is off.")]
        [SerializeField] int gridWidth = 320;
        [Tooltip("Simulation ticks per second at speed x1.")]
        [SerializeField] float ticksPerSecond = 60f;

        [Header("Brush")]
        [SerializeField] int brushSize = 3;
        [SerializeField] int maxBrushSize = 15;
        [Tooltip("Powders, liquids and gases come out as a spray of separate grains: the chance each empty cell " +
                 "under the brush gets one, per frame. 1 = solid blobs. Solids are always drawn solid.")]
        [Range(0.02f, 1f)] [SerializeField] float sprayDensity = 0.12f;

        static readonly float[] Speeds = { 0.25f, 0.5f, 1f, 2f, 4f };

        Simulation sim;
        Texture2D texture;
        SpriteRenderer view;
        Color32[] pixels;
        Color32[] palette;
        Camera cam;
        Vector2Int screenSize;
        float resizeAt;

        // The pen: two saved slots (A, B) and which one is drawing. Kept between runs in PlayerPrefs.
        readonly byte[] slots = { El.Sand, El.Water };
        int activeSlot;
        int pickerSlot = -1;    // which slot the element picker is open for, -1 = closed
        int pickerTab;          // which picker tab shows: an index into ElementDatabase.Categories, or AllTab
        static int AllTab => ElementDatabase.Categories.Length;
        static string SlotName(int slot) => slot == 0 ? "A" : "B";

        // The one stroke being drawn. strokeId = touch fingerId, or MouseStrokeId for the mouse.
        bool drawing;
        int strokeId;
        Vector2Int strokeLast;
        const int MouseStrokeId = -1;

        bool paused, replaceMode;
        int speedIndex = 2;
        float tickTimer;
        int particleCount;
        float fps;
        string message;
        float messageUntil;

        // Home menu
        bool inMenu = true;
        bool helpOpen;          // the Help screen, opened from the home menu
        int helpItem = -1;      // which element the Help screen shows, -1 = How to play
        float helpScroll;       // how far the Help text is scrolled, in pixels
        bool started;           // false until the first Play, while the demo runs behind the menu
        float demoSwapAt;
        readonly byte[] demoSpouts = new byte[3];
        static readonly byte[] DemoElements = { El.Sand, El.Water, El.Oil, El.Snow, El.Lava, El.Gunpowder, El.Acid, El.Fire };

        string SavePath => Path.Combine(Application.persistentDataPath, "sandbox.sav");
        int W => sim.Width;
        int H => sim.Height;

        void Start()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            BuildPalette();
            LoadSlots();

            var go = new GameObject("World View");
            go.transform.SetParent(transform, false);
            view = go.AddComponent<SpriteRenderer>();

            cam = Camera.main;
            if (cam == null) cam = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" }.GetComponent<Camera>();
            cam.orthographic = true;
            cam.transform.position = new Vector3(0, 0, -10);
            cam.transform.rotation = Quaternion.identity;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(8, 9, 13, 255);

            screenSize = new Vector2Int(Screen.width, Screen.height);
            MakeWorld(TargetWidth());
            Render();
        }

        // ------------------------------------------------------------------ world size

        int TargetWidth()
        {
            if (!fitWidthToScreen) return gridWidth;
            float areaW = Screen.width - StripRight, areaH = Mathf.Max(1f, Screen.height);
            return Mathf.Clamp(Mathf.RoundToInt(gridHeight * areaW / areaH), gridHeight / 2, gridHeight * 4);
        }

        /// <summary>Makes a world of the given width, keeping what was in the old one (bottom rows, centred).</summary>
        void MakeWorld(int width)
        {
            var old = sim;
            sim = new Simulation(width, gridHeight);
            if (old != null && started) sim.CopyFrom(old);

            if (texture != null) Destroy(texture);
            if (view.sprite != null) Destroy(view.sprite);
            texture = new Texture2D(width, gridHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            pixels = new Color32[width * gridHeight];
            view.sprite = Sprite.Create(texture, new Rect(0, 0, width, gridHeight), new Vector2(0.5f, 0.5f), 1f);
            drawing = false;
            if (!started) StartDemo();
        }

        /// <summary>
        /// Re-fits the world when the screen changes shape (window resize, or a phone that reports
        /// the wrong orientation on the first frame). Waits for the size to settle first.
        /// </summary>
        void CheckResize()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            if (size != screenSize)
            {
                screenSize = size;
                resizeAt = Time.unscaledTime + 0.3f;
            }
            if (resizeAt > 0f && Time.unscaledTime >= resizeAt)
            {
                resizeAt = 0f;
                int w = TargetWidth();
                if (w != W) MakeWorld(w);
            }
        }

        // ------------------------------------------------------------------ home menu

        /// <summary>Stone ledges and a Void floor, with three spouts pouring random elements on them.</summary>
        void StartDemo()
        {
            sim.Clear();
            for (int x = 0; x < W; x++) sim.Set(x, 0, El.Void);
            int ledge = W / 4;
            for (int s = 0; s < ledge; s++)
            {
                sim.Paint(W / 8 + s, H * 3 / 5 - s / 3, 1, El.Stone, true);
                sim.Paint(W * 7 / 8 - s, H * 2 / 5 - s / 3, 1, El.Stone, true);
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
                sim.Paint(W * (n + 1) / (demoSpouts.Length + 1), H - 3, 2, demoSpouts[n], false, 0.25f);

            if (particleCount > W * H / 2) StartDemo(); // getting clogged, start over
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
            pickerSlot = -1;
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
            CheckResize();
            FitCamera();

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (pickerSlot >= 0) pickerSlot = -1;
                else if (helpOpen) helpOpen = false;
                else if (inMenu) Application.Quit();
                else OpenMenu();
            }

            if (inMenu)
            {
                if (!started) UpdateDemo();
            }
            else
            {
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

        /// <summary>Fits the whole grid into the screen area right of the side strip.</summary>
        void FitCamera()
        {
            float left = Mathf.Clamp(StripRight / Mathf.Max(1, Screen.width), 0f, 0.9f);
            cam.rect = new Rect(left, 0f, 1f - left, 1f);
            float aspect = cam.pixelWidth / (float)Mathf.Max(1, cam.pixelHeight);
            cam.orthographicSize = Mathf.Max(H / 2f, W / 2f / aspect);
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
            if (Input.GetKeyDown(KeyCode.Tab)) SwitchSlot();

            var bar = ElementDatabase.Toolbar;
            for (int n = 0; n < 9 && n < bar.Count; n++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + n)) SetSlot(activeSlot, bar[n]);

            float wheel = Input.mouseScrollDelta.y;
            if (wheel > 0.01f) ChangeBrush(1);
            else if (wheel < -0.01f) ChangeBrush(-1);
        }

        void ChangeBrush(int delta) => brushSize = Mathf.Clamp(brushSize + delta, 0, maxBrushSize);

        // ------------------------------------------------------------------ pen slots A / B

        void SwitchSlot()
        {
            activeSlot = 1 - activeSlot;
            SaveSlots();
        }

        void SetSlot(int slot, byte element)
        {
            slots[slot] = element;
            SaveSlots();
        }

        /// <summary>
        /// Tapping the other slot switches to it. Tapping the slot that's already active opens the element
        /// picker for it (or closes the picker if it's open).
        /// </summary>
        void TapSlot(int slot)
        {
            if (slot != activeSlot)
            {
                activeSlot = slot;
                SaveSlots();
                if (pickerSlot >= 0) OpenPicker(slot); // picker open: follow the switch
                return;
            }
            if (pickerSlot == slot) pickerSlot = -1;
            else OpenPicker(slot);
        }

        /// <summary>Opens the element picker for a slot, on the tab holding that slot's element (All stays on All).</summary>
        void OpenPicker(int slot)
        {
            pickerSlot = slot;
            int tab = ElementDatabase.CategoryOf(slots[slot]);
            if (tab >= 0 && pickerTab != AllTab) pickerTab = tab;
        }

        void LoadSlots()
        {
            for (int s = 0; s < 2; s++)
            {
                int id = PlayerPrefs.GetInt("slot" + SlotName(s), slots[s]);
                if (id == El.Empty || (id > 0 && id < 256 && ElementDatabase.Table[id] != null)) slots[s] = (byte)id;
            }
            activeSlot = Mathf.Clamp(PlayerPrefs.GetInt("activeSlot", 0), 0, 1);
            pickerTab = Mathf.Clamp(PlayerPrefs.GetInt("pickerTab", 0), 0, AllTab);
        }

        void SaveSlots()
        {
            PlayerPrefs.SetInt("slotA", slots[0]);
            PlayerPrefs.SetInt("slotB", slots[1]);
            PlayerPrefs.SetInt("activeSlot", activeSlot);
            PlayerPrefs.SetInt("pickerTab", pickerTab);
        }

        void OnApplicationPause(bool goingToBackground) { if (goingToBackground) PlayerPrefs.Save(); }
        void OnApplicationQuit() => PlayerPrefs.Save();

        // ------------------------------------------------------------------ drawing (touch + mouse)

        void HandlePointer()
        {
            if (Input.touchCount > 0)
            {
                for (int n = 0; n < Input.touchCount; n++)
                {
                    var t = Input.GetTouch(n);
                    switch (t.phase)
                    {
                        case TouchPhase.Began: BeginStroke(t.fingerId, t.position); break;
                        case TouchPhase.Moved:
                        case TouchPhase.Stationary: ContinueStroke(t.fingerId, t.position); break;
                        default: EndStroke(t.fingerId); break;
                    }
                }
                return;
            }

            // No fingers down: drop a touch stroke that never got an Ended phase.
            if (drawing && strokeId != MouseStrokeId) drawing = false;

            if (Input.GetMouseButtonDown(0)) BeginStroke(MouseStrokeId, Input.mousePosition);
            else if (Input.GetMouseButton(0)) ContinueStroke(MouseStrokeId, Input.mousePosition);
            else EndStroke(MouseStrokeId);

            if (Input.GetMouseButtonDown(1) && OverWorld(Input.mousePosition) && pickerSlot < 0) SwitchSlot();
        }

        /// <summary>Starts a stroke if the press is over the world and nothing else is drawing.</summary>
        void BeginStroke(int id, Vector2 screenPos)
        {
            if (pickerSlot >= 0 || !OverWorld(screenPos)) return;
            if (drawing && strokeId != id) return; // one stroke at a time: other fingers are ignored
            var cell = ScreenCell(screenPos);
            drawing = true;
            strokeId = id;
            strokeLast = cell;
            PaintLine(cell, cell);
        }

        void ContinueStroke(int id, Vector2 screenPos)
        {
            if (!drawing || strokeId != id) return;
            var cell = ScreenCell(screenPos);
            PaintLine(strokeLast, cell);
            strokeLast = cell;
        }

        void EndStroke(int id)
        {
            if (drawing && strokeId == id) drawing = false;
        }

        /// <summary>
        /// Paints the active slot's element along the line between two cells so fast strokes don't leave gaps.
        /// Brush stamps are spaced about one radius apart, so a moving spray stays about as sparse as one held
        /// still. A size-0 brush always places its grain, for precise work.
        /// </summary>
        void PaintLine(Vector2Int from, Vector2Int to)
        {
            byte type = slots[activeSlot];
            bool replace = replaceMode || type == El.Empty;
            float density = brushSize == 0 ? 1f : sprayDensity;
            int dx = to.x - from.x, dy = to.y - from.y;
            int spacing = Mathf.Max(1, brushSize);
            int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) / (float)spacing);
            for (int s = 0; s <= steps; s++)
            {
                float f = steps == 0 ? 1f : s / (float)steps;
                sim.Paint(Mathf.RoundToInt(from.x + dx * f), Mathf.RoundToInt(from.y + dy * f), brushSize, type, replace, density);
            }
        }

        bool OverWorld(Vector2 screenPos) => screenPos.x >= StripRight;

        Vector2Int ScreenCell(Vector2 screenPos)
        {
            Vector3 w = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
            return new Vector2Int(Mathf.FloorToInt(w.x + W / 2f), Mathf.FloorToInt(w.y + H / 2f));
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

        // ------------------------------------------------------------------ UI (IMGUI)

        static readonly Color32 PanelColor = new Color32(22, 24, 33, 255);
        static readonly Color32 EdgeColor = new Color32(60, 64, 84, 255);
        static readonly Color32 ButtonColor = new Color32(44, 47, 62, 255);
        static readonly Color32 OnColor = new Color32(214, 176, 92, 255);
        static readonly Color32 EraserColor = new Color32(70, 72, 88, 255);
        static readonly Color DarkText = new Color(0.08f, 0.08f, 0.1f);

        GUIStyle labelStyle, stripBtnStyle, smallStyle, infoStyle, titleStyle, menuStyle, helpNameStyle, helpTagStyle, helpBodyStyle;

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
        float Gap => 5f * UiScale;
        float StripLeft => Screen.safeArea.xMin;
        float StripWidth => 96f * UiScale;
        /// <summary>Right edge of the side strip, in pixels. The world starts here.</summary>
        float StripRight => StripLeft + StripWidth;

        void OnGUI()
        {
            if (sim == null) return;
            MakeStyles();

            if (inMenu)
            {
                GUI.enabled = false;
                DrawStrip();
                GUI.enabled = true;
                if (helpOpen) DrawHelp();
                else DrawMenu();
                return;
            }

            DrawStrip();
            if (pickerSlot >= 0) DrawPicker();
            else DrawBrushPreview();
            DrawInfo();
        }

        /// <summary>The side strip: pen slots A and B, brush size, and the play/speed/replace/clear/menu tools.</summary>
        void DrawStrip()
        {
            float s = UiScale;
            Fill(new Rect(0, 0, StripRight, Screen.height), PanelColor);
            Fill(new Rect(StripRight - Mathf.Max(1f, s), 0, Mathf.Max(1f, s), Screen.height), EdgeColor);

            // Rows, in units of one button height: two tall slot buttons, then seven tool rows.
            const float penRows = 1.7f;
            const int toolRows = 7;
            float top = Screen.height - Screen.safeArea.yMax + Pad;
            float avail = Screen.safeArea.height - Pad * 2 - Gap * (toolRows + 2) - Gap * 2;
            float unit = Mathf.Min(40f * s, avail / (penRows * 2 + toolRows));
            float x = StripLeft + Pad, w = StripWidth - Pad * 2, y = top;

            for (int p = 0; p < 2; p++)
            {
                var r = new Rect(x, y, w, unit * penRows);
                if (SlotButton(r, p)) TapSlot(p);
                y += r.height + Gap;
            }
            y += Gap * 2;

            // Brush: [-] size [+]
            float third = (w - Gap * 2) / 3f;
            if (Btn(new Rect(x, y, third, unit), "-", ButtonColor, stripBtnStyle)) ChangeBrush(-1);
            if (Btn(new Rect(x + third + Gap, y, third, unit), brushSize.ToString(), PanelColor, stripBtnStyle)) brushSize = 3;
            if (Btn(new Rect(x + (third + Gap) * 2, y, third, unit), "+", ButtonColor, stripBtnStyle)) ChangeBrush(1);
            y += unit + Gap;

            if (Btn(new Rect(x, y, w, unit), paused ? "Play" : "Pause", paused ? OnColor : ButtonColor, stripBtnStyle)) paused = !paused;
            y += unit + Gap;

            if (Btn(new Rect(x, y, w, unit), "Step", ButtonColor, stripBtnStyle)) { paused = true; sim.Step(); }
            y += unit + Gap;

            if (Btn(new Rect(x, y, w, unit), $"Speed x{Speeds[speedIndex]}", speedIndex == 2 ? ButtonColor : OnColor, stripBtnStyle))
                speedIndex = (speedIndex + 1) % Speeds.Length;
            y += unit + Gap;

            if (Btn(new Rect(x, y, w, unit), "Replace", replaceMode ? OnColor : ButtonColor, stripBtnStyle)) replaceMode = !replaceMode;
            y += unit + Gap;

            if (Btn(new Rect(x, y, w, unit), "Clear", ButtonColor, stripBtnStyle)) sim.Clear();
            y += unit + Gap;

            if (Btn(new Rect(x, y, w, unit), "Menu", ButtonColor, stripBtnStyle)) OpenMenu();
        }

        /// <summary>
        /// A big swatch showing a slot's element. The active slot gets a gold frame and the other is dimmed.
        /// Tap = draw with this slot; tap it again while it's active = pick its element.
        /// </summary>
        bool SlotButton(Rect r, int slot)
        {
            float s = UiScale;
            byte id = slots[slot];
            bool active = slot == activeSlot;
            Color32 c = id == El.Empty ? EraserColor : ElementDatabase.Table[id].Color;
            bool clicked = Btn(r, "", c);
            Color text = Luminance(c) > 0.55f ? DarkText : Color.white;

            smallStyle.normal.textColor = new Color(text.r, text.g, text.b, 0.8f);
            smallStyle.alignment = TextAnchor.UpperCenter;
            GUI.Label(new Rect(r.x, r.y + 3f * s, r.width, r.height), active ? $"{SlotName(slot)} · ON" : SlotName(slot), smallStyle);
            smallStyle.alignment = TextAnchor.MiddleCenter;

            labelStyle.normal.textColor = text;
            GUI.Label(new Rect(r.x, r.y + r.height * 0.15f, r.width, r.height * 0.85f), ElementName(id), labelStyle);

            if (!active) Fill(r, new Color(0f, 0f, 0f, 0.45f));
            if (active) Outline(r, OnColor, 3f * s);
            if (pickerSlot == slot) Outline(new Rect(r.x - 2f * s, r.y - 2f * s, r.width + 4f * s, r.height + 4f * s), Color.white, 2f * s);
            return clicked;
        }

        /// <summary>
        /// The element picker: a row of tabs (one per category, plus All) over a grid of that tab's elements,
        /// with the Eraser last in every tab. Picks one for pickerSlot. Tapping outside closes it.
        /// </summary>
        void DrawPicker()
        {
            float s = UiScale;
            float right = Mathf.Min(Screen.width, Screen.safeArea.xMax);
            var area = new Rect(StripRight, 0, right - StripRight, Screen.height);
            Fill(new Rect(StripRight, 0, Screen.width - StripRight, Screen.height), new Color(0f, 0f, 0f, 0.4f));

            float margin = 10f * s, pad = 12f * s, gap = 6f * s, titleH = 30f * s, tabH = 40f * s;
            float pw = Mathf.Min(area.width - margin * 2, 1100f * s), ph = area.height - margin * 2;
            var panel = new Rect(area.x + (area.width - pw) / 2f, margin, pw, ph);
            Fill(panel, PanelColor);
            Outline(panel, EdgeColor, Mathf.Max(1f, s));

            infoStyle.alignment = TextAnchor.MiddleLeft;
            GUI.Label(new Rect(panel.x + pad, panel.y + pad * 0.5f, pw - pad * 2, titleH), $"Slot {SlotName(pickerSlot)}: pick an element", infoStyle);
            infoStyle.alignment = TextAnchor.UpperLeft;

            // ---- Tabs
            var cats = ElementDatabase.Categories;
            int tabs = cats.Length + 1;
            float y = panel.y + pad * 0.5f + titleH;
            float tw = (pw - pad * 2 - gap * (tabs - 1)) / tabs;
            var tabStyle = tw < 70f * s ? smallStyle : labelStyle;
            for (int c = 0; c < tabs; c++)
            {
                var r = new Rect(panel.x + pad + c * (tw + gap), y, tw, tabH);
                if (Btn(r, c < cats.Length ? cats[c] : "All", c == pickerTab ? OnColor : ButtonColor, tabStyle) && pickerTab != c)
                {
                    pickerTab = c;
                    SaveSlots();
                }
                // A dot on tabs holding slot A's or B's element, so you can find them.
                if (c < cats.Length && (ElementDatabase.CategoryOf(slots[0]) == c || ElementDatabase.CategoryOf(slots[1]) == c))
                    Fill(new Rect(r.xMax - 9f * s, r.y + 4f * s, 5f * s, 5f * s), c == pickerTab ? (Color)DarkText : (Color)OnColor);
            }
            y += tabH + pad;

            // ---- Elements in this tab, then the Eraser. Pick the column count that gives the biggest buttons.
            var list = pickerTab < cats.Length ? ElementDatabase.ByCategory[pickerTab] : ElementDatabase.Toolbar;
            int count = list.Count + 1;
            var grid = new Rect(panel.x + pad, y, pw - pad * 2, panel.yMax - pad - y);
            float maxW = 150f * s, maxH = 56f * s;
            int cols = 1;
            float best = -1f;
            for (int c = 1; c <= count; c++)
            {
                int rws = Mathf.CeilToInt(count / (float)c);
                float cw = (grid.width - gap * (c - 1)) / c, ch = (grid.height - gap * (rws - 1)) / rws;
                float score = Mathf.Min(Mathf.Min(cw, maxW) / 2.6f, Mathf.Min(ch, maxH));
                if (score >= best - 0.001f) { best = Mathf.Max(best, score); cols = c; } // ties: more columns, fewer rows
            }
            int rows = Mathf.CeilToInt(count / (float)cols);
            float bw = Mathf.Min(maxW, (grid.width - gap * (cols - 1)) / cols);
            float bh = Mathf.Min(maxH, (grid.height - gap * (rows - 1)) / rows);
            float gx = grid.x + (grid.width - (cols * (bw + gap) - gap)) / 2f;
            var style = bh < 24f * s || bw < 80f * s ? smallStyle : labelStyle;

            for (int n = 0; n < count; n++)
            {
                byte id = n < list.Count ? list[n] : El.Empty;
                var r = new Rect(gx + (n % cols) * (bw + gap), grid.y + (n / cols) * (bh + gap), bw, bh);
                Color32 c = id == El.Empty ? EraserColor : ElementDatabase.Table[id].Color;
                if (Btn(r, ElementName(id), c, style))
                {
                    activeSlot = pickerSlot;
                    SetSlot(pickerSlot, id);
                    pickerSlot = -1;
                }
                if (pickerSlot >= 0 && id == slots[pickerSlot]) Outline(r, Color.white, 2f * s);

                // Small badge saying which slots have this element.
                string badge = (slots[0] == id ? "A" : "") + (slots[1] == id ? "B" : "");
                if (badge.Length > 0)
                {
                    smallStyle.normal.textColor = Luminance(c) > 0.55f ? DarkText : Color.white;
                    smallStyle.alignment = TextAnchor.UpperRight;
                    GUI.Label(new Rect(r.x, r.y + 2f * s, r.width - 4f * s, r.height), badge, smallStyle);
                    smallStyle.alignment = TextAnchor.MiddleCenter;
                }
            }

            var ev = Event.current;
            if (ev.type == EventType.MouseDown && !panel.Contains(ev.mousePosition) && ev.mousePosition.x >= StripRight)
            {
                pickerSlot = -1;
                ev.Use();
            }
        }

        void DrawInfo()
        {
            string hover = "";
            if (Input.mousePresent && Input.touchCount == 0 && OverWorld(Input.mousePosition))
            {
                Vector2Int cell = ScreenCell(Input.mousePosition);
                if (sim.InBounds(cell.x, cell.y)) hover = ElementName(sim.Get(cell.x, cell.y)) + "  ·  ";
            }
            string info = $"{hover}{particleCount:N0} particles  ·  {fps:0} fps{(paused ? "  ·  PAUSED" : "")}";
            if (Time.unscaledTime < messageUntil) info += "  ·  " + message;
            GUI.Label(new Rect(StripRight + Pad + 2, Pad, Screen.width, 30f * UiScale), info, infoStyle);
        }

        void DrawMenu()
        {
            float s = UiScale;
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.55f));

            bool hasSave = File.Exists(SavePath);
            var items = new List<(string label, System.Action action)>();
            if (started)
            {
                items.Add(("Resume", () => Play()));
                items.Add(("Save", () => Save()));
                if (hasSave) items.Add(("Load save", () => { Load(); Play(); }));
                items.Add(("New sandbox", () => { sim.Clear(); Play(); }));
            }
            else
            {
                items.Add(("Play", () => Play()));
                if (hasSave) items.Add(("Load save", () => { Play(); Load(); }));
            }
            items.Add(("Help", () => { helpOpen = true; helpScroll = 0f; }));
            items.Add(("Quit", () => Application.Quit()));

            float titleH = 64f * s, subH = 24f * s, hintH = 40f * s;
            float gap = 10f * s;
            float bh = Mathf.Min(48f * s, (Screen.height - titleH - subH - hintH - gap * (items.Count + 3)) / items.Count);
            float w = 300f * s;
            float total = titleH + subH + gap * 2 + items.Count * (bh + gap) + hintH;
            float x = (Screen.width - w) / 2f, y = Mathf.Max(0f, (Screen.height - total) / 2f);

            GUI.Label(new Rect(0, y, Screen.width, titleH), "POWDER MAN", titleStyle);
            y += titleH;
            infoStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(0, y, Screen.width, subH), "a falling-sand sandbox", infoStyle);
            y += subH + gap * 2;

            foreach (var (label, action) in items)
            {
                bool primary = label == "Resume" || label == "Play";
                if (Btn(new Rect(x, y, w, bh), label, primary ? OnColor : ButtonColor, menuStyle)) action();
                y += bh + gap;
            }

            string hint = Time.unscaledTime < messageUntil ? message
                : Application.isMobilePlatform ? "Tap A or B to switch   ·   Tap the lit one again to change its element"
                : "Left mouse: draw   ·   Right mouse or Tab: switch A/B   ·   Click the lit slot: change it";
            GUI.Label(new Rect(0, y, Screen.width, hintH), hint, infoStyle);

            var version = new Rect(0, Screen.height - Pad - 24f * s, Screen.width - Pad * 2, 24f * s);
            infoStyle.alignment = TextAnchor.LowerRight;
            GUI.Label(version, $"v{BuildInfo.Version}  (build {BuildInfo.VersionCode})", infoStyle);
            infoStyle.alignment = TextAnchor.UpperLeft;
        }

        /// <summary>
        /// The Help screen: a grid of every element (plus How to play) on the left, and what the chosen one
        /// does on the right. The text scrolls by dragging or with the mouse wheel when it doesn't fit.
        /// </summary>
        void DrawHelp()
        {
            float s = UiScale;
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color32(14, 15, 21, 248));

            float pad = 12f * s, gap = 5f * s;
            var safe = Screen.safeArea;
            float left = safe.xMin + pad, right = safe.xMax - pad;
            float top = Screen.height - safe.yMax + pad, bottom = Screen.height - safe.yMin - pad;
            float barH = 40f * s;

            helpNameStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(left, top, right - left, barH), "Help", helpNameStyle);
            if (Btn(new Rect(right - 110f * s, top, 110f * s, barH), "Back", OnColor)) helpOpen = false;

            float y0 = top + barH + pad, h = bottom - y0, w = right - left;
            var grid = new Rect(left, y0, Mathf.Round(w * 0.5f), h);
            var detail = new Rect(grid.xMax + pad, y0, right - grid.xMax - pad, h);

            // ---- Grid: How to play, then every element in picker order. Pick the column count that gives the biggest buttons.
            var bar = ElementDatabase.Toolbar;
            int count = bar.Count + 1;
            int cols = 1;
            float best = -1f;
            for (int c = 2; c <= 12; c++)
            {
                int r = Mathf.CeilToInt(count / (float)c);
                float cw = (grid.width - gap * (c - 1)) / c, ch = (grid.height - gap * (r - 1)) / r;
                float score = Mathf.Min(cw / 2.6f, ch);
                if (score > best) { best = score; cols = c; }
            }
            int rows = Mathf.CeilToInt(count / (float)cols);
            float bw = (grid.width - gap * (cols - 1)) / cols;
            float bh = Mathf.Min(40f * s, (grid.height - gap * (rows - 1)) / rows);
            var style = bh < 24f * s || bw < 80f * s ? smallStyle : labelStyle;

            for (int n = 0; n < count; n++)
            {
                int item = n - 1;                                  // -1 = How to play
                byte id = item < 0 ? El.Empty : bar[item];
                var r = new Rect(grid.x + (n % cols) * (bw + gap), grid.y + (n / cols) * (bh + gap), bw, bh);
                Color32 c = item < 0 ? OnColor : ElementDatabase.Table[id].Color;
                if (Btn(r, item < 0 ? "How to play" : ElementName(id), c, style) && helpItem != item)
                {
                    helpItem = item;
                    helpScroll = 0f;
                }
                if (helpItem == item) Outline(r, Color.white, 2f * s);
            }

            // ---- Detail: swatch, name, tags, then the text.
            Fill(detail, PanelColor);
            Outline(detail, EdgeColor, Mathf.Max(1f, s));
            float ip = 14f * s, sw = 38f * s;
            byte shown = helpItem < 0 ? El.Empty : bar[Mathf.Clamp(helpItem, 0, bar.Count - 1)];
            var swatch = new Rect(detail.x + ip, detail.y + ip, sw, sw);
            Fill(swatch, helpItem < 0 ? (Color)OnColor : (Color)ElementDatabase.Table[shown].Color);
            Outline(swatch, EdgeColor, Mathf.Max(1f, s));
            float tx = swatch.xMax + 10f * s, tw = detail.xMax - ip - tx;
            GUI.Label(new Rect(tx, swatch.y - 2f * s, tw, sw * 0.62f), helpItem < 0 ? ElementHelp.HowToPlayTitle : ElementName(shown), helpNameStyle);
            if (helpItem >= 0) GUI.Label(new Rect(tx, swatch.y + sw * 0.58f, tw, sw * 0.45f), ElementHelp.Tags(shown), helpTagStyle);

            var body = new Rect(detail.x + ip, swatch.yMax + 12f * s, detail.width - ip * 2, detail.yMax - ip - swatch.yMax - 12f * s);
            string text = helpItem < 0 ? ElementHelp.HowToPlay : ElementHelp.For(shown);
            float contentH = helpBodyStyle.CalcHeight(new GUIContent(text), body.width);
            float maxScroll = Mathf.Max(0f, contentH - body.height);

            var ev = Event.current;
            if (ev.type == EventType.ScrollWheel && detail.Contains(ev.mousePosition)) { helpScroll += ev.delta.y * 20f * s; ev.Use(); }
            else if (ev.type == EventType.MouseDrag && detail.Contains(ev.mousePosition)) { helpScroll -= ev.delta.y; ev.Use(); }
            helpScroll = Mathf.Clamp(helpScroll, 0f, maxScroll);

            GUI.BeginGroup(body);
            GUI.Label(new Rect(0f, -helpScroll, body.width, contentH), text, helpBodyStyle);
            GUI.EndGroup();

            if (maxScroll > 0f)
            {
                // A thin bar on the right edge showing where you are in the text.
                float trackX = detail.xMax - 5f * s, thumbH = Mathf.Max(20f * s, body.height * body.height / contentH);
                float thumbY = body.y + (body.height - thumbH) * (helpScroll / maxScroll);
                Fill(new Rect(trackX, body.y, 3f * s, body.height), EdgeColor);
                Fill(new Rect(trackX, thumbY, 3f * s, thumbH), OnColor);
            }
        }

        void DrawBrushPreview()
        {
            Vector2 mouse = Input.mousePosition;
            if (!Input.mousePresent || Input.touchCount > 0 || !OverWorld(mouse)) return;
            Vector2Int cell = ScreenCell(mouse);
            Vector3 centre = cam.WorldToScreenPoint(new Vector3(cell.x - W / 2f + 0.5f, cell.y - H / 2f + 0.5f, 0f));
            float cellPx = cam.pixelHeight / (2f * cam.orthographicSize);
            float size = (brushSize * 2 + 1) * cellPx;
            var r = new Rect(centre.x - size / 2f, Screen.height - centre.y - size / 2f, size, size);
            if (r.xMin < StripRight) r.xMin = StripRight;
            Outline(r, new Color(1f, 1f, 1f, 0.5f), 1f);
        }

        /// <summary>A flat button: coloured box, centred label, lighter while pressed.</summary>
        bool Btn(Rect r, string text, Color32 color, GUIStyle style = null)
        {
            bool down = GUI.enabled && Input.GetMouseButton(0) && r.Contains(Event.current.mousePosition);
            Fill(r, down ? Color.Lerp(color, Color.white, 0.25f) : (Color)color);
            if (text.Length > 0)
            {
                style ??= labelStyle;
                style.normal.textColor = Luminance(color) > 0.55f ? DarkText : Color.white;
                GUI.Label(r, text, style);
            }
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        static string ElementName(byte id) => id == El.Empty ? "Eraser" : ElementDatabase.Table[id].Name;

        void MakeStyles()
        {
            int font = Mathf.RoundToInt(13 * UiScale);
            if (labelStyle != null && labelStyle.fontSize == font) return;
            labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = font, fontStyle = FontStyle.Bold, clipping = TextClipping.Clip, padding = new RectOffset(0, 0, 0, 0) };
            stripBtnStyle = new GUIStyle(labelStyle) { wordWrap = false };
            smallStyle = new GUIStyle(labelStyle) { fontSize = Mathf.RoundToInt(10 * UiScale) };
            infoStyle = new GUIStyle(GUI.skin.label) { fontSize = font };
            infoStyle.normal.textColor = new Color(0.85f, 0.87f, 0.92f);
            titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(52 * UiScale), fontStyle = FontStyle.Bold };
            titleStyle.normal.textColor = ElementDatabase.Table[El.Sand].Color;
            menuStyle = new GUIStyle(labelStyle) { fontSize = Mathf.RoundToInt(20 * UiScale) };
            helpNameStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleLeft, fontSize = Mathf.RoundToInt(20 * UiScale) };
            helpTagStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, fontSize = Mathf.RoundToInt(12 * UiScale), padding = new RectOffset(0, 0, 0, 0) };
            helpTagStyle.normal.textColor = new Color(0.6f, 0.63f, 0.72f);
            helpBodyStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(14 * UiScale), wordWrap = true, richText = true, padding = new RectOffset(0, 6, 0, 0) };
            helpBodyStyle.normal.textColor = new Color(0.88f, 0.9f, 0.94f);
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
