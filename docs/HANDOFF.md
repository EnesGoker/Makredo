# Devir Teslim (HANDOFF)

Bu dosya projenin **canlı durumudur**. Her ajan görev başında okur, görev sırasında ve sonunda günceller. Kurallar `AGENTS.md` §1–§5'te. Kısa tut: biten görevler tabloda tek satır olarak kalır, ayrıntıları PR'dadır.

**Son güncelleme:** 2026-11-06 · codex · T0.3 başladı

## Durum
- **Faz:** 0 — Kuruluş (T0.1/T0.2/T0.4 tamamlandı, T0.3 devam ediyor)
- **Hedef sürüm:** v0.9.0-beta (Faz 3 sonu)
- **Engel:** T0.3 için yok; T0.4 squash merge edildi (`e1483b8`).
- **Başlangıç istisnası:** Enes'in onayıyla yalnızca T0.1'in import ve kit commit'leri doğrudan `main`'e gönderildi. Sonraki bütün görevler branch ve PR ile ilerler; `AGENTS.md` kuralları geçerlidir.

## Aktif görevler
| Görev | Ajan | Branch | Durum | Kaldığı yer / devam notu |
|---|---|---|---|---|
| T0.3 — Veri klasörü göçü | codex | `codex/T0.3-data-migration` | Devam ediyor | Göç ve kütüphane referans uyarlaması tamam; Mac'te 38/38 vaka geçti, Release build 0 uyarı/hata ve format geçti. Kaynak korunuyor, logs taşınmıyor, mevcut hedefe dokunulmuyor; hotkey içeriği T1.5'e bırakıldı. Sıradaki: PR ve son HANDOFF. Gerçek veri klasörlerine erişilmedi. |

