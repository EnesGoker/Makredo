# Devir Teslim (HANDOFF)

Bu dosya projenin **canlı durumudur**. Her ajan görev başında okur, görev sırasında ve sonunda günceller. Kurallar `AGENTS.md` §1–§5'te. Kısa tut: biten görevler tabloda tek satır olarak kalır, ayrıntıları PR'dadır.

**Son güncelleme:** 2026-11-06 · codex · T0.5 başladı

## Durum
- **Faz:** 0 — Kuruluş (T0.1–T0.4 tamamlandı, T0.5 devam ediyor)
- **Hedef sürüm:** v0.9.0-beta (Faz 3 sonu)
- **Engel:** T0.5 için yok; T0.3 squash merge edildi (`1dbe02e`).
- **Başlangıç istisnası:** Enes'in onayıyla yalnızca T0.1'in import ve kit commit'leri doğrudan `main`'e gönderildi. Sonraki bütün görevler branch ve PR ile ilerler; `AGENTS.md` kuralları geçerlidir.

## Aktif görevler
| Görev | Ajan | Branch | Durum | Kaldığı yer / devam notu |
|---|---|---|---|---|
| T0.5 — Platform katmanı | codex | `codex/T0.5-platform-layer` | Devam ediyor | Beş adapter yeni Windows projesine taşındı; dört kullanılmayan Null/Default silindi (grep kanıtı hazır), NullAppLogger korundu. Build 0 uyarı/hata, format ve Mac 65/65 geçti; Core Win32 grep boş. Beş dosyanın gövdeleri namespace/import dışı byte olarak aynı, MainForm/Profile değişmedi. Sıradaki: PR ve son HANDOFF. |

