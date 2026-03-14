using Microsoft.Extensions.Logging;
using TecLogos.SOP.DAL.SOP;

namespace TecLogos.SOP.BAL.SOP
{
    public interface IEmployeeGroupBAL
    {
        Task<List<WebModel.SOP.EmployeeGroup>> GetAll();
        Task<WebModel.SOP.EmployeeGroup?> Get(Guid id);
        Task<Guid> Create(WebModel.SOP.EmployeeGroup model, Guid userId);
        Task<bool> Update(WebModel.SOP.EmployeeGroup model, Guid userId);
        Task<bool> Delete(Guid id, Guid userId);
    }

    public class EmployeeGroupBAL : IEmployeeGroupBAL
    {
        private readonly IEmployeeGroupDAL _dal;
        private readonly ILogger<EmployeeGroupBAL> _logger;

        public EmployeeGroupBAL(IEmployeeGroupDAL dal, ILogger<EmployeeGroupBAL> logger)
        {
            _dal = dal;
            _logger = logger;
        }

        public async Task<List<WebModel.SOP.EmployeeGroup>> GetAll()
        {
            var data = await _dal.GetAll();
            return data.Select(MapToWeb).ToList();
        }

        public async Task<WebModel.SOP.EmployeeGroup?> Get(Guid id)
        {
            var data = await _dal.GetById(id);
            return data == null ? null : MapToWeb(data);
        }

        public async Task<Guid> Create(WebModel.SOP.EmployeeGroup model, Guid userId)
        {
            return await _dal.Create(MapToData(model), userId);
        }

        public async Task<bool> Update(WebModel.SOP.EmployeeGroup model, Guid userId)
        {
            return await _dal.Update(MapToData(model), userId);
        }

        public Task<bool> Delete(Guid id, Guid userId) => _dal.Delete(id, userId);

        private static WebModel.SOP.EmployeeGroup MapToWeb(DataModel.SOP.EmployeeGroup d) => new()
        {
            ID = d.ID,
            Name = d.Name
        };

        private static DataModel.SOP.EmployeeGroup MapToData(WebModel.SOP.EmployeeGroup w) => new()
        {
            ID = w.ID,
            Name = w.Name
        };
    }
}
