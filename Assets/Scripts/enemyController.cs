using Unity.Netcode;
using UnityEngine;

public abstract class EnemyController : CharController
{
    protected int damageToPlayer;
    protected GameObject[] dropPrefabs;

    /// <summary>
    /// Inicializa la configuración base del enemigo heredada del controlador de personaje.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
    }

    /// <summary>
    /// Gestiona la interacción continua con el jugador.
    /// </summary>
    protected virtual void OnCollisionStay2D(Collision2D collision)
    {
        // Solo el servidor gestiona 
        if (!IsServer) return;
        if (!collision.gameObject.CompareTag("Player")) return;

        PlayerController player = collision.gameObject.GetComponent<PlayerController>();
        if (player == null) return;

        // Si el jugador NO está atacando  el enemigo le hace daño
        if (!player.IsAttacking)
        {
            Vector2 knockbackDir = (player.transform.position - transform.position).normalized;
            player.TakeDamageServerAuthoritative(damageToPlayer, knockbackDir);
        }
    }

    /// <summary>
    /// Conprobacion
    /// </summary>
    public override void TakeDamage(int amount, Vector2 knockbackDir)
    {
        base.TakeDamage(amount, knockbackDir);

        // Si somos el servidor y la vida baja de 0 se jodio
        if (IsServer)
        {
            checkDeath();
        }
    }

    /// <summary>
    /// Carga estadísticas de combate y referencias de drops desde EnemyStats.
    /// </summary>
    protected override void LoadStats()
    {
        base.LoadStats();

        EnemyStats enemyStats = stats as EnemyStats;

        if (enemyStats != null)
        {
            moveSpeed *= enemyStats.speedPenalty;
            damageToPlayer = enemyStats.attackDamage;
            dropPrefabs = enemyStats.dropPrefabs;
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] No tiene EnemyStats asignado. Usando valores por defecto.");
            damageToPlayer = 1;
            moveSpeed *= 0.75f;
        }
    }

    /// <summary>
    /// Marca y procesa la muerte del enemigo cuando su vida llega a cero.
    /// </summary>
    public override void Die()
    {
        if (isDead) return; // no suma dos veces
        base.Die(); 

        // reproducir muerte
        PlayDeathAnimationClientRpc();

        // aumetnar contador
        if (IsServer)
        {
            LevelGenerator generator = FindFirstObjectByType<LevelGenerator>();
            if (generator != null)
            {
                generator.GlobalEnemiesKilled.Value++;
            }
        }

        spawnDrops();
    }

    [ClientRpc]
    private void PlayDeathAnimationClientRpc()
    {
        // si no somos el server lo hacemos
        if (!IsServer)
        {
            isDead = true;
            animator.SetBool("IsDead", true);
            if (characterCollider != null) characterCollider.enabled = false;
        }
    }

    /// <summary>
    /// Verifica si el enemigo debe morir y programa su destrucción en escena.
    /// </summary>
    protected void checkDeath()
    {
        if (health <= 0 && !isDead)
        {
            Die();
            // waiteio
            StartCoroutine(DespawnAfterDelay(1.2f));
        }
    }

    private System.Collections.IEnumerator DespawnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        // se borrad e todos lados
        if (IsServer && GetComponent<NetworkObject>().IsSpawned)
        {
            GetComponent<NetworkObject>().Despawn(true);
        }
    }

    /// <summary>
    /// Genera los drops del enemigo usando la configuración activa del mapa.
    /// </summary>
    protected virtual void spawnDrops()
    {
        if (dropPrefabs == null || dropPrefabs.Length == 0)
        {
            Debug.LogWarning($"[{gameObject.name}] No tiene dropPrefabs configurados.");
            return;
        }

        EnemyDropConfig dropCfg = getDropConfig();

        if (dropCfg == null)
        {
            Debug.LogWarning($"[{gameObject.name}] No hay MapConfig activo. No se spawnean drops.");
            return;
        }

        int dropCount = Random.Range(dropCfg.minDiamondDrops, dropCfg.maxDiamondDrops + 1);
        if (dropCount <= 0) return;

        float angleStep = 360f / dropCount;
        float startAngle = Random.Range(0f, 360f);

        for (int i = 0; i < dropCount; i++)
        {
            GameObject dropPrefab = dropPrefabs[0];

            if (dropPrefabs.Length > 1 && i == 0 && Random.value < dropCfg.keyDropChance)
                dropPrefab = dropPrefabs[1];

            if (dropPrefab != null)
            {
                float angle = startAngle + i * angleStep;
                Vector3 dropPosition = transform.position +
                    new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0f) * 0.5f;

                GameObject drop = Instantiate(dropPrefab, dropPosition, Quaternion.identity);
                UniqueEntity uniqueEntity = drop.GetComponent<UniqueEntity>();
                if (uniqueEntity != null) uniqueEntity.RegenerateIdOnSpawn();
                // pone las monedas en todos los clientes
                NetworkObject netObj = drop.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn(true);
                }
                
            }

        }
    }

    /// <summary>
    /// Obtiene la configuración de drops del mapa según el tipo de enemigo.
    /// </summary>
    protected virtual EnemyDropConfig getDropConfig()
    {
        MapConfig mapCfg = GameManager.Instance?.SelectedMapConfig;
        if (mapCfg == null) return null;

        if (stats is ChaseEnemyStats)
            return mapCfg.dragonDropConfig;

        if (stats is LemniscateEnemyStats)
            return mapCfg.goatDropConfig;

        return null;
    }
}