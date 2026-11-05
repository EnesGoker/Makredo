# Devir Teslim (HANDOFF)

Bu dosya projenin **canlı durumudur**. Her ajan görev başında okur, görev sırasında ve sonunda günceller. Kurallar `AGENTS.md` §1–§5'te. Kısa tut: biten görevler tabloda tek satır olarak kalır, ayrıntıları PR'dadır.

**Son güncelleme:** 2026-11-06 · codex · T0.2 incelemede ([PR #1](https://github.com/EnesGoker/Makredo/pull/1))

## Durum
- **Faz:** 0 — Kuruluş (T0.1 tamamlandı, T0.2 incelemede)
- **Hedef sürüm:** v0.9.0-beta (Faz 3 sonu)
- **Engel:** T0.3/T0.4 için T0.2 PR'ının birleşmesi bekleniyor.
- **Başlangıç istisnası:** Enes'in onayıyla yalnızca T0.1'in import ve kit commit'leri doğrudan `main`'e gönderildi. Sonraki bütün görevler branch ve PR ile ilerler; `AGENTS.md` kuralları geçerlidir.

## Aktif görevler
| Görev | Ajan | Branch | Durum | Kaldığı yer / devam notu |
|---|---|---|---|---|
| T0.2 — Ad dönüşümü ve yapı 🔒 | codex | `codex/T0.2-rename-and-layout` | İncelemede | [PR #1](https://github.com/EnesGoker/Makredo/pull/1). Yapı, merkezi ürün adı ve `.gitattributes`/renormalizasyon tamam; son format/Release build geçti. 67 test derlendi, taşınabilir regresyon 1/1 geçti, `Makredo.exe` doğrulandı. Windows suite/manuel arayüz doğrulanmadı; CI henüz yok. Enes squash merge yapacak. `MainForm`/`AppShellLayoutProfile` kilidi PR birleşene kadar sürer. |

Durum değerleri: `Devam ediyor` · `Yarım kaldı` (limit bitti) · `İncelemede` (PR açık) · `Bekliyor` (Enes'ten cevap bekleniyor).

## Sıradaki
1. **T0.3** Veri klasörü göçü (T0.2 PR'ı birleştikten sonra)
2. **T0.4** .NET 10 (T0.2 PR'ı birleştikten sonra; SDK pini ve Mac hedefleme ayarı zaten yapıldı, framework/paket dönüşümü kaldı)
3. **T0.5** Platform katmanını ayır
4. **T0.6** Test yapısı

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
- Yeni kapsam dışı bulgu yok. T0.1'deki Mac build/format engelleri T0.2'de giderildi.

## Son doğrulama — T0.2
- `dotnet build -c Release`: **0 uyarı, 0 hata**; mevcut 62 test ve 5 yeni regresyon derlendi. `dotnet format --verify-no-changes`: geçti. 124 C# dosyasında CRLF/boşluk düzeni ayrı commit'te normalleştirildi.
- `.gitattributes` eklendi ve `git add --renormalize .` çalıştırıldı; format kontrolü ve Release build yeniden geçti (**0 uyarı, 0 hata**). Index LF/çalışma ağacı CRLF kuralları doğrulandı; kaynak içerikleri satır sonları dışında aynı, ikon byte olarak aynı kaldı.
- `tests/Makredo.Tests/ProductIdentityTests.cs`, geçici dış `net8.0` xUnit harness'ında derlenmiş Release Domain/Application/Infrastructure assembly'leriyle Mac'te **1/1 geçti**. Dört yeni Windows regresyonu ve mevcut Windows suite çalıştırılmadı; Core projesi T0.6'da ayrılacak. Denetimdeki iki eski kırmızı test T0.6'ya bırakıldı.
- `dotnet publish src/Makredo.WinForms/Makredo.WinForms.csproj -c Release -r win-x64 --self-contained false -o <geçici-çıktı>`: geçti; `Makredo.exe` ve Windows PE/x64 başlığı doğrulandı. Exe/uygulama veya gerçek girdi gönderen test çalıştırılmadı.
- `git grep -in "macromaster\|polly" -- src tests installer`: yalnız `Composition/Migration/LegacyStoragePaths.cs` ve `tests/Makredo.Tests/Migration/AppStoragePathsTests.cs` eski veri klasörü için eşleşiyor. Mevcut veri yolu T0.3'e kadar korunuyor; gerçek kullanıcı verisine dokunulmadı.
- Tek `Makredo.slnx`, beş geçerli proje/referans, `Makredo.*` assembly adları, değişmeyen ikon içeriği ve installer yolları doğrulandı. `AGENTS.md`/`CLAUDE.md` değişmedi. Bağımsız incelemede marka/yapı dışında davranış değişikliği bulunmadı.
- `gh pr checks 1`: **no checks reported**; CI T0.7'de kurulacak. Windows çalışma zamanı, manuel arayüz ve ekran görüntüsü doğrulanmadı (ekran görüntüsü altyapısı T0.8).

## Öğrenilenler ve tuzaklar
- Mac'te `net10.0-windows` testleri **çalışmaz**. Derlenir ama koşmaz. Windows testlerinin sonucu CI'dan alınır.
- Simülasyon modu açıkken oynatma hiçbir girdi göndermez. "Oynatma çalışmıyor" gibi görünen sonuçlarda önce bu ayara bak.
- F12, Windows'ta global kısayol olarak kaydedilemez (Win32 hata 1409, hata ayıklayıcıya ayrılmış).
- Windows'un varsayılan zamanlayıcı çözünürlüğü yaklaşık 15,6 ms. `Task.Delay(1)` yaklaşık 15,6 ms bekler (F-004).
- Denetim raporundaki dosya yolları ve satır numaraları eski `MacroMaster@837fe75` koduna aittir. T0.2 ve T0.5'ten sonra yeni yollara bak.
- `.slnx`, içe aktarılan .NET 8 SDK ile okunamıyor. Bu nedenle .NET 10 SDK pini (`latestFeature`) ve Mac Windows hedefleme ayarı T0.2'de öne alındı (ADR-0002); .NET 8 framework/paket dönüşümü hâlâ T0.4'te.
- T0.3, `Composition/Migration/LegacyStoragePaths` sabitini göç kaynağı olarak kullanmalı; mevcut default veri yolu regresyonunu yeni kopyala/bir-kez davranışına göre güncellemeli. `.gitattributes` ve `git add --renormalize .` T0.2'de tamamlandı; T0.7'nin işi değil. Git index'i metinleri LF saklar; C#/proje/props/solution/resx/installer dosyaları çalışma ağacına CRLF ile çıkar, ikon/PNG binary kalır.

## Tamamlananlar
| Görev | PR | Tarih | Ajan |
|---|---|---|---|
| T0.1 — İçe aktarma | PR yok (Enes onayı); [main'de import + kit](https://github.com/EnesGoker/Makredo/commits/main/) | 2026-11-05 | codex |
