import argparse
from pathlib import Path

import numpy as np
import nibabel as nib
import torch
import torch.nn as nn
import torch.nn.functional as F

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt


# ============================================================
# Attention U-Net 3D: новая модель с 2 выходными каналами
# channel 0 — яичники
# channel 1 — эндометриома
# ============================================================

class DoubleConv3D(nn.Module):
    def __init__(self, in_channels, out_channels):
        super().__init__()
        self.conv = nn.Sequential(
            nn.Conv3d(in_channels, out_channels, kernel_size=3, padding=1),
            nn.BatchNorm3d(out_channels),
            nn.ReLU(inplace=True),
            nn.Conv3d(out_channels, out_channels, kernel_size=3, padding=1),
            nn.BatchNorm3d(out_channels),
            nn.ReLU(inplace=True),
        )

    def forward(self, x):
        return self.conv(x)


class AttentionGate3D(nn.Module):
    def __init__(self, F_g, F_l, F_int):
        super().__init__()
        self.W_g = nn.Sequential(
            nn.Conv3d(F_g, F_int, kernel_size=1, stride=1, padding=0, bias=True),
            nn.BatchNorm3d(F_int),
        )
        self.W_x = nn.Sequential(
            nn.Conv3d(F_l, F_int, kernel_size=1, stride=1, padding=0, bias=True),
            nn.BatchNorm3d(F_int),
        )
        self.psi = nn.Sequential(
            nn.Conv3d(F_int, 1, kernel_size=1, stride=1, padding=0, bias=True),
            nn.BatchNorm3d(1),
            nn.Sigmoid(),
        )
        self.relu = nn.ReLU(inplace=True)

    def forward(self, g, x):
        if g.shape[2:] != x.shape[2:]:
            g = F.interpolate(g, size=x.shape[2:], mode="trilinear", align_corners=False)

        g1 = self.W_g(g)
        x1 = self.W_x(x)
        attention = self.relu(g1 + x1)
        attention = self.psi(attention)
        return x * attention


