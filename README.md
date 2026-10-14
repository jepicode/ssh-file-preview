# SSH File Preview

Aplikasi desktop Windows native untuk membuka file kode/teks, Markdown, dan gambar melalui SSH. Pada pemakaian pertama, aplikasi meminta target SSH—alias dari SSH config atau format `user@host`—sebelum memuat daftar file; target disimpan untuk pemakaian berikutnya.

## Prasyarat

- Windows 10/11 dengan **OpenSSH Client** (`ssh.exe`) tersedia di `PATH`.
- Alias SSH yang sudah terkonfigurasi atau target SSH `user@host`; autentikasi harus bisa berjalan dalam batch mode (misalnya melalui SSH key).
- .NET 10 SDK untuk build atau menjalankan source.

Contoh konfigurasi SSH (sesuaikan host dan user dengan konfigurasi Anda):

```sshconfig
Host dansaihomelab
    HostName nama-host-atau-ip
    User nama-user
    IdentityFile ~/.ssh/id_ed25519
```

## Build dan menjalankan

Prasyarat: Windows 10/11, .NET 10 SDK, dan Windows OpenSSH Client (`ssh.exe`) tersedia di `PATH`.

Dari folder `ssh-file-preview-desktop`, build aplikasi Release:

```powershell
dotnet build .\SshFilePreview.csproj --configuration Release
```

Jalankan dari source:

```powershell
dotnet run --project .\SshFilePreview.csproj
```

Untuk membuat aplikasi Windows x64 yang bisa dibuka langsung (memerlukan .NET 10 Desktop Runtime di komputer tujuan):

```powershell
dotnet publish .\SshFilePreview.csproj --configuration Release --runtime win-x64 --self-contained false --output .\publish
```

Executable ada di `publish\SshFilePreview.exe`. Alternatifnya, `.\publish.ps1` menjalankan perintah publish yang sama dan memasang shortcut **SSH File Preview** di Start Menu akun Windows saat ini.

Pada pemakaian pertama, masukkan alias SSH (contoh `dansaihomelab`) atau `user@host`; aplikasi menyimpan target yang berhasil untuk akun Windows saat ini dan menggunakannya otomatis pada startup berikutnya. Alias bawaan `dansaihomelab` mulai di `/home/tl-1-dsc-consumer`; target lain mulai dari home directory remote. Pakai tombol ↑ untuk folder induk, klik folder untuk masuk, dan klik file untuk preview. Path absolut bisa dimasukkan langsung di kolom atas.

Preview mencakup file kode/teks umum, Markdown, dan gambar PNG, JPEG, GIF, BMP, TIFF. Markdown dirender dengan tema terang; gambar relatif raster dimuat dari direktori dokumen, sedangkan URL eksternal dan path di luar direktori tidak dimuat. File utama dibatasi 12 MiB; aset Markdown dibatasi 12 MiB per aset, 16 aset, dan 24 MiB total. Kode bisa discroll horizontal maupun vertikal; gambar tampil di panel zoom-fit. File dan aset hanya memakai direktori sementara lokal selama render, lalu aset dihapus.

Koneksi memakai OpenSSH batch-mode. Target SSH yang dimasukkan disimpan di `%APPDATA%\SshFilePreview\ssh-target.txt`; target diteruskan sebagai argumen SSH terpisah, bukan bagian perintah remote. Perintah remote hanya membaca direktori/file yang diminta, path shell di-quote, dan segmen `..` ditolak.
