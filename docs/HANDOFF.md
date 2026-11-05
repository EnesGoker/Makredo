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
| T0.3 — Veri klasörü göçü | codex | `codex/T0.3-data-migration` | Devam ediyor | Transactional staging/rename, marker ve WinForms çağrısı tamam; Mac'te 26/26 göç vakası geçti, Release build 0 uyarı/hata ve format geçti. Son incelemede library-state favori/son kullanım yollarının mutlak olduğu görüldü; kopyadaki Polly/macros referanslarını yeni Makredo/macros köküne uyarlama ve regresyonları sıradaki. Kaynak/gerçek kullanıcı verileri korunuyor; gerçek veri klasörlerine erişilmedi. |

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

## Son doğrulama — T0.4
- `dotnet build -c Release`: **0 uyarı, 0 hata**, 67 test net10.0-windows için derlendi. `dotnet format --verify-no-changes`: geçti.
- `dotnet list package --vulnerable --include-transitive`: NuGet kaynağında beş projenin hiçbirinde bilinen açık bulunmadı. Newtonsoft.Json doğrudan referansı ve restore edilmiş test grafiğinden kalktı. Test SDK **18.11.0**, xUnit **2.9.3**, VSTest adapter **4.0.1**.
- Mevcut `ProductIdentityTests.cs`, geçici dış `net10.0` xUnit harness'ında derlenmiş .NET 10 Domain/Application/Infrastructure assembly'leri ve yeni paketlerle Mac'te **1/1 geçti**. Windows suite çalıştırılmadı (T0.7); Core projesi henüz yok (T0.6). Denetimdeki iki eski kırmızı test değiştirilmedi.
- Yeni analyzer suppression yalnız `src/Makredo.WinForms/Makredo.WinForms.csproj` içindeki **WFO1000**. `EventListControl` painting metodunda iki CS8602, `ConfigureGrid`'in oluşturduğu sabit `#`/`Type` kolonlarına `!` ile giderildi; yeni çalışma zamanı dalı yok. Önceki CA1859/CA1707 ve `8.0-recommended` analyzer profili değişmedi.
- `global.json`, `Directory.Build.props`, `AppStoragePaths`, `Migration/`, Persistence ve kurallar değişmedi; T0.3'e müdahale edilmedi. 67 test korundu. Uygulama, Windows testi veya gerçek girdi gönderen bir test çalıştırılmadı.
- [PR #2](https://github.com/EnesGoker/Makredo/pull/2) içinde T0.7 için ayrı Windows izleme listesi var: SystemAware/manuel DPI ölçekleme, çizim/kolon varsayımları, x64/ARM64 P/Invoke/INPUT boyutları ve hook/hotkey yaşam döngüsü, analyzer suppression, güncel test adapter discovery/kapanışı. CI henüz yok (T0.7).

## Öğrenilenler ve tuzaklar
- Mac'te `net10.0-windows` testleri **çalışmaz**. Derlenir ama koşmaz. Windows testlerinin sonucu CI'dan alınır.
- Simülasyon modu açıkken oynatma hiçbir girdi göndermez. "Oynatma çalışmıyor" gibi görünen sonuçlarda önce bu ayara bak.
- F12, Windows'ta global kısayol olarak kaydedilemez (Win32 hata 1409, hata ayıklayıcıya ayrılmış).
- Windows'un varsayılan zamanlayıcı çözünürlüğü yaklaşık 15,6 ms. `Task.Delay(1)` yaklaşık 15,6 ms bekler (F-004).
- Denetim raporundaki dosya yolları ve satır numaraları eski `MacroMaster@837fe75` koduna aittir. T0.2 ve T0.5'ten sonra yeni yollara bak.
- `.slnx` için .NET 10 SDK pini (`latestFeature`) ve Mac Windows hedefleme ayarı T0.2'de öne alındı (ADR-0002); framework ve test paketleri T0.4'te .NET 10'a taşındı. xUnit framework v2'de kalır; VSTest adapter major sürümünün framework major sürümüyle eşleşmesi gerekmez.
- T0.3, `Composition/Migration/LegacyStoragePaths` sabitini göç kaynağı olarak kullanmalı; mevcut default veri yolu regresyonunu yeni kopyala/bir-kez davranışına göre güncellemeli. `.gitattributes` ve `git add --renormalize .` T0.2'de tamamlandı; T0.7'nin işi değil. Git index'i metinleri LF saklar; C#/proje/props/solution/resx/installer dosyaları çalışma ağacına CRLF ile çıkar, ikon/PNG binary kalır.

## Tamamlananlar
| Görev | PR | Tarih | Ajan |
|---|---|---|---|
| T0.1 — İçe aktarma | PR yok (Enes onayı); [main'de import + kit](https://github.com/EnesGoker/Makredo/commits/main/) | 2026-11-05 | codex |
| T0.2 — Ad dönüşümü ve yapı | [PR #1](https://github.com/EnesGoker/Makredo/pull/1), squash merge `73dd5c0` | 2026-11-06 | codex |
| T0.4 — .NET 10 | [PR #2](https://github.com/EnesGoker/Makredo/pull/2), squash merge `e1483b8` | 2026-11-06 | codex |
