using Microsoft.Extensions.Logging;
using TecLogos.SOP.AuthBAL;
using TecLogos.SOP.DAL.SOP;
using TecLogos.SOP.WebModel.SOP;

namespace TecLogos.SOP.BAL.SOP
{
    public interface IEmployeeRoleBAL
    {
        Task<(int, List<EmployeeRoleResponse>)> GetAll(Guid? employeeId, int? year, int? month);
        Task<(int, List<RoleHistoryResponse>)> TrackRoleHistory(Guid? employeeId, int? year, int? month);
    }
    public class EmployeeRoleBAL : IEmployeeRoleBAL
    {
        private readonly IEmployeeRoleDAL _employeeRoleDAL;
        private readonly ILogger<EmployeeRoleBAL> _logger;
        private readonly IUserContextBAL _userContext;

        public EmployeeRoleBAL(
            IEmployeeRoleDAL employeeRoleDAL,
            ILogger<EmployeeRoleBAL> logger,
            IUserContextBAL userContext)
        {
            _employeeRoleDAL = employeeRoleDAL;
            _logger = logger;
            _userContext = userContext;
        }

        public async Task<(int, List<EmployeeRoleResponse>)> GetAll(Guid? employeeId, int? year, int? month)
        {
            var (count, data) = await _employeeRoleDAL.GetAll(employeeId, year, month);

            var response = data.Select(es => new EmployeeRoleResponse
            {
                ID = es.ID,
                EmployeeID = es.EmployeeID,
                Email = es.Email,
                EmployeeName = $"{es.FirstName} {es.MiddleName} {es.LastName}",
                RoleID = es.RoleID,
                RoleName = es.RoleName,
                Description = es.Description

            }).ToList();

            return (count, response);
        }
        public async Task<(int, List<RoleHistoryResponse>)> TrackRoleHistory(Guid? employeeId, int? year, int? month)
        {
            var (count, data) = await _employeeRoleDAL.TrackRoleHistory(employeeId, year, month);

            var response = data.Select(es => new RoleHistoryResponse
            {
                ID = es.ID,
                EmployeeID = es.EmployeeID,
                Email = es.Email,
                EmployeeName = $"{es.FirstName} {es.MiddleName} {es.LastName}",
                RoleID = es.RoleID,
                OldRoleID = es.OldRoleID,
                RoleName = es.RoleName,
                OldRoleName = es.OldRoleName,
                ChangedOn = es.ChangedOn,
                Remarks = es.Remarks

            }).ToList();

            return (count, response);
        }
    }
}
