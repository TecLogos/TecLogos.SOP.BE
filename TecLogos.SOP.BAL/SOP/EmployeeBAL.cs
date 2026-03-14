using Microsoft.Extensions.Logging;
using TecLogos.SOP.Common.Helpers;
using TecLogos.SOP.DAL.SOP;
using TecLogos.SOP.WebModel.SOP;

namespace TecLogos.SOP.BAL.SOP
{
    public interface IEmployeeBAL
    {
        Task<(int, List<EmployeeList>)> GetAll(int pageNumber, int pageSize, string term);
        Task<Employee?> GetById(Guid id);
        Task<Guid> Create(Employee employee, Guid createdByID);
        Task<bool> Update(Employee employee, Guid modifiedByID);
        Task<bool> Delete(Guid id, Guid deletedByID);
    }

    public class EmployeeBAL : IEmployeeBAL
    {
        private readonly IEmployeeDAL _employeeDAL;
        private readonly IAuthDAL _authDAL;
        private readonly ILogger<EmployeeBAL> _logger;

        public EmployeeBAL(IEmployeeDAL employeeDAL, IAuthDAL authDAL, ILogger<EmployeeBAL> logger)
        {
            _employeeDAL = employeeDAL;
            _authDAL = authDAL;
            _logger = logger;
        }

        public async Task<(int, List<EmployeeList>)> GetAll(int pageNumber, int pageSize, string term)
        {
            var (total, employees) = await _employeeDAL.GetAll(pageNumber, pageSize, term);
            return (total, employees.Select(e => new EmployeeList
            {
                ID = e.ID,
                FirstName = e.FirstName,
                MiddleName = e.MiddleName,
                LastName = e.LastName,
                Email = e.Email,
                MobileNumber = e.MobileNumber,
                IsActive = e.IsActive
            }).ToList());
        }

        public async Task<Employee?> GetById(Guid id)
        {
            var e = await _employeeDAL.GetById(id);
            if (e == null) return null;

            return new Employee
            {
                ID = e.ID,
                FirstName = e.FirstName,
                MiddleName = e.MiddleName,
                LastName = e.LastName,
                Email = e.Email,
                MobileNumber = e.MobileNumber,
                RoleID = e.RoleID,
                Version = e.Version,
                IsActive = e.IsActive,
                IsDeleted = e.IsDeleted,
                Created = e.Created,
                CreatedByID = e.CreatedByID,
                Modified = e.Modified,
                ModifiedByID = e.ModifiedByID ?? Guid.Empty,
                Deleted = e.Deleted,
                DeletedByID = e.DeletedByID ?? Guid.Empty
            };
        }

        public async Task<Guid> Create(Employee dto, Guid createdByID)
        {
            if (string.IsNullOrWhiteSpace(dto.FirstName))
                throw new ArgumentException("First Name is required.");
            if (string.IsNullOrWhiteSpace(dto.Email))
                throw new ArgumentException("Email is required.");
            if (await _employeeDAL.IsEmployeeEmailExists(dto.Email))
                throw new InvalidOperationException($"Email '{dto.Email}' already exists.");

            var dm = new TecLogos.SOP.DataModel.SOP.Employee
            {
                ID = Guid.NewGuid(),
                FirstName = dto.FirstName,
                MiddleName = dto.MiddleName,
                LastName = dto.LastName,
                Email = dto.Email,
                MobileNumber = dto.MobileNumber,
                RoleID = dto.RoleID == Guid.Empty ? null : dto.RoleID,
                IsActive = true,
                IsDeleted = false,
                CreatedByID = createdByID
            };

            var employeeId = await _employeeDAL.Create(dm);

            // Auto-create AuthManager record + send onboarding invite
            var tempHash = PasswordHasher.Hash("Welcome@123"); // placeholder until onboarding
            var token = PasswordHasher.GenerateToken(32);
            await _authDAL.CreateOnboardingInviteAsync(
                employeeId, token, DateTime.UtcNow.AddDays(7), createdByID);

            _logger.LogInformation("Employee created: {ID}, onboarding invite token: {Token}", employeeId, token);
            // TODO: Send email with token link

            return employeeId;
        }

        public async Task<bool> Update(Employee dto, Guid modifiedByID)
        {
            if (dto.ID == Guid.Empty) throw new ArgumentException("Invalid Employee ID.");

            var existing = await _employeeDAL.GetById(dto.ID);
            if (existing == null) throw new ArgumentException($"Employee {dto.ID} not found.");

            var dm = new TecLogos.SOP.DataModel.SOP.Employee
            {
                ID = dto.ID,
                FirstName = dto.FirstName,
                MiddleName = dto.MiddleName,
                LastName = dto.LastName,
                Email = dto.Email,
                MobileNumber = dto.MobileNumber,
                RoleID = dto.RoleID == Guid.Empty ? null : dto.RoleID,
                Version = dto.Version,
                IsActive = dto.IsActive,
                ModifiedByID = modifiedByID
            };

            var success = await _employeeDAL.Update(dm);
            if (!success)
                throw new Common.Exceptions.ConcurrencyException("Employee was modified by another user. Please reload.");

            return true;
        }

        public async Task<bool> Delete(Guid id, Guid deletedByID)
        {
            var result = await _employeeDAL.Delete(id, deletedByID);
            _logger.LogInformation("Employee soft-deleted: {ID}", id);
            return result;
        }
    }
}
