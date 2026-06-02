from pathlib import Path
from uuid import uuid4
from threading import Lock

from fastapi import FastAPI, HTTPException, BackgroundTasks
from pydantic import BaseModel

from segmentation_stub import create_mri_preview_from_nifti
from mri_endo_inference_new import segment_mri_file

import shutil

print("ЗАПУЩЕН APP.PY С ПРОГРЕССОМ")
print("Файл app.py:", __file__)

app = FastAPI(title="Endometriosis Python Service")

BASE_DIR = Path(__file__).resolve().parent
TEMP_DIR = BASE_DIR / "temp"
PREVIEWS_DIR = TEMP_DIR / "previews"
RESULTS_DIR = TEMP_DIR / "results"
MODEL_PATH = BASE_DIR / "Model" / "diploma_modelsbest_model3_ovary_endo_attentionUnet.pth"

PREVIEWS_DIR.mkdir(parents=True, exist_ok=True)
RESULTS_DIR.mkdir(parents=True, exist_ok=True)

tasks = {}
tasks_lock = Lock()


class SegmentationRequest(BaseModel):
    mri_file_path: str


class StartSegmentationResponse(BaseModel):
    task_id: str


class SegmentationStatusResponse(BaseModel):
    task_id: str
    status: str
    progress: int
    success: bool | None = None
    preview_image_path: str | None = None
    result_image_path: str | None = None
    conclusion: str | None = None
    error: str | None = None


@app.get("/health")
def health_check():
    return {"status": "ok"}


@app.post("/segment/start", response_model=StartSegmentationResponse)
def start_segmentation(request: SegmentationRequest, background_tasks: BackgroundTasks):
    mri_path = Path(request.mri_file_path)

    if not mri_path.exists():
        raise HTTPException(status_code=404, detail="MRI file not found")

    if not MODEL_PATH.exists():
        raise HTTPException(status_code=500, detail=f"Model not found: {MODEL_PATH}")

    task_id = str(uuid4())

    with tasks_lock:
        tasks[task_id] = {
            "status": "running",
            "progress": 0,
            "success": None,
            "preview_image_path": None,
            "result_image_path": None,
            "conclusion": None,
            "error": None,
            "run_dir": None,
        }

    background_tasks.add_task(run_segmentation_task, task_id, str(mri_path))

    return StartSegmentationResponse(task_id=task_id)


@app.get("/segment/status/{task_id}", response_model=SegmentationStatusResponse)
def get_segmentation_status(task_id: str):
    with tasks_lock:
        task = tasks.get(task_id)

    if task is None:
        raise HTTPException(status_code=404, detail="Task not found")

    return SegmentationStatusResponse(
        task_id=task_id,
        status=task["status"],
        progress=task["progress"],
        success=task["success"],
        preview_image_path=task["preview_image_path"],
        result_image_path=task["result_image_path"],
        conclusion=task["conclusion"],
        error=task["error"],
    )


def update_task(task_id: str, **kwargs):
    with tasks_lock:
        if task_id in tasks:
            tasks[task_id].update(kwargs)


def run_segmentation_task(task_id: str, mri_file_path: str):
    try:
        run_id = uuid4()
        
        run_dir = RESULTS_DIR / str(run_id)
        run_dir.mkdir(parents=True, exist_ok=True)

        preview_path = run_dir / "original_preview.png"
        original_png_path = run_dir / "original.png"
        result_png_path = run_dir / "endometrioma_result.png"
        ovary_png_path = run_dir / "ovary_result.png"
        endo_mask_path = run_dir / "endometrioma_mask.nii.gz"
        ovary_mask_path = run_dir / "ovary_mask.nii.gz"
        prob_png_path = run_dir / "endometrioma_probability.png"

        update_task(task_id, progress=5)

        create_mri_preview_from_nifti(mri_file_path, str(preview_path))

        update_task(task_id, progress=10)

        def progress_callback(percent: int):
            update_task(task_id, progress=max(10, min(percent, 95)))

        result = segment_mri_file(
            input_mri_path=mri_file_path,
            model_path=str(MODEL_PATH),
            output_endo_mask_path=str(endo_mask_path),
            output_ovary_mask_path=str(ovary_mask_path),
            output_png_path=str(result_png_path),
            output_prob_png_path=str(prob_png_path),
            output_ovary_png_path=str(ovary_png_path),
            output_original_png_path=str(original_png_path),
            threshold=0.5,
            min_voxels=20,
            progress_callback=progress_callback,
        )

        update_task(
            task_id,
            status="completed",
            progress=100,
            success=True,
            preview_image_path=str(original_png_path),
            result_image_path=str(result_png_path),
            conclusion=f"{result['result_text']}. Вокселей эндометриомы: {result['endo_voxels']}",
            original_image_path=str(original_png_path),
            ovary_image_path=str(ovary_png_path),
            endo_mask_path=str(endo_mask_path),
            ovary_mask_path=str(ovary_mask_path),
            probability_image_path=str(prob_png_path),
            run_dir=str(run_dir),
        )

    except Exception as ex:
        update_task(
            task_id,
            status="error",
            success=False,
            error=str(ex),
        )

@app.delete("/segment/result/{task_id}")
def delete_segmentation_result(task_id: str):
    with tasks_lock:
        task = tasks.get(task_id)

    if task is None:
        raise HTTPException(status_code=404, detail="Task not found")

    run_dir = task.get("run_dir")

    if run_dir and Path(run_dir).exists():
        shutil.rmtree(run_dir)

    with tasks_lock:
        tasks.pop(task_id, None)

    return {"status": "deleted"}