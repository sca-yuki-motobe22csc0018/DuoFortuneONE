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

        // ② Block選択ウィンドウ表示（自動ではなく手動選択）
        bool hasPlayableBlock = false;
        CardGenerator.CardData blockData = null;

        if (BlockWindow.Instance != null)
        {
            yield return StartCoroutine(BlockWindow.Instance.ShowBlockChoice(defender));
            blockData = BlockWindow.Instance.GetSelectedBlockData();

            if (blockData != null)
                hasPlayableBlock = true;
        }

        if (hasPlayableBlock && blockData != null)
        {
            yield return EffectProcessWindow.Instance.ShowProcess($"相手は Block を使用します。〔{blockData.name}〕");

            // マナ支払い
            if (defender.currentMana >= blockData.cost && SpendManaSafe(defender, blockData.cost))
            {
                defender.UpdateEnergyUI();

                // Block効果処理
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
                yield return EffectProcessWindow.Instance.ShowProcess("Blockカードを使用できません（マナ不足）。");
            }
        }
        else
        {
            yield return EffectProcessWindow.Instance.ShowProcess("相手は Block を使用しません。");
        }

        // ③ 攻撃が通った → ライフ破壊
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

        // ④ DEFENCEWindow表示（どのカードタイプでも）
        if (destroyedLifeCard != null)
        {
            yield return EffectProcessWindow.Instance.ShowProcess(
                $"破壊されたライフカード〔{destroyedLifeCard.name}〕を確認します。");

            if (DefenceWindow.Instance != null)
            {
                yield return StartCoroutine(DefenceWindow.Instance.ShowDefenceChoice(defender, destroyedLifeCard));
            }
            else
            {
                yield return EffectProcessWindow.Instance.ShowProcess("DefenceWindow が見つかりませんでした。");
            }
        }

        // ⑤ 攻撃終了
        yield return EffectProcessWindow.Instance.ShowProcess("攻撃完了。");
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
                    hasAttack = true;
                    break;

                default:
                    yield return EffectProcessWindow.Instance.ShowProcess($"未対応のBlock効果: {t}（値: {v}）");
                    break;
            }
        }

        // BlockカードがAttack効果を持つ場合 → 反撃
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


        // ★ Blockカード使用後 → 手札から削除し捨て札へ送る
        // Attack効果（反撃）を持つ場合は、反撃が完全に終わってから捨て札に送る
        if (defender != null && blockCard != null)
        {
            if (!hasAttack)
            {
                SendBlockToDiscard(defender, blockCard);
            }
            else
            {
                yield return EffectProcessWindow.Instance.ShowProcess("反撃完了。Blockカードを捨て札へ送ります。");
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
            if (cg != null && cg.cardData == blockCard)
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
