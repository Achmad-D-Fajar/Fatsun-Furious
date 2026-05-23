// =============================================================================
// PagedPanel.cs
// Panel navigasi multi-halaman dengan tombol panah kiri/kanan dan tombol return.
// Satu script untuk dua keperluan berbeda:
//
//   MODE GameObjectPages → About panel
//     Setiap halaman adalah child GameObject yang bisa berisi UI apapun
//     (text sinopsis, layout credits, gambar, dsb). Isi diatur di Inspector
//     atau di Scene — script hanya show/hide berdasarkan halaman aktif.
//
//   MODE SpriteImages → Help panel
//     Halaman berupa array Sprite tutorial. Jumlah gambar = jumlah elemen array.
//     Satu Image component di panel dipakai untuk menampilkan tiap sprite.
//     Untuk tambah/kurang gambar cukup edit array di Inspector, tidak perlu
//     ubah hierarchy.
//
// ── HIERARKI SETUP ────────────────────────────────────────────────────────────
//
//   HelpPanel / AboutPanel           ← PagedPanel.cs di sini
//   ├── Background                   ← Panel background image
//   ├── ReturnButton                 ← Button "✕" / "Kembali"
//   ├── PrevButton    (◀)            ← Button panah kiri
//   ├── NextButton    (▶)            ← Button panah kanan
//   ├── PageIndicator                ← TMP_Text (opsional) misal "2 / 4"
//   │
//   │   ── Khusus MODE GameObjectPages (About) ──────────────────────────────
//   ├── Page_Sinopsis                ← drag ke pages[0]
//   │   ├── TitleText   (TMP)
//   │   └── BodyText    (TMP)
//   └── Page_Credits                 ← drag ke pages[1]
//       ├── TitleText   (TMP)
//       └── CreditsText (TMP)
//
//   ── Khusus MODE SpriteImages (Help) ──────────────────────────────────────
//   └── TutorialImage                ← drag ke tutorialImageDisplay
//       (Image component — sprite akan diganti tiap halaman)
//
// ── CARA WIRING TOMBOL HELP & ABOUT DI MAIN MENU ────────────────────────────
//
//   1. Panel HelpPanel dan AboutPanel dibuat sebagai child Canvas, SetActive FALSE.
//   2. Tombol "Help" di main menu → onClick → HelpPanel.SetActive(true)
//      Lakukan via Button.onClick di Inspector (drag HelpPanel, pilih
//      GameObject.SetActive, centang true).
//   3. Tombol "About" → onClick → AboutPanel.SetActive(true), cara sama.
//   4. Return button dalam panel sudah auto-wired oleh script ini → SetActive(false).
//   5. TIDAK perlu modifikasi UIManager sama sekali.
//
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ── Enum mode konten panel ────────────────────────────────────────────────────

public enum PagedPanelMode
{
    /// <summary>
    /// Setiap halaman adalah child GameObject (teks, gambar, layout bebas).
    /// Dipakai untuk About panel (sinopsis + credits).
    /// </summary>
    GameObjectPages,

    /// <summary>
    /// Halaman berupa Sprite[] yang ditampilkan pada satu Image component.
    /// Dipakai untuk Help panel (kumpulan gambar tutorial).
    /// Tambah/kurang gambar cukup edit array tanpa ubah hierarchy.
    /// </summary>
    SpriteImages,
}

public class PagedPanel : MonoBehaviour
{
    // ── Mode ──────────────────────────────────────────────────────────────────

    [Header("─── Mode ───────────────────────────────────────────")]
    [Tooltip("GameObjectPages → About panel: setiap halaman adalah child GO bebas isi.\n" +
             "SpriteImages    → Help panel: array Sprite, satu Image untuk semua halaman.")]
    [SerializeField] private PagedPanelMode contentMode = PagedPanelMode.GameObjectPages;

    // ── Navigasi ──────────────────────────────────────────────────────────────

    [Header("─── Navigation Buttons ──────────────────────────────")]
    [Tooltip("Tombol kembali / tutup panel. Otomatis di-wire ke OnReturnPressed().")]
    [SerializeField] private Button returnButton;

    [Tooltip("Tombol panah kiri ◀ — ke halaman sebelumnya.")]
    [SerializeField] private Button prevButton;

    [Tooltip("Tombol panah kanan ▶ — ke halaman berikutnya.")]
    [SerializeField] private Button nextButton;

    [Header("─── Page Indicator (opsional) ─────────────────────")]
    [Tooltip("TMP_Text yang menampilkan nomor halaman, misal '2 / 4'. " +
             "Biarkan kosong kalau tidak perlu indikator.")]
    [SerializeField] private TMP_Text pageIndicatorText;

    [Tooltip("Format teks indikator. {0} = halaman aktif, {1} = total halaman.\n" +
             "Contoh: '{0} / {1}' → '2 / 4'   atau   'Hal. {0}' → 'Hal. 2'")]
    [SerializeField] private string pageIndicatorFormat = "{0} / {1}";

