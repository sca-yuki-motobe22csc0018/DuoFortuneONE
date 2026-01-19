using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class TitleManager : MonoBehaviour
{
    [SerializeField] Text clickStart;
    [SerializeField] Text title;

    public Image frontImage;
    public Image backImage;
    public Sprite[] cardSprites;
    public GameObject card;

    public Button[] buttons;
    public RectTransform[] buttonTargetPos;

    public static bool isFirstLaunch = true;

    private int lastIndex = -1;
    private float clickStartAlpha = 0.2f;
    private float alphaTime = 1.0f;
    private int loop = -1;
    private float titleTime = 1.0f;
    private float fadeAlpha = 1.0f;

    private bool clickCheck = false;
    private bool isRotating = false;
    private bool isStopped = false;

    private Tween rotationTween;
    private Tween loopTween;
    private Tween clickBlinkTween; // ★ 追加：クリックスタートの点滅Tweenを保持

    void Start()
    {
        if (isFirstLaunch)
        {
            // 通常の演出あり起動
            frontImage.enabled = true;
            backImage.enabled = false;

            ChangeRandomSprite();
            Fade();
            LoopRotation();

            isFirstLaunch = false; // ★ 次回以降は演出しない
        }
        else
        {
            // ★ 演出なし：完成状態を即作る
            SetupCompletedState();
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            gameEnd();

        if (clickCheck && Input.GetMouseButtonDown(0) && !isStopped)
            StopAndFlipToBack();
    }

    void TitleMove()
    {
        clickCheck = true;

        // 変数に保持しておく
        clickBlinkTween = clickStart
            .DOFade(clickStartAlpha, alphaTime)
            .SetEase(Ease.InSine)
            .SetLoops(loop, LoopType.Yoyo);
    }

    void Fade()
    {
        Sequence fade = DOTween.Sequence();
        fade.Append(title.DOFade(fadeAlpha, titleTime).SetEase(Ease.InSine));
        fade.Join(clickStart.DOFade(fadeAlpha, titleTime).SetEase(Ease.InSine));
        fade.Join(frontImage.DOFade(fadeAlpha, titleTime).SetEase(Ease.InSine));
        fade.OnComplete(() => TitleMove());
    }

    private void LoopRotation()
    {
        if (isStopped) return;
        loopTween = DOVirtual.DelayedCall(4f, () => RotateCard());
    }

    private void RotateCard()
    {
        if (isStopped) return;

        float duration = 0.5f;
        bool spriteChanged = false;
        isRotating = true;

        rotationTween = card.transform.DORotate(new Vector3(0, 360, 0), duration, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .OnUpdate(() =>
            {
                float yRotation = card.transform.eulerAngles.y;

                if (yRotation >= 90 && yRotation <= 270)
                {
                    frontImage.enabled = false;
                    backImage.enabled = true;
                }
                else
                {
                    frontImage.enabled = true;
                    backImage.enabled = false;
                }

                if (!spriteChanged && yRotation > 90)
                {
                    ChangeRandomSprite();
                    spriteChanged = true;
                }
            })
            .OnComplete(() =>
            {
                isRotating = false;
                LoopRotation();
            });
    }

    private void ChangeRandomSprite()
    {
        if (cardSprites == null || cardSprites.Length == 0) return;

        int newIndex;
        do
        {
            newIndex = Random.Range(0, cardSprites.Length);
        } while (newIndex == lastIndex && cardSprites.Length > 1);

        lastIndex = newIndex;
        frontImage.sprite = cardSprites[newIndex];
    }

    // クリック時にすべて確実に止めて透明化
    private void StopAndFlipToBack()
    {
        isStopped = true;
        clickCheck = false;

        // 点滅Tweenを確実にKill
        if (clickBlinkTween != null && clickBlinkTween.IsActive()) clickBlinkTween.Kill();
        DOTween.Kill(clickStart);


        // 回転停止
        if (rotationTween != null && rotationTween.IsActive()) rotationTween.Kill();
        if (loopTween != null && loopTween.IsActive()) loopTween.Kill();
        DOTween.Kill(card.transform);

        float currentY = card.transform.eulerAngles.y % 360f;
        float targetY = (currentY < 180f) ? 180f : 540f;

        card.transform
            .DORotate(new Vector3(0, targetY, 0), 0.6f, RotateMode.FastBeyond360)
            .SetEase(Ease.InOutCubic)
            .OnUpdate(() =>
            {
                float yRot = card.transform.eulerAngles.y % 360f;
                if (yRot >= 90 && yRot <= 270)
                {
                    frontImage.enabled = false;
                    backImage.enabled = true;
                }
                else
                {
                    frontImage.enabled = true;
                    backImage.enabled = false;
                }
            })
            .OnComplete(() =>
            {
                card.transform.rotation = Quaternion.Euler(0, 180, 0);
                frontImage.enabled = false;
                backImage.enabled = true;
                AfterCardBackAnimation();
            });
    }

    private void AfterCardBackAnimation()
    {
        Sequence seq = DOTween.Sequence();

        // タイトルとクリックスタートを一緒にフェードアウト（1秒）
        float fadeTime = 1.0f;
        seq.Append(title.DOFade(0f, fadeTime));
        seq.Join(clickStart.DOFade(0f, fadeTime));

        Sequence buttonSeq = DOTween.Sequence();
        float moveDuration = 0.25f;
        float stagger = 0.08f;

        for (int i = 0; i < buttons.Length; i++)
        {
            if (i >= buttonTargetPos.Length) break;
            RectTransform btnRect = buttons[i]?.GetComponent<RectTransform>();
            RectTransform tgtRect = buttonTargetPos[i];
            if (btnRect == null || tgtRect == null) continue;

            buttonSeq.Insert(i * stagger,
                btnRect.DOAnchorPos(tgtRect.anchoredPosition, moveDuration).SetEase(Ease.OutBack));
        }

        seq.Append(buttonSeq);
        seq.AppendCallback(() => FlipCardToFront());
        seq.Play();
    }

    private void FlipCardToFront()
    {
        DOTween.Kill(card.transform);

        bool spriteChanged = false; // ← 追加（1回だけ絵柄変更するためのフラグ）

        card.transform
            .DORotate(Vector3.zero, 0.6f, RotateMode.FastBeyond360)
            .SetEase(Ease.InOutCubic)
            .OnUpdate(() =>
            {
                float y = card.transform.eulerAngles.y % 360f;

                // ★ ちょうど裏→表の切り替え（90度を超えた瞬間）で絵柄を変える
                if (!spriteChanged && y > 90f)
                {
                    ChangeRandomSprite();
                    spriteChanged = true;
                }

                if (y >= 85f && y <= 275f)
                {
                    frontImage.enabled = false;
                    backImage.enabled = true;
                }
                else
                {
                    frontImage.enabled = true;
                    backImage.enabled = false;
                }
            })
            .OnComplete(() =>
            {
                card.transform.rotation = Quaternion.identity;
                frontImage.enabled = true;
                backImage.enabled = false;

            });
    }
    private void SetupCompletedState()
    {
        // ★ すべてのTweenを停止
        DOTween.KillAll();

        // ===== カードの表示を完全復元 =====
        card.transform.rotation = Quaternion.identity;

        frontImage.enabled = true;
        backImage.enabled = false;

        // ★ Alpha を必ず 1 に戻す
        Color frontColor = frontImage.color;
        frontColor.a = 1f;
        frontImage.color = frontColor;

        Color backColor = backImage.color;
        backColor.a = 1f;
        backImage.color = backColor;

        // 表イラストを1回ランダム設定
        ChangeRandomSprite();

        // ===== タイトル・クリックスタートは非表示 =====
        SetAlpha(title, 0f);
        SetAlpha(clickStart, 0f);

        // ===== ボタンを完成位置へ即配置 =====
        for (int i = 0; i < buttons.Length; i++)
        {
            if (i >= buttonTargetPos.Length) break;

            RectTransform btnRect = buttons[i].GetComponent<RectTransform>();
            btnRect.anchoredPosition = buttonTargetPos[i].anchoredPosition;
        }

        clickCheck = false;
        isStopped = true;
    }
    private void SetAlpha(Graphic g, float a)
    {
        Color c = g.color;
        c.a = a;
        g.color = c;
    }

    public void BattleScene()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("LobbyScene");
    }

    public void DeckScene()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("DeckBuilding");
    }

    public void gameEnd()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
