# SSH File Preview

Aplikasi desktop Windows native untuk membuka file kode/teks, Markdown, dan gambar dari beberapa host SSH. Profil host dapat disimpan dan diganti dari selector di bagian atas; pada pemakaian pertama aplikasi meminta alias SSH atau format `user@host` sebelum memuat daftar file.

## Prasyarat

- Windows 10/11 dengan **OpenSSH Client** (`ssh.exe`) tersedia di `PATH`.
- Alias SSH yang sudah terkonfigurasi atau target SSH `user@host`; autentikasi harus bisa berjalan dalam batch mode (misalnya melalui SSH key).
- .NET 10 SDK untuk build atau menjalankan source.

Contoh konfigurasi SSH (sesuaikan host dan user dengan konfigurasi Anda):

```sshconfig
Host my-server
    HostName example.com
    User your-user
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

## Installer wizard Windows

Build installer dari Windows setelah memasang Inno Setup 6:

```powershell
winget install --id JRSoftware.InnoSetup --exact
.\build-installer.ps1
```

File setup ada di `installer-output\SSH-File-Preview-Setup-x64.exe`. Wizard menyediakan pilihan folder pemasangan dan shortcut Start Menu, serta opsi shortcut Desktop; aplikasi bisa dihapus melalui **Settings → Apps → Installed apps**. Installer membawa .NET 10 runtime secara self-contained, jadi komputer tujuan tidak perlu memasang .NET Desktop Runtime. Windows OpenSSH Client (`ssh.exe`) tetap diperlukan untuk koneksi SSH.

Saat pertama dibuka tanpa target tersimpan, dialog meminta alias SSH (contoh `my-server`) atau `user@host`. Profil yang berhasil disimpan untuk akun Windows saat ini; gunakan selector untuk berpindah host dan **+ Host** untuk menambahkan profil. Pergantian host kembali ke folder awalnya dan mengosongkan riwayat Back. Tombol **Back** kembali ke folder yang terakhir ditinggalkan, sedangkan **Up** menuju parent directory. Klik kanan file/folder untuk menyalin path remote; menu file juga menyediakan **Download** ke lokasi yang dipilih.

Theme selector mengubah seluruh aplikasi antara Light dan Dark; pilihan tema disimpan antar sesi. Preview mencakup file kode/teks umum, Markdown, dan gambar PNG, JPEG, GIF, BMP, TIFF. Markdown dirender dengan tema yang dipilih; gambar relatif raster dimuat dari direktori dokumen, sedangkan URL eksternal dan path di luar direktori tidak dimuat. Gambar memiliki zoom 50%, 75%, 100%, 125%, 150%, 200%, 300%, dan 400%. Preview file dibatasi 12 MiB; download file dialirkan langsung ke disk dan dibatasi timeout SSH 25 detik. Aset Markdown dibatasi 12 MiB per aset, 16 aset, dan 24 MiB total.

Koneksi memakai OpenSSH batch-mode. Daftar profil disimpan di `%APPDATA%\SshFilePreview\ssh-targets.txt`, target aktif di `%APPDATA%\SshFilePreview\ssh-target.txt`, dan tema di `%APPDATA%\SshFilePreview\theme.txt`. Target diteruskan sebagai argumen SSH terpisah, bukan bagian perintah remote. Perintah remote hanya membaca direktori/file yang diminta, path shell di-quote, dan segmen `..` ditolak.
