using Microsoft.Extensions.Logging;
using TecLogos.SOP.DAL.SOP;
using Web = TecLogos.SOP.WebModel.SOP;
using Data = TecLogos.SOP.DataModel.SOP;

namespace TecLogos.SOP.BAL.SOP
{
    public interface IEmployeeGroupBAL
    {
        Task<List<Web.EmployeeGroup>> GetAll();
        Task<Web.EmployeeGroup?> Get(Guid id);
        Task<Guid> Create(Web.EmployeeGroup lag, Guid u);
        Task<bool> Update(Web.EmployeeGroup lag, Guid u);
        Task<bool> Delete(Guid id, Guid u);
    }

    public class EmployeeGroupBAL : IEmployeeGroupBAL
    {
        private readonly IEmployeeGroupDAL _dal;
        private readonly ILogger<EmployeeGroupBAL> _logger;

        public EmployeeGroupBAL(
            IEmployeeGroupDAL dal,
            ILogger<EmployeeGroupBAL> logger)
        {
            _dal = dal;
            _logger = logger;
        }

        // GET ALL
        public async Task<List<Web.EmployeeGroup>> GetAll()
        {
            var dataList = await _dal.GetAll();

            return dataList.Select(MapToWeb).ToList();
        }

        // GET BY ID
        public async Task<Web.EmployeeGroup?> Get(Guid id)
        {
            var data = await _dal.GetById(id);
            if (data == null) return null;

            return MapToWeb(data);
        }

        // CREATE
        public async Task<Guid> Create(Web.EmployeeGroup model, Guid userId)
        {
            var data = MapToData(model);
            return await _dal.Create(data, userId);
        }

        // UPDATE
        public async Task<bool> Update(Web.EmployeeGroup model, Guid userId)
        {
            var data = MapToData(model);
            return await _dal.Update(data, userId);
        }

        // DELETE (no mapping needed)
        public Task<bool> Delete(Guid id, Guid userId)
            => _dal.Delete(id, userId);

        // MAPPING FUNCTIONS

        private static Web.EmployeeGroup MapToWeb(Data.EmployeeGroup d)
        {
            return new Web.EmployeeGroup
            {
                ID = d.ID,
                Name = d.Name,
            };
        }

        private static Data.EmployeeGroup MapToData(Web.EmployeeGroup w)
        {
            return new Data.EmployeeGroup
            {
                ID = w.ID,
                Name = w.Name
            };
        }
    }
}
