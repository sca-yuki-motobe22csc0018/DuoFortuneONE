using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardDetailPanel : MonoBehaviour
{
    public static CardDetailPanel Instance;

    [Header("UI Areas")]
    public Transform leftArea;
    public TMP_Text nameText;
    public TMP_Text costText;
    public TMP_Text typeText;
    public TMP_Text descriptionText;
    public Button closeButton;

    [Header("Prefabs")]
    public GameObject cardUIPrefab;

    private GameObject currentCardUI;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        gameObject.SetActive(false);
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
    }

    public void Show(CardGenerator.CardData data)
    {
        if (data == null) return;

        if (currentCardUI != null) Destroy(currentCardUI);

        if (cardUIPrefab != null && leftArea != null)
        {
            currentCardUI = Instantiate(cardUIPrefab, leftArea);
            currentCardUI.transform.localScale = Vector3.one * 1.0f;
            var ui = currentCardUI.GetComponent<CardUI>();
            if (ui != null) ui.SetCard(data, 1);
        }

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
