using UnityEngine;
using Oculus.Interaction;
using System;

public class CursorGrabbed : MonoBehaviour
{
    [SerializeField] private InteractableUnityEventWrapper eventWrapper;

    // 현재 커서가 잡혀 있는지 여부를 전역 상태로 제공한다.
    // TargetSpawn, Experiments 등 다른 스크립트에서 trial 흐름 제어에 사용된다.
    public static bool IsGrabbed = false;

    // Release 이후 “던진 상태”인지 나타내는 전역 플래그.
    // 충돌을 유효한 시도로 인정할지 판단할 때 사용된다.
    public static bool IsThrown = false;

    // Grab 시작/종료를 외부 시스템에 알리기 위한 이벤트.
    // Experiments는 이 이벤트로 throw latency를 계산하고 trial 게이트를 초기화한다.
    public static event Action GrabStarted;
    public static event Action GrabEnded;

    private void OnEnable()
    {
        // Oculus Interaction의 Select/Unselect 이벤트를 grab/release 신호로 연결한다.
        eventWrapper.WhenSelect.AddListener(OnGrab);
        eventWrapper.WhenUnselect.AddListener(OnRelease);
    }

    private void OnDisable()
    {
        // 중복 등록 및 잔여 리스너 방지를 위해 반드시 해제한다.
        eventWrapper.WhenSelect.RemoveListener(OnGrab);
        eventWrapper.WhenUnselect.RemoveListener(OnRelease);

        // 비활성화 시 전역 상태를 초기화하여 다음 실행에 영향이 없도록 한다.
        IsGrabbed = false;
        IsThrown = false;
    }

    private void OnGrab()
    {
        // Grab 시작 시 상태를 갱신하고, 이전 throw 상태는 해제한다.
        // 재시도 상황에서 이전 trial의 던진 상태가 남지 않도록 하기 위함이다.
        IsGrabbed = true;
        IsThrown = false;

        GrabStarted?.Invoke();
    }

    private void OnRelease()
    {
        // Release 시점부터 “던진 상태”로 간주한다.
        // 이후 발생하는 충돌만 유효 시도로 처리된다.
        IsGrabbed = false;
        IsThrown = true;

        GrabEnded?.Invoke();
    }
}
