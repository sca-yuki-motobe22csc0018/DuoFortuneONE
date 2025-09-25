using UnityEngine;
using UnityEngine.UI;

public class CardDetailPanel : MonoBehaviour
{
    public static CardDetailPanel Instance;

    [Header("UI Areas")]
    public Transform leftArea;   // CardUI��u���ꏊ
    public Text nameText;
    public Text costText;
    public Text typeText;        // �� �^�C�v�p��ǉ�
    public Text descriptionText;
    public Button closeButton;

    [Header("Prefabs")]
    public GameObject cardUIPrefab; // �� �̂ĎD�Ɠ���UI�J�[�hPrefab

    private GameObject currentCardUI;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        gameObject.SetActive(false); // �� �ŏ��͔�\���ł�OK
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
    }

    public void Show(CardGenerator.CardData data)
    {
        if (data == null) return;

        // ���G���A�̊������폜
        if (currentCardUI != null) Destroy(currentCardUI);

        // �V����CardUI�𐶐�
        if (cardUIPrefab != null && leftArea != null)
        {
            currentCardUI = Instantiate(cardUIPrefab, leftArea);
            currentCardUI.transform.localScale = Vector3.one * 1.0f; // �傫���\��
            var ui = currentCardUI.GetComponent<CardUI>();
            if (ui != null) ui.SetCard(data, 1);
        }

        // �E���Ƀe�L�X�g��傫�����f
        if (nameText != null) nameText.text = data.name;
        if (costText != null) costText.text = $" {data.cost}";
        if (typeText != null) typeText.text = $"Type: {data.type}";
        if (descriptionText != null) descriptionText.text = data.text;

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (currentCardUI != null) Destroy(currentCardUI);
        gameObject.SetActive(false);
    }

}
