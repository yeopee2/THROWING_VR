using UnityEngine;
using System.IO;
using System.Text;
using System.Collections.Generic;
using TMPro;

public class Experiments : MonoBehaviour
{
    [Header("Experiment Meta")]
    public string participantID;
    public int blockNum;

    [SerializeField] private TargetSpawn targetSpawn;
    [SerializeField] private TextMeshProUGUI messageText;
    
    [Header("Trajectory Tracking")]
    [SerializeField] private string ballTag = "Pong"; // Tag로 공 찾기
    private Transform ballTransform;

    // 조건 변수: 타겟 크기(width)와 타겟까지의 거리(z축, amplitude).
    private float[] widths = new float[] { 0.3f, 0.6f, 1.2f };
    private float[] amplitudes = new float[] { 1, 1.7f, 2.3f, 3f };

    // trialOrder는 조건 index의 랜덤 시퀀스이며, 앞쪽에 더미 trial을 포함한다.
    private int[] trialOrder;
    private int trialIndex = 0;
    private const int REPEAT = 2;

    private int totalConditions;
    private int totalTrials;

    private string csvPath;
    private string resultDir; // 결과 폴더 경로
    private string jsonDir; // 결과 폴더 경로
    private const int DUMMY_COUNT = 3;

    // GrabStarted 시각과 GrabEnded 시각을 이용해 grab->throw latency를 기록한다.
    // CursorGrabbed 이벤트로 시점을 수신하여 throwLatency를 계산한다.
    private float grabStartTime = -1f;
    private bool hasGrabbed = false;
    private bool hasThrown = false;
    private float throwLatency = -1f;

    // 물리 충돌이 같은 throw에서 중복 호출될 수 있으므로, 한 trial당 1회만 처리하도록 게이트를 둔다.
    private bool trialCollisionHandled = false;
    private float lastCollisionTime = -999f;
    private const float COLLISION_DEBOUNCE_SEC = 0.10f;

    // 궤적 추적 변수
    private bool isTrackingTrajectory = false;
    private List<Vector3Serializable> currentTrajectory = new List<Vector3Serializable>();

    private void Start()
    {
        // ballTag 존재 확인
        GameObject testBall = GameObject.FindGameObjectWithTag(ballTag);
        if (testBall == null)
        {
            Debug.LogError($"[TRAJECTORY] No GameObject with tag '{ballTag}' found! Please tag your ball object.");
        }
        else
        {
            ballTransform = testBall.transform;
        }

        totalConditions = widths.Length * amplitudes.Length;
        totalTrials = totalConditions * REPEAT + DUMMY_COUNT;

        BuildRandomTrialOrder();

        // 결과 디렉토리 설정 (SetupCSV 이전에 설정되어야 함)
        resultDir = Path.Combine(Application.dataPath, "result");
        if (!Directory.Exists(resultDir)) Directory.CreateDirectory(resultDir);
        
        jsonDir = Path.Combine(Application.dataPath, "result/trajectories");
        if (!Directory.Exists(jsonDir)) Directory.CreateDirectory(jsonDir);

        SetupCSV();
        
        // 첫 trial의 조건을 적용하되, 타겟 표시는 GrabStarted(TargetSpawn)에서 수행되도록 초기에는 숨겨둔다.
        ApplyCondition(trialOrder[trialIndex]);

        if (targetSpawn.Target != null)
            targetSpawn.Target.SetActive(false);

        ResetPerTrialGates();

        Debug.Log($"[EXP] Start P={participantID} Block={blockNum} Trials={totalTrials}");
    }

    private void FixedUpdate()
    {
        // 궤적 추적 중일 때 고정 간격(50Hz)으로 공의 위치 기록
        if (isTrackingTrajectory)
        {
            RecordTrajectoryPoint();
        }
    }

    private void OnEnable()
    {
        // 실제 접촉점(world)을 받는 collision 이벤트를 사용한다.
        // Trigger는 근사점이며, 정확도 계산은 collision contact point 기반으로 처리한다.
        CollisionCursorTarget.CollisionHitWithPoint += OnCollisionHitWithPoint;

        // Grab/Release 시점을 받아 throwLatency를 계산하고, 재시도 시 게이트를 초기화한다.
        CursorGrabbed.GrabStarted += OnCursorGrabStarted;
        CursorGrabbed.GrabEnded += OnCursorGrabEnded;
    }

