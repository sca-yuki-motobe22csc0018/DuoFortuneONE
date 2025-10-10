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

        // �A Block�`�F�b�N�i�v���C�\��Block����D����T���j
        CardGenerator.CardData blockData;
        GameObject blockGO;
        bool hasPlayableBlock = TryGetPlayableBlock(defender, out blockData, out blockGO);

        if (hasPlayableBlock && blockData != null && blockGO != null)
        {
            // �B Block�g�p�i����͎����g�p�BUI�������ɒu�������\�j
            yield return EffectProcessWindow.Instance.ShowProcess($"����� Block ���g�p���܂��B�k{blockData.name}�l");

            // �}�i�x����
            if (defender.currentMana >= blockData.cost && SpendManaSafe(defender, blockData.cost))
            {
                defender.UpdateEnergyUI();

                // �g����Block�J�[�h����D�����菜�� �� �̂ĎD�ցiHandManager����Discad�֑����������j
                if (defender.handManager != null)
                {
                    defender.handManager.RemoveCard(blockGO);
                }
                else
                {
                    // �O�̂��߂̃t�H�[���o�b�N�FCardData�����̂ĎD�ɑ����Ď����j��
                    var discard = FindAnyObjectByType<DiscardManager>();
                    if (discard != null) discard.AddToDiscard(blockData);
                    Destroy(blockGO);
                }

                // �C Block���ʂ̉����iNegateAttack / LifeAdd / ManaRecover / Draw �Ȃǁj
                bool attackNegated = false;
                yield return StartCoroutine(ApplyBlockEffect(defender, blockData, neg => attackNegated = neg));

                if (attackNegated)
                {
                    yield return EffectProcessWindow.Instance.ShowProcess("�U���� Block �ɂ�薳��������܂����B");
                    yield break; // �U���I��
                }
            }
            else
            {
                // �}�i�����肸�g�p�s�\
                yield return EffectProcessWindow.Instance.ShowProcess("����� Block ���g�p�ł��܂���i�}�i�s���j�B");
            }
        }
        else
        {
            yield return EffectProcessWindow.Instance.ShowProcess("����� Block ���g�p���܂���B");
        }

        // �D �U�����ʂ��� �� ���C�t�j��
        yield return EffectProcessWindow.Instance.ShowProcess("�U�������C�t�ɒʂ�܂����B���C�t��j�󂵂܂��B");

        // LifeManager���� RemoveLife() �� CardData ��Ԃ�����
        CardGenerator.CardData destroyedLifeCard = null;
        if (defender.lifeManager != null)
        {
            destroyedLifeCard = defender.lifeManager.RemoveLife();
        }
        else
        {
            Debug.LogWarning("[BattleManager] defender.lifeManager �����ݒ�ł��B");
        }

        // �E �j�󂳂ꂽ���C�t�J�[�h�� DEFENCE �������Ă���΁A�C�Ӕ����E�B���h�E
        if (destroyedLifeCard != null && destroyedLifeCard.type == "D")
        {
            yield return EffectProcessWindow.Instance.ShowProcess("�j�󂳂ꂽ���C�t�� DEFENCE �J�[�h�ł��B�������܂����H");

            if (DefenceWindow.Instance != null)
            {
                // ���Ȃ��̊��ł� ShowDefenceChoice(PlayerManager, CardData) �������ς�
                yield return StartCoroutine(DefenceWindow.Instance.ShowDefenceChoice(defender, destroyedLifeCard));
            }
            else
            {
                // DefenceWindow ���V�[����ɖ����ꍇ
                yield return EffectProcessWindow.Instance.ShowProcess("DefenceWindow ��������܂���ł����BDEFENCE�̓X�L�b�v����܂��B");
            }
        }

        // �F �U���I��
        yield return EffectProcessWindow.Instance.ShowProcess("�U�������B");
    }

    /// <summary>
    /// ��D����u�v���C�\�� Block �J�[�h�v��T��
    /// �Etype == "B"
    /// �Ecost <= currentMana
    /// </summary>
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

            // Block ���� �}�i����������
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
    /// Block���ʂ̎��s�i�������ʂɑΉ��j
    /// �ENegateAttack: �U���������t���O�𗧂Ă�
    /// �ELifeAdd: ���C�t����
    /// �EManaRecover: �}�i��
    /// �EDraw: �R�D�����D�փh���[
    /// �� ���Ή����ʂ̓��b�Z�[�W�\���̂݁i�t���[�͑��s�j
    /// </summary>
    private IEnumerator ApplyBlockEffect(PlayerManager defender, CardGenerator.CardData blockCard, System.Action<bool> onNegateResult)
    {
        bool negated = false;

        string[] types = { blockCard.effectType1, blockCard.effectType2, blockCard.effectType3, blockCard.effectType4, blockCard.effectType5, blockCard.effectType6 };
        string[] values = { blockCard.effectValue1, blockCard.effectValue2, blockCard.effectValue3, blockCard.effectValue4, blockCard.effectValue5, blockCard.effectValue6 };

        for (int i = 0; i < types.Length; i++)
        {
            string t = types[i];
            string v = values[i];
            if (string.IsNullOrEmpty(t)) continue;

            yield return EffectProcessWindow.Instance.ShowProcess($"Block���� [{t}] ���������܂��B");

            switch (t)
            {
                case "Block":
                    // �U���𖳌����i���_���[�W���͈ȍ~�X�L�b�v�j
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

                default:
                    // ���Ή��ł��t���[���~�߂Ȃ��iNext�{�^���͏o���j
                    yield return EffectProcessWindow.Instance.ShowProcess($"���Ή���Block����: {t}�i�l: {v}�j�͖������ł��B");
                    break;
            }
        }

        onNegateResult?.Invoke(negated);
    }

    /// <summary>
    /// ���S�Ƀ}�i���x�����iSpendMana���Ȃ���΃t�H�[���o�b�N�j
    /// </summary>
    private bool SpendManaSafe(PlayerManager p, int cost)
    {
        if (p == null) return false;
        try
        {
            // ������ PlayerManager �� SpendMana(int) ������O��iCardGenerator ����Ă�ł���j
            var mi = typeof(PlayerManager).GetMethod("SpendMana");
            if (mi != null)
            {
                object r = mi.Invoke(p, new object[] { cost });
                if (r is bool b) return b;
            }

            // �t�H�[���o�b�N�F���ڌ��炷�i����E�����͌Ăяo���O�Ƀ`�F�b�N�ς݁j
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
