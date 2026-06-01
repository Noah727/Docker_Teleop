using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-900)]
public class RobotFirstPersonTestMode : MonoBehaviour
{
    public string sceneNameContains = "FirstPerson";
    public Vector3 robotViewScreenPosition = new Vector3(0.0f, 1.35f, 1.05f);
    public Vector3 robotViewScreenEuler = new Vector3(0f, 0f, 0f);
    public Vector3 robotViewScreenScale = new Vector3(0.0018f, 0.0018f, 0.0018f);
    public Vector2 robotViewScreenSize = new Vector2(620f, 420f);
    public float robotViewFov = 105f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapFirstPersonScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name.IndexOf("FirstPerson", System.StringComparison.OrdinalIgnoreCase) < 0)
            return;

        if (FindAny<RobotFirstPersonTestMode>() != null)
            return;

        GameObject root = GameObject.Find("QuestMRFeatures");
        if (root == null)
            root = new GameObject("QuestMRFeatures");
        root.AddComponent<RobotFirstPersonTestMode>();
    }

    private void Start()
    {
        ConfigureScene();
    }

    private void ConfigureScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name.IndexOf(sceneNameContains, System.StringComparison.OrdinalIgnoreCase) < 0)
            return;

        RobotViewpointController robotView = FindAny<RobotViewpointController>();
        if (robotView == null)
            robotView = gameObject.AddComponent<RobotViewpointController>();

        robotView.enabled = true;
        robotView.createRobotBaseViewCamera = true;
        robotView.createRobotViewScreen = true;
        robotView.screenName = "RobotFirstPersonTestWindow";
        robotView.screenWorldPosition = robotViewScreenPosition;
        robotView.screenWorldEuler = robotViewScreenEuler;
        robotView.screenWorldScale = robotViewScreenScale;
        robotView.screenSize = robotViewScreenSize;
        robotView.fieldOfView = robotViewFov;

        MRCentralControlPanel panel = FindAny<MRCentralControlPanel>();
        if (panel != null)
        {
            panel.panelWorldPosition = new Vector3(0f, 1.7f, 1.0f);
            panel.panelWorldEuler = Vector3.zero;
            panel.panelWorldScale = new Vector3(0.00115f, 0.00115f, 0.00115f);
        }
    }

    private static T FindAny<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
#else
        return Object.FindObjectOfType<T>(true);
#endif
    }
}