    private void OnDisable()
    {
        CollisionCursorTarget.CollisionHitWithPoint -= OnCollisionHitWithPoint;

        CursorGrabbed.GrabStarted -= OnCursorGrabStarted;
        CursorGrabbed.GrabEnded -= OnCursorGrabEnded;
    }

    private void OnCursorGrabStarted()
    {
        // 새 시도 시작 시각 기록 및 latency 측정 상태 초기화.
        grabStartTime = Time.time;
        hasGrabbed = true;
        hasThrown = false;
        throwLatency = -1f;

        GameObject ball = GameObject.FindGameObjectWithTag(ballTag);
        if (ball != null) ballTransform = ball.transform;

        // 실패 후 재grab하는 경우에도 동일 trial에서 충돌을 1회만 처리하도록 게이트를 이 시점에 해제한다.
        ResetPerTrialGates();
        
        // 이전 추적 중이었다면 중지 (재시도 시)
        if (isTrackingTrajectory)
        {
            StopTrajectoryTracking();
        }
    }

    private void OnCursorGrabEnded()
    {
        // 같은 trial에서 Release가 여러 번 들어오는 것을 방지하고, 최초 Release만 latency로 기록한다.
        if (!hasGrabbed) return;
        if (hasThrown) return;

        throwLatency = Time.time - grabStartTime;
        hasThrown = true;

        // Release 순간부터 궤적 추적 시작
        StartTrajectoryTracking();
    }

    private void BuildRandomTrialOrder()
    {
        // 모든 조건을 REPEAT만큼 반복한 뒤 셔플한다.
        // 더미 trial(DUMMY_COUNT)은 적응/연습용이며, 결과 저장에서 제외한다.
        List<int> list = new List<int>();

        for (int c = 0; c < totalConditions; c++)
            for (int r = 0; r < REPEAT; r++)
                list.Add(c);

        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        List<int> dummy = new List<int>();
        for (int i = 0; i < DUMMY_COUNT; i++)
        {
            int randCond = Random.Range(0, totalConditions);
            dummy.Add(randCond);
        }

        dummy.AddRange(list);
        trialOrder = dummy.ToArray();
    }

    private void SetupCSV()
    {
        // 결과 파일은 Assets/result 아래에 생성한다.
        // 파일이 없을 때만 헤더를 작성하여 append 기반 로그를 유지한다.
        csvPath = Path.Combine(resultDir, $"{participantID}_results.csv");

        if (!File.Exists(csvPath))
        {
            File.WriteAllText(csvPath,
                "participant,block,trial," +
                "widthVal,amplitudeVal," +
                "targetX,targetY,targetZ," +
                "contactX,contactY,contactZ," +
                "L2,radius,accuracy," +
                "throwLatency,JSONFileName\n"); // JSONFileName 헤더 추가
        }
    }

    private void AppendCSV(PerfData d)
    {
        // PerfData를 CSV 한 줄로 직렬화한다.
        StringBuilder sb = new StringBuilder();

        sb.Append($"{participantID},");
        sb.Append($"{blockNum},");
        sb.Append($"{d.trial},");
        sb.Append($"{d.widthValue},");
        sb.Append($"{d.amplitudeValue},");

        sb.Append($"{d.targetCenter.x},{d.targetCenter.y},{d.targetCenter.z},");
        sb.Append($"{d.contactPoint.x},{d.contactPoint.y},{d.contactPoint.z},");

        sb.Append($"{d.l2},{d.radius},{d.accuracy},");
        sb.Append($"{d.throwLatency},");
        sb.Append($"{d.JSONFileName}\n"); // JSONFileName 데이터 기록

        File.AppendAllText(csvPath, sb.ToString());
    }

    private void StartTrajectoryTracking()
    {
        // 궤적 추적 시작
        isTrackingTrajectory = true;
        currentTrajectory.Clear();
        
        Debug.Log($"[TRAJECTORY] Started tracking for trial {trialIndex}");
    }

    private void StopTrajectoryTracking()
    {
        // 궤적 추적 중지
        if (isTrackingTrajectory)
        {
            isTrackingTrajectory = false;
            Debug.Log($"[TRAJECTORY] Stopped tracking. Recorded {currentTrajectory.Count} points");
        }
    }

    private void RecordTrajectoryPoint()
    {
        // 매번 Find를 하지 않고, Grab 시작 때 저장해둔 참조를 사용
        if (ballTransform == null || !ballTransform.gameObject.activeInHierarchy)
        {
            // 만약 공이 사라졌다면(풀링 등으로 인해), 다시 한번만 시도
            GameObject ballObj = GameObject.FindGameObjectWithTag(ballTag);
            if (ballObj != null) 
            {
                ballTransform = ballObj.transform;
            }
            else 
            {
                // 공을 찾지 못하면 기록하지 않음
                return;
            }
        }

        // 위치 기록
        Vector3Serializable point = new Vector3Serializable(ballTransform.position);
        currentTrajectory.Add(point);
    }

