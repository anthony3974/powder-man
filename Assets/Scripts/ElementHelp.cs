using System.Collections.Generic;
using System.Text;

namespace PowderMan
{
    /// <summary>
    /// The text for the Help screen: what each element does and what it reacts with.
    /// Keep it in step with Elements.cs when you change an element. The tag line (kind, burns, hot...)
    /// is worked out from the element's settings, so only the words here need updating.
    /// </summary>
    public static class ElementHelp
    {
        public const string HowToPlayTitle = "How to play";

        public static readonly string HowToPlay = Join(
            "Draw elements into the world and watch them fall, flow, burn and react.",
            new[]
            {
                "<b>Pen slots A and B</b>: the pen holds two elements. Tap the other slot to switch to it. Tap the slot that's already lit to pick a new element for it.",
                "<b>Drawing</b>: one finger draws with the lit slot. Powders, liquids and gases spray out as grains, solids draw solid.",
                "<b>Element picker</b>: tabs along the top sort the elements (Basic, Earth, Heat, Boom, Life, Sky, Odd), and All shows every one. A dot marks the tabs holding A's and B's elements.",
                "<b>Eraser</b>: the last button in every picker tab. Put it in a slot to rub things out.",
                "<b>- 3 +</b>: brush size. Tap the number to reset it. Size 0 places single grains.",
                "<b>Pause / Step</b>: freeze the world, or move it on one tick at a time.",
                "<b>Speed</b>: from x0.25 to x4.",
                "<b>Replace</b>: draw over things instead of only into empty space.",
                "<b>Clear</b> empties the world. <b>Menu</b> has Save, Load and New sandbox.",
                "<b>Mouse and keys</b>: left mouse draws, right mouse or Tab switches A/B, wheel or [ ] sizes the brush, 1-9 picks a Basic element, Space pauses, R replace, C clear, F5 save, F9 load.",
            });

