# PRIZMA — blok bulmaca

Unity 6 (6000.6.0f1) · URP · portre mobil, hedef Android.
Block Blast türünde 8x8 blok bulmaca. Arayüz metinleri **Türkçe**.
Üç mod: **Klasik** (sonsuz), **Günlük** (günde bir hedefli bulmaca, süre + seri + rozet + bildirim), **Macera** (100 bölüm,
10 dünya, Candy Crush tarzı yol haritası; kristal / ışık karosu / taş / gölge / renk siparişi / saatli blok / buz, zor
bölümler, güçlendiriciler, galibiyet serisi, dünya sandıkları). Hepsinde **Prizma güçleri**.

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

Ekranlar `AppScreen`'den türer. Sayfalar: `MainMenuScreen`, `GameScreen`, `LevelSelectScreen` (harita, `AdventureScreens.cs`).
Modallar (`SheetScreen`, `ModalScreens.cs` + `DailyScreen.cs` + `AdventureScreens.cs`): `SettingsScreen`, `ScoresScreen`, `PauseScreen`,
`StatsScreen`, `ThemesScreen`, `DailyScreen`, `LevelStartScreen`, `ChestScreen`. Hepsi **alt sayfa** — aşağıda "Modallar" başlığı.
`GameScreen` üç parçadan oluşur: `GameHud` (moda göre dolan üst şerit), `PowerBar` (prizma + güçler),
`ResultCard` (her bitişin tek kartı). Tura giriş hep `AppController.PlayClassic/PlayDaily/PlayLevel`
üzerinden — kayıttan devam ile yeni başlangıç tek yerde karar verilir.

Modallar **yığın** (`AppController._modals`). Duraklatmadan ayarlar açılınca kapanışta
duraklatmaya döner. Android geri tuşu da bu yığını izler. Kapanış animasyonlu (`AppScreen.Dismiss`): sayfa inene kadar
yığında ve engelleyici olarak kalır; ikinci bir kapanış/açılış gelirse ilki anında tamamlanır (`FinishDismiss`).

### Modallar — alt sayfa (`ModalKit.cs`), oyun dilinde
İki tur geri bildirim: önce "modallar çok karmaşık… 2010-2015 gibi arayüz istemiyorum" (ortada kart + altta KAPAT;
şeritli başlık plakası + kırmızı X denemesi de reddedildi — **ona dönme**). Sonra alt sayfaya geçildi ama iOS ayarlar
dilinde yapıldı (tutamak, sola yaslı başlık, gri X, ikon-ad-anahtar satırları, satır arası hairline, kayan segment) ve
kullanıcı "mobil web sitesine benziyor" dedi. Karar: **yapı kalsın, dil oyunlaşsın, animasyonlu olsun.**

- `Sheet`: alttan kalkar, üst köşeler `SheetRadius`, alt kenar jest çubuğunun altına taşar. Üst kenarından yarısı
  taşan **madalyon** (`SheetHero` 176: beyaz halka + sayfanın renginde parlak disk + beyaz işaret + renkli hale),
  altında **ortalı** büyük başlık, sayfanın üstünde o rengin ışığı (`Art.TopWash`). Tutamak yok (aşağı çekme yine
  çalışır). Kapat: sağ üstte plastik disk (`UiButton.Style.Round`, dokunma alanı 144). Dört kapanış yolu aynı.
  `BuildSheet(title, icon, tint)`; `Sheet.SetHero` sonradan renk/işaret değiştirir (bölüm sayfası dünya rengini alır).
  Madalyonsuz sayfa (sonuç kartı, sandık) eski başlık düzenini kullanır.
- **Hareket:** sayfa `Ease.OutBackSoft` ile hafif taşıp oturur, madalyon dönerek "pop" eder, başlık sonra gövdenin
  parçaları yukarıdan aşağı sırayla gelir (`Cascade`: yalnız ölçek + saydamlık — yerleşime dokunmaz, yarıda kesilen
  giriş hiçbir şeyi kaydırılmış bırakmaz; dinlenme saydamlığı `Cascade.Rest`'te saklanır). Madalyon açıkken hafif
  süzülür. Butonlar bırakınca yaylanır, anahtar topuzu esneyerek kayar, kaydırıcı topuzu basılıyken büyür,
  karolar dokununca madalyonu döner, istatistik sayıları sayarak yükselir, başarım çubukları dolar, skor tablosu
  sırayla gelir, sandık açılırken sallanıp parlar.
- **Malzeme:** kartlar ve butonlar blokla aynı yumuşak plastik (`Art.PanelGradient`, üstte açık kenar, yumuşak
  gölge). Gradyan sprite'ın altı koyu olduğundan renk `SheetKit.Lifted` ile bir tık açılır.
- **Liste yerine karo:** açma/kapama ayarları 2×2 `UiTile` (açıkken kendi renginde — rengi yüzeye **karıştırma**,
  hardal/kahverengi olur; kendi rengini koyulaştır — kapalıyken sade, köşede tik). Duraklatma: üç eylem karosu +
  DEVAM ET. Satır arası tam genişlik hairline yerine kısa yumuşak oluk. Segment başparmağı vurgu renginde.
- Yüzey katmanları aynı: sayfa `SurfaceSheet` < grup `SurfaceGroup` < kontrol `SurfaceControl`.
- **Hiçbir sayfaya "KAPAT" butonu koyma.** Sığmayan gövde bütün olarak küçülür (madalyonun taşan yarısı da hesapta).
- Görünüş/hareket üzerinde çalışırken: `Tools/autotest.ps1 -Only modals` (80_…92_, giriş ortası + oturmuş hâl).

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

9. **Açılmayan APK.** Bir APK telefonda Unity splash'ından sonra boş varsayılan gökyüzünde kaldı:
   build, sahnedeki `GameRoot` → `AppController` script'ini çözememişti. Unity bunu yalnızca log'da
   bir uyarıyla geçti ("Script attached to 'GameRoot' … is missing") ve build'i **başarılı** saydı.
   Oyunun tamamı o tek bileşene bağlı olduğundan hiçbir şey kurulmadı. Build, projenin geçici bir
   kopyasından, üstelik o kopya başka bir yoldan (junction) açılarak alınmıştı — kod sağlamdı.
   - `Editor/ReleaseBuild` artık build öncesi `AppController`'ın bir sınıfa çözüldüğünü kontrol ediyor,
     build sırasında "missing script" uyarısını yakalıyor; ikisinden biri olursa APK'yı **siliyor**
     ve `Builds/PRIZMA-build.txt`'ye nedenini yazıyor. Bu korumayı kaldırma.
   - APK'yı gerçek projeden al (menü: *PRIZMA → Android APK Al*, ya da Editor kapalıyken batchmode
     `-executeMethod BlockPuzzle.EditorTools.ReleaseBuild.BuildAndroid`). Kopyadan build sadece
     `Tools/autotest.ps1`'in Windows testi için.
   - Uzun proje yolu Android'de Gradle'ı düşürür (`prefab_command.bat` 260 karakter sınırını aşar,
     "CreateProcess error=2"). `%TEMP%` altındaki kopyalardan Android build alma.

