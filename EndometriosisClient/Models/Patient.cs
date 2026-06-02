using System;

namespace EndometriosisClient.Models
{
    public class Patient
    {
        // Уникальный идентификатор
        public int Id { get; set; }

        // ФИО или код пациента
        public string FullName { get; set; }

        // Возраст
        public int Age { get; set; }

        // Дополнительная информация
        public string Notes { get; set; }

        // ID врача, которому принадлежит пациент
        public int DoctorId { get; set; }

        // Врач
        public AppUser Doctor { get; set; }

        // Дата создания записи
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}