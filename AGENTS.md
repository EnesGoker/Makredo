# Makredo — Ajan Kuralları

Bu dosya Codex ve Claude Code için **ortak ve tek** kural kaynağıdır. Codex bu dosyayı doğrudan okur, Claude Code ise `CLAUDE.md` üzerinden içe aktarır. Kuralları sadece Enes'in onayıyla değiştir.

## Proje
Makredo, Windows için makro kaydedici ve oynatıcıdır. C#, .NET 10, WinForms ile yazılıyor ve MIT lisanslı. Yayıncı Enes Göker. Önce v0.9.0-beta, ardından v1.0.0 çıkacak.

- Plan: `docs/ROADMAP.md`
- Canlı durum: `docs/HANDOFF.md`
- Bulgular (F-xxx): `docs/audit/AUDIT-2026-11-05.md`
- Kararlar: `docs/adr/`

İşi iki ajan sırayla yapıyor: limiti biten çekilir, diğeri devam eder. Senin işini başka bir ajan bağlam olmadan devralabilmeli. Aşağıdaki kuralların çoğu bu yüzden var.

## Ajan kimliği
- Claude Code isen kimliğin `claude`, branch önekin `claude/`.
- Codex isen kimliğin `codex`, branch önekin `codex/`.

## 1. Oturum başında
1. `docs/HANDOFF.md` dosyasını oku. Claude'da bu dosya otomatik yüklenir.
2. `git fetch --all --prune` ve `git branch -r` çalıştır. `origin/*/T*` branch'leri başlamış görevlerdir.
3. Enes bir görev söylemediyse HANDOFF'taki "Sıradaki" listesinin ilk maddesini al.
4. Görevin `docs/ROADMAP.md` içindeki bölümünü ve ilgili F-xxx bulgularını oku.

## 2. Görevi sahiplenme
- Branch adı `<önek><görev-id>-<kısa-ad>` biçiminde olur, örneğin `codex/T1.2-release-held-inputs`.
- **Uzakta o görevin branch'i varsa görev alınmıştır.** HANDOFF'ta yarım kaldığı yazıyorsa o branch'ten devam et, yazmıyorsa başka bir görev al.
- İlk commit'te HANDOFF'taki "Aktif görevler" tablosuna kendi satırını ekle: görev, ajan, branch, durum.

## 3. Çalışırken: limit her an bitebilir
- **Her anlamlı adımdan sonra commit ve push yap.** Yerelde bekleyen iş, limit bittiğinde kaybolmuş iştir.
- Commit mesajları ne yapıldığını anlatsın. Devralan ajanın ilk kaynağı `git log`.
- Büyük adımlarda HANDOFF'taki satırının "Kaldığı yer" sütununu güncelle: ne bitti, sıradaki adım ne, dikkat edilecek ne.
- Paylaşılan branch'te `push --force` ve geçmiş yeniden yazma yasak.
- Kapsam dışında bir sorun görürsen düzeltme. HANDOFF'taki "Sonraya" listesine yaz.

## 4. Görev bitince (Definition of Done)
1. ROADMAP'teki kabul kriterleri karşılanmış olmalı.
2. `dotnet build -c Release` 0 uyarı ve 0 hata vermeli.
3. Çekirdek testler yerelde geçmeli. CI'daki bütün işler yeşil olmalı: `gh pr checks`.
4. Her hata düzeltmesinin bir regresyon testi, her yeni mantığın bir birim testi olmalı.
5. Kullanıcıya görünen değişiklikler `CHANGELOG.md` içindeki `[Unreleased]` bölümüne yazılmalı.
6. Yeni bir karar alındıysa `docs/adr/` altına ADR yazılmalı.
7. PR açılmalı. Başlık Conventional Commit formatında olmalı, örneğin `fix(playback): release held inputs on stop (F-001)`. Gövde şablonu:
   - **Ne yapıldı**
   - **Nasıl doğrulandı:** komutlar ve sonuçları
   - **Doğrulanmayanlar:** dürüstçe yaz. Çalıştırılmayan test geçmiş sayılmaz.
   - **Ekran görüntüsü:** arayüz değiştiyse CI artifact linki
8. HANDOFF güncellenmeli:
   - Kendi satırını "İncelemede" yap ve PR linkini ekle.
   - "Sıradaki" listesini güncelle.
   - Öğrendiğin önemli bir tuzak varsa "Öğrenilenler"e bir satır ekle.

PR'ı Enes squash merge ile birleştirir. `main`'e doğrudan push yasak.

## 5. Yarım kalmış görevi devralma
1. `git checkout <branch>`, ardından `git log --oneline -20`.
2. HANDOFF'taki "Kaldığı yer" notunu oku.
3. Satırdaki ajan adını kendi adınla değiştir, notu yerinde bırak.
4. Kaldığı yerden devam et. Öncekinin yaptığını baştan yazma.

