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

        // Earth
        public const byte Dirt = 21;
        public const byte Mud = 22;
        public const byte Clay = 23;
        public const byte Brick = 24;
        public const byte Salt = 25;
        public const byte SaltWater = 26;
        public const byte Ash = 27;
        public const byte Obsidian = 28;

        // Metals and heat
        public const byte Metal = 29;
        public const byte MoltenMetal = 30;
        public const byte Rust = 31;
        public const byte Coal = 32;
        public const byte BurningCoal = 33;
        public const byte Thermite = 34;
        public const byte BurningThermite = 35;
        public const byte MoltenGlass = 36;

        // Living things
        public const byte Seed = 37;
        public const byte Bud = 38;
        public const byte Vine = 39;
        public const byte Moss = 40;
        public const byte Fungus = 41;
        public const byte Ant = 42;
        public const byte Worm = 43;

        // Gases and weather
        public const byte Cloud = 44;
        public const byte Lightning = 45;
        public const byte Spark = 46;
        public const byte Methane = 47;
        public const byte Hydrogen = 48;
        public const byte PoisonGas = 49;
        public const byte Bubble = 50;

        // Odd and fun
        public const byte Nitro = 51;
        public const byte Sponge = 52;
        public const byte WetSponge = 53;
        public const byte Ball = 54;
        public const byte Antimatter = 55;
        public const byte BlackHole = 56;
        public const byte PortalIn = 57;
        public const byte PortalOut = 58;

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
        /// <summary>Powders and liquids: chance per tick to drift sideways while falling through air. 0 = falls straight.</summary>
        public float Scatter = 0.15f;

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
        /// <summary>When its life runs out it becomes DecaysInto (with DecayChance), otherwise DecaysElse.</summary>
        public byte DecaysInto = El.Empty;
        public float DecayChance = 1f;
        /// <summary>
        /// What it becomes when DecayChance fails. Set it to the element itself to make something that lasts
        /// longer than 255 ticks: it starts a fresh life each time until DecayChance comes up.
        /// </summary>
        public byte DecaysElse = El.Empty;

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
                LifeMin = 60, LifeMax = 180, DecaysInto = El.Ash, DecayChance = 0.4f, DecaysElse = El.Smoke,
                Emits = El.Fire, EmitChance = 0.15f,
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

            // ---- Earth
            Add(El.Dirt, new Element { Name = "Dirt", Kind = Kind.Powder, Color = Hex("#7a5534"), Color2 = Hex("#5e4027"), Density = 15, Scatter = 0.08f });
            Add(El.Mud, new Element
            {
                Name = "Mud", Kind = Kind.Powder, Color = Hex("#4f3a26"), Color2 = Hex("#3d2c1c"), Density = 17, MoveChance = 0.4f,
                Scatter = 0.03f, Emits = El.Methane, EmitChance = 0.0002f,
            });
            Add(El.Clay, new Element { Name = "Clay", Kind = Kind.Solid, Color = Hex("#b87d5b"), Color2 = Hex("#a06a4a") });
            Add(El.Brick, new Element { Name = "Brick", Kind = Kind.Solid, Color = Hex("#a8432f"), Color2 = Hex("#8a3424") });
            Add(El.Salt, new Element { Name = "Salt", Kind = Kind.Powder, Color = Hex("#f2f0ea"), Color2 = Hex("#d8d4cb"), Density = 17 });
            Add(El.SaltWater, new Element { Name = "Salt Water", Kind = Kind.Liquid, Color = Hex("#3a9cc4"), Color2 = Hex("#318aad"), Density = 11, Spread = 5 });
            Add(El.Ash, new Element { Name = "Ash", Kind = Kind.Powder, Color = Hex("#9a9a9e"), Color2 = Hex("#7c7c82"), Density = 5, Scatter = 0.4f });
            Add(El.Obsidian, new Element { Name = "Obsidian", Kind = Kind.Solid, Color = Hex("#2b2238"), Color2 = Hex("#1a1424") });

            // ---- Metals and heat
            Add(El.Metal, new Element { Name = "Metal", Kind = Kind.Solid, Color = Hex("#a9b3c2"), Color2 = Hex("#8a94a3") });
            Add(El.MoltenMetal, new Element
            {
                Name = "Molten Metal", Kind = Kind.Liquid, Color = Hex("#ffe08a"), Color2 = Hex("#ff9a3c"), Density = 20, Spread = 2,
                MoveChance = 0.5f, Heat = 0.6f, Flicker = true,
                LifeMin = 200, LifeMax = 255, DecaysInto = El.Metal, DecayChance = 0.3f, DecaysElse = El.MoltenMetal,
            });
            Add(El.Rust, new Element { Name = "Rust", Kind = Kind.Powder, Color = Hex("#a5532a"), Color2 = Hex("#7d3d1d"), Density = 14 });
            Add(El.Coal, new Element { Name = "Coal", Kind = Kind.Powder, Color = Hex("#2a2a2e"), Color2 = Hex("#1a1a1d"), Density = 14, Flammability = 0.04f, BurnsInto = El.BurningCoal });
            Add(El.BurningCoal, new Element
            {
                Name = "Burning Coal", Kind = Kind.Powder, Color = Hex("#ff7a2a"), Color2 = Hex("#7a1e0c"), Density = 14, Heat = 0.4f,
                Flicker = true, Emits = El.Fire, EmitChance = 0.05f, HiddenFromToolbar = true,
                LifeMin = 200, LifeMax = 255, DecaysInto = El.Ash, DecayChance = 0.15f, DecaysElse = El.BurningCoal,
            });
            Add(El.Thermite, new Element { Name = "Thermite", Kind = Kind.Powder, Color = Hex("#8c6f6a"), Color2 = Hex("#6e5450"), Density = 18, Flammability = 0.15f, BurnsInto = El.BurningThermite });
            Add(El.BurningThermite, new Element
            {
                Name = "Burning Thermite", Kind = Kind.Powder, Color = Hex("#ffffff"), Color2 = Hex("#ffb347"), Density = 18, Heat = 1f,
                Flicker = true, Emits = El.Fire, EmitChance = 0.2f, HiddenFromToolbar = true,
                LifeMin = 80, LifeMax = 160, DecaysInto = El.MoltenMetal, DecayChance = 0.4f,
            });
            Add(El.MoltenGlass, new Element
            {
                Name = "Molten Glass", Kind = Kind.Liquid, Color = Hex("#ffcf8a"), Color2 = Hex("#f29b4c"), Density = 15, Spread = 1,
                MoveChance = 0.3f, Heat = 0.3f, Flicker = true,
                LifeMin = 200, LifeMax = 255, DecaysInto = El.Glass, DecayChance = 0.35f, DecaysElse = El.MoltenGlass,
            });

            // ---- Living things
            Add(El.Seed, new Element { Name = "Seed", Kind = Kind.Powder, Color = Hex("#c9a25a"), Color2 = Hex("#9c7a3c"), Density = 12, Flammability = 0.2f });
            // Bud: the growing tip of a sprouted seed. Climbs upward leaving Plant behind, then turns into Plant.
            Add(El.Bud, new Element
            {
                Name = "Bud", Kind = Kind.Solid, Color = Hex("#9df07a"), Color2 = Hex("#7ad65a"), Flammability = 0.12f, BurnsInto = El.Ember,
                LifeMin = 60, LifeMax = 200, DecaysInto = El.Plant, HiddenFromToolbar = true,
            });
            Add(El.Vine, new Element { Name = "Vine", Kind = Kind.Solid, Color = Hex("#2f7d32"), Color2 = Hex("#225c25"), Flammability = 0.35f });
            Add(El.Moss, new Element { Name = "Moss", Kind = Kind.Solid, Color = Hex("#5f8f3a"), Color2 = Hex("#4a7230"), Flammability = 0.1f });
            Add(El.Fungus, new Element
            {
                Name = "Fungus", Kind = Kind.Solid, Color = Hex("#c7b6d9"), Color2 = Hex("#a28fb8"), Flammability = 0.1f,
                Emits = El.Methane, EmitChance = 0.0005f,
            });
            Add(El.Ant, new Element { Name = "Ant", Kind = Kind.Powder, Color = Hex("#3a1f14"), Color2 = Hex("#1f100a"), Density = 12, Flammability = 0.5f });
            Add(El.Worm, new Element { Name = "Worm", Kind = Kind.Powder, Color = Hex("#e08a9a"), Color2 = Hex("#c46f80"), Density = 12, Flammability = 0.3f });

            // ---- Gases and weather
            // Cloud: lasts a couple of minutes (a fresh life each time DecayChance fails) and rains as it goes.
            Add(El.Cloud, new Element
            {
                Name = "Cloud", Kind = Kind.Gas, Color = Hex("#e3e8ef"), Color2 = Hex("#c4ccd8"), Density = 1, Spread = 1, MoveChance = 0.08f,
                Emits = El.Water, EmitChance = 0.002f,
                LifeMin = 200, LifeMax = 255, DecaysInto = El.Empty, DecayChance = 0.03f, DecaysElse = El.Cloud,
            });
            Add(El.Lightning, new Element { Name = "Lightning", Kind = Kind.Gas, Color = Hex("#fffbe0"), Color2 = Hex("#d9e8ff"), Flicker = true });
            Add(El.Spark, new Element
            {
                Name = "Spark", Kind = Kind.Solid, Color = Hex("#ffffff"), Color2 = Hex("#bcd4ff"), Heat = 1f, Flicker = true,
                LifeMin = 2, LifeMax = 6, HiddenFromToolbar = true,
            });
            Add(El.Methane, new Element { Name = "Methane", Kind = Kind.Gas, Color = Hex("#3b4a3a"), Color2 = Hex("#2f3b2e"), Density = 1, Spread = 3, Flammability = 0.6f, ExplodeRadius = 2 });
            Add(El.Hydrogen, new Element { Name = "Hydrogen", Kind = Kind.Gas, Color = Hex("#2f3a52"), Color2 = Hex("#273047"), Density = 0, Spread = 6, Flammability = 1f, ExplodeRadius = 2 });
            Add(El.PoisonGas, new Element
            {
                Name = "Poison Gas", Kind = Kind.Gas, Color = Hex("#8fbf2a"), Color2 = Hex("#6e9a1c"), Density = 3, Spread = 3,
                LifeMin = 200, LifeMax = 255, DecaysInto = El.Empty, DecayChance = 0.3f, DecaysElse = El.PoisonGas,
            });
            Add(El.Bubble, new Element { Name = "Bubble", Kind = Kind.Gas, Color = Hex("#cfefff"), Color2 = Hex("#a9dcf5"), Density = 1, Spread = 1, LifeMin = 120, LifeMax = 255 });

            // ---- Odd and fun
            Add(El.Nitro, new Element { Name = "Nitro", Kind = Kind.Liquid, Color = Hex("#e9f27a"), Color2 = Hex("#cfd95e"), Density = 9, Spread = 3, Flammability = 0.9f, ExplodeRadius = 6 });
            Add(El.Sponge, new Element { Name = "Sponge", Kind = Kind.Solid, Color = Hex("#f2d64b"), Color2 = Hex("#d9bc33"), Flammability = 0.05f });
            Add(El.WetSponge, new Element { Name = "Wet Sponge", Kind = Kind.Solid, Color = Hex("#b5a23a"), Color2 = Hex("#978628"), HiddenFromToolbar = true });
            Add(El.Ball, new Element { Name = "Bouncy Ball", Kind = Kind.Powder, Color = Hex("#ff4f8b"), Color2 = Hex("#e0306d"), Density = 14, Scatter = 0f });
            Add(El.Antimatter, new Element { Name = "Antimatter", Kind = Kind.Powder, Color = Hex("#e6b3ff"), Color2 = Hex("#a64dff"), Density = 10, Flicker = true });
            Add(El.BlackHole, new Element { Name = "Black Hole", Kind = Kind.Solid, Color = Hex("#000000"), Color2 = Hex("#1a0d24"), Indestructible = true });
            Add(El.PortalIn, new Element { Name = "Portal In", Kind = Kind.Solid, Color = Hex("#2f8cff"), Color2 = Hex("#1a5fd1"), Flicker = true, Indestructible = true });
            Add(El.PortalOut, new Element { Name = "Portal Out", Kind = Kind.Solid, Color = Hex("#ff8c1a"), Color2 = Hex("#d1600a"), Flicker = true, Indestructible = true });
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

            // ---- Earth
            React(El.Water, El.Dirt, El.Empty, El.Mud, 0.05f);    // dirt soaks up water
            React(El.Fire, El.Mud, El.Fire, El.Dirt, 0.1f);       // heat dries mud
            React(El.Lava, El.Mud, El.Lava, El.Dirt, 0.2f);
            React(El.Ember, El.Mud, El.Ember, El.Dirt, 0.05f);
            React(El.Fire, El.Clay, El.Fire, El.Brick, 0.05f);    // fire bakes clay
            React(El.Lava, El.Clay, El.Lava, El.Brick, 0.1f);
            React(El.Ember, El.Clay, El.Ember, El.Brick, 0.03f);
            React(El.Water, El.Clay, El.Water, El.Mud, 0.001f);   // and water slowly softens it
            React(El.Salt, El.Ice, El.Salt, El.Water, 0.02f);     // salt melts ice and snow
            React(El.Salt, El.Snow, El.Salt, El.Water, 0.05f);
            React(El.Salt, El.Water, El.Empty, El.SaltWater, 0.05f);
            React(El.Fire, El.SaltWater, El.Empty, El.SaltWater, 0.5f);
            React(El.Fire, El.SaltWater, El.Empty, El.Salt, 0.1f);       // boils away, leaving salt
            React(El.Ember, El.SaltWater, El.Wood, El.Salt, 0.3f);
            React(El.Lava, El.SaltWater, El.Stone, El.Salt, 0.5f);
            React(El.Ice, El.SaltWater, El.Water, El.SaltWater, 0.01f);  // salt water melts ice instead of freezing
            React(El.Lava, El.Ice, El.Obsidian, El.Water, 0.1f);         // lava cooled by ice sets as obsidian
            React(El.Plant, El.Mud, El.Bud, El.Dirt, 0.003f);            // plants on wet soil keep sprouting

            // ---- Metals and heat
            React(El.Lava, El.Metal, El.Lava, El.MoltenMetal, 0.005f);
            React(El.MoltenMetal, El.Water, El.Metal, El.Steam, 0.5f);
            React(El.MoltenMetal, El.Ice, El.Metal, El.Water, 0.3f);
            React(El.Water, El.Metal, El.Water, El.Rust, 0.0005f);
            React(El.SaltWater, El.Metal, El.SaltWater, El.Rust, 0.003f);
            React(El.BurningCoal, El.Water, El.Coal, El.Steam, 0.3f);
            React(El.BurningCoal, El.SaltWater, El.Coal, El.Steam, 0.3f);
            React(El.BurningThermite, El.Metal, El.BurningThermite, El.MoltenMetal, 0.3f);
            React(El.BurningThermite, El.Stone, El.BurningThermite, El.Lava, 0.1f);
            React(El.BurningThermite, El.Brick, El.BurningThermite, El.Lava, 0.1f);
            React(El.BurningThermite, El.Obsidian, El.BurningThermite, El.Lava, 0.05f);
            React(El.BurningThermite, El.Glass, El.BurningThermite, El.MoltenGlass, 0.2f);
            React(El.BurningThermite, El.Ice, El.BurningThermite, El.Steam, 0.5f);
            React(El.BurningThermite, El.Water, El.BurningThermite, El.Steam, 0.5f);
            React(El.Lava, El.Glass, El.Lava, El.MoltenGlass, 0.01f);
            React(El.MoltenGlass, El.Water, El.Glass, El.Steam, 0.5f);

            // ---- Living things
            React(El.Seed, El.Mud, El.Bud, El.Mud, 0.03f);        // seeds sprout fast on wet soil
            React(El.Seed, El.Dirt, El.Bud, El.Dirt, 0.004f);     // and slowly on dry soil
            React(El.Moss, El.Stone, El.Moss, El.Moss, 0.003f);   // moss creeps over stone
            React(El.Water, El.Stone, El.Water, El.Moss, 0.0002f);
            React(El.Fungus, El.Wood, El.Fungus, El.Fungus, 0.01f);
            React(El.Fungus, El.Plant, El.Fungus, El.Fungus, 0.01f);
            React(El.Ant, El.Water, El.Empty, El.Water, 0.02f);   // ants drown
            React(El.Ant, El.SaltWater, El.Empty, El.SaltWater, 0.02f);
            React(El.Ant, El.Seed, El.Ant, El.Empty, 0.05f);      // ...eat seeds
            React(El.Ant, El.Plant, El.Ant, El.Ant, 0.002f);      // ...and breed on plants
            React(El.Ant, El.Plant, El.Ant, El.Empty, 0.01f);
            React(El.Worm, El.Water, El.Empty, El.Water, 0.003f); // worms drown slowly
            React(El.Worm, El.Ash, El.Worm, El.Dirt, 0.05f);      // and turn ash into soil

            // ---- Gases and weather
            React(El.Steam, El.Cloud, El.Cloud, El.Cloud, 0.05f); // clouds gather steam
            React(El.PoisonGas, El.Plant, El.PoisonGas, El.Ash, 0.02f);
            React(El.PoisonGas, El.Bud, El.PoisonGas, El.Ash, 0.05f);
            React(El.PoisonGas, El.Seed, El.PoisonGas, El.Ash, 0.05f);
            React(El.PoisonGas, El.Vine, El.PoisonGas, El.Ash, 0.02f);
            React(El.PoisonGas, El.Moss, El.PoisonGas, El.Ash, 0.02f);
            React(El.PoisonGas, El.Fungus, El.PoisonGas, El.Ash, 0.01f);
            React(El.PoisonGas, El.Ant, El.PoisonGas, El.Empty, 0.1f);
            React(El.PoisonGas, El.Worm, El.PoisonGas, El.Empty, 0.1f);
            React(El.PoisonGas, El.Water, El.Empty, El.Water, 0.01f);   // water soaks it up
            React(El.Acid, El.Water, El.Acid, El.Bubble, 0.002f);       // acid fizzes in water

            // ---- Odd and fun
            React(El.Sponge, El.Water, El.WetSponge, El.Empty, 0.2f);
            React(El.Sponge, El.SaltWater, El.WetSponge, El.Empty, 0.2f);
            React(El.WetSponge, El.Fire, El.Sponge, El.Steam, 0.1f);
            React(El.WetSponge, El.Lava, El.Sponge, El.Lava, 0.2f);
            React(El.WetSponge, El.Ember, El.Sponge, El.Ember, 0.1f);

            React(El.Acid, El.Glass, El.Acid, El.Glass, 1f);      // glass is acid-proof (listed before Any so it wins)
            React(El.Acid, El.Obsidian, El.Acid, El.Obsidian, 1f);
            React(El.Acid, El.Brick, El.Acid, El.Brick, 0.8f);    // brick mostly holds up
            React(El.Acid, El.Metal, El.Acid, El.Hydrogen, 0.03f); // eating metal gives off hydrogen
            React(El.Acid, El.Water, El.Water, El.Water, 0.005f); // gets diluted
            React(El.Acid, El.Any, El.Acid, El.Empty, 0.05f);     // eats things
            React(El.Acid, El.Any, El.Empty, El.Empty, 0.02f);    // and gets used up

            React(El.Void, El.Any, El.Void, El.Empty, 1f);        // deletes whatever touches it
            React(El.Antimatter, El.Fire, El.Antimatter, El.Fire, 1f);   // its own flashes don't set it off,
            React(El.Antimatter, El.Smoke, El.Antimatter, El.Smoke, 1f); // or a pile would wipe itself out
            React(El.Antimatter, El.Any, El.Fire, El.Empty, 1f);  // wipes out whatever it touches, in a flash
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