        static readonly Dictionary<byte, string> Text = new Dictionary<byte, string>
        {
            [El.Wall] = Join("An unbreakable wall.", new[]
            {
                "Nothing gets through it.",
                "Explosions, Acid, the Void and Black Holes can't touch it.",
                "One of the few things Antimatter can rest on.",
            }),
            [El.Sand] = Join("Falls and piles up into slopes.", new[]
            {
                "Sinks through lighter liquids like Water and Oil.",
                "<b>Lava</b> melts it into Glass.",
                "<b>Lightning</b> fuses it into Glass where it strikes.",
                "Ants dig through it and Worms wriggle through it.",
            }),
            [El.Water] = Join("Falls and spreads out flat.", new[]
            {
                "Puts out <b>Fire</b>, sometimes boiling into Steam.",
                "Cools <b>Lava</b> into Stone, boiling into Steam.",
                "<b>Ice</b> slowly freezes it. <b>Snow</b> melts into it.",
                "<b>Dirt</b> soaks it up into Mud. <b>Salt</b> dissolves into Salt Water.",
                "<b>Plants</b> drink it and grow. <b>Sponges</b> soak it up.",
                "Puts out Ember (back to Wood) and Burning Coal (back to Coal).",
                "Slowly grows Moss on Stone, rusts Metal and softens Clay.",
                "Drowns Ants and Worms, and soaks up Poison Gas.",
            }),
            [El.Stone] = Join("Solid rock.", new[]
            {
                "<b>Lava</b> melts it, very slowly.",
                "<b>Burning Thermite</b> melts it into Lava.",
                "<b>Moss</b> creeps over it, and Water slowly grows Moss on it.",
                "Made when Lava touches Water.",
            }),
            [El.Wood] = Join("Solid and flammable.", new[]
            {
                "Catches fire and burns as Ember.",
                "<b>Fungus</b> spreads through it and eats it.",
                "Ember put out by Water turns back into Wood.",
            }),
            [El.Fire] = Join("Rises, flickers and dies out in a moment, often leaving Smoke.", new[]
            {
                "Sets anything flammable alight.",
                "<b>Water</b> and <b>Salt Water</b> put it out.",
                "Melts Ice and Snow into Water.",
                "Dries Mud into Dirt, bakes Clay into Brick, and dries Wet Sponge.",
                "Boils Salt Water away, leaving Salt.",
            }),
            [El.Ember] = Join("Burning Wood or Plant. Stays put, glows and throws off Fire.", new[]
            {
                "After a second or three it crumbles into Ash or Smoke.",
                "<b>Water</b> puts it out and it turns back into Wood.",
                "Dries Mud and bakes Clay.",
            }),
            [El.Oil] = Join("A liquid that floats on Water.", new[]
            {
                "Burns fast once lit.",
                "Sand and most powders sink through it.",
            }),
            [El.Lava] = Join("Slow, heavy, burning-hot liquid.", new[]
            {
                "Sets things alight.",
                "<b>Water</b>: turns into Stone, the Water into Steam.",
                "<b>Ice</b>: melts it, sometimes setting as Obsidian.",
                "<b>Sand</b> becomes Glass, <b>Glass</b> becomes Molten Glass.",
                "<b>Metal</b> slowly melts into Molten Metal. <b>Stone</b> melts very slowly.",
                "<b>Snow</b> boils to Steam. <b>Mud</b> dries to Dirt. <b>Clay</b> bakes to Brick.",
                "<b>Salt Water</b>: turns into Stone and leaves Salt.",
            }),
            [El.Ice] = Join("Frozen water.", new[]
            {
                "Slowly freezes Water next to it.",
                "Turns Steam back into Water.",
                "<b>Fire</b>, <b>Lava</b>, <b>Salt</b> and <b>Salt Water</b> melt it.",
                "<b>Lava</b> touching it can set into Obsidian.",
            }),
            [El.Snow] = Join("A light powder.", new[]
            {
                "Floats on Water and slowly melts into it.",
                "<b>Fire</b> melts it, <b>Lava</b> boils it to Steam, <b>Salt</b> melts it.",
                "Ants can dig through it.",
            }),
            [El.Plant] = Join("Green growth that spreads where there's water.", new[]
            {
                "<b>Water</b>: drinks it and grows into it.",
                "<b>Mud</b>: keeps sprouting new shoots, drying the Mud into Dirt.",
                "Burns as Ember.",
                "<b>Ants</b> eat it and breed. <b>Fungus</b> eats it. <b>Poison Gas</b> withers it to Ash.",
            }),
            [El.Acid] = Join("A liquid that eats through almost anything.", new[]
            {
                "Dissolves what it touches and gets used up doing it.",
                "Can't hurt <b>Glass</b>, <b>Obsidian</b> or unbreakable things. <b>Brick</b> mostly holds up.",
                "<b>Metal</b>: eats it and gives off Hydrogen.",
                "<b>Water</b>: slowly gets diluted and fizzes Bubbles.",
            }),
            [El.Gunpowder] = Join("A powder that explodes when lit.", new[]
            {
                "Small blast, radius 3.",
                "Sets off nearby explosives a moment later, so it chains.",
            }),
            [El.Tnt] = Join("A solid block that explodes when lit.", new[]
            {
                "Big blast, radius 8.",
                "Sets off other explosives in a chain.",
            }),
            [El.Glass] = Join("Clear and acid-proof.", new[]
            {
                "Acid can't eat it, so it makes a good acid tank.",
                "<b>Lava</b> and <b>Burning Thermite</b> melt it into Molten Glass.",
                "Made by Lava or Lightning hitting Sand.",
            }),
            [El.Steam] = Join("Hot vapour that rises.", new[]
            {
                "After a few seconds it rains back down as Water, or vanishes.",
                "Turns into Water on <b>Ice</b>.",
                "Joins <b>Clouds</b> it touches.",
            }),
            [El.Smoke] = Join("Rises and fades away after a second or two.", new string[0]),
            [El.Cloner] = Join("Copies the first thing that touches it, forever.", new[]
            {
                "Keeps spawning copies into the empty space around it.",
                "Unbreakable.",
            }),
            [El.Void] = Join("Deletes anything that touches it.", new[]
            {
                "Unbreakable. Use it as a drain.",
            }),

            // ---- Earth
            [El.Dirt] = Join("Soil. Falls and piles like Sand.", new[]
            {
                "<b>Water</b>: soaks it up and turns into Mud.",
                "<b>Seeds</b> sprout on it, slowly.",
                "Ants dig tunnels in it and Worms live in it.",
            }),
            [El.Mud] = Join("Wet soil. Heavy and slow to move.", new[]
            {
                "<b>Fire</b>, <b>Lava</b> and <b>Ember</b> dry it back into Dirt.",
                "<b>Seeds</b> sprout fast on it. <b>Plants</b> on it keep sprouting.",
                "Lets off a little Methane now and then, like a swamp.",
            }),
            [El.Clay] = Join("A solid you can shape, then bake.", new[]
            {
                "<b>Fire</b>, <b>Lava</b> or <b>Ember</b> bake it into Brick.",
                "<b>Water</b> slowly softens it into Mud.",
            }),
            [El.Brick] = Join("Baked Clay.", new[]
            {
                "Mostly holds up to <b>Acid</b>, which only eats it slowly.",
                "<b>Burning Thermite</b> melts it into Lava.",
            }),
            [El.Salt] = Join("A white powder.", new[]
            {
                "Melts <b>Ice</b> and <b>Snow</b> into Water.",
                "<b>Water</b>: dissolves into Salt Water.",
            }),
            [El.SaltWater] = Join("Water with salt in it. A little heavier than Water.", new[]
            {
                "Never freezes, and melts Ice it touches.",
                "Boils away in <b>Fire</b> or <b>Lava</b> and leaves Salt behind.",
                "Rusts <b>Metal</b> much faster than plain Water.",
                "Puts out Fire, Ember and Burning Coal like Water.",
            }),
            [El.Ash] = Join("What burnt Wood and Plants leave behind.", new[]
            {
                "Light and fluffy: drifts as it falls and floats on Water.",
                "<b>Worms</b> turn it into Dirt.",
            }),
            [El.Obsidian] = Join("Black volcanic glass.", new[]
            {
                "Made when <b>Lava</b> touches <b>Ice</b>.",
                "Acid can't touch it.",
                "<b>Burning Thermite</b> slowly melts it into Lava.",
            }),

            // ---- Metals and heat
            [El.Metal] = Join("A solid metal block.", new[]
            {
                "<b>Lava</b> slowly melts it into Molten Metal. <b>Burning Thermite</b> melts it fast.",
                "<b>Water</b> slowly rusts it. <b>Salt Water</b> rusts it faster.",
                "<b>Acid</b> eats it and gives off Hydrogen.",
            }),
            [El.MoltenMetal] = Join("Glowing liquid metal.", new[]
            {
                "Hot: sets things alight.",
                "Cools back into Metal after about ten seconds.",
                "<b>Water</b> cools it at once into Metal, boiling into Steam. <b>Ice</b> too.",
            }),
            [El.Rust] = Join("Crumbly orange powder.", new[]
            {
                "What Metal turns into after a long time in Water.",
            }),
            [El.Coal] = Join("A black powder fuel.", new[]
            {
                "Hard to light, but once lit it burns for about 25 seconds as Burning Coal.",
                "Burning Coal glows, throws off Fire, and ends as Ash.",
                "<b>Water</b> puts Burning Coal out, back into Coal.",
            }),
            [El.Thermite] = Join("A powder that burns white-hot.", new[]
            {
                "Once lit it becomes Burning Thermite: it keeps falling and melts what it lands on.",
                "Melts <b>Metal</b>, <b>Stone</b>, <b>Brick</b>, <b>Obsidian</b> and <b>Glass</b>.",
                "<b>Water</b> doesn't stop it, it boils into Steam.",
                "Burns out after a couple of seconds, often as Molten Metal.",
            }),
            [El.MoltenGlass] = Join("Glowing liquid glass. Very slow.", new[]
            {
                "Hot: sets things alight.",
                "Sets back into Glass after about ten seconds, or at once in <b>Water</b>.",
            }),

            // ---- Living things
            [El.Seed] = Join("A seed that grows into a plant.", new[]
            {
                "Sprouts fast on <b>Mud</b> and slowly on <b>Dirt</b>.",
                "The sprout climbs upward for a few seconds, leaving a Plant stalk behind.",
                "<b>Ants</b> eat seeds. <b>Poison Gas</b> kills them. Burns.",
            }),
            [El.Vine] = Join("Grows downward from wherever you draw it.", new[]
            {
                "Each vine you draw can grow up to 40 cells long, wiggling as it goes.",
                "Burns fast.",
                "<b>Poison Gas</b> withers it to Ash.",
            }),
            [El.Moss] = Join("A soft green growth on rock.", new[]
            {
                "Slowly creeps over <b>Stone</b>.",
                "<b>Water</b> sitting on Stone slowly grows Moss by itself.",
                "Burns. <b>Poison Gas</b> withers it to Ash.",
            }),
            [El.Fungus] = Join("A pale mould that rots things.", new[]
            {
                "Spreads through <b>Wood</b> and <b>Plant</b>, eating them.",
                "Lets off a little Methane as it rots.",
                "Burns. <b>Poison Gas</b> withers it to Ash.",
            }),
            [El.Ant] = Join("A little creature that walks, climbs and digs.", new[]
            {
                "Walks left and right, stops and starts, and turns around when stuck.",
                "Steps up ledges, climbs straight up walls, and steps over the top.",
                "Digs tunnels through Dirt, Sand, Mud, Snow, Ash and Salt.",
                "Falls when there's nothing below or beside it to hold on to.",
                "<b>Seeds</b>: eats them. <b>Plant</b>: eats it, and sometimes turns it into a new ant.",
                "Drowns in <b>Water</b> within about a second. Burns easily.",
                "<b>Poison Gas</b> and <b>Acid</b> kill it.",
            }),
            [El.Worm] = Join("A slow creature that lives in soil.", new[]
            {
                "Wriggles through <b>Dirt</b>, <b>Mud</b> and <b>Sand</b>, or crawls along the top.",
                "Falls through open air.",
                "<b>Ash</b>: turns it into Dirt.",
                "Drowns slowly in <b>Water</b>. Burns. <b>Poison Gas</b> kills it.",
            }),

            // ---- Gases and weather
            [El.Cloud] = Join("Drifts up to the top and rains.", new[]
            {
                "Drops Water now and then. Lasts about two minutes.",
                "Gathers <b>Steam</b> into more Cloud.",
            }),
            [El.Lightning] = Join("A bolt that shoots down, zig-zagging.", new[]
            {
                "Passes through air and gases that don't burn, leaving a flash of sparks.",
                "Sets alight whatever it hits, if it burns. Explosives go off.",
                "Fuses <b>Sand</b> around where it strikes into Glass.",
                "Lights <b>Methane</b> and <b>Hydrogen</b> on the way down.",
            }),
            [El.Methane] = Join("A dark gas that rises and stays.", new[]
            {
                "Explodes when lit (small blast), and a cloud of it chains.",
                "Comes off Mud and Fungus over time.",
            }),
            [El.Hydrogen] = Join("The lightest gas. Spreads out fast.", new[]
            {
                "Goes pop when lit, and a cloud of it chains.",
                "Made when <b>Acid</b> eats <b>Metal</b>.",
            }),
            [El.PoisonGas] = Join("A green gas that kills living things. Lasts about 13 seconds.", new[]
            {
                "Withers <b>Plant</b>, <b>Seeds</b>, <b>Vine</b>, <b>Moss</b> and <b>Fungus</b> to Ash.",
                "Kills <b>Ants</b> and <b>Worms</b>.",
                "<b>Water</b> soaks it up.",
            }),
            [El.Bubble] = Join("A bubble of air.", new[]
            {
                "Rises through liquids and pops at the surface.",
                "Acid fizzes them out in Water.",
                "Pops on its own after a few seconds.",
            }),

            // ---- Odd and fun
            [El.Nitro] = Join("A very touchy explosive liquid.", new[]
            {
                "Explodes when lit: big blast, radius 6.",
                "Also explodes if it falls more than 12 cells and lands hard, even on more Nitro.",
                "Draw it close to the ground, or pour it gently.",
            }),
            [El.Sponge] = Join("A solid that soaks up water.", new[]
            {
                "Soaks up <b>Water</b> and <b>Salt Water</b> it touches and turns into Wet Sponge.",
                "<b>Fire</b>, <b>Lava</b> or <b>Ember</b> dry a Wet Sponge back out.",
                "A dry sponge burns. A wet one doesn't.",
            }),
            [El.Ball] = Join("A bouncy rubber ball.", new[]
            {
                "Bounces when it lands, about 60% as high each time, until it settles.",
                "The further it falls, the higher it bounces.",
            }),
            [El.Antimatter] = Join("Wipes out whatever it touches, and itself, in a flash.", new[]
            {
                "Only unbreakable things can hold it: Wall, Cloner, Void, Black Hole and Portals.",
                "Its own flashes of Fire and Smoke don't set it off.",
            }),
            [El.BlackHole] = Join("Pulls in everything within 14 cells and deletes it.", new[]
            {
                "Pulls harder close in. A bigger black hole pulls faster.",
                "Unbreakable things are safe from it.",
            }),
            [El.PortalIn] = Join("Sends things to a Portal Out.", new[]
            {
                "Any powder, liquid or gas touching it is sent to a random Portal Out.",
                "Falling things come out of the bottom of the exit, gases out of the top.",
                "Does nothing until there's a Portal Out somewhere. Unbreakable.",
            }),
            [El.PortalOut] = Join("Where things from a Portal In come out.", new[]
            {
                "Draw it where you want things to appear. Unbreakable.",
                "With several, each trip picks one at random.",
            }),
        };

        /// <summary>The help text for an element (summary, blank line, one bullet per line).</summary>
        public static string For(byte id) => Text.TryGetValue(id, out var t) ? t : "";

        /// <summary>A short line from the element's settings, like "Powder  ·  Burns  ·  Explodes".</summary>
        public static string Tags(byte id)
        {
            var e = ElementDatabase.Table[id];
            var sb = new StringBuilder();
            sb.Append(id == El.Ant || id == El.Worm ? "Creature" : id == El.Lightning ? "Bolt" : e.Kind.ToString());
            if (e.ExplodeRadius > 0) sb.Append("  ·  Explodes");
            else if (e.Flammability > 0f) sb.Append("  ·  Burns");
            if (e.Heat > 0f) sb.Append("  ·  Hot");
            if (e.Indestructible) sb.Append("  ·  Unbreakable");
            return sb.ToString();
        }

        static string Join(string summary, string[] bullets)
        {
            var sb = new StringBuilder(summary);
            if (bullets.Length > 0) sb.Append('\n');
            foreach (var b in bullets) sb.Append("\n•  ").Append(b);
            return sb.ToString();
        }
    }
}
