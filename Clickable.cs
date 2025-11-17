using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(Collider))]
public class ClickableCell : MonoBehaviour
{
    [Header("Grid Index")]
    public int row;
    public int col;

    [Header("Interact")]
    public bool isAdjBoostCell;
    public bool hasTower;
    public GameObject menuCanvasPrefab;

    [Header("Visual")]
    public Renderer rend;
    [Tooltip("相邻抬升格的高亮颜色（与 GridTerrainGenerator 的 adjColor 保持一致）")]
    public Color adjColor = new Color(1f, 0.8f, 0.2f);
    [Range(0.3f, 1.0f)]
    [Tooltip("悬停时亮度倍数（0.7 = 稍暗）")]
    public float hoverDarken = 0.7f;

    private Color originalColor;
    private bool isSelected;
    private Tween pulseTween;
    private Tower currentTower;
    private Collider colli;

    // 摄像机控制
    [Header("Camera Reaction")]
    public Camera mainCam;
    [Tooltip("选中时摄像机绕Y轴偏转角度（负值向左，正值向右）")]
    public float selectYawOffset = -5f;
    [Tooltip("摄像机旋转动画时长")]
    public float camRotateDuration = 0.5f;
    private Tween camTween;
    private Quaternion camDefaultRot;
    private bool camInit;

    // 悬停兜底方案
    [Header("Hover Fallback (Optional)")]
    [Tooltip("当 OnMouseEnter/Exit 不触发时启用手动射线检测")]
    public bool useManualHoverRaycast = false;
    public Camera hoverCamera;
    public LayerMask hoverMask = ~0;
    private bool isHovering;

    private static ClickableCell currentSelected; // 防止多选冲突

    private void Reset()
    {
        if (!rend) rend = GetComponentInChildren<Renderer>();
    }

    private void Awake()
    {
        if (!rend) rend = GetComponentInChildren<Renderer>();
        colli = GetComponent<Collider>();
        if (!hoverCamera) hoverCamera = Camera.main;
        if (!mainCam) mainCam = Camera.main;
    }

    private void Start()
    {
        if (rend)
        {
            originalColor = rend.material.color;
            _ = rend.material; // 确保实例化材质
        }

        if (mainCam)
        {
            camDefaultRot = mainCam.transform.rotation;
            camInit = true;
        }
    }

    // ----------------------------
    // 悬停检测（Unity内置回调）
    // ----------------------------
    private void OnMouseEnter()
    {
        if (!isAdjBoostCell || !rend || isSelected) return;
        var target = adjColor * hoverDarken;
        rend.material.DOColor(target, 0.15f);
    }

    private void OnMouseExit()
    {
        if (!isAdjBoostCell || !rend || isSelected) return;
        rend.material.DOColor(adjColor, 0.15f);
    }

    private void OnMouseDown()
    {
        if (!isAdjBoostCell) return;
        if (!isSelected) Select();
        else Deselect();
    }

    // ----------------------------
    // 手动射线检测（兜底方案）
    // ----------------------------
    private void Update()
    {
        if (!useManualHoverRaycast) return;
        if (!hoverCamera || !isAdjBoostCell || !rend || isSelected) return;

        var ray = hoverCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hit, 1000f, hoverMask))
        {
            bool hitThis = hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform);
            if (hitThis && !isHovering)
            {
                isHovering = true;
                rend.material.DOColor(adjColor * hoverDarken, 0.12f);
            }
            else if (!hitThis && isHovering)
            {
                isHovering = false;
                rend.material.DOColor(adjColor, 0.12f);
            }
        }
        else if (isHovering)
        {
            isHovering = false;
            rend.material.DOColor(adjColor, 0.12f);
        }

        if (Input.GetMouseButtonDown(0) && isHovering)
        {
            if (!isSelected) Select();
            else Deselect();
        }
    }

    // ----------------------------
    // 选中与取消选中逻辑
    // ----------------------------
    private void Select()
    {
        if (currentSelected && currentSelected != this)
            currentSelected.Deselect();
        currentSelected = this;

        isSelected = true;

        if (rend)
        {
            DOTween.Kill(rend.material);
            pulseTween = rend.material
                .DOColor(Color.white, 0.6f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        // 摄像机偏转
        if (camInit && mainCam)
        {
            camTween?.Kill();
            Quaternion targetRot = camDefaultRot * Quaternion.Euler(0, selectYawOffset, 0);
            camTween = mainCam.transform
                .DORotateQuaternion(targetRot, camRotateDuration)
                .SetEase(Ease.InOutSine);
        }

        // 呼出菜单
        if (menuCanvasPrefab)
        {
            Vector3 topPos = GetTopPosition();
            var menu = Instantiate(menuCanvasPrefab, topPos + Vector3.up * 1.5f, Quaternion.identity);
            var comp = menu.GetComponent<TowerMenuCanvas>();
            comp.Init(this, currentTower);
        }
    }

    private void Deselect()
    {
        isSelected = false;
        if (currentSelected == this) currentSelected = null;

        pulseTween?.Kill();
        pulseTween = null;

        if (rend)
        {
            DOTween.Kill(rend.material);
            rend.material.DOColor(adjColor, 0.15f);
        }

        if (camInit && mainCam)
        {
            camTween?.Kill();
            camTween = mainCam.transform
                .DORotateQuaternion(camDefaultRot, camRotateDuration)
                .SetEase(Ease.InOutSine);
        }
    }

    // ----------------------------
    // 辅助与外部接口
    // ----------------------------
    public void SetTower(Tower tower)
    {
        currentTower = tower;
        hasTower = tower != null;
    }

    public void SetAdjColor(Color c)
    {
        adjColor = c;
        if (isAdjBoostCell && rend)
        {
            DOTween.Kill(rend.material);
            rend.material.color = adjColor;
        }
    }

    private Vector3 GetTopPosition()
    {
        if (rend == null) return transform.position;
        var bounds = rend.bounds;
        return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
    }
}