10. **Statik olaya abonelik, yeniden kurulan ekranda hayalet bırakır.** `AppScreen.Show` zaten açık bir
    sayfada tekrar çalışır (yeniden başlat, "TEKRAR OYNA"). `GameScreen` her seferinde `Progress.Changed`'e
    abone olup çıkışta bir kez ayrılıyordu. Ekran hep yaşadığı için zararsızdı — tema değişimi ekranı yok
    edince kalan abonelik yok edilmiş tahtaya dokunup `NullReferenceException` attı. Statik bir olaya
    abone olan her ekran: abonelikten önce çık (`-=` sonra `+=`) ve `OnDestroy`'da da çık.

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
| `BaseAssist` 0.26 | Boş tahtada bile uygulanan taban |
| `AssistCeiling` 0.82 | **Tavan. Turun bitmesini garantiler — kaldırma** |
| `PressureStart/Full` 0.25 / 0.68 | Yardımın başladığı ve tam güce ulaştığı doluluk |
| `PressureCurve` 1.25 | Rampanın şekli. Kareli hâli fazla arkaya yüklüydü: yarı dolu tahta tam gücün onda birini alıyordu, yani yardım ancak tur zaten kaybedilmişken geliyordu |
| `FatigueStart/Score/Max` 3000 / 14000 / 0.5 | Uzun tur rampası. Öncesi 0'dan başlayıp 3500'de yarıyı alıyordu — sıradan bir casual tur (2000 puan) daha kısacıkken yardımının üçte birini teslim etmiş oluyordu. Artık casual'ın ulaştığı yerin ötesinde başlıyor |
| `MaxSmallBias` 1.15 | Tahta sıkışıkken **ve** boşalmak üzereyken küçük parçaya yönelme |
| `DriftAmount/Memory` 0.55 / 0.7 | Dalgalanmanın genliği ve hafızası |
| `CandidateCount` 16 + `CandidateBonus` 28 | Aday sayısı. Açık tahtada 16 yeter; **sıkışık tahtada asıl tavan buydu** — bütün ayarlar sonuna kadar açıkken bile dağıtıcı 16 rastgele tepsinin en iyisini seçebiliyordu ve hiçbiri uymuyor olabilirdi. Ek adaylar yalnız yardım gerekirken üretiliyor |

### Sıralama terimleri — hangisi neyi satın alıyor
`ScoreOutcome` içindeki her terim oyuncunun hissettiği ayrı bir şeye karşılık geliyor:

| Terim | Ne satın alıyor |
|---|---|
| `Placed` eksikse ceza (150 + 650·assist) | **Turun uyarısız bitmemesi.** Eskiden 200·assist idi, yani yüzlerce puanlık terimlerin yanında ~60 puan ederdi ve hiç yarışamıyordu |
| `FitsNow` eksikse ceza (120 + 520·assist) | **İnsanın kendini kilitlememesi.** `Placed` aynı sözü vermiyor: o, tepsiyi *en iyi sırayla* oynayan bir simülasyondan geliyor. Oyuncu altı sıralamayı deneyip iyisini seçemez — yanlış parçayı önce koyar ve kalan ikisi sığmaz. Bu terim "şu anda, herhangi bir sırayla kaç parça sığıyor"u sayar |
| `ClearingMoves` (60 + 170·assist) | **Serinin yaşaması.** Ekrandaki çarpan üst üste temizleyen *hamleleri* sayıyor |
| `BestSingle > 1` (70 + 150·assist) | **Tek hamlede çok satır.** `Lines` bunu göremiyordu: üç hamlede üç satır ile tek hamlede üç satır ona aynı görünüyordu |
| `Primed` (9 + 26·assist) | **Bir sonraki temizlemenin var olması.** Tahtayı boşa itmek oyuncuya yer açar ama boş tahtada temizlenecek bir şey de yoktur — bu terim olmadan seri 1.3'te çakılı kalıyordu |
| `FinalOccupancy` cezası (7 + 20·assist) | Nefes alacak yer. Fazla bastırılırsa yukarıdaki terimle çelişir |
| ≤12 hücre ve 0 hücre ikramiyeleri | Tahtayı bitirme anı |

### Ölçülen denge
`Tools/CoreHarness balance 60`, palet 7, güçler açık, her tur doğal bitiyor (**capped 0**).
Süpürmeye **skill 0.40 eklendi** — 0.55 ve 0.80 ile ölçerken asıl zorlanan oyuncu hiç görünmüyordu,
ve şikâyet eden oyuncu oydu.

| | Önce | Sonra |
|---|---|---|
| zorlanan (0.40) — hamle (ort / medyan) | — | **110.0 / 112** |
| zorlanan — temizleme / skor | — | **38.7 / 4698** |
| casual (0.55) — hamle (ort / medyan) | 52.1 / 53 | **166.1 / 185** |
| casual — temizleme | 17.1 | **62.5** |
| casual — skor | 1995 | **7461** |
| casual — en uzun seri | 2.6 | **3.4** |
| iyi (0.80) — hamle | 150.5 | **498.3** |
| iyi — skor | 6936 | **24184** |

