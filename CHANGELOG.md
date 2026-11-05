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
  Makredo verilerinin üzerine yazılmaz. Yarım kalan kopyalar yeniden denenir.

### Changed

- Renamed the application and installer to Makredo. Window, dialog, and report
  titles use the same product name; the executable is `Makredo.exe` and log files
  are named `makredo-yyyyMMdd.log`.
- Moved projects under `src/` and `tests/` and kept `Makredo.slnx` as the solution.
- Migrated application and test projects to .NET 10 and updated test packages.
- On first launch, legacy Polly settings and macros are copied once into the
  Makredo directory. Source data is preserved, logs are excluded, and existing
  Makredo data is never overwritten. Interrupted copies are retried.
