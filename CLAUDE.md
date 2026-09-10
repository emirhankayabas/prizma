# PRIZMA — blok bulmaca

Unity 6 (6000.6.0f1) · URP · portre mobil, hedef Android.
Block Blast türünde 8x8 blok bulmaca. Arayüz metinleri **Türkçe**.

Sahne: `Assets/Scenes/SampleScene.unity` — içinde tek bir GameObject var: **`GameRoot`** + `AppController`.
Başka hiçbir sahne kurulumu yok; tüm hiyerarşi çalışma anında koddan inşa ediliyor.

---

## Değişmez kural: projede sanat/ses dosyası yok

Bloklar, paneller, çerçeveler, gölgeler ve 11 ikon `Raster` ile **kodla çiziliyor**.
Tüm ses efektleri ve fon müziği `AudioKit` / `MusicPlayer` içinde **sentezleniyor**.

Tek istisna: `Assets/Fonts/` — Poppins (Regular / SemiBold / ExtraBold), **OFL lisanslı**, dağıtılabilir.
Lisans metni `Assets/Fonts/OFL.txt`. TMP font asset'leri `Assets/Resources/Fonts/` altında
(`Design.Load` oradan `Resources.Load` ile çekiyor).

Yeni görsel gerekirse önce "bunu `Raster` ile çizebilir miyim?" diye sor. Cevap genelde evet.

---

## Mimari

### `Assets/Scripts/Core/` — saf oyun mantığı
Unity'ye hiç bağımlı değil (`UnityEngine` import etmiyor). Bu yüzden test edilebilir ve
`eval` ile hızlıca simüle edilebilir. **Bu ayrımı bozma.**

| Dosya | Görev |
|---|---|
| `BoardModel` | 8x8 tahta. Satır/sütun doluluk sayaçlarını hücre değiştikçe günceller |
| `PieceShape` | Parça şekli + doluluk maskesi (`Contains`) |
| `PieceLibrary` | 37 şekil, ağırlıklı dağılım |
| `ScoreRules` | Puanlama ve combo çarpanı — tüm sayılar burada |
| `GameSession` | Bir turu yönetir: tahta, tepsi, skor, seri, oyun sonu |
| `PieceDealer` | **Akıllı parça dağıtıcı** — aşağıda ayrı başlık |

### `Assets/Scripts/Game/` — sunum
`AppController` kök. Canvas + backdrop + ses + ekranları kurar, gezinmeyi yönetir.

Ekranlar `AppScreen`'den türer: `MainMenuScreen`, `GameScreen`, `SettingsScreen`,
`ScoresScreen`, `PauseScreen` (son üçü `IsModal`).

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

`eval` çağrıları 60 saniyede zaman aşımına uğrar; ağır simülasyonu küçük parçalara böl.

---

## Durum

**Bitti:** çekirdek oynanış · akıllı dağıtıcı · ana menü · duraklatma menüsü · ayarlar
(müzik/efekt seviyeleri + tek dokunuşla sessize alma + titreşim) · yerel ilk 10 skor ·
prosedürel görsel dil · sentezlenmiş ses ve müzik · ön-temizleme önizlemesi · combo göstergesi ·
+N puan balonu.

**Sırada:** Android'e geçip **gerçek cihazda denemek**. Masaüstünde ölçülemeyecek şeyler:
önizlemenin parmak altında okunurluğu, yardımın gerçekten fark edilmezliği, seslerin telefon
hoparlöründe tınısı, sürükleme mesafesi (`GameScreen._liftPixels`).

Sonrası: hedefli bölüm modu (özel tahta hücreleri) veya günlük tohum + lig
(`GameSession.NewDailyRun` hazır, motor deterministik).

**Çalışma adı** PRIZMA — `MainMenuScreen.GameName`, tek satır.
