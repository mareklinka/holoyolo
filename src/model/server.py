from flask import Flask, jsonify, request
from yolo import YOLO
from PIL import Image
import base64
import io

app = Flask(__name__)

yolo = None

def __initialize_model():
    global yolo
    yolo = YOLO()

__initialize_model()

@app.route("/predict", methods=['POST'])
def predict():
    payload = request.get_json()

    data = base64.b64decode(payload['data'])
    image = Image.open(io.BytesIO(data))

    out_boxes, out_scores, out_classes = yolo.detect_boxes(image)

    out_boxes = out_boxes.astype('float64', copy=False)
    out_scores = out_scores.astype('float64', copy=False)
    out_classes = out_classes.astype('float64', copy=False)

    result_boxes = [{ "Y1": out_boxes[box, 0], "X1": out_boxes[box, 1], "Y2": out_boxes[box, 2], "X2": out_boxes[box, 3] } for box in range(out_boxes.shape[0])]
    result_scores = out_scores.tolist()
    result_classes = out_classes.tolist()
    return jsonify(boxes=result_boxes, scores=result_scores, classes=result_classes)

if __name__ == "__main__":
    app.run()