Durum değerleri: `Devam ediyor` · `Yarım kaldı` (limit bitti) · `İncelemede` (PR açık) · `Bekliyor` (Enes'ten cevap bekleniyor).

## Sıradaki
1. **T0.5** Platform katmanını ayır (aktif)
2. **T0.6** Test yapısı
3. **T0.7** CI
4. **T0.8** CI keşfi

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

## Son doğrulama — T0.5
- `dotnet build -c Release`: **0 uyarı, 0 hata**; yedi proje ve mevcut Windows testleri derlendi. `dotnet format --verify-no-changes`: geçti.
- `dotnet test tests/Makredo.Migration.Tests -c Release --no-build`: Mac'te **65 geçti, 0 başarısız, 0 atlanan**. Göç/log/kalıcılık kodu değişmedi; yeni test veya paket eklenmedi.
- `git grep -nEi 'DllImport|LibraryImport|user32|kernel32' -- src/Makredo.Domain src/Makredo.Application src/Makredo.Infrastructure`: **çıktı boş, exit 1 (eşleşme yok)**. Genişletilmiş `NativeLibrary` taraması da boş.
- Silmeden önce kod/proje grep'i `NullHotkeyService`, `NullKeyboardHookSource`, `NullMouseHookSource` için yalnız sınıf tanımını; `DefaultHotkeyConfiguration` için yalnız tanım ve kendi constructor'ını verdi. Harici kullanım yoktu. Kullanılan `NullAppLogger` korundu; kanıt PR'a eklenecek.
- İki hook, `NativeMethods`, `WindowsInputPlaybackAdapter` ve `WindowsHotkeyService` yeni Platform assembly'sinde. Eski/yeni beş kaynakta yalnız namespace/import dönüşümleri var; gövdeler, P/Invoke/struct/delegate tanımları aynı. Tüketicilerde yalnız using değişti. MainForm/Profile, DPI/chrome, göç ve startup akışı değişmedi.
- Platform `net10.0-windows`, `UseWindowsForms=true` (NativeWindow); yalnız Application/Domain'e bağlı. Infrastructure aynı iki Core projeye bağlı kalır; WinForms ve Windows testleri Platform'a doğrudan referans verir. Platform'un Infrastructure/WinForms proje bağımlılığı yoktur.
- Gerçek girdi, uygulama veya Windows testleri çalıştırılmadı. CI T0.7'de kurulacak; native davranış/Windows runtime doğrulanmadı. UI'ye özgü `WindowChromeNative` ve `AppScale` WinForms'ta bırakıldı; genel hook/SendInput davranış düzeltmeleri Faz 1'e ayrıldı.

## Öğrenilenler ve tuzaklar
- Mac'te `net10.0-windows` testleri **çalışmaz**. Derlenir ama koşmaz. Windows testlerinin sonucu CI'dan alınır.
- Simülasyon modu açıkken oynatma hiçbir girdi göndermez. "Oynatma çalışmıyor" gibi görünen sonuçlarda önce bu ayara bak.
- F12, Windows'ta global kısayol olarak kaydedilemez (Win32 hata 1409, hata ayıklayıcıya ayrılmış).
- Windows'un varsayılan zamanlayıcı çözünürlüğü yaklaşık 15,6 ms. `Task.Delay(1)` yaklaşık 15,6 ms bekler (F-004).
- Denetim raporundaki dosya yolları ve satır numaraları eski `MacroMaster@837fe75` koduna aittir. T0.2 ve T0.5'ten sonra yeni yollara bak.
- `.slnx` için .NET 10 SDK pini (`latestFeature`) ve Mac Windows hedefleme ayarı T0.2'de öne alındı (ADR-0002); framework ve test paketleri T0.4'te .NET 10'a taşındı. xUnit framework v2'de kalır; VSTest adapter major sürümünün framework major sürümüyle eşleşmesi gerekmez.
- Göç logger/store kurulmadan önce tamamlanmalı; hata sonrası boş final klasörle devam etmek retry'ı bozar. `AppStoragePaths.CreateDefault()` yalnız Makredo yolunu çözer, WinForms kaynak yolunu `LegacyStoragePaths.DirectoryName` ile kurar.
- Yaş tek başına staging sahipliği kanıtı değildir: bitişik exclusive lease aktif kopyayı yaşından bağımsız korur. Silme öncesi kaynak/hedef root ve alt bağlantıları da kontrol edilir; belirsizlikte temizlik yapılmaz.
- Kütüphane durumu mutlak dosya yolları tutar; sadece byte kopyalama favori/son kullanım kayıtlarının UI tarafından budanmasına yol açar. Bu yüzden yalnız kopyadaki iç makro referansları yeniden köklendirilir; kaynak JSON değişmez.
- T0.6, `tests/Makredo.Migration.Tests` testlerini ve UnixTheory helper'ını Core'a taşımalı; Infrastructure `InternalsVisibleTo("Makredo.Migration.Tests")` test seam'ini de yeni assembly adına güncellemeli.
- `.gitattributes` ve renormalizasyon T0.2'de tamamlandı; T0.7'nin işi değil. Index LF saklar, kapsanan çalışma dosyaları CRLF ve ikon/PNG binary kalır. ADR-0003–0010 yol haritasına ayrılmıştır; göç kararı ADR-0011'dir.

## Tamamlananlar
| Görev | PR | Tarih | Ajan |
|---|---|---|---|
| T0.1 — İçe aktarma | PR yok (Enes onayı); [main'de import + kit](https://github.com/EnesGoker/Makredo/commits/main/) | 2026-11-05 | codex |
| T0.2 — Ad dönüşümü ve yapı | [PR #1](https://github.com/EnesGoker/Makredo/pull/1), squash merge `73dd5c0` | 2026-11-06 | codex |
| T0.4 — .NET 10 | [PR #2](https://github.com/EnesGoker/Makredo/pull/2), squash merge `e1483b8` | 2026-11-06 | codex |
| T0.3 — Veri klasörü göçü | [PR #3](https://github.com/EnesGoker/Makredo/pull/3), squash merge `1dbe02ed93abdba0ac89de920d7207352dea5faf` | 2026-11-06 | codex |
