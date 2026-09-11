# PRIZMA — blok bulmaca

Unity 6 (6000.6.0f1) · URP · portre mobil, hedef Android.
Block Blast türünde 8x8 blok bulmaca. Arayüz metinleri **Türkçe**.
Üç mod: **Klasik** (sonsuz), **Günlük** (herkese aynı tohum, seri sayacı), **Macera** (sonsuz,
prosedürel bölümler: kristal / buz / hamle bütçesi / yıldız). Hepsinde **Prizma güçleri**.

Sahne: `Assets/Scenes/SampleScene.unity` — içinde tek bir GameObject var: **`GameRoot`** + `AppController`.
Başka hiçbir sahne kurulumu yok; tüm hiyerarşi çalışma anında koddan inşa ediliyor.

---

## Değişmez kural: projede sanat/ses dosyası yok

Bloklar, paneller, çerçeveler, gölgeler ve 11 ikon `Raster` ile **kodla çiziliyor**.
Tüm ses efektleri ve fon müziği `AudioKit` / `MusicPlayer` içinde **sentezleniyor**.

Tek istisna: `Assets/Fonts/` — Poppins (Regular / SemiBold / ExtraBold), **OFL lisanslı**, dağıtılabilir.
Lisans metni `Assets/Fonts/OFL.txt`. TMP font asset'leri `Assets/Resources/Fonts/` altında
(`Design.Load` oradan `Resources.Load` ile çekiyor).

Türetilmiş istisna: `Assets/Generated/` — **uygulama ikonu**. Android ikonu doku varlığı olarak
istiyor; `AppIconArt` (Raster) çiziyor, `Editor/AppIconGenerator` PNG'ye yazıp atıyor
(eksikse Editor açılışında, ya da menüden *PRIZMA → Uygulama İkonunu Yeniden Üret*).
Kaynak koddur — PNG'yi elle düzenleme.

Yeni görsel gerekirse önce "bunu `Raster` ile çizebilir miyim?" diye sor. Cevap genelde evet.

---

## Mimari

### `Assets/Scripts/Core/` — saf oyun mantığı
Unity'ye hiç bağımlı değil (`UnityEngine` import etmiyor). Bu yüzden test edilebilir ve
`eval` ile hızlıca simüle edilebilir. **Bu ayrımı bozma.**

| Dosya | Görev |
|---|---|
| `BoardModel` | 8x8 tahta + kristal/buz katmanları. Satır/sütun doluluk sayaçlarını hücre değiştikçe günceller |
| `Rng` | xorshift64*. Tüm durumu tek sayı → kayda girer, platformdan bağımsız. **System.Random'a dönme** |
| `SessionTypes` | Mod/durum/güç/hedef enum'ları, `SessionConfig`, `MoveResult`, `LevelDefinition`, `SessionSnapshot` |
| `LevelGenerator` | Macera bölümü üretici — aşağıda ayrı başlık |
| `Autoplayer` | Bilgisayar oyuncu: bölüm bütçesi, denge ölçümü, otomatik test |
| `PieceShape` | Parça şekli + doluluk maskesi (`Contains`) |
| `PieceLibrary` | 37 şekil, ağırlıklı dağılım |
| `ScoreRules` | Puanlama ve combo çarpanı — tüm sayılar burada |
| `GameSession` | Bir turu yönetir: tahta, tepsi, skor, seri, prizma şarjları, güçler, bölüm hedefi, oyun sonu, kaydet/yükle |
| `PieceDealer` | **Akıllı parça dağıtıcı** — aşağıda ayrı başlık |

### `Assets/Scripts/Game/` — sunum
`AppController` kök. Canvas + backdrop + ses + ekranları kurar, gezinmeyi yönetir.

Ekranlar `AppScreen`'den türer. Sayfalar: `MainMenuScreen`, `GameScreen`, `LevelSelectScreen`.
Modallar (`IsModal`): `SettingsScreen`, `ScoresScreen`, `PauseScreen`, `StatsScreen`, `ThemesScreen`.
`GameScreen` üç parçadan oluşur: `GameHud` (moda göre dolan üst şerit), `PowerBar` (prizma + güçler),
`ResultCard` (her bitişin tek kartı). Tura giriş hep `AppController.PlayClassic/PlayDaily/PlayLevel`
üzerinden — kayıttan devam ile yeni başlangıç tek yerde karar verilir.

