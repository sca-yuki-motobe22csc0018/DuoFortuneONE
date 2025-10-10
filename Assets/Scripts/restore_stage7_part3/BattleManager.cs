using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance;

    public EffectProcessWindow processWindow; // �i�s�E�B���h�E�\���p

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
    /// �U�������{��
    /// </summary>
    public IEnumerator HandleAttack(PlayerManager attacker, PlayerManager defender, CardGenerator.CardData attackCard)
    {
        if (attacker == null || defender == null || attackCard == null)
        {
            Debug.LogError("HandleAttack: ������null�ł��B");
            yield break;
        }

        if (processWindow != null)
            processWindow.ShowMessage($"{attacker.name} �̍U���I {attackCard.name}");

        yield return new WaitForSeconds(0.6f);

        // --- �u���b�N���� ---
        if (CheckBlockOpportunity(defender))
        {
            if (processWindow != null)
                processWindow.ShowMessage("BLOCK�J�[�h�������I �U���͖���������܂����I");
            yield return new WaitForSeconds(0.6f);
            yield break; // �U���I��
        }

        // --- �U������ �� ���C�t�j�� ---
        CardGenerator.CardData destroyedCard = null;
        if (defender.lifeManager != null)
        {
            destroyedCard = defender.lifeManager.RemoveLife();
            if (processWindow != null)
                processWindow.ShowMessage($"{defender.name} �̃��C�t��1���j�󂳂ꂽ�I");
        }

        yield return new WaitForSeconds(0.6f);

        // --- DEFENCE �`�F�b�N ---
        if (destroyedCard != null)
        {
            if (processWindow != null)
                processWindow.ShowMessage("DEFENCE�����\�ȃJ�[�h���m�F���c");

            yield return DefenceWindow.Instance.ShowDefenceChoice(defender, destroyedCard);

            yield return new WaitForSeconds(0.6f);
        }

        // --- �U�����̎c����ʁi�ǉ������Ȃǁj---
        if (processWindow != null)
            processWindow.ShowMessage("�U���������������܂����B");

        yield return new WaitForSeconds(0.5f);
    }

    /// <summary>
    /// BLOCK�����J�[�h����D�ɂ��邩�m�F
    /// </summary>
    private bool CheckBlockOpportunity(PlayerManager defender)
    {
        if (defender == null || defender.handManager == null)
            return false;

        // ��D�� Transform �z���ɂ��邷�ׂẴJ�[�h�𑖍�
        foreach (Transform cardTransform in defender.handManager.transform)
        {
            CardGenerator cg = cardTransform.GetComponent<CardGenerator>();
            if (cg == null) continue;

            // �� ���Ȃ���CardGenerator�ł́umyData�v���J�[�h��������
            var data = cg.cardData;
            if (data != null && data.type == "B")
            {
                Debug.Log($"BLOCK�J�[�h���o: {data.name}");
                return true;
            }
        }

        return false;
    }
}
