using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Nifti.NET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EndometriosisClient.Services
{
    public class OnnxSegmentationService : IDisposable
    {
        private readonly InferenceSession _session;
        private readonly FileStorageService _fileStorageService;

        private readonly string _inputName;
        private readonly string _outputName;

        private const int PatchD = 32;
        private const int PatchH = 128;
        private const int PatchW = 128;

        private const int StrideD = 16;
        private const int StrideH = 64;
        private const int StrideW = 64;

        private const float Threshold = 0.5f;
        private const int MinVoxels = 20;

        private const bool OnnxOutputAlreadyHasSigmoid = true;

        public OnnxSegmentationService()
        {
            _fileStorageService = new FileStorageService();

            string modelPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Model",
                "attention_unet3d.onnx");

            if (!File.Exists(modelPath))
                throw new FileNotFoundException("ONNX-модель не найдена.", modelPath);

            var options = CreateSessionOptions();

            _session = new InferenceSession(modelPath, options);

            _inputName = _session.InputMetadata.Keys.First();
            _outputName = _session.OutputMetadata.Keys.First();
        }

        private static SessionOptions CreateSessionOptions()
        {
            var options = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
            };

            // сейчас используется CPU.
            options.AppendExecutionProvider_CPU();

            return options;
        }

        public LocalSegmentationResponse SegmentMri(string mriFilePath, IProgress<int>? progress = null)
        {
            if (string.IsNullOrWhiteSpace(mriFilePath))
                throw new ArgumentException("Путь к МРТ-файлу не указан.");

            if (!File.Exists(mriFilePath))
                throw new FileNotFoundException("Файл МРТ не найден.", mriFilePath);

            progress?.Report(1);

            var volume = LoadNiftiAsDhw(mriFilePath, out int depth, out int height, out int width);
            NormalizeInPlace(volume);

            progress?.Report(5);

            var prediction = PredictVolumeSlidingWindow(volume, depth, height, width, progress);

            byte[] ovaryMask = CreateMask(prediction.OvaryProb, Threshold);
            byte[] endoMask = CreateMask(prediction.EndoProb, Threshold);

            int endoVoxels = endoMask.Count(v => v > 0);
            bool detected = endoVoxels >= MinVoxels;

            string conclusion = detected
                ? "Эндометриома обнаружена."
                : "Эндометриома не обнаружена.";

            string previewPath = _fileStorageService.GetNewPreviewFilePath("mri_preview.png");
            string resultPath = _fileStorageService.GetNewResultFilePath("endometrioma_result.png");

            // Один общий срез для исходного изображения и результата сегментации
            int displaySlice = GetBestSlice(endoMask, depth, height, width);

            SavePreviewPng(volume, depth, height, width, displaySlice, previewPath);
            SaveOverlayPng(volume, endoMask, depth, height, width, displaySlice, resultPath);

            progress?.Report(100);

            return new LocalSegmentationResponse
            {
                Success = true,
                PreviewImagePath = previewPath,
                ResultImagePath = resultPath,
                Conclusion = $"{conclusion} Количество вокселей: {endoVoxels}.",
                Status = "processed"
            };
        }

        private static float[] LoadNiftiAsDhw(string mriFilePath, out int depth, out int height, out int width)
        {
            var nifti = NiftiFile.Read(mriFilePath);

            if (nifti.Header.dim[0] < 3)
                throw new InvalidDataException("Файл МРТ не содержит 3D-данные.");

            height = Convert.ToInt32(nifti.Header.dim[1]);
            width = Convert.ToInt32(nifti.Header.dim[2]);
            depth = Convert.ToInt32(nifti.Header.dim[3]);

            if (height <= 0 || width <= 0 || depth <= 0)
                throw new InvalidDataException("Некорректные размеры NIfTI-файла.");

            var volumeDhw = new float[depth * height * width];

            for (int z = 0; z < depth; z++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float value = Convert.ToSingle(nifti[y, x, z]);
                        volumeDhw[Index(z, y, x, height, width)] = value;
                    }
                }
            }

            return volumeDhw;
        }

        private static void NormalizeInPlace(float[] volume)
        {
            float min = float.MaxValue;
            float max = float.MinValue;

            for (int i = 0; i < volume.Length; i++)
            {
                float value = volume[i];

                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    volume[i] = 0f;
                    value = 0f;
                }

                if (value < min) min = value;
                if (value > max) max = value;
            }

            float range = max - min;

            if (range <= 1e-8f)
            {
                Array.Clear(volume, 0, volume.Length);
                return;
            }

            for (int i = 0; i < volume.Length; i++)
            {
                volume[i] = (volume[i] - min) / (range + 1e-8f);
            }
        }

        private PredictionResult PredictVolumeSlidingWindow(
            float[] volume,
            int depth,
            int height,
            int width,
            IProgress<int>? progress)
        {
            var ovarySum = new float[volume.Length];
            var endoSum = new float[volume.Length];
            var count = new float[volume.Length];

            List<int> zStarts = MakeStarts(depth, PatchD, StrideD);
            List<int> yStarts = MakeStarts(height, PatchH, StrideH);
            List<int> xStarts = MakeStarts(width, PatchW, StrideW);

            int totalPatches = zStarts.Count * yStarts.Count * xStarts.Count;
            int processedPatches = 0;

            foreach (int z in zStarts)
            {
                foreach (int y in yStarts)
                {
                    foreach (int x in xStarts)
                    {
                        int z2 = Math.Min(z + PatchD, depth);
                        int y2 = Math.Min(y + PatchH, height);
                        int x2 = Math.Min(x + PatchW, width);

                        int currentPatchD = z2 - z;
                        int currentPatchH = y2 - y;
                        int currentPatchW = x2 - x;

                        float[] patch = CreatePaddedPatch(
                            volume,
                            depth,
                            height,
                            width,
                            z,
                            y,
                            x,
                            currentPatchD,
                            currentPatchH,
                            currentPatchW);

                        float[] output = RunPatch(patch);

                        for (int pz = 0; pz < currentPatchD; pz++)
                        {
                            for (int py = 0; py < currentPatchH; py++)
                            {
                                for (int px = 0; px < currentPatchW; px++)
                                {
                                    int targetIndex = Index(z + pz, y + py, x + px, height, width);

                                    float ovaryValue = GetOutputValue(output, 0, pz, py, px);
                                    float endoValue = GetOutputValue(output, 1, pz, py, px);

                                    ovarySum[targetIndex] += ToProbability(ovaryValue);
                                    endoSum[targetIndex] += ToProbability(endoValue);
                                    count[targetIndex] += 1f;
                                }
                            }
                        }

                        processedPatches++;

                        int percent = 5 + (int)Math.Round(processedPatches * 90.0 / totalPatches);
                        progress?.Report(Math.Min(percent, 95));
                    }
                }
            }

            for (int i = 0; i < volume.Length; i++)
            {
                if (count[i] > 0)
                {
                    ovarySum[i] /= count[i];
                    endoSum[i] /= count[i];
                }
            }

            return new PredictionResult
            {
                OvaryProb = ovarySum,
                EndoProb = endoSum
            };
        }

        private float[] RunPatch(float[] patchData)
        {
            var inputTensor = new DenseTensor<float>(
                patchData,
                new[] { 1, 1, PatchD, PatchH, PatchW });

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)
            };

            using var results = _session.Run(inputs);

            var outputTensor = results
                .First(result => result.Name == _outputName)
                .AsTensor<float>();

            return outputTensor.ToArray();
        }

        private static float[] CreatePaddedPatch(
            float[] volume,
            int depth,
            int height,
            int width,
            int startZ,
            int startY,
            int startX,
            int currentPatchD,
            int currentPatchH,
            int currentPatchW)
        {
            var patch = new float[PatchD * PatchH * PatchW];

            for (int pz = 0; pz < currentPatchD; pz++)
            {
                for (int py = 0; py < currentPatchH; py++)
                {
                    for (int px = 0; px < currentPatchW; px++)
                    {
                        int sourceIndex = Index(startZ + pz, startY + py, startX + px, height, width);
                        int patchIndex = PatchIndex(pz, py, px);

                        patch[patchIndex] = volume[sourceIndex];
                    }
                }
            }

            return patch;
        }

        private static byte[] CreateMask(float[] probability, float threshold)
        {
            var mask = new byte[probability.Length];

            for (int i = 0; i < probability.Length; i++)
            {
                mask[i] = probability[i] > threshold ? (byte)1 : (byte)0;
            }

            return mask;
        }

        private static List<int> MakeStarts(int size, int patch, int stride)
        {
            var starts = new List<int>();

            if (size <= patch)
            {
                starts.Add(0);
                return starts;
            }

            for (int start = 0; start <= size - patch; start += stride)
            {
                starts.Add(start);
            }

            int lastStart = size - patch;

            if (starts[starts.Count - 1] != lastStart)
                starts.Add(lastStart);

            return starts;
        }

        private static int GetBestSlice(byte[] mask, int depth, int height, int width)
        {
            int bestSlice = depth / 2;
            int bestSum = 0;

            for (int z = 0; z < depth; z++)
            {
                int sum = 0;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        sum += mask[Index(z, y, x, height, width)];
                    }
                }

                if (sum > bestSum)
                {
                    bestSum = sum;
                    bestSlice = z;
                }
            }

            return bestSum == 0 ? depth / 2 : bestSlice;
        }

        private static void SavePreviewPng(
            float[] volume,
            int depth,
            int height,
            int width,
            int slice,
            string outputPath)
        {
            if (slice < 0 || slice >= depth)
                slice = depth / 2;

            var pixels = new byte[height * width * 4];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float value = Clamp01(volume[Index(slice, y, x, height, width)]);
                    byte gray = (byte)(value * 255);

                    int pixelIndex = (y * width + x) * 4;

                    pixels[pixelIndex + 0] = gray; // B
                    pixels[pixelIndex + 1] = gray; // G
                    pixels[pixelIndex + 2] = gray; // R
                    pixels[pixelIndex + 3] = 255;  // A
                }
            }

            SaveBgraPng(pixels, width, height, outputPath);
        }

        private static void SaveOverlayPng(
            float[] volume,
            byte[] endoMask,
            int depth,
            int height,
            int width,
            int slice,
            string outputPath)
        {
            if (slice < 0 || slice >= depth)
                slice = depth / 2;

            var pixels = new byte[height * width * 4];

            const float alpha = 0.45f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int volumeIndex = Index(slice, y, x, height, width);

                    float value = Clamp01(volume[volumeIndex]);
                    byte gray = (byte)(value * 255);

                    byte b = gray;
                    byte g = gray;
                    byte r = gray;

                    if (endoMask[volumeIndex] > 0)
                    {
                        r = (byte)(gray * (1 - alpha) + 255 * alpha);
                        g = (byte)(gray * (1 - alpha));
                        b = (byte)(gray * (1 - alpha));
                    }

                    int pixelIndex = (y * width + x) * 4;

                    pixels[pixelIndex + 0] = b;
                    pixels[pixelIndex + 1] = g;
                    pixels[pixelIndex + 2] = r;
                    pixels[pixelIndex + 3] = 255;
                }
            }

            SaveBgraPng(pixels, width, height, outputPath);
        }

        private static void SaveBgraPng(byte[] pixels, int width, int height, string outputPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            var bitmap = BitmapSource.Create(
                width,
                height,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                pixels,
                width * 4);

            bitmap.Freeze();

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using var stream = new FileStream(outputPath, FileMode.Create);
            encoder.Save(stream);
        }

        private static float ToProbability(float value)
        {
            if (OnnxOutputAlreadyHasSigmoid)
                return value;

            return 1f / (1f + MathF.Exp(-value));
        }

        private static float GetOutputValue(float[] output, int channel, int z, int y, int x)
        {
            // output shape: [1, 2, 32, 128, 128]
            return output[((channel * PatchD + z) * PatchH + y) * PatchW + x];
        }

        private static int Index(int z, int y, int x, int height, int width)
        {
            return (z * height * width) + (y * width) + x;
        }

        private static int PatchIndex(int z, int y, int x)
        {
            return (z * PatchH * PatchW) + (y * PatchW) + x;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        public void Dispose()
        {
            _session.Dispose();
        }

        private class PredictionResult
        {
            public float[] OvaryProb { get; set; } = Array.Empty<float>();
            public float[] EndoProb { get; set; } = Array.Empty<float>();
        }
    }

    public class LocalSegmentationResponse
    {
        public bool Success { get; set; }
        public string PreviewImagePath { get; set; } = string.Empty;
        public string ResultImagePath { get; set; } = string.Empty;
        public string Conclusion { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}