Modallar **yığın** (`AppController._modals`). Duraklatmadan ayarlar açılınca kapanışta
duraklatmaya döner. Android geri tuşu da bu yığını izler.

Girdi tek yoldan: `PointerRouter` her kare `Pointer.current` okur. Widget'lar (`UiButton`,
`UiSlider`, `UiToggle`) önce hak iddia eder, kalan her şey `IPointerFallback`'e (tahta) düşer.
**EventSystem kullanılmıyor** — tüm UI koddan kurulduğu için tek girdi yolu daha basit.

---

## Kritik tuzaklar (hepsi bu projede canlı yaşandı)

1. **Linear renk uzayı.** Proje Linear'da render ediyor; alfa harmanlaması lineer yapılıyor.
   `%7 beyaz` koyu zeminde sRGB sezgisinin öngördüğünden **çok daha açık** çıkar (~%25 gibi).
   Bu yüzden yüzeyler yarı saydam beyaz değil, **opak renk değerleri** (`Design.SurfaceButton`,
   `SurfaceTrack`, `SurfaceInset`, `SurfaceLeader`). Sadece hairline'lar saydam kaldı.
   Yeni yüzey eklerken bu kurala uy.

2. **Play mode'da domain reload düz C# alanlarını siler.** Play mode açıkken script derlenirse
   `GameScreen._session` gibi alanlar null olur ve `Awake` **tekrar çalışmaz** — oyun yarı ölü
   kalır. Sadece Editor sorunu, build'de yok. Test ederken: script değiştirdiysen play mode'u
   **kapat/aç**, üstüne devam etme.

3. **Editor odakta değilken kare işlemez.** Coroutine'ler donar, `capture_game_view` bayat kare
   döndürür. Test öncesi mutlaka:
   ```csharp
   UnityEditor.EditorApplication.isPaused = false;
   Application.runInBackground = true;
   ```
   Bir kez play mode duraklatılmış kaldı ve modallar hiç render olmadı — sebebi buydu.

4. **Game view portre boyutu Unity yeniden başlayınca sıfırlanır.** Ekran görüntüsü kare
   çıkıyorsa "Portrait 1080x1920" özel boyutunu reflection ile tekrar seçmek gerekir
   (`UnityEditor.GameView.SizeSelectionCallback`).

5. **Kayıp fare tıklamaları.** Editor'de fare Game view üstündeyken araya tıklama girip ekran
   değiştirir. Ekran görüntüsü almadan önce `PointerRouter.enabled = false`, sonra geri aç.

6. **Bozuk `.meta`.** Bir kez iki script AssetDatabase'e hiç girmedi (derlemeye alınmadılar,
   `CompilationPipeline.GetAssemblies` listesinde yoktular). `Refresh` çözmedi; **dosya adını
   değiştirmek** çözdü. Bir tip "bulunamıyor" ama dosya diskteyse önce bunu kontrol et.

8. **Modal arkasındaki butonlara dokunulabiliyordu.** `PointerRouter` widget'ları yalnız kayıt
   sırasına göre tarıyordu; ayarlar kartının yanına dokunmak arkadaki "OYNA"yı çalıştırıyordu.
   Artık her modal ve sonuç kartı `PointerRouter.PushBlocker` ile işaretli; en üstteki görünür
   engelleyicinin dışındaki hiçbir widget (ve tahta) dokunma almaz. Yeni bir kaplayan yüzey
   eklersen onu da engelleyici yap.

7. **Dağıtıcı çok güçlü olursa oyun hiç bitmez.** Bir kez yardımı tavansız bıraktım; yetkin bir
   oyuncuyla tahta hiç dolmadı, test döngüsü sonsuza girdi ve **Unity'yi kilitledi**.
   `PieceDealer.AssistCeiling` bu yüzden var — silme.

---

## `PieceDealer` — en kritik ve en hassas sistem

Kör rastgele dağıtım bu türde adaletsiz hissettiriyor: uyarısız ölü el veriyor ve hiç combo
kurmuyor. Dağıtıcı her tepsi için **16 aday** üretip her birini yetkin bir oyuncu gibi sonuna
kadar oynatıyor, sonra sonuca göre puanlıyor (kaç taş yerleşti, kaç satır çıktı, ne kadar sıkı
oturdu, tahta ne kadar doldu).

