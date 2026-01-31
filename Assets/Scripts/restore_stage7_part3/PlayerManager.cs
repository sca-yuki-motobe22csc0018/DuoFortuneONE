using UnityEngine;
using TMPro;
using Fusion;
using System.Collections.Generic;

public class PlayerManager : NetworkBehaviour
{
    [Header("Mana Settings")]
    [Networked] public int maxMana { get; set; }  // ★ Networked に変更
    [Networked] public int currentMana { get; set; }


    [Header("Mana Limit")]
    public int maxManaLimit = 10;
    // ★ 追加：手札枚数（Host確定で保持する）
    [Networked] public int handCount { get; set; }

    [Header("マナUI")]
    public TMP_Text energyText;          // 自分のマナ
    public TMP_Text opponentEnergyText;  // 相手のマナ

    [Header("ライフUI")]
    public TMP_Text myLifeCountText;         // 自分のライフ枚数
    public TMP_Text opponentLifeCountText;   // 相手のライフ枚数

    [Header("手札UI")]
    public TMP_Text myHandCountText;         // 自分の手札枚数表示
    public TMP_Text opponentHandCountText;   // 相手の手札枚数表示

    public Transform opponentHandBackRoot;   // 相手の裏面カードを並べる親
    public GameObject opponentBackCardPrefab; // 裏面カード用プレハブ

    [Header("Managers (Prefab 内)")]
    public HandManager handManager;
    public LifeManager lifeManager;

    // ★追加：コスト宣言UI（PlayerPrefab内）
    public CostSealDeclareUI costSealDeclareUI;

    // GameManager・相手プレイヤー参照用
    public GameManager gameManager;
    public PlayerManager opponent;

    [Header("相手手札表示用")]
    public HandManager opponentHandViewManager;   // ← 追加

    [Header("相手ライフ表示用")]
    public Transform opponentLifeRoot;            // 相手ライフCardBackを並べる親
    public GameObject opponentLifeBackPrefab;     // ライフ用の裏面（CardBack）
    public float opponentLifeSpacing = 0.75f;     // 並べる間隔

    // 相手ライフ用の裏面カードを管理
    private readonly List<GameObject> opponentLifeBackCards = new List<GameObject>();

    [Header("相手手札裏面表示用")]
    public float opponentHandSpacing = 0.75f;     // 並べる間隔（必要ならInspectorで調整）

    [Header("相手手札 扇形レイアウト設定")]
    public float oppHandCardSpacing = 1.25f;
    public float oppHandY = -3.5f;
    public float oppHandMaxWidth = 12f;

    public float oppHandMaxAngle = 10f;
    public float oppHandCurveHeight = -2f;
    public bool oppHandArcUp = false;

    public float oppHandNormalScale = 0.9f;

    public int oppHandBaseSortingOrder = 100;
    public int oppHandOrderStep = 10;


    // 相手手札用の裏面カードを管理（HandManager.handCardsは使わない）
    private readonly List<GameObject> opponentHandBackCards = new List<GameObject>();


    // ★ UI更新の無駄を減らすためのキャッシュ
    private int lastMyHandCount = -1;
    private int lastOppHandCount = -1;

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

        // ★ Host（StateAuthority）だけ初期マナ設定を行う
        if (Object.HasStateAuthority)
        {
            maxMana = 2;
            currentMana = 0;

            // ★ 念のため初期値
            handCount = 0;
        }

        // 自分のCanvasだけ ON
        if (energyText != null)
            energyText.transform.root.gameObject.SetActive(Object.HasInputAuthority);

        // ★追加：念のため初期は閉じる
        if (costSealDeclareUI != null)
            costSealDeclareUI.Close();

        UpdateEnergyUI();
        UpdateOpponentUI();

