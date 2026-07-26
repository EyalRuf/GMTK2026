using UnityEngine;
using UnityEngine.UI;

namespace NineLives
{
    /// One soul in the HUD's soul row. `dead` is the static background icon;
    /// `alive` sits on top with a radial fill that drains as the soul's time runs out.
    public class SoulIcon : MonoBehaviour
    {
        [SerializeField] Image dead;
        [SerializeField] Image alive;

        public void SetFill(float amount, Color color)
        {
            alive.fillAmount = amount;
            alive.color = color;
        }
    }
}