### Tasarım felsefesi — buna uy
> Yardım **hissedilsin ama görülmesin**. Oyuncu yardım aldığını anlamamalı.
> Bazen zorlasın, bazen yardım etsin. Asla combo'yu tepsiye koyup sunmasın.

Bunu sağlayan iki mekanizma:

- **Dalgalanma** (`_drift`): assist tahtanın düz bir fonksiyonu değil, korelasyonlu rastgele
  yürüyüş ekleniyor. Yardım dalgalar hâlinde gelir — birkaç cömert el, sonra sıkıntılı bir
  dönem. Düz fonksiyon olsaydı oyuncu tam sıkıştığı anda oyunun yumuşadığını sezerdi.
- **Sıralama kapısı** (`Choose`): çoğu dağıtımda sıralama **tamamen atlanır** ve aday rastgele
  seçilir. Adaylar zaten rastgele üretildiği için bu düpedüz rastgele dağıtım demek. Sıralama
  sadece yardım gerektiğinde devreye girer.
  **Bu kapı olmadan, 16 adayı puanlayıp en iyisini seçmek tek başına devasa bir yardım** —
  assist 0 olsa bile. Bir kez bu yüzden yardım iki katına çıktı.

### Ayar noktaları
| Sabit | Anlamı |
|---|---|
| `BaseAssist` 0.16 | Boş tahtada bile uygulanan taban |
| `AssistCeiling` 0.82 | **Tavan. Turun bitmesini garantiler — kaldırma** |
| `PressureStart/Full` 0.35 / 0.80 | Yardımın başladığı ve tam güce ulaştığı doluluk |
| `FatigueScore/Max` 3500 / 0.5 | Uzun turda yardımın geri çekilme rampası |
| `DriftAmount/Memory` 0.55 / 0.7 | Dalgalanmanın genliği ve hafızası |
| `CandidateCount` 16 | Aday sayısı (maliyet burada) |

### Ölçülen denge
Casual oyuncu simülasyonu (skill 0.55) ve iyi oyuncu (0.80), her tur doğal bitiyor:

| | Rastgele | Akıllı |
|---|---|---|
| casual — hamle | 34 | **44** (+29%) |
| casual — temizleme | 9.5 | **14.9** (+57%) |
| iyi — hamle | 63 | **74** (+17%) |
| iyi — temizleme | 22.6 | **28.0** (+24%) |

İyi oyuncuda ortalama assist **0.09** — neredeyse hiç karışmıyor. Zayıf oyuncuya daha çok,
iyi oyuncuya daha az dokunuyor. Doğru şekil bu.

### Performans
**0.17 ms/dağıtım.** Bu ucuzluk şuna bağlı, bozma:
- `BoardModel` satır/sütun sayaçlarını hücre değiştikçe günceller → `CountCompletedLines`
  tahtayı taramaz
- `PieceShape._mask` → `ContactScore` iç içe arama yapmaz
- Dağıtıcı tek bir yeniden kullanılan `_scratch` tahta tutar, aday başına kopya almaz

İlk hali bunların hiçbirine sahip değildi ve dağıtım milyonlarca işlem sürüyordu.

---

## Prizma güçleri — PRIZMA'yı türdeşlerinden ayıran şey

Reklamla satılmaz, **oynayarak kazanılır**. Satır temizledikçe prizma dolar, dolunca 1 şarj
(en çok 3). Şarjlar üç güce gider: **Döndür** (1 — tepsideki parçayı çeyrek tur),
**Yenile** (1 — kalan parçaları yeniden dağıt), **Bomba** (2 — 3x3 alanı temizle).
Tek renk satır prizmaya 2 sayılır; tahtayı tamamen sıfırlamak doğrudan 1 şarj.

Hiçbir parça sığmıyor ama şarj varsa tur bitmez: `SessionState.Stuck`. Tepsi soluklaşır, prizmanın
yerinde "BİTİR" çıkar. Kurtarmak ya da bitirmek oyuncunun kararı.

