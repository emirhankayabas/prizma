# PRIZMA — Yayına hazırlık değerlendirmesi

**Tarih:** 2026-09-13 · **Durum:** oyun olarak neredeyse hazır, yayın paketi olarak henüz değil.

Bugünkü APK Google Play'e yüklenemez; yüklenebilse de yüklenmemeli. Eksiklerin çoğu teknik ve birkaç
günde kapanır. Asıl uzun süren kısım Google'ın zorunlu kapalı test süreci.

## Güncelleme — 2026-10-03: Aşama 0'ın kodla yapılan kısmı bitti

| Madde | Durum |
|---|---|
| AAB | **Tamam.** *PRIZMA → Android Yayın Build'i (AAB + APK)* `Builds/<sürüm>-active/` altına `PRIZMA-<sürüm>.aab` (Play'e) ve aynı paketten `PRIZMA-<sürüm>.apk` (telefona) üretir; eski sürümün `-active` işareti kalkar |
| Yayın imzası | **Tamam.** Yükleme anahtarı `%USERPROFILE%\.prizma\prizma-upload.jks`, ayarı `signing.json` — ikisi de projenin ve git'in dışında. **Yedekle** (şifre yöneticisi + ikinci bir yer) |
| versionCode | **Tamam.** 1 Ocak 2026'dan beri geçen dakika; hep artar, dosya tutmaz. versionName (`1.0.0`, semantik sürüm) elle |
| Geliştirme paketleri | **Tamam.** `Editor/DevAssemblyFilter` yayın build'inde AI Assistant, Pipeline (HTTP sunucusu + Roslyn), Visual Scripting, AI Navigation derlemelerini çıkarır; Editor'de kalırlar |
| `submitAnalytics` + Engine Diagnostics | **Tamam.** Yayın build'i ikisini de kapatır |
| Build sonu kontrolü | **Tamam.** Hedef API ≥ 36, izin listesi (`ReleaseBuild.Checks.Allowed`), debug imza yok, debuggable değil, 16 KB (zip + ELF), oyuna geliştirme derlemesi girmemiş. Biri tutmazsa dosyalar silinir, neden `Builds/PRIZMA-build.txt`'de |
| INTERNET izni | **Karar değişti:** reklam gelecek, izin listesinde. Reklam SDK'sı eklenince onun izinleri de listeye, gizlilik metnine ve Veri güvenliği formuna aynı değişiklikte girer |
| Gizlilik politikası | **Metin hazır:** `Docs/PrivacyPolicy.html` (TR + EN). Eksik: geliştirici adı, e-posta, kalıcı adres. Adres `ShareSheet.PrivacyLink`'e yazılınca ayarlarda bağlantı çıkar |
| Ekran uykusu, sürüm etiketi | Zaten yapılmıştı |
| Dil | TR + EN; diller artık `Assets/Resources/Lang/*.json` |

**Telefonda deneme:** eski APK debug anahtarıyla imzalıydı; yenisi farklı anahtarla imzalı olduğu için
**üstüne kurulmaz** — önce eskisini kaldır (kayıtlı ilerleme silinir).

---

## Hazır olanlar

- **Oynanış:** özellikler tamam — 3 mod (Klasik / Günlük / Macera), Prizma güçleri, kaydet/devam, temalar.
- **Testler:** `Tools/CoreHarness tests` — 27 testin 27'si geçiyor.
- **Hedef API 36:** Play, 31 Ağustos 2026'dan beri yeni uygulama ve güncellemelerde bunu şart koşuyor.
- **16 KB bellek sayfası uyumu:** APK içindeki bütün `.so` dosyalarının LOAD hizası `0x4000`, `zipalign -P 16` doğrulaması geçti.
- **Mimari:** IL2CPP + ARM64, Development Build kapalı, uygulama debuggable değil. `minSdk` 26.
- **Ücret/lisans:** reklam, satın alma, hesap sistemi yok. Tek dış varlık OFL lisanslı Poppins fontu.

## Yayını engelleyenler

