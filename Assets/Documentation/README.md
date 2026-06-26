# Battle Pass Road — Unity Kurulum Rehberi
# Technical Artist Teslim Dokümanı
# Sefa Güngör Özkan

---

## Proje Yapısı

```
Assets/
├── BattlePass/
│   └── Rewards/
│       ├── FreeRewards/              ← 10 adet ücretsiz ödül ScriptableObject'i (L01–L10)
│       └── PremiumRewards/           ← 10 adet premium ödül ScriptableObject'i (L01–L10)
├── Documentation/
│   └── README.md                     ← Bu doküman
├── Materials/
│   ├── Model/                        ← 3D model materyalleri (Mcx_TopScorer, WaveMat)
│   ├── Particle/                     ← Parçacık materyalleri (Claim, Shine, Weapon_Particle)
│   └── UI/                           ← UI materyalleri (DiagonalShine, DiagonalScroll, Gradient)
├── Models/
│   ├── spcl_rif_mcx_topscorer.fbx    ← Task 2 silah modeli
│   └── Wave.fbx                      ← Task 2 dalga modeli
├── Prefabs/
│   ├── Nodes/
│   │   ├── BattlePassNode.prefab     ← Battle pass node prefab'ı
│   │   └── LevelBubble.prefab        ← Seviye baloncuk prefab'ı
│   └── WeaponVfx/
│       └── WeaponVfx.prefab          ← Task 2 silah VFX prefab'ı
├── Scenes/
│   ├── Task1_UI.unity                ← Task 1 — Battle Pass UI sahnesi
│   └── Task2_Vfx/
│       ├── Task2_Vfx.unity           ← Task 2 — Silah VFX sahnesi
│       └── Global Volume Profile.asset ← Task 2 post-processing profili
├── Scripts/
│   ├── Data/
│   │   └── BattlePassRewardData.cs   ← ScriptableObject ödül tanımı
│   ├── BattlePass/
│   │   ├── BattlePassManager.cs       ← Runtime durum makinesi (singleton)
│   │   └── SeasonConfig.asset         ← Sezon konfigürasyon ScriptableObject'i
│   ├── UI/
│   │   ├── BattlePassRoadScreen.cs    ← Ana ekran kontrolcüsü
│   │   ├── BattlePassNodeUI.cs        ← Node başına kart kontrolcüsü + animasyonlar
│   │   ├── BattlePassDebugUI.cs       ← Debug paneli (+XP, Reset, Claim All, Premium)
│   │   ├── LevelBubbleUI.cs           ← Seviye numarası balonuğu
│   │   └── ClaimAllButton.cs          ← Kademeli toplu claim taraması
│   └── VFX/
│       └── NodeVFXController.cs       ← Node başına tüm parçacık + shader VFX kontrolü
├── Settings/
│   ├── Mobile_RPAsset.asset           ← URP mobil render pipeline ayarları
│   ├── Mobile_Renderer.asset          ← URP mobil renderer ayarları
│   ├── PC_RPAsset.asset               ← URP PC render pipeline ayarları
│   ├── PC_Renderer.asset              ← URP PC renderer ayarları
│   └── DefaultVolumeProfile.asset     ← Varsayılan post-processing profili
├── Shaders/
│   ├── ShineSweep.shader              ← Diyagonal parlama taraması (URP)
│   ├── DiagonalShine.shader           ← UI Image'ler için şerit parlama bandı (URP)
│   ├── DiagonalScroll.shader          ← Diyagonal kayan desen katmanı (URP)
│   ├── WaveScroll.shader              ← Dalga deformasyonu + kayan doku, additive (URP)
│   └── ParticleAdditiveHDR.shader     ← Additive parçacık shader'ı, HDR tint, stencil (URP)
├── Textures/
│   ├── Star.jpg                       ← Yıldız doku
│   ├── Wave1–4.jpg                    ← Dalga dokuları (Task 2)
│   └── t_spcl_rif_mcx_topscorer_diffuse.tga ← Silah difüz dokusu
└── UI/
    └── Sprites/                       ← Tüm UI sprite'ları (ikonlar, butonlar, çerçeveler, vb.)
```

---

## Hızlı Başlangıç

Proje iki ayrı görevden oluşur. Her görevin kendi sahnesi mevcuttur.

### Görev 1 — Battle Pass UI