Tek renk satır ("prizma satırı"): baştan sona tek renk temizlenen satır +120 × combo ve prizma
süpürmesi (`BoardView.PlayPrismSweep`). Tepsi renkleri rastgele olduğundan hedeflenebilir ama bedava değil.

Öğretim yine yazısız: güçlerin kurtarabileceği **ilk** sıkışmada sürükleme öğreticisinin eli zar
butonuna dokunur (`GameScreen.MaybeHintPowers`), bir güç kullanılınca `Progress.PowersHinted` ile
bir daha çıkmaz. Bölüm kazanınca yıldızlar bir temanın eşiğini geçtiyse sonuç kartının not satırı
"YENİ TEMA: …" olur.

| `PowerRules` | Değer | Anlamı |
|---|---|---|
| `StartCharges` | 1 | Klasik/günlük başlangıç. Casual oyuncunun gerçekten kullandığı kurtarış |
| `LinesPerCharge` / `ChargeStep` | 30 / 20 | İlk şarj 30 satır, her sonraki 20 satır daha pahalı |
| `MaxCharges` | 3 | |

Ölçülen (`Tools/CoreHarness balance 40`, palet 7, her tur doğal bitti — capped 0):

| | Güçsüz | Güçlü |
|---|---|---|
| casual (0.55) — hamle | 35.5 | **52.1** (+47%) |
| iyi (0.80) — hamle | 84.2 | **150.5** (+79%) |

Nasıl buraya gelindi — **sabit fiyata dönme**:
- Sabit 10 satır: iyi oyuncunun turu **4 katına** çıktı. Güçler dağıtıcının işini yapıp gerilimi öldürdü.
- Başlangıç şarjı 0: casual oyuncu ilk şarja hiç ulaşamadı (+%12) — güçler yalnız ihtiyacı olmayana yaradı.
- Artan fiyat + 1 başlangıç şarjı ikisinin ortası. İyi oyuncu hâlâ daha çok kazanıyor; bir sonraki
  ayar denemesi `ChargeStep`'i artırmak olmalı, tavansız bırakmak değil.

---

## Macera modu ve `LevelGenerator`

Hiçbir bölüm saklanmaz: **numara → aynı tahta, hedef ve hamle bütçesi**. Mod sonsuz.
İlk 3 bölüm elle yazılmış öğretici (`LevelGenerator.Authored`): tek hamlelik kristal, bir satır +
bir sütun, ilk buz. Yazı yok — tahta, doğru hamle bariz olacak şekilde kurulu.

- **Kristal** (`BoardModel` katmanı): hücresi temizlenince toplanır, HUD'daki sayaca uçar.
- **Buz**: her temizleme bir kat kırar, blok ancak buz bitince gider (1 veya 2 kat). Bomba ikisini de alır.
- Hedefler: kristal (çoğu) / satır (her 6. bölüm) / puan (6k+3). 1-3 yıldız artan hamleye göre.
- Hamle biterken 2 şarj varsa tur hemen bitmez (`SessionState.OutOfMoves`): sonuç kartı
  **+5 hamle** teklif eder, bölüm denemesi başına bir kez. Ek hamleyle bitirilen bölüm **1 yıldız** —
  kurtarış, en iyi skora kestirme değil. Şarjların prizma dışındaki tek harcama yeri.

**Hamle bütçesi tahmin değil, ölçüm:** taslak bot (skill 0.62) ile 15 kez oynanır, %75'lik dilimdeki
hamle sayısı × pay (1.40 → 1.15, zorlukla azalır), ilk 10 bölüme +6. Bot %60'tan az kazanıyorsa taslak
atılır, daha hafifi denenir. 7 koşu + medyan ile komşu bölümler %8 ile %100 arasında savruluyordu.

Bot kristal satırlarını **hedefler** (`Autoplayer.CellsOnGemLines`). Bu olmadan kristaller tesadüfen
toplanıyordu; bütçeler hem gevşek hem düzensizdi ve bölüm 1'i iyi bot bile %83 geçiyordu.

Ölçülen (`levels 1 50`): iyi oyuncu (0.85) çoğu bölümde %92–100; casual (0.50) %17–100; bölüm 1–2 %100.
Üretim 10–80 ms, önbellekte; bir sonraki bölüm arka planda (`ThreadPool`) hazırlanır — `LevelGenerator`
Unity'ye dokunmadığı için güvenli.

