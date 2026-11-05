# Makredo — Yol Haritası (v0.9.0-beta → v1.0.0)

Sürüm 3 · 5 Kasım 2026 · Dayanak: `docs/audit/AUDIT-2026-11-05.md` (F-xxx bulguları)

Bu belge **planı** anlatır. Hangi görevin kimde olduğu, nerede kaldığı gibi canlı durum `docs/HANDOFF.md` dosyasındadır. Ajanların çalışma kuralları `AGENTS.md` dosyasındadır.

## 1. Hedef
Makredo'yu gerçek kullanıcılara dağıtılabilecek, güvenli ve kurumsal kalitede bir makro kaydediciye dönüştürmek. Konumlandırma: **"Modern, ücretsiz, açık kaynak ve güvenli bir TinyTask."**
Slogan: **"Bir kez yap, gerisini o yapsın."** / *"Do it once. Let it do the rest."*

## 2. Kilitli kararlar
| Konu | Karar |
|---|---|
| Ad | **Makredo**. Eski adlar MacroMaster ve Polly. Polly adı, aynı adlı bilinen .NET kütüphanesiyle karıştığı için bırakıldı. |
| Repo | Yeni public repo `EnesGoker/Makredo`. Kaynak `MacroMaster@837fe75`, geçmişi taşınmadan tek bir import commit'iyle alınır. Eski repo beta yayınlandıktan sonra private yapılır. |
| Lisans ve yayıncı | MIT · Enes Göker |
| Platform | .NET 10 LTS · Windows 10 22H2 ve Windows 11 · x64 ve ARM64 |
| Dağıtım | GitHub Releases (installer ve portable zip) + Microsoft Store (MSIX, v1.0'da) |
| Arayüz dili | Türkçe ve İngilizce. İşletim sistemi dili Türkçeyse Türkçe, değilse İngilizce açılır; ayarlardan değiştirilebilir. |
| Doküman dili | Önce Türkçe, sonra İngilizce. Kod, commit ve PR İngilizce. |
| Kısayollar | Kayıt: `Ctrl+Shift+Alt+R` · Oynat/Durdur: `Ctrl+Shift+Alt+P` · Acil durdurma: fiziksel `Esc` ya da `Pause` · Ayarlarda F8/F9/F10 hazır seçeneği |
| Test | Yerel sanal makine kullanılmaz. Core testleri her yerde çalışır, Windows testleri CI'da çalışır. Manuel kontroller Azure'da ya da gerçek bilgisayarda yapılır. |
| Telemetri | Yok. Kullanım istatistikleri Store paneli ve GitHub indirme sayılarından alınır. |

## 3. Fazlar ve takvim
| Faz | İçerik | Süre (iş günü) | Çıktı |
|---|---|---|---|
| 0 | Kuruluş: repo, yeni ad, .NET 10, test yapısı, CI | 2–3 | Yeşil CI |
| 1 | Güvenlik ve veri (kritik bulgular) | 5–6 | |
| 2 | Doğruluk ve sağlamlık | 3–4 | |
| 3 | Beta hazırlığı ve yayın | 2–3 | **v0.9.0-beta** |
| 4 | Arayüz: ölçekleme, `MainForm` bölme, erişilebilirlik | 5–7 | |
| 5 | Yerelleştirme (TR/EN) | 1,5–2 | |
| 6 | Çözünürlükten bağımsız oynatma | 4–5 | |
| 7 | Dağıtım: Store, imza, boyut | 2–3 | |
| 8 | Dokümantasyon ve v1.0 | 2 | **v1.0.0** |

Toplam 27–35 iş günü. Beta yaklaşık 3 haftada, v1.0 yaklaşık 6–7 haftada çıkar.

**Görev etiketleri**
- **Boyut:** `[S]` < 2 saat · `[M]` yarım–1 gün · `[L]` 1 günden fazla. L görevleri alt adımlara bölünerek commit'lenir.
- **Ortam:** `Core` her yerde test edilir · `CI-Win` Windows testleri CI'da · `Manuel` Enes kontrol eder.
- **Kilit:** `🔒` işaretli görevler `MainForm` ya da `AppShellLayoutProfile` dosyalarına dokunur. Bunlardan aynı anda sadece biri yürür.
- **Paralel:** `⇉` işaretli görev, aynı fazdaki diğer `⇉` görevleriyle paralel yürüyebilir.

---

## Faz 0 — Kuruluş
Hedef: Makredo adıyla, .NET 10 üzerinde, Mac'te çekirdek testleri çalışan ve CI'ı yeşil olan bir repo.

**Enes'in manuel adımları (T0.1'den önce)**
- **M0.1** Microsoft Store geliştirici hesabını aç (bireysel, ücretsiz) ve Partner Center'da **"Makredo"** adını rezerve et.
- **M0.2** GitHub'da boş ve **public** bir `EnesGoker/Makredo` reposu aç. README ve .gitignore ekleme.
- **M0.3** T0.7 bittikten sonra repo ayarlarını yap:
  - `main` için branch koruması: PR zorunlu, CI geçmeden merge yok, sadece squash merge.
  - Secret scanning ve push protection açık.
  - Dependabot alerts açık.