1. `Assets/Scenes/Task1_UI.unity` sahnesini açın.
2. Sahne yüklendiğinde tüm battle pass elementleri otomatik olarak ekrana gelir.
3. **Play**'e basın. Ekranın üst kısmında bir **Debug Paneli** bulunur.
4. Test akışı:
   - **+XP butonu** — XP ekleyerek seviye atlayın; böylece ödüller toplanabilir hale gelir.
   - **Reset butonu** — Seviyeyi sıfırlayın.
   - **Claim All butonu** — Tüm toplanabilir (claimable) ödülleri tek seferde toplayın.
   - **Premium butonu** — Premium ödül track'ini açın.
5. Node'lar üzerinde **tıklayarak** da test yapabilirsiniz. Kilitli (locked) ve açık (unlocked) node'lar farklı tepkiler verir — tıklayarak aradaki farkı gözlemleyin.

### Görev 2 — Silah VFX

1. `Assets/Scenes/Task2_Vfx/Task2_Vfx.unity` sahnesini açın.
2. **Play**'e basın. Silah üzerindeki VFX (parçacık efektleri, dalga deformasyonu, parlama) otomatik olarak oynar.

---

## Sahne Hiyerarşisi (Task 1)

```
Canvas (Screen Space — Camera)
└── BattlePassScreen
    ├── [BattlePassManager]            ← Component ekle, SeasonConfig'i bağla
    ├── DebugPanel/
    │   ├── AddXPButton               (Button — +XP)
    │   ├── ResetButton               (Button — Reset)
    │   ├── ClaimAllButton            (Button — Claim All)
    │   └── PremiumButton             (Button — Premium)
    ├── Header/
    │   ├── SeasonLabel               (TMP)
    │   ├── XPBar                     (Slider)
    │   ├── XPText                    (TMP)
    │   ├── LevelText                 (TMP)
    │   └── TimeLeftText              (TMP)
    ├── PremiumBanner/
    │   ├── HeroImage                 (Image)
    │   ├── MythicLabel               (TMP)
    │   ├── PremiumInactiveOverlay    (GameObject)
    │   └── GetButton                 (Button)
    ├── ScrollView (yatay)
    │   └── Viewport
    │       └── Content
    │           ├── PremiumTrackRow    (HorizontalLayoutGroup, spacing=8)
    │           ├── LevelConnectorRow  (HorizontalLayoutGroup, spacing=8)
    │           └── FreeTrackRow       (HorizontalLayoutGroup, spacing=8)
    ├── ClaimAllButton                (Button + ClaimAllButton.cs)
    └── TabBar/
        ├── ArenaPassTab
        └── MissionsTab
```

---

## Node Prefab Hiyerarşisi

```
BattlePassNode (Prefab root)  ← BattlePassNodeUI.cs
├── Background              (Image)
├── LockIcon                (Image — asma kilit sprite'ı)
├── IconImage               (Image — ödül sprite'ı, 80×80px)
├── LabelText               (TMP — ödül adı, 12pt)
├── AmountText              (TMP — "x100", 10pt)
├── ClaimButton             (Button)
│   └── ClaimLabel          (TMP — "UNLOCK NOW")
├── ClaimedStamp            (Image — onay işareti katmanı)
├── CurrentIndicator        (Image — animasyonlu halka)
├── PremiumBadge            (Image — taç ikonu)
└── VFXContainer
    ├── NodeVFXController.cs (bu objede)
    ├── ParticleGlow         (Particle System)
    ├── ParticleShine        (Particle System)
    ├── ParticleClaim        (Particle System)
    ├── ParticlePremium       (Particle System)
    └── GlowImage             (Raw Image — parlama shader'ı)
```

---

## Parçacık Sistemi Ayarları


### ParticleClaim (tek seferlik patlama)
| Özellik | Değer |
|---|---|
| Duration | 0.6 |
| Looping | false |
| Start Lifetime | 0.5–0.8 |
| Start Speed | 80–160 |
| Start Size | 6–14 |
| Max Particles | 20 |
| Emission | Burst: t=0'da 20 |
| Shape | Circle, Radius 5 |
| Gravity Modifier | -0.2 (yukarı doğru yükselme) |
| Color over Lifetime | Parlak → şeffaf |
| Material | BPNode_VFX (Additive) |

### ParticlePremium (premium track mücevher tozu)
| Özellik | Değer |
|---|---|
| Duration | 2.0 |
| Looping | true |
| Start Lifetime | 1.2–2.0 |
| Start Speed | 10–20 |
| Start Size | 4–8 |
| Max Particles | 10 |
| Color | Mor gradyan |
| Material | BPNode_VFX (Additive) |

---

## Shader Materyalleri


