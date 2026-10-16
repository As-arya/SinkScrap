# SinkScrapt

Alpha horor salvage first-person untuk 1–4 pemain. Ekspedisi berlangsung 10 menit: kapal kargo stabil selama 4 menit pertama lalu tenggelam pada 6 menit terakhir. Interior tiga lantai, letak pintu masuk, pintu terkunci, loot, serta bahaya berubah pada setiap ekspedisi. Barang di kapal kecil hanya selamat jika minimal satu kru kembali ke kapal; jika seluruh kru mati atau tertinggal, semua barang di kapal ikut hilang.

The detailed English product and technical plan is in [`Docs/PRD.md`](Docs/PRD.md). Claude Code should begin with [`CLAUDE.md`](CLAUDE.md), which explains the project workflow and core gameplay invariants.

## Mainkan

1. Buka proyek ini dengan Unity **6000.6.5f1**.
2. Buka `Assets/SinkScrapt/Scenes/Bootstrap.unity`, lalu tekan Play.
3. Pilih **Main Solo**, **Buat Permainan Online**, **Gabung Melalui Kode**, atau **Jaringan Lokal**.
4. Kampanye baru dimulai di kapal kecil di tengah laut dengan modal tim $120. Seluruh alat harus dibeli: buka **Komputer Kapal**, tab **TOKO**, isi paket (maks. 4 alat) lalu **BAYAR & TERJUNKAN**. Sekitar 30 detik kemudian paket turun dengan parasut ke dek kapal kecil; tekan E pada paket untuk membukanya. Pemesanan bisa dilakukan kapan saja (di laut, saat ekspedisi, atau di pulau). Tabung oksigen juga diisi ulang di komputer.
5. Pakai kemudi dan pilih kapal kargo. Setibanya, cutscene kedatangan diputar dan jam ekspedisi baru berjalan setelahnya.
6. Panjat tali, masuk melalui palka A (selalu di dek atas), B, atau C (bisa langsung ke dek bawah), dan bawa scrap kembali ke kapal kecil. Barang yang ditaruh (G) di kapal kecil tetap di tempatnya, tetapi **hilang semua jika tidak ada kru yang kembali ke kapal** saat berangkat. Alat bisa disimpan di **Rak Alat** dalam kabin. Tab **PETA** di komputer kapal menunjukkan dek tempat rekan yang dipantau berada (← / → ganti kru), level air, sisa waktu, dan tombol untuk mematikan bahaya selama 30 detik.
7. Gunakan kemudi untuk berangkat, atau kembali sebelum keberangkatan otomatis. Kru tetap di laut di antara ekspedisi.
8. Setelah hari ketiga (ekspedisi ketiga), berlayar ke **Pulau Dermaga 04** lewat kemudi. Jual scrap ke **pedagang** (penyelam tua berhelm kuningan), lalu selesaikan kuota di terminal. Target pertama $600; kegagalan mereset kampanye.

## Kontrol

| Tombol | Fungsi |
|---|---|
| WASD / mouse | Gerak / melihat |
| Shift / Ctrl / Space | Sprint / jongkok atau turun dalam air / lompat, naik dalam air, atau memanjat tepian rendah |
| E | Interaksi |
| 1–4 | Pilih slot tangan |
| 5 | Pindahkan senter/radio antara tangan dan **slot sabuk** |
| F | Senter nyala/mati (tetap menyala saat ganti barang atau membawa barang dua tangan) |
| G | Jatuhkan barang (di kapal kecil = taruh di kapal) |
| Klik kiri | Gunakan flare, pasang baterai yang dipilih, atau senter yang dipegang |
| W / S pada tali | Naik menuju kapal kargo / turun menuju kapal kecil |
| Space pada tali | Lepas |
| V / R | Push-to-talk dekat / radio |
| ← / → ketika spectator atau di komputer kapal | Pilih rekan yang diamati |
| Esc | Pengaturan; timer tetap berjalan |

