# Değişiklik Günlüğü / Changelog

## [Unreleased]

### Değişenler

- Varsayılan kayıt kısayolu Ctrl+Shift+Alt+R, oynat/durdur kısayolu Ctrl+Shift+Alt+P oldu.
  F8/F9/F10 ayarlarda hazır seçenek olarak korunur; kısayol ayarları için global tuş yoktur.
  Eski varsayılanlarla birebir eşleşen göç edilmiş ayarlar bir kez güncellenir;
  özel kısayollar değiştirilmez ve göç öncesi ayarlar yedeklenir.

- Uygulama ve yükleyici adı Makredo olarak değiştirildi. Pencere, iletişim kutusu
  ve rapor başlıkları aynı ürün adını kullanır; çalıştırılabilir dosya `Makredo.exe`
  ve günlük dosyaları `makredo-yyyyMMdd.log` olarak adlandırılır.
- Projeler `src/` ve `tests/` altına taşındı; çözüm dosyası `Makredo.slnx` oldu.
- Uygulama ve test projeleri .NET 10'a taşındı; test paketleri güncellendi.
- İlk açılışta eski Polly klasöründeki ayarlar ve makrolar Makredo klasörüne
  bir kez kopyalanır. Kaynak veriler korunur, günlükler taşınmaz ve mevcut
  Makredo verilerinin üzerine yazılmaz. Yarım kalan kopyalar yeniden denenir;
  favori ve son kullanım kayıtları yeni makro konumuna uyarlanır.
- Göç başarısızsa Türkçe hata mesajı gösterilir ve uygulama başlatılmaz;
  hata ayrıntıları mümkünse geçici klasöre yazılır.
- Etkin olmayan, 24 saatten eski göç geçici klasörleri temizlenir;
  açık işlem kilidi bulunan ve yeni klasörler korunur.

### Düzeltilenler

- Esc veya Pause, başka bir pencere odaktayken ve değiştirici tuşlar basılıyken
  oynatmayı, başlangıç beklemesini veya duraklatılmış oturumu durdurur. Oynatma
  bastığı girdileri bırakır. Ekran klavyesi de acil durdurmayı tetikleyebilir.

- Kısayollar bağımsız kaydedilir; çakışan bağ Türkçe, engellemeyen bir uyarı bandında
  gösterilirken diğerleri çalışır. Basılı tutma tekrarı kayıt durumunu değiştirmez.
  Fare hareketi, kaydı başlatan veya bitiren kısayolun modifier tuşlarını kayda
  sokmaz. Tıklama ve kaydırma için kullanılan modifier tuşları korunur.

- Oynatmanın kendi girdileri ve bırakma olayları kayda alınmaz. Diğer uygulamaların
  enjekte ettiği girdiler varsayılan olarak kaydedilir; bu seçenek ayarlardan kapatılabilir.

- Klavye ve fare kaydı artık arayüzden bağımsız bir hook thread'i ve arka plan
  tüketicisi kullanır. Kayıt süreleri hook'un yakalama zamanından hesaplanır;
  durdurmada kuyruktaki olaylar kaybolmadan kayda eklenir.

- Oynatma durdurulduğunda, iptal edildiğinde, hata verdiğinde veya normal
  tamamlandığında kendi bastığı tuşları ve fare düğmelerini ters sırayla bırakır.
  Pencere kapanışı bu temizliğin bitmesini bekler; simülasyon gerçek girdi göndermez.

- Eşzamanlı kayıt başlatma ve durdurma çağrıları oturumu yeniden oluşturmaz,
  olay aboneliklerini yinelemez veya kaydedilmiş olayları silmez.
- İkinci oynatma isteği etkin oynatmanın durumunu sıfırlamaz. Durum değişikliği
  bildirimleri, geçişlerin gerçekleştiği sırayla yayımlanır.

### Changed

- Default shortcuts are Ctrl+Shift+Alt+R for recording and Ctrl+Shift+Alt+P for play/stop.
  F8/F9/F10 remain available as a preset; hotkey settings have no global shortcut.
  Migrated settings matching all legacy defaults are upgraded once, with backups;
  custom bindings are preserved.

- Renamed the application and installer to Makredo. Window, dialog, and report
  titles use the same product name; the executable is `Makredo.exe` and log files
  are named `makredo-yyyyMMdd.log`.
- Moved projects under `src/` and `tests/` and kept `Makredo.slnx` as the solution.
- Migrated application and test projects to .NET 10 and updated test packages.
- On first launch, legacy Polly settings and macros are copied once into the
  Makredo directory. Source data is preserved, logs are excluded, and existing
  Makredo data is never overwritten. Interrupted copies are retried; favorites
  and recently used records are updated to the new macro location.
- Migration failures show a Turkish error message and stop startup; diagnostic
  details are written to the temporary directory when possible.
- Inactive migration staging copies older than 24 hours are reclaimed;
  active leased attempts and fresh directories are preserved.

### Fixed

- Escape or Pause stops playback during an initial delay, an event delay or
  a pause, with any window focused and any modifiers held. Playback releases
  its held inputs, and on-screen keyboards can trigger the stop.

- Hotkeys register independently. A non-modal Turkish warning identifies collisions
  while other bindings keep working. Holding a shortcut no longer toggles repeatedly.
  Mouse movement does not leak the starting or stopping shortcut's modifiers
  into the recording. Modifiers used for clicks and scrolling are preserved.

- Playback input and cleanup releases are excluded from recording. Foreign injected
  input remains enabled by default and can be disabled in settings.

- Keyboard and mouse recording now uses a dedicated hook thread and background
  consumer independent of the UI. Timing comes from native capture timestamps;
  stopping drains queued input into the recording before completing.

- Playback releases its own held keys and mouse buttons in reverse order on stop,
  cancellation, error or normal completion. Window shutdown waits for cleanup;
  simulation sends no real input.

- Overlapping recording start and stop calls no longer replace the session,
  duplicate event subscriptions, or discard recorded events.
- A second playback request no longer resets active playback state. State-change
  notifications are published in transition commit order.
