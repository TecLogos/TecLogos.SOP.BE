using Microsoft.Extensions.Logging;
using TecLogos.SOP.DAL.SOP;
using TecLogos.SOP.WebModel.SOP;

namespace TecLogos.SOP.BAL.SOP
{
    public interface IEmployeeDDLBAL
    {
        Task<List<EmployeeDDL>> GetAll();
    }

    public class EmployeeDDLBAL : IEmployeeDDLBAL
    {
        private readonly IEmployeeDDLDAL _dal;
        private readonly ILogger<EmployeeDDLBAL> _logger;

        public EmployeeDDLBAL(IEmployeeDDLDAL dal, ILogger<EmployeeDDLBAL> logger)
        {
            _dal = dal;
            _logger = logger;
        }

        public async Task<List<EmployeeDDL>> GetAll()
        {
            var dataList = await _dal.GetAll();
            return dataList.Select(d => new EmployeeDDL
            {
                ID = d.ID,
                Email = d.Email
            }).ToList();
        }
    }
}
