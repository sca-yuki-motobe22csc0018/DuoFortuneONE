using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EffectProcessWindow : MonoBehaviour
{
    public static EffectProcessWindow Instance;

    [Header("UI Elements")]
    public GameObject windowRoot;         // メッセージウィンドウ全体（常時Active）
    public Text processText;          // メッセージ本文
    public Button nextButton;             // Nextボタン

    private bool waitingForNext = false;
    private bool isShowing = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[EffectProcessWindow] Instance登録OK");
        }
        else if (Instance != this)
        {
            Debug.LogWarning("[EffectProcessWindow] 既存Instanceがあるため自動削除");
            Destroy(gameObject);
            return;
        }

        if (nextButton != null)
        {
            Debug.Log("[EffectProcessWindow] NextButton登録OK");
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(OnNextButton);
        }
        else
        {
            Debug.LogWarning("[EffectProcessWindow] NextButtonが未設定です");
        }

        if (windowRoot != null)
            windowRoot.SetActive(true);

        ShowNextButton(false);

        if (processText != null)
            processText.text = "";
    }

    //=========================================================
    //  Nextボタン待機付きメッセージ表示
    //=========================================================
    public IEnumerator ShowProcess(string message)
    {
        Debug.Log($"[ShowProcess] 呼び出し: {message}");
        if (processText != null)
            processText.text = message;

        ShowNextButton(true);

        waitingForNext = true;
        isShowing = true;

        // Nextが押されるまで待機
        while (waitingForNext)
            yield return null;

        ShowNextButton(false);
        isShowing = false;

        // メッセージを残したくない場合はここで消す
        // processText.text = "";
    }

    //=========================================================
    //  外部からの進行操作
    //=========================================================
    public void ContinueProcess()
    {
        waitingForNext = false;
        ShowNextButton(false);
    }

    //=========================================================
    //  Nextボタン押下時
    //=========================================================
    public void OnNextButton()
    {
        Debug.Log("[EffectProcessWindow] Nextボタン押下"); // ←追加
        if (!waitingForNext)
        {
            Debug.Log("[EffectProcessWindow] waitingForNextがfalseのため無視");
            return;
        }

        Debug.Log("[EffectProcessWindow] waitingForNextをfalseに変更します");
        waitingForNext = false;
    }

    //=========================================================
    //  Nextボタンの表示制御
    //=========================================================
    private void ShowNextButton(bool show)
    {
        if (nextButton != null)
            nextButton.gameObject.SetActive(show);
    }

    //=========================================================
    //  旧構文互換
    //=========================================================
    public void ShowMessage(string message)
    {
        processText.text = message;
        ShowNextButton(false);
    }

    //=========================================================
    //  ウィンドウ全体を閉じたい場合（基本不要）
    //=========================================================
    public void Close()
    {
        ShowNextButton(false);
        processText.text = "";
    }
}