- **M0.4** Azure for Students hesabını aç. Faz 4'ten önce lazım olacak.

**T0.1 — İçe aktarma** `[S]`
- `MacroMaster@837fe75`'i GitHub'dan al. Geçmişi taşıma; tek bir commit at: `chore: import source from MacroMaster@837fe75` (gövdesinde kaynak repo ve commit'in tam adresi).
- Enes'in verdiği başlangıç kitini aynı klasör yapısıyla ekle: `AGENTS.md`, `CLAUDE.md`, `docs/ROADMAP.md`, `docs/HANDOFF.md`, `docs/audit/AUDIT-2026-11-05.md`.
- `docs/adr/0001-new-repo-and-name.md` ve `docs/adr/0002-dotnet-10.md` dosyalarını bu belgenin 2. bölümündeki kararlardan yaz.
- Eski çalışma klasöründeki `ROADMAP.md`, `ROADMAP-v2.md`, `OPUS-GOREV-00.md` ve redakte edilmemiş `AUDIT.md` repoya **alınmaz**.
- `installer/PollyInstaller.iss` dosyasındaki kişisel mutlak yol **ilk commit'e girmez**. Yerine geçici olarak `<REPO_DIR>` yer tutucusu yazılır, kalıcı çözüm T3.3'te.
- **Kabul:** `main`'de import commit'i ve kit commit'i var. Repoda gerçek kullanıcı adı içeren yol yok (denetim raporundakiler `<kullanıcı>` ile maskeli): `git grep -nE 'Users\\[[:alpha:]]'` boş dönmeli.

**T0.2 — Ad dönüşümü ve yapı** `[M]` 🔒 · F-018, F-032
- Projeleri, namespace'leri ve assembly'leri `Makredo.*` olarak yeniden adlandır. Exe adı `Makredo.exe`.
- Klasörleri `src/` ve `tests/` altına taşı. Sadece `Makredo.slnx` kalsın, `.sln` silinsin.
- Ürün adı tek bir sabitten gelsin (`ProductInfo.Name`). Bu sabit kullanılacak yerler:
  - Win32 pencere başlığı
  - Çizilen başlık çubuğu
  - Hata kutusu başlığı
  - Logger thread adı
  - Kısayol mesaj penceresi
  - Log dosya adı: `makredo-yyyyMMdd.log`
- Boş `<Folder>` öğelerini kaldır. Sekme ve satır sonu bozukluklarını düzelt.
- **Kabul:** `git grep -in "macromaster\|polly" -- src tests installer` sadece göç kodunda (T0.3) sonuç veriyor. Build 0 uyarı veriyor. Davranış değişmedi.

**T0.3 — Veri klasörü göçü** `[S]` · Core
- İlk açılışta `%LOCALAPPDATA%\Makredo` yoksa ve `%LOCALAPPDATA%\Polly` varsa ayarları, kütüphane durumunu ve `macros/` ağacını **kopyala**; kaynak dosyaları değiştirme/silme, `logs/` klasörünü taşıma. Kaynak adı `LegacyStoragePaths.DirectoryName` üzerinden gelsin; varsayılan veri yolu Makredo olsun.
- Önce hedefle aynı parent altında geçici klasöre kopyala, tamamlanınca işaret dosyası yazıp klasörü Makredo adına taşı. Hata/iptalde tamamlanmış hedef oluşmasın; sonraki açılış temiz bir kopyayla yeniden denesin. Mevcut Makredo klasörünün üzerine yazma.
- Göç mantığı taşınabilir Infrastructure içinde olsun; WinForms yalnız logger/depolama oluşturulmadan önce çağırsın.
- Eski varsayılan kısayollar (F8/F9/F10/F12) aynen duruyorsa yeni varsayılanlara geç. Kullanıcı kendi kısayolunu tanımladıysa onu koru. Bu kural T1.5'te uygulanır, burada sadece göç altyapısı kurulur.
- **Kabul:** Geçici klasör testleri Mac'te gerçekten geçiyor: kopyalama, ikinci açılışta tekrar kopyalamama, kaynak koruma, logs dışlama, mevcut hedefi koruma ve yarım kalan kopyayı yeniden deneme. Release build 0 uyarı; format kontrolü geçiyor.

**T0.4 — .NET 10** `[S]`
- Hedefler `net10.0` ve `net10.0-windows` olsun.
- Test paketlerini güncelle. Kullanılmayan `Newtonsoft.Json` paketini kaldır (F-028).
- **T0.2'de tamamlandı:** `global.json` 10.0.x SDK'yı `rollForward: latestFeature` ile sabitliyor; `Directory.Build.props`, Windows dışındaki build'ler için `EnableWindowsTargeting=true` ayarını içeriyor (ADR-0002). T0.4'te yalnız framework ve paket dönüşümü kaldı.
- **Kabul:** Mac'te düz `dotnet build -c Release` 0 uyarıyla geçiyor. `dotnet list package --vulnerable --include-transitive` boş dönüyor.

**T0.5 — Platform katmanını ayır** `[M]`
- Hook'ları, `SendInput`'u, `NativeMethods`'u ve `WindowsHotkeyService`'i `src/Makredo.Platform.Windows` (`net10.0-windows`) projesine taşı.
- `Makredo.Infrastructure` taşınabilir kalsın: sadece kalıcılık ve log.
- Kullanılmayan `Null*` ve `Default*` sınıflarını sil (F-028).
- **Kabul:** Domain, Application ve Infrastructure hiçbir Win32 çağrısı içermiyor. Katman yönü `AGENTS.md`'deki şemaya uyuyor.

**T0.6 — Test yapısı** `[M]` · F-010, F-023
- `MacroMaster.Tests/Program.cs` dosyasını alanlara göre dosyalara böl. Hedef projeler: `tests/Makredo.Core.Tests` ve `tests/Makredo.Windows.Tests`.
- Özel `Expect` katmanını kaldırıp xUnit `Assert` kullan. Tablo testlerini `[Theory]` ile yaz.
- Kırmızı iki testi düzelt:
  - `"adayi"` → `"adayı"`
  - Başlık çubuğu beklentisini kodla uyumlu yap. Doğru değer hangisiyse onu seç ve gerekçesini PR'da yaz.
- **Kabul:** 62 testin hepsi yerinde, hiçbiri silinmedi. Core testleri Mac'te geçiyor. Hiçbir test sabit `Task.Delay` ile beklemiyor; zaman beklentileri `TimeProvider` ya da olay sinyaliyle yapılıyor.

**T0.7 — CI** `[M]` · F-010, F-029
- `.github/workflows/ci.yml` dosyasında üç iş olsun:
  - `ubuntu-latest`: build ve Core testleri.
  - `windows-latest`: tüm testler, `dotnet format --verify-no-changes`, açık paket taraması.
  - `windows-11-arm`: tüm testler.
- `.github/dependabot.yml` (NuGet ve GitHub Actions) ekle.
- **T0.2'de tamamlandı:** `.gitattributes` metin/binary ve CRLF kurallarını tanımlıyor; `git add --renormalize .` uygulandı. T0.7'de yeniden ekleme/normalizasyon işi yok.
- PR şablonu ekle (`AGENTS.md` §4'teki başlıklarla).
- **Kabul:** Bir test PR'ında üç işin hepsi yeşil. T0.2'de yapılan satır sonu normalizasyonu `.gitattributes` ile korunuyor.

**T0.8 — CI keşfi** `[S]` · ADR-003
- GitHub'ın Windows makinesinde iki şeyi dene:
  - (a) Bir WinForms formu açıp PNG ekran görüntüsü alınabiliyor mu?
  - (b) Low-level hook kurup `SendInput` ile gönderilen girdi yakalanabiliyor mu?
- Sonucu `docs/adr/0003-test-and-ci-strategy.md` dosyasına yaz.
- (a) çalışıyorsa ekran görüntüsü işini kur ve görüntüleri artifact olarak yükle.
- **Kabul:** ADR'de iki sorunun cevabı ve kanıtı (workflow linki) var.

---

## Faz 1 — Güvenlik ve veri
Hedef: Kritik bulguların hepsinin kapanması. Tuş takılı kalmamalı, makro her zaman durdurulabilmeli, kayıt kaybolmamalı.

**T1.1 — Durum makinesi ve tek uçuş** `[M]` · Core · ⇉ · F-013
- `TryTransitionTo` sadece açıkça izin verilmişse aynı duruma geçişe `true` dönsün.
- `Start`, `Play` ve `Stop` tek uçuşlu olsun (`SemaphoreSlim` ya da kilit içinde durum kontrolü). Abonelik iki kez eklenmesin.
- `StateChanged` olayı tutarlı bir sırayla yayımlansın.
- **Kabul:** Denetimdeki R1, R2, R4 ve R9 senaryoları kalıcı test olarak eklendi ve geçiyor.

**T1.2 — Basılı girdileri bırakma** `[M]` · Core + CI-Win · ⇉ · F-001
- Oynatma sırasında başarıyla gönderilen KeyDown ve ButtonDown olaylarını takip et.
- Durdurma, iptal, hata, normal bitiş ve pencere kapanmasında hâlâ basılı olanlar için KeyUp ve ButtonUp gönder.
- Genel bir "tüm tuşları bırak" işlemi ekleme. Simülasyon modunda gerçek bırakma gönderilmesin.
- **Kabul:** R3 testi geçiyor (LShift durdurma sonrası basılı kalmıyor). Beş çıkış yolunun her biri için test var.

**T1.3 — Hook'ları ayrı thread'e taşı** `[M]` · CI-Win · ⇉ · F-008
- Klavye ve fare hook'ları kendi mesaj döngüsü olan özel bir thread'de çalışsın.
- Callback sadece olayı bir kanala (`Channel<T>`) yazıp hemen dönsün. İşleme tüketici tarafında yapılsın.
- Zaman damgası için hook'un kendi `time` alanını kullan.
- **Kabul:** Arayüz thread'i bloke edildiğinde hook callback'leri gecikmiyor. CI'da bunu gösteren bir test var; T0.8 sonucuna göre gerçek hook ya da sahte kaynakla.

**T1.4 — Enjekte girdi** `[S]` · Core + CI-Win · F-016
- Uygulamanın kendi gönderdiği girdileri `dwExtraInfo` imzasıyla işaretle. Bunlar **asla** kaydedilmesin.
- `LLKHF_INJECTED` ve `LLMHF_INJECTED` bayraklarını oku.
- Başka programların enjekte ettiği girdiler (ekran klavyesi gibi) varsayılan olarak kaydedilsin. Ayarlardan kapatılabilsin.
- **Kabul:** Testler kendi girdimizin hiçbir durumda kayda girmediğini doğruluyor. Ayar çalışıyor.

**T1.5 — Kısayol sistemi** `[M]` · CI-Win · 🔒 · F-002, F-015
- Yeni varsayılanlar: kayıt `Ctrl+Shift+Alt+R`, oynat/durdur `Ctrl+Shift+Alt+P`. Kısayol ayarları için global kısayol yok (F12 Windows'ta zaten kaydedilemiyor). F8/F9/F10 ayarlarda hazır seçenek olarak dursun.
- `MOD_NOREPEAT` kullanılsın. Her kısayol **bağımsız** kaydedilsin; biri başarısız olursa diğerleri düşmesin.
- Kayıt hatası kullanıcıya görünür olsun: engellemeyen bir uyarı bandı.
- Kayıt ve oynatma kısayollarının tuş basışları kayda girmesin. TinyTask'ın bilinen hatası buydu.
- T0.3'teki göç kuralını uygula: eski varsayılanlar aynen duruyorsa yenilerine geç.
- **Kabul:** Bir kısayol çakıştığında diğerleri çalışıyor ve kullanıcı uyarı görüyor. Basılı tutulan kısayol kaydı açıp kapatmıyor (denetimdeki T2 senaryosu).

**T1.6 — Acil durdurma** `[M]` · Core + CI-Win · ADR-004 · F-002
- Oynatma sürerken hook üzerinden gelen **fiziksel** `Esc` ya da `Pause` tuşu oynatmayı durdursun. Odak hangi penceredeyse ve hangi modifier basılı olursa olsun çalışsın.
- Makronun kendi gönderdiği `Esc` (enjekte) durdurmayı tetiklemesin.
- Durdurmadan sonra T1.2'deki bırakma adımı çalışsın.
- Bağımlılık: T1.2, T1.3, T1.4.
- **Kabul:** Testler şunları kanıtlıyor: Ctrl basılıyken Esc oynatmayı durduruyor, enjekte Esc durdurmuyor, durdurmadan sonra basılı tuş kalmıyor.

**T1.7 — Oturum belgesi ve kaydedilmemiş değişiklikler** `[M]` · Core · 🔒 · F-003
- Oturum durumunu `MainForm`'dan çıkarıp test edilebilir bir `SessionDocument` sınıfına taşı. "Değişti" bayrağı orada tutulsun.
- Kaydedilmemiş değişiklik varsa şu durumlarda Kaydet / Kaydetme / İptal sorusu çıksın: kapatma, yeni kayıt, yükleme, temizleme.
- Kütüphanede tek tık sadece seçsin. Yükleme çift tık ya da Enter ile yapılsın.
- Kayıt bittiğinde otomatik bir kurtarma dosyası yazılsın. Uygulama çökerse bir sonraki açılışta geri yükleme teklif edilsin.
- **Kabul:** `SessionDocument` testleri dört yolu da kapsıyor. Denetimdeki T4 senaryosu artık soru soruyor.

**T1.8 — Tek kopya** `[S]` · CI-Win · F-014
- Adlandırılmış bir `Mutex` kullan. İkinci açılışta mevcut pencere öne gelsin ve ikinci süreç kapansın.
- **Kabul:** İki kez açma testinde sadece tek süreç kalıyor.

---

## Faz 2 — Doğruluk ve sağlamlık

**T2.1 — Zamanlama motoru** `[L]` · Core + CI-Win · ADR-005 · F-004
- `TimeProvider` enjekte et. Oynatma, kayıttaki zaman çizelgesine göre **mutlak hedef zamanlara** bekleyerek ilerlesin (`Stopwatch` tabanlı) ve sapmayı telafi etsin.
- Kısa beklemelerde yüksek çözünürlüklü bekleme kullan (`CREATE_WAITABLE_TIMER_HIGH_RESOLUTION`). Gerekirse son milisaniyeyi kısa bir döngüyle bekle.
- Hız çarpanı ve "Gerçek zamanlı" seçeneği bu motorla tutarlı çalışsın.
- **Kabul:**
  - `FakeTimeProvider` ile yazılan deterministik testler Mac'te geçiyor.
  - CI'da Windows doğruluk testi geçiyor: 500 × 1 ms en fazla 1,3×, 100 × 10 ms en fazla 1,1×.
  - Denetimdeki R8 tablosu yeniden ölçülüp PR'a eklenmiş.

**T2.2 — Fare kaydı** `[S]` · CI-Win · ⇉ · F-021
- Yan tuşlar (XBUTTON) ve yatay tekerlek (HWHEEL) kaydedilsin ve oynatılsın.
- Ölü `DoubleClick` eşlemesini kaldır.
- İsteğe bağlı "fare hareketini seyrelt" ayarı ekle (varsayılan kapalı, en az X ms ya da Y piksel).
- **Kabul:** Eşleme testleri yeni olay türlerini kapsıyor.

**T2.3 — Dosya doğrulama ve sınırlar** `[M]` · Core · ⇉ · ADR-006 · F-005
- Güven sınırında (dosya yükleme ve içe aktarma) şu sınırları uygula. Varsayılanlar ADR'de gerekçelendirilir:
  - Dosya boyutu en fazla 50 MB
  - Olay sayısı en fazla 500.000
  - Tek gecikme en fazla 24 saat
  - KeyCode 1–254
  - Koordinatlar ±100.000 aralığında
- Süre toplamları `long` olsun (`MacroSession`, `MacroReportGenerator`). Adaptördeki `(ushort)` dönüşümü güvenli olsun.
- Reddedilen dosya için anlaşılır bir mesaj gösterilsin. Arayüz yarım güncellenmesin.
- **Kabul:** R5, R6 ve R7 kalıcı testler olarak eklendi. Her sınır için bir sınır testi var.

**T2.4 — Hata yönetimi ve kapanış** `[S]` · ⇉ · F-017, F-034, S-04
- Global hata yakalayıcılar `Program.Main`'de **ilk iş** olarak kaydedilsin.
- Kapanış sırasında kaydetme başarısız olursa uygulama yarım durumda kalmasın: ya kapansın ya da abonelikleri geri yüklesin.
- `MacroPlaybackService` dispose edilsin.
- Çökme anında logger kuyruğu diske yazılsın (flush).
- **Kabul:** Kapanışta kaydetme hatası ve başlangıç hatası senaryoları için testler var.

**T2.5 — Küçük düzeltmeler** `[S]` · ⇉ · F-028, F-031, F-033
- Varsayılan oturum adında yerel saat kullanılsın.
- Rapor atomik yazılsın.
- Kütüphanede silme Geri Dönüşüm Kutusu'na gönderilsin.
- 18 dosyada tekrarlanan yuvarlak köşe yardımcısını tek bir yere topla.
- **Kabul:** Davranış testleri var, tekrar eden kod kalmadı.

---

## Faz 3 — Beta (v0.9.0-beta)

**T3.1 — Hızlı arayüz düzeltmeleri** `[M]` · 🔒 · F-006 (geçici)
- `FontScale` ile yapılan çift font ölçeklemesini kaldır.
- En küçük pencere boyutu ölçekle birlikte büyüsün.
- Oynatma ayarları paneli kaydırılabilir olsun.
- "Simülasyon modu" her zaman görünür olsun. Açıkken oynatma alanında belirgin bir rozet çıksın. Varsayılan değeri `false`.
- **Kabul:** CI ekran görüntülerinde ve layout testinde 100% ve 125%'te metin/kutu oranı 1,0. Simülasyon ayarı hiçbir boyutta gizlenmiyor.

**T3.2 — Gizlilik** `[M]` · ⇉ · F-009
- İlk açılışta bir gizlilik bilgilendirmesi göster. Her kayıt başlangıcında "Şifre yazmayın" hatırlatması göster ("bir daha gösterme" seçeneğiyle).
- Kayıt göstergesi ekle: bildirim alanında kırmızı simge ve isteğe bağlı küçük bir köşe göstergesi.
- Loglara tuş adı ve olay açıklaması yazılmasın.
- Log saklama: 14 gün ya da toplam 10 MB.
- `PRIVACY.md` (TR ve EN) yaz.
- **Kabul:** Log içeriği testi tuş adı bulunmadığını doğruluyor. Saklama testi var.

**T3.3 — Sürüm, publish ve installer** `[M]` · ⇉ · F-011, F-024, S-05
- Sürüm tek yerden gelsin: `Directory.Build.props` içindeki `<Version>`.
- `scripts/publish.ps1` dosyası `win-x64` ve `win-arm64` için self-contained publish yapsın.
- Installer'ı düzenle:
  - Yollar `{#SourcePath}` ile göreli olsun, sürüm parametre olarak gelsin.
  - Yayıncı "Enes Göker" olsun, yeni bir AppId kullanılsın.
  - `PrivilegesRequired=lowest`: yönetici izni istemeyen, kullanıcı başına kurulum.
  - Mimari başına bir installer: `ArchitecturesAllowed` = `x64compatible` / `arm64`.
  - `MinVersion` tanımlansın. Dil seçenekleri TR ve EN.
- `release.yml`: tag atılınca build, installer'lar, portable zip ve SHA256 üretilsin ve GitHub Release taslağı oluşturulsun. CI'da `ContinuousIntegrationBuild=true` olsun.
- **Kabul:** Tag'den çıkan taslak release'te 2 installer, 2 zip ve checksum dosyası var. Hiçbir dosyada kişisel yol yok.

**T3.4 — Beta dokümanları** `[S]` · ⇉ · F-012
- `README.md` (TR) ve `README.en.md` yaz. İçerik: ne olduğu, indirme, SmartScreen notu, gizlilik özeti, kısayollar, bilinen sınırlamalar (yüksek DPI ve farklı çözünürlükte oynatma v1.0'da).
- `LICENSE` (MIT, Enes Göker), `CHANGELOG.md` (Keep a Changelog), `SECURITY.md`.
- Issue şablonları: hata raporunda Windows sürümü, ölçek, çözünürlük ve Makredo sürümü sorulsun.
- `docs/testing/manual-checklist.md` (K1–K3 listeleri) yaz.

**K1 — Gerçek donanım kontrolü (Enes, gerçek x64 bilgisayar)** · Manuel
- Kontrol listesine göre ilerlenir. Bulunan hatalar issue olarak açılır. Beta engeli olanlar düzeltilmeden T3.5'e geçilmez.

**T3.5 — v0.9.0-beta yayını** `[S]`
- CHANGELOG'u güncelle, `v0.9.0-beta` tag'ini at, GitHub Release'i **pre-release** olarak yayımla (Enes onaylar).
- **Enes:** Eski `MacroMaster` reposunu private yap. İsteğe bağlı: Technopat ve DonanımHaber'de beta test çağrısı yap.

---

## Faz 4 — Arayüz (v1.0)
`refactor/winforms-dpi-responsive` dalı (MacroMaster reposu) **referans** olarak incelenebilir. Doğrudan merge edilmez: ortak atası yok ve çift font ölçeklemesini koruyor.

**T4.1 — `MainForm`'u bölme** `[L]` · 🔒 · F-027
- Kısayol koordinatörünü, kütüphane denetleyicisini, oturum sunucusunu ve layout yönetimini ayrı ve test edilebilir sınıflara taşı.
- Hedef: `MainForm` 800 satırın altına insin.
- Davranış değişmeyecek. Alt adımlarla commit'lenir.
- **Kabul:** Çıkarılan sınıfların testleri var. CI ekran görüntüleri öncesiyle aynı.

**T4.2 — DPI modeli** `[L]` · 🔒 · ADR-007 · F-006
- `PerMonitorV2` modunu ve `AutoScaleMode.Dpi`'ı kullan. `AppScale`'i ve sabit pikselli ölçeklemeyi kaldır.
- Layout profili pencere yeniden boyutlandırıldığında ve `DpiChanged` olayında yeniden hesaplansın.
- **Kabul:** Saf layout testleri 100, 125, 150, 175 ve 200%'te ve en küçük boyutta taşma olmadığını doğruluyor.

**T4.3 — Kendi çizilen kontroller ve DPI** `[M]` × birkaç alt görev
- `ModernSelect`, `ModernScrollBar`, `ModernCheckBox`, `ModernNumericInput`, `TitleBarControl`, `DashboardCard` ve `MacroPreviewMapControl` kontrollerinde `DeviceDpi`, `LogicalToDeviceUnits` ve `DpiChangedAfterParent` kullan.
- Her kontrol ayrı bir commit ya da alt görevdir (T4.3a, T4.3b…). Bu sayede ajanlar arasında rahat devredilir.

**T4.4 — Layout testleri ve ekran görüntüsü regresyonu** `[M]` · CI-Win
- Her formu birkaç boyutta açan Windows testleri yaz. Kontroller:
  - Hiçbir kontrol üst alanının dışına taşmıyor.
  - Metin `TextRenderer.MeasureText` ile ölçüldüğünde kutusuna sığıyor.
  - Kontroller üst üste binmiyor.
- CI her çalıştırmada ekran görüntüsü artifact'ı üretsin.

**T4.5 — Sanal olay listesi** `[M]` · F-020
- Olay listesi `VirtualMode`'da çalışsın. Yükleme sırasında ilerleme gösterilsin.
- **Kabul:** 100.000 olaylı dosya arayüzü dondurmadan yükleniyor. CI'da süre ölçümü var.

**T4.6 — Erişilebilirlik** `[M]` · F-019
- Tüm özel kontrollere `AccessibleName` ve `AccessibleRole` ver.
- Klavye odağı ve odak çerçevesi çalışsın, sekme sırası mantıklı olsun.
- Yüksek kontrast modunda `SystemColors` paletine geçilsin.
- Oynatma düğmeleri erişilebilir türden olsun (`Button` ya da `AccessibleObject` tanımlı).

**K2 — Ölçekleme kontrolü (Enes, Azure ya da gerçek x64 bilgisayar)** · Manuel
- 100%, 125% ve 150% ölçekte, en küçük pencerede ve mümkünse ikinci bir monitöre taşıyarak görsel kontrol.

---

## Faz 5 — Yerelleştirme

**T5.1 — Altyapı** `[M]`
- Metinleri `Strings.resx` (İngilizce, nötr) ve `Strings.tr.resx` dosyalarına taşı.
- Dil seçimi: işletim sistemi arayüz dili `tr` ise Türkçe, değilse İngilizce. Ayarlarda "Sistem / Türkçe / English" seçeneği olsun.
- **Kabul:**
  - İki dosyada da aynı anahtarların bulunduğunu doğrulayan bir test var.
  - Kodda sabit yazılmış arayüz metni kalmadığını tarayan bir test var.
  - Layout testleri iki dilde de geçiyor.

**T5.2 — Hata mesajları** `[M]` · F-030
- Servis katmanı tiplendirilmiş hatalar ya da hata kodları fırlatsın. Arayüz bunları yerelleştirilmiş mesajlara çevirsin.
- Türkçe karakterleri ASCII'ye çevrilmiş metinler ("adi", "bos") kalmasın.

**T5.3 — Installer ve Store metinleri** `[S]`
- TR ve EN installer dil seçenekleri ile Store açıklama taslakları.

---

## Faz 6 — Çözünürlükten bağımsız oynatma

**T6.1 — Koordinat modeli ve format v2** `[S]` · ADR-008
- ADR'yi ve `docs/session-format.md` dosyasını yaz. İçerik: v2 şeması, v1'den v2'ye göç, uyumluluk.
- Format 1.2 desteği eklenmez (F-022). Enes aksini söylerse ayrı görev açılır.

**T6.2 — Kayıt ortamı** `[M]` · Core + CI-Win · F-007
- Kayıt başında monitörleri, her monitörün sınırlarını ve DPI'ını, sanal ekranı ve birincil monitörü kaydet. Koordinatlar fiziksel olsun (PerMonitorV2).
- Her olay için hedef pencere bilgisini sakla: süreç adı ve pencere sınıfı. Pencere başlığı gizlilik nedeniyle varsayılan olarak saklanmaz.
- v2 yazıcı ve v1 okuyucu yaz (göç).
- **Kabul:** v1 dosyaları açılıyor ve kaydedilince v2 oluyor. Gidiş-dönüş testleri var.

**T6.3 — Koordinat eşleme motoru** `[M]` · Core · ⇉
- Saf fonksiyonlarla üç mod: **Mutlak**, **Oransal** (monitöre göre normalize) ve **Pencereye göre**.
- **Kabul:** Şu senaryoların testleri geçiyor: 1920×1080 → 2560×1440, 16:9 → 21:9, çoklu monitör, negatif koordinatlı monitör, karışık DPI.

**T6.4 — Oynatma tarafı** `[M]` · CI-Win · 🔒
- Hareket ve tıklama `SendInput` ile **tek çağrıda** gönderilsin (`ABSOLUTE | VIRTUALDESK`). Bu S-01'i de kapatır.
- Pencereye göre modda hedef pencere bulunamazsa oynatma güvenle dursun ve kullanıcıya nedeni söylensin.
- Oynatmadan önce kayıt ortamı ile mevcut ekran karşılaştırılsın. Fark varsa uyarı ve mod önerisi gösterilsin.
- Mod seçimi mevcut oynatma ayarları paneline eklensin.

**T6.5 — UIPI uyarısı** `[S]` · F-026
- Hedef pencere yönetici olarak çalışıyorsa açık bir uyarı göster ve README'de belgele.

---

## Faz 7 — Dağıtım

**T7.1 — Boyut incelemesi** `[S]` · ADR-009
- Self-contained, framework-dependent ve sıkıştırılmış seçenekleri ölç. Karar ve rakamlar ADR'ye yazılsın.

**T7.2 — MSIX ve Store paketi** `[M]` · ADR-010
- CI'da x64 ve ARM64 içeren bir MSIX bundle üret.
- Uygulamanın hook kullanımı Store politikalarına göre kontrol edilsin. Risk varsa erken görünür olsun.

**T7.3 — Store kaydı** `[S]` + Enes
- Açıklama (TR/EN), ekran görüntüleri, gizlilik politikası linki (`PRIVACY.md`) ve yaş derecelendirmesi. Gönderimi Enes yapar.

**T7.4 — GitHub installer imzası** `[S]`
- SignPath Foundation (ücretsiz, ama yayıncı adı olarak SignPath Foundation görünür) ile imzasız kalıp README'de açıklama arasında karar ver ve ADR-010'a ekle.

---

## Faz 8 — v1.0

**T8.1 — Tam dokümantasyon** `[M]`
- README (TR/EN) ve GIF.
- `docs/architecture.md` (Mermaid diyagramlarıyla), `docs/build-and-release.md`, `CONTRIBUTING.md`, ADR dizini.

**K3 — Son kontrol (Enes, gerçek x64 bilgisayar ve Azure)** · Manuel
- Kontrol listesinin tamamı:
  - Kurulum, güncelleme ve kaldırma
  - Ekran okuyucuyla (Narrator) kısa bir tur
  - Farklı çözünürlükte oynatma (Azure'da RDP ile çözünürlük değiştirerek)

**T8.2 — v1.0.0** `[S]`
- CHANGELOG, tag ve release. Store gönderimini Enes yapar. Repo açıklaması ve konuları (topics) güncellenir.

**Yayın sonrası (Enes)**
- AlternativeTo'ya "TinyTask alternatifi" olarak ekle.
- Technopat, DonanımHaber ve Reddit'te tanıt.
- Store ve GitHub rakamlarını takip et.

---

## Manuel kontrol noktaları
| Nokta | Ne zaman | Nerede | Süre |
|---|---|---|---|
| K1 | Beta öncesi | gerçek x64 bilgisayar | ~1 saat |
| K2 | Faz 4 sonu | Azure ya da gerçek x64 bilgisayar | ~1 saat |
| K3 | v1.0 öncesi | gerçek x64 bilgisayar ve Azure | ~1,5 saat |

## ADR listesi
| No | Konu | Görev |
|---|---|---|
| 0001 | Yeni repo, ad, geçmiş | T0.1 |
| 0002 | .NET 10 | T0.1 |
| 0003 | Test ve CI stratejisi (yerel sanal makine yok) | T0.8 |
| 0004 | Kısayollar ve acil durdurma | T1.6 |
| 0005 | Oynatma zamanlama modeli | T2.1 |
| 0006 | Dosya doğrulama sınırları | T2.3 |
| 0007 | DPI ve ölçekleme modeli | T4.2 |
| 0008 | Koordinat modeli ve format v2 | T6.1 |
| 0009 | Paket boyutu | T7.1 |
| 0010 | Dağıtım ve imzalama | T7.2, T7.4 |

## Bulgu → görev eşlemesi
| Bulgu | Görev | Bulgu | Görev |
|---|---|---|---|
| F-001 | T1.2 | F-018 | T0.2 |
| F-002 | T1.5, T1.6 | F-019 | T4.6 |
| F-003 | T1.7 | F-020 | T4.5 |
| F-004 | T2.1 | F-021 | T2.2 |
| F-005 | T2.3 | F-022 | T6.1 |
| F-006 | T3.1, T4.2–T4.4 | F-023 | T0.6 |
| F-007 | T6.2–T6.4 | F-024 | T3.3 |
| F-008 | T1.3 | F-025 | T0.1 (yeni repo) |
| F-009 | T3.2 | F-026 | T6.5 |
| F-010 | T0.6, T0.7 | F-027 | T4.1 |
| F-011 | T3.3, T7.4 | F-028 | T0.4, T0.5, T2.5 |
| F-012 | T3.4, T8.1 | F-029 | T0.7 |
| F-013 | T1.1 | F-030 | T5.2 |
| F-014 | T1.8 | F-031 | T2.5 |
| F-015 | T1.5 | F-032 | T0.2 |
| F-016 | T1.4 | F-033 | T2.5 |
| F-017 | T2.4 | F-034 | T2.4 |

Şüpheler: S-01 → T6.4 · S-02 → T6.2/K2 · S-03 → T3.3 · S-04 → T2.4 · S-05 → T3.3 · S-06 → K3 (Windows 10 varsa).

## Kapsam dışı (v1.0 sonrası)
Koşullu makrolar ve döngü editörü, görüntü tanıma, zamanlanmış çalıştırma, makroyu tek başına çalışan EXE'ye çevirme, macOS ve Linux sürümü, otomatik güncelleme (Store zaten sağlıyor).
