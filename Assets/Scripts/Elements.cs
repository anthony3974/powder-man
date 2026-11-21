using System.Collections.Generic;
using UnityEngine;

namespace PowderMan
{
    // =====================================================================================
    //  THIS IS THE FILE TO EDIT TO CHANGE THE GAME.
    //
    //  To add a new element:
    //    1. Give it an id in El below (any unused number from 1 to 254).
    //    2. Add it in ElementDatabase.DefineElements() with Add(El.MyThing, new Element { ... }).
    //       Element picker buttons appear in the order you Add them.
    //    3. Optionally add reactions in DefineReactions() with React(...).
    // =====================================================================================

    /// <summary>Element ids. Each cell of the world stores one of these bytes.</summary>
    public static class El
    {
        public const byte Empty = 0;
        public const byte Wall = 1;
        public const byte Sand = 2;
        public const byte Water = 3;
        public const byte Stone = 4;
        public const byte Wood = 5;
        public const byte Fire = 6;
        public const byte Smoke = 7;
        public const byte Steam = 8;
        public const byte Oil = 9;
        public const byte Lava = 10;
        public const byte Ice = 11;
        public const byte Snow = 12;
        public const byte Plant = 13;
        public const byte Acid = 14;
        public const byte Gunpowder = 15;
        public const byte Tnt = 16;
        public const byte Glass = 17;
        public const byte Cloner = 18;
        public const byte Void = 19;
        public const byte Ember = 20;

        /// <summary>Wildcard for reactions: matches any element except Empty, itself and Indestructible ones.</summary>
        public const byte Any = 255;
    }

    public enum Kind
    {
        Empty,
        Solid,   // never moves
        Powder,  // falls and piles up
        Liquid,  // falls and flows sideways
        Gas,     // rises and drifts
    }

    public class Element
    {
        public string Name;
        public Kind Kind;

        /// <summary>Main colour. Each cell gets a random shade between Color and Color2.</summary>
        public Color32 Color;
        /// <summary>Second shade. Leave unset to use a slightly darker Color.</summary>
        public Color32 Color2;

        /// <summary>Heavier powders and liquids sink through lighter liquids and gases.</summary>
        public int Density;
        /// <summary>Liquids and gases: how many cells they can slide sideways per tick.</summary>
        public int Spread = 3;
        /// <summary>Chance per tick to move at all. Below 1 makes it sluggish (lava).</summary>
        public float MoveChance = 1f;

        /// <summary>0 = cold. Above 0 sets fire to flammable neighbours: chance per tick = Heat x their Flammability.</summary>
        public float Heat;
        /// <summary>How easily it catches fire next to something with Heat. 0 = never burns.</summary>
        public float Flammability;
        /// <summary>What it turns into when it catches fire.</summary>
        public byte BurnsInto = El.Fire;
        /// <summary>If above 0, catching fire makes an explosion of this radius instead.</summary>
        public int ExplodeRadius;

        /// <summary>Spawns this into empty neighbouring cells (with EmitChance per tick). Empty = off.</summary>
        public byte Emits = El.Empty;
        public float EmitChance;

        /// <summary>If LifeMax is above 0, the cell disappears after LifeMin..LifeMax ticks (max 255).</summary>
        public int LifeMin, LifeMax;
        /// <summary>When its life runs out it becomes this (with DecayChance), otherwise Empty.</summary>
        public byte DecaysInto = El.Empty;
        public float DecayChance = 1f;

        /// <summary>Re-pick the shade every tick (fire).</summary>
        public bool Flicker;
        /// <summary>Explosions, acid and the void can't destroy it.</summary>
        public bool Indestructible;
        public bool HiddenFromToolbar;
    }

    public struct Reaction
    {
        public byte Other;        // the neighbour it reacts with (or El.Any)
        public byte SelfBecomes;  // what this cell turns into
        public byte OtherBecomes; // what the neighbour turns into
        public float Chance;      // chance per tick while touching
    }

    public static class ElementDatabase
    {
        public static readonly Element[] Table = new Element[256];
        public static readonly List<Reaction>[] Reactions = new List<Reaction>[256];
        public static readonly List<byte> Toolbar = new List<byte>();

