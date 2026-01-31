using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using System.Collections;
using System.Linq;

public class CostSealDeclareUI : MonoBehaviour
{
    [Header("UI Root")]
    public GameObject panelRoot;

    [Header("Buttons")]
    public Button[] numberButtons;   // size=10 (1..10)
    public Button okButton;

    [Header("Texts")]
    public TMP_Text centerChoiceText;      // 画面中央表示（選択直後/結果）
    public TMP_Text persistentSealedText;  // 常時表示（宣言が無いなら非表示）

    [Header("Visual")]
    [Range(0.1f, 1f)] public float dimAlpha = 0.45f;
    [Range(0.1f, 1f)] public float brightAlpha = 1.0f;

    private int selectedCost = -1;
    private int sessionId = -1;

    private GameManager gm;
    private NetworkRunner runner;

    // ★追加：中央表示の消去コルーチンを管理
    private Coroutine centerHideCo;


    private void Awake()
    {
        gm = GameManager.Instance ?? FindAnyObjectByType<GameManager>();
        runner = FindAnyObjectByType<NetworkRunner>();

        if (okButton != null)
            okButton.onClick.AddListener(OnClickOk);

        if (numberButtons != null && numberButtons.Length == 10)
        {
            for (int i = 0; i < 10; i++)
            {
                int cost = i + 1;
                if (numberButtons[i] != null)
                {
                    numberButtons[i].onClick.AddListener(() => OnClickNumber(cost));
                }
            }
        }

        HidePanelImmediate();
        HideCenterImmediate();
        SetPersistentCosts(null);
    }

    public void Open(int sid)
    {
        sessionId = sid;
        selectedCost = -1;

        if (panelRoot != null) panelRoot.SetActive(true);

        if (okButton != null) okButton.interactable = false;

        // 最初は全部暗い
        UpdateNumberButtonVisuals(-1);

        // 選択後すぐに中央表示（選択されたら出すので、最初は消す）
        HideCenterImmediate();
    }

    public void Close()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        selectedCost = -1;
        sessionId = -1;
        HideCenterImmediate();
    }

    private void OnClickNumber(int cost)
    {
        selectedCost = cost;

        // ボタンの明暗だけ更新（中央表示は出さない）
        UpdateNumberButtonVisuals(selectedCost);

        // 念のため、選択中は中央表示を消しておく（OK確定後にだけ出す）
        HideCenterImmediate();

        if (okButton != null) okButton.interactable = true;
    }

    private void OnClickOk()
    {
        if (selectedCost < 1 || selectedCost > 10) return;
        if (gm == null) gm = GameManager.Instance ?? FindAnyObjectByType<GameManager>();
        if (runner == null) runner = FindAnyObjectByType<NetworkRunner>();

        // まずパネルを消す（中央表示はパネル外に出す想定）
        if (panelRoot != null) panelRoot.SetActive(false);

        // ★修正：OK確定後にだけ 1秒くらい中央表示 → 消える
        ShowCenterTemp($"{selectedCost}", 1.0f);

        if (gm != null && runner != null)
        {
            gm.RPC_SubmitSealCostChoice(runner.LocalPlayer, sessionId, selectedCost);
        }
    }

    // Hostから「宣言結果」を受け取った時に呼ばれる
    public void ShowReveal(string text, int[] sealedCosts)
    {
        // ★修正：表示中の消去コルーチンがあっても上書きする
        ShowCenterTemp(text, 1.2f);

        SetPersistentCosts(sealedCosts);
    }

    public void SetPersistentCosts(int[] costs)
    {
        if (persistentSealedText == null) return;

        if (costs == null || costs.Length == 0)
        {
            persistentSealedText.gameObject.SetActive(false);
            persistentSealedText.text = "";
            return;
        }

        // 「２，１０」形式で表示（重複は除外）
        var s = string.Join("，", costs.Distinct().OrderBy(x => x));
        persistentSealedText.text = s;
        persistentSealedText.gameObject.SetActive(true);
    }

    private void UpdateNumberButtonVisuals(int selected)
    {
        if (numberButtons == null) return;

        for (int i = 0; i < numberButtons.Length; i++)
        {
            var b = numberButtons[i];
            if (b == null) continue;

            var img = b.GetComponent<Image>();
            if (img == null) continue;

            float a = (i + 1 == selected) ? brightAlpha : dimAlpha;

            var c = img.color;
            c.a = a;
            img.color = c;
        }
    }

    private void ShowCenterTemp(string text, float seconds)
    {
        if (centerChoiceText == null) return;

        centerChoiceText.text = text;
        centerChoiceText.gameObject.SetActive(true);

        if (centerHideCo != null) StopCoroutine(centerHideCo);
        centerHideCo = StartCoroutine(HideCenterAfter(seconds));
    }

    private IEnumerator HideCenterAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        HideCenterImmediate();
        centerHideCo = null;
    }

    private void HideCenterImmediate()
    {
        if (centerChoiceText != null) centerChoiceText.gameObject.SetActive(false);
    }

    private void HidePanelImmediate()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }
}
