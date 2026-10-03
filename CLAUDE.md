# PRIZMA — blok bulmaca

Unity 6 (6000.6.0f1) · URP · dikey mobil, hedef Android (Google Play, yurt dışı ağırlıklı).
Block Blast türünde 8×8 blok bulmaca. Arayüz **Türkçe + İngilizce**, diller JSON dosyalarında.

Üç mod: **Klasik** (sonsuz) · **Günlük** (günde bir hedefli bulmaca; süre, seri, rozet, bildirim) ·
**Macera** (100 bölüm, 10 dünya, yol haritası; kristal, ışık karosu, taş, gölge, renk siparişi, saatli blok, buz;
zor bölümler, güçlendiriciler, galibiyet serisi, dünya sandıkları). Hepsinde **Prizma güçleri**.

Sahne `Assets/Scenes/SampleScene.unity` içinde tek bir GameObject var: **`GameRoot`** + `AppController`.
Başka sahne kurulumu yok; bütün hiyerarşi çalışma anında koddan kuruluyor.

**İçindekiler**
1. [Değişmez kurallar](#1-değişmez-kurallar) — her işe başlamadan
2. [İş akışı: build, test, doğrulama](#2-iş-akışı)
3. [Mimari](#3-mimari)
4. [Sistemler](#4-sistemler) — dağıtıcı · güçler · macera · günlük · spektrum · temalar/kayıt · dil · ses · modallar
5. [Tasarım dili](#5-tasarım-dili)
6. [Performans](#6-performans)
7. [Kritik tuzaklar](#7-kritik-tuzaklar)
8. [Durum ve sıradakiler](#8-durum-ve-sıradakiler)

---

## 1. Değişmez kurallar

Her biri bu projede bir kez bozulup bedeli ödendi. Ayrıntısı ilgili bölümde.

| Kural | Neden / nerede |
|---|---|
| **Projede sanat/ses dosyası yok.** Görseller `Raster` ile kodla çizilir, sesler sentezlenir | Aşağıda; istisnalar yalnız font ve üretilen uygulama ikonu |
| **`Core/` Unity'ye bağımlı olmaz** (`UnityEngine` import etmez) | Test ve simülasyon onun sayesinde Unity'siz koşuyor |
| **Çağrı yerinde ham sayı ya da renk yazma** — `Design.cs` token'ı ekle | Tek kaynak |
| **Çağrı yerinde metin yazma** — `Assets/Resources/Lang/*.json` + `Str` | §4.7 |
| **Klasör, dosya, araç çıktısı ve kod adları İngilizce** | Kullanıcı isteği; üreten kodu da öyle yaz |
| **Yazıyla anlatma. Yanıp söndürme.** | §5 arayüz kuralı |
| **Hiçbir sayfaya "KAPAT" butonu koyma** | §4.9 |
| **Ses tınısına dokunmadan önce sor**; eski sesleri koru, değişikliği WAV dizisiyle dinlet | §4.8 |
| **Yüzeyler opak renk**, yarı saydam beyaz değil (Linear renk uzayı) | Tuzak 1 |
| **Temaya bağlı rengi `static readonly` alanda saklama** | §4.6 |
| **Korumaları kaldırma:** `PieceDealer.AssistCeiling`, `ReleaseBuild` kontrolleri, `targetFrameRate` 60 | Tuzaklar 7, 9; §6 |
| **Git: yalnız `main`**, yeni branch açma; `git push origin main` | |
| **Yükleme anahtarı asla repoya girmez** (`%USERPROFILE%\.prizma\`) | §2.1 |

### Sanat/ses dosyası yok — istisnalar
- Bloklar, paneller, çerçeveler, gölgeler ve ikonlar `Raster` ile **kodla çiziliyor**; ses efektleri ve müzik
  `AudioKit` / `MusicPlayer` içinde **sentezleniyor**.
- **Font:** `Assets/Fonts/` — Poppins (Regular / SemiBold / ExtraBold), OFL lisanslı (`Assets/Fonts/OFL.txt`).
  TMP font asset'leri `Assets/Resources/Fonts/` altında (`Design.Load`).
- **Uygulama ikonu:** `Assets/Generated/`. Android ikonu doku varlığı istiyor; `AppIconArt` (Raster) çiziyor,
  `Editor/AppIconGenerator` PNG'ye yazıyor (eksikse Editor açılışında, ya da *PRIZMA → Uygulama İkonunu Yeniden Üret*).
  Kaynak koddur — PNG'yi elle düzenleme.
- **Shader:** `Assets/Resources/Shaders/PrizmaBackdrop.shader` — koddur, varlık değil.
- Yeni görsel gerekirse önce "bunu `Raster` ile çizebilir miyim?" diye sor. Cevap genelde evet.

---

## 2. İş akışı

### 2.1 Build alma — sürüm sürüm `Builds/` altına

**Menü:** *PRIZMA → Android Yayın Build'i (AAB + APK)* (`Editor/ReleaseBuild`). Editor kapalıyken aynısı:
`Unity -batchmode -quit -projectPath <proje> -buildTarget Android -executeMethod BlockPuzzle.EditorTools.ReleaseBuild.BuildAndroid`.

Her sürüm kendi klasöründe, **son başarılı build `-active`** işaretli:

```
Builds/
├── 1.0.0/                  önceki sürüm (yeni build alınınca işareti kalktı)
├── 1.1.0-active/           şu anki sürüm
│   ├── PRIZMA-1.1.0.aab    Google Play'e yüklenen paket
│   ├── PRIZMA-1.1.0.apk    aynı paketten, telefona doğrudan kurulan APK
│   └── build-report.txt    ne kontrol edildi, sonuç
└── 1.2.0-failed/           geçmeyen build: yalnız rapor; aktif sürüme dokunulmaz
```

- **Sürüm adı** Player Settings → Version (`bundleVersion`), **semantik sürüm** `MAJOR.MINOR.PATCH`, ilk yayın
  **1.0.0**. Hata düzeltme / küçük ayar → PATCH (1.0.1) · yeni özellik (dil, dünya, reklam) → MINOR (1.1.0) · büyük
  yenilenme (yeni mod, kayıt formatı) → MAJOR (2.0.0). Yeni bir sürümden önce **elle artır**. Aynı sürüm
  yeniden build alınırsa o sürümün klasörü yenilenir. **versionCode** kendiliğinden: 1 Ocak 2026'dan beri geçen
  dakika (hep artar, dosya tutmaz).
- **AAB** Play'e yüklenir; Play her telefona APK'yı kendi üretir ve kendi anahtarıyla imzalar (Play App Signing).
  **APK** aynı paketten bundletool ile üretilir — telefonda denenen, yüklenenin aynısı.
- **İmza:** yükleme anahtarı `%USERPROFILE%\.prizma\prizma-upload.jks`, ayarı `signing.json` (keystore yolu
  boşsa yanındaki dosya). Projenin ve git'in dışında; **yedeği şart**. Şifreler ProjectSettings'e yazılmaz.
- **Build sonu kontrolleri** (`ReleaseBuild.Checks`): hedef API ≥ 36 · izinler `Checks.Allowed` listesinde ·
  debug imza yok · debuggable değil · 16 KB sayfa (zip + ELF) · oyuna geliştirme derlemesi girmemiş.
  Biri tutmazsa çıktılar silinir, neden rapora yazılır. Bilerek yeni izin geliyorsa (reklam SDK'sı) nedeniyle
  `Allowed`'a ekle — gizlilik metni ve Veri güvenliği formu aynı değişiklikte güncellenir.
- Build öncesi `AppController`'ın bir sınıfa çözüldüğü kontrol edilir, build sırasında "missing script" uyarısı
  yakalanır (tuzak 9).
- **`DevAssemblyFilter`** geliştirme paketlerini (AI Assistant, Pipeline — HTTP sunucusu + Roslyn —, Visual
  Scripting, AI Navigation) yalnız yayın build'inden çıkarır; Editor'de kalırlar. Gerçek kullanımı derlenmiş
  koddaki referanslardan okur — `Assembly-CSharp` kâğıt üstünde her pakete bağlıdır.
- Unity'nin veri gönderimi (donanım istatistikleri, Engine Diagnostics) yayın build'inde kapatılır.
- **Telefona kurarken:** farklı anahtarla imzalı eski bir APK varsa üstüne kurulmaz; önce kaldır (ilerleme silinir).
- Build **gerçek projeden** alınır. Editor açıkken doğrulama gerekiyorsa yalnız **kısa bir yoldaki** kopyadan
  (`C:\pzb` gibi) — uzun yolda Gradle düşer (tuzak 9) — ve iş bitince kopya silinir.
- `Builds/` **yalnız sürüm klasörleri** içerir. Başka çıktı (dinleme WAV'ları, test görüntüleri) oraya konmaz;
  `AudioCheck wav` çıktısı `Tools/AudioCheck/Output/` altında (git dışında).

### 2.2 Unity'siz araçlar (`Tools/`)

| Araç | Ne zaman | Ne yapar |
|---|---|---|
| `CoreHarness` | Denge ya da kural değişince **önce bu** | `<Unity>/Editor/Data/DotNetSdk/dotnet.exe build Tools/CoreHarness -c Release`, sonra `Harness.dll tests` / `balance 40` / `levels 1 60` / `daily 28` / **`perf`** (dağıtım başına süre + bellek; aday ya da terim büyütünce koş) |
| `AudioCheck` | Ses seviyesine ya da tarife dokununca | `wav Tools/AudioCheck/Output/audio_layers` · `measure`: tepe, yükseklik, telefon hoparlörü modeli, limiter. `wav <klasör>`: klipler + oyunun gerçek zamanlamasıyla dizi (before / after / phone presence) + hoparlör simülasyonu — **diziyi dinlet** |
| `compile-check.ps1` | Her kod değişikliğinden sonra | Oyunu (normal + `PRIZMA_AUTOTEST`) ve editor kodunu Unity'nin argümanlarıyla derler; Editor kapalı/odakta değilken de |
| `autotest.ps1` | Arayüz değişikliğinde | Projeyi `%TEMP%`'e kopyalar, `PRIZMA_AUTOTEST` Windows build'i alır, her ekranın görüntüsünü `%TEMP%\prizma_autotest\shots` altına yazar (~5 dk) |
| `theme-check.py` | Palet değişince | Kontrast ve ΔE kuralları (§4.6) |
| `lang-check.py` | Dil dosyası değişince | Anahtarlar, `{0}`'lar, liste uzunlukları, Poppins harf kapsamı (§4.7) |

PowerShell betikleri bu makinede betik izni ister: `powershell -ExecutionPolicy Bypass -File Tools/…`.

**`autotest.ps1` ayrıntıları:** tahtalar gerçek bot hamlelerinden ve elle kurulmuş kayıtlardan geldiği için görüntü ve
durum birlikte doğrulanır; tanım gerçek build'e girmez. **Arayüz değişikliğini tek oranda doğrulama:**
`-SkipBuild -Width 540 -Height 960 -Tag 16x9` (19.5:9 varsayılan 432×936, 20:9 432×960, 16:9 540×960, tablet 720×960).
`-Only adventure` (40_…61_, ~2 dk) · `-Only modals` (80_…92_) · `-Only map` (70_…76_, ~4 dk) · `-Only play` (hazır kayıtla
oyunu **açık bırakır**) · `-Monitor 2` (kullanıcının dikey ikinci ekranı) · `-TopInset 90` (telefon çentiği; masaüstünde
safe area yok). Player log'unda `[PRIZMA] boot/built …` süreleri ve `[Raster]` doku hash'leri var.

### 2.3 Doğrulama yöntemi
- Ekran görüntüsüne güvenme — **durumu koda sor**. `mcp__unity-editor-mcp__eval` ile private alanlara reflection'la
  erişip modeli ve görünümü karşılaştır. Bir kez model 81 derken ekran 0 gösteriyordu; sorun editörün kare
  işlememesiydi (tuzak 3). `eval` 60 sn'de zaman aşımına uğrar; ağır simülasyonu parçala.
- "Görünüş değişmemeli" diyen bir değişikliği **ölçerek** doğrula: önce/sonra autotest görüntülerinin piksel farkı,
  ya da `[Raster]` hash'leri. Botun skoru koşudan koşuya değişir; karşılaştırmayı aynı durumdaki ekranlarda yap.
- Denge değişikliğinden sonra simülasyonla ölç. **Hamle sınırı koy** — sınırsız döngü Unity'yi kilitler.

---

## 3. Mimari

### `Assets/Scripts/Core/` — saf oyun mantığı
Unity'ye hiç bağımlı değil. **Bu ayrımı bozma.**

| Dosya | Görev |
|---|---|
| `BoardModel` | 8×8 tahta + kristal/buz/ışık/saat katmanları. Satır/sütun doluluk sayaçlarını hücre değiştikçe günceller |
| `Rng` | xorshift64*. Tüm durumu tek sayı → kayda girer, platformdan bağımsız. **System.Random'a dönme** |
| `SessionTypes` | Mod/durum/güç/hedef enum'ları, `SessionConfig`, `MoveResult`, `LevelDefinition`, `SessionSnapshot` |
| `LevelGenerator` | Macera bölümü ve günlük bulmaca üretici (§4.3) |
| `Autoplayer` | Bilgisayar oyuncu: bölüm bütçesi, denge ölçümü, otomatik test |
| `PieceShape` / `PieceLibrary` | Şekil + doluluk maskesi (`Contains`) / 37 şekil, ağırlıklı dağılım |
| `ScoreRules` | Puanlama ve combo çarpanı — tüm sayılar burada |
| `GameSession` | Bir tur: tahta, tepsi, skor, seri, şarjlar, güçler, bölüm hedefi, oyun sonu, kaydet/yükle |
| `PieceDealer` | **Akıllı parça dağıtıcı** (§4.1) |

### `Assets/Scripts/Game/` — sunum
- `AppController` kök: canvas + backdrop + ses + ekranları kurar, gezinmeyi yönetir.
- Ekranlar `AppScreen`'den türer. **Sayfalar:** `MainMenuScreen`, `GameScreen`, `LevelSelectScreen` (harita).
  **Modallar** (alt sayfa, §4.9): `SettingsScreen`, `ScoresScreen`, `PauseScreen`, `StatsScreen`, `ThemesScreen`,
  `DailyScreen`, `LevelStartScreen`, `ChestScreen` (`ModalScreens.cs`, `DailyScreen.cs`, `AdventureScreens.cs`).
- `GameScreen` = `GameHud` (moda göre dolan üst şerit) + `PowerBar` (prizma + güçler) + `ResultCard` (her bitişin tek
  kartı). Tura giriş hep `AppController.PlayClassic/PlayDaily/PlayLevel` — kayıttan devam ile yeni başlangıç tek yerde.
- Modallar **yığın** (`AppController._modals`): duraklatmadan açılan ayarlar kapanınca duraklatmaya döner; Android geri
  tuşu da yığını izler. Kapanış animasyonlu (`AppScreen.Dismiss`): sayfa inene kadar yığında ve engelleyici kalır;
  ikinci bir kapanış/açılış gelirse ilki anında tamamlanır (`FinishDismiss`).
- **Girdi tek yoldan:** `PointerRouter` her kare `Pointer.current` okur. Widget'lar (`UiButton`, `UiSlider`, `UiToggle`)
  önce hak iddia eder, kalan her şey `IPointerFallback`'e (tahta) düşer. **EventSystem yok.** Kaplayan her yüzey
  `PointerRouter.PushBlocker` ile engelleyici (tuzak 8).

---

## 4. Sistemler

### 4.1 `PieceDealer` — en kritik ve en hassas sistem

Kör rastgele dağıtım bu türde adaletsiz hissettiriyor: uyarısız ölü el veriyor ve hiç combo kurmuyor. Dağıtıcı her
tepsi için **16 aday** üretip her birini yetkin bir oyuncu gibi sonuna kadar oynatıyor, sonra sonuca göre puanlıyor
(kaç taş yerleşti, kaç satır çıktı, ne kadar sıkı oturdu, tahta ne kadar doldu).

> **Tasarım felsefesi — buna uy:** Yardım **hissedilsin ama görülmesin**. Oyuncu yardım aldığını anlamamalı.
> Bazen zorlasın, bazen yardım etsin. Asla combo'yu tepsiye koyup sunmasın.

Bunu sağlayan iki mekanizma:
- **Dalgalanma** (`_drift`): yardım tahtanın düz bir fonksiyonu değil, korelasyonlu rastgele yürüyüş ekleniyor —
  birkaç cömert el, sonra sıkıntılı bir dönem. Düz fonksiyon olsaydı oyuncu tam sıkıştığı anda yumuşamayı sezerdi.
- **Sıralama kapısı** (`Choose`): çoğu dağıtımda sıralama **tamamen atlanır**, aday rastgele seçilir (yani düpedüz
  rastgele dağıtım). Sıralama yalnız yardım gerektiğinde devreye girer. **Bu kapı olmadan 16 adayın en iyisini seçmek
  tek başına devasa bir yardım** — assist 0 olsa bile. Bir kez bu yüzden yardım iki katına çıktı.

**Ayar noktaları**

| Sabit | Anlamı |
|---|---|
| `BaseAssist` 0.26 | Boş tahtada bile uygulanan taban |
| `AssistCeiling` 0.82 | **Tavan. Turun bitmesini garantiler — kaldırma** (tuzak 7) |
| `PressureStart/Full` 0.25 / 0.68 | Yardımın başladığı ve tam güce ulaştığı doluluk |
| `PressureCurve` 1.25 | Rampanın şekli. Kareli hâli fazla arkaya yüklüydü: yarı dolu tahta tam gücün onda birini alıyordu, yardım ancak tur kaybedilmişken geliyordu |
| `FatigueStart/Score/Max` 3000 / 14000 / 0.5 | Uzun tur rampası. Eskisi 0'dan başlayıp 3500'de yarıyı alıyordu — casual tur (2000) kısacıkken yardımın üçte birini veriyordu |
| `MaxSmallBias` 1.15 | Tahta sıkışıkken **ve** boşalmak üzereyken küçük parçaya yönelme |
| `DriftAmount/Memory` 0.55 / 0.7 | Dalgalanmanın genliği ve hafızası |
| `CandidateCount` 16 + `CandidateBonus` 28 | Açık tahtada 16 yeter; **sıkışık tahtada asıl tavan buydu** — hiçbir aday uymuyor olabiliyordu. Ek adaylar yalnız yardım gerekirken üretilir |

**Sıralama terimleri — hangisi neyi satın alıyor** (`ScoreOutcome`)

| Terim | Ne satın alıyor |
|---|---|
| `Placed` eksikse ceza (150 + 650·assist) | **Turun uyarısız bitmemesi.** Eskiden 200·assist idi; yüzlerce puanlık terimlerin yanında yarışamıyordu |
| `FitsNow` eksikse ceza (120 + 520·assist) | **İnsanın kendini kilitlememesi.** `Placed` tepsiyi *en iyi sırayla* oynayan simülasyondan gelir; oyuncu yanlış parçayı önce koyar. Bu terim "şu anda, herhangi bir sırayla kaç parça sığıyor"u sayar |
| `ClearingMoves` (60 + 170·assist) | **Serinin yaşaması.** Çarpan üst üste temizleyen *hamleleri* sayar |
| `BestSingle > 1` (70 + 150·assist) | **Tek hamlede çok satır.** `Lines` üç hamlede üç satırı tek hamlede üç satırdan ayıramıyordu |
| `Primed` (9 + 26·assist) | **Bir sonraki temizlemenin var olması.** Bu terim olmadan seri 1.3'te çakılı kalıyordu |
| `FinalOccupancy` cezası (7 + 20·assist) | Nefes alacak yer. Fazla bastırılırsa üstteki terimle çelişir |
| ≤12 hücre ve 0 hücre ikramiyeleri | Tahtayı bitirme anı |

**Ölçülen denge** (`CoreHarness balance 60`, palet 7, güçler açık, her tur doğal bitiyor — capped 0). Süpürmeye
**skill 0.40 eklendi**: 0.55 ve 0.80 ile ölçerken asıl zorlanan oyuncu görünmüyordu, şikâyet eden oydu.

| | Önce | Sonra |
|---|---|---|
| zorlanan (0.40) — hamle (ort / medyan) | — | **110.0 / 112** |
| zorlanan — temizleme / skor | — | **38.7 / 4698** |
| casual (0.55) — hamle (ort / medyan) | 52.1 / 53 | **166.1 / 185** |
| casual — temizleme / skor / en uzun seri | 17.1 / 1995 / 2.6 | **62.5 / 7461 / 3.4** |
| iyi (0.80) — hamle / skor | 150.5 / 6936 | **498.3 / 24184** |

Casual tur **3.2 kat** uzadı; medyanın ortalamaya yaklaşması **ani erken ölümlerin** kalktığını gösteriyor. Çok satırlı
temizlemenin oranı benzer kaldı (%7-11), adedi tur başına ~1.2'den ~5'e çıktı.

**Ölçümün ortaya çıkardığı iki şey — tahminle değiştirme**
1. **Turlar tahta dolduğu için bitmiyor.** Bitişte tahta ortalama %52-61 dolu, %60 üstünde geçen süre %1-7. Ölüm
   "yer kalmadı"dan değil **"elindeki parça deliklere uymuyor"**dan geliyor. Çaresi `MaxSmallBias` ve oynanamaz-tepsi
   cezası, doluluk tavanı değil.
2. **Tahtayı tamamen temizlemek ikramiyeyle değil adayla satın alınıyor.** Tahta neredeyse her turda ~3.9 hücreye
   iniyor ama kalanlar dağınık. İkramiyeyi 900·assist'e çıkarmak hiçbir şey değiştirmedi; işi bitirecek tepsi aday
   havuzunda **yoktu**. Havuz yardım altında genişleyince iyi oyuncuda turların %7'si tahtayı sıfırladı.
   Ders: sıralamayla düzelmeyen bir şey için önce aday havuzunda var mı diye bak.

**Performans: 0.17 ms/dağıtım, 0 tahsisat.** Bozma: `BoardModel` sayaçları (`CountCompletedLines` taramaz),
`PieceShape._mask` (`ContactScore` iç içe arama yapmaz), tek bir yeniden kullanılan `_scratch` tahta, havuzlanan
aday dizileri.

### 4.2 Prizma güçleri — PRIZMA'yı türdeşlerinden ayıran şey

Reklamla satılmaz, **oynayarak kazanılır**. Satır temizledikçe prizma dolar, dolunca 1 şarj (en çok 3).
**Döndür** (1 — tepsideki parçayı çeyrek tur) · **Yenile** (1 — kalan parçaları yeniden dağıt) · **Bomba** (2 — 3×3).
Tek renk satır prizmaya 2 sayılır; tahtayı sıfırlamak doğrudan 1 şarj.

- Hiçbir parça sığmıyor ama şarj varsa tur bitmez: `SessionState.Stuck`. Tepsi soluklaşır, prizmanın yerinde "BİTİR"
  çıkar; kurtarmak ya da bitirmek oyuncunun kararı.
- **Tek renk satır** ("prizma satırı"): +120 × combo ve prizma süpürmesi (`BoardView.PlayPrismSweep`). Tepsi renkleri
  rastgele olduğundan hedeflenebilir ama bedava değil.
- Öğretim yazısız: güçlerin kurtarabileceği **ilk** sıkışmada el zar butonuna dokunur (`GameScreen.MaybeHintPowers`);
  bir güç kullanılınca `Progress.PowersHinted` ile bir daha çıkmaz.

| `PowerRules` | Değer | Anlamı |
|---|---|---|
| `StartCharges` | 2 | Klasik/günlük başlangıç. 1 iken zorlanan oyuncu kurtarışı bir kez yaşıyordu |
| `LinesPerCharge` / `ChargeStep` | 12 / 26 | İlk şarj ucuz, sonrakiler dik. **30 / 20 iken ilk şarj zorlanan oyuncunun menzili dışındaydı** (tur ~20 satır) |
| `MaxCharges` | 3 | |

Ölçülen: zorlanan oyuncu tur başına 1.2'den **2.5 güce**; iyi oyuncu 3.8'de kaldı — kurtarış, yaşam biçimi değil.
`balance 40`: casual hamle 35.5 → **52.1** (+%47), iyi 84.2 → **150.5** (+%79).
**Sabit fiyata dönme:** sabit 10 satır iyi oyuncunun turunu 4 katına çıkarıp gerilimi öldürdü; başlangıç şarjı 0 iken
casual ilk şarja ulaşamadı. Bir sonraki ayar `ChargeStep`'i artırmak olmalı, tavansız bırakmak değil.

### 4.3 Macera modu, `LevelGenerator`, harita

Kullanıcı: "candy crush gibi bir yol olsun, 100 bölüm, bir çok yeni özellik; tasarım bizim dilimizde olsun".
**100 bölüm, 10 dünya**, kıvrılan yol. Hiçbir bölüm saklanmaz: **numara → aynı tahta, hedef ve hamle bütçesi**.

**Dünyalar ve öğeler** (`LevelGenerator.*From`). Her dünyanın ilk bölümü (`IsIntro`) yeni öğeyi **elle kurulmuş, kolay**
bir tahtada yazısız gösterir (`LevelGenerator.Intro`). Günlük bulmaca bunların hiçbirini almaz (`adventure: false`).

| Dünya | Bölüm | Getirdiği | Nasıl çalışır |
|---|---|---|---|
| 1 Kristal Kıyısı | 1 | kristal, satır, puan; 6'dan buz | |
| 2 Işık Tarlası | 11 | **Işık karosu** (`GoalKind.Tiles`) | Zemin katmanı (`BoardModel._tiles`), boş hücrede de durur; üstündeki blok **temizlenince** söner |
| 3 Taş Geçit | 21 | **Taş** | Satırı doldurur, temizlemeyle gitmez; bomba/çekiç kırar |
| 4 Gölge Ormanı | 31 | **Gölge** (`GoalKind.Shade`) | Hücre değeri `BoardModel.Shade` (−2). `ShadeSpread` sessiz hamlede bir boş komşuya yayılır (oturum RNG'si → deterministik); gölge temizleyen hamle sayacı sıfırlar. Tek renk satır olamaz |
| 5 Renk Çarşısı | 41 | **Renk siparişi** (`GoalKind.Colors`) | `OrderColor` renginde N blok; `ClearResult.ClearedColors` |
| 6 Saat Kulesi | 51 | **Saatli blok** | `BoardModel._timers` her hamle 1 azalır; 0 olursa tur **şarjdan bağımsız** biter (`LossReason.Timer`). Hedefe ulaşan hamle önceliklidir |
| 7 Buz Sarayı | 61 | **Çift buz** yoğun | |
| 8–10 | 71–100 | hepsi karışık | Fırtına Tepesi · Yıldız Denizi · Prizma Tacı |

- **Hedef seçimi** (`PickGoal`): 1–10 eski döngü; sonra ayrı RNG akışıyla, öğesini getiren dünyada yarı yarıya o hedef,
  kalanı açılmış hedeflerden ağırlıklı (kristal en sık).
- **Zor bölümler** (`HardnessOf`): her dünyanın 10. bölümü **çok zor** (taç, mor halka), 2. dünyadan itibaren 6. bölüm
  **zor** (şimşek, gül halka); harita ve başlangıç sayfası işaretler. Zorluk iki yoldan: tahta daha yüksek kademeden
  (`DifficultyTier` +12 / +24, intro −14) **ve** bütçe botun daha iyi günlerinden (`percentile` 0.65 / 0.55, pay ×0.92 /
  ×0.84). Yalnız tahta yetmedi: `Difficulty` 100 civarında doyuyor, 100. bölüm 99'dan kolay çıkıyordu.
- **Hamle bütçesi tahmin değil ölçüm:** taslak bot (skill 0.62) ile 15 kez oynanır; %75'lik (zorda %65/%55) dilimdeki
  hamle × pay (1.40 → 1.15), ilk 10 bölüme +6. Bot %60'tan az kazanıyorsa taslak atılır. 7 koşu + medyan ile komşu
  bölümler %8–%100 arasında savruluyordu. **Bütçe tavanı** `MaxAdventureBudget` 70 (100+ hamle sıkıcı, zor değil).
- **Bot** (`Autoplayer.BuildWeights`): hücre başına ağırlık — kristal 160, ışık 150, sipariş rengi 60, gölge 150/40,
  saat `120 + 900/kalan`. Bot hedefi kovalamazsa bütçeler gevşek ve düzensiz olur (kristalde bir kez yaşandı).
- **Üretim** 10–230 ms, önbellekte. Aynı bölüm iki yerden istenirse ikinci istek bekler, yeniden üretmez
  (`LevelGenerator.Once`); harita ve başlangıç sayfası sonrakini `Prefetch` ile hazırlar.

Ölçülen (`CoreHarness levels 1 100`, deneme başına kazanma):

| Dünya | 1 | 3 | 5 | 7 | 9 | 10 |
|---|---|---|---|---|---|---|
| zorlanan 0.40 | %79 | %68 | %58 | %57 | %47 | %49 |
| casual 0.50 | %87 | %77 | %73 | %70 | %58 | %68 |
| iyi 0.85 | %99 | %98 | %95 | %95 | %94 | %88 |

Çok zor bölümler casual'da %25–58, 100. bölüm iyi oyuncuda %33 (final).

**Güçlendiriciler, seri, sandık** (`Progress`, `Booster`) — reklam/satın alma yok, oynayarak kazanılır.
- **+3 Hamle** ve **Şarj** başlangıç sayfasında seçilir (`AppController.PlayLevel(n, moves, charge)`), yalnız gerçekten
  yeni bir deneme başlarken harcanır. Ek hamleler (`SessionConfig.ExtraMoves`) **yıldıza sayılmaz**.
- **Çekiç** oyun içinde, HUD'da skorun sağında (yalnız macera). Tek bloğu buz/taş/gölge/saat dahil kırar
  (`GameSession.TryHammer`, şarj yemez). Stokta çekiç varken sıkışma `Stuck` olur (`HasBoosterRescue`, kaydedilmez).
- **Galibiyet serisi** (`WinStreak`, en çok 3): 1 → +2 hamle, 2 → +2 hamle +1 şarj, 3 → +4 hamle +1 şarj. Kayıp ya da
  hamle yapılmışken yeniden başlatmak sıfırlar.
- **Dünya sandığı:** dünyanın son bölümü bitince altın olur; her güçlendiriciden 1 (4. dünyadan 2 çekiç, son dünya 3'er).
  İlk açılışta başlangıç stoku (2 / 2 / 3).
- Hamle biterken 2 şarj varsa tur hemen bitmez (`SessionState.OutOfMoves`): sonuç kartı **+5 hamle** teklif eder,
  deneme başına bir kez; ek hamleyle bitirilen bölüm **1 yıldız**.

**Harita** (`AdventureScreens.cs` — `LevelSelectScreen`, `MapScroll`)
- **Işık yolu.** Kullanıcı ilk hâlde "yolların kenarındaki siyah border"ı beğenmedi (koyu `SurfaceInset` yatak). Artık
  yatak yok: **gidilmiş yol yanar** (dünya renginin açığı şerit + beyaza yakın çekirdek + yumuşak parıltı),
  **gidilmemiş yol sessiz nokta izi**. Catmull-Rom eğrisinde **mesafeyle** örneklenir.
- **Durak:** bitmiş = dünya renginde plastik disk (`Art.Node`), beyaz halka, 3 yıldız. Sıradaki = büyük **düz beyaz** disk,
  sayı dünya renginde, halka + hale, üstünde prizma işaretçisi. Kilitli = küçük **buzlu cam** (beyaz %14, halka %22) —
  saydamlık **bilerek**: opak karışım yarı dünyalarda çamurlu bej çıktı. Zor kilitli bölüm zor renginde tonlu cam.
- **Dünya girişi:** amblem, "DÜNYA n" + ad, yıldız sayısı, altın ilerleme çubuğu; ulaşılmamışta kilit. Her dünya kendi
  **renk alanı** (iki havuz + üstte sonraki dünyanın rengi), arkada **paralaks** (0.3) kristaller ve dünyanın motifi.
  Blok şekilli süs yok (yerde yatan parça ya da çamur gibi okunuyordu).
- Başlık kendi `Canvas`'ında (yol başlığın üstünden geçiyordu). Üstteki geçiş 180° döndürülmüş `VerticalFade` —
  pivot **ortada** olmalı.
- **Odanın ışığı ekrandaki dünyanın rengine döner** (`Backdrop.SetLight`).
- Kaydırma kendi widget'ı (`MapScroll`): fırlatma + yumuşak kenar, **dokunmayı** bildirir (22 birimden az hareket).
  Duraklar buton değil, çizili.
- **Performans:** ~3000 görüntü. Menü boştayken kare kare **önceden kurulur** (`BuildAhead`, ~700 ms toplam); içerik
  kendi `Canvas`'ında, yalnız görünen ±700 birimdeki dünyalar açık. Numaraların gölgesi **tek paylaşılan materyal**
  (`ShadowNumber`) — TMP her gölgeli etikete ayrı materyal verir, 100 draw call olurdu.
- **Kazanınca yürüyüş:** "SONRAKİ" → harita (`ShowLevelSelect(afterWin)`), yol dünya rengiyle dolar, işaretçi sekerek
  geçer, sonra o bölümün başlangıç sayfası açılır. Dünya bitmişse açmaz — önce sandık görülsün. `Progress.MapRevealed`
  yolun son çizildiği yeri tutar; bir adımdan fazla fark animasyonsuz gösterilir.
- **Başlangıç sayfası** (`LevelStartScreen`): dünya satırı + zor çipi, hedef ve hamle kartı, en iyi yıldızlar ya da intro'da
  "YENİ · …" çipi, seri satırı, 3 güçlendirici karosu, OYNA. Kayıtlı deneme varsa DEVAM ET (güçlendiriciler kapalı).
  Kaybedince "TEKRAR DENE" de buraya gelir. Bölüm henüz üretilmediyse sayfa üretim bitince açılır.

### 4.4 Günlük bulmaca, rozetler, başarımlar, hatırlatıcı, paylaşım

**LinkedIn Zip / Wordle modeli.** Kullanıcı eski takvimi ("geçmiş günü oynayabiliyorum, bu hoş değil") reddetti.
Günde **bir** bulmaca, herkese aynı, yalnız bugün oynanır. Kaçan gün kaçmıştır — serinin ağırlığı bu.

- **Bulmaca** `LevelGenerator.GenerateDaily(date)`: bölüm üreticisiyle aynı yol, tohum tarihten. `LevelDefinition.Number`
  = bulmaca numarası (`DailyNumber`, 1 Ocak 2026 = #1). Zorluk **haftanın gününden** (`DailyTier`): Pazartesi 8. bölüm
  gibi → Pazar 45. bölüm gibi. Satır hedefi yalnız Pzt–Per (hafta sonunu düzleştiriyordu). Ölçülen (`daily 28`):
  casual kolay %96 → zor %76 → en zor %77; iyi oyuncu %92–100.
- **Oturum:** `GameMode.Daily` + `Level` dolu → bölüm kurallarıyla (hedef, hamle, +5 hamle). `GameSession.PlaySeconds`
  süreyi tutar (`GameScreen.Update` sayar; modal/sonuç kartı/arka planda durur, kayda girer).
- **Deneme:** çözülene kadar tekrar. Her yeni deneme `Progress.DailyAttemptStarted`, biten denemenin süresi
  `DailyAttemptEnded` ile bankaya. Gösterilen süre = banka + mevcut deneme.
- **Çözüm** `Progress.RecordDailySolve`: seri (dün çözüldüyse +1), en iyi seri, süre/hamle/yıldız/deneme, rozet
  istatistikleri, kusursuz hafta. Çözülen gün tekrar oynanmaz. Sonuç kartında büyük sayı **süre**, ana eylem **PAYLAŞ**,
  not satırı yeni rozet > yeni tema > seri.
- **Kart** (`DailyScreen`): Bugün sekmesi (seri + hafta şeridi + zorluk/hedef + sonuç ya da deneme durumu + geri sayım)
  ve Rozetler sekmesi (4×4 madalyon). **Hatırlatıcı anahtarı yok** — kullanıcı istemedi. Seri kart son gösterdiğinden
  büyükse bir kez sayarak yükselir (`Progress.DailyStreakShown`).
- **Rozetler** (`DailyBadges`, 16) ve **başarımlar** (`Achievements`, 11 × 3 kademe) **durumsuz** — sayılardan hesaplanır,
  hiçbir şey saklanmaz. Yıldız verirler → `Progress.TotalStars` = bölüm + rozet + başarım yıldızı → temalar.
  Görülmemiş olan için sabit nane nokta (nabız yok).
- **Hatırlatıcı** (`Reminder` + `Assets/Plugins/Android/PrizmaReminder*.java` + `Editor/ReminderManifest`): son çözümün
  **saatinde**, ertesi gün tek bildirim. Eklenti yok: AlarmManager → `PrizmaReminderReceiver` bildirimi atar ve ertesi günü
  kurar; 3 cevapsız bildirimden sonra susar; `PrizmaReminderBoot` yeniden başlatmada geri kurar. Tam zamanlı alarm izni
  bilerek yok. İzin **ilk açılışta splash sırasında** sistemin kendi penceresiyle sorulur
  (`AskNotificationsOnFirstLaunch`) — oyunun kendi modalı yok (kullanıcı isteği); sonradan Ayarlar → BİLDİRİMLER.
  Bildirim ikonu vektör XML, build sırasında `res/drawable`'a yazılır. **Cihazda henüz doğrulanmadı.**
- **Paylaş** (`ShareSheet`): Android'in kendi paylaş penceresi (intent), eklenti yok; masaüstünde panoya. Metin
  `Str.ShareText` + mağaza linki.

### 4.5 Spektrum — oyun ilerledikçe renk (`Spectrum`, `Backdrop`)

Kullanıcı: "oyun ilerledikçe, belirli şeyler yapıldıkça oyunun renkleri değişsin". Referans **Lumines**: oyuncu ne kadar
ilerlediğini odanın renginden okur, ekranda yazı yok.

**Yalnız ışık değişir:** zemin ışığı (`Glow`), üstte `Aura`, tepsi arkasında `Floor`, `Deep` tonu, ızgara ve tahta
kenarının dinlenme rengi. **Bloklar, yüzeyler ve anlam taşıyan renkler değişmez.**
- **Klasik kademeler** (`Spectrum.Thresholds`): 1500 gül · 4000 kehribar · 8000 lime · 13000 turkuaz · 20000 mavi ·
  30000 mor · 45000 **dönen prizma** (zorlanan 1–2, casual ~3, iyi ~6 kademe). Geçişte oda ~1.5 sn'de kayar, tahta
  alttan üste taranır (`BoardView.PlayStageWash`), `stage` sesi.
- **Hedefli turlar** (macera, günlük): hedefin çeyrekleri mavi → turkuaz → lime → kehribar.
- **Olaylar:** combo sürdükçe oda ısınır (`SetHeat`), tek renk satırda gökkuşağı (`Rainbow`), tahta sıfırlanınca altın
  (`Flash`), rekor geçilince tahta kenarı tur boyunca altın. Combo çipi: nane → altın (4) → gül (6) → prizma (8).
- **Kalıcı:** `Progress.BestSpectrum`; menüdeki **P-R-I-Z-M-A** harfleri kademe başına bir renklenir (`TitleMarkup`).
- Alfalar ekran görüntüsüyle ayarlandı: ilk hâlde parlak zeminde kademe değişimi **görünmüyordu**; ışığın zemine
  (`Deep`) işlemesi gerekti. `Floor` bilerek aura'dan düşük: tepsi parçaları o zeminde durur.
- Arka plan tek shader geçişiyle çizilir (§6); ışık değişmiyorken `UpdateLight` hiçbir şey yazmaz.

### 4.6 Temalar, kayıt ve ilerleme

- `RunStore`: mod başına bir yuva; her hamlede ve arka plana geçişte `SessionSnapshot` (JSON). RNG durumu içinde →
  devam edilen tur **birebir aynı** dağıtımla sürer (testi var). Bozuk kayıt reddedilir, oyun çökmez. Günlük kayıt yalnız
  başladığı gün, bölüm kaydı yalnız aynı bölüme devam eder.
- `Progress`: yıldızlar, günlük seri, yaşam boyu istatistikler, tema, renk körü modu, öğretici görüldü.
- **Temalar** yıldızla açılır ve **ekranın bütün havasını** değiştirir (zemin, ışık, ızgara, yüzeyler, tahta, vurgu, scrim,
  bloklar). Anlam taşıyan renkler **temalanmaz**: altın, nane, prizma, kristal, buz, ön-temizleme tonları.
  - İlk hâlde tema yalnız blok paletiydi, kullanıcı "temayı değiştirince bir şey değişmiyor" dedi; ölçünce haklıydı
    (renk başına ΔE 15). Şimdi zeminler arası ΔE 30–85.
  - **Uygulama yeniden kurmakla olur** (`SetTheme` → `RebuildInterface`): renkler kurulumda okunur, yerinde boyamak bir
    grafiği kaçırırsa eski temadan yama kalır. Oyuncu olduğu yerde kalır (sayfa, modallar, tur). Eski ağaç
    `Destroy`'dan **önce kapatılır** — yoksa kare sonuna kadar girdiye kayıtlı kalır.
  - `Design` renkleri `Themes.Current`'tan okunan özellikler. **Temaya bağlı rengi `static readonly` alanda saklama**
    (`BoardView.EmptyCellColor` bu yüzden özellik) — ilk temada donar.
  - **Uygulama ikonu ve splash `Themes.Default`** kullanır (Editor'de `Current` geliştiricinin son seçimidir).
  - Temalar ekranında canlı önizleme (`ThemePreview`): açık temaya dokunmak uygular, kilitliye dokunmak önizler.
  - Palet değişince **`python Tools/theme-check.py`**: yüzeyde beyaz ≥7:1, vurguda ≥3:1, çiplerde altın/nane ≥4.5:1,
    zeminde `TextOnGround` ≥3:1, tahtada her blok ≥3:1, iki blok ≥ΔE 22, hiçbir blok kristal/buza yakın değil (Pastel'in
    açık mavisi buza ΔE 20'ydi), **tepsi parçaları zemine ≥ΔE 24** (Şeker'in pembesi pembe zeminde ΔE 10'du).

### 4.7 Dil — `Str` + `Assets/Resources/Lang/*.json`

Hedef yurt dışı pazarlar; dil sayısı artacak. Kelimeler **dil başına bir JSON** (`tr.json`, `en.json`: düz
`"anahtar": "metin"` ya da liste). `Str` yalnız hangi anahtarın nereye gittiğini bilir (`Str.Play`, `Str.LevelN(n)` …).
- **Yeni dil = yeni dosya** (`de.json` …), kod değişmez. Meta anahtarlar: `_name` (seçicide görünen ad), `_culture` (sayı
  gruplama, büyük harf; boş = invariant), `_font` (Poppins'te olmayan harfler için `Resources/Fonts` altında yedek TMP font).
  Sonra **`python Tools/lang-check.py`**.
- **Font kapsamı:** Poppins Batı/Orta Avrupa, Türkçe, Endonezce dillerini kapsar; **Kiril, Yunan, CJK, Arapça, Tay,
  Vietnamca kapsamaz** — bunlar `_font` ister (Noto ailesi OFL; CJK fontları büyük, boyut kararı gerekir).
- Dil kodu metin (`GameSettings.Language` = "tr"/"en"); eski sürümdeki sayı kaydı bir kez taşınır. Varsayılan telefon dili,
  dosyası yoksa EN. **Eksik anahtar EN'e düşer** (boş etiket olmaz). Büyük harf `Str.Upper` — dilin kuralıyla (Türkçe i/İ).
- Tarih şablonları belirteçle: `{day} {MONTH}` / `{MON} {day}`.
- Değiştirmek `AppController.SetLanguage` → tema gibi arayüzü yeniden kurar. Dil seçici segment, dosya listesinden
  kurulur; **3 dili sığdırır, 4. dil bir liste sayfası ister.**
- İngilizce metinler Türkçeden uzun: sabit genişlikli etiketleri **her dilde** görüntüyle doğrula (AutoTest `20_en_*`).

### 4.8 Ses — `AudioKit`

Dosya yok, her şey sentezleniyor (`Tone` + `Chime`: sinüs + ikinci harmonik, yumuşak zarf, tek kutuplu alçak geçiren).
Zincir: `SoundSynth` (tarifler, Unity'siz) → `SoundMaster` (seviye, Unity'siz) → `AudioKit` / `MusicPlayer` (çalma).
İlk ikisi `Tools/AudioCheck` ile ölçülür. Set sade ve bu **kasıtlı**:

> **Ses estetiği dersi.** Bir kez set baştan yazıldı (katmanlı sentez, inharmonik çanlar, reverb, stereo, sub, ducking);
> ölçümler kusursuzdu ve **kullanıcı hepsini reddetti**: *"sanki mobil bir oyun oynamıyorum da başka bir şeyin sesi
> gibi"*. Uzun reverb + inharmonik çan + sub = Monument Valley estetiği; Block Blast türü **kuru, kısa, parlak** ister.
> Ölçüm yapıyı doğrular, tınıyı değil — tını yargısı kullanıcınındır. Kurallar: **(1) tınıya dokunmadan sor**, yapı
> (ne zaman, ne kadar, hangi sırayla) serbest; **(2) eskiyi koru** — 1 satırlık temizleme, bırakma, seçme beğenildi;
> **(3) dinlet** — gerçek zamanlamayla kurgulanmış **diziyi** WAV olarak ver.

**Seviye — `SoundMaster`.** Kullanıcı: "oyun sesini fullesem bile az". Sebep üretimdi: hiçbir klip tam ölçeğe
yaklaşmıyordu (en sık duyulanlar −13…−19 dBFS tepe, müzik −27 dB). Şimdi her klip **rolüne göre bir yükseklik hedefine**
getirilir (`TargetFor`; en yüksek 100 ms, K-ağırlıklı RMS), −1 dBFS altında şeffaf look-ahead limiter (en çok 1.1 dB;
`MaxReductionDb` 6'yı aşacaksa klip sessiz bırakılır). **Tını değişmedi.**

| (`AudioCheck measure`, efekt %100) | Önce | Sonra |
|---|---|---|
| bırakma (turun en sık sesi) | −23.7 | **−12.1** |
| kaldırma / menü tıkı | −26.6 / −29.7 | **−15 / −15** |
| 1 satır temizleme | −16.2 | **−9.0** |
| buz | −35 | **−13.9** |
| müzik (varsayılan) | −26.9 | **−18.1** |

Hiyerarşi: temizleme −9, combo cevabı 1.5 dB altta, bırakma −12, tık/kaldırma −15, müzik temizlemenin ~9 dB altında.
Varsayılanlar efekt %100 / müzik %70.
- **Çıkış limiteri** (`AudioBusLimiter`): iyi bir hamle 70 ms içinde üç sesi üst üste çalar; toplam kırpılırdı. Kaldırma.
- **Temizleme tek çağrı:** `PlayClear(lines, comboStreak, monoLines, perfectClear)` — parçalar sırayla: temizleme, +70 ms
  combo cevabı, +130 ms tek renk satır, +220 ms tahta boşaldı (aynı karede tetiklenince tepeler toplanıyordu). İki eksen,
  aynı tını: satır sayısı → çanın merdivende ne kadar çıktığı (`_clear`); combo serisi → cevap (`ComboLadder`, 9 basamak;
  eskisi 5'te düzleşiyordu). 1 satırlık temizleme **birebir eskisi gibi** (turun %95'i).
- **Eklenen katmanlar — eskiyi değiştirmeden** ("combo yaptıkça değiştir değil, mevcuttakine eklemeler yap"):
  `sparkle0..3` (−16.5; 3. combo'dan itibaren tiz koşu), `surge` (−12; her 5. combo'da akor), `deal` (−19; tepsi
  yenilenince), `record`, `badge`, `streak`. A/B: `AudioCheck wav` → `4_layered_combo_sequence.wav` / `4b_unlayered_…`.
- **Açık karar — telefon hoparlörü:** ~700 Hz altını neredeyse çalmaz; bırakma (130–220 Hz) hoparlörde −44 dB.
  `SoundMaster.AddPresence` harmonik ekler (−44 → −29) ama **tınıyı değiştirdiği için oyunda değil**; `AudioCheck wav`
  üçüncü varyant olarak çıkarır. **Kullanıcı dinleyip karar verecek.**
- Sentez ana iş parçacığını bloklamaz: menünün dört sesi `Awake`'te (1.3 ms), kalan klipler ve müzik `ThreadPool`'da.
- **Ayarlar tuzağı:** ses gelmiyorsa önce `GameSettings.Muted`'a bak (seviyeden bağımsız). `AudioManager.asset` →
  `m_DSPBufferSize: 512` (1024'te dokunma-ses gecikmesi fark ediliyordu, 256 zayıf cihazda underrun riski).

### 4.9 Modallar — alt sayfa (`ModalKit.cs`), oyun dilinde

İki tur geri bildirim: önce "modallar çok karmaşık… 2010-2015 gibi arayüz istemiyorum" (ortada kart + altta KAPAT;
şeritli başlık plakası + kırmızı X de reddedildi — **ona dönme**). Sonra iOS ayarlar dilinde bir alt sayfa "mobil web
sitesine benziyor" dendi. Karar: **yapı kalsın, dil oyunlaşsın, animasyonlu olsun.**

- **`Sheet`:** alttan kalkar, üst köşeler `SheetRadius`, alt kenar jest çubuğunun altına taşar. Üst kenarından yarısı taşan
  **madalyon** (`SheetHero` 176: beyaz halka + sayfanın renginde disk + beyaz işaret + hale), altında **ortalı** başlık,
  üstte o rengin ışığı (`Art.TopWash`). Tutamak yok (aşağı çekme çalışır). Kapat: sağ üstte plastik disk
  (`UiButton.Style.Round`, dokunma alanı 144). `BuildSheet(title, icon, tint)`; `Sheet.SetHero` sonradan değiştirir.
  Madalyonsuz sayfa (sonuç kartı, sandık) eski başlık düzenini kullanır.
- **Hareket:** sayfa `Ease.OutBackSoft` ile oturur, madalyon dönerek "pop" eder, gövde parçaları sırayla gelir (`Cascade`:
  yalnız ölçek + saydamlık, yerleşime dokunmaz; dinlenme saydamlığı `Cascade.Rest`, farklı ölçek `SetRestScale`).
  Butonlar yaylanır, anahtar topuzu esner, kaydırıcı topuzu basılıyken büyür, sayılar sayarak yükselir, sandık sallanır.
- **Malzeme:** kartlar ve butonlar blokla aynı yumuşak plastik (`Art.PanelGradient`: üst açık, alt koyu — bilinçli;
  renk `SheetKit.Lifted` ile bir tık açılır). Yüzeyler: sayfa `SurfaceSheet` < grup `SurfaceGroup` < kontrol `SurfaceControl`.
- **Liste yerine karo:** açma/kapama ayarları 2×2 `UiTile` (açıkken kendi renginde — yüzeye **karıştırma**, hardal olur;
  kendi rengini koyulaştır). Karo yazıları **set hâlinde boylanır** (`UiTile.MatchLabels`). Duraklatma: üç eylem karosu +
  DEVAM ET. Satır arası kısa yumuşak oluk. Segment başparmağı vurgu renginde.
- **"KAPAT" butonu yok.** Sığmayan gövde bütün olarak küçülür (madalyonun taşan yarısı dahil). Grup kartının boyu sonradan
  değişirse `SheetKit.ResizeGroup` (tutucunun boyu kartı değiştirmez).
- Ayarların en altında sürüm etiketi; `ShareSheet.PrivacyLink` doldurulunca gizlilik politikası bağlantısı.
- Görünüş üzerinde çalışırken: `Tools/autotest.ps1 -Only modals`.

---

## 5. Tasarım dili

`Design.cs` **tek kaynak**. Çağrı yerinde ham sayı yazma; token ekle.

- Tipografi: Readout 200 / Display 184 / Title 92 / Headline 72 / Body 58 / Label 50 / Caption 42
- Aralık: 8 / 16 / 24 / 40 / 56 / 80 / 112 · kenar boşluğu 48 · tahta kenarı 24 · min dokunma hedefi 144 (`TouchTarget`)
- Kontroller: `ButtonLg` 204 / `ButtonMd` 176 · köşe ikon butonu `IconButton` 168 · ikon `IconSm` 72 / `IconMd` 108 ·
  ikon-buton dolumu `GlyphFill` %54 · modal genişliği `ContentWidth` 984
- Şekil: **squircle** (süperelips), düz yuvarlak dikdörtgen değil — `Raster.FillSquircle`
- Derinlik: **bulanık ambient gölge**, sert alt dudak değil (offset küçük, yayılım büyük). Daireye `Art.DiscShadow` (tuzak 11)
- Renk: neredeyse siyah taban + mor/turkuaz/erik havuzlar + tek canlı vurgu

> **Arayüz kuralı — ihlal etme: Yazıyla anlatma. Yanıp söndürme.**
> Bir kez "2 SATIR BİRDEN" rozeti ve nabız atan ipucu eklendi; ikisi de kaldırıldı. Ön-temizleme önizlemesi
> (`BoardView.PreviewLines`) **sabit ve sessiz**: etiketsiz, nabızsız, düşük alfa; renk kademesi 1 satır nane / 2 altın /
> 3+ gül. Yoğunluk: `BoardView.AddPreviewBar` (0.22 / 0.13).

### Ölçüler dp'den seçilir, gözle değil
Scaler genişliği eşler: **1080 birim = ekran genişliği**. Android'in ~%31'i ≤360dp → **1 birim = 1/3 dp** (3 birim = 1sp).
- İlk ölçek gözle kurulmuştu ve platform alt sınırlarının altındaydı ("arayüz çok küçük"). İkinci ölçek Material'ın alt
  sınırlarını tutturdu ve şikâyet yine geldi: **"indirdiğim oyunlara göre arayüz elemanları küçük kalıyor"** — casual oyun
  bir kademe büyük kurar; fark **alt kademelerdeydi** (etiket, ikon, slider). Üçüncü ölçekte alt kademeler en çok büyüdü,
  oyuncunun okuduğu her kelime ExtraBold, tepsi `MaxScale` 0.74. **Material alt sınırına geri çekme** — taban, hedef değil.
- Okunan hiçbir şey `Caption` (42 = 14sp) altına, dokunulan hiçbir şey 144 (48dp) altına inmez; köşe ikon butonları 168.
- Ekranlar sabit ofsetle değil **`App.PageHeight`**'tan yerleşir (19.5:9'da sayfa ~2120). Oyun: `GameScreen.PlayLayout.Solve`
  — fazlalık tepsiye ve boşluklara; eksik boşluklardan → tepsiden → HUD'dan, **tahta en son** küçülür. Menü alttan yukarı
  (başparmak). 16:9'daki 1920 clamp'i kaldırıldı — **geri koyma**.
- Yerleşim `Awake`'te bir kez; çalışırken ekran değişirse (katlanabilir) yalnız safe area yenilenir. Tablet (3:4) bilinçli
  olarak telefon sütunu.

---

## 6. Performans

Hedef: **60 FPS**, zayıf telefonda da (PowerVR GE8320 / Mali-G52 sınıfı). Bunları geri alma:

**Kare hızı**
- **`Application.targetFrameRate` 60** — ayarlanmazsa Android 30'a kilitler. İlk APK'da en büyük etken buydu.
- **Kamera boşa çalışmaz:** her şey Overlay canvas; `ConfigureCamera` skybox/HDR/post'u kapatır. `Mobile_RPAsset`: HDR
  kapalı, render scale 1, MSAA kapalı. Kameranın temizleme rengi zaten `Design.BgTop`.
- **Arka plan tek opak geçiş** (`PrizmaBackdrop.shader`): eskiden 7 yarı saydam tam ekran katman (Deep, 3 havuz, Grid,
  Vignette, Grain) her kare bütün ekranı boyuyordu — ucuz GPU'nun dolum hızını tek başına yiyordu. Shader aynı dokuları
  aynı sırayla aynı formülle harmanlar; fark ≤4/255. Desteklenmezse eski katmanlar (`Backdrop.BuildLayers`).
  **Renkler `SetVector(c.linear)`** — `SetColor` yalnız Properties'te tanımlı renkleri lineerleştirir; ilk denemede her şey
  bir gama adımı açık çıktı.
- **İç içe canvas:** her kare hareket eden şey (`Backdrop`, `DragLayer`, harita içeriği, kırıklar) kendi `Canvas`'ında —
  paylaşılan canvas'ta her kare **tüm UI** yeniden batch'leniyordu. Yeni hareketli öğeye de kendi canvas'ını ver.
- Hayalet/önizleme yalnız hücre değişince yenilenir (`GameScreen._hover*`).
- `Editor/AndroidPerformanceSettings`: Optimized Frame Pacing (Swappy), Blit Type Auto. FPS'i development build'de ölçme.

**Açılış ve takılma**
- **Açılış 9.8 sn → 1.3 sn** (masaüstü). Neredeyse tamamı `Raster`'ın ilk çizimiydi: her şekil (ikondaki ince çizgi bile)
  tüm tuvali piksel başı 9 örnekle tarıyordu. Artık yalnız şeklin **sınır kutusu** ve satırlar **çekirdeklere** dağıtılır;
  106 dokunun hash'i önce/sonra **bit bit aynı**. `Paint`'e verilen fonksiyonlar **saf** kalmalı (paralel çalışırlar).
  Ana iş parçacığı 5 sn'den uzun kilitlenirse Android **ANR** verir.
- Ses sentezi, müzik, bölüm ve günlük bulmaca üretimi `ThreadPool`'da. Harita menü boştayken önceden kurulur.
- Log'da `[PRIZMA] boot: … ms` ve `map built: … ms` — telefonda logcat'ten de okunur.

**Çöp** — GC duraklaması tam sürüklemede takılmaya dönüşür. `BoardModel.ResolveLines` yeniden kullanılan `bool[,]` maske,
`Place` isteğe bağlı `ClearResult`; dağıtıcı dizileri havuzlu. **Dağıtım başına 148 KB → 0 KB**; görünümle 4.3 KB/hamle.

Ölçülen (editör, oyun içi): ana iş parçacığı 0.97 ms/kare, GPU 0.27 ms, 36 draw call.

---

## 7. Kritik tuzaklar

Hepsi bu projede canlı yaşandı.

1. **Linear renk uzayı.** Alfa harmanlaması lineer: `%7 beyaz` koyu zeminde ~%25 gibi açık çıkar. Yüzeyler yarı saydam beyaz
   değil **opak renk** (`Design.SurfaceButton`, `SurfaceTrack`, `SurfaceInset`, `SurfaceLeader`); yalnız hairline'lar saydam.
   Shader'a renk verirken `color.linear` (§6).
2. **Play mode'da domain reload düz C# alanlarını siler** (`GameScreen._session` null olur, `Awake` tekrar çalışmaz). Yalnız
   Editor sorunu. Script değiştirdiysen play mode'u **kapat/aç**.
3. **Editor odakta değilken kare işlemez** (coroutine'ler donar, bayat kare). Test öncesi
   `EditorApplication.isPaused = false; Application.runInBackground = true;`.
4. **Game view portre boyutu Unity yeniden başlayınca sıfırlanır.** "Portrait 1080x1920"yi reflection ile seç
   (`UnityEditor.GameView.SizeSelectionCallback`).
5. **Kayıp fare tıklamaları:** fare Game view üstündeyken araya tıklama girer. Görüntüden önce `PointerRouter.enabled = false`.
6. **Bozuk `.meta`:** iki script AssetDatabase'e hiç girmedi; `Refresh` çözmedi, **dosya adını değiştirmek** çözdü. Yeni
   dosyaya `.meta` yazarken GUID'i benzersiz üret.
7. **Dağıtıcı çok güçlü olursa oyun hiç bitmez.** Yardım tavansızken tahta hiç dolmadı, test döngüsü sonsuza girip
   **Unity'yi kilitledi**. `PieceDealer.AssistCeiling` bu yüzden var.
8. **Modal arkasındaki butonlara dokunulabiliyordu.** Her modal ve sonuç kartı `PointerRouter.PushBlocker` ile engelleyici;
   en üstteki görünür engelleyicinin dışındaki widget'lar (ve tahta) dokunma almaz. Yeni kaplayan yüzeyi de engelleyici yap.
9. **Açılmayan APK.** Bir APK splash'tan sonra boş gökyüzünde kaldı: build `GameRoot` → `AppController`'ı çözememiş, Unity bunu
   yalnız uyarıyla geçip **başarılı** saymıştı (build, junction'dan açılan bir kopyadan alınmıştı). `ReleaseBuild` artık
   öncesinde ve sırasında kontrol edip başarısızlıkta çıktıyı siliyor — **kaldırma**. Build gerçek projeden; uzun yol Gradle'ı
   düşürür (`prefab_command.bat` 260 karakter, "CreateProcess error=2") — `%TEMP%` altındaki kopyadan Android build alma.
10. **Statik olaya abonelik yeniden kurulan ekranda hayalet bırakır.** Tema değişimi ekranı yok edince kalan `Progress.Changed`
    aboneliği `NullReferenceException` attı. Abone olan her ekran: önce `-=` sonra `+=`, ve `OnDestroy`'da da çık.
11. **Daireye squircle gölgesi koyma** (`Art.Shadow` yuvarlak butonun arkasında kare hale taşıyordu). Daireye `Art.DiscShadow`.
12. **Yarıda kesilen animasyon iz bırakır.** `SetActive(false)` coroutine'i olduğu yerde dondurur (kaymış tahta, büyümüş parça).
    Kalıcı değeri oynatan rutin `OnDisable`'da geri koyar ya da ekran açılırken sıfırlanır; sahibi değişebilen nesneyi oynatan
    rutin her karede sahipliği kontrol eder. Gecikmeli `Tween.Scale`'de başlangıç ölçeği gecikmeden **önce** uygulanır.
13. **Kodla çizilen şekilde koşullu atlama dikiş bırakır.** `Art.Node`'un kenar halkası `y > c ? açık : koyu` ile tam ortada
    atlıyordu; diskin ortasında yatay bir kırık çıktı (kapat butonu, madalyonlar, topuzlar — 12 yer). Geçişi yumuşat
    (`SmoothStep`); yeni bir `Paint` fonksiyonunda sert eşik varsa büyütülmüş görüntüyle bak.
14. **Unity'nin varsayılan veri gönderimi açık gelir** (`submitAnalytics`, Engine Diagnostics). "Veri toplanmıyor" demek için
    yayın build'i ikisini kapatır; yeni bir Unity servisi eklenirse aynısına bak.

---

## 8. Durum ve sıradakiler

**Bitti:** çekirdek oynanış · akıllı dağıtıcı · menü, duraklatma, ayarlar · yerel ilk 10 skor · prosedürel görsel dil ·
sentezlenmiş ses ve müzik (katmanlar dahil) · ön-temizleme önizlemesi · combo göstergesi · **Prizma güçleri** · tek renk
satır ve tahtayı sıfırlama · **Macera** (100 bölüm, 7 öğe, güçlendiriciler, harita) · **Günlük bulmaca** + rozetler +
hatırlatıcı + paylaşım · başarımlar · kaldığın yerden devam · istatistikler · temalar · Spektrum · yazısız öğretici ·
haptik · uygulama ikonu · animasyonlar (kırıklar, sarsıntı, şok halkası, konfeti) · TR/EN · 60 FPS ve zayıf telefon
optimizasyonları · **yayın hattı** (imzalı AAB + APK, sürümlü klasörler, otomatik versionCode, build sonu kontrolleri) ·
JSON dil dosyaları · gizlilik politikası metni (`Docs/PrivacyPolicy.html`).

**Sırada — gerçek cihaz** (masaüstünde ölçülemeyenler): açılış süresi ve zayıf telefonda FPS · önizlemenin parmak altında
okunurluğu · yardımın fark edilmezliği · seslerin telefon hoparlöründe tınısı (+ `AddPresence` kararı) · dokunma-ses
gecikmesi · sürükleme mesafesi (`GameScreen._liftPixels`) · güç çubuğunun başparmak erişimi · bomba nişanı · haptik
şiddeti · **hatırlatıcı bildirimi**.

**Yayın için kullanıcıdan beklenenler** (ayrıntı `Docs/ReleaseReadiness.md`): geliştirici adı + e-posta (gizlilik metni) ·
gizlilik politikasının kalıcı adresi (→ `ShareSheet.PrivacyLink`) · Play Console hesabı · anahtar yedeği · 12 kişi / 14 gün
kapalı test · **isim kararı**: "Prizma Puzzle Prime" adlı yerleşik bir bulmaca serisi var (Xbox/Switch/Microsoft Store);
mağaza başlığı her durumda "İsim: Block Puzzle" kalıbında olmalı.

**Sonrası:** reklam (izinler `Checks.Allowed`, gizlilik metni, Veri güvenliği formu birlikte) · yeni diller (4. dilde dil
seçici liste sayfası; Kiril/CJK için font) · çevrimiçi lig (günlük tohum hazır, motor deterministik).

**Çalışma adı** PRIZMA — `MainMenuScreen.GameName`, tek satır.