---

## Kayıt ve ilerleme

- `RunStore`: mod başına bir yuva. Her hamlede ve uygulama arka plana geçince `SessionSnapshot` (JSON).
  RNG durumu da içinde, yani devam edilen tur **birebir aynı** dağıtımla sürer (testi var).
  `GameSession.Restore` bozuk kaydı reddeder → kayıt atılır, oyun çökmez.
- Günlük kayıt yalnız başladığı gün devam eder. Bölüm kaydı yalnız aynı bölüme.
- `Progress`: yıldızlar, günlük seri (`DailyStreak` dün ya da bugün oynandıysa yaşar), yaşam boyu
  istatistikler, tema, renk körü modu, öğretici görüldü.
- `Themes`: yalnız blok paleti değişir, yıldızla açılır. Her palette 7 renk ve tonlar birbirinden uzak —
  tek renk satır bonusu yüzünden birbirine benzeyen iki renk tuzak olur.

---

## Kare hızı (60 FPS) — bunları geri alma

İlk APK telefonda 20-30 FPS'te döndü. Sebepler ve düzeltmeler:

- **Android, `Application.targetFrameRate` ayarlanmazsa 30'a kilitler.** `AppController.Awake`
  60 istiyor. En büyük etken buydu.
- **Kamera boşa çalışıyordu.** Tüm oyun Overlay canvas; kamera sadece ekranı temizliyor. Varsayılan
  hâli skybox'ı HDR tampona %80 ölçekte çizip bloom/tonemap/post yığınını çalıştırıp büyütüyordu —
  hepsi backdrop'un arkasında, görünmez. `AppController.ConfigureCamera` bunu kapatıyor.
  `Mobile_RPAsset`: HDR kapalı, render scale 1 (1'in altı ara doku + büyütme geçişi demek).
- **Backdrop'un "Field" katmanı yok**: kameranın temizleme rengi zaten `Design.BgTop`. Tam ekran
  yarı saydam bir katman eksik. Zemin rengini değiştirirsen kamera da onu kullanıyor, ayrıca bir şey
  yapma.
- **İç içe canvas'lar**: `Backdrop` (glow her kare nefes alıyor) ve `DragLayer` (tutulan parça her
  kare hareket ediyor) kendi `Canvas`'ında. Paylaşılan canvas'ta her kare **tüm UI** yeniden
  batch'leniyordu. Her kare hareket eden yeni bir şey eklersen ona da kendi canvas'ını ver.
- **Hayalet/önizleme sadece hücre değişince yenileniyor** (`GameScreen._hover*`). Önceden her kare
  ~12 görüntüyü kapatıp açıyordu.
- **`Assets/Editor/AndroidPerformanceSettings`**: Optimized Frame Pacing (Swappy) açık, Blit Type
  Auto. Editor açılışında ve her Android build'inden önce uygulanıyor. Development Build açıksa
  uyarı veriyor — FPS'i development build'de ölçme.

Hâlâ 60 tutmazsa sıradaki aday: backdrop'un 5 tam ekran katmanını (Deep/Glow/Grid/Vignette/Grain)
tek bir RenderTexture'a bir kez çizmek. Görünüm birebir kalmalı — Linear renk uzayında sRGB RT şart.

---

## Tasarım dili

`Design.cs` **tek kaynak**. Çağrı yerinde ham sayı yazma; token ekle.

- Tipografi ölçeği: Readout 156 / Display 118 / Title 64 / Headline 50 / Body 40 / Label 34 / Caption 28
- Aralık: 8 / 16 / 24 / 40 / 56 / 80 / 112 · kenar boşluğu 56 · min dokunma hedefi 120
- Şekil: **squircle** (süperelips) — düz yuvarlak dikdörtgen değil. `Raster.FillSquircle`
- Derinlik: **bulanık ambient gölge**, sert alt dudak değil. Offset küçük, yayılım büyük —
  büyük offset gölgenin dolu çekirdeğini elemanın altından taşırır ve "dudak" gibi okunur
- Renk: neredeyse siyah taban + mor/turkuaz/erik mesh gradyan havuzları + tek canlı vurgu

### Arayüz kuralı — bunu ihlal etme
> **Yazıyla anlatma. Yanıp söndürme.**

