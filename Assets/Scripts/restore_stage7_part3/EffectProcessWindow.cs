using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EffectProcessWindow : MonoBehaviour
{
    public static EffectProcessWindow Instance;

    [Header("UI Elements")]
    public GameObject windowRoot;         // ���b�Z�[�W�E�B���h�E�S�́i�펞Active�j
    public Text processText;          // ���b�Z�[�W�{��
    public Button nextButton;             // Next�{�^��

    private bool waitingForNext = false;
    private bool isShowing = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[EffectProcessWindow] Instance�o�^OK");
        }
        else if (Instance != this)
        {
            Debug.LogWarning("[EffectProcessWindow] ����Instance�����邽�ߎ����폜");
            Destroy(gameObject);
            return;
        }

        if (nextButton != null)
        {
            Debug.Log("[EffectProcessWindow] NextButton�o�^OK");
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(OnNextButton);
        }
        else
        {
            Debug.LogWarning("[EffectProcessWindow] NextButton�����ݒ�ł�");
        }

        if (windowRoot != null)
            windowRoot.SetActive(true);

        ShowNextButton(false);

        if (processText != null)
            processText.text = "";
    }

    //=========================================================
    //  Next�{�^���ҋ@�t�����b�Z�[�W�\��
    //=========================================================
    public IEnumerator ShowProcess(string message)
    {
        Debug.Log($"[ShowProcess] �Ăяo��: {message}");
        if (processText != null)
            processText.text = message;

        ShowNextButton(true);

        waitingForNext = true;
        isShowing = true;

        // Next���������܂őҋ@
        while (waitingForNext)
            yield return null;

        ShowNextButton(false);
        isShowing = false;

        // ���b�Z�[�W���c�������Ȃ��ꍇ�͂����ŏ���
        // processText.text = "";
    }

    //=========================================================
    //  �O������̐i�s����
    //=========================================================
    public void ContinueProcess()
    {
        waitingForNext = false;
        ShowNextButton(false);
    }

    //=========================================================
    //  Next�{�^��������
    //=========================================================
    public void OnNextButton()
    {
        Debug.Log("[EffectProcessWindow] Next�{�^������"); // ���ǉ�
        if (!waitingForNext)
        {
            Debug.Log("[EffectProcessWindow] waitingForNext��false�̂��ߖ���");
            return;
        }

        Debug.Log("[EffectProcessWindow] waitingForNext��false�ɕύX���܂�");
        waitingForNext = false;
    }

    //=========================================================
    //  Next�{�^���̕\������
    //=========================================================
    private void ShowNextButton(bool show)
    {
        if (nextButton != null)
            nextButton.gameObject.SetActive(show);
    }

    //=========================================================
    //  ���\���݊�
    //=========================================================
    public void ShowMessage(string message)
    {
        processText.text = message;
        ShowNextButton(false);
    }

    //=========================================================
    //  �E�B���h�E�S�̂�������ꍇ�i��{�s�v�j
    //=========================================================
    public void Close()
    {
        ShowNextButton(false);
        processText.text = "";
    }
}
