using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NineLives
{
    /// The final splash art (credits baked into the art itself) + "press anything to continue"
    /// prompt. Pure display — GameManager.EndingSequence owns all timing via GameConfig.
    public class EndingUI : MonoBehaviour
    {
        [SerializeField] Image splash;
        [SerializeField] TMP_Text prompt;

        public void Show()
        {
            gameObject.SetActive(true);
            SetPromptVisible(false);
        }

        public void Hide() => gameObject.SetActive(false);

        public void SetPromptVisible(bool visible) => prompt.gameObject.SetActive(visible);
    }
}
