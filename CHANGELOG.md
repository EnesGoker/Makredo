# Değişiklik Günlüğü / Changelog

## [Unreleased]

### Değişenler

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

- Eşzamanlı kayıt başlatma ve durdurma çağrıları oturumu yeniden oluşturmaz,
  olay aboneliklerini yinelemez veya kaydedilmiş olayları silmez.
- İkinci oynatma isteği etkin oynatmanın durumunu sıfırlamaz. Durum değişikliği
  bildirimleri, geçişlerin gerçekleştiği sırayla yayımlanır.

### Changed

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

- Overlapping recording start and stop calls no longer replace the session,
  duplicate event subscriptions, or discard recorded events.
- A second playback request no longer resets active playback state. State-change
  notifications are published in transition commit order.
