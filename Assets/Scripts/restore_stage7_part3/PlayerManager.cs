using UnityEngine;
using TMPro;
using Fusion;

public class PlayerManager : NetworkBehaviour
{
    [Header("Mana Settings")]
    public int maxMana = 2;

    [Networked] public int currentMana { get; set; }

    [Header("マナUI")]
    public TMP_Text energyText;          // 自分のマナ
    public TMP_Text opponentEnergyText;  // 相手のマナ

    [Header("手札UI")]
    public TMP_Text myHandCountText;         // 自分の手札枚数表示
    public TMP_Text opponentHandCountText;   // 相手の手札枚数表示

    public Transform opponentHandBackRoot;   // 相手の裏面カードを並べる親
    public GameObject opponentBackCardPrefab; // 裏面カード用プレハブ

    [Header("Managers (Prefab 内)")]
    public HandManager handManager;
    public LifeManager lifeManager;

    // GameManager・相手プレイヤー参照用
    public GameManager gameManager;
    public PlayerManager opponent;

    [Header("相手手札表示用")]
    public HandManager opponentHandViewManager;   // ← 追加

    // ================================
    //  Spawn（Prefabが参加した時） 
    // ================================
    public override void Spawned()
    {
        gameManager = FindAnyObjectByType<GameManager>();

        // ★ 先に ownerPlayer を設定しておく
        if (handManager != null)
            handManager.ownerPlayer = this;

        // そのあとで GameManager に登録
        gameManager.RegisterPlayer(this);

        // 自分のCanvasだけ ON
        if (energyText != null)
            energyText.transform.root.gameObject.SetActive(Object.HasInputAuthority);

        // マナ初期値 0/2
        currentMana = 0;
        UpdateEnergyUI();
        UpdateOpponentUI();

        // 手札枚数UI 初期更新
        if (Object.HasInputAuthority)
            UpdateHandCountUI();
    }

    // ================================
    //  マナ処理（Networked）
    // ================================

    public bool SpendMana(int amount)
    {
        if (currentMana >= amount)
        {
            currentMana -= amount;
            UpdateEnergyUI();
            UpdateOpponentUI();
            return true;
        }
        Debug.Log("マナが足りません！");
        return false;
    }

    public void GainMana(int amount)
    {
        currentMana = Mathf.Min(currentMana + amount, maxMana);
        UpdateEnergyUI();
        UpdateOpponentUI();
    }

    public void ResetMana()
    {
        currentMana = maxMana;
        UpdateEnergyUI();
        UpdateOpponentUI();
    }

    public void IncreaseMaxMana(int amount)
    {
        maxMana += amount;
        ResetMana();
    }

    public void IncreaseMaxManaOnly(int amount)
    {
        maxMana += amount;
        currentMana = Mathf.Min(currentMana, maxMana);
        UpdateEnergyUI();
        UpdateOpponentUI();
    }

    // ================================
    //  UI 更新
    // ================================

    public void UpdateEnergyUI()
    {
        if (energyText != null)
            energyText.text = $"{currentMana}/{maxMana}";
    }

    public void UpdateOpponentUI()
    {
        if (opponent != null && opponentEnergyText != null)
            opponentEnergyText.text = $"{opponent.currentMana}/{opponent.maxMana}";
    }
    // HandManager から呼ばれる「手札が変わったよ」通知
    public void NotifyHandChangedForBothSides()
    {
        // 自分の入力権を持っている PlayerManager だけが
        // ローカルUIを更新する（相手側のCanvasは非表示でOK）
        if (!Object.HasInputAuthority)
            return;

        UpdateHandCountUI();
    }

    // 手札枚数UIの更新＆相手の裏面カードを並べる
    public void UpdateHandCountUI()
    {
        // 自分の手札枚数
        int myCount = (handManager != null) ? handManager.CardCount : 0;

        if (myHandCountText != null)
            myHandCountText.text = myCount.ToString();

        // 相手の手札枚数
        int oppCount = 0;
        if (opponent != null && opponent.handManager != null)
            oppCount = opponent.handManager.CardCount;

        if (opponentHandCountText != null)
            opponentHandCountText.text = oppCount.ToString();

        // 相手の裏面カードを更新
        UpdateOpponentBackCards(oppCount);
    }

    // 相手の裏面カードを枚数に合わせて増減
    // 相手の裏面カードを枚数に合わせて増減
    private void UpdateOpponentBackCards(int count)
    {
        if (opponentHandViewManager == null || opponentBackCardPrefab == null)
            return;

        var viewHM = opponentHandViewManager;

        // 既存の枚数
        int current = viewHM.handCards.Count;

        // 足りないぶんを追加
        for (int i = current; i < count; i++)
        {
            var card = GameObject.Instantiate(opponentBackCardPrefab, viewHM.transform);
            card.transform.localScale = Vector3.one;

            // HandManager に管理させる
            viewHM.handCards.Add(card);
        }

        // 多すぎるぶんを削除
        for (int i = viewHM.handCards.Count - 1; i >= count; i--)
        {
            var card = viewHM.handCards[i];
            viewHM.handCards.RemoveAt(i);
            if (card != null)
                GameObject.Destroy(card);
        }

        // ★ HandManager のレイアウトロジックをそのまま使う
        viewHM.UpdateCardPositions();
    }

    // ================================
    //  相手プレイヤーの参照セット
    // ================================
    public void SetOpponent(PlayerManager pm)
    {
        opponent = pm;
        UpdateOpponentUI();
    }
}