Barang besar harus tetap dipegang, membatasi penggunaan alat, dan membuat panjat 50% lebih lambat. Oksigen dasar 35 detik; tabung menambah 90 detik dan diisi ulang di komputer kapal ($20). Radio memerlukan alat pada inventori (radio di sabuk tetap bisa dipakai dengan tangan penuh); spectator berbicara kepada spectator lain.

**HUD:** jam tangan 24 jam (waktu di dunia game) di kiri atas, uang tim ($) dan kuota di kanan atas, kesehatan/stamina dan slot sabuk dengan indikator baterai di kiri bawah, 4 slot tangan di tengah bawah. Indikator oksigen vertikal di tepi kanan hanya muncul saat oksigen berkurang atau terisi kembali. Level tenggelam hanya ada di komputer kapal.

Senter memakai baterai (sekitar 150 detik per baterai, `flashlightBattery`); baterai dijual lewat komputer kapal ($10) dan tersebar di dalam kapal. Senter menerangi ruangan dengan sorot lebar (titik terang di tengah dan cahaya redup di sekitarnya), tanpa berkas cahaya yang digambar. Setiap ruangan punya satu pintu berengsel (lebar 1,1 m) dengan empat gaya: baja kedap air berjendela bulat, papan kayu berjendela bulat, baja berkarat, dan pintu kabin bercat dengan jendela kecil. Beberapa pintu ruangan terkunci; satu **kunci** per pintu tersebar di bagian kapal yang bisa dicapai, dan di balik pintu terkunci selalu ada barang berharga. Kunci tidak bisa dibawa pulang. Saat berenang, dorong ke arah tepian rendah (hingga sekitar 0,65 m di atas air) untuk naik; di darat, Space di depan rintangan rendah (hingga 1 m) untuk memanjat. Pagar kapal kecil (1,15 m) tetap tidak bisa dilompati.

Di menu, **MONSTER: MATI (TES)** membuat host memulai ekspedisi tanpa monster (juga `--no-monsters`). Pilihan ini disimpan.

## Tampilan, suara, dan monster

