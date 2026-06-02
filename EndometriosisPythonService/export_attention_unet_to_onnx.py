# export_attention_unet_to_onnx.py

import torch
import torch.nn as nn
from pathlib import Path

from mri_endo_inference_new import load_model


class ModelWithSigmoid(nn.Module):
    def __init__(self, model):
        super().__init__()
        self.model = model

    def forward(self, x):
        logits = self.model(x)
        return torch.sigmoid(logits)


BASE_DIR = Path(__file__).resolve().parent

model_path = BASE_DIR / "Model" / "diploma_modelsbest_model3_ovary_endo_attentionUnet.pth"
onnx_path = BASE_DIR / "Model" / "attention_unet3d.onnx"

model, device = load_model(str(model_path))
model.eval()

wrapped_model = ModelWithSigmoid(model).to(device)
wrapped_model.eval()

dummy_input = torch.randn(1, 1, 32, 128, 128, device=device)

torch.onnx.export(
    wrapped_model,
    dummy_input,
    str(onnx_path),
    input_names=["input"],
    output_names=["probabilities"],
    opset_version=17,
    do_constant_folding=True,
    dynamic_axes={
        "input": {0: "batch"},
        "probabilities": {0: "batch"}
    }
)

print("ONNX-модель сохранена:", onnx_path)