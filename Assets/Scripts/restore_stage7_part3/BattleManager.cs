using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance;

    private bool blockWaiting = false;
    private int receivedBlockCardId = -1;
    private PlayerRef expectedBlockDefenderRef;

    private bool defenceWaiting = false;
    private bool receivedUseDefence = false;
    private PlayerRef expectedDefenceDefenderRef;



    private struct AttackJob
    {
        public PlayerManager attacker;
        public PlayerManager defender;
        public CardGenerator.CardData attackCard;

        // ★追加
        public PlayerRef attackerRef;
        public int requestId;

        public AttackJob(PlayerManager a, PlayerManager d, CardGenerator.CardData c, PlayerRef ar, int rid)
        {
            attacker = a;
            defender = d;
            attackCard = c;
            attackerRef = ar;
            requestId = rid;
        }
    }


    private readonly Queue<AttackJob> attackQueue = new Queue<AttackJob>();
    private bool isProcessingAttackQueue = false;

    // ★ 追加：Hostだけが攻撃を積む
    public void EnqueueAttack(PlayerManager attacker, PlayerManager defender, CardGenerator.CardData attackCard, PlayerRef attackerRef, int requestId)
    {
        var gm = GameManager.Instance ?? FindAnyObjectByType<GameManager>();
        if (gm == null || !gm.Object.HasStateAuthority) return;

        attackQueue.Enqueue(new AttackJob(attacker, defender, attackCard, attackerRef, requestId));

        if (!isProcessingAttackQueue)
        {
            StartCoroutine(ProcessAttackQueue());
        }
    }

    // ★ 追加：キューを順番に解決
    private IEnumerator ProcessAttackQueue()
    {
        isProcessingAttackQueue = true;

        while (attackQueue.Count > 0)
        {
            AttackJob job = attackQueue.Dequeue();
            yield return StartCoroutine(HandleAttack(job.attacker, job.defender, job.attackCard, job.attackerRef, job.requestId));
            yield return null;
        }

        isProcessingAttackQueue = false;
    }


    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void ReceiveBlockChoice(PlayerRef defenderRef, int chosenCardIdOrMinusOne)
    {
        var gm = GameManager.Instance ?? FindAnyObjectByType<GameManager>();
        if (gm == null || !gm.Object.HasStateAuthority) return;

        if (!blockWaiting) return;
        if (defenderRef != expectedBlockDefenderRef) return;

        receivedBlockCardId = chosenCardIdOrMinusOne;
        blockWaiting = false;
    }

    public void ReceiveDefenceChoiceDone(PlayerRef defenderRef, bool usedDefence)
    {
        var gm = GameManager.Instance ?? FindAnyObjectByType<GameManager>();
        if (gm == null || !gm.Object.HasStateAuthority) return;

        if (!defenceWaiting) return;
        if (defenderRef != expectedDefenceDefenderRef) return;

        receivedUseDefence = usedDefence;
        defenceWaiting = false;
    }

    /// <summary>
    /// 攻撃のフローを“全部”ここで統括する（ShowProcess：Nextボタン待ち対応）
    /// </summary>
    public IEnumerator HandleAttack(PlayerManager attacker, PlayerManager defender, CardGenerator.CardData attackCard, PlayerRef attackerRef, int requestId)
    {
        // ★ 追加：Host 以外では戦闘処理をしない
        var gm = GameManager.Instance ?? FindAnyObjectByType<GameManager>();
        if (gm == null || !gm.Object.HasStateAuthority)
            yield break;

        if (attacker == null || defender == null || attackCard == null)
            yield break;

        // defender が players の何番目かを覚えておく（ライフ同期用）
        int defenderIndex = gm.players.IndexOf(defender);

        // ① 攻撃宣言
        if (EffectProcessWindow.Instance != null)
            yield return StartCoroutine(EffectProcessWindow.Instance.ShowProcessAuto($"攻撃！【{attackCard.name}】", 1.0f, false));
        else
            yield return new WaitForSeconds(1.0f);


        // ② Block選択ウインドウ表示（自動ではなく手動選択）
        bool hasPlayableBlock = false;
        CardGenerator.CardData blockData = null;

        // ★ BlockWindow は defender 側の画面に表示し、選択結果だけ Host が受け取る
        blockWaiting = true;
        receivedBlockCardId = -1;
        expectedBlockDefenderRef = defender.Object.InputAuthority;

        if (gm != null)
        {
            gm.RPC_OpenBlockChoice(expectedBlockDefenderRef);
        }

        while (blockWaiting)
            yield return null;

        if (receivedBlockCardId >= 0 && gm != null && gm.deckManager != null)
        {
            blockData = gm.deckManager.GetCardDataById(receivedBlockCardId);
            if (blockData != null)
                hasPlayableBlock = true;
        }

        if (hasPlayableBlock && blockData != null)
        {
            if (EffectProcessWindow.Instance != null)
                yield return StartCoroutine(EffectProcessWindow.Instance.ShowProcessAuto($"相手は Block を使用します。【{blockData.name}】", 1.0f, false));
            else
                yield return new WaitForSeconds(1.0f);

            // マナ支払い
            if (defender.currentMana >= blockData.cost && SpendManaSafe(defender, blockData.cost))
            {
                defender.UpdateEnergyUI();

                // Block効果処理
                bool attackNegated = false;
                yield return StartCoroutine(ApplyBlockEffect(defender, attacker, blockData, neg => attackNegated = neg));



                if (attackNegated)
                {
                    if (EffectProcessWindow.Instance != null)
                        yield return StartCoroutine(EffectProcessWindow.Instance.ShowProcessAuto("攻撃は Block により無効化されました。", 1.0f, false));
                    else
                        yield return new WaitForSeconds(1.0f);
                    yield break;
                }
            }
            else
            {
                if (EffectProcessWindow.Instance != null)
                    yield return StartCoroutine(EffectProcessWindow.Instance.ShowProcessAuto("Blockカードを使用できません。", 1.0f, false));
                else
                    yield return new WaitForSeconds(1.0f);
            }
        }
        else
        {
            if (EffectProcessWindow.Instance != null)
                yield return StartCoroutine(EffectProcessWindow.Instance.ShowProcessAuto("相手は Block を使用しません。", 1.0f, false));
            else
                yield return new WaitForSeconds(1.0f);
        }

        // ③ 攻撃が通った → ライフ破壊
        if (EffectProcessWindow.Instance != null)
            yield return StartCoroutine(EffectProcessWindow.Instance.ShowProcessAuto("攻撃がライフに通りました。ライフを破壊します。", 1.0f, false));
        else
            yield return new WaitForSeconds(1.0f);

        CardGenerator.CardData destroyedLifeCard = null;
        if (defender.lifeManager != null)
        {
            // ★ Host 側で 1 回だけライフを削る
            destroyedLifeCard = defender.lifeManager.RemoveLife();

            // ★ 他クライアントにも「同じプレイヤーのライフを1枚削れ」と通知
            if (defenderIndex >= 0)
            {
                gm.RPC_SyncRemoveLife(defenderIndex);
            }
        }
        else
        {
            Debug.LogWarning("[BattleManager] defender.lifeManager が未設定です。");
        }

        // ④ DEFENCEWindow表示（どのカードタイプでも）
        if (destroyedLifeCard != null)
        {
            if (EffectProcessWindow.Instance != null)
                yield return StartCoroutine(EffectProcessWindow.Instance.ShowProcessAuto($"破壊されたライフカード【{destroyedLifeCard.name}】を確認します。", 1.0f, false));
            else
                yield return new WaitForSeconds(1.0f);

            // ★ DefenceWindow は defender 側の画面に表示し、完了通知を Host が受け取る
            defenceWaiting = true;
            receivedUseDefence = false;
            expectedDefenceDefenderRef = defender.Object.InputAuthority;

            if (gm != null)
            {
                gm.RPC_OpenDefenceChoice(expectedDefenceDefenderRef, destroyedLifeCard.id);
            }

            while (defenceWaiting)
                yield return null;
        }
        // ▼追加：Attackの処理がすべて終わったタイミングでライフ0なら勝敗決定
        if (gm != null && gm.Object != null && gm.Object.HasStateAuthority)
        {
            if (gm.TryEndGameByLifeZeroAfterAttack(attacker, defender))
                yield break; // 勝敗がついたら以降の表示/処理は打ち切り
        }

        // ⑤ 攻撃終了
        if (EffectProcessWindow.Instance != null)
            yield return StartCoroutine(EffectProcessWindow.Instance.ShowProcessAuto("攻撃完了。", 1.0f, false));
        else
            yield return new WaitForSeconds(1.0f);

        // ★追加：攻撃完了通知（Host -> 全員）
        if (gm != null)
        {
            gm.RPC_AttackResolved(attackerRef, requestId);
        }
    }

    int counterAttackCardId = -1;
    bool hasCounterAttack = false;

    /// <summary>
    /// Block効果（Attack付き対応版）
    /// </summary>
    private IEnumerator ApplyBlockEffect(PlayerManager defender, PlayerManager attacker, CardGenerator.CardData blockCard, System.Action<bool> onNegateResult)
    {
        // ★毎回リセットしないと状態が残る
        hasCounterAttack = false;
        counterAttackCardId = -1;

        bool negated = false;

        string[] types = {
            blockCard.effectType1, blockCard.effectType2, blockCard.effectType3,
            blockCard.effectType4, blockCard.effectType5, blockCard.effectType6
        };
        string[] values = {
            blockCard.effectValue1, blockCard.effectValue2, blockCard.effectValue3,
            blockCard.effectValue4, blockCard.effectValue5, blockCard.effectValue6
        };

        bool hasAttack = false;

        for (int i = 0; i < types.Length; i++)
        {
            string t = types[i];
            string v = values[i];
            if (string.IsNullOrEmpty(t)) continue;

            if (EffectProcessWindow.Instance != null)
                yield return StartCoroutine(EffectProcessWindow.Instance.ShowProcessAuto($"Block効果 [{t}] を解決します。", 1.0f, false));
            else
                yield return new WaitForSeconds(1.0f);

            switch (t)
            {
                case "Block":
                    negated = true;
                    break;

                case "LifeAdd":
                    if (int.TryParse(v, out int lifePlus) && defender != null && defender.lifeManager != null)
                    {
                        var gm = GameManager.Instance ?? FindAnyObjectByType<GameManager>();
                        if (gm != null)
                        {
                            // Host 側で山札からライフカードを引き → 全員に同期
                            gm.AddLifeToPlayer(defender, lifePlus);
                        }
                        else
                        {
                            // 念のためのオフライン用フォールバック
                            for (int k = 0; k < lifePlus; k++)
                                defender.lifeManager.AddLife();
                        }
                    }
                    break;

                case "ManaBoost":
                    if (int.TryParse(v, out int manaBoost) && defender != null)
                    {
                        var gm = GameManager.Instance ?? FindAnyObjectByType<GameManager>();
                        if (gm != null && gm.Object != null && gm.Object.HasStateAuthority)
                        {
                            gm.EffectManaBoost(defender, manaBoost);
                        }
                    }
                    break;


                case "ManaRecover":
                    if (int.TryParse(v, out int manaRec) && defender != null)
                    {
                        defender.currentMana = Mathf.Min(defender.maxMana, defender.currentMana + manaRec);
                        defender.UpdateEnergyUI();
                    }
                    break;

                case "Draw":
                    if (int.TryParse(v, out int drawN))
                    {
                        // ★ 修正ポイント：ローカルで DeckManager に直接ドローさせない
                        //    → Host だけが GameManager 経由で EffectDraw し、
                        //      RPC_ApplyDraw で全員の手札が同期される
                        var gm = GameManager.Instance;
                        if (gm != null && gm.Object != null && gm.Object.HasStateAuthority)
                        {
                            gm.EffectDraw(defender, drawN);
                        }
                    }
                    break;

                case "CounterAttack":
                    hasCounterAttack = true;
                    negated = true; // 「Blockして止めてAttackする」を1つで成立させる
                    if (int.TryParse(v, out int cid)) counterAttackCardId = cid; // 任意：反撃に使うカードID指定
                    break;

                case "Attack":
                    hasAttack = true;
                    break;

                default:
                    if (EffectProcessWindow.Instance != null)
                        yield return StartCoroutine(EffectProcessWindow.Instance.ShowProcessAuto($"未対応のBlock効果: {t}(値: {v})", 1.0f, false));
                    else
                        yield return new WaitForSeconds(1.0f);
                    break;
            }
        }

        // BlockカードがAttack効果を持つ場合 → 反撃
        if (hasCounterAttack)
        {
            yield return EffectProcessWindow.Instance.ShowProcess($"{blockCard.name} の反撃効果を発動！");

            // 反撃は「Blockした側(defender) → 攻撃してきた側(attacker)」に固定
            PlayerManager counterAttacker = defender;
            PlayerManager counterDefender = attacker;

            // 反撃に使うカード（指定があればそれ、無ければblockCardをそのまま使う）
            CardGenerator.CardData counterCard = blockCard;

            var gm = GameManager.Instance ?? FindAnyObjectByType<GameManager>();
            if (counterAttackCardId >= 0 && gm != null && gm.deckManager != null)
            {
                var cd = gm.deckManager.GetCardDataById(counterAttackCardId);
                if (cd != null) counterCard = cd;
            }
            if (counterDefender != null)
                yield return StartCoroutine(HandleAttack(
    counterAttacker,
    counterDefender,
    counterCard,
    counterAttacker.Object.InputAuthority,
    -1
));
        }



        // ★ Blockカード使用後 → 手札から削除して捨て札へ送る
        // Attack効果（反撃）を持つ場合は、反撃が完全に終わってから捨て札へ送る
        if (defender != null && blockCard != null)
        {
            if (!hasAttack)
            {
                SendBlockToDiscard(defender, blockCard);
            }
            else
            {
                if (EffectProcessWindow.Instance != null)
                    yield return StartCoroutine(EffectProcessWindow.Instance.ShowProcessAuto("反撃完了。Blockカードを捨て札へ送ります。", 1.0f, false));
                else
                    yield return new WaitForSeconds(1.0f);
                SendBlockToDiscard(defender, blockCard);
            }
        }

        onNegateResult?.Invoke(negated);
    }

    private bool SpendManaSafe(PlayerManager p, int cost)
    {
        if (p == null) return false;
        try
        {
            var mi = typeof(PlayerManager).GetMethod("SpendMana");
            if (mi != null)
            {
                object r = mi.Invoke(p, new object[] { cost });
                if (r is bool b) return b;
            }

            if (p.currentMana >= cost)
            {
                p.currentMana -= cost;
                return true;
            }
            return false;
        }
        catch
        {
            if (p.currentMana >= cost)
            {
                p.currentMana -= cost;
                return true;
            }
            return false;
        }
    }

    private void SendBlockToDiscard(PlayerManager defender, CardGenerator.CardData blockCard)
    {
        var hand = defender?.handManager;
        if (hand == null || blockCard == null) return;

        GameObject cardObj = null;
        foreach (Transform t in hand.transform)
        {
            var cg = t.GetComponent<CardGenerator>();
            if (cg != null && cg.cardID == blockCard.id)
            {
                cardObj = t.gameObject;
                break;
            }
        }

        if (cardObj != null)
        {
            hand.RemoveCard(cardObj);
            GameObject.Destroy(cardObj);
        }
    }
}
