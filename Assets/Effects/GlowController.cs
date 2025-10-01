using UnityEngine;
using UnityEngine.UI;

public class GlowController : MonoBehaviour
{
    public Image glowImage;  // ���e�N�X�`�����A�^�b�`
    public float scale = 1.8f;
    public Color glowColor = new Color(1f, 0.95f, 0.6f, 0.8f); // ��������

    void Start()
    {
        if (glowImage != null)
        {
            glowImage.rectTransform.localScale = Vector3.one * scale;
            glowImage.color = glowColor;
        }
    }
}
