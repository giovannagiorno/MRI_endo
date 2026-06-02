using System;
using System.Collections.Generic;

namespace EndometriosisClient.Models
{
    public class AppUser
    {
        public int Id { get; set; }

        // ФИО врача или администратора
        public string FullName { get; set; }

        // Логин для входа
        public string Login { get; set; }

        // Хэш пароля
        public string PasswordHash { get; set; }

        // Admin или Doctor
        public string Role { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public List<Patient> Patients { get; set; } = new();
    }

    public static class UserRoles
    {
        public const string Admin = "Admin";
        public const string Doctor = "Doctor";
    }
}