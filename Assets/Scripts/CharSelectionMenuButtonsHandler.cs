using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode; 
using UnityEngine.UI;
using TMPro;

public class CharSelectionMenuButtonsHandler : MonoBehaviour
{
    [Header("Character Stats Assets")]
    [SerializeField] private PlayerStats greenCharacterStats;
    [SerializeField] private PlayerStats purpleCharacterStats;
    [SerializeField] private PlayerStats redCharacterStats;
    [SerializeField] private PlayerStats yellowCharacterStats;

    [Header("Multiplayer UI")]
    [SerializeField] private Button startGameButton;

    [SerializeField] private TextMeshProUGUI[] playerSlotsTexts;


    [Header("Buttons")]
    [SerializeField] private Button greenButton;
    [SerializeField] private Button purpleButton;
    [SerializeField] private Button redButton;
    [SerializeField] private Button yellowButton;

    private void Start()
    {
        // Solo ve el host el boton de start
        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer);
        }

        // subscricion de eventos
        if (NetworkLobbyManager.Instance != null)
        {
            NetworkLobbyManager.Instance.LobbyPlayers.OnListChanged += HandleLobbyListChanged;
            UpdateLobbyUI(); // Pintamos los textos nada más entrar
        }
    }

    //para desubscribirnos
    private void OnDestroy()
    {
        if (NetworkLobbyManager.Instance != null)
        {
            NetworkLobbyManager.Instance.LobbyPlayers.OnListChanged -= HandleLobbyListChanged;
        }
    }

    private void HandleLobbyListChanged(NetworkListEvent<PlayerLobbyState> changeEvent)
    {
        UpdateLobbyUI();
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
        // solo el host puede cambiar de escnea
        if (NetworkManager.Singleton.IsServer)
        {
            // solo se puede inciiar si hay mas de dos jugadores en la lista de los jugadreos
            if (NetworkLobbyManager.Instance != null && NetworkLobbyManager.Instance.LobbyPlayers.Count < 2)
            {
                Debug.LogWarning("[Lobby] NONONONONNO");
                return; 
            }

            // se reinician los datos de partidas anterir para evitar bugd
            GameManager.Instance?.ResetGameData();

            // se lleva a todos
            NetworkManager.Singleton.SceneManager.LoadScene(SceneNames.PlaygroundLevel, LoadSceneMode.Single);
        }
    }

    private void UpdateLobbyUI()
    {
        if (playerSlotsTexts == null || playerSlotsTexts.Length == 0) return;

        var lobbyPlayers = NetworkLobbyManager.Instance.LobbyPlayers;

        // si no hay suficientes jugadores no puede inciiar partida
        if (startGameButton != null && NetworkManager.Singleton.IsServer)
        {
            startGameButton.interactable = lobbyPlayers.Count >= 2;
        }

        // banderas de colores
        bool greenTaken = false;
        bool purpleTaken = false;
        bool redTaken = false;
        bool yellowTaken = false;

        // color de cada jugadror y referencia
        for (int i = 0; i < playerSlotsTexts.Length; i++)
        {
            if (i < lobbyPlayers.Count)
            {
                PlayerLobbyState state = lobbyPlayers[i];
                string colorName = GetColorName(state.CharacterIndex);
                playerSlotsTexts[i].text = $"Jugador {i + 1}: {colorName}";

                // bloqueo del color
                if (state.CharacterIndex == 0) greenTaken = true;
                if (state.CharacterIndex == 1) purpleTaken = true;
                if (state.CharacterIndex == 2) redTaken = true;
                if (state.CharacterIndex == 3) yellowTaken = true;
            }
            else
            {
                playerSlotsTexts[i].text = "Esperando jugador...";
            }
        }

        // los botones se bloquean
        if (greenButton != null) greenButton.interactable = !greenTaken;
        if (purpleButton != null) purpleButton.interactable = !purpleTaken;
        if (redButton != null) redButton.interactable = !redTaken;
        if (yellowButton != null) yellowButton.interactable = !yellowTaken;
    }

    private string GetColorName(int index)
    {
        return (index switch
        {
            0 => "Verde",
            1 => "Morado",
            2 => "Rojo",
            3 => "Amarillo",
            -1 => "Eligiendo..." // - 1 es que no pulsó nad
        });
    }
}