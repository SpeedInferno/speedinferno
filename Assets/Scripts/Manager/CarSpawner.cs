using UnityEngine;

// Loads the car chosen in the Garage into a race scene.
// The car already placed in the scene is used as the spawn point and as the source of the scene's UI wiring.
// If that same car was selected it is kept untouched (with its per-scene tuning); otherwise it is replaced.
public class CarSpawner : MonoBehaviour
{
    [Tooltip("Car already placed in this scene (spawn point + UI wiring source).")]
    public PlayerCarController sceneCar;

    [Tooltip("Selectable car prefabs in Garage order: 0 = Altima, 1 = GT-R.")]
    public GameObject[] carPrefabs;

    [Tooltip("Garage index of the car already placed in this scene.")]
    public int sceneCarIndex = 0;

    void Awake()
    {
        int index = GameSelection.SelectedCarIndex;
        if (sceneCar == null || carPrefabs == null || index < 0 || index >= carPrefabs.Length)
        {
            return;
        }
        if (index == sceneCarIndex || carPrefabs[index] == null)
        {
            return;
        }

        GameObject template = sceneCar.gameObject;
        Vector3 position = template.transform.position;
        Quaternion rotation = template.transform.rotation;

        // Hide the placed car right away so the camera and other lookups only find the new one.
        template.SetActive(false);

        GameObject newCarObject = Instantiate(carPrefabs[index], position, rotation);
        newCarObject.name = carPrefabs[index].name;

        PlayerCarController newCar = newCarObject.GetComponent<PlayerCarController>();
        if (newCar != null)
        {
            // Scene UI / HUD references
            newCar.rightBtn = sceneCar.rightBtn;
            newCar.leftBtn = sceneCar.leftBtn;
            newCar.driftBtn = sceneCar.driftBtn;
            newCar.nitroBtn = sceneCar.nitroBtn;
            newCar.speedText = sceneCar.speedText;
            newCar.gearNoText = sceneCar.gearNoText;
            newCar.hudElement = sceneCar.hudElement;
            if (newCar.tireTrail == null)
            {
                newCar.tireTrail = sceneCar.tireTrail;
            }
        }

        DriftController oldDrift = template.GetComponent<DriftController>();
        DriftController newDrift = newCarObject.GetComponent<DriftController>();
        if (oldDrift != null && newDrift != null)
        {
            bool targetWasSelf = oldDrift.targetObject == null || oldDrift.targetObject == template;
            newDrift.targetObject = targetWasSelf ? newCarObject : oldDrift.targetObject;
        }

        Destroy(template);
    }
}
