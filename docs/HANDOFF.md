# Devir Teslim (HANDOFF)

Bu dosya projenin **canlı durumudur**. Her ajan görev başında okur, görev sırasında ve sonunda günceller. Kurallar `AGENTS.md` §1–§5'te. Kısa tut: biten görevler tabloda tek satır olarak kalır, ayrıntıları PR'dadır.

**Son güncelleme:** 2026-11-06 · codex · T1.1 incelemede ([PR #9](https://github.com/EnesGoker/Makredo/pull/9))

## Durum
- **Faz:** 1 — Güvenlik ve veri (Faz 0 tamamlandı, T1.1 incelemede)
- **Hedef sürüm:** v0.9.0-beta (Faz 3 sonu)
- **Engel:** Teknik engel yok; T1.1 [PR #9](https://github.com/EnesGoker/Makredo/pull/9) incelemesini bekliyor. Main yalnız squash PR ve üç zorunlu CI kontrolüyle korunuyor.
- **Başlangıç istisnası:** Enes'in onayıyla yalnızca T0.1'in import ve kit commit'leri doğrudan `main`'e gönderildi. Sonraki bütün görevler branch ve PR ile ilerler; `AGENTS.md` kuralları geçerlidir.

## Aktif görevler
| Görev | Ajan | Branch | Durum | Kaldığı yer / devam notu |
|---|---|---|---|---|
| T1.1 — Durum makinesi ve tek uçuş | codex | `codex/T1.1-state-machine` | İncelemede | [PR #9](https://github.com/EnesGoker/Makredo/pull/9): test-only f3468dc [kırmızı 42186380050](https://github.com/EnesGoker/Makredo/actions/runs/42186380050) → düzeltme 81c24ea [yeşil 42188321449](https://github.com/EnesGoker/Makredo/actions/runs/42188321449). R1/R2/R4/R9 ve FIFO regresyonları aynı kaldı; Core 145/145, üç CI işi yeşil, Release 0 uyarı/hata. Mac ve Ubuntu CI ayrı ayrı 100×25=2500 geçti; TRX/summary artifact PR'da. ADR-0013/CHANGELOG güncel. Son devir SHA kontrolü PR açıklamasında. |

Durum değerleri: `Devam ediyor` · `Yarım kaldı` (limit bitti) · `İncelemede` (PR açık) · `Bekliyor` (Enes'ten cevap bekleniyor).

## Sıradaki
1. **T1.2** Basılı girdileri bırakma
2. **T1.3** Hook'ları ayrı thread'e taşı
3. **T1.4** Enjekte girdi

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

- T1.1 incelemesi: Recorder.Clear yalnız Recording durumunu reddediyor; asenkron Stop sırasında Stopping iken tamponları silebilir. Bu görev Start/Play/Stop sahipliğini düzeltir; Clear/oturum belgesi davranışı T1.7 sırasında ele alınmalı. AppCompositionRoot mevcut MacroPlaybackService IDisposable nesnesini disposable listesine almıyor; servis ömrü/Shutdown hata yönetimiyle birlikte ayrıca değerlendirilmeli.

## Son doğrulama — T1.1
- TDD kırmızı: yalnız test/HANDOFF commit'i `f3468dc`, [CI 42186380050](https://github.com/EnesGoker/Makredo/actions/runs/42186380050). Core 130 vakada 8 beklenen hata, 122 geçti: R1 iki izinsiz self geçişi, R2 iki start senaryosu, R4 aktif Playing→Idle, R9 boş olay listesi, iki StateChanged sırası. Üç zorunlu iş kırmızı; üretim kodu o SHA'da değişmedi.
- Yeşil: düzeltme `81c24ea`, [CI 42188321449](https://github.com/EnesGoker/Makredo/actions/runs/42188321449) üç iş ve CodeQL yeşil (`gh pr checks 9` exit 0). İlk dört regresyon dosyası kırmızı commit ile birebir aynı. Son doküman commit'i yalnız bu devir notunu değiştirir; PR'ın en son SHA kontrolü açıklamada.
- Yerel Mac: `dotnet build -c Release` **0 uyarı, 0 hata**; `dotnet format --verify-no-changes` geçti; Core **145 geçti, 0 atlanan**. Windows x64/ARM64: her birinde **132 Core geçti + mevcut 5 Unix skip**, Windows **34/34 geçti** (171 sonuç, 166 geçen). Format ve 8 proje açık paket taraması geçti.
- 100 tekrar: `scripts/repeat-concurrency-tests.ps1 -Iterations 100 -ExpectedCases 25`; Mac **100/100, 2500 geçiş, 0 hata/skip**, 94,07 saniye. Ubuntu CI **100/100, 2500 geçiş, 0 hata/skip**, 170,07 saniye. CI'dan indirilen 100 TRX dosyası ayrıca tek tek doğrulandı; [artifact](https://github.com/EnesGoker/Makredo/actions/runs/42188321449/artifacts/13487839519) her çalıştırmanın TRX/log'unu ve summary.json'u içerir. Bariyer/TCS yarışmayı kurar; sabit bekleme yok, timeout yalnız deadlock korumasıdır.
- TryTransitionTo aynı duruma yalnız açık izinle true döner. Durumlar kilitte commit + subscriber snapshot FIFO kuyruğuna alınır, tek publisher kilit dışında yayımlar. Reentrant/eşzamanlı çağrı bildirim tamamlanmadan dönebilir; CurrentState son commit'tir. Subscriber hatası kuyruğu kilitlemez; ilk hata drain sonrası korunur. Karar [ADR-0013](adr/0013-state-and-lifecycle-ownership.md)'te.
- Recorder Start/Stop ortak task ile tek sahipli; Stop startup/rollback'i bekler, Stop sırasındaki Start yeni oturum kuyruğa koymaz. Playback sahibi Playing yayını öncesi ayrılır ve final bildirimlere kadar korunur; ikinci Play cleanup'a girmez, Stop istekleri aynı uçuşa katılır. Playback Stop hâlâ yalnız durdurma isteğinin tamamlanmasıdır; Play'in tümünün bitişini beklemez.
- Değişiklikler üç Application servisi, Core testleri, tekrar script/CI adımı, ADR, changelog ve HANDOFF ile sınırlı. Platform/WinForms/native input/SDK/props/ajan kuralları değişmedi. T1.2 basılı girdi bırakma, T1.3 hook thread'i ve T1.6 acil durdurma uygulanmadı; Mac'te gerçek girdi çalıştırılmadı.

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
- T1.1: StateChanged callback'inde senkron Task bekleme kullanma; FIFO yayında event argümanı tarihsel olabilir, CurrentState daha ileride olabilir. Start/Stop tamamlanmasını ortak task temsil eder; Playback Stop yalnız istek tamamlanmasıdır. Concurrency kategorisine vaka eklenince CI tekrarındaki ExpectedCases=25 sayısını bilinçli güncelle; eksik/sıfır/skip/isim kayması script tarafından reddedilir.
- `.gitattributes` ve renormalizasyon T0.2'de tamamlandı; T0.7'nin işi değil. Index LF saklar, kapsanan çalışma dosyaları CRLF ve ikon/PNG binary kalır. ADR-0003 T0.8'de tamamlandı; ADR-0004–0010 yol haritasına ayrılmıştır. Göç kararı ADR-0011, taşınabilir test sınırları ADR-0012, durum/yaşam döngüsü sahipliği ADR-0013'tür.

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
| T0.8 — CI keşfi | [PR #8](https://github.com/EnesGoker/Makredo/pull/8), squash merge `2861670cdf668d00504e7ebd0898bdefa926071f` | 2026-11-06 | codex |