Durum değerleri: `Devam ediyor` · `Yarım kaldı` (limit bitti) · `İncelemede` (PR açık) · `Bekliyor` (Enes'ten cevap bekleniyor).

## Sıradaki
1. **T0.3** Veri klasörü göçü (aktif)
2. **T0.5** Platform katmanını ayır
3. **T0.6** Test yapısı
4. **T0.7** CI

Faz 0 içinde T0.3 ile T0.4, T0.2 bittikten sonra paralel yürüyebilir. Sıra ve bağımlılıklar `docs/ROADMAP.md`'de.

## Enes'in yapacakları (manuel)
- [x] M0.1 Store geliştirici hesabı ve "Makredo" ad rezervasyonu — Enes, 5 Kasım 2026
- [x] M0.2 GitHub `EnesGoker/Makredo` (public ve başlangıçta boş olduğu doğrulandı)
- [ ] M0.3 Branch koruması, secret scanning, Dependabot alerts (T0.7'den sonra)
- [ ] M0.4 Azure for Students hesabı (Faz 4'ten önce)
- [ ] K1 / K2 / K3 manuel kontroller (ROADMAP'te)

## Enes'e sorular
Her soru için bir varsayılan var. Ajanlar cevap gelene kadar varsayılanla devam eder.
- **S1:** Format 1.2 ile kaydedilmiş bir makro dosyası var mı? → Varsayılan: yok, 1.2 desteği eklenmez (F-022).
- **S2:** Simülasyon modu bilinçli olarak mı açık bırakılmıştı? → Varsayılan: yeni kurulumlarda kapalı, açıkken görünür rozet (T3.1).
- **S3:** Başka programların enjekte ettiği girdiler (ekran klavyesi gibi) kaydedilsin mi? → Varsayılan: evet, ayardan kapatılabilir (T1.4).
- **S4:** Dosya sınırları uygun mu? 50 MB, 500.000 olay, tek gecikme en fazla 24 saat → Varsayılan: bu değerler (T2.3).

## Sonraya (kapsam dışı görülenler)
Görev sırasında fark edilen ama o görevin işi olmayan şeyler buraya yazılır: kısa açıklama, nerede görüldüğü, hangi görevde fark edildiği.
- T0.4: .NET 10'a geçince 17 WinForms dosyasında 45 WFO1000 tasarım zamanı serialization tanısı oluştu. Kodla oluşturulan mevcut kontrollerin serialization davranışını korumak için yalnız `Makredo.WinForms.csproj` içinde WFO1000 bastırıldı. Bu proje kapsamı gelecekteki metadata eksiklerini de gizleyebilir; Faz 4 arayüz refaktöründe özelliklerin `DefaultValue`/`DesignerSerializationVisibility`/`ShouldSerialize` niyetleri belirlenip suppression kaldırılmalı.
- T0.3: `Program` global hata handler'ını composition root'tan sonra kuruyor (mevcut F-017/T2.4). Göç hatası güvenli biçimde startup'ı durdurur; özel göç hata iletişimi/önceden loglama bu görevde eklenmedi.

## Son doğrulama — T0.3
- `dotnet build -c Release`: **0 uyarı, 0 hata**. `dotnet format --verify-no-changes`: geçti. Mevcut 67 Windows testi korundu/derlendi; default veri yolu testi Makredo için güncellendi.
- `dotnet test tests/Makredo.Migration.Tests -c Release --no-build`: Mac'te **38 geçti, 0 başarısız, 0 atlanan**. Yeni proje `net10.0`; yalnız Infrastructure'a referans verir ve mevcut test paketlerini kullanır. T0.6'da Core'a taşınacak.
- Geçici klasörlerde kopyalama, byte/LastWriteTime kaynak koruması, logs/ilgisiz dosya dışlama, ikinci açılış/mevcut hedef/yarış, gerçek yarım hedef dosyası ardından enjekte I/O hatası, erişim hatası, iptal ve güncel kaynaktan retry doğrulandı. Disk gerçekten doldurulmadı; işletim sistemi kilit davranışına bağımlı test kullanılmadı.
- Favori/son kullanım yolları yalnız eski `macros/` içindeyse kopyadaki yeni köke uyarlandı; gerçek store ile favorite/tarih semantiği, dış yol sınırları, bilinmeyen alanlar, geçerli/geçersiz tarih çakışmaları ve bozuk/uygulanamaz JSON'da byte/mtime koruması test edildi.
- Marker `.legacy-data-migration.json`, `version: 1` ve `hotkeyMigrationPending: true` içerir. Kısayol dosyası byte olarak aynı kalır; T1.5 gerçek varsayılan dönüşümünü yapacak. Tasarım [ADR-0011](adr/0011-transactional-data-directory-migration.md) içinde.
- Symlink/reparse kaynakları güvenli biçimde reddedilir; üç symlink vakası Mac'te koştu. Windows'ta ayrıcalık gereksinimi nedeniyle bu vakalar açıkça Skipped raporlanacak. Hard-kill artığı staging klasörleri final sayılmaz, başka denemenin staging'i silinmez.
- Gerçek kullanıcı veri klasörlerine erişilmedi; tüm test verileri sentetik/geçici. Windows startup/NTFS ACL/sharing ve mevcut Windows suite çalıştırılmadı (T0.7). CI henüz yok; T0.4'ün Windows framework izleme listesi [PR #2](https://github.com/EnesGoker/Makredo/pull/2) içinde duruyor.

## Öğrenilenler ve tuzaklar
- Mac'te `net10.0-windows` testleri **çalışmaz**. Derlenir ama koşmaz. Windows testlerinin sonucu CI'dan alınır.
- Simülasyon modu açıkken oynatma hiçbir girdi göndermez. "Oynatma çalışmıyor" gibi görünen sonuçlarda önce bu ayara bak.
- F12, Windows'ta global kısayol olarak kaydedilemez (Win32 hata 1409, hata ayıklayıcıya ayrılmış).
- Windows'un varsayılan zamanlayıcı çözünürlüğü yaklaşık 15,6 ms. `Task.Delay(1)` yaklaşık 15,6 ms bekler (F-004).
- Denetim raporundaki dosya yolları ve satır numaraları eski `MacroMaster@837fe75` koduna aittir. T0.2 ve T0.5'ten sonra yeni yollara bak.
- `.slnx` için .NET 10 SDK pini (`latestFeature`) ve Mac Windows hedefleme ayarı T0.2'de öne alındı (ADR-0002); framework ve test paketleri T0.4'te .NET 10'a taşındı. xUnit framework v2'de kalır; VSTest adapter major sürümünün framework major sürümüyle eşleşmesi gerekmez.
- Göç logger/store kurulmadan önce tamamlanmalı; hata sonrası boş final klasörle devam etmek retry'ı bozar. `AppStoragePaths.CreateDefault()` yalnız Makredo yolunu çözer, WinForms kaynak yolunu `LegacyStoragePaths.DirectoryName` ile kurar.
- Kütüphane durumu mutlak dosya yolları tutar; sadece byte kopyalama favori/son kullanım kayıtlarının UI tarafından budanmasına yol açar. Bu yüzden yalnız kopyadaki iç makro referansları yeniden köklendirilir; kaynak JSON değişmez.
- T0.6, `tests/Makredo.Migration.Tests` testlerini ve UnixTheory helper'ını Core'a taşımalı; Infrastructure `InternalsVisibleTo("Makredo.Migration.Tests")` test seam'ini de yeni assembly adına güncellemeli.
- `.gitattributes` ve renormalizasyon T0.2'de tamamlandı; T0.7'nin işi değil. Index LF saklar, kapsanan çalışma dosyaları CRLF ve ikon/PNG binary kalır. ADR-0003–0010 yol haritasına ayrılmıştır; göç kararı ADR-0011'dir.

## Tamamlananlar
| Görev | PR | Tarih | Ajan |
|---|---|---|---|
| T0.1 — İçe aktarma | PR yok (Enes onayı); [main'de import + kit](https://github.com/EnesGoker/Makredo/commits/main/) | 2026-11-05 | codex |
| T0.2 — Ad dönüşümü ve yapı | [PR #1](https://github.com/EnesGoker/Makredo/pull/1), squash merge `73dd5c0` | 2026-11-06 | codex |
| T0.4 — .NET 10 | [PR #2](https://github.com/EnesGoker/Makredo/pull/2), squash merge `e1483b8` | 2026-11-06 | codex |
