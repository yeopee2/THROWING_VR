using UnityEngine;
using System;

public class CollisionCursorTarget : MonoBehaviour
{
    // 기존 리스너와의 호환을 위한 기본 이벤트.
    // hitObject만 전달하며, sender나 접촉 좌표는 포함하지 않는다.
    public static event Action<GameObject> TriggerHit;
    public static event Action<GameObject> CollisionHit;

    // sender(transform)까지 함께 전달하는 이벤트.
    // 어떤 커서(또는 오브젝트)에서 발생한 충돌인지 구분할 때 사용한다.
    public static event Action<Transform, GameObject> TriggerHitWithSender;
    public static event Action<Transform, GameObject> CollisionHitWithSender;

    // world 좌표계 기준 hit point까지 포함하는 이벤트.
    // Experiments에서 접촉 좌표를 이용해 정확도 및 거리(L2)를 계산할 때 사용된다.
    // Trigger는 실제 contact point가 없어 근사 좌표를 사용한다.
    public static event Action<Transform, GameObject, Vector3> TriggerHitWithPoint;
    public static event Action<Transform, GameObject, Vector3> CollisionHitWithPoint;

    private void OnTriggerEnter(Collider other)
    {
        // Trigger 충돌 발생 시 기본 이벤트 먼저 전파
        TriggerHit?.Invoke(other.gameObject);
        TriggerHitWithSender?.Invoke(transform, other.gameObject);

        // Trigger는 Unity에서 실제 접촉점을 제공하지 않으므로
        // 커서 위치 기준으로 collider의 가장 가까운 점을 근사 접촉점으로 사용
        Vector3 hitPointWorld = other.ClosestPoint(transform.position);

        // 근사 접촉 좌표를 포함한 이벤트 전파
        TriggerHitWithPoint?.Invoke(transform, other.gameObject, hitPointWorld);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Collision 충돌 발생 시 기본 이벤트 먼저 전파
        CollisionHit?.Invoke(collision.gameObject);
        CollisionHitWithSender?.Invoke(transform, collision.gameObject);

        // Collision은 실제 물리 contact point를 제공하므로
        // 첫 번째 contact를 대표 접촉점으로 사용
        Vector3 contactPointWorld = collision.GetContact(0).point;

        // 실제 접촉 좌표를 포함한 이벤트 전파
        CollisionHitWithPoint?.Invoke(transform, collision.gameObject, contactPointWorld);
    }
}