    private void DiscardCurrentTrajectory()
    {
        // 정상 trial이 아닌 경우 궤적 삭제
        StopTrajectoryTracking();
        currentTrajectory.Clear();
        Debug.Log($"[TRAJECTORY] Discarded trajectory (not a valid trial)");
    }

    private void SaveCurrentTrajectory(PerfData d)
    {
        // 정상 trial인 경우 궤적 저장 (빈 궤적도 저장하여 데이터 일관성 유지)
        if (currentTrajectory.Count == 0)
        {
            Debug.LogWarning($"[TRAJECTORY] Empty trajectory for trial {d.trial} - saving anyway for consistency");
        }

        // TrialTrajectoryData trialTraj = new TrialTrajectoryData
        // {
        //     participantID = participantID,
        //     block = blockNum,
        //     trial = d.trial,
        //     widthValue = d.widthValue,
        //     amplitudeValue = d.amplitudeValue,
        //     trajectoryPoints = new List<Vector3Serializable>(currentTrajectory)
        // };
        TrialTrajectoryData trialTraj = new TrialTrajectoryData
        {
            trajectoryPoints = new List<Vector3Serializable>(currentTrajectory)
        };

        // GetPerformanceWorldCentered에서 결정된 파일명을 사용하여 파일 저장
        string filePath = Path.Combine(jsonDir, d.JSONFileName);

        string json = JsonUtility.ToJson(trialTraj, true);
        File.WriteAllText(filePath, json);
        
        Debug.Log($"[TRAJECTORY] Saved individual file: {d.JSONFileName} ({currentTrajectory.Count} points)");
        
        // 저장 후 현재 궤적 초기화
        currentTrajectory.Clear();
    }

    private void OnCollisionHitWithPoint(Transform cursorTransform, GameObject hitObject, Vector3 contactPointWorld)
    {
        // 타겟 외 오브젝트와의 충돌은 무시한다.
        if (hitObject == null) return;
        if (!hitObject.CompareTag("Target")) return;

        // 같은 trial/같은 throw에서 충돌 이벤트가 중복 발화되는 경우를 방지한다.
        if (trialCollisionHandled) return;
        if (Time.time - lastCollisionTime < COLLISION_DEBOUNCE_SEC) return;

        trialCollisionHandled = true;
        lastCollisionTime = Time.time;

        GameObject trueTarget = targetSpawn.Target;
        if (trueTarget == null) return;

        int condIdx = trialOrder[trialIndex];

        // 접촉점을 타겟 중심 기준으로 정규화하여 거리(L2)를 계산하고 성공/실패를 판정한다.
        PerfData data = GetPerformanceWorldCentered(trueTarget, contactPointWorld, condIdx);

        // 궤적 추적 중지
        StopTrajectoryTracking();

        // 더미 trial인지 확인
        bool isDummy = trialIndex < DUMMY_COUNT;
        
        if (isDummy)
        {
            // 더미 trial: CSV 저장 안 함, 궤적 삭제
            DiscardCurrentTrajectory();
            Debug.Log($"[EXP] Dummy trial {trialIndex} - data not saved");
        }
        else
        {
            // 정상 trial: CSV와 궤적 모두 저장
            AppendCSV(data);
            SaveCurrentTrajectory(data); // 매개변수로 data 전체를 전달하여 메타데이터 활용
        }

        // 참가자에게 남은 trial과 결과를 표시한다.
        UpdateMessageText(data.accuracy);

        trialIndex++;

        if (trialIndex >= totalTrials)
        {
            Debug.Log("[EXP] ALL TRIALS DONE — APPLICATION QUIT");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
            return;
        }

        // 다음 trial의 조건을 적용하되, 타겟 노출은 GrabStarted(TargetSpawn)에서 수행된다.
        ApplyCondition(trialOrder[trialIndex]);

        if (targetSpawn.Target != null)
            targetSpawn.Target.SetActive(false);

        // 게이트는 다음 GrabStarted에서 해제된다.
        // 동일 throw에서 연속 충돌이 들어올 때 trial이 건너뛰는 문제를 방지하기 위함이다.
    }

