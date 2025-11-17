using UnityEngine;

/// <summary>
/// 让灯光像萤火虫一样：平滑忽明忽暗 + 飘忽摇摆 + 稳定回归
/// 挂到有 Light 组件的物体（或父物体），支持仅移动、仅闪烁或都开。
/// 新增：带有“回归力”，防止长时间后漂移/角度偏移。
/// </summary>
[DisallowMultipleComponent]
[ExecuteAlways]
public class FireflyLight : MonoBehaviour
{
    [Header("Light Flicker（忽明忽暗）")]
    public Light targetLight;                   // 为空则自动在本物体上找
    [Tooltip("基础亮度（平均值）")]
    public float baseIntensity = 1.5f;
    [Tooltip("亮度波动幅度（0~正）")]
    public float intensityAmplitude = 0.8f;
    [Tooltip("亮度变化速度（越大越快）")]
    public float flickerSpeed = 0.9f;
    [Tooltip("亮度平滑时间（秒），越大越柔和")]
    [Range(0f, 1.0f)] public float intensitySmoothTime = 0.08f;
    [Tooltip("附加正弦权重（0=不用正弦；建议小于0.5）")]
    [Range(0f, 1f)] public float sineBlend = 0.25f;
    [Tooltip("是否启用忽明忽暗")]
    public bool enableFlicker = true;

    [Header("Drift（飘忽不定）")]
    [Tooltip("最大飘移半径（世界单位）")]
    public Vector3 driftRadius = new Vector3(0.25f, 0.15f, 0.25f);
    [Tooltip("飘移动力/速度")]
    public float driftSpeed = 0.4f;
    [Tooltip("位置平滑时间（秒）")]
    [Range(0f, 1.0f)] public float positionSmoothTime = 0.12f;
    [Tooltip("是否启用飘移")]
    public bool enableDrift = true;

    [Header("Wobble（轻微摇摆旋转）")]
    [Tooltip("最大欧拉角摆动幅度")]
    public Vector3 wobbleAngles = new Vector3(3f, 6f, 3f);
    [Tooltip("摇摆速度")]
    public float wobbleSpeed = 0.6f;
    [Tooltip("旋转平滑（秒）")]
    [Range(0f, 1.0f)] public float rotationSmoothTime = 0.12f;
    [Tooltip("是否启用摇摆")]
    public bool enableWobble = true;

    [Header("Return Force（回归力）")]
    [Tooltip("位置回归强度（越大越快拉回原点）")]
    [Range(0f, 2f)] public float positionReturnStrength = 0.25f;
    [Tooltip("旋转回归强度（越大越快恢复原始角度）")]
    [Range(0f, 2f)] public float rotationReturnStrength = 0.25f;

    [Header("杂项")]
    [Tooltip("是否在场景视图绘制飘移范围")]
    public bool drawGizmo = false;

    // 初始状态
    private Vector3 _originPos;
    private Quaternion _originRot;
    private float _originIntensity = -1f;

    // 平滑用速度缓存
    private float _intensityVel;
    private Vector3 _posVel;       // 给 SmoothDamp 使用
    private Vector3 _wobbleVel;    // 自己做角度平滑

    // 随机种子偏移，让不同实例不同相位
    private Vector3 _noiseSeedPos;
    private float _noiseSeedFlicker;
    private Vector3 _noiseSeedRot;

    private void Reset()
    {
        targetLight = GetComponentInChildren<Light>();
    }

    private void OnEnable()
    {
        if (targetLight == null)
            targetLight = GetComponentInChildren<Light>();

        _originPos = transform.position;
        _originRot = transform.rotation;

        if (targetLight != null && _originIntensity < 0f)
            _originIntensity = targetLight.intensity;

        // 为每个实例打散相位
        float r = Random.value * 1000f;
        _noiseSeedPos = new Vector3(r + Random.value, r + 13.37f, r + 42.42f);
        _noiseSeedRot = new Vector3(r + 7.7f, r + 2.2f, r + 9.9f);
        _noiseSeedFlicker = r + 55.55f;
    }

    private void Update()
    {
        float t = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;

        if (enableFlicker && targetLight != null)
            UpdateFlicker(t);

        if (enableDrift)
            UpdateDrift(t);

        if (enableWobble)
            UpdateWobble(t);
    }

    private void UpdateFlicker(float t)
    {
        // Perlin 噪声（0..1）→ (-1..1)
        float n = Mathf.PerlinNoise(_noiseSeedFlicker, t * flickerSpeed) * 2f - 1f;

        // 叠加一点点正弦（可选，柔和律动）
        float s = Mathf.Sin((t + _noiseSeedFlicker) * (flickerSpeed * 1.7f));
        float blended = Mathf.Lerp(n, s, sineBlend);

        // 目标强度
        float target = Mathf.Max(0f, baseIntensity + blended * intensityAmplitude);

        // 平滑到目标强度
        float current = targetLight.intensity;
        float smoothed = Mathf.SmoothDamp(current, target, ref _intensityVel, intensitySmoothTime);
        targetLight.intensity = smoothed;
    }

    private void UpdateDrift(float t)
    {
        // 用噪声生成三个轴向的目标偏移（平滑、无抖动）
        Vector3 targetOffset = new Vector3(
            (Mathf.PerlinNoise(_noiseSeedPos.x, t * driftSpeed) * 2f - 1f) * driftRadius.x,
            (Mathf.PerlinNoise(_noiseSeedPos.y, t * driftSpeed) * 2f - 1f) * driftRadius.y,
            (Mathf.PerlinNoise(_noiseSeedPos.z, t * driftSpeed) * 2f - 1f) * driftRadius.z
        );

        Vector3 targetPos = _originPos + targetOffset;

        // 平滑移动（避免瞬移/锯齿）
        Vector3 smoothed = Vector3.SmoothDamp(transform.position, targetPos, ref _posVel, positionSmoothTime);

        // 加一个回归力，使其逐步拉回原点（防止长时间漂移累积）
        smoothed = Vector3.Lerp(smoothed, _originPos, positionReturnStrength * Time.deltaTime);

        transform.position = smoothed;
    }

    private void UpdateWobble(float t)
    {
        // 三轴独立噪声→角度
        Vector3 targetEuler = new Vector3(
            (Mathf.PerlinNoise(_noiseSeedRot.x, t * wobbleSpeed) * 2f - 1f) * wobbleAngles.x,
            (Mathf.PerlinNoise(_noiseSeedRot.y, t * wobbleSpeed) * 2f - 1f) * wobbleAngles.y,
            (Mathf.PerlinNoise(_noiseSeedRot.z, t * wobbleSpeed) * 2f - 1f) * wobbleAngles.z
        );

        Quaternion targetRot = _originRot * Quaternion.Euler(targetEuler);

        // 角度平滑（用 Lerp 插值 + 时间系数）
        float k = rotationSmoothTime <= 0f ? 1f : Mathf.Clamp01(Time.deltaTime / rotationSmoothTime);
        Quaternion smoothed = Quaternion.Slerp(transform.rotation, targetRot, k);

        // 加入回归到原始旋转的力（防止旋转漂移累积）
        smoothed = Quaternion.Slerp(smoothed, _originRot, rotationReturnStrength * Time.deltaTime);

        transform.rotation = smoothed;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmo) return;
        Gizmos.color = new Color(1f, 1f, 0.2f, 0.35f);
        // 画一个大致包围盒
        Vector3 size = driftRadius * 2f;
        Gizmos.DrawWireCube(Application.isPlaying ? _originPos : transform.position, size);
    }
}
