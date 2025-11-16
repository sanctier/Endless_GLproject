using UnityEngine;
using System.Collections;

public class FlyingEnemy : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 3f;
    public float detectionRange = 6f; // how far it can "see" the player
    public float stopDistance = 0.2f; // distance considered "reached" the last player location

    [Header("Attack")]
    public float attackCooldown = 1.5f;
    public float attackRange = 1f; // immediate attack if player gets this close

    private Transform player;
    private Rigidbody2D rb;
    private Animator animator;
    [Header("Combat")]
    public LayerMask damageLayer; // set this to the same layer(s) player attack colliders use

    // last known player location that the flyer will move to and attack
    // (flyer no longer chases player) use trigger contact to start attacks
    private bool playerInContact = false;
    private float attackTimer = 0f;
    private bool isBusy = false;
    public float attackFallbackDuration = 1.0f; // safety fallback to clear attack
    [Header("Stun")]
    public float stunDuration = 1.5f; // how long to be stunned instead of dying
    private bool isStunned = false;

    [Header("Wander")]
    public Vector2 minBounds = new Vector2(-10f, -5f);
    public Vector2 maxBounds = new Vector2(10f, 5f);
    public float wanderIntervalMin = 1f;
    public float wanderIntervalMax = 3f;
    private Vector2 wanderTarget;
    private float wanderTimer = 0f;
    private Coroutine fallbackCoroutine = null;

    private bool facingRight = true;
    [Header("Animator")]
    // Make these configurable so they match whatever the Animator actually uses
    public string flightStateName = "Flight";
    public string[] attackStateNames = new string[] { "Attack1", "Attack2" };

    // cached hashes
    private int flightHash;
    private int[] attackHashes;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        wanderTarget = rb != null ? rb.position : (Vector2)transform.position;
        wanderTimer = Random.Range(wanderIntervalMin, wanderIntervalMax);

        // warn if required components are missing so the user can fix prefabs
        if (rb == null)
            Debug.LogWarning(name + ": Rigidbody2D not found on FlyingEnemy. Movement will fall back to Transform.");
        if (animator == null)
            Debug.LogWarning(name + ": Animator not found on FlyingEnemy. Animation control will be skipped.");

        // compute animator state hashes for faster and more robust comparisons
        flightHash = Animator.StringToHash(flightStateName);
        attackHashes = new int[attackStateNames.Length];
        for (int i = 0; i < attackStateNames.Length; i++)
            attackHashes[i] = Animator.StringToHash(attackStateNames[i]);
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        // Player contact used for the flyer to attack the player
        if (collision.CompareTag("Player"))
        {
            playerInContact = true;
            // attack immediately on touch
            if (attackTimer <= 0f && !isBusy)
                TryAttack();

            // If the player body hits this trigger while in their Attack animation, treat it as a hit.
            if (IsColliderPlayerAttacking(collision))
            {
                ApplyStun();
                return;
            }
        }

        // Accept hits from configured damage layer (matches GoblinEnemy behavior)
        if (((1 << collision.gameObject.layer) & damageLayer) != 0)
        {
            ApplyStun();
            return;
        }
    }

    void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            playerInContact = false;
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Accept hits from configured damage layer
        if (((1 << collision.collider.gameObject.layer) & damageLayer) != 0)
        {
            ApplyStun();
            return;
        }

        // If player's body collides and their animator is in Attack state, treat it as a hit
        if (collision.collider.CompareTag("Player"))
        {
            if (IsColliderPlayerAttacking(collision.collider))
                ApplyStun();
        }
    }

    bool IsColliderPlayerAttacking(Collider2D col)
    {
        // Look for a PlayerController or Animator on the collider or its parents
        PlayerController pc = col.GetComponent<PlayerController>();
        Animator pAnim = null;
        if (pc != null)
            pAnim = pc.GetComponent<Animator>();
        else
        {
            pAnim = col.GetComponent<Animator>();
            if (pAnim == null && col.transform.parent != null)
                pAnim = col.transform.parent.GetComponent<Animator>();
        }

        if (pAnim != null)
        {
            var state = pAnim.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("Attack") || state.IsName("Attack1") || state.IsName("Attack2"))
                return true;
        }

        return false;
    }

    void Update()
    {
        if (player == null) return;

        // No chasing behaviour: attack only when player touches (trigger). We still allow
        // immediate attack if player is inside the contact trigger (playerInContact).

        // movement and behavior
        bool isMoving = false;

        // Flyer wanders when not busy
        if (!isBusy)
        {
            // use rb.position if available, otherwise transform.position as fallback
            Vector2 currentPos = rb != null ? rb.position : (Vector2)transform.position;

            // wander behavior when no target
            wanderTimer -= Time.deltaTime;
            if (wanderTimer <= 0f || Vector2.Distance(currentPos, wanderTarget) <= stopDistance)
            {
                // pick a new wander target inside bounds
                wanderTarget = new Vector2(
                    Random.Range(minBounds.x, maxBounds.x),
                    Random.Range(minBounds.y, maxBounds.y)
                );
                wanderTimer = Random.Range(wanderIntervalMin, wanderIntervalMax);
            }

            Vector2 toWander = (wanderTarget - currentPos);
            if (toWander.magnitude > stopDistance)
            {
                Vector2 dir2 = toWander.normalized;
                if (rb != null)
                {
                    rb.MovePosition(currentPos + dir2 * moveSpeed * Time.deltaTime);
                }
                else
                {
                    // fallback to transform-based movement so missing Rigidbody doesn't break the enemy
                    transform.position = (Vector3)(currentPos + dir2 * moveSpeed * Time.deltaTime);
                }
                isMoving = true;

                if (dir2.x < 0 && facingRight) Flip();
                else if (dir2.x > 0 && !facingRight) Flip();
            }
        }

        // animator
        if (animator != null) animator.SetBool("isFlying", isMoving);

        // cooldowns
        attackTimer -= Time.deltaTime;

        // If currently marked busy because of an attack, try to auto-clear busy when the animator
        // has left the attack states. This helps when animation events are missing and prevents
        // the enemy from getting stuck.
        if (isBusy && animator != null)
        {
            var s = animator.GetCurrentAnimatorStateInfo(0);
            int currentHash = s.shortNameHash;

            bool inAttackState = false;
            for (int i = 0; i < attackHashes.Length; i++)
            {
                if (currentHash == attackHashes[i])
                {
                    inAttackState = true;
                    break;
                }
            }

            // If animator has left the defined attack states, clear busy.
            if (!inAttackState)
            {
                isBusy = false;
                StopFallback();
            }

            // Extra safety: if the player is far away (e.g. left scene) force recovery
            if (player != null)
            {
                float d = Vector2.Distance(transform.position, player.position);
                if (d > detectionRange * 2f)
                {
                    ForceRecover();
                }
            }
        }
    }

    void TryAttack()
    {
        if (attackTimer > 0f) return;
        // start attack: mark busy so movement pauses
        isBusy = true;
        if (animator != null) animator.SetBool("isFlying", false);

        // pick random attack trigger like other enemies
        int attackType = Random.Range(0, 2);
        if (animator != null)
        {
            if (attackType == 0) animator.SetTrigger("Attack1");
            else animator.SetTrigger("Attack2");
        }

        // safety fallback in case animation event EndAttack doesn't fire
        StopFallback();
        fallbackCoroutine = StartCoroutine(FallbackClearBusy(attackFallbackDuration));

        attackTimer = attackCooldown;
    }

    // Make the flyer unkillable: when hit, don't die — get stunned for stunDuration seconds
    // Provide a few common method signatures so various player attack implementations will call one of them.
    public void TakeDamage(float amount)
    {
        ApplyStun();
    }

    public void TakeDamage(int amount)
    {
        ApplyStun();
    }

    // For compatibility with SendMessage "TakeHit" calls
    public void TakeHit()
    {
        ApplyStun();
    }

    void ApplyStun()
    {
        // if already stunned, refresh duration
        if (isStunned)
        {
            // restart stun coroutine
            StopCoroutineIfRunning("StunRoutine");
            StartCoroutine(StunRoutine());
            return;
        }

        // clear any pending attack/fallback and mark busy so it won't move or attack
        StopFallback();
        isBusy = true;
        isStunned = true;

        if (animator != null)
        {
            animator.SetBool("isFlying", false);
            animator.SetTrigger("TakeHit");
        }

        StartCoroutine(StunRoutine());
    }

    System.Collections.IEnumerator StunRoutine()
    {
        float t = stunDuration;
        while (t > 0f)
        {
            t -= Time.deltaTime;
            yield return null;
        }

        // recover
        isStunned = false;
        isBusy = false;
        if (animator != null)
        {
            // clear takehit trigger (in case) and return to flight
            animator.ResetTrigger("TakeHit");
            animator.SetBool("isFlying", true);
            if (animator.HasState(0, flightHash))
                animator.Play(flightHash, 0, 0f);
        }
    }

    void StopCoroutineIfRunning(string name)
    {
        // helper, attempt to stop by name; it's OK if nothing is running
        try { StopCoroutine(name); } catch { }
    }

    // Animation event called at end of attack
    public void EndAttack()
    {
        isBusy = false;
        StopFallback();
        // return to flying animation immediately
        if (animator != null)
        {
            // clear attack triggers and return to flying
            animator.ResetTrigger("Attack1");
            animator.ResetTrigger("Attack2");
            animator.SetBool("isFlying", true);
            // attempt to play the configured Flight state directly if it exists to avoid getting stuck
            if (animator.HasState(0, flightHash))
                animator.Play(flightHash, 0, 0f);
        }
    }

    void StopFallback()
    {
        if (fallbackCoroutine != null)
        {
            StopCoroutine(fallbackCoroutine);
            fallbackCoroutine = null;
        }
    }

    IEnumerator FallbackClearBusy(float duration)
    {
        yield return new WaitForSeconds(duration);
        isBusy = false;
        // ensure animator returns to flying state
        if (animator != null)
        {
            animator.ResetTrigger("Attack1");
            animator.ResetTrigger("Attack2");
            animator.SetBool("isFlying", true);
            if (animator.HasState(0, flightHash))
                animator.Play(flightHash, 0, 0f);
        }
        fallbackCoroutine = null;
    }

    void ForceRecover()
    {
        // aggressive recovery: clear triggers, stop fallback coroutine, clear busy and force the flight state
        StopFallback();
        isBusy = false;
        if (animator != null)
        {
            for (int i = 0; i < attackStateNames.Length; i++)
            {
                animator.ResetTrigger(attackStateNames[i]);
            }
            animator.SetBool("isFlying", true);
            if (animator.HasState(0, flightHash))
                animator.Play(flightHash, 0, 0f);
        }
    }

    void OnDisable()
    {
        StopFallback();
    }


    void Flip()
    {
        facingRight = !facingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    // (Removed SafeCompareTag) We now use damageLayer checks like GoblinEnemy
}
