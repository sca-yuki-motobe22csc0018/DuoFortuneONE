using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class BlockWindow : MonoBehaviour
{
    public static BlockWindow Instance;

    [Header("UI Components")]
    public GameObject windowRoot;                  // �S�̃E�B���h�E
    public Transform blockCardParent;              // ScrollView��Content����
    public Transform selectedCardParent;           // �I�𒆃J�[�h�\���G���A
    public Button useButton;                       // �g�p�{�^��
    public Button cancelButton;                    // �g�p���Ȃ��{�^��

    [Header("Prefabs")]
    public GameObject uiCardPrefab;                // UICardPrefab�iInspector�Ŋ��蓖�āj

    private PlayerManager currentPlayer;
    private CardGenerator.CardData selectedBlockData;
    private GameObject selectedCardObj;

    private List<GameObject> generatedCards = new List<GameObject>();
    private System.Action onClose;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        gameObject.SetActive(false);

        if (useButton != null)
            useButton.onClick.AddListener(OnUseClicked);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelClicked);
    }

    /// <summary>
    /// Block�J�[�h�I���E�B���h�E��\��
    /// </summary>
    public IEnumerator ShowBlockChoice(PlayerManager player)
    {
        currentPlayer = player;
        selectedBlockData = null;
        selectedCardObj = null;

        gameObject.SetActive(true);
        useButton.interactable = false;

        // �����̃J�[�h���N���A
        foreach (Transform child in blockCardParent)
            Destroy(child.gameObject);
        foreach (Transform child in selectedCardParent)
            Destroy(child.gameObject);
        generatedCards.Clear();

        // ��D����Block�J�[�h�𒊏o
        if (player != null && player.handManager != null)
        {
            foreach (var go in player.handManager.handCards)
            {
                if (go == null) continue;
                var cg = go.GetComponent<CardGenerator>();
                if (cg == null || cg.cardData == null) continue;
                var data = cg.cardData;

                if (data.type == "B") // Block�J�[�h�̂�
                {
                    GameObject card = Instantiate(uiCardPrefab, blockCardParent);
                    generatedCards.Add(card);

                    var ui = card.GetComponent<CardUI>();
                    if (ui != null)
                        ui.SetCard(data, 1, null, CardUISource.HandZone);

                    CanvasGroup grp = card.AddComponent<CanvasGroup>();
                    if (player.currentMana < data.cost)
                    {
                        grp.alpha = 0.5f;
                        grp.interactable = false;
                    }
                    else
                    {
                        grp.alpha = 1f;
                        grp.interactable = true;
                    }

                    // �N���b�N�C�x���g�o�^
                    EventTrigger trigger = card.GetComponent<EventTrigger>();
                    if (trigger == null) trigger = card.AddComponent<EventTrigger>();

                    EventTrigger.Entry entry = new EventTrigger.Entry();
                    entry.eventID = EventTriggerType.PointerClick;
                    entry.callback.AddListener((dataEvent) => OnCardClicked(card, cg.cardData));
                    trigger.triggers.Add(entry);
                }
            }
        }

        // UI�ҋ@
        bool waiting = true;
        onClose = () => waiting = false;

        while (waiting)
            yield return null;

        gameObject.SetActive(false);
        yield break;
    }

    private void OnCardClicked(GameObject cardObj, CardGenerator.CardData data)
    {
        if (data == null) return;
        // �� �ǉ��F�}�i�s���̃J�[�h�͑I��s��
        if (currentPlayer != null && currentPlayer.currentMana < data.cost)
        {
            Debug.Log($"{data.name}�F�}�i�s���̂��ߑI��s��");
            return;
        }

        RectTransform rt = cardObj.GetComponent<RectTransform>();
        if (rt == null) return;

        // --- �I�������i�߂��j ---
        if (selectedCardObj == cardObj)
        {
            cardObj.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            cardObj.transform.SetParent(blockCardParent, false);
            LayoutRebuilder.ForceRebuildLayoutImmediate(blockCardParent as RectTransform);

            // �� CardUIInteraction �� originalScale ���X�V�I
            var interaction = cardObj.GetComponent<CardUIInteraction>();
            if (interaction != null)
            {
                var field = typeof(CardUIInteraction).GetField("originalScale", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                    field.SetValue(interaction, cardObj.transform.localScale);
            }

            rt.anchoredPosition = Vector2.zero;
            selectedCardObj = null;
            selectedBlockData = null;
            useButton.interactable = false;
            return;
        }

        // --- ���ɕʂ̃J�[�h���I�΂�Ă�����߂� ---
        if (selectedCardObj != null)
        {
            selectedCardObj.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            selectedCardObj.transform.SetParent(blockCardParent, false);
            LayoutRebuilder.ForceRebuildLayoutImmediate(blockCardParent as RectTransform);
            selectedCardObj.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

            // �� �ǉ��F�߂����J�[�h�� CardUIInteraction.originalScale ���X�V
            var interReturn = selectedCardObj.GetComponent<CardUIInteraction>();
            if (interReturn != null)
            {
                var field = typeof(CardUIInteraction).GetField("originalScale", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                    field.SetValue(interReturn, selectedCardObj.transform.localScale);
            }
        }


        // --- �V�����I�� ---
        selectedCardObj = cardObj;
        selectedBlockData = data;
        cardObj.transform.SetParent(selectedCardParent, false);

        // �I������1.0�X�P�[���Œ���
        cardObj.transform.localScale = Vector3.one;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        // originalScale�X�V�ihover�Ή��j
        var inter = cardObj.GetComponent<CardUIInteraction>();
        if (inter != null)
        {
            var field = typeof(CardUIInteraction).GetField("originalScale", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
                field.SetValue(inter, cardObj.transform.localScale);
        }

        useButton.interactable = true;
    }




    private void OnUseClicked()
    {
        if (selectedBlockData == null)
            return;

        onClose?.Invoke();
    }

    private void OnCancelClicked()
    {
        selectedBlockData = null;
        selectedCardObj = null;
        onClose?.Invoke();
    }

    public CardGenerator.CardData GetSelectedBlockData()
    {
        return selectedBlockData;
    }
}
