using Microsoft.Extensions.Logging;
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
        private readonly IEmployeeRoleDAL _dal;
        private readonly ILogger<EmployeeRoleBAL> _logger;

        public EmployeeRoleBAL(IEmployeeRoleDAL dal, ILogger<EmployeeRoleBAL> logger)
        {
            _dal = dal;
            _logger = logger;
        }

        public async Task<(int, List<EmployeeRoleResponse>)> GetAll(Guid? employeeId, int? year, int? month)
        {
            var (count, data) = await _dal.GetAll(employeeId, year, month);
            var response = data.Select(es => new EmployeeRoleResponse
            {
                ID = es.ID,
                EmployeeID = es.EmployeeID,
                Email = es.Email,
                EmployeeName = $"{es.FirstName} {es.MiddleName} {es.LastName}".Replace("  ", " ").Trim(),
                RoleID = es.RoleID,
                RoleName = es.RoleName,
                Description = es.Description
            }).ToList();
            return (count, response);
        }

        public async Task<(int, List<RoleHistoryResponse>)> TrackRoleHistory(Guid? employeeId, int? year, int? month)
        {
            var (count, data) = await _dal.TrackRoleHistory(employeeId, year, month);
            var response = data.Select(es => new RoleHistoryResponse
            {
                ID = es.ID,
                EmployeeID = es.EmployeeID,
                Email = es.Email,
                EmployeeName = $"{es.FirstName} {es.MiddleName} {es.LastName}".Replace("  ", " ").Trim(),
                RoleID = es.RoleID,
                OldRoleID = es.OldRoleID,
                RoleName = es.RoleName,
                OldRoleName = es.OldRoleName,
                OverridenOn = es.OverridenOn,
                Remarks = es.Remarks
            }).ToList();
            return (count, response);
        }
    }
}