    [Header("─── Behaviour ──────────────────────────────────────")]
    [Tooltip("Sembunyikan tombol ◀ di halaman pertama dan ▶ di halaman terakhir.\n" +
             "Jika false: tombol tetap terlihat tapi tidak bisa diklik (interactable = false).")]
    [SerializeField] private bool hideArrowsAtEdges = true;

    [Tooltip("Kembali ke halaman pertama setiap kali panel dibuka.")]
    [SerializeField] private bool resetToFirstPageOnOpen = true;

    // ── Konten Mode: GameObjectPages ─────────────────────────────────────────

    [Header("─── GO Pages — untuk About panel ────────────────────")]
    [Tooltip("Isi setiap halaman sebagai child GameObject.\n\n" +
             "Contoh About:\n" +
             "  pages[0] = Page_Sinopsis\n" +
             "  pages[1] = Page_Credits\n\n" +
             "Setiap GO bisa berisi TMP_Text, Image, layout, apapun. " +
             "Script hanya show/hide GO berdasarkan halaman aktif.")]
    [SerializeField] private GameObject[] pages;

    [Tooltip("TMP_Text yang menampilkan judul halaman aktif, misal 'Synopsis' atau 'Credits'.\n" +
             "Biarkan kosong kalau tidak perlu judul.\n\n" +
             "Letakkan TMP_Text ini di LUAR page GO (langsung child panel) agar " +
             "judul tetap terlihat saat halaman berganti.")]
    [SerializeField] private TMP_Text pageTitleText;

    [Tooltip("Judul tiap halaman — harus sama jumlahnya dengan pages[].\n\n" +
             "Contoh:\n" +
             "  pageTitles[0] = 'Synopsis'\n" +
             "  pageTitles[1] = 'Credits'\n\n" +
             "Biarkan elemen kosong ('') kalau halaman tertentu tidak perlu judul.")]
    [SerializeField] private string[] pageTitles;

    // ── Konten Mode: SpriteImages ─────────────────────────────────────────────

    [Header("─── Sprite Pages — untuk Help panel ─────────────────")]
    [Tooltip("Image component yang dipakai untuk menampilkan tutorial sprite.\n" +
             "Buat satu Image di dalam panel dan drag ke sini.")]
    [SerializeField] private Image tutorialImageDisplay;

    [Tooltip("Array gambar tutorial. Jumlah elemen = jumlah halaman help.\n\n" +
             "Untuk tambah halaman: klik '+' dan drag sprite baru.\n" +
             "Untuk kurang halaman: klik '-'.\n" +
             "Urutan array = urutan halaman saat navigasi.")]
    [SerializeField] private Sprite[] tutorialSprites;

    [Tooltip("Cara scaling sprite tutorial pada Image component.\n\n" +
             "• PreserveAspect ON  → sprite tidak terdistorsi (direkomendasikan).\n" +
             "• Image Type Filled  → untuk efek khusus.\n\n" +
             "Set Image.PreserveAspect = true di Inspector Image component " +
             "dan biarkan Image Type = Simple.")]
    [SerializeField] private bool preserveAspectRatio = true;

    // ── Private State ─────────────────────────────────────────────────────────

    private int _currentPage = 0;

    // ==========================================================================
    //  Unity Lifecycle
    // ==========================================================================

    private void Awake()
    {
        // Auto-wire tombol agar tidak perlu setup manual di Inspector onClick
        if (returnButton != null) returnButton.onClick.AddListener(OnReturnPressed);
        if (prevButton   != null) prevButton.onClick.AddListener(OnPrevPressed);
        if (nextButton   != null) nextButton.onClick.AddListener(OnNextPressed);

        // Sync preserve aspect ratio ke Image component
        if (tutorialImageDisplay != null)
            tutorialImageDisplay.preserveAspect = preserveAspectRatio;
    }

    private void OnEnable()
    {
        // Dipanggil setiap kali panel dibuka (SetActive(true))
        if (resetToFirstPageOnOpen) _currentPage = 0;
        RefreshPage();
    }

    // ==========================================================================
    //  Button Callbacks
    // ==========================================================================

    /// <summary>Tutup panel. Kembali ke main menu / state sebelumnya.</summary>
    public void OnReturnPressed()
    {
        gameObject.SetActive(false);
    }

    /// <summary>Pindah ke halaman sebelumnya.</summary>
    public void OnPrevPressed()
    {
        if (_currentPage > 0) _currentPage--;
        RefreshPage();
    }

    /// <summary>Pindah ke halaman berikutnya.</summary>
    public void OnNextPressed()
    {
        if (_currentPage < TotalPages - 1) _currentPage++;
        RefreshPage();
    }

    // ==========================================================================
    //  Page Refresh
    // ==========================================================================

