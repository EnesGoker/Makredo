# Devir Teslim (HANDOFF)

Bu dosya projenin **canlı durumudur**. Her ajan görev başında okur, görev sırasında ve sonunda günceller. Kurallar `AGENTS.md` §1–§5'te. Kısa tut: biten görevler tabloda tek satır olarak kalır, ayrıntıları PR'dadır.

**Son güncelleme:** 2026-11-06 · codex · T0.7 incelemede ([PR #6](https://github.com/EnesGoker/Makredo/pull/6))

## Durum
- **Faz:** 0 — Kuruluş (T0.1–T0.6 tamamlandı, T0.7 incelemede)
- **Hedef sürüm:** v0.9.0-beta (Faz 3 sonu)
- **Engel:** Teknik engel yok; T0.7 [PR #6](https://github.com/EnesGoker/Makredo/pull/6) incelemesini bekliyor.
- **Başlangıç istisnası:** Enes'in onayıyla yalnızca T0.1'in import ve kit commit'leri doğrudan `main`'e gönderildi. Sonraki bütün görevler branch ve PR ile ilerler; `AGENTS.md` kuralları geçerlidir.

## Aktif görevler
| Görev | Ajan | Branch | Durum | Kaldığı yer / devam notu |
|---|---|---|---|---|
| T0.7 — CI | codex | `codex/T0.7-ci` | İncelemede | [PR #6](https://github.com/EnesGoker/Makredo/pull/6): [42128747961](https://github.com/EnesGoker/Makredo/actions/runs/42128747961) üç iş yeşil. Ubuntu Core 120/120; Windows x64/ARM64 Core 107 geçti + mevcut 5 Unix skip, Windows 34/34 (146 sonuç). TRX kaynak/metot/vaka denetimi ve artifact var; Windows format/açık paket taraması geçti. İlk koşudaki T0.3 fixture yol hatası ayrı 0c713b3, TRX okuyucu düzeltmesi ayrı 9f503c1; assertion/Skip/ürün kodu değişmedi. |

Durum değerleri: `Devam ediyor` · `Yarım kaldı` (limit bitti) · `İncelemede` (PR açık) · `Bekliyor` (Enes'ten cevap bekleniyor).

## Sıradaki
1. **T0.8** CI keşfi

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
- T0.3 düzeltmesi yalnız göç/hazırlık hatasını WinForms'ta yakalar; mesaj ve best-effort geçici tanı sonrası uygulama başlamaz. Diğer composition/logger kurulum hatalarının genel işlenmesi mevcut F-017/T2.4 kapsamındadır.
- T0.5: Hook/SendInput davranışları taşınırken değiştirilmedi. Basılı girdilerin bırakılması F-001/T1.2, fiziksel acil durdurma F-002/T1.6 kapsamındadır; yeni yollar `src/Makredo.Platform.Windows/Hooks` ve `Interop` altındadır.

## Son doğrulama — T0.7
- [PR #6](https://github.com/EnesGoker/Makredo/pull/6), doğrulanan kod SHA `9f503c1`; [CI 42128747961](https://github.com/EnesGoker/Makredo/actions/runs/42128747961): **Ubuntu / Core, Windows / x64, Windows / ARM64 yeşil** (`gh pr checks 6` exit 0). Devir commit'i yalnız bu dokümanı günceller; PR'ın son SHA kontrolü PR açıklamasında.
- Ubuntu: **120 Core geçti, 0 atlanan**. Windows x64 ve native ARM64: **107 Core geçti + önceden mevcut 5 UnixTheory skip**, **34 Windows geçti**; her makinede **146 TRX sonucu, 141 geçen, 5 mevcut skip, 0 başarısız**. 154 kaynak vakasının 13 Unix satırı Windows'ta 5 skip olarak keşfedilir; kaynak metotları/vaka sayıları ve TRX sonuçları birebir denetlenir. Yeni Skip/filtre/beklenti değişikliği yok.
- İlk [CI 42128392866](https://github.com/EnesGoker/Makredo/actions/runs/42128392866) Windows x64/ARM64'te yalnız `ExistingDestinationStillAllowsCleanupWithoutChangingUserData` kırmızıydı: expected `macros/kept.json`, actual `macros\kept.json`. T0.3 fixture'ında birleşik slash dizgesi korunuyordu; `Path.Combine("macros", "kept.json")` ile dosya kurulum yolu düzeltildi (`0c713b3`), bütün assertion'lar aynı. Windows.Tests ilk koşuda da iki mimaride 34/34 geçti; masaüstü/hook gerekçesiyle hiçbir test atlanmadı.
- Yeni TRX okuyucusunun ilk koşuda saptanan sayaç uyumu sorunu ayrı `9f503c1` commit'inde giderildi: VSTest, 5 NotExecuted sonucu için `notExecuted=0` yazabiliyor. Tekil outcome, total/executed/passed/failed zorunlu tutarlılığı korunur; skip sayacı yok/0 olabilir. Olumsuz fixture'lar eksik/sıfır/tekrarlı/başarısız/beklenmeyen skip ve çelişkili sayaçları reddeder.
- Yerel Mac: `dotnet build -c Release` **0 uyarı, 0 hata**; Core **120/120**, `dotnet format --verify-no-changes` geçti. İki PowerShell scriptinin self-testleri ve gerçek Mac/CI TRX kayıtlarına karşı doğrulama geçti.
- Windows x64 CI: format geçti; doğrudan+transitif NuGet audit JSON'u **7 proje, açık yok**. JSON kapısı tarama hatası/bozuk rapor/açık bulgusunu başarısız sayar; yalnız CLI exit koduna güvenmez. Yerel audit ve olumlu/olumsuz fixture'ları da geçti.
- `contents: read`, PR concurrency iptali, persist-credentials kapalı checkout, yalnız SHA pinli actions/*, global.json SDK ve native mimari kontrolleri var. Her iş TRX artifact yükler; ARM testhost açıkça ARM64 hedeflenir. Dependabot (haftalık NuGet/Actions) ve AGENTS §4 başlıklı PR şablonu eklendi.
- Geliştiricinin Mac'inde Windows testi/uygulama/gerçek girdi çalıştırılmadı. CI'da da bu görev yeni hook/SendInput senaryosu eklemedi; mevcut Windows testleri callback/yerleşim/adapter kapsamıdır. Ekran görüntüsü ve gerçek hook+SendInput fizibilitesi T0.8'e kaldı.

## Öğrenilenler ve tuzaklar
- Mac'te `net10.0-windows` testleri **çalışmaz**. T0.7 ile Windows x64 ve ARM64 sonuçları CI'dan alınır; TRX artifact'ları her işte saklanır. `scripts/verify-ci-tests.ps1` kaynak ve TRX kapsamını kontrol eder.
- Simülasyon modu açıkken oynatma hiçbir girdi göndermez. "Oynatma çalışmıyor" gibi görünen sonuçlarda önce bu ayara bak.
- F12, Windows'ta global kısayol olarak kaydedilemez (Win32 hata 1409, hata ayıklayıcıya ayrılmış).
- Windows'un varsayılan zamanlayıcı çözünürlüğü yaklaşık 15,6 ms. `Task.Delay(1)` yaklaşık 15,6 ms bekler (F-004).
- Denetim raporundaki dosya yolları ve satır numaraları eski `MacroMaster@837fe75` koduna aittir. T0.2 ve T0.5'ten sonra yeni yollara bak.
- `.slnx` için .NET 10 SDK pini (`latestFeature`) ve Mac Windows hedefleme ayarı T0.2'de öne alındı (ADR-0002); framework ve test paketleri T0.4'te .NET 10'a taşındı. xUnit framework v2'de kalır; VSTest adapter major sürümünün framework major sürümüyle eşleşmesi gerekmez.
- Göç logger/store kurulmadan önce tamamlanmalı; hata sonrası boş final klasörle devam etmek retry'ı bozar. `AppStoragePaths.CreateDefault()` yalnız Makredo yolunu çözer, WinForms kaynak yolunu `LegacyStoragePaths.DirectoryName` ile kurar.
- Yaş tek başına staging sahipliği kanıtı değildir: bitişik exclusive lease aktif kopyayı yaşından bağımsız korur. Silme öncesi kaynak/hedef root ve alt bağlantıları da kontrol edilir; belirsizlikte temizlik yapılmaz.
- Kütüphane durumu mutlak dosya yolları tutar; sadece byte kopyalama favori/son kullanım kayıtlarının UI tarafından budanmasına yol açar. Bu yüzden yalnız kopyadaki iç makro referansları yeniden köklendirilir; kaynak JSON değişmez.
- T0.6: göç testleri ve UnixTheory artık `tests/Makredo.Core.Tests/Migration` altında, Infrastructure test seam'i Core.Tests'e açık. WinForms proje adı `Makredo.WinForms` olsa da gerçek AssemblyName `Makredo`; taşınan internal saf sınıfların UI friend adı bu olmalı. Kaynak kopyası linklemek yerine gerçek taşınabilir assembly test edilir.
- T0.6 sayım scripti baseline `001acea` kaynaklarını `git show` ile okur; CI'da çalıştırılacaksa bu commit'i içeren history fetch edilmelidir. Kaynak sayımı Windows testlerini çalıştırmaz; Mac Core sonucu ayrıca raporlanır.
- T0.7: Test fixture'larında `macros/file.json` gibi birleşik yol metinlerini dönen dosya listeleriyle dizge olarak kıyaslama; Path.Combine ile bileşenleri ayrı kur. Mevcut UnixTheory'ler Windows'ta 13 satırdan 5 skip sonucu üretir; VSTest'in notExecuted sayacı 0 kalabilir, tekil outcome ve total−executed kanıtına bak.
- `.gitattributes` ve renormalizasyon T0.2'de tamamlandı; T0.7'nin işi değil. Index LF saklar, kapsanan çalışma dosyaları CRLF ve ikon/PNG binary kalır. ADR-0003–0010 yol haritasına ayrılmıştır; göç kararı ADR-0011'dir.

## Tamamlananlar
| Görev | PR | Tarih | Ajan |
|---|---|---|---|
| T0.1 — İçe aktarma | PR yok (Enes onayı); [main'de import + kit](https://github.com/EnesGoker/Makredo/commits/main/) | 2026-11-05 | codex |
| T0.2 — Ad dönüşümü ve yapı | [PR #1](https://github.com/EnesGoker/Makredo/pull/1), squash merge `73dd5c0` | 2026-11-06 | codex |
| T0.4 — .NET 10 | [PR #2](https://github.com/EnesGoker/Makredo/pull/2), squash merge `e1483b8` | 2026-11-06 | codex |
| T0.3 — Veri klasörü göçü | [PR #3](https://github.com/EnesGoker/Makredo/pull/3), squash merge `1dbe02ed93abdba0ac89de920d7207352dea5faf` | 2026-11-06 | codex |
| T0.5 — Platform katmanı | [PR #4](https://github.com/EnesGoker/Makredo/pull/4), squash merge `001acea3ce5050a31ff72499648cadea3a2bdfa0` | 2026-11-06 | codex |
| T0.6 — Test yapısı | [PR #5](https://github.com/EnesGoker/Makredo/pull/5), squash merge `462c32cba5eee136b841836b6c02e9baa1a1adcb` | 2026-11-06 | codex |
