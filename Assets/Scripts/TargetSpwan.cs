// TargetSpawn.cs
// 타겟의 표시/비표시만 담당한다.
// 충돌 판정 자체는 CollisionCursorTarget에서 발생하며, 여기서는 “맞춘 뒤 일정 시간 유지 후 숨김”만 수행한다.
// 타겟이 실제로 숨겨진 직후(TargetHidden) 이벤트를 발행하여, Experiments가 조건 변경을 안전한 타이밍에 수행하도록 유도한다.

using UnityEngine;
using System;
using System.Collections;

public class TargetSpawn : MonoBehaviour
{
    [SerializeField] private GameObject target;         // 화면에 보이는 실제 타겟(렌더러 포함)
    [SerializeField] private GameObject collisionArea;  // 판정 영역(트리거/콜라이더 등)
    [SerializeField] private float hideDelay = 0.3f;    // 적중 후 타겟을 유지하는 시간

    public GameObject Target => target;
    public GameObject CollisionArea => collisionArea;

    // 타겟이 비활성화된 직후를 알리는 이벤트.
    // 다음 trial 조건 적용은 이 시점 이후에 수행하는 것이 안전하다(타겟이 화면에 남아있는 동안 크기/위치를 바꾸지 않기 위함).
    public event Action TargetHidden;

    private Coroutine hideCoroutine;

    private void OnEnable()
    {
        // Grab 시작 시 타겟을 노출한다(던지기 전 “준비 상태”).
        CursorGrabbed.GrabStarted += OnGrabStarted;

        // Trigger/Collision 적중 이벤트를 모두 수신하여 “맞춘 뒤 숨김” 로직을 공통 처리한다.
        // 실제 적중 좌표가 필요한 경우는 Experiments에서 CollisionHitWithPoint를 별도로 구독한다.
        CollisionCursorTarget.TriggerHit += OnAnyHit;
        CollisionCursorTarget.CollisionHit += OnAnyHit;
    }

    private void OnDisable()
    {
        CursorGrabbed.GrabStarted -= OnGrabStarted;
        CollisionCursorTarget.TriggerHit -= OnAnyHit;
        CollisionCursorTarget.CollisionHit -= OnAnyHit;
    }

    private void Start()
    {
        // 시작 시에는 타겟을 숨긴 상태로 둔다(Grab 이후에만 노출).
        if (target != null) target.SetActive(false);
    }

    private void OnGrabStarted()
    {
        // 새로 잡았을 때 이전 적중으로 예약된 hide 코루틴이 남아있다면 취소한다.
        // 재시도(다시 grab) 상황에서 타겟이 예기치 않게 꺼지는 것을 방지한다.
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }

        if (target != null) target.SetActive(true);
    }

    private void OnAnyHit(GameObject hitObject)
    {
        // “던진 상태”에서만 적중으로 인정한다(잡은 상태에서의 접촉/겹침은 무시).
        if (!CursorGrabbed.IsThrown) return;

        // 타겟 태그만 처리한다.
        if (hitObject == null || !hitObject.CompareTag("Target")) return;

        // 이미 hide가 예약되어 있다면 중복 예약을 방지한다.
        if (hideCoroutine != null) return;

        // 적중 시 hideDelay 동안 타겟을 유지한 뒤 숨김 처리한다.
        hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        // 적중 직후 타겟을 즉시 숨기지 않고, 시각적 피드백을 위해 일정 시간 유지한다.
        yield return new WaitForSeconds(hideDelay);

        if (target != null) target.SetActive(false);
        hideCoroutine = null;

        // 다음 시도를 위해 “던진 상태”를 해제한다.
        // 이후 GrabStarted가 다시 들어오면 타겟을 다시 보여주는 흐름으로 연결된다.
        CursorGrabbed.IsThrown = false;

        // 타겟이 완전히 숨겨진 직후를 알린다.
        TargetHidden?.Invoke();
    }
}
