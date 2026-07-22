using System.Collections.Generic;
using UnityEngine;

namespace NineLives
{
    /// A gate or platform that slides between two positions based on whether
    /// any plate sharing its link is pressed.
    public class LinkedMover : MonoBehaviour
    {
        MoverDef def;
        List<PressurePlate> plates;
        Vector3 closedWorld, openWorld;

        public void Build(GameConfig cfg, MoverDef d, List<PressurePlate> allPlates, Transform parent)
        {
            def = d;
            plates = allPlates;
            closedWorld = new Vector3(d.Closed.x, d.Closed.y, 0f);
            openWorld = new Vector3(d.Open.x, d.Open.y, 0f);

            transform.SetParent(parent, false);
            transform.position = closedWorld;

            var col = d.Gate ? GreyboxFactory.Gate : GreyboxFactory.Mover;
            var box = GreyboxFactory.Box(d.Gate ? "Gate" : "Lift", transform, Vector3.zero,
                new Vector3(d.Size.x, d.Size.y, cfg.levelDepth), GreyboxFactory.Make(col, 0.15f));
            box.transform.localPosition = Vector3.zero;
        }

        void FixedUpdate()
        {
            bool active = false;
            foreach (var p in plates)
                if (p.Link == def.Link && p.Pressed) { active = true; break; }

            var target = active ? openWorld : closedWorld;
            transform.position = Vector3.MoveTowards(transform.position, target, def.Speed * Time.fixedDeltaTime);
        }
    }
}
