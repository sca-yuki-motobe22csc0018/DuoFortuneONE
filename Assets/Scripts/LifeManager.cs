using System.Collections.Generic;
using UnityEngine;

public class LifeManager : MonoBehaviour
{
    [Header("Life Settings")]
    public GameObject lifeCardPrefab; // �J�[�h���ʂ̃v���n�u
    public Transform lifeParent;      // ���C�t����ׂ�e�I�u�W�F�N�g
    public float spacing = 1.0f;      // �����т̊Ԋu

    private List<GameObject> lifeCards = new List<GameObject>();

    /// <summary>
    /// �Q�[���J�n���ɏ������C�t��z�u
    /// </summary>
    public void SetupInitialLife(int count)
    {
        // �����̃��C�t���폜
        foreach (var card in lifeCards)
        {
            Destroy(card);
        }
        lifeCards.Clear();

        // ���C�t�������тŐ���
        for (int i = 0; i < count; i++)
        {
            GameObject card = Instantiate(lifeCardPrefab, lifeParent);
            card.transform.localScale = Vector3.one;

            // ���ɕ��ׂ�
            card.transform.localPosition = new Vector3(i * spacing, 0, 0);

            lifeCards.Add(card);
        }

        // �������낦
        float totalWidth = (count - 1) * spacing;
        foreach (var card in lifeCards)
        {
            card.transform.localPosition -= new Vector3(totalWidth / 2f, 0, 0);
        }
    }

    /// <summary>
    /// ���C�t��1���₷
    /// </summary>
    public void AddLife()
    {
        GameObject card = Instantiate(lifeCardPrefab, lifeParent);
        card.transform.localScale = Vector3.one;

        lifeCards.Add(card);
        RearrangeLife();
    }

    /// <summary>
    /// ���C�t��1���炷
    /// </summary>
    public void RemoveLife()
    {
        if (lifeCards.Count == 0) return;

        GameObject last = lifeCards[lifeCards.Count - 1];
        lifeCards.RemoveAt(lifeCards.Count - 1);
        Destroy(last);

        RearrangeLife();
    }

    /// <summary>
    /// �����тɍĔz�u
    /// </summary>
    private void RearrangeLife()
    {
        int count = lifeCards.Count;
        for (int i = 0; i < count; i++)
        {
            lifeCards[i].transform.localPosition = new Vector3(i * spacing, 0, 0);
        }

        float totalWidth = (count - 1) * spacing;
        foreach (var card in lifeCards)
        {
            card.transform.localPosition -= new Vector3(totalWidth / 2f, 0, 0);
        }
    }
}
