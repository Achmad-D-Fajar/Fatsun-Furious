// =============================================================================
// AnimalDetectionZone.cs
// Script kecil yang dipasang di child "DetectionZone" dari animal prefab.
// Tugasnya hanya satu: mendeteksi player dan menotifikasi AnimalHazard.cs
// di parent untuk mulai bergerak.
//
// SETUP:
//   1. Buat child GameObject bernama "DetectionZone" di dalam animal prefab.
//   2. Tambahkan BoxCollider2D ke DetectionZone → IsTrigger = ✓
//   3. Resize collider agar menjulur KE DEPAN animal
//      (arah yang akan didatangi player terlebih dahulu).
//   4. Pasang script ini ke DetectionZone.
//   5. TIDAK perlu drag reference apapun — script otomatis cari AnimalHazard
//      di parent-nya saat Awake.
//
// UKURAN COLLIDER YANG DISARANKAN:
//   - Width : 4.5 (cover semua 3 lane)
//   - Height: 3–5 (seberapa jauh player terdeteksi sebelum zona animal)
// =============================================================================

using UnityEngine;

public class AnimalDetectionZone : MonoBehaviour
{
    // Cached reference — dicari sekali saat Awake, bukan setiap frame.
    private AnimalHazard _animal;

    private void Awake()
    {
        _animal = GetComponentInParent<AnimalHazard>();

        if (_animal == null)
            Debug.LogError($"[AnimalDetectionZone] '{gameObject.name}' tidak menemukan " +
                           $"AnimalHazard.cs di parent-nya! Pastikan AnimalHazard.cs " +
                           $"terpasang di root animal, bukan di child ini.");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (_animal == null) return;

        _animal.OnPlayerEnterDetection();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (_animal == null) return;

        _animal.OnPlayerExitDetection();
    }

    // ── Gizmos — zona deteksi berwarna oranye ─────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return;
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.35f); // oranye
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);
    }
}