using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DefenceWindow : MonoBehaviour
{
    public static DefenceWindow Instance;

    [Header("UI Components")]
    public TMP_Text titleText;
    public Transform cardParent;       // UICard��\������e�i���RectTransform�j
    public Button useButton;
    public Button okButton;

    [Header("Prefabs")]
    public GameObject uiCardPrefab;    // Assets/Prefab/UICard.prefab ��Inspector�Ŋ��蓖��

    [Header("Card Display Settings")]
    public Vector3 cardScale = new Vector3(0.8f, 0.8f, 0.8f);  // �����J�[�h�̃X�P�[��

    private bool isWaiting = false;
    private bool useDefence = false;
    private CardGenerator.CardData currentCardData;
    private PlayerManager currentPlayer;

    private GameObject currentCardObj; // ��������UICard

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        gameObject.SetActive(false);

        if (useButton != null) useButton.onClick.AddListener(OnUseClicked);
        if (okButton != null) okButton.onClick.AddListener(OnOkClicked);
    }

    /// <summary>
    /// ���C�t�j�󎞂�DEFENCE�����m�F�E�B���h�E
    /// </summary>
    public IEnumerator ShowDefenceChoice(PlayerManager player, CardGenerator.CardData cardData)
    {
        if (player == null || cardData == null) yield break;

        currentPlayer = player;
        currentCardData = cardData;
        isWaiting = true;
        useDefence = false;

        gameObject.SetActive(true);
        if (titleText != null) titleText.text = "���C�t���j�󂳂�܂����I";

        // ���ɕ\�����̃J�[�h��j��
        if (currentCardObj != null) Destroy(currentCardObj);

        // --- UICard �𐶐����ē��e�𔽉f ---
        if (uiCardPrefab != null && cardParent != null)
        {
            currentCardObj = Instantiate(uiCardPrefab, cardParent);
            // �� �T�C�Y�����iInspector�Őݒ肵���{���𔽉f�j
            currentCardObj.transform.localScale = cardScale;

            // CardUI �ɓ��e�𔽉f�iDiscardManager�͕s�v�Ȃ̂�null�A�]�[���͂ǂ���ł�OK�j
            var ui = currentCardObj.GetComponent<CardUI>();
            if (ui != null)
            {
                ui.SetCard(cardData, 1, null, CardUISource.RecoverZone);
            }

            // �N���b�N�ŏڍׂ��J����悤�� CardUIDetail �ɂ���������n��
            var detail = currentCardObj.GetComponent<CardUIDetail>();
            if (detail != null)
            {
                detail.Init(cardData);
            }
        }
        else
        {
            Debug.LogWarning("DefenceWindow: uiCardPrefab �� cardParent �����ݒ�ł��B");
        }

        // DEFENCE�ȊO�͎g�p�{�^���𖳌���
        if (useButton != null) useButton.interactable = (cardData.type == "D");

        // --- �v���C���[���͑҂� ---
        while (isWaiting)
            yield return null;

        // --- �v���C���[�̑I������ ---
        if (useDefence)
        {
            Debug.Log("DEFENCE����: " + cardData.name);
            yield return StartCoroutine(UseDefenceCard());

            // DEFENCE���g�����ꍇ �� �̂ĎD�]�[���֑���
            if (currentPlayer != null && currentPlayer.discardManager != null)
            {
                currentPlayer.discardManager.AddToDiscard(cardData);
                Debug.Log($"DEFENCE�J�[�h {cardData.name} ���̂ĎD�]�[���֑��M");
            }
        }
        else
        {
            Debug.Log("DEFENCE�𔭓����܂���ł����B");

            // �g��Ȃ������ꍇ �� ��D�ɉ�����
            if (currentPlayer != null && currentPlayer.handManager != null)
            {
                currentPlayer.handManager.AddCardFromData(cardData);
                Debug.Log($"�j�󂳂ꂽ�J�[�h {cardData.name} ����D�ɉ����܂���");
            }
        }

        // �I������
        if (currentCardObj != null)
            Destroy(currentCardObj);

        gameObject.SetActive(false);
    }

    private void OnUseClicked()
    {
        useDefence = true;
        isWaiting = false;

        // �� �C��: gameObject���A�N�e�B�u�ɂ����ACanvasGroup�œ����ɂ��ĕ���
        HideWindowVisual();
    }

    private void HideWindowVisual()
    {
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = gameObject.AddComponent<CanvasGroup>();
        }
        cg.alpha = 0;        // ���S�ɓ���
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }

    private void OnOkClicked()
    {
        useDefence = false;
        isWaiting = false;
    }

    /// <summary>
    /// DEFENCE�J�[�h�̌��ʂ����ۂɎ��s
    /// </summary>
    private IEnumerator UseDefenceCard()
    {
        if (currentCardData == null || currentPlayer == null) yield break;

        GameObject tempCard = new GameObject("TempDefenceCard");
        var cg = tempCard.AddComponent<CardGenerator>();
        cg.player = currentPlayer;
        cg.ApplyCardData(currentCardData);

        yield return cg.StartCoroutine("EffectSequenceCoroutine");

        Destroy(tempCard);

        // �� �ǉ�: �������I�������UI�𕜌�
        RestoreWindowVisual();
    }

    private void RestoreWindowVisual()
    {
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
    }
}
