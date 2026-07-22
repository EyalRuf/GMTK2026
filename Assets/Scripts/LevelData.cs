using System.Collections.Generic;
using UnityEngine;

namespace NineLives
{
    public struct BlockDef
    {
        public Vector2 Center;
        public Vector2 Size;
        public bool Alt;      // alternate shade, purely visual
        public BlockDef(Vector2 c, Vector2 s, bool alt = false) { Center = c; Size = s; Alt = alt; }
    }

    public struct PlateDef
    {
        public Vector2 Pos;   // sits on a surface; Pos is the top-surface point
        public float Width;
        public int Link;
        public PlateDef(Vector2 pos, float width, int link) { Pos = pos; Width = width; Link = link; }
    }

    public struct MoverDef
    {
        public int Link;
        public Vector2 Closed;   // center when no plate pressed
        public Vector2 Open;     // center when a linked plate is pressed
        public Vector2 Size;
        public float Speed;
        public bool Gate;        // visual: red gate vs purple platform
        public MoverDef(int link, Vector2 closed, Vector2 open, Vector2 size, float speed, bool gate)
        { Link = link; Closed = closed; Open = open; Size = size; Speed = speed; Gate = gate; }
    }

    public class LevelDef
    {
        public string Name;
        public string Hint;
        public float Timer;
        public Vector2 Entry;      // feet position
        public Vector2 Exit;       // feet position of the exit pad
        public List<BlockDef> Blocks = new();
        public List<PlateDef> Plates = new();
        public List<MoverDef> Movers = new();

        // xMin..xMax slab whose TOP surface is at topY.
        public LevelDef Slab(float xMin, float xMax, float topY, float thickness = 1.5f, bool alt = false)
        {
            float w = xMax - xMin;
            Blocks.Add(new BlockDef(new Vector2(xMin + w * 0.5f, topY - thickness * 0.5f),
                                    new Vector2(w, thickness), alt));
            return this;
        }
        public LevelDef Wall(float x, float yBottom, float yTop, float thickness = 1.5f, bool alt = false)
        {
            float h = yTop - yBottom;
            Blocks.Add(new BlockDef(new Vector2(x, yBottom + h * 0.5f), new Vector2(thickness, h), alt));
            return this;
        }
        public LevelDef Ledge(float xCenter, float topY, float width, float thickness = 1f, bool alt = false)
        {
            Blocks.Add(new BlockDef(new Vector2(xCenter, topY - thickness * 0.5f),
                                    new Vector2(width, thickness), alt));
            return this;
        }
        public LevelDef Plate(float x, float topY, int link, float width = 2.2f)
        { Plates.Add(new PlateDef(new Vector2(x, topY), width, link)); return this; }
        public LevelDef Mover(MoverDef m) { Movers.Add(m); return this; }
    }

    public static class Levels
    {
        public static List<LevelDef> Build()
        {
            return new List<LevelDef> { One(), Two(), Three() };
        }

        // L1 — teach: sacrifice a body onto a plate to hold a gate open.
        static LevelDef One()
        {
            var L = new LevelDef { Name = "Weigh In", Timer = 11f,
                Hint = "A pressure plate needs weight. You leave a body behind when you die (Q). Use it." };
            L.Entry = new Vector2(2f, 0f);
            L.Exit  = new Vector2(30f, 0f);
            L.Slab(-2f, 34f, 0f, 2f);                 // ground
            L.Wall(-2f, 0f, 8f, 1.5f);                // left wall
            L.Plate(8f, 0f, 1);                       // the plate
            // gate: solid wall that drops into the ground when the plate is held
            L.Mover(new MoverDef(1,
                closed: new Vector2(18f, 2.5f),        // blocks the corridor
                open:   new Vector2(18f, -2.6f),       // sunk into the floor
                size:   new Vector2(1.4f, 5.2f),
                speed:  9f, gate: true));
            L.Ledge(30f, 0.6f, 4f, 0.6f);             // exit pad
            return L;
        }

        // L2 — teach: a held body raises a lift; ride it up to a high exit.
        static LevelDef Two()
        {
            var L = new LevelDef { Name = "Step Up", Timer = 12f,
                Hint = "That exit is too high to jump. Weight on the plate raises the platform." };
            L.Entry = new Vector2(2f, 0f);
            L.Exit  = new Vector2(20f, 5f);
            L.Slab(-2f, 16f, 0f, 2f);
            L.Wall(-2f, 0f, 10f, 1.5f);
            L.Plate(5f, 0f, 1);
            // lift rises from flush-with-floor to a step you can climb
            L.Mover(new MoverDef(1,
                closed: new Vector2(11f, -0.25f),      // hidden in the floor
                open:   new Vector2(11f, 2.0f),        // top at ~2.5
                size:   new Vector2(3f, 0.5f),
                speed:  4.5f, gate: false));
            L.Wall(16f, 0f, 4.5f, 1.5f, true);         // pillar under the exit ledge
            L.Ledge(19f, 5f, 5f, 1f, true);            // high exit ledge
            L.Ledge(20f, 5.3f, 0.1f, 0.1f);            // (exit pad marker sits on the ledge)
            return L;
        }

        // L3 — combine: spend one body to hold a far gate, another to climb to the exit.
        static LevelDef Three()
        {
            var L = new LevelDef { Name = "Two Down", Timer = 14f,
                Hint = "Two problems, and lives to spend. Plan where each body goes." };
            L.Entry = new Vector2(2f, 0f);
            L.Exit  = new Vector2(37f, 4.5f);
            L.Slab(-2f, 42f, 0f, 2f);
            L.Wall(-2f, 0f, 9f, 1.5f);
            L.Plate(6f, 0f, 1);
            // far gate, opens only while the plate is weighted
            L.Mover(new MoverDef(1,
                closed: new Vector2(22f, 3f),
                open:   new Vector2(22f, -3.1f),
                size:   new Vector2(1.4f, 6f),
                speed:  9f, gate: true));
            L.Wall(33f, 0f, 9f, 1.5f, true);           // tall wall; exit ledge only reachable by climbing a body
            L.Ledge(37.5f, 4.5f, 6f, 1f, true);        // high exit ledge behind the wall
            return L;
        }
    }
}
