using Microsoft.Extensions.Logging;
using TecLogos.SOP.DAL.SOP;

namespace TecLogos.SOP.BAL.SOP
{
    public interface IEmployeeDDLBAL
    {
        Task<List<TecLogos.SOP.WebModel.SOP.EmployeeDDL>> GetAll();
    }
    public class EmployeeDDLBAL : IEmployeeDDLBAL
    {
        private readonly IEmployeeDDLDAL _DAL;
        private readonly ILogger<EmployeeDDLBAL> _logger;

        public EmployeeDDLBAL(IEmployeeDDLDAL DAL, ILogger<EmployeeDDLBAL> logger)
        {
            _DAL = DAL;
            _logger = logger;
        }

        public async Task<List<TecLogos.SOP.WebModel.SOP.EmployeeDDL>> GetAll()
        {
            var dataList = await _DAL.GetAll();

            return dataList.Select(ddl =>
            new TecLogos.SOP.WebModel.SOP.EmployeeDDL
            {
                ID = ddl.ID,
                Email = ddl.Email,
                FirstName = ddl.FirstName,
                LastName = ddl.LastName
            }).ToList();
        }
    }
}