        static void DefineElements()
        {
            Add(El.Empty, new Element { Name = "Empty", Kind = Kind.Empty, Color = Hex("#11131b"), Color2 = Hex("#11131b"), HiddenFromToolbar = true });

            Add(El.Wall, new Element { Name = "Wall", Kind = Kind.Solid, Color = Hex("#6b6f7d"), Color2 = Hex("#5c606d"), Indestructible = true });
            Add(El.Sand, new Element { Name = "Sand", Kind = Kind.Powder, Color = Hex("#e6c983"), Color2 = Hex("#c9a764"), Density = 16 });
            Add(El.Water, new Element { Name = "Water", Kind = Kind.Liquid, Color = Hex("#3d7fe0"), Color2 = Hex("#3572cc"), Density = 10, Spread = 5 });
            Add(El.Stone, new Element { Name = "Stone", Kind = Kind.Solid, Color = Hex("#8a8580"), Color2 = Hex("#6e6a66") });
            Add(El.Wood, new Element { Name = "Wood", Kind = Kind.Solid, Color = Hex("#8a5a33"), Color2 = Hex("#704626"), Flammability = 0.08f, BurnsInto = El.Ember });
            Add(El.Fire, new Element
            {
                Name = "Fire", Kind = Kind.Gas, Color = Hex("#ffd34d"), Color2 = Hex("#ff5a1f"), Density = 1, Spread = 1,
                Heat = 1f, LifeMin = 8, LifeMax = 26, DecaysInto = El.Smoke, DecayChance = 0.25f, Flicker = true,
            });
            // Ember: burning wood/plant. Stays put, glows, throws off fire and slowly spreads, then crumbles.
            Add(El.Ember, new Element
            {
                Name = "Ember", Kind = Kind.Solid, Color = Hex("#ff8a2a"), Color2 = Hex("#a8240c"), Heat = 0.2f, Flicker = true,
                LifeMin = 60, LifeMax = 180, DecaysInto = El.Smoke, DecayChance = 0.3f, Emits = El.Fire, EmitChance = 0.15f,
            });
            Add(El.Oil, new Element { Name = "Oil", Kind = Kind.Liquid, Color = Hex("#4a3a2a"), Color2 = Hex("#3b2e21"), Density = 8, Spread = 3, Flammability = 0.4f });
            Add(El.Lava, new Element
            {
                Name = "Lava", Kind = Kind.Liquid, Color = Hex("#ff6a1a"), Color2 = Hex("#d6360f"), Density = 14, Spread = 1,
                MoveChance = 0.35f, Heat = 1f, Flicker = true,
            });
            Add(El.Ice, new Element { Name = "Ice", Kind = Kind.Solid, Color = Hex("#bfe8ff"), Color2 = Hex("#a3d6f5") });
            Add(El.Snow, new Element { Name = "Snow", Kind = Kind.Powder, Color = Hex("#f4f8ff"), Color2 = Hex("#dfe8f5"), Density = 6 });
            Add(El.Plant, new Element { Name = "Plant", Kind = Kind.Solid, Color = Hex("#3fbf4a"), Color2 = Hex("#2e9a39"), Flammability = 0.12f, BurnsInto = El.Ember });
            Add(El.Acid, new Element { Name = "Acid", Kind = Kind.Liquid, Color = Hex("#a6f53a"), Color2 = Hex("#84d61f"), Density = 11, Spread = 4 });
            Add(El.Gunpowder, new Element { Name = "Gunpowder", Kind = Kind.Powder, Color = Hex("#4d4d55"), Color2 = Hex("#2f2f36"), Density = 15, Flammability = 0.9f, ExplodeRadius = 3 });
            Add(El.Tnt, new Element { Name = "TNT", Kind = Kind.Solid, Color = Hex("#d93a3a"), Color2 = Hex("#b52b2b"), Flammability = 0.9f, ExplodeRadius = 8 });
            Add(El.Glass, new Element { Name = "Glass", Kind = Kind.Solid, Color = Hex("#d9f2ee"), Color2 = Hex("#b8ddd8") });
            Add(El.Steam, new Element
            {
                Name = "Steam", Kind = Kind.Gas, Color = Hex("#c8d3e0"), Color2 = Hex("#aab6c4"), Density = 1, Spread = 4,
                LifeMin = 90, LifeMax = 220, DecaysInto = El.Water, DecayChance = 0.4f,
            });
            Add(El.Smoke, new Element
            {
                Name = "Smoke", Kind = Kind.Gas, Color = Hex("#4a4a52"), Color2 = Hex("#3a3a40"), Density = 2, Spread = 3,
                LifeMin = 40, LifeMax = 100,
            });
            // Cloner: grabs the first thing that touches it and keeps spawning copies of it.
            Add(El.Cloner, new Element { Name = "Cloner", Kind = Kind.Solid, Color = Hex("#d4a72c"), Color2 = Hex("#b88d1c"), Indestructible = true });
            Add(El.Void, new Element { Name = "Void", Kind = Kind.Solid, Color = Hex("#2a0f3d"), Color2 = Hex("#1c0829"), Indestructible = true });
        }