- **Filter retro (PS1 horor):** render beresolusi rendah dengan upscaling titik, dither Bayer, pita warna 5-bit, grain film, vignette, dan grading murung. Shader: `Assets/SinkScrapt/Art/RetroPost.shader`, dipasang sebagai `FullScreenPassRendererFeature` pada `PC_Renderer` oleh menu **SinkScrapt / Build Retro Look**. `RetroLook` mengatur tingkat (Mati/Ringan/Sedang/Kuat) dan memberi umpan balik luka, air, dan ketakutan ke shader. HUD digambar di atas filter sehingga tetap tajam.
- **Atmosfer:** siang dan malam (`SkyClock`): ekspedisi yang dimulai siang berakhir malam dan sebaliknya; pulau mendapat siang atau malam acak di setiap kunjungan. Matahari, bulan, langit prosedural, cahaya ambien, kabut laut kelabu (lebih tebal di malam hari), dan pantulan laut mengikuti jam bersama host. Di dalam kapal hampir gelap gulita: hanya sekitar 12% sel lorong dan seperempat ruangan yang masih punya lampu (sebagian mati), sehingga senter diperlukan. Lampu tersendat dan padam saat Penguntit Lambung berada dalam 9 m.
- **Horor:** `WorldDecor.cs` mendandani bangkai kapal: noda darah, cap tangan, bekas seretan, genangan, goresan cakar (juga di sebagian pintu), karat, noda lembap, pesan bercat di dinding ("MATIKAN SENTER", "DIA MENDENGAR", "JANGAN TURUN", …), kabel yang menjuntai, dan puing (kursi terguling, ember, sepatu bot yang ditinggalkan, kertas, papan patah, jaket pelampung). Semua decal menerima cahaya, jadi baru terlihat saat disenteri. Di geladak kapal kargo tertulis "JANGAN MASUK" dan "TOLONG".
- **Lainnya:** lampu dermaga, lampu navigasi kapal, asap cerobong, uap, percikan listrik, debu, lampu darurat yang berkedip pada dua menit terakhir (**SinkScrapt / Build Atmosphere** membuat material langit, partikel, kaca, dan decal).
- **Suara:** rekaman CC0 yang diolah (`ArtSource/Audio/build_audio.py` → `Resources/SinkScrapt/Audio`, sumber di `Docs/Assets.md`). Di luar: deburan laut, air beriak di lambung, camar pada siang hari, angin, derit kayu kapal kecil. Di dalam kapal: hampir sunyi, hanya dengung ruang, derit dan erangan lambung, tetesan air, dentang logam di kejauhan, dan sesekali pintu yang terbanting jauh; tanpa musik. Langkah kaki mengikuti bahan lantai (kayu, besi, kisi, beton/ubin, kerikil/pasir, air). Monster berteriak ketika melihat penyelam. Bunyi UI dan alarm tetap disintesis (`SoundBank`). `AudioDirector` mengelola suara 3D, lapisan suasana, gema interior, dan peredaman bawah air; lapisan ketegangan rendah hanya muncul saat monster mengejar.
- **Karakter:** empat kru (jaket parka hijau, medis, teknisi, nelayan berjas kuning) dan pedagang penyelam di pulau adalah model Blender ber-rig dengan tubuh utuh yang menyatu (tanpa sambungan patah-patah), wajah, sarung tangan, dan sepatu bot (`Models/Characters`, sumber di `ArtSource/Blender/characters.blend`, skrip `charbody.py`/`crew.py`). Setiap pemain mendapat karakter berbeda.
- **Monster:** `NetEnemy` (otak server) memakai mesin status Patroli, Selidiki, Teriak, Kejar, Siap Serang, Serang, Cengkeram, Pulih, Cari, Teralihkan flare, dan Mundur. Saat pertama melihat penyelam, monster berhenti dan berteriak (1,3 detik) lalu mengejar. Penguntit Lambung kini sosok kurus pucat setinggi 2,35 m dengan tulang rusuk menonjol, lengan panjang bercakar, dan rahang yang menganga; ia bisa memukul atau mencengkeram, mengangkat, lalu melempar penyelam. Pemburu Tenggelam berupa ikan sungut ganda (anglerfish) gembung dengan rahang penuh gigi jarum dan umpan bercahaya.
- **Ikon item:** slot tangan, slot sabuk, toko di komputer kapal, dan daftar jual pedagang menampilkan gambar item yang dirender dari modelnya (`Resources/SinkScrapt/Icons`). `EnemyRig` (klien) membuat model, animasi, cahaya, dan suara. Parameter ada di `Balance.asset`. Penguntit Lambung memburu di lorong kering; Pemburu Tenggelam muncul ketika air mencapai dek tengah. Monster hanya bergerak di sepanjang graf navigasi interior; titik awal rute dan langkah lurus (mengejar, menerjang) diperiksa dengan sapuan selebar tubuh sehingga tidak menembus dinding.
- **Tekstur dan model:** semua tekstur (pelat baja berpaku keling, pelat berkarat, pelat lantai berpola berlian, kisi, dinding dua warna, kayu, seng gelombang, batu, beton, pasir, ubin, garis bahaya, lambung) disintesis dalam kode oleh `Surfaces.cs` dengan filter titik ala PS1. Kotak memakai UV dalam meter sehingga tekstur tidak meregang. Kapal kecil, pulau, dan eksterior kapal kargo (anjungan bertingkat, derek, kontainer bertumpuk, sekoci, cerobong) dibangun di `WorldExterior.cs`.
- **Air:** shader `Water.shader` kini transparan dan membaca depth kamera: air dangkal memperlihatkan lantai dengan warna keruh, air dalam memudar gelap, dan ada garis busa di tepi dinding dan lambung. Interior memakai varian yang lebih keruh.
- **Antarmuka:** `UiKit` (tema, bingkai 9-slice, tombol, bar, slider) dan `GameHUD` (menu, HUD, dialog toko/jual/kuota/kemudi/komputer kapal, pengaturan, panduan pertama). Font monospace Consolas diambil dari sistem dengan cadangan bawaan.

