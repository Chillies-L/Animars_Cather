using UnityEngine;

namespace AnimarsCatcher
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(AudioSource))]
    public class TankController : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float MoveSpeed = 10f;            // 移动速度
        public float RotationSpeed = 120f;       // 转向速度（度/秒）

        [Header("Audio Settings")]
        public AudioSource MoveAudioSource;      // 行走音效
        public AudioClip MoveClip;               // 音效片段

        [Header("Animation Settings")]
        public Animator Animator;                // 动画控制器
        public string SpeedParam = "Speed";      // 动画参数名

        [Header("Smoke FX")]
        public GameObject FXSmoke;               // 烟雾特效（沿用原字段名）

        private CharacterController _controller;
        private Camera _mainCamera;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _mainCamera = Camera.main;

            if (MoveAudioSource == null)
                MoveAudioSource = GetComponent<AudioSource>();

            if (MoveClip != null)
            {
                MoveAudioSource.clip = MoveClip;
                MoveAudioSource.loop = true;
            }

            // 初始时根据需要隐藏烟雾
            if (FXSmoke != null) FXSmoke.SetActive(false);
        }

        private void Update()
        {
            HandleMovement();
        }

        private void HandleMovement()
        {
            // 输入
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            Vector3 inputDir = new Vector3(h, 0f, v);

            Vector3 worldMoveDir = Vector3.zero;

            if (inputDir.sqrMagnitude > 0.01f)
            {
                // 相机朝向对齐
                float camY = _mainCamera != null ? _mainCamera.transform.eulerAngles.y : transform.eulerAngles.y;
                worldMoveDir = Quaternion.Euler(0, camY, 0) * inputDir.normalized;

                // 转向 + 移动
                Quaternion targetRot = Quaternion.LookRotation(worldMoveDir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, RotationSpeed * Time.deltaTime);
                _controller.SimpleMove(worldMoveDir * MoveSpeed);

                // 声音
                if (!MoveAudioSource.isPlaying) MoveAudioSource.Play();
            }
            else
            {
                MoveAudioSource.Stop();
            }

            // 动画：传速度
            if (Animator != null)
            {
                float speed = inputDir.magnitude * MoveSpeed;
                Animator.SetFloat(SpeedParam, speed);
            }

            // 烟雾：沿用原接口逻辑
            SetSmoke(worldMoveDir * MoveSpeed);
        }

        /// <summary>
        /// 沿用原 Player 脚本的接口签名与逻辑：
        /// 速度大于0则显示烟雾，并让其 forward 指向速度反向
        /// </summary>
        private void SetSmoke(Vector3 speed)
        {
            if (FXSmoke == null) return;
            bool moving = speed.sqrMagnitude > 0f;
            FXSmoke.SetActive(moving);
            if (moving)
            {
                // 与原实现一致：烟雾朝速度反向
                FXSmoke.transform.forward = -speed;
            }
        }
    }
}
