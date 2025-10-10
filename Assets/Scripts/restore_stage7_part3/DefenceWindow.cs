using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DefenceWindow : MonoBehaviour
{
    public static DefenceWindow Instance;

    [Header("UI Components")]
    public TMP_Text titleText;
    public Transform cardParent;       // UICardを表示する親（空のRectTransform）
    public Button useButton;
    public Button okButton;

    [Header("Prefabs")]
    public GameObject uiCardPrefab;    // Assets/Prefab/UICard.prefab をInspectorで割り当て

    [Header("Card Display Settings")]
    public Vector3 cardScale = new Vector3(0.8f, 0.8f, 0.8f);  // 生成カードのスケール

    private bool isWaiting = false;
    private bool useDefence = false;
    private CardGenerator.CardData currentCardData;
    private PlayerManager currentPlayer;

    private GameObject currentCardObj; // 生成したUICard

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        gameObject.SetActive(false);

        if (useButton != null) useButton.onClick.AddListener(OnUseClicked);
        if (okButton != null) okButton.onClick.AddListener(OnOkClicked);
    }

    /// <summary>
    /// ライフ破壊時のDEFENCE発動確認ウィンドウ
    /// </summary>
    public IEnumerator ShowDefenceChoice(PlayerManager player, CardGenerator.CardData cardData)
    {
        if (player == null || cardData == null) yield break;

        currentPlayer = player;
        currentCardData = cardData;
        isWaiting = true;
        useDefence = false;

        gameObject.SetActive(true);
        if (titleText != null) titleText.text = "ライフが破壊されました！";

        // 既に表示中のカードを破棄
        if (currentCardObj != null) Destroy(currentCardObj);

        // --- UICard を生成して内容を反映 ---
        if (uiCardPrefab != null && cardParent != null)
        {
            currentCardObj = Instantiate(uiCardPrefab, cardParent);
            // ★ サイズ調整（Inspectorで設定した倍率を反映）
            currentCardObj.transform.localScale = cardScale;

            // CardUI に内容を反映（DiscardManagerは不要なのでnull、ゾーンはどちらでもOK）
            var ui = currentCardObj.GetComponent<CardUI>();
            if (ui != null)
            {
                ui.SetCard(cardData, 1, null, CardUISource.RecoverZone);
            }

            // クリックで詳細を開けるように CardUIDetail にも初期化を渡す
            var detail = currentCardObj.GetComponent<CardUIDetail>();
            if (detail != null)
            {
                detail.Init(cardData);
            }
        }
        else
        {
            Debug.LogWarning("DefenceWindow: uiCardPrefab か cardParent が未設定です。");
        }

        // DEFENCE以外は使用ボタンを無効化
        if (useButton != null) useButton.interactable = (cardData.type == "D");

        // --- プレイヤー入力待ち ---
        while (isWaiting)
            yield return null;

        // --- プレイヤーの選択結果 ---
        if (useDefence)
        {
            Debug.Log("DEFENCE発動: " + cardData.name);
            yield return StartCoroutine(UseDefenceCard());

            // DEFENCEを使った場合 → 捨て札ゾーンへ送る
            if (currentPlayer != null && currentPlayer.discardManager != null)
            {
                currentPlayer.discardManager.AddToDiscard(cardData);
                Debug.Log($"DEFENCEカード {cardData.name} を捨て札ゾーンへ送信");
            }
        }
        else
        {
            Debug.Log("DEFENCEを発動しませんでした。");

            // 使わなかった場合 → 手札に加える
            if (currentPlayer != null && currentPlayer.handManager != null)
            {
                currentPlayer.handManager.AddCardFromData(cardData);
                Debug.Log($"破壊されたカード {cardData.name} を手札に加えました");
            }
        }

        // 終了処理
        if (currentCardObj != null)
            Destroy(currentCardObj);

        gameObject.SetActive(false);
    }

    private void OnUseClicked()
    {
        useDefence = true;
        isWaiting = false;

        // ★ 修正: gameObjectを非アクティブにせず、CanvasGroupで透明にして閉じる
        HideWindowVisual();
    }

    private void HideWindowVisual()
    {
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = gameObject.AddComponent<CanvasGroup>();
        }
        cg.alpha = 0;        // 完全に透明
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }

    private void OnOkClicked()
    {
        useDefence = false;
        isWaiting = false;
    }

    /// <summary>
    /// DEFENCEカードの効果を実際に実行
    /// </summary>
    private IEnumerator UseDefenceCard()
    {
        if (currentCardData == null || currentPlayer == null) yield break;

        GameObject tempCard = new GameObject("TempDefenceCard");
        var cg = tempCard.AddComponent<CardGenerator>();
        cg.player = currentPlayer;
        cg.ApplyCardData(currentCardData);

        yield return cg.StartCoroutine("EffectSequenceCoroutine");

        Destroy(tempCard);

        // ★ 追加: 処理が終わったらUIを復元
        RestoreWindowVisual();
    }

    private void RestoreWindowVisual()
    {
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
    }
}
