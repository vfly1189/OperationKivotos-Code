using UnityEngine;

public class MouthController : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private int materialIndex = 0;

    private Material material;
    private static readonly int MouthOffsetID = Shader.PropertyToID("_MouthOffset");

    void Start()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        // EyeMouth 이름이 포함된 Material 찾기
        Material[] materials = targetRenderer.materials;
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i].name.Contains("EyeMouth"))
            {
                material = materials[i];
                Debug.Log($"Found EyeMouth material at index {i}: {material.name}");
                break;
            }
        }

        if (material == null)
        {
            Debug.LogError("EyeMouth material not found!");
        }
    }

    // 애니메이션 이벤트에서 호출
    public void SetMouthTile(int index)
    {
        Debug.Log("호출됨");
        if (material == null)
        {
            Debug.Log("Material이 없음");
            return;
        }
        Debug.Log("Material이 있음");
        int x = index % 8;
        int y = index / 8;

        Debug.Log($" x : {x} ,  y : {y}");

        material.SetVector(MouthOffsetID, new Vector4(x, y, 0, 0));
    }
}