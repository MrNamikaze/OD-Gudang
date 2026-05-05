import base64
import json
import sys
import uuid
from datetime import datetime, timezone

import pika


PNG_BASE64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aJkQAAAAASUVORK5CYII="


def main() -> int:
    credentials = pika.PlainCredentials("admin", "123456")
    parameters = pika.ConnectionParameters(
        host="localhost",
        port=5672,
        virtual_host="/",
        credentials=credentials,
    )

    connection = pika.BlockingConnection(parameters)
    channel = connection.channel()
    channel.queue_declare(queue="odgudang.detections", durable=True)

    payload = {
        "eventId": str(uuid.uuid4()),
        "timestampUtc": datetime.now(timezone.utc).isoformat(),
        "objectName": "python_test_object",
        "cameraId": "CAM_PY_TEST_01",
        "confidence": 0.88,
        "zone": "python-test",
        "imageFileName": "python-test.png",
        "imageBase64": PNG_BASE64,
        "contentType": "image/png",
    }

    channel.basic_publish(
        exchange="",
        routing_key="odgudang.detections",
        body=json.dumps(payload).encode("utf-8"),
        properties=pika.BasicProperties(delivery_mode=2),
    )

    connection.close()
    print(json.dumps(payload))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
