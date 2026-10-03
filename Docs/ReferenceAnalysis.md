# Referans oyun analizi — rakip blok bulmaca

İki telefon ekran kaydının (`video1.mp4`, `video2.mp4`) kare kare ve ses ölçümüyle incelenmesi.
**Bu oyun PRIZMA değil**; Block Blast türünde ticari bir blok bulmaca. Buradaki her sayı ölçümdür,
izlenim değil — yöntem en altta.

---

## Kaynak

|  | video2 | video1 |
|---|---|---|
| kayıt saati | 10:19 | 10:24 |
| süre | 2:31 | 2:38 |
| başlangıç rekoru | 10391 | 13812 |
| bitiş skoru | **13812** (yeni rekor) | 13727 (rekoru kıl payı kaçırdı) |

**video2 önce** (rekoru kıran tur), **video1 sonra** (onu geçmeye çalışan tur).
İkisi de 592x1280 portre, mono 48 kHz, cihaz iç sesi — mikrofon değil (gürültü tabanı −64…−70 dBFS).

Arayüz İngilizce, reklamlar Türkçe. Açılış ikonu: mavi zeminde altın çerçeveli konfeti topu / havai fişek.

---

## 1. Oyun nasıl işliyor

Çekirdek döngü aynı: 8x8 tahta, 3'lü tepsi, sürükle-bırak, satır/sütun dolunca temizlenir, üçü de
bitince tepsi yenilenir. **Döndürme yok, güç yok, geri alma yok.** Tek mod; ana menü yok, açılıştan
doğrudan tahtaya düşüyor. Üstte solda altın taç + rekor, sağda dişli (kayıtlarda hiç açılmadı),
ortada büyük skor.

### Sürükleme — en dikkat çeken kısım

- Parça **ızgaraya kilitlenmiyor**, parmağı serbestçe takip ediyor; tahtanın dışına bile taşabiliyor.
- Parça **parmağın ~1 hücre üstünde** çiziliyor → başparmak parçayı kapatmıyor.
- **Ayrı bir hayalet/silüet yok.** Nereye oturacağını gösteren tek şey, temizlenecek satır/sütunun ışıması.
- Tepside parçalar küçük (~%60), elde tam hücre boyunda büyüyor.

### Ön-temizleme ışıması

Tek satırda beyaz-mavi, ikilide yeşil / altın / turuncu, üç ve üstünde tam gökkuşağı.
Işık **tahtanın kenarından dışarı taşıyor**. İki klipten kesin kural çıkmadı — aynı 2 sütun farklı
anlarda yeşil ve turuncu yandı; satır sayısı ve combo serisinin birlikte belirlediği bir kademe gibi.

### Geri bildirim katmanı

- **Combo:** ardışık temizleyen hamleleri sayıyor, **10'a kadar** görüldü (`Combo 10`, `Combo 9`, `Combo 3`).
- **Puan balonları:** yerleştirme noktasında altın `+101` / `+485` / `+970`, ayrıca skorun üstünde ikinci bir `+N`.
- **Övgü metinleri:** tahtanın ortasında `Good!` → `Great!` → `Perfect!` (altın halka patlaması + konfeti)
  → `Masterful!`. Üst üste binebiliyor: tek karede hem `+970`, hem `Good!`, hem `Masterful!` vardı.

### Rekor anı üç aşamalı

1. Skoru geçtiğin an **taçtaki sayı canlı olarak skorla birlikte artmaya başlıyor**.
2. Geçiş anında tahtayı kaplayan `NEW HIGH SCORE` + mor `100%` patlaması.
3. Tur ortasında ayrıca `Better than 60%!` altın kupa kartı — yüzdelik dilim kutlaması.

### Oyun sonu

Hiçbir uyarı yok; sığmayan parça tepside kalıyor, ekran kararıyor, **5 saniyelik halka geri sayım** +
yeşil `▶ Revive` (ödüllü reklam). Alınmazsa tam ekran mor bitiş kartı: taç, `Fun doesn't Stop Here!`,
skor, altın devam düğmesi. Kart iki yandan konfeti yağarak giriyor.

### Para kazanma

Tur ortasında altta banner beliriyor (yoksa yer de kaplamıyor), farklı kreatiflerle dönüyor;
oyun sonunda ödüllü revive; bitiş kartı kendi interstitial'ı.

### Çözülemeyen

Tahtada bazen bir grup bloğun üstünde **👍 rozeti + ışık patlaması** çıkıyor. Satır temizlemede
gitmiyorlar, bir süre sonra başka bir gruba (nane yeşili bloklara) geçiyorlar. Süreli bir
etkinlik/koleksiyon katmanı gibi — iki klipten ödülü anlaşılmadı.

---

## 2. Animasyonlar (ölçülen)

Tahta bölgesinin kare-kare değişim enerjisiyle:

| Aşama | Süre |
|---|---|
| bırakma → temizleme çekirdeği | **~300–350 ms** |
| skorun sayarak yükselmesi | temizlemeden sonra **~1.5 s** daha |
| yeni tepsi dağıtımı | temizleme bittikten ~250 ms sonra, iki kısa sıçrama |
| artçı parçacıklar | ~1 s daha düşük yoğunlukta |

Temizleme animasyonunun anatomisi (20 fps karelerden):

1. Temizlenecek hücreler **parlak ışık şeridine** dönüşüyor (sütunda dikey, satırda yatay huzme).
2. Huzme yarı saydam bir bara dönüşüp içindeki bloklar **küçülerek** kayboluyor.
3. Aynı anda bloklar **renkli küp kırıkları** olarak savruluyor + konfeti + kıvılcım.
4. Çok satırlı temizlemede hücreler yok olmadan önce **gökkuşağı gradyanına** boyanıyor (yeşil→sarı→turuncu).
5. Skorun arkasında **elmas parlaması** — rengi değişiyor (beyaz / mavi / camgöbeği / altın).

---

## 3. Ses

**Fon müziği yok.** En sessiz 3 saniyelik pencere video1'de −68.1 dBFS RMS, yani dijital sessizlik.
Turun %36–39'u sessiz. Ya oyunda müzik yok ya da kapalıydı — ama efektler sessizliğin üstünde
tek başına taşıyor.

İki videoda 111 + 137 olay, üç net sınıf:

| Ses | Adet | Süre | Baskın frekans | Tepe |
|---|---|---|---|---|
| **Kaldırma** | 78 | ~90 ms | 1553 Hz (spektral merkez ~2.1 kHz) | −16 … −17 dBFS |
| **Bırakma** | 76 | ~80 ms | 545 Hz (merkez ~585 Hz) | −16 … −17 dBFS |
| **Temizleme** | 72 | 0.5–4.0 s | ezgi (aşağıda) | **−9 … −12 dBFS** |

Kaldırma → bırakma arası medyan **390–465 ms**: bu iki ses turun ritmini kuran metronom.

**Seviye hiyerarşisi:** temizleme, bırakmadan **~4 dB yukarıda**. PRIZMA'nın `SoundMaster` hedefleri
(temizleme −9, bırakma −12, tık/kaldırma −15) ile **aynı düzen**; PRIZMA'da fark 3 dB, burada 4 dB.
Mutlak dBFS'ler karşılaştırılamaz (telefonun ses seviyesi araya giriyor) ama **oranlar tutuyor** —
`SoundMaster` çalışması doğru yerde.

### Temizleme sesi gerçek bir ezgi, çan değil

- Perdeler akort içinde (±10 sent), majör arpejler: `C5 – E5 – G5 – C6 – E6 – G6` gibi 4–8 notalık çıkış.
- Farklı temizlemeler **farklı tonalitede**: C, F, D, G, A♯, B, C minör… havuz halinde dolaşıp tekrar
  ediyor (t=13.03 ile t=61.86 birebir aynı dizi).
- İlk iki vuruş arası medyan **245–370 ms**.
- **Combo yükselince merdiven tükeniyor:** yüksek seride hep aynı 2.02 s'lik ezgi tekrarlıyor
  (t=41.8'den 110.7'ye kadar dokuz kez birebir aynı).

Son madde doğrudan kullanışlı: CLAUDE.md'de eski `_combo`'nun 5 basamakta tekrara düşmesi sorun olarak
yazılı ve `ComboLadder` 9 basamağa çıkarılmıştı. **Referans oyun da aynı hatayı yapıyor**, sadece daha
yukarıda. 9 basamak onlardan iyi durumda.

---

## 4. Performans ve tahta doluluğu

**Kare hızı.** En ağır an (çok satırlı temizleme + parçacıklar) seçilip kayıt karelerinin kaçının
gerçekten değiştiği sayıldı: kayıt 91.6 fps, değişen kare oranı %65 → **gerçek güncelleme ~59 kare/s**.
Tür standardı gerçek cihazda 60 FPS; PRIZMA'nın 0.97 ms/kare ölçümü doğru hedefte.

**Tahta doluluğu** (saniyede bir, hücre doygunluğundan; kutlama kaplamaları birkaç kareyi şişiriyor):

| | video1 | video2 | PRIZMA (ölçülen) |
|---|---|---|---|
| ortalama / medyan | %45 / %47 | %51 / %50 | — |
| %60 üstünde geçen süre | %21 | %29 | **%1–7** |
| ölüm anındaki doluluk | **%66–69** | — | **%52–61** |

**Referans oyun belirgin biçimde daha dolu oynanıyor ve daha dolu tahtada ölüyor.** PRIZMA'nın
dağıtıcısı onlarınkinden daha cömert. "Boş yerim az" şikâyetinin tersine, ölçüm PRIZMA'nın tahtasının
daha ferah olduğunu söylüyor.

---

## 5. PRIZMA için çıkarımlar

### Zaten PRIZMA'da var — değiştirme

Karşılaştırma yapılırken kodla doğrulandı, referans oyun bu dördünde **PRIZMA'nın gerisinde**:

