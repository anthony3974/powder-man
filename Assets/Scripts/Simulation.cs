using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PowderMan
{
    /// <summary>
    /// The cell grid and its rules. y = 0 is the bottom row. Plain C#, no Unity objects,
    /// so it's easy to read and change. Element behaviour comes from ElementDatabase.
    /// </summary>
    public class Simulation
    {
        public readonly int Width, Height;
        public readonly byte[] Cells;
        /// <summary>Each cell's temperature in degrees C (air too). Moves with the cell.</summary>
        public readonly float[] Temp;
        public const float Room = 20f;
        readonly byte[] life;   // remaining ticks for things that decay; for Cloner, the element it copies
        readonly byte[] shade;  // 0..15, picks a colour variant
        readonly int[] stamp;   // tick number a cell last moved/changed, so nothing moves twice per tick
        int tick;

        // Electricity. Each Lightning strike on a conductor starts a charge (a numbered wave) that runs through touching
        // Electric cells, ChargeStepsPerTick cells a tick. Each cell remembers the last wave it carried and the step it
        // carried it, so a wave goes through each cell once and always ends, even in moving Salt Water.
        readonly int[] charged, chargeWave;
        int chargeStep, waves;
        List<int> chargeFront = new List<int>(), chargeSpreading = new List<int>();
        const int ChargeStepsPerTick = 4, NoCharge = int.MinValue / 2;
        const float ChargeHeat = 30f;                   // degrees a cell warms up each time a charge passes through
        /// <summary>For how many charge steps (4 a tick) a cell shows as lit up after a charge passes.</summary>
        public const int ChargeGlowSteps = 24;
        static readonly int[] Side = { 1, 3, 4, 6 };   // the 4 neighbours (below, left, right, above) in DX/DY
        // Portal Out cells seen last tick (where Portal In sends things), and the ones being collected this tick.
        List<int> exitPortals = new List<int>(), nextExitPortals = new List<int>();
        uint rng = 2463534242u;

        static readonly Element[] E = ElementDatabase.Table;
        static readonly List<Reaction>[] R = ElementDatabase.Reactions;
        static readonly int[] DX = { -1, 0, 1, -1, 1, -1, 0, 1 };
        static readonly int[] DY = { -1, -1, -1, 0, 0, 1, 1, 1 };

        // Per-element temperature settings copied into flat arrays, for the heat pass that runs over every cell.
        static readonly float[] Cond = new float[256], Hold = new float[256], HotLimit = new float[256], ColdLimit = new float[256];
        /// <summary>
        /// The heat pass runs every HeatEvery ticks (it touches every cell, so this saves time). Each run, this much of
        /// the temperature difference flows between two touching cells, times the lower Conductivity. Keep it at 0.5 or under.
        /// </summary>
        const int HeatEvery = 2;
        const float HeatFlow = 0.4f;

        static Simulation()
        {
            for (int t = 0; t < 256; t++)
            {
                var e = E[t];
                HotLimit[t] = float.MaxValue;
                ColdLimit[t] = float.MinValue;
                if (e == null) continue;
                Cond[t] = e.Conductivity;
                Hold[t] = Mathf.Min(1f, e.Hold * HeatEvery);
                if (e.HeatsInto != El.Empty) HotLimit[t] = e.HeatsAt;
                if (e.Flammability > 0f) HotLimit[t] = Mathf.Min(HotLimit[t], e.IgnitesAt);
                if (e.CoolsInto != El.Empty) ColdLimit[t] = e.CoolsAt;
            }
        }

        public Simulation(int width, int height)
        {
            Width = width;
            Height = height;
            int n = width * height;
            Cells = new byte[n];
            Temp = new float[n];
            Array.Fill(Temp, Room);
            charged = new int[n];
            Array.Fill(charged, NoCharge);
            chargeWave = new int[n];
            life = new byte[n];
            shade = new byte[n];
            stamp = new int[n];
        }

        public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;
        public byte Get(int x, int y) => InBounds(x, y) ? Cells[y * Width + x] : El.Wall;
        public byte Shade(int i) => shade[i];

        public void Clear()
        {
            Array.Clear(Cells, 0, Cells.Length);
            Array.Clear(life, 0, life.Length);
            Array.Fill(Temp, Room);
            Array.Fill(charged, NoCharge);
            Array.Clear(chargeWave, 0, chargeWave.Length);
            chargeFront.Clear();
        }

        public void Set(int x, int y, byte type)
        {
            if (InBounds(x, y)) SetIndex(y * Width + x, type);
        }

        /// <summary>
        /// Paints a circle. Only fills empty cells unless replace is true. For powders, liquids and gases,
        /// each cell is filled with chance `density` (a spray of separate grains); solids and the eraser always fill.
        /// </summary>
        public void Paint(int cx, int cy, int radius, byte type, bool replace, float density = 1f)
        {
            var e = E[type];
            float fill = e.Kind == Kind.Solid || e.Kind == Kind.Empty ? 1f : density;
            int r2 = radius * radius + radius;
            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx * dx + dy * dy > r2) continue;
                int x = cx + dx, y = cy + dy;
                if (!InBounds(x, y)) continue;
                int i = y * Width + x;
                if (type == El.Empty) { Cells[i] = El.Empty; Temp[i] = Room; continue; }
                if (!replace && Cells[i] != El.Empty) continue;
                if (fill < 1f && Rand01() > fill) continue;
                SetIndex(i, type);
                Temp[i] = e.Temperature;
            }
        }

        // ------------------------------------------------------------------ update

        public void Step()
        {
            tick++;
            (exitPortals, nextExitPortals) = (nextExitPortals, exitPortals);
            nextExitPortals.Clear();
            for (int y = 0; y < Height; y++)
            {
                if ((Next() & 1) == 0)
                    for (int x = 0; x < Width; x++) UpdateCell(x, y);
                else
                    for (int x = Width - 1; x >= 0; x--) UpdateCell(x, y);
            }
            UpdateCharge();
            if (tick % HeatEvery == 0) UpdateHeat();
        }

        void UpdateCell(int x, int y)
        {
            int i = y * Width + x;
            byte t = Cells[i];
            if (t == El.Empty || stamp[i] == tick) return;
            var e = E[t];

            if (e.LifeMax > 0)
            {
                if (life[i] <= 1)
                {
                    SetIndex(i, Rand01() < e.DecayChance ? e.DecaysInto : e.DecaysElse);
                    return;
                }
                life[i]--;
            }
            if (e.Flicker) shade[i] = (byte)(Next() & 15);

            if (t == El.Cloner) { UpdateCloner(i, x, y); return; }

            if (e.Heat > 0f)
            {
                IgniteNeighbours(x, y, e.Heat);
                if (Cells[i] != t) return; // got caught in an explosion
            }

            if (R[t] != null || e.Emits != El.Empty)
            {
                Interact(i, x, y, t, e);
                if (Cells[i] != t) return;
            }

            if (UpdateSpecial(i, x, y, t, e)) return;

            if (e.MoveChance < 1f && Rand01() > e.MoveChance) return;

            switch (e.Kind)
            {
                case Kind.Powder:
                {
                    int d = RandDir();
                    if (Scatter(i, x, y, d, e)) return;
                    if (TryMove(i, x, y - 1, e)) return;
                    if (TryMove(i, x + d, y - 1, e)) return;
                    TryMove(i, x - d, y - 1, e);
                    break;
                }
                case Kind.Liquid:
                {
                    int d = RandDir();
                    if (Scatter(i, x, y, d, e)) return;
                    if (TryMove(i, x, y - 1, e)) return;
                    if (TryMove(i, x + d, y - 1, e)) return;
                    if (TryMove(i, x - d, y - 1, e)) return;
                    if (Slide(i, x, y, d, e.Spread)) return;
                    Slide(i, x, y, -d, e.Spread);
                    break;
                }
                case Kind.Gas:
                {
                    int d = RandDir();
                    int ux = (Next() & 1) == 0 ? x : x + d; // wobble as it rises
                    if (TryMove(i, ux, y + 1, e)) return;
                    if (TryMove(i, x - d, y + 1, e)) return;
                    if (Slide(i, x, y, d, e.Spread)) return;
                    Slide(i, x, y, -d, e.Spread);
                    break;
                }
            }
        }

        /// <summary>Hot cells try to set fire to all 8 neighbours every tick.</summary>
        void IgniteNeighbours(int x, int y, float heat)
        {
            for (int k = 0; k < 8; k++)
            {
                int nx = x + DX[k], ny = y + DY[k];
                if (!InBounds(nx, ny)) continue;
                int j = ny * Width + nx;
                var oe = E[Cells[j]];
                if (oe.Flammability > 0f && Rand01() < oe.Flammability * heat) Ignite(nx, ny, j, oe);
            }
        }

        /// <summary>Checks one random neighbour for emitting and reactions.</summary>
        void Interact(int i, int x, int y, byte t, Element e)
        {
            int k = (int)(Next() & 7);
            int nx = x + DX[k], ny = y + DY[k];
            if (!InBounds(nx, ny)) return;
            int j = ny * Width + nx;
            byte o = Cells[j];
            if (o == El.Empty)
            {
                if (e.Emits != El.Empty && Rand01() < e.EmitChance) SetIndex(j, e.Emits);
                return;
            }

            var list = R[t];
            if (list == null) return;
            for (int n = 0; n < list.Count; n++)
            {
                var r = list[n];
                bool match = r.Other == El.Any ? (o != t && !E[o].Indestructible) : r.Other == o;
                if (!match || Rand01() >= r.Chance) continue;
                if (r.SelfBecomes != t) SetIndex(i, r.SelfBecomes);
                if (r.OtherBecomes != o) SetIndex(j, r.OtherBecomes);
                return;
            }
        }

        void Ignite(int x, int y, int i, Element e)
        {
            if (e.ExplodeRadius > 0) Explode(x, y, e.ExplodeRadius);
            else SetIndex(i, e.BurnsInto);
        }

        public void Explode(int cx, int cy, int radius)
        {
            int r2 = radius * radius;
            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx * dx + dy * dy > r2) continue;
                int x = cx + dx, y = cy + dy;
                if (!InBounds(x, y)) continue;
                int i = y * Width + x;
                var e = E[Cells[i]];
                if (e.Indestructible) continue;
                bool center = dx == 0 && dy == 0;
                // Other explosives are left alone so the fire sets them off a moment later (chain reaction).
                if (!center && e.ExplodeRadius > 0) continue;
                if (e.Kind == Kind.Liquid && e.Flammability <= 0f) continue;
                SetIndex(i, center || e.Kind == Kind.Empty || e.Kind == Kind.Gas || Rand01() < 0.5f ? El.Fire : El.Empty);
            }
        }

        void UpdateCloner(int i, int x, int y)
        {
            int k = (int)(Next() & 7);
            int nx = x + DX[k], ny = y + DY[k];
            if (!InBounds(nx, ny)) return;
            int j = ny * Width + nx;
            byte o = Cells[j];
            if (life[i] == 0)
            {
                if (o != El.Empty && o != El.Cloner && !E[o].Indestructible) life[i] = o;
            }
            else if (o == El.Empty && Rand01() < 0.3f)
            {
                SetIndex(j, life[i]);
            }
        }

        // ------------------------------------------------------------------ temperature

        /// <summary>
        /// Runs over every cell once a tick. Heat flows between touching cells, faster through good conductors
        /// (Metal). Things that keep their own temperature pull back toward it: Fire and Lava stay hot, Ice stays
        /// cold, air settles back to room temperature. A cell that gets hot or cold enough changes (melts, boils,
        /// freezes) or catches fire.
        /// </summary>
        void UpdateHeat()
        {
            for (int y = 0; y < Height; y++)
            {
                int row = y * Width;
                bool hasUp = y + 1 < Height;
                for (int x = 0; x < Width; x++)
                {
                    int i = row + x;
                    byte t = Cells[i];
                    float ti = Temp[i], ci = Cond[t];
                    if (ci > 0f)
                    {
                        if (x + 1 < Width) ti = Exchange(ti, ci, i + 1);
                        if (hasUp) ti = Exchange(ti, ci, i + Width);
                    }
                    float hold = Hold[t];
                    if (hold > 0f) ti += (E[t].Temperature - ti) * hold;
                    Temp[i] = ti;
                    if (ti > HotLimit[t]) Heated(i, x, y, t, ti);
                    else if (ti < ColdLimit[t]) SetIndex(i, E[t].CoolsInto);
                }
            }
        }

        /// <summary>Moves heat between a cell (temperature ti, conductivity ci) and cell j. Returns the cell's new temperature.</summary>
        float Exchange(float ti, float ci, int j)
        {
            float d = Temp[j] - ti;
            if (d < 0.5f && d > -0.5f) return ti;
            float f = d * Mathf.Min(ci, Cond[Cells[j]]) * HeatFlow;
            Temp[j] -= f;
            return ti + f;
        }

        /// <summary>
        /// A cell past its hot limit: melts or boils if it's past HeatsAt, otherwise catches fire. With Latent, it stays
        /// at HeatsAt and stores the extra heat in its life (255 = full) until it has soaked up enough to change.
        /// </summary>
        void Heated(int i, int x, int y, byte t, float ti)
        {
            var e = E[t];
            if (e.HeatsInto == El.Empty || ti <= e.HeatsAt)
            {
                Ignite(x, y, i, e);
                return;
            }
            if (e.Latent > 0f)
            {
                Temp[i] = e.HeatsAt;
                int stored = life[i] + Mathf.Max(1, (int)((ti - e.HeatsAt) * 255f / e.Latent));
                if (stored < 255) { life[i] = (byte)stored; return; }
            }
            SetIndex(i, e.HeatsChance >= 1f || Rand01() < e.HeatsChance ? e.HeatsInto : e.HeatsElse);
        }

        /// <summary>
        /// Keeps a temperature inside the range an element can exist at, so something that just changed doesn't
        /// change straight back (Ice melting gives Water just above freezing, not below it).
        /// </summary>
        static float FitTemp(Element e, float t)
        {
            if (e.HeatsInto != El.Empty) t = Mathf.Min(t, e.HeatsAt - 1f);
            if (e.Flammability > 0f) t = Mathf.Min(t, e.IgnitesAt - 1f);
            if (e.CoolsInto != El.Empty) t = Mathf.Max(t, e.CoolsAt + 1f);
            return t;
        }

        // ------------------------------------------------------------------ electricity

        /// <summary>How many charge steps ago cell i last carried a charge (big = never). PowderGame lights it up while small.</summary>
        public int ChargeAge(int i) => chargeStep - charged[i];

        /// <summary>Starts a new charge in cell i, if it carries electricity.</summary>
        public void Charge(int i)
        {
            if (E[Cells[i]].Electric) Carry(i, ++waves);
        }

        /// <summary>Cell i carries wave w: lights up, warms a little, and passes it on next step.</summary>
        void Carry(int i, int w)
        {
            chargeWave[i] = w;
            charged[i] = chargeStep;
            Temp[i] += ChargeHeat;
            chargeFront.Add(i);
        }

        /// <summary>Moves every charge on one cell at a time, ChargeStepsPerTick times.</summary>
        void UpdateCharge()
        {
            for (int s = 0; s < ChargeStepsPerTick; s++)
            {
                chargeStep++;
                if (chargeFront.Count == 0) continue;
                (chargeFront, chargeSpreading) = (chargeSpreading, chargeFront);
                chargeFront.Clear();
                foreach (int i in chargeSpreading)
                    if (charged[i] == chargeStep - 1 && E[Cells[i]].Electric) SpreadCharge(i);
            }
        }

        /// <summary>
        /// A charged cell passes the charge to Electric neighbours, and shocks the rest: things that burn may
        /// catch fire, and Ants and Worms are fried to Ash.
        /// </summary>
        void SpreadCharge(int i)
        {
            int x = i % Width, y = i / Width;
            foreach (int k in Side)
            {
                int nx = x + DX[k], ny = y + DY[k];
                if (!InBounds(nx, ny)) continue;
                int j = ny * Width + nx;
                byte o = Cells[j];
                var oe = E[o];
                if (oe.Electric) { if (chargeWave[j] != chargeWave[i]) Carry(j, chargeWave[i]); }
                else if (o == El.Ant || o == El.Worm) SetIndex(j, El.Ash);
                else if (oe.Flammability > 0f && Rand01() < oe.Flammability * 0.5f) Ignite(nx, ny, j, oe);
            }
        }

        // ------------------------------------------------------------------ special elements
        // Elements that need more than the Element settings and reactions. Each gets a branch here.
        // `life` is spare for elements that don't decay, so several of them keep their own state in it.

        /// <summary>Runs an element's own rules. Returns true if it handled the cell this tick (skip normal movement).</summary>
        bool UpdateSpecial(int i, int x, int y, byte t, Element e)
        {
            switch (t)
            {
                case El.Ball: UpdateBall(i, x, y, e); return true;
                case El.Nitro: return UpdateNitro(i, x, y);
                case El.Bubble: return UpdateBubble(i, x, y);
                case El.Lightning: UpdateLightning(i, x, y); return true;
                case El.BlackHole: UpdateBlackHole(x, y); return true;
                case El.PortalIn: UpdatePortalIn(x, y); return true;
                case El.PortalOut: nextExitPortals.Add(i); return true;
                case El.Ant: UpdateAnt(i, x, y, e); return true;
                case El.Worm: UpdateWorm(i, x, y, e); return true;
                case El.Vine: UpdateVine(i, x, y); return true;
                case El.Bud: UpdateBud(i, x, y); return true;
                default: return false;
            }
        }

        /// <summary>
        /// life = fall speed while dropping (gains 1 per tick), or 128 + cells left to rise while bouncing up.
        /// On landing it bounces back up 60% of the way, so each bounce is lower until it settles.
        /// </summary>
        void UpdateBall(int i, int x, int y, Element e)
        {
            int v = life[i];
            int d = RandDir();
            if (v >= 128)
            {
                int left = v - 128;
                int nx = (Next() & 3) == 0 ? x + d : x;
                int j = left > 0 ? MoveTo(i, nx, y + 1, e) : -1;
                if (j < 0) life[i] = 0;                                    // top of the bounce
                else life[j] = (byte)(left > 1 ? 128 + left - 1 : 0);
                return;
            }
            int k = MoveTo(i, x, y - 1, e);
            if (k < 0) k = MoveTo(i, x + d, y - 1, e);
            if (k >= 0) { life[k] = (byte)Mathf.Min(v + 1, 120); return; }
            life[i] = (byte)(v > 3 ? 128 + v * 6 / 10 : 0);              // landed: bounce, or rest
        }

        /// <summary>Counts how far it has fallen in life. Landing after a long drop sets it off.</summary>
        bool UpdateNitro(int i, int x, int y)
        {
            if (Get(x, y - 1) == El.Empty)
            {
                if (life[i] < 255) life[i]++;
                return false;
            }
            if (life[i] > 12)
            {
                Explode(x, y, E[El.Nitro].ExplodeRadius);
                return true;
            }
            life[i] = 0;
            return false;
        }

        /// <summary>Rises through liquids and pops when it reaches open air.</summary>
        bool UpdateBubble(int i, int x, int y)
        {
            if (y + 1 >= Height) { SetIndex(i, El.Empty); return true; }
            int ux = (Next() & 1) == 0 ? x : x + RandDir();
            if (!InBounds(ux, y + 1)) ux = x;
            int j = (y + 1) * Width + ux;
            byte o = Cells[j];
            if (o == El.Empty)
            {
                if (Rand01() < 0.3f) SetIndex(i, El.Empty); // pop
                return true;
            }
            if (E[o].Kind == Kind.Liquid) { Swap(i, j); return true; }
            return false; // stuck under something: drift like a gas
        }

        /// <summary>
        /// Shoots down up to 4 cells a tick, zig-zagging and leaving a short-lived Spark trail.
        /// Passes through air and gases that don't burn, and strikes the first thing it hits.
        /// </summary>
        void UpdateLightning(int i, int x, int y)
        {
            for (int s = 0; s < 4; s++)
            {
                int nx = x + (int)(Next() % 3) - 1, ny = y - 1;
                if (!InBounds(nx, ny)) { SetIndex(i, El.Spark); return; }
                int j = ny * Width + nx;
                byte o = Cells[j];
                var oe = E[o];
                bool through = o == El.Empty || o == El.Spark || o == El.Lightning || (oe.Kind == Kind.Gas && oe.Flammability <= 0f);
                if (!through)
                {
                    Strike(nx, ny, j, oe);
                    if (Cells[i] == El.Lightning) SetIndex(i, El.Spark);
                    return;
                }
                SetIndex(j, El.Lightning);
                SetIndex(i, El.Spark);
                i = j; x = nx; y = ny;
            }
        }

        /// <summary>Lightning hitting (x, y): electrifies Metal and Salt Water, sets it alight if it burns, and fuses nearby Sand into Glass.</summary>
        void Strike(int x, int y, int i, Element e)
        {
            if (e.Electric) Charge(i);
            if (e.Flammability > 0f) Ignite(x, y, i, e);
            for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
                if (Get(x + dx, y + dy) == El.Sand && dx * dx + dy * dy <= 5) Set(x + dx, y + dy, El.Glass);
        }

        /// <summary>Picks random cells within reach, drags each one a step closer, and deletes the ones touching it.</summary>
        void UpdateBlackHole(int x, int y)
        {
            const int Reach = 14;
            for (int n = 0; n < 10; n++)
            {
                // A random range first, then a cell within it, so the pull is stronger close in.
                int r = 2 + (int)(Next() % (Reach - 1));
                int dx = (int)(Next() % (uint)(2 * r + 1)) - r, dy = (int)(Next() % (uint)(2 * r + 1)) - r;
                int px = x + dx, py = y + dy;
                if (!InBounds(px, py)) continue;
                int j = py * Width + px;
                byte o = Cells[j];
                if (o == El.Empty || E[o].Indestructible) continue;
                if (Mathf.Abs(dx) <= 1 && Mathf.Abs(dy) <= 1) { SetIndex(j, El.Empty); continue; }
                int sx = px - Math.Sign(dx), sy = py - Math.Sign(dy);
                int k = sy * Width + sx;
                if (Cells[k] == El.Empty) Swap(j, k);
            }
        }

        /// <summary>
        /// Sends a powder, liquid or gas touching it to a random Portal Out: out of the bottom for things
        /// that fall, out of the top for gases, or any free side.
        /// </summary>
        void UpdatePortalIn(int x, int y)
        {
            if (exitPortals.Count == 0) return;
            int k = (int)(Next() & 7);
            int nx = x + DX[k], ny = y + DY[k];
            if (!InBounds(nx, ny)) return;
            int j = ny * Width + nx;
            var oe = E[Cells[j]];
            if (oe.Kind == Kind.Empty || oe.Kind == Kind.Solid || oe.Indestructible) return;

            for (int tries = 0; tries < 4; tries++)
            {
                int p = exitPortals[(int)(Next() % (uint)exitPortals.Count)];
                if (Cells[p] != El.PortalOut) continue;
                int side = tries == 0 ? (oe.Kind == Kind.Gas ? 6 : 1) : (int)(Next() & 7); // 1 = below, 6 = above
                int tx = p % Width + DX[side], ty = p / Width + DY[side];
                if (!InBounds(tx, ty)) continue;
                int target = ty * Width + tx;
                if (Cells[target] != El.Empty) continue;
                Swap(j, target);
                return;
            }
        }

        static bool Diggable(byte t) => t == El.Dirt || t == El.Sand || t == El.Mud || t == El.Snow || t == El.Ash || t == El.Salt;

        /// <summary>
        /// Walks left or right (life bit 0 = facing right), steps up ledges, climbs walls, and digs through
        /// soft ground. Falls when there's nothing under it or beside it to hold on to.
        /// </summary>
        void UpdateAnt(int i, int x, int y, Element e)
        {
            if (!Firm(x, y - 1) && !Firm(x - 1, y) && !Firm(x + 1, y))
            {
                MoveTo(i, x, y - 1, e);
                return;
            }
            if (Rand01() > 0.35f) return; // ants stop and start

            int dir = (life[i] & 1) == 0 ? -1 : 1;
            if (Rand01() < 0.01f) dir = -dir;
            int fx = x + dir;
            byte ahead = Get(fx, y);
            int j = -1;

            if (Diggable(Get(x, y - 1)) && Rand01() < 0.01f)
            {
                Set(x, y - 1, El.Empty);                       // dig down
                j = MoveIntoEmpty(i, x, y - 1);
            }
            else if (ahead == El.Empty) j = MoveIntoEmpty(i, fx, y);
            else if (Diggable(ahead) && Rand01() < 0.1f)
            {
                Set(fx, y, El.Empty);                          // dig a tunnel
                j = MoveIntoEmpty(i, fx, y);
            }
            else if (Get(fx, y + 1) == El.Empty) j = MoveIntoEmpty(i, fx, y + 1);              // step up
            else if (Firm(fx, y) && Get(x, y + 1) == El.Empty) j = MoveIntoEmpty(i, x, y + 1); // climb the wall
            else dir = -dir;

            life[j >= 0 ? j : i] = (byte)(dir > 0 ? 1 : 0);
        }

        static bool Soil(byte t) => t == El.Dirt || t == El.Mud || t == El.Sand;

        /// <summary>Wriggles slowly through Dirt, Mud and Sand, or crawls along the surface. Falls through air.</summary>
        void UpdateWorm(int i, int x, int y, Element e)
        {
            if (!Firm(x, y - 1) && MoveTo(i, x, y - 1, e) >= 0) return;
            if (Rand01() > 0.2f) return;
            int k = (int)(Next() & 7);
            int nx = x + DX[k], ny = y + DY[k];
            if (!InBounds(nx, ny)) return;
            int j = ny * Width + nx;
            byte o = Cells[j];
            if (Soil(o)) Swap(i, j);
            else if (o == El.Empty && DY[k] == 0 && Firm(nx, ny - 1)) Swap(i, j);
        }

        /// <summary>
        /// Grows down one cell at a time from any vine with nothing under it. life = length left
        /// (0 = a vine you drew, which can grow 40 cells).
        /// </summary>
        void UpdateVine(int i, int x, int y)
        {
            if (Rand01() > 0.03f) return;
            int len = life[i] == 0 ? 40 : life[i];
            if (len <= 1) return;
            for (int dx = -1; dx <= 1; dx++)
                if (Get(x + dx, y - 1) == El.Vine) return; // already grew
            int nx = x + (int)(Next() % 3) - 1, ny = y - 1;
            if (!InBounds(nx, ny) || Cells[ny * Width + nx] != El.Empty) return;
            int j = ny * Width + nx;
            SetIndex(j, El.Vine);
            life[j] = (byte)(len - 1);
        }

        /// <summary>The tip of a sprouting plant: climbs up into empty cells leaving Plant behind, until its life runs out.</summary>
        void UpdateBud(int i, int x, int y)
        {
            if (Rand01() > 0.1f) return;
            int nx = x + ((Next() & 3) == 0 ? RandDir() : 0), ny = y + 1;
            if (!InBounds(nx, ny) || Cells[ny * Width + nx] != El.Empty) return;
            int j = ny * Width + nx;
            byte left = life[i];
            SetIndex(j, El.Bud);
            life[j] = left;
            SetIndex(i, El.Plant);
        }

        /// <summary>
        /// Free-falling grains (nothing under them) sometimes drift diagonally instead of straight down,
        /// so falling streams fan out into loose grains. Piles aren't affected.
        /// </summary>
        bool Scatter(int i, int x, int y, int d, Element e)
        {
            if (e.Scatter <= 0f || Get(x, y - 1) != El.Empty || Rand01() >= e.Scatter) return false;
            return TryMove(i, x + d, y - 1, e);
        }

        bool TryMove(int i, int nx, int ny, Element e) => MoveTo(i, nx, ny, e) >= 0;

        /// <summary>
        /// Moves cell i to (nx, ny) if that's empty, or a lighter liquid or gas it can sink through.
        /// Returns the cell's new index, or -1 if it couldn't move.
        /// </summary>
        int MoveTo(int i, int nx, int ny, Element e)
        {
            if (!InBounds(nx, ny)) return -1;
            int j = ny * Width + nx;
            byte o = Cells[j];
            if (o != El.Empty)
            {
                if (e.Kind == Kind.Gas) return -1;
                var oe = E[o];
                if ((oe.Kind != Kind.Liquid && oe.Kind != Kind.Gas) || oe.Density >= e.Density) return -1;
            }
            Swap(i, j);
            return j;
        }

        /// <summary>Moves cell i to (nx, ny) only if that cell is empty. Returns the new index, or -1.</summary>
        int MoveIntoEmpty(int i, int nx, int ny)
        {
            if (!InBounds(nx, ny)) return -1;
            int j = ny * Width + nx;
            if (Cells[j] != El.Empty) return -1;
            Swap(i, j);
            return j;
        }

        /// <summary>Something to stand on or hold on to: a solid or a powder (out of bounds counts as wall).</summary>
        bool Firm(int x, int y)
        {
            var k = E[Get(x, y)].Kind;
            return k == Kind.Solid || k == Kind.Powder;
        }

        /// <summary>Slides sideways through empty cells, up to `spread` cells.</summary>
        bool Slide(int i, int x, int y, int dir, int spread)
        {
            int best = -1;
            for (int s = 1; s <= spread; s++)
            {
                int nx = x + dir * s;
                if (!InBounds(nx, y) || Cells[y * Width + nx] != El.Empty) break;
                best = nx;
            }
            if (best < 0) return false;
            Swap(i, y * Width + best);
            return true;
        }

        void Swap(int a, int b)
        {
            (Cells[a], Cells[b]) = (Cells[b], Cells[a]);
            (life[a], life[b]) = (life[b], life[a]);
            (shade[a], shade[b]) = (shade[b], shade[a]);
            (Temp[a], Temp[b]) = (Temp[b], Temp[a]);
            (charged[a], charged[b]) = (charged[b], charged[a]);
            (chargeWave[a], chargeWave[b]) = (chargeWave[b], chargeWave[a]);
            stamp[a] = tick;
            stamp[b] = tick;
        }

        /// <summary>
        /// Turns cell i into `type`. Something appearing in empty space starts at its own Temperature; something
        /// changing into another element keeps its heat (Molten Metal sets into hot Metal), within the new one's range.
        /// </summary>
        void SetIndex(int i, byte type)
        {
            var e = E[type];
            if (Cells[i] == El.Empty) Temp[i] = e.Temperature;
            else if (type != El.Empty) Temp[i] = FitTemp(e, Temp[i]);
            Cells[i] = type;
            charged[i] = NoCharge;
            chargeWave[i] = 0;
            life[i] = e.LifeMax > 0 ? (byte)(e.LifeMin + (int)(Next() % (uint)(Mathf.Max(1, e.LifeMax - e.LifeMin + 1)))) : (byte)0;
            shade[i] = (byte)(Next() & 15);
            stamp[i] = tick;
        }

        // ------------------------------------------------------------------ random (xorshift, fast)

        uint Next()
        {
            rng ^= rng << 13;
            rng ^= rng >> 17;
            rng ^= rng << 5;
            return rng;
        }

        float Rand01() => (Next() & 0xFFFFFF) / 16777216f;
        int RandDir() => (Next() & 1) == 0 ? -1 : 1;

        // ------------------------------------------------------------------ save / load

        public void Save(string path)
        {
            using var w = new BinaryWriter(File.Create(path));
            w.Write(Width);
            w.Write(Height);
            w.Write(Cells);
        }

        /// <summary>Loads a save. If the grid size changed, the bottom rows are kept, centred sideways.</summary>
        public bool Load(string path)
        {
            if (!File.Exists(path)) return false;
            using var r = new BinaryReader(File.OpenRead(path));
            int w = r.ReadInt32(), h = r.ReadInt32();
            byte[] data = r.ReadBytes(w * h);
            Import(data, null, null, w, h);
            return true;
        }

        /// <summary>Copies another world into this one (used when the screen changes shape).</summary>
        public void CopyFrom(Simulation other) => Import(other.Cells, other.life, other.Temp, other.Width, other.Height);

        /// <summary>Replaces the world with a w x h grid of cells: bottom rows kept, centred sideways.</summary>
        void Import(byte[] data, byte[] lifeData, float[] tempData, int w, int h)
        {
            Clear();
            int ox = (Width - w) / 2;
            for (int y = 0; y < Mathf.Min(h, Height); y++)
            for (int x = 0; x < w; x++)
            {
                int tx = x + ox;
                if ((uint)tx >= (uint)Width) continue;
                int src = y * w + x;
                if (src >= data.Length) return; // truncated save
                int i = y * Width + tx;
                if (tempData != null) Temp[i] = tempData[src];
                byte t = data[src];
                if (t == El.Empty || E[t] == null) continue;
                SetIndex(i, t);
                if (lifeData != null) life[i] = lifeData[src];
                if (tempData != null) Temp[i] = tempData[src];
            }
        }
    }
}