Casual tur **3.2 kat** uzadı. Medyanın ortalamaya yaklaşması (0.40'ta 112 / 110) en az uzunluk
kadar önemli: dağılım daraldı, yani **ani erken ölümler** kalktı.

Çok satırlı temizlemenin **oranı** benzer kaldı (%7-11), **adedi** ise tur başına ~1.2'den
~5'e çıktı. Daha sık temizlendiği için iki satırın aynı anda olgunlaşmasına daha az fırsat kalıyor;
oyuncunun gördüğü şey yine de kat kat fazla combo.

### Ölçümün ortaya çıkardığı iki şey — tahminle değiştirme
1. **Turlar tahta dolduğu için bitmiyor.** Bitişte tahta ortalama **%52-61 dolu**, ve tur boyunca
   %60'ın üstünde geçen süre sadece **%1-7**. Yani ölüm "yer kalmadı"dan değil, **elindeki parça
   deliklere uymuyor**dan geliyor. "Boş yerim oldukça az" şikâyetinin gerçek karşılığı bu —
   ve çaresi `MaxSmallBias` ile oynanamaz-tepsi cezası, doluluk tavanı değil.
2. **Tahtayı tamamen temizlemek ikramiyeyle satın alınmıyor, adayla satın alınıyor.** Tahta
   neredeyse her turda bir noktada **~3.9 hücreye** kadar iniyor ama kalanlar 2.4 satır × 2.3
   sütuna dağılmış oluyor. İkramiyeyi 900·assist'e kadar çıkarmak hiçbir şey değiştirmedi — çünkü
   sorun dağıtıcının *istemesi* değildi, 16 adayın arasında işi bitirecek tepsinin **bulunmamasıydı**.
   Aday havuzu yardım altında genişleyince tahta boşalmaya başladı: 0 → iyi oyuncuda turların
   **%7'si**. Ders: bir şey sıralamayla düzelmiyorsa, önce o şeyin aday havuzunda var olup
   olmadığına bak.

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
| `StartCharges` | 2 | Klasik/günlük başlangıç. 1 iken zorlanan oyuncu bütün turu o tek şarjla geçiriyordu: kurtarış bir kez vardı, sonra bir daha yoktu |
| `LinesPerCharge` / `ChargeStep` | 12 / 26 | İlk şarj ucuz, sonrakiler dik. **30 / 20 iken ilk şarj, kurtarmayı amaçladığı oyuncunun menzilinin dışındaydı**: zorlanan bir tur toplam ~20 satır temizliyor, yani tek bir şarj bile kazanamıyordu |
| `MaxCharges` | 3 | |

Ölçülen: zorlanan oyuncu (0.40) tur başına 1.2 güçten **2.5 güce** çıktı. Dik `ChargeStep` sayesinde
iyi oyuncu 3.8'de kaldı — kurtarış olmayı sürdürüyor, yaşam biçimi olmadı.

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

Kullanıcı: "candy crush gibi bir yol olsun, 100 bölüm, bir çok yeni özellik; tasarım bizim dilimizde olsun".
Eskiden sonsuz bir mod ve 4×6 numaralı karo ızgarası (sayfa sayfa) idi. Artık **100 bölüm, 10 dünya**, kıvrılan bir
yol haritası. Hiçbir bölüm saklanmaz: **numara → aynı tahta, hedef ve hamle bütçesi**.

### Dünyalar ve yeni öğeler (`LevelGenerator.*From`)
Her dünya bir yeni şey getirir; ilk bölümü (`IsIntro`) o öğeyi **elle kurulmuş, kolay** bir tahtada yazısız gösterir
(`LevelGenerator.Intro`) — ilk 3 bölümün öğretici mantığıyla. Günlük bulmaca bunların hiçbirini almaz (`adventure: false`),
ölçülmüş haftası kaymasın diye.

| Dünya | Bölüm | Getirdiği | Nasıl çalışır |
|---|---|---|---|
| 1 Kristal Kıyısı | 1 | kristal, satır, puan; 6'dan buz | eski kurallar |
| 2 Işık Tarlası | 11 | **Işık karosu** (`GoalKind.Tiles`) | Candy Crush'ın jölesi: zemin katmanı (`BoardModel._tiles`), boş hücrede de durur; üstündeki blok **temizlenince** söner. Hedef: hepsini söndür |
| 3 Taş Geçit | 21 | **Taş** | satırı doldurur, temizlemeyle gitmez; bomba/çekiç kırar |
| 4 Gölge Ormanı | 31 | **Gölge** (`GoalKind.Shade`) | Çikolata: hücre değeri `BoardModel.Shade` (−2), renksiz blok. `LevelDefinition.ShadeSpread` sessiz hamlede bir boş komşuya yayılır (`SpreadShade`, oturum RNG'si → deterministik). Gölge temizleyen hamle sayacı sıfırlar. Tek renk satır olamaz |
| 5 Renk Çarşısı | 41 | **Renk siparişi** (`GoalKind.Colors`) | `OrderColor` renginde N blok temizle; `ClearResult.ClearedColors` her silinen hücrenin rengini taşır |
| 6 Saat Kulesi | 51 | **Saatli blok** | `BoardModel._timers`: her hamle 1 azalır (`TickTimers`), 0 olursa tur **şarjdan bağımsız** biter (`LossReason.Timer`). Satırı temizlenirse söner. Hedefe ulaşan hamle önceliklidir |
| 7 Buz Sarayı | 61 | **Çift buz** yoğun | |
| 8–10 | 71–100 | hepsi karışık | Fırtına Tepesi · Yıldız Denizi · Prizma Tacı |

Hedef seçimi (`PickGoal`): 1–10 eski döngü; sonra ayrı bir RNG akışıyla, öğesini getiren dünyada yarı yarıya o hedef,
geri kalanı açılmış hedeflerden ağırlıklı (kristal en sık).

### Zor bölümler (`HardnessOf`)
Her dünyanın 10. bölümü **çok zor** (taç rozeti, mor halka), 2. dünyadan itibaren 6. bölüm **zor** (şimşek, gül halka).
Harita ve başlangıç sayfası işaretler — uyarılmadan gelen duvar oyuncuya haksızlık gibi gelir. Zorluk iki yoldan:
tahta daha yüksek kademeden kurulur (`DifficultyTier`: +12 / +24, intro −14) **ve** bütçe botun daha iyi günlerinden
(`percentile` 0.65 / 0.55, pay ×0.92 / ×0.84). Yalnız tahta zorlaştırmak yetmedi: `Difficulty` 100 civarında doyuyor,
100. bölüm 99'dan kolay çıkıyordu.

**Bütçe tavanı** `MaxAdventureBudget` 70: botun 100+ hamle istediği taslak sıkıcı, zor değil → daha hafifi denenir.

### Ölçülen (`Tools/CoreHarness levels 1 100`, deneme başına kazanma)
| Dünya | zorlanan 0.40 | casual 0.50 | iyi 0.85 |
|---|---|---|---|
| 1 | %79 | %87 | %99 |
| 3 | %68 | %77 | %98 |
| 5 | %58 | %73 | %95 |
| 7 | %57 | %70 | %95 |
| 9 | %47 | %58 | %94 |
| 10 | %49 | %68 | %88 |

Çok zor bölümler casual'da %25–58, 100. bölüm iyi oyuncuda %33 (final). Üretim 10–230 ms (renk hedefi en yavaş),
önbellekte; harita ve başlangıç sayfası bir sonrakini `ThreadPool`'da hazırlar.

### Bot (`Autoplayer.BuildWeights`)
Her hedef ve engel **hücre başına bir ağırlık**: kristal 160, ışık 150, sipariş rengi 60, gölge 150/40, saat
`120 + 900/kalan`. Temizlenen satırlardaki ağırlık toplamı + hedef satırlara konan hücreler. Bütçe bu botla ölçüldüğü
için bot hedefi kovalamazsa bütçeler gevşek ve düzensiz olur (kristalde bir kez yaşandı).

### Güçlendiriciler, seri, sandık (`Progress`, `Booster`)
Reklam/satın alma yok — **oynayarak** kazanılır.
- **+3 Hamle** ve **Şarj** bölüm başlamadan başlangıç sayfasında seçilir (`AppController.PlayLevel(n, moves, charge)`),
  yalnız gerçekten yeni bir deneme başlarken harcanır. Ek hamleler (`SessionConfig.ExtraMoves`) **yıldıza sayılmaz**
  (`StarsEarned` onları önce harcanmış sayar) — güçlendirici geçirir, daha iyi sonuç satın almaz.
- **Çekiç** oyun içinde: HUD'da skorun sağında, stok rozetiyle (yalnız macerada). Tek bloğu buz/taş/gölge/saat dahil
  kırar (`GameSession.TryHammer`, şarj yemez). Stokta çekiç varken sıkışma `Stuck` olur (`HasBoosterRescue`,
  kaydedilmez — oyun her `Begin`'de stoktan kurar).
- **Galibiyet serisi** (`WinStreak`, en çok 3): 1 → +2 hamle, 2 → +2 hamle +1 şarj, 3 → +4 hamle +1 şarj. Kayıp,
  ya da hamle yapılmışken yeniden başlatmak sıfırlar.
- **Dünya sandığı**: dünyanın son bölümü bitince yolun üstündeki sandık altın olur; açınca her güçlendiriciden 1
  (4. dünyadan itibaren 2 çekiç, son dünya 3'er). İlk açılışta başlangıç stoku (2 / 2 / 3).

### Harita (`AdventureScreens.cs` — `LevelSelectScreen`, `MapScroll`)
- **Işık yolu.** Kullanıcı ilk hâli beğenmedi: "yolların kenarındaki siyah border". Şerit koyu bir `SurfaceInset`
  yatağın üstündeydi ve her yerde ağır bir siyah çerçeve gibi okunuyordu. Artık yatak yok: **gidilmiş yol yanar** (dünya
  renginin açığı şerit + beyaza yakın çekirdek + dünya renginde yumuşak parıltı), **gidilmemiş yol sessiz nokta izi**.
  Yol, Catmull-Rom eğrisinde **mesafeyle** örneklenir (parçalar/noktalar eşit aralıklı); durak diski yolun ucunu örter.
- Durak: bitmiş = dünya renginde yumuşak plastik disk (`Art.Node`), beyaz halka, altında 3 yıldız. Sıradaki = büyük
  **düz beyaz** disk, sayı dünya renginde, dünya renginde halka + hale, üstünde prizma işaretçisi (hafif süzülür).
  Kilitli = küçük **buzlu cam** (beyaz %14, halka %22). Burada saydamlık **bilerek**: opak dünya+zemin karışımı
  yarı dünyalarda çamurlu bej çıktı; cam her zeminin rengini alır. Zor kilitli bölüm cam, zor renginde tonlu —
  saydam camın altında dolu halka dolu disk gibi görünüp "oynanmış" okunuyordu.
- Dünya girişinde kart: dünya renginde amblem (dünyanın işareti), "DÜNYA n" + ad, yıldız sayısı ve altın ilerleme
  çubuğu; ulaşılmamışta kilit. Her dünya kendi **renk alanı** (iki geniş havuz + üstte bir sonraki dünyanın rengi,
  geçiş yumuşak), arkada **paralaks** (0.3) süzülen kristaller ve dünyanın motifi. Blok şekilli süs yok: soluk bloklar
  yerde yatan parça ya da çamur gibi okunuyordu.
- Başlık kendi `Canvas`'ında: içerik de iç içe canvas ve sonra gelen kardeşlerin üstüne çiziliyordu (yol başlığın
  üstünden geçiyordu). Üstteki geçiş 180° döndürülmüş `VerticalFade` — pivot **ortada** olmalı, üstteyken ekran dışına dönüyordu.
- Görünüş üzerinde çalışırken: `Tools/autotest.ps1 -Only map` (70_…76_ görüntüleri, ~4 dk).
- **Odanın ışığı ekrandaki dünyanın rengine döner** (`Backdrop.SetLight`) — Spektrum'la aynı dil.
- Kaydırma: kendi widget'ı (`MapScroll`), fırlatma + yumuşak kenar. `UiScroll`'dan farkı **dokunmayı** bildirmesi
  (22 birimden az hareket = dokunma, içerik koordinatında). Duraklar buton değil, çizili.
- Performans: harita **ilk ziyarette** kurulur (~3000 görüntü; açılışı yavaşlatmasın), içerik kendi `Canvas`'ında,
  yalnız görünen ±700 birimdeki dünyalar açık. Numaraların gölgesi **tek paylaşılan materyal** (`ShadowNumber`) —
  TMP her gölgeli etikete ayrı materyal verir, 100 etiket 100 draw call olurdu.
- **Kazanınca yürüyüş**: sonuç kartı "SONRAKİ" → harita (`ShowLevelSelect(afterWin)`), yol yeni bölüme kadar dünya
  rengiyle dolar, işaretçi sekerek geçer, sonra o bölümün başlangıç sayfası kendiliğinden açılır. Dünya bitmişse
  açmaz — önce sandık görülsün. `Progress.MapRevealed` yolun son çizildiği yeri tutar; bir adımdan fazla fark
  (eski kayıt) animasyonsuz gösterilir.
- **Başlangıç sayfası** (`LevelStartScreen`): dünya satırı + zor çipi, hedef ve hamle kartı, en iyi yıldızlar ya da
  intro'da "YENİ · …" çipi, seri satırı (3 alev + hediye), 3 güçlendirici karosu, OYNA. Kayıtlı deneme varsa DEVAM ET
  ve güçlendiriciler kapalı. Kaybedince "TEKRAR DENE" de buraya gelir (güçlendirici seçilebilsin).

- Hamle biterken 2 şarj varsa tur hemen bitmez (`SessionState.OutOfMoves`): sonuç kartı
  **+5 hamle** teklif eder, bölüm denemesi başına bir kez. Ek hamleyle bitirilen bölüm **1 yıldız**.

**Hamle bütçesi tahmin değil, ölçüm:** taslak bot (skill 0.62) ile 15 kez oynanır, %75'lik (zorda %65/%55) dilimdeki
hamle sayısı × pay (1.40 → 1.15, zorlukla azalır), ilk 10 bölüme +6. Bot %60'tan az kazanıyorsa taslak atılır, daha
hafifi denenir. 7 koşu + medyan ile komşu bölümler %8 ile %100 arasında savruluyordu.

---

## Spektrum — oyun ilerledikçe renk (`Spectrum`, `Backdrop`)

Kullanıcı: "oyun ilerledikçe, belirli şeyler yapıldıkça oyunun renkleri değişsin". Referans **Lumines**'in skin geçişleri:
oyuncu ne kadar ilerlediğini odanın renginden okur, ekranda yazı yok.

**Yalnız ışık değişir:** zemin ışığı (`Glow`), üstte `Aura` ve tepsi arkasında `Floor` havuzları, `Deep` alt katmanın
tonu, ızgara ve tahta kenarının dinlenme rengi. **Bloklar, yüzeyler ve anlam taşıyan renkler değişmez** — tek renk
satır ve `theme-check.py` kontrastları olduğu gibi kalır.

- **Klasik tur kademeleri** (`Spectrum.Thresholds`): 1500 gül · 4000 kehribar · 8000 lime · 13000 turkuaz ·
  20000 mavi · 30000 mor · 45000 **dönen prizma**. Ölçülen turlara göre dizildi (zorlanan 1–2, casual ~3, iyi ~6 kademe).
  Geçişte: oda ~1.5 sn'de yeni renge kayar, tahta alttan üste o renkle taranır (`BoardView.PlayStageWash`), `stage`
  sesi (yükselen arpej, −10.5).
- **Hedefli turlar** (macera, günlük): hedefin çeyrekleri mavi → turkuaz → lime → kehribar; soğuktan sıcağa.
- **Olaylar:** combo sürdükçe oda ısınır (`SetHeat`, altına kayar ve büyür), tek renk satırda ışık bir kez gökkuşağından
  geçer (`Rainbow`), tahta sıfırlanınca altın parlar (`Flash`), rekor geçilince tahta kenarı tur boyunca altın kalır.
  Combo çipi aynı merdiveni izler: nane → altın (4) → gül (6) → prizma (8).
- **Kalıcı:** `Progress.BestSpectrum` = klasikte ulaşılan en yüksek kademe. Menüdeki **P-R-I-Z-M-A** harfleri kademe
  başına bir tane renklenir (`Spectrum.TitleMarkup`), menünün ışığı o kademenin renginde.
- Alfalar ekran görüntüsüyle ayarlandı: ilk hâlinde (aura 0.2, zemin tonsuz) parlak varsayılan zeminde kademe
  değişimi **görünmüyordu**. Linear uzayda alfa beklenenden açık çıkar ama beyazımsı pus olarak — renk olarak değil.
  Işığın zemine (`Deep`) işlemesi gerekti. `Floor` bilerek aura'dan düşük: tepsi parçaları o zeminde durur.
- Boş havuzlar `cullTransparentMesh` ile çizilmez; ışık değişmiyorken `UpdateLight` hiçbir şeyi boyamaz.

## Dil — `Str`

Arayüz **Türkçe + İngilizce**. Oyuncunun okuduğu her kelime `Str.cs`'te (`T(tr, en)`); çağrı yerinde metin
yazma — grep ile Türkçe sabit kalmadığı doğrulandı, öyle kalsın. Varsayılan telefon dilinden
(`GameSettings.Language`: Türkçe telefon → TR, diğerleri → EN). Değiştirmek `AppController.SetLanguage` →
tema değişimi gibi arayüzü yeniden kurar. Ayarların ilk satırı, satır adı iki dilde ("DİL · LANGUAGE").
Büyük harf `Str.Upper` ile — Türkçe i/İ kuralı. Yeni dil: `Language` enum'una ekle, `T`'ye sütun.
İngilizce metinler Türkçeden uzun: sabit genişlikli etiketleri **iki dilde** görüntüyle doğrula
(AutoTest `20_en_*` görüntüleri bunun için).

## Günlük bulmaca, rozetler, hatırlatıcı, paylaşım

**LinkedIn Zip / Wordle modeli.** Kullanıcı eski takvimi ("geçmiş günü oynayabiliyorum, bu hoş değil") reddetti.
Artık günde **bir** bulmaca var, herkese aynı, yalnız bugün oynanır. Kaçan gün kaçmıştır — serinin ağırlığı bu.

- **Bulmaca** `LevelGenerator.GenerateDaily(date)`: bölüm üreticisiyle aynı yol (taslak → bot ile bütçe),
  tohum tarihten. `LevelDefinition.Number` = bulmaca numarası (`DailyNumber`, 1 Ocak 2026 = #1); kayıt bu
  numarayla eşleşir. Zorluk **haftanın gününden** (`DailyTier`): Pazartesi 8. bölüm gibi → Pazar 45. bölüm gibi.
  Satır hedefi yalnız Pzt–Per (ölçüldü: satır hedefi aynı kademede bir derece kolay oynuyor, hafta sonunu düzleştiriyordu).
  Ölçülen (`CoreHarness daily 28`, deneme başına kazanma): casual kolay %96 → zor %76 → en zor %77; iyi oyuncu %92–100.
- **Oturum**: `GameMode.Daily` + `Level` dolu → bölüm kurallarıyla oynar (hedef, hamle, +5 hamle). `GameSession.PlaySeconds`
  süreyi tutar (Core'da saat yok; `GameScreen.Update` sayar, modal/sonuç kartı/arka planda durur, kayda girer).
- **Deneme**: çözülene kadar tekrar denenir. Her yeni deneme `Progress.DailyAttemptStarted`, biten/atılan denemenin
  süresi `DailyAttemptEnded` ile bankaya. Gösterilen süre = banka + mevcut deneme.
- **Çözüm** `Progress.RecordDailySolve`: seri (dün çözüldüyse +1, değilse 1), en iyi seri, süre/hamle/yıldız/deneme,
  rozet istatistikleri, kusursuz hafta. Çözülen gün tekrar oynanmaz; `PlayDaily` kartı açar. Sonuç kartı: büyük sayı
  **süre**, ana eylem **PAYLAŞ**, not satırı yeni rozet > yeni tema > seri.
- **Kart** (`DailyScreen`): Bugün sekmesi (seri + hafta şeridi + günün zorluğu/hedefi + sonuç ya da deneme durumu +
  bir sonraki bulmacaya geri sayım, kendi hapında) ve Rozetler sekmesi (4×4 madalyon + seçilen rozetin kartı).
  **Hatırlatıcı anahtarı yok** — kullanıcı istemedi.
  Seri kart son gösterdiğinden büyükse bir kez sayarak yükselir (`Progress.DailyStreakShown`).
- **Rozetler** (`DailyBadges`): 16 tane, `Achievements` gibi **durumsuz** — sayılardan hesaplanır. Her biri 1 yıldız →
  `Progress.TotalStars`. Görülmemiş rozet: menü kartında ve sekmede sabit nane nokta.
- **Hatırlatıcı** (`Reminder` + `Assets/Plugins/Android/PrizmaReminder*.java` + `Editor/ReminderManifest`): son çözümün
  **saatinde**, ertesi gün tek bildirim ("12:00'de çözdüyse yarın 12:00"). Eklenti yok: AlarmManager →
  `PrizmaReminderReceiver` bildirimi atar ve ertesi günü kurar; 3 cevapsız bildirimden sonra susar; `PrizmaReminderBoot`
  yeniden başlatma/güncellemede geri kurar. Tam zamanlı alarm izni bilerek yok (birkaç dakika sapabilir).
  İzin **ilk açılışta, splash sırasında** doğrudan sistemin kendi penceresiyle sorulur
  (`AppController.AskNotificationsOnFirstLaunch`, `GameSettings.ReminderAsked`) — oyunun kendi modalı yok (kullanıcı
  isteği). Sonradan Ayarlar → BİLDİRİMLER anahtarı. `Reminder.Refresh` açılışta, uygulama öne gelince, çözümde ve
  anahtarda çağrılır; ilk çözümden önce kurulacak bir şey yoktur. Bildirim ikonu vektör XML —
  build sırasında `res/drawable`'a yazılıyor, kod gibi. **Cihazda doğrulanmadı** (Editor açıkken APK alınmadı);
  Java, Unity'nin JDK'sıyla `android-36` jar'ına karşı derlendi.
- **Paylaş** (`ShareSheet`): günlük sonuç kartında; Android'in kendi paylaş penceresi (intent), eklenti yok.
  Editör/masaüstünde panoya kopyalar. Metin `Str.ShareText` + mağaza linki (`Application.identifier`).
- **Başarımlar** (`Achievements`): mevcut istatistikler üzerinde 11 × 3 kademe; **hiçbir şey saklanmaz**, kademe
  sayıdan hesaplanır. Kademe 1/2/3 yıldız verir → `Progress.TotalStars` = bölüm + başarım yıldızı → temalar.
  İstatistik ekranında ikinci sekme; görülmemiş kademe varsa menüdeki kısayolda sabit nane nokta (nabız yok).
- **Reklam planı:** yayınla birlikte reklam gelecek, yani internet izni gerekecek. `docs/YAYIN-HAZIRLIK.md`'deki
  "INTERNET izni yok" kontrolü o zaman kaldırılmalı; Veri güvenliği formu ve gizlilik politikası buna göre.

## Kayıt ve ilerleme

- `RunStore`: mod başına bir yuva. Her hamlede ve uygulama arka plana geçince `SessionSnapshot` (JSON).
  RNG durumu da içinde, yani devam edilen tur **birebir aynı** dağıtımla sürer (testi var).
  `GameSession.Restore` bozuk kaydı reddeder → kayıt atılır, oyun çökmez.
- Günlük kayıt yalnız başladığı gün devam eder. Bölüm kaydı yalnız aynı bölüme.
- `Progress`: yıldızlar, günlük seri (`DailyStreak` dün ya da bugün oynandıysa yaşar), yaşam boyu
  istatistikler, tema, renk körü modu, öğretici görüldü.
- `Themes`: yıldızla açılır ve **ekranın bütün havasını** değiştirir — zemin, ışık, ızgara, tüm yüzeyler,
  tahta, ana buton vurgusu, scrim ve bloklar. Oyunda anlam taşıyan renkler **temalanmaz**: altın, nane,
  prizma, kristal, buz, ön-temizleme tonları.

### Temalar — neden böyle, nasıl değiştirilir
İlk hâlinde tema yalnız blok paletiydi ve kullanıcı "temayı değiştirince bir şey değişmiyor" dedi.
Ölçünce haklıydı: beş palet aynı renk sırasını paylaşıp yalnız açıklıkla oynuyordu; Prizma–Şeker renk başına
ΔE 15, bir rengi ΔE 6. Üstelik tema bloksuz bir menüde seçilip bir tur sonra hafızadan yargılanıyordu.
Şimdi zeminler temalar arası ΔE 30–85.

- **Uygulama yeniden kurmakla olur** (`AppController.SetTheme` → `RebuildInterface`). Renkler arayüzün her
  yerinde kurulum anında okunuyor (dolgular, vurgu için saklanan dinlenme renkleri, scrim, backdrop);
  yerinde boyamak tek bir grafiği kaçırsa eski temadan bir yama kalır. Yeniden kurma oyuncuyu olduğu yerde
  bırakır: aynı sayfa, açık modallar, süren tur. Eski ağaç `Destroy`'dan **önce kapatılır** — yoksa kare
  sonuna kadar eski widget'lar girdiye kayıtlı kalır.
- `Design` renkleri artık `Themes.Current`'tan okunan özellikler. **Temaya bağlı bir rengi `static readonly`
  alanda saklama** (`BoardView.EmptyCellColor` bu yüzden özellik oldu) — ilk temada donar.
- **Uygulama ikonu ve splash `Themes.Default`** kullanır, `Current` değil: ikon Editor'de üretiliyor ve orada
  `Current`, geliştiricinin test ederken en son seçtiği tema olur.
- Temalar ekranında canlı önizleme (`ThemePreview`): açık temaya dokunmak hemen uygular, kilitliye dokunmak
  onu yıldız bedeliyle önizler.
- Palet değiştirince **`python Tools/theme-check.py`** koş. Değerleri doğrudan `Themes.cs`'ten okur:
  yüzeyde beyaz ≥7:1, vurguda ≥3:1, çiplerde altın/nane ≥4.5:1, zeminde `TextOnGround` ≥3:1, tahtada her
  blok ≥3:1, paletteki iki blok ≥ΔE 22 (tek renk satır), hiçbir blok kristal/buza yakın değil. Son kural
  canlı yakaladı: Pastel'in açık mavisi buza ΔE 20'ydi. Mücevher'de bu yüzden inci beyazı blok yok.
  Bir kural daha: **tepsideki parçalar tahtada değil zeminde durur** — her blok tepsinin altındaki zemine
  ≥ΔE 24. Şeker'in pembe parçası pembe zeminde ΔE 10'du ve görünmüyordu; ölçüt Prizma'nın mavi zemindeki
  mavi parçası (ΔE 26).

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

- **Çöp, kare süresi kadar önemli.** Kare süresi rahat olsa bile hamle başına ayrılan bellek GC
  duraklamasına, o da tam sürükleme sırasında takılmaya dönüşür. Ölçülen ve düzeltilenler:
  - `BoardModel.ResolveLines` her çözümlenen yerleştirmede bir `ClearResult` (7 liste) **ve bir
    `HashSet<CellOffset>`** ayırıyordu. Artık yeniden kullanılan bir `bool[,]` maske var, ve
    `Place` isteğe bağlı bir `ClearResult` alıyor. Oyunun kendi hamlesi hâlâ taze bir sonuç alıyor
    (animasyonlar onu kareden sonra da tutuyor); dağıtıcı aynı örneği geri veriyor.
  - Dağıtıcı aday başına `PieceShape[]`, dağıtım başına `List<Candidate>` ayırıyordu. İkisi de
    havuzlandı.
  - Sonuç: **dağıtım başına 148 KB → 0 KB.** Çekirdek tarafı (model + dağıtıcı) artık hamle başına
    **sıfır** tahsisat yapıyor; görünümle birlikte toplam 4.3 KB/hamle.
- **`AudioKit` sentezi ana iş parçacığını bloklamamalı.** Tüm seti `Awake` içinde üretmek masaüstünde
  **197 ms** sürüyordu — telefonda birkaç katı, ve her milisaniyesi açılışta donmuş bir kare.
  Şimdi yalnız menünün hemen üretebileceği dört ses `Awake`'te kuruluyor (**2 ms**), kalan 26 klip
  `ThreadPool`'da hesaplanıp karede altışar tane klibe dönüştürülüyor. `Play` hazır olmayan klibi
  sessizce atlıyor. Örnekler birebir aynı kaldı (30 klibin hepsi bayt bayt doğrulandı).

Ölçülen (editör, oyun içi): ana iş parçacığı **0.97 ms/kare**, GPU 0.27 ms, 36 draw call.
Bütçe 16.7 ms — bolca yer var.

Hâlâ 60 tutmazsa sıradaki aday: backdrop'un 5 tam ekran katmanını (Deep/Glow/Grid/Vignette/Grain)
tek bir RenderTexture'a bir kez çizmek. Görünüm birebir kalmalı — Linear renk uzayında sRGB RT şart.

---

## Tasarım dili

`Design.cs` **tek kaynak**. Çağrı yerinde ham sayı yazma; token ekle.

- Tipografi ölçeği: Readout 200 / Display 184 / Title 92 / Headline 72 / Body 58 / Label 50 / Caption 42
- Aralık: 8 / 16 / 24 / 40 / 56 / 80 / 112 · kenar boşluğu 48 · tahta kenarı 24 · min dokunma hedefi 144
- Kontroller: `ButtonLg` 204 / `ButtonMd` 176 · köşe ikon butonu `IconButton` 168 · ikon `IconSm` 72 / `IconMd` 108 ·
  ikon-buton dolumu `GlyphFill` %54 · modal genişliği `ContentWidth` (984)
- Şekil: **squircle** (süperelips) — düz yuvarlak dikdörtgen değil. `Raster.FillSquircle`
- Derinlik: **bulanık ambient gölge**, sert alt dudak değil. Offset küçük, yayılım büyük —
  büyük offset gölgenin dolu çekirdeğini elemanın altından taşırır ve "dudak" gibi okunur
- Renk: neredeyse siyah taban + mor/turkuaz/erik mesh gradyan havuzları + tek canlı vurgu

### Ölçüler dp'den seçilir, gözle değil
Scaler dikey telefonda genişliği eşler: **1080 birim = ekran genişliği**, yoğunluk ne olursa olsun.
Android trafiğinin ~%31'i ≤360dp genişlikte → hedef hesap **1 birim = 1/3 dp** (3 birim = 1sp).
İlk ölçek masaüstünde gözle kurulmuştu ve telefonda platform alt sınırlarının altındaydı:
caption 9sp, gövde 13sp, duraklat butonu 35dp, güç butonları 35dp yüksek, tahta genişliğin %90'ı.
Kullanıcının şikâyeti "arayüz çok küçük, ekranı verimli kullanmıyor" buydu.

İkinci ölçek Material'ın alt sınırlarını tutturdu — ve kullanıcının bir sonraki şikâyeti tam oydu:
**"indirdiğim oyunlara göre arayüz elemanları küçük kalıyor"**. Material ölçüleri uygulama içindir; casual oyun
hamle aralarında bir bakışta okunur ve tür kontrolleri bir kademe büyük kurar. Fark başlıklarda değil **alt
kademelerdeydi**: etiket 15sp, ikon 20dp, güç butonunda ikon butonun üçte biri, maliyet noktaları 5dp, slider
9dp'lik bir çizgi, ayar etiketleri %70 beyaz orta kalınlıkta (telefonun ayarlar uygulaması gibi). Üçüncü ölçekte
alt kademeler en çok büyüdü (caption +%10, label +%14, ikonlar +%20-30), zaten büyük olan skor ve başlık en az.
Oyuncunun okuduğu her kelime artık ExtraBold; geniş harf aralığı (6) 3'e indi. Tepsi `MaxScale` 0.66 → 0.74.
**Material alt sınırına geri çekme** — sınır tabandır, hedef değil.

Kurallar:
- Oyuncunun okuduğu hiçbir şey `Caption` (42 = 14sp) altına inmez. Dokunulan hiçbir şey 144 (48dp) altına inmez;
  sayfa köşelerindeki ikon butonları (duraklat, ayarlar, geri) `IconButton` 168 (56dp).
- Ekranlar **sabit ofsetle ortaya dizilmez**, `App.PageHeight`'tan yerleşir. Eskiden her şey 1920'lik
  bir bantta ortalanıyordu; 19.5:9'da sayfa ~2120, yani menünün ve oyunun altında çeyrek ekran boş kalıyordu.
  - Oyun: `GameScreen.PlayLayout.Solve` — her bandın istediği ve razı olduğu boy var. Fazlalık tepsiye
    ve boşluklara, eksik önce boşluklardan → tepsiden → HUD'dan, **tahta en son** küçülür.
  - Menü alttan yukarı (başparmak), başlık kalan üst alanda ortalı. Macera haritası sığdığı kadar satır
    (19.5:9'da 6 → sayfa başı 24). Modallar içerikten boylanır, sığmayan gövde ölçeklenir ya da (başarımlar) kaydırılır.
- 16:9'da `PageHeight` eskiden 1920'ye clamp ediliyordu; gerçek sayfa ~1700 olduğu için alt yığın jest
  çubuğuna biniyordu. Clamp kaldırıldı — geri koyma.
- Yerleşim `Awake`'te bir kez kurulur; çalışırken ekran boyutu değişirse (katlanabilir) yalnız safe area yenilenir.
- Tablet (3:4) bilinçli olarak telefon sütunu: içerik 1080 genişlikte ortada, tahta daha küçük.

### Arayüz kuralı — bunu ihlal etme
> **Yazıyla anlatma. Yanıp söndürme.**

Bir kez "2 SATIR BİRDEN" rozeti ve tamamlanmaya yakın hücreleri nabız attıran ipucu eklendi;
ikisi de kaldırıldı. Ön-temizleme önizlemesi (`BoardView.PreviewLines`) **sabit ve sessiz**:
etiketsiz, nabızsız, düşük alfa. Renk kademesi var (1 satır nane / 2 altın / 3+ gül) ama
göze sokmuyor. Yoğunluk ayarı: `BoardView.AddPreviewBar` içindeki iki alfa değeri (0.22 / 0.13).

---

## Ses — `AudioKit`

Dosya yok, her şey açılışta sentezleniyor (`Tone` + `Chime`, sinüs + ikinci harmonik, yumuşak
zarf, tek kutuplu alçak geçiren). Set sade ve bu **kasıtlı** — aşağıdaki nota bak.

Zincir üç dosya: `SoundSynth` (tarifler, Unity'siz) → `SoundMaster` (seviye, Unity'siz) → `AudioKit` /
`MusicPlayer` (çalma). Unity'siz ikisi `Tools/AudioCheck` ile ölçülüyor; araç oyunun çaldığının aynısını ölçer.

### Seviye — `SoundMaster`
Kullanıcı: **"oyun sesini fullesem bile az"**. Ölçünce haklıydı ve sebep ayar değil üretimdi: hiçbir klip tam
ölçeğe yaklaşmıyordu. En yüksek klip −5 dBFS tepe; en sık duyulanlar (kaldırma, bırakma, menü tıkı) −13…−19.
Zincirde onları yükselten hiçbir adım yoktu, yani "efekt %100" cihazın çalabildiğinin 5–24 dB altında bir tavandı.
Müzik ayrıca 0.45 tavan × 0.55 varsayılan ile −27 dB'deydi.

Şimdi her klip **rolüne göre bir yükseklik hedefine** getiriliyor (`SoundMaster.TargetFor`; ölçü: en yüksek
100 ms, K-ağırlıklı RMS) ve −1 dBFS altında şeffaf bir look-ahead limiter'la tutuluyor. Tarif — perde, zarf,
filtre, yani **tını — değişmedi**. Limiter en çok 1.1 dB çalışıyor; `MaxReductionDb` 6'yı aşacak bir hedefte
klip ezilmek yerine sessiz bırakılır.

| (`AudioCheck measure`, efekt %100) | Önce | Sonra |
|---|---|---|
| bırakma (turun en sık sesi) | −23.7 | **−12.1** |
| kaldırma / menü tıkı | −26.6 / −29.7 | **−15 / −15** |
| 1 satır temizleme | −16.2 | **−9.0** |
| buz | −35 | **−13.9** |
| müzik (varsayılan) | −26.9 | **−18.1** |

Hiyerarşi hedeflerde: temizleme en üstte (−9), combo cevabı ondan 1.5 dB altta, bırakma −12, tık/kaldırma −15,
müzik bir temizlemenin ~9 dB altında. Varsayılanlar efekt %100 / müzik %70 (`SoundMaster.Default*`).

- **Çıkış limiteri** (`AudioBusLimiter`, AudioListener'da `OnAudioFilterRead`): her klip tek başına −1 dBFS'te
  ama iyi bir hamle bırakma + temizleme + combo'yu 70 ms içinde üst üste çalıyor; toplam cihazda sert kırpılırdı.
  Tek bir ses ona hiç ulaşmıyor. Kaldırma — kaldırırsan hedefleri de düşürmen gerekir.
- Müzik döngüsü artık `ThreadPool`'da üretilip master'lanıyor; eskiden `Awake`'te senkrondu. Ana iş parçacığında
  kalan: ilk dört klip, **1.3 ms**.
- **Açık karar — telefon hoparlörü:** telefon hoparlörü ~700 Hz altını neredeyse çalmaz. Bırakma (130–220 Hz),
  hatalı bırakma ve bomba seviyesi ne olursa olsun hoparlörde zayıf kalıyor (bırakma telefon modelinde −44 dB).
  `SoundMaster.AddPresence` alçak bölgenin harmoniklerini 500 Hz üstüne ekler (bırakma −44 → −29). **Tınıyı
  değiştirdiği için oyunda değil**; `AudioCheck wav` onu üçüncü varyant olarak çıkarıyor. Kullanıcı dinleyip
  karar verecek.

### Eklenen katmanlar — eskiyi değiştirmeden
Kullanıcı: "combo yaptıkça değiştir değil, mevcuttakine eklemeler yap". Eski kliplerin tarifi **aynı**; yenileri
aynı ilkellerle (Chime/Tone), aynı pentatonikte, üstüne biner:
- `sparkle0..3` (−16.5): 3. combo'dan itibaren temizlemeye eklenen hızlı tiz koşu, her iki combo'da bir nota uzar.
  −14.5'te telefon hoparlöründe temizlemeye 2 dB'ye yaklaşıyordu; geri çekildi.
- `surge` (−12): her 5. combo'da tam akor. `deal` (−19): tepsi yenilenince üç hafif tık.
- `record` (rekor geçildi), `badge` (rozet), `streak` (seri sayacı).
Dinlemek için: `AudioCheck wav <klasör>` → `4_KATMANLI_combo_dizisi.wav` / `4b_KATMANSIZ_...` (A/B).

### Temizleme tek çağrı
`PlayClear(lines, comboStreak, monoLines, perfectClear)`. Öncesinde iyi bir hamle
`PlayClear + PlayCombo + PlayPrism + PlayFanfare`'ı **aynı karede** tetikliyor, tepeler
toplanıyor ve sertleşiyordu. Artık parçalar sırayla geliyor: temizleme, +70 ms combo cevabı,
+130 ms tek renk satır, +220 ms tahta boşaldı.

İki eksen, aynı tını:
- **Tek hamledeki satır sayısı** → çanın merdivende ne kadar yukarı çıktığı (`_clear`, 1-4 nota sayısı artar)
- **Combo serisi** → bir tık daha yukarıdan gelen cevap (`ComboLadder`, 9 basamak)

1 satırlık temizleme **birebir eskisi gibi** — turun %95'i o ve dokunulmadı. Çok satır aynı çanı
aynı skalada daha yukarı taşır: yeni bir tını değil, aynı ses daha fazlasını söylüyor. Farklı bir
tını "daha iyi bir hamle" değil "farklı bir olay" diye okunur.

Eski `_combo` 5 basamakta durup kendini tekrar ediyordu; oyuncu tam iyi giderken merdiven
düzleşiyordu. `ComboLadder` 9 basamak.

### Ses estetiği notu — buradan ders çıkar
Bir kez ses seti baştan yazıldı: katmanlı sentez, inharmonik çanlar, pişirilmiş Schroeder reverb,
stereo genişlik, sub ağırlığı, parıltı tanecikleri, ducking, vuruş eşitlemesi. Ölçümler kusursuzdu
(kademeler arası 0.08 dB, merdiven 7 sent içinde doğru). **Ve kullanıcı hepsini reddetti.**

Söylediği şey teşhisin kendisiydi:

> "sanki mobil bir oyun oynamıyorum da başka bir şeyin sesi gibi"

Uzun reverb kuyruğu + inharmonik çan + sub ağırlığı = Monument Valley / Alto estetiği. Block Blast
türü **kuru, kısa, parlak** ister. Ölçüm *yapıyı* doğrular, *tınıyı* doğrulamaz — ve bu projede
tını yargısı kullanıcınındır.

Buradan üç kural:
1. **Ses tınısına dokunmadan önce sor.** Yapı (ne zaman, ne kadar, hangi sırayla) serbest; tını değil.
2. **Eskiyi koru.** 1 satırlık temizleme, bırakma ve seçme sesleri turun neredeyse tamamı —
   onlar beğenildi, elleme.
3. **Kulağın yoksa dinlet.** Klipleri WAV'a çıkarıp oyundaki gerçek zamanlamayla kurgula
   (bırakma + temizleme + combo üst üste), kullanıcı tek tıkla dinlesin. Tek vuruş güzel gelip
   seri hâlinde yorucu olabilir; hep **diziyi** dinlet.

### Ayarlar tuzağı
Ses gelmiyorsa önce `GameSettings`'e bak: `Muted` ayrı bir bayrak ve ses seviyesinden bağımsız.
Bir kez mute açık **ve** efekt %5 / müzik %1'e çekilmişken "ses bozuk" sanıldı.
`AudioManager.asset` → `m_DSPBufferSize: 512` ("Good latency"); 1024'te dokunma-ses arası gecikme
fark ediliyordu, 256 zayıf cihazda underrun riski.

## Doğrulama yöntemi

Ekran görüntüsüne güvenme — **durumu koda sor**. `mcp__unity-editor-mcp__eval` ile private
alanlara reflection'la erişip modeli ve görünümü karşılaştır. Bir noktada model 81 derken ekran
0 gösteriyordu; sorun kodda değil, editörün kare işlememesindeydi.

Denge değişikliklerinden sonra simülasyonla ölç (casual/iyi oyuncu, rastgele vs akıllı).
**Hamle sınırı koymayı unutma** — sınırsız döngü Unity'yi kilitler.

### Unity'siz doğrulama (`Tools/`)
- `Tools/CoreHarness` — Core'u Unity olmadan derleyip kural testlerini, dengeyi, bölüm eğrisini ve
  dağıtıcı maliyetini koşar. Unity'nin kendi SDK'sı yeter:
  `<Unity>/Editor/Data/DotNetSdk/dotnet.exe build Tools/CoreHarness -c Release`, sonra
  `...Harness.dll tests` / `balance 40` / `levels 1 60` / **`perf`**.
  Denge ya da kural değişince **önce bunu koş**.
  `perf` dağıtım başına **süreyi ve ayrılan belleği** doluluk kademelerine göre yazar — dağıtıcı
  ana iş parçacığında, oyunun ortasında çalışıyor, yani ikisi de kare süresidir. Aday sayısını ya da
  sıralama terimlerini büyüttükten sonra bunu koş.
- `Tools/AudioCheck` — `SoundSynth` + `SoundMaster`'ı Unity'siz derler. `measure`: her klibin tepe seviyesi,
  yüksekliği, telefon hoparlörü modelinden geçmiş yüksekliği ve limiter'ın çalışması, önce/sonra; üretim süresi.
  `wav <klasör>`: klipler + oyunun gerçek zamanlamasıyla kurgulanmış bir oyun dizisi (önce / sonra / telefon eki),
  bir de hoparlör simülasyonu. **Ses seviyesine ya da tarife dokununca bunu koş ve diziyi dinlet.**
- `Tools/compile-check.ps1` — oyun (normal + `PRIZMA_AUTOTEST`) ve editor kodunu Unity'nin derleyici
  argümanlarıyla derler. Editor kapalıyken/odakta değilken derleme hatası yakalar.
- `Tools/autotest.ps1` — projeyi geçici klasöre kopyalar, batchmode Unity ile `PRIZMA_AUTOTEST` tanımlı
  Windows build alır, dikey pencerede çalıştırır. `AutoTest` her ekranın görüntüsünü (ikon dahil)
  `%TEMP%\prizma_autotest\shots` altına yazar. Tahtalar gerçek bot hamlelerinden ve elle kurulmuş
  kayıtlardan geldiği için görüntü ile durum aynı anda doğrulanır. Tanım gerçek build'e girmez.
  **Arayüz değişikliğini tek oranda doğrulama.** `-SkipBuild -Width 540 -Height 960 -Tag 16x9` ile aynı
  build'i başka oranlarda koş (19.5:9 varsayılan 432x936, 20:9 432x960, 16:9 540x960, tablet 720x960).
  `-Only adventure` yalnız macera görüntülerini alır (40_…61_, ~2 dk); `-Only play` hazır bir kayıtla oyunu **açık
  bırakır** (elle denemek için). `-Monitor 2` pencereyi ikinci ekranda açar (kullanıcının dikey ikinci ekranı var).
  `-TopInset` (varsayılan 90) telefon çentiğini simüle eder — masaüstü oyuncusunun safe area'sı yoktur
  ve onsuz her görüntü telefondakinden fazla üst alan gösterir. Bir oran ~5 dk sürer.

`eval` çağrıları 60 saniyede zaman aşımına uğrar; ağır simülasyonu küçük parçalara böl.

---

## Durum

**Animasyon eklemeleri (bu tur):** temizlemede bloklardan savrulan kırıklar (`BoardView.PlayShards`, kendi
canvas'ı, havuzlu, tek coroutine, en çok 72), çok satırda küçük sarsıntı (`Shake`), her 5. combo'da damla noktasından
şok halkası (`PlayWave`, 10'dan sonra prizma rengi), tahta sıfırlanınca merkezden halka, tepsi parçaları aşağıdan kayarak
gelir (`TrayView.DealIn` — sürüklenmeye başlanan parçayı bırakır), kazanınca/yeni rekorda konfeti (`ResultCard.Celebrate`).

**Bitti:** çekirdek oynanış · akıllı dağıtıcı · ana menü · duraklatma menüsü (yeniden başlat dahil) ·
ayarlar (müzik/efekt + sessize alma + titreşim + renk körü modu) · yerel ilk 10 skor ·
prosedürel görsel dil · sentezlenmiş ses ve müzik (+ çok satır / combo ödül sesi) ·
ön-temizleme önizlemesi · combo göstergesi ·
+N puan balonu · **Prizma güçleri** (döndür/yenile/bomba, sıkışma durumu) · tek renk satır ve
tahtayı sıfırlama bonusları · **Macera modu** (sonsuz, kristal/buz, yıldız, harita) ·
**Günlük bulmaca** + seri · **kaldığın yerden devam** (her mod) · istatistikler · temalar ·
yazısız el öğreticisi · kısa native haptik · uygulama ikonu + açılış rengi · 60 FPS ayarları.

**Sırada:** Android'de **gerçek cihazda denemek**. Masaüstünde ölçülemeyecek şeyler:
önizlemenin parmak altında okunurluğu, yardımın gerçekten fark edilmezliği, seslerin telefon
hoparlöründe tınısı, dokunma-ses gecikmesi (DSP tamponu 512), sürükleme mesafesi
(`GameScreen._liftPixels`), güç çubuğunun başparmak erişimi, bomba nişanının parmak altında
görünürlüğü, haptik şiddeti (`AppController.Tick/VibrateClear`).

Sonrası: çevrimiçi lig (günlük tohum hazır, motor deterministik), başarımlar, sürüm numarası ve
mağaza hazırlığı (imzalama anahtarı, gizlilik metni).

**Çalışma adı** PRIZMA — `MainMenuScreen.GameName`, tek satır.
