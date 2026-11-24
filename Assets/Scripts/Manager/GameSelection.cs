using UnityEngine;

// Holds what the player picked in the Garage (car + map). Remembered between sessions via PlayerPrefs.
// Index order must match GarageController and CarSpawner: cars 0 = Altima, 1 = GT-R; maps 0 = Map 1, 1 = Map 2.
public static class GameSelection
{
    public static readonly string[] CarNames = { "Altima", "GT-R" };
    public static readonly string[] MapNames = { "Map 1", "Map 2" };
    public static readonly string[] MapScenes = { "MainGame", "Map2" };

    private const string CarKey = "Garage_SelectedCar";
    private const string MapKey = "Garage_SelectedMap";

    public static int SelectedCarIndex
    {
        get { return Mathf.Clamp(PlayerPrefs.GetInt(CarKey, 0), 0, CarNames.Length - 1); }
        set { PlayerPrefs.SetInt(CarKey, Mathf.Clamp(value, 0, CarNames.Length - 1)); PlayerPrefs.Save(); }
    }

    public static int SelectedMapIndex
    {
        get { return Mathf.Clamp(PlayerPrefs.GetInt(MapKey, 0), 0, MapScenes.Length - 1); }
        set { PlayerPrefs.SetInt(MapKey, Mathf.Clamp(value, 0, MapScenes.Length - 1)); PlayerPrefs.Save(); }
    }

    public static string SelectedMapScene
    {
        get { return MapScenes[SelectedMapIndex]; }
    }
}
