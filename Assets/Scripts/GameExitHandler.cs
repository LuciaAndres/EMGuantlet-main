using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using TMPro;

public class GameExitHandler : MonoBehaviour
{
    [SerializeField] private GameObject disconnectPanel;
    [SerializeField] private TextMeshProUGUI statusText;

    private void Start()
    {
        // para detectar el evento nos subsctibimos
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnectDetected;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnectDetected;
        }
    }

    private void OnDisconnectDetected(ulong clientId)
    {
        // Host 
        if (clientId == NetworkManager.ServerClientId)
        {
            ShowMessage("Host desconectado. Pulsa para volver al menú.");
        }
        // Cliente
        else if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            ShowMessage("Te has desconectado de la partida.");
        }
    }

    public void OnExitButtonClicked()
    {
        string mensaje = "";
        if (NetworkManager.Singleton.IsServer)
        {
             mensaje = "Has cerrado la partida porque eras el host";
        }
        else
        {
            mensaje = "Te has desconectado.";
        }
            

        ShowMessage(mensaje);
        NetworkManager.Singleton.Shutdown();
    }

    private void ShowMessage(string msg)
    {
        if (disconnectPanel != null) disconnectPanel.SetActive(true);
        if (statusText != null) statusText.text = msg;
    }

    public void BackToMainMenu()
    {
        if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene("MainMenu");
    }
}