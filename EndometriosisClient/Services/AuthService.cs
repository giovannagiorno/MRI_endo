using System;
using System.Linq;
using EndometriosisClient.Data;
using EndometriosisClient.Models;

namespace EndometriosisClient.Services
{
    public class AuthService
    {
        public AppUser Login(string login, string password)
        {
            using var db = new AppDbContext();

            login = login.Trim();

            var user = db.Users.FirstOrDefault(u => u.Login == login);

            if (user == null)
                return null;

            bool passwordIsValid = PasswordHasher.VerifyPassword(password, user.PasswordHash);

            if (!passwordIsValid)
                return null;

            return user;
        }

        public bool HasUsers()
        {
            using var db = new AppDbContext();
            return db.Users.Any();
        }

        public AppUser RegisterUser(string fullName, string login, string password)
        {
            using var db = new AppDbContext();

            fullName = fullName.Trim();
            login = login.Trim();

            if (db.Users.Any(u => u.Login == login))
                throw new Exception("Пользователь с таким логином уже существует.");

            string role = db.Users.Any()
                ? UserRoles.Doctor
                : UserRoles.Admin;

            var user = new AppUser
            {
                FullName = fullName,
                Login = login,
                PasswordHash = PasswordHasher.HashPassword(password),
                Role = role
            };

            db.Users.Add(user);
            db.SaveChanges();

            return user;
        }
    }
}