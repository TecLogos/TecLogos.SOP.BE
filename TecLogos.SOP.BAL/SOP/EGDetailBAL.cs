using Microsoft.Extensions.Logging;
using TecLogos.SOP.DAL.SOP;

namespace TecLogos.SOP.BAL.SOP
{
    public interface IEGDetailBAL
    {
        Task<List<WebModel.SOP.EGDetail>> GetAll();
        Task<WebModel.SOP.EGDetail?> Get(Guid id);
        Task<Guid> Create(WebModel.SOP.EGDetail model, Guid userId);
        Task<bool> Update(WebModel.SOP.EGDetail model, Guid userId);
        Task<bool> Delete(Guid id, Guid userId);
    }

    public class EGDetailBAL : IEGDetailBAL
    {
        private readonly IEGDetailDAL _dal;

        public EGDetailBAL(IEGDetailDAL dal)
        {
            _dal = dal;
        }

        public async Task<List<WebModel.SOP.EGDetail>> GetAll()
        {
            var data = await _dal.GetAll();
            return data.Select(MapToWebModel).ToList();
        }

        public async Task<WebModel.SOP.EGDetail?> Get(Guid id)
        {
            var data = await _dal.GetById(id);
            return data == null ? null : MapToWebModel(data);
        }

        public async Task<Guid> Create(WebModel.SOP.EGDetail model, Guid userId)
        {
            var dm = MapToDataModel(model);
            return await _dal.Create(dm, userId);
        }

        public async Task<bool> Update(WebModel.SOP.EGDetail model, Guid userId)
        {
            var dm = MapToDataModel(model);
            return await _dal.Update(dm, userId);
        }

        public Task<bool> Delete(Guid id, Guid userId) => _dal.Delete(id, userId);

        private static WebModel.SOP.EGDetail MapToWebModel(DataModel.SOP.EGDetail d) => new()
        {
            ID = d.ID,
            EmployeeGroupID = d.EmployeeGroupID,
            GroupName = d.GroupName,
            EmployeeID = d.EmployeeID,
            EmployeeName = d.EmployeeName,
            Email = d.Email
        };

        private static DataModel.SOP.EGDetail MapToDataModel(WebModel.SOP.EGDetail w) => new()
        {
            ID = w.ID,
            EmployeeGroupID = w.EmployeeGroupID,
            EmployeeID = w.EmployeeID
        };
    }
}
