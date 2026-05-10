using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(UniqueEntity))]
public class DoorController : NetworkBehaviour
{
    [SerializeField] private Sprite openDoorSprite;


    private NetworkVariable<bool> nOpen = new NetworkVariable<bool>(false);

    private Collider2D triggerCollider;
    private Collider2D blockingCollider;
    private SpriteRenderer spriteRenderer;
    private UniqueEntity uniqueEntity;

    public string EntityId => uniqueEntity?.EntityId ?? "UNKNOWN";
    public EntityType EntityType => uniqueEntity?.Type ?? EntityType.Interactive_Door;

    public bool IsOpen => nOpen.Value;

    private void Awake()
    {
        uniqueEntity = GetComponent<UniqueEntity>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        cacheColliders();
    }

    public override void OnNetworkSpawn()
    {
        nOpen.OnValueChanged += OnDoorStateChanged;

        if (nOpen.Value)
        {
            ApplyOpenVisuals();
        }
    }

    public override void OnNetworkDespawn()
    {
        nOpen.OnValueChanged -= OnDoorStateChanged;
    }


    private void OnDoorStateChanged(bool previousValue, bool newValue)
    {
        if (newValue) ApplyOpenVisuals();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (nOpen.Value || !other.CompareTag("Player")) return;
        if (!other.TryGetComponent(out PlayerController pc)) return;

        if (pc.IsOwner && GameManager.Instance != null)
        {
            if (GameManager.Instance.GetKeys() > 0)
            {
                pc.RequestOpenDoorServerRpc(GetComponent<NetworkObject>());
            }
        }
    }

    public void OpenDoorServer()
    {
        if (!IsServer) return;

        nOpen.Value = true;
    }

    private void ApplyOpenVisuals()
    {
        if (openDoorSprite != null && spriteRenderer != null)
        {
            spriteRenderer.sprite = openDoorSprite;
        }

        if (blockingCollider != null)
        {
            blockingCollider.enabled = false;
        }
    }

    private void cacheColliders()
    {
        Collider2D[] colliders = GetComponents<Collider2D>();
        foreach (Collider2D col in colliders)
        {
            if (col.isTrigger) triggerCollider = col;
            else blockingCollider = col;
        }
    }
}
