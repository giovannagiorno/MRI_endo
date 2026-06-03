using System;

namespace EndometriosisClient.Models
{
    public class SegmentationResult
    {
        // Уникальный идентификатор результата
        public int Id { get; set; }

        // ID исследования МРТ
        public int MRIStudyId { get; set; }

        // Путь к исходному МРТ-превью
        public string PreviewImagePath { get; set; } = string.Empty;

        // Путь к файлу результата
        public string ResultPath { get; set; }

        // Статус обработки: demo, processed, error
        public string Status { get; set; }

        // Краткое заключение
        public string Conclusion { get; set; }

        // Дата создания результата
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public MRIStudy MRIStudy { get; set; }
    }
}