## Multiplayer dan layanan online

Paket terpasang: Netcode for GameObjects 2.13.3, Multiplayer Services 2.4.0, Authentication 3.8.0, Vivox 16.12.1. Paket Pipeline diperbarui ke 0.8.0-exp.1 agar CLI 1.0.0-beta.12 dapat menjalankan perintah berargumen.

Proyek lokal memakai proyek Unity Cloud **SinkScrapt** yang sudah tersedia pada akun pemilik. Untuk koneksi internet, layanan koneksi multiplayer dan Vivox pada proyek tersebut harus tersedia/aktif. Tidak ada signing secret pada client. Hosting privat menggunakan API utama paket multiplayer Unity; simulasi di komputer host. Host keluar mengakhiri permainan.

Jaringan lokal memakai port UDP 7777. Gunakan build yang sama pada seluruh komputer. Pemain yang datang saat ekspedisi menjadi spectator hingga fase aman berikutnya.

Checkpoint disimpan di `Application.persistentDataPath/campaign.json`, beserta `.bak`. Untuk membuang kampanye, pilih **Kampanye: Baru** sebelum menjadi host. Checkpoint rusak tidak otomatis ditimpa. File QA memakai folder terpisah.

## Struktur dan balancing

- `Assets/SinkScrapt/Runtime`: gameplay, jaringan, procedural geometry, UI, audio, efek, dan voice.
- `Assets/SinkScrapt/Art`: shader air dan filter retro.
- `Assets/SinkScrapt/Resources/SinkScrapt`: empat network prefab, material, dan `Balance.asset`.
- `Assets/SinkScrapt/Scenes`: Bootstrap, SmallBoat, Island, Exterior, Interior.
- `Assets/SinkScrapt/Tests/Editor`: pengujian core termasuk 1.000 seed.
- `Tools`: skrip C# untuk menjalankan pembangunan aset dan QA melalui Unity CLI.
- `Artifacts`: checkpoint dan hasil QA terisolasi.

Interior dibangun dari blueprint milik host. Geometri statis dibuat dari blueprint yang sama; loot dan monster adalah NetworkObjects yang di-spawn host. Waktu server menggerakkan kapal, air, tali, alarm, dan ekstraksi. Interior memakai ruang terpisah pada X=1000 dengan elevasi lantai 2/5/8 dan airlock 10, sesuai elevasi kapal luar.

**Generator interior (v3, `InteriorLayout.cs`):** tiap dek adalah grid 9 x 13 sel berukuran 4 m. Per seed, generator menaruh dua tangga per pasangan dek, tiga pintu masuk (A di dek atas dengan tangga airlock, B dan C di dua dek berbeda dan sisi lambung acak, yang di dek bawah berupa ruang palka datar), sebuah lorong melingkar (dengan lorong silang opsional dan cabang), lalu 8–10 ruangan persegi (hingga 3 x 3 sel) dengan satu atau dua pintu. Dua sampai empat ruangan buntu mendapat pintu terkunci. Blueprint dikirim sebagai JSON terkompresi gzip (base64). Sel yang tidak terpakai menjadi rongga tertutup. Dinding berdiri pada setiap tepi antara modul yang berbeda kecuali bila blueprint menyatakan bukaan, jadi lambung tertutup rapat dan geometri, graf navigasi monster, serta validasi memakai aturan bukaan yang sama (`InteriorLayout.Open`). Bentuk interior tidak harus sama dengan bentuk luar kapal.

