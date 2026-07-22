using System.Collections.Generic;
using UnityEngine;

namespace NineLives
{
    /// Instantiates one LevelDef as greybox geometry and holds the runtime handles.
    public class LevelRuntime
    {
        public GameObject Root;
        public Vector2 Entry;
        public Vector2 Exit;
        public readonly List<PressurePlate> Plates = new();

        public static LevelRuntime Build(LevelDef def, GameConfig cfg, System.Action onExit)
        {
            var rt = new LevelRuntime { Entry = def.Entry, Exit = def.Exit };
            rt.Root = new GameObject($"Level_{def.Name}");
            var t = rt.Root.transform;

            var matA = GreyboxFactory.Make(GreyboxFactory.Ground, 0.05f);
            var matB = GreyboxFactory.Make(GreyboxFactory.Ground2, 0.05f);

            foreach (var b in def.Blocks)
                GreyboxFactory.Box("Block", t, new Vector3(b.Center.x, b.Center.y, 0f),
                    new Vector3(b.Size.x, b.Size.y, cfg.levelDepth), b.Alt ? matB : matA);

            foreach (var p in def.Plates)
            {
                var go = new GameObject("PressurePlate");
                var plate = go.AddComponent<PressurePlate>();
                plate.Build(cfg, p, t, null);
                rt.Plates.Add(plate);
            }

            foreach (var m in def.Movers)
            {
                var go = new GameObject("Mover");
                go.AddComponent<LinkedMover>().Build(cfg, m, rt.Plates, t);
            }

            // Exit pad
            var pad = GreyboxFactory.Box("ExitPad", t, new Vector3(def.Exit.x, def.Exit.y + 0.15f, 0f),
                new Vector3(2.4f, 0.3f, cfg.levelDepth), GreyboxFactory.Make(GreyboxFactory.Exit, 0.4f, true));
            var flag = GreyboxFactory.Box("ExitFlag", t, new Vector3(def.Exit.x, def.Exit.y + 1.4f, 0f),
                new Vector3(0.3f, 2.4f, 0.3f), GreyboxFactory.Make(GreyboxFactory.Exit, 0.4f, true), collider: false);

            var trigger = new GameObject("ExitTrigger");
            trigger.transform.SetParent(t, false);
            trigger.transform.position = new Vector3(def.Exit.x, def.Exit.y + 1.2f, 0f);
            var bc = trigger.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(2.4f, 2.6f, cfg.levelDepth);
            trigger.AddComponent<LevelExit>().Init(onExit);

            return rt;
        }

        public void Destroy()
        {
            if (Root != null) Object.Destroy(Root);
        }
    }
}