1. **Gerçek telefonda hiç test edilmedi.** CLAUDE.md'de "sırada": dokunma hissi, ses, sürükleme mesafesi,
   titreşim, önizlemenin parmak altında okunurluğu. En büyük ürün riski.
2. **APK debug sertifikasıyla imzalı** (`CN=Android Debug`). Play kabul etmez; yükleme anahtarı (keystore)
   gerekiyor. Anahtar kaybolursa uygulama bir daha güncellenemez — **yedeklenmesi şart**.
3. **Format APK.** Play yeni uygulamalar için AAB istiyor; `Editor/ReleaseBuild` şu an
   `buildAppBundle = false` yapıyor.
4. **Sürüm `1.0.0` (semantik sürüm) / versionCode otomatik.** Her yüklemede versionCode artmalı; build betiği otomatik yapmalı.
5. **Geliştirme araçları oyunun içine giriyor** (APK açılıp incelendi):
   - `com.unity.pipeline` (deneysel): oyun açılırken sahneden önce `RuntimePipelineBootstrap` ve
     `ConsoleLogCapture` çalışıyor; her log satırı bir tampona kopyalanıyor. İçinde HTTP sunucusu
     (port 7900–7949) ve Roslyn C# derleyicisi var. Sunucu yapılandırılmadığı için büyük ihtimalle
     başlamıyor, ama yayın build'inde olmamalı.
   - AI Assistant'ın `Unity.AI.MCP.Runtime`, Visual Scripting, AI Navigation, Collections test DLL'leri.
   - Sonuç: hiç ağ kullanmayan oyun **`android.permission.INTERNET`** istiyor; APK 32 MB,
     `libil2cpp.so` açılmış hâlde 50 MB.
   - **Bilinçli eklenen izinler (günlük hatırlatıcı):** `POST_NOTIFICATIONS` (Android 13+, oyun içinde ilk
     çözümden sonra soruluyor) ve `RECEIVE_BOOT_COMPLETED` (telefon yeniden başlayınca alarmı geri kurmak için).
     `Editor/ReminderManifest` build sırasında ekliyor. İkisi de veri toplamaz; Veri güvenliği formunu değiştirmez.
     Tam zamanlı alarm izni (`SCHEDULE_EXACT_ALARM`) **bilerek yok** — hatırlatıcı birkaç dakika geç gelebilir.
6. **Unity donanım istatistikleri açık** (`ProjectSettings.asset` → `submitAnalytics: 1`). Böyle kalırsa
   "Veri güvenliği" formunda "veri toplanmıyor" demek dürüst olmaz. Kapatılmalı.
7. **Gizlilik politikası yok.** Hiç veri toplanmasa bile Play her uygulama için politika bağlantısı istiyor.
   Oyun içinden (ayarlar) de erişilebilir olmalı.
8. **Kapalı test şartı.** 13 Kasım 2023 sonrası açılmış kişisel geliştirici hesaplarında üretime çıkmadan
   önce **en az 12 test kullanıcısı, kesintisiz 14 gün** kapalı test. 2026'dan beri testçilerin uygulamayı
   gerçekten kullanıp kullanmadığına da bakılıyor. Takvimi en çok bu belirliyor.

## Yapılması gerekenler

- **Mağaza sayfası:** 512 px ikon (`Editor/AppIconGenerator` hazır), 1024×500 tanıtım görseli, telefon
  ekran görüntüleri, kısa/uzun açıklama, içerik derecelendirme anketi. Hedef kitle **13+** önerilir;
  13 yaş altı seçilirse Aileler politikası devreye girer.
- **İsim:** PRIZMA hâlâ çalışma adı; Play'de başka "Prizma" uygulamaları var (cam firması uygulaması,
  bir Minecraft doku paketi). Mağaza başlığı önerisi: **"PRIZMA: Blok Bulmaca"**. Paket kimliği
  `com.emirhankayabas.prizma` ilk yüklemeden sonra değişmez; görünen isim değişebilir.