Bir kez "2 SATIR BİRDEN" rozeti ve tamamlanmaya yakın hücreleri nabız attıran ipucu eklendi;
ikisi de kaldırıldı. Ön-temizleme önizlemesi (`BoardView.PreviewLines`) **sabit ve sessiz**:
etiketsiz, nabızsız, düşük alfa. Renk kademesi var (1 satır nane / 2 altın / 3+ gül) ama
göze sokmuyor. Yoğunluk ayarı: `BoardView.AddPreviewBar` içindeki iki alfa değeri (0.22 / 0.13).

---

## Doğrulama yöntemi

Ekran görüntüsüne güvenme — **durumu koda sor**. `mcp__unity-editor-mcp__eval` ile private
alanlara reflection'la erişip modeli ve görünümü karşılaştır. Bir noktada model 81 derken ekran
0 gösteriyordu; sorun kodda değil, editörün kare işlememesindeydi.

Denge değişikliklerinden sonra simülasyonla ölç (casual/iyi oyuncu, rastgele vs akıllı).
**Hamle sınırı koymayı unutma** — sınırsız döngü Unity'yi kilitler.

### Unity'siz doğrulama (`Tools/`)
- `Tools/CoreHarness` — Core'u Unity olmadan derleyip kural testlerini, dengeyi ve bölüm eğrisini koşar.
  Unity'nin kendi SDK'sı yeter: `<Unity>/Editor/Data/DotNetSdk/dotnet.exe build Tools/CoreHarness -c Release`,
  sonra `...Harness.dll tests` / `balance 40` / `levels 1 60`. Denge ya da kural değişince **önce bunu koş**.
- `Tools/compile-check.ps1` — oyun (normal + `PRIZMA_AUTOTEST`) ve editor kodunu Unity'nin derleyici
  argümanlarıyla derler. Editor kapalıyken/odakta değilken derleme hatası yakalar.
- `Tools/autotest.ps1` — projeyi geçici klasöre kopyalar, batchmode Unity ile `PRIZMA_AUTOTEST` tanımlı
  Windows build alır, dikey pencerede çalıştırır. `AutoTest` her ekranın görüntüsünü (ikon dahil)
  `%TEMP%\prizma_autotest\shots` altına yazar. Tahtalar gerçek bot hamlelerinden ve elle kurulmuş
  kayıtlardan geldiği için görüntü ile durum aynı anda doğrulanır. Tanım gerçek build'e girmez.

`eval` çağrıları 60 saniyede zaman aşımına uğrar; ağır simülasyonu küçük parçalara böl.

---

## Durum

**Bitti:** çekirdek oynanış · akıllı dağıtıcı · ana menü · duraklatma menüsü (yeniden başlat dahil) ·
ayarlar (müzik/efekt + sessize alma + titreşim + renk körü modu) · yerel ilk 10 skor ·
prosedürel görsel dil · sentezlenmiş ses ve müzik · ön-temizleme önizlemesi · combo göstergesi ·
+N puan balonu · **Prizma güçleri** (döndür/yenile/bomba, sıkışma durumu) · tek renk satır ve
tahtayı sıfırlama bonusları · **Macera modu** (sonsuz, kristal/buz, yıldız, harita) ·
**Günlük bulmaca** + seri · **kaldığın yerden devam** (her mod) · istatistikler · temalar ·
yazısız el öğreticisi · kısa native haptik · uygulama ikonu + açılış rengi · 60 FPS ayarları.

**Sırada:** Android'de **gerçek cihazda denemek**. Masaüstünde ölçülemeyecek şeyler:
önizlemenin parmak altında okunurluğu, yardımın gerçekten fark edilmezliği, seslerin telefon
hoparlöründe tınısı, sürükleme mesafesi (`GameScreen._liftPixels`), güç çubuğunun başparmak
erişimi, bomba nişanının parmak altında görünürlüğü, haptik şiddeti (`AppController.Tick/Vibrate`).

Sonrası: çevrimiçi lig (günlük tohum hazır, motor deterministik), başarımlar, sürüm numarası ve
mağaza hazırlığı (imzalama anahtarı, gizlilik metni).

**Çalışma adı** PRIZMA — `MainMenuScreen.GameName`, tek satır.
