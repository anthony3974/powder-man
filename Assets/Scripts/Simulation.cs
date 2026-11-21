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
        readonly byte[] life;   // remaining ticks for things that decay; for Cloner, the element it copies
        readonly byte[] shade;  // 0..15, picks a colour variant
        readonly int[] stamp;   // tick number a cell last moved/changed, so nothing moves twice per tick
        int tick;
        uint rng = 2463534242u;

        static readonly Element[] E = ElementDatabase.Table;
        static readonly List<Reaction>[] R = ElementDatabase.Reactions;
        static readonly int[] DX = { -1, 0, 1, -1, 1, -1, 0, 1 };
        static readonly int[] DY = { -1, -1, -1, 0, 0, 1, 1, 1 };

        public Simulation(int width, int height)
        {
            Width = width;
            Height = height;
            int n = width * height;
            Cells = new byte[n];
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
                if (type == El.Empty) { Cells[i] = El.Empty; continue; }
                if (!replace && Cells[i] != El.Empty) continue;
                if (fill < 1f && Rand01() > fill) continue;
                SetIndex(i, type);
            }
        }

        // ------------------------------------------------------------------ update

        public void Step()
        {
            tick++;
            for (int y = 0; y < Height; y++)
            {
                if ((Next() & 1) == 0)
                    for (int x = 0; x < Width; x++) UpdateCell(x, y);
                else
                    for (int x = Width - 1; x >= 0; x--) UpdateCell(x, y);
            }
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
                    SetIndex(i, e.DecaysInto != El.Empty && Rand01() < e.DecayChance ? e.DecaysInto : El.Empty);
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

        /// <summary>
        /// Free-falling grains (nothing under them) sometimes drift diagonally instead of straight down,
        /// so falling streams fan out into loose grains. Piles aren't affected.
        /// </summary>
        bool Scatter(int i, int x, int y, int d, Element e)
        {
            if (e.Scatter <= 0f || Get(x, y - 1) != El.Empty || Rand01() >= e.Scatter) return false;
            return TryMove(i, x + d, y - 1, e);
        }

        bool TryMove(int i, int nx, int ny, Element e)
        {
            if (!InBounds(nx, ny)) return false;
            int j = ny * Width + nx;
            byte o = Cells[j];
            if (o != El.Empty)
            {
                if (e.Kind == Kind.Gas) return false;
                var oe = E[o];
                if ((oe.Kind != Kind.Liquid && oe.Kind != Kind.Gas) || oe.Density >= e.Density) return false;
            }
            Swap(i, j);
            return true;
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
            stamp[a] = tick;
            stamp[b] = tick;
        }

        void SetIndex(int i, byte type)
        {
            var e = E[type];
            Cells[i] = type;
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
            Import(data, null, w, h);
            return true;
        }

        /// <summary>Copies another world into this one (used when the screen changes shape).</summary>
        public void CopyFrom(Simulation other) => Import(other.Cells, other.life, other.Width, other.Height);

        /// <summary>Replaces the world with a w x h grid of cells: bottom rows kept, centred sideways.</summary>
        void Import(byte[] data, byte[] lifeData, int w, int h)
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
                byte t = data[src];
                if (t == El.Empty || E[t] == null) continue;
                int i = y * Width + tx;
                SetIndex(i, t);
                if (lifeData != null) life[i] = lifeData[src];
            }
        }
    }
}
