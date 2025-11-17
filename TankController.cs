using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class TankController : MonoBehaviour
{
    [Header("移动设置")]
    public float moveSpeed = 5f;
    public float turnSpeed = 100f;

    [Header("摄像机设置")]
    public Transform cameraTransform;   // 普通跟随摄像机（非 Cinemachine）
    public float cameraDistance = 10f;
    public float cameraHeight = 15f;
    public float cameraSmooth = 5f;

    [Header("Cinemachine 支持")]
    // 改成 CinemachineVirtualCameraBase，可以放 FreeLook、VirtualCamera、ClearShot 等任何虚拟机
    public List<CinemachineVirtualCameraBase> virtualCameras = new List<CinemachineVirtualCameraBase>();
    private int currentCameraIndex = 0;

    [Header("动画设置")]
    public List<Animator> animators = new List<Animator>();

    [Header("粒子特效列表")]
    public List<ParticleSystem> moveParticles = new List<ParticleSystem>();
    public float emissionMultiplier = 10f;
    private bool particleActive = false;

    private float currentSpeed;

    private void Start()
    {
        // 确保只有第一台 Cinemachine 相机激活
        if (virtualCameras.Count > 0)
        {
            for (int i = 0; i < virtualCameras.Count; i++)
                virtualCameras[i].Priority = (i == currentCameraIndex) ? 20 : 0;
        }
    }

    private void Update()
    {
        HandleMovement();

        if (virtualCameras.Count == 0)
            HandleCameraFollow();

        HandleAnimation();
        HandleCameraSwitch();
        HandleParticles();
    }

    void HandleMovement()
    {
        float moveInput = Input.GetAxis("Vertical");
        float turnInput = Input.GetAxis("Horizontal");

        float move = moveInput * moveSpeed * Time.deltaTime;
        transform.Translate(Vector3.forward * move);

        float turn = turnInput * turnSpeed * Time.deltaTime;
        transform.Rotate(Vector3.up * turn);

        currentSpeed = Mathf.Abs(moveInput) * moveSpeed;
    }

    void HandleCameraFollow()
    {
        if (cameraTransform == null) return;

        Vector3 forwardXZ = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;
        Vector3 offset = -forwardXZ * cameraDistance + Vector3.up * cameraHeight;
        Vector3 targetPos = transform.position + offset;

        cameraTransform.position = Vector3.Lerp(
            cameraTransform.position,
            targetPos,
            Time.deltaTime * cameraSmooth
        );

        cameraTransform.LookAt(transform.position + Vector3.up * 2f);
    }

    void HandleCameraSwitch()
    {
        if (virtualCameras.Count == 0) return;

        if (Input.GetKeyDown(KeyCode.F2))
        {
            currentCameraIndex = (currentCameraIndex + 1) % virtualCameras.Count;

            for (int i = 0; i < virtualCameras.Count; i++)
                virtualCameras[i].Priority = (i == currentCameraIndex) ? 20 : 0;

            Debug.Log($"切换到相机: {virtualCameras[currentCameraIndex].name}");
        }
    }

    void HandleAnimation()
    {
        if (animators.Count == 0) return;

        bool driving = Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f
                    || Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f;

        foreach (Animator anim in animators)
        {
            if (anim != null)
                anim.SetBool("isDriving", driving);
        }
    }

    void HandleParticles()
    {
        if (moveParticles.Count == 0) return;

        bool driving = Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f
                    || Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f;

        if (driving && !particleActive)
        {
            foreach (var ps in moveParticles)
                if (ps != null) ps.Play();

            particleActive = true;
        }
        else if (!driving && particleActive)
        {
            foreach (var ps in moveParticles)
                if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            particleActive = false;
        }

        if (driving)
        {
            foreach (var ps in moveParticles)
            {
                if (ps != null)
                {
                    var emission = ps.emission;
                    emission.rateOverTime = currentSpeed * emissionMultiplier;
                }
            }
        }
    }
}
