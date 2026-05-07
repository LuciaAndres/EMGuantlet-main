using Unity.Netcode;  //para que funcione
//using Unity.Netcode.Components;  
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public class PlayerController : CharController
{
    [Header("Multiplayer Stats (0:Green, 1:Purple, 2:Red, 3:Yellow)")]
    [SerializeField] private PlayerStats[] availableStats;

    // Variable de red para nuestro color
    private NetworkVariable<int> netCharacterIndex = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    //mov
    private NetworkVariable<Vector2> netMovement = new NetworkVariable<Vector2>(Vector2.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

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

    /* public override void OnNetworkSpawn()
     {
         base.OnNetworkSpawn();

         // todos se van a la scena
         StartCoroutine(WaitAndTeleportToSpawn());

         if (IsOwner)
         {
             //DEBUG 
             if (GameManager.Instance != null && GameManager.Instance.LocalPlayerController != null)
             {
                 Debug.LogError("El objeto [" + gameObject.name + "] ES EL CULPABLE.");
                 return;
             }

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

             // controles para cada ubno
             controls = new PlayerControls();
             controls.Enable();
             controls.Player.Move.performed += ctx => movement = ctx.ReadValue<Vector2>();
             controls.Player.Move.canceled += _ => movement = Vector2.zero;
             controls.Player.Attack.performed += onAttack;

             UniqueEntity uniqueEntity = GetComponent<UniqueEntity>();
             if (GameManager.Instance != null)
                 GameManager.Instance.RegisterLocalPlayer(this, uniqueEntity);
         }

         netCharacterIndex.OnValueChanged += (oldVal, newVal) => ApplyNetworkedStats(newVal);
         if (netCharacterIndex.Value != -1)
         {
             ApplyNetworkedStats(netCharacterIndex.Value);
         }
     }
    */
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Debug.Log($"Nace jugador {IsOwner}");

        if (IsServer)
        {
            StartCoroutine(HostAssignSpawnPositions());
        }

        if (IsOwner)
        {
            // debig para el bug
            if (GameManager.Instance != null && GameManager.Instance.LocalPlayerController != null)
            {
                Debug.LogError("🚨 ¡CAZADO! Intento de clon bloqueado.");
                return;
            }

            //color
            int myIndex = 0; //verde
            if (GameManager.Instance != null && GameManager.Instance.SelectedCharacterStats != null)
            {
                Debug.Log($"[COLOR] {GameManager.Instance.SelectedCharacterStats.characterName}");

                if (availableStats != null)
                {
                    for (int i = 0; i < availableStats.Length; i++)
                    {
                        if (availableStats[i] != null && availableStats[i].characterName == GameManager.Instance.SelectedCharacterStats.characterName)
                        {
                            myIndex = i;
                            Debug.Log($"[COLOR] slot {i}");
                            break;
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning("Verde por defecto");
            }

            // se guarda
            netCharacterIndex.Value = myIndex;

            //cada un se lo aplica
            ApplyNetworkedStats(myIndex);

            // contorles
            controls = new PlayerControls();
            controls.Enable();
            controls.Player.Move.performed += ctx => movement = ctx.ReadValue<Vector2>();
            controls.Player.Move.canceled += _ => movement = Vector2.zero;
            controls.Player.Attack.performed += onAttack;

            UniqueEntity uniqueEntity = GetComponent<UniqueEntity>();
            if (GameManager.Instance != null)
                GameManager.Instance.RegisterLocalPlayer(this, uniqueEntity);
        }

        // subscripcion al cambio de color
        netCharacterIndex.OnValueChanged += (oldVal, newVal) => {
            Debug.Log($"[COLOR] jugador red cambio de color al {newVal}");
            ApplyNetworkedStats(newVal);
        };

        // se aplica al entrar trde
        if (!IsOwner && netCharacterIndex.Value != -1)
        {
            ApplyNetworkedStats(netCharacterIndex.Value);
        }
    }

    // spawn en esquinas
    private System.Collections.IEnumerator HostAssignSpawnPositions()
    {
        LevelGenerator generator = FindFirstObjectByType<LevelGenerator>();

        // espera activa del mapa 
        while (generator == null || generator.OuterRingBounds.size.x == 0)
        {
            yield return null;
            generator = FindFirstObjectByType<LevelGenerator>();
        }

        Bounds bounds = generator.OuterRingBounds;
        Vector3 finalPos = Vector3.zero;

        
        float offset = 15.0f; // por defecto 1º mapa
        if (GameManager.Instance != null && GameManager.Instance.SelectedMapConfig != null)
        {
            // el grosos del bosue del mapa seleccionado
            int forestWidth = GameManager.Instance.SelectedMapConfig.outerForest.ringWidth;

            // Cálculo exacto:
            // Muro exterior + ancho del bosque + muro separador +2 pasosd e seguridad
            offset = 1f + forestWidth + 1f + 2f;
        }
        //cada uno a una esquina
        if (OwnerClientId == 0)
        {
            finalPos = new Vector3(bounds.min.x + offset, bounds.min.y + offset, 0);
        }
        else if (OwnerClientId == 1)
        {
            finalPos = new Vector3(bounds.max.x - offset, bounds.max.y - offset, 0);
        }
        else if (OwnerClientId == 2)
        {
            finalPos = new Vector3(bounds.min.x + offset, bounds.max.y - offset, 0);
        }
        else
        {
            finalPos = new Vector3(bounds.max.x - offset, bounds.min.y + offset, 0);
        }

        finalPos.z = 0f;

        PlacePlayerClientRpc(finalPos);
    }

    [ClientRpc]
    private void PlacePlayerClientRpc(Vector3 assignedPosition)
    {
        //hoat
        if (IsOwner)
        {
            //esqiona
            transform.position = assignedPosition;

            //antes de hacerse visible se lee el color
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

            //lo mandamos
            netCharacterIndex.Value = myIndex;
            ApplyNetworkedStats(myIndex);
            Debug.Log($"[LLEGADA AL MAPA] Color: {myIndex}");

            // sibiles
            if (spriteRenderer != null) spriteRenderer.enabled = true;
            if (characterCollider != null) characterCollider.enabled = true;
        }
        if (spriteRenderer != null) spriteRenderer.enabled = true;
        if (characterCollider != null) characterCollider.enabled = true;
    }



    // en funcion del index del jugador aplica las stats al mismo
    private void ApplyNetworkedStats(int index)
    {
        if (availableStats == null || availableStats.Length == 0)
        {
            Debug.LogError("error en los stats cabezon");
            return;
        }

        if (index >= 0 && index < availableStats.Length)
        {
            ApplyCharacterStats(availableStats[index]);
            Debug.Log($"jugador : {availableStats[index].characterName}");
        }
        else
        {
            Debug.LogError($" ERROR: {index}");
        }
    }
    /*
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

        // se desativa el networktransform para no joder el tp
        NetworkTransform netTransform = GetComponent<NetworkTransform>();
        if (netTransform != null) netTransform.enabled = false;

        // se separ el jugador del dueño
        Vector3 offset = Vector3.zero;
        if (OwnerClientId == 1) offset = new Vector3(1.5f, 0, 0);
        else if (OwnerClientId == 2) offset = new Vector3(-1.5f, 0, 0);
        else if (OwnerClientId == 3) offset = new Vector3(0, 1.5f, 0);

        transform.position = generator.ServerSpawnPosition.Value + offset;

        yield return new WaitForFixedUpdate();

        // se activa otra vez para ya jugar normal
        if (characterCollider != null) characterCollider.enabled = true;
        if (netTransform != null) netTransform.enabled = true;
    }
    */
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
        // si somos el dueño metemos los daots en el netwoek variable 
        if (IsOwner)
        {
            netMovement.Value = movement;
        }

        // todos axtualizan el estado de todos
        Vector2 currentNetMovement = netMovement.Value;

        animator.SetFloat("speed", currentNetMovement.sqrMagnitude);

        if (currentNetMovement.sqrMagnitude > 0.01f)
        {
            float angle = Mathf.Atan2(currentNetMovement.y, currentNetMovement.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
        }

        checkDeath();
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
        if (isDead) return;
        isDead = true;
        health = 0;
        animator.SetBool("IsDead", true);

        if (IsOwner)
        {
            GameEvents.PlayerDied();

            // el servidor borra nuestro personaje
            DespawnPlayerServerRpc();

            // gameover personal
            GameManager.Instance?.TriggerGameOver();
        }
    }

    [ServerRpc]
    private void DespawnPlayerServerRpc()
    {
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn(true); // propaga la destruccion del personaje
        }
    }

    /// <summary>
    /// Aplica daño al jugador y notifica el cambio de salud al HUD.
    /// </summary>
    public override void TakeDamage(int amount, Vector2 knockbackDir)
    {
        base.TakeDamage(amount, knockbackDir);
        GameEvents.HealthChanged(health);
    }

    [ClientRpc]
    private void TakeDamageClientRpc(int amount, Vector2 knockbackDir)
    {
        base.TakeDamage(amount, knockbackDir);

        if (IsOwner) // Solo host actualza
        {
            GameEvents.HealthChanged(health);
        }
    }
    public void TakeDamageServerAuthoritative(int amount, Vector2 knockbackDir)
    {
        if (IsServer)
        {
            TakeDamageClientRpc(amount, knockbackDir);
        }
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
            moveSpeed *= playerStats.speedBonus;
            damageToEnemy = playerStats.attackDamage;
            attackCooldown = playerStats.attackCooldown;

            
            if (playerStats.animatorController != null && animator != null)
            {
                animator.runtimeAnimatorController = playerStats.animatorController;
            }
        }
    }

    /// <summary>
    /// Verifica si la salud ha llegado a cero y ejecuta la muerte una sola vez.
    /// </summary>
    private void checkDeath()
    {
        if (health <= 0 && !isDead) Die();
    }

    private void onAttack(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (IsAttacking) return;

        IsAttacking = true;

        // avisa 
        SetAttackStateServerRpc(true);

        Invoke(nameof(endAttack), attackCooldown);
    }

    private void endAttack()
    {
        IsAttacking = false;

        //avisamos que ya no atacamos
        SetAttackStateServerRpc(false);
    }

    [ServerRpc]
    private void SetAttackStateServerRpc(bool state)
    {
        IsAttacking = state;

        // si atacamos avisamoa a todos
        if (state)
        {
            PlayAttackAnimationClientRpc();
        }
    }

    // se reproduce
    [ClientRpc]
    private void PlayAttackAnimationClientRpc()
    {
        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }
    }

    [ServerRpc]
    public void TriggerVictoryServerRpc()
    {
        Debug.Log("Victoria....");
       
        NetworkManager.Singleton.SceneManager.LoadScene(SceneNames.VictoryScene, LoadSceneMode.Single);
    }

    
}