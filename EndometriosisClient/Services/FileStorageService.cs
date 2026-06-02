using System;
using System.IO;

namespace EndometriosisClient.Services
{
    public class FileStorageService
    {
        private readonly string _baseStoragePath;
        private readonly string _mriFolder;
        private readonly string _resultsFolder;
        private readonly string _previewsFolder;

        public FileStorageService()
        {
            _baseStoragePath = Path.Combine(Directory.GetCurrentDirectory(), "Storage");
            _mriFolder = Path.Combine(_baseStoragePath, "mri");
            _resultsFolder = Path.Combine(_baseStoragePath, "results");
            _previewsFolder = Path.Combine(_baseStoragePath, "previews");

            Directory.CreateDirectory(_baseStoragePath);
            Directory.CreateDirectory(_mriFolder);
            Directory.CreateDirectory(_resultsFolder);
            Directory.CreateDirectory(_previewsFolder);
        }

        public string SaveMRIFile(string sourceFilePath)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath))
                throw new ArgumentException("Путь к МРТ-файлу не указан.");

            if (!File.Exists(sourceFilePath))
                throw new FileNotFoundException("Файл МРТ не найден.", sourceFilePath);

            string originalFileName = Path.GetFileName(sourceFilePath);
            string uniqueFileName = $"{Guid.NewGuid()}_{originalFileName}"; //добавляет к имени файла уникальный айди
            string destinationPath = Path.Combine(_mriFolder, uniqueFileName);

            File.Copy(sourceFilePath, destinationPath, overwrite: true);

            return destinationPath;
        }

        public string SaveResultFile(string sourceFilePath)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath))
                throw new ArgumentException("Путь к файлу результата не указан.");

            if (!File.Exists(sourceFilePath))
                throw new FileNotFoundException("Файл результата не найден.", sourceFilePath);

            string originalFileName = Path.GetFileName(sourceFilePath);
            string uniqueFileName = $"{Guid.NewGuid()}_{originalFileName}";
            string destinationPath = Path.Combine(_resultsFolder, uniqueFileName);

            File.Copy(sourceFilePath, destinationPath, overwrite: true);

            return destinationPath;
        }

        public string GetNewResultFilePath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("Имя файла результата не указано.");

            string uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
            return Path.Combine(_resultsFolder, uniqueFileName);
        }

        public string GetNewPreviewFilePath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("Имя файла превью не указано.");

            string uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
            return Path.Combine(_previewsFolder, uniqueFileName);
        }
    }
}