    private void ApplyCondition(int idx)
    {
        // 조건 index를 width/amplitude index로 분해하여 타겟 및 판정 영역의 크기/위치를 설정한다.
        GameObject target = targetSpawn.Target;
        GameObject collisionArea = targetSpawn.CollisionArea;
        if (target == null || collisionArea == null) return;

        int wIdx = idx / amplitudes.Length;
        int aIdx = idx % amplitudes.Length;

        float width = widths[wIdx];

        Vector3 s = target.transform.localScale;
        s.x = width;
        s.y = width;
        target.transform.localScale = s;

        // collisionArea는 타겟보다 넓게 설정하여 판정 영역을 별도로 운용한다.
        Vector3 sc = collisionArea.transform.localScale;
        sc.x = width * 3f;
        sc.y = width * 3f;
        collisionArea.transform.localScale = sc;

        float z = amplitudes[aIdx];

        Vector3 p = target.transform.position;
        p.z = z;
        target.transform.position = p;

        Vector3 pc = collisionArea.transform.position;
        pc.z = z;
        collisionArea.transform.position = pc;
    }

    private void UpdateMessageText(int accuracy)
    {
        // 결과 표시는 SUCCESS/FAIL만 제공하며, 남은 trial 수를 함께 표시한다.
        if (messageText == null) return;

        messageText.fontSize = 0.1f;

        int remaining = totalTrials - trialIndex - 1;

        string resultText;
        if (accuracy == 1)
            resultText = "<color=#3A86FF>SUCCESS</color>";
        else
            resultText = "<color=#FF3B30>FAIL</color>";

        messageText.text =
            $"Remaining: {remaining}\n" +
            $"Result: {resultText}";
    }

    private PerfData GetPerformanceWorldCentered(GameObject trueTarget, Vector3 contactPointWorld, int condIdx)
    {
        // 접촉점을 타겟 중심 기준으로 변환하여 거리 기반 성능을 계산한다.
        int wIdx = condIdx / amplitudes.Length;
        int aIdx = condIdx % amplitudes.Length;

        float width = widths[wIdx];
        float amplitude = amplitudes[aIdx];

        Vector3 targetCenter = trueTarget.transform.position;
        Vector3 centered = contactPointWorld - targetCenter;

        Vector2 xy = new Vector2(centered.x, centered.y);
        float l2 = xy.magnitude;

        // 성공 판정 반경은 width 기반으로 정의한다.
        float radius = width * 0.25f;
        int acc = (l2 <= radius) ? 1 : 0;

        // throwLatency는 Release가 정상적으로 감지된 경우에만 기록한다.
        float latencyToSave = (hasThrown && throwLatency >= 0f) ? throwLatency : -1f;

        // 실제 Trial 번호 계산
        int realTrialNum = trialIndex - DUMMY_COUNT;

        return new PerfData
        {
            trial = realTrialNum,
            widthValue = width,
            amplitudeValue = amplitude,
            targetCenter = targetCenter,
            contactPoint = contactPointWorld,
            l2 = l2,
            radius = radius,
            accuracy = acc,
            throwLatency = latencyToSave,
            // JSONFileName을 여기서 생성하여 CSV와 JSON 저장 시 동일한 이름을 사용하도록 보장
            JSONFileName = $"{participantID}_{blockNum}_{realTrialNum}_{radius}_{amplitude}_trajectories.json"
        };
    }

    private void ResetPerTrialGates()
    {
        // 한 trial에서 충돌을 단 한 번만 처리하도록 게이트를 초기화한다.
        // 재grab 재시도 흐름에서는 GrabStarted 시점에 다시 초기화된다.
        trialCollisionHandled = false;
        lastCollisionTime = -999f;
    }
}

[System.Serializable]
public class PerfData
{
    public int trial;
    public float widthValue;
    public float amplitudeValue;
    public Vector3 targetCenter;
    public Vector3 contactPoint;
    public float l2;
    public float radius;
    public int accuracy;
    public float throwLatency;
    public string JSONFileName; // 추가된 필드
}

[System.Serializable]
public class Vector3Serializable
{
    public float x;
    public float y;
    public float z;

    public Vector3Serializable(Vector3 v)
    {
        x = v.x;
        y = v.y;
        z = v.z;
    }
}

[System.Serializable]
public class TrialTrajectoryData
{
    public string participantID;
    public int block;
    public int trial;
    public float widthValue;      
    public float amplitudeValue; 
    public List<Vector3Serializable> trajectoryPoints;
}

[System.Serializable]
public class TrajectoryDataCollection
{
    public List<TrialTrajectoryData> trajectories;
}