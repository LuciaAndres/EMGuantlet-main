using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode; 
using UnityEngine.UI; 

public class CharSelectionMenuButtonsHandler : MonoBehaviour
{
    [Header("Character Stats Assets")]
    [SerializeField] private PlayerStats greenCharacterStats;
    [SerializeField] private PlayerStats purpleCharacterStats;
    [SerializeField] private PlayerStats redCharacterStats;
    [SerializeField] private PlayerStats yellowCharacterStats;

    [Header("Multiplayer UI")]
    [SerializeField] private Button startGameButton; 

    private void Start()
    {
        // SOLO VISIBLE SI ERES EL HOST
        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(NetworkManager.Singleton.IsServer);
        }
    }

    public void OnBackButtonClicked()
    {
        //CIERRA LA CONEXION SI VUELVE ARAS EN LA SELECCION
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }
        SceneManager.LoadScene(SceneNames.MainMenu);
    }

    //  Los personajes son estos: 0=Green, 1=Purple, 2=Red, 3=Yellow
    public void OnGreenButtonClicked() { SendSelectionToServer(0, greenCharacterStats); }
    public void OnPurpleButtonClicked() { SendSelectionToServer(1, purpleCharacterStats); }
    public void OnRedButtonClicked() { SendSelectionToServer(2, redCharacterStats); }
    public void OnYellowButtonClicked() { SendSelectionToServer(3, yellowCharacterStats); }

    private void SendSelectionToServer(int characterIndex, PlayerStats localStats)
    {
        if (NetworkLobbyManager.Instance == null) return;

        // 1. AVISAMOS DE LA SELECCION
        NetworkLobbyManager.Instance.SelectCharacterServerRpc(characterIndex);

        // 2. SE GUARDA LOCALMENTE PARA CUANDO ESTEMOS EN EL JUEGO
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SelectedCharacterStats = localStats;
            Debug.Log($"[Lobby] Has seleccionado a {localStats.characterName}.");
        }
    }

    public void OnStartGameClicked()
    {
       // OTRA COMPROBACION, SOLO EL HOST PUEDE CAMBIAR DE ESCENA
        if (NetworkManager.Singleton.IsServer)
        {
            // REINICIAN LOS DATOS DE PARTIDAS ANTERIORES SI LOS HUBIERA
            GameManager.Instance?.ResetGameData();

            // ES EL DE NETCODE PARA LLEVAR A TODOS A LA NUEVA ESCENA, NO AL HOST SOLO
            NetworkManager.Singleton.SceneManager.LoadScene(SceneNames.PlaygroundLevel, LoadSceneMode.Single);
        }
    }
}