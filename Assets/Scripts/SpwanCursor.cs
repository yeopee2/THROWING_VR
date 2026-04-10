using UnityEngine;
using Oculus.Interaction;

public class SpawnCursor : MonoBehaviour
{
    [Header("Cursor object (the actual throwable / cursor in the scene)")]
    [SerializeField] private Transform cursor;               // 실제 던지기/충돌 판정에 사용되는 커서 오브젝트

    [Header("Controller transform (e.g., RightHandAnchor)")]
    [SerializeField] private Transform rightController;      // 리콜 시 기준이 되는 컨트롤러 위치

    [Header("Optional (auto-find if left empty)")]
    [SerializeField] private Rigidbody cursorRb;             // 커서의 물리 제어용 Rigidbody
    [SerializeField] private Grabbable cursorGrabbable;      // Oculus Interaction Grab/Throw 인터페이스

    // 리콜 직후 커서를 공중에 고정시키기 위한 물리 잠금 플래그
    // Grab 또는 Throw 이벤트가 발생할 때까지 유지된다.
    private bool _freezePhysics = false;

    private void Awake()
    {
        // 커서가 지정되어 있으면 필수 컴포넌트를 자동으로 연결한다.
        // 수동 할당이 없을 때를 대비한 초기화 로직이다.
        if (cursor != null)
        {
            if (cursorRb == null)
                cursorRb = cursor.GetComponent<Rigidbody>();

            if (cursorGrabbable == null)
                cursorGrabbable = cursor.GetComponent<Grabbable>();
        }
    }

    private void OnEnable()
    {
        // Throw 이벤트 구독.
        // 실제로 던져지는 순간 물리 잠금을 해제하기 위해 사용한다.
        if (cursorGrabbable != null && cursorGrabbable.VelocityThrow != null)
            cursorGrabbable.VelocityThrow.WhenThrown += OnThrown;
    }

    private void OnDisable()
    {
        // 이벤트 중복 구독 방지를 위해 반드시 해제한다.
        if (cursorGrabbable != null && cursorGrabbable.VelocityThrow != null)
            cursorGrabbable.VelocityThrow.WhenThrown -= OnThrown;
    }

    private void Update()
    {
        // 필수 참조가 없으면 동작하지 않는다.
        if (cursor == null || rightController == null)
            return;

        // A 버튼 입력 시 커서를 컨트롤러 위치로 리콜한다.
        // 실험 흐름에서 “다음 시도 준비 상태”를 만드는 진입점이다.
        if (OVRInput.GetDown(OVRInput.Button.One))
        {
            // 커서를 컨트롤러 위치/회전으로 즉시 이동
            cursor.SetPositionAndRotation(rightController.position, rightController.rotation);

            // 비활성화 상태였다면 다시 활성화
            cursor.gameObject.SetActive(true);

            // 기존 물리 상태를 완전히 초기화하고 정지 상태로 고정
            if (cursorRb != null)
            {
                cursorRb.isKinematic = false;            // 속도 초기화를 안전하게 적용하기 위한 임시 해제
                cursorRb.velocity = Vector3.zero;
                cursorRb.angularVelocity = Vector3.zero;
                cursorRb.Sleep();                        // 물리 시뮬레이션 정지
                cursorRb.isKinematic = true;             // 외력 영향 차단
            }

            // Grab 전까지 커서를 고정 상태로 유지
            _freezePhysics = true;

            // Transform 변경 사항을 즉시 물리 엔진에 반영
            Physics.SyncTransforms();
        }
    }

    private void FixedUpdate()
    {
        // 사용자가 다시 Grab하면 상호작용을 위해 잠금을 해제한다.
        // Grab 상태는 Grabbable의 SelectingPointsCount로 판단한다.
        if (_freezePhysics && cursorGrabbable != null && cursorGrabbable.SelectingPointsCount > 0)
        {
            UnfreezeForInteraction();
            return;
        }

        // 잠금 상태에서는 매 물리 프레임마다 속도를 0으로 유지하여
        // 외부 충돌이나 중력 영향으로 이동하지 않도록 한다.
        if (_freezePhysics && cursorRb != null)
        {
            cursorRb.velocity = Vector3.zero;
            cursorRb.angularVelocity = Vector3.zero;
            cursorRb.isKinematic = true;
        }
    }

    private void OnThrown(Vector3 v, Vector3 w)
    {
        // 실제 Throw가 발생하면 물리 잠금을 해제한다.
        // 이후 CollisionCursorTarget에서 충돌 이벤트가 발생하게 된다.
        _freezePhysics = false;

        if (cursorRb != null)
        {
            cursorRb.isKinematic = false;
            cursorRb.WakeUp();
        }
    }

    private void UnfreezeForInteraction()
    {
        // Grab이 시작된 경우에도 잠금을 해제하여
        // 정상적인 물리 기반 상호작용이 가능하도록 한다.
        _freezePhysics = false;

        if (cursorRb != null)
        {
            cursorRb.isKinematic = false;
            cursorRb.WakeUp();
        }
    }
}