class AttentionUNet3D(nn.Module):
    def __init__(self, in_channels=1, out_channels=2, features=(8, 16, 32, 64)):
        super().__init__()
        self.downs = nn.ModuleList()
        self.upconvs = nn.ModuleList()
        self.attention_gates = nn.ModuleList()
        self.decoder_convs = nn.ModuleList()
        self.pool = nn.MaxPool3d(kernel_size=2, stride=2)

        current_channels = in_channels
        for feature in features:
            self.downs.append(DoubleConv3D(current_channels, feature))
            current_channels = feature

        self.bottleneck = DoubleConv3D(features[-1], features[-1] * 2)

        for feature in reversed(features):
            self.upconvs.append(
                nn.ConvTranspose3d(feature * 2, feature, kernel_size=2, stride=2)
            )
            self.attention_gates.append(
                AttentionGate3D(F_g=feature, F_l=feature, F_int=feature // 2)
            )
            self.decoder_convs.append(DoubleConv3D(feature * 2, feature))

        self.final_conv = nn.Conv3d(features[0], out_channels, kernel_size=1)

    def forward(self, x):
        skip_connections = []

        for down in self.downs:
            x = down(x)
            skip_connections.append(x)
            x = self.pool(x)

        x = self.bottleneck(x)
        skip_connections = skip_connections[::-1]

        for idx in range(len(self.upconvs)):
            x = self.upconvs[idx](x)
            skip_connection = skip_connections[idx]

            if x.shape[2:] != skip_connection.shape[2:]:
                x = F.interpolate(
                    x,
                    size=skip_connection.shape[2:],
                    mode="trilinear",
                    align_corners=False,
                )

            skip_connection = self.attention_gates[idx](g=x, x=skip_connection)
            x = torch.cat((skip_connection, x), dim=1)
            x = self.decoder_convs[idx](x)

        return self.final_conv(x)


# ============================================================
# Загрузка модели и предобработка
# ============================================================

def load_model(model_path):
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    model = AttentionUNet3D(in_channels=1, out_channels=2, features=(8, 16, 32, 64)).to(device)

    checkpoint = torch.load(model_path, map_location=device)
    if isinstance(checkpoint, dict) and "model_state_dict" in checkpoint:
        state_dict = checkpoint["model_state_dict"]
    elif isinstance(checkpoint, dict) and "state_dict" in checkpoint:
        state_dict = checkpoint["state_dict"]
    else:
        state_dict = checkpoint

    model.load_state_dict(state_dict)
    model.eval()
    return model, device


def normalize_mri(volume):
    volume = volume.astype(np.float32)
    min_val = float(volume.min())
    max_val = float(volume.max())
    return (volume - min_val) / (max_val - min_val + 1e-8)


def make_starts(size, patch, stride):
    if size <= patch:
        return [0]
    starts = list(range(0, size - patch + 1, stride))
    if starts[-1] != size - patch:
        starts.append(size - patch)
    return starts


# ============================================================
# Sliding window prediction
# ============================================================

def predict_volume_sliding_window(
    model,
    volume_dhw,
    device,
    patch_size=(32, 128, 128),
    stride=(16, 64, 64),
    progress_callback=None,
):
    d, h, w = volume_dhw.shape
    pd, ph, pw = patch_size
    sd, sh, sw = stride

    ovary_sum = np.zeros((d, h, w), dtype=np.float32)
    endo_sum = np.zeros((d, h, w), dtype=np.float32)
    count = np.zeros((d, h, w), dtype=np.float32)

    z_starts = make_starts(d, pd, sd)
    y_starts = make_starts(h, ph, sh)
    x_starts = make_starts(w, pw, sw)

    total_patches = len(z_starts) * len(y_starts) * len(x_starts)
    processed_patches = 0

    with torch.no_grad():
        for z in z_starts:
            for y in y_starts:
                for x in x_starts:
                    z2 = min(z + pd, d)
                    y2 = min(y + ph, h)
                    x2 = min(x + pw, w)

                    patch = volume_dhw[z:z2, y:y2, x:x2]
                    padded_patch = np.zeros(patch_size, dtype=np.float32)
                    padded_patch[: patch.shape[0], : patch.shape[1], : patch.shape[2]] = patch

                    input_tensor = torch.from_numpy(padded_patch).unsqueeze(0).unsqueeze(0)
                    input_tensor = input_tensor.float().to(device)

                    logits = model(input_tensor)
                    probs = torch.sigmoid(logits)[0].cpu().numpy()  # [2, D, H, W]

                    ovary_sum[z:z2, y:y2, x:x2] += probs[0, : patch.shape[0], : patch.shape[1], : patch.shape[2]]
                    endo_sum[z:z2, y:y2, x:x2] += probs[1, : patch.shape[0], : patch.shape[1], : patch.shape[2]]
                    count[z:z2, y:y2, x:x2] += 1

                    processed_patches += 1
                    if progress_callback is not None:
                        percent = int(processed_patches / total_patches * 90)
                        progress_callback(percent)

    ovary_prob = ovary_sum / np.maximum(count, 1e-8)
    endo_prob = endo_sum / np.maximum(count, 1e-8)
    return ovary_prob, endo_prob


# ============================================================
# Визуализация и сохранение
# ============================================================

def get_best_slice(mask_dhw):
    sums = mask_dhw.sum(axis=(1, 2))
    if sums.max() == 0:
        return mask_dhw.shape[0] // 2
    return int(np.argmax(sums))


def save_overlay_png(image_dhw, endo_mask_dhw, output_png_path, result_text, slice_idx=None):
    if slice_idx is None:
        slice_idx = get_best_slice(endo_mask_dhw)

    plt.figure(figsize=(7, 7))
    plt.imshow(image_dhw[slice_idx], cmap="gray")

    masked = np.ma.masked_where(endo_mask_dhw[slice_idx] == 0, endo_mask_dhw[slice_idx])
    plt.imshow(masked, cmap="Reds", alpha=0.35)

    plt.title(f"{result_text}\nСрез: {slice_idx}")
    plt.axis("off")
    plt.tight_layout()
    plt.savefig(output_png_path, dpi=150, bbox_inches="tight", pad_inches=0)
    plt.close()

def save_original_png(image_dhw, slice_idx, output_png_path):
    plt.figure(figsize=(7, 7))
    plt.imshow(image_dhw[slice_idx], cmap="gray")
    plt.title(f"Оригинальный МРТ-срез\nСрез: {slice_idx}")
    plt.axis("off")
    plt.tight_layout()
    plt.savefig(output_png_path, dpi=150, bbox_inches="tight", pad_inches=0)
    plt.close()

def save_probability_png(image_dhw, prob_dhw, output_png_path):
    slice_idx = get_best_slice(prob_dhw > 0.5)
    plt.figure(figsize=(7, 7))
    plt.imshow(image_dhw[slice_idx], cmap="gray")
    plt.imshow(prob_dhw[slice_idx], cmap="Reds", alpha=0.45, vmin=0, vmax=1)
    plt.title(f"Вероятность эндометриомы, срез: {slice_idx}")
    plt.axis("off")
    plt.tight_layout()
    plt.savefig(output_png_path, dpi=150, bbox_inches="tight", pad_inches=0)
    plt.close()

def save_original_png(image_dhw, slice_idx, output_png_path):
    plt.figure(figsize=(7, 7))
    plt.imshow(image_dhw[slice_idx], cmap="gray")
    plt.title(f"Оригинальный МРТ-срез\nСрез: {slice_idx}")
    plt.axis("off")
    plt.tight_layout()
    plt.savefig(output_png_path, dpi=150, bbox_inches="tight", pad_inches=0)
    plt.close()

def save_ovary_overlay_png(image_dhw, ovary_mask_dhw, output_png_path, slice_idx=None):
    if slice_idx is None:
        slice_idx = get_best_slice(ovary_mask_dhw)

    image_slice = image_dhw[slice_idx]
    mask_slice = ovary_mask_dhw[slice_idx]

    plt.figure(figsize=(7, 7))
    plt.imshow(image_slice, cmap="gray")

    masked = np.ma.masked_where(mask_slice == 0, mask_slice)
    plt.imshow(masked, cmap="Blues", alpha=0.35)

    plt.title(f"Сегментация яичников\nСрез: {slice_idx}")
    plt.axis("off")
    plt.tight_layout()
    plt.savefig(output_png_path, dpi=150, bbox_inches="tight", pad_inches=0)
    plt.close()

# ============================================================
# Главная функция
# ============================================================

def segment_mri_file(
    input_mri_path,
    model_path,
    output_endo_mask_path,
    output_png_path,
    output_ovary_mask_path=None,
    output_prob_png_path=None,
    output_ovary_png_path=None,
    output_original_png_path=None,
    threshold=0.5,
    min_voxels=20,
    progress_callback=None,
):
    model, device = load_model(model_path)

    nii = nib.load(input_mri_path)
    volume_hwd = nii.get_fdata()
    volume_hwd = normalize_mri(volume_hwd)

    # Как в старом коде: H, W, D -> D, H, W
    volume_dhw = np.transpose(volume_hwd, (2, 0, 1))

    ovary_prob_dhw, endo_prob_dhw = predict_volume_sliding_window(
        model=model,
        volume_dhw=volume_dhw,
        device=device,
        patch_size=(32, 128, 128),
        stride=(16, 64, 64),
        progress_callback=progress_callback,
    )

    ovary_mask_dhw = (ovary_prob_dhw > threshold).astype(np.uint8)
    endo_mask_dhw = (endo_prob_dhw > threshold).astype(np.uint8)

    endo_voxels = int(endo_mask_dhw.sum())
    detected = endo_voxels >= min_voxels
    result_text = "эндометриома обнаружена" if detected else "эндометриома не обнаружена"

    # D, H, W -> H, W, D для сохранения NIfTI
    endo_mask_hwd = np.transpose(endo_mask_dhw, (1, 2, 0))
    endo_nii = nib.Nifti1Image(endo_mask_hwd, affine=nii.affine, header=nii.header)
    nib.save(endo_nii, output_endo_mask_path)

    if output_ovary_mask_path is not None:
        ovary_mask_hwd = np.transpose(ovary_mask_dhw, (1, 2, 0))
        ovary_nii = nib.Nifti1Image(ovary_mask_hwd, affine=nii.affine, header=nii.header)
        nib.save(ovary_nii, output_ovary_mask_path)

    best_slice_idx = get_best_slice(endo_mask_dhw)

    if output_original_png_path is not None:
        save_original_png(volume_dhw, best_slice_idx, output_original_png_path)

    save_overlay_png(
        volume_dhw,
        endo_mask_dhw,
        output_png_path,
        result_text,
        slice_idx=best_slice_idx
    )

    if output_prob_png_path is not None:
        save_probability_png(volume_dhw, endo_prob_dhw, output_prob_png_path)

    if output_ovary_png_path is not None:
        save_ovary_overlay_png(
            volume_dhw,
            ovary_mask_dhw,
            output_ovary_png_path,
            slice_idx=best_slice_idx
        )

    return {
        "result_text": result_text,
        "detected": detected,
        "endo_voxels": endo_voxels,
        "output_endo_mask_path": str(output_endo_mask_path),
        "output_png_path": str(output_png_path),
        "output_ovary_mask_path": str(output_ovary_mask_path) if output_ovary_mask_path else None,
        "output_prob_png_path": str(output_prob_png_path) if output_prob_png_path else None,
        "output_ovary_png_path": str(output_ovary_png_path) if output_ovary_png_path else None,
        "output_original_png_path": str(output_original_png_path) if output_original_png_path else None,
        "slice_idx": best_slice_idx,
    }


def main():
    parser = argparse.ArgumentParser(description="Сегментация эндометриомы на MRI через AttentionUNet3D")
    parser.add_argument("--input", required=True, help="Путь к MRI .nii или .nii.gz")
    parser.add_argument("--model", required=True, help="Путь к best_model .pth")
    parser.add_argument("--output-dir", default="results", help="Папка для результатов")
    parser.add_argument("--threshold", type=float, default=0.5, help="Порог вероятности для маски")
    parser.add_argument("--min-voxels", type=int, default=20, help="Минимум вокселей для вывода 'обнаружена'")
    args = parser.parse_args()

    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    input_path = Path(args.input)
    stem = input_path.name.replace(".nii.gz", "").replace(".nii", "")

    result = segment_mri_file(
        input_mri_path=str(input_path),
        model_path=str(args.model),
        output_endo_mask_path=str(output_dir / f"{stem}_endometrioma_mask.nii.gz"),
        output_ovary_mask_path=str(output_dir / f"{stem}_ovary_mask.nii.gz"),
        output_png_path=str(output_dir / f"{stem}_endometrioma_overlay.png"),
        output_prob_png_path=str(output_dir / f"{stem}_endometrioma_probability.png"),
        threshold=args.threshold,
        min_voxels=args.min_voxels,
    )

    print("Готово")
    print("Результат:", result["result_text"])
    print("Вокселей эндометриомы:", result["endo_voxels"])
    print("Маска эндометриомы:", result["output_endo_mask_path"])
    print("PNG результат:", result["output_png_path"])
    print("Маска яичников:", result["output_ovary_mask_path"])


if __name__ == "__main__":
    main()