        static void DefineReactions()
        {
            // React(a, b, aBecomes, bBecomes, chance) works both ways round, so you only write it once.
            // Burning is automatic (Heat + Flammability), you don't need reactions for it.

            React(El.Fire, El.Water, El.Empty, El.Water, 0.5f);   // water puts fire out
            React(El.Fire, El.Water, El.Empty, El.Steam, 0.1f);   // ...and sometimes boils
            React(El.Fire, El.Ice, El.Fire, El.Water, 0.1f);
            React(El.Fire, El.Snow, El.Empty, El.Water, 0.4f);
            React(El.Ember, El.Water, El.Wood, El.Steam, 0.3f);  // water puts burning wood out

            React(El.Lava, El.Water, El.Stone, El.Steam, 0.5f);
            React(El.Lava, El.Ice, El.Lava, El.Water, 0.2f);
            React(El.Lava, El.Snow, El.Lava, El.Steam, 0.4f);
            React(El.Lava, El.Sand, El.Lava, El.Glass, 0.02f);
            React(El.Lava, El.Stone, El.Lava, El.Lava, 0.001f);   // very slowly melts stone

            React(El.Ice, El.Water, El.Ice, El.Ice, 0.003f);      // freezes nearby water
            React(El.Steam, El.Ice, El.Water, El.Ice, 0.05f);
            React(El.Snow, El.Water, El.Water, El.Water, 0.02f);

            React(El.Plant, El.Water, El.Plant, El.Plant, 0.03f); // plants drink water and grow

            React(El.Acid, El.Glass, El.Acid, El.Glass, 1f);      // glass is acid-proof (listed before Any so it wins)
            React(El.Acid, El.Water, El.Water, El.Water, 0.005f); // gets diluted
            React(El.Acid, El.Any, El.Acid, El.Empty, 0.05f);     // eats things
            React(El.Acid, El.Any, El.Empty, El.Empty, 0.02f);    // and gets used up

            React(El.Void, El.Any, El.Void, El.Empty, 1f);        // deletes whatever touches it
        }

        // ---------------------------------------------------------------------------------

        static ElementDatabase()
        {
            DefineElements();
            DefineReactions();
        }

        static void Add(byte id, Element e)
        {
            if (Table[id] != null) Debug.LogError($"Element id {id} is used twice ({Table[id].Name} and {e.Name}).");
            if (e.Color2.a == 0)
            {
                var c = (Color)e.Color;
                e.Color2 = (Color32)new Color(c.r * 0.85f, c.g * 0.85f, c.b * 0.85f, 1f);
            }
            e.LifeMin = Mathf.Clamp(e.LifeMin, 1, 255);
            e.LifeMax = Mathf.Clamp(e.LifeMax, 0, 255);
            Table[id] = e;
            if (!e.HiddenFromToolbar) Toolbar.Add(id);
        }

        static void React(byte a, byte b, byte aBecomes, byte bBecomes, float chance)
        {
            AddReaction(a, new Reaction { Other = b, SelfBecomes = aBecomes, OtherBecomes = bBecomes, Chance = chance });
            if (b != El.Any && b != a)
                AddReaction(b, new Reaction { Other = a, SelfBecomes = bBecomes, OtherBecomes = aBecomes, Chance = chance });
        }

        static void AddReaction(byte id, Reaction r)
        {
            (Reactions[id] ??= new List<Reaction>()).Add(r);
        }

        static Color32 Hex(string html)
        {
            return ColorUtility.TryParseHtmlString(html, out var c) ? (Color32)c : new Color32(255, 0, 255, 255);
        }
    }
}
