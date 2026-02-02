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

    private bool choiceMultiWaiting = false;
    private int expectedChoiceMultiSessionId = -1;
    private PlayerRef expectedChoiceMultiChooserRef;
    private int[] receivedChoiceMultiPickedCounts = null;
    private int choiceMultiSessionSeq = 0;


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
            int processingBlockId = -1;
            bool attackWasNegated = false;

            // ▼追加：processingBlockId は finally で必ず消す
            try
            {
                if (gm != null && gm.Object != null && gm.Object.HasStateAuthority)
                {
                    processingBlockId = gm.BeginProcessingCardHost(blockData.id);
                }

                if (EffectProcessWindow.Instance != null)
                    yield return StartCoroutine(EffectProcessWindow.Instance.ShowProcessAuto($"相手は Block を使用します。【{blockData.name}】", 1.0f, false));
                else
                    yield return new WaitForSeconds(1.0f);

                // ★追加：封印コストなら Block は使用できない（見た目はBlockWindow側でコスト不足扱い）
                if (gm != null && gm.IsCostSealed(blockData.cost))
                {
                    if (EffectProcessWindow.Instance != null)
                        yield return StartCoroutine(EffectProcessWindow.Instance.ShowProcessAuto("Blockカードを使用できません。", 1.0f, false));
                    else
                        yield return new WaitForSeconds(1.0f);
                }
                else
                {
                    // マナ支払い
                    if (defender.currentMana >= blockData.cost && SpendManaSafe(defender, blockData.cost))
                    {
                        defender.UpdateEnergyUI();

                        // ★追加：BlockカードをHost手札リストから消して捨て札へ（handCountのズレ防止）
                        if (gm != null && gm.Object != null && gm.Object.HasStateAuthority)
                        {
                            gm.ConsumeHandCardToDiscardHost(expectedBlockDefenderRef, blockData.id);
                        }

                        // Block効果処理
                        bool attackNegated = false;
                        yield return StartCoroutine(ApplyBlockEffect(defender, attacker, blockData, neg => attackNegated = neg));

                        if (attackNegated)
                        {
                            attackWasNegated = true;

                            if (EffectProcessWindow.Instance != null)
                                yield return StartCoroutine(EffectProcessWindow.Instance.ShowProcessAuto("攻撃は Block により無効化されました。", 1.0f, false));
                            else
                                yield return new WaitForSeconds(1.0f);

                            // ★ここで return/yield break はしない（後段で完了通知を送る）
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
            }
            finally
            {
                // ▼追加：Block表示は必ず消す
                if (gm != null && gm.Object != null && gm.Object.HasStateAuthority)
                {
                    gm.EndProcessingCardHost(processingBlockId);
                }
            }

            // ▼追加：Blockで無効化されたなら「攻撃終了」＋「完了通知」を送って終了
            if (attackWasNegated)
            {
                if (EffectProcessWindow.Instance != null)
                    yield return StartCoroutine(EffectProcessWindow.Instance.ShowProcessAuto("攻撃完了（Blockで無効化）。", 1.0f, false));
                else
                    yield return new WaitForSeconds(1.0f);

                if (gm != null)
                {
                    gm.RPC_AttackResolved(attackerRef, requestId);
                }
                yield break;
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
            blockCard.effectType4, blockCard.effectType5, blockCard.effectType6, blockCard.effectType7,blockCard.effectType8
        };
        string[] values = {
            blockCard.effectValue1, blockCard.effectValue2, blockCard.effectValue3,
            blockCard.effectValue4, blockCard.effectValue5, blockCard.effectValue6, blockCard.effectValue7,blockCard.effectValue8
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

                case "ChoiceMulti":
                    // ※ Block効果中の ChoiceMulti は「入力権限がある側（=この端末のプレイヤー）」のみ実行（まずは最低限）
                    yield return StartCoroutine(DoChoiceMultiBlockRoutine(defender, attacker, blockCard, v));
                    break;

                // ApplyBlockEffect の switch(t) の中に追加（Draw の後あたりが分かりやすい）
                case "SelectDiscardSelf":
                    // value: "ALL" or number (e.g. "2")
                    if (v == "ALL")
                    {
                        yield return StartCoroutine(DoSelectDiscardSelfFromBlock(defender, -1));
                    }
                    else if (int.TryParse(v, out int nDiscard))
                    {
                        yield return StartCoroutine(DoSelectDiscardSelfFromBlock(defender, nDiscard));
                    }
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
            if (EffectProcessWindow.Instance != null)
                yield return StartCoroutine(EffectProcessWindow.Instance.ShowProcessAuto($"{blockCard.name} の反撃効果を発動！", 1.0f, false));
            else
                yield return new WaitForSeconds(1.0f);

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
    // ============================================================
    //  Block効果：SelectDiscardSelf（手札から指定枚数を選んで捨てる）
    //  - HandDiscardSelectManager を開くのは「そのプレイヤーの入力権限側」だけ
    //  - Host側は handCount の変化を待って処理を続行する
    // ============================================================
    private IEnumerator DoSelectDiscardSelfFromBlock(PlayerManager target, int requested)
    {
        if (target == null) yield break;

        var gm = GameManager.Instance ?? FindAnyObjectByType<GameManager>();
        if (gm == null) yield break;

        // Attack処理はHostが回す前提
        if (gm.Object == null || !gm.Object.HasStateAuthority) yield break;
        if (gm.players == null) yield break;

        int targetIndex = gm.players.IndexOf(target);
        if (targetIndex < 0 || targetIndex >= gm.players.Count) yield break;

        int before = target.handCount;
        if (before <= 0) yield break;

        int discardCount;
        if (requested < 0)
        {
            discardCount = before; // ALL
        }
        else
        {
            discardCount = Mathf.Min(Mathf.Max(1, requested), before);
        }

        if (discardCount <= 0) yield break;

        // 演出（短め）
        if (EffectProcessWindow.Instance != null)
            yield return StartCoroutine(EffectProcessWindow.Instance.ShowProcessAuto($"手札から {discardCount} 枚捨てます。", 0.6f, false));
        else
            yield return new WaitForSeconds(0.6f);

        // 選択UIを開く（実際に開くのは inputAuthority のクライアントだけ）
        gm.RPC_OpenSelectDiscardSelf(targetIndex, discardCount);

        // Hostは handCount が減るのを待つ（HandDiscardSelectManager→RPC_RequestDiscardFromHand で更新される）
        int expected = before - discardCount;
        yield return new WaitUntil(() => target.handCount == expected);
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

    // ============================================================
    //  ChoiceMulti（Block効果内での最低限対応）
    //  - defender がこの端末の InputAuthority を持つ場合のみ UI を開いて実行します
    //  - まだ「相手側クライアントにUIを開かせる」同期までは実装していません
    // ============================================================

    private class ChoiceMultiOptionDef_BM
    {
        public string text;
        public List<(string type, string value)> effects = new List<(string type, string value)>();
    }

    private bool TryParseChoiceMultiValue_BM(string raw, out int pickMax, out int sameMax, out List<ChoiceMultiOptionDef_BM> options)
    {
        pickMax = 0;
        sameMax = 0;
        options = new List<ChoiceMultiOptionDef_BM>();

        if (string.IsNullOrEmpty(raw)) return false;

        string[] parts = raw.Split(new char[] { ';', '；', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);

        foreach (var p0 in parts)
        {
            string p = (p0 ?? "").Trim();
            if (string.IsNullOrEmpty(p)) continue;

            if (p.StartsWith("P=", System.StringComparison.OrdinalIgnoreCase) ||
                p.StartsWith("Pick=", System.StringComparison.OrdinalIgnoreCase))
            {
                int eq = p.IndexOf('=');
                if (eq >= 0 && int.TryParse(p.Substring(eq + 1).Trim(), out int pv))
                    pickMax = pv;
                continue;
            }

            if (p.StartsWith("M=", System.StringComparison.OrdinalIgnoreCase) ||
                p.StartsWith("Max=", System.StringComparison.OrdinalIgnoreCase))
            {
                int eq = p.IndexOf('=');
                if (eq >= 0 && int.TryParse(p.Substring(eq + 1).Trim(), out int mv))
                    sameMax = mv;
                continue;
            }

            if (p.StartsWith("O", System.StringComparison.OrdinalIgnoreCase))
            {
                int eq = p.IndexOf('=');
                if (eq < 0) continue;

                string rhs = p.Substring(eq + 1);
                if (string.IsNullOrEmpty(rhs)) continue;

                string display = rhs;
                string effectChain = "";

                int arrow = rhs.IndexOf("=>", System.StringComparison.Ordinal);
                if (arrow >= 0)
                {
                    display = rhs.Substring(0, arrow);
                    effectChain = rhs.Substring(arrow + 2);
                }
                else
                {
                    int bar = rhs.IndexOf('|');
                    if (bar >= 0)
                    {
                        display = rhs.Substring(0, bar);
                        effectChain = rhs.Substring(bar + 1);
                    }
                }

                display = (display ?? "").Trim();
                effectChain = (effectChain ?? "").Trim();

                var opt = new ChoiceMultiOptionDef_BM();
                opt.text = display;

                if (!string.IsNullOrEmpty(effectChain))
                {
                    string[] effs = effectChain.Split(new char[] { '|', '｜' }, System.StringSplitOptions.RemoveEmptyEntries);
                    foreach (var e0 in effs)
                    {
                        string e = (e0 ?? "").Trim();
                        if (string.IsNullOrEmpty(e)) continue;

                        int colon = e.IndexOf(':');
                        string t = (colon >= 0) ? e.Substring(0, colon).Trim() : e;
                        string v = (colon >= 0) ? e.Substring(colon + 1).Trim() : "";

                        if (!string.IsNullOrEmpty(t))
                            opt.effects.Add((t, v));
                    }
                }

                options.Add(opt);
            }
        }

        if (pickMax <= 0) pickMax = 1;
        if (sameMax <= 0) sameMax = 1;

        pickMax = Mathf.Clamp(pickMax, 1, 4);
        sameMax = Mathf.Clamp(sameMax, 1, 4);

        if (options.Count < 2) return false;
        if (options.Count > 4) options = options.GetRange(0, 4);

        return true;
    }

    private IEnumerator DoChoiceMultiBlockRoutine(PlayerManager defender, PlayerManager attacker, CardGenerator.CardData sourceCard, string rawValue)
    {
        var gm = GameManager.Instance ?? FindAnyObjectByType<GameManager>();
        if (gm == null || gm.Object == null || !gm.Object.HasStateAuthority) yield break;

        if (defender == null || defender.Object == null) yield break;

        if (!TryParseChoiceMultiValue_BM(rawValue, out int pickMax, out int sameMax, out List<ChoiceMultiOptionDef_BM> options))
        {
            if (EffectProcessWindow.Instance != null)
                yield return StartCoroutine(EffectProcessWindow.Instance.ShowProcessAuto($"ChoiceMulti value が不正です: {rawValue}", 1.0f, false));
            yield break;
        }

        // --- Hostが「選択者の端末」にUIを開かせる ---
        int sessionId = ++choiceMultiSessionSeq;

        expectedChoiceMultiSessionId = sessionId;
        expectedChoiceMultiChooserRef = defender.Object.InputAuthority;
        receivedChoiceMultiPickedCounts = null;
        choiceMultiWaiting = true;

        int sourceCardId = (sourceCard != null) ? sourceCard.id : -1;

        gm.RPC_OpenChoiceMulti(expectedChoiceMultiChooserRef, sessionId, sourceCardId, rawValue);

        while (choiceMultiWaiting)
            yield return null;

        int[] pickedCounts = receivedChoiceMultiPickedCounts;

        if (pickedCounts == null || pickedCounts.Length == 0)
        {
            if (EffectProcessWindow.Instance != null)
                yield return StartCoroutine(EffectProcessWindow.Instance.ShowProcessAuto("ChoiceMulti: 選択結果が空のため処理を中止します。", 0.8f, false));
            yield break;
        }

        // --- 文章の上から順番に実行 ---
        for (int i = 0; i < options.Count; i++)
        {
            int times = (i < pickedCounts.Length) ? pickedCounts[i] : 0;
            if (times <= 0) continue;

            for (int rep = 0; rep < times; rep++)
            {
                if (EffectProcessWindow.Instance != null)
                    yield return StartCoroutine(EffectProcessWindow.Instance.ShowProcessAuto($"ChoiceMulti: {options[i].text}", 0.6f, true));

                foreach (var eff in options[i].effects)
                {
                    string t = eff.type;
                    string v = eff.value;

                    if (string.IsNullOrEmpty(t)) continue;

                    // ★ここはまず「入力不要」の効果だけ対応（安全版）
                    switch (t)
                    {
                        case "Draw":
                            if (int.TryParse(v, out int drawCount))
                                gm.EffectDraw(defender, drawCount);
                            break;

                        case "ManaBoost":
                            if (int.TryParse(v, out int boost))
                                gm.EffectManaBoost(defender, boost);
                            break;

                        case "ManaRecover":
                            if (v == "ALL")
                            {
                                gm.EffectManaRecover(defender, 0, true);
                            }
                            else if (int.TryParse(v, out int recover))
                            {
                                gm.EffectManaRecover(defender, recover, false);
                            }
                            break;

                        case "LifeAdd":
                            if (int.TryParse(v, out int life))
                                gm.AddLifeToPlayer(defender, life);
                            break;

                        case "RandomDiscardSelf":
                            if (int.TryParse(v, out int rds))
                            {
                                // Host側で実行（RPC関数だがStateAuthorityへ飛ぶのでOK）
                                gm.RPC_RequestRandomDiscard(defender.Object.InputAuthority, defender.Object.InputAuthority, rds);
                            }
                            break;

                        case "RandomDiscardOpponent":
                            if (attacker != null && attacker.Object != null && int.TryParse(v, out int rdo))
                            {
                                gm.RPC_RequestRandomDiscard(defender.Object.InputAuthority, attacker.Object.InputAuthority, rdo);
                            }
                            break;

                        default:
                            if (EffectProcessWindow.Instance != null)
                                yield return StartCoroutine(EffectProcessWindow.Instance.ShowProcessAuto($"ChoiceMulti(Block内): 未対応の効果 [{t}] をスキップしました。", 0.8f, false));
                            break;
                    }

                    // 1フレームだけ進めてUI/同期を落ち着かせる
                    yield return null;
                }
            }
        }
    }


    public void ReceiveChoiceMultiResult(int sessionId, PlayerRef chooserRef, int[] pickedCounts)
    {
        if (!choiceMultiWaiting) return;
        if (sessionId != expectedChoiceMultiSessionId) return;
        if (chooserRef != expectedChoiceMultiChooserRef) return;

        receivedChoiceMultiPickedCounts = pickedCounts;
        choiceMultiWaiting = false;
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
