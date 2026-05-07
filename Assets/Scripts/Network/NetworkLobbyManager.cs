using System;
using Unity.Netcode;
using UnityEngine;


public struct PlayerLobbyState : INetworkSerializable, IEquatable<PlayerLobbyState> //ES LO QUE SE ALMACENA DE CADA JUGADOR
{
    public ulong ClientId;       //ID UNICO (LO DA NETCODE) DE CADA JUGADOR
    public int CharacterIndex;   // 0=GREEN, 1=PURPLE, 2=RED, 3=YELLOW
    public bool IsReady;         // PARA VER SI YA ELIGIÓ Y SE PUEDE PROSEGUIR

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref CharacterIndex);
        serializer.SerializeValue(ref IsReady);
    }

    public bool Equals(PlayerLobbyState other)
    {
        return ClientId == other.ClientId && CharacterIndex == other.CharacterIndex && IsReady == other.IsReady;
    }
}

// LOBBY MANAGER
public class NetworkLobbyManager : NetworkBehaviour
{
    public static NetworkLobbyManager Instance { get; private set; }

    // SINCRONIZA AUTOMATICAMENTE (AL SER NETWORK LIST) CON TODOS LOS CLIENTEZ
    public NetworkList<PlayerLobbyState> LobbyPlayers;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        LobbyPlayers = new NetworkList<PlayerLobbyState>();
    }

    public override void OnNetworkSpawn()
    {
        // EN FUNCION DE LO QUE SEAMOS (HOST / CLIENT) VAMOS A UN SITIO U OTRO
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;

            AddPlayerToList(NetworkManager.Singleton.LocalClientId);
        }

        // SUBSCRIBIMOS EL EVENTO PARA QUE TODOS VEAN EL CAMBIO REFLEJADO
        LobbyPlayers.OnListChanged += HandleLobbyPlayersStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
        }
        LobbyPlayers.OnListChanged -= HandleLobbyPlayersStateChanged;
    }


    private void HandleClientConnected(ulong clientId)
    {
        AddPlayerToList(clientId);
    }

    private void HandleClientDisconnect(ulong clientId)
    {
        for (int i = 0; i < LobbyPlayers.Count; i++)
        {
            if (LobbyPlayers[i].ClientId == clientId)
            {
                LobbyPlayers.RemoveAt(i);
                break;
            }
        }
    }

    private void AddPlayerToList(ulong clientId)
    {
        LobbyPlayers.Add(new PlayerLobbyState
        {
            ClientId = clientId,
            CharacterIndex = -1, // -1 ES QUE NO HA ELEGIDO
            IsReady = false
        });
    }


    // SOLO SE EJECUTA EN EL SERVIDOR (AL LLEVAR EL RCP)
    [ServerRpc]
    public void SelectCharacterServerRpc(int characterIndex, ServerRpcParams serverRpcParams = default)
    {
        ulong senderClientId = serverRpcParams.Receive.SenderClientId;

        for (int i = 0; i < LobbyPlayers.Count; i++)
        {
            if (LobbyPlayers[i].ClientId == senderClientId)
            {
                // SE ACTUALIZA ELESTADO DEL JUGADOR EN LA LISTA
                LobbyPlayers[i] = new PlayerLobbyState
                {
                    ClientId = senderClientId,
                    CharacterIndex = characterIndex,
                    IsReady = true
                };
                break;
            }
        }
    }


    private void HandleLobbyPlayersStateChanged(NetworkListEvent<PlayerLobbyState> changeEvent)
    {
        Debug.Log($"La lista del lobby ha cambiado. Jugadores conectados: {LobbyPlayers.Count}");
    }
}