- **Ekran uykusu:** kodda `Screen.sleepTimeout` yok; uzun düşünen oyuncunun ekranı kararır. Oyun
  sayfasında `NeverSleep`, menüde `SystemSetting`.
- **Sürüm numarası:** ayarlarda görünsün (destek konuşmaları için).
- **Dil:** arayüz yalnız Türkçe → önce Türkiye'de yayın, İngilizce sonra.
- **Splash:** Unity 6 Personal'da Unity logolu açılış ekranı kapatılabiliyor (isteğe bağlı).
- **Diğer sessiz ayarlar:** `muteOtherAudioSources: 0` (oyuncunun müziğinin üstüne çalar — bilinçli karar
  olsun), çökme görünürlüğü için başlangıçta Play Console "Android vitals" yeterli.

## Önerilen yol

### Aşama 0 — kodla yapılabilecekler (1–2 gün)
- Yayın build hattı: AAB, imza ayarı (keystore yolu/alias; şifreler repoya **girmez**), otomatik
  versionCode, geliştirme paketlerini yayın build'inden çıkarma, `submitAnalytics` kapalı.
- Build sonu kontrolü: manifestte INTERNET izni yok, debug imzası yok, hedef API ≥ 36, 16 KB uyumu.
- Gizlilik politikası metni + ayarlarda bağlantı.
- Oyun sırasında ekranın uyumaması, sürüm etiketi.
- Mağaza ekran görüntüleri (`Tools/autotest.ps1` ile), tanıtım görseli (Raster), Türkçe mağaza metinleri.

### Aşama 1 — geliştiricinin adımları
- Play Console hesabı.
- Keystore oluşturma (şifre geliştiricide kalır) ve güvenli yedek.
- APK'yı kendi telefonunda oynamak; CLAUDE.md "Sırada" listesindeki her maddeye bakmak.
- 12 kişilik kapalı testi başlatmak.

### Aşama 2 — yayın
- 14 günlük test boyunca gelen düzeltmeler.
- Üretim erişimi başvurusu → kademeli yayın (%20 → %100).

**Gerçekçi en hızlı takvim:** yaklaşık 3 hafta (çoğu kapalı test süresi).

## Açık kararlar

1. Play Console hesabı var mı, ne zaman açıldı? (kapalı test şartı buna bağlı)
2. İlk sürümde reklam / satın alma olacak mı? Öneri: yok, 1.0 temiz çıksın.
3. Mağaza adı "PRIZMA: Blok Bulmaca" olsun mu?
4. `com.unity.ai.assistant` ve `com.unity.pipeline` Editor'de kullanılıyor mu? Kullanılıyorsa tamamen
   kaldırılmaz; yalnız yayın build'i sırasında çıkarılıp geri konur.
5. Gizlilik politikası nerede barındırılacak (GitHub Pages ya da benzeri kalıcı bir adres)?

## Bu değerlendirmede nasıl bakıldı

- `ProjectSettings/ProjectSettings.asset`, `UnityConnectSettings.asset`, `Packages/manifest.json` okundu.
- `Builds/PRIZMA.apk` (13 Eylül 03:01 build'i) Unity'nin Android SDK araçlarıyla incelendi:
  `aapt2 dump badging` (izinler, hedef API), `apksigner verify --print-certs` (imza),
  `zipalign -c -P 16` + ELF program başlıkları (16 KB), `ScriptingAssemblies.json` (giren DLL'ler).
- `Library/PackageCache` içinde `com.unity.pipeline` ve `com.unity.ai.assistant` runtime kodu okundu.
- `Tools/CoreHarness tests` koşuldu.

## Kaynaklar

- [Target API level requirements — Play Console Help](https://support.google.com/googleplay/android-developer/answer/11926878?hl=en)
- [App testing requirements for new personal developer accounts — Play Console Help](https://support.google.com/googleplay/android-developer/answer/14151465?hl=en)
- [User Data policy — Play Console Help](https://support.google.com/googleplay/android-developer/answer/10144311?hl=en)
- [Google Play Store privacy policy requirements — Termly](https://termly.io/resources/articles/google-play-store-privacy-policy-updates/)
