using UnityEngine;

public class LayerManager : MonoBehaviour
{
    public GameObject modelContainer; // 直接指向自身

    void Start()
    {
        // 确保层级存在
        if (LayerMask.NameToLayer("3DModel") == -1)
        {
            Debug.LogError("请先在Unity中创建 '3D Model' 层！");
            return;
        }

        // 设置层级
        modelContainer.layer = LayerMask.NameToLayer("3DModel");
    }
}

