using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PawnMover : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 4f;
    public float stopDistance = 0.05f;

    [Header("Ground")]
    public float gravity = -20f;
    public float groundStickForce = -2f;

    private CharacterController cc;
    private Vector3 target;
    private bool hasTarget;

    private float verticalVel;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        target = transform.position;
        hasTarget = false;
    }

    public void SetMoveTarget(Vector3 worldPoint)
    {
        // 지면이 평면이거나, hit.point의 y를 그대로 쓰고 싶으면 아래 줄을 지우셔도 됩니다.
        worldPoint.y = transform.position.y;

        target = worldPoint;
        hasTarget = true;
    }

    private void Update()
    {
        // 중력
        if (cc.isGrounded)
        {
            verticalVel = groundStickForce; // 바닥에 붙도록 살짝 눌러줌
        }
        else
        {
            verticalVel += gravity * Time.deltaTime;
        }

        Vector3 move = new Vector3(0f, verticalVel, 0f);

        // 수평 이동
        if (hasTarget)
        {
            Vector3 to = target - transform.position;
            to.y = 0f;

            float dist = to.magnitude;
            if (dist <= stopDistance)
            {
                hasTarget = false;
            }
            else
            {
                Vector3 dir = to / dist; // normalize

                // 나중에 “그리드 가중치/알고리즘” 붙일 곳은 여기(dir 계산)입니다.
                // Vector3 dir = SteeringAlgorithm.GetDirection(transform.position, target);

                move += dir * moveSpeed;
                // 바라보게 하고 싶으면(선택)
                // transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 12f * Time.deltaTime);
            }
        }

        cc.Move(move * Time.deltaTime);
    }

    public bool HasTarget() => hasTarget;
    public Vector3 Target() => target;
}