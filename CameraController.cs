using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class CameraController : MonoBehaviour
{
    // 摄像机要跟随的目标（即你的玩家模型）
    public Transform target;
    // 摄像机与目标之间的偏移量
    public Vector3 offset = new Vector3(1.5f, 2.0f, -4.0f);
    // 跟随的平滑度
    public float smoothSpeed = 0.125f;

    void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        // 计算摄像机想要到达的理想位置。
        // 这将摄像机放置在目标后方，并根据模型旋转调整其位置
        Vector3 desiredPosition = target.position + target.rotation * offset;

        // 使用Lerp进行平滑过渡，让摄像机平滑地移动到新位置
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;

        // 让摄像机朝向目标
        // transform.LookAt(target); // 不再使用这个，因为它会使摄像机直接面向目标

        // 我们要让摄像机朝向一个略微偏离目标的点，以形成越肩效果
        Vector3 lookDirection = (target.position - transform.position).normalized;

        // 计算新的旋转，使其朝向目标
        Quaternion lookRotation = Quaternion.LookRotation(lookDirection);

        // 使用Lerp平滑地过渡到新旋转
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, smoothSpeed);
    }
}