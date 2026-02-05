using UnityEngine;

public class CharacterMovement
{
    private Transform _transform;
    private float _speed;
    private int _obstacleMask;

    public CharacterMovement(Transform transform, float speed)
    {
        _transform = transform;
        _speed = speed;
        _obstacleMask = LayerMask.GetMask("Wall", "Barricade");
    }

    public void Move(Vector2 inputDir)
    {
        Vector3 moveDir = CalculateCameraRelativeDirection(inputDir);
        float moveDist = _speed * Time.deltaTime;

        // 충돌 체크
        if (!CheckObstacle(moveDir, moveDist))
        {
            _transform.position += moveDir * moveDist;
        }

        // 회전
        if (moveDir != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            _transform.rotation = Quaternion.Slerp(_transform.rotation, targetRotation, 10.0f * Time.deltaTime);
        }
    }

    public void RotateToMouse()
    {
        if (Camera.main == null) return;

        Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);

        int layerMask = LayerMask.GetMask("MapGround");
        if (layerMask == 0) layerMask = -1;

        if (Physics.Raycast(ray, out RaycastHit hit, 100.0f, layerMask))
        {
            Vector3 target = hit.point;
            target.y = _transform.position.y;
            _transform.LookAt(target);
        }
    }

    private Vector3 CalculateCameraRelativeDirection(Vector2 inputDir)
    {
        if (Camera.main == null)
            return new Vector3(inputDir.x, 0, inputDir.y).normalized;

        Vector3 camFwd = Camera.main.transform.forward;
        Vector3 camRight = Camera.main.transform.right;
        camFwd.y = 0;
        camRight.y = 0;
        camFwd.Normalize();
        camRight.Normalize();

        return (camFwd * inputDir.y + camRight * inputDir.x).normalized;
    }

    private bool CheckObstacle(Vector3 direction, float distance)
    {
        Vector3 rayOrigin = _transform.position + Vector3.up * 0.5f;
        return Physics.Raycast(rayOrigin, direction, distance + 0.5f, _obstacleMask);
    }
}
