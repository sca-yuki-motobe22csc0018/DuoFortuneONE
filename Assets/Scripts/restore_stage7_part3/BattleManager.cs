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
    /// �U���̃t���[���g�S���h�����œ�������iShowProcess�FNext�{�^���҂��Ή��j
    /// </summary>
    public IEnumerator HandleAttack(PlayerManager attacker, PlayerManager defender, CardGenerator.CardData attackCard)
    {
        if (attacker == null || defender == null || attackCard == null)
            yield break;

        // �@ �U���錾
        yield return EffectProcessWindow.Instance.ShowProcess($"�U���I �k{attackCard.name}�l");

        // �A Block�I���E�B���h�E�\���i�����ł͂Ȃ��蓮�I���j
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
            yield return EffectProcessWindow.Instance.ShowProcess($"����� Block ���g�p���܂��B�k{blockData.name}�l");

            // �}�i�x����
            if (defender.currentMana >= blockData.cost && SpendManaSafe(defender, blockData.cost))
            {
                defender.UpdateEnergyUI();

                // Block���ʏ���
                bool attackNegated = false;
                yield return StartCoroutine(ApplyBlockEffect(defender, blockData, neg => attackNegated = neg));

                if (attackNegated)
                {
                    yield return EffectProcessWindow.Instance.ShowProcess("�U���� Block �ɂ�薳��������܂����B");
                    yield break;
                }
            }
            else
            {
                yield return EffectProcessWindow.Instance.ShowProcess("Block�J�[�h���g�p�ł��܂���i�}�i�s���j�B");
            }
        }
        else
        {
            yield return EffectProcessWindow.Instance.ShowProcess("����� Block ���g�p���܂���B");
        }

        // �B �U�����ʂ��� �� ���C�t�j��
        yield return EffectProcessWindow.Instance.ShowProcess("�U�������C�t�ɒʂ�܂����B���C�t��j�󂵂܂��B");

        CardGenerator.CardData destroyedLifeCard = null;
        if (defender.lifeManager != null)
        {
            destroyedLifeCard = defender.lifeManager.RemoveLife();
        }
        else
        {
            Debug.LogWarning("[BattleManager] defender.lifeManager �����ݒ�ł��B");
        }

        // �C DEFENCEWindow�\���i�ǂ̃J�[�h�^�C�v�ł��j
        if (destroyedLifeCard != null)
        {
            yield return EffectProcessWindow.Instance.ShowProcess(
                $"�j�󂳂ꂽ���C�t�J�[�h�k{destroyedLifeCard.name}�l���m�F���܂��B");

            if (DefenceWindow.Instance != null)
            {
                yield return StartCoroutine(DefenceWindow.Instance.ShowDefenceChoice(defender, destroyedLifeCard));
            }
            else
            {
                yield return EffectProcessWindow.Instance.ShowProcess("DefenceWindow ��������܂���ł����B");
            }
        }

        // �D �U���I��
        yield return EffectProcessWindow.Instance.ShowProcess("�U�������B");
    }

    /// <summary>
    /// Block���ʁiAttack�t���Ή��Łj
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

            yield return EffectProcessWindow.Instance.ShowProcess($"Block���� [{t}] ���������܂��B");

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
                    yield return EffectProcessWindow.Instance.ShowProcess($"���Ή���Block����: {t}�i�l: {v}�j");
                    break;
            }
        }

        // Block�J�[�h��Attack���ʂ����ꍇ �� ����
        if (hasAttack)
        {
            yield return EffectProcessWindow.Instance.ShowProcess($"{blockCard.name} �̔������ʂ𔭓��I");

            PlayerManager counterAttacker = defender;
            GameManager gm = FindAnyObjectByType<GameManager>();
            PlayerManager counterDefender = (gm != null && counterAttacker == gm.player1)
                ? gm.player2
                : gm.player1;

            yield return StartCoroutine(HandleAttack(counterAttacker, counterDefender, blockCard));
        }


        // �� Block�J�[�h�g�p�� �� ��D����폜���̂ĎD�֑���
        // Attack���ʁi�����j�����ꍇ�́A���������S�ɏI����Ă���̂ĎD�ɑ���
        if (defender != null && blockCard != null)
        {
            if (!hasAttack)
            {
                SendBlockToDiscard(defender, blockCard);
            }
            else
            {
                yield return EffectProcessWindow.Instance.ShowProcess("���������BBlock�J�[�h���̂ĎD�֑���܂��B");
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
