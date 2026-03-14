using TecLogos.SOP.DAL.SOP;

namespace TecLogos.SOP.BAL.SOP
{
    public interface IRoleBAL
    {
        Task<List<WebModel.SOP.Role>> GetAll();
        Task<WebModel.SOP.Role?> Get(Guid id);
        Task<Guid> Create(WebModel.SOP.Role r, Guid userId);
        Task<bool> Update(WebModel.SOP.Role r, Guid userId);
        Task<bool> Delete(Guid id, Guid userId);
    }

    public class RoleBAL : IRoleBAL
    {
        private readonly IRoleDAL _dal;

        public RoleBAL(IRoleDAL dal) => _dal = dal;

        public async Task<List<WebModel.SOP.Role>> GetAll()
        {
            var data = await _dal.GetAll();
            return data.Select(MapToWeb).ToList();
        }

        public async Task<WebModel.SOP.Role?> Get(Guid id)
        {
            var d = await _dal.GetById(id);
            return d == null ? null : MapToWeb(d);
        }

        public async Task<Guid> Create(WebModel.SOP.Role r, Guid userId)
            => await _dal.Create(MapToData(r), userId);

        public async Task<bool> Update(WebModel.SOP.Role r, Guid userId)
            => await _dal.Update(MapToData(r), userId);

        public Task<bool> Delete(Guid id, Guid userId) => _dal.Delete(id, userId);

        private static WebModel.SOP.Role MapToWeb(DataModel.SOP.Role d) => new()
        {
            ID = d.ID,
            Name = d.Name,
            Description = d.Description,
            Created = d.Created,
            CreatedByID = d.CreatedByID,
            Modified = d.Modified,
            ModifiedByID = d.ModifiedByID,
            Version = d.Version,
            IsActive = d.IsActive,
            IsDeleted = d.IsDeleted
        };

        private static DataModel.SOP.Role MapToData(WebModel.SOP.Role w) => new()
        {
            ID = w.ID,
            Name = w.Name,
            Description = w.Description
        };
    }
}

