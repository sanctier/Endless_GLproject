using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach this to your Slash prefab (must have a Trigger Collider2D).
/// Applies damage every `tickInterval` seconds to any enemy that stays inside.
/// It supports enemies with `EnemyHealth.TakeDamage(float)` or common enemy `TakeDamage(int)` methods.
/// </summary>
public class SlashDamageZone : MonoBehaviour
{
    [Tooltip("Damage applied each tick")]
    public float damagePerTick = 10f;
    [Tooltip("Seconds between damage ticks")]
    public float tickInterval = 1f;

    [Header("Audio")]
    [Tooltip("Sound to play when an enemy is damaged by this slash (played at hit position)")]
    public AudioClip hitClip;
    [Range(0f,1f)] public float hitVolume = 1f;

    // track running coroutines per-collider so each enemy gets its own timer
    private Dictionary<Collider2D, Coroutine> running = new Dictionary<Collider2D, Coroutine>();

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;
        // Only apply to objects that look like enemies: tag or component
        if (IsEnemyCollider(other))
        {
            if (!running.ContainsKey(other))
            {
                var c = StartCoroutine(DamageOverTime(other));
                running.Add(other, c);
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other == null) return;
        if (running.TryGetValue(other, out var c))
        {
            if (c != null) StopCoroutine(c);
            running.Remove(other);
        }
    }

    void OnDisable()
    {
        // stop all coroutines
        foreach (var kv in running)
        {
            if (kv.Value != null) StopCoroutine(kv.Value);
        }
        running.Clear();
    }

    bool IsEnemyCollider(Collider2D col)
    {
        if (col == null) return false;
        if (col.CompareTag("Enemy")) return true;
        // look for common enemy components
        if (col.GetComponentInParent<EnemyHealth>() != null) return true;
        if (col.GetComponentInParent<GoblinEnemy>() != null) return true;
        if (col.GetComponentInParent<MushroomEnemy>() != null) return true;
        if (col.GetComponentInParent<SkeletonEnemy>() != null) return true;
        if (col.GetComponentInParent<FlyingEnemy>() != null) return true;
        return false;
    }

    IEnumerator DamageOverTime(Collider2D col)
    {
        while (col != null)
        {
            // apply damage if the object still exists and is an enemy
            ApplyDamageToCollider(col, damagePerTick);
            yield return new WaitForSeconds(tickInterval);
        }
        // cleanup will be handled in OnTriggerExit/OnDisable
    }

    void ApplyDamageToCollider(Collider2D col, float dmg)
    {
        if (col == null) return;

        // Prefer EnemyHealth if present
        var eh = col.GetComponentInParent<EnemyHealth>();
        if (eh != null)
        {
            eh.TakeDamage(dmg);
            PlayHitSoundAt(col.transform.position);
            return;
        }

        // Try specific enemy types with int signature
        var gob = col.GetComponentInParent<GoblinEnemy>();
        if (gob != null)
        {
            gob.TakeDamage((int)dmg);
            PlayHitSoundAt(col.transform.position);
            return;
        }

        var mush = col.GetComponentInParent<MushroomEnemy>();
        if (mush != null)
        {
            mush.TakeDamage((int)dmg);
            PlayHitSoundAt(col.transform.position);
            return;
        }

        var skel = col.GetComponentInParent<SkeletonEnemy>();
        if (skel != null)
        {
            skel.TakeDamage((int)dmg);
            PlayHitSoundAt(col.transform.position);
            return;
        }

        var fly = col.GetComponentInParent<FlyingEnemy>();
        if (fly != null)
        {
            fly.TakeDamage(dmg);
            PlayHitSoundAt(col.transform.position);
            return;
        }

        // Fallback: try SendMessage (works if the object implements TakeDamage)
        try
        {
            col.gameObject.SendMessageUpwards("TakeDamage", (int)dmg, SendMessageOptions.DontRequireReceiver);
            PlayHitSoundAt(col.transform.position);
        }
        catch { }
    }

    void PlayHitSoundAt(Vector3 pos)
    {
        if (hitClip == null) return;
        // Play the local slash audio for slash damage. The centralized
        // fireball audio is only invoked by the fireball code itself,
        // so we should not suppress the slash clip here.
        AudioSource.PlayClipAtPoint(hitClip, pos, hitVolume);
    }
}