### DiagonalShineMat
- Shader: `BattlePass/DiagonalShine`
- Herhangi bir UI Image üzerinde diyagonal olarak süpüren şerit şeklinde bir vurgu,
  her `_SweepPeriod` saniyede bir döngü yapar (varsayılan 1 sn).
- **Screen-space mod** — `_ScreenSpace = 1` iken parlama bandı ekran koordinatlarından
  projeksiyon yapılır; aynı materyali paylaşan tüm UI Image'lerde tek sürekli tarama
  görünür (DiagonalScroll ile aynı teknik). `_ScreenSpace = 0` iken her Image kendi
  bağımsız taramasını yapar.
- `DiagonalShineMat`'i herhangi bir UI Image'in **Material** slot'una atayın.
- Inspector özellikleri:
  - `_MainTex` — baz sprite dokusu (UI Image'ın kendi dokusu)
  - `_ShineColor` — parlama bandının rengi (varsayılan beyaz, alpha 0.8)
  - `_ShineWidth` — şerit kalınlığı, Range 0.01–5 (varsayılan 0.12)
  - `_ShineAngle` — tarama yönü derece cinsinden, Range 0–360 (varsayılan 30)
  - `_SweepPeriod` — tam tarama başına saniye (varsayılan 1.0)
  - `_PauseDuration` — taramalar arası duraklama saniye cinsinden (varsayılan 0.0)
  - `_ShineDistance` — bandın UV kenarlarının ötesine ne kadar ilerlediği, Range -10–10 (varsayılan 0.2)
  - `_ShineIntensity` — parlaklık çarpanı (varsayılan 1.0)
  - `_AutoPlay` — 1 = zaman tabanlı döngü, 0 = `_SweepOffset` ile manuel
  - `_SweepOffset` — `_AutoPlay` = 0 iken manuel tarama konumu, Range -10–10 (varsayılan 0)
  - `_ScreenSpace` — 1 = ekran uzayı taraması (tüm Image'lerde sürekli), 0 = her Image bağımsız (varsayılan 0)
- Blend: Alpha (SrcAlpha, OneMinusSrcAlpha) — overlay blend, sprite şeklini korur
- Stencil etkin (ScrollRect/Mask/RectMask2D uyumlu)
- SRP-Batcher uyumlu, `clip()` fill-rate culling

### DiagonalScrollMat
- Shader: `BattlePass/DiagonalScroll`
- Bir desen dokusunun UI Image üzerinde sürekli olarak diyagonal kayması,
  arka plan üzerine overlay blend modu ile harmanlanır.
- Kayan desen, ekran uzayından (screen-space) projeksiyon yapılır — UI Image'ın
  boyutundan veya en-boy oranından bağımsız olarak doku ölçeği sabit kalır.
- `DiagonalScrollMat`'i bir arka plan UI Image'in **Material** slot'una atayın.
- Inspector özellikleri:
  - `_MainTex` — baz sprite dokusu (UI Image'ın kendi dokusu)
  - `_ScrollTex` — kayan desen dokusu (seamless/tileable olmalı,
    Wrap Mode = Repeat)
  - `_ScrollColor` — kayan desen tint rengi (varsayılan beyaz, alpha 0.5)
  - `_ScrollSpeed` — kayma hızı (varsayılan 0.3)
  - `_ScrollAngle` — diyagonal kayma yönü derece cinsinden, Range 0–360 (varsayılan 45)
  - `_ScrollScale` — ekran uzayı doku döşeme yoğunluğu (yüksek = daha fazla tekrar, varsayılan 1.0)
  - `_TextureRotation` — desen dönüşü derece cinsinden, Range 0–360 (varsayılan 0)
  - `_OverlayStrength` — overlay blend yoğunluğu, Range 0–1 (varsayılan 0.5)
- Blend: Alpha (SrcAlpha, OneMinusSrcAlpha)
- Stencil etkin (ScrollRect/Mask/RectMask2D uyumlu)
- SRP-Batcher uyumlu, `clip()` fill-rate culling

### WaveScroll
- Shader: `BattlePass/WaveScroll`
- İkisi bir arada: sinüs dalgası mesh deformasyonu + kayan doku, additive blend.
- 3D mesh'ler için tasarlanmıştır (UI değil). Pürüzsüz dalga deformasyonu için
  yüksek vertex sayılı mesh gerektirir (örn. 50×50 alt bölümlü düzlem).
- Dalga ve gürültü (noise) deformasyonu world-space koordinatlarda hesaplanır —
  aynı materyali paylaşan birden fazla mesh arasında tutarlı görünür.
- **Fake bloom desteği** — `_GlowPower` ve `_GlowBoost` ile post-processing bloom'u
  shader içinde prosedürel olarak taklit eder. Bloom post-processing'i kapatıp bu
  property'leri ayarlayarak ~16 draw call tasarruf sağlanır.
- Inspector özellikleri:
  - `_MainTex` — kayan doku (Wrap Mode = Repeat)
  - `_TintColor` — HDR renk tint'i (bloom için intensity > 1)
  - `_WaveAmplitude` — dalga yüksekliği (varsayılan 0.1)
  - `_WaveFreqX` — X ekseni dalga yoğunluğu (varsayılan 2.0)
  - `_WaveFreqZ` — Z ekseni dalga yoğunluğu (varsayılan 2.0)
  - `_WaveSpeed` — dalga animasyon hızı (varsayılan 1.0)
  - `_WaveDirection` — dalga ilerleme yönü derece cinsinden, Range 0–360 (varsayılan 0)
  - `_NoiseAmplitude` — prosedürel gürültü genliği (varsayılan 0.05)
  - `_NoiseScale` — gürültü doku ölçeği (varsayılan 3.0)
  - `_NoiseSpeed` — gürültü animasyon hızı (varsayılan 0.5)
  - `_NoiseInfluence` — gürültünün dalgaya etkisi, Range 0–1 (varsayılan 0.5)
  - `_ScrollSpeed` — doku kayma hızı (varsayılan 0.3)
  - `_ScrollAngle` — doku kayma yönü derece cinsinden, Range 0–360 (varsayılan 45)
  - `_ScrollScale` — doku döşeme ölçeği (varsayılan 1.0)
  - `_TextureRotation` — doku dönüşü derece cinsinden, Range 0–360 (varsayılan 0)
  - `_FadeIn` — giriş kenarı geçiş genişliği, Range 0–0.5 (varsayılan 0.15)
  - `_FadeOut` — çıkış kenarı geçiş genişliği, Range 0–0.5 (varsayılan 0.15)
  - `_GlowPower` — fake bloom alpha power curve, Range 0.25–4.0 (varsayılan 1.0 = kapalı, <1.0 = daha yumuşak glow)
  - `_GlowBoost` — fake bloom radial halo parlama, Range 0–3.0 (varsayılan 0.0 = kapalı)
- Blend: Additive (SrcAlpha, One)
- SRP-Batcher uyumlu, `clip()` fill-rate culling

### ParticleAdditiveHDR
- Shader: `BattlePass/ParticleAdditiveHDR`
- HDR tint ve stencil desteği olan unlit additive parçacık shader'ı.
- Parçacık sistemleri için optimize edilmiştir: tek pass, minimal ALU, fill-rate culling.
- **Fake bloom desteği** — `_GlowPower` ve `_GlowBoost` ile post-processing bloom'u
  shader içinde prosedürel olarak taklit eder. Bloom post-processing'i kapatıp bu
  property'leri ayarlayarak ~16 draw call tasarruf sağlanır.
- Inspector özellikleri:
  - `_MainTex` — parçacık dokusu
  - `_TintColor` — HDR renk tint'i (bloom parlama için intensity > 1)
  - `_ColorStrength` — renk çarpanı (varsayılan 1.0)
  - `_AlphaStrength` — alpha çarpanı, Range 0–5 (varsayılan 1.0)
  - `_GlowPower` — fake bloom alpha power curve, Range 0.25–4.0 (varsayılan 1.0 = kapalı, <1.0 = daha yumuşak glow)
  - `_GlowBoost` — fake bloom radial halo parlama, Range 0–3.0 (varsayılan 0.0 = kapalı)
  - `_SoftParticlesNear` — soft parçacık geçiş başlangıç mesafesi (varsayılan 0.0)
  - `_SoftParticlesFar` — soft parçacık geçiş bitiş mesafesi (varsayılan 0.0)
- Blend: Additive (SrcAlpha, One)
- Stencil etkin (Mask/RectMask2D/ScrollRect uyumlu)
- SRP-Batcher uyumlu, `clip()` fill-rate culling
- Soft parçacıklar `_SOFTPARTICLES_ON` keyword ile opsiyonel (URP Depth Texture gerektirir)


---

## Durum Makinesi Özeti

`NodeState` enum'u beş olası durum tanımlar:

```
Locked     — oyuncu bu seviyeye henüz ulaşamadı (premium track'de premium yoksa da Locked)
Unlocked   — serbest ama henüz toplanabilir değil (UI/VFX katmanında kullanılır)
Claimable  — oyuncu bu seviyeyi geçti ve henüz toplamadı — Claim butonu aktif
Claimed    — oyuncu Claim'e bastı, ödül toplandı
Current    — oyuncu şu an tam olarak bu seviyede (UI/VFX katmanında kullanılır)
```

> **Not:** `BattlePassManager.GetNodeState()` runtime'da şu üç durumu atar:
> `Locked`, `Claimable` veya `Claimed`. Oyuncunun bulunduğu seviye (`nodeLevel == playerLevel`)
> şu an `Locked` olarak gösterilir — ödüller henüz toplanabilir değildir.
> `Unlocked` ve `Current` durumları UI/VFX katmanında (önizleme, animasyon) kullanılmak üzere
> tanımlanmıştır; Manager tarafından runtime'da atanmaz.

- **BattlePassManager** tüm durum geçişlerini yönetir
- **BattlePassNodeUI** tüm görsel/animasyon tepkilerini yönetir
- **NodeVFXController** tüm parçacık/shader tepkilerini yönetir
- UI'da durum depolanmaz — her zaman Manager'dan yönetilir

---

## Backend Entegrasyon Kontrol Listesi

Gerçek bir backend'e bağlanırken şu 3 çağrıyı değiştirin:

```csharp
// 1. Oyun başlangıcında / oturum devamında:
BattlePassManager.Instance.Initialize(
    xp: playerSave.battlePassXP,
    premiumOwned: playerSave.hasPremiumPass
);

// 2. XP kazanıldıktan sonra:
BattlePassManager.Instance.AddXP(amount);

// 3. Premium satın alındıktan sonra:
BattlePassManager.Instance.UnlockPremium();
```

`BattlePassManager.cs` içindeki `TryClaimReward()` callback'i, envanter/para birimi
verme sisteminize bağlanacağınız yerdir — `// TODO` yorumu ile işaretlenmiştir.

---

## Performans Bütçesi

### Task 1 — Battle Pass UI

**Statistics (Play modu, editor):**

| Metrik | Sprite Atlas öncesi | Sprite Atlas sonrası |
|---|---|---|
| CPU main | 4.8 ms | 4.9 ms |
| Render thread | 1.5 ms | 1.4 ms |
| Tris | 3.3k | 3.4k |
| Verts | 8.4k | 8.1k |
| Batches | 99 | **40** |
| SetPass calls | 53 | **38** |
| Saved by batching | 0 | 0 |

> **Not:** UI sistemlerinde batching sınırlıdır. Her UI Image ayrı bir draw call
> olabilir çünkü Canvas her batch için aynı materyali, aynı texture atlas'ı ve aynı
> clip rect'i gerektirir. 30 node × birden fazla Image/Text alt eleman = yüksek batch
> sayısı doğal bir sonuçtur. Editor'da ölçülmüştür; build'te ~2–3x daha düşük beklenir.

> **Sprite Atlas ile kazanım:** Tüm UI sprite'ları tek bir atlas'a pack'lendiğinde,
> aynı atlas'ı paylaşan UI Image'ler tek batch'te çizilir. Batches **99 → 40**'a
> düştü (~%60 azalma), SetPass calls **53 → 38**'e düştü (~%28 azalma). CPU main
> değerindeki küçük artış editor ölçüm gürültüsüdür — build'te bu fark kaybolur.



### Task 2 — Silah VFX

**Statistics (Play modu, editor):**

| Metrik | Bloom açık | Fake bloom (shader) |
|---|---|---|
| CPU main | 4.3 ms | ~2.5 ms |
| Render thread | 1.0 ms | ~0.6 ms |
| Tris | 15.3k | 15.3k |
| Verts | 19.5k | 19.5k |
| Batches | 50 | ~34 |
| SetPass calls | 31 | ~15 |
| Frame Debugger draw call | 38 | **22** |
| Saved by batching | 0 | 0 |

> **Fake bloom ile kazanım:** Bloom post-processing kapatılıp, bunun yerine
> `ParticleAdditiveHDR` ve `WaveScroll` shader'larındaki `_GlowPower` ve `_GlowBoost`
> property'leri ile prosedürel glow kullanıldığında, Frame Debugger'da draw call
> **38 → 22**'ye düşer (~%42 azalma). Shader başına ek maliyet ~5-8 ALU instruction
> (ölçülemez kadar küçük), sıfır ek draw call.




