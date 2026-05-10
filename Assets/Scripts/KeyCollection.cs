using UnityEngine;
using Unity.Netcode;
[RequireComponent(typeof(UniqueEntity))]
public class KeyCollection : NetworkBehaviour
{
    [SerializeField] private string playerTag = "Player";

    private UniqueEntity uniqueEntity;

    public string EntityId => uniqueEntity?.EntityId ?? "UNKNOWN";
    public EntityType EntityType => uniqueEntity?.Type ?? EntityType.Pickup_Key;

    [HideInInspector]
    public bool cogida = false;

    /// <summary>
    /// Inicializa la referencia de entidad única y valida el tipo configurado.
    /// </summary>
    private void Awake()
    {
        uniqueEntity = GetComponent<UniqueEntity>();


        if (uniqueEntity != null && uniqueEntity.Type != EntityType.Pickup_Key)
        {
            Debug.LogWarning($"[KeyCollection] {gameObject.name} tiene tipo {uniqueEntity.Type} en lugar de Pickup_Key");
        }
    }

    /// <summary>
    /// Detecta la colisión con el jugador e intenta recoger la llave.
    /// </summary>
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!IsHost) return;
        if (!IsServer) return;
       
        if (!collision.gameObject.CompareTag(playerTag)) return;
        PlayerController player = collision.gameObject.GetComponent<PlayerController>();
        if (player == null) return;
        if (GameManager.Instance == null) return;
        if (cogida) return;
        cogida = true;

        player.GivePlayerKeyAutoritative();


        GetComponent<NetworkObject>().Despawn(true);

        cogida = false;

        
    }
}
