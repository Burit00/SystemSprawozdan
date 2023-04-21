﻿using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using SystemSprawozdan.Backend.Data;
using SystemSprawozdan.Backend.Data.Enums;
using SystemSprawozdan.Backend.Data.Models.DbModels;
using SystemSprawozdan.Backend.Data.Models.Dto;
using SystemSprawozdan.Backend.Data.Models.Others;
using SystemSprawozdan.Backend.Exceptions;

namespace SystemSprawozdan.Backend.Services
{
    public interface IAccountService
    {
        string LoginUser(LoginUserDto loginUserDto);
        void RegisterStudent(RegisterStudentDto registerStudentDto);
        void RegisterTeacherOrAdmin(RegisterTeacherOrAdminDto registerTeacherOrAdminDto);
    }

    public class AccountService : IAccountService
    {
        private readonly ApiDbContext _dbContext;
        private readonly IPasswordHasher<Student> _passwordHasherStudent;
        private readonly IPasswordHasher<Teacher> _passwordHasherTeacher;
        private readonly IPasswordHasher<Admin> _passwordHasherAdmin;
        private readonly AuthenticationSettings _authenticationSettings;
        private readonly IUserContextService _userContextService;
        public readonly IMapper _mapper;

        public AccountService(ApiDbContext dbContext, 
            IPasswordHasher<Student> passwordHasherStudent, 
            IPasswordHasher<Teacher> passwordHasherTeacher, 
            IPasswordHasher<Admin> passwordHasherAdmin, 
            AuthenticationSettings authenticationSettings, 
            IUserContextService userContextService, 
            IMapper mapper)
        {
            _dbContext = dbContext;
            _passwordHasherStudent = passwordHasherStudent;
            _passwordHasherTeacher = passwordHasherTeacher;
            _passwordHasherAdmin = passwordHasherAdmin;
            _authenticationSettings = authenticationSettings;
            _userContextService = userContextService;
            _mapper = mapper;
        }

        public string LoginUser(LoginUserDto loginUserDto)
        {
            var student = _dbContext.Student.FirstOrDefault(user => user.Login == loginUserDto.Login && user.IsDeleted == false);
            var teacher = _dbContext.Teacher.FirstOrDefault(user => user.Login == loginUserDto.Login && user.IsDeleted == false);
            var admin = _dbContext.Admin.FirstOrDefault(user => user.Login == loginUserDto.Login);

            PasswordVerificationResult result = new PasswordVerificationResult();
            User user = new User();

            if (student is null && teacher is null && admin is null)
                throw new BadRequestException("Wrong username or password!");

            else if (student is not null)
            {
                result = _passwordHasherStudent
                    .VerifyHashedPassword(student, student.Password, loginUserDto.Password);

                if (result == PasswordVerificationResult.Failed)
                    throw new BadRequestException("Wrong username or password!");

                user = _mapper.Map<User>(student);
                user.UserRole = UserRoleEnum.Student;

                return _GenerateJwt(user);
            }
            else if (teacher is not null)
            {
                result = _passwordHasherTeacher
                    .VerifyHashedPassword(teacher, teacher.Password, loginUserDto.Password);

                if (result == PasswordVerificationResult.Failed)
                    throw new BadRequestException("Wrong username or password!");

                user = _mapper.Map<User>(teacher);
                user.UserRole = UserRoleEnum.Teacher;

                return _GenerateJwt(user);
            }

            result = _passwordHasherAdmin
                .VerifyHashedPassword(admin, admin.Password, loginUserDto.Password);

            if (result == PasswordVerificationResult.Failed)
                throw new BadRequestException("Wrong username or password!");

            user = _mapper.Map<User>(admin);
            user.UserRole = UserRoleEnum.Admin;

            return _GenerateJwt(user);
        }

        public void RegisterStudent(RegisterStudentDto registerStudentDto)
        {
            var newStudent = new Student()
            {
                Name = registerStudentDto.Name,
                Surname = registerStudentDto.Surname,
                Email = registerStudentDto.Email,
                Login = registerStudentDto.Login,
            };
            newStudent.Password = _passwordHasherStudent.HashPassword(newStudent, registerStudentDto.Password);

            _dbContext.Student.Add(newStudent);
            _dbContext.SaveChanges();
        }

        public void RegisterTeacherOrAdmin(RegisterTeacherOrAdminDto registerTeacherOrAdminDto)
        {
            if (registerTeacherOrAdminDto.UserRole == UserRoleEnum.Teacher)
            {
                var newTeacher = new Teacher()
                {
                    Name = registerTeacherOrAdminDto.Name,
                    Surname = registerTeacherOrAdminDto.Surname,
                    Email = registerTeacherOrAdminDto.Email,
                    Degree = registerTeacherOrAdminDto.Degree,
                    Position = registerTeacherOrAdminDto.Position,
                    Login = registerTeacherOrAdminDto.Login,
                };
                newTeacher.Password = _passwordHasherTeacher.HashPassword(newTeacher, registerTeacherOrAdminDto.Password);

                _dbContext.Teacher.Add(newTeacher);
                _dbContext.SaveChanges();
            }
            else if (registerTeacherOrAdminDto.UserRole == UserRoleEnum.Admin)
            {
                var newAdmin = new Admin()
                {
                    Login = registerTeacherOrAdminDto.Login,
                };
                newAdmin.Password = _passwordHasherAdmin.HashPassword(newAdmin, registerTeacherOrAdminDto.Password);

                _dbContext.Admin.Add(newAdmin);
                _dbContext.SaveChanges();
            }
            else
                throw new BadRequestException("Wrong user role!");

        }

        private string _GenerateJwt(User user)
        {
            var claims = new List<Claim>()
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Role, $"{(int)user.UserRole}")
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_authenticationSettings.JwtKey));
            var cred = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.Now.AddDays(_authenticationSettings.JwtExpireDays);

            var token = new JwtSecurityToken(
                _authenticationSettings.JwtIssuer,
                _authenticationSettings.JwtIssuer,
                claims,
                expires: expires,
                signingCredentials: cred
                );

            var tokenStr = new JwtSecurityTokenHandler().WriteToken(token);

            return tokenStr;
        }
    }
}
