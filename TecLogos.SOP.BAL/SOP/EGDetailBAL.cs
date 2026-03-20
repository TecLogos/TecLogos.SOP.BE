using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TecLogos.SOP.DAL.SOP;
using TecLogos.SOP.WebModel.SOP;


namespace TecLogos.SOP.BAL.SOP
{
    public interface IEGDetailBAL
    {
        Task<List<EGDetail>> GetAll();
        Task<EGDetail?> Get(Guid id);
        Task<Guid> Create(EGDetail model, Guid userId);
        Task<bool> Update(EGDetail model, Guid userId);
        Task<bool> Delete(Guid id, Guid userId);
    }

    public class EGDetailBAL : IEGDetailBAL
    {
        private readonly IEGDetailDAL _dal;

        public EGDetailBAL(IEGDetailDAL dal)
        {
            _dal = dal;
        }

        public async Task<List<EGDetail>> GetAll()
        {
            var data = await _dal.GetAll();

            return data.Select(MapToWebModel).ToList();
        }

        public async Task<EGDetail?> Get(Guid id)
        {
            var data = await _dal.GetById(id);
            return data == null ? null : MapToWebModel(data);
        }

        public async Task<Guid> Create(EGDetail model, Guid userId)
        {
            var dataModel = MapToDataModel(model);
            return await _dal.Create(dataModel, userId);
        }

        public async Task<bool> Update(EGDetail model, Guid userId)
        {
            var dataModel = MapToDataModel(model);
            return await _dal.Update(dataModel, userId);
        }

        public Task<bool> Delete(Guid id, Guid userId)
            => _dal.Delete(id, userId);

        // Mapping Methods

        private static EGDetail MapToWebModel(DataModel.SOP.EGDetail d) => new()
        {
            ID = d.ID,
            EmployeeGroupID = d.EmployeeGroupID,
            GroupName = d.GroupName,
            EmployeeID = d.EmployeeID,
           
            EmployeeName = d.EmployeeName,
            Email = d.Email
        };

        private static DataModel.SOP.EGDetail MapToDataModel(EGDetail w) => new()
        {
            ID = w.ID,
            EmployeeGroupID = w.EmployeeGroupID,
            EmployeeID = w.EmployeeID
        };
    }
}