        // 手札＆ライフUI 初期更新（自分視点）
        if (Object.HasInputAuthority)
        {
            // ★ 初期配布(RPC_InitHandsAndLife)より前に 0 を送るのを防ぐため、少し待ってから同期
            StartCoroutine(DelayedInitialHandSync());


            UpdateHandCountUI();
            UpdateLifeUI();
        }
    }

    private bool _didInitialHandSync = false;

    private System.Collections.IEnumerator DelayedInitialHandSync()
    {
        if (_didInitialHandSync) yield break;
        _didInitialHandSync = true;

        // 初期配布が落ち着くまで待つ（ホスト側で相手手札が0表示になりがちなので少し長め）
        yield return null;
        yield return new WaitForSeconds(1.0f);

        // さらに「手札が0のまま」なら、生成反映待ちを少しだけ粘る（最大2秒）
        float timeout = 2.0f;
        float elapsed = 0f;
        while (handManager != null && handManager.CardCount == 0 && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.2f);
            elapsed += 0.2f;
        }

        // handCount の Host確定 + 自分画面の UI 更新
        NotifyHandChangedForBothSides();
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
        maxMana = Mathf.Min(maxMana + amount, maxManaLimit);
        ResetMana();
    }

    public void IncreaseMaxManaOnly(int amount)
    {
        maxMana = Mathf.Min(maxMana + amount, maxManaLimit);
        currentMana = Mathf.Min(currentMana, maxMana);
        UpdateEnergyUI();
        UpdateOpponentUI();
    }

    public void DecreaseMaxMana(int amount)
    {
        maxMana = Mathf.Max(maxMana - amount, 0);
        currentMana = Mathf.Min(currentMana, maxMana);
        UpdateEnergyUI();
        UpdateOpponentUI();
    }

    public void DecreaseMaxManaOnly(int amount)
    {
        maxMana = Mathf.Max(maxMana - amount, 0);
        currentMana = Mathf.Min(currentMana, maxMana);
        UpdateEnergyUI();
        UpdateOpponentUI();
    }

    // ================================
    //  手札枚数：Host確定同期
    // ================================

    // InputAuthority（自分）→ StateAuthority（Host）へ「今の手札枚数」を報告
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_ReportHandCount(int newCount)
    {
        handCount = newCount;
    }

    private void ReportHandCountToHost()
    {
        int c = (handManager != null) ? handManager.CardCount : 0;

        // Host（StateAuthority）は自分で確定できる（InputAuthority不要）
        if (Object.HasStateAuthority)
        {
            handCount = c;
            return;
        }

        // Client は InputAuthority を持っている時だけ Host に報告
        if (Object.HasInputAuthority)
        {
            RPC_ReportHandCount(c);
        }
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
        // ★ Host確定(handCount更新)は常に実行
        ReportHandCountToHost();

        // ★ UI更新は “自分の画面” だけ
        if (Object.HasInputAuthority)
            UpdateHandCountUI();
    }


    // 手札枚数UIの更新＆相手の裏面カードを並べる
    public void UpdateHandCountUI()
    {
        // ★ 自分のCanvasだけ更新すればOK
        if (!Object.HasInputAuthority)
            return;

        // 自分の手札枚数（ローカル実体から）
        int myCount = (handManager != null) ? handManager.CardCount : 0;

        if (myCount != lastMyHandCount)
        {
            lastMyHandCount = myCount;

            if (myHandCountText != null)
                myHandCountText.text = myCount.ToString();
        }

        // 相手の手札枚数
        int oppCount = 0;
        if (opponent != null)
            oppCount = opponent.handCount;

        if (opponentHandCountText != null)
            opponentHandCountText.text = oppCount.ToString();

        // 相手の裏面カードを更新
        UpdateOpponentBackCards(oppCount);
    }

    // ライフ枚数UIの更新＆相手ライフの裏面カードを並べる
    public void UpdateLifeUI()
    {
        // 自分のライフ枚数
        int myLife = (lifeManager != null) ? lifeManager.LifeCount : 0;
        if (myLifeCountText != null)
            myLifeCountText.text = myLife.ToString();

        // 相手のライフ枚数
        int oppLife = 0;
        if (opponent != null && opponent.lifeManager != null)
            oppLife = opponent.lifeManager.LifeCount;

        if (opponentLifeCountText != null)
            opponentLifeCountText.text = oppLife.ToString();

        // 相手ライフの裏面カードを更新
        UpdateOpponentLifeBacks(oppLife);
    }

    // 相手のライフ裏面カードを枚数に合わせて増減＆整列
    private void UpdateOpponentLifeBacks(int count)
    {
        if (opponentLifeRoot == null || opponentLifeBackPrefab == null)
            return;

        // 足りないぶん追加
        int current = opponentLifeBackCards.Count;
        for (int i = current; i < count; i++)
        {
            var obj = GameObject.Instantiate(opponentLifeBackPrefab, opponentLifeRoot);
            obj.transform.localScale = Vector3.one;
            opponentLifeBackCards.Add(obj);
        }

        // 余分なぶん削除
        for (int i = opponentLifeBackCards.Count - 1; i >= count; i--)
        {
            var obj = opponentLifeBackCards[i];
            opponentLifeBackCards.RemoveAt(i);
            if (obj != null)
                GameObject.Destroy(obj);
        }

        // 中央揃えで横並び配置
        int n = opponentLifeBackCards.Count;
        if (n == 0) return;

        float spacing = opponentLifeSpacing;
        float totalWidth = spacing * (n - 1);
        float startX = -totalWidth / 2f;

        for (int i = 0; i < n; i++)
        {
            var tr = opponentLifeBackCards[i].transform;
            tr.localPosition = new Vector3(startX + spacing * i, 0f, 0f);
        }
    }

    // 相手の裏面カードを枚数に合わせて増減
    // 相手の裏面カードを枚数に合わせて増減＆整列（HandManager.handCardsは使わない）
    private void UpdateOpponentBackCards(int count)
    {
        if (opponentHandBackRoot == null || opponentBackCardPrefab == null)
            return;

        // 足りないぶん追加
        int current = opponentHandBackCards.Count;
        for (int i = current; i < count; i++)
        {
            var obj = GameObject.Instantiate(opponentBackCardPrefab, opponentHandBackRoot);
            obj.transform.localScale = Vector3.one;
            opponentHandBackCards.Add(obj);
        }

        // 余分なぶん削除
        for (int i = opponentHandBackCards.Count - 1; i >= count; i--)
        {
            var obj = opponentHandBackCards[i];
            opponentHandBackCards.RemoveAt(i);
            if (obj != null)
                GameObject.Destroy(obj);
        }

        // 扇形で並べる（HandManager.UpdateCardPositions と同じ計算）
        int n = opponentHandBackCards.Count;
        if (n == 0) return;

        float totalWidth = oppHandCardSpacing * (n - 1);
        float scaleFactor = 1f;

        if (totalWidth > oppHandMaxWidth)
        {
            scaleFactor = oppHandMaxWidth / totalWidth;
            totalWidth = oppHandMaxWidth;
        }

        for (int i = 0; i < n; i++)
        {
            var tr = opponentHandBackCards[i].transform;

            float x, y, angle;

            if (n == 1)
            {
                x = 0f;
                y = oppHandY;
                angle = 0f;
            }
            else
            {
                float t = (float)i / (n - 1);
                x = -totalWidth / 2f + i * oppHandCardSpacing * scaleFactor;

                if (oppHandArcUp)
                {
                    y = oppHandY - Mathf.Pow(t - 0.5f, 2) * oppHandCurveHeight + oppHandCurveHeight;
                    angle = (t - 0.5f) * oppHandMaxAngle * 2f;
                }
                else
                {
                    y = oppHandY + Mathf.Pow(t - 0.5f, 2) * oppHandCurveHeight;
                    angle = -(t - 0.5f) * oppHandMaxAngle * 2f;
                }
            }

            tr.localPosition = new Vector3(x, y, 0f);
            tr.localRotation = Quaternion.Euler(0f, 0f, angle);
            tr.localScale = Vector3.one * oppHandNormalScale;

            // 見た目の重なり順（必要なら）
            var sr = opponentHandBackCards[i].GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.sortingOrder = oppHandBaseSortingOrder + i * oppHandOrderStep;
        }

    }


    // ================================
    //  相手プレイヤーの参照セット
    // ================================
    public void SetOpponent(PlayerManager pm)
    {
        opponent = pm;
        UpdateOpponentUI();
    }

    // ================================
    //  Networked 値に合わせて UI を補正
    // ================================
    public override void Render()
    {
        base.Render();

        // Networked な currentMana / maxMana / opponent の値に基づいて
        // 毎フレーム UI を最新状態に補正する
        UpdateEnergyUI();
        UpdateOpponentUI();

        // ★ 相手手札枚数はNetworkedで変化するので、見えている側は追従更新
        if (Object.HasInputAuthority)
            UpdateHandCountUI();
    }

}