## 6. Kesin kurallar
- Ürün kararını kendin verme. ROADMAP'te bir varsayılan varsa onu uygula, yoksa HANDOFF'taki "Enes'e sorular"a yaz ve kararsız kısmı atla.
- **Geliştiricinin bilgisayarında gerçek tuş veya fare girdisi gönderen bir test ya da uygulama çalıştırma.** Bu tür testler sadece CI'da ya da Enes'in açıkça istediği durumda çalışır.
- `%LOCALAPPDATA%\Makredo` ve `%LOCALAPPDATA%\Polly` klasörlerine dokunacaksan önce yedek al. Gerçek kullanıcı makrolarını repoya ve test dosyalarına koyma.
- `MainForm` ve `AppShellLayoutProfile` dosyalarında aynı anda sadece bir görev çalışır. HANDOFF'ta bu dosyalara dokunan aktif bir görev varsa bekle.
- Koddaki yorumlara körü körüne güvenme ("enterprise-grade" gibi). Davranışı koddan ve testten doğrula.
- Bağımlılık eklemek ya da çıkarmak gerekiyorsa gerekçesini PR'da yaz.
- Bu dosyayı (AGENTS.md) ve `CLAUDE.md`'yi sadece Enes isterse değiştir. Canlı durum her zaman `docs/HANDOFF.md`'ye yazılır.

## 7. Komutlar
Mac ve Linux'ta Windows hedefleri derlenir ama Windows testleri çalışmaz:
```bash
dotnet build -c Release                              # 0 uyarı olmalı
dotnet test tests/Makredo.Core.Tests -c Release      # her yerde çalışır
dotnet format --verify-no-changes                    # format kontrolü
```
Windows'ta ya da CI'da:
```bash
dotnet test -c Release                               # Core + Windows testleri
```
CI durumu: `gh pr checks` ve `gh run view --log-failed`.

Faz 0 bitene kadar proje yolları eski adlarla (`MacroMaster.*`) olabilir. ROADMAP'teki T0 görevlerine bak.

## 8. Mimari
```
src/Makredo.Domain              net10.0          modeller, enum'lar (bağımlılık yok)
src/Makredo.Application         net10.0          servisler, arayüzler (Domain)
src/Makredo.Infrastructure      net10.0          kalıcılık, log (Application)
src/Makredo.Platform.Windows    net10.0-windows  hook, SendInput, kısayol (Application)
src/Makredo.WinForms            net10.0-windows  arayüz ve composition root (hepsi)
tests/Makredo.Core.Tests        net10.0          Domain/Application/Infrastructure
tests/Makredo.Windows.Tests     net10.0-windows  Platform, WinForms ve layout
```
- Bağımlılık yönü içe doğrudur: Domain ← Application ← Infrastructure / Platform ← WinForms.
- Test edilebilir mantık Core'da olur. Win32 ve arayüz kodu ince bir adaptör katmanı olarak kalır.
- Zaman `TimeProvider` üzerinden alınır, `DateTime.UtcNow` ya da `Task.Delay` doğrudan kullanılmaz. Testlerde `FakeTimeProvider` kullanılır.

## 9. Kod standartları
- Kod, yorum, commit ve PR İngilizce yazılır. Kullanıcıya dönük dokümanlar önce Türkçe, sonra İngilizce yazılır (`README.md` / `README.en.md`).
- Arayüz metinleri Faz 5'ten sonra sadece resource dosyalarında olur. O zamana kadar Türkçe karakterler doğru yazılır ("adi" değil "adı").
- `Nullable` açık, analyzer'lar açık, uyarılar hata sayılır. Yeni bir uyarıyı bastırmak için PR'da gerekçe yazılır.
- `async void` sadece olay işleyicilerde kullanılır. `.Result` ve `.Wait()` yasak. Her uzun işlem `CancellationToken` alır.
- `IDisposable` nesneler sahibinde dispose edilir. GDI nesneleri `using` ile kullanılır.
- Toplamlar ve süreler için `long` kullanılır. Dışarıdan gelen her değer güven sınırında doğrulanır.
- Testler xUnit `Assert` kullanır, tablo testleri `[Theory]` ile yazılır, zaman testleri sabit `sleep`'e dayanmaz.

## 10. Test stratejisi (yerel sanal makine yok)
- **Core testleri** her yerde çalışır. İş mantığının çoğu burada test edilir.
- **Windows testleri** CI'da `windows-latest` (x64) ve `windows-11-arm` makinelerinde çalışır.
- **Arayüz:** saf layout testleri ve CI'ın ürettiği ekran görüntüleri.
- **Manuel kontrol noktaları (K1–K3):** Enes Azure'da ya da gerçek bir bilgisayarda yapar. Ajan bunları yapmış gibi yazmaz.
