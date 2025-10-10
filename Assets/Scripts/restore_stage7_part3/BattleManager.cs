using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// 攻撃のフローを“全部”ここで統括する（ShowProcess：Nextボタン待ち対応）
    /// </summary>
    public IEnumerator HandleAttack(PlayerManager attacker, PlayerManager defender, CardGenerator.CardData attackCard)
    {
        if (attacker == null || defender == null || attackCard == null)
            yield break;

        // ① 攻撃宣言
        yield return EffectProcessWindow.Instance.ShowProcess($"攻撃！ 〔{attackCard.name}〕");

        // ② Blockチェック（プレイ可能なBlockを手札から探す）
        CardGenerator.CardData blockData;
        GameObject blockGO;
        bool hasPlayableBlock = TryGetPlayableBlock(defender, out blockData, out blockGO);

        if (hasPlayableBlock && blockData != null && blockGO != null)
        {
            // ③ Block使用
            yield return EffectProcessWindow.Instance.ShowProcess($"相手は Block を使用します。〔{blockData.name}〕");

            // マナ支払い
            if (defender.currentMana >= blockData.cost && SpendManaSafe(defender, blockData.cost))
            {
                defender.UpdateEnergyUI();

                // 使ったBlockカードを手札から取り除き → 捨て札へ
                if (defender.handManager != null)
                {
                    defender.handManager.RemoveCard(blockGO);
                }
                else
                {
                    var discard = FindAnyObjectByType<DiscardManager>();
                    if (discard != null) discard.AddToDiscard(blockData);
                    Destroy(blockGO);
                }

                // ④ Block効果の解決（拡張：Attack効果を含む場合は反撃）
                bool attackNegated = false;
                yield return StartCoroutine(ApplyBlockEffect(defender, blockData, neg => attackNegated = neg));

                if (attackNegated)
                {
                    yield return EffectProcessWindow.Instance.ShowProcess("攻撃は Block により無効化されました。");
                    yield break;
                }
            }
            else
            {
                yield return EffectProcessWindow.Instance.ShowProcess("相手は Block を使用できません（マナ不足）。");
            }
        }
        else
        {
            yield return EffectProcessWindow.Instance.ShowProcess("相手は Block を使用しません。");
        }

        // ⑤ 攻撃が通った → ライフ破壊
        yield return EffectProcessWindow.Instance.ShowProcess("攻撃がライフに通りました。ライフを破壊します。");

        CardGenerator.CardData destroyedLifeCard = null;
        if (defender.lifeManager != null)
        {
            destroyedLifeCard = defender.lifeManager.RemoveLife();
        }
        else
        {
            Debug.LogWarning("[BattleManager] defender.lifeManager が未設定です。");
        }

        // ★ ライフカードのタイプに関係なく DefenceWindow を表示
        if (destroyedLifeCard != null)
        {
            yield return EffectProcessWindow.Instance.ShowProcess(
                $"破壊されたライフカード〔{destroyedLifeCard.name}〕を確認します。");

            if (DefenceWindow.Instance != null)
            {
                // DefenceWindow はどのカードでも表示、Useボタンはtype==Dのみ有効
                yield return StartCoroutine(DefenceWindow.Instance.ShowDefenceChoice(defender, destroyedLifeCard));
            }
            else
            {
                yield return EffectProcessWindow.Instance.ShowProcess("DefenceWindow が見つかりませんでした。確認をスキップします。");
            }
        }

        // ⑦ 攻撃終了
        yield return EffectProcessWindow.Instance.ShowProcess("攻撃完了。");
    }

    private bool TryGetPlayableBlock(PlayerManager player, out CardGenerator.CardData blockData, out GameObject blockGO)
    {
        blockData = null;
        blockGO = null;

        if (player == null || player.handManager == null) return false;
        var list = player.handManager.handCards;
        if (list == null || list.Count == 0) return false;

        foreach (var go in list)
        {
            if (go == null) continue;
            var cg = go.GetComponent<CardGenerator>();
            if (cg == null) continue;

            var data = cg.cardData;
            if (data == null) continue;

            if (data.type == "B" && player.currentMana >= data.cost)
            {
                blockData = data;
                blockGO = go;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Block効果（Attack付き対応版）
    /// </summary>
    private IEnumerator ApplyBlockEffect(PlayerManager defender, CardGenerator.CardData blockCard, System.Action<bool> onNegateResult)
    {
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

            yield return EffectProcessWindow.Instance.ShowProcess($"Block効果 [{t}] を解決します。");

            switch (t)
            {
                case "Block":
                    negated = true;
                    break;

                case "LifeAdd":
                    if (int.TryParse(v, out int lifePlus) && defender != null && defender.lifeManager != null)
                    {
                        for (int k = 0; k < lifePlus; k++)
                            defender.lifeManager.AddLife();
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
                        var deck = FindAnyObjectByType<DeckManager>();
                        if (deck != null)
                        {
                            for (int d = 0; d < drawN; d++)
                                deck.DrawCardToHand(defender);
                        }
                    }
                    break;

                case "Attack":
                    // ★ 新規: BlockカードにAttack効果がある場合、反撃フラグを立てる
                    hasAttack = true;
                    break;

                default:
                    yield return EffectProcessWindow.Instance.ShowProcess($"未対応のBlock効果: {t}（値: {v}）は未実装です。");
                    break;
            }
        }

        // ★ BlockカードがAttack効果を持っている場合 → 反撃開始
        if (hasAttack)
        {
            yield return EffectProcessWindow.Instance.ShowProcess($"{blockCard.name} の反撃効果を発動！");

            PlayerManager counterAttacker = defender;
            GameManager gm = FindAnyObjectByType<GameManager>();
            PlayerManager counterDefender = (gm != null && counterAttacker == gm.player1)
                ? gm.player2
                : gm.player1;

            yield return StartCoroutine(HandleAttack(counterAttacker, counterDefender, blockCard));
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
}
