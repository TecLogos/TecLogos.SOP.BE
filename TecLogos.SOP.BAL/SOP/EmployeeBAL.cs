using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using TecLogos.SOP.AuthBAL;
using TecLogos.SOP.BAL.Auth;
using TecLogos.SOP.DAL.SOP;

namespace TecLogos.SOP.BAL.SOP
{
    public interface IEmployeeBAL
    {
        Task<(int, List<WebModel.SOP.EmployeeList>)> GetAll(int pageNumber, int pageSize, string term);
        Task<WebModel.SOP.Employee?> GetById(Guid id);
        Task<Guid> Create(WebModel.SOP.Employee employee, Guid createdByID);
        Task<bool> Update(WebModel.SOP.Employee employee, Guid modifiedByID);
        Task<bool> Delete(Guid id, Guid deletedByID);
    }

    public class EmployeeBAL : IEmployeeBAL
    {
        private readonly IEmployeeDAL _employeeDAL;
        private readonly ILogger<EmployeeBAL> _logger;
        private readonly IUserContextBAL _userContext;
        private readonly IAuthOnboardingBAL _onboarding;

        public EmployeeBAL(IEmployeeDAL employeeDAL, ILogger<EmployeeBAL> logger, IUserContextBAL userContext, IAuthOnboardingBAL onboarding)
        {
            _employeeDAL = employeeDAL;
            _logger = logger;
            _userContext = userContext;
            _onboarding = onboarding;

        }

        // GET ALL
        public async Task<(int, List<WebModel.SOP.EmployeeList>)> GetAll(int pageNumber, int pageSize, string term)
        {
            var result = await _employeeDAL.GetAll(pageNumber, pageSize, term);
            var dtos = new List<WebModel.SOP.EmployeeList>();

            foreach (var employee in result.Item2)
            {
                dtos.Add(MapToResponseGetAll(employee));
            }

            return (result.Item1, dtos);
        }

        // GET BY ID
        public async Task<WebModel.SOP.Employee?> GetById(Guid id)
        {
            var employee = await _employeeDAL.GetById(id);

            if (employee == null)
                return null;
            return MapToResponseDto(employee);
        }

        // CREATE
        public async Task<Guid> Create(WebModel.SOP.Employee employee, Guid createdByID)
        {

            if (string.IsNullOrWhiteSpace(employee.FirstName))
                throw new ArgumentException("First Name is required");

            if (string.IsNullOrWhiteSpace(employee.Email))
                throw new ArgumentException("Email is required");

            if (await _employeeDAL.IsEmployeeEmailExists(employee.Email))
                throw new InvalidOperationException($"Employee Email '{employee.Email}' already exists");

            var employeeDM = new DataModel.SOP.Employee
            {
                ID = Guid.NewGuid(),
                FirstName = employee.FirstName,
                MiddleName = employee.MiddleName,
                LastName = employee.LastName,
                Email = employee.Email,
                MobileNumber = employee.MobileNumber,
                Address = employee.Address,
                ManagerID = employee.ManagerID,

                IsActive = true,
                IsDeleted = false,
                Created = DateTime.Now,
                CreatedByID = createdByID
            };



            // Save employee
            var employeeId = await _employeeDAL.Create(employeeDM);

            // Auto send onboarding invite 🔥
            await _onboarding.SendInvite(employeeId, createdByID);

            // Return new employee ID
            return employeeId;
        }

        // UPDATE
        public async Task<bool> Update(WebModel.SOP.Employee dto, Guid modifiedByID)
        {
            if (dto.ID == Guid.Empty)
                throw new ArgumentException("Invalid Employee ID.");

            var existingEmployee = await _employeeDAL.GetById(dto.ID);
            if (existingEmployee == null)
                throw new ArgumentException($"Employee with ID {dto.ID} not found.");

            var employee = new DataModel.SOP.Employee
            {
                ID = dto.ID,

                FirstName = dto.FirstName,
                MiddleName = dto.MiddleName,
                LastName = dto.LastName,
                Email = dto.Email,
                MobileNumber = dto.MobileNumber,
                Address = dto.Address,
                ManagerID = dto.ManagerID,
                Version = dto.Version,
                IsActive = dto.IsActive,
                ModifiedByID = modifiedByID
            };



            var success = await _employeeDAL.Update(employee);

            if (!success)
                throw new Common.Exceptions.ConcurrencyException(
                    "Employee was modified by another user. Please reload.");

            return true;
        }

        // DELETE
        public async Task<bool> Delete(Guid id, Guid deletedByID)
        {
            var result = await _employeeDAL.Delete(id, deletedByID);
            _logger.LogInformation($"Employee deleted (soft delete) with ID: {id}");
            return result;
        }

        private WebModel.SOP.EmployeeList MapToResponseGetAll(TecLogos.SOP.DataModel.SOP.EmployeeList employee)
        {
            return new WebModel.SOP.EmployeeList
            {
                ID = employee.ID,

                FirstName = employee.FirstName,
                MiddleName = employee.MiddleName,
                LastName = employee.LastName,
                Email = employee.Email,
                MobileNumber = employee.MobileNumber,
                Version = employee.Version,
                IsActive = employee.IsActive,
            };
        }

        private WebModel.SOP.Employee MapToResponseDto(TecLogos.SOP.DataModel.SOP.Employee employee)
        {
            return new WebModel.SOP.Employee
            {
                ID = employee.ID,

                FirstName = employee.FirstName,
                MiddleName = employee.MiddleName,
                LastName = employee.LastName,
                Email = employee.Email,
                MobileNumber = employee.MobileNumber,
                Address = employee.Address,
                ManagerID = employee.ManagerID,

          
                Version = employee.Version,
                IsActive = employee.IsActive,
                IsDeleted = employee.IsDeleted,
                Created = employee.Created ?? default(DateTime),
                CreatedByID = employee.CreatedByID ?? Guid.Empty,
                Modified = employee.Modified,
                ModifiedByID = employee.ModifiedByID ?? Guid.Empty,
                Deleted = employee.Deleted,
                DeletedByID = employee.DeletedByID ?? Guid.Empty,


            };
        }
    }
}