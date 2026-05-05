import base64
import json
import time
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path

import cv2
import pika


BASE_DIR = Path(__file__).resolve().parent
OUTPUT_DIR = BASE_DIR / "output" / "captures"
CONFIG_PATH = BASE_DIR / "config.json"
DEFAULT_LIVE_OUTPUT = (BASE_DIR.parent / "backend" / "App_Data" / "Uploads" / "live").resolve()

DEFAULT_CLASSES = [
    "background",
    "aeroplane",
    "bicycle",
    "bird",
    "boat",
    "bottle",
    "bus",
    "car",
    "cat",
    "chair",
    "cow",
    "diningtable",
    "dog",
    "horse",
    "motorbike",
    "person",
    "pottedplant",
    "sheep",
    "sofa",
    "train",
    "tvmonitor",
]


@dataclass
class AppConfig:
    rabbitmq_host: str
    rabbitmq_port: int
    rabbitmq_username: str
    rabbitmq_password: str
    rabbitmq_virtual_host: str
    rabbitmq_queue: str
    camera_id: str
    zone: str
    camera_index: int
    confidence_threshold: float
    detection_cooldown_seconds: int
    live_frame_output_path: str
    live_frame_interval_ms: int
    show_preview: bool
    model_prototxt: str
    model_weights: str
    labels_file: str | None


def load_config() -> AppConfig:
    if not CONFIG_PATH.exists():
        raise FileNotFoundError(
            f"Config file belum ada. Salin '{BASE_DIR / 'config.example.json'}' menjadi '{CONFIG_PATH.name}'."
        )

    raw_config = json.loads(CONFIG_PATH.read_text(encoding="utf-8-sig"))
    return AppConfig(
        rabbitmq_host=raw_config.get("rabbitmq_host", "localhost"),
        rabbitmq_port=int(raw_config.get("rabbitmq_port", 5672)),
        rabbitmq_username=raw_config.get("rabbitmq_username", "admin"),
        rabbitmq_password=raw_config.get("rabbitmq_password", "123456"),
        rabbitmq_virtual_host=raw_config.get("rabbitmq_virtual_host", "/"),
        rabbitmq_queue=raw_config.get("rabbitmq_queue", "odgudang.detections"),
        camera_id=raw_config.get("camera_id", "CAM_OBJECT_01"),
        zone=raw_config.get("zone", "webcam"),
        camera_index=int(raw_config.get("camera_index", 0)),
        confidence_threshold=float(raw_config.get("confidence_threshold", 0.55)),
        detection_cooldown_seconds=max(int(raw_config.get("detection_cooldown_seconds", 10)), 10),
        live_frame_output_path=raw_config.get("live_frame_output_path", str(DEFAULT_LIVE_OUTPUT)),
        live_frame_interval_ms=max(int(raw_config.get("live_frame_interval_ms", 1000)), 250),
        show_preview=bool(raw_config.get("show_preview", True)),
        model_prototxt=raw_config["model_prototxt"],
        model_weights=raw_config["model_weights"],
        labels_file=raw_config.get("labels_file"),
    )


def load_labels(labels_file: str | None) -> list[str]:
    if not labels_file:
        return DEFAULT_CLASSES

    path = (BASE_DIR / labels_file).resolve()
    if not path.exists():
        raise FileNotFoundError(f"Labels file tidak ditemukan: {path}")

    return [line.strip() for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]


def save_frame(frame) -> Path:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    file_path = OUTPUT_DIR / f"capture_{datetime.now().strftime('%Y%m%d_%H%M%S_%f')}.jpg"
    cv2.imwrite(str(file_path), frame)
    return file_path


def save_live_frame(config: AppConfig, frame) -> Path:
    live_dir = Path(config.live_frame_output_path).resolve()
    live_dir.mkdir(parents=True, exist_ok=True)
    file_path = live_dir / f"{config.camera_id.lower()}_latest.jpg"
    cv2.imwrite(str(file_path), frame)
    return file_path


def create_rabbitmq_connection(config: AppConfig) -> pika.BlockingConnection:
    credentials = pika.PlainCredentials(config.rabbitmq_username, config.rabbitmq_password)
    parameters = pika.ConnectionParameters(
        host=config.rabbitmq_host,
        port=config.rabbitmq_port,
        virtual_host=config.rabbitmq_virtual_host,
        credentials=credentials,
    )
    return pika.BlockingConnection(parameters)


