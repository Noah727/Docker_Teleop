#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class AssignHandReferences : MonoBehaviour
{
    [MenuItem("Tools/Assign Hand References")]
    public static void Assign()
    {
        GameObject senderObj = GameObject.Find("NetworkSender");
        if (senderObj == null) { Debug.LogError("NetworkSender not found!"); return; }

        HandPoseSender sender = senderObj.GetComponent<HandPoseSender>();
        if (sender == null) { Debug.LogError("HandPoseSender component not found!"); return; }

        GameObject leftHand = GameObject.Find("OVRHandPrefab_Left");
        GameObject rightHand = GameObject.Find("OVRHandPrefab_Right");

        if (leftHand != null) sender.leftHandTransform = leftHand.transform;
        else Debug.LogError("OVRHandPrefab_Left not found!");

        if (rightHand != null) sender.rightHandTransform = rightHand.transform;
        else Debug.LogError("OVRHandPrefab_Right not found!");

        EditorUtility.SetDirty(sender);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(senderObj.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(senderObj.scene);
        Debug.Log("Successfully assigned Hand Transforms to NetworkSender.");
    }
}
#endif