    private void RefreshPage()
    {
        switch (contentMode)
        {
            case PagedPanelMode.GameObjectPages: RefreshGameObjectPage(); break;
            case PagedPanelMode.SpriteImages:    RefreshSpritePage();     break;
        }

        RefreshPageTitle();
        RefreshNavigation();
        RefreshIndicator();
    }

    // ── Mode: GameObjectPages ─────────────────────────────────────────────────

    private void RefreshGameObjectPage()
    {
        if (pages == null || pages.Length == 0)
        {
            Debug.LogWarning($"[PagedPanel] '{gameObject.name}': pages[] kosong. " +
                              "Drag child GO ke array pages[] di Inspector.");
            return;
        }

        // Tampilkan hanya halaman aktif, sembunyikan sisanya
        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i] != null)
                pages[i].SetActive(i == _currentPage);
        }
    }

    // ── Mode: GameObjectPages title ───────────────────────────────────────────

    private void RefreshPageTitle()
    {
        if (pageTitleText == null) return;

        // Judul hanya relevan untuk mode GameObjectPages (About panel)
        if (contentMode != PagedPanelMode.GameObjectPages)
        {
            pageTitleText.gameObject.SetActive(false);
            return;
        }

        // Kalau pageTitles tidak diisi atau kosong, sembunyikan saja
        if (pageTitles == null || pageTitles.Length == 0)
        {
            pageTitleText.gameObject.SetActive(false);
            return;
        }

        // Ambil judul untuk halaman aktif
        // Kalau index di luar range atau isinya kosong, sembunyikan teks
        if (_currentPage < pageTitles.Length &&
            !string.IsNullOrWhiteSpace(pageTitles[_currentPage]))
        {
            pageTitleText.gameObject.SetActive(true);
            pageTitleText.text = pageTitles[_currentPage];
        }
        else
        {
            pageTitleText.gameObject.SetActive(false);
        }
    }



    private void RefreshSpritePage()
    {
        if (tutorialImageDisplay == null)
        {
            Debug.LogWarning($"[PagedPanel] '{gameObject.name}': tutorialImageDisplay belum di-assign!");
            return;
        }

        if (tutorialSprites == null || tutorialSprites.Length == 0)
        {
            Debug.LogWarning($"[PagedPanel] '{gameObject.name}': tutorialSprites[] kosong. " +
                              "Drag sprite tutorial ke array.");
            return;
        }

        Sprite current = tutorialSprites[_currentPage];
        tutorialImageDisplay.sprite          = current;
        tutorialImageDisplay.preserveAspect  = preserveAspectRatio;
        tutorialImageDisplay.gameObject.SetActive(current != null);
    }

    // ── Navigasi arrows ───────────────────────────────────────────────────────

    private void RefreshNavigation()
    {
        bool isFirst = _currentPage <= 0;
        bool isLast  = _currentPage >= TotalPages - 1;

        if (hideArrowsAtEdges)
        {
            // Sembunyikan tombol di ujung
            if (prevButton != null) prevButton.gameObject.SetActive(!isFirst);
            if (nextButton != null) nextButton.gameObject.SetActive(!isLast);
        }
        else
        {
            // Tombol tetap terlihat, tapi non-interaktif di ujung
            if (prevButton != null) prevButton.interactable = !isFirst;
            if (nextButton != null) nextButton.interactable = !isLast;
        }
    }

    // ── Page indicator text ───────────────────────────────────────────────────

    private void RefreshIndicator()
    {
        if (pageIndicatorText == null) return;

        if (TotalPages <= 1)
        {
            // Sembunyikan indikator kalau hanya 1 halaman
            pageIndicatorText.gameObject.SetActive(false);
            return;
        }

        pageIndicatorText.gameObject.SetActive(true);
        pageIndicatorText.text = string.Format(pageIndicatorFormat,
                                               _currentPage + 1, TotalPages);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    /// <summary>Total halaman berdasarkan mode aktif.</summary>
    private int TotalPages => contentMode switch
    {
        PagedPanelMode.GameObjectPages => pages != null ? pages.Length : 0,
        PagedPanelMode.SpriteImages    => tutorialSprites != null ? tutorialSprites.Length : 0,
        _                              => 0
    };

    // ==========================================================================
    //  Public API — opsional, bisa dipanggil dari script lain
    // ==========================================================================

    /// <summary>Buka panel dan langsung jump ke halaman tertentu (zero-based).</summary>
    public void ShowAtPage(int pageIndex)
    {
        _currentPage = Mathf.Clamp(pageIndex, 0, Mathf.Max(0, TotalPages - 1));
        gameObject.SetActive(true);
    }

    // ==========================================================================
    //  Editor Gizmos — label panel di Scene view
    // ==========================================================================

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Clamp halaman aktif kalau ukuran array berubah di Editor
        if (TotalPages > 0)
            _currentPage = Mathf.Clamp(_currentPage, 0, TotalPages - 1);
    }
#endif
}