def publish_detection_event(config: AppConfig, image_path: Path, object_name: str, confidence: float) -> None:
    with image_path.open("rb") as image_file:
        payload = {
            "eventId": datetime.now().strftime("%Y%m%d%H%M%S%f"),
            "timestampUtc": datetime.now(timezone.utc).isoformat(),
            "objectName": object_name,
            "cameraId": config.camera_id,
            "confidence": round(confidence, 4),
            "zone": config.zone,
            "imageFileName": image_path.name,
            "imageBase64": base64.b64encode(image_file.read()).decode("utf-8"),
            "contentType": "image/jpeg",
        }

    connection = create_rabbitmq_connection(config)
    try:
        channel = connection.channel()
        channel.queue_declare(queue=config.rabbitmq_queue, durable=True)
        channel.basic_publish(
            exchange="",
            routing_key=config.rabbitmq_queue,
            body=json.dumps(payload).encode("utf-8"),
            properties=pika.BasicProperties(delivery_mode=2),
        )
    finally:
        connection.close()

    print(
        f"[OK] Object '{object_name}' dipublish ke queue '{config.rabbitmq_queue}' "
        f"dengan capture '{image_path.name}'."
    )


def load_detection_model(config: AppConfig):
    prototxt_path = (BASE_DIR / config.model_prototxt).resolve()
    weights_path = (BASE_DIR / config.model_weights).resolve()

    if not prototxt_path.exists():
        raise FileNotFoundError(f"Model prototxt tidak ditemukan: {prototxt_path}")
    if not weights_path.exists():
        raise FileNotFoundError(f"Model weights tidak ditemukan: {weights_path}")

    return cv2.dnn.readNetFromCaffe(str(prototxt_path), str(weights_path))


def main() -> int:
    config = load_config()
    labels = load_labels(config.labels_file)
    net = load_detection_model(config)

    camera = cv2.VideoCapture(config.camera_index)
    if not camera.isOpened():
        print("Kamera tidak dapat dibuka.")
        return 1

    last_capture_time = 0.0
    last_live_frame_write = 0.0
    print("Object detection berjalan. Tekan tombol Q untuk keluar.")

    try:
        while True:
            ok, frame = camera.read()
            if not ok:
                print("Gagal membaca frame dari kamera.")
                time.sleep(1)
                continue

            frame_height, frame_width = frame.shape[:2]
            blob = cv2.dnn.blobFromImage(
                cv2.resize(frame, (300, 300)),
                scalefactor=0.007843,
                size=(300, 300),
                mean=127.5,
            )

            net.setInput(blob)
            detections = net.forward()

            detected_objects: dict[str, float] = {}

            for index in range(detections.shape[2]):
                confidence = float(detections[0, 0, index, 2])
                if confidence < config.confidence_threshold:
                    continue

                class_index = int(detections[0, 0, index, 1])
                if class_index >= len(labels):
                    continue

                object_name = labels[class_index]
                box = detections[0, 0, index, 3:7] * [frame_width, frame_height, frame_width, frame_height]
                start_x, start_y, end_x, end_y = box.astype("int")

                cv2.rectangle(frame, (start_x, start_y), (end_x, end_y), (0, 255, 0), 2)
                cv2.putText(
                    frame,
                    f"{object_name}: {confidence:.2f}",
                    (start_x, max(start_y - 10, 10)),
                    cv2.FONT_HERSHEY_SIMPLEX,
                    0.5,
                    (0, 255, 0),
                    2,
                )

                previous_confidence = detected_objects.get(object_name, 0.0)
                if confidence > previous_confidence:
                    detected_objects[object_name] = confidence

            current_time = time.time()
            if current_time - last_live_frame_write >= (config.live_frame_interval_ms / 1000):
                save_live_frame(config, frame)
                last_live_frame_write = current_time

            if detected_objects and current_time - last_capture_time >= config.detection_cooldown_seconds:
                image_path = save_frame(frame)
                print(f"[INFO] Object terdeteksi. Capture disimpan: {image_path}")

                for object_name, confidence in detected_objects.items():
                    try:
                        publish_detection_event(config, image_path, object_name, confidence)
                    except Exception as exc:
                        print(f"[ERROR] Gagal publish object '{object_name}': {exc}")

                last_capture_time = current_time

            if config.show_preview:
                cv2.imshow("OD Gudang Object Detection", frame)
                if cv2.waitKey(1) & 0xFF == ord("q"):
                    break
            else:
                time.sleep(0.05)

    finally:
        camera.release()
        cv2.destroyAllWindows()

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