- **Sürüklenen parça parmağın üstünde:** `GameScreen._liftPixels = CellSize * 1.55f` — referansın
  ~1 hücresinden fazla.
- **Hizalanmış hayalet:** `BoardView.ShowGhost` parçanın oturacağı hücreleri gösteriyor; referansta
  hayalet **hiç yok**, parça serbest yüzüyor ve nereye oturacağı ancak ışıktan anlaşılıyor.
- **Rekorun canlı takibi:** `GameHud.Refresh` → `Mathf.Max(HighScores.Best, _session.Score)`.
- **Skorun sayarak yükselmesi:** `GameScreen.ScoreRoutine` + `Tween.CountUp` (süresi farklı, aşağıda).

### Alınmaya değer (ve PRIZMA'da gerçekten eksik)

1. **Temizlenen hücrelerin ışığa dönüşmesi.** Referansta bloklar yok olmadan önce *kendileri* parlak
   huzme oluyor. PRIZMA'da `PlayLineSweep` blokların **üzerinden** 0.7 alfayla geçiyor — olay bloğun
   kendisinde değil, üstünde. En büyük "juice" farkı bu.
2. **Skor sayacının süresi.** PRIZMA 0.32 s, referans ~1.5 s. Kazanılan puanla ölçeklenen daha uzun
   bir sayım, hiçbir yeni öğe eklemeden gerilim üretiyor.
3. **Ön-temizleme ışığının tahta dışına taşması.** `AddPreviewBar` kenar payı 30 px; referansta ışık
   tahtanın belirgin biçimde dışına çıkıyor ve göz çok daha kolay yakalıyor. Alfaları (0.22 / 0.13)
   değiştirmeden sadece payı büyütmek sessizliği bozmaz.
4. **Oyun sonunda süreli ikinci şans.** Klasik modda gerçek ölümden sonra `ResultCard` var, karar anı
   yok. `SessionState.OutOfMoves` altyapısı zaten duruyor.
5. **Açılıştan tahtaya inen yol.** Referans doğrudan tahtaya düşüyor; PRIZMA'da arada menü var
   (`Screens.cs:205` kayıt varsa "DEVAM" yazıyor, yani bir dokunuş).

### Bilinçli olarak alınmaması gerekenler

CLAUDE.md'deki **"Yazıyla anlatma. Yanıp söndürme."** kuralıyla doğrudan çatışıyorlar:

- `Good!` / `Great!` / `Perfect!` / `Masterful!` rozet yağmuru
- `Combo N` büyük rozet, `NEW HIGH SCORE 100%` tam ekran patlama, `Better than 60%!` kupa kartı
- Skorun arkasındaki sürekli elmas parlaması

Bunlar o oyunun karakteri ve iyi çalışıyor, ama PRIZMA'nın sessiz dilinin tam zıddı. "2 SATIR BİRDEN"
rozeti bir kez eklenip kaldırılmıştı; bu oyun onun on katını yapıyor. **İkisi bir arada olmaz** — ya
sessiz kalınır ya bu yola girilir.

### Ses tarafı

Tını yargısı kullanıcınındır, o yüzden öneri değil gözlem: bu oyunun temizleme sesi gerçek bir majör
arpej, çan merdiveni değil, ve tonalitesi değişiyor. "Kuru, kısa, parlak" tercihine ters düşmüyor;
aksine o estetiğin kendisi. `Tools/AudioCheck wav` ile oyunun gerçek zamanlamasıyla kurgulanmış dizi
çıkarılıp bu iki klibin sesiyle yan yana dinletilebilir.

---

## Yöntem

Makinede `ffmpeg` yoktu; scratchpad'de izole bir venv'e `imageio-ffmpeg` + `numpy` kuruldu
(projeye ve sistem Python'una dokunulmadı).

- **Kareler:** 3 s aralıkla tam tur taraması; kritik anlar 20 fps'e kadar, gerektiğinde kırpılıp büyütülerek.
- **Animasyon süreleri:** 74x160 gri kareye indirgenmiş görüntüde tahta / tepsi / skor bölgelerinin
  ardışık kare farkının ortalaması.
- **Kare hızı:** kapsayıcı pts damgaları + ardışık kare farkı eşiği (değişen kare oranı × kapsayıcı fps).
- **Doluluk:** tahta bölgesi 8x8'e ölçeklenip hücre doygunluğu (`max−min` RGB) eşiklenerek, saniyede bir.
- **Ses:** WAV'a ayrıştırma, 5 ms adımlı RMS zarfı ile olay tespiti (eşik −45 dBFS, 120 ms birleştirme),
  her olayda tepe / RMS / spektral merkez / baskın frekans; uzun olaylarda 60 ms pencerelerle perde izi
  ve nota/sent dönüşümü.
- **Kaldırma/bırakma ayrımı** ses ile karenin eşleştirilmesiyle doğrulandı: 20 fps karelerde parçanın
  tepsiden ayrıldığı an 1553 Hz'lik sese, tahtaya oturduğu an 545 Hz'lik sese denk geliyor.
