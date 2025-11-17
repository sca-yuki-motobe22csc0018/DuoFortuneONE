using UnityEngine;
using UnityEngine.UI;

public class CardShineEffect : MonoBehaviour
{
    public RectTransform shineImage;
    public float speed = 100f;  // ˆÚ“®‘¬“x(px/s)
    public float loopHeight = 500f;  // ƒ‹[ƒv‚‚³

    private Vector2 startPos;

    void Start()
    {
        if (shineImage != null)
            startPos = shineImage.anchoredPosition;
    }

    void Update()
    {
        if (shineImage == null) return;

        // ã‚©‚ç‰º‚ÉˆÚ“®
        shineImage.anchoredPosition -= new Vector2(0, speed * Time.deltaTime);

        // ‰º‚Ü‚Å—ˆ‚½‚çã‚É–ß‚·
        if (shineImage.anchoredPosition.y <= -loopHeight)
        {
            shineImage.anchoredPosition = startPos;
        }
    }
}
