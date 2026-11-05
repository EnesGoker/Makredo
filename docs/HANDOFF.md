# Devir Teslim (HANDOFF)

Bu dosya projenin **canlı durumudur**. Her ajan görev başında okur, görev sırasında ve sonunda günceller. Kurallar `AGENTS.md` §1–§5'te. Kısa tut: biten görevler tabloda tek satır olarak kalır, ayrıntıları PR'dadır.

**Son güncelleme:** 2026-11-06 · codex · T0.8 incelemede ([PR #8](https://github.com/EnesGoker/Makredo/pull/8))

## Durum
- **Faz:** 0 — Kuruluş (T0.1–T0.7 tamamlandı, T0.8 incelemede)
- **Hedef sürüm:** v0.9.0-beta (Faz 3 sonu)
- **Engel:** Teknik engel yok; T0.8 [PR #8](https://github.com/EnesGoker/Makredo/pull/8) incelemesini bekliyor. Main yalnız squash PR ve üç zorunlu CI kontrolüyle korunuyor.
- **Başlangıç istisnası:** Enes'in onayıyla yalnızca T0.1'in import ve kit commit'leri doğrudan `main`'e gönderildi. Sonraki bütün görevler branch ve PR ile ilerler; `AGENTS.md` kuralları geçerlidir.

## Aktif görevler
| Görev | Ajan | Branch | Durum | Kaldığı yer / devam notu |
|---|---|---|---|---|
| T0.8 — CI keşfi | codex | `codex/T0.8-ci-discovery` | İncelemede | [PR #8](https://github.com/EnesGoker/Makredo/pull/8), [ADR-0003](adr/0003-test-and-ci-strategy.md). İlk [deney 42132475118](https://github.com/EnesGoker/Makredo/actions/runs/42132475118): x64 gerçek form PNG doğrulandı; ARM64 PNG privacy setup gösteriyor, form yakalama henüz doğrulanmadı. Her iki mimaride hook/SendInput 4/4, klavye 0x10/0x90 ve fare 0x01, cleanup başarılı. [CI 42132475038](https://github.com/EnesGoker/Makredo/actions/runs/42132475038) üç iş yeşil. Son doküman SHA'sının kontrolleri PR açıklamasında; ürün/test dosyaları değişmedi. |

Durum değerleri: `Devam ediyor` · `Yarım kaldı` (limit bitti) · `İncelemede` (PR açık) · `Bekliyor` (Enes'ten cevap bekleniyor).

## Sıradaki
1. **T1.1** Durum makinesi ve tek uçuş
2. **T1.2** Basılı girdileri bırakma
3. **T1.3** Hook'ları ayrı thread'e taşı

Faz 0 içinde T0.3 ile T0.4, T0.2 bittikten sonra paralel yürüyebilir. Sıra ve bağımlılıklar `docs/ROADMAP.md`'de.

## Enes'in yapacakları (manuel)
- [x] M0.1 Store geliştirici hesabı ve "Makredo" ad rezervasyonu — Enes, 5 Kasım 2026
- [x] M0.2 GitHub `EnesGoker/Makredo` (public ve başlangıçta boş olduğu doğrulandı)
- [x] M0.3 Branch koruması, secret scanning, Dependabot alerts — Enes, 6 Kasım 2026; main yalnız squash PR, üç CI kontrolü zorunlu
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

## Son doğrulama — T0.8
- [PR #8](https://github.com/EnesGoker/Makredo/pull/8), araç kaynak SHA `c0ebeb3`; [ayrı discovery 42132475118](https://github.com/EnesGoker/Makredo/actions/runs/42132475118). Gerçek rapor/PNG ve Faz 1 kararı [ADR-0003](adr/0003-test-and-ci-strategy.md)'te; kanıt artifact linkleri orada.
- x64: WinForms formu gösterildi, gerçek ekran PNG'si iki marker ile ilk denemede doğrulandı (640×360, 96 DPI). ARM64: form Visible/Handle/Paint ve PNG yazma başarılı, ama 10 deneme de form marker'ları yerine Windows cihaz gizliliği ekranını yakaladı. ARM64 API desteği genel olarak yok denmiyor; bu runner masaüstünde kullanılabilir form görüntüsü kanıtlanmadı, kesin foreground/desktop nedeni teşhis edilmedi.
- x64 ve native ARM64: 40 byte INPUT, keyboard/mouse low-level hook kuruldu; 4/4 etiketli F24 down/up + iki tıklamasız hareket yakalandı. Raw klavye flags 0x10/0x90, fare flags 0x01. Cleanup key-up 1/1, cursor restore ve iki unhook başarılı. İlişkisiz gerçek tuş değerleri kaydedilmedi.
- Deney aracı IsTestProject=false, normal dotnet test'e veya ürün/test referanslarına girmez. CI/Windows/arch kapısı ve açık --ci-discovery bayrağı UI/native/dosya işlemlerinden önce zorunlu; input wrapper ayrıca CI kontrolü yapar. Workflow'da GITHUB_ACTIONS=false ve eksik bayrak denemeleri exit2 + çıktı yok ile geçti. Mac'te araç hiç çalıştırılmadı.
- [Zorunlu CI 42132475038](https://github.com/EnesGoker/Makredo/actions/runs/42132475038) üç iş yeşil: Ubuntu Core 120; Windows x64/ARM64 107 Core + 34 Windows geçti, mevcut 5 Unix skip (146 sonuç). Build 0 uyarı/hata, format/audit geçti. Solution'daki sekizinci araç nedeniyle audit beklenen proje sayısı XML'den hesaplanıyor; zorunlu iş adları ve testleri aynı.
- Yerel Mac: Release build 0 uyarı/hata, Core 120/120 ve format geçti. Ürün kodu, test dosyaları, SDK pini, props ve ajan kuralları değişmedi. Ayrı discovery workflow dispatch + ilgili PR değişiklikleriyle çalışır; PNG/JSON/log artifact'ları 30 gün saklanır. Yeşil keşif işi her yetenek destekleniyor demek değildir; negatif gözlem de raporlanır.
- Faz 1: iki mimaride gerçek hook/SendInput entegrasyonu mümkün; güvenlik çıkış yolları Core fake'lerle ve CI gerçek adaptörle birlikte test edilmeli. Fiziksel acil durdurmanın pozitif yolu Core non-injected fake + manuel K1 ister; injected CI girdisini fiziksel kanıt sayma. x64 ekran görüntüsü baseline'ı kanıtlandı, ARM64 görüntü karşılaştırmasından önce kullanılabilir masaüstü doğrulanmalı.

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
- T0.8: Form Visible/Handle/Paint olması ekran PNG'sinin o formu gösterdiğini kanıtlamaz. CopyFromScreen sonucunda marker doğrula; ARM64 ilk koşusunda privacy setup görüldü. Hook başarısı fiziksel acil durdurma kanıtı değildir: SendInput olayları gerçekten injected flag taşır.
- `.gitattributes` ve renormalizasyon T0.2'de tamamlandı; T0.7'nin işi değil. Index LF saklar, kapsanan çalışma dosyaları CRLF ve ikon/PNG binary kalır. ADR-0003 T0.8'de tamamlandı; ADR-0004–0010 yol haritasına ayrılmıştır. Göç kararı ADR-0011, taşınabilir test sınırları ADR-0012'dir.

## Tamamlananlar
| Görev | PR | Tarih | Ajan |
|---|---|---|---|
| T0.1 — İçe aktarma | PR yok (Enes onayı); [main'de import + kit](https://github.com/EnesGoker/Makredo/commits/main/) | 2026-11-05 | codex |
| T0.2 — Ad dönüşümü ve yapı | [PR #1](https://github.com/EnesGoker/Makredo/pull/1), squash merge `73dd5c0` | 2026-11-06 | codex |
| T0.4 — .NET 10 | [PR #2](https://github.com/EnesGoker/Makredo/pull/2), squash merge `e1483b8` | 2026-11-06 | codex |
| T0.3 — Veri klasörü göçü | [PR #3](https://github.com/EnesGoker/Makredo/pull/3), squash merge `1dbe02ed93abdba0ac89de920d7207352dea5faf` | 2026-11-06 | codex |
| T0.5 — Platform katmanı | [PR #4](https://github.com/EnesGoker/Makredo/pull/4), squash merge `001acea3ce5050a31ff72499648cadea3a2bdfa0` | 2026-11-06 | codex |
| T0.6 — Test yapısı | [PR #5](https://github.com/EnesGoker/Makredo/pull/5), squash merge `462c32cba5eee136b841836b6c02e9baa1a1adcb` | 2026-11-06 | codex |
| T0.7 — CI | [PR #6](https://github.com/EnesGoker/Makredo/pull/6), squash merge `e2f7732c33cc72dcd155f6b98cfda238b0ae44a8` | 2026-11-06 | codex |
