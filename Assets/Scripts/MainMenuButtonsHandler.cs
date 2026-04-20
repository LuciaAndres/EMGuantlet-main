using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode; 
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MainMenuButtonsHandler : MonoBehaviour
{
    [Header("Map Configs disponibles")]
    [SerializeField] private MapConfig[] availableMaps;

    [Header("UI")]
    [SerializeField] private TMP_Dropdown mapsDropdown;

    private void Start() { initializeMapDropdown(); }
    private void OnDestroy() { if (mapsDropdown != null) mapsDropdown.onValueChanged.RemoveListener(onMapDropdownChanged); }

    //BOTONES MULTIJUGADOR

    public void OnHostButtonClicked()
    {
        if (GameManager.Instance?.SelectedMapConfig == null)
        {
            Debug.LogWarning("[MainMenu] No hay mapa seleccionado.");
            return;
        }

        // 1. SE ENCIENDE LA RED COMO HOST
        NetworkManager.Singleton.StartHost();

        // 2.EL HOST MANDA Y MANDA CARGAR LA ESCENA PARA TODOS
        NetworkManager.Singleton.SceneManager.LoadScene(SceneNames.CharSelection, LoadSceneMode.Single);
    }

    public void OnClientButtonClicked()
    {
        // 1. ENCIENCE LA RED COMO CLIENTE
        NetworkManager.Singleton.StartClient();

    }


    public void OnOptionsButtonClicked() { Debug.Log("Options button pressed"); }
    public void OnExitButtonClicked()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void initializeMapDropdown()
    {
        if (mapsDropdown == null || availableMaps == null || availableMaps.Length == 0) return;
        mapsDropdown.ClearOptions();
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();
        foreach (MapConfig map in availableMaps) options.Add(new TMP_Dropdown.OptionData(map != null ? map.mapName : "Sin nombre"));
        mapsDropdown.AddOptions(options);
        mapsDropdown.value = 0;
        mapsDropdown.RefreshShownValue();
        mapsDropdown.onValueChanged.AddListener(onMapDropdownChanged);
        applySelectedMap(0);
    }

    private void onMapDropdownChanged(int index) { applySelectedMap(index); }
    private void applySelectedMap(int index)
    {
        if (availableMaps == null || index < 0 || index >= availableMaps.Length) return;
        if (GameManager.Instance != null) GameManager.Instance.SelectedMapConfig = availableMaps[index];
    }
}