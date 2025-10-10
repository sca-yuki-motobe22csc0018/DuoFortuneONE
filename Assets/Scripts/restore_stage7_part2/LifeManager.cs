using System.Collections.Generic;
using UnityEngine;

public class LifeManager : MonoBehaviour
{
    [Header("Life Settings")]
    public GameObject lifeCardPrefab; // ���������C�t�J�[�h
    public Transform lifeParent;
    public float maxWidth = 6.0f;     // �z�u�G���A�̍ő啝�i��D�̂悤�Ȓ��S�����j
    public float minSpacing = 0.6f;   // �l�܂肷���h�~�̍ŏ��Ԋu

    private List<GameObject> lifeCards = new List<GameObject>();
    private Dictionary<GameObject, CardGenerator.CardData> lifeDataDict = new Dictionary<GameObject, CardGenerator.CardData>();
    private CardGenerator.CardData lastDestroyedCard = null;

    /// <summary>
    /// �������C�t���R�D����Z�b�g�A�b�v�i�f�[�^�̂݃h���[���Ĕz�u�j
    /// </summary>
    public void SetupInitialLife(int count, DeckManager deckManager)
    {
        if (deckManager == null)
        {
            Debug.LogError("LifeManager.SetupInitialLife: deckManager�����ݒ�ł��B");
            return;
        }

        // �������N���A
        foreach (var card in lifeCards)
            Destroy(card);
        lifeCards.Clear();
        lifeDataDict.Clear();

        // �w�薇���Ԃ�f�[�^�݈̂����ė��������C�t�Ƃ��Ĕz�u
        for (int i = 0; i < count; i++)
        {
            var data = deckManager.DrawCardDataOnly();
            if (data == null) break;
            AddLife(data);
        }

        RearrangeLife();
    }

    /// <summary>
    /// �w��̃J�[�h�f�[�^�Ń��C�t��1���ǉ��i�O������f�[�^���n�����ꍇ�Ɏg�p�j
    /// </summary>
    public void AddLife(CardGenerator.CardData data)
    {
        if (data == null) return;

        GameObject card = Instantiate(lifeCardPrefab, lifeParent);
        card.transform.localScale = Vector3.one;
        card.name = "LifeCard_" + data.id;

        lifeCards.Add(card);
        lifeDataDict[card] = data;

        RearrangeLife();
        // �� �C��: �ȑO�͂����� AddLife(null) ���Ă�ł������폜�i�����ċA�h�~�j
    }

    /// <summary>
    /// �R�D����f�[�^�݈̂����ă��C�t��1���ǉ��iBlock���ʂ�LifeAdd���Ŏg�p�j
    /// </summary>
    public void AddLife()
    {
        // �� �C��: DeckManager��Find���Ď����擾����`�ɕύX�i�O���Q�ƈێ��j
        var deckManager = FindAnyObjectByType<DeckManager>();
        if (deckManager == null)
        {
            Debug.LogWarning("LifeManager.AddLife(): DeckManager ��������܂���B���C�t�ǉ����X�L�b�v���܂��B");
            return;
        }

        var data = deckManager.DrawCardDataOnly();
        if (data == null)
        {
            Debug.LogWarning("LifeManager.AddLife(): �R�D����̂��߃��C�t��ǉ��ł��܂���B");
            return;
        }

        AddLife(data);
    }

    /// <summary>
    /// �����̃��C�t��1���j�󂵁A���̃f�[�^��Ԃ�
    /// </summary>
    public CardGenerator.CardData RemoveLife()
    {
        if (lifeCards.Count == 0) return null;

        GameObject last = lifeCards[lifeCards.Count - 1];
        lifeCards.RemoveAt(lifeCards.Count - 1);

        CardGenerator.CardData destroyedData = null;
        if (lifeDataDict.ContainsKey(last))
        {
            destroyedData = lifeDataDict[last];
            lifeDataDict.Remove(last);
        }

        Destroy(last);
        RearrangeLife();

        lastDestroyedCard = destroyedData;
        return destroyedData;
    }

    /// <summary>
    /// ���߂ɔj�󂳂ꂽ���C�t�J�[�h�̃f�[�^���擾
    /// </summary>
    public CardGenerator.CardData GetDestroyedCard()
    {
        return lastDestroyedCard;
    }

    /// <summary>
    /// ���C�t�̉����z�u�����������i�����񂹁j
    /// </summary>
    private void RearrangeLife()
    {
        int count = lifeCards.Count;
        if (count == 0) return;

        // ����spacing�v�Z
        float spacing = (count > 1)
            ? Mathf.Max(maxWidth / (count - 1), minSpacing)
            : 0f;

        // ���v�����v�Z���Ē�����
        float totalWidth = spacing * (count - 1);
        float startX = -totalWidth / 2f;

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = new Vector3(startX + spacing * i, 0, 0);
            lifeCards[i].transform.localPosition = pos;
        }
    }
}
