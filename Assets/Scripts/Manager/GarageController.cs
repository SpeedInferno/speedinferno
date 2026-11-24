using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// Garage screen: pick one of the cars and one of the maps, then start the race.
// Array order must match GameSelection (cars 0 = Altima, 1 = GT-R; maps 0 = Map 1, 1 = Map 2).
public class GarageController : MonoBehaviour
{
    [Header("Cars")]
    public Button[] carButtons;
    [Tooltip("Preview model for each car, shown on the turntable when selected.")]
    public GameObject[] carPreviews;

    [Header("Maps")]
    public Button[] mapButtons;

    [Header("Labels / actions")]
    public TextMeshProUGUI selectionText;
    public Button raceButton;
    public Button backButton;

    [Header("Turntable")]
    public Transform turntable;
    public float turntableSpeed = 20f;

    [Header("Button colours")]
    public Color normalColor = new Color(0.12f, 0.12f, 0.14f, 0.9f);
    public Color selectedColor = new Color(0.95f, 0.35f, 0.05f, 1f);

    [Header("Scenes")]
    public string loadingScene = "Loading1";
    public string titleScene = "Title";

    private int carIndex;
    private int mapIndex;

    void Start()
    {
        carIndex = GameSelection.SelectedCarIndex;
        mapIndex = GameSelection.SelectedMapIndex;

        for (int i = 0; i < carButtons.Length; i++)
        {
            int index = i;
            if (carButtons[i] != null)
            {
                carButtons[i].onClick.AddListener(() => SelectCar(index));
            }
        }
        for (int i = 0; i < mapButtons.Length; i++)
        {
            int index = i;
            if (mapButtons[i] != null)
            {
                mapButtons[i].onClick.AddListener(() => SelectMap(index));
            }
        }
        if (raceButton != null)
        {
            raceButton.onClick.AddListener(StartRace);
        }
        if (backButton != null)
        {
            backButton.onClick.AddListener(BackToTitle);
        }

        Refresh();
    }

    void Update()
    {
        if (turntable != null)
        {
            turntable.Rotate(0f, turntableSpeed * Time.deltaTime, 0f, Space.World);
        }
    }

    public void SelectCar(int index)
    {
        carIndex = Mathf.Clamp(index, 0, GameSelection.CarNames.Length - 1);
        GameSelection.SelectedCarIndex = carIndex;
        Refresh();
    }

    public void SelectMap(int index)
    {
        mapIndex = Mathf.Clamp(index, 0, GameSelection.MapScenes.Length - 1);
        GameSelection.SelectedMapIndex = mapIndex;
        Refresh();
    }

    public void StartRace()
    {
        GameSelection.SelectedCarIndex = carIndex;
        GameSelection.SelectedMapIndex = mapIndex;
        SceneManager.LoadScene(loadingScene);
    }

    public void BackToTitle()
    {
        SceneManager.LoadScene(titleScene);
    }

    private void Refresh()
    {
        for (int i = 0; i < carPreviews.Length; i++)
        {
            if (carPreviews[i] != null)
            {
                carPreviews[i].SetActive(i == carIndex);
            }
        }
        Highlight(carButtons, carIndex);
        Highlight(mapButtons, mapIndex);

        if (selectionText != null)
        {
            selectionText.text = GameSelection.CarNames[carIndex] + "  |  " + GameSelection.MapNames[mapIndex];
        }
    }

    private void Highlight(Button[] buttons, int selected)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null)
            {
                continue;
            }
            Image image = buttons[i].GetComponent<Image>();
            if (image != null)
            {
                image.color = i == selected ? selectedColor : normalColor;
            }
        }
    }
}
