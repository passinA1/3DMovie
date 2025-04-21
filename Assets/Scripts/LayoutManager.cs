using UnityEngine;

public class LayerManager : MonoBehaviour
{
    public GameObject modelContainer; // ֱ��ָ������

    void Start()
    {
        // ȷ���㼶����
        if (LayerMask.NameToLayer("3DModel") == -1)
        {
            Debug.LogError("������Unity�д��� '3D Model' �㣡");
            return;
        }

        // ���ò㼶
        modelContainer.layer = LayerMask.NameToLayer("3DModel");
    }
}

