using UnityEngine;
using Oculus.Interaction;

public class EnablePhysicsOnThrow : MonoBehaviour
{
    [SerializeField] private Grabbable grabbable;
    [SerializeField] private Rigidbody rb;

    private void Reset()
    {
        // 컴포넌트가 같은 오브젝트에 붙어 있는 경우를 기준으로 자동 연결한다.
        // 수동 할당을 하지 않았을 때를 대비한 초기화 로직이다.
        grabbable = GetComponent<Grabbable>();
        rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        // Throw 이벤트 구독.
        // Grab 상태에서는 kinematic으로 고정되어 있던 Rigidbody를
        // 실제 throw 순간에 물리 시뮬레이션 상태로 전환하기 위해 사용한다.
        if (grabbable != null && grabbable.VelocityThrow != null)
            grabbable.VelocityThrow.WhenThrown += OnThrown;
    }

    private void OnDisable()
    {
        // 중복 구독 및 잔여 콜백 방지를 위해 반드시 해제한다.
        if (grabbable != null && grabbable.VelocityThrow != null)
            grabbable.VelocityThrow.WhenThrown -= OnThrown;
    }

    private void OnThrown(Vector3 v, Vector3 w)
    {
        // 실제 throw가 발생한 순간 Rigidbody를 동적 상태로 전환한다.
        // 리콜/대기 상태에서 kinematic으로 잠겨 있던 물리를 다시 활성화한다.
        rb.isKinematic = false;
        rb.WakeUp();
    }
}
