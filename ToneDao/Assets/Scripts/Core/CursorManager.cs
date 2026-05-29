using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CursorManager : MonoBehaviour
{

    public static CursorManager Instance { get; private set; }
    public Sprite cursorSprite;

    public Vector2 cursorSize = new Vector2(48f, 48f);

    public Vector2 hotspotNormalized = new Vector2(0f, 1f);

    private Canvas _canvas;
    private RectTransform _cursorRect;
    private Image _cursorImage;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Cursor.visible = false;

        CreateCursorUI();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Cursor.visible = true;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {

        Cursor.visible = false;
    }

    private void Update()
    {
        if (_cursorRect == null || Mouse.current == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();

        Vector2 offset = new Vector2(
            hotspotNormalized.x * cursorSize.x,
            -(1f - hotspotNormalized.y) * cursorSize.y
        );

        _cursorRect.position = (Vector3)(mousePos - offset);
    }

    private void CreateCursorUI()
    {

        var canvasGo = new GameObject("CursorCanvas");
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 32000;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        var cg = canvasGo.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        var cursorGo = new GameObject("Cursor");
        cursorGo.transform.SetParent(canvasGo.transform, false);

        _cursorImage = cursorGo.AddComponent<Image>();
        _cursorImage.sprite = cursorSprite;
        _cursorImage.raycastTarget = false;

        _cursorRect = cursorGo.GetComponent<RectTransform>();
        _cursorRect.anchorMin = Vector2.zero;
        _cursorRect.anchorMax = Vector2.zero;
        _cursorRect.pivot = Vector2.zero;
        _cursorRect.sizeDelta = cursorSize;
    }

    public void Refresh()
    {
        if (_cursorImage != null) _cursorImage.sprite = cursorSprite;
        if (_cursorRect != null) _cursorRect.sizeDelta = cursorSize;
    }

    private void LateUpdate()
    {
        if (_cursorRect != null && _cursorRect.sizeDelta != cursorSize)
            _cursorRect.sizeDelta = cursorSize;
    }
}
