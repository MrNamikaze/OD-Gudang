# Python Object Detection Publisher

Script ini menjalankan object detection dari webcam dengan OpenCV DNN.
Saat object terdeteksi:

- frame dicapture
- file capture disimpan ke `python/output/captures`
- event dipublish ke RabbitMQ
- backend ASP.NET akan consume event lalu menyimpan metadata + gambar ke database/infrastruktur website

## Data yang dikirim

Setiap event yang dipublish berisi:

- `nama_object`
- `waktu_terdeteksi`
- `nama_file_capture`
- `gambar_capture` (dalam format base64)

## Persiapan

1. Salin `config.example.json` menjadi `config.json`
2. Letakkan file model di folder `python/models`
   - `MobileNetSSD_deploy.prototxt`
   - `MobileNetSSD_deploy.caffemodel`
3. Install dependency

```powershell
cd python
python -m pip install -r requirements.txt
```

## Jalankan

```powershell
cd python
python object_detection_publisher.py
```

Tekan `Q` untuk keluar dari preview.

## Alur sistem

```text
Python object detection -> RabbitMQ -> ASP.NET backend -> PostgreSQL -> Website
```
