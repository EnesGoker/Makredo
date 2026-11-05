# Devir Teslim (HANDOFF)

Bu dosya projenin **canlı durumudur**. Her ajan görev başında okur, görev sırasında ve sonunda günceller. Kurallar `AGENTS.md` §1–§5'te. Kısa tut: biten görevler tabloda tek satır olarak kalır, ayrıntıları PR'dadır.

**Son güncelleme:** 2026-11-05 · codex · T0.2 başladı

## Durum
- **Faz:** 0 — Kuruluş (T0.1 tamamlandı, T0.2 devam ediyor)
- **Hedef sürüm:** v0.9.0-beta (Faz 3 sonu)
- **Engel:** T0.2 için teknik engel yok. M0.1'in durumu doğrulanmadı; Enes T0.1'in başlamasını açıkça istedi, manuel takip sürüyor.
- **Başlangıç istisnası:** Enes'in onayıyla yalnızca T0.1'in import ve kit commit'leri doğrudan `main`'e gönderildi. Sonraki bütün görevler branch ve PR ile ilerler; `AGENTS.md` kuralları geçerlidir.

## Aktif görevler
| Görev | Ajan | Branch | Durum | Kaldığı yer / devam notu |
|---|---|---|---|---|
| T0.2 — Ad dönüşümü ve yapı 🔒 | codex | `codex/T0.2-rename-and-layout` | Devam ediyor | Beş proje `src/` ve `tests/` altında `Makredo.*` oldu; tek `Makredo.slnx` kaldı. Düz Release build 0 uyarı/hata. `.slnx` ve Mac build için SDK pini/Windows hedefleme ayarı T0.4'ten öne alındı (ADR-0002); framework'ler hâlâ .NET 8. Sıradaki: `ProductInfo.Name`, göç için eski veri yolunu koruma, regresyon testleri ve format doğrulama. `MainForm`/`AppShellLayoutProfile` kilitli. |

Durum değerleri: `Devam ediyor` · `Yarım kaldı` (limit bitti) · `İncelemede` (PR açık) · `Bekliyor` (Enes'ten cevap bekleniyor).

## Sıradaki
1. **T0.2** Ad dönüşümü ve yapı 🔒
2. **T0.3** Veri klasörü göçü (T0.2'den sonra)
3. **T0.4** .NET 10 (T0.2'den sonra)
4. **T0.5** Platform katmanını ayır

Faz 0 içinde T0.3 ile T0.4, T0.2 bittikten sonra paralel yürüyebilir. Sıra ve bağımlılıklar `docs/ROADMAP.md`'de.

## Enes'in yapacakları (manuel)
- [ ] M0.1 Store geliştirici hesabı ve "Makredo" ad rezervasyonu
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
- T0.1: Mac'te düz `dotnet build -c Release`, mevcut Windows hedeflerinde `NETSDK1100` veriyor. Kalıcı `EnableWindowsTargeting` ayarı T0.4'te; bu import sırasında kaynak yapılandırması değiştirilmedi.
- T0.1: `dotnet format --verify-no-changes`, restore başarısız olduğu için tamamlanamadı. Format doğrulaması Windows hedefleme ayarı sonrası tekrar çalıştırılmalı (T0.4/T0.7).

## Son doğrulama — T0.1
- Kaynak: [`MacroMaster@837fe7585f90102d54062898061481d3a3870a2f`](https://github.com/EnesGoker/MacroMaster/commit/837fe7585f90102d54062898061481d3a3870a2f), GitHub arşivinden geçmişsiz alındı. 133 kaynak dosyasında tek fark `installer/PollyInstaller.iss` içindeki `RepoDir` değerinin `<REPO_DIR>` olması; kişisel yol ilk commit'e girmedi.
- Import commit'i [`bb66ec3`](https://github.com/EnesGoker/Makredo/commit/bb66ec3db2ea6be3176944dfd8b383d4b5c4ebac) ebeveynsizdir; gövdesinde kaynak repo ve tam commit adresi bulunur. İkinci commit başlangıç kitini ve ADR-0001/0002'yi ekler. `AGENTS.md`, `CLAUDE.md`, ROADMAP ve redakte denetim raporu verilen kitle aynıdır.
- `git grep -nE 'Users\\[[:alpha:]]'`: eşleşme yok. Ek Windows/macOS kişisel yol taraması temiz; eski kök `ROADMAP.md`, `ROADMAP-v2.md`, `OPUS-GOREV-00.md`, `AUDIT.md` yok.
- `dotnet build -c Release -p:EnableWindowsTargeting=true`: **0 uyarı, 0 hata**. Düz build: **0 uyarı, 2 NETSDK1100 hatası** (T0.4).
- Core test projesi henüz yok (T0.6). Mevcut `MacroMaster.Tests`, `net8.0-windows`/WinForms hedefli; Mac'te testler çalıştırılmadı. Uygulama veya gerçek girdi gönderen test çalıştırılmadı.
- CI henüz yok (T0.7); Enes'in başlangıç istisnasıyla PR açılmadı, `gh pr checks` uygulanamaz. Windows testleri ve manuel kontroller doğrulanmadı.

## Öğrenilenler ve tuzaklar
- Mac'te `net10.0-windows` testleri **çalışmaz**. Derlenir ama koşmaz. Windows testlerinin sonucu CI'dan alınır.
- Simülasyon modu açıkken oynatma hiçbir girdi göndermez. "Oynatma çalışmıyor" gibi görünen sonuçlarda önce bu ayara bak.
- F12, Windows'ta global kısayol olarak kaydedilemez (Win32 hata 1409, hata ayıklayıcıya ayrılmış).
- Windows'un varsayılan zamanlayıcı çözünürlüğü yaklaşık 15,6 ms. `Task.Delay(1)` yaklaşık 15,6 ms bekler (F-004).
- Denetim raporundaki dosya yolları ve satır numaraları eski `MacroMaster@837fe75` koduna aittir. T0.2 ve T0.5'ten sonra yeni yollara bak.
- T0.1 sonrası kaynak hâlâ `MacroMaster.*` adlarını ve .NET 8 hedeflerini kullanır. Ad/yapı dönüşümü T0.2'de, .NET 10/SDK ve Mac build ayarı T0.4'te, taşınabilir testler T0.6'da yapılacak.

## Tamamlananlar
| Görev | PR | Tarih | Ajan |
|---|---|---|---|
| T0.1 — İçe aktarma | PR yok (Enes onayı); [main'de import + kit](https://github.com/EnesGoker/Makredo/commits/main/) | 2026-11-05 | codex |
