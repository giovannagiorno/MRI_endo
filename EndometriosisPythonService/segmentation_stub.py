from pathlib import Path

import nibabel as nib
import numpy as np
from PIL import Image, ImageDraw


def load_middle_slice(mri_file_path: str) -> np.ndarray:
    nii = nib.load(mri_file_path)
    data = nii.get_fdata()

    if data.ndim < 3:
        raise ValueError("MRI file does not contain 3D data")

    # Берём средний срез по третьей оси
    slice_index = data.shape[2] // 2
    image_slice = data[:, :, slice_index]

    return image_slice


def normalize_slice(image_slice: np.ndarray) -> np.ndarray:
    image_slice = np.nan_to_num(image_slice)

    min_val = np.min(image_slice)
    max_val = np.max(image_slice)

    if max_val - min_val == 0:
        return np.zeros_like(image_slice, dtype=np.uint8)

    normalized = (image_slice - min_val) / (max_val - min_val)
    normalized = (normalized * 255).astype(np.uint8)

    return normalized


def prepare_display_image(image_slice: np.ndarray, target_size: tuple[int, int] = (512, 512)) -> Image.Image:
    normalized = normalize_slice(image_slice)

    image = Image.fromarray(normalized).convert("L")
    image = image.resize(target_size, Image.Resampling.LANCZOS)
    image = image.convert("RGB")

    return image


def create_mri_preview_from_nifti(mri_file_path: str, output_path: str) -> str:
    output = Path(output_path)
    output.parent.mkdir(parents=True, exist_ok=True)

    image_slice = load_middle_slice(mri_file_path)
    image = prepare_display_image(image_slice)

    image.save(output, format="PNG")
    return str(output)


def create_segmentation_result_from_nifti(mri_file_path: str, output_path: str) -> str:
    output = Path(output_path)
    output.parent.mkdir(parents=True, exist_ok=True)

    image_slice = load_middle_slice(mri_file_path)
    image = prepare_display_image(image_slice)

    draw = ImageDraw.Draw(image)

    width, height = image.size

    # Демонстрационный контур "сегментации"
    left = int(width * 0.42)
    top = int(height * 0.38)
    right = int(width * 0.70)
    bottom = int(height * 0.58)

    draw.ellipse((left, top, right, bottom), outline="red", width=4)

    image.save(output, format="PNG")
    return str(output)