**Air got:** dek terbawah sudah tergenang air setinggi pinggang (0,9 m) sejak awal ekspedisi. Pemain berjalan lebih lambat (`wadeSpeedFactor` di `Balance.asset`) tetapi belum berenang atau kehabisan napas. Banjir dari kurva tenggelam bersama mengambil alih ketika lebih tinggi (sekitar menit 6); lihat `ShipTimeline.InteriorWater`.

Edit `Balance.asset` untuk parameter durasi, pergerakan, oksigen, dan alat. Harga serta definisi scrap berada di `ItemCatalog` dalam `GameData.cs`; titik banjir berada di `ShipTimeline`.

## Git dan GitHub

`.gitignore` mengecualikan folder yang dibuat Unity (`Library`, `Temp`, `Logs`, `UserSettings`, `obj`), build (`Builds`), hasil QA dan checkpoint (`Artifacts`), file solusi IDE, cadangan Blender (`*.blend1`), konfigurasi MCP lokal (`.mcp.json`, berisi path komputer pribadi), serta pola file rahasia (`.env`, `*.pem`, `*.key`, dll.). `.gitattributes` menandai model, `.blend`, dan gambar sebagai biner; semua file di bawah batas 100 MB GitHub, jadi Git LFS tidak diperlukan. Pengaturan Vivox di `ProjectSettings` hanya berisi ID publik (kunci token kosong). Setelah memasang Git:

```
git init
git add .
git commit -m "SinkScrapt alpha"
```

## Verifikasi dan build

Menu Editor **SinkScrapt / Build Alpha Assets** membuat aset awal melalui API Editor dan mempertahankan aset yang sudah ada. Pengujian NUnit tersedia di Unity Test Runner, assembly `SinkScrapt.Tests`.

QA runtime hanya aktif melalui `Tools/StartQa.cs` atau argumen `--qa-host`. QA menjalankan pembelian, traversal tiga lantai pada sepuluh map (setiap tangga, ketiga pintu masuk, berjalan dari lorong ke ruangan), pintu terkunci dan kunci, baterai senter, memanjat tepian dari air, tali yang melengkung, siang/malam, peta komputer kapal, pemeriksaan air got setinggi pinggang, uji dorong ke dinding agar penyelam tidak bisa keluar lambung, pickup, penyimpanan, kematian tim, ekstraksi otomatis, penjualan idempotent, dan checkpoint. Monster/bahaya dinonaktifkan khusus pada fixture traversal agar pengujian geometri tidak terganggu.

Argumen build untuk pengujian lokal: `--local-host`, `--local-client`, `--profile <nama>`, `--save-path <path>`, `--qa-host`, `--new-campaign`, `--no-monsters`, dan `--qa-capture`. `--qa-host` menunggu empat pemain, menulis `qa-result.json` di sebelah checkpoint QA, lalu keluar dengan kode 0/1. Dengan lebih dari satu pemain, QA juga bertanya kepada setiap klien apa yang tampil di layarnya (karakter rekan dan animasinya, sorot senter, cutscene, hitung mundur paket di HUD, cengkeraman monster); klien yang dijalankan dengan `--qa-capture` menyimpan tangkapan layar `client-*.png` di folder `--save-path`-nya.

Build Windows menggunakan lima scene pada Build Settings. Build alpha terbaru ada di `Builds/StandaloneWindows64/SinkScrapt.exe` (jalankan langsung untuk main solo). QA pada build: host `SinkScrapt.exe --qa-host --new-campaign --save-path <folder>\campaign.json` bersama tiga klien `SinkScrapt.exe --local-client --profile p2 -batchmode -nographics` (`--qa-players N` untuk jumlah pemain lain); hasil ada di `qa-result.json` di folder checkpoint. Lihat `Docs/Verification.md` untuk hasil yang benar-benar sudah diperiksa dan batas pengujian.
