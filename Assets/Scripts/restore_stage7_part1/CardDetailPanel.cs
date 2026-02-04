using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CardDetailPanel : MonoBehaviour
{
    public static CardDetailPanel Instance;

    [Header("UI Areas")]
    public Transform leftArea;   // CardUIを置く場所
    public TMP_Text nameText;
    public TMP_Text rubyText;
    public TMP_Text costText;
    public TMP_Text typeText;        // ★ タイプ用を追加
    public TMP_Text descriptionText;
    public Button closeButton;

    public Image AttackBack;
    public Image BlockBack;
    public Image DefenceBack;
    public Image ExBack;

    [Header("Prefabs")]
    public GameObject cardUIPrefab; // ← 捨て札と同じUIカードPrefab

    private GameObject currentCardUI;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        gameObject.SetActive(false); // ← 最初は非表示でもOK
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
    }

    public void Show(CardGenerator.CardData data)
    {
        if (data == null) return;

        // 左エリアの既存を削除
        if (currentCardUI != null) Destroy(currentCardUI);

        // 新しくCardUIを生成
        if (cardUIPrefab != null && leftArea != null)
        {
            currentCardUI = Instantiate(cardUIPrefab, leftArea);
            currentCardUI.transform.localScale = Vector3.one * 1.0f; // 大きく表示
            var ui = currentCardUI.GetComponent<CardUI>();
            if (ui != null) ui.SetCard(data, 1);
        }

        // 右側にテキストを大きく反映
        if (nameText != null) nameText.text = data.name;
        if (rubyText != null) rubyText.text = data.ruby;
        if (costText != null) costText.text = $" {data.cost}";
        if (typeText != null)
        {
            string typeDisplay = data.type;

            switch (data.type)
            {
                case "A":
                    typeDisplay = "Attack";
                    AttackBack.gameObject.SetActive(true);
                    BlockBack.gameObject.SetActive(false);
                    DefenceBack.gameObject.SetActive(false);
                    ExBack.gameObject.SetActive(false);
                    break;
                case "B":
                    typeDisplay = "Block";
                    AttackBack.gameObject.SetActive(false);
                    BlockBack.gameObject.SetActive(true);
                    DefenceBack.gameObject.SetActive(false);
                    ExBack.gameObject.SetActive(false);
                    break;
                case "D":
                    typeDisplay = "Defence";
                    AttackBack.gameObject.SetActive(false);
                    BlockBack.gameObject.SetActive(false);
                    DefenceBack.gameObject.SetActive(true);
                    ExBack.gameObject.SetActive(false);
                    break;
                case "E":
                    typeDisplay = "EX";
                    AttackBack.gameObject.SetActive(false);
                    BlockBack.gameObject.SetActive(false);
                    DefenceBack.gameObject.SetActive(false);
                    ExBack.gameObject.SetActive(true);
                    break;
                default:
                    typeDisplay = data.type; // そのまま表示
                    break;
            }

            typeText.text = $"{typeDisplay}";
        }
        if (descriptionText != null) descriptionText.text = data.text;

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (currentCardUI != null) Destroy(currentCardUI);
        gameObject.SetActive(false);
    }

}
