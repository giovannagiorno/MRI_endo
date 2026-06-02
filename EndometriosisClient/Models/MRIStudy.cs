using System;

namespace EndometriosisClient.Models
{
    public class MRIStudy
    {
        // Уникальный идентификатор исследования
        public int Id { get; set; }

        // ID пациента (связь с Patient)
        public int PatientId { get; set; }

        // Путь к файлу МРТ (.nii или .nii.gz)
        public string FilePath { get; set; }

        // Имя файла (для отображения)
        public string FileName { get; set; }

        // Дата загрузки исследования
        public DateTime UploadDate { get; set; } = DateTime.Now;

        // Тип файла (например: NIfTI)
        public string FileType { get; set; }

        // Навигационное свойство (связь с пациентом)
        public Patient Patient { get; set; }
    }
}