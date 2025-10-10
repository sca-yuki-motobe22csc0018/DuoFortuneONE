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

    public void SetupInitialLife(int count, DeckManager deckManager)
    {
        if (deckManager == null)
        {
            Debug.LogError("LifeManager.SetupInitialLife: deckManager�����ݒ�ł��B");
            return;
        }

        foreach (var card in lifeCards)
            Destroy(card);
        lifeCards.Clear();
        lifeDataDict.Clear();

        for (int i = 0; i < count; i++)
        {
            var data = deckManager.DrawCardDataOnly();
            if (data == null) break;
            AddLife(data);
        }

        RearrangeLife();
    }

    public void AddLife(CardGenerator.CardData data)
    {
        if (data == null) return;

        GameObject card = Instantiate(lifeCardPrefab, lifeParent);
        card.transform.localScale = Vector3.one;
        card.name = "LifeCard_" + data.id;

        lifeCards.Add(card);
        lifeDataDict[card] = data;

        RearrangeLife();
        AddLife(null);
    }
    public void AddLife()
    {
        AddLife(null);
    }

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

    public CardGenerator.CardData GetDestroyedCard()
    {
        return lastDestroyedCard;
    }

    private void RearrangeLife()
    {
        int count = lifeCards.Count;
        if (count == 0) return;

        // �� ����spacing�v�Z
        float spacing = (count > 1)
            ? Mathf.Max(maxWidth / (count - 1), minSpacing)
            : 0f;

        // �� ���v�����v�Z���Ē�����
        float totalWidth = spacing * (count - 1);
        float startX = -totalWidth / 2f;

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = new Vector3(startX + spacing * i, 0, 0);
            lifeCards[i].transform.localPosition = pos;
        }
    }
}
