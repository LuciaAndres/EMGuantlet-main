using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;  //para que funcione
using Unity.Netcode.Components; //para apagar la red

public class PlayerController : CharController
{
    [Header("Multiplayer Stats (0:Green, 1:Purple, 2:Red, 3:Yellow)")]
    [SerializeField] private PlayerStats[] availableStats;

    // Variable de red para nuestro color
    private NetworkVariable<int> netCharacterIndex = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    protected int damageToEnemy;
    protected float attackCooldown;
    private PlayerControls controls;

    public bool IsAttacking { get; private set; } = false;
    public int DamageToEnemy => damageToEnemy;

    private SpriteRenderer spriteRenderer;

    /// <summary>
    /// Inicializa controles de entrada y registra el jugador local en el gestor global.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // se apagan fisicas para no chocar cuando se crea todo
        if (characterCollider != null) characterCollider.enabled = false;

        // invisible para el bug del menu
        if (spriteRenderer != null) spriteRenderer.enabled = false;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            controls = new PlayerControls();
            controls.Enable();
            controls.Player.Move.performed += ctx => movement = ctx.ReadValue<Vector2>();
            controls.Player.Move.canceled += _ => movement = Vector2.zero;
            controls.Player.Attack.performed += onAttack;

            UniqueEntity uniqueEntity = GetComponent<UniqueEntity>();
            if (GameManager.Instance != null)
                GameManager.Instance.RegisterLocalPlayer(this, uniqueEntity);

            // tp al inicio, lo calcula el dusñeo
            StartCoroutine(WaitAndTeleportToSpawn());
        }

        // subscripcion al cambio de color
        netCharacterIndex.OnValueChanged += (oldVal, newVal) => ApplyNetworkedStats(newVal);
        if (netCharacterIndex.Value != -1)
        {
            ApplyNetworkedStats(netCharacterIndex.Value);
        }
    }

    // en funcion del index del jugador aplica las stats al mismo
    private void ApplyNetworkedStats(int index)
    {
        if (availableStats != null && index >= 0 && index < availableStats.Length)
        {
            ApplyCharacterStats(availableStats[index]);
        }
    }

    private System.Collections.IEnumerator WaitAndTeleportToSpawn()
    {
        LevelGenerator generator = FindFirstObjectByType<LevelGenerator>();

        // eimre qe no estemos en la partida no hace nada
        while (generator == null || generator.ServerSpawnPosition.Value == Vector3.zero)
        {
            yield return null;
            generator = FindFirstObjectByType<LevelGenerator>();
        }

        // lee el color
        int myIndex = 0;
        if (GameManager.Instance != null && GameManager.Instance.SelectedCharacterStats != null && availableStats != null)
        {
            for (int i = 0; i < availableStats.Length; i++)
            {
                if (availableStats[i] != null && availableStats[i].characterName == GameManager.Instance.SelectedCharacterStats.characterName)
                {
                    myIndex = i;
                    break;
                }
            }
        }
        netCharacterIndex.Value = myIndex;

        // se apaga la red para no joder el tp
        NetworkTransform netTransform = GetComponent<NetworkTransform>();
        if (netTransform != null) netTransform.enabled = false;

        // se separ el jugador del dueño
        Vector3 offset = Vector3.zero;
        if (OwnerClientId == 1) offset = new Vector3(1.5f, 0, 0);
        else if (OwnerClientId == 2) offset = new Vector3(-1.5f, 0, 0);
        else if (OwnerClientId == 3) offset = new Vector3(0, 1.5f, 0);

        transform.position = generator.ServerSpawnPosition.Value + offset;

        yield return new WaitForFixedUpdate();

        // se enciende otra vez para ya jugar normal
        if (characterCollider != null) characterCollider.enabled = true;
        if (netTransform != null) netTransform.enabled = true;
    }

    protected override void Move()
    {
        if (!IsOwner) return;
        base.Move();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (controls != null)
        {
            controls.Player.Attack.performed -= onAttack;
            controls.Disable();
        }
    }

    /// <summary>
    /// Inicializa estado del jugador y notifica los valores iniciales al HUD.
    /// </summary>
    protected override void Start()
    {
        base.Start();
        if (IsOwner)
        {
            GameEvents.HealthChanged(health);
            GameEvents.KeysChanged();
            GameEvents.DiamondsChanged();
        }
        IsAttacking = false;
    }

    /// <summary>
    /// Actualiza animación, orientación y estado de vida en cada frame.
    /// </summary>
    protected override void Update()
    {
        animator.SetFloat("speed", movement.sqrMagnitude);

        if (movement.sqrMagnitude > 0.01f)
        {
            float angle = Mathf.Atan2(movement.y, movement.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
        }

        checkDeath();

        // si eres un sprite que ers invisible y hay un level generator en la escena ya vuelves a ser visible
        if (spriteRenderer != null && !spriteRenderer.enabled && FindFirstObjectByType<LevelGenerator>() != null)
        {
            spriteRenderer.enabled = true;
        }
    }

    /*
     *  /// <summary>
    /// Activa el mapa de controles y suscribe la acción de ataque.
    /// </summary>
    private void OnEnable()
    {
        controls.Enable();
        controls.Player.Attack.performed += onAttack;
    }

    /// <summary>
    /// Desuscribe la acción de ataque y desactiva el mapa de controles.
    /// </summary>
    private void OnDisable()
    {
        controls.Player.Attack.performed -= onAttack;
        controls.Disable();
    }

     */

    /// <summary>
    /// Gestiona la muerte del jugador y lanza el flujo de fin de partida.
    /// </summary>
    public override void Die()
    {
        base.Die();
        GameEvents.PlayerDied();
        GameManager.Instance?.TriggerGameOver();
    }

    /// <summary>
    /// Aplica daño al jugador y notifica el cambio de salud al HUD.
    /// </summary>
    public override void TakeDamage(int amount, Vector2 knockbackDir)
    {
        base.TakeDamage(amount, knockbackDir);
        GameEvents.HealthChanged(health);
    }

    /// <summary>
    /// Aplica un conjunto de estadísticas de personaje y recarga sus valores activos.
    /// </summary>
    public void ApplyCharacterStats(PlayerStats newStats)
    {
        if (newStats == null) return;
        stats = newStats;
        LoadStats();
    }

    /// <summary>
    /// Carga estadísticas del personaje seleccionado y aplica valores de combate y movimiento.
    /// </summary>
    protected override void LoadStats()
    {
        base.LoadStats();
        PlayerStats playerStats = stats as PlayerStats;

        if (playerStats != null)
        {
            // Aplica el bonus de velocidad del jugador

            moveSpeed *= playerStats.speedBonus;

            // Carga stats específicas del jugador
            damageToEnemy = playerStats.attackDamage;
            attackCooldown = playerStats.attackCooldown;
        }
        else            
        // Valores por defecto si no hay PlayerStats

        {
            damageToEnemy = 50;
            attackCooldown = 0.5f;
            moveSpeed *= 1.25f;
        }
    }

    /// <summary>
    /// Verifica si la salud ha llegado a cero y ejecuta la muerte una sola vez.
    /// </summary>
    private void checkDeath()
    {
        if (health <= 0 && !isDead) Die();
    }

    private void onAttack(InputAction.CallbackContext context)
    {
        animator.SetTrigger("Attack");
        IsAttacking = true;
        Invoke(nameof(endAttack), attackCooldown);
    }

    private void endAttack()
    {
        IsAttacking = false;
    }
}