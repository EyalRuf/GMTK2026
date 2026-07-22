using UnityEngine;

namespace NineLives
{
    /// Depresses when a body (corpse) or the player rests on it.
    public class PressurePlate : MonoBehaviour
    {
        public int Link;
        public bool Pressed { get; private set; }

        GameConfig cfg;
        Transform cap;
        Vector3 upPos, downPos;
        Vector3 checkCenter;
        Vector3 checkHalf;
        Material matUp, matDown;
        System.Action onPressChanged;

        public void Build(GameConfig config, PlateDef def, Transform parent, System.Action onChanged)
        {
            cfg = config;
            Link = def.Link;
            onPressChanged = onChanged;

            matUp = GreyboxFactory.Make(GreyboxFactory.Plate, 0.2f, true);
            matDown = GreyboxFactory.Make(GreyboxFactory.PlateHit, 0.2f, true);

            float depth = cfg.levelDepth * 0.9f;
            var root = new GameObject("Plate").transform;
            root.SetParent(parent, false);
            root.position = new Vector3(def.Pos.x, def.Pos.y, 0f);
            transform.SetParent(root, false);

            // frame
            GreyboxFactory.Box("PlateFrame", root, new Vector3(0f, -0.15f, 0f),
                new Vector3(def.Width + 0.4f, 0.3f, depth + 0.4f),
                GreyboxFactory.Make(GreyboxFactory.Ground2, 0.1f));

            var capGo = GreyboxFactory.Box("PlateCap", root, new Vector3(0f, 0.12f, 0f),
                new Vector3(def.Width, 0.24f, depth), matUp, collider: false);
            cap = capGo.transform;
            upPos = cap.localPosition;
            downPos = upPos + Vector3.down * 0.18f;

            checkHalf = new Vector3(def.Width * 0.5f, 0.45f, depth * 0.5f);
            checkCenter = root.position + Vector3.up * 0.4f;
        }

        void FixedUpdate()
        {
            bool now = AnyPresserOn();
            cap.localPosition = Vector3.MoveTowards(cap.localPosition, now ? downPos : upPos, 3f * Time.fixedDeltaTime);
            cap.GetComponent<MeshRenderer>().sharedMaterial = now ? matDown : matUp;

            if (now != Pressed)
            {
                Pressed = now;
                onPressChanged?.Invoke();
            }
        }

        bool AnyPresserOn()
        {
            var hits = Physics.OverlapBox(checkCenter, checkHalf, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.GetComponentInParent<Corpse>() != null) return true;
                if (h.GetComponentInParent<PlayerController>() != null) return true;
            }
            return false;
        }
    }
}
