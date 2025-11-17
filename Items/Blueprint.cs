using UnityEngine;

namespace AnimarsCatcher
{
    [RequireComponent(typeof(Renderer))]
    public class Blueprint : MonoBehaviour
    {
        [Header("Float Settings")]
        public float floatAmplitude = 0.2f;    // 上下浮动幅度
        public float floatSpeed = 2f;          // 浮动速度

        [Header("Rotation Settings")]
        public Vector3 rotationSpeed = new Vector3(0f, 50f, 0f);  // 旋转速度

        [Header("Emission Settings")]
        public Color baseEmissionColor = Color.cyan; // 自发光基础颜色
        public float emissionMin = 0.3f;             // 最小亮度
        public float emissionMax = 2f;               // 最大亮度
        public float emissionSpeed = 2f;             // 发光变化速度

        [Header("Optional Light")]
        public Light linkedLight;                    // 可选同步灯光

        [Header("Custom Trigger Settings")]
        [Tooltip("拖入一个带 Collider 的物体作为触发器；留空则使用当前物体的 Collider。")]
        public Collider customTrigger;

        [Header("Audio Settings")]
        [Tooltip("蓝图被拾取时播放的音效。")]
        public AudioClip pickupSound;
        public float pickupVolume = 1f;

        [Header("Particle Settings")]
        [Tooltip("蓝图被拾取时播放的粒子特效（可选）")]
        public GameObject pickupEffectPrefab;
        public float effectLifetime = 3f;

        [Header("Destroy Settings")]
        [Tooltip("蓝图销毁前的延迟时间（秒），用于播放音效或特效）")]
        public float destroyDelay = 1f;
        [Tooltip("是否同时销毁自定义触发器（如果有）")]
        public bool destroyCustomTrigger = true;

        private Material _material;
        private float _startY;
        private AudioSource _audioSource;

        private void Start()
        {
            _startY = transform.position.y;

            // 初始化材质
            _material = GetComponent<Renderer>().material;
            _material.EnableKeyword("_EMISSION");

            // 设置触发器
            SetupTriggerCollider();

            // 确保有 Rigidbody（用于触发检测）
            if (!TryGetComponent(out Rigidbody rb))
            {
                rb = gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
            }

            // 初始化音频源
            if (pickupSound != null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.clip = pickupSound;
                _audioSource.volume = pickupVolume;
                _audioSource.spatialBlend = 1f; // 3D 声音
            }
        }

        private void SetupTriggerCollider()
        {
            if (customTrigger != null && customTrigger.gameObject != gameObject)
            {
                customTrigger.isTrigger = true;
                var bridge = customTrigger.gameObject.AddComponent<BlueprintTriggerBridge>();
                bridge.Init(this);
            }
            else
            {
                if (!TryGetComponent(out Collider col))
                    col = gameObject.AddComponent<BoxCollider>();

                col.isTrigger = true;
            }
        }

        private void Update()
        {
            // 上下浮动
            float newY = _startY + Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);

            // 旋转
            transform.Rotate(rotationSpeed * Time.deltaTime);

            // 自发光强度变化
            float emissionFactor = Mathf.Lerp(emissionMin, emissionMax,
                (Mathf.Sin(Time.time * emissionSpeed) + 1f) / 2f);

            Color finalColor = baseEmissionColor * emissionFactor;
            _material.SetColor("_EmissionColor", finalColor);

            // 灯光同步变化
            if (linkedLight != null)
            {
                linkedLight.intensity = emissionFactor;
                linkedLight.color = baseEmissionColor;
            }
        }

        public void OnBlueprintTriggered(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            // 蓝图计数 +1
            var root = FindObjectOfType<GameRoot>();
            if (root != null)
            {
                root.GameModel.BlueprintCount.Value++;
                Debug.Log($"[Blueprint] 玩家拾取蓝图，当前蓝图数量 = {root.GameModel.BlueprintCount.Value}");
            }

            // 播放音效
            if (_audioSource != null && pickupSound != null)
                _audioSource.Play();

            // 播放粒子特效
            if (pickupEffectPrefab != null)
            {
                var fx = Instantiate(pickupEffectPrefab, transform.position, Quaternion.identity);
                Destroy(fx, effectLifetime);
            }

            // 安全移除 UI 指针
            if (UIBlueprintManager.Instance != null)
            {
                try { UIBlueprintManager.Instance.RemovePointer(transform); }
                catch { Debug.LogWarning($"[Blueprint] RemovePointer 未找到 {name} 记录，已跳过。"); }
            }

            // ✅ 销毁触发器（如果是独立物体且需要销毁）
            if (destroyCustomTrigger && customTrigger != null && customTrigger.gameObject != gameObject)
            {
                Destroy(customTrigger.gameObject, destroyDelay);
            }

            // ✅ 延迟销毁蓝图本体
            Destroy(gameObject, destroyDelay);
        }

        private void OnTriggerEnter(Collider other)
        {
            OnBlueprintTriggered(other);
        }
    }

    // 用于自定义触发器事件转发
    public class BlueprintTriggerBridge : MonoBehaviour
    {
        private Blueprint owner;

        public void Init(Blueprint blueprint)
        {
            owner = blueprint;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (owner != null)
                owner.OnBlueprintTriggered(other);
        }
    }
}
